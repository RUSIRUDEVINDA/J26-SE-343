using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.GisAdministrativeVerification;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.GisAdministrativeVerification;

public sealed class SoilGroupEnrichmentEvaluatorTests
{
    [Fact]
    public void SelectPrimaryOverlap_chooses_largest_overlap_area()
    {
        var overlaps = new List<SoilGroupOverlapCandidate>
        {
            new(Guid.NewGuid(), "Group A", "src", "layer", 1000, 25m),
            new(Guid.NewGuid(), "Group B", "src", "layer", 3000, 75m),
            new(Guid.NewGuid(), "Group C", "src", "layer", 500, 12.5m)
        };

        var primary = SoilGroupEnrichmentEvaluator.SelectPrimaryOverlap(overlaps);

        Assert.NotNull(primary);
        Assert.Equal("Group B", primary!.SoilGroupName);
        Assert.Equal(75m, primary.OverlapPercentage);
    }

    [Fact]
    public void ShouldPreserveOfficialSoilType_returns_true_only_for_official_provenance()
    {
        Assert.True(SoilGroupEnrichmentEvaluator.ShouldPreserveOfficialSoilType(AttributeProvenanceSourceType.Official));
        Assert.False(SoilGroupEnrichmentEvaluator.ShouldPreserveOfficialSoilType(AttributeProvenanceSourceType.Derived));
        Assert.False(SoilGroupEnrichmentEvaluator.ShouldPreserveOfficialSoilType(null));
    }

    [Fact]
    public void ToOverlapEvidence_orders_by_overlap_area_descending()
    {
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        var overlaps = new List<SoilGroupOverlapCandidate>
        {
            new(idA, "A", "src", "layer", 100, 10m),
            new(idB, "B", "src", "layer", 900, 90m)
        };

        var evidence = SoilGroupEnrichmentEvaluator.ToOverlapEvidence(overlaps);

        Assert.Equal(2, evidence.Count);
        Assert.Equal(idB, evidence[0].SoilGroupId);
        Assert.Equal(idA, evidence[1].SoilGroupId);
    }
}
