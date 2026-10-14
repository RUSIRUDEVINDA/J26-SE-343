using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisReferenceData;

public sealed class GisReferenceDataImportServiceTests : IDisposable
{
    private readonly string _dataRoot;
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly GisReferenceDataImportService _importService;

    public GisReferenceDataImportServiceTests()
    {
        _dataRoot = Path.Combine(Path.GetTempPath(), "gis-import-tests", Guid.NewGuid().ToString("N"));
        HambantotaPilotGeoJsonFixtures.Write(_dataRoot);

        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new LandIntelligenceDbContext(options);
        _importService = new GisReferenceDataImportService(_dbContext, NullLogger<GisReferenceDataImportService>.Instance);
    }

    [Fact]
    public async Task ImportHambantotaPilotAsync_identifies_hambantota_district_and_southern_province()
    {
        var result = await _importService.ImportHambantotaPilotAsync(_dataRoot);

        Assert.Equal("Hambantota", result.HambantotaDistrictName);
        Assert.Equal("Southern", result.ProvinceName);

        var boundaries = await _dbContext.GisAdministrativeBoundaries.ToListAsync();
        Assert.Contains(boundaries, boundary =>
            boundary.Name == "Hambantota" && boundary.BoundaryType == GisAdministrativeBoundaryType.District);
        Assert.Contains(boundaries, boundary =>
            boundary.Name == "Southern" && boundary.BoundaryType == GisAdministrativeBoundaryType.Province);
    }

    [Fact]
    public async Task ImportHambantotaPilotAsync_imports_inside_and_crossing_features_and_excludes_outside_features()
    {
        await _importService.ImportHambantotaPilotAsync(_dataRoot);

        var roads = await _dbContext.GisRoads.ToListAsync();
        Assert.Single(roads);
        Assert.Equal(GisRoadType.Expressway, roads[0].RoadType);
        Assert.Equal("Inside Expressway", roads[0].Name);

        var water = await _dbContext.GisWaterFeatures.ToListAsync();
        Assert.Equal(2, water.Count);
        Assert.Contains(water, feature => feature.FeatureType == GisWaterFeatureType.Canal && feature.Name == "Inside Canal");
        Assert.Contains(water, feature => feature.FeatureType == GisWaterFeatureType.Lake && feature.Name == "Crossing Lake");
        Assert.DoesNotContain(water, feature => feature.Name == "Outside Canal");

        var soilGroups = await _dbContext.GisSoilGroups.ToListAsync();
        Assert.Single(soilGroups);
        Assert.Equal("Inside Soil", soilGroups[0].Name);
    }

    [Fact]
    public async Task ImportHambantotaPilotAsync_maps_source_feature_id_and_skips_invalid_geometry()
    {
        var result = await _importService.ImportHambantotaPilotAsync(_dataRoot);

        var district = await _dbContext.GisAdministrativeBoundaries
            .SingleAsync(boundary => boundary.Name == "Hambantota");
        Assert.Equal("8", district.SourceFeatureId);

        var fingerprintEntity = await _dbContext.GisSoilGroups.SingleAsync();
        Assert.Null(fingerprintEntity.SourceFeatureId);
        Assert.NotNull(fingerprintEntity.SourceFingerprint);

        Assert.True(result.FeaturesSkipped >= 1);
        Assert.Contains(result.SkipLog, entry => entry.Contains("soil_erosion/999"));
    }

    [Fact]
    public async Task ImportHambantotaPilotAsync_is_idempotent_on_second_run()
    {
        var first = await _importService.ImportHambantotaPilotAsync(_dataRoot);
        var second = await _importService.ImportHambantotaPilotAsync(_dataRoot);

        Assert.Equal(first.TableCounts["gis_administrative_boundaries"], second.TableCounts["gis_administrative_boundaries"]);
        Assert.Equal(first.TableCounts["gis_roads"], second.TableCounts["gis_roads"]);
        Assert.Equal(first.TableCounts["gis_water_features"], second.TableCounts["gis_water_features"]);
        Assert.Equal(first.TableCounts["gis_soil_groups"], second.TableCounts["gis_soil_groups"]);
        Assert.True(second.FeaturesUpdated >= 1);
        Assert.Equal(0, second.FeaturesImported);
    }

    [Fact]
    public async Task Imported_geometries_use_srid_4326()
    {
        await _importService.ImportHambantotaPilotAsync(_dataRoot);

        var district = await _dbContext.GisAdministrativeBoundaries.SingleAsync(b => b.Name == "Hambantota");
        var road = await _dbContext.GisRoads.SingleAsync();

        Assert.Equal(PostGisConfiguration.DefaultSpatialReferenceSystemId, district.Boundary.SRID);
        Assert.Equal(PostGisConfiguration.DefaultSpatialReferenceSystemId, road.Geometry.SRID);
    }

    public void Dispose()
    {
        _dbContext.Dispose();

        if (Directory.Exists(_dataRoot))
        {
            Directory.Delete(_dataRoot, recursive: true);
        }
    }
}

internal static class HambantotaPilotGeoJsonFixtures
{
    public static void Write(string dataRoot)
    {
        Directory.CreateDirectory(Path.Combine(dataRoot, "boundaries"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "transport"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "water"));
        Directory.CreateDirectory(Path.Combine(dataRoot, "soil"));

        File.WriteAllText(Path.Combine(dataRoot, "boundaries", "district_boundaries.geojson"), DistrictBoundaries);
        File.WriteAllText(Path.Combine(dataRoot, "boundaries", "province_boundaries.geojson"), ProvinceBoundaries);
        File.WriteAllText(Path.Combine(dataRoot, "transport", "expressways.geojson"), Expressways);
        File.WriteAllText(Path.Combine(dataRoot, "water", "canals.geojson"), Canals);
        File.WriteAllText(Path.Combine(dataRoot, "water", "lakes.geojson"), Lakes);
        File.WriteAllText(Path.Combine(dataRoot, "soil", "soil_groups.geojson"), SoilGroups);
        File.WriteAllText(Path.Combine(dataRoot, "soil", "soil_conservation_areas.geojson"), SoilConservationAreas);
        File.WriteAllText(Path.Combine(dataRoot, "soil", "soil_erosion.geojson"), SoilErosion);
    }

    private const string DistrictBoundaries = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": {
                "objectid": 8,
                "district_name": "Hambantota",
                "province_name": "Southern"
              },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [80.0, 6.0],
                    [80.2, 6.0],
                    [80.2, 6.2],
                    [80.0, 6.2],
                    [80.0, 6.0]
                  ]
                ]
              }
            },
            {
              "type": "Feature",
              "properties": {
                "objectid": 1,
                "district_name": "Colombo",
                "province_name": "Western"
              },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [79.8, 6.8],
                    [79.9, 6.8],
                    [79.9, 6.9],
                    [79.8, 6.9],
                    [79.8, 6.8]
                  ]
                ]
              }
            }
          ]
        }
        """;

    private const string ProvinceBoundaries = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": {
                "objectid": 7,
                "province_name": "Southern"
              },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [79.9, 5.9],
                    [80.3, 5.9],
                    [80.3, 6.3],
                    [79.9, 6.3],
                    [79.9, 5.9]
                  ]
                ]
              }
            }
          ]
        }
        """;

    private const string Expressways = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "objectid": 101, "road_name": "Inside Expressway" },
              "geometry": {
                "type": "LineString",
                "coordinates": [[80.05, 6.05], [80.15, 6.15]]
              }
            },
            {
              "type": "Feature",
              "properties": { "objectid": 102, "road_name": "Outside Expressway" },
              "geometry": {
                "type": "LineString",
                "coordinates": [[79.0, 7.0], [79.1, 7.1]]
              }
            }
          ]
        }
        """;

    private const string Canals = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "objectid": 201, "canal_name": "Inside Canal" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [80.05, 6.05],
                    [80.08, 6.05],
                    [80.08, 6.08],
                    [80.05, 6.08],
                    [80.05, 6.05]
                  ]
                ]
              }
            },
            {
              "type": "Feature",
              "properties": { "objectid": 202, "canal_name": "Outside Canal" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [79.0, 7.0],
                    [79.05, 7.0],
                    [79.05, 7.05],
                    [79.0, 7.05],
                    [79.0, 7.0]
                  ]
                ]
              }
            }
          ]
        }
        """;

    private const string Lakes = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "objectid": 301, "lake_name": "Crossing Lake" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [79.95, 6.1],
                    [80.25, 6.1],
                    [80.25, 6.15],
                    [79.95, 6.15],
                    [79.95, 6.1]
                  ]
                ]
              }
            }
          ]
        }
        """;

    private const string SoilGroups = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "name": "Inside Soil" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [80.06, 6.06],
                    [80.09, 6.06],
                    [80.09, 6.09],
                    [80.06, 6.09],
                    [80.06, 6.06]
                  ]
                ]
              }
            },
            {
              "type": "Feature",
              "properties": { "name": "Invalid Soil" },
              "geometry": { "type": "Point", "coordinates": [80.07, 6.07] }
            }
          ]
        }
        """;

    private const string SoilConservationAreas = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "objectid": 401, "id": "A", "description": "Inside conservation" },
              "geometry": {
                "type": "Polygon",
                "coordinates": [
                  [
                    [80.04, 6.04],
                    [80.07, 6.04],
                    [80.07, 6.07],
                    [80.04, 6.07],
                    [80.04, 6.04]
                  ]
                ]
              }
            }
          ]
        }
        """;

    private const string SoilErosion = """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": { "objectid": 501, "erosion_rate": 12.5 },
              "geometry": { "type": "Point", "coordinates": [80.1, 6.1] }
            },
            {
              "type": "Feature",
              "properties": { "objectid": 999, "name": "invalid-feature" },
              "geometry": { "type": "LineString", "coordinates": [[80.1, 6.1], [80.11, 6.11]] }
            }
          ]
        }
        """;
}
