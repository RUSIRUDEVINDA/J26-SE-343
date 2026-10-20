namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class HumanEvidencePageReader(IAnalysisArtifactReader artifacts) : IHumanEvidencePageReader
{
    public async Task<string?> ReadAsync(Guid version, Guid run, int page, CancellationToken ct)
    {
        var bytes = await artifacts.ReadAsync($"analysis-artifacts/{version:D}/{run:D}/pages/{page}.json", ct);
        if (!bytes.HasValue) return null;
        var transcript = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(bytes.Value.Span);
        return transcript?.PageNumber == page ? transcript.CleanText : null;
    }
}
