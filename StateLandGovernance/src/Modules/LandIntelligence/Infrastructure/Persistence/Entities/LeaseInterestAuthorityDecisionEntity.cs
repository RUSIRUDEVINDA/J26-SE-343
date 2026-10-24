using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

/// <summary>
/// Immutable authority decision record. Append-only; recording a decision does not create a lease.
/// </summary>
public sealed class LeaseInterestAuthorityDecisionEntity
{
    public Guid Id { get; set; }

    public Guid LeaseInterestRequestId { get; set; }

    public LeaseInterestAuthorityDecisionKind Decision { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? ReviewedValuationId { get; set; }

    public int? ReviewedValuationRevision { get; set; }

    public LeaseInterestRequestStatus ResultingStatus { get; set; }

    public Guid RecordedByAuthorityId { get; set; }

    public DateTimeOffset RecordedAtUtc { get; set; }

    public LeaseInterestRequestEntity? Request { get; set; }

    public LeaseInterestValuationEntity? ReviewedValuation { get; set; }
}
