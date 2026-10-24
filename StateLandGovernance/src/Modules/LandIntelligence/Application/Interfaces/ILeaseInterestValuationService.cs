using StateLandGovernance.LandIntelligence.Application.DTOs;

namespace StateLandGovernance.LandIntelligence.Application.Interfaces;

public interface ILeaseInterestValuationService
{
    Task<IReadOnlyList<LeaseInterestValuationDto>> ListAsync(Guid requestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new valuation revision. The same <paramref name="idempotencyKey"/> for a request
    /// always returns the previously created valuation and never overwrites it.
    /// </summary>
    Task<LeaseInterestValuationDto> RecordAsync(
        Guid requestId,
        RecordLeaseInterestValuationDto input,
        string? idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Latest valuation summary for the authenticated applicant who owns the request.
    /// Returns null when no valuation has been recorded yet.
    /// </summary>
    Task<LeaseInterestApplicantValuationSummaryDto?> GetApplicantLatestSummaryAsync(
        Guid requestId,
        CancellationToken cancellationToken = default);
}
