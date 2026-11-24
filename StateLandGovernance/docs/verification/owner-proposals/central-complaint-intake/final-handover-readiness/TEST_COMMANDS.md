# Verification commands and scope

Executed 2026-10-23. Logs are retained in this directory in UTF-8. No ordinary fixture/inference.

```powershell
# From StateLandGovernance.Web:
npm.cmd test
npm.cmd run lint -- src/app/sign-in/page.tsx src/modules/component-04/components/RegisteredComplaints.tsx src/modules/component-04/components/MeasuredWorkflowFeatures.tsx src/modules/component-04/components/ManualEvidenceVerification.tsx src/modules/component-04/components/IndependentRegulatoryReports.tsx src/modules/component-04/components/RegulatoryFindingExplanation.tsx src/modules/component-04/components/RegulatoryScreening.tsx src/modules/component-04/services/applicantComplaintReturn.ts tests/component-04-handover.test.ts
npx.cmd tsc --noEmit

# From repository root; isolated Docker PostgreSQL must be running.
# Configure COMPONENT4_TEST_POSTGRES_CONNECTION securely for ONLY loopback57657,
# database component4_screening_test_handling. Never use ordinary5432.
dotnet test StateLandGovernance/tests/UnitTests/GovernanceIntelligence/StateLandGovernance.UnitTests.GovernanceIntelligence.csproj --no-restore --filter 'FullyQualifiedName~GovernanceAccess|FullyQualifiedName~ManualEvidence|FullyQualifiedName~Complaint|FullyQualifiedName~CommissionerReportRead|FullyQualifiedName~OfficerReportController|FullyQualifiedName~IndependentRegulatoryReport|FullyQualifiedName~DependencyInjection|FullyQualifiedName~OfficerFlow|FullyQualifiedName~Kiwula|FullyQualifiedName~PostWorkflow'
dotnet test StateLandGovernance/tests/ArchitectureTests/StateLandGovernance.ArchitectureTests.csproj --no-restore
```

Private local orchestration scripts were reviewed before execution: `.tmp/c4-handover-backup.py`, `c4-handover-live.py`, `c4-handover-manual.py`, `c4-handover-preservation.py`, `c4-handover-catalogue.py`, `c4-handover-sql-check.py`, `c4-handover-grants-health.py`. They are not portable release tooling. Do not rerun blindly: archive/isolated names must stay unique and current ordinary lifecycle states must not be replaced with old fixture assumptions. Secrets remain in existing private environment or memory, not published test commands.

Live evidence:495read/authorization checks,48savedreports,69applicant API denials,10applicant frontend redirects;79manual evidence read/ownership/revocation checks across11workspaces. Same-JWT revocation tests atomically restore the exact original permission bytes in finally. No ordinary business action or model call.

Frontend156/backend312/architecture7 passed. Focused lint0errors/two existing ref-cleanup warnings. TypeScript3existing C1 failures only. No graphical browser proof or green whole production build. Follow-up persistence tests remain pending schema approval/implementation, not skipped tests represented as passing.
