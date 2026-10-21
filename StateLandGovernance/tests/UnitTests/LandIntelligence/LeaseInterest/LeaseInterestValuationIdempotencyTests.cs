using Microsoft.EntityFrameworkCore;
using StateLandGovernance.LandIntelligence.Application.DTOs;
using StateLandGovernance.LandIntelligence.Application.Interfaces;
using StateLandGovernance.LandIntelligence.Domain.Enums;
using StateLandGovernance.LandIntelligence.Domain.Exceptions;
using StateLandGovernance.LandIntelligence.Infrastructure.LeaseInterest;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence;
using StateLandGovernance.LandIntelligence.Infrastructure.Persistence.Entities;
using ValidationException = StateLandGovernance.LandIntelligence.Application.Interfaces.ValidationException;

namespace StateLandGovernance.UnitTests.LandIntelligence.LeaseInterest;

public sealed class LeaseInterestValuationIdempotencyTests : IDisposable
{
    private readonly LandIntelligenceDbContext _db;
    private readonly Guid _officerId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private readonly Guid _requestId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
    private readonly Guid _parcelId = Guid.Parse("99999999-8888-7777-6666-555555555555");
    private readonly LeaseInterestValuationService _service;
    private readonly FixedClock _clock = new(new DateTimeOffset(2026, 10, 18, 6, 0, 0, TimeSpan.Zero));

    public LeaseInterestValuationIdempotencyTests()
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new LandIntelligenceDbContext(options);
        SeedReferredRequest();
        _service = new LeaseInterestValuationService(
            _db,
            new StubOfficer(_officerId),
            new StubCaller(Guid.NewGuid()),
            _clock);
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task RecordAsync_first_save_creates_revision_one_and_keeps_referred_status()
    {
        var recorded = await _service.RecordAsync(
            _requestId,
            ValidInput(1_250_000m),
            "save-attempt-1",
            CancellationToken.None);

        Assert.Equal(1, recorded.Revision);
        Assert.Equal("save-attempt-1", recorded.IdempotencyKey);
        Assert.Equal(1, await _db.LeaseInterestValuations.CountAsync());
        Assert.Equal(
            LeaseInterestRequestStatus.ReferredToAuthority,
            (await _db.LeaseInterestRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecordAsync_same_idempotency_key_returns_previous_valuation_without_new_revision()
    {
        var first = await _service.RecordAsync(
            _requestId,
            ValidInput(1_000_000m),
            "retry-safe-key",
            CancellationToken.None);

        var replay = await _service.RecordAsync(
            _requestId,
            ValidInput(9_999_999m),
            "retry-safe-key",
            CancellationToken.None);

        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(first.Revision, replay.Revision);
        Assert.Equal(1_000_000m, replay.AssessedLandValueLkr);
        Assert.Equal(1, await _db.LeaseInterestValuations.CountAsync());
        Assert.Equal(
            LeaseInterestRequestStatus.ReferredToAuthority,
            (await _db.LeaseInterestRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecordAsync_two_distinct_keys_allocate_sequential_revisions()
    {
        var first = await _service.RecordAsync(
            _requestId,
            ValidInput(100m),
            "key-a",
            CancellationToken.None);
        var second = await _service.RecordAsync(
            _requestId,
            ValidInput(200m),
            "key-b",
            CancellationToken.None);

        Assert.Equal(1, first.Revision);
        Assert.Equal(2, second.Revision);
        Assert.Equal(2, await _db.LeaseInterestValuations.CountAsync());
        Assert.Equal(
            LeaseInterestRequestStatus.ReferredToAuthority,
            (await _db.LeaseInterestRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecordAsync_simultaneous_new_saves_do_not_overwrite_and_keep_status()
    {
        var dbName = Guid.NewGuid().ToString();
        await using var dbA = CreateDb(dbName);
        await using var dbB = CreateDb(dbName);
        await using var dbVerify = CreateDb(dbName);
        SeedReferredRequest(dbA);

        var serviceA = new LeaseInterestValuationService(
            dbA, new StubOfficer(_officerId), new StubCaller(Guid.NewGuid()), _clock);
        var serviceB = new LeaseInterestValuationService(
            dbB, new StubOfficer(_officerId), new StubCaller(Guid.NewGuid()), _clock);

        var results = await Task.WhenAll(
            serviceA.RecordAsync(_requestId, ValidInput(300m), "parallel-0", CancellationToken.None),
            serviceB.RecordAsync(_requestId, ValidInput(301m), "parallel-1", CancellationToken.None));

        Assert.Equal(2, results.Select(x => x.Id).Distinct().Count());
        Assert.Equal(2, results.Select(x => x.IdempotencyKey).Distinct().Count());
        Assert.Equal(
            new[] { 1, 2 },
            results.Select(x => x.Revision).OrderBy(x => x).ToArray());
        Assert.Equal(2, await dbVerify.LeaseInterestValuations.CountAsync());
        Assert.Equal(
            LeaseInterestRequestStatus.ReferredToAuthority,
            (await dbVerify.LeaseInterestRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecordAsync_database_conflict_response_is_controlled_invalid_state()
    {
        // Mirror the exhausted-retry conflict message the service surfaces to the API layer (HTTP 409).
        var conflict = new LeaseInterestInvalidStateException(
            "Another valuation was recorded at the same time. Refresh and try again with a new save attempt.");

        Assert.Contains("same time", conflict.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Refresh and try again", conflict.Message, StringComparison.OrdinalIgnoreCase);

        // Existing revision must never be overwritten when a later save fails/conflicts.
        var first = await _service.RecordAsync(
            _requestId,
            ValidInput(750_000m),
            "keep-original",
            CancellationToken.None);
        Assert.Equal(1, first.Revision);
        Assert.Equal(750_000m, first.AssessedLandValueLkr);

        var replay = await _service.RecordAsync(
            _requestId,
            ValidInput(1m),
            "keep-original",
            CancellationToken.None);
        Assert.Equal(first.Id, replay.Id);
        Assert.Equal(750_000m, replay.AssessedLandValueLkr);
        Assert.Equal(
            LeaseInterestRequestStatus.ReferredToAuthority,
            (await _db.LeaseInterestRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task RecordAsync_missing_idempotency_key_is_validation_error()
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RecordAsync(_requestId, ValidInput(100m), null, CancellationToken.None));
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.RecordAsync(_requestId, ValidInput(100m), "  ", CancellationToken.None));
        Assert.Equal(0, await _db.LeaseInterestValuations.CountAsync());
    }

    private void SeedReferredRequest(LandIntelligenceDbContext? db = null)
    {
        var target = db ?? _db;
        var now = _clock.GetUtcNow();
        target.LeaseInterestRequests.Add(new LeaseInterestRequestEntity
        {
            Id = _requestId,
            OwnerId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            LandParcelId = _parcelId,
            Status = LeaseInterestRequestStatus.ReferredToAuthority,
            PayloadJson = "{}",
            RequestedPurpose = (int)LandUseType.Agricultural,
            ConsentToPreliminaryAssessment = true,
            AcknowledgementReference = "LI-VAL-001",
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            SubmittedAtUtc = now,
        });
        target.SaveChanges();
    }

    private static LandIntelligenceDbContext CreateDb(string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<LandIntelligenceDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
        return new LandIntelligenceDbContext(options);
    }

    private static RecordLeaseInterestValuationDto ValidInput(decimal amount) =>
        new()
        {
            FormType = "AgriculturalCommercial",
            AssessedLandValueLkr = amount,
            Worksheet = new LeaseWorksheetDetailsDto
            {
                LeaseFileNumber = "LF-100",
                DivisionalSecretariat = "Colombo",
                PreviousAssessedValueLkr = 50m,
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
