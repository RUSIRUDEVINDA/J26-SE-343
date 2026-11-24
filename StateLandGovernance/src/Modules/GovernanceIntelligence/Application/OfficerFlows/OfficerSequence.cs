using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;

public interface IOfficerSequencePrerequisites {
 Task RequireEarlyAsync(Guid actor,Guid request,string proposal,CancellationToken ct);
}
public sealed record OfficerSequenceReadiness(Guid? EarlyRequestId,Guid? RegulatoryEvaluationId,bool CanRunRegulatory,bool CanProceedToConflict,IReadOnlyList<string> Holds);
public interface IOfficerSequenceStore {
 Task<OfficerSequenceReadiness> ReadAsync(Guid? early,Guid? regulatory,CancellationToken ct);
}
public static class RegulatoryProgression {
 public static string[] Holds(IReadOnlyList<ComplianceFindingDto>? findings,IReadOnlyList<RegulatoryFollowupEvent> history,Guid? evaluationId=null){
  if(findings==null||findings.Count==0)return ["Saved regulatory findings are unavailable."];
  if(findings.Select(x=>x.RuleCode).Distinct().Count()!=findings.Count)return ["Saved regulatory rule identities are ambiguous."];
  var holds=new List<string>();
  foreach(var f in findings){
   // This gate advances only within C4. Original outcomes and later approvals remain untouched.
   if(f.Status=="Compliant"||(f.Status=="NotApplicable"&&f.Applicability=="NotApplicable"&&!f.IsBlocking&&!f.RequiresHumanReview))continue;
   var latest=history.Where(x=>(evaluationId==null||x.EvaluationId==evaluationId)&&x.Status=="Recorded"&&x.RuleCode==f.RuleCode&&x.RuleVersion==f.RuleVersion).Where(x=>x.Operation==FindingClosurePolicy.Operation||IsAddressingFollowup(f,x)).OrderByDescending(x=>x.Revision).FirstOrDefault();
   if(latest?.Operation==FindingClosurePolicy.Operation&&FindingClosurePolicy.CanResolveC4Finding(f)){
    try{FindingClosurePolicy.ValidatePayload(new(Guid.Empty,f.RuleCode,latest.Operation,latest.ReviewOutcome,latest.MissingInformation,latest.Remarks,latest.References,latest.Revision-1));continue;}catch(ArgumentException){/* Malformed events cannot resolve a finding. */}
   }
   if(latest!=null&&IsAddressingFollowup(f,latest))continue;
   holds.Add(f.RuleCode+": awaiting officer follow-up; record a valid permitted clarification, human review or Verify and Close. Outstanding concerns remain for downstream handling.");
  }
  return holds.ToArray();
 }
 public static bool IsAddressingFollowup(ComplianceFindingDto finding,RegulatoryFollowupEvent saved){
  if(saved.Operation is not ("ClarificationRequested" or "HumanReviewRecorded")||saved.Status!="Recorded"||saved.Revision<1)return false;
  try{
   var input=RegulatoryFollowupInput.Validate(new(saved.Id,saved.RuleCode,saved.Operation,saved.ReviewOutcome,saved.MissingInformation,saved.Remarks,saved.References,saved.Revision-1));
   RegulatoryFollowupInput.ValidateFinding(input,finding);
   return true;
  }catch(ArgumentException){return false;}
 }
}
