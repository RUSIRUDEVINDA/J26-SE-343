namespace StateLandGovernance.UnitTests.WorkflowGovernance.Integrations;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Mappers;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.Exceptions;
using Xunit;

public class Component4ScreeningGatewayTests
{
    private static readonly string Sha256Checksum = new string('a', 64);

    #region Helper Factories

    private static (LeaseCase LeaseCase, GovernedDocument Document, DocumentAnalysis Analysis, VerifiedFactSnapshot Snapshot)
        CreateCaseWithVerifiedSnapshot(string district = "Hambantota", string proposedUse = "ecotourism", string extent = "10", string unit = "acres")
    {
        var actId = Guid.NewGuid();
        var caseId = new LeaseCaseId(Guid.NewGuid());
        var now = DateTime.UtcNow;
        var validFrom = now.AddHours(-3);
        var verificationTime = now.AddHours(-2);
        var validUntil = now.AddHours(2);

        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString("D"));
        var caseAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "LeaseInitiator" },
            scope,
            validFrom,
            verificationTime,
            validUntil);

        var leaseCase = new LeaseCase(caseId, "APP-SCR-TEST-001", actId, now, caseAuthority);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var docScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D"));
        var versionId = new DocumentVersionId(Guid.NewGuid());
        var checksum = new DocumentChecksum("SHA-256", Sha256Checksum);

        var docSubmitAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "DocumentSubmitter" },
            scope,
            validFrom,
            verificationTime,
            validUntil);

        var governedDocument = new GovernedDocument(
            docId,
            caseId,
            "ProposalDocument",
            versionId,
            checksum,
            new DocumentContentReference("storage/proposal.pdf"),
            "proposal.pdf",
            "application/pdf",
            4096,
            actId,
            now.AddHours(-1),
            docSubmitAuthority);

        var analysis = new DocumentAnalysis(
            new DocumentAnalysisId(Guid.NewGuid()),
            docId,
            versionId,
            checksum,
            1,
            1,
            now.AddHours(-1));

        var runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(runId, new AnalysisModelReference("Qwen", "1.0", "1.0"), new[] { new AnalysisCapabilityCode("ProposalIntake") }, now.AddMinutes(-50));
        analysis.StartRun(runId, now.AddMinutes(-48));

        var extractedFact1 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("district"), new AnalysisFactValue(AnalysisFactValueKind.Text, district), new ConfidenceScore(0.9m), null);
        var extractedFact2 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("proposed_land_use"), new AnalysisFactValue(AnalysisFactValueKind.Text, proposedUse), new ConfidenceScore(0.9m), null);
        var extractedFact3 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("land_extent"), new AnalysisFactValue(AnalysisFactValueKind.Text, extent), new ConfidenceScore(0.9m), null);
        var extractedFact4 = new ExtractedFactInput(new ExtractedFactId(Guid.NewGuid()), new FactCode("land_extent_unit"), new AnalysisFactValue(AnalysisFactValueKind.Text, unit), new ConfidenceScore(0.9m), null);

        var runResultId = new AnalysisRunResultId(Guid.NewGuid());
        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { extractedFact1, extractedFact2, extractedFact3, extractedFact4 },
            now.AddMinutes(-45));

        var verAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "FactVerifier" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            extractedFact1.Id,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actId,
            now.AddMinutes(-40),
            verAuthority);

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            extractedFact2.Id,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actId,
            now.AddMinutes(-40),
            verAuthority);

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            extractedFact3.Id,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actId,
            now.AddMinutes(-40),
            verAuthority);

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            extractedFact4.Id,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actId,
            now.AddMinutes(-40),
            verAuthority);

        var pubAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "FactSnapshotPublisher" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        analysis.PublishVerifiedFactSnapshot(
            snapshotId,
            runResultId,
            actId,
            now.AddMinutes(-35),
            pubAuthority);

        var snapshot = analysis.VerifiedFactSnapshots.First(s => s.Id.Value == snapshotId.Value);
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshot.Id.Value);

        return (leaseCase, governedDocument, analysis, snapshot);
    }

    private static (EvaluateComplianceCommandHandler Handler, InMemoryGovernanceEvaluationStore Store) CreateC4Handler()
    {
        var ruleProvider = new InMemoryRegulatoryRuleProvider();
        var auditRepo = new InMemoryGovernanceAuditRepository();
        var evalStore = new InMemoryGovernanceEvaluationStore(auditRepo);
        var complianceEngine = new RegulatoryComplianceEngine();

        var handler = new EvaluateComplianceCommandHandler(
            ruleProvider,
            complianceEngine,
            evalStore);

        return (handler, evalStore);
    }

    #endregion

    #region Unit & Gateway Tests

    [Fact]
    public async Task DispatchScreeningRequestAsync_Succeeds_And_RecordsResult_With_CorrelatedSnapshot()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();

        // 1. Case has active Pending screening request
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId,
            leaseCase.Id,
            snapshot.Id.Value,
            ScreeningOutcome.Pending,
            "Pending C4 screening",
            DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var (c4Handler, _) = CreateC4Handler();

        var gateway = new Component4ScreeningGateway(
            c4Handler,
            caseRepo,
            recordHandler,
            TimeProvider.System);

        var factEntries = snapshot.Entries.Select(e => new ScreeningFactEntryDto(
            e.SourceExtractedFactId?.Value,
            e.FactCode.Value,
            e.EffectiveValue.CanonicalValue,
            e.EffectiveValue.Kind.ToString(),
            e.Decision.ToString(),
            e.VerifyingActorId,
            e.VerifiedAt
        )).ToList();

        var request = new Component4ScreeningRequest(
            ScreeningRequestId: reqId,
            LeaseCaseId: leaseCase.Id.Value,
            ApplicationReference: "APP-SCR-TEST-001",
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Facts: factEntries,
            RequestedAtUtc: DateTime.UtcNow
        );

        // Act
        await gateway.DispatchScreeningRequestAsync(request, CancellationToken.None);

        // Assert
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(reqId, leaseCase.LatestScreening.Id);
        Assert.Equal(snapshot.Id.Value, leaseCase.LatestScreening.VerifiedFactSnapshotId);
        Assert.False(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));
        Assert.NotEqual(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);
        Assert.NotNull(leaseCase.LatestScreening.Remarks);
        Assert.Equal(1, uow.CommitCount);
    }

    [Fact]
    public async Task DispatchScreeningRequestAsync_WhenC4Fails_ThrowsException_And_LeavesCasePending()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();

        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId,
            leaseCase.Id,
            snapshot.Id.Value,
            ScreeningOutcome.Pending,
            "Pending C4 screening",
            DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        // Faulty C4 evaluation store throwing an unexpected persistence exception
        var ruleProvider = new InMemoryRegulatoryRuleProvider();
        var faultyEvalStore = new FaultyEvaluationStore();
        var complianceEngine = new RegulatoryComplianceEngine();
        var failingC4Handler = new EvaluateComplianceCommandHandler(ruleProvider, complianceEngine, faultyEvalStore);

        var gateway = new Component4ScreeningGateway(
            failingC4Handler,
            caseRepo,
            recordHandler,
            TimeProvider.System);

        // Minimal facts that force legacy fallback which hits the throwing provider
        var request = new Component4ScreeningRequest(
            ScreeningRequestId: reqId,
            LeaseCaseId: leaseCase.Id.Value,
            ApplicationReference: "APP-FAIL-TEST",
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Facts: Array.Empty<ScreeningFactEntryDto>(),
            RequestedAtUtc: DateTime.UtcNow
        );

        // Act & Assert
        // Gateway throws controlled integration exception
        var ex = await Assert.ThrowsAsync<Component4ScreeningException>(
            () => gateway.DispatchScreeningRequestAsync(request, CancellationToken.None));

        Assert.Equal(reqId, ex.ScreeningRequestId);
        Assert.Equal(leaseCase.Id.Value, ex.LeaseCaseId);

        // Invariant: technical failure MUST NOT clear screening
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);

        // Invariant: pending screening continues to block workflow progression
        var gateEx = Assert.Throws<PendingScreeningException>(() => leaseCase.ProgressWorkflow());
        Assert.Contains("pending", gateEx.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, uow.CommitCount);
    }

    #endregion

    #region End-to-End & Stale Snapshot Tests

    [Fact]
    public async Task EndToEnd_ScreeningWorkflowGate_IntegratesSeamlessly_With_RealC4()
    {
        // 1. Arrange domain entities and repositories
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot(
            district: "Colombo", proposedUse: "commercial", extent: "5", unit: "hectares");

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        // 2. Step 1: User/System requests screening via RequestScreeningCommandHandler
        var requestHandler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var reqScreeningCmd = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            Remarks: "Initiating pre-workflow institutional screening");

        var screeningPendingDto = await requestHandler.HandleAsync(reqScreeningCmd, CancellationToken.None);

        Assert.Equal("Pending", screeningPendingDto.Outcome);
        Assert.Equal(leaseCase.CurrentVerifiedFactSnapshotId, screeningPendingDto.BoundVerifiedFactSnapshotId);

        // 3. Step 2: Component 4 gateway dispatches the pending request
        var (c4Handler, _) = CreateC4Handler();
        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var gateway = new Component4ScreeningGateway(
            c4Handler, caseRepo, recordHandler, TimeProvider.System);

        var facts = snapshot.Entries.Select(e => new ScreeningFactEntryDto(
            e.SourceExtractedFactId?.Value,
            e.FactCode.Value,
            e.EffectiveValue.CanonicalValue,
            e.EffectiveValue.Kind.ToString(),
            e.Decision.ToString(),
            e.VerifyingActorId,
            e.VerifiedAt
        )).ToList();

        var dispatchRequest = new Component4ScreeningRequest(
            ScreeningRequestId: screeningPendingDto.ScreeningResultId,
            LeaseCaseId: leaseCase.Id.Value,
            ApplicationReference: "APP-SCR-TEST-001",
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Facts: facts,
            RequestedAtUtc: DateTime.UtcNow
        );

        await gateway.DispatchScreeningRequestAsync(dispatchRequest, CancellationToken.None);

        // 4. Assert result on aggregate
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.NotEqual(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);
        Assert.Equal(screeningPendingDto.ScreeningResultId, leaseCase.LatestScreening.Id);
        Assert.Equal(snapshot.Id.Value, leaseCase.LatestScreening.VerifiedFactSnapshotId);

        // 5. If outcome is Advisory, workflow gate passes; if Blocked, gate halts
        if (leaseCase.LatestScreening.Outcome == ScreeningOutcome.Advisory ||
            leaseCase.LatestScreening.Outcome == ScreeningOutcome.Cleared)
        {
            var gateException = Record.Exception(() => leaseCase.ProgressWorkflow());
            Assert.Null(gateException);
        }
        else
        {
            Assert.Throws<BlockedScreeningException>(() => leaseCase.ProgressWorkflow());
        }
    }

    [Fact]
    public async Task StaleSnapshot_ScreeningResultBoundToEarlierSnapshot_BlocksWorkflowProgression_WhenNewSnapshotPublished()
    {
        // 1. Arrange Case with Snapshot A
        var (leaseCase, document, analysis, snapshotA) = CreateCaseWithVerifiedSnapshot(
            district: "Colombo", proposedUse: "commercial", extent: "2", unit: "hectares");

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var (c4Handler, _) = CreateC4Handler();
        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var gateway = new Component4ScreeningGateway(
            c4Handler, caseRepo, recordHandler, TimeProvider.System);

        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshotA.Id.Value, ScreeningOutcome.Pending, "Pending A", DateTime.UtcNow));

        var factsA = snapshotA.Entries.Select(e => new ScreeningFactEntryDto(
            e.SourceExtractedFactId?.Value,
            e.FactCode.Value,
            e.EffectiveValue.CanonicalValue,
            e.EffectiveValue.Kind.ToString(),
            e.Decision.ToString(),
            e.VerifyingActorId,
            e.VerifiedAt
        )).ToList();

        var requestA = new Component4ScreeningRequest(
            reqId, leaseCase.Id.Value, "APP-STALE-TEST", snapshotA.Id.Value, factsA, DateTime.UtcNow);

        // 2. Gateway screens and records result for Snapshot A
        await gateway.DispatchScreeningRequestAsync(requestA, CancellationToken.None);

        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(snapshotA.Id.Value, leaseCase.LatestScreening.VerifiedFactSnapshotId);
        Assert.False(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));

        // 3. Simulate human verifier publishing a new Snapshot B and linking it to the case
        var snapshotBId = Guid.NewGuid();
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshotBId);

        // 4. Invariant Assertion: Screening result A is now STALE!
        Assert.True(leaseCase.LatestScreening.IsStale(leaseCase.CurrentVerifiedFactSnapshotId));
        Assert.Equal(snapshotA.Id.Value, leaseCase.LatestScreening.VerifiedFactSnapshotId);
        Assert.Equal(snapshotBId, leaseCase.CurrentVerifiedFactSnapshotId);

        // 5. Workflow progression MUST be blocked with StaleScreeningException
        var staleEx = Assert.Throws<StaleScreeningException>(() => leaseCase.ProgressWorkflow());
        Assert.Contains("stale", staleEx.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MissingGnd_RealC4Evaluation_ReturnsInsufficientInformation_And_TranslatesToAdvisory()
    {
        // Arrange: Real C4 evaluation components
        var (c4Handler, _) = CreateC4Handler();

        // Location with valid Province, District, DSD, but null GND
        var locationDto = new ProjectLocationDto(
            Province: "Western",
            District: "Colombo",
            DivisionalSecretariatDivision: "Colombo DSD",
            GramaNiladhariDivision: null,
            Description: "Plot 104, Galle Road"
        );

        var landReqDto = new LandRequirementDto(
            RequiresLand: true,
            ExtentHectares: 2.5m,
            LandType: "Commercial",
            AllocationDetails: "Commercial lease development"
        );

        var inputDto = new ProposalComplianceInputDto(
            ProposalId: "PROP-GND-TEST-001",
            Location: locationDto,
            LandRequirement: landReqDto
        );

        var command = new EvaluateComplianceCommand(
            ActionName: "GndComplianceCheck",
            LeaseDurationYears: 30,
            ProposedUse: "Commercial",
            LeaseAmount: 50000000m,
            Input: inputDto
        );

        // Act: Invoke real C4 handler in-process
        var c4Result = await c4Handler.HandleAsync(command, CancellationToken.None);

        // Assert: C4 Result status and findings
        Assert.Equal("InsufficientInformation", c4Result.Status);
        Assert.Empty(c4Result.Violations);
        Assert.NotEmpty(c4Result.Conditions);
        Assert.NotNull(c4Result.Findings);

        var locFinding = c4Result.Findings.FirstOrDefault(f => f.RuleCode == "NPD-LOC-001");
        Assert.NotNull(locFinding);
        Assert.Equal("InsufficientInformation", locFinding.Status);
        Assert.Equal("Medium", locFinding.Severity);
        Assert.False(locFinding.IsBlocking);
        Assert.True(locFinding.RequiresHumanReview);
        Assert.Equal("Missing", locFinding.EvidenceStatus);
        Assert.Equal("MissingInputs", locFinding.CalculationStatus);

        // Act: Map through C3 ComplianceResultTranslator
        var (outcome, remarks) = ComplianceResultTranslator.Translate(c4Result);

        // Assert: C3 maps this to Advisory, not Cleared and not Blocked
        Assert.Equal(ScreeningOutcome.Advisory, outcome);
        Assert.Contains("InsufficientInformation", remarks);
        Assert.Contains("NPD-LOC-001", remarks);
    }

    [Theory]
    [InlineData("perpetual")]
    [InlineData("indefinite")]
    [InlineData("unspecified")]
    [InlineData(null)]
    public async Task AmbiguousDuration_DoesNotProduceFabricatedRegulatoryViolation(string? ambiguousDuration)
    {
        // Arrange
        var (c4Handler, _) = CreateC4Handler();

        var facts = new List<ScreeningFactEntryDto>
        {
            new(Guid.NewGuid(), "district", "Colombo", "Text", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "proposed_land_use", "commercial", "Text", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "land_extent", "5", "Text", "Confirmed", Guid.NewGuid(), DateTime.UtcNow),
            new(Guid.NewGuid(), "land_extent_unit", "hectares", "Text", "Confirmed", Guid.NewGuid(), DateTime.UtcNow)
        };

        if (ambiguousDuration != null)
        {
            facts.Add(new(Guid.NewGuid(), "requested_lease_period", ambiguousDuration, "Text", "Confirmed", Guid.NewGuid(), DateTime.UtcNow));
        }

        var reqId = Guid.NewGuid();
        var caseId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();

        var request = new Component4ScreeningRequest(
            ScreeningRequestId: reqId,
            LeaseCaseId: caseId,
            ApplicationReference: "APP-AMB-DUR-001",
            VerifiedFactSnapshotId: snapshotId,
            Facts: facts,
            RequestedAtUtc: DateTime.UtcNow
        );

        // Act 1: Build command via ACL mapper
        var command = Component4ScreeningGateway.BuildEvaluateComplianceCommand(request);

        // Assert 1: Invariant: C3 does not invent a legal duration in AllocationDetails
        Assert.NotNull(command.Input);
        if (command.Input.LandRequirement?.AllocationDetails != null)
        {
            Assert.DoesNotContain("Lease Term: 0 years", command.Input.LandRequirement.AllocationDetails);
        }

        // Act 2: Execute command through real C4 handler
        var result = await c4Handler.HandleAsync(command, CancellationToken.None);

        // Assert 2: Invariant: Structured mode does NOT trigger legacy MaxLeaseDurationRule violation
        Assert.DoesNotContain(result.Violations, v => v.RuleCode == "RULE_LEASE_MAX_DURATION");
        Assert.DoesNotContain(result.Violations, v => v.Message.Contains("greater than zero", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Test Doubles

    private sealed class FaultyEvaluationStore : IGovernanceEvaluationStore
    {
        public Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated audit store cluster crash.");
        }

        public Task StoreComplianceEvaluationAsync(GovernanceAuditRecord auditRecord, ComplianceResult result, string actionName, string? proposalId, string? caseId = null, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated audit store cluster crash.");
        }

        public Task StoreConflictEvaluationAsync(GovernanceAuditRecord auditRecord, IReadOnlyList<DetectedConflict> conflicts, string actionName, int totalEvaluatedDecisions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StoreRiskEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceRiskAssessmentResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StoreExplanationEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceExplanationResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StoreConsensusEvaluationAsync(GovernanceAuditRecord auditRecord, GovernanceConsensusResult result, IReadOnlyList<InstitutionalGovernancePosition> positions, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StoreConditionalVerificationEvaluationAsync(GovernanceAuditRecord auditRecord, ConditionalVerificationResult result, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class TestLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _cases = new();

        public TestLeaseCaseRepository(params LeaseCase[] cases)
        {
            foreach (var c in cases) _cases[c.Id.Value] = c;
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _cases.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var found = _cases.Values.FirstOrDefault(c => string.Equals(c.ApplicationReference, applicationReference, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            _cases[leaseCase.Id.Value] = leaseCase;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
        {
            _cases[leaseCase.Id.Value] = leaseCase;
            return Task.CompletedTask;
        }
    }

    private sealed class TestGovernedDocumentRepository : IGovernedDocumentRepository
    {
        private readonly Dictionary<Guid, GovernedDocument> _documents = new();

        public TestGovernedDocumentRepository(params GovernedDocument[] documents)
        {
            foreach (var d in documents) _documents[d.Id.Value] = d;
        }

        public Task<GovernedDocument?> GetByIdAsync(GovernedDocumentId id, CancellationToken cancellationToken = default)
        {
            _documents.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<GovernedDocument>> GetByLeaseCaseIdAsync(LeaseCaseId leaseCaseId, CancellationToken cancellationToken = default)
        {
            var found = _documents.Values.Where(d => d.LeaseCaseId == leaseCaseId).ToList();
            return Task.FromResult<IReadOnlyList<GovernedDocument>>(found);
        }

        public Task AddAsync(GovernedDocument document, CancellationToken cancellationToken = default)
        {
            _documents[document.Id.Value] = document;
            return Task.CompletedTask;
        }
    }

    private sealed class TestDocumentAnalysisRepository : IDocumentAnalysisRepository
    {
        private readonly Dictionary<Guid, DocumentAnalysis> _analyses = new();

        public TestDocumentAnalysisRepository(params DocumentAnalysis[] analyses)
        {
            foreach (var a in analyses) _analyses[a.Id.Value] = a;
        }

        public Task<DocumentAnalysis?> GetByIdAsync(DocumentAnalysisId id, CancellationToken cancellationToken = default)
        {
            _analyses.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<DocumentAnalysis?> GetByDocumentVersionIdAsync(DocumentVersionId documentVersionId, CancellationToken cancellationToken = default)
        {
            var found = _analyses.Values.FirstOrDefault(a => a.DocumentVersionId.Value == documentVersionId.Value);
            return Task.FromResult(found);
        }

        public Task<VerifiedFactSnapshot?> GetSnapshotByIdAsync(VerifiedFactSnapshotId snapshotId, CancellationToken cancellationToken = default)
        {
            foreach (var a in _analyses.Values)
            {
                var s = a.VerifiedFactSnapshots.FirstOrDefault(x => x.Id.Value == snapshotId.Value);
                if (s != null) return Task.FromResult<VerifiedFactSnapshot?>(s);
            }
            return Task.FromResult<VerifiedFactSnapshot?>(null);
        }

        public Task AddAsync(DocumentAnalysis documentAnalysis, CancellationToken cancellationToken = default)
        {
            _analyses[documentAnalysis.Id.Value] = documentAnalysis;
            return Task.CompletedTask;
        }
    }

    private sealed class SpyWorkflowGovernanceUnitOfWork : IWorkflowGovernanceUnitOfWork
    {
        public int CommitCount { get; private set; }
        public Task<int> CommitAsync(CancellationToken cancellationToken = default)
        {
            CommitCount++;
            return Task.FromResult(1);
        }
    }

    #endregion
}
