namespace StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;

public sealed record AttachScreeningEvidence(Guid SubmissionKey,string Concern,string FileName,string ContentType,byte[] Content);
public sealed record VerifyScreeningEvidence(Guid SubmissionKey,string Concern,int ExpectedRevision,string Decision,string Remarks,Guid[] AttachmentIds,string[] EvidenceReferences);
public sealed record ScreeningEvidence(Guid Id,Guid RequestId,string Concern,Guid ActorId,DateTimeOffset CreatedAtUtc,string FileName,string ContentType,int Bytes,string Sha256);
public sealed record ScreeningVerification(Guid Id,Guid RequestId,string Concern,int Revision,string PreviousStatus,string Status,Guid ActorId,DateTimeOffset CreatedAtUtc,string Remarks,Guid[] AttachmentIds,string[] EvidenceReferences,string EventSha256);
public sealed record ManualEvidenceView(bool CanVerify,IReadOnlyList<ScreeningEvidence> Attachments,IReadOnlyList<ScreeningVerification> History);
public sealed record PreviousScreeningEvidence(Guid RequestId,Guid AssessmentId,DateTimeOffset CreatedAtUtc,ManualEvidenceView Evidence);
public sealed record PreviousScreeningEvidencePage(IReadOnlyList<PreviousScreeningEvidence> Items,string? NextCursor);
public interface IManualEvidenceStore {
 Task<PreviousScreeningEvidencePage> PreviousAsync(Guid request,int limit,string? cursor,CancellationToken ct);
 Task<ManualEvidenceView> ReadAsync(Guid request,CancellationToken ct);
 Task<ScreeningEvidence> AttachAsync(Guid request,AttachScreeningEvidence input,CancellationToken ct);
 Task<ScreeningVerification> VerifyAsync(Guid request,VerifyScreeningEvidence input,CancellationToken ct);
 Task<(ScreeningEvidence Metadata,byte[] Content)> DownloadAsync(Guid request,Guid attachment,CancellationToken ct);
}
public static class ManualEvidenceInput {
 public const int MaxBytes=5*1024*1024;
 public static string Concern(string concern)=>Enum.GetNames<StateLandGovernance.GovernanceIntelligence.Domain.Enums.EarlyGovernanceIndicatorType>().Contains(concern)?concern:throw new ArgumentException("Unknown governance concern.");
 public static AttachScreeningEvidence Validate(AttachScreeningEvidence input){
  Concern(input.Concern);if(input.SubmissionKey==Guid.Empty||input.Content is not {Length:>0 and <=MaxBytes})throw new ArgumentException("Evidence must be between 1 byte and 5 MiB.");
  var name=(input.FileName??"").Trim();if(name.Length is <1 or >150||name.Any(c=>char.IsControl(c)||c is '/' or '\\'||c==':'))throw new ArgumentException("Use a safe evidence filename.");
  var bytes=input.Content;var valid=input.ContentType switch {
   "application/pdf"=>bytes.Length>=5&&System.Text.Encoding.ASCII.GetString(bytes,0,5)=="%PDF-",
   "image/png"=>bytes.Length>=8&&bytes.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}),
   "image/jpeg"=>bytes.Length>=3&&bytes[0]==255&&bytes[1]==216&&bytes[2]==255,
   "text/plain"=>ValidText(bytes),_=>false};
  if(!valid)throw new ArgumentException("Supported evidence: PDF, PNG, JPEG or UTF-8 plain text with matching content type. Files are stored as untrusted evidence and downloaded as attachments.");
  return input with{FileName=name};
 }
 static bool ValidText(byte[] bytes){try{return !new System.Text.UTF8Encoding(false,true).GetString(bytes).Any(c=>char.IsControl(c)&&c is not ('\r' or '\n' or '\t'));}catch(System.Text.DecoderFallbackException){return false;}}
 public static VerifyScreeningEvidence Validate(VerifyScreeningEvidence input){
  Concern(input.Concern);var remarks=(input.Remarks??"").Trim();var refs=input.EvidenceReferences??[];var ids=input.AttachmentIds??[];
  if(input.SubmissionKey==Guid.Empty||input.ExpectedRevision<0||input.Decision is not ("Verified" or "Rejected" or "MoreInformationRequired")||remarks.Length is <20 or >2000||ids.Length>10||ids.Any(x=>x==Guid.Empty)||ids.Distinct().Count()!=ids.Length||refs.Length>10||refs.Any(x=>string.IsNullOrWhiteSpace(x)||x.Length>500||x.Any(char.IsControl)))throw new ArgumentException("Record a supported decision, meaningful mandatory remarks (20–2000 characters), current revision and bounded evidence references.");
  if(input.Decision=="Verified"&&ids.Length==0&&refs.Length==0)throw new ArgumentException("Verification requires specified attachments or evidence/document references.");
  return input with{Remarks=remarks,AttachmentIds=ids.OrderBy(x=>x).ToArray(),EvidenceReferences=refs.Select(x=>x.Trim()).Distinct().OrderBy(x=>x,StringComparer.Ordinal).ToArray()};
 }
}
