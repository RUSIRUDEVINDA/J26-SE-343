namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

using System.Text.Json;

public sealed record CaseScreeningSource(Guid CaseId, string ApplicationReference,
    VerifiedFactSnapshotDto? Snapshot, IReadOnlyList<GovernedDocumentDto> Documents, Guid? ParcelId,
    string CompletenessBinding = "", IReadOnlyList<CandidateFactDto>? FactEvidence = null);
public sealed record PreparedCaseScreening(string InputFingerprint, string PolicyFingerprint,
    string? InputJson, IReadOnlyList<string> MissingInputs, string? BindingJson = null,
    bool IsDevelopmentSpatialMock = false, string? SpatialProvenanceLabel = null);
public sealed record StoredCaseScreening(string Schema, Guid CaseId, Guid SnapshotId, Guid? ParcelId,
    string InputFingerprint, string PolicyFingerprint, JsonElement Assessment,
    string? InputJson = null, string? SourceBindingsJson = null, Guid? RequestedByOfficerId = null);
public sealed record CaseScreeningState(Guid CaseId, int CaseRevision, Guid? VerifiedFactSnapshotId,
    Guid? ParcelId, string Status, bool IsCurrent, IReadOnlyList<string> MissingInputs, StoredCaseScreening? LatestAssessment,
    bool IsDevelopmentSpatialMock = false, string? SpatialProvenanceLabel = null);
public sealed record StartCaseScreeningRequest(Guid VerifiedFactSnapshotId, int ExpectedCaseRevision, Guid? ParcelId = null);
public sealed record ScreeningEvidenceContext(string? Version, string? GovernanceJson,
    string? SpatialReviewJson, IReadOnlyList<string> MissingInputs, Guid? BoundCaseId = null, Guid? BoundParcelId = null);
