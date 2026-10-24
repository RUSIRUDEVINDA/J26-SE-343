using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StateLandGovernance.LandIntelligence.Application.Configuration;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Application.Validators;
using StateLandGovernance.LandIntelligence.Domain.Entities;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.ValueObjects;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestRequestFindWithoutCreateTests : IDisposable
{
    private static readonly string TestKey = Convert.ToBase64String(new byte[32]);

    private readonly LandIntelligenceDbContext _db;
    private readonly Guid _ownerId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly LandParcel _parcel;
    private readonly StubParcelRepository _parcels = new();
    private readonly LeaseInterestRequestService _service;

    public LeaseInterestRequestFindWithoutCreateTests()
    {
        Environment.SetEnvironmentVariable("LEASE_INTEREST_NIC_ENCRYPTION_KEY", TestKey);
        Environment.SetEnvironmentVariable("LEASE_INTEREST_NIC_LOOKUP_KEY", TestKey);

        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new LandIntelligenceDbContext(options);

        _parcel = new LandParcel(
            new ParcelIdentifier("CAD-FIND-001", "PLAN-1"),
            new LandCategory(LandCategoryType.StateLand, "test"),
            new LandArea(1m, AreaUnit.Hectares),
            new AdministrativeLocation("Western", "Colombo", "Colombo", "GN"),
            new SpatialReference(6.9, 79.8, "EPSG:4326"),
            new LandUse(LandUseType.Agricultural, null),
            new LandCharacteristics(null, null, null));
        _parcels.Seed(_parcel);

        var protector = new NicPersonalDataProtector(
            Options.Create(new LeaseInterestPersonalDataOptions()));
        _service = new LeaseInterestRequestService(
            _db,
            new StubCaller(_ownerId),
            _parcels,
            new StubCostAssessment(),
            new LeaseInterestDraftValidator(new LeaseInterestOptions()),
            protector,
            TimeProvider.System,
            new StubEligibility());
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task FindForParcel_returns_null_without_creating()
    {
        var found = await _service.FindForParcelAsync(_parcel.Id, CancellationToken.None);

        Assert.Null(found);
        Assert.Equal(0, await _db.LeaseInterestRequests.CountAsync());
    }

    [Fact]
    public async Task GetDraftOrCreate_then_Find_and_second_create_resume_same_draft()
    {
        var first = await _service.GetDraftOrCreateAsync(_parcel.Id, CancellationToken.None);
        var found = await _service.FindForParcelAsync(_parcel.Id, CancellationToken.None);
        var second = await _service.GetDraftOrCreateAsync(_parcel.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(first.Id, found!.Id);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await _db.LeaseInterestRequests.CountAsync());
        Assert.Equal(LeaseInterestRequestStatus.Draft, found.Status);
    }

    [Fact]
    public async Task FindForParcel_returns_submitted_request_when_no_draft()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new LeaseInterestRequestEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            LandParcelId = _parcel.Id,
            Status = LeaseInterestRequestStatus.PendingVerification,
            PayloadJson = LeaseInterestPayloadSerializer.Serialize(
                new UpsertLeaseInterestDraftDto(
                    new LeaseInterestApplicantDto(
                        LeaseInterestApplicantKindDto.Individual,
                        "A",
                        "1",
                        "a@b.c",
                        "addr",
                        null,
                        null,
                        null,
                        null),
                    new LeaseInterestProposalDto(
                        LandUseType.Agricultural,
                        "act",
                        1m,
                        "2026",
                        5,
                        "sum"),
                    new LeaseInterestSupportingInfoDto(""),
                    true)),
            RequestedPurpose = (int)LandUseType.Agricultural,
            ConsentToPreliminaryAssessment = true,
            AcknowledgementReference = "LI-TEST-001",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = now,
        };
        _db.LeaseInterestRequests.Add(entity);
        await _db.SaveChangesAsync();

        var found = await _service.FindForParcelAsync(_parcel.Id, CancellationToken.None);

        Assert.NotNull(found);
        Assert.Equal(entity.Id, found!.Id);
        Assert.Equal(LeaseInterestRequestStatus.PendingVerification, found.Status);
        Assert.Equal(1, await _db.LeaseInterestRequests.CountAsync());
    }

    [Fact]
    public async Task DeleteDraft_removes_owned_draft()
    {
        var draft = await _service.GetDraftOrCreateAsync(_parcel.Id, CancellationToken.None);

        await _service.DeleteDraftAsync(draft.Id, CancellationToken.None);

        Assert.Equal(0, await _db.LeaseInterestRequests.CountAsync());
        Assert.Null(await _service.FindForParcelAsync(_parcel.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DeleteDraft_rejects_submitted_request()
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new LeaseInterestRequestEntity
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerId,
            LandParcelId = _parcel.Id,
            Status = LeaseInterestRequestStatus.PendingVerification,
            PayloadJson = "{}",
            RequestedPurpose = (int)LandUseType.Agricultural,
            ConsentToPreliminaryAssessment = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = now,
        };
        _db.LeaseInterestRequests.Add(entity);
        await _db.SaveChangesAsync();

        await Assert.ThrowsAsync<LeaseInterestInvalidStateException>(() =>
            _service.DeleteDraftAsync(entity.Id, CancellationToken.None));
    }

    private sealed class StubCaller(Guid ownerId) : ILandIntelligenceCallerContext
    {
        public Guid? OwnerId => ownerId;
    }

    private sealed class StubParcelRepository : ILandParcelRepository
    {
        private readonly Dictionary<Guid, LandParcel> _byId = new();

        public void Seed(LandParcel parcel) => _byId[parcel.Id] = parcel;

        public Task<LandParcel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_byId.TryGetValue(id, out var p) ? p : null);

        public Task<LandParcel?> GetByCadastralNumberAsync(
            string cadastralNumber,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<LandParcel?>(null);

        public Task<IReadOnlyList<LandParcel>> SearchAsync(
            LandSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LandParcel>>([]);

        public Task<int> CountSearchAsync(
            LandSearchRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task AddAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task UpdateAsync(LandParcel parcel, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<string>> ListDerivedSoilGroupOptionsAsync(
            string? district = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class StubCostAssessment : ILeaseInterestCostAssessmentService
    {
        public LeaseInterestCostSummaryDto BuildCostSummary(Guid landParcelId) =>
            new("LKR", "Test", false, []);
    }

    private sealed class StubEligibility : ILeaseInterestParcelEligibilityService
    {
        public Task EnsureEligibleForLeaseInterestAsync(
            Guid landParcelId,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
