namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using System.Text.Json;

public sealed record PostWorkflowTraceEvent(int Sequence, string Activity, string Institution, string Actor, DateTime AtUtc);
public sealed record PostWorkflowTrace(Guid CaseId, Guid ExecutionId, int Revision, string InputFingerprint,
    IReadOnlyList<PostWorkflowTraceEvent> Events, bool IsDevelopmentSpatialMock);
public sealed record PostWorkflowAssessmentResult(Guid AssessmentId, Guid CaseId, Guid ExecutionId,
    string InputFingerprint, bool Flagged, JsonElement Assessment, PostWorkflowTrace? SourceTrace = null);

public interface IPostWorkflowAssessmentGateway
{
    bool Available { get; }
    Task<PostWorkflowAssessmentResult> AssessAsync(PostWorkflowTrace trace, CancellationToken ct);
}
