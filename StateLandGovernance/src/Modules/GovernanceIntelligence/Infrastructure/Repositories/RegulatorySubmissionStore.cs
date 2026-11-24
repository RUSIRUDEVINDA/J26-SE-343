using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence.Entities;
namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Repositories;
public sealed class RegulatorySubmissionStore(GovernanceIntelligenceDbContext db,IIndependentRegulatoryReportStore reports,IConfiguration config):IRegulatorySubmissionStore{
 private bool Enabled=>config.GetValue<bool>("GovernanceIntelligence:RegulatoryProvenance:Enabled");
 private DbSet<RegulatoryEvaluationProvenanceEntity> Rows=>db.Set<RegulatoryEvaluationProvenanceEntity>();
 public async Task<bool> CanSubmitAsync(CancellationToken ct){
  if(!Enabled||!config.GetValue<bool>("GovernanceIntelligence:RegulatoryProvenance:WritesEnabled"))return false;
  try{
   await Rows.AsNoTracking().Take(1).CountAsync(ct);
   const string readinessSql = """
   SELECT current_setting('transaction_read_only') = 'off'
    AND has_table_privilege(current_user, 'governance_intelligence.regulatory_evaluation_provenance', 'INSERT')
    AND has_table_privilege(current_user, 'governance_intelligence.compliance_evaluations', 'INSERT')
    AND has_table_privilege(current_user, 'governance_intelligence.governance_audit_records', 'INSERT')
    AND has_table_privilege(current_user, 'governance_intelligence.governance_audit_anchor_outbox', 'INSERT') AS "Value"
   """;
   return await db.Database.SqlQueryRaw<bool>(readinessSql).SingleAsync(ct);
  }catch(OperationCanceledException){throw;}catch{return false;}
 }
 public async Task<Guid?> ProtectedEvaluationForAuditAsync(Guid auditId,CancellationToken ct)=>!Enabled?null:await (from p in Rows.AsNoTracking() join e in db.ComplianceEvaluations.AsNoTracking() on p.EvaluationId equals e.Id where e.AuditRecordId==auditId select (Guid?)e.Id).SingleOrDefaultAsync(ct);
 public async Task<IReadOnlyList<Guid>> OwnedIdsAsync(Guid actor,CancellationToken ct)=>!Enabled?Array.Empty<Guid>():await Rows.AsNoTracking().Where(x=>x.CreatorActorId==actor).Select(x=>x.EvaluationId).ToArrayAsync(ct);
 public async Task<Guid?> FindSubmissionAsync(Guid actor,Guid key,CancellationToken ct)=>!Enabled?null:await Rows.AsNoTracking().Where(x=>x.CreatorActorId==actor&&x.SubmissionKey==key).Select(x=>(Guid?)x.EvaluationId).SingleOrDefaultAsync(ct);
 public async Task<SavedRegulatoryProvenance?> ProvenanceAsync(Guid id,CancellationToken ct){
  if(!Enabled)return null;var row=await Rows.AsNoTracking().SingleOrDefaultAsync(x=>x.EvaluationId==id,ct);return row==null?null:JsonSerializer.Deserialize<SavedRegulatoryProvenance>(row.ProvenanceJson,RegulatoryJson.Options);
 }
 public async Task<RegulatoryRunResponse> RunAsync(Guid actor,Guid key,string requestHash,Func<Task<Guid>> evaluate,CancellationToken ct){
  if(!await CanSubmitAsync(ct))throw new RegulatorySubmissionUnavailableException();
  await using var tx=await db.Database.BeginTransactionAsync(ct);
  // Exact submission serialization in this repository only. No network calls: engine + database are local.
  // Parameterized PG transaction lock prevents concurrent duplicate engine execution before the unique index.
  var bytes=System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(actor+":"+key));var lockId=System.Buffers.Binary.BinaryPrimitives.ReadInt64BigEndian(bytes);
  await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({lockId})",ct);
  var row=await Rows.AsNoTracking().SingleOrDefaultAsync(x=>x.CreatorActorId==actor&&x.SubmissionKey==key,ct);
  Guid id;if(row!=null){if(row.SubmissionHash!=requestHash)throw new RegulatorySubmissionConflictException();id=row.EvaluationId;}
  else id=await evaluate();
  var report=await reports.GetAsync(id,ct)??throw new RegulatorySubmissionUnavailableException();
  report=report with{Provenance=await ProvenanceAsync(id,ct)};
  await tx.CommitAsync(ct);return new(report,row!=null);
 }
}
