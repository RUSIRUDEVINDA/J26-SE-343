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

public sealed class FailDocumentAnalysisCommandHandler : ICommandHandler<FailDocumentAnalysisCommand, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly FailDocumentAnalysisCommandValidator _validator;
    private readonly TimeProvider _timeProvider;

    public FailDocumentAnalysisCommandHandler(
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        FailDocumentAnalysisCommandValidator validator,
        TimeProvider timeProvider)
    {
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        FailDocumentAnalysisCommand command,
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

        // 4. Construct sanitized failure
        var failure = new AnalysisRunFailure(command.FailureCode, command.SafeDescription);

        // 5. Fail run on aggregate
        analysis.FailRun(
            new AnalysisRunId(command.AnalysisRunId),
            failure,
            actionTime);

        // 6. Commit once via unit of work
        await _unitOfWork.CommitAsync(cancellationToken);

        // 7. Return mapped DTO
        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
