namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class ConfirmProposalContentAssessmentCommandHandler : ICommandHandler<ConfirmProposalContentAssessmentCommand, ProposalContentAssessmentDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly ConfirmProposalContentAssessmentCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public ConfirmProposalContentAssessmentCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        ConfirmProposalContentAssessmentCommandValidator validator,
        TimeProvider timeProvider)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ProposalContentAssessmentDto> HandleAsync(
        ConfirmProposalContentAssessmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(command.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(command.LeaseCaseId);
        }

        if (leaseCase.Revision != command.ExpectedRevision)
        {
            throw new LeaseCaseConcurrencyException(command.LeaseCaseId, command.ExpectedRevision, leaseCase.Revision);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;
        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var resultId = new ProposalContentAssessmentResultId(command.AssessmentResultId);

        var confirmed = leaseCase.ConfirmProposalContentAssessment(
            resultId,
            authoritySnapshot,
            command.ActorId,
            actionTime,
            command.Notes);

        await _unitOfWork.CommitAsync(cancellationToken);

        return ProposalContentMapper.ToDto(confirmed);
    }
}
