namespace StateLandGovernance.WorkflowGovernance.Domain.DocumentAnalysis;

using StateLandGovernance.WorkflowGovernance.Domain.Authority;
using StateLandGovernance.WorkflowGovernance.Domain.Documents;
using StateLandGovernance.WorkflowGovernance.Domain.Exceptions;

/// <summary>A human assertion with document evidence; never a machine extraction candidate.</summary>
public sealed record HumanEvidenceFact
{
    public Guid Id { get; }
    public GovernedDocumentId DocumentId { get; }
    public DocumentVersionId VersionId { get; }
    public DocumentChecksum Checksum { get; }
    public FactCode FactCode { get; }
    public AnalysisFactValue Value { get; }
    public int PageNumber { get; }
    public string Quote { get; }
    public string ReviewReason { get; }
    public Guid ActorId { get; }
    public DateTime RecordedAtUtc { get; }
    public string? VerifiedCapability { get; }
    public string? AuthorityScopeIdentifier { get; }
    public DateTime? AuthorityValidFromUtc { get; }
    public DateTime? AuthorityVerificationTimeUtc { get; }
    public DateTime? AuthorityValidUntilUtc { get; }

    public HumanEvidenceFact(Guid id, GovernedDocumentId documentId, DocumentVersionId versionId,
        DocumentChecksum checksum, FactCode factCode, AnalysisFactValue value, int pageNumber,
        string quote, string reviewReason, Guid actorId, DateTime recordedAtUtc,
        VerifiedAuthoritySnapshot authority)
    {
        if (id == Guid.Empty || documentId.Value == Guid.Empty || versionId.Value == Guid.Empty || checksum == null ||
            factCode == null || value == null || pageNumber <= 0 || string.IsNullOrWhiteSpace(quote) || string.IsNullOrWhiteSpace(reviewReason))
            throw new InvalidVerifiedFactSnapshotException("Human evidence requires exact document/version/checksum, page, quote, value and review reason.");
        if (authority == null) throw new MissingVerifiedAuthorityException("Human evidence authority is required.");
        authority.EnsureAuthorizes(actorId, "HumanEvidenceReviewer",
            new(AuthorityScopeKind.GovernedDocument, documentId.Value.ToString("D")), recordedAtUtc);
        Id = id; DocumentId = documentId; VersionId = versionId; Checksum = checksum;
        FactCode = factCode; Value = value; PageNumber = pageNumber; Quote = quote.Trim(); ReviewReason = reviewReason.Trim();
        ActorId = actorId; RecordedAtUtc = recordedAtUtc;
        VerifiedCapability = "HumanEvidenceReviewer";
        AuthorityScopeIdentifier = documentId.Value.ToString("D");
        AuthorityValidFromUtc = authority.ValidFrom;
        AuthorityVerificationTimeUtc = authority.VerificationTime;
        AuthorityValidUntilUtc = authority.ValidUntil;
    }
}
