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

public sealed class CreateVerifiedFactSnapshotCommandHandler : ICommandHandler<CreateVerifiedFactSnapshotCommand, VerifiedFactSnapshotDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly CreateVerifiedFactSnapshotCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CreateVerifiedFactSnapshotCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        CreateVerifiedFactSnapshotCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<VerifiedFactSnapshotDto> HandleAsync(
        CreateVerifiedFactSnapshotCommand command,
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

        var analysis = await _analysisRepository.GetByIdAsync(
            new DocumentAnalysisId(command.DocumentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(command.DocumentAnalysisId);
        }

        if (analysis.Revision != command.ExpectedAnalysisRevision)
        {
            throw new AnalysisConcurrencyException(command.DocumentAnalysisId, command.ExpectedAnalysisRevision, analysis.Revision);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;
        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var snapshotId = command.SnapshotId.HasValue && command.SnapshotId.Value != Guid.Empty
            ? new VerifiedFactSnapshotId(command.SnapshotId.Value)
            : new VerifiedFactSnapshotId(Guid.NewGuid());

        // Domain method is PublishVerifiedFactSnapshot
        analysis.PublishVerifiedFactSnapshot(
            snapshotId,
            new AnalysisRunResultId(command.AnalysisRunResultId),
            command.PublishingActorId,
            actionTime,
            authoritySnapshot);

        await _unitOfWork.CommitAsync(cancellationToken);

        var created = analysis.VerifiedFactSnapshots.First(s => s.Id.Value == snapshotId.Value);
        return FactVerificationMapper.ToDto(created);
    }
}
