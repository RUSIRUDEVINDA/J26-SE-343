namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record RegisterLeaseCaseCommand(
    string ApplicationReference,
    Guid ActorId,
    VerifiedAuthorityContext AuthorityContext,
    Guid? LeaseCaseId = null,
    ProposalIntakeInput? ProposalIntake = null
) : ICommand
{
    public RegisterLeaseCaseCommand(
        string applicationReference,
        Guid actorId,
        VerifiedAuthoritySnapshot authoritySnapshot,
        Guid? leaseCaseId = null,
        LeaseProposalIntake? proposalIntake = null)
        : this(
            applicationReference,
            actorId,
            authoritySnapshot != null ? VerifiedAuthorityContext.FromDomain(authoritySnapshot) : null!,
            leaseCaseId,
            proposalIntake != null ? ProposalIntakeInput.FromDomain(proposalIntake) : null)
    {
    }
}
