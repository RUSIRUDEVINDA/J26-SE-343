using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestRequestService
{
    LeaseInterestDocumentChecklistDto GetDocumentChecklist();

    /// <summary>
    /// Returns an existing request for the parcel without creating one.
    /// Prefers an in-progress draft; otherwise the most recently updated request.
    /// </summary>
    Task<LeaseInterestRequestDetailDto?> FindForParcelAsync(
        Guid landParcelId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Explicitly starts (or resumes) a draft for the parcel. Does not create a second draft
    /// when one already exists.
    /// </summary>
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
