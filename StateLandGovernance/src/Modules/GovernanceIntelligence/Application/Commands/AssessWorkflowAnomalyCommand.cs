using System.Collections.ObjectModel;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

/// <summary>
/// Requests an advisory workflow-anomaly assessment for a completed workflow trace
/// or a persisted <see cref="Component3WorkflowGovernanceProjection"/>.
/// <para>
/// Governance Boundary: Does not invoke lease-approval timing calculations, does not combine
/// anomaly flags with timing expectations, and never triggers an automatic Commissioner referral.
/// </para>
/// </summary>
public sealed record AssessWorkflowAnomalyCommand(
    string CaseId,
    Guid WorkflowRunId,
    IReadOnlyList<WorkflowAnomalyEvent>? Events,
    GovernanceInputProvenance? Provenance,
    Guid? WorkflowProjectionId = null,
    string? ModelArtifactSha256 = WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1)
{
    public static AssessWorkflowAnomalyCommand FromTrace(
        string caseId,
        Guid workflowRunId,
        IReadOnlyList<WorkflowAnomalyEvent> events,
        GovernanceInputProvenance provenance,
        Guid? workflowProjectionId = null,
        string? modelArtifactSha256 = WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1) =>
        new(
            CaseId: caseId,
            WorkflowRunId: workflowRunId,
            Events: events,
            Provenance: provenance,
            WorkflowProjectionId: workflowProjectionId,
            ModelArtifactSha256: modelArtifactSha256);

    public static AssessWorkflowAnomalyCommand FromProjection(
        Guid workflowProjectionId,
        string caseId,
        Guid workflowRunId,
        string? modelArtifactSha256 = WorkflowAnomalyAssessment.FrozenModelArtifactSha256V1) =>
        new(
            CaseId: caseId,
            WorkflowRunId: workflowRunId,
            Events: null,
            Provenance: null,
            WorkflowProjectionId: workflowProjectionId,
            ModelArtifactSha256: modelArtifactSha256);
}

/// <summary>
/// Orchestrates completed-trace validation, canonical input hashing, <see cref="IWorkflowAnomalyClient"/>
/// inference, and idempotent persistence of <see cref="WorkflowAnomalyAssessment"/> records.
/// </summary>
public sealed class AssessWorkflowAnomalyCommandHandler
{
    private readonly IWorkflowAnomalyClient _client;
    private readonly IWorkflowAnomalyAssessmentStore _assessmentStore;
    private readonly IComponent3WorkflowGovernanceProjectionStore? _projectionStore;
    private readonly TimeProvider _timeProvider;

    public AssessWorkflowAnomalyCommandHandler(
        IWorkflowAnomalyClient client,
        IWorkflowAnomalyAssessmentStore assessmentStore,
        TimeProvider timeProvider,
        IComponent3WorkflowGovernanceProjectionStore? projectionStore = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _assessmentStore = assessmentStore ?? throw new ArgumentNullException(nameof(assessmentStore));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _projectionStore = projectionStore;
    }

    public async Task<WorkflowAnomalyAssessmentResult> HandleAsync(
        AssessWorkflowAnomalyCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);

        // 1. Receive or load the exact completed trace
        var resolved = await ResolveTraceAndProvenanceAsync(command, cancellationToken).ConfigureAwait(false);
        if (resolved.Error is not null)
        {
            return WorkflowAnomalyAssessmentResult.Failure(resolved.Error);
        }

        var caseId = resolved.CaseId!;
        var workflowRunId = resolved.WorkflowRunId;
        var exactEvents = resolved.Events!;
        var provenance = resolved.Provenance!;
        var workflowProjectionId = resolved.WorkflowProjectionId;

        // 2. Validate its identity and anomaly-model eligibility
        var validationError = ValidateAnomalyEligibleTrace(caseId, workflowRunId, exactEvents);
        if (validationError is not null)
        {
            return WorkflowAnomalyAssessmentResult.Failure(validationError);
        }

        // 3. Compute the canonical input SHA-256 over the exact trace snapshot sent to IWorkflowAnomalyClient
        var inputSha256 = WorkflowAnomalyAssessment.ComputeCanonicalInputSha256(
            workflowRunId,
            caseId,
            exactEvents,
            provenance);

        // 4. Call the existing IWorkflowAnomalyClient with the exact same caseId and event snapshot
        var clientResult = await _client.PredictAnomalyAsync(
            caseId,
            exactEvents,
            cancellationToken).ConfigureAwait(false);

        // 5. Receive and verify its validated response
        if (!clientResult.IsSuccess || clientResult.Prediction is null)
        {
            return WorkflowAnomalyAssessmentResult.Failure(
                clientResult.Error ?? new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidResponse,
                    "The workflow anomaly client returned neither a prediction nor an error."));
        }

        var prediction = clientResult.Prediction;
        var postValidationError = ValidatePredictionConsistency(prediction, caseId, exactEvents.Count);
        if (postValidationError is not null)
        {
            return WorkflowAnomalyAssessmentResult.Failure(postValidationError);
        }

        // 6. Create an immutable WorkflowAnomalyAssessment
        var features = prediction.FeatureValues;
        WorkflowAnomalyAssessment assessment;
        try
        {
            assessment = new WorkflowAnomalyAssessment(
                assessmentId: Guid.NewGuid(),
                workflowRunId: workflowRunId,
                caseId: caseId,
                workflowProjectionId: workflowProjectionId,
                modelVersion: prediction.ModelVersion,
                modelArtifactSha256: command.ModelArtifactSha256,
                anomalyScore: prediction.AnomalyScore,
                threshold: prediction.Threshold,
                flagged: prediction.Flagged,
                eventCount: features.EventCount,
                elapsedDays: features.ElapsedDays,
                maxGapHours: features.MaxGapHours,
                meanGapHours: features.MeanGapHours,
                uniqueActivities: features.UniqueActivities,
                repeatedActivityCount: features.RepeatedActivityCount,
                resourceHandoffs: features.ResourceHandoffs,
                institutionSwitches: features.InstitutionSwitches,
                distinctResources: features.DistinctResources,
                advisoryNote: prediction.AdvisoryNote,
                inputSha256: inputSha256,
                provenance: provenance,
                assessedAtUtc: _timeProvider.GetUtcNow());
        }
        catch (ArgumentException ex)
        {
            return WorkflowAnomalyAssessmentResult.Failure(
                WorkflowAnomalyErrorCode.InvalidResponse,
                $"Workflow anomaly prediction could not construct a valid assessment record: {ex.Message}");
        }

        // 7. Persist it idempotently by (WorkflowRunId, ModelVersion, InputSha256)
        var persisted = await _assessmentStore.UpsertAsync(assessment, cancellationToken).ConfigureAwait(false);

        // 8. Return the persisted assessment
        return WorkflowAnomalyAssessmentResult.Success(persisted);
    }

    private async Task<ResolvedAnomalyInput> ResolveTraceAndProvenanceAsync(
        AssessWorkflowAnomalyCommand command,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.CaseId) ||
            !string.Equals(command.CaseId, command.CaseId.Trim(), StringComparison.Ordinal))
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "CaseId is required and must not contain leading or trailing whitespace.");
        }

        if (command.CaseId.Length > WorkflowAnomalyConstraints.MaximumCaseIdLength)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"CaseId must not exceed {WorkflowAnomalyConstraints.MaximumCaseIdLength} characters.");
        }

        if (command.WorkflowRunId == Guid.Empty)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "WorkflowRunId cannot be empty.");
        }

        if (command.WorkflowProjectionId.HasValue && command.WorkflowProjectionId.Value == Guid.Empty)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "WorkflowProjectionId cannot be Guid.Empty when provided.");
        }

        // Direct trace mode
        if (command.Events is not null)
        {
            if (command.Provenance is null)
            {
                return ResolvedAnomalyInput.Fail(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    "Provenance is required when scoring a workflow trace.");
            }

            if (command.Events.Any(e => e is null))
            {
                return ResolvedAnomalyInput.Fail(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    "Events collection cannot contain null entries.");
            }

            var snapshot = new ReadOnlyCollection<WorkflowAnomalyEvent>(command.Events.ToList());
            return ResolvedAnomalyInput.Ok(
                command.CaseId,
                command.WorkflowRunId,
                snapshot,
                command.Provenance,
                command.WorkflowProjectionId);
        }

        // Stored projection mode
        if (!command.WorkflowProjectionId.HasValue)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "Either Events or a valid WorkflowProjectionId must be supplied.");
        }

        if (_projectionStore is null)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "WorkflowProjectionId was supplied, but IComponent3WorkflowGovernanceProjectionStore is not configured.");
        }

        Component3WorkflowGovernanceProjection? projection;
        try
        {
            projection = await _projectionStore
                .GetByIdAsync(command.WorkflowProjectionId.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Stored Component 3 workflow projection '{command.WorkflowProjectionId.Value}' is malformed: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Stored Component 3 workflow projection '{command.WorkflowProjectionId.Value}' is invalid: {ex.Message}");
        }

        if (projection is null)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Component 3 workflow projection '{command.WorkflowProjectionId.Value}' was not found.");
        }

        if (!string.Equals(projection.CaseId, command.CaseId, StringComparison.Ordinal) ||
            projection.WorkflowRunId != command.WorkflowRunId)
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                "Supplied CaseId and WorkflowRunId do not match the referenced Component 3 workflow projection.");
        }

        if (!projection.TryExtractAnomalyScoringEvents(out var extractedEvents, out var extractionError))
        {
            return ResolvedAnomalyInput.Fail(
                WorkflowAnomalyErrorCode.InvalidInput,
                extractionError ?? "Referenced Component 3 workflow projection is not eligible for anomaly scoring.");
        }

        return ResolvedAnomalyInput.Ok(
            projection.CaseId,
            projection.WorkflowRunId,
            extractedEvents,
            projection.Provenance,
            projection.ProjectionId);
    }

    private static WorkflowAnomalyError? ValidateAnomalyEligibleTrace(
        string caseId,
        Guid workflowRunId,
        IReadOnlyList<WorkflowAnomalyEvent> events)
    {
        if (string.IsNullOrWhiteSpace(caseId) || workflowRunId == Guid.Empty)
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidInput,
                "CaseId and WorkflowRunId are required.");
        }

        if (events.Count == 0)
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidInput,
                "Events collection must not be empty.");
        }

        if (events.Count > WorkflowAnomalyConstraints.MaximumEvents)
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Events count {events.Count} exceeds operational limit of {WorkflowAnomalyConstraints.MaximumEvents}.");
        }

        DateTime? previousNaive = null;
        for (int i = 0; i < events.Count; i++)
        {
            var ev = events[i];
            var expectedSeq = i + 1;
            if (ev.EventSeq != expectedSeq)
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"EventSeq must be strictly contiguous 1..N. Expected {expectedSeq} at index {i}, but found {ev.EventSeq}.");
            }

            if (string.IsNullOrWhiteSpace(ev.Activity) ||
                !string.Equals(ev.Activity, ev.Activity.Trim(), StringComparison.Ordinal))
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Activity at index {i} must be non-blank without surrounding whitespace.");
            }

            if (string.IsNullOrWhiteSpace(ev.Institution) ||
                !string.Equals(ev.Institution, ev.Institution.Trim(), StringComparison.Ordinal))
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Institution at index {i} must be non-blank without surrounding whitespace.");
            }

            if (string.IsNullOrWhiteSpace(ev.Resource) ||
                !string.Equals(ev.Resource, ev.Resource.Trim(), StringComparison.Ordinal))
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Resource at index {i} must be non-blank without surrounding whitespace.");
            }

            var timestampError = Component3WorkflowProjectionEvent.ValidateNaiveIso8601Timestamp(
                ev.Timestamp,
                out var parsedNaive);
            if (timestampError is not null)
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Timestamp at index {i} is invalid: {timestampError}");
            }

            if (previousNaive.HasValue && parsedNaive < previousNaive.Value)
            {
                return new WorkflowAnomalyError(
                    WorkflowAnomalyErrorCode.InvalidInput,
                    $"Event timestamps must be monotonic non-decreasing. EventSeq {ev.EventSeq} ('{ev.Timestamp}') precedes previous event.");
            }

            previousNaive = parsedNaive;
        }

        // Model-specific completion rule for workflow_anomaly_model_v1
        var terminalActivity = events[^1].Activity;
        if (!string.Equals(
                terminalActivity,
                WorkflowAnomalyAssessment.AnomalyModelV1TerminalActivity,
                StringComparison.Ordinal))
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidInput,
                $"Trace is ineligible for workflow_anomaly_model_v1 scoring: final event activity must be '{WorkflowAnomalyAssessment.AnomalyModelV1TerminalActivity}', but was '{terminalActivity}'. " +
                "(This requirement defines anomaly-model input eligibility only and does not represent legal lease completion or timing-policy satisfaction.)");
        }

        return null;
    }

    private static WorkflowAnomalyError? ValidatePredictionConsistency(
        WorkflowAnomalyPrediction prediction,
        string expectedCaseId,
        int expectedEventCount)
    {
        if (!string.Equals(prediction.CaseId, expectedCaseId, StringComparison.Ordinal))
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidResponse,
                "The workflow anomaly service response did not preserve the supplied CaseId.");
        }

        if (prediction.FeatureValues is null || prediction.FeatureValues.EventCount != expectedEventCount)
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidResponse,
                "The workflow anomaly service response event_count did not match the submitted trace length.");
        }

        bool expectedFlagged = prediction.AnomalyScore > prediction.Threshold;
        if (prediction.Flagged != expectedFlagged)
        {
            return new WorkflowAnomalyError(
                WorkflowAnomalyErrorCode.InvalidResponse,
                "The workflow anomaly service response flagged state violates strict anomaly_score > threshold semantics.");
        }

        return null;
    }

    private sealed record ResolvedAnomalyInput(
        string? CaseId,
        Guid WorkflowRunId,
        IReadOnlyList<WorkflowAnomalyEvent>? Events,
        GovernanceInputProvenance? Provenance,
        Guid? WorkflowProjectionId,
        WorkflowAnomalyError? Error)
    {
        public static ResolvedAnomalyInput Ok(
            string caseId,
            Guid workflowRunId,
            IReadOnlyList<WorkflowAnomalyEvent> events,
            GovernanceInputProvenance provenance,
            Guid? workflowProjectionId) =>
            new(caseId, workflowRunId, events, provenance, workflowProjectionId, null);

        public static ResolvedAnomalyInput Fail(WorkflowAnomalyErrorCode code, string message) =>
            new(null, Guid.Empty, null, null, null, new WorkflowAnomalyError(code, message));
    }
}
