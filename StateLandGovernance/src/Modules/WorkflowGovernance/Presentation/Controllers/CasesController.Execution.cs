namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed partial class CasesController
{
    private VerifiedAuthorityContext? ExecutionAuthority(Guid caseId, string? capability = null)
    {
        if (User.Identity?.IsAuthenticated != true || !User.HasClaim("authority_case_id", caseId.ToString("D"))) return null;
        var caps = User.FindAll("capability").Select(c => c.Value).Distinct().ToArray();
        if (capability != null && !caps.Contains(capability)) return null;
        if (!long.TryParse(User.FindFirst("exp")?.Value, out var expiry) || expiry <= DateTimeOffset.UtcNow.ToUnixTimeSeconds() || expiry > 253402300799) return null;
        var now = DateTime.UtcNow;
        return new(ResolveActorId(), caps, "LeaseCase", caseId.ToString("D"), now.AddSeconds(-1), now, DateTimeOffset.FromUnixTimeSeconds(expiry).UtcDateTime);
    }
    private InstitutionalActorContext? InstitutionActor(Guid caseId) => ExecutionAuthority(caseId) is { } authority &&
        User.FindFirst("institution")?.Value is { Length: > 0 } institution ? new(authority, institution) : null;

    [HttpGet("{caseId:guid}/workflow-execution")]
    public async Task<IActionResult> GetExecution(Guid caseId, [FromServices] WorkflowExecutionService service, CancellationToken ct)
    { if (!IsOfficer()) return StatusCode(403); return Ok(await service.GetAsync(caseId, ct)); }

    [HttpPost("{caseId:guid}/workflow-execution/start")]
    public async Task<IActionResult> StartExecution(Guid caseId, StartExecutionRequest request, [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "WorkflowExecutor") is not { } authority) return StatusCode(403);
        return Ok(await service.StartAsync(caseId, request, authority, ct));
    }
    [HttpGet("{caseId:guid}/institutional-requests/{stageId:guid}")]
    public async Task<IActionResult> InstitutionalRequest(Guid caseId, Guid stageId, [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        return Ok(await service.RestrictedAsync(caseId, stageId, actor, ct));
    }
    [HttpPost("{caseId:guid}/institutional-requests/{stageId:guid}/start")]
    public async Task<IActionResult> StartInstitutionalStage(Guid caseId, Guid stageId, StageActionRequest request,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.StageStartAsync(caseId, stageId, request.ExpectedExecutionRevision, actor, ct);
        return Ok(await service.RestrictedAsync(caseId, stageId, actor, ct));
    }
    [HttpPost("{caseId:guid}/institutional-requests/{stageId:guid}/decision")]
    public async Task<IActionResult> RecordInstitutionalDecision(Guid caseId, Guid stageId, StageDecisionRequest request,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.DecideAsync(caseId, stageId, request, actor, ct);
        return Ok(await service.RestrictedAsync(caseId, stageId, actor, ct));
    }
    [HttpPost("{caseId:guid}/workflow-execution/departments")]
    public async Task<IActionResult> AddExecutionDepartment(Guid caseId, AddDepartmentRequest request,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor || actor.Institution != "DS") return StatusCode(403);
        await service.AddDepartmentAsync(caseId, request, actor, ct);
        return Ok(new { status = "DraftRevisionAwaitingApproval", detail = "The added review is not activated until authorized revision approval." });
    }
    public sealed record ApproveExecutionRevisionRequest(int ExpectedCaseRevision, int ExpectedPlanRevision, int ExpectedExecutionRevision);
    [HttpPost("{caseId:guid}/workflow-execution/plans/{planId:guid}/approve")]
    public async Task<IActionResult> ApproveExecutionRevision(Guid caseId, Guid planId, ApproveExecutionRevisionRequest request,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "WorkflowPlanApprover") is not { } authority) return StatusCode(403);
        await service.ApproveRevisionAsync(caseId, planId, request.ExpectedCaseRevision, request.ExpectedPlanRevision,
            request.ExpectedExecutionRevision, authority, ct);
        return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpGet("workflow-execution/rejected")]
    public async Task<IActionResult> RejectedExecutionCases([FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true || User.FindFirst("institution")?.Value != "COMMISSIONER" ||
            !User.HasClaim("capability", "CommissionerReviewer")) return StatusCode(403);
        var ids = await service.RejectedCasesAsync(ct);
        return Ok(ids.Where(id => ExecutionAuthority(id, "CommissionerReviewer") != null));
    }
    [HttpGet("{caseId:guid}/workflow-progress")]
    public async Task<IActionResult> ApplicantWorkflowProgress(Guid caseId, [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        var c = await _getCaseHandler.HandleAsync(new Application.Queries.GetLeaseCaseByIdQuery(caseId), ct);
        if (!IsOfficer() && c.CreatedByActorId != ResolveActorId()) return StatusCode(403);
        var state = await service.GetAsync(caseId, ct);
        var commissionerApproved = state.Stages.Any(s => s.Code == "COMMISSIONER" && s.Decision?.Outcome is "Approved" or "ApprovedWithConditions");
        return Ok(new { state.CaseId, state.Status, state.IsDevelopmentMock,
            milestones = state.Stages.Select(s => new { s.Code, s.Status }),
            blockingRejections = state.Stages.Where(s => s.Decision?.Outcome == "Rejected" &&
                !(commissionerApproved && s.Code is "PROVINCIAL_LC" or "INTER_PROVINCIAL_LC"))
                .Select(s => new { s.Institution, reason = s.Decision!.Reason?.Split(" Evidence: ")[0], reportedToCommissioner = true }),
            reviewedRegionalNonRecommendations = state.Stages.Where(s => commissionerApproved && s.Decision?.Outcome == "Rejected" &&
                s.Code is "PROVINCIAL_LC" or "INTER_PROVINCIAL_LC").Select(s => new { s.Institution,
                    reason = s.Decision!.Reason?.Split(" Evidence: ")[0], detail = "The Commissioner subsequently recorded an independent decision; this regional recommendation no longer blocks progress." }),
            detail = state.Current ? "Institutional decisions remain explicit; screening and plan approval are not application approval." : "Progress is blocked pending current prerequisite evidence and approvals." });
    }
    [HttpGet("{caseId:guid}/institutional-requests/{stageId:guid}/documents")]
    public async Task<IActionResult> InstitutionDocuments(Guid caseId, Guid stageId, [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        var request = await service.RestrictedAsync(caseId, stageId, actor, ct);
        var docs = await _getDocsHandler.HandleAsync(new Application.Queries.GetDocumentsByLeaseCaseIdQuery(caseId), ct);
        return Ok(docs.Where(d => d.LogicalCategory == WorkflowExecutionService.DocumentCategory(request.ExecutionId, stageId) ||
            d.LogicalCategory == $"WorkflowEvidence:{request.ExecutionId:N}:{stageId:N}")
            .Select(d => new { documentId = d.Id, versions = d.Versions.Select(v => new { versionId = v.Id, v.OriginalFileName, v.SubmittedAt }) }));
    }
    [HttpPost("{caseId:guid}/institutional-requests/{stageId:guid}/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadInstitutionDocument(Guid caseId, Guid stageId, [FromForm] IFormFile file,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor || !actor.Authority.Capabilities.Contains("DocumentSubmitter")) return StatusCode(403);
        var request = await service.RestrictedAsync(caseId, stageId, actor, ct);
        if (file.Length <= 0) return BadRequest(new { error = "A decision document is required." });
        var id = Guid.NewGuid(); var version = Guid.NewGuid();
        using var stream = file.OpenReadStream();
        var receipt = await _contentWriter.WriteAsync(stream, file.FileName, file.ContentType, id, version, ct);
        var document = await _registerDocHandler.HandleAsync(new RegisterGovernedDocumentCommand(caseId,
            WorkflowExecutionService.DocumentCategory(request.ExecutionId, stageId), receipt, actor.Authority.ActorId, actor.Authority, id, version), ct);
        return Ok(new { documentId = document.Id, versionId = version, status = "SubmittedEvidence", detail = "Upload does not establish institutional approval." });
    }
    [HttpPost("{caseId:guid}/institutional-requests/{stageId:guid}/evidence")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> DistributeInstitutionEvidence(Guid caseId, Guid stageId, [FromForm] IFormFile file,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "CaseEvidenceDistributor") is not { } authority ||
            !authority.Capabilities.Contains("DocumentSubmitter")) return StatusCode(403);
        var state = await service.GetAsync(caseId, ct);
        if (!state.ExecutionId.HasValue || !state.Stages.Any(s => s.Id == stageId)) return NotFound();
        if (file.Length <= 0) return BadRequest();
        var id = Guid.NewGuid(); var version = Guid.NewGuid();
        using var stream = file.OpenReadStream();
        var receipt = await _contentWriter.WriteAsync(stream, file.FileName, file.ContentType, id, version, ct);
        var document = await _registerDocHandler.HandleAsync(new RegisterGovernedDocumentCommand(caseId,
            $"WorkflowEvidence:{state.ExecutionId.Value:N}:{stageId:N}", receipt, authority.ActorId, authority, id, version), ct);
        return Ok(new { documentId = document.Id, versionId = version, status = "ScopedRequestEvidence", detail = "Explicitly distributed evidence; no fact verification is transferred." });
    }
    [HttpGet("{caseId:guid}/institutional-requests/{stageId:guid}/documents/{documentId:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadInstitutionDocument(Guid caseId, Guid stageId, Guid documentId, Guid versionId,
        [FromServices] WorkflowExecutionService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        var request = await service.RestrictedAsync(caseId, stageId, actor, ct);
        var doc = await _documentRepository.GetByIdAsync(new(documentId), ct);
        if (doc == null || doc.LeaseCaseId.Value != caseId ||
            (doc.LogicalCategory != WorkflowExecutionService.DocumentCategory(request.ExecutionId, stageId) &&
             doc.LogicalCategory != $"WorkflowEvidence:{request.ExecutionId:N}:{stageId:N}")) return NotFound();
        var version = doc.Versions.SingleOrDefault(v => v.Id.Value == versionId);
        if (version == null) return NotFound();
        var stream = await _contentReader.ReadContentAsync(doc.Id, version.Id, version.ContentReference.Value, ct);
        return stream == null ? NotFound() : File(stream, version.MediaType, version.OriginalFileName);
    }
}
