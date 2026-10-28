using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.ComplaintClassification;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public class RegisteredComplaintTests
{
 sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
 sealed class Access:IComplaintReferenceAccess,IComplaintOfficerDirectory{
  public Guid Reviewer=Guid.NewGuid(),Case=Guid.NewGuid();public bool Revoked,Reject;public int ReferenceCalls;
  public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(false);
  public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot(Revoked?[]:[new(Reviewer,"Test DS","TEST")],new string('a',64)));
  public Task<ComplaintReference> ValidateAsync(string? c,string? p,CancellationToken ct){ReferenceCalls++;if(Reject)throw new ArgumentException("Unknown reference");return Task.FromResult(new ComplaintReference(c==null?null:Case,p));}
  public Task<IReadOnlyList<Guid>> ReviewerCasesAsync(Guid actor,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>(actor==Reviewer&&!Revoked?new[]{Case}:Array.Empty<Guid>());
 }
 sealed class Classifier:IComplaintClassificationClient{
  public int Calls;public string? LastText;public bool Fail=true;public Func<Task>? During;
  public async Task<ComplaintClassificationResult> ClassifyAsync(string text,string? binding=null,CancellationToken ct=default){Calls++;LastText=text;if(During!=null)await During();return Fail?ComplaintClassificationResult.Failure(ComplaintClassificationErrorCode.ServiceUnavailable,"No service"):ComplaintClassificationResult.Success(new("actual-test-model","AdministrativeProceduralIntegrity",new Dictionary<string,double>{{"AdministrativeProceduralIntegrity",1}},binding,"advisory","closed-set"));}
 }
 sealed class Assessments:IComplaintClassificationAssessmentStore{
  public Dictionary<Guid,ComplaintClassificationAssessment> Rows=new();
  public Task AddAsync(ComplaintClassificationAssessment a,CancellationToken ct=default){Rows.Add(a.AssessmentId,a);return Task.CompletedTask;}
  public Task<ComplaintClassificationAssessment?> GetByIdAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Rows.GetValueOrDefault(id));
  public Task<IReadOnlyList<ComplaintClassificationAssessment>> GetByCaseIdAsync(string id,int limit=100,CancellationToken ct=default)=>Task.FromResult<IReadOnlyList<ComplaintClassificationAssessment>>(Rows.Values.Where(x=>x.CaseId==id).ToArray());
 }
 sealed class Store(Assessments assessments):IRegisteredComplaintStore{
  public Dictionary<Guid,RegisteredComplaint> Rows=new();
  public Dictionary<Guid,Guid> ExplicitAssignments=new();
  static RegisteredComplaint Clone(RegisteredComplaint c)=>JsonSerializer.Deserialize<RegisteredComplaint>(JsonSerializer.Serialize(c))!;
  public Task<RegisteredComplaint?> GetAsync(Guid id,CancellationToken ct)=>Task.FromResult(Rows.TryGetValue(id,out var r)?Clone(r):null);
  public Task<RegisteredComplaint?> FindSubmissionAsync(Guid a,Guid k,CancellationToken ct)=>Task.FromResult(Rows.Values.SingleOrDefault(x=>x.ApplicantId==a&&x.SubmissionKey==k));
  public Task<IReadOnlyList<RegisteredComplaint>> ListAsync(Guid? a,IReadOnlyList<Guid> cases,CancellationToken ct)=>Task.FromResult<IReadOnlyList<RegisteredComplaint>>(Rows.Values.Where(x=>a!=null?x.ApplicantId==a:x.CaseId!=null&&cases.Contains(x.CaseId.Value)).ToArray());
  public Task<bool> InsertAsync(RegisteredComplaint c,CancellationToken ct){if(Rows.Values.Any(x=>x.ApplicantId==c.ApplicantId&&x.SubmissionKey==c.SubmissionKey))return Task.FromResult(false);Rows.Add(c.Id,Clone(c));return Task.FromResult(true);}
  public async Task SaveAsync(RegisteredComplaint c,int revision,ComplaintClassificationAssessment? assessment,CancellationToken ct){if(Rows[c.Id].Revision!=revision)throw new ComplaintConcurrencyException();Rows[c.Id]=Clone(c);if(assessment!=null)await assessments.AddAsync(assessment,ct);}
  public Task<Guid?> ComplaintForAssessmentAsync(Guid id,CancellationToken ct)=>Task.FromResult<Guid?>(Rows.Values.SingleOrDefault(x=>x.Attempts.Any(a=>a.AssessmentId==id))?.Id);
 }
 static SubmitRegisteredComplaint Input()=>new(Guid.NewGuid(),"Concern","Original fictional English complaint.","known-reference",null);
 sealed class Routing(Store store,Access directory,Clock clock):IComplaintRoutingStore{
  public Task<ComplaintAssignment?> CurrentAsync(Guid id,CancellationToken ct)=>Task.FromResult(store.ExplicitAssignments.TryGetValue(id,out var target)?new ComplaintAssignment(Guid.NewGuid(),id,1,target,"TEST",Guid.NewGuid(),clock.Now,"fixture",Guid.NewGuid(),new string('a',64),null,new string('a',64),Guid.NewGuid(),"test","{}",new string('a',64)):null);
  public Task<IReadOnlyList<RegisteredComplaint>> AssignedAsync(Guid actor,CancellationToken ct)=>Task.FromResult<IReadOnlyList<RegisteredComplaint>>(store.Rows.Values.Where(x=>store.ExplicitAssignments.GetValueOrDefault(x.Id)==actor&&!directory.Revoked).ToArray());
  public Task<IReadOnlyList<ComplaintAssignment>> HistoryAsync(Guid id,CancellationToken ct)=>throw new NotSupportedException();
  public Task<(IReadOnlyList<RegisteredComplaint> Items,string? Next)> QueueAsync(bool assigned,string? cursor,CancellationToken ct)=>throw new NotSupportedException();
  public Task<ComplaintAssignmentResult> AssignAsync(Guid id,AssignComplaint input,CancellationToken ct)=>throw new NotSupportedException();
  public async Task<RegisteredComplaint> ReviewAsync(Guid actor,Guid id,int revision,string status,string reason,CancellationToken ct,int? expectedHandlingRevision=null){var row=store.Rows[id];if(store.ExplicitAssignments.GetValueOrDefault(id)!=actor||directory.Revoked)throw new KeyNotFoundException();if(row.Revision!=revision)throw new ComplaintConcurrencyException();var updated=row with{Revision=revision+1,ReviewStatus=status,Reviews=row.Reviews.Append(new ComplaintReview(Guid.NewGuid(),actor,clock.Now,status,reason)).ToArray()};await store.SaveAsync(updated,revision,null,ct);return updated;}
 }
 static (RegisteredComplaintService,Store,Access,Classifier,Assessments,Clock) Setup(){var a=new Assessments();var s=new Store(a);var access=new Access();var c=new Classifier();var t=new Clock();return(new(s,access,c,a,t,new Routing(s,access,t),new ComplaintRoutingPolicy(new(){Enabled=true},access,t)),s,access,c,a,t);}

 [Fact]public async Task MlFailure_RetainsOriginalAndTimestamp_AndDuplicateDoesNotClassifyAgain(){
  var(s,store,_,client,assessments,time)=Setup();var owner=Guid.NewGuid();var input=Input();var first=await s.SubmitAsync(owner,input,default);var same=await s.SubmitAsync(owner,input,default);
  Assert.Equal(first.Id,same.Id);Assert.Equal(input.ComplaintText,first.ComplaintText);Assert.Equal(time.Now,first.SubmittedAtUtc);Assert.Equal("Failed",first.ClassificationState);Assert.Empty(assessments.Rows);Assert.Equal(1,client.Calls);Assert.Single(store.Rows);
  await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.SubmitAsync(owner,input with{ComplaintText="Changed"},default));
 }
 [Fact]public async Task ControlledRetry_ReusesComplaint_PreservesFailureAndReturnedModelMetadata(){
  var(s,store,_,client,assessments,_)=Setup();var owner=Guid.NewGuid();var first=await s.SubmitAsync(owner,Input(),default);client.Fail=false;var retried=await s.ClassifyAsync(owner,false,first.Id,first.Revision,default);
  Assert.Equal(first.Id,retried.Id);Assert.Equal("Classified",retried.ClassificationState);Assert.Equal("Open",retried.ReviewStatus);Assert.Equal(2,retried.Attempts.Length);Assert.Equal("Failed",retried.Attempts[0].State);
  var detail=await s.DetailAsync(owner,false,first.Id,default);Assert.Equal("actual-test-model",Assert.Single(detail.Classifications).ModelVersion);Assert.Single(store.Rows);Assert.Single(assessments.Rows);
  await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.ClassifyAsync(owner,false,retried.Id,retried.Revision,default));
 }
 [Fact]public async Task OtherApplicantAndUnassignedOfficer_CannotReadRetryOrReview(){
  var(s,store,access,_,_,_)=Setup();var owner=Guid.NewGuid();var row=await s.SubmitAsync(owner,Input(),default);var stranger=Guid.NewGuid();store.ExplicitAssignments.Add(row.Id,access.Reviewer);
  await Assert.ThrowsAsync<KeyNotFoundException>(()=>s.DetailAsync(stranger,false,row.Id,default));
  await Assert.ThrowsAsync<KeyNotFoundException>(()=>s.ClassifyAsync(stranger,false,row.Id,row.Revision,default));
  await Assert.ThrowsAsync<KeyNotFoundException>(()=>s.ReviewAsync(stranger,row.Id,row.Revision,"UnderReview","reason",default));
  Assert.Empty(await s.ListAsync(stranger,true,default));Assert.Single(await s.ListAsync(access.Reviewer,true,default));
  access.Revoked=true;await Assert.ThrowsAsync<KeyNotFoundException>(()=>s.DetailAsync(access.Reviewer,true,row.Id,default));
 }
 [Fact]public async Task ReviewHistory_ReasonAndRevisionRequired_ClassificationNeverVerifies(){
  var(s,store,access,_,_,_)=Setup();var row=await s.SubmitAsync(Guid.NewGuid(),Input(),default);store.ExplicitAssignments.Add(row.Id,access.Reviewer);
  await Assert.ThrowsAsync<ArgumentException>(()=>s.ReviewAsync(access.Reviewer,row.Id,row.Revision,"Verified","unsupported power",default));
  await Assert.ThrowsAsync<ArgumentException>(()=>s.ReviewAsync(access.Reviewer,row.Id,row.Revision,"UnderReview","",default));
  var review=await s.ReviewAsync(access.Reviewer,row.Id,row.Revision,"UnderReview","Read original concern",default);
  await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.ReviewAsync(access.Reviewer,row.Id,row.Revision,"InformationRequested","Outdated",default));
  var next=await s.ReviewAsync(access.Reviewer,row.Id,review.Revision,"InformationRequested","Please supply a source",default);Assert.Equal(2,next.Reviews.Length);Assert.Equal(row.ComplaintText,next.ComplaintText);Assert.Equal("Failed",next.ClassificationState);
 }
 [Fact]public async Task AddressAndUnknownReferences_ArePreservedUnverified_WithoutPeerLookups(){
  var(s,store,access,client,_,_)=Setup();access.Reject=true;var actor=Guid.NewGuid();var input=Input() with{LandAddress="Kiwulakale, Mahakumbukkadawala, Puttalam.",ParcelId="unknown-parcel"};
  var row=await s.SubmitAsync(actor,input,default);Assert.Null(row.CaseId);Assert.Null(row.ParcelId);Assert.Equal(input.LandAddress,row.LandAddress);Assert.Equal(input.CaseReference,row.UnverifiedCaseReference);Assert.Equal(input.ParcelId,row.UnverifiedParcelReference);Assert.Equal(0,access.ReferenceCalls);Assert.Equal(input.ComplaintText,client.LastText);
  Assert.Empty(await s.ListAsync(access.Reviewer,true,default));Assert.Equal(row.Id,(await s.SubmitAsync(actor,input,default)).Id);Assert.Single(store.Rows);Assert.Equal(1,client.Calls);
  await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.SubmitAsync(actor,input with{LandAddress="Changed address"},default));
 }
 [Fact]public async Task AddressOnlyAndLegacyBlankReferences_AreAcceptedWithoutTrustedAssociations(){
  var(s,_,access,_,_,_)=Setup();access.Reject=true;var address=await s.SubmitAsync(Guid.NewGuid(),Input() with{CaseReference=null,ParcelId=null,LandAddress="Village road, near the school."},default);Assert.Null(address.CaseId);Assert.Null(address.ParcelId);
  var legacy=await s.SubmitAsync(Guid.NewGuid(),Input() with{CaseReference=null,ParcelId=null},default);Assert.Null(legacy.LandAddress);Assert.DoesNotContain("LandAddress",JsonSerializer.Serialize(legacy));Assert.DoesNotContain("UnverifiedCaseReference",JsonSerializer.Serialize(legacy));Assert.Equal(0,access.ReferenceCalls);
 }
 [Fact]public async Task InvalidLocationIsRejectedBeforeRegistrationOrClassifier(){
  var(s,store,_,client,_,_)=Setup();foreach(var value in new[]{new string('a',2001),"location\0invalid"})await Assert.ThrowsAsync<ArgumentException>(()=>s.SubmitAsync(Guid.NewGuid(),Input() with{LandAddress=value},default));Assert.Empty(store.Rows);Assert.Equal(0,client.Calls);
 }
 [Fact]public async Task LegacyHashReplayRetainsOriginalValidatedLinkWithoutNewLookup(){
  var(s,store,access,client,_,time)=Setup();var actor=Guid.NewGuid();var input=Input();var canonical=JsonSerializer.Serialize(new{input.Subject,input.ComplaintText,input.CaseReference,input.ParcelId});var hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
  var original=new RegisteredComplaint(Guid.NewGuid(),actor,input.SubmissionKey,hash,input.Subject,input.ComplaintText,access.Case,"legacy-parcel",time.Now,0,"Pending","Open",[],[]);store.Rows.Add(original.Id,original);access.Reject=true;
  var replay=await s.SubmitAsync(actor,input,default);Assert.Equal(original,replay);Assert.Equal(access.Case,replay.CaseId);Assert.Equal("legacy-parcel",replay.ParcelId);Assert.Equal(0,access.ReferenceCalls);Assert.Equal(0,client.Calls);
 }
 [Fact]public async Task ConcurrentRetry_DoesNotInvokeModelTwice(){
  var(s,_,_,client,_,_)=Setup();var owner=Guid.NewGuid();var row=await s.SubmitAsync(owner,Input(),default);
  client.During=async()=>await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.ClassifyAsync(owner,false,row.Id,row.Revision,default));
  await s.ClassifyAsync(owner,false,row.Id,row.Revision,default);Assert.Equal(2,client.Calls);
 }
 [Fact]public async Task CancelledModelCall_LeavesRegisteredLease_AndExpiredLeaseCanBeRecovered(){
  var(s,store,_,client,_,time)=Setup();var owner=Guid.NewGuid();var row=await s.SubmitAsync(owner,Input(),default);
  using var cts=new CancellationTokenSource();client.During=()=>{cts.Cancel();throw new OperationCanceledException(cts.Token);};
  await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>s.ClassifyAsync(owner,false,row.Id,row.Revision,cts.Token));
  var interrupted=store.Rows[row.Id];Assert.Equal("InProgress",interrupted.ClassificationState);Assert.Equal(row.ComplaintText,interrupted.ComplaintText);
  await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>s.ClassifyAsync(owner,false,row.Id,interrupted.Revision,default));
  time.Now=time.Now.AddMinutes(5);client.During=null;var recovered=await s.ClassifyAsync(owner,false,row.Id,interrupted.Revision,default);
  Assert.Equal("Interrupted",recovered.Attempts[^2].State);Assert.Equal("Failed",recovered.Attempts[^1].State);Assert.Single(store.Rows);
 }
 [Fact]public async Task Repository_RevisionsAndOriginalSurviveReload_InIsolatedMemoryOnly(){
  using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var repository=new RegisteredComplaintStore(db,new Assessments());var input=Input();var row=new RegisteredComplaint(Guid.NewGuid(),Guid.NewGuid(),input.SubmissionKey,"hash",input.Subject,input.ComplaintText,null,null,DateTimeOffset.UtcNow,0,"Pending","Open",[],[]);
  Assert.True(await repository.InsertAsync(row,default));db.ChangeTracker.Clear();Assert.Equal(row.ComplaintText,(await repository.GetAsync(row.Id,default))!.ComplaintText);
  await repository.SaveAsync(row with{Revision=1},0,null,default);await Assert.ThrowsAsync<ComplaintConcurrencyException>(()=>repository.SaveAsync(row with{Revision=2},0,null,default));
 }
 [Fact]public async Task Controller_DefaultDisabledAndAnonymousCannotWrite(){
  var(s,_,_,_,_,_)=Setup();var c=new RegisteredComplaintsController(s,new ConfigurationBuilder().Build()){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
  Assert.IsType<UnauthorizedResult>(await c.Submit(Input(),default));c.HttpContext.User=new(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,Guid.NewGuid().ToString()),new Claim(ClaimTypes.Role,"Applicant")},"test"));
  var result=Assert.IsType<ObjectResult>(await c.Submit(Input(),default));Assert.Equal(503,result.StatusCode);
 }
}
