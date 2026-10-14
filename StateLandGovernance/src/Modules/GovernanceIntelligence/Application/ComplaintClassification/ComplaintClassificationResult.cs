namespace StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

public enum ComplaintClassificationErrorCode
{
    InvalidInput,
    UpstreamValidationRejected,
    ServiceUnavailable,
    Timeout,
    UpstreamFailure,
    InvalidResponse
}

public sealed record ComplaintClassificationError(
    ComplaintClassificationErrorCode Code,
    string Message);

public sealed record ComplaintClassificationPrediction(
    string ModelVersion,
    string PredictedCategory,
    IReadOnlyDictionary<string, double> ClassProbabilities,
    string? CaseId,
    string AdvisoryNote,
    string ClosedSetNote);

/// <summary>
/// Application-level outcome. Expected integration failures never fabricate a prediction.
/// Caller-requested cancellation is propagated as cancellation rather than represented here.
/// </summary>
public sealed record ComplaintClassificationResult
{
    private ComplaintClassificationResult(
        ComplaintClassificationPrediction? prediction,
        ComplaintClassificationError? error)
    {
        Prediction = prediction;
        Error = error;
    }

    public bool IsSuccess => Prediction is not null;
    public ComplaintClassificationPrediction? Prediction { get; }
    public ComplaintClassificationError? Error { get; }

    public static ComplaintClassificationResult Success(ComplaintClassificationPrediction prediction) =>
        new(prediction ?? throw new ArgumentNullException(nameof(prediction)), null);

    public static ComplaintClassificationResult Failure(
        ComplaintClassificationErrorCode code,
        string message) =>
        new(null, new ComplaintClassificationError(code, message));
}
