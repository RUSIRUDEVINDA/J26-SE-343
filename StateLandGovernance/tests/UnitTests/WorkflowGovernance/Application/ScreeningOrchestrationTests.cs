namespace StateLandGovernance.UnitTests.WorkflowGovernance.Application;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Queries;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;
using Xunit;

public class ScreeningOrchestrationTests
{
    #region Test Setup & Helpers

    private const string Sha256V1 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

    private static (LeaseCase LeaseCase, GovernedDocument Document, DocumentAnalysis Analysis, VerifiedFactSnapshot Snapshot) CreateCaseWithVerifiedSnapshot(
        Guid? leaseCaseId = null,
        Guid? actorId = null)
    {
        var caseId = new LeaseCaseId(leaseCaseId ?? Guid.NewGuid());
        var actId = actorId ?? Guid.NewGuid();
        var now = DateTime.UtcNow;

        var validFrom = now.AddHours(-2);
        var verificationTime = now.AddMinutes(-50);
        var verifiedAt = now.AddMinutes(-40);
        var publishedAt = now.AddMinutes(-30);
        var validUntil = now.AddHours(2);

        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString());
        var caseAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "LeaseInitiator", LeaseCase.ProposalReviewCapability },
            scope,
            validFrom,
            verificationTime,
            validUntil);

        var leaseCase = new LeaseCase(caseId, "APP-SCR-001", actId, now, caseAuthority);

        var docId = new GovernedDocumentId(Guid.NewGuid());
        var docScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, docId.Value.ToString("D"));
        var pubAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "FactSnapshotPublisher" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        var versionId = new DocumentVersionId(Guid.NewGuid());
        var checksum = new DocumentChecksum("SHA-256", Sha256V1);

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
            "SurveyPlan",
            versionId,
            checksum,
            new DocumentContentReference("storage/plan.pdf"),
            "plan.pdf",
            "application/pdf",
            2048,
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
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("Provider", "Model", "1.0"),
            new[] { new AnalysisCapabilityCode("CAP") },
            now.AddMinutes(-55));

        analysis.StartRun(runId, now.AddMinutes(-52));

        var runResultId = new AnalysisRunResultId(Guid.NewGuid());
        var extractedFactId = new ExtractedFactId(Guid.NewGuid());
        var factInput = new ExtractedFactInput(
            extractedFactId,
            new FactCode("SurveyPlan.PlanNumber"),
            new AnalysisFactValue(AnalysisFactValueKind.Text, "SP-2026-9901"),
            new ConfidenceScore(0.95m),
            null);

        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { factInput },
            now.AddMinutes(-51));

        var verificationId = new HumanFactVerificationId(Guid.NewGuid());
        var verAuthority = new VerifiedAuthoritySnapshot(
            actId,
            new[] { "FactVerifier" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        analysis.RecordFactVerification(
            verificationId,
            runResultId,
            extractedFactId,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actId,
            verifiedAt,
            verAuthority);

        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        analysis.PublishVerifiedFactSnapshot(
            snapshotId,
            runResultId,
            actId,
            publishedAt,
            pubAuthority);

        var snapshot = analysis.VerifiedFactSnapshots.First(s => s.Id.Value == snapshotId.Value);

        // Link snapshot to lease case
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshot.Id.Value);

        return (leaseCase, governedDocument, analysis, snapshot);
    }

    private static VerifiedFactSnapshot CreateSecondPublishedSnapshot(
        DocumentAnalysis analysis,
        Guid actorId,
        DateTime now)
    {
        var validFrom = now.AddHours(-2);
        var verificationTime = now.AddMinutes(-50);
        var verifiedAt = now.AddMinutes(-20);
        var publishedAt = now.AddMinutes(-10);
        var validUntil = now.AddHours(2);

        var runId = new AnalysisRunId(Guid.NewGuid());
        analysis.RequestRun(
            runId,
            new AnalysisModelReference("Provider", "Model", "2.0"),
            new[] { new AnalysisCapabilityCode("CAP2") },
            now.AddMinutes(-28));

        analysis.StartRun(runId, now.AddMinutes(-26));

        var runResultId = new AnalysisRunResultId(Guid.NewGuid());
        var extractedFactId = new ExtractedFactId(Guid.NewGuid());
        var factInput = new ExtractedFactInput(
            extractedFactId,
            new FactCode("SurveyPlan.PlanNumber"),
            new AnalysisFactValue(AnalysisFactValueKind.Text, "SP-2026-9902"),
            new ConfidenceScore(0.98m),
            null);

        analysis.CompleteRun(
            runId,
            runResultId,
            AnalysisResultOutcome.OutputsProduced,
            new List<AnalysisResultArtifactReference>(),
            new List<ExtractedFactInput> { factInput },
            now.AddMinutes(-23));

        var docScope = new AuthorityScope(AuthorityScopeKind.GovernedDocument, analysis.GovernedDocumentId.Value.ToString("D"));
        var verAuthority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactVerifier" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        analysis.RecordFactVerification(
            new HumanFactVerificationId(Guid.NewGuid()),
            runResultId,
            extractedFactId,
            FactVerificationDecision.Confirmed,
            null,
            null,
            actorId,
            verifiedAt,
            verAuthority);

        var snapshotId = new VerifiedFactSnapshotId(Guid.NewGuid());
        var pubAuthority = new VerifiedAuthoritySnapshot(
            actorId,
            new[] { "FactSnapshotPublisher" },
            docScope,
            validFrom,
            verificationTime,
            validUntil);

        analysis.PublishVerifiedFactSnapshot(
            snapshotId,
            runResultId,
            actorId,
            publishedAt,
            pubAuthority);

        return analysis.VerifiedFactSnapshots.First(s => s.Id.Value == snapshotId.Value);
    }

    #endregion

    #region Section 19: Tests — Request Lifecycle

    [Fact]
    public async Task RequestScreening_Succeeds_When_CaseHasCurrentVerifiedSnapshot()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            Remarks: "Initiating regulatory check");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert: Returns factual DTO; commits Pending state exactly once
        Assert.NotNull(result);
        Assert.Equal(leaseCase.Id.Value, result.LeaseCaseId);
        Assert.Equal(snapshot.Id.Value, result.BoundVerifiedFactSnapshotId);
        Assert.Equal(leaseCase.CurrentVerifiedFactSnapshotId, result.CurrentVerifiedFactSnapshotId);
        Assert.Equal("Pending", result.Outcome);
        Assert.False(result.IsStale);
        Assert.Equal(1, uow.CommitCount);
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);
    }

    [Fact]
    public async Task RequestScreening_Fails_When_CaseHasNoCurrentVerifiedSnapshot()
    {
        // Arrange: Case without CurrentVerifiedFactSnapshotId (Guid.Empty)
        var actId = Guid.NewGuid();
        var caseId = new LeaseCaseId(Guid.NewGuid());
        var now = DateTime.UtcNow;
        var scope = new AuthorityScope(AuthorityScopeKind.LeaseCase, caseId.Value.ToString());
        var authority = new VerifiedAuthoritySnapshot(
            actId, new[] { "LeaseInitiator" }, scope,
            now.AddMinutes(-5), now.AddMinutes(-2), now.AddHours(1));

        var leaseCase = new LeaseCase(caseId, "APP-NOSNAP-001", actId, now, authority);
        Assert.Equal(Guid.Empty, leaseCase.CurrentVerifiedFactSnapshotId);

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository();
        var docRepo = new TestGovernedDocumentRepository();
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<MissingCurrentVerifiedFactSnapshotException>(() => handler.HandleAsync(command));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RequestScreening_Binds_Exact_CurrentSnapshotId()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert: Screening is bound strictly to current snapshot ID
        Assert.Equal(snapshot.Id.Value, result.BoundVerifiedFactSnapshotId);
        Assert.Equal(leaseCase.LatestScreening!.VerifiedFactSnapshotId, snapshot.Id.Value);
    }

    [Fact]
    public async Task RequestScreening_Fails_When_ExpectedRevision_Is_Stale()
    {
        // Arrange
        var (leaseCase, document, analysis, _) = CreateCaseWithVerifiedSnapshot();
        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision + 99); // Stale revision

        // Act & Assert
        await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => handler.HandleAsync(command));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RequestScreening_Fails_When_Snapshot_BelongsToAnotherCase()
    {
        // Arrange: Case A has document and snapshot A. Case B links snapshot A as its current snapshot.
        var (caseA, docA, analysisA, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var (caseB, _, _, _) = CreateCaseWithVerifiedSnapshot();

        // Cross-case linkage: Case B points to snapshot A
        caseB.UpdateCurrentVerifiedFactSnapshot(snapshotA.Id.Value);

        var caseRepo = new TestLeaseCaseRepository(caseA, caseB);
        var analysisRepo = new TestDocumentAnalysisRepository(analysisA);
        var docRepo = new TestGovernedDocumentRepository(docA);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: caseB.Id.Value,
            ExpectedLeaseCaseRevision: caseB.Revision);

        // Act & Assert: Cross-case ownership mismatch rejected, 0 commits
        var ex = await Assert.ThrowsAsync<ScreeningSnapshotCaseMismatchException>(() => handler.HandleAsync(command));
        Assert.Equal(caseB.Id.Value, ex.TargetLeaseCaseId);
        Assert.Equal(snapshotA.Id.Value, ex.SnapshotId);
        Assert.Equal(caseA.Id.Value, ex.OwningLeaseCaseId);
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RequestScreening_DoesNotPerformSynchronousNetworkDispatch_CommitsOnce()
    {
        // Arrange: Request command requires no live network call and commits Pending state once
        var (leaseCase, document, analysis, _) = CreateCaseWithVerifiedSnapshot();
        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var command = new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act
        var result = await handler.HandleAsync(command);

        // Assert: Safely committed without any gateway dependency
        Assert.Equal(1, uow.CommitCount);
        Assert.Equal("Pending", result.Outcome);
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);
    }

    #endregion

    #region Section 20: Tests — Result Ingestion Lifecycle

    [Theory]
    [InlineData("Cleared", ScreeningOutcome.Cleared)]
    [InlineData("Advisory", ScreeningOutcome.Advisory)]
    [InlineData("Blocked", ScreeningOutcome.Blocked)]
    public async Task RecordScreeningResult_Succeeds_And_Preserves_Outcome_When_CorrelatedToPendingRequest(
        string outcomeInput,
        ScreeningOutcome expectedOutcome)
    {
        // Arrange: Pre-populate Pending screening request on case
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var screeningRequestId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            screeningRequestId,
            leaseCase.Id,
            snapshot.Id.Value,
            ScreeningOutcome.Pending,
            "Screening requested",
            DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: screeningRequestId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: outcomeInput,
            ExpectedLeaseCaseRevision: leaseCase.Revision,
            Remarks: "Component 4 assessment result recorded");

        // Act
        var result = await handler.HandleAsync(command);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expectedOutcome.ToString(), result.Outcome);
        Assert.Equal(snapshot.Id.Value, result.BoundVerifiedFactSnapshotId);
        Assert.Equal(leaseCase.CurrentVerifiedFactSnapshotId, result.CurrentVerifiedFactSnapshotId);
        Assert.False(result.IsStale);
        Assert.Equal(1, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_NoPendingScreeningExists_UnsolicitedResult()
    {
        // Arrange: Case has no latest screening request
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        Assert.Null(leaseCase.LatestScreening);

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: Guid.NewGuid(),
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert: Unsolicited result rejected, zero commits
        await Assert.ThrowsAsync<NoPendingScreeningRequestException>(() => handler.HandleAsync(command));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_ScreeningRequestId_DoesNotMatch_ActivePendingRequest()
    {
        // Arrange: Case has Pending request R1; command supplies R2
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var activePendingRequestId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            activePendingRequestId,
            leaseCase.Id,
            snapshot.Id.Value,
            ScreeningOutcome.Pending,
            "Screening requested",
            DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var wrongRequestId = Guid.NewGuid();
        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: wrongRequestId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert: Mismatched request ID rejected, zero commits
        var ex = await Assert.ThrowsAsync<ScreeningRequestMismatchException>(() => handler.HandleAsync(command));
        Assert.Equal(wrongRequestId, ex.SuppliedScreeningRequestId);
        Assert.Equal(activePendingRequestId, ex.ActiveScreeningRequestId);
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_Snapshot_BelongsToAnotherCase()
    {
        // Arrange: Case A owns snapshot A. Case B has a Pending screening but result command references Case B with snapshot A.
        var (caseA, docA, analysisA, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var (caseB, docB, analysisB, snapshotB) = CreateCaseWithVerifiedSnapshot();

        var pendingRequestIdB = Guid.NewGuid();
        caseB.RecordScreeningResult(new ScreeningResult(
            pendingRequestIdB,
            caseB.Id,
            snapshotA.Id.Value, // Artificially pointing to snapshot A
            ScreeningOutcome.Pending,
            "Pending",
            DateTime.UtcNow));

        caseB.UpdateCurrentVerifiedFactSnapshot(snapshotA.Id.Value);

        var caseRepo = new TestLeaseCaseRepository(caseA, caseB);
        var analysisRepo = new TestDocumentAnalysisRepository(analysisA, analysisB);
        var docRepo = new TestGovernedDocumentRepository(docA, docB);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: caseB.Id.Value,
            ScreeningRequestId: pendingRequestIdB,
            VerifiedFactSnapshotId: snapshotA.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: caseB.Revision);

        // Act & Assert: Cross-case ownership mismatch rejected, zero commits
        var ex = await Assert.ThrowsAsync<ScreeningSnapshotCaseMismatchException>(() => handler.HandleAsync(command));
        Assert.Equal(caseB.Id.Value, ex.TargetLeaseCaseId);
        Assert.Equal(snapshotA.Id.Value, ex.SnapshotId);
        Assert.Equal(caseA.Id.Value, ex.OwningLeaseCaseId);
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_SnapshotId_DoesNotExist()
    {
        // Arrange: Pending screening exists, but command references nonexistent snapshot
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var nonexistentSnapshotId = Guid.NewGuid();
        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: nonexistentSnapshotId,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert: Snapshot mismatch between command and pending screening
        await Assert.ThrowsAsync<ScreeningSnapshotMismatchException>(() => handler.HandleAsync(command));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_ExpectedRevision_Is_Stale()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision + 10);

        // Act & Assert
        await Assert.ThrowsAsync<LeaseCaseConcurrencyException>(() => handler.HandleAsync(command));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_When_Outcome_Is_Invalid_Or_Pending()
    {
        // Arrange
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var commandInvalid = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "SuperCleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        var commandPending = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Pending", // Pending cannot be recorded as final result
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(commandInvalid));
        await Assert.ThrowsAsync<ValidationException>(() => handler.HandleAsync(commandPending));
        Assert.Equal(0, uow.CommitCount);
    }

    [Fact]
    public async Task RecordScreeningResult_Fails_On_DuplicateDelivery_When_Already_Accepted()
    {
        // Arrange: Request R was created and result was already recorded (Outcome is Cleared)
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // First delivery: Accepted
        var firstResult = await handler.HandleAsync(command);
        Assert.Equal("Cleared", firstResult.Outcome);
        Assert.Equal(1, uow.CommitCount);

        // Second delivery (duplicate): LatestScreening is now Cleared, no longer Pending
        var duplicateCommand = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshot.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert: Duplicate delivery rejected, commit count remains 1
        await Assert.ThrowsAsync<NoPendingScreeningRequestException>(() => handler.HandleAsync(duplicateCommand));
        Assert.Equal(1, uow.CommitCount);
    }

    #endregion

    #region Section 21: Tests — Staleness Scenarios A, B, C, D

    [Fact]
    public async Task Staleness_Scenario_A_CurrentSnapshot_Equals_ResultSnapshot_IsCurrent_And_ProgressionSucceeds()
    {
        // Scenario A: Current Snapshot = A, Screening Result = A / Cleared -> Screening is current
        var (leaseCase, document, analysis, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshotA.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var command = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshotA.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        var result = await handler.HandleAsync(command);

        // Assert: Screening is current
        Assert.False(result.IsStale);

        // Domain verification: workflow progress succeeds without exception
        var ex = Record.Exception(() => leaseCase.ProgressWorkflow());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Staleness_Scenario_B_CurrentSnapshot_Changes_To_B_OldResult_A_Becomes_Stale()
    {
        // Scenario B: Screening Result = A (Cleared), Current Snapshot changes to B -> Screening A becomes stale
        var (leaseCase, document, analysis, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshotA.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();
        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        // 1. Record Cleared screening for snapshot A
        await handler.HandleAsync(new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqId,
            VerifiedFactSnapshotId: snapshotA.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision));

        // 2. Publish snapshot B and link it as current on case
        var snapshotB = CreateSecondPublishedSnapshot(analysis, leaseCase.CreatedByActorId, DateTime.UtcNow);
        analysisRepo.SeedSnapshot(snapshotB);
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshotB.Id.Value);

        // Act: Query status for case
        var queryHandler = new GetCurrentScreeningStatusQueryHandler(caseRepo);
        var status = await queryHandler.HandleAsync(new GetCurrentScreeningStatusQuery(leaseCase.Id.Value));

        // Assert: Result A is now stale
        Assert.NotNull(status);
        Assert.Equal(snapshotA.Id.Value, status.BoundVerifiedFactSnapshotId);
        Assert.Equal(snapshotB.Id.Value, status.CurrentVerifiedFactSnapshotId);
        Assert.True(status.IsStale);

        // Domain verification: ProgressWorkflow throws StaleScreeningException
        Assert.Throws<StaleScreeningException>(() => leaseCase.ProgressWorkflow());
    }

    [Fact]
    public async Task Staleness_Scenario_C_Old_A_Cleared_Current_B_Pending_Old_A_Must_Not_Authorize_Progression()
    {
        // Scenario C: Old A = Cleared, Current B = Pending -> Old A must not authorize progression
        var (leaseCase, document, analysis, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var reqIdA = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqIdA, leaseCase.Id, snapshotA.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var requestHandler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        // 1. Cleared result recorded for A
        await recordHandler.HandleAsync(new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqIdA,
            VerifiedFactSnapshotId: snapshotA.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision));

        // 2. Snapshot changes to B
        var snapshotB = CreateSecondPublishedSnapshot(analysis, leaseCase.CreatedByActorId, DateTime.UtcNow);
        analysisRepo.SeedSnapshot(snapshotB);
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshotB.Id.Value);

        // 3. Screening requested for B (sets Pending for B)
        var pendingResult = await requestHandler.HandleAsync(new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision));

        // Assert: Case status reflects B as pending, old Cleared outcome for A does NOT authorize progression
        Assert.Equal(snapshotB.Id.Value, pendingResult.BoundVerifiedFactSnapshotId);
        Assert.Equal("Pending", pendingResult.Outcome);

        // Domain verification: ProgressWorkflow throws PendingScreeningException
        Assert.Throws<PendingScreeningException>(() => leaseCase.ProgressWorkflow());
    }

    [Fact]
    public async Task Staleness_Scenario_D_LateResult_A_Arriving_When_B_Is_Pending_Is_Rejected_And_Preserves_Pending_B()
    {
        // Scenario D:
        // Snapshot A screened
        // Snapshot B becomes current -> Screening B requested (LatestScreening = Pending B)
        // Late result for A arrives -> Correlation/snapshot mismatch -> REJECTED -> 0 commits
        // LatestScreening strictly remains Pending B!
        var (leaseCase, document, analysis, snapshotA) = CreateCaseWithVerifiedSnapshot();
        var reqIdA = Guid.NewGuid();

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var requestHandler = new RequestScreeningCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RequestScreeningCommandValidator(),
            TimeProvider.System);

        var recordHandler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        // 1. Snapshot B published and set as current
        var snapshotB = CreateSecondPublishedSnapshot(analysis, leaseCase.CreatedByActorId, DateTime.UtcNow);
        analysisRepo.SeedSnapshot(snapshotB);
        leaseCase.UpdateCurrentVerifiedFactSnapshot(snapshotB.Id.Value);

        // 2. Screening requested for B
        var pendingB = await requestHandler.HandleAsync(new RequestScreeningCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ExpectedLeaseCaseRevision: leaseCase.Revision));

        Assert.Equal(snapshotB.Id.Value, pendingB.BoundVerifiedFactSnapshotId);
        Assert.Equal("Pending", pendingB.Outcome);
        var pendingBId = pendingB.ScreeningResultId;
        var commitsBeforeLateResult = uow.CommitCount;

        // 3. Late Component 4 result for Snapshot A arrives with reqIdA
        var lateCommand = new RecordScreeningResultCommand(
            LeaseCaseId: leaseCase.Id.Value,
            ScreeningRequestId: reqIdA,
            VerifiedFactSnapshotId: snapshotA.Id.Value,
            Outcome: "Cleared",
            ExpectedLeaseCaseRevision: leaseCase.Revision);

        // Act & Assert: Late result A is rejected due to request ID mismatch with active Pending B
        await Assert.ThrowsAsync<ScreeningRequestMismatchException>(() => recordHandler.HandleAsync(lateCommand));

        // Zero additional commits occurred
        Assert.Equal(commitsBeforeLateResult, uow.CommitCount);

        // CRITICAL: LatestScreening remains Pending B, exactly preserving authoritative state!
        Assert.NotNull(leaseCase.LatestScreening);
        Assert.Equal(pendingBId, leaseCase.LatestScreening.Id);
        Assert.Equal(snapshotB.Id.Value, leaseCase.LatestScreening.VerifiedFactSnapshotId);
        Assert.Equal(ScreeningOutcome.Pending, leaseCase.LatestScreening.Outcome);
    }

    #endregion

    #region Section 22: Tests — Pending / Blocked / Advisory / Cleared Semantics

    [Fact]
    public async Task Outcome_Pending_Blocks_WorkflowProgression()
    {
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Screening requested", DateTime.UtcNow));

        var queryHandler = new GetCurrentScreeningStatusQueryHandler(new TestLeaseCaseRepository(leaseCase));
        var status = await queryHandler.HandleAsync(new GetCurrentScreeningStatusQuery(leaseCase.Id.Value));

        Assert.NotNull(status);
        Assert.Equal("Pending", status.Outcome);
        Assert.Throws<PendingScreeningException>(() => leaseCase.ProgressWorkflow());
    }

    [Fact]
    public async Task Outcome_Blocked_Blocks_WorkflowProgression()
    {
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var result = await handler.HandleAsync(new RecordScreeningResultCommand(
            leaseCase.Id.Value, reqId, snapshot.Id.Value, "Blocked", leaseCase.Revision, Remarks: "Severe conflict"));

        Assert.Equal("Blocked", result.Outcome);
        Assert.Throws<BlockedScreeningException>(() => leaseCase.ProgressWorkflow());
    }

    [Fact]
    public async Task Outcome_Advisory_Allows_WorkflowProgression()
    {
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var result = await handler.HandleAsync(new RecordScreeningResultCommand(
            leaseCase.Id.Value, reqId, snapshot.Id.Value, "Advisory", leaseCase.Revision, Remarks: "Minor notice"));

        Assert.Equal("Advisory", result.Outcome);

        // Domain verification: Advisory passes screening gate without throwing
        var ex = Record.Exception(() => leaseCase.ProgressWorkflow());
        Assert.Null(ex);
    }

    [Fact]
    public async Task Outcome_Cleared_Allows_WorkflowProgression_Only_For_Exact_Current_Snapshot()
    {
        var (leaseCase, document, analysis, snapshot) = CreateCaseWithVerifiedSnapshot();
        var reqId = Guid.NewGuid();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            reqId, leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Pending, "Pending", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var analysisRepo = new TestDocumentAnalysisRepository(analysis);
        var docRepo = new TestGovernedDocumentRepository(document);
        var uow = new SpyWorkflowGovernanceUnitOfWork();

        var handler = new RecordScreeningResultCommandHandler(
            caseRepo, analysisRepo, docRepo, uow,
            new RecordScreeningResultCommandValidator(),
            TimeProvider.System);

        var result = await handler.HandleAsync(new RecordScreeningResultCommand(
            leaseCase.Id.Value, reqId, snapshot.Id.Value, "Cleared", leaseCase.Revision));

        Assert.Equal("Cleared", result.Outcome);

        var ex = Record.Exception(() => leaseCase.ProgressWorkflow());
        Assert.Null(ex);
    }

    #endregion

    #region Section 23: Tests — Queries

    [Fact]
    public async Task GetCurrentScreeningStatusQuery_Returns_Null_When_NoScreening_Exists()
    {
        // Arrange
        var (leaseCase, _, _, _) = CreateCaseWithVerifiedSnapshot();
        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var queryHandler = new GetCurrentScreeningStatusQueryHandler(caseRepo);

        // Act
        var result = await queryHandler.HandleAsync(new GetCurrentScreeningStatusQuery(leaseCase.Id.Value));

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task GetCurrentScreeningStatusQuery_Maps_ScreeningState_Accurately_And_ZeroCommits()
    {
        // Arrange
        var (leaseCase, _, _, snapshot) = CreateCaseWithVerifiedSnapshot();
        leaseCase.RecordScreeningResult(new ScreeningResult(
            Guid.NewGuid(), leaseCase.Id, snapshot.Id.Value, ScreeningOutcome.Cleared, "Test remarks", DateTime.UtcNow));

        var caseRepo = new TestLeaseCaseRepository(leaseCase);
        var queryHandler = new GetCurrentScreeningStatusQueryHandler(caseRepo);

        // Act
        var result = await queryHandler.HandleAsync(new GetCurrentScreeningStatusQuery(leaseCase.Id.Value));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(leaseCase.Id.Value, result.LeaseCaseId);
        Assert.Equal(snapshot.Id.Value, result.BoundVerifiedFactSnapshotId);
        Assert.Equal(leaseCase.CurrentVerifiedFactSnapshotId, result.CurrentVerifiedFactSnapshotId);
        Assert.Equal("Cleared", result.Outcome);
        Assert.Equal("Test remarks", result.Remarks);
        Assert.False(result.IsStale);
    }

    #endregion

    #region Test Spy & Fake Implementations

    private sealed class TestLeaseCaseRepository : ILeaseCaseRepository
    {
        private readonly Dictionary<Guid, LeaseCase> _cases = new();

        public TestLeaseCaseRepository(params LeaseCase[] cases)
        {
            foreach (var c in cases)
            {
                _cases[c.Id.Value] = c;
            }
        }

        public Task<LeaseCase?> GetByIdAsync(LeaseCaseId id, CancellationToken cancellationToken = default)
        {
            _cases.TryGetValue(id.Value, out var found);
            return Task.FromResult(found);
        }

        public Task<LeaseCase?> GetByApplicationReferenceAsync(string applicationReference, CancellationToken cancellationToken = default)
        {
            var found = _cases.Values.FirstOrDefault(c => c.ApplicationReference == applicationReference);
            return Task.FromResult(found);
        }

        public Task AddAsync(LeaseCase leaseCase, CancellationToken cancellationToken = default)
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
            foreach (var d in documents)
            {
                _documents[d.Id.Value] = d;
            }
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
        private readonly Dictionary<Guid, VerifiedFactSnapshot> _standaloneSnapshots = new();

        public TestDocumentAnalysisRepository(params DocumentAnalysis[] analyses)
        {
            foreach (var a in analyses)
            {
                _analyses[a.Id.Value] = a;
            }
        }

        public void SeedSnapshot(VerifiedFactSnapshot snapshot)
        {
            _standaloneSnapshots[snapshot.Id.Value] = snapshot;
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

            if (_standaloneSnapshots.TryGetValue(snapshotId.Value, out var standalone))
            {
                return Task.FromResult<VerifiedFactSnapshot?>(standalone);
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
