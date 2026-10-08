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
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class GetProposalContentAssessmentQueryHandler : IQueryHandler<GetProposalContentAssessmentQuery, ProposalContentAssessmentDto?>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public GetProposalContentAssessmentQueryHandler(ILeaseCaseRepository leaseCaseRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<ProposalContentAssessmentDto?> HandleAsync(
        GetProposalContentAssessmentQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null)
        {
            throw new ArgumentNullException(nameof(query));
        }

        var leaseCase = await _leaseCaseRepository.GetByIdAsync(
            new LeaseCaseId(query.LeaseCaseId),
            cancellationToken);

        if (leaseCase == null)
        {
            return null;
        }

        var resultId = new ProposalContentAssessmentResultId(query.AssessmentResultId);
        var result = leaseCase.ProposalContentAssessmentHistory.FirstOrDefault(a => a.Id.Equals(resultId));

        return result != null ? ProposalContentMapper.ToDto(result) : null;
    }
}
