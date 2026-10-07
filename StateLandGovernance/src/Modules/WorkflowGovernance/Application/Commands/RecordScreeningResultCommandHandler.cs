namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.Screening;

public sealed class RecordScreeningResultCommandHandler : ICommandHandler<RecordScreeningResultCommand, ScreeningResultDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RecordScreeningResultCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public RecordScreeningResultCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IDocumentAnalysisRepository analysisRepository,
        IGovernedDocumentRepository documentRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        RecordScreeningResultCommandValidator validator,
        TimeProvider timeProvider)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ScreeningResultDto> HandleAsync(
        RecordScreeningResultCommand command,
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

        // 2. Load LeaseCase aggregate
        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(command.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(command.LeaseCaseId);
        }

        // 3. Optimistic concurrency check
        if (leaseCase.Revision != command.ExpectedLeaseCaseRevision)
        {
            throw new LeaseCaseConcurrencyException(
                command.LeaseCaseId,
                command.ExpectedLeaseCaseRevision,
                leaseCase.Revision);
        }

        // 4. Require an active Pending screening request on the case
        if (leaseCase.LatestScreening == null || leaseCase.LatestScreening.Outcome != ScreeningOutcome.Pending)
        {
            throw new NoPendingScreeningRequestException(command.LeaseCaseId);
        }

        // 5. Correlate with active pending request ID
        if (leaseCase.LatestScreening.Id != command.ScreeningRequestId)
        {
            throw new ScreeningRequestMismatchException(
                command.LeaseCaseId,
                command.ScreeningRequestId,
                leaseCase.LatestScreening.Id);
        }

        // 6. Correlate snapshot ID with pending screening and verify snapshot is still current
        if (leaseCase.LatestScreening.VerifiedFactSnapshotId != command.VerifiedFactSnapshotId ||
            leaseCase.CurrentVerifiedFactSnapshotId != command.VerifiedFactSnapshotId)
        {
            throw new ScreeningSnapshotMismatchException(
                command.LeaseCaseId,
                command.VerifiedFactSnapshotId,
                leaseCase.LatestScreening.VerifiedFactSnapshotId,
                leaseCase.CurrentVerifiedFactSnapshotId);
        }

        // 7. Verify snapshot exists in repository
        var snapshot = await _analysisRepository.GetSnapshotByIdAsync(
            new VerifiedFactSnapshotId(command.VerifiedFactSnapshotId),
            cancellationToken);

        if (snapshot == null)
        {
            throw new VerifiedFactSnapshotNotFoundException(command.VerifiedFactSnapshotId);
        }

        // 8. Cross-case snapshot ownership verification:
        // VerifiedFactSnapshot -> DocumentAnalysis -> GovernedDocument -> LeaseCaseId == leaseCase.Id
        var analysis = await _analysisRepository.GetByIdAsync(
            snapshot.DocumentAnalysisId,
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(snapshot.DocumentAnalysisId.Value);
        }

        var document = await _documentRepository.GetByIdAsync(
            analysis.GovernedDocumentId,
            cancellationToken);

        if (document == null)
        {
            throw new GovernedDocumentNotFoundException(analysis.GovernedDocumentId.Value);
        }

        if (document.LeaseCaseId != leaseCase.Id)
        {
            throw new ScreeningSnapshotCaseMismatchException(
                leaseCase.Id.Value,
                snapshot.Id.Value,
                document.LeaseCaseId.Value);
        }

        var assessedAt = command.AssessedAtUtc ?? _timeProvider.GetUtcNow().UtcDateTime;

        // 9. Parse validated outcome
        var outcome = Enum.Parse<ScreeningOutcome>(command.Outcome, ignoreCase: true);

        // 10. Construct screening result bound to the exact snapshot ID assessed by Component 4
        var screeningResultId = command.ScreeningResultId.HasValue && command.ScreeningResultId.Value != Guid.Empty
            ? command.ScreeningResultId.Value
            : Guid.NewGuid();

        var screeningResult = new ScreeningResult(
            Id: screeningResultId,
            LeaseCaseId: leaseCase.Id,
            VerifiedFactSnapshotId: command.VerifiedFactSnapshotId,
            Outcome: outcome,
            Remarks: command.Remarks,
            AssessedAtUtc: assessedAt);

        // 11. Record screening result on LeaseCase aggregate
        leaseCase.RecordScreeningResult(screeningResult);

        // 12. Commit transaction
        await _unitOfWork.CommitAsync(cancellationToken);

        // 13. Return mapped DTO reflecting factual status
        return ScreeningMapper.ToDto(screeningResult, leaseCase.CurrentVerifiedFactSnapshotId);
    }
}
