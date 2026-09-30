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

public sealed class CompleteDocumentAnalysisCommandHandler : ICommandHandler<CompleteDocumentAnalysisCommand, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly CompleteDocumentAnalysisCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public CompleteDocumentAnalysisCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        CompleteDocumentAnalysisCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        CompleteDocumentAnalysisCommand command,
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

        // 2. Load DocumentAnalysis aggregate
        var analysis = await _analysisRepository.GetByIdAsync(
            new DocumentAnalysisId(command.DocumentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(command.DocumentAnalysisId);
        }

        // 3. Optimistic concurrency check (Application-level boundary protection)
        if (analysis.Revision != command.ExpectedRevision)
        {
            throw new AnalysisConcurrencyException(command.DocumentAnalysisId, command.ExpectedRevision, analysis.Revision);
        }

        var actionTime = _timeProvider.GetUtcNow().UtcDateTime;

        // 4. Map outcome
        var outcome = Enum.Parse<AnalysisResultOutcome>(command.Outcome, ignoreCase: true);

        // 5. Map artifacts
        var artifacts = command.Artifacts.Select(a =>
        {
            var artId = a.ArtifactId.HasValue && a.ArtifactId.Value != Guid.Empty
                ? new AnalysisResultArtifactId(a.ArtifactId.Value)
                : new AnalysisResultArtifactId(Guid.NewGuid());
            var checksum = new AnalysisArtifactChecksum(a.ChecksumAlgorithm, a.ChecksumValue);
            return new AnalysisResultArtifactReference(artId, a.ArtifactKind, a.StorageReference, a.ContentType, checksum);
        }).ToList();

        // 6. Map candidate facts (machine output, strictly unverified)
        var facts = command.ExtractedCandidateFacts.Select(f =>
        {
            var factId = f.FactId.HasValue && f.FactId.Value != Guid.Empty
                ? new ExtractedFactId(f.FactId.Value)
                : new ExtractedFactId(Guid.NewGuid());
            var factCode = new FactCode(f.FactCode);
            var kind = Enum.Parse<AnalysisFactValueKind>(f.ValueKind, ignoreCase: true);
            var factValue = new AnalysisFactValue(kind, f.CanonicalValue);
            ConfidenceScore? confidence = f.ConfidenceScore.HasValue ? new ConfidenceScore(f.ConfidenceScore.Value) : null;
            AnalysisEvidenceReference? evidence = null;
            if (f.EvidenceArtifactId.HasValue && f.EvidenceArtifactId.Value != Guid.Empty)
            {
                evidence = new AnalysisEvidenceReference(
                    new AnalysisResultArtifactId(f.EvidenceArtifactId.Value),
                    f.PageNumber,
                    f.Excerpt);
            }
            return new ExtractedFactInput(factId, factCode, factValue, confidence, evidence);
        }).ToList();

        var resultId = command.ResultId.HasValue && command.ResultId.Value != Guid.Empty
            ? new AnalysisRunResultId(command.ResultId.Value)
            : new AnalysisRunResultId(Guid.NewGuid());

        // 7. Complete run on aggregate
        analysis.CompleteRun(
            new AnalysisRunId(command.AnalysisRunId),
            resultId,
            outcome,
            artifacts,
            facts,
            actionTime);

        // 8. Commit once via unit of work
        await _unitOfWork.CommitAsync(cancellationToken);

        // 9. Return mapped DTO
        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
