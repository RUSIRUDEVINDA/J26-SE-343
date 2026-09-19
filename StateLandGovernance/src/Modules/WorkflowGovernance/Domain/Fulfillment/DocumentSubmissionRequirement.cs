namespace StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class DocumentSubmissionRequirement
{
    public Guid Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public DocumentClassificationCode DocumentClassificationCode { get; }
    public DateTime DueDateUtc { get; }
    public FulfillmentStatus Status { get; private set; }
    public GovernedDocumentId? FulfilledByDocumentId { get; private set; }
    public DateTime? FulfilledAtUtc { get; private set; }

    public DocumentSubmissionRequirement(
        Guid id,
        LeaseCaseId leaseCaseId,
        DocumentClassificationCode documentClassificationCode,
        DateTime dueDateUtc,
        FulfillmentStatus status = FulfillmentStatus.Pending,
        GovernedDocumentId? fulfilledByDocumentId = null,
        DateTime? fulfilledAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidFulfillmentException("DocumentSubmissionRequirement Id cannot be empty.");
        }

        if (documentClassificationCode == null)
        {
            throw new InvalidFulfillmentException("DocumentClassificationCode cannot be null.");
        }

        Id = id;
        LeaseCaseId = leaseCaseId;
        DocumentClassificationCode = documentClassificationCode;
        DueDateUtc = dueDateUtc;
        Status = status;
        FulfilledByDocumentId = fulfilledByDocumentId;
        FulfilledAtUtc = fulfilledAtUtc;
    }

    public void FulfillWithDocument(GovernedDocumentId documentId, DateTime fulfilledAt)
    {
        if (documentId == default || documentId.Value == Guid.Empty)
        {
            throw new InvalidFulfillmentException("GovernedDocumentId cannot be empty.");
        }

        if (Status == FulfillmentStatus.Fulfilled)
        {
            throw new InvalidFulfillmentException($"Document submission requirement '{Id}' is already fulfilled.");
        }

        if (Status == FulfillmentStatus.Waived)
        {
            throw new InvalidFulfillmentException($"Document submission requirement '{Id}' has been waived and cannot be fulfilled.");
        }

        FulfilledByDocumentId = documentId;
        Status = FulfillmentStatus.Fulfilled;
        FulfilledAtUtc = fulfilledAt;
    }
}
