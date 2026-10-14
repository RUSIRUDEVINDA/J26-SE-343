namespace StateLandGovernance.WorkflowGovernance.Domain.ProposalContent;

using System;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;
using StateLandGovernance.WorkflowGovernance.Domain.LeaseCases;

public sealed record ProposalSourceBinding
{
    public LeaseCaseId LeaseCaseId { get; }
    public GovernedDocumentId ProposalDocumentId { get; }
    public DocumentVersionId DocumentVersionId { get; }
    public DocumentChecksum DocumentChecksum { get; }
    public ProposalTemplateId TemplateId { get; }
    public string TemplateVersion { get; }
    public DateTime AssessedAt { get; }
    public string? ExtractionReference { get; }

    public ProposalSourceBinding(
        LeaseCaseId leaseCaseId,
        GovernedDocumentId proposalDocumentId,
        DocumentVersionId documentVersionId,
        DocumentChecksum documentChecksum,
        ProposalTemplateId templateId,
        string templateVersion,
        DateTime assessedAt,
        string? extractionReference = null)
    {
        if (leaseCaseId == default || leaseCaseId.Value == Guid.Empty)
        {
            throw new InvalidProposalSourceBindingException("LeaseCaseId cannot be empty.");
        }

        if (proposalDocumentId == default || proposalDocumentId.Value == Guid.Empty)
        {
            throw new InvalidProposalSourceBindingException("ProposalDocumentId cannot be empty.");
        }

        if (documentVersionId == default || documentVersionId.Value == Guid.Empty)
        {
            throw new InvalidProposalSourceBindingException("DocumentVersionId cannot be empty.");
        }

        if (documentChecksum == null)
        {
            throw new InvalidProposalSourceBindingException("DocumentChecksum is required.");
        }

        if (templateId == default || string.IsNullOrWhiteSpace(templateId.Value))
        {
            throw new InvalidProposalSourceBindingException("TemplateId is required.");
        }

        if (string.IsNullOrWhiteSpace(templateVersion))
        {
            throw new InvalidProposalSourceBindingException("TemplateVersion cannot be null, empty, or whitespace.");
        }

        if (assessedAt.Kind != DateTimeKind.Utc)
        {
            throw new InvalidProposalSourceBindingException("AssessedAt must be UTC.");
        }

        if (extractionReference != null && string.IsNullOrWhiteSpace(extractionReference))
        {
            throw new InvalidProposalSourceBindingException("ExtractionReference cannot be whitespace when provided.");
        }

        LeaseCaseId = leaseCaseId;
        ProposalDocumentId = proposalDocumentId;
        DocumentVersionId = documentVersionId;
        DocumentChecksum = documentChecksum;
        TemplateId = templateId;
        TemplateVersion = templateVersion.Trim();
        AssessedAt = assessedAt;
        ExtractionReference = extractionReference?.Trim();
    }

    public bool MatchesDocument(DocumentVersionId versionId, DocumentChecksum checksum)
    {
        return DocumentVersionId.Equals(versionId) && DocumentChecksum.Equals(checksum);
    }

    public bool MatchesTemplate(ProposalTemplateId templateId, string templateVersion)
    {
        return TemplateId.Equals(templateId) &&
               string.Equals(TemplateVersion, templateVersion?.Trim(), StringComparison.Ordinal);
    }

    public bool MatchesSource(
        DocumentVersionId versionId,
        DocumentChecksum checksum,
        ProposalTemplateId templateId,
        string templateVersion)
    {
        return MatchesDocument(versionId, checksum) && MatchesTemplate(templateId, templateVersion);
    }

    public bool MatchesSource(ProposalSourceBinding? other)
    {
        if (other is null) return false;
        return LeaseCaseId.Equals(other.LeaseCaseId) &&
               ProposalDocumentId.Equals(other.ProposalDocumentId) &&
               DocumentVersionId.Equals(other.DocumentVersionId) &&
               DocumentChecksum.Equals(other.DocumentChecksum) &&
               TemplateId.Equals(other.TemplateId) &&
               string.Equals(TemplateVersion, other.TemplateVersion, StringComparison.Ordinal);
    }
}
