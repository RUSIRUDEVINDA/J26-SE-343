using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
namespace StateLandGovernance.UnitTests.GovernanceIntelligence;
public sealed class CommissionerReportReadTests
{
 static readonly Guid Actor=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3");
 sealed class Source:ICommissionerReportGrantSource{
  public CentralComplaintGrant Grant=new(Actor,Guid.NewGuid(),"COMMISSIONER",[CommissionerReportReadAccess.Permission],DateTimeOffset.UnixEpoch,null,"Explicit report-read test provision"){Persistent=true};
  public bool Missing;
  public Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct)=>Task.FromResult(Missing?Array.Empty<CentralComplaintGrant>():[Grant]);
 }
 sealed class Directory:IComplaintOfficerDirectory{
  public bool Active=true;
  public Task<bool> IsCommissionerAsync(Guid actor,CancellationToken ct)=>Task.FromResult(Active);
  public Task<ComplaintDirectorySnapshot> ReadAsync(CancellationToken ct)=>Task.FromResult(new ComplaintDirectorySnapshot([],"unused"));
 }
 [Theory][InlineData("authorized")][InlineData("disabled")][InlineData("inactive")][InlineData("other-actor")][InlineData("role-only")][InlineData("revoked")][InlineData("expired")][InlineData("complaint-permission")]
 public async Task ReadOnlyGrant_IsExplicitCurrentAndIndependentFromComplaintPermissions(string fault){
  var source=new Source();var directory=new Directory();var options=new CommissionerReportReadOptions{Enabled=fault!="disabled"};
  if(fault=="inactive")directory.Active=false;if(fault=="role-only")source.Missing=true;if(fault=="revoked")source.Grant.Revoked=true;if(fault=="expired")source.Grant.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);if(fault=="complaint-permission")source.Grant.Permissions=[ComplaintRoutingPolicy.Read];
  var policy=new CommissionerReportReadAccess(options,source,directory,TimeProvider.System);
  Assert.Equal(fault=="authorized",await policy.CanReadAllAsync(fault=="other-actor"?Guid.NewGuid():Actor,default));
 }
 sealed class Assignments:IIndependentRegulatoryReadAssignments{public Task<IReadOnlyList<Guid>> ReadAllowedIdsAsync(Guid actor,CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([]);}
 sealed class Store:IIndependentRegulatoryReportStore{
  public Guid Id=Guid.NewGuid();public Action? AfterRead;
  public Task<IReadOnlyList<Guid>> SavedIdsAsync(CancellationToken ct)=>Task.FromResult<IReadOnlyList<Guid>>([Id]);
  public Task<IReadOnlyList<IndependentRegulatorySummary>> ListAsync(IReadOnlyList<Guid> ids,int limit,CancellationToken ct)=>Task.FromResult<IReadOnlyList<IndependentRegulatorySummary>>([Report().Metadata]);
  IndependentRegulatoryReport Report()=>new(new(Id,Guid.NewGuid(),null,"unchanged-proposal","Saved","Compliant",DateTime.UtcNow,"original-catalogue",null),[]);
  public Task<IndependentRegulatoryReport?> GetAsync(Guid id,CancellationToken ct){var r=Report();AfterRead?.Invoke();return Task.FromResult<IndependentRegulatoryReport?>(r);}
 }
 [Fact] public async Task RegulatoryRead_RevocationDuringAsyncRead_DeniesRatherThanLeakingReport(){
  var source=new Source();var policy=new CommissionerReportReadAccess(new(){Enabled=true},source,new Directory(),TimeProvider.System);var store=new Store();store.AfterRead=()=>source.Grant.Revoked=true;
  await Assert.ThrowsAsync<UnauthorizedAccessException>(()=>new IndependentRegulatoryReportQueries(store,new Assignments(),null,policy).GetAsync(Actor,store.Id,default));
 }
 [Fact] public async Task RegulatoryRead_DoesNotReplaceStoredMetadataOrInventMissingOwnership(){
  var store=new Store();var policy=new CommissionerReportReadAccess(new(){Enabled=true},new Source(),new Directory(),TimeProvider.System);
  var report=await new IndependentRegulatoryReportQueries(store,new Assignments(),null,policy).GetAsync(Actor,store.Id,default);
  Assert.Equal("original-catalogue",report!.Metadata.RuleSetVersion);Assert.Null(report.Provenance);
 }
}
