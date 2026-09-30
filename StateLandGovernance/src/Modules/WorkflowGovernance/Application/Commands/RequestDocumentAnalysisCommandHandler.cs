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
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed class RequestDocumentAnalysisCommandHandler : ICommandHandler<RequestDocumentAnalysisCommand, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IGovernedDocumentRepository _documentRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly RequestDocumentAnalysisCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public RequestDocumentAnalysisCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IGovernedDocumentRepository documentRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        RequestDocumentAnalysisCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        RequestDocumentAnalysisCommand command,
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

        // 2. Load GovernedDocument
        var document = await _documentRepository.GetByIdAsync(
            new GovernedDocumentId(command.GovernedDocumentId),
            cancellationToken);

        if (document == null)
        {
            throw new GovernedDocumentNotFoundException(command.GovernedDocumentId);
        }

        // 3. Verify exact DocumentVersion exists within the document
        // Never silently default to the latest or active version; exact binding is mandatory
        var targetVersion = document.Versions.FirstOrDefault(v => v.Id.Value == command.DocumentVersionId);
        if (targetVersion == null)
        {
            throw new DocumentVersionNotFoundException(command.GovernedDocumentId, command.DocumentVersionId);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 4. Load or create DocumentAnalysis aggregate for this exact version
        var analysis = await _analysisRepository.GetByDocumentVersionIdAsync(
            targetVersion.Id,
            cancellationToken);

        if (analysis == null)
        {
            // Creating a new aggregate: if caller specified an expected revision, they expected an existing aggregate
            if (command.ExpectedAnalysisRevision.HasValue)
            {
                throw new AnalysisConcurrencyException(
                    command.DocumentAnalysisId ?? Guid.Empty,
                    command.ExpectedAnalysisRevision.Value,
                    0);
            }

            var analysisId = command.DocumentAnalysisId.HasValue && command.DocumentAnalysisId.Value != Guid.Empty
                ? new DocumentAnalysisId(command.DocumentAnalysisId.Value)
                : new DocumentAnalysisId(Guid.NewGuid());

            analysis = new DocumentAnalysis(
                id: analysisId,
                governedDocumentId: document.Id,
                documentVersionId: targetVersion.Id,
                documentChecksum: targetVersion.Checksum,
                documentVersionNumber: targetVersion.VersionNumber,
                sourceDocumentRevision: document.Revision,
                createdAt: actionTime);

            await _analysisRepository.AddAsync(analysis, cancellationToken);
        }
        else
        {
            // Re-analysis against an existing aggregate: ExpectedAnalysisRevision is mandatory
            if (!command.ExpectedAnalysisRevision.HasValue)
            {
                throw new AnalysisConcurrencyException(
                    analysis.Id.Value,
                    0,
                    analysis.Revision);
            }

            if (analysis.Revision != command.ExpectedAnalysisRevision.Value)
            {
                throw new AnalysisConcurrencyException(
                    analysis.Id.Value,
                    command.ExpectedAnalysisRevision.Value,
                    analysis.Revision);
            }
        }

        // 5. Request analysis run on the aggregate (remains in Requested state)
        var runId = command.AnalysisRunId.HasValue && command.AnalysisRunId.Value != Guid.Empty
            ? new AnalysisRunId(command.AnalysisRunId.Value)
            : new AnalysisRunId(Guid.NewGuid());

        var modelRef = new AnalysisModelReference(
            command.ModelProvider,
            command.ModelName,
            command.ModelVersion);

        var capabilities = command.RequestedCapabilities
            .Select(c => new AnalysisCapabilityCode(c))
            .ToList();

        analysis.RequestRun(runId, modelRef, capabilities, actionTime);

        // 6. Commit transaction: run is persisted in Requested state
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Return mapped DTO
        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
