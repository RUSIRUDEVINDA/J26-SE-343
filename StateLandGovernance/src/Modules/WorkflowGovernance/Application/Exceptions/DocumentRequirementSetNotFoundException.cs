namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class DocumentRequirementSetNotFoundException : Exception
{
    public string Identifier { get; }
    public string Version { get; }

    public DocumentRequirementSetNotFoundException(string identifier, string version)
        : base($"Document requirement set '{identifier}' version '{version}' was not found in the governed requirement catalogue.")
    {
        Identifier = identifier;
        Version = version;
    }
}
