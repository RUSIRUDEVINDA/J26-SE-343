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
    private readonly AccessibilityCriterionEvaluator _accessibilityEvaluator = new();
    private readonly CustomCriteriaEvaluator _customCriteriaEvaluator = new();
    private readonly EnvironmentalCriterionEvaluator _environmentalEvaluator = new();

    [Fact]
    public void Accessibility_evaluator_carries_derived_road_distance_provenance()
    {
        var parcel = CreateParcelWithRoadProvenance(
            AttributeProvenance.Derived("PostGIS nearest-road", confidence: 0.95m),
            3000m);

        var result = _accessibilityEvaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Accessibility = new AccessibilityCriteria { MaxRoadDistanceMeters = 5000m }
            });

        Assert.NotNull(result.DataProvenance);
        Assert.Equal(AttributeProvenanceSourceType.Derived, result.DataProvenance!.SourceType);
        Assert.Equal("PostGIS nearest-road", result.DataProvenance.SourceName);
    }

    [Fact]
    public void Accessibility_evaluator_uses_unknown_when_road_distance_missing()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithInfrastructureFeatures(
            "SYNTH-NO-ROAD-PROV",
            (InfrastructureFeatureType.WaterSupply, "[SYNTHETIC] Hospital", 500m));

        var result = _accessibilityEvaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Accessibility = new AccessibilityCriteria { MaxRoadDistanceMeters = 5000m }
            });

        Assert.False(result.IsMet);
        Assert.NotNull(result.DataProvenance);
        Assert.Equal(AttributeProvenanceSourceType.Unknown, result.DataProvenance!.SourceType);
    }

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
    public void Environmental_evaluator_carries_synthetic_restriction_provenance()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel();
        parcel.AddEnvironmentalRestriction(new EnvironmentalRestriction(
            EnvironmentalRestrictionType.Wetland,
            "[SYNTHETIC] Wetland buffer",
            RestrictionSeverity.Medium,
            AttributeProvenance.Synthetic("Synthetic GIS dataset")));

        var result = _environmentalEvaluator.Evaluate(
            parcel,
            new LandRecommendationSearchRequest
            {
                RequiredPurpose = LandUseType.Agricultural,
                Environmental = new EnvironmentalCriteria
                {
                    MaxAllowedEnvironmentalSeverity = RestrictionSeverity.High
                }
            });

        Assert.NotNull(result.DataProvenance);
        Assert.Equal(AttributeProvenanceSourceType.Synthetic, result.DataProvenance!.SourceType);
    }

    [Fact]
    public async Task RecommendAsync_evidence_exposes_provenance_labels_without_changing_score()
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
            Accessibility = new AccessibilityCriteria
            {
                RequireRoadAccess = true,
                MaxRoadDistanceMeters = 1000m
            },
            TargetParcelId = parcel.Id
        };

        var derivedResponse = await engine.RecommendAsync(request);
        var baselineResponse = await baselineEngine.RecommendAsync(request with { TargetParcelId = baselineParcel.Id });

        var derivedRecommendation = Assert.Single(derivedResponse.Recommendations);
        var baselineRecommendation = Assert.Single(baselineResponse.Recommendations);

        Assert.Equal(baselineRecommendation.SuitabilityScore, derivedRecommendation.SuitabilityScore);

        var accessibilityEvidence = derivedRecommendation.Evidence
            .Single(e => e.RelatedCriterionName == "Accessibility");

        Assert.Equal("Derived GIS data", accessibilityEvidence.Source);
        Assert.Contains("Derived GIS data", accessibilityEvidence.Description, StringComparison.Ordinal);
        Assert.NotNull(accessibilityEvidence.DataProvenance);
        Assert.Equal(AttributeProvenanceSourceType.Derived, accessibilityEvidence.DataProvenance!.SourceType);
    }

    [Fact]
    public void ProvenanceEvidenceFormatter_describes_source_categories_for_explanations()
    {
        Assert.Equal("Official data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Official));
        Assert.Equal("Derived GIS data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Derived));
        Assert.Equal("Estimated data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Imputed));
        Assert.Equal("Synthetic data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Synthetic));
        Assert.Equal("Unknown data", ProvenanceEvidenceFormatter.DescribeSourceType(AttributeProvenanceSourceType.Unknown));
    }

    private static LandParcel CreateParcelWithRoadProvenance(AttributeProvenance roadProvenance, decimal distanceMeters)
    {
        var parcel = new LandParcel(
            new ParcelIdentifier("SYNTH-PROV-001", "SYNTHETIC-PLAN"),
            new LandCategory(LandCategoryType.StateLand, "[SYNTHETIC]"),
            new LandArea(5m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo DS"),
            new SpatialReference(6.9271, 79.8612, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, "[SYNTHETIC]"),
            new LandCharacteristics("Loam", "Gently sloping", 25m));

        parcel.AddInfrastructureFeature(new InfrastructureFeature(
            InfrastructureFeatureType.Road,
            "[SYNTHETIC] Access Road",
            distanceMeters,
            distanceProvenance: roadProvenance));

        return parcel;
    }

    private static LandRecommendationSearchRequest CreateCustomSoilTypeRequest(string expectedSoilType) =>
        new()
        {
            RequiredPurpose = LandUseType.Agricultural,
            AdditionalCriteria =
            [
                new CustomCriterionCriteria
                {
                    Key = "soil-type",
                    Name = "Soil Type",
                    ExpectedValue = expectedSoilType,
                    ParcelAttributePath = "characteristics.soilType",
                    Weight = 0.1m
                }
            ]
        };
}
