using System;
using System.Collections.Generic;
using System.Linq;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class EarlyGovernanceScreeningEngineTests
{
    private readonly EarlyGovernanceScreeningEngine _engine = new();

    private static readonly DateTimeOffset SampleUtcTime = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly EarlyGovernanceIndicatorType[] AllSevenIndicatorTypes =
    [
        EarlyGovernanceIndicatorType.LegalDispute,
        EarlyGovernanceIndicatorType.UnauthorizedOccupation,
        EarlyGovernanceIndicatorType.UnauthorizedConstruction,
        EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim,
        EarlyGovernanceIndicatorType.MultipleClaimants,
        EarlyGovernanceIndicatorType.UnresolvedObjection,
        EarlyGovernanceIndicatorType.PreviousIllegalLandActivity
    ];

    private static EarlyGovernanceIndicatorRecord CreateVerifiedAbsent(EarlyGovernanceIndicatorType type) =>
        new(type, EarlyGovernanceEvidenceState.VerifiedAbsent, $"REF-ABSENT-{type}", SampleUtcTime);

    private static EarlyGovernanceIndicatorRecord CreateVerifiedPresent(EarlyGovernanceIndicatorType type) =>
        new(type, EarlyGovernanceEvidenceState.VerifiedPresent, $"REF-PRESENT-{type}", SampleUtcTime);

    private static EarlyGovernanceIndicatorRecord CreateNotApplicable(EarlyGovernanceIndicatorType type) =>
        new(type, EarlyGovernanceEvidenceState.NotApplicable, $"REF-NA-{type}", SampleUtcTime);

    // =========================================================================
    // A. All seven indicators VerifiedAbsent -> Clear, no incomplete evidence
    // =========================================================================
    [Fact]
    public void Screen_AllSevenIndicatorsVerifiedAbsent_ReturnsClearWithoutIncompleteEvidence()
    {
        var indicators = AllSevenIndicatorTypes.Select(CreateVerifiedAbsent).ToList();
        var input = new EarlyGovernanceScreeningInput("CASE-001", "v1.0", indicators);

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.Clear, result.OverallStatus);
        Assert.False(result.HasIncompleteEvidence);
        Assert.Equal("CASE-001", result.CaseId);
        Assert.Equal("v1.0", result.InputVersion);
        Assert.Equal(7, result.IndicatorResults.Count);
        Assert.All(result.IndicatorResults, r =>
        {
            Assert.Equal(EarlyGovernanceEvidenceState.VerifiedAbsent, r.EvidenceState);
            Assert.False(r.RequiresReview);
            Assert.False(r.NeedsEvidence);
            Assert.StartsWith("EG_", r.ReasonCode);
            Assert.EndsWith("_VERIFIED_ABSENT", r.ReasonCode);
        });
    }

    // =========================================================================
    // B. Each indicator separately VerifiedPresent -> ReviewRequired
    // =========================================================================
    public static IEnumerable<object[]> VerifiedPresentTestData()
    {
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.LegalDispute,
            "EG_LEGAL_DISPUTE_VERIFIED_PRESENT",
            "Verified case evidence records a legal dispute. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.UnauthorizedOccupation,
            "EG_UNAUTHORIZED_OCCUPATION_VERIFIED_PRESENT",
            "Verified case evidence records unauthorized occupation. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.UnauthorizedConstruction,
            "EG_UNAUTHORIZED_CONSTRUCTION_VERIFIED_PRESENT",
            "Verified case evidence records unauthorized construction. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim,
            "EG_FAMILY_INHERITANCE_CLAIM_VERIFIED_PRESENT",
            "Verified case evidence records a family or inheritance claim. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.MultipleClaimants,
            "EG_MULTIPLE_CLAIMANTS_VERIFIED_PRESENT",
            "Verified case evidence records multiple claimants. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.UnresolvedObjection,
            "EG_UNRESOLVED_OBJECTION_VERIFIED_PRESENT",
            "Verified case evidence records an unresolved objection. Officer review is required."
        };
        yield return new object[]
        {
            EarlyGovernanceIndicatorType.PreviousIllegalLandActivity,
            "EG_PREVIOUS_ILLEGAL_ACTIVITY_VERIFIED_PRESENT",
            "Verified case evidence records previous illegal land activity. Officer review is required."
        };
    }

    [Theory]
    [MemberData(nameof(VerifiedPresentTestData))]
    public void Screen_EachIndicatorSeparatelyVerifiedPresent_ReturnsReviewRequired(
        EarlyGovernanceIndicatorType flaggedType,
        string expectedReasonCode,
        string expectedMessage)
    {
        var indicators = AllSevenIndicatorTypes
            .Select(t => t == flaggedType ? CreateVerifiedPresent(t) : CreateVerifiedAbsent(t))
            .ToList();
        var input = new EarlyGovernanceScreeningInput("CASE-002", "v1.0", indicators);

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.ReviewRequired, result.OverallStatus);
        Assert.False(result.HasIncompleteEvidence);

        var flaggedResult = result.IndicatorResults.Single(r => r.IndicatorType == flaggedType);
        Assert.True(flaggedResult.RequiresReview);
        Assert.False(flaggedResult.NeedsEvidence);
        Assert.Equal(EarlyGovernanceEvidenceState.VerifiedPresent, flaggedResult.EvidenceState);
        Assert.Equal(expectedReasonCode, flaggedResult.ReasonCode);
        Assert.Equal(expectedMessage, flaggedResult.Message);

        var otherResults = result.IndicatorResults.Where(r => r.IndicatorType != flaggedType).ToList();
        Assert.All(otherResults, r => Assert.False(r.RequiresReview));
    }

    // =========================================================================
    // C. Each incomplete state with all others VerifiedAbsent -> InsufficientInformation
    // =========================================================================
    [Theory]
    [InlineData(EarlyGovernanceEvidenceState.Unverified)]
    [InlineData(EarlyGovernanceEvidenceState.Missing)]
    [InlineData(EarlyGovernanceEvidenceState.Unavailable)]
    public void Screen_SingleIncompleteIndicator_ReturnsInsufficientInformation(EarlyGovernanceEvidenceState incompleteState)
    {
        var targetType = EarlyGovernanceIndicatorType.UnauthorizedOccupation;
        var indicators = AllSevenIndicatorTypes
            .Select(t => t == targetType
                ? new EarlyGovernanceIndicatorRecord(t, incompleteState)
                : CreateVerifiedAbsent(t))
            .ToList();
        var input = new EarlyGovernanceScreeningInput("CASE-003", "v1.0", indicators);

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.InsufficientInformation, result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);

        var targetResult = result.IndicatorResults.Single(r => r.IndicatorType == targetType);
        Assert.False(targetResult.RequiresReview);
        Assert.True(targetResult.NeedsEvidence);
        Assert.Equal(incompleteState, targetResult.EvidenceState);
    }

    // =========================================================================
    // D. Empty input collection -> seven Missing results, InsufficientInformation
    // =========================================================================
    [Fact]
    public void Screen_EmptyInputCollection_EmitsSevenMissingResultsAndInsufficientInformation()
    {
        var input = new EarlyGovernanceScreeningInput("CASE-EMPTY", "v1.0", Array.Empty<EarlyGovernanceIndicatorRecord>());

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.InsufficientInformation, result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);
        Assert.Equal(7, result.IndicatorResults.Count);
        Assert.All(result.IndicatorResults, r =>
        {
            Assert.Equal(EarlyGovernanceEvidenceState.Missing, r.EvidenceState);
            Assert.False(r.RequiresReview);
            Assert.True(r.NeedsEvidence);
            Assert.Null(r.EvidenceReference);
            Assert.Null(r.RecordedAtUtc);
            Assert.EndsWith("_MISSING", r.ReasonCode);
            Assert.Contains("Required evidence is missing", r.Message);
        });
    }

    // =========================================================================
    // E. Partially omitted indicators -> Missing results for omitted items
    // =========================================================================
    [Fact]
    public void Screen_PartiallyOmittedIndicators_EmitsMissingResultsForOmittedTypes()
    {
        var supplied = new[]
        {
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.LegalDispute),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.UnauthorizedOccupation)
        };
        var input = new EarlyGovernanceScreeningInput("CASE-PARTIAL", "v1.0", supplied);

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.InsufficientInformation, result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);
        Assert.Equal(7, result.IndicatorResults.Count);

        var legalResult = result.IndicatorResults.Single(r => r.IndicatorType == EarlyGovernanceIndicatorType.LegalDispute);
        Assert.Equal(EarlyGovernanceEvidenceState.VerifiedAbsent, legalResult.EvidenceState);
        Assert.False(legalResult.NeedsEvidence);

        var omittedResults = result.IndicatorResults
            .Where(r => r.IndicatorType != EarlyGovernanceIndicatorType.LegalDispute
                     && r.IndicatorType != EarlyGovernanceIndicatorType.UnauthorizedOccupation)
            .ToList();

        Assert.Equal(5, omittedResults.Count);
        Assert.All(omittedResults, r =>
        {
            Assert.Equal(EarlyGovernanceEvidenceState.Missing, r.EvidenceState);
            Assert.True(r.NeedsEvidence);
        });
    }

    // =========================================================================
    // F. VerifiedPresent combined with incomplete evidence -> ReviewRequired AND HasIncompleteEvidence = true
    // =========================================================================
    [Fact]
    public void Screen_VerifiedPresentWithIncompleteEvidence_ReturnsReviewRequiredAndHasIncompleteEvidenceTrue()
    {
        var indicators = new List<EarlyGovernanceIndicatorRecord>
        {
            CreateVerifiedPresent(EarlyGovernanceIndicatorType.LegalDispute),
            new(EarlyGovernanceIndicatorType.UnauthorizedOccupation, EarlyGovernanceEvidenceState.Unavailable),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.UnauthorizedConstruction),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.MultipleClaimants),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.UnresolvedObjection),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.PreviousIllegalLandActivity)
        };
        var input = new EarlyGovernanceScreeningInput("CASE-MIXED", "v1.0", indicators);

        var result = _engine.Screen(input);

        // Precedence: verified concern is NOT hidden by incomplete evidence
        Assert.Equal(EarlyGovernanceScreeningStatus.ReviewRequired, result.OverallStatus);
        // Nor does verified concern hide the presence of incomplete evidence
        Assert.True(result.HasIncompleteEvidence);

        var dispute = result.IndicatorResults.Single(r => r.IndicatorType == EarlyGovernanceIndicatorType.LegalDispute);
        Assert.True(dispute.RequiresReview);
        Assert.False(dispute.NeedsEvidence);

        var occupation = result.IndicatorResults.Single(r => r.IndicatorType == EarlyGovernanceIndicatorType.UnauthorizedOccupation);
        Assert.False(occupation.RequiresReview);
        Assert.True(occupation.NeedsEvidence);
    }

    // =========================================================================
    // G. NotApplicable with valid provenance -> no review requirement
    // =========================================================================
    [Fact]
    public void Screen_NotApplicableWithValidProvenance_RequiresNoReviewAndNeedsNoEvidence()
    {
        var indicators = AllSevenIndicatorTypes
            .Select(t => t == EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim
                ? CreateNotApplicable(t)
                : CreateVerifiedAbsent(t))
            .ToList();
        var input = new EarlyGovernanceScreeningInput("CASE-NA", "v1.0", indicators);

        var result = _engine.Screen(input);

        Assert.Equal(EarlyGovernanceScreeningStatus.Clear, result.OverallStatus);
        Assert.False(result.HasIncompleteEvidence);

        var naResult = result.IndicatorResults.Single(r => r.IndicatorType == EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim);
        Assert.False(naResult.RequiresReview);
        Assert.False(naResult.NeedsEvidence);
        Assert.Equal(EarlyGovernanceEvidenceState.NotApplicable, naResult.EvidenceState);
        Assert.EndsWith("_NOT_APPLICABLE", naResult.ReasonCode);
        Assert.Contains("not applicable", naResult.Message);
    }

    // =========================================================================
    // H. NotApplicable without provenance -> invalid input
    // =========================================================================
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("REF-001", true)]
    public void IndicatorRecord_NotApplicableWithoutProvenance_ThrowsArgumentException(string? evidenceRef, bool omitTimestamp)
    {
        DateTimeOffset? timestamp = omitTimestamp ? null : SampleUtcTime;

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceIndicatorRecord(
                EarlyGovernanceIndicatorType.LegalDispute,
                EarlyGovernanceEvidenceState.NotApplicable,
                evidenceRef,
                timestamp));
    }

    // =========================================================================
    // I. VerifiedPresent or VerifiedAbsent without required provenance -> invalid input
    // =========================================================================
    [Theory]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedPresent, null, false)]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedPresent, "REF-001", true)]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedAbsent, null, false)]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedAbsent, "REF-001", true)]
    public void IndicatorRecord_VerifiedStateWithoutProvenance_ThrowsArgumentException(
        EarlyGovernanceEvidenceState state,
        string? evidenceRef,
        bool omitTimestamp)
    {
        DateTimeOffset? timestamp = omitTimestamp ? null : SampleUtcTime;

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceIndicatorRecord(
                EarlyGovernanceIndicatorType.LegalDispute,
                state,
                evidenceRef,
                timestamp));
    }

    // =========================================================================
    // J. Duplicate indicator types -> invalid input
    // =========================================================================
    [Fact]
    public void ScreeningInput_DuplicateIndicatorTypesSameState_ThrowsArgumentException()
    {
        var duplicates = new[]
        {
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.LegalDispute),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.LegalDispute)
        };

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceScreeningInput("CASE-DUP", "v1.0", duplicates));
    }

    [Fact]
    public void ScreeningInput_DuplicateIndicatorTypesContradictoryStates_ThrowsArgumentException()
    {
        var duplicates = new[]
        {
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.LegalDispute),
            CreateVerifiedPresent(EarlyGovernanceIndicatorType.LegalDispute)
        };

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceScreeningInput("CASE-DUP-CONTRADICT", "v1.0", duplicates));
    }

    // =========================================================================
    // K. Undefined enum values -> invalid input
    // =========================================================================
    [Fact]
    public void IndicatorRecord_UndefinedIndicatorType_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceIndicatorRecord(
                (EarlyGovernanceIndicatorType)999,
                EarlyGovernanceEvidenceState.Missing));
    }

    [Fact]
    public void IndicatorRecord_UndefinedEvidenceState_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceIndicatorRecord(
                EarlyGovernanceIndicatorType.LegalDispute,
                (EarlyGovernanceEvidenceState)888));
    }

    // =========================================================================
    // L. Null/blank required inputs -> invalid input
    // =========================================================================
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ScreeningInput_NullOrWhitespaceCaseId_ThrowsArgumentException(string? invalidCaseId)
    {
        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceScreeningInput(invalidCaseId!, "v1.0", Array.Empty<EarlyGovernanceIndicatorRecord>()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ScreeningInput_NullOrWhitespaceInputVersion_ThrowsArgumentException(string? invalidVersion)
    {
        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceScreeningInput("CASE-001", invalidVersion!, Array.Empty<EarlyGovernanceIndicatorRecord>()));
    }

    [Fact]
    public void ScreeningInput_NullIndicatorsCollection_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new EarlyGovernanceScreeningInput("CASE-001", "v1.0", null!));
    }

    [Fact]
    public void ScreeningInput_NullElementInIndicatorsCollection_ThrowsArgumentException()
    {
        var list = new EarlyGovernanceIndicatorRecord[] { null! };

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceScreeningInput("CASE-001", "v1.0", list));
    }

    [Fact]
    public void Engine_NullInput_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _engine.Screen(null!));
    }

    // =========================================================================
    // M. Input order does not change output ordering or result content
    // =========================================================================
    [Fact]
    public void Screen_DifferentInputOrders_ProduceIdenticalOutputOrderAndResults()
    {
        var forwardIndicators = AllSevenIndicatorTypes.Select(CreateVerifiedAbsent).ToList();
        var reversedIndicators = forwardIndicators.AsEnumerable().Reverse().ToList();

        var inputForward = new EarlyGovernanceScreeningInput("CASE-ORDER", "v1.0", forwardIndicators);
        var inputReversed = new EarlyGovernanceScreeningInput("CASE-ORDER", "v1.0", reversedIndicators);

        var resultForward = _engine.Screen(inputForward);
        var resultReversed = _engine.Screen(inputReversed);

        var expectedOrder = new[]
        {
            EarlyGovernanceIndicatorType.LegalDispute,
            EarlyGovernanceIndicatorType.UnauthorizedOccupation,
            EarlyGovernanceIndicatorType.UnauthorizedConstruction,
            EarlyGovernanceIndicatorType.FamilyOrInheritanceClaim,
            EarlyGovernanceIndicatorType.MultipleClaimants,
            EarlyGovernanceIndicatorType.UnresolvedObjection,
            EarlyGovernanceIndicatorType.PreviousIllegalLandActivity
        };

        Assert.Equal(expectedOrder, resultForward.IndicatorResults.Select(r => r.IndicatorType));
        Assert.Equal(expectedOrder, resultReversed.IndicatorResults.Select(r => r.IndicatorType));

        Assert.Equal(resultForward.IndicatorResults.Count, resultReversed.IndicatorResults.Count);
        for (int i = 0; i < resultForward.IndicatorResults.Count; i++)
        {
            var forward = resultForward.IndicatorResults[i];
            var reversed = resultReversed.IndicatorResults[i];

            Assert.Equal(forward.IndicatorType, reversed.IndicatorType);
            Assert.Equal(forward.EvidenceState, reversed.EvidenceState);
            Assert.Equal(forward.RequiresReview, reversed.RequiresReview);
            Assert.Equal(forward.NeedsEvidence, reversed.NeedsEvidence);
            Assert.Equal(forward.ReasonCode, reversed.ReasonCode);
            Assert.Equal(forward.Message, reversed.Message);
            Assert.Equal(forward.EvidenceReference, reversed.EvidenceReference);
            Assert.Equal(forward.RecordedAtUtc, reversed.RecordedAtUtc);
        }
    }

    // =========================================================================
    // N. Output preserves evidence reference, timestamp, CaseId and InputVersion
    // =========================================================================
    [Fact]
    public void Screen_PreservesEvidenceReferenceTimestampCaseIdAndInputVersion()
    {
        var expectedTime = new DateTimeOffset(2026, 7, 15, 8, 30, 0, TimeSpan.Zero);
        var record = new EarlyGovernanceIndicatorRecord(
            EarlyGovernanceIndicatorType.LegalDispute,
            EarlyGovernanceEvidenceState.VerifiedPresent,
            "DOC-REF-9999",
            expectedTime);

        var input = new EarlyGovernanceScreeningInput("CASE-PROVENANCE-001", "v2.5.1", new[] { record });

        var result = _engine.Screen(input);

        Assert.Equal("CASE-PROVENANCE-001", result.CaseId);
        Assert.Equal("v2.5.1", result.InputVersion);

        var disputeResult = result.IndicatorResults.Single(r => r.IndicatorType == EarlyGovernanceIndicatorType.LegalDispute);
        Assert.Equal("DOC-REF-9999", disputeResult.EvidenceReference);
        Assert.Equal(expectedTime, disputeResult.RecordedAtUtc);
    }

    // =========================================================================
    // O. Evaluation does not mutate input collections
    // =========================================================================
    [Fact]
    public void Screen_DoesNotMutateInputCollection()
    {
        var list = new List<EarlyGovernanceIndicatorRecord>
        {
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.LegalDispute),
            CreateVerifiedAbsent(EarlyGovernanceIndicatorType.UnauthorizedOccupation)
        };
        var input = new EarlyGovernanceScreeningInput("CASE-IMMUTABLE", "v1.0", list);

        int countBefore = input.Indicators.Count;
        _engine.Screen(input);
        int countAfter = input.Indicators.Count;

        Assert.Equal(countBefore, countAfter);
        Assert.Equal(2, input.Indicators.Count);

        // Modifying caller list does not mutate input snapshot
        list.Add(CreateVerifiedAbsent(EarlyGovernanceIndicatorType.UnauthorizedConstruction));
        Assert.Equal(2, input.Indicators.Count);
    }

    // =========================================================================
    // P. Non-zero UTC offset supplied timestamps -> invalid input
    // =========================================================================
    [Theory]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedPresent)]
    [InlineData(EarlyGovernanceEvidenceState.VerifiedAbsent)]
    [InlineData(EarlyGovernanceEvidenceState.Unverified)]
    [InlineData(EarlyGovernanceEvidenceState.Missing)]
    [InlineData(EarlyGovernanceEvidenceState.Unavailable)]
    [InlineData(EarlyGovernanceEvidenceState.NotApplicable)]
    public void IndicatorRecord_NonZeroUtcOffsetTimestamp_AllEvidenceStates_ThrowsArgumentException(EarlyGovernanceEvidenceState state)
    {
        var nonUtcOffset = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(5.5)); // UTC+5:30

        Assert.Throws<ArgumentException>(() =>
            new EarlyGovernanceIndicatorRecord(
                EarlyGovernanceIndicatorType.LegalDispute,
                state,
                "REF-VALID-PROVENANCE",
                nonUtcOffset));
    }

    // =========================================================================
    // Q. Constructor explicit untyped null arguments regression test
    // =========================================================================
    [Fact]
    public void IndicatorRecord_ExplicitNullProvenanceArguments_CompilesAndProducesNullFields()
    {
        var record = new EarlyGovernanceIndicatorRecord(
            EarlyGovernanceIndicatorType.LegalDispute,
            EarlyGovernanceEvidenceState.Missing,
            null,
            null);

        Assert.Equal(EarlyGovernanceIndicatorType.LegalDispute, record.IndicatorType);
        Assert.Equal(EarlyGovernanceEvidenceState.Missing, record.EvidenceState);
        Assert.Null(record.EvidenceReference);
        Assert.Null(record.RecordedAtUtc);
    }
}
