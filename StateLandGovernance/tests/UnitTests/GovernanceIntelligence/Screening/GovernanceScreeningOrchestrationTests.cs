using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Fixtures;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.Screening;

public class GovernanceScreeningOrchestrationTests
{
    private readonly InMemoryRegulatoryRuleProvider _ruleProvider;
    private readonly RegulatoryComplianceEngine _complianceEngine;
    private readonly GovernanceConflictEngine _conflictEngine;
    private readonly EarlyGovernanceScreeningEngine _earlyGovernanceEngine;
    private readonly InMemoryConsolidatedGovernanceScreeningStore _consolidatedStore;
    private readonly InMemoryGovernanceAuditRepository _auditRepository;
    private readonly InMemoryGovernanceEvaluationStore _evalStore;
    private readonly OrchestrateGovernanceScreeningCommandHandler _handler;

    public GovernanceScreeningOrchestrationTests()
    {
        _ruleProvider = new InMemoryRegulatoryRuleProvider();
        _complianceEngine = new RegulatoryComplianceEngine();
        _conflictEngine = new GovernanceConflictEngine();
        _earlyGovernanceEngine = new EarlyGovernanceScreeningEngine();
        _consolidatedStore = new InMemoryConsolidatedGovernanceScreeningStore();
        _auditRepository = new InMemoryGovernanceAuditRepository();
        _evalStore = new InMemoryGovernanceEvaluationStore(_auditRepository);

        _handler = new OrchestrateGovernanceScreeningCommandHandler(
            _complianceEngine,
            _ruleProvider,
            _conflictEngine,
            _earlyGovernanceEngine,
            _evalStore,
            _consolidatedStore,
            earlyScreeningStore: null,
            timeProvider: TimeProvider.System);
    }

    [Fact]
    public async Task HandleAsync_ThrowsArgumentNullException_WhenCommandIsNull()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _handler.HandleAsync(null!, CancellationToken.None));
    }

    [Theory]
    [InlineData("", "APP-001", "1.0", true)]
    [InlineData("CASE-001", "", "1.0", true)]
    [InlineData("CASE-001", "APP-001", "", true)]
    [InlineData("CASE-001", "APP-001", "1.0", false)]
    public async Task HandleAsync_ThrowsArgumentException_WhenRequiredIdentifiersMissing(
        string caseId, string appRef, string propVer, bool validGuid)
    {
        var input = new GovernanceScreeningCaseInputDto(
            CaseId: caseId,
            ApplicationReference: appRef,
            ProposalVersion: propVer,
            VerifiedFactSnapshotId: validGuid ? Guid.NewGuid() : Guid.Empty,
            SnapshotTimestampUtc: DateTimeOffset.UtcNow,
            ParcelId: "PARCEL-PL-001",
            CadastralNumber: "PL-001",
            ApplicantName: "Test Applicant",
            ProposedUse: "Commercial",
            ExtentHectares: 2.5m,
            LeaseDurationYears: 20,
            InvestmentAmount: 500_000m,
            InvestmentCurrency: "LKR",
            ZoningArea: "Commercial Zone",
            District: "Colombo",
            DivisionalSecretariat: "Colombo",
            Province: "Western"
        );

        var command = new OrchestrateGovernanceScreeningCommand(input);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            _handler.HandleAsync(command, CancellationToken.None));
    }

    [Fact]
    public void SnapshotHasher_ProducesDeterministicOutput_ForEquivalentInputs()
    {
        var fixture1 = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
        var fixture2 = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();

        var hash1 = OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(fixture1);
        var hash2 = OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(fixture2);

        Assert.NotNull(hash1);
        Assert.Equal(hash1, hash2);
    }

    [Fact]
    public void SnapshotHasher_ProducesDifferentOutput_WhenInputsVary()
    {
        var fixture1 = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
        var fixture2 = C4DevelopmentCaseFixtureProvider.GetSolarPowerPlantFixture();

        var hash1 = OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(fixture1);
        var hash2 = OrchestrateGovernanceScreeningCommandHandler.ComputeSnapshotHash(fixture2);

        Assert.NotEqual(hash1, hash2);
    }

    [Fact]
    public async Task HandleAsync_IsIdempotent_ReturnsCachedResultForSameSnapshot()
    {
        var fixture = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
        var command = new OrchestrateGovernanceScreeningCommand(fixture);

        var firstResult = await _handler.HandleAsync(command, CancellationToken.None);
        var secondResult = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(firstResult);
        Assert.NotNull(secondResult);
        Assert.Equal(firstResult.AssessmentId, secondResult.AssessmentId);
        Assert.Equal(firstResult.SnapshotSha256, secondResult.SnapshotSha256);

        var history = await _consolidatedStore.GetHistoryByCaseIdAsync(fixture.CaseId, cancellationToken: CancellationToken.None);
        Assert.Single(history);
    }

    [Fact]
    public async Task HandleAsync_CoordinatesAllEngines_ForEcoTourismFixture()
    {
        var fixture = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
        var command = new OrchestrateGovernanceScreeningCommand(fixture);

        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(fixture.CaseId, result.CaseId);
        Assert.Equal(fixture.ApplicationReference, result.ApplicationReference);

        // Verify Step 1: Regulatory Compliance
        Assert.NotNull(result.RegulatoryCheck);
        Assert.Equal("Compliant", result.RegulatoryCheck.Status);
        Assert.Equal(0, result.RegulatoryCheck.ViolationsCount);

        // Verify Step 2: Conflict Detection
        Assert.NotNull(result.ConflictCheck);
        Assert.Equal("Clear", result.ConflictCheck.Status);

        // Verify Step 3: Early Governance Screening
        Assert.NotNull(result.EarlyGovernanceCheck);
        Assert.Equal("ReviewRequired", result.EarlyGovernanceCheck.Status);
        Assert.True(result.EarlyGovernanceCheck.ReferralRecommended);

        // Verify Consolidated Summary
        Assert.NotNull(result.RequiredConditions);
        Assert.Equal("RecommendedWithConditions", result.OverallStatus);
        Assert.NotNull(result.OfficerRecommendationSummary);
        Assert.NotNull(result.Provenance);
        Assert.True(result.Provenance.IsDevelopmentFixture);
    }

    [Fact]
    public async Task HandleAsync_AggregatesBlockedStatus_WhenRuleViolated()
    {
        var fixture = C4DevelopmentCaseFixtureProvider.GetEcoTourismResortFixture();
        // Modify to violate maximum duration rule (120 years exceeds 99-year ceiling)
        var invalidInput = fixture with
        {
            ProposalVersion = "2.0-violation",
            VerifiedFactSnapshotId = Guid.NewGuid(),
            LeaseDurationYears = 120 // Violation!
        };

        var command = new OrchestrateGovernanceScreeningCommand(invalidInput);
        var result = await _handler.HandleAsync(command, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("NonCompliant", result.RegulatoryCheck.Status);
        Assert.True(result.RegulatoryCheck.ViolationsCount > 0);
        Assert.Equal("Blocked", result.OverallStatus);
    }

    [Fact]
    public void C4DevelopmentCaseFixtureProvider_ReturnsValidFixturesWithSyntheticProvenance()
    {
        var fixtures = C4DevelopmentCaseFixtureProvider.GetAllFixtures();

        Assert.Equal(3, fixtures.Count);

        foreach (var fixture in fixtures)
        {
            Assert.False(string.IsNullOrWhiteSpace(fixture.CaseId));
            Assert.False(string.IsNullOrWhiteSpace(fixture.ApplicationReference));
            Assert.False(string.IsNullOrWhiteSpace(fixture.ProposalVersion));
            Assert.NotEqual(Guid.Empty, fixture.VerifiedFactSnapshotId);
            Assert.False(string.IsNullOrWhiteSpace(fixture.ParcelId));
            Assert.False(string.IsNullOrWhiteSpace(fixture.ApplicantName));
        }
    }

    [Fact]
    public void C4DevelopmentCaseFixtureProvider_TryGetByCaseId_ReturnsCorrectMatch()
    {
        var fixture = C4DevelopmentCaseFixtureProvider.TryGetByCaseId("LLA-2025-001");
        Assert.NotNull(fixture);
        Assert.Equal("LLA-2025-001", fixture.CaseId);

        var missing = C4DevelopmentCaseFixtureProvider.TryGetByCaseId("NON-EXISTENT-999");
        Assert.Null(missing);
    }
}
