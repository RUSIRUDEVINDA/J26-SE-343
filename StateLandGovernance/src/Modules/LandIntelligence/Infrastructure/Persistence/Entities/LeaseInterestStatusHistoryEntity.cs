using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class LeaseInterestStatusHistoryEntity
{
    public Guid Id { get; set; }

    public Guid LeaseInterestRequestId { get; set; }

    public LeaseInterestRequestStatus Status { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    public string? Note { get; set; }

    public LeaseInterestStatusActorKind ActorKind { get; set; }

    public Guid? ActorId { get; set; }

    public bool VisibleToApplicant { get; set; } = true;

    public LeaseInterestRequestEntity? Request { get; set; }
}
