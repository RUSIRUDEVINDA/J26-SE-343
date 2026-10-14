using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

/// <summary>
/// Chooses which existing lease-interest request to surface when opening a parcel form.
/// Prefer an in-progress draft; otherwise the most recently updated request.
/// </summary>
public static class LeaseInterestParcelRequestSelector
{
    public static LeaseInterestRequestEntity? PreferForParcelView(
        IEnumerable<LeaseInterestRequestEntity> requests)
    {
        var list = requests as IList<LeaseInterestRequestEntity> ?? requests.ToList();
        if (list.Count == 0)
        {
            return null;
        }

        var draft = list
            .Where(x => x.Status == LeaseInterestRequestStatus.Draft)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefault();
        if (draft is not null)
        {
            return draft;
        }

        return list.OrderByDescending(x => x.UpdatedAtUtc).First();
    }
}
