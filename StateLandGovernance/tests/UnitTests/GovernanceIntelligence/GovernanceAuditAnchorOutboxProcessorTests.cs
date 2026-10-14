using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.BackgroundServices;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

public class GovernanceAuditAnchorOutboxProcessorTests
{
    [Fact]
    public async Task ProcessBatch_WhenAnchorSucceeds_MarksOutboxCompletedAndSavesReceipt()
    {
        // Arrange
        var services = new ServiceCollection();
        var anchorStore = new InMemoryGovernanceAuditAnchorStore();
        var mockBlockchainService = new MockOutboxBlockchainService { Succeed = true };

        services.AddSingleton<IGovernanceAuditAnchorStore>(anchorStore);
        services.AddSingleton<IBlockchainAuditAnchorService>(mockBlockchainService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var testRecordId = Guid.NewGuid();
        var testHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        // Enqueue an outbox item
        await anchorStore.EnqueueOutboxAsync(testRecordId, testHash, EngineType.RegulatoryCompliance);

        var processor = new GovernanceAuditAnchorOutboxProcessor(
            scopeFactory,
            NullLogger<GovernanceAuditAnchorOutboxProcessor>.Instance);

        // Act - Run via reflection or trigger private ProcessPendingBatchAsync
        var method = typeof(GovernanceAuditAnchorOutboxProcessor)
            .GetMethod("ProcessPendingBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        await (Task)method.Invoke(processor, new object[] { CancellationToken.None })!;

        // Assert
        var pendingAfter = await anchorStore.GetPendingOutboxBatchAsync(10);
        Assert.Empty(pendingAfter); // Completed items are removed from pending

        var receipt = await anchorStore.GetReceiptByAuditRecordIdAsync(testRecordId);
        Assert.NotNull(receipt);
        Assert.Equal(AnchorStatus.Anchored, receipt.AnchorStatus);
    }

    [Fact]
    public async Task ProcessBatch_WhenLedgerUnavailable_RecordsFailureAndAppliesBackoff()
    {
        // Arrange
        var services = new ServiceCollection();
        var anchorStore = new InMemoryGovernanceAuditAnchorStore();
        var mockBlockchainService = new MockOutboxBlockchainService { Succeed = false };

        services.AddSingleton<IGovernanceAuditAnchorStore>(anchorStore);
        services.AddSingleton<IBlockchainAuditAnchorService>(mockBlockchainService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var testRecordId = Guid.NewGuid();
        var testHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";

        await anchorStore.EnqueueOutboxAsync(testRecordId, testHash, EngineType.GovernanceConflict);

        var processor = new GovernanceAuditAnchorOutboxProcessor(
            scopeFactory,
            NullLogger<GovernanceAuditAnchorOutboxProcessor>.Instance);

        var method = typeof(GovernanceAuditAnchorOutboxProcessor)
            .GetMethod("ProcessPendingBatchAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

        // Act
        await (Task)method.Invoke(processor, new object[] { CancellationToken.None })!;

        // Assert
        var receipt = await anchorStore.GetReceiptByAuditRecordIdAsync(testRecordId);
        Assert.NotNull(receipt);
        Assert.Equal(AnchorStatus.Unavailable, receipt.AnchorStatus);
    }

    private sealed class MockOutboxBlockchainService : IBlockchainAuditAnchorService
    {
        public bool Succeed { get; set; } = true;

        public Task<BlockchainAnchorReceipt> AnchorRecordAsync(AnchorAuditRequest request, CancellationToken cancellationToken = default)
        {
            if (Succeed)
            {
                return Task.FromResult(new BlockchainAnchorReceipt(
                    request.AuditRecordId,
                    request.RecordHash,
                    AnchorStatus.Anchored,
                    "0xtxoutbox123",
                    501,
                    DateTime.UtcNow));
            }

            return Task.FromResult(new BlockchainAnchorReceipt(
                request.AuditRecordId,
                request.RecordHash,
                AnchorStatus.Unavailable,
                null,
                null,
                null));
        }

        public Task<BlockchainAnchorReceipt?> GetAnchorReceiptAsync(Guid auditRecordId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BlockchainAnchorReceipt?>(null);
        }

        public Task<AuditIntegrityVerificationResult> VerifyAuditIntegrityAsync(Guid auditRecordId, string currentHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AuditIntegrityVerificationResult(
                auditRecordId, currentHash, null, AuditVerificationStatus.Unavailable, null, null, DateTime.UtcNow, "N/A"));
        }
    }
}
