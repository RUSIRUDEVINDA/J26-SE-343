namespace StateLandGovernance.WorkflowGovernance.Application.DTOs;

public sealed record RoutingPolicyDto(string Identifier, string Version, string SourceReference,
    Guid ApprovedByActorId, DateTime EffectiveFrom, DateTime? EffectiveUntil,
    IReadOnlyDictionary<string, string> ApplicableFacts, IReadOnlyList<string> Institutions,
    bool RequiresCommissionerApproval, int StandardDays, int DivisionalDays, int CommissionerDays,
    string StandardCapability, string DivisionalCapability, string CommissionerCapability,
    IReadOnlyList<string> RequiredEvidence, ProjectRoutingContext? ProjectRouting = null);
public sealed record ProjectRoutingContext(string SourceReference, string Version, string SpatialVersion,
    bool IsDevelopmentMock, IReadOnlyList<string> Provinces,
    IReadOnlyDictionary<string, string> SelectedDepartmentReasons, decimal CabinetThresholdAcres = 50m);
public sealed record PlanStageDto(Guid Id, string Code, string Institution, string Capability,
    string Type, IReadOnlyList<Guid> Dependencies, int? TargetDays, string Explanation);
public sealed record WorkflowPlanDto(Guid Id, int Revision, string Status, Guid SnapshotId,
    string RuleIdentifier, string RuleVersion, string Binding, Guid GeneratedBy, Guid? ApprovedBy,
    DateTime CreatedAt, DateTime? ApprovedAt, string? RevisionReason,
    IReadOnlyList<string> RequiredEvidence, IReadOnlyList<PlanStageDto> Stages,
    string InputBindingsJson = "", Guid? SupersededBy = null, DateTime? SupersededAt = null);
public sealed record WorkflowPlanningState(Guid CaseId, int CaseRevision, Guid? SnapshotId,
    bool Ready, IReadOnlyList<string> MissingInputs, string? Binding,
    WorkflowPlanDto? ActivePlan, IReadOnlyList<WorkflowPlanDto> History, bool CanApprove);
public sealed record GenerateWorkflowPlanRequest(Guid VerifiedFactSnapshotId, int ExpectedCaseRevision,
    Guid? ExpectedPlanId, int? ExpectedPlanRevision, string? RevisionReason);
public sealed record ApproveWorkflowPlanRequest(Guid VerifiedFactSnapshotId, int ExpectedCaseRevision,
    int ExpectedPlanRevision);
