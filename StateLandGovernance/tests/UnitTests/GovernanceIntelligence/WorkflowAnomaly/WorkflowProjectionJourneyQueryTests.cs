using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence.WorkflowAnomaly;

public sealed class WorkflowProjectionJourneyQueryTests
{
    [Fact]
    public async Task ApplyPp1Seed_CommissionerScenario_ProducesFullRouteWithoutModelOutcome()
    {
        var c1Store = new SpyComponent1Store();
        var c3Store = new SpyProjectionStore(null);
        var handler = new ApplyPp1SeedScenarioCommandHandler(c1Store, c3Store);

        var result = await handler.HandleAsync(new ApplyPp1SeedScenarioCommand(
            "PP1-ANOMALY-COMMISSIONER-001",
            Guid.Parse("fbaf9801-690f-51f8-9312-404bd3475216"),
            Guid.Parse("7bdcb5d7-79e0-53ec-933e-df6c898ec3a3"),
            Guid.Parse("b1694cb5-60a5-5817-b11b-55bead079189"),
            new DateTimeOffset(2026, 10, 12, 10, 0, 0, TimeSpan.Zero),
            GovernanceInputProvenance.CommissionerAnomalySyntheticSeedVersion));

        var saved = Assert.IsType<Component3WorkflowGovernanceProjection>(c3Store.Persisted);
        Assert.Equal(GovernanceInputProvenance.CommissionerAnomalySyntheticSeedVersion, result.SyntheticSeedVersion);
        Assert.Equal(16, saved.Events.Count);
        Assert.Equal(Enumerable.Range(1, 16), saved.Events.Select(item => item.EventSeq));
        Assert.Contains(saved.Events, item => item.InstitutionCode == "DS" && item.Activity == "DS Scrutiny");
        Assert.Contains(saved.Events, item => item.InstitutionCode == "PLC_DLC");
        Assert.Contains(saved.Events, item => item.InstitutionCode == "LCGD");
        Assert.Contains(saved.Events, item => item.Activity == "Environmental Clearance");
        Assert.Contains(saved.Events, item => item.Activity == "Ministry/Competent Authority Approval" && item.EventOutcome == "Completed");
        Assert.Contains(saved.Events, item => item.Activity == "Land Registry Registration" && item.EventOutcome == "NotApplicable");
        Assert.Equal(Component3WorkflowGovernanceProjection.AnomalyModelV1TerminalActivity, saved.Events[^1].Activity);
        Assert.True(saved.Events.Zip(saved.Events.Skip(1)).All(pair => pair.First.Timestamp <= pair.Second.Timestamp));
    }

    [Fact]
    public async Task HandleAsync_MapsExactStoredOrderAndUnavailableRouteMetadata_WithoutMutation()
    {
        var projection = CreateProjection();
        var store = new SpyProjectionStore(projection);
        var handler = new GetWorkflowProjectionJourneyByCaseQueryHandler(store);

        var result = await handler.HandleAsync(
            new GetWorkflowProjectionJourneyByCaseQuery(projection.CaseId));

        Assert.NotNull(result);
        Assert.Equal(new[] { 1, 2, 3 }, result.Events.Select(item => item.EventSequence));
        Assert.Equal(new[] { "Complete Proposal Received", "Land Registry Registration", "DS Final Notification" }, result.Events.Select(item => item.Activity));
        Assert.Equal("NotApplicable", result.Events[1].Status);
        Assert.Equal("Unavailable", result.LegalRouteLabel.Availability);
        Assert.Null(result.LegalRouteLabel.Value);
        Assert.Equal("Unavailable", result.RouteApplicability.RequiredClearances.Availability);
        Assert.Equal("Unavailable", result.RouteApplicability.HigherApprovalApplicable.Availability);
        Assert.Equal("Unavailable", result.RouteApplicability.RegistrationApplicable.Availability);
        Assert.Equal("Eligible", result.AssessmentEligibility);
        Assert.Equal(0, store.UpsertCalls);
        Assert.Equal(1, store.ReadCalls);
    }

    [Fact]
    public async Task HandleAsync_MissingProjection_ReturnsNullWithoutMutation()
    {
        var store = new SpyProjectionStore(null);
        var handler = new GetWorkflowProjectionJourneyByCaseQueryHandler(store);

        var result = await handler.HandleAsync(new GetWorkflowProjectionJourneyByCaseQuery("MISSING-CASE"));

        Assert.Null(result);
        Assert.Equal(0, store.UpsertCalls);
        Assert.Equal(1, store.ReadCalls);
    }

    private static Component3WorkflowGovernanceProjection CreateProjection()
    {
        const string caseId = "PP1-ANOMALY-COMMISSIONER-001";
        var workflowRunId = Guid.Parse("b1694cb5-60a5-5817-b11b-55bead079189");
        var start = new DateTimeOffset(2026, 8, 3, 10, 0, 0, TimeSpan.Zero);
        var provenance = GovernanceInputProvenance.CreatePp1Fixture(
            "SyntheticPp1Seed.Component3",
            $"{caseId}:C3",
            $"{GovernanceInputProvenance.CommissionerAnomalySyntheticSeedVersion}:component3",
            start,
            caseId,
            start,
            GovernanceInputProvenance.CommissionerAnomalySyntheticSeedVersion);
        var events = new[]
        {
            Event(1, "Complete Proposal Received", "DS", "DS-Intake", start, "Completed"),
            Event(2, "Land Registry Registration", "LAND_REGISTRY", "Route-Control", start.AddDays(1), "NotApplicable"),
            Event(3, Component3WorkflowGovernanceProjection.AnomalyModelV1TerminalActivity, "DS", "DS-Officer", start.AddDays(2), "Completed")
        };
        var complete = new TimingBoundaryEvidence(start, TimingBoundaryEvidenceStatus.Confirmed, "SyntheticPp1Seed", "complete", caseId, workflowRunId);
        var handover = new TimingBoundaryEvidence(start.AddDays(2), TimingBoundaryEvidenceStatus.Confirmed, "SyntheticPp1Seed", "handover", caseId, workflowRunId);
        var hash = Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(
            workflowRunId, caseId, "LandLeaseApproval", Component3WorkflowTraceStatus.Completed,
            Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring, "DS_FINAL_NOTIFICATION",
            Component3EventTimestampSemantics.ExactUtcTimestamp, Component3WorkflowGovernanceProjection.RequiredColomboTimeZone,
            events, complete, handover, provenance);
        return new Component3WorkflowGovernanceProjection(
            Guid.Parse("7bdcb5d7-79e0-53ec-933e-df6c898ec3a3"), workflowRunId, caseId, "LandLeaseApproval",
            Component3WorkflowTraceStatus.Completed, Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,
            "DS_FINAL_NOTIFICATION", Component3EventTimestampSemantics.ExactUtcTimestamp,
            Component3WorkflowGovernanceProjection.RequiredColomboTimeZone, events, complete, handover, hash, provenance, start);
    }

    private static Component3WorkflowProjectionEvent Event(
        int sequence, string activity, string institution, string resource, DateTimeOffset timestamp, string outcome) =>
        new($"EVENT-{sequence}", sequence, activity, $"STAGE-{sequence}", "Officer", institution, timestamp, outcome,
            $"evidence-{sequence}", resource, timestamp.ToString("yyyy-MM-dd'T'HH:mm:ss"));

    private sealed class SpyProjectionStore : IComponent3WorkflowGovernanceProjectionStore
    {
        private Component3WorkflowGovernanceProjection? _projection;

        public SpyProjectionStore(Component3WorkflowGovernanceProjection? projection) => _projection = projection;

        public int UpsertCalls { get; private set; }
        public int ReadCalls { get; private set; }
        public Component3WorkflowGovernanceProjection? Persisted { get; private set; }

        public Task<Component3WorkflowGovernanceProjection> UpsertAsync(Component3WorkflowGovernanceProjection value, CancellationToken cancellationToken = default)
        {
            UpsertCalls++;
            Persisted = value;
            _projection = value;
            return Task.FromResult(value);
        }

        public Task<Component3WorkflowGovernanceProjection?> GetByIdAsync(Guid projectionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_projection);

        public Task<Component3WorkflowGovernanceProjection?> GetByWorkflowRunIdAndVersionAsync(Guid workflowRunId, string sourceRecordVersion, CancellationToken cancellationToken = default) =>
            Task.FromResult(_projection);

        public Task<IReadOnlyList<Component3WorkflowGovernanceProjection>> GetByCaseIdAsync(string caseId, int limit = 100, CancellationToken cancellationToken = default)
        {
            ReadCalls++;
            IReadOnlyList<Component3WorkflowGovernanceProjection> result = _projection is null ? [] : [_projection];
            return Task.FromResult(result);
        }
    }

    private sealed class SpyComponent1Store : IComponent1GovernanceCaseProjectionStore
    {
        private Component1GovernanceCaseProjection? _projection;

        public Task<Component1GovernanceCaseProjection> UpsertAsync(Component1GovernanceCaseProjection projection, CancellationToken cancellationToken = default)
        {
            _projection = projection;
            return Task.FromResult(projection);
        }

        public Task<Component1GovernanceCaseProjection?> GetByIdAsync(Guid projectionId, CancellationToken cancellationToken = default) => Task.FromResult(_projection);
        public Task<Component1GovernanceCaseProjection?> GetByCaseIdAndVersionAsync(string caseId, string sourceRecordVersion, CancellationToken cancellationToken = default) => Task.FromResult(_projection);
        public Task<IReadOnlyList<Component1GovernanceCaseProjection>> GetByCaseIdAsync(string caseId, int limit = 100, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Component1GovernanceCaseProjection> result = _projection is null ? [] : [_projection];
            return Task.FromResult(result);
        }
    }
}
