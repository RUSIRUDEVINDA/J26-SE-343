using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.GisCoverage;

public static class GisIntelligenceLayerCoverageClassifier
{
    public static GisIntelligenceCoverageStatus ClassifyLayer(
        int totalParcelCount,
        int referenceFeatureCount,
        int parcelsWithEvidenceCount)
    {
        if (totalParcelCount <= 0)
        {
            return GisIntelligenceCoverageStatus.Unavailable;
        }

        if (referenceFeatureCount <= 0 && parcelsWithEvidenceCount <= 0)
        {
            return GisIntelligenceCoverageStatus.Unavailable;
        }

        if (referenceFeatureCount > 0 && parcelsWithEvidenceCount <= 0)
        {
            return GisIntelligenceCoverageStatus.Unavailable;
        }

        if (parcelsWithEvidenceCount >= totalParcelCount)
        {
            return GisIntelligenceCoverageStatus.Available;
        }

        return GisIntelligenceCoverageStatus.Partial;
    }

    public static GisIntelligenceCoverageStatus ClassifyOverallEnrichment(
        int totalParcelCount,
        int enrichmentSnapshotCount,
        int completeCount,
        int partialCount)
    {
        if (totalParcelCount <= 0 || enrichmentSnapshotCount <= 0)
        {
            return GisIntelligenceCoverageStatus.Unavailable;
        }

        if (completeCount >= totalParcelCount)
        {
            return GisIntelligenceCoverageStatus.Available;
        }

        if (completeCount > 0 || partialCount > 0)
        {
            return GisIntelligenceCoverageStatus.Partial;
        }

        return GisIntelligenceCoverageStatus.Unavailable;
    }

    public static string BuildLayerLimitation(
        GisIntelligenceCoverageStatus status,
        int totalParcelCount,
        int referenceFeatureCount,
        int parcelsWithEvidenceCount,
        string missingReferenceNote)
    {
        return status switch
        {
            GisIntelligenceCoverageStatus.Unavailable when referenceFeatureCount <= 0 =>
                $"{missingReferenceNote} No parcel-level GIS evidence is stored for this layer.",
            GisIntelligenceCoverageStatus.Unavailable when parcelsWithEvidenceCount <= 0 =>
                "Reference data may be present, but parcel GIS enrichment has not produced evidence for this layer in the connected database.",
            GisIntelligenceCoverageStatus.Partial =>
                $"Evidence exists for {parcelsWithEvidenceCount} of {totalParcelCount} indexed parcels. Missing parcels are not treated as assessed.",
            GisIntelligenceCoverageStatus.Available =>
                $"GIS evidence is stored for all {totalParcelCount} indexed parcels in this database (does not imply official verification).",
            _ => "Coverage could not be determined.",
        };
    }
}
