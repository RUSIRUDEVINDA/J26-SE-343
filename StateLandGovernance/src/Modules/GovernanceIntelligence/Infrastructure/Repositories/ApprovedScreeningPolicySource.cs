namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;

/// <summary>Owner-provisioned manifest pins the approved catalogue contents. No baseline fallback.</summary>
public sealed class ApprovedScreeningPolicySource(IComplianceRuleCatalogue catalogue, IConfiguration configuration) : IApprovedScreeningPolicySource
{
    public async Task<ApprovedScreeningPolicy?> ReadAsync(DateTime at, CancellationToken ct)
    {
        var section = configuration.GetSection("GovernanceIntelligence:Screening:ApprovedPolicy");
        var reference = section["SourceReference"]; var version = section["Version"]; var expected = section["RulesSha256"];
        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(version) || string.IsNullOrWhiteSpace(expected)) return null;
        var rules = await catalogue.GetActiveRulesAsync(at, ct);
        if (rules.Count == 0) return null;
        var json = JsonSerializer.Serialize(rules.OrderBy(r => r.RuleCode, StringComparer.Ordinal).ThenBy(r => r.RuleVersion, StringComparer.Ordinal),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
        if (!string.Equals(digest, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Active C4 catalogue differs from its owner-approved policy manifest.");
        return new(reference, version + ":" + digest, rules);
    }
}
