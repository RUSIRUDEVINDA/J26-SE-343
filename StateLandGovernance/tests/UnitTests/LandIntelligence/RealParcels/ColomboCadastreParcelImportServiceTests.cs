using System.IO.Compression;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.RealParcels;

namespace StateLandGovernance.UnitTests.LandIntelligence.RealParcels;

public sealed class ColomboCadastreParcelImportServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "colombo-real-import", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData("2", "Garden")]
    [InlineData("999", null)]
    public void Source_domains_decode_pcl_if_luc_without_inventing_unknown_labels(string code, string? expected) =>
        Assert.Equal(expected, ColomboCadastreSourceDomains.TryDecodeLandUseEnglishLabel(code));

    [Fact]
    public async Task DryRun_accepts_real_shape_fields_without_exposing_pii()
    {
        Directory.CreateDirectory(_root);
        var zipPath = Path.Combine(_root, "source.zip");
        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            Write(archive, ColomboCadastreParcelImportService.DistrictEntry, FeatureCollection("""
                {"type":"Feature","properties":{"DISTRICT_N":"COLOMBO"},"geometry":{"type":"Polygon","coordinates":[[[79.8,6.8],[80.0,6.8],[80.0,7.0],[79.8,7.0],[79.8,6.8]]]}}
                """));
            Write(archive, ColomboCadastreParcelImportService.GnEntry, FeatureCollection("""
                {"type":"Feature","properties":{"CM_Index":"42","DSD_N":"Colombo","GND_N":"Fort"},"geometry":{"type":"Polygon","coordinates":[[[79.8,6.8],[80.0,6.8],[80.0,7.0],[79.8,7.0],[79.8,6.8]]]}}
                """));
            Write(archive, ColomboCadastreParcelImportService.CadastreEntry, FeatureCollection("""
                {"type":"Feature","properties":{"OBJECTID":1,"pcl_pg_id":"COL-1","pcl_pg_id2":"P-1","sht_pg_id2":"S-1","adm_gn_ix":42,"pcl_if_luc":2,"pcl_if_lu":"Residential","pcl_if_clc":3,"pcl_if_clm":"PERSON MUST NOT LEAK","created_user":"operator"},"geometry":{"type":"Polygon","coordinates":[[[79.85,6.85],[79.86,6.85],[79.86,6.86],[79.85,6.86],[79.85,6.85]]]}}
                """));
        }

        var reportPath = Path.Combine(_root, "report.json");
        var service = new ColomboCadastreParcelImportService(NullLogger<ColomboCadastreParcelImportService>.Instance);
        var report = await service.RunAsync(new ColomboCadastreImportOptions(zipPath, Limit: 1, ReportPath: reportPath));

        Assert.Equal(1, report.ValidFeatures);
        Assert.Equal(0, report.Skipped);
        var reportJson = await File.ReadAllTextAsync(reportPath);
        Assert.DoesNotContain("PERSON MUST NOT LEAK", reportJson, StringComparison.Ordinal);
        Assert.DoesNotContain("operator", reportJson, StringComparison.Ordinal);
    }

    private static string FeatureCollection(string feature) => $"{{\"type\":\"FeatureCollection\",\"features\":[{feature}]}}";
    private static void Write(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open());
        writer.Write(content);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }
}
