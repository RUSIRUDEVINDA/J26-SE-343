namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class CorrectProposalContentAssessmentCommandHandler : ICommandHandler<CorrectProposalContentAssessmentCommand, ProposalContentAssessmentDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IProposalTemplateProvider _templateProvider;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly CorrectProposalContentAssessmentCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CorrectProposalContentAssessmentCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IProposalTemplateProvider templateProvider,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        CorrectProposalContentAssessmentCommandValidator validator,
        TimeProvider timeProvider)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ProposalContentAssessmentDto> HandleAsync(
        CorrectProposalContentAssessmentCommand command,
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

        var resultId = new ProposalContentAssessmentResultId(command.AssessmentResultId);
        var original = leaseCase.ProposalContentAssessmentHistory.FirstOrDefault(a => a.Id.Equals(resultId));
        if (original == null)
        {
            throw new ProposalContentAssessmentNotFoundException(command.AssessmentResultId);
        }

        var template = await _templateProvider.GetTemplateAsync(
            original.SourceBinding.TemplateId,
            original.SourceBinding.TemplateVersion,
            cancellationToken);

        if (template == null)
        {
            throw new ProposalTemplateNotFoundException(
                original.SourceBinding.TemplateId.Value,
                original.SourceBinding.TemplateVersion);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;
        var authoritySnapshot = command.AuthorityContext.ToDomain();

        var mappedObservations = command.CorrectedObservations.Select(obs =>
        {
            var reqId = new ProposalRequirementId(obs.RequirementId);
            var state = Enum.Parse<ProposalObservationState>(obs.State, ignoreCase: true);

            return new ProposalRequirementObservation(
                reqId,
                state,
                original.SourceBinding,
                obs.PageNumber,
                obs.TextSpan,
                obs.EvidenceReference,
                obs.ExtractionReference,
                obs.Explanation,
                isMachineGenerated: false);
        }).ToList();

        var corrected = leaseCase.CorrectProposalContentAssessment(
            resultId,
            authoritySnapshot,
            command.ActorId,
            actionTime,
            command.Reason,
            mappedObservations,
            command.EvidenceReference,
            template);

        await _unitOfWork.CommitAsync(cancellationToken);

        return ProposalContentMapper.ToDto(corrected);
    }
}
