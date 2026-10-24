using System;
using System.Text.Json.Serialization;

namespace StateLandGovernance.GovernanceIntelligence.Infrastructure.Integrations.Blockchain;

public sealed record BlockchainAnchorTransportRequest(
    [property: JsonPropertyName("auditRecordId")] string AuditRecordId,
    [property: JsonPropertyName("recordHash")] string RecordHash,
    [property: JsonPropertyName("engineType")] string EngineType,
    [property: JsonPropertyName("recordVersion")] string RecordVersion
);

public sealed record BlockchainAnchorTransportResponse(
    [property: JsonPropertyName("auditRecordId")] string AuditRecordId,
    [property: JsonPropertyName("recordHash")] string? RecordHash,
    [property: JsonPropertyName("anchorStatus")] string AnchorStatus,
    [property: JsonPropertyName("transactionReference")] string? TransactionReference,
    [property: JsonPropertyName("blockNumber")] long? BlockNumber,
    [property: JsonPropertyName("anchoredAtUtc")] DateTime? AnchoredAtUtc,
    [property: JsonPropertyName("errorMessage")] string? ErrorMessage
);

public sealed record BlockchainVerifyTransportRequest(
    [property: JsonPropertyName("expectedHash")] string ExpectedHash
);

public sealed record BlockchainVerifyTransportResponse(
    [property: JsonPropertyName("auditRecordId")] string AuditRecordId,
    [property: JsonPropertyName("currentHash")] string CurrentHash,
    [property: JsonPropertyName("anchoredHash")] string? AnchoredHash,
    [property: JsonPropertyName("verificationStatus")] string VerificationStatus,
    [property: JsonPropertyName("transactionReference")] string? TransactionReference,
    [property: JsonPropertyName("anchoredAtUtc")] DateTime? AnchoredAtUtc,
    [property: JsonPropertyName("verifiedAtUtc")] DateTime VerifiedAtUtc,
    [property: JsonPropertyName("explanation")] string Explanation
);
