using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestOfficerLookupService : ILeaseInterestOfficerLookupService
{
    private const int MaxPageSize = 50;
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandParcelRepository _parcelRepository;
    private readonly INicPersonalDataProtector _nicProtector;

    public LeaseInterestOfficerLookupService(
        LandIntelligenceDbContext dbContext,
        ILandParcelRepository parcelRepository,
        INicPersonalDataProtector nicProtector)
    {
        _dbContext = dbContext;
        _parcelRepository = parcelRepository;
        _nicProtector = nicProtector;
    }

    public async Task<LeaseInterestOfficerLookupResponseDto> LookupByNicAsync(
        LeaseInterestOfficerLookupRequestDto request,
        CancellationToken cancellationToken)
    {
        var nicProvided = !string.IsNullOrWhiteSpace(request.NationalIdentityCardNumber);
        var brnProvided = !string.IsNullOrWhiteSpace(request.BusinessRegistrationNumber);
        if (nicProvided == brnProvided)
        {
            throw new ValidationException(
                [
                    nicProvided
                        ? "Provide either a NIC or a business registration number (BRN), not both."
                        : "Enter a NIC or business registration number (BRN) to search.",
                ]);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        if (nicProvided)
        {
            return await LookupByNicDigestAsync(
                request.NationalIdentityCardNumber!,
                page,
                pageSize,
                cancellationToken);
        }

        return await LookupByBrnDigestAsync(
            request.BusinessRegistrationNumber!,
            page,
            pageSize,
            cancellationToken);
    }

    private async Task<LeaseInterestOfficerLookupResponseDto> LookupByNicDigestAsync(
        string rawNic,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!SriLankanNic.TryValidate(rawNic, out var normalized, out var validationError))
        {
            throw new ValidationException([validationError!]);
        }

        var digest = _nicProtector.ComputeLookupDigest(normalized);
        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                x.ApplicantNicLookupDigest == digest
                && x.Status != LeaseInterestRequestStatus.Draft);

        return await MaterializeLookupPageAsync(query, page, pageSize, normalized, null, cancellationToken);
    }

    private async Task<LeaseInterestOfficerLookupResponseDto> LookupByBrnDigestAsync(
        string rawBrn,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!LeaseInterestBusinessRegistration.TryValidate(rawBrn, out var normalized, out var validationError))
        {
            throw new ValidationException([validationError!]);
        }

        var digest = _nicProtector.ComputeLookupDigest(normalized);
        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                x.ApplicantBrnLookupDigest == digest
                && x.Status != LeaseInterestRequestStatus.Draft);

        return await MaterializeLookupPageAsync(query, page, pageSize, null, normalized, cancellationToken);
    }

    private async Task<LeaseInterestOfficerLookupResponseDto> MaterializeLookupPageAsync(
        IQueryable<Infrastructure.Persistence.Entities.LeaseInterestRequestEntity> query,
        int page,
        int pageSize,
        string? normalizedNic,
        string? normalizedBrn,
        CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<LeaseInterestOfficerLookupItemDto>();
        foreach (var row in rows)
        {
            var parcel = await _parcelRepository.GetByIdAsync(row.LandParcelId, cancellationToken);
            var draft = LeaseInterestPayloadSerializer.Deserialize(row.PayloadJson);
            var applicant = draft.Applicant;
            var nic =
                applicant.Kind == LeaseInterestApplicantKindDto.Individual
                    ? _nicProtector.Decrypt(row.ApplicantNicCiphertext) ?? normalizedNic
                    : _nicProtector.Decrypt(row.ApplicantNicCiphertext);
            var brn = LeaseInterestBusinessRegistration.TryNormalize(
                applicant.OrganizationRegistrationNumber,
                out var normalizedFromPayload)
                ? normalizedFromPayload
                : normalizedBrn;

            items.Add(
                new LeaseInterestOfficerLookupItemDto(
                    row.Id,
                    row.LandParcelId,
                    row.AcknowledgementReference,
                    row.SubmittedAtUtc,
                    row.Status,
                    parcel?.Identifier.CadastralNumber,
                    parcel?.Location.District,
                    (LandUseType)row.RequestedPurpose,
                    row.RequestedDurationYears,
                    applicant.Kind,
                    nic,
                    brn));
        }

        return new LeaseInterestOfficerLookupResponseDto(items, total, page, pageSize);
    }
}
