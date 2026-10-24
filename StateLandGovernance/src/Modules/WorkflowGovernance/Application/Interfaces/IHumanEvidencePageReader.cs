namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

public interface IHumanEvidencePageReader
{
    Task<string?> ReadAsync(Guid documentVersionId, Guid runId, int pageNumber, CancellationToken ct);
}
