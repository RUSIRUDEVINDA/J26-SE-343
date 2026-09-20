namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using System.Linq;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct IntakeSourceReference : IEquatable<IntakeSourceReference>
{
    public string Reference { get; }
    public string Version { get; }

    public IntakeSourceReference(string reference, string version)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            throw new InvalidProposalIntakeException("Intake source reference cannot be null, empty, or whitespace.");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            throw new InvalidProposalIntakeException("Intake source version cannot be null, empty, or whitespace.");
        }

        Reference = reference.Trim();
        Version = version.Trim();

        if (Reference.Any(char.IsControl) || Version.Any(char.IsControl))
        {
            throw new InvalidProposalIntakeException("Intake source reference cannot contain control characters.");
        }
    }

    public override string ToString() => $"{Reference} (v{Version})";
}
