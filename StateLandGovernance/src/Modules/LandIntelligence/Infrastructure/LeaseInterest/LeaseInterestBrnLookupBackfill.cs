using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

/// <summary>Backfills BRN lookup digests for organization applicants saved before BRN indexing existed.</summary>
public static class LeaseInterestBrnLookupBackfill
{
    public static async Task ApplyAsync(
        LandIntelligenceDbContext dbContext,
        INicPersonalDataProtector nicProtector,
        CancellationToken cancellationToken = default)
    {
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return;
        }

        var candidates = await dbContext.LeaseInterestRequests
            .Where(x =>
                x.ApplicantBrnLookupDigest == null
                && x.Status != LeaseInterestRequestStatus.Draft)
            .Select(x => new { x.Id, x.PayloadJson })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            return;
        }

        var updates = new List<(Guid Id, string Digest)>();
        foreach (var row in candidates)
        {
            var draft = LeaseInterestPayloadSerializer.Deserialize(row.PayloadJson);
            if (draft.Applicant.Kind != LeaseInterestApplicantKindDto.Organization)
            {
                continue;
            }

            if (!LeaseInterestBusinessRegistration.TryNormalize(
                    draft.Applicant.OrganizationRegistrationNumber,
                    out var normalizedBrn))
            {
                continue;
            }

            updates.Add((row.Id, nicProtector.ComputeLookupDigest(normalizedBrn)));
        }

        if (updates.Count == 0)
        {
            return;
        }

        foreach (var group in updates.GroupBy(x => x.Digest))
        {
            var ids = group.Select(x => x.Id).ToList();
            await dbContext.LeaseInterestRequests
                .Where(x => ids.Contains(x.Id))
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        x => x.ApplicantBrnLookupDigest,
                        group.Key),
                    cancellationToken);
        }
    }
}
