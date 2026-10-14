namespace StateLandGovernance.LandIntelligence.Domain.ValueObjects;

public sealed record ParcelIdentifier
{
    public string CadastralNumber { get; }
    public string? SurveyPlanReference { get; }

    public ParcelIdentifier(string cadastralNumber, string? surveyPlanReference = null)
    {
        if (string.IsNullOrWhiteSpace(cadastralNumber))
        {
            throw new ArgumentException("Cadastral number is required.", nameof(cadastralNumber));
        }

        CadastralNumber = cadastralNumber.Trim();
        SurveyPlanReference = string.IsNullOrWhiteSpace(surveyPlanReference)
            ? null
            : surveyPlanReference.Trim();
    }

    public override string ToString() => SurveyPlanReference is null
        ? CadastralNumber
        : $"{CadastralNumber} ({SurveyPlanReference})";
}
