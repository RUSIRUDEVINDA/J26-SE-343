using System.Data;
using System.Data.Common;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Commands;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DependencyInjection;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.Safety;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.LandIntelligence.Presentation.DependencyInjection;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.LandIntelligence.ColomboExperimentalMlE2EVerify;

/// <summary>
/// Opt-in Step-3 E2E: PostGIS persistence, road-buffer samples, experimental ML recommendations.
/// Requires COLUMBO_EXP_ISOLATED_CONNECTION. Does not touch state_land_governance or ml_service.py.
/// </summary>
internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var report = new Dictionary<string, object?>(StringComparer.Ordinal);
        var failures = new List<string>();
        try
        {
            EnvFileLoader.LoadFromRepositoryRoot();
            // Avoid Neo4j retries during disposable E2E (graph sync is out of scope).
            Environment.SetEnvironmentVariable("NEO4J_CONNECTION", null);
            Environment.SetEnvironmentVariable("NEO4J_USERNAME", null);
            Environment.SetEnvironmentVariable("NEO4J_PASSWORD", null);

            var isolated = IsolatedLandIntelligenceConnectionGuard.RequireIsolatedConnectionString();
            Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION", isolated);

            var enableExperimental = !args.Contains("--disabled-experimental", StringComparer.OrdinalIgnoreCase);
            var solutionRoot = ResolveSolutionRoot();
            var geoJsonPath = args.FirstOrDefault(a => !a.StartsWith('-')) is { } path
                ? Path.GetFullPath(path)
                : Path.Combine(
                    solutionRoot,
                    "data",
                    "gis",
                    "experiments",
                    "colombo",
                    "osm_motor_roads_colombo_buffer.geojson");

            report["isolated_connection_database"] = ExtractDatabaseName(isolated);
            report["experimental_enabled_for_run"] = enableExperimental;
            report["model_activation_enabled_default"] = false;
            report["live_ml_service_unchanged"] = true;
            report["geojson_path"] = geoJsonPath;

            if (!File.Exists(geoJsonPath))
            {
                throw new FileNotFoundException("Buffered OSM GeoJSON not found.", geoJsonPath);
            }

            var configuration = BuildConfiguration(enableExperimental);
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Warning);
                builder.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
            });
            services.AddLandIntelligenceInfrastructure(configuration);
            services.AddLandIntelligenceApplication();
            // Graph sync may be unconfigured — already handled by infrastructure.
            await using var provider = services.BuildServiceProvider();

            await using (var migrateScope = provider.CreateAsyncScope())
            {
                var db = migrateScope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
                await EnsurePostgisAndMigrateAsync(db);
                report["migrations_applied"] = true;

                var roadCount = await db.GisRoads
                    .CountAsync(r => r.SourceLayer == GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer);
                report["osm_motor_roads_count_before_seed"] = roadCount;
                if (roadCount == 0)
                {
                    await SeedGisBaselineAsync(migrateScope.ServiceProvider, solutionRoot, geoJsonPath, report);
                }
            }

            // --- Persistence ---
            report["persistence"] = await RunPersistenceChecksAsync(provider, failures);

            // --- Road buffer empirical samples ---
            await using (var roadScope = provider.CreateAsyncScope())
            {
                var db = roadScope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
                report["road_buffer"] = await RunRoadBufferChecksAsync(db, failures);
            }

            // --- Experimental ML health ---
            await using (var mlScope = provider.CreateAsyncScope())
            {
                var client = mlScope.ServiceProvider.GetRequiredService<IExperimentalColomboMlClient>();
                var options = mlScope.ServiceProvider.GetRequiredService<IOptions<ExperimentalColomboMlOptions>>().Value;
                var healthy = enableExperimental && await client.IsHealthyAsync();
                report["experimental_ml_health"] = new
                {
                    options.Enabled,
                    options.ServiceBaseUrl,
                    options.ExpectedCandidateId,
                    healthy
                };
                if (enableExperimental && !healthy)
                {
                    failures.Add(
                        "Experimental ML service not healthy on "
                        + options.ServiceBaseUrl
                        + " (start experimental_ml_service.py).");
                }
            }

            // --- Recommendation scenarios ---
            report["recommendations"] = await RunRecommendationScenariosAsync(
                provider,
                enableExperimental,
                failures);

            // --- HTTP contract sample (Land Intelligence controllers only) ---
            report["http_sample"] = await RunHttpContractSampleAsync(
                configuration,
                solutionRoot,
                failures);

            // --- Default-off smoke (in-process config override) ---
            report["default_off_smoke"] = await RunDisabledExperimentalSmokeAsync(solutionRoot, geoJsonPath);

            report["failures"] = failures;
            report["passed"] = failures.Count == 0;
            await WriteReportAsync(solutionRoot, report);

            if (failures.Count > 0)
            {
                Console.Error.WriteLine("FAILURES:");
                foreach (var f in failures)
                {
                    Console.Error.WriteLine(" - " + f);
                }

                return 1;
            }

            Console.WriteLine("Step-3 E2E verification passed.");
            return 0;
        }
        catch (Exception ex)
        {
            report["error"] = ex.ToString();
            report["failures"] = failures;
            try
            {
                var root = ResolveSolutionRoot();
                await WriteReportAsync(root, report);
            }
            catch
            {
                // ignore
            }

            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static async Task SeedGisBaselineAsync(
        IServiceProvider sp,
        string solutionRoot,
        string geoJsonPath,
        Dictionary<string, object?> report)
    {
        var osmImport = sp.GetRequiredService<IOsmMotorRoadImportService>();
        var pilotImport = sp.GetRequiredService<IGisReferenceDataImportService>();
        var colomboLayers = sp.GetRequiredService<ColomboExperimentGisImportService>();

        var pilot = await pilotImport.ImportHambantotaPilotAsync(
            Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));
        report["hambantota_pilot_import"] = new { pilot.FeaturesImported, pilot.TableCounts };

        await osmImport.ImportDistrictBoundaryAsync(
            GisEnrichmentCoverageDefaults.Colombo,
            Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));

        var roads = await osmImport.ImportFromGeoJsonAsync(geoJsonPath);
        report["osm_import"] = roads;

        var layers = await colomboLayers.ImportWaterSoilConservationForDistrictAsync(
            GisEnrichmentCoverageDefaults.Colombo,
            Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS"));
        report["colombo_layer_import"] = layers;
    }

    private static async Task<object> RunPersistenceChecksAsync(
        ServiceProvider provider,
        List<string> failures)
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
            PostGisConfiguration.DefaultSpatialReferenceSystemId);
        var colomboA = factory.CreatePoint(new Coordinate(79.8612, 6.9271));
        var colomboB = factory.CreatePoint(new Coordinate(79.88, 6.94));
        Guid parcelId;
        Guid otherParcelId;
        LandParcelGisEnrichmentOverallStatus otherStatusBeforeSiblingUpdate = default;
        object? before;
        object? afterInvalidate;
        object? afterReEnrich;
        object? otherIntact;
        object? partialCoord;
        object? invalidCoord;

        await using (var scope = provider.CreateAsyncScope())
        {
            var parcels = scope.ServiceProvider.GetRequiredService<ILandParcelRepository>();
            var enrich = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
            var persist = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
            var update = scope.ServiceProvider.GetRequiredService<UpdateLandParcelCommandHandler>();
            var validator = scope.ServiceProvider.GetRequiredService<UpdateLandParcelCommandValidator>();

            var parcel = await UpsertParcelAsync(
                parcels,
                "COL-E2E-PERSIST-1",
                "Western",
                "Colombo",
                colomboA);
            parcelId = parcel.Id;

            var other = await UpsertParcelAsync(
                parcels,
                "COL-E2E-PERSIST-OTHER",
                "Western",
                "Colombo",
                factory.CreatePoint(new Coordinate(79.87, 6.93)));
            otherParcelId = other.Id;

            var first = await enrich.EnrichAsync(parcelId);
            await persist.PersistAsync(first);
            var otherResult = await enrich.EnrichAsync(otherParcelId);
            await persist.PersistAsync(otherResult);
            otherStatusBeforeSiblingUpdate = otherResult.OverallStatus;
            before = new
            {
                first.OverallStatus,
                road = first.RoadAccessibility?.DistanceMeters,
                roadStatus = first.RoadAccessibility?.Status.ToString(),
                otherOverallStatus = otherStatusBeforeSiblingUpdate.ToString(),
                srs = 4326
            };

            await using (var reloadScope = provider.CreateAsyncScope())
            {
                var db = reloadScope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
                var entity = await db.LandParcels.AsNoTracking().FirstAsync(p => p.Id == parcelId);
                if (entity.Centroid is null
                    || Math.Abs(entity.Centroid.X - colomboA.X) > 1e-6
                    || Math.Abs(entity.Centroid.Y - colomboA.Y) > 1e-6)
                {
                    failures.Add("Centroid not persisted correctly after create/enrich.");
                }

                if (entity.SpatialReferenceSystemId != 4326)
                {
                    failures.Add($"Expected SRS 4326, got {entity.SpatialReferenceSystemId}.");
                }
            }

            partialCoord = validator.Validate(new UpdateLandParcelCommand
            {
                LandParcelId = parcelId,
                CentroidLatitude = 6.94
            });
            if (((ValidationResult)partialCoord).IsValid)
            {
                failures.Add("Partial coordinate update should fail validation.");
            }

            invalidCoord = validator.Validate(new UpdateLandParcelCommand
            {
                LandParcelId = parcelId,
                CentroidLatitude = 120,
                CentroidLongitude = 79.88
            });
            if (((ValidationResult)invalidCoord).IsValid)
            {
                failures.Add("Out-of-range latitude should fail validation.");
            }

            await update.HandleAsync(new UpdateLandParcelCommand
            {
                LandParcelId = parcelId,
                CentroidLatitude = colomboB.Y,
                CentroidLongitude = colomboB.X
            });
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LandIntelligenceDbContext>();
            var entity = await db.LandParcels.AsNoTracking().FirstAsync(p => p.Id == parcelId);
            if (entity.Centroid is null
                || Math.Abs(entity.Centroid.X - colomboB.X) > 1e-6
                || Math.Abs(entity.Centroid.Y - colomboB.Y) > 1e-6)
            {
                failures.Add("Fresh DbContext reload did not see updated centroid.");
            }

            var snapshot = await db.LandParcelGisEnrichmentSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.LandParcelId == parcelId);
            afterInvalidate = new
            {
                snapshotStatus = snapshot?.OverallStatus.ToString(),
                infraCount = await db.InfrastructureFeatures.CountAsync(f => f.LandParcelId == parcelId)
            };
            if (snapshot?.OverallStatus != LandParcelGisEnrichmentOverallStatus.Unavailable)
            {
                failures.Add("Expected enrichment snapshot Unavailable after location update.");
            }

            var otherSnap = await db.LandParcelGisEnrichmentSnapshots
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.LandParcelId == otherParcelId);
            otherIntact = new
            {
                beforeSiblingUpdate = otherStatusBeforeSiblingUpdate.ToString(),
                afterSiblingUpdate = otherSnap?.OverallStatus.ToString(),
                otherId = otherParcelId
            };
            if (otherSnap is null)
            {
                failures.Add("Unrelated parcel enrichment snapshot missing after sibling update.");
            }
            else if (otherSnap.OverallStatus != otherStatusBeforeSiblingUpdate)
            {
                failures.Add(
                    "Unrelated parcel enrichment status changed after sibling location update "
                    + $"({otherStatusBeforeSiblingUpdate} → {otherSnap.OverallStatus}).");
            }
        }

        await using (var scope = provider.CreateAsyncScope())
        {
            var enrich = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
            var persist = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
            var second = await enrich.EnrichAsync(parcelId);
            await persist.PersistAsync(second);
            afterReEnrich = new
            {
                second.OverallStatus,
                road = second.RoadAccessibility?.DistanceMeters,
                roadStatus = second.RoadAccessibility?.Status.ToString(),
                sourceLayer = second.RoadAccessibility?.SourceLayer
            };
            if (second.RoadAccessibility?.Status == RoadAccessibilityEnrichmentStatus.Available
                && second.RoadAccessibility.SourceLayer != GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer)
            {
                failures.Add("Colombo re-enrichment used unexpected road source layer.");
            }
        }

        return new
        {
            parcelId,
            before,
            afterInvalidate,
            afterReEnrich,
            otherIntact,
            partialCoordValid = ((ValidationResult)partialCoord!).IsValid,
            invalidCoordValid = ((ValidationResult)invalidCoord!).IsValid
        };
    }

    private static async Task<object> RunRoadBufferChecksAsync(
        LandIntelligenceDbContext db,
        List<string> failures)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

        // Empirical samples: interior Colombo, near district boundary, and cross-boundary nearest-road case.
        var samples = new (string Label, double Lon, double Lat, bool RequireInsideDistrict)[]
        {
            ("interior_colombo", 79.8612, 6.9271, true),
            ("near_boundary", 79.95, 6.90, true),
            ("cross_boundary_knn", 79.99289101549337, 6.935148694012742, false)
        };

        var sampleResults = new List<object>();
        double maxObservedNearestM = 0;

        foreach (var sample in samples)
        {
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                WITH pt AS (
                  SELECT ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)::geography AS g
                ),
                district AS (
                  SELECT ST_SetSRID(b."Boundary", 4326) AS geom
                  FROM land_intelligence.gis_administrative_boundaries b
                  WHERE b."Name" = 'Colombo' AND b."BoundaryType" = 2
                  LIMIT 1
                )
                SELECT
                  ST_Distance(
                    (SELECT g FROM pt),
                    r."Geometry"::geography
                  ) AS dist_m,
                  ST_DWithin(
                    (SELECT geom FROM district)::geography,
                    r."Geometry"::geography,
                    25000
                  ) AS road_within_25km_of_district,
                  ST_Contains(
                    (SELECT geom FROM district),
                    ST_SetSRID(ST_MakePoint(@lon, @lat), 4326)
                  ) AS point_in_district
                FROM land_intelligence.gis_roads r
                WHERE r."SourceLayer" = 'osm_motor_roads'
                ORDER BY r."Geometry"::geography <-> (SELECT g FROM pt)
                LIMIT 1
                """;
            AddParameter(cmd, "lon", sample.Lon);
            AddParameter(cmd, "lat", sample.Lat);

            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                failures.Add($"No OSM motor road found for sample {sample.Label}.");
                continue;
            }

            var dist = reader.GetDouble(0);
            var roadInBuffer = reader.GetBoolean(1);
            var inDistrict = reader.GetBoolean(2);
            maxObservedNearestM = Math.Max(maxObservedNearestM, dist);

            sampleResults.Add(new
            {
                sample.Label,
                sample.Lon,
                sample.Lat,
                nearestRoadDistanceM = dist,
                nearestRoadWithin25kmOfDistrictPolygon = roadInBuffer,
                pointInColomboDistrict = inDistrict
            });

            if (sample.RequireInsideDistrict && !inDistrict)
            {
                failures.Add($"Sample {sample.Label} expected inside Colombo district polygon.");
            }

            if (!roadInBuffer)
            {
                failures.Add(
                    $"Sample {sample.Label}: nearest OSM motor road is outside the 25 km district buffer "
                    + $"extent (dist={dist:F1}m). Buffer cannot guarantee this query without a fallback.");
            }
        }

        return new
        {
            bufferKm = 25,
            guarantee =
                "Empirical only: for these representative interior/boundary/cross-boundary samples, "
                + "the nearest osm_motor_roads feature lies within the prepared 25 km district buffer. "
                + "This is NOT a general correctness proof for every possible coordinate in Sri Lanka.",
            sufficiencyRationale =
                "Supported Colombo enrichment queries use district coverage + KNN against the buffered "
                + "import. Points inside Colombo (or near its boundary) whose nearest motor road is within "
                + "25 km of the district polygon are covered by construction of the prepared GeoJSON. "
                + "OutsideCoverage remains the explicit fallback when no filtered road is found.",
            maxObservedNearestRoadDistanceM = maxObservedNearestM,
            samples = sampleResults
        };
    }

    private static async Task<object> RunRecommendationScenariosAsync(
        ServiceProvider provider,
        bool experimentalEnabled,
        List<string> failures)
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
            PostGisConfiguration.DefaultSpatialReferenceSystemId);
        Guid supportedId;
        Guid abstainId;
        Guid hardRejectId;
        decimal? scoreWithExp = null;

        await using (var scope = provider.CreateAsyncScope())
        {
            var parcels = scope.ServiceProvider.GetRequiredService<ILandParcelRepository>();
            var enrich = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentService>();
            var persist = scope.ServiceProvider.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
            var engine = scope.ServiceProvider.GetRequiredService<ILandRecommendationEngine>();

            var supported = await UpsertParcelAsync(
                parcels,
                "COL-E2E-ML-SUPPORTED",
                "Western",
                "Colombo",
                factory.CreatePoint(new Coordinate(79.8612, 6.9271)));
            supportedId = supported.Id;
            await persist.PersistAsync(await enrich.EnrichAsync(supportedId));

            // Outside coverage → conservation/road often unavailable → abstention
            var abstain = await UpsertParcelAsync(
                parcels,
                "COL-E2E-ML-ABSTAIN",
                "Western",
                "Colombo",
                factory.CreatePoint(new Coordinate(80.5, 7.5)));
            abstainId = abstain.Id;
            await persist.PersistAsync(await enrich.EnrichAsync(abstainId));

            var hard = new LandParcel(
                new ParcelIdentifier($"COL-E2E-H-{Guid.NewGuid():N}"[..18], "COL-E2E"),
                new LandCategory(LandCategoryType.StateLand, "e2e probe"),
                new LandArea(2.5m, AreaUnit.Hectares),
                new AdministrativeLocation("Western", "Colombo", "E2E DS"),
                new SpatialReference(6.93, 79.87, "EPSG:4326"));
            hard.AddEnvironmentalRestriction(new EnvironmentalRestriction(
                EnvironmentalRestrictionType.Wetland,
                "[E2E] Prohibitive wetland",
                RestrictionSeverity.Prohibitive));
            await parcels.AddAsync(hard);
            hardRejectId = hard.Id;

            var request = new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                PreferredLocation = new PreferredLocationCriteria { District = "Colombo" },
                MaxResults = 20
            };

            var response = await engine.RecommendAsync(request);
            var supportedRec = response.Recommendations.FirstOrDefault(r => r.ParcelId == supportedId);
            var abstainRec = response.Recommendations.FirstOrDefault(r => r.ParcelId == abstainId);
            var hardRec = response.Recommendations.FirstOrDefault(r => r.ParcelId == hardRejectId);

            if (supportedRec is null)
            {
                failures.Add("Supported Colombo parcel missing from recommendation results.");
            }
            else
            {
                scoreWithExp = supportedRec.SuitabilityScore;
                if (experimentalEnabled)
                {
                    var hasPrediction = supportedRec.Evidence.Any(e =>
                        e.Source == "ExperimentalColomboOsmRf"
                        && e.RelatedCriterionName == "ExperimentalColomboMlPrediction");
                    var hasAbstention = supportedRec.Evidence.Any(e =>
                        e.RelatedCriterionName == "ExperimentalColomboMlAbstention");
                    var hasUnavailable = supportedRec.Evidence.Any(e =>
                        e.RelatedCriterionName == "ExperimentalColomboMlUnavailable");
                    if (!hasPrediction && !hasAbstention && !hasUnavailable)
                    {
                        failures.Add(
                            "Enabled experimental path produced no ExperimentalColomboOsmRf evidence "
                            + "for supported parcel.");
                    }

                    if (supportedRec.Evidence.Any(e => e.Source == "RandomForestSuitabilityModel"))
                    {
                        failures.Add(
                            "Production ML evidence present while SuppressProductionMlWhenEnabled should skip it.");
                    }
                }
            }

            if (experimentalEnabled && abstainRec is not null)
            {
                var abstained = abstainRec.Evidence.Any(e =>
                    e.RelatedCriterionName is "ExperimentalColomboMlAbstention"
                        or "ExperimentalColomboMlUnavailable");
                if (!abstained && !abstainRec.HardConstraintRejected)
                {
                    // Outside coverage should abstain or soft-fail — not claim a confident prediction without conservation.
                    var predicted = abstainRec.Evidence.Any(e =>
                        e.RelatedCriterionName == "ExperimentalColomboMlPrediction");
                    if (predicted)
                    {
                        failures.Add(
                            "Outside-coverage parcel should not receive experimental prediction without assessed conservation.");
                    }
                }
            }

            if (hardRec is null || !hardRec.HardConstraintRejected)
            {
                failures.Add("Hard-rejection parcel was not rejected.");
            }
            else if (hardRec.Evidence.Any(e => e.Source == "ExperimentalColomboOsmRf"))
            {
                failures.Add("Hard-rejected parcel should not call experimental ML.");
            }

            // Hambantota road source check via enrichment
            var hambantota = await UpsertParcelAsync(
                parcels,
                "COL-E2E-HAM-ROAD",
                "Southern",
                "Hambantota",
                factory.CreatePoint(new Coordinate(81.1185, 6.1429)));
            var hamEnrich = await enrich.EnrichAsync(hambantota.Id);
            await persist.PersistAsync(hamEnrich);
            var hamRoadLayer = hamEnrich.RoadAccessibility?.SourceLayer;
            if (hamEnrich.RoadAccessibility?.Status == RoadAccessibilityEnrichmentStatus.Available
                && !string.Equals(hamRoadLayer, "expressways", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"Hambantota expected expressways road source, got '{hamRoadLayer}'.");
            }

            return new
            {
                candidateCount = response.CandidateCount,
                supported = SummarizeRec(supportedRec),
                abstain = SummarizeRec(abstainRec),
                hardReject = SummarizeRec(hardRec),
                hambantotaRoadSource = hamRoadLayer,
                hambantotaRoadStatus = hamEnrich.RoadAccessibility?.Status.ToString(),
                scoreWithExperimental = scoreWithExp
            };
        }
    }

    private static async Task<object> RunHttpContractSampleAsync(
        IConfiguration configuration,
        string solutionRoot,
        List<string> failures)
    {
        // Land Intelligence controllers only — same route/body as Swagger External API.
        // Avoids full StateLandGovernance.Api host (WorkflowGovernance ILeaseCaseRepository DI gap).
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.AddConsole().SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(options => options.ListenLocalhost(5265));
        builder.Services.AddSingleton<IConfiguration>(configuration);
        builder.Services.AddLandIntelligenceInfrastructure(configuration);
        builder.Services.AddLandIntelligencePresentation();

        await using var app = builder.Build();
        app.UseLandIntelligenceExceptionHandling();
        app.MapControllers();

        await app.StartAsync();
        await Task.Delay(500);

        using var http = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:5265") };
        var body = """
            {"requiredPurpose":1,"preferredLocation":{"district":"Colombo"},"maxResults":10}
            """;
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync("/api/v1/land/recommendations", content);
        var json = await response.Content.ReadAsStringAsync();
        var samplePath = Path.Combine(
            solutionRoot,
            "data",
            "gis",
            "experiments",
            "colombo",
            "colombo_exp_step3_swagger_sample.json");
        await File.WriteAllTextAsync(samplePath, json);

        if (!response.IsSuccessStatusCode)
        {
            failures.Add($"HTTP sample failed: {(int)response.StatusCode} {json}");
        }

        var experimentalNotes = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (doc.RootElement.TryGetProperty("recommendations", out var recs))
            {
                foreach (var rec in recs.EnumerateArray())
                {
                    if (!rec.TryGetProperty("evidence", out var evidence))
                    {
                        continue;
                    }

                    foreach (var item in evidence.EnumerateArray())
                    {
                        if (item.TryGetProperty("source", out var source)
                            && source.GetString() == "ExperimentalColomboOsmRf")
                        {
                            experimentalNotes.Add(
                                $"{rec.GetProperty("cadastralNumber").GetString()}: "
                                + item.GetProperty("relatedCriterionName").GetString()
                                + " — "
                                + Truncate(item.GetProperty("description").GetString(), 160));
                        }
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            failures.Add("HTTP sample JSON parse failed: " + ex.Message);
        }

        await app.StopAsync();
        return new
        {
            endpoint = "POST /api/v1/land/recommendations",
            requestBody = body.Trim(),
            statusCode = (int)response.StatusCode,
            samplePath,
            note =
                "Observed via Land Intelligence controllers on http://127.0.0.1:5265 "
                + "(identical route/contract to Swagger External API). "
                + "Full Api host was not started: WorkflowGovernance DI cannot resolve ILeaseCaseRepository.",
            experimentalNotes
        };
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
        {
            return value ?? string.Empty;
        }

        return value[..max] + "...";
    }

    private static async Task<object> RunDisabledExperimentalSmokeAsync(
        string solutionRoot,
        string geoJsonPath)
    {
        var configuration = BuildConfiguration(enableExperimental: false);
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Error));
        services.AddLandIntelligenceInfrastructure(configuration);
        services.AddLandIntelligenceApplication();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<ExperimentalColomboMlOptions>>().Value;
        var engine = scope.ServiceProvider.GetRequiredService<ILandRecommendationEngine>();
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            PreferredLocation = new PreferredLocationCriteria { District = "Colombo" },
            MaxResults = 5
        });

        var anyExperimental = response.Recommendations.SelectMany(r => r.Evidence)
            .Any(e => e.Source == "ExperimentalColomboOsmRf");

        return new
        {
            options.Enabled,
            experimentalEvidencePresent = anyExperimental,
            retainedExistingBehavior = !options.Enabled && !anyExperimental
        };
    }

    private static object? SummarizeRec(LandParcelRecommendationResult? rec)
    {
        if (rec is null)
        {
            return null;
        }

        return new
        {
            rec.ParcelId,
            rec.CadastralNumber,
            rec.SuitabilityScore,
            rec.HardConstraintRejected,
            experimentalEvidence = rec.Evidence
                .Where(e => e.Source == "ExperimentalColomboOsmRf")
                .Select(e => new { e.RelatedCriterionName, e.Description })
                .ToArray()
        };
    }

    private static async Task EnsurePostgisAndMigrateAsync(LandIntelligenceDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
        await db.Database.MigrateAsync();
    }

    private static async Task<LandParcel> UpsertParcelAsync(
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
            new ParcelIdentifier(cadastral, "COL-E2E"),
            new LandCategory(LandCategoryType.StateLand, "e2e probe"),
            new LandArea(2.5m, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, "E2E DS"),
            new SpatialReference(centroid.Y, centroid.X, "EPSG:4326"));
        await repository.AddAsync(parcel);
        return parcel;
    }

    private static IConfiguration BuildConfiguration(bool enableExperimental)
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
                GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion,
            ["LandIntelligence:ExperimentalColomboMl:Enabled"] = enableExperimental ? "true" : "false",
            ["LandIntelligence:ExperimentalColomboMl:ServiceBaseUrl"] = "http://127.0.0.1:8501",
            ["LandIntelligence:ExperimentalColomboMl:ExpectedCandidateId"] = "colombo_osm_backend_compatible",
            ["LandIntelligence:ExperimentalColomboMl:TimeoutSeconds"] = "5",
            ["LandIntelligence:ExperimentalColomboMl:SuppressProductionMlWhenEnabled"] = "true"
        };

        return new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddInMemoryCollection(values) // wins over process env leftovers
            .Build();
    }

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
            "colombo_exp_step3_e2e_report.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Report: {path}");
    }
}
