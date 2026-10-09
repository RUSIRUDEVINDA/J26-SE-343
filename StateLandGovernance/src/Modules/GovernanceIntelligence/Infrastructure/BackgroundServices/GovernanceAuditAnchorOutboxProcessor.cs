using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.BackgroundServices;

/// <summary>
/// Reliable background processor that polls the PostgreSQL audit anchor outbox and dispatches
/// unanchored digests to the blockchain microservice asynchronously. Ensures authoritative evaluations
/// complete immediately without blocking on blockchain confirmation.
/// </summary>
public sealed class GovernanceAuditAnchorOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<GovernanceAuditAnchorOutboxProcessor> _logger;
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);

    public GovernanceAuditAnchorOutboxProcessor(
        IServiceScopeFactory scopeFactory,
        ILogger<GovernanceAuditAnchorOutboxProcessor> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Governance Audit Anchor Outbox Processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning("Unexpected error during outbox batch processing: {Message}", ex.Message);
            }

            try
            {
                await Task.Delay(_pollingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Governance Audit Anchor Outbox Processor stopped.");
    }

    private async Task ProcessPendingBatchAsync(CancellationToken stoppingToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var anchorStore = scope.ServiceProvider.GetRequiredService<IGovernanceAuditAnchorStore>();
        var anchorService = scope.ServiceProvider.GetRequiredService<IBlockchainAuditAnchorService>();

        var pendingItems = await anchorStore.GetPendingOutboxBatchAsync(10, stoppingToken);
        if (pendingItems.Count == 0)
        {
            return;
        }

        foreach (var item in pendingItems)
        {
            if (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            var request = new AnchorAuditRequest(
                AuditRecordId: item.AuditRecordId,
                RecordHash: item.RecordHash,
                EngineType: item.EngineType.ToString(),
                RecordVersion: item.RecordVersion
            );

            var receipt = await anchorService.AnchorRecordAsync(request, stoppingToken);

            if (receipt.AnchorStatus == AnchorStatus.Anchored)
            {
                await anchorStore.SaveReceiptAsync(receipt, cancellationToken: stoppingToken);
                await anchorStore.MarkOutboxCompletedAsync(item.Id, stoppingToken);
                _logger.LogInformation("Successfully anchored audit record {RecordId} with tx {TxRef}", item.AuditRecordId, receipt.TransactionReference);
            }
            else
            {
                // Exponential backoff: 5s, 10s, 20s, 40s... capped at 300s
                var retryDelay = Math.Min(300, (int)Math.Pow(2, Math.Min(item.RetryCount, 6)) * 5);
                var errorMsg = receipt.AnchorStatus == AnchorStatus.Unavailable
                    ? "Blockchain service or ledger unavailable."
                    : "Ledger transaction rejected or failed.";

                await anchorStore.RecordOutboxFailureAsync(item.Id, errorMsg, retryDelay, stoppingToken);
                await anchorStore.SaveReceiptAsync(receipt, failureReason: errorMsg, cancellationToken: stoppingToken);
            }
        }
    }
}
