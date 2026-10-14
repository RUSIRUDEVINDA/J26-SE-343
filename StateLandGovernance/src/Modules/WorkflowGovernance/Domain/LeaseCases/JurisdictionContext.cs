namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public readonly record struct JurisdictionContext : IEquatable<JurisdictionContext>
{
    public string Code { get; }
    public string? Region { get; }
    public string? District { get; }

    public JurisdictionContext(string code, string? region = null, string? district = null)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidProposalIntakeException("Jurisdiction code cannot be null, empty, or whitespace.");
        }

        Code = code.Trim();
        Region = string.IsNullOrWhiteSpace(region) ? null : region.Trim();
        District = string.IsNullOrWhiteSpace(district) ? null : district.Trim();
    }

    public bool Equals(JurisdictionContext other) =>
        string.Equals(Code, other.Code, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        StringComparer.OrdinalIgnoreCase.GetHashCode(Code ?? string.Empty);

    public override string ToString() => Code ?? string.Empty;
}
