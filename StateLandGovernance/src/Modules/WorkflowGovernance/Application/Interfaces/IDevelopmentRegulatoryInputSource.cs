namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;
public sealed record DevelopmentRegulatoryInput(string Version,string SourceReference,string InputJson);
public interface IDevelopmentRegulatoryInputSource
{
 Task<DevelopmentRegulatoryInput?> ReadAsync(Guid caseId,Guid snapshotId,Guid proposalVersion,CancellationToken ct);
}
