using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestValuationService : ILeaseInterestValuationService
{
    private const int MaxRevisionAllocationAttempts = 5;
    private static readonly HashSet<string> FormTypes = ["AgriculturalCommercial", "Residential"];

    /// <summary>
    /// Process-local gate so concurrent saves for the same request cannot allocate the same revision
    /// when the provider does not honor row locks / unique conflicts the same way as Postgres.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RevisionGates = new();

    private readonly LandIntelligenceDbContext _db;
    private readonly ILeaseInterestOfficerActorContext _actor;
    private readonly ILandIntelligenceCallerContext _caller;
    private readonly TimeProvider _clock;

    public LeaseInterestValuationService(
        LandIntelligenceDbContext db,
        ILeaseInterestOfficerActorContext actor,
        ILandIntelligenceCallerContext caller,
        TimeProvider clock)
    {
        _db = db;
        _actor = actor;
        _caller = caller;
        _clock = clock;
    }

    public async Task<IReadOnlyList<LeaseInterestValuationDto>> ListAsync(
        Guid requestId, CancellationToken cancellationToken = default)
    {
        RequireOfficer();
        await RequireSubmittedRequestAsync(requestId, cancellationToken);
        var rows = await _db.LeaseInterestValuations.AsNoTracking()
            .Where(x => x.LeaseInterestRequestId == requestId)
            .OrderByDescending(x => x.Revision)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<LeaseInterestValuationDto> RecordAsync(
        Guid requestId,
        RecordLeaseInterestValuationDto input,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var officerId = RequireOfficer();
        var normalizedKey = NormalizeIdempotencyKey(idempotencyKey);
        if (normalizedKey is null)
        {
            throw new ValidationException(["Provide a non-empty Idempotency-Key header for each valuation save attempt."]);
        }

        var request = await RequireSubmittedRequestAsync(requestId, cancellationToken);
        if (request.Status != LeaseInterestRequestStatus.ReferredToAuthority)
        {
            throw new LeaseInterestInvalidStateException(
                "Record the valuation after the request has been referred to the authority.");
        }

        ValidateInput(input);

        var prior = await FindByIdempotencyKeyAsync(requestId, normalizedKey, cancellationToken);
        if (prior is not null)
        {
            return Map(prior);
        }

        var gate = RevisionGates.GetOrAdd(requestId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            for (var attempt = 1; attempt <= MaxRevisionAllocationAttempts; attempt++)
            {
                prior = await FindByIdempotencyKeyAsync(requestId, normalizedKey, cancellationToken);
                if (prior is not null)
                {
                    return Map(prior);
                }

                // Re-load tracked request so UpdatedAtUtc can be touched without changing Status.
                request = await RequireSubmittedRequestAsync(requestId, cancellationToken);
                if (request.Status != LeaseInterestRequestStatus.ReferredToAuthority)
                {
                    throw new LeaseInterestInvalidStateException(
                        "Record the valuation after the request has been referred to the authority.");
                }

                Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
                try
                {
                    if (_db.Database.IsRelational())
                    {
                        transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                        // Serialize revision allocation for this request without overwriting an existing row.
                        await _db.Database.ExecuteSqlInterpolatedAsync(
                            $"""
                             SELECT 1
                             FROM land_intelligence.lease_interest_requests
                             WHERE "Id" = {requestId}
                             FOR UPDATE
                             """,
                            cancellationToken);
                    }

                    prior = await FindByIdempotencyKeyAsync(requestId, normalizedKey, cancellationToken);
                    if (prior is not null)
                    {
                        if (transaction is not null)
                        {
                            await transaction.CommitAsync(cancellationToken);
                        }

                        return Map(prior);
                    }

                    var lastRevision = await _db.LeaseInterestValuations
                        .Where(x => x.LeaseInterestRequestId == requestId)
                        .MaxAsync(x => (int?)x.Revision, cancellationToken) ?? 0;

                    var entity = new LeaseInterestValuationEntity
                    {
                        Id = Guid.NewGuid(),
                        LeaseInterestRequestId = requestId,
                        LandParcelId = request.LandParcelId,
                        Revision = lastRevision + 1,
                        FormType = input.FormType,
                        AssessedLandValueLkr = decimal.Round(input.AssessedLandValueLkr, 2),
                        AssessedAreaHectares = input.AssessedAreaHectares.HasValue
                            ? decimal.Round(input.AssessedAreaHectares.Value, 6)
                            : null,
                        InspectionDate = input.InspectionDate,
                        ValuationDate = input.ValuationDate,
                        ValuationReference = input.ValuationReference?.Trim(),
                        IssuingOffice = input.IssuingOffice?.Trim(),
                        SiteCondition = input.SiteCondition?.Trim(),
                        CultivationDetails = input.CultivationDetails?.Trim(),
                        ResidentialImprovements = input.ResidentialImprovements?.Trim(),
                        ProjectUseDetails = input.ProjectUseDetails?.Trim(),
                        OtherTypeDetails = input.OtherTypeDetails?.Trim(),
                        OfficerNotes = input.OfficerNotes?.Trim(),
                        WorksheetJson = JsonSerializer.Serialize(input.Worksheet, WorksheetJsonOptions),
                        IdempotencyKey = normalizedKey,
                        RecordedByOfficerId = officerId,
                        RecordedAtUtc = _clock.GetUtcNow(),
                    };

                    // Intentionally does not update LeaseInterestRequest.Status — valuation is separate from referral.
                    request.UpdatedAtUtc = entity.RecordedAtUtc;
                    _db.LeaseInterestValuations.Add(entity);
                    await _db.SaveChangesAsync(cancellationToken);
                    if (transaction is not null)
                    {
                        await transaction.CommitAsync(cancellationToken);
                    }

                    return Map(entity);
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (transaction is not null)
                    {
                        await SafeRollbackAsync(transaction, cancellationToken);
                    }

                    _db.ChangeTracker.Clear();
                    throw new LeaseInterestInvalidStateException(
                        "This request changed while the valuation was being recorded. Refresh and review its current status before trying again.");
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    if (transaction is not null)
                    {
                        await SafeRollbackAsync(transaction, cancellationToken);
                    }

                    _db.ChangeTracker.Clear();

                    var byKey = await FindByIdempotencyKeyAsync(requestId, normalizedKey, cancellationToken);
                    if (byKey is not null)
                    {
                        return Map(byKey);
                    }

                    if (attempt == MaxRevisionAllocationAttempts)
                    {
                        throw new LeaseInterestInvalidStateException(
                            "Another valuation was recorded at the same time. Refresh and try again with a new save attempt.");
                    }
                }
                finally
                {
                    if (transaction is not null)
                    {
                        await transaction.DisposeAsync();
                    }
                }
            }

            throw new LeaseInterestInvalidStateException(
                "Another valuation was recorded at the same time. Refresh and try again with a new save attempt.");
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<LeaseInterestApplicantValuationSummaryDto?> GetApplicantLatestSummaryAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        if (!_caller.IsAuthenticated || !_caller.OwnerId.HasValue)
        {
            throw new LandIntelligenceAuthorizationException(
                "An authenticated applicant is required to view valuation status.");
        }

        var ownerId = _caller.OwnerId.Value;
        var request = await _db.LeaseInterestRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new LeaseInterestRequestNotFoundException(requestId);

        if (request.OwnerId != ownerId)
        {
            throw new LeaseInterestRequestNotFoundException(requestId);
        }

        if (request.Status == LeaseInterestRequestStatus.Draft)
        {
            return null;
        }

        var latest = await _db.LeaseInterestValuations.AsNoTracking()
            .Where(x => x.LeaseInterestRequestId == requestId)
            .OrderByDescending(x => x.Revision)
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return null;
        }

        var worksheet = latest.WorksheetJson is null
            ? null
            : JsonSerializer.Deserialize<LeaseWorksheetDetailsDto>(latest.WorksheetJson, WorksheetJsonOptions);

        return new LeaseInterestApplicantValuationSummaryDto
        {
            Revision = latest.Revision,
            RecordedAtUtc = latest.RecordedAtUtc,
            FormType = latest.FormType,
            AssessedLandValueLkr = latest.AssessedLandValueLkr,
            LeaseFileNumber = worksheet?.LeaseFileNumber,
            DivisionalSecretariat = worksheet?.DivisionalSecretariat,
        };
    }

    private void ValidateInput(RecordLeaseInterestValuationDto input)
    {
        var errors = new List<string>();
        if (!FormTypes.Contains(input.FormType))
        {
            errors.Add("Select a valid valuation form type.");
        }

        if (input.AssessedLandValueLkr <= 0 || input.AssessedLandValueLkr > 999_999_999_999_999.99m)
        {
            errors.Add("Assessed land value must be a positive LKR amount.");
        }

        if (input.AssessedAreaHectares is <= 0 or > 1_000_000m)
        {
            errors.Add("When supplied, assessed area must be a positive number of hectares.");
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        if (input.InspectionDate > today || input.ValuationDate > today)
        {
            errors.Add("Inspection and valuation dates cannot be in the future.");
        }

        if (input.InspectionDate.HasValue && input.ValuationDate.HasValue && input.ValuationDate < input.InspectionDate)
        {
            errors.Add("Valuation date cannot precede the inspection date.");
        }

        if (input.ValuationReference?.Length > 128)
        {
            errors.Add("Valuation reference must be at most 128 characters.");
        }

        if (input.IssuingOffice?.Length > 200)
        {
            errors.Add("Issuing office must be at most 200 characters.");
        }

        if (input.SiteCondition?.Length > 2000)
        {
            errors.Add("Site condition must be at most 2000 characters.");
        }

        foreach (var value in new[] { input.CultivationDetails, input.ResidentialImprovements, input.ProjectUseDetails, input.OtherTypeDetails })
        {
            if (value?.Length > 2000)
            {
                errors.Add("Type-specific details must be at most 2000 characters.");
            }
        }

        if (input.OfficerNotes?.Length > 4000)
        {
            errors.Add("Officer notes must be at most 4000 characters.");
        }

        if (input.Worksheet is null)
        {
            errors.Add("Worksheet details are required.");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(input.Worksheet.LeaseFileNumber) || input.Worksheet.LeaseFileNumber.Length > 128)
            {
                errors.Add("Enter the lease file number (at most 128 characters).");
            }

            if (string.IsNullOrWhiteSpace(input.Worksheet.DivisionalSecretariat) || input.Worksheet.DivisionalSecretariat.Length > 200)
            {
                errors.Add("Enter the Divisional Secretariat division (at most 200 characters).");
            }

            if (input.Worksheet.WorksheetNotes?.Length > 4000)
            {
                errors.Add("Worksheet notes must be at most 4000 characters.");
            }

            if (input.Worksheet.LeaseRatePercent is < 0 or > 100)
            {
                errors.Add("The manually entered lease rate must be between 0 and 100 percent.");
            }

            foreach (var amount in new[]
                     {
                         input.Worksheet.PreviousAssessedValueLkr, input.Worksheet.AnnualLeaseAmountLkr,
                         input.Worksheet.FirstPeriodAmountLkr, input.Worksheet.SecondPeriodAmountLkr,
                         input.Worksheet.SecondPeriodAnnualAmountLkr, input.Worksheet.Circular2018AmountLkr,
                         input.Worksheet.Circular2020SixAdjustmentLkr,
                     })
            {
                if (amount is < 0 or > 999_999_999_999_999.99m)
                {
                    errors.Add("Worksheet amounts must be non-negative LKR values within the supported range.");
                }
            }

            foreach (var group in new[] { input.Worksheet.FirstPeriodLines, input.Worksheet.SecondPeriodLines })
            {
                if (group is { Count: > 12 })
                {
                    errors.Add("Each worksheet table supports at most 12 rows.");
                }

                if (group is null)
                {
                    continue;
                }

                foreach (var row in group)
                {
                    if (string.IsNullOrWhiteSpace(row.Period) || row.Period.Length > 120)
                    {
                        errors.Add("Each table row needs a period description of at most 120 characters.");
                    }

                    if (row.AmountLkr is < 0 or > 999_999_999_999_999.99m)
                    {
                        errors.Add("Table amounts must be non-negative LKR values within the supported range.");
                    }
                }
            }
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
    }

    private async Task<LeaseInterestValuationEntity?> FindByIdempotencyKeyAsync(
        Guid requestId,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        await _db.LeaseInterestValuations.AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.LeaseInterestRequestId == requestId && x.IdempotencyKey == idempotencyKey,
                cancellationToken);

    private static string? NormalizeIdempotencyKey(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }

        var trimmed = idempotencyKey.Trim();
        return trimmed.Length is < 1 or > 128 ? null : trimmed;
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var inner = exception.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                return true;
            }
        }

        // In-memory / non-Postgres providers surface message text instead of SqlState.
        var message = exception.InnerException?.Message ?? exception.Message;
        return message.Contains("unique", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task SafeRollbackAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        try
        {
            await transaction.RollbackAsync(cancellationToken);
        }
        catch
        {
            // Transaction may already be completed after a failed SaveChanges.
        }
    }

    private Guid RequireOfficer()
    {
        if (!_actor.IsAuthenticated || !_actor.OfficerActorId.HasValue)
        {
            throw new LandIntelligenceAuthorizationException("An authenticated officer is required.");
        }

        return _actor.OfficerActorId.Value;
    }

    private async Task<LeaseInterestRequestEntity> RequireSubmittedRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var request = await _db.LeaseInterestRequests
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken)
            ?? throw new LeaseInterestRequestNotFoundException(requestId);
        if (request.Status == LeaseInterestRequestStatus.Draft)
        {
            throw new LeaseInterestInvalidStateException("Draft requests cannot be valued.");
        }

        return request;
    }

    private static readonly JsonSerializerOptions WorksheetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static LeaseInterestValuationDto Map(LeaseInterestValuationEntity x) => new()
    {
        Id = x.Id,
        LeaseInterestRequestId = x.LeaseInterestRequestId,
        LandParcelId = x.LandParcelId,
        Revision = x.Revision,
        FormType = x.FormType,
        AssessedLandValueLkr = x.AssessedLandValueLkr,
        AssessedAreaHectares = x.AssessedAreaHectares,
        InspectionDate = x.InspectionDate,
        ValuationDate = x.ValuationDate,
        ValuationReference = x.ValuationReference,
        IssuingOffice = x.IssuingOffice,
        SiteCondition = x.SiteCondition,
        CultivationDetails = x.CultivationDetails,
        ResidentialImprovements = x.ResidentialImprovements,
        ProjectUseDetails = x.ProjectUseDetails,
        OtherTypeDetails = x.OtherTypeDetails,
        OfficerNotes = x.OfficerNotes,
        Worksheet = x.WorksheetJson is null
            ? null
            : JsonSerializer.Deserialize<LeaseWorksheetDetailsDto>(x.WorksheetJson, WorksheetJsonOptions),
        IdempotencyKey = x.IdempotencyKey,
        RecordedByOfficerId = x.RecordedByOfficerId,
        RecordedAtUtc = x.RecordedAtUtc,
    };
}
