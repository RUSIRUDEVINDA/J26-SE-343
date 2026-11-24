using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

internal static class LeaseInterestWorkflowIntegrationTestCleanup
{
    public const string TestParcelCadastralPrefix = "LI-E2E-";

    public static async Task RemoveTestParcelsAsync(
        LandIntelligenceDbContext db,
        IReadOnlyCollection<Guid> parcelIds,
        CancellationToken cancellationToken = default)
    {
        var ids = parcelIds.Count > 0
            ? parcelIds.ToArray()
            : await db.LandParcels
                .Where(p => EF.Functions.Like(p.CadastralNumber, TestParcelCadastralPrefix + "%"))
                .Select(p => p.Id)
                .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return;
        }

        var requestIds = await db.LeaseInterestRequests
            .Where(r => ids.Contains(r.LandParcelId))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (requestIds.Count > 0)
        {
            var valuations = await db.LeaseInterestValuations
                .Where(v => requestIds.Contains(v.LeaseInterestRequestId))
                .ToListAsync(cancellationToken);
            db.LeaseInterestValuations.RemoveRange(valuations);

            var authorityDecisions = await db.LeaseInterestAuthorityDecisions
                .Where(d => requestIds.Contains(d.LeaseInterestRequestId))
                .ToListAsync(cancellationToken);
            db.LeaseInterestAuthorityDecisions.RemoveRange(authorityDecisions);

            var requests = await db.LeaseInterestRequests
                .Where(r => requestIds.Contains(r.Id))
                .ToListAsync(cancellationToken);
            db.LeaseInterestRequests.RemoveRange(requests);
        }

        var parcels = await db.LandParcels
            .Where(p => ids.Contains(p.Id))
            .ToListAsync(cancellationToken);
        db.LandParcels.RemoveRange(parcels);

        await db.SaveChangesAsync(cancellationToken);
    }
}
