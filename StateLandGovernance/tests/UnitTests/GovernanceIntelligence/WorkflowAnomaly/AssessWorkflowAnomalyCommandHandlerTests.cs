using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

public sealed class AssessWorkflowAnomalyCommandHandlerTests
{
    private const string FrozenModelVersion = WorkflowAnomalyAssessment.FrozenModelVersionV1;
    private const double FrozenThreshold = WorkflowAnomalyAssessment.FrozenThresholdV1;
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 12, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_DirectCompleteTrace_CallsClientAndPersistsAssessmentWithAllFeatures()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient(
            caseId: "SLG-CASE-2026-0001",
            anomalyScore: 0.6125,
            threshold: FrozenThreshold,
            flagged: true);
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        var workflowRunId = Guid.NewGuid();
        var traceEvents = CreateSampleAnomalyTrace();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-ANOM-01");

        var command = AssessWorkflowAnomalyCommand.FromTrace(
            caseId: "SLG-CASE-2026-0001",
            workflowRunId: workflowRunId,
            events: traceEvents,
            provenance: provenance);

        var result = await handler.HandleAsync(command);

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(result.Assessment);
        Assert.Equal(1, fakeClient.CallCount);

        var assessment = result.Assessment!;
        Assert.Equal("SLG-CASE-2026-0001", assessment.CaseId);
        Assert.Equal(workflowRunId, assessment.WorkflowRunId);
        Assert.Equal(FrozenModelVersion, assessment.ModelVersion);
        Assert.Equal(WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1, assessment.ModelArtifactSha256);
        Assert.Equal(0.6125, assessment.AnomalyScore, 6);
        Assert.Equal(FrozenThreshold, assessment.Threshold, 12);
        Assert.True(assessment.Flagged);
        Assert.Equal(assessment.AnomalyScore > assessment.Threshold, assessment.Flagged);
        Assert.Equal(FixedUtcNow, assessment.AssessedAtUtc);

        // Verify all 9 features persisted accurately
        Assert.Equal(4, assessment.EventCount);
        Assert.Equal(2.041667, assessment.ElapsedDays, 6);
        Assert.Equal(23.0, assessment.MaxGapHours, 6);
        Assert.Equal(16.333333, assessment.MeanGapHours, 6);
        Assert.Equal(4, assessment.UniqueActivities);
        Assert.Equal(0, assessment.RepeatedActivityCount);
        Assert.Equal(2, assessment.ResourceHandoffs);
        Assert.Equal(2, assessment.InstitutionSwitches);
        Assert.Equal(3, assessment.DistinctResources);

        var persisted = await assessmentStore.GetByIdAsync(assessment.AssessmentId);
        Assert.NotNull(persisted);
        Assert.Equal(assessment.InputSha256, persisted!.InputSha256);
    }

    [Fact]
    public async Task HandleAsync_FromStoredComponent3Projection_ExtractsExactTraceAndMatchesDirectTraceHash()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient(
            caseId: "SLG-CASE-2026-0001",
            anomalyScore: 0.4812,
            threshold: FrozenThreshold,
            flagged: false);
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var workflowRunId = Guid.NewGuid();
        var projection = CreateCompletedProjectionWithNaiveTimestamps(workflowRunId, "SLG-CASE-2026-0001");
        await projectionStore.UpsertAsync(projection);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        var result = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(
            workflowProjectionId: projection.ProjectionId,
            caseId: "SLG-CASE-2026-0001",
            workflowRunId: workflowRunId));

        Assert.True(result.IsSuccess, result.Error?.Message);
        Assert.NotNull(result.Assessment);
        Assert.Equal(1, fakeClient.CallCount);
        Assert.Equal(projection.ProjectionId, result.Assessment!.WorkflowProjectionId);

        // High-value trace/hash consistency test:
        // The exact trace sent to IWorkflowAnomalyClient must produce the exact InputSha256 stored on the assessment
        Assert.NotNull(fakeClient.LastSubmittedEvents);
        var expectedDirectHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            fakeClient.LastSubmittedCaseId!,
            fakeClient.LastSubmittedEvents!,
            projection.Provenance);

        Assert.Equal(expectedDirectHash, result.Assessment.InputSha256);

        // Verify exact event strings & verbatim naive timestamps sent to client
        var expectedTrace = CreateSampleAnomalyTrace();
        Assert.Equal(expectedTrace.Count, fakeClient.LastSubmittedEvents!.Count);
        for (var i = 0; i < expectedTrace.Count; i++)
        {
            Assert.Equal(expectedTrace[i].EventSeq, fakeClient.LastSubmittedEvents[i].EventSeq);
            Assert.Equal(expectedTrace[i].Activity, fakeClient.LastSubmittedEvents[i].Activity);
            Assert.Equal(expectedTrace[i].Institution, fakeClient.LastSubmittedEvents[i].Institution);
            Assert.Equal(expectedTrace[i].Resource, fakeClient.LastSubmittedEvents[i].Resource);
            Assert.Equal(expectedTrace[i].Timestamp, fakeClient.LastSubmittedEvents[i].Timestamp);
        }
    }

    [Fact]
    public void ComputeCanonicalInputSha256_MutatingOrderStringsOrNaiveTimestampPrecision_ChangesInputSha256()
    {
        var workflowRunId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-HASH");
        var baselineEvents = CreateSampleAnomalyTrace();
        var baselineHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            "SLG-CASE-2026-0001",
            baselineEvents,
            provenance);

        // 1. Reordering events in the submitted list (even with same EventSeq numbers) must change hash
        var reversedListOrder = new[]
        {
            baselineEvents[1],
            baselineEvents[0],
            baselineEvents[2],
            baselineEvents[3]
        };
        var reversedOrderHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            "SLG-CASE-2026-0001",
            reversedListOrder,
            provenance);
        Assert.NotEqual(baselineHash, reversedOrderHash);

        // 2. Changing fractional seconds representation on naive timestamp changes hash
        var fractionalSecondsEvents = new[]
        {
            baselineEvents[0] with { Timestamp = "2031-02-03T08:00:00.000" },
            baselineEvents[1],
            baselineEvents[2],
            baselineEvents[3]
        };
        var fractionalTimestampHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            "SLG-CASE-2026-0001",
            fractionalSecondsEvents,
            provenance);
        Assert.NotEqual(baselineHash, fractionalTimestampHash);

        // 3. Changing resource string changes hash
        var modifiedResourceEvents = new[]
        {
            baselineEvents[0] with { Resource = "SYNTH_OFFICER_99" },
            baselineEvents[1],
            baselineEvents[2],
            baselineEvents[3]
        };
        var resourceHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            "SLG-CASE-2026-0001",
            modifiedResourceEvents,
            provenance);
        Assert.NotEqual(baselineHash, resourceHash);

        // 4. Changing institution string changes hash
        var modifiedInstitutionEvents = new[]
        {
            baselineEvents[0] with { Institution = "SURVEY_DEPT" },
            baselineEvents[1],
            baselineEvents[2],
            baselineEvents[3]
        };
        var institutionHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            "SLG-CASE-2026-0001",
            modifiedInstitutionEvents,
            provenance);
        Assert.NotEqual(baselineHash, institutionHash);
    }

    [Fact]
    public async Task HandleAsync_IdempotentDuplicateAssessment_DoesNotCreateDuplicateRow()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient(
            caseId: "SLG-CASE-2026-0001",
            anomalyScore: 0.6001,
            threshold: FrozenThreshold,
            flagged: true);
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        var workflowRunId = Guid.NewGuid();
        var command = AssessWorkflowAnomalyCommand.FromTrace(
            caseId: "SLG-CASE-2026-0001",
            workflowRunId: workflowRunId,
            events: CreateSampleAnomalyTrace(),
            provenance: CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-ANOM-IDEM"));

        var firstResult = await handler.HandleAsync(command);
        var secondResult = await handler.HandleAsync(command);

        Assert.True(firstResult.IsSuccess);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal(firstResult.Assessment!.AssessmentId, secondResult.Assessment!.AssessmentId);
        Assert.Equal(firstResult.Assessment.InputSha256, secondResult.Assessment.InputSha256);
        Assert.Equal(1, await dbContext.WorkflowAnomalyAssessments.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_SameWorkflowRunIdWithChangedTrace_ProducesDistinctInputSha256AndSecondAssessment()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient(
            caseId: "SLG-CASE-2026-0001",
            anomalyScore: 0.5910,
            threshold: FrozenThreshold,
            flagged: true);
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        var workflowRunId = Guid.NewGuid();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-ANOM-DIFF");

        var trace1 = CreateSampleAnomalyTrace();
        var trace2 = new List<WorkflowAnomalyEvent>(trace1)
        {
            [2] = trace1[2] with { Timestamp = "2031-02-04T14:00:00" }
        };

        var res1 = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromTrace(
            "SLG-CASE-2026-0001",
            workflowRunId,
            trace1,
            provenance));

        var res2 = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromTrace(
            "SLG-CASE-2026-0001",
            workflowRunId,
            trace2,
            provenance));

        Assert.True(res1.IsSuccess);
        Assert.True(res2.IsSuccess);
        Assert.NotEqual(res1.Assessment!.InputSha256, res2.Assessment!.InputSha256);
        Assert.NotEqual(res1.Assessment.AssessmentId, res2.Assessment.AssessmentId);
        Assert.Equal(2, await dbContext.WorkflowAnomalyAssessments.CountAsync());
        Assert.Equal(2, fakeClient.CallCount);
    }

    [Fact]
    public async Task HandleAsync_WhenClientFails_DoesNotPersistAssessment()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var failingClient = new FailingWorkflowAnomalyClient(
            WorkflowAnomalyErrorCode.ServiceUnavailable,
            "FastAPI anomaly service unavailable.");
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            failingClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        var workflowRunId = Guid.NewGuid();
        var result = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromTrace(
            "SLG-CASE-2026-0001",
            workflowRunId,
            CreateSampleAnomalyTrace(),
            CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-FAIL")));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Assessment);
        Assert.NotNull(result.Error);
        Assert.Equal(WorkflowAnomalyErrorCode.ServiceUnavailable, result.Error!.Code);
        Assert.Equal(0, await dbContext.WorkflowAnomalyAssessments.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_MalformedUnavailableOrIneligibleSourceProjection_FailsClosedWithoutCallingClient()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient("SLG-CASE-2026-0001", 0.50, FrozenThreshold, false);
        var timeProvider = new FakeTimeProvider(FixedUtcNow);

        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            timeProvider,
            projectionStore);

        // 1. Missing projection in store
        var missingProjectionId = Guid.NewGuid();
        var missingResult = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(
            missingProjectionId,
            "SLG-CASE-2026-0001",
            Guid.NewGuid()));
        Assert.False(missingResult.IsSuccess);
        Assert.Equal(WorkflowAnomalyErrorCode.InvalidInput, missingResult.Error!.Code);
        Assert.Contains("not found", missingResult.Error.Message);

        // 2. Ongoing partial projection ineligible for anomaly scoring
        var ongoingRunId = Guid.NewGuid();
        var ongoingProjection = CreateOngoingProjection(ongoingRunId, "SLG-CASE-2026-0002");
        await projectionStore.UpsertAsync(ongoingProjection);

        var ongoingResult = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(
            ongoingProjection.ProjectionId,
            "SLG-CASE-2026-0002",
            ongoingRunId));
        Assert.False(ongoingResult.IsSuccess);
        Assert.Equal(WorkflowAnomalyErrorCode.InvalidInput, ongoingResult.Error!.Code);

        // 3. Completed projection missing explicit naive AnomalyInputTimestamp (refuses to strip offset)
        var noNaiveRunId = Guid.NewGuid();
        var noNaiveProjection = CreateCompletedProjectionWithoutNaiveTimestamps(noNaiveRunId, "SLG-CASE-2026-0003");
        await projectionStore.UpsertAsync(noNaiveProjection);

        var noNaiveResult = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromProjection(
            noNaiveProjection.ProjectionId,
            "SLG-CASE-2026-0003",
            noNaiveRunId));
        Assert.False(noNaiveResult.IsSuccess);
        Assert.Equal(WorkflowAnomalyErrorCode.InvalidInput, noNaiveResult.Error!.Code);
        Assert.Contains("AnomalyInputTimestamp", noNaiveResult.Error.Message);

        // 4. Direct trace not ending in "DS Final Notification"
        var nonTerminalTrace = new[]
        {
            new WorkflowAnomalyEvent(1, "Application Created", "DS_OFFICE", "SYNTH_OFFICER_01", "2031-02-03T08:00:00"),
            new WorkflowAnomalyEvent(2, "Divisional Secretary Reckoning", "DS_OFFICE", "SYNTH_OFFICER_02", "2031-02-04T07:00:00")
        };
        var nonTerminalResult = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromTrace(
            "SLG-CASE-2026-0004",
            Guid.NewGuid(),
            nonTerminalTrace,
            CreateValidPp1Provenance("Component3.SyntheticFixture", "RUN-4", "PP1-C3-04")));
        Assert.False(nonTerminalResult.IsSuccess);
        Assert.Equal(WorkflowAnomalyErrorCode.InvalidInput, nonTerminalResult.Error!.Code);

        Assert.Equal(0, fakeClient.CallCount);
        Assert.Equal(0, await dbContext.WorkflowAnomalyAssessments.CountAsync());
    }

    [Fact]
    public async Task HandleAsync_PreCanceledToken_ThrowsOperationCanceledException()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var projectionStore = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);
        var fakeClient = new RecordingWorkflowAnomalyClient("SLG-CASE-2026-0001", 0.50, FrozenThreshold, false);
        var handler = new AssessWorkflowAnomalyCommandHandler(
            fakeClient,
            assessmentStore,
            new FakeTimeProvider(FixedUtcNow),
            projectionStore);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.HandleAsync(
                AssessWorkflowAnomalyCommand.FromTrace(
                    "SLG-CASE-2026-0001",
                    Guid.NewGuid(),
                    CreateSampleAnomalyTrace(),
                    CreateValidPp1Provenance("Component3.SyntheticFixture", "RUN-1", "PP1-C3-01")),
                cts.Token));
    }

    [Fact]
    public async Task Queries_GetByIdAndBoundedHistoryByCaseAndWorkflowRun_ReturnDeterministicNewestFirstAndEnforceLimits()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var assessmentStore = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var workflowRunId = Guid.NewGuid();
        const string caseId = "SLG-CASE-2026-0001";

        var baseTrace = CreateSampleAnomalyTrace();
        var traceEarlier = new List<WorkflowAnomalyEvent>(baseTrace)
        {
            [2] = baseTrace[2] with { Timestamp = "2031-02-04T13:00:00" }
        };
        var traceLater = new List<WorkflowAnomalyEvent>(baseTrace)
        {
            [2] = baseTrace[2] with { Timestamp = "2031-02-04T15:00:00" }
        };

        var assessmentEarlier = CreateAssessment(
            caseId,
            workflowRunId,
            traceEarlier,
            anomalyScore: 0.4500,
            assessedAtUtc: FixedUtcNow.AddMinutes(-10));
        var assessmentLater = CreateAssessment(
            caseId,
            workflowRunId,
            traceLater,
            anomalyScore: 0.6200,
            assessedAtUtc: FixedUtcNow);

        await assessmentStore.UpsertAsync(assessmentEarlier);
        await assessmentStore.UpsertAsync(assessmentLater);

        var byIdHandler = new GetWorkflowAnomalyAssessmentQueryHandler(assessmentStore);
        var byCaseHandler = new GetWorkflowAnomalyHistoryByCaseQueryHandler(assessmentStore);
        var byRunHandler = new GetWorkflowAnomalyHistoryByWorkflowRunQueryHandler(assessmentStore);

        var fetchedById = await byIdHandler.HandleAsync(new GetWorkflowAnomalyAssessmentQuery(assessmentLater.AssessmentId));
        Assert.NotNull(fetchedById);
        Assert.Equal(assessmentLater.AssessmentId, fetchedById!.AssessmentId);

        var caseHistory = await byCaseHandler.HandleAsync(new GetWorkflowAnomalyHistoryByCaseQuery(caseId, Limit: 10));
        Assert.Equal(2, caseHistory.Count);
        Assert.Equal(new[] { assessmentLater.AssessmentId, assessmentEarlier.AssessmentId }, caseHistory.Select(x => x.AssessmentId));

        var limitedRunHistory = await byRunHandler.HandleAsync(new GetWorkflowAnomalyHistoryByWorkflowRunQuery(workflowRunId, Limit: 1));
        Assert.Single(limitedRunHistory);
        Assert.Equal(assessmentLater.AssessmentId, limitedRunHistory[0].AssessmentId);

        // Invalid limits (< 1 or > 200) must throw ArgumentOutOfRangeException
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            byCaseHandler.HandleAsync(new GetWorkflowAnomalyHistoryByCaseQuery(caseId, Limit: 0)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            byCaseHandler.HandleAsync(new GetWorkflowAnomalyHistoryByCaseQuery(caseId, Limit: 201)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            byRunHandler.HandleAsync(new GetWorkflowAnomalyHistoryByWorkflowRunQuery(workflowRunId, Limit: -1)));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            byRunHandler.HandleAsync(new GetWorkflowAnomalyHistoryByWorkflowRunQuery(workflowRunId, Limit: 250)));
    }

    // =========================================================================
    // Helpers & Fakes
    // =========================================================================

    private static GovernanceIntelligenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase($"Phase3B_AnomalyUnitTest_{Guid.NewGuid():N}")
            .Options;
        return new GovernanceIntelligenceDbContext(options);
    }

    private static GovernanceInputProvenance CreateValidPp1Provenance(
        string sourceSystem,
        string sourceRecordId,
        string syntheticSeedId)
    {
        return GovernanceInputProvenance.CreatePp1Fixture(
            sourceSystem: sourceSystem,
            sourceRecordId: sourceRecordId,
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            syntheticSeedId: syntheticSeedId,
            generatedAtUtc: FixedUtcNow);
    }

    private static IReadOnlyList<WorkflowAnomalyEvent> CreateSampleAnomalyTrace()
    {
        return new[]
        {
            new WorkflowAnomalyEvent(1, "Application Created", "DS_OFFICE", "SYNTH_OFFICER_01", "2031-02-03T08:00:00"),
            new WorkflowAnomalyEvent(2, "Divisional Secretary Reckoning", "DS_OFFICE", "SYNTH_OFFICER_02", "2031-02-04T07:00:00"),
            new WorkflowAnomalyEvent(3, "Env Clearance Requested", "CEA", "SYNTH_OFFICER_02", "2031-02-04T12:00:00"),
            new WorkflowAnomalyEvent(4, "DS Final Notification", "DS_OFFICE", "SYNTH_OFFICER_03", "2031-02-05T09:00:00")
        };
    }

    private static Component3WorkflowGovernanceProjection CreateCompletedProjectionWithNaiveTimestamps(
        Guid workflowRunId,
        string caseId)
    {
        var colomboOffset = TimeSpan.FromHours(5.5);
        var events = new[]
        {
            new Component3WorkflowProjectionEvent(
                "EVT-1", 1, "Application Created", "STAGE_01", "Clerk", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 3, 8, 0, 0, colomboOffset), "Completed", "REF-1",
                resource: "SYNTH_OFFICER_01", anomalyInputTimestamp: "2031-02-03T08:00:00"),
            new Component3WorkflowProjectionEvent(
                "EVT-2", 2, "Divisional Secretary Reckoning", "STAGE_02", "DS", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 4, 7, 0, 0, colomboOffset), "Completed", "REF-2",
                resource: "SYNTH_OFFICER_02", anomalyInputTimestamp: "2031-02-04T07:00:00"),
            new Component3WorkflowProjectionEvent(
                "EVT-3", 3, "Env Clearance Requested", "STAGE_03", "Officer", "CEA",
                new DateTimeOffset(2031, 2, 4, 12, 0, 0, colomboOffset), "Completed", "REF-3",
                resource: "SYNTH_OFFICER_02", anomalyInputTimestamp: "2031-02-04T12:00:00"),
            new Component3WorkflowProjectionEvent(
                "EVT-4", 4, "DS Final Notification", "STAGE_FINAL", "DS", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 5, 9, 0, 0, colomboOffset), "Approved", "REF-4",
                resource: "SYNTH_OFFICER_03", anomalyInputTimestamp: "2031-02-05T09:00:00")
        };

        var startBoundary = new TimingBoundaryEvidence(
            events[0].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3", "EVT-1", caseId, workflowRunId);
        var handoverBoundary = new TimingBoundaryEvidence(
            events[^1].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3", "EVT-4", caseId, workflowRunId);
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-PROJ-01");

        var sha256 = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "STAGE_FINAL",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            provenance);

        return new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(),
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "STAGE_FINAL",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            sha256,
            provenance,
            FixedUtcNow);
    }

    private static Component3WorkflowGovernanceProjection CreateCompletedProjectionWithoutNaiveTimestamps(
        Guid workflowRunId,
        string caseId)
    {
        var colomboOffset = TimeSpan.FromHours(5.5);
        var events = new[]
        {
            new Component3WorkflowProjectionEvent(
                "EVT-1", 1, "Application Created", "STAGE_01", "Clerk", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 3, 8, 0, 0, colomboOffset), "Completed", "REF-1"),
            new Component3WorkflowProjectionEvent(
                "EVT-2", 2, "DS Final Notification", "STAGE_FINAL", "DS", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 5, 9, 0, 0, colomboOffset), "Approved", "REF-2")
        };

        var startBoundary = new TimingBoundaryEvidence(
            events[0].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3", "EVT-1", caseId, workflowRunId);
        var handoverBoundary = new TimingBoundaryEvidence(
            events[^1].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3", "EVT-2", caseId, workflowRunId);
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-NONAIVE");

        var sha256 = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "STAGE_FINAL",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            provenance);

        return new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(),
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "STAGE_FINAL",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            sha256,
            provenance,
            FixedUtcNow);
    }

    private static Component3WorkflowGovernanceProjection CreateOngoingProjection(
        Guid workflowRunId,
        string caseId)
    {
        var colomboOffset = TimeSpan.FromHours(5.5);
        var events = new[]
        {
            new Component3WorkflowProjectionEvent(
                "EVT-1", 1, "Application Created", "STAGE_01", "Clerk", "DS_OFFICE",
                new DateTimeOffset(2031, 2, 3, 8, 0, 0, colomboOffset), "Completed", "REF-1",
                resource: "SYNTH_OFFICER_01", anomalyInputTimestamp: "2031-02-03T08:00:00")
        };

        var startBoundary = new TimingBoundaryEvidence(
            events[0].Timestamp, TimingBoundaryEvidenceStatus.Confirmed, "Component3", "EVT-1", caseId, workflowRunId);
        var handoverBoundary = new TimingBoundaryEvidence(
            null, TimingBoundaryEvidenceStatus.Unavailable, "Component3", "PENDING", caseId, workflowRunId);
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-ONGOING");

        var sha256 = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Ongoing,
            Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring,
            "STAGE_01",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            provenance);

        return new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(),
            workflowRunId,
            caseId,
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Ongoing,
            Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring,
            "STAGE_01",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            events,
            startBoundary,
            handoverBoundary,
            sha256,
            provenance,
            FixedUtcNow);
    }

    private static WorkflowAnomalyAssessment CreateAssessment(
        string caseId,
        Guid workflowRunId,
        IReadOnlyList<WorkflowAnomalyEvent> trace,
        double anomalyScore,
        DateTimeOffset assessedAtUtc)
    {
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", workflowRunId.ToString("D"), "PP1-C3-HIST");
        var inputSha = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(workflowRunId, caseId, trace, provenance);
        var flagged = anomalyScore > FrozenThreshold;

        return new WorkflowAnomalyAssessment(
            assessmentId: Guid.NewGuid(),
            workflowRunId: workflowRunId,
            caseId: caseId,
            workflowProjectionId: null,
            modelVersion: FrozenModelVersion,
            modelArtifactSha256: WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1,
            anomalyScore: anomalyScore,
            threshold: FrozenThreshold,
            flagged: flagged,
            eventCount: 4,
            elapsedDays: 2.041667,
            maxGapHours: 23.0,
            meanGapHours: 16.333333,
            uniqueActivities: 4,
            repeatedActivityCount: 0,
            resourceHandoffs: 2,
            institutionSwitches: 2,
            distinctResources: 3,
            advisoryNote: "Advisory decision-support signal only.",
            inputSha256: inputSha,
            provenance: provenance,
            assessedAtUtc: assessedAtUtc);
    }

    private sealed class RecordingWorkflowAnomalyClient : IWorkflowAnomalyClient
    {
        private readonly string _caseId;
        private readonly double _anomalyScore;
        private readonly double _threshold;
        private readonly bool _flagged;

        public RecordingWorkflowAnomalyClient(
            string caseId,
            double anomalyScore,
            double threshold,
            bool flagged)
        {
            _caseId = caseId;
            _anomalyScore = anomalyScore;
            _threshold = threshold;
            _flagged = flagged;
        }

        public int CallCount { get; private set; }
        public string? LastSubmittedCaseId { get; private set; }
        public IReadOnlyList<WorkflowAnomalyEvent>? LastSubmittedEvents { get; private set; }

        public Task<WorkflowAnomalyResult> PredictAnomalyAsync(
            string caseId,
            IReadOnlyList<WorkflowAnomalyEvent> events,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastSubmittedCaseId = caseId;
            LastSubmittedEvents = events.ToList();

            var prediction = new WorkflowAnomalyPrediction(
                CaseId: _caseId,
                AnomalyScore: _anomalyScore,
                Threshold: _threshold,
                Flagged: _flagged,
                FeatureValues: new WorkflowFeatureValues(
                    EventCount: 4,
                    ElapsedDays: 2.041667,
                    MaxGapHours: 23.0,
                    MeanGapHours: 16.333333,
                    UniqueActivities: 4,
                    RepeatedActivityCount: 0,
                    ResourceHandoffs: 2,
                    InstitutionSwitches: 2,
                    DistinctResources: 3),
                ModelVersion: FrozenModelVersion,
                AdvisoryNote: "Advisory workflow anomaly assessment only; requires human review.");

            return Task.FromResult(WorkflowAnomalyResult.Success(prediction));
        }
    }

    private sealed class FailingWorkflowAnomalyClient : IWorkflowAnomalyClient
    {
        private readonly WorkflowAnomalyErrorCode _code;
        private readonly string _message;

        public FailingWorkflowAnomalyClient(WorkflowAnomalyErrorCode code, string message)
        {
            _code = code;
            _message = message;
        }

        public Task<WorkflowAnomalyResult> PredictAnomalyAsync(
            string caseId,
            IReadOnlyList<WorkflowAnomalyEvent> events,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WorkflowAnomalyResult.Failure(_code, _message));
        }
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FakeTimeProvider(DateTimeOffset utcNow) => _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
