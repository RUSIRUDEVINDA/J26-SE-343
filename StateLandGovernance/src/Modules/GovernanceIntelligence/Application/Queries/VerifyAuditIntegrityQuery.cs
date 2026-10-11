using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record VerifyAuditIntegrityQuery(Guid AuditRecordId);

public sealed class VerifyAuditIntegrityQueryHandler
{
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly IGovernanceAuditAnchorStore _anchorStore;
    private readonly IBlockchainAuditAnchorService _anchorService;
    private readonly TimeProvider _timeProvider;

    public VerifyAuditIntegrityQueryHandler(
        IGovernanceAuditRepository auditRepository,
        IGovernanceAuditAnchorStore anchorStore,
        IBlockchainAuditAnchorService anchorService,
        TimeProvider? timeProvider = null)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _anchorStore = anchorStore ?? throw new ArgumentNullException(nameof(anchorStore));
        _anchorService = anchorService ?? throw new ArgumentNullException(nameof(anchorService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<GovernanceAuditVerificationReceiptDto?> HandleAsync(
        VerifyAuditIntegrityQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var auditRecord = await _auditRepository.GetByIdAsync(query.AuditRecordId, cancellationToken);
        if (auditRecord is null)
        {
            return null;
        }

        var currentHash = GovernanceAuditHasher.ComputeCanonicalHash(auditRecord);
        var verificationResult = await _anchorService.VerifyAuditIntegrityAsync(auditRecord.Id, currentHash, cancellationToken);

        var existingReceipt = await _anchorStore.GetReceiptByAuditRecordIdAsync(auditRecord.Id, cancellationToken);

        // Update local receipt with verification timestamp and status if receipt exists
        if (existingReceipt != null)
        {
            await _anchorStore.SaveReceiptAsync(
                existingReceipt,
                verificationStatus: verificationResult.VerificationStatus,
                cancellationToken: cancellationToken);
        }

        var anchorStatusStr = existingReceipt != null
            ? existingReceipt.AnchorStatus.ToString()
            : (verificationResult.VerificationStatus == AuditVerificationStatus.Match ? "Anchored" : "NotAnchored");

        return new GovernanceAuditVerificationReceiptDto(
            RecordId: auditRecord.Id,
            EngineType: auditRecord.EngineType.ToString(),
            ActionName: auditRecord.ActionName,
            GovernanceStatus: auditRecord.Status,
            DecisionRecordedAt: auditRecord.Timestamp,
            RecordHash: currentHash,
            VerificationStatus: verificationResult.VerificationStatus.ToString(),
            AnchorStatus: anchorStatusStr,
            LedgerTransactionReference: verificationResult.TransactionReference ?? existingReceipt?.TransactionReference,
            BlockNumber: existingReceipt?.BlockNumber,
            AnchoredAtUtc: verificationResult.AnchoredAtUtc ?? existingReceipt?.AnchoredAtUtc,
            VerifiedAtUtc: verificationResult.VerifiedAtUtc,
            Explanation: verificationResult.Explanation
        );
    }
}
