using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;

public sealed class LeaseInterestRequestService : ILeaseInterestRequestService
{
    private readonly LandIntelligenceDbContext _dbContext;
    private readonly ILandIntelligenceCallerContext _caller;
    private readonly ILandParcelRepository _parcelRepository;
    private readonly ILeaseInterestCostAssessmentService _costAssessment;
    private readonly LeaseInterestDraftValidator _validator;
    private readonly INicPersonalDataProtector _nicProtector;
    private readonly TimeProvider _timeProvider;
    private readonly ILeaseInterestParcelEligibilityService _parcelEligibility;

    public LeaseInterestRequestService(
        LandIntelligenceDbContext dbContext,
        ILandIntelligenceCallerContext caller,
        ILandParcelRepository parcelRepository,
        ILeaseInterestCostAssessmentService costAssessment,
        LeaseInterestDraftValidator validator,
        INicPersonalDataProtector nicProtector,
        TimeProvider timeProvider,
        ILeaseInterestParcelEligibilityService parcelEligibility)
    {
        _dbContext = dbContext;
        _caller = caller;
        _parcelRepository = parcelRepository;
        _costAssessment = costAssessment;
        _validator = validator;
        _nicProtector = nicProtector;
        _timeProvider = timeProvider;
        _parcelEligibility = parcelEligibility;
    }

    public LeaseInterestDocumentChecklistDto GetDocumentChecklist() =>
        new(
            [
                new("nic", "National Identity Card (individual)", "May be requested during verification."),
                new("registration", "Business registration certificate (organization)", "May be requested during verification."),
                new("survey", "Survey plan or cadastral extract", "If available from your records."),
                new("project", "Project brief or feasibility summary", "Optional at this stage."),
                new("financial", "Financial capacity evidence", "Not collected electronically in this pilot."),
            ],
            "Secure document upload is not enabled. An officer may request documents through official channels after preliminary review.");

    public async Task<LeaseInterestRequestDetailDto> GetDraftOrCreateAsync(
        Guid landParcelId,
        CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();
        await EnsureParcelExistsAsync(landParcelId, cancellationToken);
        await _parcelEligibility.EnsureEligibleForLeaseInterestAsync(landParcelId, cancellationToken);

        var entity = await _dbContext.LeaseInterestRequests
            .Include(x => x.StatusHistory)
            .Where(x =>
                x.OwnerId == ownerId
                && x.LandParcelId == landParcelId
                && x.Status == LeaseInterestRequestStatus.Draft)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var now = _timeProvider.GetUtcNow();
        if (entity is null)
        {
            var emptyDraft = new UpsertLeaseInterestDraftDto(
                new LeaseInterestApplicantDto(
                    LeaseInterestApplicantKindDto.Individual,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    null,
                    null,
                    null,
                    null),
                new LeaseInterestProposalDto(
                    LandUseType.Other,
                    string.Empty,
                    null,
                    string.Empty,
                    1,
                    string.Empty),
                new LeaseInterestSupportingInfoDto(string.Empty),
                false);

            entity = new LeaseInterestRequestEntity
            {
                Id = Guid.NewGuid(),
                OwnerId = ownerId,
                LandParcelId = landParcelId,
                Status = LeaseInterestRequestStatus.Draft,
                PayloadJson = LeaseInterestPayloadSerializer.Serialize(emptyDraft),
                RequestedPurpose = (int)LandUseType.Other,
                ConsentToPreliminaryAssessment = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            _dbContext.LeaseInterestRequests.Add(entity);
            AppendStatusHistory(
                entity.Id,
                LeaseInterestRequestStatus.Draft,
                now,
                "Draft created.",
                LeaseInterestStatusActorKind.System,
                null,
                true);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return MapDetail(entity);
    }

    public async Task<LeaseInterestRequestDetailDto> SaveDraftAsync(
        Guid requestId,
        UpsertLeaseInterestDraftDto draft,
        CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();
        var errors = _validator.Validate(draft, forSubmit: false);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        var entity = await LoadOwnedRequestAsync(requestId, ownerId, cancellationToken);
        if (entity.Status != LeaseInterestRequestStatus.Draft)
        {
            throw new LeaseInterestInvalidStateException(
                "Only draft requests can be edited. Submit a new interest from parcel intelligence if needed.");
        }

        var now = _timeProvider.GetUtcNow();
        entity.PayloadJson = LeaseInterestPayloadSerializer.Serialize(draft);
        ApplyNicFromDraft(entity, draft);
        entity.RequestedPurpose = (int)draft.Proposal.RequestedPurpose;
        entity.RequestedDurationYears = draft.Proposal.RequestedDurationYears;
        entity.ConsentToPreliminaryAssessment = draft.ConsentToPreliminaryAssessment;
        entity.UpdatedAtUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<LeaseInterestRequestDetailDto> SubmitAsync(
        Guid requestId,
        UpsertLeaseInterestDraftDto draft,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();
        var errors = _validator.Validate(draft, forSubmit: true);
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var prior = await _dbContext.LeaseInterestRequests
                .Include(x => x.StatusHistory)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.OwnerId == ownerId && x.SubmitIdempotencyKey == idempotencyKey,
                    cancellationToken);

            if (prior is not null)
            {
                return MapDetail(prior);
            }
        }

        var entity = await LoadOwnedRequestAsync(requestId, ownerId, cancellationToken);
        if (entity.Status != LeaseInterestRequestStatus.Draft)
        {
            if (!string.IsNullOrWhiteSpace(idempotencyKey)
                && entity.SubmitIdempotencyKey == idempotencyKey)
            {
                return MapDetail(entity);
            }

            throw new LeaseInterestInvalidStateException("This request has already been submitted.");
        }

        await EnsureParcelExistsAsync(entity.LandParcelId, cancellationToken);
        await _parcelEligibility.EnsureEligibleForLeaseInterestAsync(entity.LandParcelId, cancellationToken);

        var now = _timeProvider.GetUtcNow();
        entity.PayloadJson = LeaseInterestPayloadSerializer.Serialize(draft);
        ApplyNicFromDraft(entity, draft);
        entity.RequestedPurpose = (int)draft.Proposal.RequestedPurpose;
        entity.RequestedDurationYears = draft.Proposal.RequestedDurationYears;
        entity.ConsentToPreliminaryAssessment = draft.ConsentToPreliminaryAssessment;
        entity.SubmittedAtUtc = now;
        entity.UpdatedAtUtc = now;
        entity.SubmitIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        entity.AcknowledgementReference = LeaseInterestReferenceGenerator.Create(now);
        entity.Status = LeaseInterestRequestStatus.PendingVerification;

        AppendStatusHistory(
            entity.Id,
            LeaseInterestRequestStatus.Submitted,
            now,
            "Submitted by applicant. Preliminary expression of interest only — not a reservation or lease.",
            LeaseInterestStatusActorKind.Applicant,
            ownerId,
            true);
        AppendStatusHistory(
            entity.Id,
            LeaseInterestRequestStatus.PendingVerification,
            now,
            "Queued for officer verification. Official lease availability is unchanged.",
            LeaseInterestStatusActorKind.System,
            null,
            true);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            _dbContext.ChangeTracker.Clear();

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                var existing = await _dbContext.LeaseInterestRequests
                    .Include(x => x.StatusHistory)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.OwnerId == ownerId && x.SubmitIdempotencyKey == idempotencyKey,
                        cancellationToken);
                if (existing is not null)
                {
                    return MapDetail(existing);
                }
            }

            var reloaded = await _dbContext.LeaseInterestRequests
                .Include(x => x.StatusHistory)
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    x => x.Id == requestId && x.OwnerId == ownerId,
                    cancellationToken);
            if (reloaded is not null && reloaded.Status != LeaseInterestRequestStatus.Draft)
            {
                return MapDetail(reloaded);
            }

            throw;
        }

        await _dbContext.Entry(entity).Collection(e => e.StatusHistory).LoadAsync(cancellationToken);
        return MapDetail(entity);
    }

    public async Task<IReadOnlyList<LeaseInterestRequestSummaryDto>> ListMineAsync(
        CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();

        var rows = await _dbContext.LeaseInterestRequests
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapSummary).ToList();
    }

    public async Task<LeaseInterestRequestDetailDto> GetByIdAsync(
        Guid requestId,
        CancellationToken cancellationToken)
    {
        var ownerId = RequireOwnerId();
        var entity = await LoadOwnedRequestAsync(requestId, ownerId, cancellationToken);
        return MapDetail(entity);
    }

    private async Task EnsureParcelExistsAsync(Guid landParcelId, CancellationToken cancellationToken)
    {
        if (await _parcelRepository.GetByIdAsync(landParcelId, cancellationToken) is null)
        {
            throw new LandParcelNotFoundException(landParcelId);
        }
    }

    private async Task<LeaseInterestRequestEntity> LoadOwnedRequestAsync(
        Guid requestId,
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var entity = await _dbContext.LeaseInterestRequests
            .Include(x => x.StatusHistory)
            .FirstOrDefaultAsync(x => x.Id == requestId, cancellationToken);

        if (entity is null)
        {
            throw new LeaseInterestRequestNotFoundException(requestId);
        }

        if (entity.OwnerId != ownerId)
        {
            throw new LandIntelligenceAuthorizationException("You do not have access to this request.");
        }

        return entity;
    }

    private LeaseInterestRequestSummaryDto MapSummary(LeaseInterestRequestEntity entity) =>
        new(
            entity.Id,
            entity.LandParcelId,
            entity.Status,
            entity.AcknowledgementReference,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            entity.SubmittedAtUtc,
            (LandUseType)entity.RequestedPurpose,
            entity.RequestedDurationYears);

    private void ApplyNicFromDraft(LeaseInterestRequestEntity entity, UpsertLeaseInterestDraftDto draft)
    {
        var raw = LeaseInterestApplicantNic.SelectRawNic(draft.Applicant);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return;
        }

        if (!SriLankanNic.TryNormalize(raw, out var normalized))
        {
            return;
        }

        entity.ApplicantNicCiphertext = _nicProtector.Encrypt(normalized);
        entity.ApplicantNicLookupDigest = _nicProtector.ComputeLookupDigest(normalized);
    }

    private LeaseInterestRequestDetailDto MapDetail(LeaseInterestRequestEntity entity)
    {
        var draft = LeaseInterestPayloadSerializer.Deserialize(entity.PayloadJson);
        draft = draft with
        {
            Applicant = LeaseInterestApplicantNic.ClearNicFromApplicant(draft.Applicant),
        };
        var decryptedNic = _nicProtector.Decrypt(entity.ApplicantNicCiphertext);
        draft = draft with
        {
            Applicant = LeaseInterestApplicantNic.MergeDecryptedNic(draft.Applicant, decryptedNic),
        };

        if (entity.Status != LeaseInterestRequestStatus.Draft)
        {
            draft = draft with
            {
                Applicant = LeaseInterestApplicantNic.MaskForApiResponse(draft.Applicant),
            };
        }

        var history = entity.StatusHistory
            .Where(x => x.VisibleToApplicant)
            .OrderBy(x => x.ChangedAtUtc)
            .Select(x => new LeaseInterestStatusHistoryEntryDto(
                x.Status,
                x.ChangedAtUtc,
                x.Note,
                (LeaseInterestStatusActorKindDto)x.ActorKind,
                x.ActorId,
                x.VisibleToApplicant))
            .ToList();

        return new LeaseInterestRequestDetailDto(
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
            _costAssessment.BuildCostSummary(entity.LandParcelId));
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

    private Guid RequireOwnerId()
    {
        if (!_caller.IsAuthenticated)
        {
            throw new LandIntelligenceAuthorizationException(
                "An authenticated applicant identity is required for this operation.");
        }

        return _caller.OwnerId!.Value;
    }
}
