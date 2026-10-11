namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;

public static class ScreeningMapper
{
    public static ScreeningResultDto ToDto(ScreeningResult result, Guid currentVerifiedFactSnapshotId)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        bool isStale = result.IsStale(currentVerifiedFactSnapshotId);

        return new ScreeningResultDto(
            ScreeningResultId: result.Id,
            LeaseCaseId: result.LeaseCaseId.Value,
            BoundVerifiedFactSnapshotId: result.VerifiedFactSnapshotId,
            CurrentVerifiedFactSnapshotId: currentVerifiedFactSnapshotId,
            Outcome: result.Outcome.ToString(),
            Remarks: result.Remarks,
            AssessedAtUtc: result.AssessedAtUtc,
            IsStale: isStale);
    }
}
