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

public sealed class GetGovernedDocumentByIdQueryHandler : IQueryHandler<GetGovernedDocumentByIdQuery, GovernedDocumentDto>
{
    private readonly IGovernedDocumentRepository _documentRepository;

    public GetGovernedDocumentByIdQueryHandler(IGovernedDocumentRepository documentRepository)
    {
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
    }

    public async Task<GovernedDocumentDto> HandleAsync(
        GetGovernedDocumentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        if (query.GovernedDocumentId == Guid.Empty)
        {
            throw new ValidationException(["GovernedDocumentId cannot be empty."]);
        }

        var document = await _documentRepository.GetByIdAsync(new GovernedDocumentId(query.GovernedDocumentId), cancellationToken)
            ?? throw new GovernedDocumentNotFoundException(query.GovernedDocumentId);

        return GovernedDocumentMapper.ToDto(document);
    }
}
