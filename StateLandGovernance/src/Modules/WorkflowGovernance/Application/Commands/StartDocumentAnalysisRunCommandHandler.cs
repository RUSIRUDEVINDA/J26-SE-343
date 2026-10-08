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

public sealed class StartDocumentAnalysisRunCommandHandler : ICommandHandler<StartDocumentAnalysisRunCommand, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly StartDocumentAnalysisRunCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public StartDocumentAnalysisRunCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        StartDocumentAnalysisRunCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        StartDocumentAnalysisRunCommand command,
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

        // 4. Start run on aggregate (transitions run from Requested to Running)
        analysis.StartRun(
            new AnalysisRunId(command.AnalysisRunId),
            actionTime);

        // 5. Commit transaction immediately
        await _unitOfWork.CommitAsync(cancellationToken);

        // 6. Return mapped DTO
        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
