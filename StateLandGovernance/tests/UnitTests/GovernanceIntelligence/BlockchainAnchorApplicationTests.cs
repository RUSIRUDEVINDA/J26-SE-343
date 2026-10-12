using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.Commands;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class BlockchainAnchorApplicationTests
{
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly IGovernanceAuditAnchorStore _anchorStore;
    private readonly TestMockBlockchainService _blockchainService;
    private readonly AnchorAuditRecordCommandHandler _anchorHandler;
    private readonly VerifyAuditIntegrityQueryHandler _verifyHandler;
    private readonly GetAuditReceiptQueryHandler _receiptHandler;

    private readonly GovernanceAuditRecord _sampleRecord;

    public BlockchainAnchorApplicationTests()
    {
        _auditRepository = new InMemoryGovernanceAuditRepository();
        _anchorStore = new InMemoryGovernanceAuditAnchorStore();
        _blockchainService = new TestMockBlockchainService();

        _anchorHandler = new AnchorAuditRecordCommandHandler(_auditRepository, _anchorStore, _blockchainService);
        _verifyHandler = new VerifyAuditIntegrityQueryHandler(_auditRepository, _anchorStore, _blockchainService);
        _receiptHandler = new GetAuditReceiptQueryHandler(_auditRepository, _anchorStore);

        _sampleRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "EvaluateLeaseDuration",
            "Satisfied",
            "Lease duration 30 years verified.");

        _auditRepository.AddAsync(_sampleRecord).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task AnchorRecord_WhenSuccessful_PersistsReceiptWithAnchoredStatus()
    {
        // Act
        var receipt = await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Assert
        Assert.NotNull(receipt);
        Assert.Equal(_sampleRecord.Id, receipt.AuditRecordId);
        Assert.Equal(AnchorStatus.Anchored, receipt.AnchorStatus);
        Assert.NotNull(receipt.TransactionReference);
        Assert.NotNull(receipt.BlockNumber);

        // Verify stored in anchor store
        var stored = await _anchorStore.GetReceiptByAuditRecordIdAsync(_sampleRecord.Id);
        Assert.NotNull(stored);
        Assert.Equal(AnchorStatus.Anchored, stored.AnchorStatus);
    }

    [Fact]
    public async Task AnchorRecord_WhenAlreadyAnchored_IsIdempotentAndReturnsExistingReceipt()
    {
        // Arrange
        var firstReceipt = await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Act - call again
        var secondReceipt = await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Assert
        Assert.Equal(firstReceipt.TransactionReference, secondReceipt.TransactionReference);
        Assert.Equal(firstReceipt.BlockNumber, secondReceipt.BlockNumber);
        Assert.Equal(1, _blockchainService.AnchorCallCount); // Did not make redundant blockchain call
    }

    [Fact]
    public async Task AnchorRecord_WhenLedgerUnavailable_ReturnsUnavailableStatusAndEnqueuesOutbox()
    {
        // Arrange
        _blockchainService.SimulateUnavailable = true;

        // Act - Must NOT throw; returns Unavailable receipt and queues outbox
        var receipt = await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Assert
        Assert.Equal(AnchorStatus.Unavailable, receipt.AnchorStatus);

        // Check that outbox item was enqueued for background retry
        var pendingOutbox = await _anchorStore.GetPendingOutboxBatchAsync(10);
        Assert.Single(pendingOutbox);
        Assert.Equal(_sampleRecord.Id, pendingOutbox[0].AuditRecordId);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashMatches_ReturnsMatchStatus()
    {
        // Arrange - Anchor first
        await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Act
        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(_sampleRecord.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Match", result.VerificationStatus);
        Assert.Equal(result.RecordHash, _blockchainService.AnchoredHash);
        Assert.Contains("matches", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenHashDiverges_ReturnsMismatchStatusWithoutDefamatoryLanguage()
    {
        // Arrange - Anchor with original hash
        await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Alter blockchain anchor to simulate mismatch
        _blockchainService.SimulateTamperedLedgerHash = "0000000000000000000000000000000000000000000000000000000000000000";

        // Act
        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(_sampleRecord.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Mismatch", result.VerificationStatus);
        Assert.Contains("do not match", result.Explanation, StringComparison.OrdinalIgnoreCase);

        // Ethical language check: must NOT describe mismatch as fraud, corruption, or crime
        Assert.DoesNotContain("fraud", result.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("corruption", result.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("guilty", result.Explanation, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("crime", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenNotAnchored_ReturnsNotAnchoredStatus()
    {
        // Create an unanchored record
        var unanchored = GovernanceAuditRecord.Create(
            EngineType.Consensus, "ConsensusCheck", "Approved", "Consensus reached");
        await _auditRepository.AddAsync(unanchored);

        // Act
        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(unanchored.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("NotAnchored", result.VerificationStatus);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenServiceUnavailable_ReturnsUnavailableStatusWithoutThrowing()
    {
        // Arrange
        _blockchainService.SimulateUnavailable = true;

        // Act
        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(_sampleRecord.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Unavailable", result.VerificationStatus);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenLegacyPrecisionCannotBeReconstructed_ReturnsLegacyUnverifiable()
    {
        var persistedRecord = GovernanceAuditRecord.Rehydrate(
            Guid.NewGuid(),
            EngineType.RegulatoryCompliance,
            "LegacyFixture",
            "Satisfied",
            "Fictional legacy fixture.",
            new DateTime(2026, 10, 9, 10, 30, 0, 123, DateTimeKind.Utc).AddTicks(4560),
            GovernanceAuditHasher.AuditV1);
        var originalPrePersistenceRecord = GovernanceAuditRecord.Rehydrate(
            persistedRecord.Id,
            persistedRecord.EngineType,
            persistedRecord.ActionName,
            persistedRecord.Status,
            persistedRecord.Details,
            persistedRecord.Timestamp.AddTicks(7),
            GovernanceAuditHasher.AuditV1);
        var anchoredHash = GovernanceAuditHasher.ComputeCanonicalHash(originalPrePersistenceRecord);
        await _auditRepository.AddAsync(persistedRecord);
        await _anchorStore.SaveReceiptAsync(new BlockchainAnchorReceipt(
            persistedRecord.Id, anchoredHash, AnchorStatus.Anchored, "0xlegacy", 1, DateTime.UtcNow));
        _blockchainService.ForceAnchoredHash(anchoredHash);

        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(persistedRecord.Id));

        Assert.NotNull(result);
        Assert.Equal("LegacyUnverifiable", result.VerificationStatus);
        Assert.Contains("not evidence of tampering", result.Explanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenAuditV1IsMicrosecondAligned_ReturnsMatch()
    {
        var record = GovernanceAuditRecord.Rehydrate(
            Guid.NewGuid(), EngineType.RegulatoryCompliance, "LegacyAligned", "Satisfied", "Fictional legacy fixture.",
            new DateTime(2026, 10, 9, 10, 30, 0, 123, DateTimeKind.Utc).AddTicks(4560), GovernanceAuditHasher.AuditV1);
        await _auditRepository.AddAsync(record);
        await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(record.Id));

        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(record.Id));

        Assert.NotNull(result);
        Assert.Equal("Match", result.VerificationStatus);
    }

    [Fact]
    public async Task VerifyIntegrity_WhenVersionIsUnknown_FailsClosedWithoutLedgerCall()
    {
        var record = GovernanceAuditRecord.Rehydrate(
            Guid.NewGuid(), EngineType.RegulatoryCompliance, "UnknownVersion", "Satisfied", "Fictional fixture.",
            DateTime.UtcNow, "AUDIT-V99");
        await _auditRepository.AddAsync(record);
        var callsBeforeVerification = _blockchainService.VerificationCallCount;

        var result = await _verifyHandler.HandleAsync(new VerifyAuditIntegrityQuery(record.Id));

        Assert.NotNull(result);
        Assert.Equal("UnsupportedVersion", result.VerificationStatus);
        Assert.Equal(callsBeforeVerification, _blockchainService.VerificationCallCount);
    }

    [Fact]
    public async Task GetReceipt_WhenRecordExists_ReturnsReceiptDto()
    {
        // Arrange - Anchor first
        var receipt = await _anchorHandler.HandleAsync(new AnchorAuditRecordCommand(_sampleRecord.Id));

        // Act
        var result = await _receiptHandler.HandleAsync(new GetAuditReceiptQuery(_sampleRecord.Id));

        // Assert
        Assert.NotNull(result);
        Assert.Equal(_sampleRecord.Id, result.RecordId);
        Assert.Equal("Anchored", result.AnchorStatus);
        Assert.Equal(receipt.TransactionReference, result.LedgerTransactionReference);
    }

    [Fact]
    public async Task GetReceipt_WhenNonExistentRecord_ReturnsNull()
    {
        var result = await _receiptHandler.HandleAsync(new GetAuditReceiptQuery(Guid.NewGuid()));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetReceipt_WhenPersistedVerificationExists_ReturnsItWithoutLedgerCall()
    {
        var verifiedAt = new DateTime(2026, 10, 11, 12, 30, 0, DateTimeKind.Utc);
        var hash = GovernanceAuditHasher.ComputeCanonicalHash(_sampleRecord, _sampleRecord.HashVersion);
        await _anchorStore.SaveReceiptAsync(new BlockchainAnchorReceipt(
            _sampleRecord.Id, hash, AnchorStatus.Anchored, "0xpersisted", 12945,
            verifiedAt.AddMinutes(-1), AuditVerificationStatus.Match, verifiedAt));
        var callsBeforeRead = _blockchainService.VerificationCallCount;

        var result = await _receiptHandler.HandleAsync(new GetAuditReceiptQuery(_sampleRecord.Id));

        Assert.NotNull(result);
        Assert.Equal("Match", result.VerificationStatus);
        Assert.Equal(verifiedAt, result.VerifiedAtUtc);
        Assert.Equal(callsBeforeRead, _blockchainService.VerificationCallCount);
    }

    [Fact]
    public async Task GetReceipt_WhenVerificationIsPending_PreservesPendingAndNullTimestamp()
    {
        var hash = GovernanceAuditHasher.ComputeCanonicalHash(_sampleRecord, _sampleRecord.HashVersion);
        await _anchorStore.SaveReceiptAsync(new BlockchainAnchorReceipt(
            _sampleRecord.Id, hash, AnchorStatus.Anchored, "0xpersisted", 1,
            DateTime.UtcNow, AuditVerificationStatus.Pending, null));

        var result = await _receiptHandler.HandleAsync(new GetAuditReceiptQuery(_sampleRecord.Id));

        Assert.NotNull(result);
        Assert.Equal("Pending", result.VerificationStatus);
        Assert.Null(result.VerifiedAtUtc);
    }

    // Test double for IBlockchainAuditAnchorService
    private sealed class TestMockBlockchainService : IBlockchainAuditAnchorService
    {
        public bool SimulateUnavailable { get; set; }
        public string? SimulateTamperedLedgerHash { get; set; }
        public string? AnchoredHash { get; private set; }
        public int AnchorCallCount { get; private set; }
        public int VerificationCallCount { get; private set; }

        public void ForceAnchoredHash(string hash) => AnchoredHash = hash;

        public Task<BlockchainAnchorReceipt> AnchorRecordAsync(AnchorAuditRequest request, CancellationToken cancellationToken = default)
        {
            AnchorCallCount++;
            if (SimulateUnavailable)
            {
                return Task.FromResult(new BlockchainAnchorReceipt(
                    request.AuditRecordId, request.RecordHash, AnchorStatus.Unavailable, null, null, null));
            }

            AnchoredHash = request.RecordHash;

            return Task.FromResult(new BlockchainAnchorReceipt(
                request.AuditRecordId,
                request.RecordHash,
                AnchorStatus.Anchored,
                "0x1111111111111111111111111111111111111111111111111111111111111111",
                1001,
                DateTime.UtcNow));
        }

        public Task<BlockchainAnchorReceipt?> GetAnchorReceiptAsync(Guid auditRecordId, CancellationToken cancellationToken = default)
        {
            if (SimulateUnavailable)
            {
                return Task.FromResult<BlockchainAnchorReceipt?>(new BlockchainAnchorReceipt(
                    auditRecordId, string.Empty, AnchorStatus.Unavailable, null, null, null));
            }

            if (AnchoredHash == null) return Task.FromResult<BlockchainAnchorReceipt?>(null);

            return Task.FromResult<BlockchainAnchorReceipt?>(new BlockchainAnchorReceipt(
                auditRecordId, AnchoredHash, AnchorStatus.Anchored, "0x1111111111111111111111111111111111111111111111111111111111111111", 1001, DateTime.UtcNow));
        }

        public Task<AuditIntegrityVerificationResult> VerifyAuditIntegrityAsync(Guid auditRecordId, string currentHash, CancellationToken cancellationToken = default)
        {
            VerificationCallCount++;
            if (SimulateUnavailable)
            {
                return Task.FromResult(new AuditIntegrityVerificationResult(
                    auditRecordId, currentHash, null, AuditVerificationStatus.Unavailable, null, null, DateTime.UtcNow, "Service unavailable."));
            }

            if (AnchoredHash == null)
            {
                return Task.FromResult(new AuditIntegrityVerificationResult(
                    auditRecordId, currentHash, null, AuditVerificationStatus.NotAnchored, null, null, DateTime.UtcNow, "Record not anchored."));
            }

            var targetHash = SimulateTamperedLedgerHash ?? AnchoredHash;
            var isMatch = currentHash.Equals(targetHash, StringComparison.OrdinalIgnoreCase);

            return Task.FromResult(new AuditIntegrityVerificationResult(
                auditRecordId,
                currentHash,
                targetHash,
                isMatch ? AuditVerificationStatus.Match : AuditVerificationStatus.Mismatch,
                "0x1111111111111111111111111111111111111111111111111111111111111111",
                DateTime.UtcNow,
                DateTime.UtcNow,
                isMatch
                    ? "Off-chain digest matches immutable blockchain ledger anchor."
                    : "The current off-chain record does not match the anchored record version."));
        }
    }
}
