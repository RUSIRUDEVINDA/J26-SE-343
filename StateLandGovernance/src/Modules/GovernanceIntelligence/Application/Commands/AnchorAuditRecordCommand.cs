using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace StateLandGovernance.GovernanceIntelligence.Application.Commands;

public sealed record AnchorAuditRecordCommand(Guid AuditRecordId);

public sealed class AnchorAuditRecordCommandHandler
{
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly IGovernanceAuditAnchorStore _anchorStore;
    private readonly IBlockchainAuditAnchorService _anchorService;

    public AnchorAuditRecordCommandHandler(
        IGovernanceAuditRepository auditRepository,
        IGovernanceAuditAnchorStore anchorStore,
        IBlockchainAuditAnchorService anchorService)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _anchorStore = anchorStore ?? throw new ArgumentNullException(nameof(anchorStore));
        _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
    }

    public async Task<BlockchainAnchorReceipt> HandleAsync(
        AnchorAuditRecordCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var auditRecord = await _auditRepository.GetByIdAsync(command.AuditRecordId, cancellationToken)
            ?? throw new ArgumentException($"Audit record with ID {command.AuditRecordId} not found.", nameof(command));

        var canonicalHash = GovernanceAuditHasher.ComputeCanonicalHash(auditRecord);

        // Check if an anchor receipt already exists
        var existingReceipt = await _anchorStore.GetReceiptByAuditRecordIdAsync(command.AuditRecordId, cancellationToken);
        if (existingReceipt != null && existingReceipt.AnchorStatus == AnchorStatus.Anchored)
        {
            return existingReceipt;
        }

        var anchorRequest = new AnchorAuditRequest(
            AuditRecordId: auditRecord.Id,
            RecordHash: canonicalHash,
            EngineType: auditRecord.EngineType.ToString(),
            RecordVersion: GovernanceAuditHasher.CanonicalFormatVersion
        );

        // Attempt direct anchor dispatch
        var receipt = await _anchorService.AnchorRecordAsync(anchorRequest, cancellationToken);

        // If not directly anchored (e.g. offline/pending), ensure it is enqueued in the outbox for async background retry
        if (receipt.AnchorStatus != AnchorStatus.Anchored)
        {
            await _anchorStore.EnqueueOutboxAsync(
                auditRecord.Id,
                canonicalHash,
                auditRecord.EngineType,
                GovernanceAuditHasher.CanonicalFormatVersion,
                cancellationToken);
        }

        await _anchorStore.SaveReceiptAsync(receipt, cancellationToken: cancellationToken);

        return receipt;
    }
}
