using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
public static class C4FollowupCompletionProof {
 public static async Task RunAsync(HttpClient owner,HttpClient other,Guid early,IndependentRegulatoryReport original,Action<bool,string> check){
  var id=original.Metadata.Id;var findings=original.Findings!;
  var route="/api/governance-intelligence/regulatory-assessments/"+id+"/follow-ups";
  var sequence="/api/governance-intelligence/officer-sequence?earlyRequestId="+early+"&regulatoryEvaluationId="+id;
  var missing=findings.Where(f=>f.Status=="InsufficientInformation").ToArray();var reviews=findings.Where(f=>f.Status=="RequiresHumanReview").ToArray();
  check(findings.Count==24&&missing.Length==7&&reviews.Length==3,"Step19 actual saved 14/7/3 engine distribution");
  check((await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!.Holds.Count==10,"Step19 no saved follow-ups leaves ten awaiting");
  foreach(var f in missing){var input=new RecordRegulatoryFollowup(Guid.NewGuid(),f.RuleCode,"ClarificationRequested",null,"The outstanding information is requested for downstream handling.","The officer documents the clarification required for this finding.",["Isolated supporting reference"]);check((await owner.PostAsJsonAsync(route,input)).IsSuccessStatusCode,"Step19 clarification saved "+f.RuleCode);}
  check((await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!.Holds.Count==3,"Step19 seven clarification requests leave exactly three awaiting");
  for(var i=0;i<3;i++){
   var input=new RecordRegulatoryFollowup(Guid.NewGuid(),reviews[i].RuleCode,"HumanReviewRecorded",RegulatoryFollowupInput.Outcomes[i],null,"The review outcome documents concerns for downstream handling.",[]);
   check((await other.PostAsJsonAsync(route,input)).StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,"Step19 foreign DS denied review");
   check((await owner.PostAsJsonAsync(route,input)).IsSuccessStatusCode,"Step19 saved review outcome "+input.ReviewOutcome);
   var ready=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!;check(ready.Holds.Count==2-i&&ready.CanProceedToConflict==(i==2),"Step19 authoritative completeness after review "+i);
  }
  var history=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!.History;
  check(history.Count==10&&history.Count(e=>e.Operation=="ClarificationRequested"&&e.MissingInformation!.Contains("outstanding"))==7&&history.Any(e=>e.ReviewOutcome=="FurtherInstitutionalLegalReview"),"Step19 all outstanding concerns remain saved alongside completion");
  var before=JsonSerializer.Serialize(history,RegulatoryJson.Options);
  for(var i=0;i<2;i++)check((await owner.GetFromJsonAsync<OfficerSequenceReadiness>(sequence))!.CanProceedToConflict,"Step19 repeated authenticated read retains 24 addressed");
  check(before==JsonSerializer.Serialize((await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route))!.History,RegulatoryJson.Options),"Step19 readiness does not mutate follow-up audit");
  var saved=(await owner.GetFromJsonAsync<IndependentRegulatoryReport>("/api/governance-intelligence/regulatory-assessments/"+id))!;
  check(saved.Metadata.Status==original.Metadata.Status&&JsonSerializer.Serialize(saved.Findings,RegulatoryJson.Options)==JsonSerializer.Serialize(findings,RegulatoryJson.Options),"Step19 original aggregate and every engine finding unchanged");
  check(RegulatoryProgression.Holds(findings,history,Guid.NewGuid()).Length==10,"Step19 foreign evaluation history cannot address current findings");
 }
}
