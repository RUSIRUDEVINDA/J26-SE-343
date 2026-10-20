namespace StateLandGovernance.WorkflowGovernance.Presentation.Controllers;
using Microsoft.AspNetCore.Mvc;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed partial class CasesController
{
    [HttpGet("{caseId:guid}/completeness/proposal/{assessmentId:guid}/observations")]
    public async Task<IActionResult> ReadProposalObservations(Guid caseId, Guid assessmentId, [FromServices] Application.Interfaces.ICorrectionRepository records, CancellationToken ct)
    {
        if (!IsOfficer()) return StatusCode(403);
        var c = await _leaseCaseRepository.GetByIdAsync(new(caseId), ct);
        var result = c?.ProposalContentAssessmentHistory.SingleOrDefault(a => a.Id.Value == assessmentId);
        if (result == null) return NotFound();
        var analysis = await _getAnalysisHandler.HandleAsync(new Application.Queries.GetDocumentAnalysisByVersionIdQuery(result.SourceBinding.DocumentVersionId.Value), ct);
        if (analysis.ActiveRun?.Id.ToString("D") != result.SourceBinding.ExtractionReference) return Conflict(new { error = "Assessment analysis run is stale." });
        var template = await _proposalTemplateProvider.GetTemplateAsync(result.SourceBinding.TemplateId, result.SourceBinding.TemplateVersion, ct);
        var dto = Application.Mappings.ProposalContentMapper.ToDto(result);
        // Confirmed results point to the exact result they reviewed; never borrow from another run/template.
        var reviewedResult = result;
        var visited = new HashSet<Guid>();
        while (reviewedResult.ConfirmedResultOfId is { } parentId && visited.Add(reviewedResult.Id.Value))
        {
            var parent = c!.ProposalContentAssessmentHistory.SingleOrDefault(a => a.Id == parentId);
            if (parent == null) break;
            reviewedResult = parent;
        }
        var recordId = reviewedResult.Id.Value;
        var saved = await records.GetAsync(caseId, recordId, ct);
        var reviewed = saved?.Purpose == "ProposalReviewObservations" && saved.PayloadJson != null
            ? System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<ProposalObservationDto>>(saved.PayloadJson,
                new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)) : null;
        return Ok(new { assessmentId, dto.SourceBinding, template = dto.TemplateSnapshot, observationSource = reviewed != null ? "SavedOfficerReview" : "MachineSuggestions",
            savedReviewActorId = reviewed != null ? saved!.RequestedBy : (Guid?)null,
            savedReviewAtUtc = reviewed != null ? saved!.CreatedAtUtc : (DateTime?)null,
            observations = reviewed ?? (_templateMapping == null || template == null ? null : await _templateMapping.ReadCachedAsync(analysis, template, ct)) });
    }
    public sealed record ConfirmProposalRequest(int ExpectedCaseRevision, string Notes);
    public sealed record CorrectProposalRequest(int ExpectedCaseRevision, string Reason, string EvidenceReference,
        IReadOnlyList<ProposalObservationDto> Observations);
    public sealed record ReviewClassificationRequest(Guid ClassifiedDocumentId, Guid DocumentId, int ExpectedAssessmentRevision,
        string Decision, string? CorrectedClassificationCode, string? Reason);

    [HttpPost("{caseId:guid}/completeness/proposal/{assessmentId:guid}/confirm")]
    public async Task<IActionResult> ConfirmProposalAssessment(Guid caseId, Guid assessmentId, ConfirmProposalRequest request,
        [FromServices] CompletenessReviewService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "ProposalReviewer") is not { } authority) return StatusCode(403);
        return Ok(await service.ConfirmAsync(new(caseId, assessmentId, authority.ActorId, authority, request.ExpectedCaseRevision, request.Notes), ct));
    }
    [HttpPost("{caseId:guid}/completeness/proposal/{assessmentId:guid}/correct")]
    public async Task<IActionResult> CorrectProposalAssessment(Guid caseId, Guid assessmentId, CorrectProposalRequest request,
        [FromServices] CompletenessReviewService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "ProposalReviewer") is not { } authority) return StatusCode(403);
        return Ok(await service.CorrectAsync(new(caseId, assessmentId, authority.ActorId, authority, request.ExpectedCaseRevision,
            request.Reason, request.EvidenceReference, request.Observations), ct));
    }
    [HttpPost("{caseId:guid}/completeness/documents/{assessmentId:guid}/review")]
    public async Task<IActionResult> ReviewDocumentAssessment(Guid caseId, Guid assessmentId, ReviewClassificationRequest request,
        [FromServices] CompletenessReviewService service, CancellationToken ct)
    {
        if (!IsOfficer() || ExecutionAuthority(caseId, "ClassificationReviewer") is not { } authority ||
            !User.HasClaim("authority_document_id", request.DocumentId.ToString("D"))) return StatusCode(403);
        // The identity explicitly grants this document scope; a case role/body cannot manufacture it.
        authority = authority with { ScopeKind = "GovernedDocument", ScopeTargetIdentifier = request.DocumentId.ToString("D") };
        return Ok(await service.ReviewClassificationAsync(caseId, new(assessmentId, request.ClassifiedDocumentId,
            request.Decision, request.CorrectedClassificationCode, request.Reason, authority.ActorId, authority,
            request.ExpectedAssessmentRevision), ct));
    }
}
