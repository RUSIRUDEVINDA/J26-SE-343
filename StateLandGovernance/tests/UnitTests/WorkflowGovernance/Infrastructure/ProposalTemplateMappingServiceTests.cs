namespace StateLandGovernance.UnitTests.WorkflowGovernance.Infrastructure;
using System.Net;
using System.Text.Json;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

public sealed class ProposalTemplateMappingServiceTests
{
    [Fact]
    public async Task Map_ActualSnakeCasePageArtifact_ReadsExactRunAndUnavailableRemainsUncertain()
    {
        var version = Guid.NewGuid(); var runId = Guid.NewGuid(); var now = DateTime.UtcNow;
        var run = new AnalysisRunDto(runId, 1, "Completed", "Gemini", "TEST", "1", [], now, now, now, null, null,
            null, null, null, new(Guid.NewGuid(), "Completed", now, [new(Guid.NewGuid(), "OcrPageTranscript", "application/json", "SHA-256", new string('a',64))], []));
        var analysis = new DocumentAnalysisDto(Guid.NewGuid(), Guid.NewGuid(), version, "SHA-256", new string('a',64), 1, 1, now, 1, 1, run, [run]);
        var template = new ProposalTemplate(new("TEST"), "1", "TEST ONLY", "TEST SOURCE", ProposalTemplateStatus.Active,
            [new(new("section_beyond_catalogue"), ProposalRequirementKind.Section, "Extra section", true, new("TEST"), "1", "TEST SOURCE PAGE 1", 1)]);
        var reader = new Reader($"analysis-artifacts/{version:D}/{runId:D}/pages/1.json");
        using var handler = new Handler(); using var client = new HttpClient(handler) { BaseAddress = new Uri("http://test/") };
        var service = new ProposalTemplateMappingService(client, reader, new Writer());
        var result = await service.MapAsync(analysis, template, default);
        Assert.Equal(1, handler.Calls);
        Assert.Equal("Uncertain", Assert.Single(result).State);
        Assert.Equal(runId.ToString("D"), result[0].ExtractionReference);
        Assert.Contains("section_beyond_catalogue", handler.Body);
        Assert.Contains("TEST exact OCR text", handler.Body);
        Assert.DoesNotContain("TEST raw", handler.Body);
    }
    private sealed class Handler : HttpMessageHandler
    {
        public int Calls; public string Body = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        { Calls++; Body = await request.Content!.ReadAsStringAsync(ct); return new(HttpStatusCode.ServiceUnavailable); }
    }
    private sealed class Reader(string key) : IAnalysisArtifactReader
    {
        public Task<ReadOnlyMemory<byte>?> ReadAsync(string reference, CancellationToken ct = default) =>
            Task.FromResult<ReadOnlyMemory<byte>?>(reference == key ? new ReadOnlyMemory<byte>(JsonSerializer.SerializeToUtf8Bytes(
                new FastApiPageTranscriptArtifactDto(1, "sin+eng", "TEST raw", "TEST exact OCR text"))) : (ReadOnlyMemory<byte>?)null);
    }
    private sealed class Writer : IAnalysisArtifactWriter
    {
        public Task<AnalysisArtifactWriteReceipt> WriteAsync(string key, string contentType, ReadOnlyMemory<byte> content, CancellationToken ct = default) =>
            throw new InvalidOperationException("Unavailable semantic output must not be written as success.");
    }
}
