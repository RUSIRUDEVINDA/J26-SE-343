using StateLandGovernance.LandIntelligence.Application.DTOs;

using StateLandGovernance.LandIntelligence.Domain.Entities;

using StateLandGovernance.LandIntelligence.Domain.Enums;

using StateLandGovernance.LandIntelligence.Domain.ValueObjects;

using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations;

using StateLandGovernance.LandIntelligence.Infrastructure.Recommendations.Criteria;

using StateLandGovernance.UnitTests.LandIntelligence.Recommendations;



namespace StateLandGovernance.UnitTests.LandIntelligence.Provenance;



public sealed class AttributeProvenanceEvidenceTests

{

    private readonly CustomCriteriaEvaluator _customCriteriaEvaluator = new();



    [Fact]

    public void Custom_criteria_does_not_match_when_soil_type_missing()

    {

        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();

        parcel.UpdateCharacteristics(new LandCharacteristics());



        var result = _customCriteriaEvaluator.Evaluate(

            parcel,

            CreateCustomSoilTypeRequest("Loam"));



        Assert.False(result.IsMet);

        Assert.Equal(0m, result.Score);

        Assert.NotNull(result.DataProvenance);

        Assert.Equal(AttributeProvenanceSourceType.Unknown, result.DataProvenance!.SourceType);

    }



    [Fact]

    public void Custom_criteria_carries_official_soil_type_provenance()

    {

        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();

        parcel.UpdateCharacteristics(new LandCharacteristics(

            soilType: "Loam",

            soilTypeProvenance: AttributeProvenance.Official("Survey Department")));



        var result = _customCriteriaEvaluator.Evaluate(parcel, CreateCustomSoilTypeRequest("Loam"));



        Assert.True(result.IsMet);

        Assert.NotNull(result.DataProvenance);

        Assert.Equal(AttributeProvenanceSourceType.Official, result.DataProvenance!.SourceType);

        Assert.True(result.DataProvenance.Verified);

    }



    [Fact]

    public async Task RecommendAsync_preserves_scores_when_gis_road_evidence_is_present_but_unused()

    {

        var parcel = CreateParcelWithRoadProvenance(

            AttributeProvenance.Derived("PostGIS nearest-road", confidence: 0.95m),

            250m);

        var engine = RecommendationEngineTestFactory.Create(parcel);



        var baselineParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();

        var baselineEngine = RecommendationEngineTestFactory.Create(baselineParcel);



        var request = new LandRecommendationSearchRequest

        {

            RequiredPurpose = LandUseType.Agricultural,

            RequiredAreaHectares = 5m,

            RequiredLandCategory = LandCategoryType.StateLand,

            RequiredLandUse = LandUseType.Agricultural,

            PreferredLocation = new PreferredLocationCriteria { Province = "Western" },

            TargetParcelId = parcel.Id

        };



        var derivedResponse = await engine.RecommendAsync(request);

        var baselineResponse = await baselineEngine.RecommendAsync(request with { TargetParcelId = baselineParcel.Id });



        var derivedRecommendation = Assert.Single(derivedResponse.Recommendations);

        var baselineRecommendation = Assert.Single(baselineResponse.Recommendations);



        Assert.Equal(baselineRecommendation.SuitabilityScore, derivedRecommendation.SuitabilityScore);

        Assert.DoesNotContain(

            derivedRecommendation.MatchingCriteria.Concat(derivedRecommendation.FailedCriteria),

            c => c.Key == "accessibility");

    }



    [Fact]

    public void ProvenanceEvidenceFormatter_describes_source_categories_for_explanations()

    {

        Assert.Equal("Official data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Official));

        Assert.Equal("Derived GIS data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Derived));

        Assert.Equal("Unknown data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Unknown));

    }



    private static LandRecommendationSearchRequest CreateCustomSoilTypeRequest(string expected) =>

        new()

        {

            RequiredPurpose = LandUseType.Agricultural,

            AdditionalCriteria =

            [

                new CustomCriterionCriteria

                {

                    Key = "soil-type",

                    Name = "Soil Type",

                    ParcelAttributePath = "characteristics.soiltype",

                    ExpectedValue = expected,

                    Weight = 0.05m

                }

            ]

        };



    private static LandParcel CreateParcelWithRoadProvenance(

        AttributeProvenance provenance,

        decimal distanceMeters)

    {

        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("SYNTH-PROV-ROAD");

        parcel.ReplaceInfrastructureFeatures(

        [

            new InfrastructureFeature(

                InfrastructureFeatureType.Road,

                "[SYNTHETIC] Access Road",

                distanceMeters,

                "[SYNTHETIC] Road distance for provenance tests.",

                provenance)

        ]);

        return parcel;

    }

}


