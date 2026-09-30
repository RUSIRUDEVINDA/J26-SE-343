using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class ScreenEarlyGovernanceCommandHandlerTests
{
    private static readonly DateTimeOffset SampleUtcTime = new(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

    private static readonly string[] AllSevenIndicatorNames =
    [
        "LegalDispute",
        "UnauthorizedOccupation",
        "UnauthorizedConstruction",
        "FamilyOrInheritanceClaim",
        "MultipleClaimants",
        "UnresolvedObjection",
        "PreviousIllegalLandActivity"
    ];

    private static EarlyGovernanceIndicatorDto CreateVerifiedAbsentDto(string type) =>
        new(type, "VerifiedAbsent", $"REF-ABSENT-{type}", SampleUtcTime);

    private static EarlyGovernanceIndicatorDto CreateVerifiedPresentDto(string type) =>
        new(type, "VerifiedPresent", $"REF-PRESENT-{type}", SampleUtcTime);

    // Recording fake to observe engine interactions
    private sealed class RecordingEarlyGovernanceScreeningEngine : IEarlyGovernanceScreeningEngine
    {
        public int CallCount { get; private set; }
        public EarlyGovernanceScreeningInput? CapturedInput { get; private set; }
        private readonly EarlyGovernanceScreeningResult? _cannedResult;

        public RecordingEarlyGovernanceScreeningEngine(EarlyGovernanceScreeningResult? cannedResult = null)
        {
            _cannedResult = cannedResult;
        }

        public EarlyGovernanceScreeningResult Screen(EarlyGovernanceScreeningInput input)
        {
            CallCount++;
            CapturedInput = input;

            if (_cannedResult != null)
            {
                return _cannedResult;
            }

            return new EarlyGovernanceScreeningEngine().Screen(input);
        }
    }

    // =========================================================================
    // 1. All seven VerifiedAbsent indicators -> Clear, canonical strings, preserved provenance
    // =========================================================================
    [Fact]
    public async Task HandleAsync_AllSevenVerifiedAbsent_ReturnsClearWithCanonicalStringsAndProvenance()
    {
        var handler = new ScreenEarlyGovernanceCommandHandler(new EarlyGovernanceScreeningEngine());
        var dtos = AllSevenIndicatorNames.Select(CreateVerifiedAbsentDto).ToList();
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", dtos);

        var result = await handler.HandleAsync(command);

        Assert.Equal("CASE-001", result.CaseId);
        Assert.Equal("v1.0", result.InputVersion);
        Assert.Equal("Clear", result.OverallStatus);
        Assert.False(result.HasIncompleteEvidence);
        Assert.Equal(7, result.IndicatorResults.Count);
        Assert.All(result.IndicatorResults, r =>
        {
            Assert.Equal("VerifiedAbsent", r.EvidenceState);
            Assert.False(r.RequiresReview);
            Assert.False(r.NeedsEvidence);
            Assert.StartsWith("REF-ABSENT-", r.EvidenceReference);
            Assert.Equal(SampleUtcTime, r.RecordedAtUtc);
            Assert.StartsWith("EG_", r.ReasonCode);
            Assert.EndsWith("_VERIFIED_ABSENT", r.ReasonCode);
            Assert.Contains("records no", r.Message);
        });
    }

    // =========================================================================
    // 2. VerifiedPresent plus unavailable or omitted evidence -> ReviewRequired with HasIncompleteEvidence = true
    // =========================================================================
    [Fact]
    public async Task HandleAsync_VerifiedPresentWithUnavailableEvidence_ReturnsReviewRequiredAndHasIncompleteEvidenceTrue()
    {
        var handler = new ScreenEarlyGovernanceCommandHandler(new EarlyGovernanceScreeningEngine());
        var dtos = new List<EarlyGovernanceIndicatorDto>
        {
            CreateVerifiedPresentDto("LegalDispute"),
            new("UnauthorizedOccupation", "Unavailable", null, null),
            CreateVerifiedAbsentDto("UnauthorizedConstruction"),
            CreateVerifiedAbsentDto("FamilyOrInheritanceClaim"),
            CreateVerifiedAbsentDto("MultipleClaimants"),
            CreateVerifiedAbsentDto("UnresolvedObjection"),
            CreateVerifiedAbsentDto("PreviousIllegalLandActivity")
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-MIXED", "v1.0", dtos);

        var result = await handler.HandleAsync(command);

        Assert.Equal("ReviewRequired", result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);

        var disputeResult = result.IndicatorResults.Single(r => r.IndicatorType == "LegalDispute");
        Assert.True(disputeResult.RequiresReview);
        Assert.False(disputeResult.NeedsEvidence);
        Assert.Equal("VerifiedPresent", disputeResult.EvidenceState);

        var occupationResult = result.IndicatorResults.Single(r => r.IndicatorType == "UnauthorizedOccupation");
        Assert.False(occupationResult.RequiresReview);
        Assert.True(occupationResult.NeedsEvidence);
        Assert.Equal("Unavailable", occupationResult.EvidenceState);
    }

    // =========================================================================
    // 3. Explicit empty collection -> accepted and returns seven Missing results
    // =========================================================================
    [Fact]
    public async Task HandleAsync_ExplicitEmptyCollection_ReturnsInsufficientInformationWithSevenMissingResults()
    {
        var handler = new ScreenEarlyGovernanceCommandHandler(new EarlyGovernanceScreeningEngine());
        var command = new ScreenEarlyGovernanceCommand("CASE-EMPTY", "v1.0", Array.Empty<EarlyGovernanceIndicatorDto>());

        var result = await handler.HandleAsync(command);

        Assert.Equal("InsufficientInformation", result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);
        Assert.Equal(7, result.IndicatorResults.Count);
        Assert.All(result.IndicatorResults, r =>
        {
            Assert.Equal("Missing", r.EvidenceState);
            Assert.False(r.RequiresReview);
            Assert.True(r.NeedsEvidence);
            Assert.Null(r.EvidenceReference);
            Assert.Null(r.RecordedAtUtc);
            Assert.EndsWith("_MISSING", r.ReasonCode);
        });
    }

    // =========================================================================
    // 4. Null collection -> rejected before calling engine
    // =========================================================================
    [Fact]
    public async Task HandleAsync_NullIndicatorsCollection_ThrowsArgumentExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", null!);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    // =========================================================================
    // 5. Null command, blank identifiers, null indicator entries -> rejected before calling engine
    // =========================================================================
    [Fact]
    public async Task HandleAsync_NullCommand_ThrowsArgumentNullExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);

        await Assert.ThrowsAsync<ArgumentNullException>(() => handler.HandleAsync(null!));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceCaseId_ThrowsArgumentExceptionBeforeCallingEngine(string? invalidCaseId)
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand(invalidCaseId!, "v1.0", Array.Empty<EarlyGovernanceIndicatorDto>());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_NullOrWhitespaceInputVersion_ThrowsArgumentExceptionBeforeCallingEngine(string? invalidVersion)
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand("CASE-001", invalidVersion!, Array.Empty<EarlyGovernanceIndicatorDto>());

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Fact]
    public async Task HandleAsync_NullIndicatorElement_ThrowsArgumentExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", new EarlyGovernanceIndicatorDto[] { null! });

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    // =========================================================================
    // 6. Symbolic parsing: accepts mixed case and surrounding whitespace;
    //    rejects numeric, signed numeric, unknown, blank, and comma-separated values for BOTH fields
    // =========================================================================
    [Fact]
    public async Task HandleAsync_SymbolicParsing_AcceptsMixedCaseAndSurroundingWhitespace()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto("  legaldispute  ", "  verifiedabsent  ", "REF-001", SampleUtcTime)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", dtos);

        var result = await handler.HandleAsync(command);

        Assert.Equal(1, fakeEngine.CallCount);
        var record = fakeEngine.CapturedInput!.Indicators.Single();
        Assert.Equal(EarlyGovernanceIndicatorType.LegalDispute, record.IndicatorType);
        Assert.Equal(EarlyGovernanceEvidenceState.VerifiedAbsent, record.EvidenceState);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("999")]
    [InlineData("LegalDispute,MultipleClaimants")]
    [InlineData("UnknownConcern")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task HandleAsync_InvalidIndicatorType_ThrowsArgumentExceptionBeforeCallingEngine(string? invalidType)
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto(invalidType!, "VerifiedAbsent", "REF-001", SampleUtcTime)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", dtos);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("999")]
    [InlineData("VerifiedPresent,VerifiedAbsent")]
    [InlineData("UnknownState")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task HandleAsync_InvalidEvidenceState_ThrowsArgumentExceptionBeforeCallingEngine(string? invalidState)
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto("LegalDispute", invalidState!, "REF-001", SampleUtcTime)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-001", "v1.0", dtos);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    // =========================================================================
    // 7. Domain validation: duplicate indicators, missing provenance, non-zero UTC offset
    //    rejected before engine invocation
    // =========================================================================
    [Fact]
    public async Task HandleAsync_DuplicateIndicatorTypes_ThrowsArgumentExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            CreateVerifiedAbsentDto("LegalDispute"),
            CreateVerifiedAbsentDto("LegalDispute")
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-DUP", "v1.0", dtos);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Fact]
    public async Task HandleAsync_VerifiedPresentWithoutProvenance_ThrowsArgumentExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto("LegalDispute", "VerifiedPresent", null, null)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-NOPROV", "v1.0", dtos);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    [Fact]
    public async Task HandleAsync_NonZeroUtcOffsetTimestamp_ThrowsArgumentExceptionBeforeCallingEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var nonZeroOffset = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.FromHours(5.5));
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto("LegalDispute", "VerifiedPresent", "REF-001", nonZeroOffset)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-OFFSET", "v1.0", dtos);

        await Assert.ThrowsAsync<ArgumentException>(() => handler.HandleAsync(command));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    // =========================================================================
    // 8. Pre-cancelled token: throws OperationCanceledException; engine is not called
    // =========================================================================
    [Fact]
    public async Task HandleAsync_PreCancelledToken_ThrowsOperationCanceledExceptionAndDoesNotCallEngine()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand("CASE-CANCEL", "v1.0", Array.Empty<EarlyGovernanceIndicatorDto>());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(command, cts.Token));
        Assert.Equal(0, fakeEngine.CallCount);
    }

    // =========================================================================
    // 9. Recording fake: verifies one engine call with correctly mapped input
    // =========================================================================
    [Fact]
    public async Task HandleAsync_ValidCommand_InvokesEngineExactlyOnceWithCorrectlyMappedDomainInput()
    {
        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine();
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var dtos = new[]
        {
            new EarlyGovernanceIndicatorDto("LegalDispute", "VerifiedPresent", "REF-DISPUTE", SampleUtcTime),
            new EarlyGovernanceIndicatorDto("UnauthorizedOccupation", "VerifiedAbsent", "REF-OCCUPATION", SampleUtcTime)
        };
        var command = new ScreenEarlyGovernanceCommand("CASE-MAP-001", "v2.0", dtos);

        var result = await handler.HandleAsync(command);

        Assert.Equal(1, fakeEngine.CallCount);
        Assert.NotNull(fakeEngine.CapturedInput);
        Assert.Equal("CASE-MAP-001", fakeEngine.CapturedInput!.CaseId);
        Assert.Equal("v2.0", fakeEngine.CapturedInput.InputVersion);
        Assert.Equal(2, fakeEngine.CapturedInput.Indicators.Count);

        var first = fakeEngine.CapturedInput.Indicators[0];
        Assert.Equal(EarlyGovernanceIndicatorType.LegalDispute, first.IndicatorType);
        Assert.Equal(EarlyGovernanceEvidenceState.VerifiedPresent, first.EvidenceState);
        Assert.Equal("REF-DISPUTE", first.EvidenceReference);
        Assert.Equal(SampleUtcTime, first.RecordedAtUtc);

        var second = fakeEngine.CapturedInput.Indicators[1];
        Assert.Equal(EarlyGovernanceIndicatorType.UnauthorizedOccupation, second.IndicatorType);
        Assert.Equal(EarlyGovernanceEvidenceState.VerifiedAbsent, second.EvidenceState);
        Assert.Equal("REF-OCCUPATION", second.EvidenceReference);
        Assert.Equal(SampleUtcTime, second.RecordedAtUtc);
    }

    // =========================================================================
    // 10. Response mapping: verifies all fields, ordering, canonical names, flags,
    //     reason codes, messages, and provenance match actual returned domain result
    // =========================================================================
    [Fact]
    public async Task HandleAsync_ResponseMapping_PreservesAllFieldsOrderingCanonicalNamesAndFlags()
    {
        var fakeIndicatorResults = new List<EarlyGovernanceIndicatorResult>
        {
            new(EarlyGovernanceIndicatorType.LegalDispute, EarlyGovernanceEvidenceState.VerifiedPresent,
                requiresReview: true, needsEvidence: false, "EG_LEGAL_DISPUTE_VERIFIED_PRESENT",
                "Verified case evidence records a legal dispute. Officer review is required.", "DOC-101", SampleUtcTime),
            new(EarlyGovernanceIndicatorType.UnauthorizedOccupation, EarlyGovernanceEvidenceState.Unavailable,
                requiresReview: false, needsEvidence: true, "EG_UNAUTHORIZED_OCCUPATION_UNAVAILABLE",
                "Evidence regarding unauthorized occupation could not be obtained. Required evidence is unavailable.", null, null)
        };
        var cannedDomainResult = new EarlyGovernanceScreeningResult(
            "CASE-MAPPED",
            "v3.1",
            EarlyGovernanceScreeningStatus.ReviewRequired,
            hasIncompleteEvidence: true,
            fakeIndicatorResults);

        var fakeEngine = new RecordingEarlyGovernanceScreeningEngine(cannedDomainResult);
        var handler = new ScreenEarlyGovernanceCommandHandler(fakeEngine);
        var command = new ScreenEarlyGovernanceCommand("CASE-MAPPED", "v3.1", Array.Empty<EarlyGovernanceIndicatorDto>());

        var result = await handler.HandleAsync(command);

        Assert.Equal("CASE-MAPPED", result.CaseId);
        Assert.Equal("v3.1", result.InputVersion);
        Assert.Equal("ReviewRequired", result.OverallStatus);
        Assert.True(result.HasIncompleteEvidence);
        Assert.Equal(2, result.IndicatorResults.Count);

        var firstDto = result.IndicatorResults[0];
        Assert.Equal("LegalDispute", firstDto.IndicatorType);
        Assert.Equal("VerifiedPresent", firstDto.EvidenceState);
        Assert.True(firstDto.RequiresReview);
        Assert.False(firstDto.NeedsEvidence);
        Assert.Equal("EG_LEGAL_DISPUTE_VERIFIED_PRESENT", firstDto.ReasonCode);
        Assert.Equal("Verified case evidence records a legal dispute. Officer review is required.", firstDto.Message);
        Assert.Equal("DOC-101", firstDto.EvidenceReference);
        Assert.Equal(SampleUtcTime, firstDto.RecordedAtUtc);

        var secondDto = result.IndicatorResults[1];
        Assert.Equal("UnauthorizedOccupation", secondDto.IndicatorType);
        Assert.Equal("Unavailable", secondDto.EvidenceState);
        Assert.False(secondDto.RequiresReview);
        Assert.True(secondDto.NeedsEvidence);
        Assert.Equal("EG_UNAUTHORIZED_OCCUPATION_UNAVAILABLE", secondDto.ReasonCode);
        Assert.Equal("Evidence regarding unauthorized occupation could not be obtained. Required evidence is unavailable.", secondDto.Message);
        Assert.Null(secondDto.EvidenceReference);
        Assert.Null(secondDto.RecordedAtUtc);
    }

    // =========================================================================
    // 11. Null engine constructor dependency -> rejected
    // =========================================================================
    [Fact]
    public void Constructor_NullEngine_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => new ScreenEarlyGovernanceCommandHandler(null!));
    }
}
