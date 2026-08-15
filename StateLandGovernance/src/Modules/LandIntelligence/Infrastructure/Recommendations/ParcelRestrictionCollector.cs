using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

internal static class ParcelRestrictionCollector
{
    public static IReadOnlyList<RestrictionSummaryDto> Collect(LandParcel parcel)
    {
        var restrictions = new List<RestrictionSummaryDto>();

        restrictions.AddRange(parcel.SpatialConstraints.Select(c => new RestrictionSummaryDto(
            c.Type.ToString(),
            c.Description,
            c.Severity,
            "SpatialConstraint")));

        restrictions.AddRange(parcel.EnvironmentalRestrictions.Select(r => new RestrictionSummaryDto(
            r.Type.ToString(),
            r.Description,
            r.Severity,
            "EnvironmentalRestriction")));

        restrictions.AddRange(parcel.RegulatoryReferences.Select(r => new RestrictionSummaryDto(
            "RegulatoryReference",
            $"{r.Title} ({r.GazetteNumber})",
            RestrictionSeverity.Low,
            "RegulatoryReference")));

        return restrictions;
    }
}
