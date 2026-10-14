using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestParcelRequestSelectorTests
{
    [Fact]
    public void PreferForParcelView_returns_null_when_empty()
    {
        Assert.Null(LeaseInterestParcelRequestSelector.PreferForParcelView([]));
    }

    [Fact]
    public void PreferForParcelView_prefers_draft_over_newer_submitted()
    {
        var parcelId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var submitted = Create(
            parcelId,
            ownerId,
            LeaseInterestRequestStatus.PendingVerification,
            DateTimeOffset.Parse("2026-10-14T12:00:00Z"));
        var draft = Create(
            parcelId,
            ownerId,
            LeaseInterestRequestStatus.Draft,
            DateTimeOffset.Parse("2026-10-13T08:00:00Z"));

        var selected = LeaseInterestParcelRequestSelector.PreferForParcelView([submitted, draft]);

        Assert.Same(draft, selected);
    }

    [Fact]
    public void PreferForParcelView_returns_latest_when_only_submitted_exist()
    {
        var parcelId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var older = Create(
            parcelId,
            ownerId,
            LeaseInterestRequestStatus.PendingVerification,
            DateTimeOffset.Parse("2026-10-01T00:00:00Z"));
        var newer = Create(
            parcelId,
            ownerId,
            LeaseInterestRequestStatus.NeedsInformation,
            DateTimeOffset.Parse("2026-10-10T00:00:00Z"));

        var selected = LeaseInterestParcelRequestSelector.PreferForParcelView([older, newer]);

        Assert.Same(newer, selected);
    }

    [Fact]
    public void PreferForParcelView_repeated_opens_keep_same_draft()
    {
        var parcelId = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        var draft = Create(
            parcelId,
            ownerId,
            LeaseInterestRequestStatus.Draft,
            DateTimeOffset.Parse("2026-10-12T00:00:00Z"));

        var first = LeaseInterestParcelRequestSelector.PreferForParcelView([draft]);
        var second = LeaseInterestParcelRequestSelector.PreferForParcelView([draft]);

        Assert.Same(draft, first);
        Assert.Same(first, second);
        Assert.Equal(draft.Id, second!.Id);
    }

    private static LeaseInterestRequestEntity Create(
        Guid parcelId,
        Guid ownerId,
        LeaseInterestRequestStatus status,
        DateTimeOffset updatedAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            LandParcelId = parcelId,
            OwnerId = ownerId,
            Status = status,
            PayloadJson = "{}",
            RequestedPurpose = (int)LandUseType.Other,
            CreatedAtUtc = updatedAtUtc.AddDays(-1),
            UpdatedAtUtc = updatedAtUtc,
        };
}
