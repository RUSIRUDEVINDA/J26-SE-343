using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Recommendations;
using StateLandGovernance.LandIntelligence.Application.RealParcels;
using StateLandGovernance.LandIntelligence.Domain.Enums;

namespace StateLandGovernance.UnitTests.LandIntelligence.Recommendations;

public sealed class SourceReportedClaimantScreeningTests
{
    [Fact]
    public void Evaluate_State_IsPotentialCandidateNotVerified()
    {
        var result = SourceReportedClaimantScreening.Evaluate(CreateSource("2", "State"));

        Assert.Equal(StateLandCandidateScreeningOutcome.PotentialCandidateNotVerified, result.Outcome);
        Assert.Equal(SourceReportedClaimantScreening.PotentialCandidateLabel, result.StatusLabel);
        Assert.True(result.IncludeInCandidateResults);
        Assert.Contains("State", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("available to lease", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_Private_IsExcluded()
    {
        var result = SourceReportedClaimantScreening.Evaluate(CreateSource("3", "Private"));

        Assert.Equal(StateLandCandidateScreeningOutcome.ExcludedPrivate, result.Outcome);
        Assert.False(result.IncludeInCandidateResults);
        Assert.Equal(SourceReportedClaimantScreening.ExcludedPrivateLabel, result.StatusLabel);
    }

    [Theory]
    [InlineData("1", "Not Checked")]
    [InlineData("4", "Not Known")]
    [InlineData("6", "Unsettled")]
    [InlineData("5", "Other Authority")]
    [InlineData("7", "Other Authority - LRC")]
    [InlineData("13", "Other Authority - UDA")]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void Evaluate_UnknownUncheckedOtherAuthority_NeedsVerification(string? code, string? label)
    {
        var source = code is null && label is null
            ? null
            : CreateSource(code, label);
        var result = SourceReportedClaimantScreening.Evaluate(source);

        Assert.Equal(StateLandCandidateScreeningOutcome.NeedsOfficialVerification, result.Outcome);
        Assert.True(result.IncludeInCandidateResults);
        Assert.Equal(SourceReportedClaimantScreening.NeedsVerificationLabel, result.StatusLabel);
        Assert.Contains("verification", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_DoesNotTreatParcelStatusAsLeaseAvailability()
    {
        var result = SourceReportedClaimantScreening.Evaluate(
            CreateSource("2", "State", parcelStatus: "ACTIVE"));

        Assert.Equal("ACTIVE", result.ParcelStatusCode);
        Assert.Equal(SourceReportedClaimantScreening.PotentialCandidateLabel, result.StatusLabel);
        Assert.Contains("not treated as lease availability", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Engine_ExcludesPrivate_KeepsStateAndNeedsVerification()
    {
        var stateParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("STATE-001");
        var privateParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("PRIVATE-001");
        var uncheckedParcel = SyntheticRecommendationParcelFactory.CreateSuitableParcel("UNCHECKED-001");

        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [stateParcel.Id] = CreateSource("2", "State"),
            [privateParcel.Id] = CreateSource("3", "Private"),
            [uncheckedParcel.Id] = CreateSource("1", "Not Checked"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(
            sources,
            stateParcel,
            privateParcel,
            uncheckedParcel);

        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10
        });

        Assert.Equal(2, response.CandidateCount);
        Assert.DoesNotContain(response.Recommendations, r => r.CadastralNumber == "PRIVATE-001");
        Assert.Contains(response.Recommendations, r => r.CadastralNumber == "STATE-001");
        Assert.Contains(response.Recommendations, r => r.CadastralNumber == "UNCHECKED-001");

        var stateResult = response.Recommendations.Single(r => r.CadastralNumber == "STATE-001");
        Assert.Equal(SourceReportedClaimantScreening.PotentialCandidateLabel, stateResult.CandidateStatusLabel);
        Assert.Equal("2", stateResult.SourceReportedClaimantCategoryCode);

        var uncheckedResult = response.Recommendations.Single(r => r.CadastralNumber == "UNCHECKED-001");
        Assert.Equal(SourceReportedClaimantScreening.NeedsVerificationLabel, uncheckedResult.CandidateStatusLabel);

        Assert.Contains(
            response.Transparency!.FilterStages,
            stage => stage.Name == "State-land claimant screening" && stage.CandidateCount == 2);
    }

    [Fact]
    public async Task Engine_HardRestrictions_RemainSeparateFromClaimantScreening()
    {
        var restricted = SyntheticRecommendationParcelFactory.CreateRestrictedParcel("STATE-RESTRICTED-001");
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [restricted.Id] = CreateSource("2", "State"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(sources, restricted);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10
        });

        Assert.Single(response.Recommendations);
        var result = response.Recommendations[0];
        Assert.Equal(SourceReportedClaimantScreening.PotentialCandidateLabel, result.CandidateStatusLabel);
        Assert.True(result.HardConstraintRejected);
        Assert.False(string.IsNullOrWhiteSpace(result.HardConstraintReason));
    }

    [Fact]
    public async Task Engine_MissingGisEvidence_DoesNotImplyAvailability()
    {
        var parcel = SyntheticRecommendationParcelFactory.CreateParcelWithRoadDistance(
            roadDistanceMeters: null,
            cadastralNumber: "STATE-NO-ROAD-001");
        var sources = new Dictionary<Guid, CadastralSourceMetadataDto>
        {
            [parcel.Id] = CreateSource("2", "State"),
        };

        var engine = RecommendationEngineTestFactory.CreateWithSources(sources, parcel);
        var response = await engine.RecommendAsync(new LandRecommendationSearchRequest
        {
            RequiredPurpose = LandUseType.Agricultural,
            MaxResults = 10
        });

        Assert.Single(response.Recommendations);
        var result = response.Recommendations[0];
        Assert.Equal(SourceReportedClaimantScreening.PotentialCandidateLabel, result.CandidateStatusLabel);
        // Missing road GIS evidence must not imply lease availability or positive road match.
        Assert.DoesNotContain(
            result.MatchingCriteria,
            c => c.Key.Contains("accessibility", StringComparison.OrdinalIgnoreCase) && c.IsMet);
        Assert.Contains("official availability not verified", result.CandidateStatusLabel, StringComparison.OrdinalIgnoreCase);
    }

    private static CadastralSourceMetadataDto CreateSource(
        string? code,
        string? label,
        string? parcelStatus = null) =>
        new(
            ColomboCadastreSourceDomains.SourceProvenanceLabel,
            "Cadastre_Parcels_Landuse_classified___Land_Use_0",
            "feature-1",
            SourceReportedLandUseCode: "73",
            SourceReportedLandUseEnglishLabel: "Paddy Field",
            SourceReportedLandUseText: null,
            SourceReportedClaimantCategoryCode: code,
            SourceReportedClaimantCategoryEnglishLabel: label,
            SourceParcelStatusCode: parcelStatus,
            GnIndex: null,
            CoordinateStatus: null,
            ImportedAtUtc: DateTimeOffset.Parse("2026-01-15T00:00:00Z"))
        {
            SourceReportedLandUseDisplay = "Paddy Field",
            SourceReportedClaimantCategoryDisplay =
                ColomboCadastreSourceDomains.FormatSourceReportedClaimantCategoryDisplay(code, label),
        };
}
