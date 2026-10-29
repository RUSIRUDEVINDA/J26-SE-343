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
        if (nicProvided && brnProvided)
        {
            throw new ValidationException(
                ["Provide either a NIC or a business registration number (BRN), not both."]);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 10 : Math.Min(request.PageSize, MaxPageSize);

        if (!nicProvided && !brnProvided)
        {
            return await ListAllSubmittedAsync(page, pageSize, cancellationToken);
        }

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

    private async Task<LeaseInterestOfficerLookupResponseDto> ListAllSubmittedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                x.Status != LeaseInterestRequestStatus.Draft
                && x.SubmittedAtUtc != null);

        return await MaterializeLookupPageAsync(
            query,
            page,
            pageSize,
            revealApplicantIdentifiers: false,
            normalizedNic: null,
            normalizedBrn: null,
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
                && x.Status != LeaseInterestRequestStatus.Draft
                && x.SubmittedAtUtc != null);

        return await MaterializeLookupPageAsync(
            query,
            page,
            pageSize,
            revealApplicantIdentifiers: true,
            normalizedNic: normalized,
            normalizedBrn: null,
            cancellationToken);
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
                && x.Status != LeaseInterestRequestStatus.Draft
                && x.SubmittedAtUtc != null);

        return await MaterializeLookupPageAsync(
            query,
            page,
            pageSize,
            revealApplicantIdentifiers: true,
            normalizedNic: null,
            normalizedBrn: normalized,
            cancellationToken);
    }

    private async Task<LeaseInterestOfficerLookupResponseDto> MaterializeLookupPageAsync(
        IQueryable<Infrastructure.Persistence.Entities.LeaseInterestRequestEntity> query,
        int page,
        int pageSize,
        bool revealApplicantIdentifiers,
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
            string? nic = null;
            string? brn = null;
            var decryptedNic = _nicProtector.Decrypt(row.ApplicantNicCiphertext);
            var normalizedBrnFromPayload = LeaseInterestBusinessRegistration.TryNormalize(
                applicant.OrganizationRegistrationNumber,
                out var normalizedFromPayload)
                ? normalizedFromPayload
                : null;

            if (revealApplicantIdentifiers)
            {
                nic =
                    applicant.Kind == LeaseInterestApplicantKindDto.Individual
                        ? decryptedNic ?? normalizedNic
                        : decryptedNic;
                brn = normalizedBrnFromPayload ?? normalizedBrn;
            }
            else
            {
                nic = SriLankanNic.Mask(decryptedNic);
                if (applicant.Kind == LeaseInterestApplicantKindDto.Organization)
                {
                    brn = SriLankanNic.Mask(normalizedBrnFromPayload);
                }
            }

            var hasValuation = await _dbContext.LeaseInterestValuations
                .AsNoTracking()
                .AnyAsync(v => v.LeaseInterestRequestId == row.Id, cancellationToken);

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
                    brn,
                    hasValuation));
        }

        return new LeaseInterestOfficerLookupResponseDto(items, total, page, pageSize);
    }
}
