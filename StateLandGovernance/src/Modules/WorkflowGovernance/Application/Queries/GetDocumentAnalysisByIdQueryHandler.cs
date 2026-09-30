namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

public sealed class GetDocumentAnalysisByIdQueryHandler : IQueryHandler<GetDocumentAnalysisByIdQuery, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _repository;

    public GetDocumentAnalysisByIdQueryHandler(IDocumentAnalysisRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        GetDocumentAnalysisByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var analysis = await _repository.GetByIdAsync(
            new DocumentAnalysisId(query.DocumentAnalysisId),
            cancellationToken);

        if (analysis == null)
        {
            throw new DocumentAnalysisNotFoundException(query.DocumentAnalysisId);
        }

        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
