using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Infrastructure.Integrations;
using StateLandGovernance.WorkflowGovernance.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Screening.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
namespace StateLandGovernance.Api.Security;

/// <summary>Explicit separate Development fixture; never supplies synthetic governance to a real application.</summary>
public sealed class PortalResearchEvidence(DevelopmentAccounts accounts, IHostEnvironment env, UploadedCaseResearchScenarios? uploaded = null) : IScreeningEvidenceProvider
{
 public static readonly Guid ResearchCase=Guid.Parse("de4c0000-0000-4000-8000-000000000001");
 public static readonly Guid FullCoverageParcel=Guid.Parse("de4c0000-0000-4000-8000-000000000012");
 public static readonly Guid ManualProposalCase=Guid.Parse("de4c0000-0000-4000-8000-000000000021");
 public static readonly Guid ManualOverlapCoverage=Guid.Parse("de4c0000-0000-4000-8000-000000000032");
 public static readonly Guid ManualNoConflictCoverage=Guid.Parse("de4c0000-0000-4000-8000-000000000034");
 public async Task<ScreeningEvidenceContext> ReadAsync(Guid caseId,Guid? parcelId,CancellationToken ct)
 {
  var manualCoverage=(caseId==ManualProposalCase || (uploaded!=null && await uploaded.CanUseAsync(caseId,ct))) && (parcelId==ManualOverlapCoverage || parcelId==ManualNoConflictCoverage);
  if(!env.IsDevelopment() || !accounts.Enabled || (!(caseId==ResearchCase && parcelId==FullCoverageParcel) && !manualCoverage))
   return await new UnavailableScreeningEvidenceProvider().ReadAsync(caseId,parcelId,ct);
  const string label="SYNTHETIC DEVELOPMENT RESEARCH COVERAGE ONLY; explicit test flags and empty-coverage fixture, never actual clearance";
  return new ScreeningEvidenceContext(manualCoverage ? "DEV-12A-EXPLICIT-SYNTHETIC-COVERAGE-1" : "DEV-RESEARCH-GOVERNANCE-COMPLAINT-COVERAGE-1",
   JsonSerializer.Serialize(new GovernanceEvidence(label,false,false,false,false,false,[],[]),CaseScreeningService.JsonOptions),
   JsonSerializer.Serialize(new SpatialReviewEvidence(label,parcelId!=ManualNoConflictCoverage,[],[]),CaseScreeningService.JsonOptions),[],caseId,parcelId);
 }
}

public sealed class PortalResearchPolicy(IComplianceRuleCatalogue catalogue,IConfiguration config,
 IHttpContextAccessor http,DevelopmentAccounts accounts,IHostEnvironment env) : IApprovedScreeningPolicySource
{
 public async Task<ApprovedScreeningPolicy?> ReadAsync(DateTime at,CancellationToken ct)
 {
  var context=http.HttpContext;
  if(env.IsDevelopment() && accounts.Enabled && context!=null && accounts.Local(context) &&
   Guid.TryParse(context.Request.RouteValues["caseId"]?.ToString(),out var caseId) &&
   ((caseId==PortalResearchEvidence.ResearchCase && context.User.HasClaim("development_account","research")) ||
    (caseId==PortalResearchEvidence.ManualProposalCase && context.User.HasClaim("development_account","reviewtest")) ||
    (context.Items["UploadedSyntheticScreeningCase"] is Guid uploadedCase && uploadedCase==caseId)) &&
   context.User.HasClaim("authority_case_id",caseId.ToString("D")))
   return new ApprovedScreeningPolicy("DEVELOPMENT RESEARCH POLICY ONLY; existing labelled NPD research-rule scenario, not statutory approval","DEV-RESEARCH-NPD-1",
    [NpdRuleCatalogue.GetDefaultRuleDefinitions().First() with { SourceReference=new(RuleSourceType.ResearchConfiguration,
      "DEVELOPMENT RESEARCH ONLY","Prototype scenario rule","Synthetic",null,"1") }]);
  return await new ApprovedScreeningPolicySource(catalogue,config).ReadAsync(at,ct);
 }
}
