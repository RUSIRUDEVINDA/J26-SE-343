# Proposed Component 3 Commissioner Referral Contract — Unagreed

Status: **proposal only**. Component 3 does not currently publish this boundary.
Component 4 production delivery remains unconfigured until the owning Component 3
team agrees and implements it.

The proposed receiver accepts one referral containing:

- Component 4 referral, correlation and screening-assessment identifiers;
- the exact Case ID and Workflow Run ID supplied by the caller;
- referral reason and supporting evidence references; and
- the UTC request timestamp.

The receiver must process `CorrelationId` idempotently. One accepted correlation
must end the identified normal workflow run for referral without marking the land
application rejected and must create exactly one separate Land Commissioner review
process. Those two effects must be one reliable Component 3 transition, not two
unrelated fire-and-forget operations.

An acknowledgement is valid only after normal progression has been prevented and
the separate review process exists. It returns the Commissioner review process
reference. Re-delivery of the same correlation returns the same reference.

The receiver must reject, without silently retargeting:

- a Case ID / Workflow Run ID mismatch;
- an unknown workflow run;
- a stale or already-ended run unless this correlation was already completed; and
- a request that races with an in-flight normal transition.

The Component 3 transition must serialize against normal progression so a normal
stage cannot advance after referral wins the race. Temporary failure must be
reported without acknowledgement. Component 4 retains a durable failed/pending
state and retries explicitly with the same correlation identity. A transport call
alone is never acknowledgement.
