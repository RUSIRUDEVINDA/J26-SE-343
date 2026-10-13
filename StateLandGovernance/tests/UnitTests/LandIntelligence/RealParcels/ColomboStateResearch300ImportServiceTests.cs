using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class ColomboStateResearch300ImportServiceTests
{
    // Requires StateLandGovernance/data/gis/.../state_research_300 (gitignored). Excluded from CI via Category!=Integration.
    [Fact]
    [Trait("Category", "Integration")]
    public async Task DryRun_ValidatesThreeHundredParcelsAndMatchingIds()
    {
        var root = ResolveDatasetRoot();
        var service = new ColomboStateResearch300ImportService(NullLogger<ColomboStateResearch300ImportService>.Instance);
        var report = await service.RunAsync(new ColomboStateResearch300ImportOptions(
            Path.Combine(root, "colombo_state_parcels_300_real.csv"),
            Path.Combine(root, "colombo_state_parcels_300_boundaries.geojson"),
            Path.Combine(root, "lease_workflow_SIMULATED_300.csv"),
            Apply: false,
            ReportPath: Path.Combine(Path.GetTempPath(), $"colombo-research-dry-run-{Guid.NewGuid():N}.json")));

        Assert.Equal("dry-run", report.Mode);
        Assert.Equal(300, report.CsvRecordCount);
        Assert.Equal(300, report.GeoJsonFeatureCount);
        Assert.Equal(300, report.ValidParcels);
        Assert.Equal(0, report.Failed);
    }

    // Requires StateLandGovernance/data/gis/.../state_research_300 (gitignored). Excluded from CI via Category!=Integration.
    [Fact]
    [Trait("Category", "Integration")]
    public void Stored_centroid_uses_csv_centroid_not_map_marker_for_known_record()
    {
        var root = ResolveDatasetRoot();
        var csvPath = Path.Combine(root, "colombo_state_parcels_300_real.csv");
        var text = File.ReadAllText(csvPath);
        var rows = ColomboStateResearch300CsvParser.Parse(text);
        var columnIndex = ColomboStateResearch300CsvParser.ReadHeader(rows[0]);
        var dataRow = rows.Skip(1).First(row =>
            string.Equals(
                ColomboStateResearch300CsvParser.GetField(row, columnIndex, "record_id"),
                "COL-STATE-520001020057",
                StringComparison.OrdinalIgnoreCase));

        var centroidLat = double.Parse(
            ColomboStateResearch300CsvParser.GetField(dataRow, columnIndex, "centroid_latitude"),
            CultureInfo.InvariantCulture);
        var centroidLon = double.Parse(
            ColomboStateResearch300CsvParser.GetField(dataRow, columnIndex, "centroid_longitude"),
            CultureInfo.InvariantCulture);
        var markerLat = double.Parse(
            ColomboStateResearch300CsvParser.GetField(dataRow, columnIndex, "map_marker_latitude"),
            CultureInfo.InvariantCulture);
        var markerLon = double.Parse(
            ColomboStateResearch300CsvParser.GetField(dataRow, columnIndex, "map_marker_longitude"),
            CultureInfo.InvariantCulture);

        Assert.NotEqual(centroidLat, markerLat);
        Assert.NotEqual(centroidLon, markerLon);

        var stored = ColomboStateResearch300CoordinateSemantics.ToStoredCentroid(centroidLat, centroidLon);
        Assert.Equal(centroidLon, stored.X, precision: 8);
        Assert.Equal(centroidLat, stored.Y, precision: 8);
        Assert.NotEqual(markerLon, stored.X, precision: 8);
        Assert.NotEqual(markerLat, stored.Y, precision: 8);
    }

    [Fact]
    public void AssertResearchDatabase_RejectsSharedDatabases()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ColomboStateResearch300ImportService.AssertResearchDatabase(
                "Host=localhost;Database=land_intel_colombo_real_demo;Username=postgres;Password=x"));
        Assert.Contains("Refusing", ex.Message);
    }

    private static string ResolveDatasetRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(
                dir.FullName,
                "data",
                "gis",
                "experiments",
                "colombo",
                "state_research_300");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate state_research_300 dataset folder.");
    }
}
