using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Exceptions;

public sealed class FeasibilityAssessmentNotFoundException : LeaseFeasibilityDomainException
{
    public string ApplicationId { get; }

    public FeasibilityAssessmentNotFoundException(string applicationId)
        : base($"Feasibility assessment for application '{applicationId}' was not found.")
    {
        ApplicationId = applicationId;
    }
}
