namespace StateLandGovernance.WorkflowGovernance.Application.Exceptions;

using System;

public sealed class ProposalTemplateNotFoundException : Exception
{
    public string TemplateId { get; }
    public string Version { get; }

    public ProposalTemplateNotFoundException(string templateId, string version)
        : base($"ProposalTemplate '{templateId}' version '{version}' was not found.")
    {
        TemplateId = templateId;
        Version = version;
    }
}
