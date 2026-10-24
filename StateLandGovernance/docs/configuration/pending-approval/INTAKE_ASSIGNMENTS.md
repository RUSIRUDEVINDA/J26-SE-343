# Intake jurisdiction and responsibility

Ordinary uploads do not require account/case configuration edits. The applicant identity remains the existing owner. Intake is persisted before storage/OCR, with an exact versioned jurisdiction match or explicit Unassigned state. Jurisdiction is the applicant's declared intake value; OCR never establishes jurisdiction authority. Unknown or ambiguous mappings never select an office.

Trusted provisioning: fill `intake-jurisdiction.example.json` with a real configuration source/publisher, exact intake jurisdiction code, office and version/effective dates. Then run from the workspace root:

```powershell
dotnet run --project StateLandGovernance/tools/ProjectWorkflowProvisioner -- --publish-intake-jurisdiction <reviewed-json-path>
```

This local command is restricted to localhost `state_land_governance`. It publishes append-only versions, never case reviews or approval. Overlapping applicable versions block routing. Effective applicability is evaluated at intake creation time and the matching id/version is retained. Later policy publication does not silently move existing cases.

Identity/office administration supplies `WorkflowGovernance:IntakeCoordinators` (ActorId, OfficeCode, CanManageUnassigned, ExpiresAtUtc, SourceReference) and `IntakeReviewers` (ActorId, DisplayName, OfficeCode). Those entries authorize an office directory and intake metadata, not global case evidence. Production requires the actual identity/office source. Development accounts load the test directory only behind the existing local Development opt-in.

Coordinator UI: `/workflow-governance/assignments`. Choose an eligible reviewer, exact document, reason and validity. The server owns the ProposalReview capability profile. Assignment does not confer institutional decisions, workflow approval or Commissioner authority. Reassignment/revocation is atomic with retained history and optimistic revision; requests re-read the active grant. For multiple documents, the current responsible officer workspace can view case evidence, but verification/publication is restricted to the explicitly selected document. A different document requires a reasoned assignment change, never transferred verification.

The synthetic `DEVELOPMENT-INTAKE-ASSIGNMENT-ONLY/1.0` test policy maps only `DEVELOPMENT-ASSIGNMENT-TEST-ONLY` to `DEVELOPMENT_DS`. It is an explicit test mapping, not geographic/statutory authority. No real district mapping has been invented. The local DS test identity may manage unresolved intake and assign DS or reviewtest as a test Proposal reviewer. Institutional accounts remain excluded.

Managed cases ignore legacy coordinating grants, including after revocation. Legacy institutional grants without workspace capability remain independently restricted. Previously unmanaged cases retain legacy compatibility until explicitly assigned through this lifecycle; no historical grants are deleted. Normal intake creates a managed row immediately, so new uploads never depend on that compatibility path.

Migration adds two tables only. Its Down refuses loss of assignment history/policy versions. Reversal requires a reviewed forward repair or coordinated restoration; dropping tables is not a safe rollback.
