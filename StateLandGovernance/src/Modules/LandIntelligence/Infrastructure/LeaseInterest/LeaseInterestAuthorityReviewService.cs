using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestAuthorityReviewService : ILeaseInterestAuthorityReviewService
{
    private const int MaxPageSize = 50;
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandParcelRepository _parcelRepository;
    private readonly INicPersonalDataProtector _nicProtector;
    private readonly ILeaseInterestAuthorityActorContext _authorityActor;
    private readonly TimeProvider _timeProvider;

    public LeaseInterestAuthorityReviewService(
        LandIntelligenceDbContext dbContext,
        ILandParcelRepository parcelRepository,
        INicPersonalDataProtector nicProtector,
        ILeaseInterestAuthorityActorContext authorityActor,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _parcelRepository = parcelRepository;
        _nicProtector = nicProtector;
        _authorityActor = authorityActor;
        _timeProvider = timeProvider;
    }

    public async Task<LeaseInterestAuthorityQueueResponseDto> ListQueueAsync(
        LeaseInterestAuthorityQueueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireAuthorityActor();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        if (!LeaseInterestAuthorityQueueFilters.TryParse(request.StatusFilter, out var filter))
        {
            throw new ValidationException(["statusFilter must be one of: Pending, Approved, Rejected, All."]);
        }

        // Requests that already have at least one officer valuation.
        var valuedRequestIds = _dbContext.LeaseInterestValuations
            .AsNoTracking()
            .Select(v => v.LeaseInterestRequestId)
            .Distinct();

        // Requests that went through the authority decision path.
        var decidedRequestIds = _dbContext.LeaseInterestAuthorityDecisions
            .AsNoTracking()
            .Select(d => d.LeaseInterestRequestId)
            .Distinct();

        var baseQuery = _dbContext.LeaseInterestRequests.AsNoTracking();
        var query = filter switch
        {
            // Pending: referred to authority with an officer valuation (original queue behaviour).
            LeaseInterestAuthorityQueueFilter.Pending => baseQuery.Where(x =>
                x.Status == LeaseInterestRequestStatus.ReferredToAuthority
                && valuedRequestIds.Contains(x.Id)),
            LeaseInterestAuthorityQueueFilter.Approved => baseQuery.Where(x =>
                x.Status == LeaseInterestRequestStatus.ApprovedInPrinciple),
            // Rejected: declined after the valuation / authority path (not officer-only early declines).
            LeaseInterestAuthorityQueueFilter.Rejected => baseQuery.Where(x =>
                x.Status == LeaseInterestRequestStatus.Declined
                && (valuedRequestIds.Contains(x.Id) || decidedRequestIds.Contains(x.Id))),
            _ => baseQuery.Where(x =>
                (x.Status == LeaseInterestRequestStatus.ReferredToAuthority
                    && valuedRequestIds.Contains(x.Id))
                || x.Status == LeaseInterestRequestStatus.ApprovedInPrinciple
                || (x.Status == LeaseInterestRequestStatus.Declined
                    && (valuedRequestIds.Contains(x.Id) || decidedRequestIds.Contains(x.Id)))),
        };

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = new List<LeaseInterestAuthorityQueueItemDto>();
        foreach (var row in rows)
        {
            var latestValuation = await _dbContext.LeaseInterestValuations
                .AsNoTracking()
                .Where(v => v.LeaseInterestRequestId == row.Id)
                .OrderByDescending(v => v.Revision)
                .Select(v => new { v.Revision, v.RecordedAtUtc })
                .FirstOrDefaultAsync(cancellationToken);

            var parcel = await _parcelRepository.GetByIdAsync(row.LandParcelId, cancellationToken);
            var draft = LeaseInterestPayloadSerializer.Deserialize(row.PayloadJson);
            var applicant = draft.Applicant;
            var nic = _nicProtector.Decrypt(row.ApplicantNicCiphertext);
            string? brn = null;
            if (applicant.Kind == LeaseInterestApplicantKindDto.Organization)
            {
                brn = LeaseInterestBusinessRegistration.TryNormalize(
                    applicant.OrganizationRegistrationNumber,
                    out var normalizedBrn)
                    ? normalizedBrn
                    : null;
            }

            items.Add(
                new LeaseInterestAuthorityQueueItemDto(
                    row.Id,
                    row.LandParcelId,
                    row.AcknowledgementReference,
                    row.SubmittedAtUtc,
                    latestValuation?.RecordedAtUtc,
                    latestValuation?.Revision ?? 0,
                    row.Status,
                    parcel?.Identifier.CadastralNumber,
                    parcel?.Location.District,
                    applicant.Kind,
                    nic,
                    brn));
        }

        return new LeaseInterestAuthorityQueueResponseDto(items, total, page, pageSize);
    }

    public async Task<LeaseInterestAuthorityRequestDetailDto> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        RequireAuthorityActor();
        var entity = await LoadRequestAsync(requestId, cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    public async Task<LeaseInterestAuthorityRequestDetailDto> RecordDecisionAsync(
        Guid requestId,
        RecordLeaseInterestAuthorityDecisionDto input,
        CancellationToken cancellationToken = default)
    {
        var authorityId = RequireAuthorityActor();

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new ValidationException(["A reason or instructions is required for every authority decision."]);
        }

        if (input.Reason.Trim().Length > 4000)
        {
            throw new ValidationException(["Reason must be at most 4000 characters."]);
        }

        if (!Enum.IsDefined(input.Decision))
        {
            throw new ValidationException(["Select a valid authority decision."]);
        }

        var entity = await LoadRequestAsync(requestId, cancellationToken);

        var latestValuation = await _dbContext.LeaseInterestValuations
            .AsNoTracking()
            .Where(v => v.LeaseInterestRequestId == requestId)
            .OrderByDescending(v => v.Revision)
            .FirstOrDefaultAsync(cancellationToken);
        if (latestValuation is null)
        {
            throw new LeaseInterestInvalidStateException(
                "An officer valuation must be recorded before an authority decision.");
        }

        if (latestValuation.Id != input.ReviewedValuationId
            || latestValuation.Revision != input.ReviewedValuationRevision)
        {
            throw new LeaseInterestInvalidStateException(
                "The officer valuation changed after this review was opened. Refresh the page and review the latest valuation before deciding.");
        }

        if (entity.Status != LeaseInterestRequestStatus.ReferredToAuthority)
        {
            throw new LeaseInterestInvalidStateException(
                "This request already has an authority outcome and cannot accept another decision while in its current status.");
        }

        if (entity.UpdatedAtUtc != input.ExpectedUpdatedAtUtc)
        {
            throw new LeaseInterestInvalidStateException(
                "This request was updated by someone else. Refresh the page and review the latest details before submitting again.");
        }

        if (!LeaseInterestStatusTransitions.TryGetAuthorityTransition(
                entity.Status,
                input.Decision,
                out var nextStatus,
                out var rejection))
        {
            throw new LeaseInterestInvalidStateException(rejection!);
        }

        var now = _timeProvider.GetUtcNow();
        var reason = input.Reason.Trim();

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        if (nextStatus == LeaseInterestRequestStatus.ApprovedInPrinciple)
        {
            var parcelId = entity.LandParcelId;
            var conflictingApproval = await _dbContext.LeaseInterestRequests
                .AnyAsync(
                    x =>
                        x.LandParcelId == parcelId
                        && x.Id != entity.Id
                        && x.Status == LeaseInterestRequestStatus.ApprovedInPrinciple,
                    cancellationToken);
            if (conflictingApproval)
            {
                throw new LeaseInterestInvalidStateException(
                    LeaseInterestParcelExclusiveApprovalPolicy.BlockSecondApprovalMessage);
            }

            await CloseOtherOpenRequestsForParcelAsync(
                parcelId,
                entity.Id,
                now,
                authorityId,
                cancellationToken);
        }

        entity.Status = nextStatus;
        entity.UpdatedAtUtc = now;

        _dbContext.LeaseInterestAuthorityDecisions.Add(new LeaseInterestAuthorityDecisionEntity
        {
            Id = Guid.NewGuid(),
            LeaseInterestRequestId = entity.Id,
            Decision = input.Decision,
            Reason = reason,
            ReviewedValuationId = latestValuation.Id,
            ReviewedValuationRevision = latestValuation.Revision,
            ResultingStatus = nextStatus,
            RecordedByAuthorityId = authorityId,
            RecordedAtUtc = now,
        });

        AppendStatusHistory(
            entity.Id,
            nextStatus,
            now,
            BuildApplicantVisibleNote(input.Decision, reason),
            LeaseInterestStatusActorKind.Authority,
            authorityId,
            true);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new LeaseInterestInvalidStateException(
                "This request was updated by someone else. Refresh the page and review the latest details before submitting again.");
        }
        catch (DbUpdateException ex) when (IsOneApprovalPerParcelViolation(ex))
        {
            throw new LeaseInterestInvalidStateException(
                LeaseInterestParcelExclusiveApprovalPolicy.BlockSecondApprovalMessage);
        }

        await _dbContext.Entry(entity).Collection(e => e.StatusHistory).LoadAsync(cancellationToken);
        return await MapDetailAsync(entity, cancellationToken);
    }

    private Guid RequireAuthorityActor()
    {
        if (!_authorityActor.IsAuthenticated)
        {
            throw new LandIntelligenceAuthorizationException(
                "An authenticated authority reviewer identity is required for this operation.");
        }

        return _authorityActor.AuthorityActorId!.Value;
    }

    private async Task<LeaseInterestRequestEntity> LoadRequestAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LeaseInterestRequests
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (entity is null)
        {
            throw new LeaseInterestRequestNotFoundException(requestId);
        }

        if (entity.Status == LeaseInterestRequestStatus.Draft)
        {
            throw new LeaseInterestInvalidStateException(
                "Draft requests are not available for authority review.");
        }

        return entity;
    }

    private async Task<LeaseInterestAuthorityRequestDetailDto> MapDetailAsync(
        LeaseInterestRequestEntity entity,
        CancellationToken cancellationToken)
    {
        var draft = LeaseInterestPayloadSerializer.Deserialize(entity.PayloadJson);
        var decryptedNic = _nicProtector.Decrypt(entity.ApplicantNicCiphertext);
        draft = draft with
        {
            Applicant = LeaseInterestApplicantNic.MergeDecryptedNic(
                LeaseInterestApplicantNic.ClearNicFromApplicant(draft.Applicant),
                decryptedNic),
        };
        draft = draft with
        {
            Applicant = LeaseInterestApplicantNic.MaskForApiResponse(draft.Applicant),
        };

        var parcel = await _parcelRepository.GetByIdAsync(entity.LandParcelId, cancellationToken);
        var history = entity.StatusHistory
            .OrderBy(x => x.ChangedAtUtc)
            .Select(entry => new LeaseInterestStatusHistoryEntryDto(
                entry.Status,
                entry.ChangedAtUtc,
                entry.Note,
                (LeaseInterestStatusActorKindDto)entry.ActorKind,
                entry.ActorId,
                entry.VisibleToApplicant))
            .ToList();

        var valuationRows = await _dbContext.LeaseInterestValuations
            .AsNoTracking()
            .Where(v => v.LeaseInterestRequestId == entity.Id)
            .OrderByDescending(v => v.Revision)
            .ToListAsync(cancellationToken);
        var valuations = valuationRows.Select(MapValuation).ToList();

        var decisions = await _dbContext.LeaseInterestAuthorityDecisions
            .AsNoTracking()
            .Where(d => d.LeaseInterestRequestId == entity.Id)
            .OrderByDescending(d => d.RecordedAtUtc)
            .Select(d => new LeaseInterestAuthorityDecisionDto(
                d.Id,
                d.LeaseInterestRequestId,
                d.Decision,
                d.Reason,
                d.ReviewedValuationId,
                d.ReviewedValuationRevision,
                d.RecordedByAuthorityId,
                d.RecordedAtUtc,
                d.ResultingStatus))
            .ToListAsync(cancellationToken);

        var canRecord =
            entity.Status == LeaseInterestRequestStatus.ReferredToAuthority
            && valuations.Count > 0;

        return new LeaseInterestAuthorityRequestDetailDto(
            entity.Id,
            entity.LandParcelId,
            entity.Status,
            entity.AcknowledgementReference,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.SubmittedAtUtc,
            draft.Applicant,
            draft.Proposal,
            draft.Supporting,
            entity.ConsentToPreliminaryAssessment,
            history,
            parcel?.Identifier.CadastralNumber,
            parcel?.Location.District,
            LeaseInterestStatusTransitions.ApplicantOutcomeSummary(entity.Status),
            valuations.FirstOrDefault(),
            valuations,
            decisions,
            canRecord);
    }

    private async Task CloseOtherOpenRequestsForParcelAsync(
        Guid landParcelId,
        Guid approvedRequestId,
        DateTimeOffset now,
        Guid authorityId,
        CancellationToken cancellationToken)
    {
        var others = await _dbContext.LeaseInterestRequests
            .Where(x => x.LandParcelId == landParcelId && x.Id != approvedRequestId)
            .ToListAsync(cancellationToken);

        foreach (var other in others)
        {
            if (!LeaseInterestParcelExclusiveApprovalPolicy.IsOpenForParcelExclusiveClosure(other.Status))
            {
                continue;
            }

            other.Status = LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved;
            other.UpdatedAtUtc = now;
            AppendStatusHistory(
                other.Id,
                LeaseInterestRequestStatus.ClosedAnotherPreliminaryApproved,
                now,
                LeaseInterestParcelExclusiveApprovalPolicy.ApplicantClosedBecauseAnotherApprovedNote,
                LeaseInterestStatusActorKind.System,
                authorityId,
                true);
        }
    }

    private void AppendStatusHistory(
        Guid leaseInterestRequestId,
        LeaseInterestRequestStatus status,
        DateTimeOffset changedAtUtc,
        string? note,
        LeaseInterestStatusActorKind actorKind,
        Guid? actorId,
        bool visibleToApplicant)
    {
        _dbContext.LeaseInterestStatusHistory.Add(new LeaseInterestStatusHistoryEntity
        {
            Id = Guid.NewGuid(),
            LeaseInterestRequestId = leaseInterestRequestId,
            Status = status,
            ChangedAtUtc = changedAtUtc,
            Note = note,
            ActorKind = actorKind,
            ActorId = actorId,
            VisibleToApplicant = visibleToApplicant,
        });
    }

    private static bool IsOneApprovalPerParcelViolation(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
        {
            return false;
        }

        return postgres.SqlState == PostgresErrorCodes.UniqueViolation
            && postgres.ConstraintName?.Contains(
                "one_approved_in_principle_per_parcel",
                StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string BuildApplicantVisibleNote(
        LeaseInterestAuthorityDecisionKind decision,
        string reason) =>
        decision switch
        {
            LeaseInterestAuthorityDecisionKind.Approved => reason,
            LeaseInterestAuthorityDecisionKind.Rejected => reason,
            _ => reason,
        };

    private static readonly JsonSerializerOptions WorksheetJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static LeaseInterestValuationDto MapValuation(LeaseInterestValuationEntity x) => new()
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
        RecordedByOfficerId = x.RecordedByOfficerId,
        RecordedAtUtc = x.RecordedAtUtc,
    };
}
