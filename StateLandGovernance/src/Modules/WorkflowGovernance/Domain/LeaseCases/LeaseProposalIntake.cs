namespace StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

public sealed class LeaseProposalIntake
{
    public LeasePurpose? Purpose { get; }
    public LandExtent? RequestedExtent { get; }
    public JurisdictionContext? Jurisdiction { get; }
    public IntakeSourceReference SourceReference { get; }

    public LeaseProposalIntake(
        LeasePurpose? purpose,
        LandExtent? requestedExtent,
        JurisdictionContext? jurisdiction,
        IntakeSourceReference sourceReference)
    {
        if (string.IsNullOrWhiteSpace(sourceReference.Reference) || string.IsNullOrWhiteSpace(sourceReference.Version))
        {
            throw new InvalidProposalIntakeException("Intake source reference is mandatory.");
        }

        Purpose = purpose;
        RequestedExtent = requestedExtent;
        Jurisdiction = jurisdiction;
        SourceReference = sourceReference;
    }
}
