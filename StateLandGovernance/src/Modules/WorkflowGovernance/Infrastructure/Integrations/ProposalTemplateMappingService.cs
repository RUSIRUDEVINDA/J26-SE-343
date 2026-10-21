namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class ProposalTemplateMappingService(HttpClient http, IAnalysisArtifactReader reader,
    IAnalysisArtifactWriter writer) : IProposalTemplateMappingService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public async Task<IReadOnlyList<ProposalObservationDto>?> ReadCachedAsync(DocumentAnalysisDto analysis, ProposalTemplate template, CancellationToken ct)
    {
        if (analysis.ActiveRun?.State != "Completed") return null;
        var key = $"analysis-artifacts/{analysis.DocumentVersionId:D}/{analysis.ActiveRun.Id:D}/template-{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(template.DefinitionDigest))).ToLowerInvariant()}.json";
        var bytes = await reader.ReadAsync(key, ct);
        if (!bytes.HasValue) return null;
        using var doc = JsonDocument.Parse(bytes.Value);
        if (doc.RootElement.GetProperty("documentVersionId").GetGuid() != analysis.DocumentVersionId ||
            doc.RootElement.GetProperty("analysisRunId").GetGuid() != analysis.ActiveRun.Id ||
            doc.RootElement.GetProperty("definitionDigest").GetString() != template.DefinitionDigest)
            throw new InvalidOperationException("Template observation artifact binding mismatch.");
        return doc.RootElement.GetProperty("observations").Deserialize<List<ProposalObservationDto>>(Json);
    }
    public async Task<IReadOnlyList<ProposalObservationDto>> MapAsync(DocumentAnalysisDto analysis, ProposalTemplate template, CancellationToken ct)
    {
        var run = analysis.ActiveRun;
        IReadOnlyList<ProposalObservationDto> Unavailable(string reason) => template.Requirements.Select(r =>
            new ProposalObservationDto(r.Id.Value, "Uncertain", null, null, null, run?.Id.ToString("D"), reason)).ToArray();
        if (run?.State != "Completed" || run.Result == null) return Unavailable("Current analysis is not complete.");
        var pages = new List<object>();
        var pageCount = run.Result.Artifacts.Count(a => a.ArtifactKind == "OcrPageTranscript");
        for (var page = 1; page <= pageCount; page++)
        {
            var bytes = await reader.ReadAsync($"analysis-artifacts/{analysis.DocumentVersionId:D}/{run.Id:D}/pages/{page}.json", ct);
            if (!bytes.HasValue) return Unavailable("Exact run page artifacts are unavailable; absence cannot be assessed.");
            var transcript = JsonSerializer.Deserialize<FastApiPageTranscriptArtifactDto>(bytes.Value.Span)
                ?? throw new InvalidOperationException("OCR page artifact is empty.");
            if (transcript.PageNumber != page) throw new InvalidOperationException("OCR artifact page binding mismatch.");
            pages.Add(new { pageNumber = page, text = transcript.CleanText ?? "" });
        }
        if (pages.Count == 0) return Unavailable("No page artifacts exist for the exact current run.");
        var snapshot = template.CreateSnapshot();
        var digest = snapshot.DefinitionDigest;
        var key = $"analysis-artifacts/{analysis.DocumentVersionId:D}/{run.Id:D}/template-{Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(digest))).ToLowerInvariant()}.json";
        var cached = await reader.ReadAsync(key, ct);
        JsonElement result;
        if (cached.HasValue) result = JsonSerializer.Deserialize<JsonElement>(cached.Value.Span, Json);
        else
        {
            using var response = await http.PostAsJsonAsync("proposal-template/map", new {
                documentId = analysis.GovernedDocumentId, documentVersionId = analysis.DocumentVersionId,
                analysisRunId = run.Id, templateId = template.Id.Value, templateVersion = template.Version,
                definitionDigest = digest, pages, requirements = template.Requirements.Select(r => new {
                    requirementId = r.Id.Value, displayName = r.DisplayName,
                    sourceReference = r.AuthoritativeSourceReference, applicabilityContext = r.ApplicabilityContext }) }, ct);
            if (!response.IsSuccessStatusCode) return Unavailable("Semantic template mapping unavailable; officer review required.");
            result = await response.Content.ReadFromJsonAsync<JsonElement>(Json, ct);
        }
        if (result.GetProperty("documentId").GetGuid() != analysis.GovernedDocumentId ||
            result.GetProperty("documentVersionId").GetGuid() != analysis.DocumentVersionId ||
            result.GetProperty("analysisRunId").GetGuid() != run.Id || result.GetProperty("definitionDigest").GetString() != digest ||
            result.GetProperty("templateId").GetString() != template.Id.Value || result.GetProperty("templateVersion").GetString() != template.Version)
            throw new InvalidOperationException("Template mapping response binding mismatch.");
        var observations = result.GetProperty("observations").Deserialize<List<ProposalObservationDto>>(Json)!;
        if (observations.Count != template.Requirements.Count || observations.Select(o => o.RequirementId).Distinct().Count() != observations.Count ||
            observations.Any(o => !template.Requirements.Any(r => r.Id.Value == o.RequirementId)))
            throw new InvalidOperationException("Template observation identifiers mismatch.");
        if (!cached.HasValue) await writer.WriteAsync(key, "application/json", JsonSerializer.SerializeToUtf8Bytes(result, Json), ct);
        return observations;
    }
}
