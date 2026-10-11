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
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class LinkVerifiedFactSnapshotToCaseCommandHandler : ICommandHandler<LinkVerifiedFactSnapshotToCaseCommand, LeaseCaseDto>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;
    private readonly IDocumentAnalysisRepository _analysisRepository;
    private readonly IWorkflowGovernanceUnitOfWork _unitOfWork;
    private readonly LinkVerifiedFactSnapshotToCaseCommandValidator _validator;

    public LinkVerifiedFactSnapshotToCaseCommandHandler(
        ILeaseCaseRepository leaseCaseRepository,
        IDocumentAnalysisRepository analysisRepository,
        IWorkflowGovernanceUnitOfWork unitOfWork,
        LinkVerifiedFactSnapshotToCaseCommandValidator validator)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
        _analysisRepository = analysisRepository ?? throw new ArgumentNullException(nameof(analysisRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<LeaseCaseDto> HandleAsync(
        LinkVerifiedFactSnapshotToCaseCommand command,
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

        var snapshot = analysis.VerifiedFactSnapshots.FirstOrDefault(s => s.Id.Value == command.SnapshotId);
        if (snapshot == null)
        {
            throw new VerifiedFactSnapshotNotFoundException(command.SnapshotId);
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(command.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null)
        {
            throw new LeaseCaseNotFoundException(command.LeaseCaseId);
        }

        if (leaseCase.Revision != command.ExpectedLeaseCaseRevision)
        {
            throw new LeaseCaseConcurrencyException(command.LeaseCaseId, command.ExpectedLeaseCaseRevision, leaseCase.Revision);
        }

        leaseCase.UpdateCurrentVerifiedFactSnapshot(command.SnapshotId);

        await _unitOfWork.CommitAsync(cancellationToken);

        return LeaseCaseMapper.ToDto(leaseCase);
    }
}
