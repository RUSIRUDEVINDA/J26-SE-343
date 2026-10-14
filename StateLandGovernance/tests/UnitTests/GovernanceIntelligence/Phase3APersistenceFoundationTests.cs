using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Npgsql;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.DependencyInjection;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Configurations;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class Phase3APersistenceFoundationTests
{
    private const string ConnectionEnvVar = "COMPONENT4_TEST_POSTGRES_CONNECTION";
    private const string AllowedTestDatabasePrefix = "component4_screening_test_";
    private static readonly DateTimeOffset FixedUtcNow = new(2026, 10, 12, 6, 0, 0, TimeSpan.Zero);

    // =========================================================================
    // 1. Provenance Validation Tests
    // =========================================================================

    [Fact]
    public void Provenance_ValidPp1Fixture_SucceedsAndPassesPp1Validation()
    {
        var provenance = GovernanceInputProvenance.CreatePp1Fixture(
            sourceSystem: "Component1.SyntheticSeed",
            sourceRecordId: "SLG-CASE-2026-0001",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            syntheticSeedId: "PP1-C1-CASE-01",
            generatedAtUtc: FixedUtcNow);

        Assert.True(provenance.IsSynthetic);
        Assert.Equal(GovernanceProvenanceKind.SyntheticPp1Seed, provenance.ProvenanceKind);
        Assert.Equal("PP1-SYNTH-2026-10-v1", provenance.SyntheticSeedVersion);
        Assert.Equal(GovernanceProvenancePurpose.PP1Demonstration, provenance.Purpose);
        provenance.ValidateForPp1Demonstration();
    }

    [Theory]
    [InlineData(null, "PP1-SYNTH-2026-10-v1")]
    [InlineData("   ", "PP1-SYNTH-2026-10-v1")]
    [InlineData("PP1-SEED-01", null)]
    [InlineData("PP1-SEED-01", "   ")]
    public void Provenance_SyntheticMissingSeedIdOrVersion_ThrowsArgumentException(
        string? seedId,
        string? seedVersion)
    {
        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1.SyntheticSeed",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            isSynthetic: true,
            provenanceKind: GovernanceProvenanceKind.SyntheticFixture,
            syntheticSeedId: seedId,
            syntheticSeedVersion: seedVersion,
            generatedAtUtc: FixedUtcNow,
            purpose: GovernanceProvenancePurpose.PP1Demonstration));
    }

    [Fact]
    public void Provenance_NonSyntheticWithSyntheticFixtureOrSeedValues_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            isSynthetic: false,
            provenanceKind: GovernanceProvenanceKind.SyntheticFixture,
            syntheticSeedId: null,
            syntheticSeedVersion: null,
            generatedAtUtc: FixedUtcNow,
            purpose: GovernanceProvenancePurpose.ProductionPeerIntegration));

        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            isSynthetic: false,
            provenanceKind: GovernanceProvenanceKind.RealComponent1,
            syntheticSeedId: "SEED-01",
            syntheticSeedVersion: null,
            generatedAtUtc: FixedUtcNow,
            purpose: GovernanceProvenancePurpose.ProductionPeerIntegration));

        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            isSynthetic: false,
            provenanceKind: GovernanceProvenanceKind.RealComponent1,
            syntheticSeedId: null,
            syntheticSeedVersion: "PP1-SYNTH-2026-10-v1",
            generatedAtUtc: FixedUtcNow,
            purpose: GovernanceProvenancePurpose.ProductionPeerIntegration));
    }

    [Fact]
    public void Provenance_NonUtcTimestamps_ThrowsArgumentException()
    {
        var colomboTime = new DateTimeOffset(2026, 10, 12, 11, 30, 0, TimeSpan.FromHours(5.5));

        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: colomboTime,
            isSynthetic: true,
            provenanceKind: GovernanceProvenanceKind.SyntheticFixture,
            syntheticSeedId: "SEED-01",
            syntheticSeedVersion: "PP1-SYNTH-2026-10-v1",
            generatedAtUtc: FixedUtcNow,
            purpose: GovernanceProvenancePurpose.PP1Demonstration));

        Assert.Throws<ArgumentException>(() => new GovernanceInputProvenance(
            sourceSystem: "Component1",
            sourceRecordId: "REC-01",
            sourceRecordVersion: "v1",
            sourceObservedAtUtc: FixedUtcNow,
            isSynthetic: true,
            provenanceKind: GovernanceProvenanceKind.SyntheticFixture,
            syntheticSeedId: "SEED-01",
            syntheticSeedVersion: "PP1-SYNTH-2026-10-v1",
            generatedAtUtc: colomboTime,
            purpose: GovernanceProvenancePurpose.PP1Demonstration));
    }

    // =========================================================================
    // 2. Component 1 Case Projection Validation & Persistence Round-Trip
    // =========================================================================

    [Fact]
    public async Task Component1Projection_ValidProjection_RoundTripsAndIsIdempotent()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresComponent1GovernanceCaseProjectionStore(dbContext);
        var provenance = CreateValidPp1Provenance("Component1.SyntheticFixture", "SLG-CASE-2026-0001", "PP1-C1-01");

        var sha256 = Component1GovernanceCaseProjection.ComputeCanonicalSha256(
            caseId: "SLG-CASE-2026-0001",
            landParcelId: "LP-CMB-0001",
            divisionCode: "DS-CMB-01",
            districtName: "Colombo",
            provinceName: "Western",
            gnDivisionCode: "GN-101A",
            requestedLandUseCategory: "Commercial",
            extentHa: 2.4500m,
            ownershipEvidenceStatus: Component1OwnershipEvidenceStatus.EvidenceProvided,
            protectedZoneOverlapState: Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected,
            overlappingSensitiveZoneCodes: new[] { "WETLAND-BUFFER-02", "FOREST-RES-01" },
            boundaryConfirmationStatus: Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            encumbranceOrDisputeFlag: true,
            parcelEvidenceRefs: new[] { "SURVEY-PLAN-2026-11", "DEED-EXTRACT-09" },
            provenance: provenance);

        var projection = new Component1GovernanceCaseProjection(
            projectionId: Guid.NewGuid(),
            caseId: "SLG-CASE-2026-0001",
            landParcelId: "LP-CMB-0001",
            divisionCode: "DS-CMB-01",
            districtName: "Colombo",
            provinceName: "Western",
            gnDivisionCode: "GN-101A",
            requestedLandUseCategory: "Commercial",
            extentHa: 2.4500m,
            ownershipEvidenceStatus: Component1OwnershipEvidenceStatus.EvidenceProvided,
            protectedZoneOverlapState: Component1ProtectedZoneOverlapState.ProtectedZoneOverlapDetected,
            overlappingSensitiveZoneCodes: new[] { "WETLAND-BUFFER-02", "FOREST-RES-01" },
            boundaryConfirmationStatus: Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            encumbranceOrDisputeFlag: true,
            parcelEvidenceRefs: new[] { "SURVEY-PLAN-2026-11", "DEED-EXTRACT-09" },
            inputSha256: sha256,
            provenance: provenance,
            ingestedAtUtc: FixedUtcNow);

        var persisted = await store.UpsertAsync(projection);
        var duplicateUpsert = await store.UpsertAsync(projection);
        var byId = await store.GetByIdAsync(projection.ProjectionId);

        Assert.NotNull(byId);
        Assert.Equal(projection.ProjectionId, byId!.ProjectionId);
        Assert.Equal(projection.ProjectionId, duplicateUpsert.ProjectionId);
        Assert.Equal(1, await dbContext.Component1CaseProjections.CountAsync());
        Assert.Equal(new[] { "FOREST-RES-01", "WETLAND-BUFFER-02" }, byId.OverlappingSensitiveZoneCodes);
        Assert.Equal(new[] { "DEED-EXTRACT-09", "SURVEY-PLAN-2026-11" }, byId.ParcelEvidenceRefs);
        Assert.Equal(persisted.InputSha256, byId.InputSha256);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1.25)]
    public void Component1Projection_NonPositiveExtentHa_ThrowsArgumentOutOfRangeException(decimal invalidExtent)
    {
        var provenance = CreateValidPp1Provenance("Component1.SyntheticFixture", "SLG-CASE-2026-0001", "PP1-C1-01");

        Assert.Throws<ArgumentOutOfRangeException>(() => new Component1GovernanceCaseProjection(
            projectionId: Guid.NewGuid(),
            caseId: "SLG-CASE-2026-0001",
            landParcelId: "LP-CMB-0001",
            divisionCode: "DS-CMB-01",
            districtName: "Colombo",
            provinceName: "Western",
            gnDivisionCode: "GN-101A",
            requestedLandUseCategory: "Commercial",
            extentHa: invalidExtent,
            ownershipEvidenceStatus: Component1OwnershipEvidenceStatus.EvidenceProvided,
            protectedZoneOverlapState: Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
            overlappingSensitiveZoneCodes: Array.Empty<string>(),
            boundaryConfirmationStatus: Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            encumbranceOrDisputeFlag: false,
            parcelEvidenceRefs: new[] { "SURVEY-PLAN-2026-11" },
            inputSha256: new string('a', 64),
            provenance: provenance,
            ingestedAtUtc: FixedUtcNow));
    }

    [Theory]
    [InlineData("", "LP-01")]
    [InlineData("   ", "LP-01")]
    [InlineData("CASE-01", "")]
    [InlineData("CASE-01", "   ")]
    public void Component1Projection_BlankCaseIdOrLandParcelId_ThrowsArgumentException(
        string caseId,
        string landParcelId)
    {
        var provenance = CreateValidPp1Provenance("Component1.SyntheticFixture", "SLG-CASE-2026-0001", "PP1-C1-01");

        Assert.Throws<ArgumentException>(() => new Component1GovernanceCaseProjection(
            projectionId: Guid.NewGuid(),
            caseId: caseId,
            landParcelId: landParcelId,
            divisionCode: "DS-CMB-01",
            districtName: "Colombo",
            provinceName: "Western",
            gnDivisionCode: "GN-101A",
            requestedLandUseCategory: "Commercial",
            extentHa: 1.0m,
            ownershipEvidenceStatus: Component1OwnershipEvidenceStatus.EvidenceProvided,
            protectedZoneOverlapState: Component1ProtectedZoneOverlapState.NoProtectedZoneOverlap,
            overlappingSensitiveZoneCodes: Array.Empty<string>(),
            boundaryConfirmationStatus: Component1BoundaryConfirmationStatus.BoundaryConfirmed,
            encumbranceOrDisputeFlag: false,
            parcelEvidenceRefs: new[] { "SURVEY-PLAN-2026-11" },
            inputSha256: new string('a', 64),
            provenance: provenance,
            ingestedAtUtc: FixedUtcNow));
    }

    // =========================================================================
    // 3. Component 3 Workflow Projection Validation & Persistence Round-Trip
    // =========================================================================

    [Fact]
    public async Task Component3Projection_CompletedAndOngoingTraces_RoundTripWithExactEventOrder()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresComponent3WorkflowGovernanceProjectionStore(dbContext);

        // 3a. Completed trace ending in "DS Final Notification"
        var completedRunId = Guid.NewGuid();
        var completedProvenance = CreateValidPp1Provenance("Component3.SyntheticFixture", completedRunId.ToString("D"), "PP1-C3-01");
        var completedEvents = new[]
        {
            new Component3WorkflowProjectionEvent(
                "EVT-1", 1, "Application Registered", "STAGE_01", "SubjectClerk", "DS_OFFICE",
                new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.FromHours(5.5)), "Completed", "REF-1"),
            new Component3WorkflowProjectionEvent(
                "EVT-2", 2, "Complete Proposal Roadmap Confirmed", "STAGE_02", "LandOfficer", "DS_OFFICE",
                new DateTimeOffset(2026, 2, 5, 10, 30, 0, TimeSpan.FromHours(5.5)), "Completed", "REF-2"),
            new Component3WorkflowProjectionEvent(
                "EVT-3", 3, "DS Final Notification", "STAGE_FINAL", "DivisionalSecretary", "DS_OFFICE",
                new DateTimeOffset(2026, 3, 20, 15, 0, 0, TimeSpan.FromHours(5.5)), "Approved", "REF-3")
        };

        var roadmapStart = new TimingBoundaryEvidence(
            OccurredAt: completedEvents[1].Timestamp,
            Status: TimingBoundaryEvidenceStatus.Confirmed,
            Source: "Component3.StageTransition",
            SourceReference: "EVT-2",
            CaseId: "SLG-CASE-2026-0001",
            WorkflowRunId: completedRunId);

        var approvalHandover = new TimingBoundaryEvidence(
            OccurredAt: completedEvents[2].Timestamp,
            Status: TimingBoundaryEvidenceStatus.Confirmed,
            Source: "Component3.StageTransition",
            SourceReference: "EVT-3",
            CaseId: "SLG-CASE-2026-0001",
            WorkflowRunId: completedRunId);

        var completedSha = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            completedRunId,
            "SLG-CASE-2026-0001",
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "STAGE_FINAL",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            completedEvents,
            roadmapStart,
            approvalHandover,
            completedProvenance);

        var completedProjection = new Component3WorkflowGovernanceProjection(
            projectionId: Guid.NewGuid(),
            workflowRunId: completedRunId,
            caseId: "SLG-CASE-2026-0001",
            workflowType: "StateLandLeaseApproval",
            traceStatus: Component3WorkflowTraceStatus.Completed,
            traceCompleteness: Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            currentOrTerminalStageCode: "STAGE_FINAL",
            eventTimestampSemantics: Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            eventTimeZone: "Asia/Colombo",
            events: completedEvents,
            completeProposalRoadmapStart: roadmapStart,
            approvalHandover: approvalHandover,
            inputSha256: completedSha,
            provenance: completedProvenance,
            ingestedAtUtc: FixedUtcNow);

        await store.UpsertAsync(completedProjection);
        var loadedCompleted = await store.GetByIdAsync(completedProjection.ProjectionId);

        Assert.NotNull(loadedCompleted);
        Assert.Equal(3, loadedCompleted!.Events.Count);
        Assert.Equal(new[] { 1, 2, 3 }, loadedCompleted.Events.Select(e => e.EventSeq));
        Assert.Equal("DS Final Notification", loadedCompleted.Events[^1].Activity);

        // 3b. Valid ongoing trace marked PartialTraceIneligibleForAnomalyScoring
        var ongoingRunId = Guid.NewGuid();
        var ongoingProvenance = CreateValidPp1Provenance("Component3.SyntheticFixture", ongoingRunId.ToString("D"), "PP1-C3-02");
        var ongoingEvents = new[]
        {
            new Component3WorkflowProjectionEvent(
                "EVT-10", 1, "Application Registered", "STAGE_01", "SubjectClerk", "DS_OFFICE",
                new DateTimeOffset(2026, 5, 1, 9, 0, 0, TimeSpan.FromHours(5.5)), "Completed", "REF-10")
        };

        var ongoingStart = new TimingBoundaryEvidence(
            OccurredAt: ongoingEvents[0].Timestamp,
            Status: TimingBoundaryEvidenceStatus.Confirmed,
            Source: "Component3.StageTransition",
            SourceReference: "EVT-10",
            CaseId: "SLG-CASE-2026-0002",
            WorkflowRunId: ongoingRunId);

        var ongoingHandover = new TimingBoundaryEvidence(
            OccurredAt: null,
            Status: TimingBoundaryEvidenceStatus.Unavailable,
            Source: "Component3.StageTransition",
            SourceReference: "PENDING",
            CaseId: "SLG-CASE-2026-0002",
            WorkflowRunId: ongoingRunId);

        var ongoingSha = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            ongoingRunId,
            "SLG-CASE-2026-0002",
            "StateLandLeaseApproval",
            Component3WorkflowTraceStatus.Ongoing,
            Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring,
            "STAGE_01",
            Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            "Asia/Colombo",
            ongoingEvents,
            ongoingStart,
            ongoingHandover,
            ongoingProvenance);

        var ongoingProjection = new Component3WorkflowGovernanceProjection(
            projectionId: Guid.NewGuid(),
            workflowRunId: ongoingRunId,
            caseId: "SLG-CASE-2026-0002",
            workflowType: "StateLandLeaseApproval",
            traceStatus: Component3WorkflowTraceStatus.Ongoing,
            traceCompleteness: Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring,
            currentOrTerminalStageCode: "STAGE_01",
            eventTimestampSemantics: Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,
            eventTimeZone: "Asia/Colombo",
            events: ongoingEvents,
            completeProposalRoadmapStart: ongoingStart,
            approvalHandover: ongoingHandover,
            inputSha256: ongoingSha,
            provenance: ongoingProvenance,
            ingestedAtUtc: FixedUtcNow);

        await store.UpsertAsync(ongoingProjection);
        var loadedOngoing = await store.GetByIdAsync(ongoingProjection.ProjectionId);
        Assert.NotNull(loadedOngoing);
        Assert.Equal(Component3WorkflowTraceStatus.Ongoing, loadedOngoing!.TraceStatus);
        Assert.Equal(Component3WorkflowTraceCompleteness.PartialTraceIneligibleForAnomalyScoring, loadedOngoing.TraceCompleteness);
    }

    [Fact]
    public void Component3Projection_InvalidEventSeqChronologyTerminalActivityOrTimeZone_ThrowsArgumentException()
    {
        var runId = Guid.NewGuid();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", runId.ToString("D"), "PP1-C3-ERR");
        var startBoundary = new TimingBoundaryEvidence(
            OccurredAt: FixedUtcNow,
            Status: TimingBoundaryEvidenceStatus.Confirmed,
            Source: "Component3",
            SourceReference: "REF-1",
            CaseId: "SLG-CASE-2026-0099",
            WorkflowRunId: runId);
        var handoverBoundary = new TimingBoundaryEvidence(
            OccurredAt: null,
            Status: TimingBoundaryEvidenceStatus.Unavailable,
            Source: "Component3",
            SourceReference: "REF-2",
            CaseId: "SLG-CASE-2026-0099",
            WorkflowRunId: runId);

        // Non-contiguous EventSeq (1, 3)
        var nonContiguousEvents = new[]
        {
            new Component3WorkflowProjectionEvent("E1", 1, "Application Registered", "S1", "Clerk", "DS", FixedUtcNow, "OK"),
            new Component3WorkflowProjectionEvent("E3", 3, "DS Final Notification", "S2", "DS", "DS", FixedUtcNow.AddHours(1), "OK")
        };

        Assert.Throws<ArgumentException>(() => new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0099", "Lease",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "S2", Component3EventTimestampSemantics.ExactUtcTimestamp, "Asia/Colombo",
            nonContiguousEvents, startBoundary, handoverBoundary, new string('b', 64), provenance, FixedUtcNow));

        // Out-of-order timestamps
        var outOfOrderEvents = new[]
        {
            new Component3WorkflowProjectionEvent("E1", 1, "Application Registered", "S1", "Clerk", "DS", FixedUtcNow.AddHours(2), "OK"),
            new Component3WorkflowProjectionEvent("E2", 2, "DS Final Notification", "S2", "DS", "DS", FixedUtcNow.AddHours(1), "OK")
        };

        Assert.Throws<ArgumentException>(() => new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0099", "Lease",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "S2", Component3EventTimestampSemantics.ExactUtcTimestamp, "Asia/Colombo",
            outOfOrderEvents, startBoundary, handoverBoundary, new string('b', 64), provenance, FixedUtcNow));

        // CompletedTraceEligibleForAnomalyScoring not ending in "DS Final Notification"
        var wrongTerminalEvents = new[]
        {
            new Component3WorkflowProjectionEvent("E1", 1, "Application Registered", "S1", "Clerk", "DS", FixedUtcNow, "OK"),
            new Component3WorkflowProjectionEvent("E2", 2, "Survey Valuation", "S2", "Surveyor", "SURVEY", FixedUtcNow.AddHours(1), "OK")
        };

        Assert.Throws<ArgumentException>(() => new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0099", "Lease",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "S2", Component3EventTimestampSemantics.ExactUtcTimestamp, "Asia/Colombo",
            wrongTerminalEvents, startBoundary, handoverBoundary, new string('b', 64), provenance, FixedUtcNow));

        // Ongoing trace marked CompletedTraceEligibleForAnomalyScoring
        var validEvents = new[]
        {
            new Component3WorkflowProjectionEvent("E1", 1, "DS Final Notification", "S2", "DS", "DS", FixedUtcNow, "OK")
        };

        Assert.Throws<ArgumentException>(() => new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0099", "Lease",
            Component3WorkflowTraceStatus.Ongoing,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "S2", Component3EventTimestampSemantics.ExactUtcTimestamp, "Asia/Colombo",
            validEvents, startBoundary, handoverBoundary, new string('b', 64), provenance, FixedUtcNow));

        // Non-Asia/Colombo EventTimeZone
        Assert.Throws<ArgumentException>(() => new Component3WorkflowGovernanceProjection(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0099", "Lease",
            Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "S2", Component3EventTimestampSemantics.ExactUtcTimestamp, "UTC",
            validEvents, startBoundary, handoverBoundary, new string('b', 64), provenance, FixedUtcNow));
    }

    // =========================================================================
    // 4. Workflow Anomaly Assessment Validation & Persistence Round-Trip
    // =========================================================================

    [Fact]
    public async Task WorkflowAnomalyAssessment_ValidAssessmentWithNineFeatures_RoundTripsAndIsIdempotent()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresWorkflowAnomalyAssessmentStore(dbContext);
        var runId = Guid.NewGuid();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", runId.ToString("D"), "PP1-ANOM-01");

        var assessment = new WorkflowAnomalyAssessment(
            assessmentId: Guid.NewGuid(),
            workflowRunId: runId,
            caseId: "SLG-CASE-2026-0001",
            workflowProjectionId: Guid.NewGuid(),
            modelVersion: WorkflowAnomalyAssessment.FrozenModelVersionV1,
            modelArtifactSha256: WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1,
            anomalyScore: 0.6125,
            threshold: WorkflowAnomalyAssessment.FrozenThresholdV1,
            flagged: true,
            eventCount: 14,
            elapsedDays: 94.5,
            maxGapHours: 480.0,
            meanGapHours: 162.2,
            uniqueActivities: 9,
            repeatedActivityCount: 5,
            resourceHandoffs: 11,
            institutionSwitches: 6,
            distinctResources: 7,
            advisoryNote: "Advisory anomaly indicator only; score is not a probability or confidence value. Human review recommended.",
            inputSha256: new string('c', 64),
            provenance: provenance,
            assessedAtUtc: FixedUtcNow);

        var first = await store.UpsertAsync(assessment);
        var second = await store.UpsertAsync(assessment);
        var loaded = await store.GetByIdAsync(assessment.AssessmentId);

        Assert.NotNull(loaded);
        Assert.Equal(first.AssessmentId, second.AssessmentId);
        Assert.Equal(1, await dbContext.WorkflowAnomalyAssessments.CountAsync());
        Assert.Equal(14, loaded!.EventCount);
        Assert.Equal(94.5, loaded.ElapsedDays);
        Assert.Equal(480.0, loaded.MaxGapHours);
        Assert.Equal(162.2, loaded.MeanGapHours);
        Assert.Equal(9, loaded.UniqueActivities);
        Assert.Equal(5, loaded.RepeatedActivityCount);
        Assert.Equal(11, loaded.ResourceHandoffs);
        Assert.Equal(6, loaded.InstitutionSwitches);
        Assert.Equal(7, loaded.DistinctResources);
    }

    [Fact]
    public void WorkflowAnomalyAssessment_NonFiniteScoresNegativeCountsOrBlankFields_ThrowExceptions()
    {
        var runId = Guid.NewGuid();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", runId.ToString("D"), "PP1-ANOM-ERR");

        // Non-finite score
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowAnomalyAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            WorkflowAnomalyAssessment.FrozenModelVersionV1, null,
            double.NaN, WorkflowAnomalyAssessment.FrozenThresholdV1, false,
            10, 12.0, 24.0, 4.0, 8, 2, 5, 3, 4,
            "Advisory note", new string('c', 64), provenance, FixedUtcNow));

        // Negative feature count
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowAnomalyAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            WorkflowAnomalyAssessment.FrozenModelVersionV1, null,
            0.42, WorkflowAnomalyAssessment.FrozenThresholdV1, false,
            10, 12.0, 24.0, 4.0, 8, -1, 5, 3, 4,
            "Advisory note", new string('c', 64), provenance, FixedUtcNow));

        // Blank model version or advisory note
        Assert.Throws<ArgumentException>(() => new WorkflowAnomalyAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            "   ", null,
            0.42, WorkflowAnomalyAssessment.FrozenThresholdV1, false,
            10, 12.0, 24.0, 4.0, 8, 0, 5, 3, 4,
            "Advisory note", new string('c', 64), provenance, FixedUtcNow));

        Assert.Throws<ArgumentException>(() => new WorkflowAnomalyAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            WorkflowAnomalyAssessment.FrozenModelVersionV1, null,
            0.42, WorkflowAnomalyAssessment.FrozenThresholdV1, false,
            10, 12.0, 24.0, 4.0, 8, 0, 5, 3, 4,
            "   ", new string('c', 64), provenance, FixedUtcNow));
    }

    // =========================================================================
    // 5. Lease Approval Timing Assessment Validation & Persistence Round-Trip
    // =========================================================================

    [Fact]
    public async Task LeaseApprovalTimingAssessment_AllFourTimingStates_RoundTripThroughStore()
    {
        await using var dbContext = CreateInMemoryDbContext();
        var store = new PostgresLeaseApprovalTimingAssessmentStore(dbContext);
        var calculator = new LeaseApprovalTimingCalculator();

        // 5a. Completed Within Expected Two Calendar Months
        var run1 = Guid.NewGuid();
        var prov1 = CreateValidPp1Provenance("Component3.SyntheticFixture", run1.ToString("D"), "PP1-TIMING-01");
        var input1 = new LeaseApprovalTimingAssessmentInput(
            "SLG-CASE-2026-0001",
            run1,
            LeaseApprovalWorkflowState.Completed,
            new TimingBoundaryEvidence(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.FromHours(5.5)), TimingBoundaryEvidenceStatus.Confirmed, "C3", "E1", "SLG-CASE-2026-0001", run1),
            new TimingBoundaryEvidence(new DateTimeOffset(2026, 3, 10, 14, 0, 0, TimeSpan.FromHours(5.5)), TimingBoundaryEvidenceStatus.Confirmed, "C3", "E2", "SLG-CASE-2026-0001", run1),
            new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.FromHours(5.5)));
        var record1 = LeaseApprovalTimingAssessment.FromCalculationResult(
            Guid.NewGuid(),
            calculator.Assess(input1),
            null,
            LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(input1, prov1),
            prov1,
            FixedUtcNow);

        // 5b. Completed Exceeded Expected Two Calendar Months
        var run2 = Guid.NewGuid();
        var prov2 = CreateValidPp1Provenance("Component3.SyntheticFixture", run2.ToString("D"), "PP1-TIMING-02");
        var input2 = new LeaseApprovalTimingAssessmentInput(
            "SLG-CASE-2026-0002",
            run2,
            LeaseApprovalWorkflowState.Completed,
            new TimingBoundaryEvidence(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.FromHours(5.5)), TimingBoundaryEvidenceStatus.Confirmed, "C3", "E1", "SLG-CASE-2026-0002", run2),
            new TimingBoundaryEvidence(new DateTimeOffset(2026, 4, 5, 14, 0, 0, TimeSpan.FromHours(5.5)), TimingBoundaryEvidenceStatus.Confirmed, "C3", "E2", "SLG-CASE-2026-0002", run2),
            new DateTimeOffset(2026, 4, 6, 10, 0, 0, TimeSpan.FromHours(5.5)));
        var record2 = LeaseApprovalTimingAssessment.FromCalculationResult(
            Guid.NewGuid(),
            calculator.Assess(input2),
            null,
            LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(input2, prov2),
            prov2,
            FixedUtcNow);

        // 5c. Ongoing Exceeded Expected Two Calendar Months
        var run3 = Guid.NewGuid();
        var prov3 = CreateValidPp1Provenance("Component3.SyntheticFixture", run3.ToString("D"), "PP1-TIMING-03");
        var input3 = new LeaseApprovalTimingAssessmentInput(
            "SLG-CASE-2026-0003",
            run3,
            LeaseApprovalWorkflowState.Ongoing,
            new TimingBoundaryEvidence(new DateTimeOffset(2026, 1, 15, 9, 0, 0, TimeSpan.FromHours(5.5)), TimingBoundaryEvidenceStatus.Confirmed, "C3", "E1", "SLG-CASE-2026-0003", run3),
            null,
            new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(5.5)));
        var record3 = LeaseApprovalTimingAssessment.FromCalculationResult(
            Guid.NewGuid(),
            calculator.Assess(input3),
            null,
            LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(input3, prov3),
            prov3,
            FixedUtcNow);

        // 5d. Unavailable Evidence
        var run4 = Guid.NewGuid();
        var prov4 = CreateValidPp1Provenance("Component3.SyntheticFixture", run4.ToString("D"), "PP1-TIMING-04");
        var input4 = new LeaseApprovalTimingAssessmentInput(
            "SLG-CASE-2026-0004",
            run4,
            LeaseApprovalWorkflowState.Ongoing,
            null,
            null,
            new DateTimeOffset(2026, 4, 1, 10, 0, 0, TimeSpan.FromHours(5.5)));
        var record4 = LeaseApprovalTimingAssessment.FromCalculationResult(
            Guid.NewGuid(),
            calculator.Assess(input4),
            null,
            LeaseApprovalTimingAssessment.ComputeCanonicalInputSha256(input4, prov4),
            prov4,
            FixedUtcNow);

        await store.UpsertAsync(record1);
        await store.UpsertAsync(record2);
        await store.UpsertAsync(record3);
        await store.UpsertAsync(record4);

        var loaded1 = await store.GetByIdAsync(record1.AssessmentId);
        var loaded2 = await store.GetByIdAsync(record2.AssessmentId);
        var loaded3 = await store.GetByIdAsync(record3.AssessmentId);
        var loaded4 = await store.GetByIdAsync(record4.AssessmentId);

        Assert.Equal(LeaseApprovalTimingState.WithinExpectedTwoCalendarMonths, loaded1!.TimingState);
        Assert.Equal(0, loaded1.DaysBeyondExpected);

        Assert.Equal(LeaseApprovalTimingState.ExceededExpectedTwoCalendarMonths, loaded2!.TimingState);
        Assert.Equal(21, loaded2.DaysBeyondExpected);

        Assert.Equal(LeaseApprovalTimingState.OngoingExceededExpectedTwoCalendarMonths, loaded3!.TimingState);
        Assert.Equal(17, loaded3.DaysBeyondExpected);

        Assert.Equal(LeaseApprovalTimingState.UnavailableEvidence, loaded4!.TimingState);
        Assert.Null(loaded4.DaysBeyondExpected);
    }

    [Fact]
    public void LeaseApprovalTimingAssessment_UnavailableWithDaysBeyondOrMismatchedExpectedDateOrWrongTimeZone_ThrowsArgumentException()
    {
        var runId = Guid.NewGuid();
        var provenance = CreateValidPp1Provenance("Component3.SyntheticFixture", runId.ToString("D"), "PP1-TIMING-ERR");

        // UnavailableEvidence with non-null DaysBeyondExpected
        Assert.Throws<ArgumentException>(() => new LeaseApprovalTimingAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            LeaseApprovalTimingCalculator.PolicyVersion, "Asia/Colombo",
            new DateOnly(2026, 4, 1), null, null, null, null,
            daysBeyondExpected: 5,
            LeaseApprovalTimingState.UnavailableEvidence,
            "RoadmapStartUnavailable",
            LeaseApprovalTimingCalculator.ExpertReportedProvenanceNote,
            new string('d', 64), provenance, FixedUtcNow));

        // Mismatched ExpectedApprovalHandoverDate (not AddMonths(2))
        Assert.Throws<ArgumentException>(() => new LeaseApprovalTimingAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            LeaseApprovalTimingCalculator.PolicyVersion, "Asia/Colombo",
            new DateOnly(2026, 4, 1),
            completeProposalRoadmapStartDate: new DateOnly(2026, 1, 15),
            expectedApprovalHandoverDate: new DateOnly(2026, 3, 16),
            approvalHandoverDate: new DateOnly(2026, 3, 10),
            daysElapsed: 54,
            daysBeyondExpected: 0,
            LeaseApprovalTimingState.WithinExpectedTwoCalendarMonths,
            "Confirmed",
            LeaseApprovalTimingCalculator.ExpertReportedProvenanceNote,
            new string('d', 64), provenance, FixedUtcNow));

        // Non-Asia/Colombo TimeZoneId
        Assert.Throws<ArgumentException>(() => new LeaseApprovalTimingAssessment(
            Guid.NewGuid(), runId, "SLG-CASE-2026-0001", null,
            LeaseApprovalTimingCalculator.PolicyVersion, "UTC",
            new DateOnly(2026, 4, 1),
            completeProposalRoadmapStartDate: new DateOnly(2026, 1, 15),
            expectedApprovalHandoverDate: new DateOnly(2026, 3, 15),
            approvalHandoverDate: new DateOnly(2026, 3, 10),
            daysElapsed: 54,
            daysBeyondExpected: 0,
            LeaseApprovalTimingState.WithinExpectedTwoCalendarMonths,
            "Confirmed",
            LeaseApprovalTimingCalculator.ExpertReportedProvenanceNote,
            new string('d', 64), provenance, FixedUtcNow));
    }

    // =========================================================================
    // 6. Architecture Boundary, DbContext Model & DI Registration
    // =========================================================================

    [Fact]
    public void ArchitectureBoundary_ApplicationAndInfrastructure_DoNotReferenceComponent1OrComponent3()
    {
        var appAssembly = typeof(GovernanceInputProvenance).Assembly;
        var infraAssembly = typeof(GovernanceIntelligenceDbContext).Assembly;

        var forbiddenPrefixes = new[]
        {
            "StateLandGovernance.LandIntelligence",
            "StateLandGovernance.LeaseFeasibility",
            "StateLandGovernance.WorkflowGovernance"
        };

        foreach (var referenced in appAssembly.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(
                forbiddenPrefixes,
                prefix => referenced.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var referenced in infraAssembly.GetReferencedAssemblies())
        {
            Assert.DoesNotContain(
                forbiddenPrefixes,
                prefix => referenced.Name!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void DiAndDbContextModel_RegistersAllPhase3AStoresAndMapsTablesInGovernanceIntelligenceSchema()
    {
        var connectionString = GetValidatedTestDatabaseConnectionString();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:GovernanceIntelligenceConnection"] = connectionString
            })
            .Build();

        var services = new ServiceCollection();
        services.AddGovernanceIntelligenceInfrastructure(configuration, new FakeHostEnvironment());

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IComponent1GovernanceCaseProjectionStore) &&
            d.ImplementationType == typeof(PostgresComponent1GovernanceCaseProjectionStore) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IComponent3WorkflowGovernanceProjectionStore) &&
            d.ImplementationType == typeof(PostgresComponent3WorkflowGovernanceProjectionStore) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(IWorkflowAnomalyAssessmentStore) &&
            d.ImplementationType == typeof(PostgresWorkflowAnomalyAssessmentStore) &&
            d.Lifetime == ServiceLifetime.Scoped);

        Assert.Contains(services, d =>
            d.ServiceType == typeof(ILeaseApprovalTimingAssessmentStore) &&
            d.ImplementationType == typeof(PostgresLeaseApprovalTimingAssessmentStore) &&
            d.Lifetime == ServiceLifetime.Scoped);

        using var dbContext = CreateInMemoryDbContext();
        var c1Entity = dbContext.Model.FindEntityType(typeof(Component1CaseProjectionEntity));
        var c3Entity = dbContext.Model.FindEntityType(typeof(Component3WorkflowProjectionEntity));
        var anomalyEntity = dbContext.Model.FindEntityType(typeof(WorkflowAnomalyAssessmentEntity));
        var timingEntity = dbContext.Model.FindEntityType(typeof(LeaseApprovalTimingAssessmentEntity));

        Assert.NotNull(c1Entity);
        Assert.Equal(Component1CaseProjectionEntityConfiguration.TableName, c1Entity!.GetTableName());
        Assert.Equal(GovernanceIntelligenceDbContext.SchemaName, c1Entity.GetSchema());

        Assert.NotNull(c3Entity);
        Assert.Equal(Component3WorkflowProjectionEntityConfiguration.TableName, c3Entity!.GetTableName());
        Assert.Equal(GovernanceIntelligenceDbContext.SchemaName, c3Entity.GetSchema());

        Assert.NotNull(anomalyEntity);
        Assert.Equal(WorkflowAnomalyAssessmentEntityConfiguration.TableName, anomalyEntity!.GetTableName());
        Assert.Equal(GovernanceIntelligenceDbContext.SchemaName, anomalyEntity.GetSchema());

        Assert.NotNull(timingEntity);
        Assert.Equal(LeaseApprovalTimingAssessmentEntityConfiguration.TableName, timingEntity!.GetTableName());
        Assert.Equal(GovernanceIntelligenceDbContext.SchemaName, timingEntity.GetSchema());
    }

    private static string GetValidatedTestDatabaseConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionEnvVar);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"PostgreSQL test configuration is required. Set {ConnectionEnvVar} to an isolated database starting with '{AllowedTestDatabasePrefix}'.");
        }

        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.Database) ||
            !builder.Database.StartsWith(AllowedTestDatabasePrefix, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Refusing non-isolated PostgreSQL test target. Database name must start with '{AllowedTestDatabasePrefix}'.");
        }

        return connectionString;
    }

    private static GovernanceIntelligenceDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>()
            .UseInMemoryDatabase($"phase3a-tests-{Guid.NewGuid():N}")
            .Options;
        return new GovernanceIntelligenceDbContext(options);
    }

    private static GovernanceInputProvenance CreateValidPp1Provenance(
        string sourceSystem,
        string sourceRecordId,
        string syntheticSeedId) =>
        GovernanceInputProvenance.CreatePp1Fixture(
            sourceSystem: sourceSystem,
            sourceRecordId: sourceRecordId,
            sourceRecordVersion: "PP1-2026-10-v1",
            sourceObservedAtUtc: FixedUtcNow,
            syntheticSeedId: syntheticSeedId,
            generatedAtUtc: FixedUtcNow);

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "StateLandGovernance.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
