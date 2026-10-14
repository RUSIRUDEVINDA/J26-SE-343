# Commissioner workflow anomaly journey

`PP1-ANOMALY-COMMISSIONER-001` is an append-only synthetic PP1 demonstration under seed contract `pp1-anomaly-commissioner-v1`. It does not replace or modify the five final v2 scenarios.

The scenario represents one configurable Sri Lankan long-term lease route: complete proposal receipt at the Divisional Secretariat, DS scrutiny and field verification, environmental and survey clearances, PLC/DLC recommendation, LCGD intake and review, a documented correction/resubmission interval, competent-authority approval, lease-instrument finalization, return to the DS, and final notification. Presidential Secretariat processing and Land Registry registration are stored as `NotApplicable` events for this route.

The controlled perturbation is the 18-day interval between the correction request and LCGD re-review. The manifest contains neither a model score nor a predetermined flagged decision.

## Evidence boundaries

- The workflow projection is synthetic C1/C3 handoff evidence with `SyntheticPp1Seed` provenance.
- Component 4 retrieves the ordered projection from PostgreSQL.
- The existing server path validates the trace and sends it to the frozen `workflow_anomaly_model_v1` feature extractor and Isolation Forest.
- The backend applies the strict `anomaly_score > threshold` comparison and persists the assessment in PostgreSQL.
- The frontend displays returned values; it does not extract the nine features or calculate the outcome.
- Structured legal-route and applicability metadata are not persisted in the current projection schema. The API reports those fields as unavailable rather than inferring them from event text.
- No automatic referral, officer decision, audit verification, or blockchain action is part of this journey.

The anomaly score is not a probability or confidence value. It does not establish corruption, misconduct, legal non-compliance, or officer guilt.
