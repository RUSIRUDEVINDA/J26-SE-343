namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed class GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler : IQueryHandler<GetLatestDocumentCompletenessAssessmentByCaseIdQuery, DocumentCompletenessAssessmentDto?>
{
    private readonly IDocumentCompletenessAssessmentRepository _completenessRepository;

    public GetLatestDocumentCompletenessAssessmentByCaseIdQueryHandler(IDocumentCompletenessAssessmentRepository completenessRepository)
    {
        _completenessRepository = completenessRepository ?? throw new ArgumentNullException(nameof(completenessRepository));
    }

    public async Task<DocumentCompletenessAssessmentDto?> HandleAsync(
        GetLatestDocumentCompletenessAssessmentByCaseIdQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var assessments = await _completenessRepository.GetByLeaseCaseIdAsync(
            new LeaseCaseId(query.LeaseCaseId),
            cancellationToken);

        var latest = assessments.OrderByDescending(a => a.AssessedAt).FirstOrDefault();
        return latest != null ? DocumentCompletenessMapper.ToDto(latest) : null;
    }
}
