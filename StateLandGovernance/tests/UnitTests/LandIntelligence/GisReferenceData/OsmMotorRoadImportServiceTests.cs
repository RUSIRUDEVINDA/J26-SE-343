using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Import;
using StateLandGovernance.LandIntelligence.Infrastructure.PostGIS;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisReferenceData;

public sealed class OsmMotorRoadImportServiceTests : IDisposable
{
    private readonly string _geoJsonPath;
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly OsmMotorRoadImportService _importService;

    public OsmMotorRoadImportServiceTests()
    {
        var dir = Path.Combine(Path.GetTempPath(), "osm-motor-road-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _geoJsonPath = Path.Combine(dir, "osm_motor_roads.geojson");
        File.WriteAllText(_geoJsonPath, BuildFixtureGeoJson());

        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _dbContext = new LandIntelligenceDbContext(options);
        _importService = new OsmMotorRoadImportService(
            _dbContext,
            NullLogger<OsmMotorRoadImportService>.Instance,
            Options.Create(new GisEnrichmentCoverageOptions()));
    }

    [Fact]
    public async Task ImportFromGeoJsonAsync_is_idempotent_and_preserves_osm_identity()
    {
        var first = await _importService.ImportFromGeoJsonAsync(_geoJsonPath);
        var second = await _importService.ImportFromGeoJsonAsync(_geoJsonPath);

        Assert.Equal(2, first.FeaturesImported);
        Assert.Equal(0, first.FeaturesUpdated);
        Assert.Equal(0, second.FeaturesImported);
        Assert.Equal(2, second.FeaturesUpdated);
        Assert.Equal(2, second.TotalOsmMotorRoadsInTable);

        var roads = await _dbContext.GisRoads.ToListAsync();
        Assert.Equal(2, roads.Count);
        Assert.All(roads, road =>
        {
            Assert.Equal(GisEnrichmentCoverageDefaults.OsmMotorRoadsLayer, road.SourceLayer);
            Assert.NotNull(road.SourceFeatureId);
            Assert.StartsWith("[", road.Name);
        });

        Assert.Contains(roads, road =>
            road.SourceFeatureId == "1001"
            && road.RoadType == GisRoadType.Expressway
            && OsmMotorRoadAttributeEncoding.TryDecodeHighway(road.Name) == "motorway");
        Assert.Contains(roads, road =>
            road.SourceFeatureId == "1002"
            && OsmMotorRoadAttributeEncoding.TryDecodeHighway(road.Name) == "residential");
    }

    [Fact]
    public async Task DeleteOsmMotorRoadsAsync_removes_only_osm_motor_roads_layer()
    {
        await _importService.ImportFromGeoJsonAsync(_geoJsonPath);

        _dbContext.GisRoads.Add(new StateLandGovernance.LandIntelligence.Infrastructure.Persistence.GisReferenceData.Entities.GisRoadEntity
        {
            Id = Guid.NewGuid(),
            SourceName = GisReferenceDataPaths.SourceName,
            SourceLayer = GisReferenceDataPaths.ExpresswaysLayer,
            SourceFeatureId = "expressway-1",
            Name = "Pilot Expressway",
            RoadType = GisRoadType.Expressway,
            Geometry = CreateLine(80.0, 6.0, 80.1, 6.0),
            ImportedAt = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        var deleted = await _importService.DeleteOsmMotorRoadsAsync();
        Assert.Equal(2, deleted);

        var remaining = await _dbContext.GisRoads.ToListAsync();
        Assert.Single(remaining);
        Assert.Equal(GisReferenceDataPaths.ExpresswaysLayer, remaining[0].SourceLayer);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        var dir = Path.GetDirectoryName(_geoJsonPath);
        if (dir is not null && Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private static MultiLineString CreateLine(double x1, double y1, double x2, double y2)
    {
        var factory = NtsGeometryServices.Instance.CreateGeometryFactory(
            PostGisConfiguration.DefaultSpatialReferenceSystemId);
        var line = factory.CreateLineString(
        [
            new Coordinate(x1, y1),
            new Coordinate(x2, y2)
        ]);
        return factory.CreateMultiLineString([line]);
    }

    private static string BuildFixtureGeoJson() =>
        """
        {
          "type": "FeatureCollection",
          "features": [
            {
              "type": "Feature",
              "properties": {
                "osm_id": "1001",
                "highway": "motorway",
                "name": "Southern Expressway Link"
              },
              "geometry": {
                "type": "LineString",
                "coordinates": [[79.85, 6.90], [79.90, 6.92]]
              }
            },
            {
              "type": "Feature",
              "properties": {
                "osm_id": "1002",
                "highway": "residential",
                "name": "Sample Street"
              },
              "geometry": {
                "type": "LineString",
                "coordinates": [[79.86, 6.91], [79.87, 6.91]]
              }
            }
          ]
        }
        """;
}
