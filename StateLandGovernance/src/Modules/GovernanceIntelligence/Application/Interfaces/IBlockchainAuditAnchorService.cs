using System;
using System.Threading;
using System.Threading.Tasks;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;

namespace StateLandGovernance.GovernanceIntelligence.Application.Interfaces;

/// <summary>
/// Neutral application-layer interface for interacting with the blockchain trust ledger.
/// Strictly decoupled from underlying blockchain protocols, networks (Besu/Ethereum), or SDKs.
/// </summary>
public interface IBlockchainAuditAnchorService
{
    /// <summary>
    /// Anchors a precomputed canonical governance audit digest onto the blockchain ledger.
    /// </summary>
    Task<BlockchainAnchorReceipt> AnchorRecordAsync(
        AnchorAuditRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves an existing blockchain anchor receipt for a given audit record ID.
    /// </summary>
    Task<BlockchainAnchorReceipt?> GetAnchorReceiptAsync(
        Guid auditRecordId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the current off-chain hash against the recorded ledger anchor.
    /// </summary>
    Task<AuditIntegrityVerificationResult> VerifyAuditIntegrityAsync(
        Guid auditRecordId,
        string currentHash,
        CancellationToken cancellationToken = default);
}
