using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Application.LeaseInterest;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestTwoOfficerHandoffTests : IDisposable
{
    private static readonly Guid VerifierId = Guid.Parse("a1111111-1111-4111-8111-111111111111");
    private static readonly Guid ValuerId = Guid.Parse("b2222222-2222-4222-8222-222222222222");
    private readonly LandIntelligenceDbContext _db;
    private readonly Guid _requestId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 24, 12, 0, 0, TimeSpan.Zero));

    public LeaseInterestTwoOfficerHandoffTests()
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new LandIntelligenceDbContext(options);
        SeedAwaitingValuation(VerifierId);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public void WorkflowRules_same_officer_can_value_after_verification()
    {
        Assert.True(
            LeaseInterestOfficerWorkflowRules.CanOfficerRecordValuation(
                LeaseInterestRequestStatus.AwaitingValuation,
                false,
                VerifierId,
                VerifierId));
    }

    [Fact]
    public async Task ValuationService_same_officer_refers_to_authority()
    {
        var service = new LeaseInterestValuationService(
            _db,
            new StubOfficer(VerifierId),
            new StubCaller(Guid.NewGuid()),
            _clock);

        await service.RecordAsync(
            _requestId,
            ValidInput(500_000m),
            "handoff-key-1",
            CancellationToken.None);

        var entity = await _db.LeaseInterestRequests.SingleAsync();
        Assert.Equal(LeaseInterestRequestStatus.ReferredToAuthority, entity.Status);
        Assert.Equal(1, await _db.LeaseInterestValuations.CountAsync());
    }

    [Fact]
    public async Task ValuationService_different_officer_refers_to_authority()
    {
        var service = new LeaseInterestValuationService(
            _db,
            new StubOfficer(ValuerId),
            new StubCaller(Guid.NewGuid()),
            _clock);

        await service.RecordAsync(
            _requestId,
            ValidInput(500_000m),
            "handoff-key-2",
            CancellationToken.None);

        var entity = await _db.LeaseInterestRequests.SingleAsync();
        Assert.Equal(LeaseInterestRequestStatus.ReferredToAuthority, entity.Status);
        Assert.Equal(1, await _db.LeaseInterestValuations.CountAsync());
    }

    [Fact]
    public async Task ValuationService_second_save_is_rejected()
    {
        var service = new LeaseInterestValuationService(
            _db,
            new StubOfficer(ValuerId),
            new StubCaller(Guid.NewGuid()),
            _clock);

        await service.RecordAsync(
            _requestId,
            ValidInput(500_000m),
            "handoff-key-3",
            CancellationToken.None);

        await Assert.ThrowsAsync<LeaseInterestInvalidStateException>(() =>
            service.RecordAsync(
                _requestId,
                ValidInput(600_000m),
                "handoff-key-4",
                CancellationToken.None));
    }

    private void SeedAwaitingValuation(Guid verifierId)
    {
        var now = _clock.GetUtcNow();
        _db.LeaseInterestRequests.Add(new LeaseInterestRequestEntity
        {
            Id = _requestId,
            OwnerId = Guid.NewGuid(),
            LandParcelId = Guid.NewGuid(),
            Status = LeaseInterestRequestStatus.AwaitingValuation,
            VerificationCompletedByOfficerId = verifierId,
            PayloadJson = "{}",
            RequestedPurpose = (int)LandUseType.Agricultural,
            ConsentToPreliminaryAssessment = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = now,
        });
        _db.SaveChanges();
    }

    private static RecordLeaseInterestValuationDto ValidInput(decimal amount) =>
        new()
        {
            FormType = "AgriculturalCommercial",
            AssessedLandValueLkr = amount,
            Worksheet = new LeaseWorksheetDetailsDto
            {
                LeaseFileNumber = "LF-HO",
                DivisionalSecretariat = "Colombo",
            },
        };

    private sealed class StubOfficer(Guid officerId) : ILeaseInterestOfficerActorContext
    {
        public Guid? OfficerActorId => officerId;
    }

    private sealed class StubCaller(Guid ownerId) : ILandIntelligenceCallerContext
    {
        public Guid? OwnerId => ownerId;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
