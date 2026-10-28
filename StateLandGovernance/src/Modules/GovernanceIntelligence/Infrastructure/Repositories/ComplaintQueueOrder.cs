using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;

public static class ComplaintQueueOrder
{
    public static IOrderedQueryable<RegisteredComplaintEntity> Apply(
        IQueryable<RegisteredComplaintEntity> rows, RegisteredComplaintEntity? after = null)
    {
        if (after != null)
        {
            var date = after.SubmittedAtUtc;
            var id = after.Id;
            rows = rows.Where(x => x.SubmittedAtUtc < date ||
                (x.SubmittedAtUtc == date && x.Id.CompareTo(id) > 0));
        }
        return rows.OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id);
    }
}
