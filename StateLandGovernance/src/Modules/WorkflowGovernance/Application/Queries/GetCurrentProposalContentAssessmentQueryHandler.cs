namespace StateLandGovernance.WorkflowGovernance.Application.Queries;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.BuildingBlocks.CQRS;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.Mappings;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public sealed class GetCurrentProposalContentAssessmentQueryHandler : IQueryHandler<GetCurrentProposalContentAssessmentQuery, ProposalContentAssessmentDto?>
{
    private readonly ILeaseCaseRepository _leaseCaseRepository;

    public GetCurrentProposalContentAssessmentQueryHandler(ILeaseCaseRepository leaseCaseRepository)
    {
        _leaseCaseRepository = leaseCaseRepository ?? throw new ArgumentNullException(nameof(leaseCaseRepository));
    }

    public async Task<ProposalContentAssessmentDto?> HandleAsync(
        GetCurrentProposalContentAssessmentQuery query,
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

        if (query.CurrentSourceBinding == null)
        {
            var latestMatching = leaseCase.ProposalContentAssessmentHistory
                .Where(a => string.Equals(a.TemplateSnapshot.DefinitionDigest, query.TemplateDefinitionDigest.Trim(), StringComparison.Ordinal))
                .OrderByDescending(a => a.SequenceNumber)
                .FirstOrDefault();

            if (latestMatching == null)
            {
                return null;
            }

            var fallback = leaseCase.GetCurrentProposalContentAssessment(latestMatching.SourceBinding, query.TemplateDefinitionDigest);
            return fallback != null ? ProposalContentMapper.ToDto(fallback) : null;
        }

        var sourceBinding = new ProposalSourceBinding(
            leaseCase.Id,
            new GovernedDocumentId(query.CurrentSourceBinding.ProposalDocumentId),
            new DocumentVersionId(query.CurrentSourceBinding.DocumentVersionId),
            new DocumentChecksum(query.CurrentSourceBinding.ChecksumAlgorithm, query.CurrentSourceBinding.ChecksumValue),
            new ProposalTemplateId(query.CurrentSourceBinding.TemplateId),
            query.CurrentSourceBinding.TemplateVersion,
            query.CurrentSourceBinding.AssessedAt,
            query.CurrentSourceBinding.ExtractionReference);

        var current = leaseCase.GetCurrentProposalContentAssessment(sourceBinding, query.TemplateDefinitionDigest);
        return current != null ? ProposalContentMapper.ToDto(current) : null;
    }
}
