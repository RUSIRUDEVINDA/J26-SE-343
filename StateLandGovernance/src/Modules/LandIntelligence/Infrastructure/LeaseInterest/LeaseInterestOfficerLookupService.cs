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
        if (!SriLankanNic.TryValidate(
                request.NationalIdentityCardNumber,
                out var normalized,
                out var validationError))
        {
            throw new ValidationException([validationError!]);
        }

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);
        var digest = _nicProtector.ComputeLookupDigest(normalized);

        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                x.ApplicantNicLookupDigest == digest
                && x.Status != LeaseInterestRequestStatus.Draft);

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
            var nationalIdentityCardNumber =
                _nicProtector.Decrypt(row.ApplicantNicCiphertext) ?? normalized;

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
                    nationalIdentityCardNumber));
        }

        return new LeaseInterestOfficerLookupResponseDto(items, total, page, pageSize);
    }
}
