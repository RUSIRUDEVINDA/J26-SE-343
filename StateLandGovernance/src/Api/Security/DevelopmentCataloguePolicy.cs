using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
namespace StateLandGovernance.Api.Security;
// Called only inside PortalResearchPolicy's existing Development/loopback/designation/assignment guard.
public static class DevelopmentCataloguePolicy
{
 public static async Task<ApprovedScreeningPolicy> ReadAsync(IComplianceRuleCatalogue catalogue,IConfiguration config,DateTime at,CancellationToken ct)
 {
  var version=config["GovernanceIntelligence:DevelopmentCatalogue:Version"];
  var expected=config["GovernanceIntelligence:DevelopmentCatalogue:RulesSha256"];
  if(string.IsNullOrWhiteSpace(version)||string.IsNullOrWhiteSpace(expected))
   throw new InvalidOperationException("Development catalogue revision and owner-pinned artifact digest are required; this is not official approval.");
  var rules=(await catalogue.GetActiveRulesAsync(at,ct)).OrderBy(r=>r.RuleCode,StringComparer.Ordinal).ThenBy(r=>r.RuleVersion,StringComparer.Ordinal).ToArray();
  var json=JsonSerializer.Serialize(rules,new JsonSerializerOptions(JsonSerializerDefaults.Web));
  var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();
  if(rules.Length==0 || !string.Equals(expected,digest,StringComparison.OrdinalIgnoreCase))
   throw new InvalidOperationException("Development catalogue artifact differs from its pinned digest; no single-rule fallback was used.");
  return new("Development PostgreSQL catalogue demonstration only; not officially approved policy",$"DEV-CATALOGUE:{version}:{digest}",rules);
 }
}
