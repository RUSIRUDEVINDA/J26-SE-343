using System;
using System.Collections.Generic;

namespace StateLandGovernance.GovernanceIntelligence.Application.Screening.Contracts;

/// <summary>
/// Authoritative input contract required from Component 3 (Workflow Governance) for initial governance screening.
/// Contains strictly verified proposal facts, case metadata, and evidence references.
/// </summary>
public sealed record GovernanceScreeningCaseInputDto(
    string CaseId,
    string ApplicationReference,
    string ProposalVersion,
    Guid VerifiedFactSnapshotId,
    DateTimeOffset SnapshotTimestampUtc,
    string ParcelId,
    string CadastralNumber,
    string ApplicantName,
    string ProposedUse,
    decimal ExtentHectares,
    int LeaseDurationYears,
    decimal InvestmentAmount,
    string InvestmentCurrency,
    string? ZoningArea,
    string District,
    string DivisionalSecretariat,
    string Province,
    IReadOnlyList<ScreeningDocumentReferenceDto>? DocumentReferences = null,
    bool VerifiedLegalDisputeFlag = false,
    bool UnauthorizedOccupationFlag = false,
    bool ObjectionReceivedFlag = false,
    bool FamilyOrInheritanceClaimFlag = false,
    bool DocumentedPriorActivityFlag = false,
    IReadOnlyList<ScreeningComplaintEvidenceDto>? ContemporaneousComplaints = null,
    string? FinancialAssessmentReference = null,
    C1ParcelEvidenceDto? SpatialEvidence = null,
    IReadOnlyList<ConcurrentApplicationEvidenceDto>? ConcurrentApplications = null,
    string? FullInputFingerprint = null,
    string? EffectivePolicyVersion = null,
    string? EvidenceVersion = null,
    bool IsDevelopmentSpatialMock = false
);

public sealed record ScreeningDocumentReferenceDto(
    string DocumentType,
    string DocumentTitle,
    string StorageReference,
    int SourcePageNumber,
    string ExtractedFactCode
);

public sealed record ScreeningComplaintEvidenceDto(
    string ComplaintId,
    string Category,
    string Summary,
    string VerificationStatus,
    DateTimeOffset ReceivedAtUtc
);

public sealed record ConcurrentApplicationEvidenceDto(
    string ConcurrentCaseId,
    string ApplicantName,
    string ProposedUse,
    DateTimeOffset SubmittedAtUtc,
    string WorkflowStatus
);

/// <summary>
/// Authoritative land evidence contract expected from Component 1 (Land Intelligence).
/// </summary>
public sealed record C1ParcelEvidenceDto(
    string ParcelId,
    string CadastralNumber,
    decimal ExtentHectares,
    string Province,
    string District,
    string DivisionalSecretariat,
    string? GramaNiladhariDivision,
    string CoordinateSystem,
    double CentroidLatitude,
    double CentroidLongitude,
    string? BoundaryGeoJson,
    IReadOnlyList<C1SpatialConstraintItemDto>? SpatialConstraints = null,
    IReadOnlyList<C1AdjacentLeaseEvidenceDto>? AdjacentLeases = null,
    bool HasOverlappingParcels = false,
    string? OverlapDetails = null
);

public sealed record C1SpatialConstraintItemDto(
    string ConstraintType,
    string Severity,
    string Description,
    decimal? AffectedAreaPercentage,
    bool IsBufferZone,
    double? BufferDistanceMeters
);

public sealed record C1AdjacentLeaseEvidenceDto(
    string LeaseReference,
    string ParcelCadastralNumber,
    string LesseeName,
    string CurrentStatus,
    int ProximityDistanceMeters
);

/// <summary>
/// Unified consolidated governance screening result produced by Component 4.
/// Dispatched back to Component 3 to guide authorized administrative workflow routing.
/// </summary>
public sealed record ConsolidatedGovernanceScreeningResultDto(
    Guid AssessmentId,
    string CaseId,
    string ApplicationReference,
    string ProposalVersion,
    Guid VerifiedFactSnapshotId,
    DateTimeOffset EvaluatedAtUtc,
    string DeterministicEvaluationId,
    string SnapshotSha256,
    string OverallStatus, // Cleared, RequiresReview, RecommendedWithConditions, Blocked, InsufficientInformation
    string OfficerRecommendationSummary,
    IReadOnlyList<string> RequiredConditions,
    IReadOnlyList<ConsolidatedFindingDto> ConsolidatedFindings,
    RegulatoryComplianceSectionDto RegulatoryCheck,
    ConflictDetectionSectionDto ConflictCheck,
    EarlyGovernanceSectionDto EarlyGovernanceCheck,
    ScreeningProvenanceMetadataDto Provenance,
    string? FullInputFingerprint = null,
    string? EffectivePolicyVersion = null,
    string? EvidenceVersion = null
);

public sealed record ConsolidatedFindingDto(
    string Dimension, // Regulatory, Conflict, EarlyGovernance
    string ItemName,
    string Status, // Compliant, RequiresReview, NonCompliant, Clear, ConflictDetected, NotApplicable
    string Details,
    bool IsBlocking,
    bool RequiresHumanReview,
    string? EvidenceReference,
    string? SourceAuthorityOrRule
);

public sealed record RegulatoryComplianceSectionDto(
    string Status,
    int EvaluatedRulesCount,
    int ViolationsCount,
    int ConditionsCount,
    IReadOnlyList<string> Violations,
    IReadOnlyList<string> Conditions
);

public sealed record ConflictDetectionSectionDto(
    string Status,
    string HighestSeverity,
    int ConflictsCount,
    IReadOnlyList<string> DetectedConflictSummaries
);

public sealed record EarlyGovernanceSectionDto(
    string Status,
    int IndicatorsCount,
    bool ReferralRecommended,
    string? ReferralReason
);

public sealed record ScreeningProvenanceMetadataDto(
    string EngineVersion,
    string RegulatoryCatalogueSource,
    string RuleSetVersion,
    bool IsDevelopmentFixture,
    string FixtureLabel
);

