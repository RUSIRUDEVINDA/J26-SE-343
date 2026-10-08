using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Mappings;
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
            "EnvironmentalRestriction",
            AttributeProvenanceMapper.ToDto(
                ParcelAttributeProvenanceResolver.ResolveOrUnknown(r.DataProvenance)))));

        restrictions.AddRange(parcel.RegulatoryReferences.Select(r => new RestrictionSummaryDto(
            "RegulatoryReference",
            $"{r.Title} ({r.GazetteNumber})",
            RestrictionSeverity.Low,
            "RegulatoryReference",
            AttributeProvenanceMapper.ToDto(
                ParcelAttributeProvenanceResolver.ResolveOrUnknown(r.DataProvenance)))));

        return restrictions;
    }
}
