using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StateLandGovernance.GovernanceIntelligence.Application.DTOs;
using StateLandGovernance.GovernanceIntelligence.Application.Interfaces;
using StateLandGovernance.GovernanceIntelligence.Domain.Enums;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;

/// <summary>
/// Infrastructure HTTP client communicating with the Go blockchain microservice (which in turn anchors to Hyperledger Besu).
/// Completely encapsulates network transport, resilient timeout handling, and neutral status mapping.
/// </summary>
public sealed class HttpBlockchainAuditAnchorService : IBlockchainAuditAnchorService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpBlockchainAuditAnchorService> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpBlockchainAuditAnchorService(
        HttpClient httpClient,
        ILogger<HttpBlockchainAuditAnchorService> logger,
        TimeProvider? timeProvider = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BlockchainAnchorReceipt> AnchorRecordAsync(
        AnchorAuditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var transportReq = new BlockchainAnchorTransportRequest(
            AuditRecordId: request.AuditRecordId.ToString("D"),
            RecordHash: request.RecordHash,
            EngineType: request.EngineType,
            RecordVersion: request.RecordVersion
        );

        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/anchors", transportReq, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Blockchain microservice returned HTTP {StatusCode}: {ErrorBody}", response.StatusCode, errorBody);

                return new BlockchainAnchorReceipt(
                    AuditRecordId: request.AuditRecordId,
                    RecordHash: request.RecordHash,
                    AnchorStatus: AnchorStatus.Failed,
                    TransactionReference: null,
                    BlockNumber: null,
                    AnchoredAtUtc: null
                );
            }

            var transportResp = await response.Content.ReadFromJsonAsync<BlockchainAnchorTransportResponse>(JsonOptions, cancellationToken);
            if (transportResp is null)
            {
                return new BlockchainAnchorReceipt(
                    AuditRecordId: request.AuditRecordId,
                    RecordHash: request.RecordHash,
                    AnchorStatus: AnchorStatus.Failed,
                    TransactionReference: null,
                    BlockNumber: null,
                    AnchoredAtUtc: null
                );
            }

            return MapAnchorResponse(request.AuditRecordId, request.RecordHash, transportResp);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning("Blockchain microservice is unreachable during anchor dispatch: {Message}", ex.Message);

            return new BlockchainAnchorReceipt(
                AuditRecordId: request.AuditRecordId,
                RecordHash: request.RecordHash,
                AnchorStatus: AnchorStatus.Unavailable,
                TransactionReference: null,
                BlockNumber: null,
                AnchoredAtUtc: null
            );
        }
    }

    public async Task<BlockchainAnchorReceipt?> GetAnchorReceiptAsync(
        Guid auditRecordId,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/anchors/{auditRecordId:D}";

        try
        {
            var response = await _httpClient.GetAsync(url, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Blockchain service returned HTTP {StatusCode} on receipt query.", response.StatusCode);
                return null;
            }

            var transportResp = await response.Content.ReadFromJsonAsync<BlockchainAnchorTransportResponse>(JsonOptions, cancellationToken);
            if (transportResp is null)
            {
                return null;
            }

            return MapAnchorResponse(auditRecordId, transportResp.RecordHash ?? string.Empty, transportResp);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning("Blockchain service unreachable on receipt query: {Message}", ex.Message);
            return new BlockchainAnchorReceipt(
                AuditRecordId: auditRecordId,
                RecordHash: string.Empty,
                AnchorStatus: AnchorStatus.Unavailable,
                TransactionReference: null,
                BlockNumber: null,
                AnchoredAtUtc: null
            );
        }
    }

    public async Task<AuditIntegrityVerificationResult> VerifyAuditIntegrityAsync(
        Guid auditRecordId,
        string currentHash,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/v1/anchors/{auditRecordId:D}/verify";
        var transportReq = new BlockchainVerifyTransportRequest(ExpectedHash: currentHash);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, transportReq, JsonOptions, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AuditIntegrityVerificationResult(
                    AuditRecordId: auditRecordId,
                    CurrentHash: currentHash,
                    AnchoredHash: null,
                    VerificationStatus: AuditVerificationStatus.Unavailable,
                    TransactionReference: null,
                    AnchoredAtUtc: null,
                    VerifiedAtUtc: now,
                    Explanation: $"Blockchain verification service returned HTTP {(int)response.StatusCode}."
                );
            }

            var transportResp = await response.Content.ReadFromJsonAsync<BlockchainVerifyTransportResponse>(JsonOptions, cancellationToken);
            if (transportResp is null)
            {
                return new AuditIntegrityVerificationResult(
                    AuditRecordId: auditRecordId,
                    CurrentHash: currentHash,
                    AnchoredHash: null,
                    VerificationStatus: AuditVerificationStatus.Unavailable,
                    TransactionReference: null,
                    AnchoredAtUtc: null,
                    VerifiedAtUtc: now,
                    Explanation: "Failed to deserialize verification response from blockchain service."
                );
            }

            var status = transportResp.VerificationStatus switch
            {
                "Match" => AuditVerificationStatus.Match,
                "Mismatch" => AuditVerificationStatus.Mismatch,
                "NotAnchored" => AuditVerificationStatus.NotAnchored,
                _ => AuditVerificationStatus.Unavailable
            };

            return new AuditIntegrityVerificationResult(
                AuditRecordId: auditRecordId,
                CurrentHash: currentHash,
                AnchoredHash: transportResp.AnchoredHash,
                VerificationStatus: status,
                TransactionReference: transportResp.TransactionReference,
                AnchoredAtUtc: transportResp.AnchoredAtUtc,
                VerifiedAtUtc: transportResp.VerifiedAtUtc,
                Explanation: transportResp.Explanation
            );
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            _logger.LogWarning("Blockchain microservice unreachable during verification: {Message}", ex.Message);

            return new AuditIntegrityVerificationResult(
                AuditRecordId: auditRecordId,
                CurrentHash: currentHash,
                AnchoredHash: null,
                VerificationStatus: AuditVerificationStatus.Unavailable,
                TransactionReference: null,
                AnchoredAtUtc: null,
                VerifiedAtUtc: now,
                Explanation: "The blockchain verification ledger service is temporarily unreachable."
            );
        }
    }

    private static BlockchainAnchorReceipt MapAnchorResponse(Guid auditRecordId, string recordHash, BlockchainAnchorTransportResponse resp)
    {
        var status = resp.AnchorStatus switch
        {
            "Anchored" => AnchorStatus.Anchored,
            "Failed" => AnchorStatus.Failed,
            "Unavailable" => AnchorStatus.Unavailable,
            "NotAnchored" => AnchorStatus.NotAnchored,
            _ => AnchorStatus.Pending
        };

        return new BlockchainAnchorReceipt(
            AuditRecordId: auditRecordId,
            RecordHash: string.IsNullOrEmpty(resp.RecordHash) ? recordHash : resp.RecordHash,
            AnchorStatus: status,
            TransactionReference: resp.TransactionReference,
            BlockNumber: resp.BlockNumber,
            AnchoredAtUtc: resp.AnchoredAtUtc
        );
    }
}
