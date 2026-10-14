namespace StateLandGovernance.LandIntelligence.Presentation.Models;

public sealed record ApiErrorResponse(
    int Status,
    string Title,
    string Detail,
    IReadOnlyList<string>? Errors = null);
