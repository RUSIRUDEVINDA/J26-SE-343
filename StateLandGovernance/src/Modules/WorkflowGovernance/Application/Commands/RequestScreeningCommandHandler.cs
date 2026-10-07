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

public sealed class RequestScreeningCommandHandler : ICommandHandler<RequestScreeningCommand, ScreeningResultDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RequestScreeningCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public RequestScreeningCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IDocumentAnalysisRepository analysisRepository,
        IGovernedDocumentRepository documentRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        RequestScreeningCommandValidator validator,
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
        RequestScreeningCommand command,
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

        // 4. Enforce that case has an authoritative CurrentVerifiedFactSnapshot linked
        if (leaseCase.CurrentVerifiedFactSnapshotId == Guid.Empty)
        {
            throw new MissingCurrentVerifiedFactSnapshotException(command.LeaseCaseId);
        }

        // 5. Verify authoritative snapshot exists in repository
        var snapshot = await _analysisRepository.GetSnapshotByIdAsync(
            new VerifiedFactSnapshotId(leaseCase.CurrentVerifiedFactSnapshotId),
            cancellationToken);

        if (snapshot == null)
        {
            throw new VerifiedFactSnapshotNotFoundException(leaseCase.CurrentVerifiedFactSnapshotId);
        }

        // 6. Cross-case snapshot ownership verification:
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

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 7. Record Pending screening result bound to exact current verified snapshot
        var screeningRequestId = command.ScreeningRequestId.HasValue && command.ScreeningRequestId.Value != Guid.Empty
            ? command.ScreeningRequestId.Value
            : Guid.NewGuid();

        var pendingScreening = new ScreeningResult(
            Id: screeningRequestId,
            LeaseCaseId: leaseCase.Id,
            VerifiedFactSnapshotId: leaseCase.CurrentVerifiedFactSnapshotId,
            Outcome: ScreeningOutcome.Pending,
            Remarks: command.Remarks ?? "Screening requested and pending Component 4 assessment.",
            AssessedAtUtc: actionTime);

        leaseCase.RecordScreeningResult(pendingScreening);

        // 8. Commit unit of work: state is safely persisted as Pending before any external dispatch.
        // No synchronous network calls are performed. Background workers/outbox dispatch to Component 4 asynchronously.
        await _unitOfWork.CommitAsync(cancellationToken);

        return ScreeningMapper.ToDto(leaseCase.LatestScreening!, leaseCase.CurrentVerifiedFactSnapshotId);
    }
}
