# Lease-Approval Handover Timing Boundary — Integration Pending

Status: **Component 4 calculator implemented; live Component 3 boundary unavailable.**

This capability evaluates an expert-reported expectation that approval handover
typically occurs within two calendar months of the complete-proposal roadmap
start. It is not a verified statutory deadline, legal rule, operational SLA,
misconduct finding, workflow transition, or Commissioner-referral rule.

## Repository evidence and boundary mapping

| Business boundary | Actual repository event or field | Confirmed meaning | Missing evidence |
| --- | --- | --- | --- |
| Complete-proposal roadmap start | Component 3 `WorkflowExecution.StartedAt` and `WorkflowExecutionStarted.OccurredOn` | UTC start instant for an exact `WorkflowExecutionId` linked to a `LeaseCaseId` | No published read contract, and no evidence that execution start is the confirmed-complete-proposal roadmap start |
| Proposal completeness confirmation | `ProposalContentCompletenessResult.IsConfirmed`, `Outcome`, and `ConfirmedAtUtc` | A proposal-content assessment can be confirmed and can have outcome `Complete` | No contract tying that confirmation to the exact start of a workflow execution |
| Approval granted | `CompletedWorkflowDecision.DecidedAtUtc` and final decision outcome | Time and outcome of the final workflow decision | Approval granted is not approval handed to the applicant |
| Case handoff package | `CaseHandoffPackage.GeneratedAtUtc`, `DownstreamAcknowledgementId`, and `LeaseCaseStatus.HandedOff` | A downstream case package was generated and the case was marked handed off | No applicant recipient, approval-delivery meaning, or acknowledgement timestamp |
| Exact case/run linkage | Internal Component 3 `LeaseCaseId` and `WorkflowExecutionId` | Domain-level identities exist | `IWorkflowInstanceRepository` has no methods; no query, shared contract, or integration event publishes the required timing snapshot |

Component 3's workflow events are domain events, not published integration
contracts. Component 4 therefore does not reference Component 3 assemblies and
does not infer timestamps from those internal types.

## Required future Component 3 read boundary

A trustworthy read-only operation must return, for one exact case and workflow
run:

- Case ID and workflow-run ID with authoritative linkage validation;
- confirmed complete-proposal roadmap-start instant and provenance;
- explicit ongoing/completed state;
- approval-handover-to-applicant instant and provenance when completed; and
- an unavailable/unconfirmed state when either semantic boundary is not known.

The contract must distinguish approval decision, package generation, downstream
acknowledgement, registration completion, and actual handover to the applicant.
Until Component 3 publishes that operation or equivalent integration events,
live timing integration remains unavailable. Production timestamps must not be
fabricated from internal candidates.

## Versioned calculation convention

Policy version:
`lease-approval-handover-asia-colombo-calendar-months-v1`

- Inputs use `DateTimeOffset`; ambiguous naive timestamps are not accepted or
  assigned a timezone.
- Each instant is converted to the `Asia/Colombo` calendar date.
- Expected handover date is the start date plus two calendar months.
- The day is preserved where possible; otherwise it is clamped to the last
  valid day in the target month.
- The expected date itself is within expectation; only a later local date is
  exceeded.
- Completed runs use the confirmed handover date. Ongoing runs use the explicit
  assessment date.
- Calendar days beyond expectation are never negative.
- Weekends, holidays, pauses, and working-day adjustments are not applied.
- Missing or unconfirmed required boundaries return an explicit unavailable /
  unknown result, never zero duration or within expectation.
- Contradictory chronology and mismatched evidence linkage are rejected.

The calculator does not terminate workflows, create legal violations, trigger
referrals, or persist results.

## Separation from workflow anomaly scoring

The existing Isolation Forest remains a separate synthetic completed-trace
experiment. Its `elapsed_days` feature is the difference between the first and
last timestamps in synthetic event-sequence order. Its required final raw event
is `DS Final Notification`, and its timestamps have unspecified naive timezone
semantics.

Neither the first synthetic event nor `DS Final Notification` is proven to be
the business start or approval-handover boundary defined here. The timing
calculator does not change the model, threshold, nine-feature contract, dataset,
completed-trace restriction, training, or held-out evaluation.
