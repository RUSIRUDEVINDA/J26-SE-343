namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

/// <summary>
/// Exception thrown when a lease case with the specified application reference already exists.
/// Corresponds to an HTTP 409 Conflict condition at the presentation boundary.
/// </summary>
public sealed class DuplicateApplicationReferenceException : Exception
{
    public string ApplicationReference { get; }

    public DuplicateApplicationReferenceException(string applicationReference)
        : base($"A lease case with application reference '{applicationReference}' already exists.")
    {
        ApplicationReference = applicationReference;
    }
}
