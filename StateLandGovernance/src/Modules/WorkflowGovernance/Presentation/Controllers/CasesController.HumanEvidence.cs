namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;

using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/proposal/human-evidence")]
    public async Task<IActionResult> ReadHumanEvidence(Guid caseId, [FromQuery] Guid documentId,
        [FromServices] HumanEvidenceFactService service, CancellationToken ct)
    {
        if (!IsOfficer()) return StatusCode(403);
        var authority = ExecutionAuthority(caseId, "HumanEvidenceReviewer");
        var canPublish = authority != null && authority.Capabilities.Contains("FactSnapshotPublisher") &&
            User.HasClaim("authority_document_id", documentId.ToString("D"));
        return Ok(new { source = await service.ReadAsync(caseId, documentId, ct), canPublish,
            authorityRequired = "Authenticated case/document-scoped HumanEvidenceReviewer and FactSnapshotPublisher capabilities" });
    }
    [HttpGet("{caseId:guid}/proposal/human-evidence/page")]
    public async Task<IActionResult> ReadHumanEvidencePage(Guid caseId,[FromQuery]Guid documentId,[FromQuery]int pageNumber,
        [FromServices]HumanEvidenceFactService service,CancellationToken ct)
    {
        if(!IsOfficer() || ExecutionAuthority(caseId,"HumanEvidenceReviewer")==null ||
            !User.HasClaim("authority_document_id",documentId.ToString("D")))return StatusCode(403);
        return Ok(await service.ReadPageAsync(caseId,documentId,pageNumber,ct));
    }
    [HttpPost("{caseId:guid}/proposal/human-evidence/snapshot")]
    public async Task<IActionResult> PublishHumanEvidenceSnapshot(Guid caseId, PublishHumanEvidenceRequest request,
        [FromServices] HumanEvidenceFactService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "HumanEvidenceReviewer") is not { } authority ||
            !authority.Capabilities.Contains("FactSnapshotPublisher") ||
            !User.HasClaim("authority_document_id", request.DocumentId.ToString("D"))) return StatusCode(403);
        if (request.Observations == null || request.Observations.Any(o => !Canonical17Fields.Any(f => f.Code == o.FactCode)))
            return BadRequest(new { error = "Human evidence must use the unchanged canonical fact catalogue." });
        authority = authority with { ScopeKind = "GovernedDocument", ScopeTargetIdentifier = request.DocumentId.ToString("D") };
        // Publication does not silently link the new snapshot to the case; the existing explicit link action remains required.
        return Ok(new { snapshot = await service.PublishAsync(caseId, request, authority, ct),
            analysisRevision = checked(request.ExpectedAnalysisRevision + 1) });
    }

    [HttpPost("{caseId:guid}/proposal/human-evidence/snapshots/{snapshotId:guid}/link")]
    public async Task<IActionResult> LinkHumanEvidenceSnapshot(Guid caseId, Guid snapshotId, LinkHumanEvidenceSnapshotRequest request,
        [FromServices] HumanEvidenceFactService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "FactSnapshotPublisher") is not { } authority ||
            !User.HasClaim("authority_document_id", request.DocumentId.ToString("D"))) return StatusCode(403);
        authority = authority with { ScopeKind = "GovernedDocument", ScopeTargetIdentifier = request.DocumentId.ToString("D") };
        await service.LinkAsync(caseId, snapshotId, request, authority, ct);
        return Ok(new { status = "ExplicitlyLinked", snapshotId });
    }
}
