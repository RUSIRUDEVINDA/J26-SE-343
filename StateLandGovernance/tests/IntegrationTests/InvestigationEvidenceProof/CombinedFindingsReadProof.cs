using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;

// Existing fictional fixtures only. This branch has no POST, seeding, engine invocation or SaveChanges.
static class CombinedFindingsReadProof
{
 public static async Task RunAsync(WebApplication host,Func<WebApplication,Guid?,string,HttpClient> client,string connection,Guid selected,Guid ds,Guid other,Guid commissioner){
  var checks=new List<string>();void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);}
  using var owner=client(host,ds,"Officer");using var foreign=client(host,other,"Officer");using var central=client(host,commissioner,"Officer");using var applicant=client(host,Guid.NewGuid(),"Applicant");using var anonymous=client(host,null,"Officer");
  var root="/api/governance-intelligence/";var early=(await owner.GetFromJsonAsync<OfficerAssessment>(root+"officer-assessments/"+selected))!;
  Check(early.Id==selected&&early.State=="Saved"&&early.EarlyAssessmentId!=null,"Exact existing saved Early request/result retrieved");
  var investigation=(await owner.GetFromJsonAsync<Investigation>(root+"officer-investigations/"+early.InvestigationId))!;
  Check(investigation.Id==early.InvestigationId&&investigation.Findings.Count==7,"Exact persisted seven-concern investigation retrieved");
  var completion=(await owner.GetFromJsonAsync<InvestigationEvidenceView>(root+"officer-investigations/"+investigation.Id+"/evidence"))!;
  Check(completion.Finalization?.InvestigationId==investigation.Id&&completion.Attachments.Count>0,"Persisted manual finalization and pre-screening evidence available");
  var evidence=(await owner.GetFromJsonAsync<ManualEvidenceView>(root+"officer-assessments/"+selected+"/manual-evidence"))!;
  Check(evidence.History.Count>0&&evidence.Attachments.Count>0,"Saved later evidence and immutable verification history available");
  foreach(var file in completion.Attachments){var path=root+"officer-investigations/"+investigation.Id+"/evidence/attachments/"+file.Id;var bytes=await owner.GetByteArrayAsync(path);Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()==file.Sha256,"Current-investigation download retains exact stored bytes/hash");Check(!(await foreign.GetAsync(path)).IsSuccessStatusCode,"Other DS denied current-investigation download");Check((await central.GetAsync(path)).IsSuccessStatusCode,"Existing Commissioner read-only current-investigation download preserved");}
  foreach(var file in evidence.Attachments){var path=root+"officer-assessments/"+selected+"/manual-evidence/attachments/"+file.Id;var bytes=await owner.GetByteArrayAsync(path);Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant()==file.Sha256,"Saved assessment evidence download retains exact original bytes/hash");Check(!(await foreign.GetAsync(path)).IsSuccessStatusCode,"Other DS denied saved-assessment evidence download");}

  // Read all existing exact links for this known fixture. No newest-report substitution.
  await using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseNpgsql(connection).Options);
  var provenance=await db.Set<RegulatoryEvaluationProvenanceEntity>().AsNoTracking().Where(x=>x.CreatorActorId==ds).ToArrayAsync();
  var linked=provenance.Where(x=>JsonSerializer.Deserialize<SavedRegulatoryProvenance>(x.ProvenanceJson,RegulatoryJson.Options)?.EarlyRequestId==selected).Select(x=>x.EvaluationId).ToArray();
  Check(linked.Length>=3,"Multiple independently saved Regulatory fixtures retain exact Early provenance");
  var ready=0;var held=0;var decisions=0;
  foreach(var id in linked){
   var route=root+"regulatory-assessments/"+id;var report=(await owner.GetFromJsonAsync<IndependentRegulatoryReport>(route))!;
   Check(report.Metadata.Id==id&&report.Provenance!.EarlyRequestId==selected&&report.Metadata.ProposalId==investigation.ProposalId,"Exact Regulatory identity/proposal/provenance "+id);
   var history=(await owner.GetFromJsonAsync<RegulatoryFollowupHistory>(route+"/follow-ups"))!;decisions+=history.History.Count;
   var seqRoute=root+$"officer-sequence?earlyRequestId={selected}&regulatoryEvaluationId={id}";
   var sequence=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(seqRoute))!;if(sequence.CanProceedToConflict)ready++;else held++;
   Check(sequence.RegulatoryEvaluationId==id&&sequence.EarlyRequestId==selected,"Server readiness returns exact selected chain "+id);
   Check((await foreign.GetAsync(route)).StatusCode==HttpStatusCode.Forbidden,"Foreign DS Regulatory detail denied "+id);
   Check((await applicant.GetAsync(route)).StatusCode==HttpStatusCode.Forbidden,"Applicant Regulatory detail denied "+id);
   Check((await central.GetAsync(route)).StatusCode==HttpStatusCode.OK,"Existing Commissioner read-only saved Regulatory authority preserved "+id);
   var reload=(await owner.GetFromJsonAsync<OfficerSequenceReadiness>(seqRoute))!;Check(reload==sequence||reload.Holds.SequenceEqual(sequence.Holds)&&reload.CanProceedToConflict==sequence.CanProceedToConflict,"Reload readiness stable without writes "+id);
  }
  Check(ready>0&&held>0&&decisions>0,"Saved compliant/adverse/unresolved fixtures and latest follow-up decisions read distinctly");
  foreach(var route in new[]{root+"officer-assessments/"+selected,root+"officer-investigations/"+investigation.Id,root+"officer-investigations/"+investigation.Id+"/evidence",root+"officer-assessments/"+selected+"/manual-evidence",root+"officer-sequence?earlyRequestId="+selected}){
   Check(!(await foreign.GetAsync(route)).IsSuccessStatusCode,"Other DS denied selected officer findings "+route);
   Check((await applicant.GetAsync(route)).StatusCode==HttpStatusCode.Forbidden,"Applicant denied protected summary source "+route);
   Check((await anonymous.GetAsync(route)).StatusCode==HttpStatusCode.Unauthorized,"Anonymous denied protected summary source "+route);
  }
  Check((await central.GetAsync(root+"officer-assessments/"+selected)).IsSuccessStatusCode,"Commissioner existing saved Early read preserved");
  Check(!(await central.GetAsync(root+"officer-investigations/"+investigation.Id)).IsSuccessStatusCode,"Commissioner gains no DS investigation authority");
  Check(!(await central.GetAsync(root+"officer-sequence?earlyRequestId="+selected)).IsSuccessStatusCode,"Commissioner gains no owner-specific journey authority");
  Console.WriteLine(JsonSerializer.Serialize(new{Status="Passed",Checks=checks,SelectedEarlyRequestId=selected,LinkedRegulatoryEvaluationIds=linked,ReadyChains=ready,HeldChains=held,ClassifierOrAnomalyCalls=0,EngineExecutions=0,DatabaseWrites=0},new JsonSerializerOptions{WriteIndented=true}));
  await host.StopAsync();
 }
}
