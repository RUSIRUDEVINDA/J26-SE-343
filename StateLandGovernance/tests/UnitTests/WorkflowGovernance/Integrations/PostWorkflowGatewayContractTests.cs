namespace StateLandGovernance.UnitTests.WorkflowGovernance.Integrations;

using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using Xunit;

// Fixture inference only; exercises the actual C4 command's validation/hash/persistence orchestration.
public sealed class PostWorkflowGatewayContractTests
{
    private sealed class ProductionEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "TEST ONLY";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task PostWorkflowAdapter_ProductionTrace_IsNotMislabelledLocalDevelopment()
    {
        var store = new Store();
        var adapter = new Component4PostWorkflowAssessmentGateway(new(new Client(), store, TimeProvider.System), new ProductionEnvironment());
        var at = DateTime.UtcNow.AddDays(-2);
        await adapter.AssessAsync(new(Guid.NewGuid(), Guid.NewGuid(), 1, new string('a',64),
            [new(1, "COMMISSIONER:Approved", "COMMISSIONER", "TEST ONLY", at),
             new(2, "DS Final Notification", "DS", "TEST ONLY", at.AddDays(1))], false), default);
        Assert.Equal(GovernanceProvenancePurpose.ProductionPeerIntegration, store.Value!.Provenance.Purpose);
    }

    private sealed class Client : IWorkflowAnomalyClient
    {
        public IReadOnlyList<WorkflowAnomalyEvent>? Received;
        public Task<WorkflowAnomalyResult> PredictAnomalyAsync(string id, IReadOnlyList<WorkflowAnomalyEvent> events, CancellationToken ct = default)
        {
            Received = events;
            return Task.FromResult(WorkflowAnomalyResult.Success(new(id, WorkflowAnomalyAssessment.FrozenModelVersionV1,
                0.9, WorkflowAnomalyAssessment.FrozenThresholdV1, true, new(events.Count, 1, 24, 24, 2, 0, 1, 1, 2), "EXPLICIT TEST INFERENCE ONLY")));
        }
    }
    private sealed class Store : IWorkflowAnomalyAssessmentStore
    {
        public WorkflowAnomalyAssessment? Value;
        public Task<WorkflowAnomalyAssessment> UpsertAsync(WorkflowAnomalyAssessment a, CancellationToken ct = default) { Value = a; return Task.FromResult(a); }
        public Task<WorkflowAnomalyAssessment?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Value?.AssessmentId == id ? Value : null);
        public Task<WorkflowAnomalyAssessment?> GetByWorkflowRunModelAndHashAsync(Guid id, string version, string hash, CancellationToken ct = default) =>
            Task.FromResult(Value?.WorkflowRunId == id && Value.ModelVersion == version && Value.InputSha256 == hash ? Value : null);
        public Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByWorkflowRunIdAsync(Guid id, int limit = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowAnomalyAssessment>>(Value == null ? [] : [Value]);
        public Task<IReadOnlyList<WorkflowAnomalyAssessment>> GetByCaseIdAsync(string id, int limit = 100, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<WorkflowAnomalyAssessment>>(Value == null ? [] : [Value]);
    }
    [Fact]
    public async Task PostWorkflowAdapter_ActualC4Handler_PreservesExactCaseTraceHashAndExplicitUtcSemantics()
    {
        var client = new Client(); var store = new Store();
        var adapter = new Component4PostWorkflowAssessmentGateway(new(client, store, TimeProvider.System));
        var at = new DateTime(2026, 10, 10, 8, 0, 0, DateTimeKind.Utc);
        var trace = new PostWorkflowTrace(Guid.NewGuid(), Guid.NewGuid(), 9, new string('a',64),
            [new(1, "COMMISSIONER:Approved", "COMMISSIONER", "TEST_ACTOR_1", at), new(2, "DS Final Notification", "DS", "TEST_ACTOR_2", at.AddDays(1))], true);
        var result = await adapter.AssessAsync(trace, default);
        Assert.Equal(trace.CaseId, result.CaseId); Assert.Equal(trace.ExecutionId, result.ExecutionId);
        Assert.Equal(trace.InputFingerprint, result.InputFingerprint); Assert.True(result.Flagged);
        Assert.Equal("2026-10-10T08:00:00.0000000", client.Received!.First().Timestamp);
        Assert.Contains("timestamps=UTC", store.Value!.Provenance.SourceSystem);
        Assert.Contains("developmentSpatialMock=True", store.Value.Provenance.SourceSystem);
        Assert.Equal(64, store.Value.InputSha256.Length);
    }
    [Fact]
    public async Task PostWorkflowAdapter_WithoutActualTerminalNotification_DoesNotCallInference()
    {
        var client = new Client(); var adapter = new Component4PostWorkflowAssessmentGateway(new(client, new Store(), TimeProvider.System));
        var trace = new PostWorkflowTrace(Guid.NewGuid(), Guid.NewGuid(), 1, new string('b',64),
            [new(1, "COMMISSIONER:Approved", "COMMISSIONER", "TEST_ACTOR", DateTime.UtcNow)], true);
        await Assert.ThrowsAsync<InvalidOperationException>(() => adapter.AssessAsync(trace, default));
        Assert.Null(client.Received);
    }
}
