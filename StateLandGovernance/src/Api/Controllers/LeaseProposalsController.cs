using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.LeaseFeasibility.Infrastructure.Proposals;
using StateLandGovernance.LeaseFeasibility.Application.Commands;
using StateLandGovernance.LeaseFeasibility.Application.Proposals;

namespace StateLandGovernance.Api.Controllers;

[ApiController]
[Route("api/lease-feasibility/proposals")]
public sealed class LeaseProposalsController(GenerateLeaseProposalCommandHandler handler, IProposalNarrativeGenerator narratives, IConfiguration configuration) : ControllerBase
{
    [HttpGet("requirements")]
    public IActionResult Requirements()
    {
        Response.Headers.CacheControl = "no-store";
        return Ok(new { templateVersion = LandDepartmentProposalTemplate.Version, sections = LandDepartmentProposalTemplate.Sections,
            aiConfigured = narratives.IsConfigured, retrievalMethod = "Reviewed section-filtered reference summaries", serverPersistence = false });
    }
    [HttpPost("generate")]
    [RequestSizeLimit(300_000)]
    public async Task<IActionResult> Generate(ProposalGenerationRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(await handler.HandleAsync(new(request), cancellationToken)); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
        catch (InvalidOperationException ex) { return Problem(statusCode: 503, detail: ex.Message); }
        catch (HttpRequestException) { return Problem(statusCode: 503, detail: "AI provider could not be reached. No substitute AI draft was generated."); }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return Problem(statusCode: 504, detail: "AI generation timed out. Retry or use structured drafting."); }
        catch (JsonException) { return Problem(statusCode: 502, detail: "AI returned malformed draft data. No draft was accepted."); }
    }
    public sealed record ExportRequest(string Text, bool BlankForm = false);
    [HttpPost("export/docx")]
    [RequestSizeLimit(1_000_000)]
    public IActionResult Export(ExportRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        try
        {
            var pages = LandDepartmentProposalForm.Build(request.Text, request.BlankForm);
            var bytes = ProposalFormRenderer.Word(pages, configuration["ProposalGeneration:DocumentFont"] ?? "Sinhala MN");
            return File(bytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "land-department-project-proposal.docx");
        }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }
    [HttpPost("export/preview")]
    [RequestSizeLimit(1_000_000)]
    public IActionResult Preview(ExportRequest request)
    {
        Response.Headers.CacheControl = "no-store";
        try { return Ok(new { html = ProposalFormRenderer.Html(LandDepartmentProposalForm.Build(request.Text, request.BlankForm)), layoutVersion = LandDepartmentProposalForm.LayoutVersion }); }
        catch (ArgumentException ex) { return Problem(statusCode: 400, detail: ex.Message); }
    }
}
