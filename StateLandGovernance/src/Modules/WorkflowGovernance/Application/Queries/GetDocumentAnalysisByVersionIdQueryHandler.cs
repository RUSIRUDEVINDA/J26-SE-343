namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Exceptions;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;

public sealed class GetDocumentAnalysisByVersionIdQueryHandler : IQueryHandler<GetDocumentAnalysisByVersionIdQuery, DocumentAnalysisDto>
{
    private readonly IDocumentAnalysisRepository _repository;

    public GetDocumentAnalysisByVersionIdQueryHandler(IDocumentAnalysisRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<DocumentAnalysisDto> HandleAsync(
        GetDocumentAnalysisByVersionIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var analysis = await _repository.GetByDocumentVersionIdAsync(
            new DocumentVersionId(query.DocumentVersionId),
            cancellationToken);

        if (analysis == null)
        {
            throw DocumentAnalysisNotFoundException.ForVersion(query.DocumentVersionId);
        }

        return DocumentAnalysisMapper.ToDto(analysis);
    }
}
