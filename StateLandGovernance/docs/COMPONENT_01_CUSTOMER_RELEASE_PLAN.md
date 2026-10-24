# Component 1 — real customer release plan (not production-ready)

**Status:** Planning document only. Component 1 is **not** ready for real customers seeking government land leases. This file must not be cited as evidence of production readiness.

Scope: Component 1 (Land Intelligence) only. Do not treat Components 2–4 as covered here.

## Current reality (checked against code)

| Fact | Implication |
| --- | --- |
| 400 research parcels (300 + 100) have **unknown / not verified** official lease availability | Customers cannot rely on the demo DB for lease decisions |
| **Project lease-ready** is a dataset designation | Not an official authority decision — shown separately from official availability on recommendations |
| **Simulated** lease workflow (ResearchDemo) including officer valuation + authority Approved/Rejected | Demo labels only — not legal status; Approved still requires formal lease processing |
| Research-demo JWT minting + client renew-once | Must be replaced by real authentication / RBAC for deployment |
| No secure document upload | Checklist text only |
| Officer / authority tools use demo JWT / ResearchDemo gates | Not production identity or RBAC |
| No payment integration | Authority fee process unknown / unimplemented |
| GIS enrichment typically **0** snapshots on research DB | Environmental evidence unavailable — not assessed |
| Standard ML ~76.5% on **synthetic** held-out samples, supplementary | Must not drive availability or approval; area always converted to hectares |
| Recommendations are criterion **match** scores | Not legal suitability or lease approval |

**Hard rule (already required in product posture):** keep unknown availability explicitly unknown. Never infer official lease availability from:

- source-reported claimant category  
- source-reported land use  
- GIS distances / soil / enrichment  
- project lease-ready designation  
- ML predicted label or probability  
- recommendation match score  

## Target product (authoritative availability)

Before real customers use Component 1 to seek leases, each parcel that is offered for interest must carry an **authoritative availability record** separate from cadastre metadata and project designations.

### Proposed record (design — not implemented as authority truth)

| Field | Requirement |
| --- | --- |
| `parcelId` | Land parcel UUID |
| `status` | Controlled enum, e.g. `Unknown`, `UnderVerification`, `AvailableForInterest`, `NotAvailable`, `Withdrawn`, `Expired` — default **`Unknown`** |
| `sourceSystem` / `sourceReference` | Issuing authority system + document/reference id |
| `verifiedBy` | Officer / organisational identity (not demo header) |
| `verifiedAtUtc` | Instant of verification decision |
| `validFromUtc` / `validUntilUtc` | Window during which the status may be shown as current |
| `recheckDueUtc` | Mandatory re-verification trigger |
| `statusHistory[]` | Append-only: previous status, actor, timestamp, reason, evidence refs |
| `evidenceRefs[]` | Links to sealed documents / authority case ids (not fabricated) |

### Workflow outline

1. **Ingest cadastre** → availability stays **`Unknown`** (current behaviour for non-designated / unverified parcels).
2. **Officer verification case** opened with required evidence checklist from the **actual** land authority (TBD by authority).
3. **Decision** recorded with source, verifier, timestamps, history entry.
4. **Expiry / recheck** job marks status **`Expired` → `Unknown` or `UnderVerification`** when `validUntilUtc` / `recheckDueUtc` passes — never auto-promote to Available.
5. **Lease interest** allowed only when status is in an authority-approved set (e.g. `AvailableForInterest`) **and** hard domain blocks still apply.
6. **Applicant-facing UI** must show status + “as of {verifiedAtUtc}, valid until {validUntilUtc}” and must not show project lease-ready as official availability.

### What exists today vs gap

| Capability | Today | Gap |
| --- | --- | --- |
| Cadastre display | Yes | — |
| Project lease-ready designation | Yes (research) | Must remain non-authoritative |
| Simulated workflow labels | Yes (ResearchDemo) | Must stay off in production |
| Authoritative availability entity + history | **No** | Build after authority rules confirmed |
| Expiry / recheck automation | **No** | Requires policy + jobs |
| Block interest when Unknown | Partial (Private screening only) | Must gate on authoritative status |

**Implementation note:** No authoritative availability tables or “Available” defaults are shipped in this planning pass. Inventing verified status without authority data would violate the hard rule above.

## Production identity and officer permissions

| Requirement | Today | Remaining work |
| --- | --- | --- |
| Applicant identity | Dev session JWT / optional demo owner header | Integrate real IdP (national ID / gov SSO) per authority |
| Officer identity | Demo officer JWT under ResearchDemo | Real roles: verify availability, review interest, escalate |
| Permissions | Policy names + demo gates | Fine-grained RBAC; remove `AllowDemoOwnerHeader` / `AllowDevelopmentSessionEndpoint` in all prod configs |
| Audit of auth events | Limited | Login, role change, impersonation prohibition |

Acceptance: production config cannot enable demo auth; every officer action ties to a named principal.

## Secure document handling

| Requirement | Today | Remaining work |
| --- | --- | --- |
| Document checklist | Text list + note that upload is disabled | — |
| Encrypted upload / storage | **No** | Authority-approved store, virus scan, retention, access audit |
| Officer request for docs | Informal note in checklist copy | Structured requests + applicant upload + chain of custody |

Acceptance: no plaintext NICs or documents in logs; uploads optional only when authority mandates them.

## Auditability

| Requirement | Today | Remaining work |
| --- | --- | --- |
| Lease interest status history | Yes (applicant/officer transitions) | Expand to availability verification history |
| Recommendation explainability | Criterion scores + provenance | Keep; never rewrite as legal proof |
| Immutable audit export | Incomplete | Export for authority compliance reviews |

## Payments

| Requirement | Today | Remaining work |
| --- | --- | --- |
| Application / lease fees | **Unknown / not implemented** | Confirm with the **actual** land authority: fee schedule, receipting, refunds, reconciliation |
| Payment provider | None | Only after written authority process exists |

**Do not implement a payment flow until the authority fee process is specified.** Listing “payments required” without a schedule would be speculative.

## GIS and ML in a customer product

- GIS layers: only show measured enrichment; Unavailable stays not assessed.
- ML: supplementary; no availability or approval decisions.
- Colombo experimental ML: remains opt-in research, default off.

## Concrete remaining-work list

### P0 — blockers for any real-customer pilot

1. Authority-confirmed **availability schema** + verification SOP (source, verifier, timestamps, history, expiry/recheck).
2. Persist and API-expose authoritative availability; default **Unknown**; never auto-infer.
3. Gate lease-interest start/submit on authoritative status (not project lease-ready alone).
4. Production identity (applicant + officer) and disable all demo auth paths.
5. Officer RBAC for verification and interest review (replace ResearchDemo gates).
6. Explicit UI copy: Unknown ≠ Available; project lease-ready ≠ official.

### P1 — before public launch

7. Secure document upload + retention policy (if authority requires).
8. Full audit export for availability and interest decisions.
9. GIS enrichment for districts actually offered (or keep Unavailable honest).
10. Operational runbooks: recheck jobs, incident response, data retention.
11. Payment integration **only if** authority mandates fees (spec first).

### P2 — quality / scale

12. Real labelled data for ML if still used as evidence; keep supplementary.
13. Neo4j sync only if product requires graph exploration in production.
14. Performance, backup, and SLOs for parcel + interest volumes.

## Acceptance criteria (customer pilot — aspirational)

A build may be called a **limited customer pilot** only when **all** of the following are true:

1. Every parcel shown for interest has authoritative availability status ∈ {Unknown, UnderVerification, AvailableForInterest, NotAvailable, Withdrawn, Expired} with source + verifier + timestamps when not Unknown.
2. No UI or API path presents project lease-ready, simulated status, GIS, land use, claimant category, or ML as official availability.
3. Lease interest cannot be submitted when status is Unknown / NotAvailable / Expired / Withdrawn (unless authority explicitly defines an exception — documented).
4. Demo auth headers and Development session endpoint are **false** in the deployed environment.
5. Officer actions are attributable and appear in audit history.
6. Document and payment features match written authority requirements or are explicitly out of scope for that pilot.
7. Release notes state remaining gaps; no “production ready for government leasing” claim without authority sign-off.

Until then, the product remains a **research / preliminary-interest demo**.

## Related

- [COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md](./COMPONENT_01_LAND_INTELLIGENCE_RELEASE.md) — what is implemented now  
- [COMPONENT_01_DEMO_VERIFICATION.md](./COMPONENT_01_DEMO_VERIFICATION.md) — research demo checklist  
- [STATE_LAND_CANDIDATE_SCREENING.md](./STATE_LAND_CANDIDATE_SCREENING.md) — screening ≠ availability  
