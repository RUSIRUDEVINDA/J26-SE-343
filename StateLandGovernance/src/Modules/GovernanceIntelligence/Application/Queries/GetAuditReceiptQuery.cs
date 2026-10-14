using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;
using StateLandGovernance.GovernanceIntelligence.Domain.Services;

namespace StateLandGovernance.GovernanceIntelligence.Application.Queries;

public sealed record GetAuditReceiptQuery(Guid AuditRecordId);

public sealed class GetAuditReceiptQueryHandler
{
    private readonly IGovernanceAuditRepository _auditRepository;
    private readonly IGovernanceAuditAnchorStore _anchorStore;

    public GetAuditReceiptQueryHandler(
        IGovernanceAuditRepository auditRepository,
        IGovernanceAuditAnchorStore anchorStore)
    {
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _anchorStore = anchorStore ?? throw new ArgumentNullException(nameof(anchorStore));
    }

    public async Task<GovernanceAuditVerificationReceiptDto?> HandleAsync(
        GetAuditReceiptQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var auditRecord = await _auditRepository.GetByIdAsync(query.AuditRecordId, cancellationToken);
        if (auditRecord is null)
        {
            return null;
        }

        var receipt = await _anchorStore.GetReceiptByAuditRecordIdAsync(query.AuditRecordId, cancellationToken);

        if (!GovernanceAuditHasher.IsSupportedVersion(auditRecord.HashVersion))
        {
            return new GovernanceAuditVerificationReceiptDto(
                RecordId: auditRecord.Id,
                EngineType: auditRecord.EngineType.ToString(),
                ActionName: auditRecord.ActionName,
                GovernanceStatus: auditRecord.Status,
                DecisionRecordedAt: auditRecord.Timestamp,
                RecordHash: receipt?.RecordHash ?? string.Empty,
                VerificationStatus: AuditVerificationStatus.UnsupportedVersion.ToString(),
                AnchorStatus: (receipt?.AnchorStatus ?? AnchorStatus.NotAnchored).ToString(),
                LedgerTransactionReference: receipt?.TransactionReference,
                BlockNumber: receipt?.BlockNumber,
                AnchoredAtUtc: receipt?.AnchoredAtUtc,
                VerifiedAtUtc: null,
                Explanation: $"Audit hash version '{auditRecord.HashVersion}' is unsupported; verification is fail-closed.",
                RecordVersion: auditRecord.HashVersion);
        }

        var currentHash = GovernanceAuditHasher.ComputeCanonicalHash(auditRecord, auditRecord.HashVersion);

        var anchorStatus = receipt?.AnchorStatus ?? AnchorStatus.NotAnchored;
        var verificationStatus = receipt?.VerificationStatus.ToString()
            ?? AuditVerificationStatus.NotAnchored.ToString();

        return new GovernanceAuditVerificationReceiptDto(
            RecordId: auditRecord.Id,
            EngineType: auditRecord.EngineType.ToString(),
            ActionName: auditRecord.ActionName,
            GovernanceStatus: auditRecord.Status,
            DecisionRecordedAt: auditRecord.Timestamp,
            RecordHash: currentHash,
            VerificationStatus: verificationStatus,
            AnchorStatus: anchorStatus.ToString(),
            LedgerTransactionReference: receipt?.TransactionReference,
            BlockNumber: receipt?.BlockNumber,
            AnchoredAtUtc: receipt?.AnchoredAtUtc,
            VerifiedAtUtc: receipt?.LastVerifiedAtUtc,
            Explanation: receipt != null
                ? $"Ledger receipt status: {receipt.AnchorStatus}"
                : "No blockchain anchor receipt has been recorded for this governance record.",
            RecordVersion: auditRecord.HashVersion
        );
    }
}
