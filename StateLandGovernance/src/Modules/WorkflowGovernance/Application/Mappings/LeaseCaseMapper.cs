namespace StateLandGovernance.WorkflowGovernance.Application.Mappings;

using System;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public static class LeaseCaseMapper
{
    public static LeaseCaseDto ToDto(LeaseCase leaseCase)
    {
        if (leaseCase == null)
        {
            throw new ArgumentNullException(nameof(leaseCase));
        }

        ProposalIntakeSummaryDto? intakeSummary = null;
        if (leaseCase.ProposalIntake != null)
        {
            var intake = leaseCase.ProposalIntake;
            intakeSummary = new ProposalIntakeSummaryDto(
                Purpose: intake.Purpose?.Value,
                RequestedExtent: intake.RequestedExtent?.Value,
                RequestedExtentUnit: intake.RequestedExtent?.Unit.Name,
                JurisdictionCode: intake.Jurisdiction?.Code,
                SourceReference: intake.SourceReference.Reference,
                SourceVersion: intake.SourceReference.Version
            );
        }

        return new LeaseCaseDto(
            Id: leaseCase.Id.Value,
            ApplicationReference: leaseCase.ApplicationReference,
            CreatedByActorId: leaseCase.CreatedByActorId,
            CreatedAt: leaseCase.CreatedAt,
            Status: leaseCase.Status.ToString(),
            Revision: leaseCase.Revision,
            CurrentVerifiedFactSnapshotId: leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty
                ? null
                : leaseCase.CurrentVerifiedFactSnapshotId,
            ProposalIntake: intakeSummary
        );
    }
}
