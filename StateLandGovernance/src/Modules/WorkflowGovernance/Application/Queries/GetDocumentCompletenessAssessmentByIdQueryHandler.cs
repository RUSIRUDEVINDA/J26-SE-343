namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.DocumentCompleteness;

public sealed class GetDocumentCompletenessAssessmentByIdQueryHandler : IQueryHandler<GetDocumentCompletenessAssessmentByIdQuery, DocumentCompletenessAssessmentDto?>
{
    private readonly IDocumentCompletenessAssessmentRepository _completenessRepository;

    public GetDocumentCompletenessAssessmentByIdQueryHandler(IDocumentCompletenessAssessmentRepository completenessRepository)
    {
        _completenessRepository = completenessRepository ?? throw new ArgumentNullException(nameof(completenessRepository));
    }

    public async Task<DocumentCompletenessAssessmentDto?> HandleAsync(
        GetDocumentCompletenessAssessmentByIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var assessment = await _completenessRepository.GetByIdAsync(
            new DocumentCompletenessAssessmentId(query.AssessmentId),
            cancellationToken);

        return assessment != null ? DocumentCompletenessMapper.ToDto(assessment) : null;
    }
}
