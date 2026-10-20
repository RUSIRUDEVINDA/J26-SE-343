namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

public sealed record DevelopmentSpatialInput(Guid CaseId, Guid ParcelId, string Version, JsonElement Parcel,
    string SpatialReviewJson, IReadOnlyList<string> Provinces, string Label);

public interface IDevelopmentSpatialInputProvider
{
    Task<DevelopmentSpatialInput?> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken ct);
}

public sealed class DevelopmentSpatialInputProvider(IOptions<CaseScreeningOptions> options, IHostEnvironment? environment = null) : IDevelopmentSpatialInputProvider
{
    public async Task<DevelopmentSpatialInput?> ReadAsync(Guid caseId, Guid? parcelId, CancellationToken ct)
    {
        if (environment?.IsDevelopment() != true || !options.Value.EnableDevelopmentSpatialMocks || !parcelId.HasValue) return null;
        var path = options.Value.DevelopmentSpatialFixturePath;
        if (string.IsNullOrWhiteSpace(path)) return null;
        using var stream = File.OpenRead(path);
        var fixtures = await JsonSerializer.DeserializeAsync<List<DevelopmentSpatialInput>>(stream, CaseScreeningServiceOptions(), ct);
        var fixture = fixtures?.SingleOrDefault(f => f.CaseId == caseId && (!parcelId.HasValue || f.ParcelId == parcelId));
        if (fixture == null) return null;
        if (fixture.ParcelId == Guid.Empty || string.IsNullOrWhiteSpace(fixture.Version) ||
            !fixture.Label.StartsWith("Development mock", StringComparison.Ordinal) || fixture.Provinces.Count == 0)
            throw new InvalidOperationException("Development spatial fixture provenance is incomplete.");
        return fixture;
    }
    private static JsonSerializerOptions CaseScreeningServiceOptions() => new(JsonSerializerDefaults.Web);
}
