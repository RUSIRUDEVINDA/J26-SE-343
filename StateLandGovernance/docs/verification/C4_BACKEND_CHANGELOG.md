# C4 backend integration repairs — 2026-10-16

Authorization: the user's updated C3–C4 workflow request and continuation explicitly permit minimal C4 backend integration repairs. C4 screening algorithms remain unchanged. C1/C2 are untouched.

| Files under `src/Modules/GovernanceIntelligence` | Reason / behaviour and contract |
| --- | --- |
| `Application/Screening/Contracts/GovernanceScreeningContracts.cs` | Add optional separately named FullInputFingerprint, EffectivePolicyVersion and EvidenceVersion to request/response; add request IsDevelopmentSpatialMock. ProposalVersion retains the actual proposal-version identity. Optional trailing fields preserve source compatibility for legacy callers. |
| `Application/Screening/Interfaces/IApprovedScreeningPolicySource.cs` | Trusted approved policy manifest port returns the actual versioned rules and source. Catalogue presence alone is not approval. |
| `Application/Screening/Commands/OrchestrateGovernanceScreeningCommand.cs` | Hash the entire received contract, including nested document/spatial/complaint contents and provenance versions. Versioned calls validate the actual loaded policy before cache lookup and re-read policy before persistence; echo bindings. Use existing EvaluateNpd with the pinned catalogue rules and available verified inputs; unknown NPD facts remain null. Legacy calls retain their existing regulatory engine path, now truthfully labelled as a legacy provider. Mock spatial provenance is explicit. No screening/routing/approval algorithm is duplicated. |
| `Infrastructure/Repositories/ApprovedScreeningPolicySource.cs` | Owner-supplied SourceReference, Version and RulesSha256 under GovernanceIntelligence:Screening:ApprovedPolicy pin the active catalogue. Empty/missing/mismatched configuration blocks. No invented approval source or policy seed is installed. |
| `Infrastructure/Persistence/Entities/ConsolidatedScreeningEntity.cs`, `Configurations/ConsolidatedScreeningEntityConfiguration.cs` | Append-only consolidated response row; UUID primary key, unique case/proposal/full-snapshot hash and case/time history index. |
| `Infrastructure/Repositories/PostgresConsolidatedGovernanceScreeningStore.cs` | Durable lookup/history/idempotency; rejects replacement of existing assessment IDs. C3 also retains exact inputs/source bindings in its existing durable envelope. |
| `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs` | Register durable store when PostgreSQL is configured and approved-policy manifest adapter. In-memory store remains for explicit non-durable development/test composition. |
| `Infrastructure/Persistence/Migrations/20261016123700_AddDurableConsolidatedScreening.cs`, `.Designer.cs`, `GovernanceIntelligenceDbContextModelSnapshot.cs` | Add only consolidated_screening_assessments and its indexes. Down refuses destructive history deletion. |
| `Presentation/Controllers/GovernanceIntelligenceController.cs` — stabilization | Consolidated latest/history and underlying early-screening result/history now require authenticated, unexpired case-scoped CaseWorkspaceReviewer authority and officer role. Institutional role alone and Development headers cannot read stored case evidence. Direct HTTP orchestration additionally requires a separately issued ConsolidatedScreeningDispatcher capability; ordinary officer callers use C3's prerequisite checks. In-process C3 orchestration is unchanged. Public labelled development fixtures remain separate. |

## Local migration evidence

The intended localhost `state_land_governance` database had no C4 schema/tables. Reviewed idempotent forward SQL: historical removal of two legacy child tables applies only to empty tables created earlier in that same chain; no pre-existing C4 data existed. A full database backup was written to ignored `.tmp/workflow-before-c4-full-db-20261016.dump` before applying all twelve existing/new C4 migrations through the existing design-time EF context. No database reset/recreation, policy seeding or worker suppression occurred. C3 before counts: 420 cases, 291 documents, 358 versions, 290 analyses.

The attempted C4-only dump failed because the schema did not exist; it is not an acceptance backup. The separate full-database dump above succeeded.

## Compatibility and reversal

Old partial hashes will not collide with the new full-contract hash; old assessment history is retained. Stabilization exercised the actual engines and PostgreSQL store, including restart retrieval. C3 now accepts cache reuse only when the server hash of the complete received contract, nonempty full-input/policy/evidence bindings, case, snapshot and actual ProposalVersion all match. Legacy/partial/mismatched cache entries remain rejected. No fingerprint is hidden in ProposalVersion. A code reversal must retain all new database rows/tables and source envelopes and keep unsupported versioned screening blocked. Removing the HTTP guards would reopen evidence exposure and is not a safe reversal. Do not run a destructive Down migration or restore an old database over new work.

Tests and normal-host demonstrations are recorded in [the workflow slice acceptance report](USER_WORKFLOW_SLICE_ACCEPTANCE.md). Successful test-policy runs are fixtures, not approved runtime policy or real evidence clearance. The new C3 post-workflow adapter reuses C4's existing anomaly command without further C4 backend edits; external finalized-manifest integrity registration remains an explicit owner contract gap.

## Dependencies and stabilization acceptance — 2026-10-18

The new C4 table requires the existing C4 migration chain and PostgreSQL JSONB support. Historical migrations include legacy-table deletion; they are not universally safe to reverse or apply blindly to populated installations. Review each target's existing objects/history and take a verified backup first. The initial local installation evidence above does not authorize deletion elsewhere.

The versioned regulatory path requires the actual pinned approved-policy provider/catalogue, not merely a client version string. Conflict/early-governance algorithms retain their existing C4 implementation; their research heuristics and source labels are not independently approved statutory policy. Real-case acceptance still requires the owners' policy/evidence coverage. HTTP access additionally requires the host to register and run authentication and the identity issuer to provide reviewed case grants; the controller creates no grants.

The isolated acceptance host uses real RegulatoryComplianceEngine, GovernanceConflictEngine, EarlyGovernanceScreeningEngine, OrchestrateGovernanceScreeningCommandHandler and PostgreSQL consolidated/compliance/early stores. Only its explicit policy, identity, documents and spatial/governance evidence are synthetic. It does not substitute a C4 engine double. Results, blocked rejection responses and restart identifiers are linked in [STABILIZATION_ACCEPTANCE.md](STABILIZATION_ACCEPTANCE.md).

C3's full result envelope moved to its existing correction_records JSON store under a distinct CaseScreeningAssessment purpose, with a bounded reference in screening remarks. This is a C3 persistence repair, not a C4 migration or alteration of the frozen remarks limit. The envelope/reference commit together through C3's existing unit of work; C4 and C3 are separate persistence transactions. An interrupted C3 recording can recover the exact fully bound C4 assessment without claiming a distributed transaction.

## Exact accumulated C4 source coverage

Verified against all tracked and non-ignored untracked changes: **12 C4 backend files**, including the migration designer/model snapshot and screening HTTP guards. No C4 Domain file changed.

- `Application/Screening/Commands/OrchestrateGovernanceScreeningCommand.cs`
- `Application/Screening/Contracts/GovernanceScreeningContracts.cs`
- `Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs`
- `Infrastructure/Persistence/Migrations/GovernanceIntelligenceDbContextModelSnapshot.cs`
- `Presentation/Controllers/GovernanceIntelligenceController.cs`
- `Application/Screening/Interfaces/IApprovedScreeningPolicySource.cs`
- `Infrastructure/Persistence/Configurations/ConsolidatedScreeningEntityConfiguration.cs`
- `Infrastructure/Persistence/Entities/ConsolidatedScreeningEntity.cs`
- `Infrastructure/Persistence/Migrations/20261016123700_AddDurableConsolidatedScreening.Designer.cs`
- `Infrastructure/Persistence/Migrations/20261016123700_AddDurableConsolidatedScreening.cs`
- `Infrastructure/Repositories/ApprovedScreeningPolicySource.cs`
- `Infrastructure/Repositories/PostgresConsolidatedGovernanceScreeningStore.cs`

Tests: existing `tests/UnitTests/GovernanceIntelligence/Screening/GovernanceScreeningOrchestrationTests.cs` plus new `ConsolidatedScreeningAuthorizationTests.cs`. The [machine coverage manifest](stabilization-c4-coverage.json) enumerates the exact paths/status.

## Normal-portal research screening repair — 2026-10-18

File: `Application/Screening/Commands/OrchestrateGovernanceScreeningCommand.cs`.

Confirmed defect: an existing `Parcel Overlap Check` could be blocking while `ConflictCheck.Status` and its count still said Clear/0 because only decision-context engine conflicts were summarized. The section now also includes the existing explicit overlap finding, with High severity unless already Critical, a summary/count, and a matching audit status/description. This adds no new screening rule or clearance inference. The engine provenance version is `C4-Engine-v2.5.1`. Regression: `ExplicitParcelOverlapCannotProduceClearConflictSection` in the existing orchestration test suite.

No C4 contract or schema/migration change was needed in this slice. Existing PostgreSQL append-only consolidated persistence and full-input/policy/evidence cache bindings remain in use. The old contradictory assessment is retained as historical evidence, alongside a fresh corrected assessment; it is not updated in place. Changed C4 assembly identity invalidates the old C3 input binding. Reverting this code would reintroduce the inconsistent section and must not be described as a safe business rollback. Never delete assessment/audit rows to reverse the demonstration.

Host-owned Development wiring (outside C4 backend): `Api/Security/PortalResearchScreening.cs`, `Api/Program.cs`, `Api/development-accounts.local.json` and `Api/portal-development-spatial.json` provide an explicitly selected separate synthetic case's research-policy/evidence sources. Actual C4 engines/orchestration are reused. Research policy uses the existing NPD research rule with ResearchConfiguration metadata; it is scoped to local Development, the dedicated account and case. Production/real-case paths retain the existing approved catalogue provider. No catalogue policy, statutory source or real evidence clearance was seeded. Disable the local opt-in to stop these Development sources; preserve the labelled scenario and assessment history. See PORTAL_C4_SCREENING_ACCEPTANCE.md for actual IDs, dependencies and demonstrations.

## 2026-10-19 — Synthetic 12-acre manual test package

No C4 backend, rule implementation, cache contract, persistence or migration change in this slice. Host-only Development evidence/policy selection and C4 frontend scenario choices add explicit no-conflict/overlap synthetic inputs for the separate UI test case; actual engines/rules remain unchanged. See SYNTHETIC_PROPOSAL_ACCEPTANCE.md. Reversing host configuration must preserve existing assessment/input history; do not delete database rows or label destructive cleanup a rollback.
