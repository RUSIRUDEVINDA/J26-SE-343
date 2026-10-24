namespace StateLandGovernance.WorkflowGovernance.Application.Interfaces;
using StateLandGovernance.WorkflowGovernance.Application.DTOs;
using StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

public interface IProposalTemplateMappingService
{
    Task<IReadOnlyList<ProposalObservationDto>> MapAsync(DocumentAnalysisDto analysis,
        ProposalTemplate template, CancellationToken ct);
    Task<IReadOnlyList<ProposalObservationDto>?> ReadCachedAsync(DocumentAnalysisDto analysis,
        ProposalTemplate template, CancellationToken ct);
}
