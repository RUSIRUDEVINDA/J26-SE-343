namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record InstitutionalActorContext(VerifiedAuthorityContext Authority, string Institution);
public sealed record WorkflowExecutionState(Guid CaseId, Guid? ExecutionId, Guid? PlanId, int? Revision,
    string Status, bool Current, IReadOnlyList<string> MissingInputs, IReadOnlyList<ExecutionStageDto> Stages,
    IReadOnlyList<ExecutionPlanHistoryDto> PlanHistory, bool IsDevelopmentMock,
    int CaseRevision = 0, Guid? ActivePlanId = null, int? ActivePlanRevision = null, string? ActivePlanStatus = null);
public sealed record ExecutionStageDto(Guid Id, string Code, string Institution, string Capability,
    string Status, IReadOnlyList<Guid> Dependencies, string Request,
    ExecutionDecisionDto? Decision, IReadOnlyList<ExecutionDecisionHistoryDto> DecisionHistory);
public sealed record ExecutionDecisionDto(Guid Id, string Outcome, string? Reason, IReadOnlyList<string> Conditions,
    Guid ActorId, DateTime At);
public sealed record ExecutionDecisionHistoryDto(ExecutionDecisionDto Previous, ExecutionDecisionDto Replacement,
    string Reason, int Revision);
public sealed record ExecutionPlanHistoryDto(Guid PreviousPlanId, Guid ReplacementPlanId, Guid ActorId, string Reason,
    DateTime At, int Revision);
public sealed record StartExecutionRequest(Guid PlanId, int ExpectedPlanRevision, int ExpectedCaseRevision);
public sealed record StageActionRequest(int ExpectedExecutionRevision);
public sealed record StageDecisionRequest(int ExpectedExecutionRevision, string Outcome, string Reason,
    IReadOnlyList<string> Conditions, IReadOnlyList<DecisionDocumentBinding> Documents,
    Guid? PriorDecisionId = null, string? RevisionReason = null);
public sealed record DecisionDocumentBinding(Guid DocumentId, Guid VersionId);
public sealed record AddDepartmentRequest(int ExpectedExecutionRevision, int ExpectedCaseRevision,
    string Institution, string Reason);
public sealed record RestrictedInstitutionalRequest(Guid CaseId, Guid ExecutionId, int Revision,
    Guid StageId, string Institution, string Status, string Request, ExecutionDecisionDto? OwnDecision,
    IReadOnlyList<ExecutionDecisionHistoryDto> OwnDecisionHistory, int CaseRevision = 0, bool CanStart = false);
