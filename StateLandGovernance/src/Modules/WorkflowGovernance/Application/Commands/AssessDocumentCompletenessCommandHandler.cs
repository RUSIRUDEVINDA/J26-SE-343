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
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class AssessDocumentCompletenessCommandHandler : ICommandHandler<AssessDocumentCompletenessCommand, DocumentCompletenessAssessmentDto>
{
    private readonly IDocumentCompletenessAssessmentRepository _completenessRepository;
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IDocumentRequirementProvider _requirementProvider;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly AssessDocumentCompletenessCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public AssessDocumentCompletenessCommandHandler(
        IDocumentCompletenessAssessmentRepository completenessRepository,
        ILeaseCaseRepository leaseCaseRepository,
        IDocumentRequirementProvider requirementProvider,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        AssessDocumentCompletenessCommandValidator validator,
        TimeProvider timeProvider)
    {
        _completenessRepository = completenessRepository ?? throw new ArgumentNullException(nameof(completenessRepository));
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _requirementProvider = requirementProvider ?? throw new ArgumentNullException(nameof(requirementProvider));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentCompletenessAssessmentDto> HandleAsync(
        AssessDocumentCompletenessCommand command,
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

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        var requirements = await _requirementProvider.GetRequirementSetAsync(
            command.RequirementSetIdentifier,
            command.RequirementSetVersion,
            cancellationToken);

        if (requirements == null)
        {
            throw new DocumentRequirementSetNotFoundException(
                command.RequirementSetIdentifier,
                command.RequirementSetVersion);
        }

        var assessedDocs = command.AssessedDocuments.Select(d =>
            new AssessedDocumentBinding(
                new GovernedDocumentId(d.GovernedDocumentId),
                new DocumentVersionId(d.DocumentVersionId),
                new DocumentChecksum(d.ChecksumAlgorithm, d.ChecksumValue))
        ).ToList();

        var classifiedDocs = command.ClassifiedDocuments.Select(c =>
        {
            var binding = new AssessedDocumentBinding(
                new GovernedDocumentId(c.DocumentBinding.GovernedDocumentId),
                new DocumentVersionId(c.DocumentBinding.DocumentVersionId),
                new DocumentChecksum(c.DocumentBinding.ChecksumAlgorithm, c.DocumentBinding.ChecksumValue));

            var originalCode = !string.IsNullOrWhiteSpace(c.OriginalClassificationCode)
                ? new DocumentClassificationCode(c.OriginalClassificationCode)
                : null;

            var confidence = c.ConfidenceScore.HasValue
                ? new ConfidenceScore(c.ConfidenceScore.Value)
                : null;

            var status = Enum.Parse<DocumentClassificationStatus>(c.Status, ignoreCase: true);

            var classifiedId = c.Id != Guid.Empty
                ? new ClassifiedDocumentId(c.Id)
                : new ClassifiedDocumentId(Guid.NewGuid());

            return new ClassifiedDocument(classifiedId, binding, originalCode, confidence, status, null, null, null, c.ClassifiedAt);
        }).ToList();

        var assessmentId = command.AssessmentId.HasValue && command.AssessmentId.Value != Guid.Empty
            ? new DocumentCompletenessAssessmentId(command.AssessmentId.Value)
            : new DocumentCompletenessAssessmentId(Guid.NewGuid());

        var assessment = new DocumentCompletenessAssessment(
            assessmentId,
            leaseCase.Id,
            command.RequirementSetIdentifier,
            command.RequirementSetVersion,
            assessedDocs,
            classifiedDocs,
            requirements,
            actionTime);

        await _completenessRepository.AddAsync(assessment, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return DocumentCompletenessMapper.ToDto(assessment);
    }
}
