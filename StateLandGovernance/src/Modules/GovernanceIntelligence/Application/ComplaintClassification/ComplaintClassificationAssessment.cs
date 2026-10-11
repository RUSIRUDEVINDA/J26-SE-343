namespace StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;

/// <summary>
/// Immutable application snapshot of a completed advisory complaint classification.
/// </summary>
public sealed record ComplaintClassificationAssessment(
    Guid AssessmentId,
    string CaseId,
    string ComplaintText,
    string ModelVersion,
    string PredictedCategory,
    IReadOnlyDictionary<string, double> ClassProbabilities,
    string AdvisoryNote,
    string ClosedSetNote,
    DateTimeOffset AssessedAtUtc);

/// <summary>
/// Outcome of the classify-and-persist application flow.
/// Persistence failures are propagated and therefore never produce a successful result.
/// </summary>
public sealed record ComplaintClassificationAssessmentResult
{
    private ComplaintClassificationAssessmentResult(
        ComplaintClassificationAssessment? assessment,
        ComplaintClassificationError? error)
    {
        Assessment = assessment;
        Error = error;
    }

    public bool IsSuccess => Assessment is not null;
    public ComplaintClassificationAssessment? Assessment { get; }
    public ComplaintClassificationError? Error { get; }

    public static ComplaintClassificationAssessmentResult Success(
        ComplaintClassificationAssessment assessment) =>
        new(assessment ?? throw new ArgumentNullException(nameof(assessment)), null);

    public static ComplaintClassificationAssessmentResult Failure(
        ComplaintClassificationError error) =>
        new(null, error ?? throw new ArgumentNullException(nameof(error)));

    public static ComplaintClassificationAssessmentResult Failure(
        ComplaintClassificationErrorCode code,
        string message) =>
        new(null, new ComplaintClassificationError(code, message));
}
