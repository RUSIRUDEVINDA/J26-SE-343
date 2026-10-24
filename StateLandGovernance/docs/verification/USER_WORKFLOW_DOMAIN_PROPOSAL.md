# Scoped Domain extension — authorized implementation

Source: the user's updated workflow request, 2026-10-16. These are project requirements, not independently verified law. The user explicitly replied “Authorize the scoped Domain extension” on 2026-10-16. This authorizes only the operations below; unrelated Domain remains protected.

## Concrete conflicts in the current operations

- `WorkflowExecution.RecordStageDecision` releases dependent stages when prerequisites are Completed, including Rejected. The requested active required external rejection must block DS approval/progression.
- The same operation throws `WorkflowStageAlreadyDecidedException` for a second decision. There is no authorized append-only decision reversal/revision operation.
- `WorkflowExecution` captures immutable stage definitions from the approved plan. There is no operation to adopt a governed revised plan and add a department after activation while preserving existing work/decisions. The current application correctly blocks replacing an activated plan.

Cabinet (>50 acres) and provincial/inter-provincial planning can be composed from existing stage/DAG operations without extending Domain. Restricted reviewer projections, evidence-document authorization, configuration, C4 fixes and prototype spatial adapters also do not need Domain edits.

## Proposed minimal operations

1. Extend `Domain/WorkflowExecution/WorkflowExecution.cs` with outcome-aware prerequisite validation, using a versioned prerequisite gate policy supplied from the approved execution definition. Required approval prerequisites permit Approved/ApprovedWithConditions only. Review/visibility forwarding may explicitly accept a Rejected outcome but cannot convert it into approval. Preserve existing generic behaviour for definitions without the new policy to avoid silently changing unrelated workflows.
2. Add `ReviseStageDecision(stageId, expectedExecutionRevision, priorDecisionId, replacementDecision, verifiedInstitutionalAuthority, reason, at)` and append-only history on `WorkflowStageExecution`. Validate same case/institution/capability, nonempty reason, chronology and exact prior revision/decision. Recalculate downstream readiness. If a changed prerequisite invalidates already-started downstream work, block/review it explicitly; do not erase completed decisions. Disallow mutation of an execution with an immutable completion/handoff record; use the existing post-workflow follow-up boundary instead.
3. Add `AdoptRevisedPlan(approvedDefinition, expectedExecutionRevision, verifiedAuthority, reason, at)` to the execution. Require same case, a newer approved plan binding and explicit supersession authority. Keep IDs/history for unchanged stages, create newly required stages Blocked/Ready according to current prerequisite outcomes, reject removal/redefinition of started/decided stages without a separate explicit resolution. Preserve prior definition/revision and reason. New required reviews must invalidate DS/progression readiness.

Supporting Domain files: `WorkflowExecutionDefinition.cs`, `WorkflowStageDefinition.cs`, `WorkflowStageExecution.cs` and a small immutable gate/history record alongside them. No C1/C2 change, workflow engine, legal threshold inference or C4 rule duplication is proposed. Application persistence will serialize the added state through existing execution storage; additive migration only if its current JSON fields cannot carry it safely.

Required tests before release: external rejection blocks DS start/approval; conditional approval permits only its approved dependencies; rejection forwarding retains rejection; authorized revision retains both decisions/actor/reason/time; unauthorized/stale revisions fail without mutation; added department blocks DS until reviewed; started work cannot be silently removed; completed/handoff state cannot be rewritten; unchanged legacy definitions retain current tests.

## Implementation status

Implemented the explicit versioned prerequisite gate policy, `ReviseStageDecision`, `AdoptRevisedPlan`, append-only decision/plan/invalidated-consensus histories and revision audit events. Definitions without an attached gate preserve legacy completion-based behaviour. Gate policy identifies dependent stages whose prerequisites must be Approved or ApprovedWithConditions; ungated visibility forwarding retains the original rejection. Started downstream work blocks decision changes pending explicit resolution. Started/decided stage removal, redefinition or gate weakening is rejected. Completed execution remains immutable. Historical decision identifiers and previously adopted plan identifiers cannot be reused.

Trusted persistence JSON converters retain exact definitions, gates, decisions and histories without emitting events. These are not browser deserializers. Automated fixtures exercise authorization, stale revisions, outcome gating, decision history, active department addition and JSON roundtrip. No live execution, identity grant, approval, persisted execution row, fulfillment or handoff is claimed. Application/API/UI orchestration and PostgreSQL restart acceptance for execution remain outstanding.

Validation: 13 focused synthetic revision tests passed; relevant C3 suite 1,261 passed with four existing live-service skips (shared PostgreSQL/live OCR fixtures excluded). Ordinary API Release build passed with seven pre-existing C2 nullable warnings. Debug output was locked by the running normal host; Release verified compilation without stopping that host. Tracked and untracked whitespace checks passed. No schema/data changes, C1/C2/C4 backend edits, OCR changes, catalog changes, commit or push occurred in this scoped continuation.
