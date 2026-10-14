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
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class RegisterGovernedDocumentCommandHandler : ICommandHandler<RegisterGovernedDocumentCommand, GovernedDocumentDto>
{
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RegisterGovernedDocumentCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public RegisterGovernedDocumentCommandHandler(
        IGovernedDocumentRepository documentRepository,
        ILeaseCaseRepository leaseCaseRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        RegisterGovernedDocumentCommandValidator validator,
        TimeProvider timeProvider)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<GovernedDocumentDto> HandleAsync(
        RegisterGovernedDocumentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        // 1. Boundary validation (including trusted ingestion receipt and SHA-256 validation)
        var validation = _validator.Validate(command);
        if (!validation.IsValid)
        {
            throw new ValidationException(validation.Errors);
        }

        // 2. Verify referenced LeaseCase exists (documents cannot be orphaned)
        var leaseCase = await _leaseCaseRepository.GetByIdAsync(new LeaseCaseId(command.LeaseCaseId), cancellationToken);
        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(command.LeaseCaseId);
        }

        // 3. Map boundary authority context to Domain snapshot
        var authoritySnapshot = command.AuthorityContext.ToDomain();
        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 4. Construct GovernedDocument aggregate
        // The aggregate constructor creates the document and its initial DocumentVersion atomically.
        // Domain constructor strictly validates required capability ("DocumentSubmitter"), scope, and validity window.
        var documentId = command.GovernedDocumentId.HasValue && command.GovernedDocumentId.Value != Guid.Empty
            ? new GovernedDocumentId(command.GovernedDocumentId.Value)
            : new GovernedDocumentId(Guid.NewGuid());

        var initialVersionId = command.InitialVersionId.HasValue && command.InitialVersionId.Value != Guid.Empty
            ? new DocumentVersionId(command.InitialVersionId.Value)
            : new DocumentVersionId(Guid.NewGuid());

        var checksum = new DocumentChecksum(DocumentContentReceipt.CanonicalAlgorithm, command.ContentReceipt.ChecksumValue.ToLowerInvariant());
        var contentReference = new DocumentContentReference(command.ContentReceipt.ContentReference);

        var document = new GovernedDocument(
            documentId,
            new LeaseCaseId(command.LeaseCaseId),
            command.LogicalCategory,
            initialVersionId,
            checksum,
            contentReference,
            command.ContentReceipt.OriginalFileName,
            command.ContentReceipt.MediaType,
            command.ContentReceipt.FileSizeInBytes,
            command.ActorId,
            actionTime,
            authoritySnapshot);

        // 5. Add aggregate to repository
        await _documentRepository.AddAsync(document, cancellationToken);

        // 6. Commit once via unit of work (ensures document and initial version persist atomically)
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Map to DTO (without exposing internal storage secrets)
        return GovernedDocumentMapper.ToDto(document);
    }
}
