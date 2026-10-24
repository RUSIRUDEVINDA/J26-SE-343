using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestOfficerReviewService : ILeaseInterestOfficerReviewService
{
    private const int MaxPageSize = 50;
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandParcelRepository _parcelRepository;
    private readonly INicPersonalDataProtector _nicProtector;
    private readonly ILeaseInterestOfficerActorContext _officerActor;
    private readonly TimeProvider _timeProvider;

    public LeaseInterestOfficerReviewService(
        LandIntelligenceDbContext dbContext,
        ILandParcelRepository parcelRepository,
        INicPersonalDataProtector nicProtector,
        ILeaseInterestOfficerActorContext officerActor,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _parcelRepository = parcelRepository;
        _nicProtector = nicProtector;
        _officerActor = officerActor;
        _timeProvider = timeProvider;
    }

    public async Task<LeaseInterestOfficerRequestDetailDto> GetForReviewAsync(
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        RequireOfficerActor();
        var entity = await LoadRequestAsync(requestId, cancellationToken);
        return await MapOfficerDetailAsync(entity, cancellationToken);
    }

    public async Task<LeaseInterestOfficerQueueResponseDto> ListQueueAsync(
        LeaseInterestOfficerQueueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireOfficerActor();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                x.Status == LeaseInterestRequestStatus.PendingVerification
                || x.Status == LeaseInterestRequestStatus.NeedsInformation);

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await MapQueueItemsAsync(rows, cancellationToken);
        return new LeaseInterestOfficerQueueResponseDto(items, total, page, pageSize);
    }

    public async Task<LeaseInterestOfficerQueueResponseDto> ListValuationQueueAsync(
        LeaseInterestOfficerQueueRequestDto request,
        CancellationToken cancellationToken = default)
    {
        RequireOfficerActor();

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize < 1 ? 20 : Math.Min(request.PageSize, MaxPageSize);

        var valuedRequestIds = _dbContext.LeaseInterestValuations
            .AsNoTracking()
            .Select(v => v.LeaseInterestRequestId)
            .Distinct();

        var query = _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x =>
                !valuedRequestIds.Contains(x.Id)
                && (x.Status == LeaseInterestRequestStatus.AwaitingValuation
                    || x.Status == LeaseInterestRequestStatus.ReferredToAuthority));

        var total = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(x => x.SubmittedAtUtc ?? x.UpdatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await MapQueueItemsAsync(rows, cancellationToken);
        return new LeaseInterestOfficerQueueResponseDto(items, total, page, pageSize);
    }

    public async Task<LeaseInterestOfficerRequestDetailDto> UpdateStatusAsync(
        Guid requestId,
        LeaseInterestOfficerStatusUpdateDto update,
        CancellationToken cancellationToken = default)
    {
        var officerId = RequireOfficerActor();

        if (string.IsNullOrWhiteSpace(update.Note))
        {
            throw new ValidationException(["Officer note is required for status updates."]);
        }

        var entity = await LoadRequestAsync(requestId, cancellationToken);

        if (!LeaseInterestStatusTransitions.TryGetTransition(
                entity.Status,
                update.NewStatus,
                out var rejection))
        {
            throw new LeaseInterestInvalidStateException(rejection!);
        }

        if (!LeaseInterestOfficerWorkflowRules.IsVerificationQueueStatus(entity.Status))
        {
            throw new LeaseInterestInvalidStateException(
                "Verification decisions can only be recorded while the request is in the officer verification queue.");
        }

        var now = _timeProvider.GetUtcNow();
        entity.Status = update.NewStatus;
        entity.UpdatedAtUtc = now;

        if (update.NewStatus == LeaseInterestRequestStatus.AwaitingValuation)
        {
            entity.VerificationCompletedByOfficerId = officerId;
        }

        AppendHistory(
            entity.Id,
            update.NewStatus,
            now,
            update.Note.Trim(),
            LeaseInterestStatusActorKind.Officer,
            officerId,
            update.VisibleToApplicant);

        await _dbContext.SaveChangesAsync(cancellationToken);
        await _dbContext.Entry(entity).Collection(e => e.StatusHistory).LoadAsync(cancellationToken);
        return await MapOfficerDetailAsync(entity, cancellationToken);
    }

    private Guid RequireOfficerActor()
    {
        if (!_officerActor.IsAuthenticated)
        {
            throw new LandIntelligenceAuthorizationException(
                "An authenticated officer identity is required for this operation.");
        }

        return _officerActor.OfficerActorId!.Value;
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
                "Draft requests are not available for officer review.");
        }

        return entity;
    }

    private async Task<LeaseInterestOfficerRequestDetailDto> MapOfficerDetailAsync(
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
            .Select(MapHistoryEntry)
            .ToList();

        var latestAuthority = await _dbContext.LeaseInterestAuthorityDecisions
            .AsNoTracking()
            .Where(d => d.LeaseInterestRequestId == entity.Id)
            .OrderByDescending(d => d.RecordedAtUtc)
            .Select(d => new { d.Decision, d.Reason, d.RecordedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);

        LeaseInterestApplicantAuthorityDecisionDto? authoritySummary = latestAuthority is null
            ? null
            : new LeaseInterestApplicantAuthorityDecisionDto(
                latestAuthority.Decision,
                latestAuthority.Reason,
                latestAuthority.RecordedAtUtc,
                LeaseInterestAuthorityDecisionLabels.DecisionLabel(latestAuthority.Decision),
                LeaseInterestAuthorityDecisionLabels.NextActionHint(latestAuthority.Decision));

        var hasValuation = await _dbContext.LeaseInterestValuations
            .AsNoTracking()
            .AnyAsync(v => v.LeaseInterestRequestId == entity.Id, cancellationToken);
        var officerId = _officerActor.OfficerActorId;
        var canVerify = LeaseInterestOfficerWorkflowRules.IsVerificationQueueStatus(entity.Status);
        var canValue = officerId is { } oid
            && LeaseInterestOfficerWorkflowRules.CanOfficerRecordValuation(
                entity.Status,
                hasValuation,
                entity.VerificationCompletedByOfficerId,
                oid);

        return new LeaseInterestOfficerRequestDetailDto(
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
            authoritySummary,
            entity.VerificationCompletedByOfficerId,
            officerId,
            canVerify,
            canValue,
            hasValuation);
    }

    private async Task<List<LeaseInterestOfficerQueueItemDto>> MapQueueItemsAsync(
        IReadOnlyList<LeaseInterestRequestEntity> rows,
        CancellationToken cancellationToken)
    {
        var items = new List<LeaseInterestOfficerQueueItemDto>();
        foreach (var row in rows)
        {
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

            var hasValuation = await _dbContext.LeaseInterestValuations
                .AsNoTracking()
                .AnyAsync(v => v.LeaseInterestRequestId == row.Id, cancellationToken);

            items.Add(
                new LeaseInterestOfficerQueueItemDto(
                    row.Id,
                    row.LandParcelId,
                    row.AcknowledgementReference,
                    row.SubmittedAtUtc,
                    row.Status,
                    parcel?.Identifier.CadastralNumber,
                    parcel?.Location.District,
                    applicant.Kind,
                    nic,
                    brn,
                    hasValuation));
        }

        return items;
    }

    private static LeaseInterestStatusHistoryEntryDto MapHistoryEntry(
        LeaseInterestStatusHistoryEntity entry) =>
        new(
            entry.Status,
            entry.ChangedAtUtc,
            entry.Note,
            (LeaseInterestStatusActorKindDto)entry.ActorKind,
            entry.ActorId,
            entry.VisibleToApplicant);

    private void AppendHistory(
        Guid requestId,
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
            LeaseInterestRequestId = requestId,
            Status = status,
            ChangedAtUtc = changedAtUtc,
            Note = note,
            ActorKind = actorKind,
            ActorId = actorId,
            VisibleToApplicant = visibleToApplicant,
        });
    }
}
