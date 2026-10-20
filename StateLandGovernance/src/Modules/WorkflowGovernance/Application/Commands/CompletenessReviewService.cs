namespace StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class CompletenessReviewService(ILeaseCaseRepository cases, IGovernedDocumentRepository documents,
    IDocumentAnalysisRepository analyses, IDocumentCompletenessAssessmentRepository assessments,
    IProposalTemplateProvider templates, ICommandHandler<ConfirmProposalContentAssessmentCommand, ProposalContentAssessmentDto> confirm,
    ICommandHandler<CorrectProposalContentAssessmentCommand, ProposalContentAssessmentDto> correct,
    ICommandHandler<ReviewDocumentClassificationCommand, DocumentCompletenessAssessmentDto> classify)
{
    private async Task CurrentProposalAsync(Guid caseId, Guid assessmentId, CancellationToken ct)
    {
        var c = await cases.GetByIdAsync(new(caseId), ct) ?? throw new InvalidOperationException("Case not found.");
        var assessment = c.ProposalContentAssessmentHistory.SingleOrDefault(a => a.Id.Value == assessmentId)
            ?? throw new InvalidOperationException("Assessment not found on this case.");
        var binding = assessment.SourceBinding;
        var doc = await documents.GetByIdAsync(binding.ProposalDocumentId, ct);
        if (doc == null || doc.LeaseCaseId.Value != caseId || doc.LogicalCategory != "Proposal" || doc.ActiveVersionId != binding.DocumentVersionId ||
            doc.Versions.Single(v => v.Id == doc.ActiveVersionId).Checksum != binding.DocumentChecksum)
            throw new InvalidOperationException("Proposal assessment source is stale or not the selected Proposal.");
        var analysis = await analyses.GetByDocumentVersionIdAsync(binding.DocumentVersionId, ct);
        var run = analysis?.Runs.OrderBy(r => r.RunNumber).LastOrDefault();
        if (run?.State.ToString() != "Completed" || run.Id.Value.ToString("D") != binding.ExtractionReference)
            throw new InvalidOperationException("Proposal assessment's exact analysis run is no longer current.");
        var template = await templates.GetTemplateAsync(binding.TemplateId, binding.TemplateVersion, ct);
        if (template?.DefinitionDigest != assessment.TemplateSnapshot.DefinitionDigest)
            throw new InvalidOperationException("Proposal template binding is stale.");
    }
    public async Task<ProposalContentAssessmentDto> ConfirmAsync(ConfirmProposalContentAssessmentCommand command, CancellationToken ct)
    { await CurrentProposalAsync(command.LeaseCaseId, command.AssessmentResultId, ct); return await confirm.HandleAsync(command, ct); }
    public async Task<ProposalContentAssessmentDto> CorrectAsync(CorrectProposalContentAssessmentCommand command, CancellationToken ct)
    { await CurrentProposalAsync(command.LeaseCaseId, command.AssessmentResultId, ct); return await correct.HandleAsync(command, ct); }
    public async Task<DocumentCompletenessAssessmentDto> ReviewClassificationAsync(Guid caseId, ReviewDocumentClassificationCommand command, CancellationToken ct)
    {
        var assessment = await assessments.GetByIdAsync(new(command.AssessmentId), ct) ?? throw new InvalidOperationException("Assessment not found.");
        if (assessment.LeaseCaseId.Value != caseId) throw new InvalidOperationException("Assessment is on another case.");
        var classified = assessment.ClassifiedDocuments.SingleOrDefault(d => d.Id.Value == command.ClassifiedDocumentId)
            ?? throw new InvalidOperationException("Classification not found in assessment.");
        var doc = await documents.GetByIdAsync(classified.DocumentBinding.GovernedDocumentId, ct);
        if (doc == null || doc.LeaseCaseId.Value != caseId || doc.ActiveVersionId != classified.DocumentBinding.DocumentVersionId)
            throw new InvalidOperationException("Document classification source is stale.");
        return await classify.HandleAsync(command, ct);
    }
}
