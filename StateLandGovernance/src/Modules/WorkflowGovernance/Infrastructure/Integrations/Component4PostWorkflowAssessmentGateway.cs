namespace StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public sealed class Component4PostWorkflowAssessmentGateway(AssessWorkflowAnomalyCommandHandler? handler, IHostEnvironment? environment = null) : IPostWorkflowAssessmentGateway
{
    public bool Available => handler != null;
    public async Task<PostWorkflowAssessmentResult> AssessAsync(PostWorkflowTrace trace, CancellationToken ct)
    {
        if (handler == null) throw new InvalidOperationException("C4 workflow anomaly orchestration is unavailable.");
        if (trace.Events.Any(e => e.AtUtc.Kind != DateTimeKind.Utc)) throw new InvalidOperationException("Trace timestamps must explicitly use UTC semantics.");
        // These are actual stored UTC instants. The model's naive timestamp field is explicitly UTC here;
        // no local-date inference or synthetic terminal event is performed.
        var events = trace.Events.Select(e => new WorkflowAnomalyEvent(e.Sequence, e.Activity, e.Institution, e.Actor,
            e.AtUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture))).ToArray();
        var provenance = new GovernanceInputProvenance("C3 persisted execution; timestamps=UTC; developmentSpatialMock=" + trace.IsDevelopmentSpatialMock,
            trace.ExecutionId.ToString("D"), "execution-revision:" + trace.Revision,
            new(trace.Events.Max(e => e.AtUtc)), false, GovernanceProvenanceKind.RealComponent3, null, null,
            DateTimeOffset.UtcNow, environment?.IsProduction() == true
                ? GovernanceProvenancePurpose.ProductionPeerIntegration : GovernanceProvenancePurpose.LocalDevelopment);
        var result = await handler.HandleAsync(AssessWorkflowAnomalyCommand.FromTrace(trace.CaseId.ToString("D"), trace.ExecutionId, events, provenance), ct);
        var a = result.Assessment ?? throw new InvalidOperationException(result.Error?.Message ?? "C4 anomaly assessment unavailable.");
        var expectedHash = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(trace.ExecutionId, trace.CaseId.ToString("D"), events, provenance);
        if (a.CaseId != trace.CaseId.ToString("D") || a.WorkflowRunId != trace.ExecutionId || a.InputSha256 != expectedHash)
            throw new InvalidOperationException("C4 post-workflow result does not match the exact submitted trace.");
        return new(a.AssessmentId, trace.CaseId, trace.ExecutionId, trace.InputFingerprint, a.Flagged,
            JsonSerializer.SerializeToElement(a, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }
}
