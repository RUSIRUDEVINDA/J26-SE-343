namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

/// <summary>Administrative intake and review only. Completeness is always reassessed separately.</summary>
public sealed class CorrectionWorkflowService(ICorrectionRepository corrections, ILeaseCaseRepository cases,
    IGovernedDocumentRepository documents, IWorkflowGovernanceUnitOfWork unitOfWork, TimeProvider clock,
    ICorrectionSubmissionTransaction transaction)
{
    public Task<CorrectionRecord> RegisterAndSubmitAsync(Guid caseId, Guid requestId, Guid actor,
        Func<Task<GovernedDocumentDto>> registerDocument, string? notes, int expectedRevision, CancellationToken ct)
        => transaction.ExecuteAsync(async () =>
        {
            var document = await registerDocument();
            return await SubmitAsync(caseId, requestId, actor, document.Id, document.ActiveVersionId, notes, expectedRevision, ct);
        }, ct);

    public async Task<CorrectionRecord> CreateAsync(Guid caseId, Guid officer, string action,
        string classification, string? sectionId, string instructions, DateTime dueDate,
        CancellationToken cancellationToken)
    {
        if (action is not ("UploadSupportingDocument" or "ReviseProposal") || string.IsNullOrWhiteSpace(instructions))
            throw new ArgumentException("A supported correction action and officer instructions are required.");
        if (dueDate.Kind != DateTimeKind.Utc) throw new ArgumentException("The due date must be UTC.");
        if (action == "ReviseProposal") classification = "Proposal";
        var leaseCase = await cases.GetByIdAsync(new LeaseCaseId(caseId), cancellationToken)
            ?? throw new ArgumentException("Case not found.");
        var requirement = leaseCase.AddDocumentSubmissionRequirement(new DocumentClassificationCode(classification), dueDate);
        var record = new CorrectionRecord { Id = requirement.Id, LeaseCaseId = caseId, RequestedBy = officer,
            CreatedAtUtc = clock.GetUtcNow().UtcDateTime, DueDateUtc = dueDate, RequiredAction = action,
            ClassificationCode = classification, SectionId = sectionId, Instructions = instructions };
        await corrections.SaveAsync(record, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return record;
    }

    public async Task<CorrectionRecord> SubmitAsync(Guid caseId, Guid requestId, Guid actor,
        Guid documentId, Guid versionId, string? notes, int expectedRevision, CancellationToken cancellationToken)
    {
        var record = await GetPendingAsync(caseId, requestId, expectedRevision, cancellationToken);
        var leaseCase = await cases.GetByIdAsync(new LeaseCaseId(caseId), cancellationToken)
            ?? throw new ArgumentException("Case not found.");
        if (leaseCase.CreatedByActorId != actor) throw new UnauthorizedAccessException("Only the applicant can submit a correction.");
        var document = await documents.GetByIdAsync(new GovernedDocumentId(documentId), cancellationToken)
            ?? throw new ArgumentException("Document not found.");
        if (document.LeaseCaseId.Value != caseId || document.ActiveVersionId.Value != versionId)
            throw new ArgumentException("Correction must reference a current document version belonging to this case.");
        if (!string.Equals(document.LogicalCategory, record.ClassificationCode, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The document category does not match the correction request.");
        record.Submissions.Add(new(documentId, versionId, actor, clock.GetUtcNow().UtcDateTime, notes));
        record.Status = "SubmittedAwaitingReview";
        record.Revision++;
        await corrections.SaveAsync(record, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return record;
    }

    public async Task<CorrectionRecord> ReviewAsync(Guid caseId, Guid requestId, Guid officer,
        bool accept, string reason, int expectedRevision, CancellationToken cancellationToken)
    {
        var record = await GetPendingAsync(caseId, requestId, expectedRevision, cancellationToken);
        if (record.Status != "SubmittedAwaitingReview" || string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("An awaiting submission and review reason are required.");
        var submission = record.Submissions.Last();
        var document = await documents.GetByIdAsync(new GovernedDocumentId(submission.DocumentId), cancellationToken);
        if (document is null || document.LeaseCaseId.Value != caseId || document.ActiveVersionId.Value != submission.VersionId)
            throw new InvalidOperationException("The submitted version is stale. Submit the current version for review.");
        var now = clock.GetUtcNow().UtcDateTime;
        if (accept)
        {
            var leaseCase = await cases.GetByIdAsync(new LeaseCaseId(caseId), cancellationToken)
                ?? throw new ArgumentException("Case not found.");
            leaseCase.FulfillDocumentRequirement(requestId, new GovernedDocumentId(submission.DocumentId), now);
        }
        record.Submissions[^1] = submission with { ReviewedBy = officer, ReviewedAtUtc = now,
            Decision = accept ? "Accepted" : "Rejected", ReviewReason = reason };
        record.Status = accept ? "AcceptedAwaitingReassessment" : "ChangesRequested";
        record.Revision++;
        await corrections.SaveAsync(record, cancellationToken);
        await unitOfWork.CommitAsync(cancellationToken);
        return record;
    }

    private async Task<CorrectionRecord> GetPendingAsync(Guid caseId, Guid requestId, int revision, CancellationToken ct)
    {
        var record = await corrections.GetAsync(caseId, requestId, ct) ?? throw new ArgumentException("Correction not found.");
        if (record.Purpose != "Correction") throw new ArgumentException("This record requires the governed fulfillment API.");
        if (record.Revision != revision) throw new InvalidOperationException("Correction revision conflict. Reload before retrying.");
        if (record.Status == "AcceptedAwaitingReassessment") throw new InvalidOperationException("Correction is already reviewed.");
        return record;
    }
}
