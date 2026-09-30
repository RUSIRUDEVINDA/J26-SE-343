using System;
using StateLandGovernance.LeaseFeasibility.Domain.Enums;

namespace StateLandGovernance.LeaseFeasibility.Domain.ValueObjects;

public sealed record FinancialDocumentEvidence
{
    public string DocumentId { get; }
    public DocumentType DocumentType { get; }
    public string ApplicantId { get; }
    public string IssuingInstitution { get; }
    public DateTime ExtractedDate { get; }
    public string? DocumentUri { get; }

    public FinancialDocumentEvidence(
        string documentId,
        DocumentType documentType,
        string applicantId,
        string issuingInstitution,
        DateTime extractedDate,
        string? documentUri = null)
    {
        if (string.IsNullOrWhiteSpace(documentId))
        {
            throw new ArgumentException("Document ID is required.", nameof(documentId));
        }

        if (string.IsNullOrWhiteSpace(applicantId))
        {
            throw new ArgumentException("Applicant ID is required.", nameof(applicantId));
        }

        if (string.IsNullOrWhiteSpace(issuingInstitution))
        {
            throw new ArgumentException("Issuing institution is required.", nameof(issuingInstitution));
        }

        DocumentId = documentId.Trim();
        DocumentType = documentType;
        ApplicantId = applicantId.Trim();
        IssuingInstitution = issuingInstitution.Trim();
        ExtractedDate = extractedDate;
        DocumentUri = string.IsNullOrWhiteSpace(documentUri)
            ? null
            : documentUri.Trim();
    }
}
