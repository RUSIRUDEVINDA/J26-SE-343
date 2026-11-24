using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Presentation.Controllers;
using Microsoft.EntityFrameworkCore;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public sealed class OfficerReportControllerAuthorizationTests
{
 static readonly Guid Commissioner=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3");
 sealed class Source:ICommissionerReportGrantSource {
  public string Fault="active";
  public CentralComplaintGrant Grant=new(Commissioner,Guid.NewGuid(),"COMMISSIONER",[CommissionerReportReadAccess.Permission],DateTimeOffset.UnixEpoch,null,"Explicit controller test grant"){Persistent=true};
  public Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct) {
   if(Fault is "malformed" or "unavailable")throw new CentralComplaintUnavailableException();
   return Task.FromResult(Fault=="missing"?Array.Empty<CentralComplaintGrant>():[Grant]);
  }
 }
 sealed class Directory:IComplaintOfficerDirectory {
  public bool Active=true;
  public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(Active&&actor==Commissioner);
  public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],"test"));
 }
 sealed class Environment:IHostEnvironment {
  public string EnvironmentName{get;set;}="Development";public string ApplicationName{get;set;}="controller-test";
  public string ContentRootPath{get;set;}="";public IFileProvider ContentRootFileProvider{get;set;}=new NullFileProvider();
 }
 sealed class Store:IOfficerFlowStore {
  public int Reads,Commands;public bool Deny;public readonly Guid Creator=Guid.NewGuid();
  OfficerAssessment Report(Guid id)=>new(id,Creator,"early","original-scope","original-version","original-hash","Saved",DateTimeOffset.UnixEpoch,null,null,null,null,null,null,null,null,1,null,null,[]);
  public Task<OfficerAssessment> GetAsync(Guid actor,Guid id,CancellationToken ct){Reads++;if(Deny)throw new UnauthorizedAccessException();return Task.FromResult(Report(id));}
  public Task<IReadOnlyList<OfficerAssessment>> HistoryAsync(Guid actor,CancellationToken ct){Reads++;return Task.FromResult<IReadOnlyList<OfficerAssessment>>([Report(Guid.NewGuid())]);}
  public Task<bool> ReadyAsync(CancellationToken ct){Commands++;return Task.FromResult(true);}
  public Task<Investigation> SaveAsync(Guid actor,SaveInvestigation input,CancellationToken ct){Commands++;throw new InvalidOperationException("Must not write");}
  public Task<Investigation> InvestigationAsync(Guid actor,Guid id,CancellationToken ct){Commands++;throw new InvalidOperationException("Must not read private draft");}
  public Task<IReadOnlyList<Investigation>> InvestigationsAsync(Guid actor,CancellationToken ct){Commands++;throw new InvalidOperationException("Must not list drafts");}
  public Task<OfficerAssessment> EarlyAsync(Guid actor,EarlyRequest input,CancellationToken ct){Commands++;throw new InvalidOperationException("Must not run");}
  public Task<OfficerAssessment> AnomalyAsync(Guid actor,AnomalyRequest input,CancellationToken ct){Commands++;throw new InvalidOperationException("Must not infer");}
 }
 static OfficerFlowsController Controller(Store store,Source source,Directory directory,Guid? actor=null,string role="Officer",string institution="COMMISSIONER",bool authenticated=true) {
  var config=new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"GovernanceIntelligence:KiwulaWorkspace:Enabled","true"}}).Build();
  var policy=new CommissionerReportReadAccess(new(){Enabled=true},source,directory,TimeProvider.System);
  var c=new OfficerFlowsController(store,new Environment(),config,policy){ControllerContext=new(){HttpContext=new DefaultHttpContext()}};
  c.HttpContext.User=new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier,(actor??Commissioner).ToString()),new(ClaimTypes.Role,role),new("institution",institution)],authenticated?"authenticated-test":null));return c;
 }
 [Theory][InlineData(false)][InlineData(true)]
 public async Task ExplicitCommissioner_SavedGetAndHistory_SucceedWithoutChangingCreator(bool history) {
  var store=new Store();var c=Controller(store,new(),new());var result=history?await c.History(default):await c.Get(Guid.NewGuid(),default);
  var ok=Assert.IsType<OkObjectResult>(result);var report=history?Assert.IsAssignableFrom<IReadOnlyList<OfficerAssessment>>(ok.Value)[0]:Assert.IsType<OfficerAssessment>(ok.Value);
  Assert.Equal(store.Creator,report.CreatorActorId);Assert.Equal("original-version",report.SourceVersion);Assert.Equal(0,store.Commands);Assert.Equal("no-store",c.Response.Headers.CacheControl);
 }
 [Theory][InlineData("missing")][InlineData("revoked")][InlineData("expired")][InlineData("malformed")][InlineData("unavailable")][InlineData("inactive")][InlineData("wrong-actor")][InlineData("complaint-only")]
 public async Task SavedRoutes_FailClosedBeforeStore_WhenExplicitAuthorityInvalid(string fault) {
  var source=new Source{Fault=fault};var directory=new Directory{Active=fault!="inactive"};var store=new Store();
  if(fault=="revoked")source.Grant.Revoked=true;if(fault=="expired")source.Grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);if(fault=="complaint-only")source.Grant.Permissions=[ComplaintRoutingPolicy.Read];
  var c=Controller(store,source,directory,fault=="wrong-actor"?Guid.NewGuid():null);
  Assert.IsType<ForbidResult>(await c.Get(Guid.NewGuid(),default));Assert.IsType<ForbidResult>(await c.History(default));Assert.Equal(0,store.Reads);
 }
 [Theory][InlineData("Applicant","APPLICANT")][InlineData("Officer","OTHER")][InlineData("Officer","COMMISSIONER")]
 public async Task RoleOrInstitutionAloneCannotRead(string role,string institution) {
  var store=new Store();var c=Controller(store,new(),new(),Guid.NewGuid(),role,institution);Assert.IsType<ForbidResult>(await c.Get(Guid.NewGuid(),default));Assert.IsType<ForbidResult>(await c.History(default));Assert.Equal(0,store.Reads);
 }
 [Fact]public async Task AnonymousDeniedBeforeStore(){var store=new Store();var c=Controller(store,new(),new(),authenticated:false);Assert.IsType<UnauthorizedResult>(await c.Get(Guid.NewGuid(),default));Assert.Equal(0,store.Reads);}
 [Fact]public async Task DsPathRemainsIndependentOfCommissionerGrant(){var store=new Store();var c=Controller(store,new(){Fault="unavailable"},new(),Guid.NewGuid(),institution:"DS");Assert.IsType<OkObjectResult>(await c.Get(Guid.NewGuid(),default));Assert.IsType<OkObjectResult>(await c.History(default));}
 [Theory][InlineData("catalogue")][InlineData("save")][InlineData("draft")][InlineData("drafts")][InlineData("early")][InlineData("anomaly")]
 public async Task ReportPermissionNeverAdmitsCommandsOrPrivateDrafts(string operation){
  var store=new Store();var c=Controller(store,new(),new());var id=Guid.NewGuid();var result=operation switch {
   "catalogue"=>await c.Catalogue(default),"save"=>await c.Save(new(id,"test",[]),default),"draft"=>await c.Investigation(id,default),"drafts"=>await c.Investigations(default),"early"=>await c.Early(new(id,id),default),_=>await c.Anomaly(new("test",id),default)};
  Assert.IsType<ForbidResult>(result);Assert.Equal(0,store.Commands);
 }
 [Fact]public async Task LiveRevocationDeniesNextGetAndHistory(){var source=new Source();var store=new Store();var c=Controller(store,source,new());Assert.IsType<OkObjectResult>(await c.Get(Guid.NewGuid(),default));source.Grant.Revoked=true;Assert.IsType<ForbidResult>(await c.Get(Guid.NewGuid(),default));Assert.IsType<ForbidResult>(await c.History(default));Assert.Equal(1,store.Reads);}
 [Fact]public async Task ControllerPreservesRepositoryDenialForForeignPendingOrUnknown(){var store=new Store{Deny=true};Assert.IsType<ForbidResult>(await Controller(store,new(),new()).Get(Guid.NewGuid(),default));}
 [Theory][InlineData("Pending")][InlineData("Unknown")]
 public async Task RealRepository_ExplicitReadGrantDoesNotExposeForeignUncertainRequests(string state){
  await using var db=new GovernanceIntelligenceDbContext(new DbContextOptionsBuilder<GovernanceIntelligenceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
  var row=new OfficerAssessmentRequestEntity{Id=Guid.NewGuid(),CreatorActorId=Guid.NewGuid(),SubmissionKey=Guid.NewGuid(),Kind="anomaly",ScopeKey="test",SourceVersion="test/1",InputSha256="test",State=state,CreatedAtUtc=DateTimeOffset.UtcNow};db.Set<OfficerAssessmentRequestEntity>().Add(row);await db.SaveChangesAsync();
  var policy=new CommissionerReportReadAccess(new(){Enabled=true},new Source(),new Directory(),TimeProvider.System);
  var store=new OfficerFlowStore(db,new ConfigurationBuilder().Build(),TimeProvider.System,null!,null!,null!,null!,null!,policy);
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>store.GetAsync(Commissioner,row.Id,default));Assert.Empty(await store.HistoryAsync(Commissioner,default));
 }
}
