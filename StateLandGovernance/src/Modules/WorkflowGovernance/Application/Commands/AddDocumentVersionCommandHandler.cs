namespace StateLandGovernance.WorkflowGovernance.Application.Commands;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Application.Validators;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed class AddDocumentVersionCommandHandler : ICommandHandler<AddDocumentVersionCommand, GovernedDocumentDto>
{
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly AddDocumentVersionCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public AddDocumentVersionCommandHandler(
        IGovernedDocumentRepository documentRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        AddDocumentVersionCommandValidator validator,
        TimeProvider timeProvider)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GovernedDocumentDto> HandleAsync(
        AddDocumentVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        // 1. Boundary validation (including mandatory ExpectedRevision, trusted receipt, and strict SHA-256)
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // 2. Load existing aggregate
        var document = await _documentRepository.GetByIdAsync(new GovernedDocumentId(command.GovernedDocumentId), cancellationToken);
        if (document == null)
        {
            throw new GovernedDocumentNotFoundException(command.GovernedDocumentId);
        }

        // 3. Optimistic concurrency check (Application-level boundary protection against stale writes)
        if (document.Revision != command.ExpectedRevision)
        {
            throw new DocumentConcurrencyException(command.GovernedDocumentId, command.ExpectedRevision, document.Revision);
        }

        // 4. Map boundary authority context to Domain snapshot
        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 5. Invoke Domain operation
        // Domain enforces required capability ("DocumentSubmitter"), version lineage / predecessor validity,
        // duplicate version ID, and duplicate checksum invariants.
        var newVersionId = command.NewVersionId.HasValue && command.NewVersionId.Value != Guid.Empty
            ? new DocumentVersionId(command.NewVersionId.Value)
            : new DocumentVersionId(Guid.NewGuid());

        var checksum = new DocumentChecksum(DocumentContentReceipt.CanonicalAlgorithm, command.ContentReceipt.ChecksumValue.ToLowerInvariant());
        var contentReference = new DocumentContentReference(command.ContentReceipt.ContentReference);

        document.AddVersion(
            newVersionId,
            new DocumentVersionId(command.ExpectedPredecessorVersionId),
            checksum,
            contentReference,
            command.ContentReceipt.OriginalFileName,
            command.ContentReceipt.MediaType,
            command.ContentReceipt.FileSizeInBytes,
            command.ActorId,
            actionTime,
            authoritySnapshot);

        // 6. Commit once via unit of work (tracked aggregate mutation)
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Map to DTO (without exposing internal storage secrets)
        return GovernedDocumentMapper.ToDto(document);
    }
}
