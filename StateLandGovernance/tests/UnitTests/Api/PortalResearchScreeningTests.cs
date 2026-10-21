using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using StateLandGovernance.Api.Security;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Presentation.Security;
using Xunit;
public sealed class PortalResearchScreeningTests
{
 private static readonly IConfiguration Config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{["LocalDevelopmentSignIn:Enabled"]="true"}).Build();
 private static DevelopmentAccounts Accounts(IHostEnvironment env)=>new(Config,env,new LandIntelligenceJwtTokenService(Options.Create(new LandIntelligenceAuthenticationOptions{JwtSigningKeyBase64=Convert.ToBase64String(new byte[32])})));
 [Fact] public async Task SyntheticGovernanceOnlyExistsForSeparateResearchCaseAndExplicitCoverageParcel(){var env=new Env("Development");var p=new PortalResearchEvidence(Accounts(env),env);
 var real=await p.ReadAsync(Guid.Parse("23d18b9c-2b80-4ebc-8875-5951f02a1964"),PortalResearchEvidence.FullCoverageParcel,default);Assert.Null(real.GovernanceJson);Assert.NotEmpty(real.MissingInputs);
 var unselected=await p.ReadAsync(PortalResearchEvidence.ResearchCase,null,default);Assert.Null(unselected.GovernanceJson);
 var spatialOnly=await p.ReadAsync(PortalResearchEvidence.ResearchCase,Guid.Parse("de4c0000-0000-4000-8000-000000000011"),default);Assert.Null(spatialOnly.GovernanceJson);
 var selected=await p.ReadAsync(PortalResearchEvidence.ResearchCase,PortalResearchEvidence.FullCoverageParcel,default);Assert.Contains("SYNTHETIC",selected.GovernanceJson);Assert.Empty(selected.MissingInputs);
 var prod=new Env("Production");Assert.Null((await new PortalResearchEvidence(Accounts(prod),prod).ReadAsync(PortalResearchEvidence.ResearchCase,PortalResearchEvidence.FullCoverageParcel,default)).GovernanceJson);}
 [Fact] public async Task ResearchPolicyIsNotInstalledForRealCaseProductionOrAnotherAccount(){var env=new Env("Development");var http=new HttpContextAccessor{HttpContext=new DefaultHttpContext()};http.HttpContext.Connection.RemoteIpAddress=System.Net.IPAddress.Loopback;
 http.HttpContext.Request.RouteValues["caseId"]=PortalResearchEvidence.ResearchCase.ToString("D");http.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("authority_case_id",PortalResearchEvidence.ResearchCase.ToString("D")),new Claim("development_account","research")},"TEST-ONLY"));
 var p=new PortalResearchPolicy(null!,Config,http,Accounts(env),env);var selected=await p.ReadAsync(DateTime.UtcNow,default);Assert.NotNull(selected);Assert.Contains("RESEARCH",selected.SourceReference);Assert.Single(selected.Rules);
 http.HttpContext.Request.RouteValues["caseId"]=Guid.NewGuid().ToString();Assert.Null(await p.ReadAsync(DateTime.UtcNow,default));
 http.HttpContext.Request.RouteValues["caseId"]=PortalResearchEvidence.ResearchCase.ToString("D");http.HttpContext.User=new ClaimsPrincipal(new ClaimsIdentity());Assert.Null(await p.ReadAsync(DateTime.UtcNow,default));
 var prod=new Env("Production");Assert.Null(await new PortalResearchPolicy(null!,Config,http,Accounts(prod),prod).ReadAsync(DateTime.UtcNow,default));}
 [Fact] public async Task ManualProposalCoverageRequiresExplicitReservedSelectionAndNeverServesRealCases(){
 var env=new Env("Development");var provider=new PortalResearchEvidence(Accounts(env),env);
 var unset=await provider.ReadAsync(PortalResearchEvidence.ManualProposalCase,Guid.Parse("de4c0000-0000-4000-8000-000000000033"),default);Assert.Null(unset.GovernanceJson);
 var clear=await provider.ReadAsync(PortalResearchEvidence.ManualProposalCase,PortalResearchEvidence.ManualNoConflictCoverage,default);Assert.Contains("SYNTHETIC",clear.GovernanceJson);Assert.Contains("\"hasOverlappingParcels\":false",clear.SpatialReviewJson);Assert.Empty(clear.MissingInputs);
 var blocked=await provider.ReadAsync(PortalResearchEvidence.ManualProposalCase,PortalResearchEvidence.ManualOverlapCoverage,default);Assert.Contains("\"hasOverlappingParcels\":true",blocked.SpatialReviewJson);
 var real=await provider.ReadAsync(Guid.Parse("23d18b9c-2b80-4ebc-8875-5951f02a1964"),PortalResearchEvidence.ManualNoConflictCoverage,default);Assert.Null(real.GovernanceJson);
 var prod=new Env("Production");Assert.Null((await new PortalResearchEvidence(Accounts(prod),prod).ReadAsync(PortalResearchEvidence.ManualProposalCase,PortalResearchEvidence.ManualNoConflictCoverage,default)).GovernanceJson);
 }
 [Fact] public async Task ManualProposalPolicyRequiresTheExactDevelopmentAccountAndCase(){
 var env=new Env("Development");var context=new DefaultHttpContext();context.Connection.RemoteIpAddress=System.Net.IPAddress.Loopback;context.Request.RouteValues["caseId"]=PortalResearchEvidence.ManualProposalCase.ToString("D");
 context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("authority_case_id",PortalResearchEvidence.ManualProposalCase.ToString("D")),new Claim("development_account","reviewtest")},"TEST-ONLY"));
 var policy=new PortalResearchPolicy(null!,Config,new HttpContextAccessor{HttpContext=context},Accounts(env),env);Assert.Single((await policy.ReadAsync(DateTime.UtcNow,default))!.Rules);
 context.User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim("authority_case_id",PortalResearchEvidence.ManualProposalCase.ToString("D")),new Claim("development_account","ds")},"TEST-ONLY"));Assert.Null(await policy.ReadAsync(DateTime.UtcNow,default));
 }
 private sealed class Env(string name):IHostEnvironment{public string EnvironmentName{get;set;}=name;public string ApplicationName{get;set;}="TEST-ONLY";public string ContentRootPath{get;set;}=".";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();}
}
