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

public sealed class VerifyCandidateFactCommandHandler : ICommandHandler<VerifyCandidateFactCommand, HumanFactVerificationDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly VerifyCandidateFactCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public VerifyCandidateFactCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        VerifyCandidateFactCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<HumanFactVerificationDto> HandleAsync(
        VerifyCandidateFactCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        // 1. Boundary validation
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // 2. Load aggregate
        var analysis = await _analysisRepository.GetByIdAsync(
            new DocumentAnalysisId(command.DocumentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(command.DocumentAnalysisId);
        }

        // 3. Concurrency check
        if (analysis.Revision != command.ExpectedAnalysisRevision)
        {
            throw new AnalysisConcurrencyException(command.DocumentAnalysisId, command.ExpectedAnalysisRevision, analysis.Revision);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 4. Map inputs
        var decision = Enum.Parse<FactVerificationDecision>(command.Decision, ignoreCase: true);
        AnalysisFactValue? correctedValue = command.CorrectedValue != null
            ? FactVerificationMapper.ToDomain(command.CorrectedValue)
            : null;

        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var verificationId = command.VerificationId.HasValue && command.VerificationId.Value != Guid.Empty
            ? new HumanFactVerificationId(command.VerificationId.Value)
            : new HumanFactVerificationId(Guid.NewGuid());

        // 5. Execute domain behavior
        analysis.RecordFactVerification(
            verificationId,
            new AnalysisRunResultId(command.AnalysisRunResultId),
            new ExtractedFactId(command.ExtractedFactId),
            decision,
            correctedValue,
            command.Reason,
            command.VerifyingActorId,
            actionTime,
            authoritySnapshot);

        // 6. Commit atomically
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Return verification DTO
        var recorded = analysis.Verifications.First(v => v.Id.Value == verificationId.Value);
        return FactVerificationMapper.ToDto(recorded);
    }
}
