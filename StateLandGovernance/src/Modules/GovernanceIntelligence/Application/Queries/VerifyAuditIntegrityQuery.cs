using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Entities;
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

        var existingReceipt = await _anchorStore.GetReceiptByAuditRecordIdAsync(auditRecord.Id, cancellationToken);
        if (!GovernanceAuditHasher.IsSupportedVersion(auditRecord.HashVersion))
        {
            return BuildReceipt(
                auditRecord,
                existingReceipt,
                string.Empty,
                AuditVerificationStatus.UnsupportedVersion,
                null,
                null,
                null,
                $"Audit record uses unsupported hash protocol version '{auditRecord.HashVersion}'. Verification was not sent to the ledger.");
        }

        var currentHash = GovernanceAuditHasher.ComputeCanonicalHash(auditRecord, auditRecord.HashVersion);
        var verificationResult = await _anchorService.VerifyAuditIntegrityAsync(auditRecord.Id, currentHash, cancellationToken);
        var storedHash = existingReceipt?.RecordHash;
        var anchoredHash = verificationResult.AnchoredHash ?? storedHash;
        var status = ResolveStatus(auditRecord.HashVersion, currentHash, storedHash, anchoredHash, verificationResult.VerificationStatus);
        var explanation = BuildExplanation(auditRecord.HashVersion, status, verificationResult.Explanation);

        // Update local receipt with verification timestamp and status if receipt exists
        if (existingReceipt != null)
        {
            await _anchorStore.SaveReceiptAsync(
                existingReceipt,
                verificationStatus: status,
                cancellationToken: cancellationToken);
        }

        return BuildReceipt(
            auditRecord,
            existingReceipt,
            currentHash,
            status,
            verificationResult.TransactionReference,
            verificationResult.AnchoredAtUtc,
            verificationResult.VerifiedAtUtc,
            explanation);
    }

    private static AuditVerificationStatus ResolveStatus(
        string version,
        string recomputedHash,
        string? storedHash,
        string? anchoredHash,
        AuditVerificationStatus ledgerStatus)
    {
        if (ledgerStatus is AuditVerificationStatus.Unavailable or AuditVerificationStatus.NotAnchored or AuditVerificationStatus.Pending)
        {
            return ledgerStatus;
        }

        if (string.IsNullOrWhiteSpace(storedHash) || string.IsNullOrWhiteSpace(anchoredHash))
        {
            return AuditVerificationStatus.NotAnchored;
        }

        if (!string.Equals(storedHash, anchoredHash, StringComparison.Ordinal))
        {
            return AuditVerificationStatus.Mismatch;
        }

        if (string.Equals(recomputedHash, storedHash, StringComparison.Ordinal))
        {
            return AuditVerificationStatus.Match;
        }

        return version == GovernanceAuditHasher.AuditV1
            ? AuditVerificationStatus.LegacyUnverifiable
            : AuditVerificationStatus.Mismatch;
    }

    private static string BuildExplanation(string version, AuditVerificationStatus status, string ledgerExplanation) => status switch
    {
        AuditVerificationStatus.LegacyUnverifiable =>
            "The stored AUDIT-V1 digest agrees with the ledger, but it cannot be reproduced from PostgreSQL microsecond-precision data. This result is not evidence of tampering.",
        AuditVerificationStatus.UnsupportedVersion =>
            "The audit hash protocol version is unsupported; verification failed closed and no ledger comparison was attempted.",
        AuditVerificationStatus.Mismatch when version == GovernanceAuditHasher.AuditV2 =>
            "The AUDIT-V2 recomputed, stored, and anchored digests do not match.",
        _ => ledgerExplanation
    };

    private static GovernanceAuditVerificationReceiptDto BuildReceipt(
        GovernanceAuditRecord auditRecord,
        BlockchainAnchorReceipt? existingReceipt,
        string recordHash,
        AuditVerificationStatus verificationStatus,
        string? transactionReference,
        DateTime? anchoredAtUtc,
        DateTime? verifiedAtUtc,
        string explanation)
    {
        var anchorStatus = existingReceipt?.AnchorStatus.ToString()
            ?? (verificationStatus == AuditVerificationStatus.Match ? "Anchored" : "NotAnchored");

        return new GovernanceAuditVerificationReceiptDto(
            auditRecord.Id,
            auditRecord.EngineType.ToString(),
            auditRecord.ActionName,
            auditRecord.Status,
            auditRecord.Timestamp,
            recordHash,
            verificationStatus.ToString(),
            anchorStatus,
            transactionReference ?? existingReceipt?.TransactionReference,
            existingReceipt?.BlockNumber,
            anchoredAtUtc ?? existingReceipt?.AnchoredAtUtc,
            verifiedAtUtc,
            explanation,
            auditRecord.HashVersion);
    }
}
