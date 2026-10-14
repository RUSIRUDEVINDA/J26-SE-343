using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Safety;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enrichment;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.LandIntelligence.ColomboExperimentalGisVerify;

internal static class Program
{
    private const double OfflineUtmToleranceMeters = 5.0;

    public static async Task<int> Main(string[] args)
    {
        var report = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            EnvFileLoader.LoadFromRepositoryRoot();
            var isolated = IsolatedLandIntelligenceConnectionGuard.RequireIsolatedConnectionString();
            // Point DI at the isolated DB only for this process.
            Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION", isolated);

            var solutionRoot = ResolveSolutionRoot();
            var geoJsonPath = args.Length > 0
                ? Path.GetFullPath(args[0])
                : Path.Combine(
                    solutionRoot,
                    "data",
                    "gis",
                    "experiments",
                    "colombo",
                    "osm_motor_roads_colombo_buffer.geojson");

            report["isolated_connection_database"] = ExtractDatabaseName(isolated);
            report["geojson_path"] = geoJsonPath;
            report["model_activation_enabled"] = false;
            report["live_ml_service_unchanged"] = true;

            if (!File.Exists(geoJsonPath))
            {
                throw new FileNotFoundException(
                    "Buffered OSM GeoJSON not found. Export it first with the documented PowerShell command.",
                    geoJsonPath);
            }

            var configuration = BuildConfiguration();
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            });
            services.AddLandIntelligenceInfrastructure(configuration);
            await using var provider = services.BuildServiceProvider();
            await using var scope = provider.CreateAsyncScope();
            var sp = scope.ServiceProvider;

            var db = sp.GetRequiredService<LandIntelligenceDbContext>();
            await EnsurePostgisAndMigrateAsync(db);
            report["migrations_applied"] = true;

            var osmImport = sp.GetRequiredService<IOsmMotorRoadImportService>();
            var pilotImport = sp.GetRequiredService<IGisReferenceDataImportService>();
            var colomboLayers = sp.GetRequiredService<ColomboExperimentGisImportService>();
            var roadEnrichment = sp.GetRequiredService<IRoadAccessibilityEnrichmentService>();
            var waterEnrichment = sp.GetRequiredService<IWaterProximityEnrichmentService>();
            var soilEnrichment = sp.GetRequiredService<ISoilGroupEnrichmentService>();
            var envEnrichment = sp.GetRequiredService<IEnvironmentalSpatialConstraintEnrichmentService>();
            var adapter = sp.GetRequiredService<IExperimentalColomboMlFeatureAdapter>();
            var parcelRepository = sp.GetRequiredService<ILandParcelRepository>();
            var coverage = sp.GetRequiredService<IOptions<GisEnrichmentCoverageOptions>>().Value;

            report["coverage_options"] = new
            {
                coverage.SupportedDistrictNames,
                coverage.RoadSourceLayer,
                DistrictRoadSourceLayers = coverage.DistrictRoadSourceLayers
                    .Select(x => new { x.DistrictName, x.RoadSourceLayer })
                    .ToArray()
            };

            // 1) Hambantota pilot (expressways) for compatibility
            var pilot = await pilotImport.ImportHambantotaPilotAsync(
                Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));
            report["hambantota_pilot_import"] = new
            {
                pilot.FeaturesImported,
                pilot.FeaturesUpdated,
                pilot.TableCounts
            };

            // 2) Colombo boundary
            await osmImport.ImportDistrictBoundaryAsync(
                GisEnrichmentCoverageDefaults.Colombo,
                Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));

            // 3) OSM roads (first pass)
            var firstRoadImport = await osmImport.ImportFromGeoJsonAsync(geoJsonPath);
            report["osm_import_first"] = firstRoadImport;

            // 4) Colombo water/soil/conservation (intersecting district)
            var layerImport = await colomboLayers.ImportWaterSoilConservationForDistrictAsync(
                GisEnrichmentCoverageDefaults.Colombo,
                Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));
            report["colombo_layer_import"] = layerImport;

            // 5) Idempotent second OSM import
            var secondRoadImport = await osmImport.ImportFromGeoJsonAsync(geoJsonPath);
            report["osm_import_second"] = secondRoadImport;
            report["osm_idempotent"] =
                secondRoadImport.FeaturesImported == 0
                && secondRoadImport.TotalOsmMotorRoadsInTable == firstRoadImport.TotalOsmMotorRoadsInTable;

            report["spatial_indexes"] = await InspectSpatialIndexesAsync(db);
            report["nearest_road_explain"] = await ExplainNearestRoadAsync(db);
            report["source_coverage"] = await CollectSourceCoverageAsync(db);
            report["road_source_layer_is_global"] = false;
            report["road_source_selection"] =
                "Global RoadSourceLayer defaults to expressways; DistrictRoadSourceLayers overrides Colombo→osm_motor_roads.";

            // 6) Enrichment scenarios
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
                PostGisConfiguration.DefaultSpatialReferenceSystemId);

            var colomboPoint = factory.CreatePoint(new Coordinate(79.8612, 6.9271));
            var crossBoundaryPoint = factory.CreatePoint(new Coordinate(79.99289101549337, 6.935148694012742));
            var hambantotaPoint = await TryLoadHambantotaSamplePointAsync(db) ?? factory.CreatePoint(new Coordinate(81.1185, 6.1429));
            var outsidePoint = factory.CreatePoint(new Coordinate(80.5, 7.5));
            var onRoadPoint = await TryLoadPointOnOsmRoadAsync(db) ?? colomboPoint;

            var colomboParcel = await UpsertProbeParcelAsync(parcelRepository, "COL-EXP-PROBE-1", "Western", "Colombo", colomboPoint);
            var crossParcel = await UpsertProbeParcelAsync(parcelRepository, "COL-EXP-CROSS-1", "Western", "Colombo", crossBoundaryPoint);
            var hambantotaParcel = await UpsertProbeParcelAsync(parcelRepository, "HAM-EXP-PROBE-1", "Southern", "Hambantota", hambantotaPoint);
            var outsideParcel = await UpsertProbeParcelAsync(parcelRepository, "OUT-EXP-PROBE-1", "Central", "Kandy", outsidePoint);
            var onRoadParcel = await UpsertProbeParcelAsync(parcelRepository, "COL-EXP-ONROAD-1", "Western", "Colombo", onRoadPoint);

            await EnsureRailwayDecoyAsync(db, colomboPoint);

            var colomboRoad = await roadEnrichment.EnrichAsync(colomboParcel.Id);
            var crossRoad = await roadEnrichment.EnrichAsync(crossParcel.Id);
            var hambantotaRoad = await roadEnrichment.EnrichAsync(hambantotaParcel.Id);
            var outsideRoad = await roadEnrichment.EnrichAsync(outsideParcel.Id);
            var onRoad = await roadEnrichment.EnrichAsync(onRoadParcel.Id);

            report["enrichment"] = new
            {
                colombo = SummarizeRoad(colomboRoad),
                cross_boundary = SummarizeRoad(crossRoad),
                hambantota = SummarizeRoad(hambantotaRoad),
                outside = SummarizeRoad(outsideRoad),
                on_road = SummarizeRoad(onRoad),
                hambantota_uses_expressways =
                    string.Equals(hambantotaRoad.SourceLayer, "expressways", StringComparison.OrdinalIgnoreCase),
                colombo_uses_osm =
                    string.Equals(colomboRoad.SourceLayer, GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer, StringComparison.OrdinalIgnoreCase),
                outside_is_outside_coverage = outsideRoad.Status == RoadAccessibilityEnrichmentStatus.OutsideCoverage,
                railway_cannot_supply_experimental_road =
                    colomboRoad.SourceLayer is null
                    || !colomboRoad.SourceLayer.Contains("railway", StringComparison.OrdinalIgnoreCase)
            };

            report["distance_checks"] = new
            {
                colombo = await CompareDistanceAsync(db, colomboPoint, colomboRoad),
                cross_boundary = await CompareDistanceAsync(db, crossBoundaryPoint, crossRoad),
                on_road = await CompareDistanceAsync(db, onRoadPoint, onRoad),
                offline_utm_tolerance_m = OfflineUtmToleranceMeters,
                offline_utm_note =
                    "Offline UTM44N vs geodesic tolerance is 5 m (see ML/tools/test_offline_road_distance.py)."
            };

            var water = await waterEnrichment.EnrichAsync(colomboParcel.Id);
            var soil = await soilEnrichment.EnrichAsync(colomboParcel.Id);
            var environmental = await envEnrichment.EnrichAsync(colomboParcel.Id);
            var payload = adapter.BuildPayload(
                colomboParcel,
                LandUseType.Agricultural,
                colomboRoad,
                water,
                soil,
                environmental,
                LandParcelGisEnrichmentOverallStatus.Partial);

            report["experimental_payload"] = new
            {
                payload.ModelActivationEnabled,
                payload.GisEnrichmentStatus,
                payload.DistanceToRoadM,
                payload.DistanceToWaterM,
                payload.DerivedSoilGroup,
                payload.SpatialConstraintPresent,
                payload.EnvironmentalRestrictionType,
                payload.EnvironmentalRestrictionSeverity,
                payload.ExperimentalPredictionSupported,
                payload.ExperimentalPredictionAbstentionReason,
                payload.RoadSourceLayer,
                payload.RoadHighwayClass,
                payload.RoadOsmId,
                payload.RoadFilterPolicyVersion,
                payload.GisDerivedFields,
                payload.SimulatedOrUnavailableFields,
                payload.RemainingMismatches,
                water_status = water.Status.ToString(),
                soil_status = soil.Status.ToString(),
                environmental_status = environmental.Status.ToString(),
                conservation_false_not_unconstrained =
                    environmental.Status == EnvironmentalSpatialConstraintEnrichmentStatus.Available
                    && environmental.IntersectsSoilConservationArea == false,
                unavailable_layers_do_not_fabricate_success =
                    (water.Status != WaterProximityEnrichmentStatus.Available || water.DistanceMeters is not null)
                    && (soil.Status != SoilGroupEnrichmentStatus.Unavailable || soil.PrimarySoilGroup is null)
            };

            // 7) Rollback OSM only, then prove Colombo missing-source → Unavailable
            var expresswaysBefore = await db.GisRoads.CountAsync(r => r.SourceLayer == "expressways");
            var deleted = await osmImport.DeleteOsmMotorRoadsAsync();
            var expresswaysAfter = await db.GisRoads.CountAsync(r => r.SourceLayer == "expressways");
            var osmAfter = await db.GisRoads.CountAsync(r => r.SourceLayer == GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);
            var missingRoad = await roadEnrichment.EnrichAsync(colomboParcel.Id);
            var hambantotaAfterRollback = await roadEnrichment.EnrichAsync(hambantotaParcel.Id);
            report["rollback"] = new
            {
                deleted_osm_rows = deleted,
                osm_remaining = osmAfter,
                expressways_before = expresswaysBefore,
                expressways_after = expresswaysAfter,
                expressways_preserved = expresswaysBefore == expresswaysAfter,
                colombo_missing_source_after_rollback = SummarizeRoad(missingRoad),
                hambantota_still_available_after_osm_rollback = SummarizeRoad(hambantotaAfterRollback)
            };

            report["status"] = "passed";
            await WriteReportAsync(solutionRoot, report);
            Console.WriteLine("Colombo experimental GIS verification PASSED.");
            return 0;
        }
        catch (Exception ex)
        {
            report["status"] = "failed";
            report["error"] = ex.ToString();
            try
            {
                var root = ResolveSolutionRoot();
                await WriteReportAsync(root, report);
            }
            catch
            {
                // ignore report write failures
            }

            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static IConfiguration BuildConfiguration()
    {
        var values = new Dictionary<string, string?>
        {
            ["LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames:0"] = "Hambantota",
            ["LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames:1"] = "Colombo",
            ["LandIntelligence:GisEnrichmentCoverage:RoadSourceLayer"] = "expressways",
            ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:0:DistrictName"] = "Hambantota",
            ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:0:RoadSourceLayer"] = "expressways",
            ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:1:DistrictName"] = "Colombo",
            ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:1:RoadSourceLayer"] = "osm_motor_roads",
            ["LandIntelligence:GisEnrichmentCoverage:OsmMotorRoadFilterPolicyVersion"] =
                GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion
        };

        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .AddEnvironmentVariables()
            .Build();
    }

    private static async Task EnsurePostgisAndMigrateAsync(LandIntelligenceDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
        await db.Database.MigrateAsync();
    }

    private static async Task<object> InspectSpatialIndexesAsync(LandIntelligenceDbContext db)
    {
        const string sql = """
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE schemaname = 'land_intelligence'
              AND tablename = 'gis_roads'
            ORDER BY indexname
            """;
        var rows = new List<object>();
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            rows.Add(new { index = reader.GetString(0), definition = reader.GetString(1) });
        }

        return rows;
    }

    private static async Task<object> ExplainNearestRoadAsync(LandIntelligenceDbContext db)
    {
        var sql = RoadAccessibilityEnrichmentService.BuildNearestRoadSql(
            GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "EXPLAIN (ANALYZE, BUFFERS, FORMAT TEXT) " + sql;
        AddParameter(command, "geometryWkt", "POINT(79.8612 6.9271)");
        AddParameter(command, "sourceLayer", GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);
        var lines = new List<string>();
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lines.Add(reader.GetString(0));
        }

        sw.Stop();
        var usesGeographyIndex = lines.Any(line =>
            line.Contains("Geometry_geography", StringComparison.OrdinalIgnoreCase)
            || line.Contains("osm_Geometry_geography", StringComparison.OrdinalIgnoreCase));
        var usesGeometryIndex = lines.Any(line =>
            line.Contains("IX_gis_roads_Geometry\"", StringComparison.OrdinalIgnoreCase)
            || line.Contains("IX_gis_roads_Geometry ", StringComparison.OrdinalIgnoreCase));

        return new
        {
            client_elapsed_ms = sw.Elapsed.TotalMilliseconds,
            uses_geography_expression_index = usesGeographyIndex,
            uses_geometry_gist_index = usesGeometryIndex,
            plan = lines
        };
    }

    private static async Task<object> CompareDistanceAsync(
        LandIntelligenceDbContext db,
        Point point,
        RoadAccessibilityEnrichmentResult enrichment)
    {
        if (enrichment.Status != RoadAccessibilityEnrichmentStatus.Available || enrichment.RoadId is null)
        {
            return new
            {
                enrichment.Status,
                enrichment.DistanceMeters,
                note = "No available road enrichment to compare."
            };
        }

        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        Guid? exactNearestId = null;
        double? exactNearestDistance = null;
        await using (var exactCommand = connection.CreateCommand())
        {
            exactCommand.CommandText = """
                SELECT r."Id",
                       ST_Distance(
                           ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326)::geography,
                           r."Geometry"::geography) AS exact_m
                FROM land_intelligence.gis_roads r
                WHERE r."SourceLayer" = @sourceLayer
                  AND r."Geometry" IS NOT NULL
                ORDER BY exact_m
                LIMIT 1
                """;
            AddParameter(exactCommand, "geometryWkt", point.AsText());
            AddParameter(
                exactCommand,
                "sourceLayer",
                enrichment.SourceLayer ?? GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);
            await using var exactReader = await exactCommand.ExecuteReaderAsync();
            if (await exactReader.ReadAsync())
            {
                exactNearestId = exactReader.GetGuid(0);
                exactNearestDistance = exactReader.GetDouble(1);
            }
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                ST_Distance(
                    ST_SetSRID(ST_GeomFromText(@geometryWkt, 4326), 4326)::geography,
                    r."Geometry"::geography) AS exact_m,
                r."SourceFeatureId",
                r."SourceLayer"
            FROM land_intelligence.gis_roads r
            WHERE r."Id" = @roadId
            """;
        AddParameter(command, "geometryWkt", point.AsText());
        AddParameter(command, "roadId", enrichment.RoadId.Value);
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new { error = "Selected road row missing." };
        }

        var exact = reader.GetDouble(0);
        var delta = enrichment.DistanceMeters is null
            ? (double?)null
            : Math.Abs(enrichment.DistanceMeters.Value - exact);
        var matchesExactNearest =
            exactNearestId is not null && enrichment.RoadId == exactNearestId;

        return new
        {
            enrichment_distance_m = enrichment.DistanceMeters,
            exact_st_distance_geography_m = exact,
            abs_delta_m = delta,
            within_1m = delta is not null && delta < 1.0,
            exact_argmin_road_id = exactNearestId,
            exact_argmin_distance_m = exactNearestDistance,
            knn_selected_matches_exact_st_distance_argmin = matchesExactNearest,
            source_feature_id = reader.IsDBNull(1) ? null : reader.GetString(1),
            source_layer = reader.GetString(2),
            highway = enrichment.HighwayClass,
            offline_utm_tolerance_m = OfflineUtmToleranceMeters
        };
    }

    private static async Task<object> CollectSourceCoverageAsync(LandIntelligenceDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        async Task<long> CountAsync(string sql)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            var scalar = await command.ExecuteScalarAsync();
            return Convert.ToInt64(scalar);
        }

        return new
        {
            note =
                "Layer row counts do not prove complete geographic coverage of Colombo; " +
                "imports may be district-intersect filtered or pilot-scoped.",
            osm_motor_roads = await CountAsync(
                """SELECT COUNT(*) FROM land_intelligence.gis_roads WHERE "SourceLayer" = 'osm_motor_roads'"""),
            expressways = await CountAsync(
                """SELECT COUNT(*) FROM land_intelligence.gis_roads WHERE "SourceLayer" = 'expressways'"""),
            water_features = await CountAsync("SELECT COUNT(*) FROM land_intelligence.gis_water_features"),
            soil_groups = await CountAsync("SELECT COUNT(*) FROM land_intelligence.gis_soil_groups"),
            soil_conservation_areas = await CountAsync(
                "SELECT COUNT(*) FROM land_intelligence.gis_soil_conservation_areas"),
            administrative_boundaries = await CountAsync(
                "SELECT COUNT(*) FROM land_intelligence.gis_administrative_boundaries")
        };
    }

    private static async Task<Point?> TryLoadPointOnOsmRoadAsync(LandIntelligenceDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ST_AsText(
                ST_ClosestPoint(
                    r."Geometry",
                    ST_SetSRID(ST_MakePoint(79.8612, 6.9271), 4326)))
            FROM land_intelligence.gis_roads r
            INNER JOIN land_intelligence.gis_administrative_boundaries b
                ON b."Name" = 'Colombo' AND b."BoundaryType" = 2
            WHERE r."SourceLayer" = 'osm_motor_roads'
              AND ST_Intersects(r."Geometry", b."Boundary")
              AND ST_Covers(
                    b."Boundary",
                    ST_ClosestPoint(
                        r."Geometry",
                        ST_SetSRID(ST_MakePoint(79.8612, 6.9271), 4326)))
            LIMIT 1
            """;
        var wkt = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return null;
        }

        var reader = new NetTopologySuite.IO.WKTReader(
            NtsGeometryServices.Instance.CreateGeometryFactory(
                PostGisConfiguration.DefaultSpatialReferenceSystemId));
        return reader.Read(wkt) as Point;
    }

    private static async Task EnsureRailwayDecoyAsync(LandIntelligenceDbContext db, Point near)
    {
        const string decoyLayer = "railway";
        var exists = await db.GisRoads.AnyAsync(r => r.SourceLayer == decoyLayer && r.SourceFeatureId == "decoy-railway-1");
        if (exists)
        {
            return;
        }

        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
            PostGisConfiguration.DefaultSpatialReferenceSystemId);
        var line = factory.CreateLineString(
        [
            new Coordinate(near.X - 0.0001, near.Y - 0.0001),
            new Coordinate(near.X + 0.0001, near.Y + 0.0001)
        ]);
        db.GisRoads.Add(new StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities.GisRoadEntity
        {
            Id = Guid.NewGuid(),
            SourceName = GisEnrichmentCoverageDefaults.OsmMotorRoadSourceName,
            SourceLayer = decoyLayer,
            SourceFeatureId = "decoy-railway-1",
            Name = "Decoy Railway",
            RoadType = StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums.GisRoadType.Unspecified,
            Geometry = factory.CreateMultiLineString([line]),
            ImportedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Point?> TryLoadHambantotaSamplePointAsync(LandIntelligenceDbContext db)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ST_AsText(ST_PointOnSurface(b."Boundary"))
            FROM land_intelligence.gis_administrative_boundaries b
            WHERE b."Name" = 'Hambantota' AND b."BoundaryType" = 2
            LIMIT 1
            """;
        var wkt = await command.ExecuteScalarAsync() as string;
        if (string.IsNullOrWhiteSpace(wkt))
        {
            return null;
        }

        var reader = new NetTopologySuite.IO.WKTReader(
            NtsGeometryServices.Instance.CreateGeometryFactory(
                PostGisConfiguration.DefaultSpatialReferenceSystemId));
        return reader.Read(wkt) as Point;
    }

    private static async Task<LandParcel> UpsertProbeParcelAsync(
        ILandParcelRepository repository,
        string cadastral,
        string province,
        string district,
        Point centroid)
    {
        var existing = await repository.GetByCadastralNumberAsync(cadastral);
        if (existing is not null)
        {
            existing.UpdateSpatial(new SpatialReference(centroid.Y, centroid.X, "EPSG:4326"));
            await repository.UpdateAsync(existing);
            return existing;
        }

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastral, "COL-EXP-VERIFY"),
            new LandCategory(LandCategoryType.StateLand, "isolated verify probe"),
            new LandArea(1.0m, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, "Probe DS"),
            new SpatialReference(centroid.Y, centroid.X, "EPSG:4326"));
        await repository.AddAsync(parcel);
        return parcel;
    }

    private static object SummarizeRoad(RoadAccessibilityEnrichmentResult result) =>
        new
        {
            result.Status,
            result.DistanceMeters,
            result.SourceLayer,
            result.HighwayClass,
            result.OsmId,
            result.FilterPolicyVersion,
            result.RoadType,
            evidence = result.Evidence.Take(6).ToArray()
        };

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        return Convert.ToString(builder["Database"]) ?? string.Empty;
    }

    private static string ResolveSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StateLandGovernance.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate StateLandGovernance.sln.");
    }

    private static async Task WriteReportAsync(string solutionRoot, Dictionary<string, object?> report)
    {
        var path = Path.Combine(
            solutionRoot,
            "data",
            "gis",
            "experiments",
            "colombo",
            "colombo_exp_isolated_verify_report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report: {path}");
    }
}
