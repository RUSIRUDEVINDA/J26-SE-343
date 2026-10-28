using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Demonstrations;
using StateLandGovernance.GovernanceIntelligence.Application.Projections;
using StateLandGovernance.GovernanceIntelligence.Application.Timing;
using StateLandGovernance.GovernanceIntelligence.Application.WorkflowAnomaly;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
namespace StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
public sealed record OfficerFinding(string Concern,string Choice,string Remarks);
public sealed record SaveInvestigation(Guid SubmissionKey,string ProposalId,IReadOnlyList<OfficerFinding> Findings,Guid? SupersedesId=null);
public sealed record Investigation(Guid Id,Guid CreatorActorId,string ProposalId,Guid SubmissionKey,string SourceVersion,string InputSha256,IReadOnlyList<OfficerFinding> Findings,DateTimeOffset CreatedAtUtc,Guid? SupersedesId);
public sealed record EarlyRequest(Guid InvestigationId,Guid SubmissionKey);
public sealed record AnomalyRequest(string Scenario,Guid SubmissionKey);
public sealed record OfficerAssessment(Guid Id,Guid CreatorActorId,string Kind,string ScopeKey,string SourceVersion,string InputSha256,string State,DateTimeOffset CreatedAtUtc,DateTimeOffset? FinishedAtUtc,Guid? InvestigationId,string? Scenario,Guid? WorkflowRunId,Guid? ProjectionId,Guid? EarlyAssessmentId,Guid? AnomalyAssessmentId,string? FailureCode,int Revision,object? Result,object? Projection,IReadOnlyList<object> Audit);
public interface IOfficerFlowStore {
 Task<bool> ReadyAsync(CancellationToken ct);
 Task<Investigation> SaveAsync(Guid actor,SaveInvestigation input,CancellationToken ct);
 Task<Investigation> InvestigationAsync(Guid actor,Guid id,CancellationToken ct);
 Task<IReadOnlyList<Investigation>> InvestigationsAsync(Guid actor,CancellationToken ct);
 Task<OfficerAssessment> EarlyAsync(Guid actor,EarlyRequest input,CancellationToken ct);
 Task<OfficerAssessment> AnomalyAsync(Guid actor,AnomalyRequest input,CancellationToken ct);
 Task<OfficerAssessment> GetAsync(Guid actor,Guid id,CancellationToken ct);
 Task<IReadOnlyList<OfficerAssessment>> HistoryAsync(Guid actor,CancellationToken ct);
}
public sealed class OfficerConflictException:Exception;
public sealed class OfficerUnavailableException:Exception;
public static class OfficerFlowInput {
 public const string Version="officer-investigation/1";
 public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
 public static SaveInvestigation Validate(SaveInvestigation input){
  if(input.SubmissionKey==Guid.Empty||input.ProposalId!=KiwulaCatalogue.ProposalId||input.Findings==null)throw new ArgumentException("Invalid investigation identity.");
  var names=Enum.GetNames<EarlyGovernanceIndicatorType>();
  if(input.Findings.Any(x=>x is null)||input.Findings.Count!=names.Length||input.Findings.Select(x=>x.Concern).Distinct().Count()!=names.Length||input.Findings.Any(x=>!names.Contains(x.Concern)))throw new ArgumentException("Exactly the seven supported concerns are required.");
  var rows=input.Findings.Select(x=>x with{Remarks=(x.Remarks??"").Trim()}).OrderBy(x=>Array.IndexOf(names,x.Concern)).ToArray();
  if(rows.Any(x=>x.Choice is not ("IssueFound" or "NoIssueFound" or "NotYetVerified")||x.Remarks.Length>2000||(x.Choice=="IssueFound"&&(x.Remarks.Length<20||x.Remarks.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length<3))))throw new ArgumentException("Invalid observation or insufficient issue description.");
  return input with{Findings=rows};
 }
 public static EarlyGovernanceIndicatorDto[] Indicators(Investigation saved)=>saved.Findings.Select(f=>new EarlyGovernanceIndicatorDto(f.Concern,f.Choice=="NotYetVerified"&&string.IsNullOrWhiteSpace(f.Remarks)?"Missing":"Unverified","officer-investigation:"+saved.Id,saved.CreatedAtUtc)).ToArray();
}
public static class NamedOfficerWorkflow {
 public const string Scenario="kiwula-kale-named-route-v2",Version="kiwula-named-route/2";
 public static Component3WorkflowGovernanceProjection Create(Guid run,Guid projection,DateTimeOffset created){
  string[] institutions=["Mahakumbukkadawala Divisional Secretariat","Department of Agriculture","North Western Provincial Environmental Authority","North Western Provincial Land Commissioner’s Department","Land Commissioner General’s Department"];
  string[] activities=["Application Received","Document Review","Recommendation","DS Review","Regional Review","Commissioner Decision","DS Final Notification"];
  string[] outcomes=["Received","Technical review requested","Conditional recommendation","Recommended subject to evidence","Conditional recommendation","Conditional approval","Decision notified"];
  int[] indices=[0,1,2,0,3,4,0],days=[0,3,6,9,12,15,17];
  var start=new DateTimeOffset(2026,2,2,9,0,0,TimeSpan.FromMinutes(330));
  var events=activities.Select((a,i)=>new Component3WorkflowProjectionEvent("prepared-"+(i+1),i+1,a,"PP1-"+(i+1),"Prepared institutional role",institutions[indices[i]],start.AddDays(days[i]),outcomes[i],"prepared://"+Version+"/"+(i+1),"prepared-route-role-"+(indices[i]+1),start.AddDays(days[i]).ToString("yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture))).ToArray();
  var caseId=PostWorkflowDemonstrations.CasePrefix+run.ToString("D");
  var provenance=GovernanceInputProvenance.CreatePp1Fixture("C4 approved prepared institutional route; provincial branch and department applicability are assumptions",Scenario,Version,created,Scenario,created);
  var missing=new TimingBoundaryEvidence(null,TimingBoundaryEvidenceStatus.Unavailable,"Prepared PP1 history; no official decision or legal handover",Scenario,caseId,run);
  var hash=Component3WorkflowGovernanceProjection.ComputeCanonicalSha256(run,caseId,"Prepared Kiwula PP1",Component3WorkflowTraceStatus.Completed,Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,"DS-FINAL-NOTIFICATION",Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,"Asia/Colombo",events,missing,missing,provenance);
  return new(projection,run,caseId,"Prepared Kiwula PP1",Component3WorkflowTraceStatus.Completed,Component3WorkflowTraceCompleteness.CompletedTraceEligibleForAnomalyScoring,"DS-FINAL-NOTIFICATION",Component3EventTimestampSemantics.LocalBusinessDayWithColomboOffset,"Asia/Colombo",events,missing,missing,hash,provenance,created);
 }
}
