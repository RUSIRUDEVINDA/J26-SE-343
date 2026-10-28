using StateLandGovernance.GovernanceIntelligence.Application.Complaints;
namespace StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
public sealed class CommissionerReportReadOptions { public bool Enabled {get;set;} public string? GrantFilePath {get;set;} }
public interface ICommissionerReportGrantSource { Task<CentralComplaintGrant[]> ReadAsync(CancellationToken ct); }
public interface ICommissionerReportReadAccess { Task<bool> CanReadAllAsync(Guid actor,CancellationToken ct); }
public sealed class CommissionerReportReadAccess(CommissionerReportReadOptions options,ICommissionerReportGrantSource source,IComplaintOfficerDirectory directory,TimeProvider clock):ICommissionerReportReadAccess
{
 public const string Permission="C4AssessmentReadAll";
 public async Task<bool> CanReadAllAsync(Guid actor,CancellationToken ct){
  if(!options.Enabled||actor!=Guid.Parse("0ab669fe-d0ab-5695-ad55-10f6b3b5aab3"))return false;
  try{
   var grants=await source.ReadAsync(ct);var matches=grants.Where(x=>x.ActorId==actor).ToArray();if(matches.Length!=1)return false;var g=matches[0];var now=clock.GetUtcNow();
   return g.GrantId!=Guid.Empty&&!g.Revoked&&g.Persistent&&g.Institution=="COMMISSIONER"&&g.ValidFromUtc!=default&&g.ValidFromUtc.Offset==TimeSpan.Zero&&now>=g.ValidFromUtc&&
    (g.ExpiresAtUtc==null||(g.ExpiresAtUtc.Value.Offset==TimeSpan.Zero&&now<g.ExpiresAtUtc&&g.ExpiresAtUtc>g.ValidFromUtc))&&
    g.Permissions.SequenceEqual(new[]{Permission})&&g.ReviewOperations.Length==0&&!string.IsNullOrWhiteSpace(g.SourceReference)&&g.SourceReference.Length<=500&&grants.Count(x=>x.GrantId==g.GrantId)==1&&await directory.IsCommissionerAsync(actor,ct);
  }catch(CentralComplaintUnavailableException){return false;}
 }
}
