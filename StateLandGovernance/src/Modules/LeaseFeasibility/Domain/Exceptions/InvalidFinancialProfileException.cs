using System;

namespace StateLandGovernance.LeaseFeasibility.Domain.Exceptions;

public sealed class InvalidFinancialProfileException : LeaseFeasibilityDomainException
{
    public string ApplicationId { get; }

    public InvalidFinancialProfileException(string applicationId, string reason)
        : base($"The financial profile for application '{applicationId}' is invalid: {reason}")
    {
        ApplicationId = applicationId;
    }
}
