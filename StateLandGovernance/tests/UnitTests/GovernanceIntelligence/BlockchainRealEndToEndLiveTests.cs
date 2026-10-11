using System;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Application.Queries;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.BackgroundServices;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;
using StateLandGovernance.GovernanceIntelligence.Infrastructure.Persistence;
using Xunit;

namespace StateLandGovernance.UnitTests.GovernanceIntelligence;

[Trait("Category", "LiveIntegration")]
[Trait("Category", "Integration")]
[Trait("Category", "LiveBlockchain")]
public sealed class BlockchainRealEndToEndLiveTests
{
    private static bool IsLiveBesuAvailable()
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var res = client.GetAsync("http://localhost:8550/health").GetAwaiter().GetResult();
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    [Fact]
    public async Task LiveEndToEnd_FullGovernanceAuditBlockchainLifecycle_MatchesAllRequirements()
    {
        // Explicit exclusion from standard unit test runs:
        // This is a live end-to-end integration test requiring Docker, a 4-node Besu QBFT network,
        // deployed GovernanceAuditRegistry contract, and the Go blockchain microservice.
        // It is excluded by default from normal unit test execution and only runs when explicitly enabled.
        var isExplicitlyEnabled = string.Equals(
            Environment.GetEnvironmentVariable("ENABLE_BLOCKCHAIN_LIVE_TESTS"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!isExplicitlyEnabled)
        {
            return;
        }

        if (!IsLiveBesuAvailable())
        {
            throw new InvalidOperationException(
                "Live blockchain integration test is explicitly enabled (ENABLE_BLOCKCHAIN_LIVE_TESTS=true), " +
                "but the Go blockchain service at http://localhost:8550 is unreachable. " +
                "Ensure Docker Besu containers and governance-blockchain-service are running.");
        }

        var services = new ServiceCollection();
        var anchorStore = new InMemoryGovernanceAuditAnchorStore();
        var auditRepository = new InMemoryGovernanceAuditRepository();

        var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:8550/") };
        var liveBlockchainService = new HttpBlockchainAuditAnchorService(
            httpClient,
            NullLogger<HttpBlockchainAuditAnchorService>.Instance
        );

        services.AddSingleton<IGovernanceAuditAnchorStore>(anchorStore);
        services.AddSingleton<IBlockchainAuditAnchorService>(liveBlockchainService);

        var serviceProvider = services.BuildServiceProvider();
        var scopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();

        var auditRecord = GovernanceAuditRecord.Create(
            EngineType.RegulatoryCompliance,
            "COMPLIANCE_EVALUATED",
            "OFFICIAL_APPROVED",
            "{\"CaseNumber\":\"CASE-LK-2026-REAL\",\"ApprovedBy\":\"Officer-42\"}"
        );

        await auditRepository.AddAsync(auditRecord);
        var recordId = auditRecord.Id;

        // Compute authoritative canonical hash in C#
        var canonicalHash = GovernanceAuditHasher.ComputeCanonicalHash(auditRecord);
        Assert.NotNull(canonicalHash);
        Assert.Equal(64, canonicalHash.Length);

        // Step 1: Enqueue in Outbox & Record initial pending receipt
        await anchorStore.EnqueueOutboxAsync(recordId, canonicalHash, EngineType.RegulatoryCompliance);
        await anchorStore.SaveReceiptAsync(new BlockchainAnchorReceipt(
            recordId,
            canonicalHash,
            AnchorStatus.Pending,
            null,
            null,
            null
        ));

        // Step 2: Background Processor triggers dispatch to Go service -> Besu QBFT
        var processor = new GovernanceAuditAnchorOutboxProcessor(
            scopeFactory,
            NullLogger<GovernanceAuditAnchorOutboxProcessor>.Instance
        );

        var processMethod = typeof(GovernanceAuditAnchorOutboxProcessor)
            .GetMethod("ProcessPendingBatchAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        await (Task)processMethod.Invoke(processor, new object[] { CancellationToken.None })!;

        // Requirement A: New audit record anchors successfully
        var receipt = await anchorStore.GetReceiptByAuditRecordIdAsync(recordId);
        Assert.NotNull(receipt);
        Assert.Equal(AnchorStatus.Anchored, receipt.AnchorStatus);
        Assert.NotNull(receipt.TransactionReference);
        Assert.NotNull(receipt.BlockNumber);
        Assert.NotNull(receipt.AnchoredAtUtc);

        // Requirement B: Retrieve the anchored record from Besu
        var getQueryHandler = new GetAuditReceiptQueryHandler(auditRepository, anchorStore);
        var queryReceipt = await getQueryHandler.HandleAsync(new GetAuditReceiptQuery(recordId), CancellationToken.None);
        Assert.NotNull(queryReceipt);
        Assert.Equal("Anchored", queryReceipt.AnchorStatus);
        Assert.Equal(receipt.TransactionReference, queryReceipt.LedgerTransactionReference);

        // Requirement C: Verify identical current record returns Match
        var verifyQueryHandler = new VerifyAuditIntegrityQueryHandler(auditRepository, anchorStore, liveBlockchainService);
        var matchResult = await verifyQueryHandler.HandleAsync(new VerifyAuditIntegrityQuery(recordId), CancellationToken.None);

        Assert.Equal("Match", matchResult.VerificationStatus);
        Assert.Equal("The current off-chain record digest matches the anchored blockchain ledger record.", matchResult.Explanation);

        // Requirement D: Modify a controlled off-chain test record and verify Mismatch (without accusatory tone)
        // Reconstitute record with same Guid using reflection on private constructor to preserve Domain immutability
        var tamperedRecord = InstantiateRecordWithExplicitId(
            recordId,
            EngineType.RegulatoryCompliance,
            "COMPLIANCE_EVALUATED",
            "UNOFFICIAL_MODIFIED",
            "{\"CaseNumber\":\"CASE-LK-2026-TAMPERED\"}",
            auditRecord.Timestamp
        );
        await auditRepository.AddAsync(tamperedRecord); // overwrite in off-chain repo

        var mismatchResult = await verifyQueryHandler.HandleAsync(new VerifyAuditIntegrityQuery(recordId), CancellationToken.None);
        Assert.Equal("Mismatch", mismatchResult.VerificationStatus);
        Assert.Equal("The current off-chain record does not match the anchored record version.", mismatchResult.Explanation);

        // Requirement E: Resubmit the same AuditRecordId + same hash and verify idempotent behavior
        var idempotentAnchor = await liveBlockchainService.AnchorRecordAsync(new AnchorAuditRequest(
            recordId,
            canonicalHash,
            "RegulatoryCompliance",
            "AUDIT-V1"
        ), CancellationToken.None);

        Assert.Equal(AnchorStatus.Anchored, idempotentAnchor.AnchorStatus);

        // Requirement F: Resubmit same AuditRecordId + different hash and verify rejection
        var tamperedHash = GovernanceAuditHasher.ComputeCanonicalHash(tamperedRecord);
        var conflictingAnchor = await liveBlockchainService.AnchorRecordAsync(new AnchorAuditRequest(
            recordId,
            tamperedHash,
            "RegulatoryCompliance",
            "AUDIT-V1"
        ), CancellationToken.None);

        Assert.Equal(AnchorStatus.Failed, conflictingAnchor.AnchorStatus);

        // Requirement G: Simulating ledger unavailable during governance evaluation
        var offlineClient = new HttpBlockchainAuditAnchorService(
            new HttpClient { BaseAddress = new Uri("http://localhost:9999/") },
            NullLogger<HttpBlockchainAuditAnchorService>.Instance
        );

        var offlineRecordId = Guid.NewGuid();
        var offlineHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        // Primary operation persists to outbox despite blockchain unavailability
        await anchorStore.EnqueueOutboxAsync(offlineRecordId, offlineHash, EngineType.RegulatoryCompliance);
        var offlineReceipt = await offlineClient.AnchorRecordAsync(new AnchorAuditRequest(
            offlineRecordId,
            offlineHash,
            "RegulatoryCompliance",
            "AUDIT-V1"
        ), CancellationToken.None);

        // Must fail safely and return Unavailable state without crashing the primary business operation
        Assert.Equal(AnchorStatus.Unavailable, offlineReceipt.AnchorStatus);

        // Requirement H: Recovery succeeds once connection restored
        var liveAnchorResult = await liveBlockchainService.AnchorRecordAsync(new AnchorAuditRequest(
            offlineRecordId,
            offlineHash,
            "RegulatoryCompliance",
            "AUDIT-V1"
        ), CancellationToken.None);

        Assert.Equal(AnchorStatus.Anchored, liveAnchorResult.AnchorStatus);
    }

    private static GovernanceAuditRecord InstantiateRecordWithExplicitId(
        Guid id,
        EngineType engineType,
        string actionName,
        string status,
        string details,
        DateTime timestamp)
    {
        var ctor = typeof(GovernanceAuditRecord).GetConstructor(
            BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            new[] { typeof(Guid), typeof(EngineType), typeof(string), typeof(string), typeof(string), typeof(DateTime) },
            null)!;

        return (GovernanceAuditRecord)ctor.Invoke(new object[] { id, engineType, actionName, status, details, timestamp });
    }
}
