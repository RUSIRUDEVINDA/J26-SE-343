using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

public sealed class LeaseInterestStatusHistoryEntity
{
    public Guid Id { get; set; }

    public Guid LeaseInterestRequestId { get; set; }

    public LeaseInterestRequestStatus Status { get; set; }

    public DateTimeOffset ChangedAtUtc { get; set; }

    public string? Note { get; set; }

    public LeaseInterestRequestEntity? Request { get; set; }
}
