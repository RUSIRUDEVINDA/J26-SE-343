namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;
public sealed record ProjectDocumentObligation(string Code, string Institution, string Title, bool TitlePending = false,
    string? Applicability = null, IReadOnlyList<string>? ForwardRoute = null);
public sealed record ConfigureProjectObligationRequest(string Code, DateTime DueAtUtc, string? ActualTitle,
    string? ActualTitleSourceReference);
public sealed record ConfigureProjectFulfillmentRequest(int ExpectedCaseRevision, string Version,
    IReadOnlyList<ConfigureProjectObligationRequest> Requirements);
public sealed record FulfillmentRequirementDto(Guid Id, int Revision, string Code, string Institution, string Title,
    string Status, DateTime DueAtUtc, bool TitlePending, string SourceReference,
    IReadOnlyList<CorrectionSubmission> Submissions, IReadOnlyList<FulfillmentForwardReceipt>? Forwarding = null,
    bool? LeaseRecommended = null);
public sealed record ProjectFulfillmentState(Guid CaseId, int CaseRevision, string CaseStatus,
    IReadOnlyList<ProjectDocumentObligation> Unconfigured, IReadOnlyList<FulfillmentRequirementDto> Requirements,
    IReadOnlyList<string> MissingInputs, bool ReadyForFinalRecord, bool ReadyForHandoff,
    IReadOnlyList<CaseConditionDto> Conditions);
public sealed record CaseConditionDto(Guid Id, string Institution, string Description, string Status);
public sealed record FulfillmentReviewRequest(int ExpectedRevision, bool Accept, string Reason);
public sealed record FulfillConditionRequest(int ExpectedCaseRevision, string Reason, string EvidenceReference);
public sealed record FulfillmentForwardRequest(int ExpectedRevision, string Reason);
public sealed record LeaseRecommendationRequest(int ExpectedRevision, Guid DsDecisionId, bool RecommendLease, string Reason);

public sealed record WaiveFulfillmentRequest(int ExpectedCaseRevision, int ExpectedRequirementRevision, string Reason, string EvidenceReference);
