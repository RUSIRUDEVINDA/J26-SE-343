using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using StateLandGovernance.Api.Security;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Presentation.Security;
using Xunit;
public sealed class DevelopmentCatalogueSelectionTests
{
 sealed class Catalogue : IComplianceRuleCatalogue
 {
  public int Calls;public IReadOnlyList<ComplianceRuleDefinition> Rules=NpdRuleCatalogue.GetDefaultRuleDefinitions();
  public Task<IReadOnlyList<ComplianceRuleDefinition>> GetActiveRulesAsync(DateTime at,CancellationToken ct){Calls++;return Task.FromResult(Rules);}
 }
 sealed class Env(string name) : IHostEnvironment
 {
  public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="UnitTest";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
 }
 static (PortalResearchPolicy Policy,Catalogue Catalogue) Build(bool enabled,string environment="Development",bool assigned=true,bool invalidDigest=false,bool productionManifest=false,bool local=true)
 {
  var catalogue=new Catalogue();var rules=catalogue.Rules.OrderBy(r=>r.RuleCode,StringComparer.Ordinal).ThenBy(r=>r.RuleVersion,StringComparer.Ordinal).ToArray();
  var digest=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rules,new JsonSerializerOptions(JsonSerializerDefaults.Web))))).ToLowerInvariant();
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["LocalDevelopmentSignIn:Enabled"]="true",["GovernanceIntelligence:DevelopmentCatalogue:Enabled"]=enabled.ToString(),["GovernanceIntelligence:DevelopmentCatalogue:Version"]="UNIT-TEST-1",["GovernanceIntelligence:DevelopmentCatalogue:RulesSha256"]=invalidDigest?new string('0',64):digest,
   ["GovernanceIntelligence:Screening:ApprovedPolicy:SourceReference"]=productionManifest?"UNIT-TEST-EXISTING-POLICY":null,
   ["GovernanceIntelligence:Screening:ApprovedPolicy:Version"]=productionManifest?"UNIT-TEST-PRODUCTION":null,
   ["GovernanceIntelligence:Screening:ApprovedPolicy:RulesSha256"]=productionManifest?digest:null }).Build();
  var env=new Env(environment);var http=new DefaultHttpContext();http.Connection.RemoteIpAddress=local?System.Net.IPAddress.Loopback:System.Net.IPAddress.Parse("192.0.2.10");http.Request.RouteValues["caseId"]=PortalResearchEvidence.ResearchCase.ToString();
  var claims=new List<Claim>{new("development_account","research")};if(assigned)claims.Add(new("authority_case_id",PortalResearchEvidence.ResearchCase.ToString("D")));
  http.User=new ClaimsPrincipal(new ClaimsIdentity(claims,"unit-test"));
  var accounts=new DevelopmentAccounts(config,env,new LandIntelligenceJwtTokenService(Options.Create(new LandIntelligenceAuthenticationOptions{JwtSigningKeyBase64=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))})));
  return(new PortalResearchPolicy(catalogue,config,new HttpContextAccessor{HttpContext=http},accounts,env),catalogue);
 }
 [Theory][InlineData(false,1)][InlineData(true,24)] public async Task ExplicitModeSelectsCatalogueWithoutChangingDefault(bool enabled,int count)
 {var value=Build(enabled);var policy=await value.Policy.ReadAsync(DateTime.UtcNow,default);Assert.Equal(count,policy!.Rules.Count);Assert.Equal(enabled?1:0,value.Catalogue.Calls);Assert.StartsWith(enabled?"DEV-CATALOGUE:":"DEV-RESEARCH-NPD-1",policy.Version);}
 [Theory][InlineData("Production",true)][InlineData("Development",false)] public async Task CatalogueFlagDoesNotGrantProductionOrUnassignedAccess(string environment,bool assigned)
 {var value=Build(true,environment,assigned);Assert.Null(await value.Policy.ReadAsync(DateTime.UtcNow,default));Assert.Equal(0,value.Catalogue.Calls);}
 [Fact] public async Task InvalidDigestDoesNotSilentlyFallBackToSingleRule()
 {var value=Build(true,invalidDigest:true);await Assert.ThrowsAsync<InvalidOperationException>(()=>value.Policy.ReadAsync(DateTime.UtcNow,default));}
 [Fact] public async Task ChangedCatalogueRuleVersionCannotReuseThePinnedPolicy()
 {
  var value=Build(true);
  value.Catalogue.Rules=value.Catalogue.Rules.Select((rule,index)=>index==0?rule with {RuleVersion="UNIT-TEST-CHANGED"}:rule).ToArray();
  await Assert.ThrowsAsync<InvalidOperationException>(()=>value.Policy.ReadAsync(DateTime.UtcNow,default));
 }
 [Fact] public async Task MissingCataloguePinFailsClosed()
 {
  await Assert.ThrowsAsync<InvalidOperationException>(()=>DevelopmentCataloguePolicy.ReadAsync(new Catalogue(),new ConfigurationBuilder().Build(),DateTime.UtcNow,default));
 }
 [Fact] public async Task ProductionUsesItsExistingConfiguredPolicyEvenWhenDemonstrationFlagIsTrue()
 {var value=Build(true,"Production",productionManifest:true);var policy=await value.Policy.ReadAsync(DateTime.UtcNow,default);Assert.StartsWith("UNIT-TEST-PRODUCTION:",policy!.Version);Assert.Equal("UNIT-TEST-EXISTING-POLICY",policy.SourceReference);}
 [Fact] public async Task RemoteRequestDoesNotSelectDevelopmentCatalogue()
 {var value=Build(true,local:false);Assert.Null(await value.Policy.ReadAsync(DateTime.UtcNow,default));Assert.Equal(0,value.Catalogue.Calls);}
 [Theory][InlineData("case")][InlineData("snapshot")][InlineData("proposal")][InlineData("input-id")]
 public void SyntheticEnvelopeRejectsEveryIdentityMismatch(string field)
 {
  var caseId=Guid.NewGuid();var snapshot=Guid.NewGuid();var proposal=Guid.NewGuid();
  var input=JsonSerializer.SerializeToElement(new {proposalId=field=="input-id"?Guid.NewGuid().ToString("D"):caseId.ToString("D")});
  var envelope=new DevelopmentRegulatoryInputSource.Envelope(field=="case"?Guid.NewGuid():caseId,field=="snapshot"?Guid.NewGuid():snapshot,field=="proposal"?Guid.NewGuid():proposal,"UNIT-TEST-1","UNIT-TEST-SYNTHETIC",input);
  Assert.Throws<InvalidOperationException>(()=>DevelopmentRegulatoryInputSource.Bind(envelope,caseId,snapshot,proposal));
 }
 [Fact] public void BoundSyntheticEnvelopePreservesUnknownsAndChangesFullSourceFingerprint()
 {
  var caseId=Guid.NewGuid();var snapshot=Guid.NewGuid();var proposal=Guid.NewGuid();
  var raw=JsonSerializer.SerializeToElement(new {proposalId=caseId.ToString("D"),financing=new {revenueExpected=(bool?)null}});
  var envelope=new DevelopmentRegulatoryInputSource.Envelope(caseId,snapshot,proposal,"UNIT-TEST-1","UNIT-TEST-SYNTHETIC",raw);
  var first=DevelopmentRegulatoryInputSource.Bind(envelope,caseId,snapshot,proposal);
  using var document=JsonDocument.Parse(first.InputJson);Assert.Equal(JsonValueKind.Null,document.RootElement.GetProperty("financing").GetProperty("revenueExpected").ValueKind);
  var second=first with {InputJson=JsonSerializer.Serialize(new {proposalId=caseId.ToString("D"),financing=new {revenueExpected=false}})};
  Assert.NotEqual(StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.ConsolidatedCaseScreeningAdapter.Fingerprint(first),StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations.ConsolidatedCaseScreeningAdapter.Fingerprint(second));
 }
}
