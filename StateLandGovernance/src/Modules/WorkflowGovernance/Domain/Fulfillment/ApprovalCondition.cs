namespace StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.WorkflowPlanning;

public sealed class ApprovalCondition
{
    public Guid Id { get; }
    public LeaseCaseId LeaseCaseId { get; }
    public InstitutionCode InstitutionCode { get; }
    public string Description { get; }
    public FulfillmentStatus Status { get; private set; }
    public DateTime? FulfilledAtUtc { get; private set; }

    public ApprovalCondition(
        Guid id,
        LeaseCaseId leaseCaseId,
        InstitutionCode institutionCode,
        string description,
        FulfillmentStatus status = FulfillmentStatus.Pending,
        DateTime? fulfilledAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new InvalidFulfillmentException("ApprovalCondition Id cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new InvalidFulfillmentException("Description cannot be empty.");
        }

        Id = id;
        LeaseCaseId = leaseCaseId;
        InstitutionCode = institutionCode;
        Description = description.Trim();
        Status = status;
        FulfilledAtUtc = fulfilledAtUtc;
    }

    public void Fulfill(DateTime fulfilledAt)
    {
        if (Status == FulfillmentStatus.Fulfilled)
        {
            throw new InvalidFulfillmentException($"ApprovalCondition '{Id}' is already fulfilled.");
        }

        if (Status == FulfillmentStatus.Waived)
        {
            throw new InvalidFulfillmentException($"ApprovalCondition '{Id}' has been waived and cannot be fulfilled.");
        }

        Status = FulfillmentStatus.Fulfilled;
        FulfilledAtUtc = fulfilledAt;
    }
}
