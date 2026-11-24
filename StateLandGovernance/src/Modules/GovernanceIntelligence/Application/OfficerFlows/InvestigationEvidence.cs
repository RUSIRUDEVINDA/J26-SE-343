namespace StateLandGovernance.GovernanceIntelligence.Application.OfficerFlows;

public sealed record InvestigationAttachment(Guid Id,Guid InvestigationId,string Concern,Guid ActorId,DateTimeOffset CreatedAtUtc,string FileName,string ContentType,int Bytes,string Sha256);
public sealed record FinalizeInvestigation(Guid SubmissionKey,string ExpectedInputSha256);
public sealed record InvestigationFinalization(Guid Id,Guid InvestigationId,Guid ActorId,DateTimeOffset CreatedAtUtc,Guid SubmissionKey,string InputSha256,string EventSha256);
public sealed record InvestigationEvidenceView(bool CanWrite,IReadOnlyList<InvestigationAttachment> Attachments,InvestigationFinalization? Finalization);
public interface IInvestigationEvidenceStore {
 Task<InvestigationEvidenceView> ReadAsync(Guid investigation,CancellationToken ct);
 Task<InvestigationAttachment> AttachAsync(Guid investigation,AttachScreeningEvidence input,CancellationToken ct);
 Task<(InvestigationAttachment Metadata,byte[] Content)> DownloadAsync(Guid investigation,Guid attachment,CancellationToken ct);
 Task<InvestigationFinalization> FinalizeAsync(Guid investigation,FinalizeInvestigation input,CancellationToken ct);
 Task RequireFinalizedAsync(Guid actor,Guid investigation,CancellationToken ct);
}
public static class InvestigationFinalizationInput {
 public static void Validate(Investigation investigation,IReadOnlyCollection<string> savedEvidenceConcerns){
  if(investigation.Findings.Count!=7||investigation.Findings.Select(x=>x.Concern).Distinct().Count()!=7)throw new ArgumentException("Seven explicit findings are required.");
  foreach(var finding in investigation.Findings){ManualEvidenceInput.Concern(finding.Concern);if(finding.Choice is not ("IssueFound" or "NoIssueFound"))throw new ArgumentException("Select Issue Found or No Issue Found for every concern before finalizing.");
   if(finding.Choice=="IssueFound"&&(!savedEvidenceConcerns.Contains(finding.Concern)||finding.Remarks.Trim().Length<20||finding.Remarks.Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).Length<3))throw new ArgumentException("Every Issue Found concern requires meaningful remarks and persisted evidence for this investigation.");
  }
 }
}
