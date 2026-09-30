namespace StateLandGovernance.WorkflowGovernance.Domain.RequirementAssessment;

public sealed record AssessmentInputSnapshot
{
    public string? Purpose { get; }
    public decimal? ExtentValue { get; }
    public string? ExtentUnit { get; }
    public string? JurisdictionCode { get; }
    public string? IntakeSourceReference { get; }

    public AssessmentInputSnapshot(
        string? purpose,
        decimal? extentValue,
        string? extentUnit,
        string? jurisdictionCode,
        string? intakeSourceReference)
    {
        Purpose = purpose;
        ExtentValue = extentValue;
        ExtentUnit = extentUnit;
        JurisdictionCode = jurisdictionCode;
        IntakeSourceReference = intakeSourceReference;
    }
}
