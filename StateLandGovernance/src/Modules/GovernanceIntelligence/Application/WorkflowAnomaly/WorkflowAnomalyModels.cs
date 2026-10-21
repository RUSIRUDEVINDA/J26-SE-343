namespace StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;

/// <summary>
/// Operational limits for workflow anomaly evaluation payloads.
/// </summary>
public static class WorkflowAnomalyConstraints
{
    public const int MaximumEvents = 500;
    public const int MaximumCaseIdLength = 100;
}

/// <summary>
/// An individual event in an ordered complete workflow trace.
/// Timestamps are naive ISO-8601 strings (e.g. "2031-02-03T08:00:00") with unspecified timezone semantics.
/// </summary>
public sealed record WorkflowAnomalyEvent(
    int EventSeq,
    string Activity,
    string Institution,
    string Resource,
    string Timestamp);

/// <summary>
/// Exact nine-feature representations extracted from a completed workflow trace.
/// </summary>
public sealed record WorkflowFeatureValues(
    int EventCount,
    double ElapsedDays,
    double MaxGapHours,
    double MeanGapHours,
    int UniqueActivities,
    int RepeatedActivityCount,
    int ResourceHandoffs,
    int InstitutionSwitches,
    int DistinctResources);

/// <summary>
/// Advisory outcome of a workflow anomaly detection evaluation.
/// The score is not a probability or confidence value.
/// </summary>
public sealed record WorkflowAnomalyPrediction(
    string CaseId,
    string ModelVersion,
    double AnomalyScore,
    double Threshold,
    bool Flagged,
    WorkflowFeatureValues FeatureValues,
    string AdvisoryNote);

public enum WorkflowAnomalyErrorCode
{
    InvalidInput,
    UpstreamValidationRejected,
    ServiceUnavailable,
    Timeout,
    UpstreamFailure,
    InvalidResponse
}

public sealed record WorkflowAnomalyError(
    WorkflowAnomalyErrorCode Code,
    string Message);

/// <summary>
/// Application-level outcome for workflow anomaly detection.
/// Caller-requested cancellation is propagated as cancellation rather than captured as an error result.
/// </summary>
public sealed record WorkflowAnomalyResult
{
    private WorkflowAnomalyResult(
        WorkflowAnomalyPrediction? prediction,
        WorkflowAnomalyError? error)
    {
        Prediction = prediction;
        Error = error;
    }

    public bool IsSuccess => Prediction is not null;
    public WorkflowAnomalyPrediction? Prediction { get; }
    public WorkflowAnomalyError? Error { get; }

    public static WorkflowAnomalyResult Success(WorkflowAnomalyPrediction prediction) =>
        new(prediction ?? throw new ArgumentNullException(nameof(prediction)), null);

    public static WorkflowAnomalyResult Failure(
        WorkflowAnomalyErrorCode code,
        string message) =>
        new(null, new WorkflowAnomalyError(code, message ?? throw new ArgumentNullException(nameof(message))));

    public static WorkflowAnomalyResult Failure(WorkflowAnomalyError error) =>
        new(null, error ?? throw new ArgumentNullException(nameof(error)));
}
