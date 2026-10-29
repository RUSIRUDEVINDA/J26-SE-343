using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
namespace StateLandGovernance.GovernanceIntelligence.Application.RegulatoryReports;
public sealed record RegulatoryCaseBinding(Guid CaseId,Guid? SnapshotId=null,Guid? ProposalVersion=null);
public sealed record SavedRegulatoryProvenance(Guid CreatorActorId,Guid SubmissionKey,string SubmissionHash,string InputJson,
 string EvaluatedInputHash,string InputVersion,string SchemaVersion,string SourceReference,string CatalogueDigest,string CatalogueVersion,RegulatoryCaseBinding? C3Binding){
 [System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] public Guid? EarlyRequestId {get;init;}
}
public sealed record RegulatoryRunResponse(IndependentRegulatoryReport Report,bool Reused);
public interface IRegulatoryCaseBindingValidator { Task ValidateAsync(Guid actor,RegulatoryCaseBinding? binding,CancellationToken ct); }
public sealed class UnavailableRegulatoryCaseBindingValidator:IRegulatoryCaseBindingValidator{
 public Task ValidateAsync(Guid actor,RegulatoryCaseBinding? binding,CancellationToken ct){if(binding!=null)throw new UnauthorizedAccessException();return Task.CompletedTask;}
}
public interface IRegulatorySubmissionStore{
 Task<bool> CanSubmitAsync(CancellationToken ct)=>Task.FromResult(false);
 Task<Guid?> ProtectedEvaluationForAuditAsync(Guid auditId,CancellationToken ct)=>Task.FromResult<Guid?>(null);
 Task<RegulatoryRunResponse> RunAsync(Guid actor,Guid key,string requestHash,Func<Task<Guid>> evaluate,CancellationToken ct);
 Task<IReadOnlyList<Guid>> OwnedIdsAsync(Guid actor,CancellationToken ct);
 Task<SavedRegulatoryProvenance?> ProvenanceAsync(Guid id,CancellationToken ct);
 Task<Guid?> FindSubmissionAsync(Guid actor,Guid key,CancellationToken ct)=>Task.FromResult<Guid?>(null);
}
public sealed class RegulatorySubmissionConflictException:Exception{}
public sealed class RegulatorySubmissionUnavailableException:Exception{}
public static class RegulatoryJson{
 public static readonly JsonSerializerOptions Options=new(JsonSerializerDefaults.Web);
 public static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
public sealed class RegulatorySubmissionService(EvaluateComplianceCommandHandler handler,IRegulatorySubmissionStore store,IRegulatoryCaseBindingValidator bindings,StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows.IOfficerSequencePrerequisites? prerequisites=null){
 public async Task<RegulatoryRunResponse> RunAsync(Guid actor,EvaluateComplianceCommand command,CancellationToken ct){
  if(actor==Guid.Empty)throw new UnauthorizedAccessException();
  if(command.Input==null||command.SubmissionKey is not {} key||key==Guid.Empty||command.SchemaVersion!="c4-regulatory-input/1"||string.IsNullOrWhiteSpace(command.InputVersion)||command.InputVersion.Length>100||string.IsNullOrWhiteSpace(command.SourceReference)||command.SourceReference.Length>2000||string.IsNullOrWhiteSpace(command.ActionName)||command.ActionName.Length>100||command.CaseId!=null)
   throw new ArgumentException("Supply structured input, submissionKey, inputVersion, schemaVersion c4-regulatory-input/1 and declared sourceReference. Use optional validated C3Binding rather than a free-form CaseId.");
  await bindings.ValidateAsync(actor,command.C3Binding,ct);
  var inputJson=JsonSerializer.Serialize(command.Input,RegulatoryJson.Options);
  var requestHash=RegulatoryJson.Hash(JsonSerializer.Serialize(command with{SubmissionKey=null},RegulatoryJson.Options));
  var provenance=new SavedRegulatoryProvenance(actor,key,requestHash,inputJson,"",command.InputVersion,command.SchemaVersion,command.SourceReference,"","",command.C3Binding){EarlyRequestId=command.EarlyRequestId};
  return await store.RunAsync(actor,key,requestHash,async()=>{
   if(prerequisites==null||command.EarlyRequestId is not {} earlyRequest)throw new ArgumentException("Complete Early Governance and select its saved request before a new regulatory execution.");
   try{await prerequisites.RequireEarlyAsync(actor,earlyRequest,command.Input.ProposalId,ct);}catch(Exception e)when(e is StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows.OfficerUnavailableException or StateLandGovernance.GovernanceIntelligence.Application.Complaints.CentralComplaintUnavailableException){throw new RegulatorySubmissionUnavailableException();}
   var result=await handler.HandleAsync(command,ct,provenance);
   return result.StoredEvaluationId??throw new RegulatorySubmissionUnavailableException();
  },ct);
 }
}
