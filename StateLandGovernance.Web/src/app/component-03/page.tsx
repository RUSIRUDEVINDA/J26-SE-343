import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { Button } from "@/shared/components/Button";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";

export default function Component03Page() {
  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
    >
      <PageHeading
        title="Component 3: Workflow Governance"
        subtitle="AI-Driven Digital Lease Governance: Case Intake, Document Intelligence, Human Verification, and Statutory Clearance."
      />

      <div className="landingGrid" style={{ marginTop: "1.5rem" }}>
        <Link href="/workflow-governance/cases" className="landingCard">
          <h2>Officer Case Queue</h2>
          <p>
            Access the administrative case review queue, verify candidate facts against canonical 17-field inventory, and publish immutable snapshots.
          </p>
        </Link>

        <Link href="/workflow-governance/applicant/intake" className="landingCard">
          <h2>Applicant Proposal Intake</h2>
          <p>
            Submit state land lease proposals with automated multi-page OCR and semantic parsing pipeline.
          </p>
        </Link>

        <Link href="/workflow-governance/applicant/tracking" className="landingCard">
          <h2>Application Milestone Tracking</h2>
          <p>
            Real-time milestone progression with privacy-filtered statutory tracking for applicants.
          </p>
        </Link>

        <Link href="/workflow-governance/ocr-demo" className="landingCard">
          <h2>OCR Intelligence Lab</h2>
          <p>
            Interactive testing workbench for Sinhala/English OCR and Gemini semantic extraction.
          </p>
        </Link>
      </div>
    </PageLayout>
  );
}
