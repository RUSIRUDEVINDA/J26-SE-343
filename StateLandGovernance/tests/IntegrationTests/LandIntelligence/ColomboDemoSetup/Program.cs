using System.Data.Common;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;
using StateLandGovernance.Shared.Infrastructure.Configuration;

namespace StateLandGovernance.LandIntelligence.ColomboDemoSetup;

/// <summary>
/// Idempotent local demo database preparation for Land Intelligence + frontend.
/// Creates synthetic, clearly labelled demo parcels only — not cadastral truth
/// and not the 1,500 offline training sample locations.
/// </summary>
internal static class Program
{
    public const string DemoDatabaseName = "land_intel_colombo_demo";
    public const string DemoConnectionEnvironmentVariable = "LAND_INTELLIGENCE_DEMO_CONNECTION";

    public const string EligibleCadastral = "DEMO-COL-ELIGIBLE-001";
    public const string HardRejectCadastral = "DEMO-COL-HARD-REJECT-001";
    public const string SparseGisCadastral = "DEMO-COL-SPARSE-GIS-001";

    public static async Task<int> Main(string[] args)
    {
        var report = new Dictionary<string, object?>(StringComparer.Ordinal);
        try
        {
            EnvFileLoader.LoadFromRepositoryRoot();
            var connection = RequireDemoConnectionString();
            Environment.SetEnvironmentVariable("LAND_INTELLIGENCE_CONNECTION", connection);

            var databaseName = ExtractDatabaseName(connection);
            if (!string.Equals(databaseName, DemoDatabaseName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Demo setup refuses database '{databaseName}'. Expected '{DemoDatabaseName}'.");
            }

            if (string.Equals(databaseName, "state_land_governance", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Refusing to modify state_land_governance.");
            }

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

            report["database"] = databaseName;
            report["geojson_path"] = geoJsonPath;
            report["experimental_ml_enabled"] = false;
            report["training_locations_imported"] = false;
            report["provenance_note"] =
                "Demo parcels are synthetic fixtures for local UI/API demonstration. " +
                "They are not Land Commissioner cadastral records. " +
                "The 1,500 offline Colombo training sample locations were not imported.";

            if (!File.Exists(geoJsonPath))
            {
                throw new FileNotFoundException(
                    "Buffered OSM GeoJSON not found. Prepare it with the ML tools README export command first.",
                    geoJsonPath);
            }

            var configuration = BuildDemoConfiguration();
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole().SetMinimumLevel(LogLevel.Information);
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
            var enrichment = sp.GetRequiredService<ILandParcelGisEnrichmentService>();
            var enrichmentPersistence = sp.GetRequiredService<ILandParcelGisEnrichmentPersistenceService>();
            var parcels = sp.GetRequiredService<ILandParcelRepository>();

            var gisRoot = Path.Combine(solutionRoot, "data", "gis", "LandIntelligence_GIS");

            var pilot = await pilotImport.ImportHambantotaPilotAsync(gisRoot);
            report["hambantota_pilot_import"] = new
            {
                pilot.FeaturesImported,
                pilot.FeaturesUpdated,
                pilot.TableCounts
            };

            await osmImport.ImportDistrictBoundaryAsync(
                GisEnrichmentCoverageDefaults.Colombo,
                gisRoot);

            var roadImport = await osmImport.ImportFromGeoJsonAsync(geoJsonPath);
            report["osm_motor_roads_import"] = roadImport;

            var layerImport = await colomboLayers.ImportWaterSoilConservationForDistrictAsync(
                GisEnrichmentCoverageDefaults.Colombo,
                gisRoot);
            report["colombo_layer_import"] = layerImport;

            var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
                PostGisConfiguration.DefaultSpatialReferenceSystemId);

            // Eligible: Colombo centroid — real GIS enrichment against imported layers.
            var eligiblePoint = factory.CreatePoint(new Coordinate(79.8612, 6.9271));
            var eligible = await UpsertDemoParcelAsync(
                parcels,
                EligibleCadastral,
                "DEMO-SURVEY-ELIGIBLE-001",
                "Western",
                "Colombo",
                "Colombo DS",
                eligiblePoint,
                areaHectares: 3.5m,
                currentUse: LandUseType.Agricultural,
                categoryDescription:
                    "[DEMO SYNTHETIC] Eligible rule-based recommendation fixture. Not an official cadastral parcel.",
                environmentalRestrictions: null);

            // Hard reject: same district with an explicitly simulated prohibitive restriction.
            var hardPoint = factory.CreatePoint(new Coordinate(79.8700, 6.9350));
            var hard = await UpsertDemoParcelAsync(
                parcels,
                HardRejectCadastral,
                "DEMO-SURVEY-HARD-001",
                "Western",
                "Colombo",
                "Colombo DS",
                hardPoint,
                areaHectares: 2.0m,
                currentUse: LandUseType.Agricultural,
                categoryDescription:
                    "[DEMO SYNTHETIC] Hard-constraint fixture. Not an official cadastral parcel.",
                environmentalRestrictions:
                [
                    new EnvironmentalRestriction(
                        EnvironmentalRestrictionType.ProtectedArea,
                        "[DEMO SIMULATED] Prohibitive protected-area restriction for local hard-reject demonstration. Not sourced from official gazette GIS.",
                        RestrictionSeverity.Prohibitive)
                ]);

            // Sparse GIS: outside Colombo/Hambantota coverage → enrichment unavailable / outside coverage.
            var sparsePoint = factory.CreatePoint(new Coordinate(80.6337, 7.2906));
            var sparse = await UpsertDemoParcelAsync(
                parcels,
                SparseGisCadastral,
                "DEMO-SURVEY-SPARSE-001",
                "Central",
                "Kandy",
                "Kandy DS",
                sparsePoint,
                areaHectares: 1.5m,
                currentUse: LandUseType.Agricultural,
                categoryDescription:
                    "[DEMO SYNTHETIC] Outside configured GIS enrichment coverage (Kandy). " +
                    "Soil/road GIS evidence expected unavailable. Not an official cadastral parcel.",
                environmentalRestrictions: null,
                clearCharacteristics: true);

            report["demo_parcels"] = new
            {
                eligible = new
                {
                    id = eligible.Id,
                    cadastralNumber = eligible.Identifier.CadastralNumber,
                    scenario = "Eligible for rule-based Agricultural recommendations in Colombo",
                    provenance = "synthetic_demo_fixture"
                },
                hard_reject = new
                {
                    id = hard.Id,
                    cadastralNumber = hard.Identifier.CadastralNumber,
                    scenario = "HardConstraintRejected via simulated Prohibitive ProtectedArea",
                    provenance = "synthetic_demo_fixture_with_simulated_restriction"
                },
                sparse_gis = new
                {
                    id = sparse.Id,
                    cadastralNumber = sparse.Identifier.CadastralNumber,
                    scenario = "Outside GIS enrichment coverage — missing evidence stays Unavailable",
                    provenance = "synthetic_demo_fixture"
                }
            };

            var eligibleEnrichment = await enrichment.EnrichAsync(eligible.Id);
            await enrichmentPersistence.PersistAsync(eligibleEnrichment);
            var hardEnrichment = await enrichment.EnrichAsync(hard.Id);
            await enrichmentPersistence.PersistAsync(hardEnrichment);
            var sparseEnrichment = await enrichment.EnrichAsync(sparse.Id);
            await enrichmentPersistence.PersistAsync(sparseEnrichment);

            report["enrichment"] = new
            {
                eligible = new
                {
                    eligibleEnrichment.OverallStatus,
                    administrative = eligibleEnrichment.Administrative?.Status.ToString(),
                    road = eligibleEnrichment.RoadAccessibility?.Status.ToString(),
                    water = eligibleEnrichment.WaterProximity?.Status.ToString(),
                    soil = eligibleEnrichment.Soil?.Status.ToString(),
                    environmental = eligibleEnrichment.Environmental?.Status.ToString(),
                    warnings = eligibleEnrichment.Warnings
                },
                hard_reject = new
                {
                    hardEnrichment.OverallStatus,
                    road = hardEnrichment.RoadAccessibility?.Status.ToString()
                },
                sparse_gis = new
                {
                    sparseEnrichment.OverallStatus,
                    administrative = sparseEnrichment.Administrative?.Status.ToString(),
                    road = sparseEnrichment.RoadAccessibility?.Status.ToString(),
                    note = "OutsideCoverage / Unavailable expected for Kandy under Colombo+Hambantota coverage options."
                }
            };

            report["gis_sources"] = new
            {
                osm_motor_roads =
                    "data/gis/experiments/colombo/osm_motor_roads_colombo_buffer.geojson (OSM/HDX ODbL; filter 2026-03-20-colombo-v1)",
                hambantota_pilot = "data/gis/LandIntelligence_GIS (expressways + Hambantota layers)",
                colombo_boundary_water_soil_conservation =
                    "data/gis/LandIntelligence_GIS district-intersect imports via ColomboExperimentGisImportService",
                coverage_limitations =
                    "Layer presence does not prove complete geographic coverage. Missing themes stay Unknown/Unavailable."
            };

            report["simulated_fields"] = new[]
            {
                "Parcel identifiers, area, category, current use (demo attributes)",
                "DEMO-COL-HARD-REJECT-001 environmental restriction (ProtectedArea / Prohibitive) — labelled [DEMO SIMULATED]",
                "Offline ML training labels/locations were NOT imported"
            };

            await WriteReportAsync(solutionRoot, report);
            Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine();
            Console.WriteLine("Demo database ready. Experimental ML remains disabled in committed API config.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            report["error"] = ex.Message;
            try
            {
                var solutionRoot = ResolveSolutionRoot();
                await WriteReportAsync(solutionRoot, report);
            }
            catch
            {
                // ignore report write failures after primary error
            }

            return 1;
        }
    }

    private static string RequireDemoConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable(DemoConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connection))
        {
            throw new InvalidOperationException(
                $"Set {DemoConnectionEnvironmentVariable} to the PostGIS connection string for '{DemoDatabaseName}'. " +
                "Do not point this tool at state_land_governance. Passwords must come from the environment, not committed files.");
        }

        return connection;
    }

    private static IConfiguration BuildDemoConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LandIntelligence:ExperimentalColomboMl:Enabled"] = "false",
                ["LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames:0"] = "Hambantota",
                ["LandIntelligence:GisEnrichmentCoverage:SupportedDistrictNames:1"] = "Colombo",
                ["LandIntelligence:GisEnrichmentCoverage:RoadSourceLayer"] = "expressways",
                ["LandIntelligence:GisEnrichmentCoverage:OsmMotorRoadFilterPolicyVersion"] =
                    GisEnrichmentCoverageDefaults.OsmMotorRoadFilterPolicyVersion,
                ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:0:DistrictName"] = "Hambantota",
                ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:0:RoadSourceLayer"] = "expressways",
                ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:1:DistrictName"] = "Colombo",
                ["LandIntelligence:GisEnrichmentCoverage:DistrictRoadSourceLayers:1:RoadSourceLayer"] = "osm_motor_roads"
            })
            .AddEnvironmentVariables()
            .Build();

    private static async Task EnsurePostgisAndMigrateAsync(LandIntelligenceDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS postgis;");
        await db.Database.MigrateAsync();
    }

    private static async Task<LandParcel> UpsertDemoParcelAsync(
        ILandParcelRepository repository,
        string cadastral,
        string surveyPlan,
        string province,
        string district,
        string divisionalSecretariat,
        Point centroid,
        decimal areaHectares,
        LandUseType currentUse,
        string categoryDescription,
        IReadOnlyList<EnvironmentalRestriction>? environmentalRestrictions,
        bool clearCharacteristics = false)
    {
        var existing = await repository.GetByCadastralNumberAsync(cadastral);
        if (existing is not null)
        {
            existing.UpdateSpatial(new SpatialReference(centroid.Y, centroid.X, "EPSG:4326"));
            existing.UpdateCurrentUse(new LandUse(currentUse, "[DEMO SYNTHETIC] Agricultural demonstration use"));
            if (clearCharacteristics)
            {
                existing.UpdateCharacteristics(new LandCharacteristics());
            }

            // Do not ReplaceEnvironmentalRestrictions on update: that would drop GIS-owned
            // rows persisted by enrichment and can raise EF concurrency errors.
            if (environmentalRestrictions is { Count: > 0 })
            {
                var missingSimulated = environmentalRestrictions.Where(desired =>
                    existing.EnvironmentalRestrictions.All(existingRestriction =>
                        !string.Equals(
                            existingRestriction.Description,
                            desired.Description,
                            StringComparison.Ordinal))).ToList();

                foreach (var restriction in missingSimulated)
                {
                    existing.AddEnvironmentalRestriction(restriction);
                }
            }

            await repository.UpdateAsync(existing);
            return existing;
        }

        var parcel = new LandParcel(
            new ParcelIdentifier(cadastral, surveyPlan),
            new LandCategory(LandCategoryType.StateLand, categoryDescription),
            new LandArea(areaHectares, AreaUnit.Hectares),
            new AdministrativeLocation(province, district, divisionalSecretariat),
            new SpatialReference(centroid.Y, centroid.X, "EPSG:4326"),
            new LandUse(currentUse, "[DEMO SYNTHETIC] Agricultural demonstration use"),
            clearCharacteristics
                ? new LandCharacteristics()
                : null);

        if (environmentalRestrictions is { Count: > 0 })
        {
            foreach (var restriction in environmentalRestrictions)
            {
                parcel.AddEnvironmentalRestriction(restriction);
            }
        }

        await repository.AddAsync(parcel);
        return parcel;
    }

    private static string ExtractDatabaseName(string connectionString)
    {
        var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        if (builder.TryGetValue("Database", out var database))
        {
            return Convert.ToString(database) ?? string.Empty;
        }

        if (builder.TryGetValue("database", out database))
        {
            return Convert.ToString(database) ?? string.Empty;
        }

        return string.Empty;
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
            "colombo_demo_fixtures.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Wrote {path}");
    }
}
