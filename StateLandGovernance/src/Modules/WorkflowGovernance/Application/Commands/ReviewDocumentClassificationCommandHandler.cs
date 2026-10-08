namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public sealed class ReviewDocumentClassificationCommandHandler : ICommandHandler<ReviewDocumentClassificationCommand, DocumentCompletenessAssessmentDto>
{
    private readonly IDocumentCompletenessAssessmentRepository _completenessRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly ReviewDocumentClassificationCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public ReviewDocumentClassificationCommandHandler(
        IDocumentCompletenessAssessmentRepository completenessRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        ReviewDocumentClassificationCommandValidator validator,
        TimeProvider timeProvider)
    {
        _completenessRepository = completenessRepository ?? throw new ArgumentNullException(nameof(completenessRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentCompletenessAssessmentDto> HandleAsync(
        ReviewDocumentClassificationCommand command,
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

        var assessment = await _completenessRepository.GetByIdAsync(
            new DocumentCompletenessAssessmentId(command.AssessmentId),
            cancellationToken);

        if (assessment == null)
        {
            throw new DocumentCompletenessAssessmentNotFoundException(command.AssessmentId);
        }

        if (assessment.Revision != command.ExpectedRevision)
        {
            throw new DocumentCompletenessConcurrencyException(command.AssessmentId, command.ExpectedRevision, assessment.Revision);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;
        var decision = Enum.Parse<ClassificationReviewDecision>(command.Decision, ignoreCase: true);
        var correctedCode = !string.IsNullOrWhiteSpace(command.CorrectedClassificationCode)
            ? new DocumentClassificationCode(command.CorrectedClassificationCode)
            : null;

        var reviewId = command.ReviewId.HasValue && command.ReviewId.Value != Guid.Empty
            ? new ClassificationReviewId(command.ReviewId.Value)
            : new ClassificationReviewId(Guid.NewGuid());

        var authoritySnapshot = command.AuthorityContext.ToDomain();

        assessment.RecordHumanClassificationReview(
            reviewId,
            new ClassifiedDocumentId(command.ClassifiedDocumentId),
            decision,
            correctedCode,
            command.Reason,
            command.ReviewingActorId,
            actionTime,
            authoritySnapshot);

        await _unitOfWork.CommitAsync(cancellationToken);

        return DocumentCompletenessMapper.ToDto(assessment);
    }
}
