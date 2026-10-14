using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

namespace StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Mappings;

internal static class ParcelGisDerivedIntelligencePersistenceMapper
{
    public static ParcelGisDerivedIntelligence? ToDomain(
        LandParcelGisEnrichmentSnapshotEntity? snapshot,
        ParcelDerivedSoilGroupEntity? derivedSoilGroup)
    {
        if (snapshot is null && derivedSoilGroup is null)
        {
            return null;
        }

        ParcelDerivedSoilGroupEvidence? soilEvidence = null;
        if (derivedSoilGroup is not null)
        {
            soilEvidence = new ParcelDerivedSoilGroupEvidence(
                derivedSoilGroup.SoilGroupName,
                derivedSoilGroup.OverlapPercentage,
                AttributeProvenancePersistenceMapper.Deserialize(derivedSoilGroup.ProvenanceJson));
        }

        return new ParcelGisDerivedIntelligence(
            snapshot is null
                ? GisEnrichmentOverallStatus.Unavailable
                : MapEnrichmentStatus(snapshot.OverallStatus),
            soilEvidence);
    }

    private static GisEnrichmentOverallStatus MapEnrichmentStatus(
        LandParcelGisEnrichmentOverallStatus status) =>
        status switch
        {
            LandParcelGisEnrichmentOverallStatus.Complete => GisEnrichmentOverallStatus.Complete,
            LandParcelGisEnrichmentOverallStatus.Partial => GisEnrichmentOverallStatus.Partial,
            _ => GisEnrichmentOverallStatus.Unavailable
        };
}
