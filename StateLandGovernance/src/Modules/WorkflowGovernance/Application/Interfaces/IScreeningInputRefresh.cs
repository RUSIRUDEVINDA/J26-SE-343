namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;

using StateLandGovernance.WorkflowGovernance.Application.DTOs;

/// <summary>Reads committed inputs outside the dispatch request's aggregate identity map.</summary>
public interface IScreeningInputRefresh
{
    Task<ScreeningInputRevision> ReadAsync(Guid caseId, Guid snapshotId, Guid? parcelId, CancellationToken ct);
}

public sealed record ScreeningInputRevision(Guid SnapshotId, PreparedCaseScreening Prepared, IReadOnlyList<string> MissingInputs);
