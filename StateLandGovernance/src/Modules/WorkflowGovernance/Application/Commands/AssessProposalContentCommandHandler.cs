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
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class AssessProposalContentCommandHandler : ICommandHandler<AssessProposalContentCommand, ProposalContentAssessmentDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IProposalTemplateProvider _templateProvider;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly AssessProposalContentCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public AssessProposalContentCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IProposalTemplateProvider templateProvider,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        AssessProposalContentCommandValidator validator,
        TimeProvider timeProvider)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _templateProvider = templateProvider ?? throw new ArgumentNullException(nameof(templateProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ProposalContentAssessmentDto> HandleAsync(
        AssessProposalContentCommand command,
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

        var templateId = new ProposalTemplateId(command.TemplateId);
        var template = await _templateProvider.GetTemplateAsync(templateId, command.TemplateVersion, cancellationToken);
        if (template == null)
        {
            throw new ProposalTemplateNotFoundException(command.TemplateId, command.TemplateVersion);
        }

        var sourceBinding = new ProposalSourceBinding(
            leaseCase.Id,
            new GovernedDocumentId(command.ProposalDocumentId),
            new DocumentVersionId(command.DocumentVersionId),
            new DocumentChecksum(command.ChecksumAlgorithm, command.ChecksumValue),
            templateId,
            command.TemplateVersion,
            actionTime,
            command.ExtractionReference);

        var observations = command.Observations.Select(obs =>
        {
            var reqId = new ProposalRequirementId(obs.RequirementId);
            var state = Enum.Parse<ProposalObservationState>(obs.State, ignoreCase: true);

            return new ProposalRequirementObservation(
                reqId,
                state,
                sourceBinding,
                obs.PageNumber,
                obs.TextSpan,
                obs.EvidenceReference,
                obs.ExtractionReference,
                obs.Explanation,
                isMachineGenerated: true);
        }).ToList();

        var evaluated = ProposalContentCompletenessEvaluator.Evaluate(sourceBinding, template, observations);

        leaseCase.RecordProposalContentAssessment(evaluated);

        await _unitOfWork.CommitAsync(cancellationToken);

        var stored = leaseCase.ProposalContentAssessmentHistory.Last(a => a.Id.Equals(evaluated.Id));
        return ProposalContentMapper.ToDto(stored);
    }
}
