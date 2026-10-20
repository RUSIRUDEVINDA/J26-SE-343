namespace StateLandGovernance.WorkflowGovernance.Domain.Fulfillment;

using StateLandGovernance.BuildingBlocks.Events;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record FulfillmentWaived(Guid EventId, DateTime OccurredOn, LeaseCaseId LeaseCaseId,
    Guid ObligationId, bool IsCondition, Guid ActorId, string Reason, string EvidenceReference,
    int CaseRevision) : IDomainEvent;
