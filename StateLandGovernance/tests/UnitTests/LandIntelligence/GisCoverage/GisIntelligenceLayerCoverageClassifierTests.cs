using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisCoverage;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisCoverage;

public sealed class GisIntelligenceLayerCoverageClassifierTests
{
    [Fact]
    public void ClassifyLayer_WhenNoReferenceAndNoEvidence_ReturnsUnavailable()
    {
        var status = GisIntelligenceLayerCoverageClassifier.ClassifyLayer(300, 0, 0);
        Assert.Equal(GisIntelligenceCoverageStatus.Unavailable, status);
    }

    [Fact]
    public void ClassifyLayer_WhenReferenceWithoutParcelEvidence_ReturnsUnavailable()
    {
        var status = GisIntelligenceLayerCoverageClassifier.ClassifyLayer(300, 1200, 0);
        Assert.Equal(GisIntelligenceCoverageStatus.Unavailable, status);
    }

    [Fact]
    public void ClassifyLayer_WhenPartialParcelEvidence_ReturnsPartial()
    {
        var status = GisIntelligenceLayerCoverageClassifier.ClassifyLayer(300, 1200, 42);
        Assert.Equal(GisIntelligenceCoverageStatus.Partial, status);
    }

    [Fact]
    public void ClassifyOverallEnrichment_WhenNoSnapshots_ReturnsUnavailable()
    {
        var status = GisIntelligenceLayerCoverageClassifier.ClassifyOverallEnrichment(300, 0, 0, 0);
        Assert.Equal(GisIntelligenceCoverageStatus.Unavailable, status);
    }
}
