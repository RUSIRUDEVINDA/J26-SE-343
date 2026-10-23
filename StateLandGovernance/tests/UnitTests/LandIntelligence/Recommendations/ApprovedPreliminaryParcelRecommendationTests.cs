using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class ApprovedPreliminaryParcelRecommendationTests
{
    [Fact]
    public async Task RecommendAsync_ExcludesParcelsWithApprovedPreliminaryRequest_BeforeRanking()
    {
        var approved = SyntheticRecommendationParcelFactory.CreateSuitableParcel("APPROVED-001");
        var eligible = SyntheticRecommendationParcelFactory.CreateSuitableParcel("ELIGIBLE-001");

        var engine = RecommendationEngineTestFactory.CreateWithApprovedParcelIds(
            [approved.Id],
            sources: null,
            approved,
            eligible);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10,
        });

        Assert.Equal(1, response.CandidateCount);
        Assert.DoesNotContain(response.Recommendations, r => r.LandParcelId == approved.Id);
        Assert.Contains(response.Recommendations, r => r.LandParcelId == eligible.Id);
        Assert.Contains(
            response.Transparency!.FilterStages,
            stage => stage.Name == "Approved preliminary interest");
    }
}
