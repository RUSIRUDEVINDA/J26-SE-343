namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/fulfillment")]
    public async Task<IActionResult> GetFulfillment(Guid caseId, [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    { if (!IsOfficer()) return StatusCode(403); return Ok(await service.GetAsync(caseId, ct)); }

    [HttpGet("{caseId:guid}/fulfillment/assigned")]
    public async Task<IActionResult> GetAssignedFulfillment(Guid caseId, [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        var c = await _getCaseHandler.HandleAsync(new Application.Queries.GetLeaseCaseByIdQuery(caseId), ct);
        var actor = InstitutionActor(caseId);
        var applicant = c.CreatedByActorId == ResolveActorId();
        if (!applicant && actor == null) return StatusCode(403);
        var institution = applicant ? "APPLICANT" : actor!.Institution;
        var state = await service.GetAsync(caseId, ct);
        var forwardReviewer = actor?.Authority.Capabilities.Contains("FulfillmentForwardReviewer") == true && institution is "PROVINCIAL_LC" or "COMMISSIONER";
        return Ok(new { caseId, state.CaseRevision, requirements = state.Requirements.Where(r => r.Institution == institution ||
                (forwardReviewer && r.Code == "DS_RELEVANT_LAND_REPORT")),
            conditions = state.Conditions.Where(r => r.Institution == institution),
            unconfigured = state.Unconfigured.Where(r => r.Institution == institution) });
    }
    [HttpPost("{caseId:guid}/fulfillment/configure")]
    public async Task<IActionResult> ConfigureFulfillment(Guid caseId, ConfigureProjectFulfillmentRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "FulfillmentConfigurator") is not { } actor) return StatusCode(403);
        await service.ConfigureAsync(caseId, request, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpPost("{caseId:guid}/fulfillment/{requirementId:guid}/submit")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> SubmitFulfillment(Guid caseId, Guid requirementId, [FromForm] IFormFile file,
        [FromForm] int expectedRevision, [FromForm] string? notes,
        [FromServices] ProjectFulfillmentService service, [FromServices] ICorrectionSubmissionTransaction transaction, CancellationToken ct)
    {
        var actorId = ResolveActorId(); var institution = InstitutionActor(caseId);
        var c = await _getCaseHandler.HandleAsync(new Application.Queries.GetLeaseCaseByIdQuery(caseId), ct);
        VerifiedAuthorityContext authority;
        if (c.CreatedByActorId == actorId)
        {
            // Reuse the established owner-checked applicant document-intake authority boundary.
            var now = DateTime.UtcNow;
            authority = new(actorId, ["DocumentSubmitter"], "LeaseCase", caseId.ToString("D"), now.AddSeconds(-1), now, now.AddMinutes(5));
        }
        else if (institution is { } assigned && assigned.Authority.Capabilities.Contains("DocumentSubmitter")) authority = assigned.Authority;
        else return StatusCode(403);
        var record = await service.GetAssignedAsync(caseId, requirementId, actorId, institution?.Institution, ct);
        if (record.Revision != expectedRevision) return Conflict(new { error = "Requirement revision changed." });
        if (file.Length <= 0) return BadRequest();
        var docId = Guid.NewGuid(); var versionId = Guid.NewGuid();
        using var stream = file.OpenReadStream();
        var receipt = await _contentWriter.WriteAsync(stream, file.FileName, file.ContentType, docId, versionId, ct);
        await transaction.ExecuteAsync(async () => {
            await _registerDocHandler.HandleAsync(new RegisterGovernedDocumentCommand(caseId, "ProjectFulfillment:" + record.ClassificationCode,
                receipt, actorId, authority, docId, versionId), ct);
            await service.SubmitAsync(caseId, requirementId, actorId, institution?.Institution, new(docId, versionId), expectedRevision, notes, ct);
            return await service.GetAssignedAsync(caseId, requirementId, actorId, institution?.Institution, ct);
        }, ct);
        return Ok(new { requirementId, documentId = docId, versionId, status = "SubmittedAwaitingReview" });
    }
    [HttpPost("{caseId:guid}/fulfillment/{requirementId:guid}/review")]
    public async Task<IActionResult> ReviewFulfillment(Guid caseId, Guid requirementId, FulfillmentReviewRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "FulfillmentReviewer") is not { } actor) return StatusCode(403);
        await service.ReviewAsync(caseId, requirementId, request, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpGet("{caseId:guid}/fulfillment/{requirementId:guid}/documents/{documentId:guid}/versions/{versionId:guid}/download")]
    public async Task<IActionResult> DownloadFulfillmentEvidence(Guid caseId, Guid requirementId, Guid documentId, Guid versionId,
        [FromServices] ICorrectionRepository records, CancellationToken ct)
    {
        var record = await records.GetAsync(caseId, requirementId, ct);
        if (record?.Purpose != "ProjectFulfillment" || !record.Submissions.Any(s => s.DocumentId == documentId && s.VersionId == versionId)) return NotFound();
        var c = await _getCaseHandler.HandleAsync(new Application.Queries.GetLeaseCaseByIdQuery(caseId), ct);
        var actor = InstitutionActor(caseId);
        var owner = record.AssignedInstitution == "APPLICANT" && c.CreatedByActorId == ResolveActorId();
        var assigned = actor?.Institution == record.AssignedInstitution;
        var forward = record.ClassificationCode == "DS_RELEVANT_LAND_REPORT" && actor?.Institution is "PROVINCIAL_LC" or "COMMISSIONER" &&
            actor!.Authority.Capabilities.Contains("FulfillmentForwardReviewer");
        if (!IsOfficer() && !owner && !assigned && !forward) return StatusCode(403);
        var doc = await _documentRepository.GetByIdAsync(new(documentId), ct);
        if (doc?.LeaseCaseId.Value != caseId || doc.LogicalCategory != "ProjectFulfillment:" + record.ClassificationCode) return NotFound();
        var version = doc.Versions.SingleOrDefault(v => v.Id.Value == versionId);
        if (version == null) return NotFound();
        var stream = await _contentReader.ReadContentAsync(doc.Id, version.Id, version.ContentReference.Value, ct);
        return stream == null ? NotFound() : File(stream, version.MediaType, version.OriginalFileName);
    }
    [HttpPost("{caseId:guid}/fulfillment/{requirementId:guid}/lease-recommendation")]
    public async Task<IActionResult> RecordReportRecommendation(Guid caseId, Guid requirementId, LeaseRecommendationRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.RecordLeaseRecommendationAsync(caseId, requirementId, request, actor, ct); return Ok(new { status = "RecommendationRecorded" });
    }
    [HttpPost("{caseId:guid}/fulfillment/{requirementId:guid}/forward-receipt")]
    public async Task<IActionResult> RecordReportForwarding(Guid caseId, Guid requirementId, FulfillmentForwardRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.RecordForwardingAsync(caseId, requirementId, request, actor, ct); return Ok(new { status = "ExactVersionForwardingReceiptRecorded" });
    }
    [HttpPost("{caseId:guid}/fulfillment/conditions/{conditionId:guid}/fulfill")]
    public async Task<IActionResult> FulfillCaseCondition(Guid caseId, Guid conditionId, FulfillConditionRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.FulfillConditionAsync(caseId, conditionId, request, actor, ct); return Ok(new { status = "ConditionFulfilledAfterReview" });
    }
    public sealed record RecordFinalWorkflowRequest(int ExpectedCaseRevision);
    [HttpPost("{caseId:guid}/fulfillment/final-record")]
    public async Task<IActionResult> RecordFinalWorkflow(Guid caseId, RecordFinalWorkflowRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "WorkflowFinalRecorder") is not { } actor) return StatusCode(403);
        await service.RecordFinalAsync(caseId, request.ExpectedCaseRevision, actor, ct); return Ok(await service.GetAsync(caseId, ct));
    }
    [HttpPost("{caseId:guid}/fulfillment/{requirementId:guid}/waive")]
    public async Task<IActionResult> WaiveFulfillment(Guid caseId, Guid requirementId, WaiveFulfillmentRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.WaiveAsync(caseId, requirementId, request, actor, ct);
        return Ok(new { requirementId, status = "WaivedWithSourceEvidence" });
    }
    [HttpPost("{caseId:guid}/fulfillment/conditions/{conditionId:guid}/waive")]
    public async Task<IActionResult> WaiveFulfillmentCondition(Guid caseId, Guid conditionId, FulfillConditionRequest request,
        [FromServices] ProjectFulfillmentService service, CancellationToken ct)
    {
        if (InstitutionActor(caseId) is not { } actor) return StatusCode(403);
        await service.WaiveConditionAsync(caseId, conditionId, request, actor, ct);
        return Ok(new { status = "ConditionWaivedWithSourceEvidence" });
    }
}
