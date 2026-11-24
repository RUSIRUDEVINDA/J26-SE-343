using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class OfficerSequenceStore(GovernanceIntelligenceDbContext db,IComplaintActorContext actor,IComplaintOfficerDirectory directory,IInvestigationEvidenceStore evidence,IndependentRegulatoryReportQueries reports,IRegulatoryFollowupStore followups):IOfficerSequenceStore,IOfficerSequencePrerequisites {
 async Task Eligible(Guid expected,CancellationToken ct){if(actor.ActorId==Guid.Empty||actor.ActorId!=expected||!(await directory.ReadAsync(ct)).Officers.Any(x=>x.ActorId==expected))throw new UnauthorizedAccessException();}
 async Task<OfficerAssessmentRequestEntity> Owned(Guid id,CancellationToken ct){await Eligible(actor.ActorId,ct);return await db.Set<OfficerAssessmentRequestEntity>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.CreatorActorId==actor.ActorId&&x.Kind=="early",ct)??throw new UnauthorizedAccessException();}
 public async Task RequireEarlyAsync(Guid expected,Guid request,string proposal,CancellationToken ct){
  await Eligible(expected,ct);var r=await Owned(request,ct);
  var investigation=await db.Set<OfficerInvestigationEntity>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==r.InvestigationId&&x.CreatorActorId==expected,ct);
  if(r.State!="Saved"||r.EarlyAssessmentId==null||investigation==null||r.ScopeKey!=proposal||investigation.ProposalId!=proposal||r.InputSha256!=investigation.InputSha256||r.SourceVersion!=investigation.SourceVersion)throw new ArgumentException("A saved Early screening of this exact proposal and finalized investigation is required.");
  if(!await db.EarlyGovernanceScreeningEvaluations.AsNoTracking().AnyAsync(x=>x.AssessmentId==r.EarlyAssessmentId&&x.WorkflowRunId==r.Id&&x.InputVersion==r.SourceVersion,ct))throw new ArgumentException("The Early request has no matching saved engine result/provenance.");
  await evidence.RequireFinalizedAsync(expected,investigation.Id,ct);await Eligible(expected,ct);
 }
 public async Task<OfficerSequenceReadiness> ReadAsync(Guid? early,Guid? regulatory,CancellationToken ct){
  await Eligible(actor.ActorId,ct);if(early==null)return new(null,null,false,false,["Complete and explicitly select a finalized, saved Early Governance screening first."]);
  var r=await Owned(early.Value,ct);
  try{await RequireEarlyAsync(actor.ActorId,r.Id,r.ScopeKey,ct);}catch(ArgumentException e){return new(early,null,false,false,[e.Message]);}
  if(regulatory==null)return new(early,null,true,false,["Run or explicitly select a regulatory assessment bound to this Early request."]);
  var report=await reports.GetAsync(actor.ActorId,regulatory.Value,ct)??throw new UnauthorizedAccessException();
  if(report.Provenance?.CreatorActorId!=actor.ActorId||report.Provenance.EarlyRequestId!=early||report.Metadata.ProposalId!=r.ScopeKey)throw new ArgumentException("Select a saved regulatory evaluation bound to this exact owned Early request; historical unbound reports remain read-only history.");
  var events=await followups.ReadAsync(regulatory.Value,ct);var holds=RegulatoryProgression.Holds(report.Findings,events.History,regulatory.Value);
  await RequireEarlyAsync(actor.ActorId,r.Id,r.ScopeKey,ct);await reports.GetAsync(actor.ActorId,regulatory.Value,ct);
  return new(early,regulatory,true,holds.Length==0,holds);
 }
}
