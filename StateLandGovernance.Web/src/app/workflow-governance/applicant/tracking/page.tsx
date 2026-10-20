"use client";

import React, { useState } from "react";
import { useRouter } from "next/navigation";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { Button } from "@/shared/components/Button";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";
import styles from "./TrackingLookupPage.module.css";

export default function TrackingLookupPage() {
  const router = useRouter();
  const [reference, setReference] = useState<string>("");
  const [error, setError] = useState<string | null>(null);

  const handleLookup = (e: React.FormEvent) => {
    e.preventDefault();
    const cleanRef = reference.trim();
    if (!cleanRef) {
      setError("Please enter a valid application reference number.");
      return;
    }
    router.push(`/workflow-governance/applicant/tracking/${encodeURIComponent(cleanRef)}`);
  };

  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
    >
      <PageHeading
        title="Application Status & Milestone Tracking"
        subtitle="Lookup the current administrative status and review progression for your state land lease proposal."
      />

      <div className={styles.lookupCard}>
        <h3>Enter Application Reference</h3>
        <p className={styles.lookupHint}>
          Enter the unique reference code provided during application submission (e.g. SL-LMS-2026-123456).
        </p>

        {error && <div className={styles.errorBanner}>{error}</div>}

        <form onSubmit={handleLookup} className={styles.lookupForm}>
          <div className={styles.inputGroup}>
            <input
              type="text"
              required
              placeholder="e.g. SL-LMS-2026-100001"
              value={reference}
              onChange={(e) => {
                setReference(e.target.value);
                setError(null);
              }}
              className={styles.referenceInput}
            />
            <Button variant="primary" type="submit">
              Track Application →
            </Button>
          </div>
        </form>

        <div className={styles.noteBox}>
          <strong>Governance Note:</strong>
          <p>
            Milestone tracking reflects authenticated applicant status. Detailed internal administrative notes,
            officer audit rationale, and inter-departmental consensus deliberation are protected under administrative governance rules.
          </p>
        </div>
      </div>
    </PageLayout>
  );
}
