namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System;

/// <summary>
/// Trusted server-generated ingestion receipt representing document content stored by infrastructure.
/// Carries authoritative metadata computed directly from stored bytes (exact byte count and SHA-256 digest).
/// Untrusted client/browser callers are prevented from specifying arbitrary storage references or checksums.
/// </summary>
public sealed record DocumentContentReceipt(
    string ContentReference,
    string ChecksumAlgorithm,
    string ChecksumValue,
    string OriginalFileName,
    string MediaType,
    long FileSizeInBytes)
{
    public const string CanonicalAlgorithm = "SHA-256";

    public static DocumentContentReceipt CreateSha256(
        string contentReference,
        string sha256Hex,
        string originalFileName,
        string mediaType,
        long fileSizeInBytes)
    {
        return new DocumentContentReceipt(
            contentReference,
            CanonicalAlgorithm,
            sha256Hex,
            originalFileName,
            mediaType,
            fileSizeInBytes);
    }
}
