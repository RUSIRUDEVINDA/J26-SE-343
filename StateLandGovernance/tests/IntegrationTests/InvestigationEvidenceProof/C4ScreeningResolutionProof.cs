using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
public static class C4ScreeningResolutionProof {
 public static async Task RunAsync(HttpClient owner,HttpClient other,Guid early,IndependentRegulatoryReport original,Action<bool,string> check){
  var evaluation=original.Metadata.Id;var findings=original.Findings!;var actionable=findings.Where(f=>f.Status is "InsufficientInformation" or "RequiresHumanReview").ToArray();
  check(findings.Count==24&&findings.Count(f=>f.Status=="Compliant")==14&&actionable.Length==10,"Actual original engine saves 14 Compliant and 10 actionable findings");
  string route="/api/governance-intelligence/regulatory-assessments/"+evaluation+"/follow-ups";
  string sequence="/api/governance-intelligence/officer-sequence?earlyRequestId="+early+"&regulatoryEvaluationId="+evaluation;
  check(!(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!.CanProceedToConflict,"Ten unresolved C4 findings initially block only C4 progression");
  for(var i=0;i<9;i++){var response=await owner.PostAsJsonAsync(route,new RecordRegulatoryFollowup(Guid.NewGuid(),actionable[i].RuleCode,"FindingVerifiedClosed",null,null));check(response.IsSuccessStatusCode,"C4 closure "+i+" saved without evidence/remarks");}
  var nine=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!;check(!nine.CanProceedToConflict&&nine.Holds.Count==1&&nine.Holds.Single().StartsWith(actionable[9].RuleCode+":"),"14 Compliant plus 9 closed leaves exactly one C4 finding unresolved");
  var input=new RecordRegulatoryFollowup(Guid.NewGuid(),actionable[9].RuleCode,"FindingVerifiedClosed",null,null);
  var denied=await other.PostAsJsonAsync(route,input);check(denied.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,"Other DS cannot close the final owned finding");
  check((await owner.PostAsJsonAsync(route,input)).IsSuccessStatusCode,"Final unresolved finding closes through protected existing API");
  var clear=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!;check(clear.CanProceedToConflict&&clear.Holds.Count==0,"14 Compliant plus 10 saved closures enables Continue within C4 only");
  var refreshed=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!;check(refreshed.CanProceedToConflict,"Fresh read retains enabled C4 progression");
  var saved=(await owner.GetFromJsonAsync<IndependentRegulatoryReport>("/api/governance-intelligence/regulatory-assessments/"+evaluation))!;
  check(JsonSerializer.Serialize(saved.Findings,RegulatoryJson.Options)==JsonSerializer.Serialize(original.Findings,RegulatoryJson.Options)&&saved.Metadata.Status==original.Metadata.Status,"Original 24 engine outcomes and original overall result remain unchanged");
  var events=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!.History;check(events.Count==10&&events.All(e=>e.Operation=="FindingVerifiedClosed"&&e.EvaluationId==evaluation&&e.ActorId!=Guid.Empty&&e.EventSha256.Length==64),"Only ten auditable internal C4 dispositions exist; no external approval event created");
  var unmatched=Guid.NewGuid();check(RegulatoryProgression.Holds(findings,events,unmatched).Length==10,"Previous evaluation events cannot clear another current evaluation");
 }
}
