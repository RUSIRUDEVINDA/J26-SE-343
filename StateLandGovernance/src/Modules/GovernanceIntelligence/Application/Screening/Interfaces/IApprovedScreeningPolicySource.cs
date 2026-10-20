namespace StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;

public sealed record ApprovedScreeningPolicy(string SourceReference, string Version,
    IReadOnlyList<ComplianceRuleDefinition> Rules);

/// <summary>Explicit owner-provisioned active policy metadata; catalogue presence alone is not approval.</summary>
public interface IApprovedScreeningPolicySource
{
    Task<ApprovedScreeningPolicy?> ReadAsync(DateTime at, CancellationToken ct);
}
