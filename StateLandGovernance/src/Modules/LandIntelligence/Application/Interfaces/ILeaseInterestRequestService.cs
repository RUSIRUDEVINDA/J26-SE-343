using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestRequestService
{
    LeaseInterestDocumentChecklistDto GetDocumentChecklist();

    Task<LeaseInterestRequestDetailDto> GetDraftOrCreateAsync(
        Guid landParcelId,
        CancellationToken cancellationToken);

    Task<LeaseInterestRequestDetailDto> SaveDraftAsync(
        Guid requestId,
        UpsertLeaseInterestDraftDto draft,
        CancellationToken cancellationToken);

    Task<LeaseInterestRequestDetailDto> SubmitAsync(
        Guid requestId,
        UpsertLeaseInterestDraftDto draft,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<LeaseInterestRequestSummaryDto>> ListMineAsync(
        CancellationToken cancellationToken);

    Task<LeaseInterestRequestDetailDto> GetByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken);
}
