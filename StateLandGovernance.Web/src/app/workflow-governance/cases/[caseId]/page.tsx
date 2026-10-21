"use client";
import { caseNextAction } from "@/modules/workflow-governance/services/caseNextAction";
import { ProposalEvidenceViewer } from "@/modules/workflow-governance/components/ProposalEvidenceViewer";
import { reviewFilters, matchesFactFilter, type ReviewFilter } from "@/modules/workflow-governance/services/reviewFilters";
import { PostWorkflowGovernanceWorkspace } from "@/modules/workflow-governance/components/PostWorkflowGovernanceWorkspace";
import { CompletenessNextActions } from "@/modules/workflow-governance/components/CompletenessNextActions";

import { assessmentState } from "@/modules/workflow-governance/services/assessmentState";
import React, { useEffect, useState, useId, useTransition } from "react";
import { useParams, useRouter } from "next/navigation";
import Link from "next/link";
import { PageLayout, PageHeading } from "@/shared/components/PageLayout";
import { StatusBadge } from "@/shared/components/StatusBadge";
import { Button } from "@/shared/components/Button";
import { LoadingState } from "@/shared/components/LoadingState";
import { ErrorMessage } from "@/shared/components/ErrorMessage";
import {
  workflowGovernanceNavItems,
  WORKFLOW_GOVERNANCE_BASE,
} from "@/shared/constants/navigation";
import {
  getCaseDetail,
  reanalyzeDocument,
  verifyFact,
  publishSnapshot,
  addSupportingDocument,
  downloadCaseDocument,
  getCaseCompletenessSummary,
  assessDocumentCompleteness,
  assessProposalContent,
  createCorrectionRequest,
  reviewCorrection,
} from "@/modules/workflow-governance/services/workflowGovernanceApi";
import type {
  CaseDetailResponse,
  CaseGovernedFieldRowDto,
  CaseDocumentSummaryDto,
  VerifyFactApiRequest,
  CaseCompletenessSummaryResponse,
  CaseCorrectionRequestDto,
} from "@/modules/workflow-governance/types/caseManagement";
import styles from "./CaseDetailPage.module.css";
import { HumanEvidenceWorkspace } from "@/modules/workflow-governance/components/HumanEvidenceWorkspace";
import { ProjectFulfillmentWorkspace } from "@/modules/workflow-governance/components/ProjectFulfillmentWorkspace";
import { WorkflowExecutionWorkspace } from "@/modules/workflow-governance/components/WorkflowExecutionWorkspace";
import { TemplateCompletenessReview } from "@/modules/workflow-governance/components/TemplateCompletenessReview";
import { WorkflowPlanWorkspace } from "@/modules/workflow-governance/components/WorkflowPlanWorkspace";
import { selectedProposalDocument } from "@/modules/workflow-governance/services/proposalWorkspaceSelection";
import { ConsolidatedCaseScreening } from "@/modules/component-04/components/ConsolidatedCaseScreening";

export default function CaseDetailPage() {
  const params = useParams();
  const caseId = params.caseId as string;
  const router = useRouter();

  const [detail, setDetail] = useState<CaseDetailResponse | null>(null);
  const [completenessSummary, setCompletenessSummary] = useState<CaseCompletenessSummaryResponse | null>(null);
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [error, setError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState<"facts" | "documents" | "completeness" | "screening" | "planning" | "execution" | "fulfillment" | "overview" | "governance">("overview");
  useEffect(()=>{const open=(event:Event)=>{const view=(event as CustomEvent<string>).detail;if(view==="facts"||view==="completeness")setActiveTab(view);};window.addEventListener("open-case-view",open);return()=>window.removeEventListener("open-case-view",open);},[]);
  useEffect(()=>{const view=new URLSearchParams(window.location.search).get("view");if(view==="facts"||view==="completeness")setActiveTab(view);},[caseId]);
  const [selectedProposalId, setSelectedProposalId] = useState<string | undefined>();

  // Reanalysis and action states
  const [isActionInProgress, setIsActionInProgress] = useState<boolean>(false);
  const [downloadingDocId, setDownloadingDocId] = useState<string | null>(null);
  const [actionSuccessMessage, setActionSuccessMessage] = useState<string | null>(null);
  const [actionErrorMessage, setActionErrorMessage] = useState<string | null>(null);

  // Correction request modal state
  const [showCorrectionModal, setShowCorrectionModal] = useState<boolean>(false);
  const [correctionAction, setCorrectionAction] = useState<string>("UploadSupportingDocument");
  const [correctionCode, setCorrectionCode] = useState<string>("SurveyPlan");
  const [correctionSection, setCorrectionSection] = useState<string>("");
  const [correctionInstructions, setCorrectionInstructions] = useState<string>("");
  const [correctionDueDate, setCorrectionDueDate] = useState<string>("");
  const [reviewReasons, setReviewReasons] = useState<Record<string, string>>({});

  const handleReviewCorrection = async (request: CaseCorrectionRequestDto, accept: boolean) => {
    setIsActionInProgress(true);
    setActionErrorMessage(null);
    try {
      await reviewCorrection(caseId, request.id, accept, reviewReasons[request.id] || "", request.revision);
      setCompletenessSummary(await getCaseCompletenessSummary(caseId, "officer"));
      setActionSuccessMessage(accept ? "Submission accepted. Run both completeness checks to reassess." : "Further changes requested.");
    } catch (err: unknown) {
      setActionErrorMessage(err instanceof Error ? err.message : "Review failed.");
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Verification modal / inline state
  const [factFilter,setFactFilter]=useState<ReviewFilter>("Needs review");
  const [saveAndNext,setSaveAndNext]=useState(false);
  const [selectedField, setSelectedField] = useState<CaseGovernedFieldRowDto | null>(null);
  const [decisionType, setDecisionType] = useState<"Confirm" | "Correct" | "MarkUnsupported">("Confirm");
  const [correctedValue, setCorrectedValue] = useState<string>("");
  const [officerReason, setOfficerReason] = useState<string>("");

  // Document upload state
  const [uploadCategory, setUploadCategory] = useState<string>("SupportingDocument");
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [isUploadingDoc, setIsUploadingDoc] = useState<boolean>(false);

  const fileInputId = useId();

  const handleDownload = async (doc: CaseDocumentSummaryDto) => {
    if (!detail) return;
    setDownloadingDocId(doc.documentId);
    setActionErrorMessage(null);
    try {
      await downloadCaseDocument(
        detail.leaseCaseId,
        doc.documentId,
        doc.activeVersionId,
        doc.originalFileName,
        "officer"
      );
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to download document.";
      setActionErrorMessage(msg);
    } finally {
      setDownloadingDocId(null);
    }
  };

  const loadCaseData = async (proposalId = selectedProposalId, retainEditor = false) => {
    if (!retainEditor) { setIsLoading(true); setDetail(null); setCompletenessSummary(null); }
    setError(null);
    try {
      const [data, comp] = await Promise.all([
        getCaseDetail(caseId, "officer", proposalId),
        getCaseCompletenessSummary(caseId, "officer"),
      ]);
      if (data.fieldInventory.length === 0) throw new Error("Officer access is required to load the Proposal fact inventory. Sign in with your assigned officer identity.");
      setDetail(data);
      setCompletenessSummary(comp);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to load case workspace.";
      setError(msg);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    if (caseId) {
      loadCaseData();
    }
  }, [caseId]);

  // Handle Check A: Required Supporting Documents Completeness
  const handleRunDocCompleteness = async () => {
    if (!caseId) return;
    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);
    try {
      const res = await assessDocumentCompleteness(caseId, "DOC-REQ-AGRI-V1", "1.0", "officer");
      const summary = await getCaseCompletenessSummary(caseId, "officer");
      setCompletenessSummary(summary);
      setActionSuccessMessage(
        `Check A (Supporting Documents) assessed: Outcome is ${res.outcome} (${res.requirements.length} configured requirements evaluated).`
      );
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to assess document completeness.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Check B: Required Proposal Content Completeness
  const handleRunPropCompleteness = async () => {
    if (!caseId) return;
    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);
    try {
      const res = await assessProposalContent(caseId, "STD-PROP-2026-V1", "1.0", "officer", detail?.proposalSelection?.documentId);
      const summary = await getCaseCompletenessSummary(caseId, "officer");
      setCompletenessSummary(summary);
      setActionSuccessMessage(
        `Check B (Proposal Content) assessed: Outcome is ${res.outcome} (${res.satisfiedMandatory.length} mandatory sections satisfied).`
      );
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to assess proposal content completeness.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Officer creating administrative correction request
  const handleCreateCorrection = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!caseId) return;
    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);
    try {
      await createCorrectionRequest(
        caseId,
        {
          requiredAction: correctionAction,
          requiredClassificationCode: correctionCode,
          requiredSectionId: correctionSection || undefined,
          officerInstructions: correctionInstructions,
          dueDateUtc: correctionDueDate ? new Date(correctionDueDate).toISOString() : undefined,
        },
        "officer"
      );
      setShowCorrectionModal(false);
      setCorrectionInstructions("");
      const summary = await getCaseCompletenessSummary(caseId, "officer");
      setCompletenessSummary(summary);
      setActionSuccessMessage("Administrative correction request created and dispatched to applicant.");
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to create correction request.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Retrying Analysis on an Existing Document Version
  const handleReanalyze = async (doc: CaseDocumentSummaryDto) => {
    if (!detail) return;
    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);

    try {
      const res = await reanalyzeDocument(
        detail.leaseCaseId,
        doc.documentId,
        doc.activeVersionId,
        { executionMode: "ocr", semanticProvider: "gemini" },
        "officer"
      );
      setActionSuccessMessage(
        `Re-analysis initiated successfully. Extracted ${res.candidatesCount} candidate facts.`
      );
      await loadCaseData();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Document analysis failed.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Human Fact Verification
  const handleVerifySubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !selectedField || !selectedField.extractedFactId) return;

    // Find the primary document's analysis id
    const doc = selectedProposalDocument(detail);
    if (!doc?.documentAnalysisId || !doc.latestRunResultId || doc.latestRunState !== "Completed") {
      setActionErrorMessage("No active document analysis found for this case.");
      return;
    }

    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);

    try {
      // Invariant: analysis revision
      const request: VerifyFactApiRequest = {
        documentAnalysisId: doc.documentAnalysisId,
        analysisRunResultId: doc.latestRunResultId,
        extractedFactId: selectedField.extractedFactId,
        expectedAnalysisRevision: doc.documentAnalysisRevision || 1,
        decision: decisionType,
        correctedValue: decisionType === "Correct" ? correctedValue : undefined,
        reason: officerReason || `Officer verified as ${decisionType}`,
      };

      await verifyFact(detail.leaseCaseId, request, "officer");
      setActionSuccessMessage(`Fact ${selectedField.displayName} marked as ${decisionType}.`);
      const nextField = saveAndNext ? detail.fieldInventory.slice(detail.fieldInventory.findIndex(f=>f.factCode===selectedField.factCode)+1).find(f => f.extractedFactId && matchesFactFilter(f.status, "Needs review")) : null;
      setSelectedField(nextField || null);
      setDecisionType("Confirm");
      setCorrectedValue("");
      setOfficerReason("");
      await loadCaseData(selectedProposalId, true);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to record verification.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Publishing Verified Snapshot
  const handlePublishSnapshot = async () => {
    if (!detail) return;
    const doc = selectedProposalDocument(detail);
    if (!doc?.documentAnalysisId || !doc.latestRunResultId || doc.latestRunState !== "Completed") {
      setActionErrorMessage("No active analysis found to publish.");
      return;
    }

    setIsActionInProgress(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);

    try {
      await publishSnapshot(
        detail.leaseCaseId,
        {
          documentAnalysisId: doc.documentAnalysisId,
          analysisRunResultId: doc.latestRunResultId,
          expectedAnalysisRevision: doc.documentAnalysisRevision || 1,
          expectedLeaseCaseRevision: detail.revision,
        },
        "officer"
      );
      setActionSuccessMessage("Immutable verified fact snapshot successfully published and linked to case!");
      await loadCaseData();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Snapshot publication failed.";
      setActionErrorMessage(msg);
    } finally {
      setIsActionInProgress(false);
    }
  };

  // Handle Uploading Supporting Document
  const handleUploadDocument = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!detail || !uploadFile) return;

    setIsUploadingDoc(true);
    setActionSuccessMessage(null);
    setActionErrorMessage(null);

    const formData = new FormData();
    formData.append("file", uploadFile);
    formData.append("logicalCategory", uploadCategory);

    try {
      await addSupportingDocument(detail.leaseCaseId, formData, "officer");
      setActionSuccessMessage(`Supporting document ${uploadFile.name} attached successfully.`);
      setUploadFile(null);
      await loadCaseData();
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Failed to attach document.";
      setActionErrorMessage(msg);
    } finally {
      setIsUploadingDoc(false);
    }
  };

  if (isLoading) {
    return (
      <PageLayout
        navigationItems={workflowGovernanceNavItems}
        navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
      >
        <LoadingState message="Loading Case Workspace..." />
      </PageLayout>
    );
  }

  if (error || !detail) {
    return (
      <PageLayout
        navigationItems={workflowGovernanceNavItems}
        navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
      >
        <ErrorMessage
          title="Case Workspace Error"
          message={error || "Case details could not be found."}
        />
      </PageLayout>
    );
  }

  const proposalSnapshot = detail.proposalSelection?.linkedSnapshotMatchesSelection ? detail.snapshot : null;

  const unverifiedFacts = detail.fieldInventory.filter(
    (f) => f.extractedFactId && (f.status === "Extracted" || f.status === "Unavailable")
  );
  const isSnapshotPublishable = unverifiedFacts.length === 0 && detail.fieldInventory.some((f) => f.status === "Confirmed" || f.status === "Corrected");

  return (
    <PageLayout
      navigationItems={workflowGovernanceNavItems}
      navigationBasePath={WORKFLOW_GOVERNANCE_BASE}
    >
      <div className={styles.workspaceHeader}>
        <div className={styles.headerLeft}>
          <div className={styles.breadcrumb}>
            <Link href="/workflow-governance/cases">← Back to Case Queue</Link>
          </div>
          <h1 className={styles.caseTitle}>
            Case {detail.applicationReference}
          </h1>
          <div className={styles.caseMetaRow}>
            <details><summary>Case identifier</summary>{detail.leaseCaseId}</details>
            <span className={styles.metaItem}>
              <strong>Revision:</strong> {detail.revision}
            </span>
            <span className={styles.metaItem}>
              <strong>Submitted:</strong> {new Date(detail.createdAt).toLocaleString()}
            </span>
            <span className={styles.metaItem}>
              <strong>Jurisdiction:</strong> {detail.jurisdictionCode || "Not recorded"}
            </span>
          </div>
        </div>
        <div className={styles.headerRight}>
          <StatusBadge
            tone={
              detail.status === "ApprovedWithConditions" || detail.status === "HandedOff"
                ? "success"
                : detail.status === "InWorkflow"
                ? "primary"
                : "neutral"
            }
          >
            {detail.status}
          </StatusBadge>
          {proposalSnapshot ? (
            <StatusBadge tone="success">Snapshot Published</StatusBadge>
          ) : (
            <StatusBadge tone="warning">Pending Snapshot</StatusBadge>
          )}
        </div>
      </div>

      <section aria-label="Proposal selection">
        <h2>Proposal review source</h2>
        <label htmlFor="proposal-document-selection">Proposal document</label>
        <select id="proposal-document-selection" value={detail.proposalSelection?.documentId || ""} disabled={isActionInProgress}
          onChange={e => { const id = e.target.value || undefined; setSelectedProposalId(id); setSelectedField(null); void loadCaseData(id); }}>
          <option value="">Select a Proposal</option>
          {detail.documents.filter(d => d.logicalCategory.toLowerCase() === "proposal").map(d =>
            <option key={d.documentId} value={d.documentId}>{d.originalFileName}</option>)}
        </select>
        <details><summary>Technical source identifiers</summary><p>Document: {detail.proposalSelection?.documentId || "None"}<br />Version: {detail.proposalSelection?.versionId || "None"}<br />
          Analysis: {detail.proposalSelection?.analysisId || "Not analyzed"}<br />Run: {detail.proposalSelection?.runId || "None"}</p></details>
        {detail.proposalSelection?.notice && <p role="status">{detail.proposalSelection.notice}</p>}
      </section>

      {activeTab === "overview" && <section className={styles.guidedOverview} aria-label="Case overview">
        <h2>Case overview</h2>
        <p><strong>Current work:</strong> {caseNextAction(detail,completenessSummary,!!proposalSnapshot).stage}. <strong>Recorded case state:</strong> {detail.status}.</p>
        <p><strong>Responsible role:</strong> assigned coordinating / DS officer; applicant responds to requested corrections.</p>
        <p><strong>Outstanding next action:</strong> {caseNextAction(detail,completenessSummary,!!proposalSnapshot).text}</p>
        <Button onClick={()=>setActiveTab(caseNextAction(detail,completenessSummary,!!proposalSnapshot).view)}>Continue</Button>
        <p>No stage is completed by opening its screen. The actions below recheck current inputs and permission on the server.</p>
      </section>}
      {activeTab !== "overview" && <p className={styles.workspacePosition}><strong>{caseNextAction(detail,completenessSummary,!!proposalSnapshot).stage}</strong> · {caseNextAction(detail,completenessSummary,!!proposalSnapshot).text} <button onClick={()=>setActiveTab("overview")}>Case overview</button></p>}
      {/* Notifications */}
      {activeTab === "facts" && detail.fieldInventory.some(f => ["investment_amount", "investment_currency"].includes(f.factCode) && !["Confirmed", "Corrected"].includes(f.status)) && <p>
        Investment amount/currency require reviewed source evidence. Use the human-evidence form only for a source-backed page and quote, or <button onClick={() => {
          setCorrectionAction("ReviseProposal"); setCorrectionCode("Proposal"); setCorrectionSection("investment_and_funding"); setCorrectionInstructions("Please supply the proposed investment amount and currency with a clear source document/page. Do not infer these values from other financial figures."); setShowCorrectionModal(true);
        }}>Request applicant investment evidence</button>. No value is filled automatically.</p>}
      {actionSuccessMessage && (
        <div className={styles.successBanner}>{actionSuccessMessage}</div>
      )}
      {actionErrorMessage && (
        <div className={styles.errorBanner}>{actionErrorMessage}</div>
      )}

      <nav className={styles.guidedStages} aria-label="Case workflow stages">
        {([ ["overview","Overview"],["documents","1. Submission"],["facts","2. Verification & Completeness"],["screening","3. C4 Screening"],["planning","4. Department Approvals"],["fulfillment","5. Fulfillment & Handoff"] ] as const).map(([tab,label])=><button key={tab} className={`${styles.tabButton} ${(activeTab===tab || (tab==="facts"&&activeTab==="completeness") || (tab==="planning"&&activeTab==="execution")) ? styles.tabActive : ""}`} aria-current={activeTab===tab?"step":undefined} onClick={()=>setActiveTab(tab)}>{label}</button>)}
      </nav>
      <div className={styles.tabBar} aria-label="Workspace views">
        {(["facts","completeness"].includes(activeTab)) && <>
          <button className={styles.tabButton} aria-pressed={activeTab==="facts"} onClick={()=>setActiveTab("facts")}>17-Field Fact Verification ({detail.fieldInventory.length})</button>
          <button className={styles.tabButton} aria-pressed={activeTab==="completeness"} onClick={()=>setActiveTab("completeness")}>Completeness & Corrections</button>
        </>}
        {(["planning","execution"].includes(activeTab)) && <>
          <button className={styles.tabButton} onClick={()=>setActiveTab("planning")}>Plan preview & history</button>
          <button className={styles.tabButton} onClick={()=>setActiveTab("execution")}>Institutional execution</button>
        </>}
        <button className={styles.tabButton} onClick={()=>setActiveTab("governance")}>Post-workflow governance & audit</button>
      </div>
      {["screening","planning","execution","fulfillment"].includes(activeTab) && <aside className={styles.stageHelp}>
        <strong>Prerequisites:</strong> {activeTab==="screening" ? "Current reviewed snapshot, both completeness checks and explicit evidence/policy coverage." : activeTab==="planning"||activeTab==="execution" ? "Current nonblocking C4 screening and approved applicable routing; execution also requires an approved plan and institutional assignment." : "Completed institutional decisions, required documents and fulfilled conditions; handoff also needs a real downstream acknowledgement."}
        <button onClick={()=>setActiveTab(activeTab==="screening"?"completeness":activeTab==="fulfillment"?"execution":"screening")}>Open prerequisite</button>
      </aside>}
      {activeTab === "governance" && <PostWorkflowGovernanceWorkspace caseId={caseId} />}
      {activeTab === "planning" && <WorkflowPlanWorkspace caseId={caseId} />}
      {activeTab === "execution" && <WorkflowExecutionWorkspace caseId={caseId} />}
      {activeTab === "fulfillment" && <ProjectFulfillmentWorkspace caseId={caseId} hidePostWorkflow />}
      {/* TAB 1: 17-Field Fact Review */}
      {activeTab === "screening" && <ConsolidatedCaseScreening caseId={caseId} snapshotId={proposalSnapshot?.snapshotId} />}
      {activeTab === "facts" && (
        <div className={styles.tabContent}>
          {detail.proposalSelection?.documentId && <HumanEvidenceWorkspace caseId={caseId}
            documentId={detail.proposalSelection.documentId} caseRevision={detail.revision}
            linkedSnapshotId={proposalSnapshot?.snapshotId} fields={detail.fieldInventory}
            onLinked={() => void loadCaseData()} />}
          {(() => {
            const proposalDoc = selectedProposalDocument(detail);
            if (proposalDoc?.latestRunState === "Failed") {
              return (
                <div className={styles.errorBanner} style={{ marginBottom: "1.25rem" }}>
                  <strong>Document Intelligence Notice:</strong> Automated OCR / Semantic extraction encountered an issue ({proposalDoc.latestRunFailureCode || "FAILED"}: {proposalDoc.latestRunFailureDescription || "Analysis did not complete"}). The uploaded proposal document remains safely preserved in storage. You can view or download the document, or trigger a re-analysis retry, under the &ldquo;Governed Documents&rdquo; tab.
                </div>
              );
            }
            return null;
          })()}

          <div className={styles.reviewBanner}>
            <div>
              <h3>Administrative Fact Verification & Grounding</h3>
              <p>
                Review candidate facts extracted by Document Intelligence against their source evidence.
                Each candidate fact must be confirmed, corrected, or rejected before publishing an immutable snapshot.
              </p>
            </div>
            <div className={styles.publishAction}>
              {proposalSnapshot ? (
                <div className={styles.snapshotPublishedBox}>
                  <strong>Snapshot Published:</strong> {proposalSnapshot.snapshotId.slice(0, 8)}…
                  <br />
                  <span>{proposalSnapshot.publishedFactCount} verified facts linked</span>
                </div>
              ) : (
                <Button
                  variant="primary"
                  disabled={!detail.canPublishSnapshot || !isSnapshotPublishable || isActionInProgress}
                  onClick={handlePublishSnapshot}
                >
                  {isActionInProgress ? "Publishing..." : "Publish Immutable Snapshot"}
                </Button>
              )}
            </div>
          </div>

          {!proposalSnapshot && unverifiedFacts.length > 0 && (
            <div className={styles.unverifiedWarning}>
              Notice: {unverifiedFacts.length} candidate fact(s) are awaiting officer review before snapshot publication can occur.
            </div>
          )}

          <fieldset><legend>Fact inventory filter</legend>{reviewFilters.map(filter=><button type="button" key={filter} aria-pressed={factFilter===filter} onClick={()=>setFactFilter(filter)}>{filter}</button>)}</fieldset>
          <p>{detail.fieldInventory.filter(f=>matchesFactFilter(f.status,factFilter)).length} of {detail.fieldInventory.length} fields shown. Reviewed means a persisted decision, including an unsupported finding. Missing candidates do not prove content is absent from the source.</p>
          <div className={styles.factTableWrapper}>
            <table className={styles.factTable}>
              <thead>
                <tr>
                  <th>Field Code & Name</th>
                  <th>Extracted / Preferred Value</th>
                  <th>Status</th>
                  <th>Confidence & Evidence Grounding</th>
                  <th>Officer Action</th>
                </tr>
              </thead>
              <tbody>
                {detail.fieldInventory.filter(row=>matchesFactFilter(row.status,factFilter)).map((row) => {
                  const isExtracted = Boolean(row.extractedFactId);
                  const isVerified = row.status === "Confirmed" || row.status === "Corrected" || row.status === "Rejected" || row.status === "Verified";
                  const proposalDoc = selectedProposalDocument(detail);
                  const isAnalysisFailed = proposalDoc?.latestRunState === "Failed";
                  const isAnalysisPending = proposalDoc?.latestRunState === "Running" || proposalDoc?.latestRunState === "Requested";

                  return (
                    <tr key={row.factCode} className={styles.factTableRow}>
                      <td className={styles.factCodeCell}>
                        <div className={styles.factName}>{row.displayName}</div>
                        <code className={styles.factCode}>{row.factCode}</code>
                      </td>
                      <td className={styles.factValueCell}>
                        {row.preferredValue ? (
                          <div className={styles.preferredVal}>{row.preferredValue}</div>
                        ) : row.candidateValue ? (
                          <div className={styles.candidateVal}>{row.candidateValue}</div>
                        ) : row.status === "Unavailable" ? (
                          <span className={styles.unavailableTag}>
                            {isAnalysisFailed
                              ? "[Unavailable — Analysis Failed, Retry in Documents tab]"
                              : isAnalysisPending
                              ? "[Unavailable — Analysis In Progress]"
                              : "[Unavailable]"}
                          </span>
                        ) : row.status === "NoCandidate" ? (
                          <span className={styles.unavailableTag}>[No Candidate Extracted]</span>
                        ) : row.status === "Missing" ? (
                          <span className={styles.missingTag}>[Evidence-Confirmed Absence / Not Provided]</span>
                        ) : (
                          <span className={styles.missingTag}>[Not Extracted]</span>
                        )}
                        {row.status === "Corrected" && row.candidateValue && (
                          <div className={styles.origSub}>Original: {row.candidateValue}</div>
                        )}
                        {row.status === "Ungrounded" && (
                          <div style={{ fontSize: "0.75rem", color: "#d97706", fontWeight: 600, marginTop: "0.25rem" }}>
                            [Ungrounded — Citation Unverified in OCR]
                          </div>
                        )}
                      </td>
                      <td>
                        <StatusBadge
                          tone={
                            row.status === "Confirmed" || row.status === "Verified"
                              ? "success"
                              : row.status === "Corrected"
                              ? "primary"
                              : row.status === "Rejected" || row.status === "Unsupported"
                              ? "danger"
                              : row.status === "Extracted"
                              ? "neutral"
                              : row.status === "Ungrounded"
                              ? "warning"
                              : "neutral"
                          }
                        >
                          {row.status === "Ungrounded"
                            ? "Ungrounded Candidate"
                            : row.status === "NoCandidate"
                            ? "No Candidate"
                            : row.status}
                        </StatusBadge>
                      </td>
                      <td className={styles.groundingCell}>
                        {row.sourceKind === "HumanDocumentEvidence" && <p>Human document evidence (page {row.pageNumber}); no machine candidate or confidence is asserted.</p>}
                        {row.confidenceScore !== null && row.confidenceScore !== undefined ? (
                          <div className={styles.confidenceScore}>
                            Confidence: {Math.round(row.confidenceScore * 100)}%
                            {row.pageNumber ? ` (Page ${row.pageNumber})` : ""}
                          </div>
                        ) : null}
                        {row.evidenceQuote ? (
                          <blockquote className={styles.evidenceQuote}>
                            &ldquo;{row.evidenceQuote}&rdquo;
                          </blockquote>
                        ) : (
                          <span className={styles.noEvidence}>No quote available</span>
                        )}
                        {row.status === "Ungrounded" && (
                          <div style={{ color: "#b45309", fontSize: "0.7rem", marginTop: "0.25rem" }}>
                            ⚠️ Proposed by model; character match against OCR transcript failed. Check document manually.
                          </div>
                        )}
                      </td>
                      <td className={styles.actionCell}>
                        {isExtracted && !proposalSnapshot && (
                          <Button
                            variant="secondary"
                            disabled={!detail.canVerifyFacts}
                            onClick={() => {
                              setSelectedField(row);
                              setDecisionType(row.status === "Corrected" ? "Correct" : "Confirm");
                              setCorrectedValue(row.preferredValue || row.candidateValue || "");
                              setOfficerReason(row.officerReason || "");
                            }}
                          >
                            {isVerified ? "Change" : "Verify Fact"}
                          </Button>
                        )}
                        {proposalSnapshot && (isVerified || row.evidenceQuote) && (
                          <><span className={styles.lockedText}>Locked in snapshot</span><button type="button" onClick={()=>setSelectedField(row)}>View reviewed source</button></>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* TAB 2: Governed Documents & Re-analysis */}
      {activeTab === "documents" && (
        <div className={styles.tabContent}>
          <div className={styles.documentsHeader}>
            <div>
              <h3>Governed Documents & Re-analysis Pipeline</h3>
              <p>
                Immutable document versions with SHA-256 provenance. Re-running analysis preserves this case and version history.
              </p>
            </div>
          </div>

          <div className={styles.docTableWrapper}>
            <table className={styles.factTable}>
              <thead>
                <tr>
                  <th>Category</th>
                  <th>Original Filename</th>
                  <th>Version</th>
                  <th>Size</th>
                  <th>Checksum (SHA-256)</th>
                  <th>Analysis Status</th>
                  <th>Candidates</th>
                  <th>Action</th>
                </tr>
              </thead>
              <tbody>
                {detail.documents.map((doc) => (
                  <tr key={doc.documentId}>
                    <td>
                      <strong>{doc.logicalCategory}</strong>
                    </td>
                    <td>{doc.originalFileName}</td>
                    <td>v{doc.versionNumber}</td>
                    <td>{(doc.fileSizeBytes / 1024).toFixed(1)} KB</td>
                    <td className={styles.checksumCell}>
                      <code>{doc.checksumValue ? `${doc.checksumValue.slice(0, 12)}…` : "N/A"}</code>
                    </td>
                    <td>
                      <StatusBadge
                        tone={
                          doc.latestRunState === "Completed"
                            ? "success"
                            : doc.latestRunState === "Failed"
                            ? "danger"
                            : "neutral"
                        }
                      >
                        {doc.latestRunState || "Not Run"}
                      </StatusBadge>
                      {doc.latestRunState === "Failed" && (
                        <div style={{ fontSize: "0.75rem", color: "#b91c1c", marginTop: "0.35rem", maxWidth: "220px", wordBreak: "break-word" }}>
                          <strong>{doc.latestRunFailureCode || "ERROR"}:</strong> {doc.latestRunFailureDescription || "Analysis did not complete"}
                        </div>
                      )}
                    </td>
                    <td>{doc.latestRunCandidatesCount} facts</td>
                    <td className={styles.actionCell}>
                      <div style={{ display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
                        <Button
                          variant="secondary"
                          disabled={downloadingDocId === doc.documentId}
                          onClick={() => handleDownload(doc)}
                        >
                          {downloadingDocId === doc.documentId ? "Downloading..." : "View / Download"}
                        </Button>
                        <Button
                          variant="secondary"
                          disabled={isActionInProgress}
                          onClick={() => handleReanalyze(doc)}
                        >
                          {isActionInProgress ? "Running..." : "Retry Analysis"}
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Attach Supporting Document Section */}
          <div className={styles.uploadSection}>
            <h4>Attach a New Governed Document</h4>
            <p>Select Proposal to intake a separate proposal document on this case. This does not reclassify existing documents or copy their analysis, verification or snapshots. Run analysis on the new document after upload.</p>
            <form onSubmit={handleUploadDocument} className={styles.uploadForm}>
              <div className={styles.formGroup}>
                <label>Document Category</label>
                <select
                  value={uploadCategory}
                  onChange={(e) => setUploadCategory(e.target.value)}
                  className={styles.selectInput}
                >
                  <option value="SupportingDocument">Supporting Document</option>
                  <option value="Proposal">Proposal — new document, separate review</option>
                  <option value="SurveyPlan">Survey Plan</option>
                  <option value="NationalIdentityCard">National Identity Card (NIC)</option>
                  <option value="EnvironmentalClearance">Environmental Clearance</option>
                  <option value="ValuationReport">Valuation Report</option>
                </select>
              </div>

              <div className={styles.formGroup}>
                <label htmlFor={fileInputId}>Select File (PDF, PNG, JPG)</label>
                <input
                  id={fileInputId}
                  type="file"
                  accept=".pdf,.png,.jpg,.jpeg"
                  onChange={(e) => setUploadFile(e.target.files?.[0] || null)}
                  className={styles.fileInput}
                />
              </div>

              <Button
                variant="primary"
                type="submit"
                disabled={!uploadFile || isUploadingDoc}
              >
                {isUploadingDoc ? "Attaching..." : "Attach Document"}
              </Button>
            </form>
          </div>
        </div>
      )}

      {/* TAB: Dual Completeness & Corrections Flow */}
      {activeTab === "completeness" && (
        <div className={styles.tabContent}>
          <div className={styles.completenessContainer}>
            {/* Staleness Banner */}
            {completenessSummary &&
              (assessmentState(completenessSummary.documentCompleteness, completenessSummary.isDocumentCompletenessCurrent) === "Stale" ||
                assessmentState(completenessSummary.proposalContent, completenessSummary.isProposalContentCurrent) === "Stale") && (
                <div className={styles.stalenessAlert}>
                  <div className={styles.stalenessText}>
                    <strong>Stale Assessment Detected</strong>
                    Document versions or verified fact snapshots have evolved since the last evaluation. Re-run checks to verify current compliance.
                  </div>
                  <div style={{ display: "flex", gap: "0.5rem" }}>
                    {!completenessSummary.isDocumentCompletenessCurrent && (
                      <Button
                        variant="secondary"
                        disabled={isActionInProgress}
                        onClick={handleRunDocCompleteness}
                      >
                        Re-run Doc Check
                      </Button>
                    )}
                    {!completenessSummary.isProposalContentCurrent && (
                      <Button
                        variant="secondary"
                        disabled={isActionInProgress}
                        onClick={handleRunPropCompleteness}
                      >
                        Re-run Proposal Check
                      </Button>
                    )}
                  </div>
                </div>
              )}

            {completenessSummary && <CompletenessNextActions caseId={caseId} summary={completenessSummary} onChanged={() => void loadCaseData()}
              onRequest={(section, instructions) => { setCorrectionAction("ReviseProposal"); setCorrectionCode("Proposal"); setCorrectionSection(section); setCorrectionInstructions(instructions); setShowCorrectionModal(true); }} />}
            <TemplateCompletenessReview caseId={caseId} documentId={detail.proposalSelection?.documentId} versionId={detail.proposalSelection?.versionId} isConfirmed={completenessSummary?.proposalContent?.isConfirmed} assessmentId={completenessSummary?.proposalContent?.id} onChanged={() => void loadCaseData(selectedProposalId, true)} />
            {/* Check A and Check B Columns */}
            <div className={styles.completenessColumns}>
              {/* Check A: Required Supporting Documents */}
              <div className={styles.checkCard}>
                <div className={styles.checkCardHeader}>
                  <div className={styles.checkTitleArea}>
                    <h3>Check A: Supporting Documents</h3>
                    <p>Evaluated against the configured document requirement set</p>
                  </div>
                  <div>
                    {completenessSummary?.documentCompleteness ? (
                      <StatusBadge
                        tone={
                          completenessSummary.documentCompleteness.outcome === "Complete" && completenessSummary.isDocumentCompletenessCurrent
                            ? "success"
                            : "warning"
                        }
                      >
                        {completenessSummary.documentCompleteness.outcome}
                      </StatusBadge>
                    ) : (
                      <StatusBadge tone="neutral">{completenessSummary?.documentRequirementsConfigured ? "Not assessed" : "Requirements not configured"}</StatusBadge>
                    )}
                  </div>
                </div>

                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div className={styles.checkMetrics}>
                    <div className={styles.metricItem}>
                      <span>Requirements</span>
                      <strong>{completenessSummary?.documentCompleteness?.requirements.length || 0}</strong>
                    </div>
                    <div className={styles.metricItem}>
                      <span>Deficits</span>
                      <strong style={{ color: (completenessSummary?.documentCompleteness?.missingRequirements.length || 0) > 0 ? "#b91c1c" : "#10b981" }}>
                        {completenessSummary?.documentCompleteness?.missingRequirements.length || 0}
                      </strong>
                    </div>
                  </div>
                  <Button
                    variant="secondary"
                    disabled={isActionInProgress}
                    onClick={handleRunDocCompleteness}
                  >
                    {isActionInProgress ? "Evaluating..." : "Run Document Check"}
                  </Button>
                </div>

                {completenessSummary?.documentCompleteness?.missingRequirements &&
                  completenessSummary.documentCompleteness.missingRequirements.length > 0 && (
                    <div className={styles.explanationBox} style={{ background: "#fef2f2", borderColor: "#fecaca" }}>
                      <strong style={{ color: "#991b1b" }}>Outstanding Document Deficits:</strong>
                      <ul>
                        {completenessSummary.documentCompleteness.missingRequirements.map((m) => (
                          <li key={m.requirementId} style={{ color: "#991b1b" }}>
                            <strong>{m.requiredClassificationCode}:</strong> {m.description} ({m.satisfiedCount}/{m.requiredCount} satisfied)
                          </li>
                        ))}
                      </ul>
                    </div>
                  )}

                <div className={styles.factTableWrapper}>
                  <table className={styles.factTable}>
                    <thead>
                      <tr>
                        <th>Required Document</th>
                        <th>Criticality</th>
                        <th>Status</th>
                        <th>Description</th>
                      </tr>
                    </thead>
                    <tbody>
                      {completenessSummary?.documentCompleteness?.requirements.map((req) => {
                        const isMissing = completenessSummary.documentCompleteness?.missingRequirements.some(
                          (m) => m.requiredClassificationCode === req.requiredClassificationCode
                        );
                        return (
                          <tr key={req.id}>
                            <td>
                              <strong>{req.requiredClassificationCode}</strong>
                            </td>
                            <td>
                              <span style={{ fontSize: "0.75rem", fontWeight: 600, color: req.criticality === "Mandatory" ? "#b91c1c" : "#4b5563" }}>
                                {req.criticality}
                              </span>
                            </td>
                            <td>
                              <StatusBadge tone={isMissing ? "danger" : "neutral"}>
                                {req.applicability === "NotRequired" ? "Not applicable"
                                  : req.applicability === "Undetermined" ? "Applicability unresolved"
                                  : req.criticality === "Optional" ? "Optional"
                                  : isMissing ? "Missing"
                                  : completenessSummary.isDocumentCompletenessCurrent && completenessSummary.documentCompleteness?.outcome === "Complete"
                                    ? "Satisfied" : "Needs review / reassessment"}
                              </StatusBadge>
                            </td>
                            <td style={{ fontSize: "0.8rem", color: "#4b5563" }}>
                              {req.description}
                            </td>
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
              </div>

              {/* Check B: Required Proposal Content */}
              <div className={styles.checkCard}>
                <div className={styles.checkCardHeader}>
                  <div className={styles.checkTitleArea}>
                    <h3>Check B: Proposal Content</h3>
                    <p>Evaluated against configured template <code>STD-PROP-2026-V1</code></p>
                  </div>
                  <div>
                    {completenessSummary?.proposalContent ? (
                      <StatusBadge
                        tone={
                          completenessSummary.proposalContent.outcome === "Complete" && completenessSummary.proposalContent.isConfirmed && completenessSummary.isProposalContentCurrent
                            ? "success"
                            : completenessSummary.proposalContent.outcome !== "Incomplete"
                            ? "warning"
                            : "danger"
                        }
                      >
                        {completenessSummary.proposalContent.outcome}
                      </StatusBadge>
                    ) : (
                      <StatusBadge tone="neutral">{completenessSummary?.proposalTemplateConfigured ? "Not assessed" : "Requirements not configured"}</StatusBadge>
                    )}
                  </div>
                </div>

                <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
                  <div className={styles.checkMetrics}>
                    <div className={styles.metricItem}>
                      <span>Satisfied</span>
                      <strong style={{ color: "#10b981" }}>
                        {completenessSummary?.proposalContent?.satisfiedMandatory.length || 0}
                      </strong>
                    </div>
                    <div className={styles.metricItem}>
                      <span>Missing</span>
                      <strong style={{ color: (completenessSummary?.proposalContent?.missingMandatory.length || 0) > 0 ? "#b91c1c" : "#10b981" }}>
                        {completenessSummary?.proposalContent?.missingMandatory.length || 0}
                      </strong>
                    </div>
                    <div className={styles.metricItem}>
                      <span>Review Req</span>
                      <strong style={{ color: (completenessSummary?.proposalContent?.reviewRequiredMandatory.length || 0) > 0 ? "#d97706" : "#4b5563" }}>
                        {completenessSummary?.proposalContent?.reviewRequiredMandatory.length || 0}
                      </strong>
                    </div>
                  </div>
                  <Button
                    variant="secondary"
                    disabled={isActionInProgress}
                    onClick={handleRunPropCompleteness}
                  >
                    {isActionInProgress ? "Evaluating..." : "Run Proposal Check"}
                  </Button>
                </div>

                {completenessSummary?.proposalContent?.explanations &&
                  completenessSummary.proposalContent.explanations.length > 0 && (
                    <div className={styles.explanationBox}>
                      <details><summary>Assessment explanations</summary>
                      <ul>
                        {completenessSummary.proposalContent.explanations.map((exp, idx) => (
                          <li key={idx}>{exp}</li>
                        ))}
                      </ul>
                      </details>
                    </div>
                  )}

                <details><summary>All persisted template findings and configuration sources</summary>
                <div className={styles.factTableWrapper}>
                  <table className={styles.factTable}>
                    <thead>
                      <tr>
                        <th>Section / Field</th>
                        <th>Mandatory</th>
                        <th>Assessment</th>
                        <th>Configuration Source</th>
                      </tr>
                    </thead>
                    <tbody>
                      {/* Satisfied items */}
                      {completenessSummary?.proposalContent?.satisfiedMandatory.map((item) => (
                        <tr key={item.id}>
                          <td>
                            <strong>{item.displayName}</strong>
                            <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>{item.id}</div>
                          </td>
                          <td>Yes</td>
                          <td>
                            <StatusBadge tone="success">Satisfied</StatusBadge>
                          </td>
                          <td>
                            <code>{item.authoritativeSourceReference}</code>
                          </td>
                        </tr>
                      ))}
                      {/* Review Required items */}
                      {completenessSummary?.proposalContent?.reviewRequiredMandatory.map((item) => (
                        <tr key={item.id}>
                          <td>
                            <strong>{item.displayName}</strong>
                            <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>{item.id}</div>
                          </td>
                          <td>Yes</td>
                          <td>
                            <StatusBadge tone="warning">Review Required</StatusBadge>
                          </td>
                          <td>
                            <code>{item.authoritativeSourceReference}</code>
                          </td>
                        </tr>
                      ))}
                      {/* Missing items */}
                      {completenessSummary?.proposalContent?.missingMandatory.map((item) => (
                        <tr key={item.id}>
                          <td>
                            <strong>{item.displayName}</strong>
                            <div style={{ fontSize: "0.75rem", color: "#6b7280" }}>{item.id}</div>
                          </td>
                          <td>Yes</td>
                          <td>
                            <StatusBadge tone="danger">Missing</StatusBadge>
                          </td>
                          <td>
                            <code>{item.authoritativeSourceReference}</code>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                </details>
              </div>
            </div>

            {/* Administrative Corrections Flow Section */}
            <div>
              <div className={styles.correctionsHeader}>
                <div>
                  <h3>Administrative Correction Requests</h3>
                  <p style={{ margin: "0.25rem 0 0 0", fontSize: "0.85rem", color: "var(--color-muted)" }}>
                    Document and revision requests for the applicant.
                  </p>
                </div>
                <Button
                  variant="primary"
                  onClick={() => setShowCorrectionModal(true)}
                >
                  Request Applicant Correction
                </Button>
              </div>

              <div style={{ marginTop: "1rem" }}>
                {completenessSummary?.activeCorrectionRequests &&
                completenessSummary.activeCorrectionRequests.length > 0 ? (
                  <div className={styles.correctionsList}>
                    {completenessSummary.activeCorrectionRequests.map((req) => (
                      <div
                        key={req.id}
                        className={`${styles.correctionItem} ${
                          req.status === "Pending"
                            ? styles.correctionItemPending
                            : styles.correctionItemFulfilled
                        }`}
                      >
                        <div className={styles.correctionItemDetails}>
                          <div className={styles.correctionActionTag}>
                            {req.requiredAction}: {req.requiredClassificationCode || req.requiredSectionId || "Document"}
                          </div>
                          <div className={styles.correctionInstructions}>
                            {req.officerInstructions}
                          </div>
                          <div className={styles.correctionMeta}>
                            Created: {new Date(req.createdAtUtc).toLocaleDateString()}
                            {req.dueDateUtc && ` | Due: ${new Date(req.dueDateUtc).toLocaleDateString()}`}
                            {req.respondedAtUtc && ` | Responded: ${new Date(req.respondedAtUtc).toLocaleDateString()}`}
                          </div>
                        </div>
                        <div>
                          <StatusBadge tone={req.status === "AcceptedAwaitingReassessment" ? "neutral" : "warning"}>
                            {req.status}
                          </StatusBadge>
                          {req.status === "SubmittedAwaitingReview" && (
                            <div>
                              <input aria-label="Review reason" placeholder="Officer review reason" value={reviewReasons[req.id] || ""}
                                onChange={(e) => setReviewReasons({ ...reviewReasons, [req.id]: e.target.value })} />
                              <Button disabled={isActionInProgress || !reviewReasons[req.id]?.trim()} onClick={() => handleReviewCorrection(req, true)}>Accept submission</Button>
                              <Button disabled={isActionInProgress || !reviewReasons[req.id]?.trim()} onClick={() => handleReviewCorrection(req, false)}>Request changes</Button>
                            </div>
                          )}
                        </div>
                      </div>
                    ))}
                  </div>
                ) : (
                  <div style={{ padding: "1.5rem", background: "#f8fafc", borderRadius: "8px", textAlign: "center", color: "#64748b", fontSize: "0.875rem" }}>
                    No correction requests recorded. Completeness is assessed separately.
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* TAB 3: Screening & Governance Gates */}
      {activeTab === "overview" && (
        <div className={styles.tabContent}>
          <div className={styles.gatesGrid}>
            {/* Gate 1: Human Fact Verification */}
            <div className={styles.gateCard}>
              <h4>Gate 1: Fact Verification & Snapshot</h4>
              <p>Ensures 100% human verification coverage over AI-extracted candidate facts.</p>
              <div className={styles.gateStatus}>
                {proposalSnapshot ? (
                  <StatusBadge tone="success">Snapshot Linked</StatusBadge>
                ) : (
                  <StatusBadge tone="warning">Pending Verification</StatusBadge>
                )}
              </div>
              <div className={styles.gateDetails}>
                Snapshot: {proposalSnapshot ? "Published for the selected source" : "Not linked"}
                <br />
                Outcome: {proposalSnapshot?.snapshotOutcome || "Pending"}
              </div>
            </div>

            {/* Gate 2: Component 4 Screening Orchestrator */}
            <div className={styles.gateCard}>
              <h4>Gate 2: Governance Screening Orchestrator</h4>
              <p>Consolidated governance checks, conflict detection, and early governance screening.</p>
              <div className={styles.gateStatus}>
                <Button onClick={() => setActiveTab("screening")}>Check current screening readiness</Button>
              </div>
              <div className={styles.gateDetails}>
                The C4 Screening tab checks completeness, exact verified snapshot, parcel context and evidence coverage. Missing inputs remain blocked; no screening clearance is assumed here.
              </div>
            </div>

            {/* Gate 3: Workflow DAG Planning */}
            <div className={styles.gateCard}>
              <h4>Gate 3: Institutional Workflow Execution</h4>
              <p>Deterministic workflow sequencing based on validated lease typology and regional regulations.</p>
              <div className={styles.gateStatus}>
                <Button onClick={()=>setActiveTab("planning")}>Check plan and department readiness</Button>
              </div>
              <div className={styles.gateDetails}>
                Read the persisted plan and current prerequisites. No plan or activated request is inferred from this overview.
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Verification Modal / Drawer */}
      {selectedField && (
        <div className={styles.modalBackdrop}>
          <div className={`${styles.modalCard} ${styles.reviewDialog}`} role="dialog" aria-modal="true" aria-label="Fact source review">
            <h3>Verify Fact: {selectedField.displayName}</h3>
            <p className={styles.modalSub}>
              Fact Code: <code>{selectedField.factCode}</code>
            </p>

            <ProposalEvidenceViewer caseId={caseId} documentId={detail.proposalSelection?.documentId} versionId={detail.proposalSelection?.versionId} pageNumber={selectedField.pageNumber} />
            {!proposalSnapshot ? <form onSubmit={handleVerifySubmit} className={styles.verifyForm}>
              <label><input type="checkbox" checked={saveAndNext} onChange={e=>setSaveAndNext(e.target.checked)} /> Save & Next: open the next outstanding fact after this review is saved</label>
              <div className={styles.modalFieldGroup}>
                <label>Extracted Candidate Value</label>
                <div className={styles.candidateDisplay}>
                  {selectedField.candidateValue || "[No candidate extracted]"}
                </div>
              </div>

              {selectedField.evidenceQuote && (
                <div className={styles.modalFieldGroup}>
                  <label>Evidence Grounding (Page {selectedField.pageNumber || 1})</label>
                  <blockquote className={styles.modalEvidence}>
                    &ldquo;{selectedField.evidenceQuote}&rdquo;
                  </blockquote>
                </div>
              )}

              <div className={styles.modalFieldGroup}>
                <label>Officer Decision</label>
                <div className={styles.radioGroup}>
                  <label className={styles.radioOption}>
                    <input
                      type="radio"
                      name="decision"
                      value="Confirm"
                      checked={decisionType === "Confirm"}
                      onChange={() => setDecisionType("Confirm")}
                    />
                    <span>Confirm Candidate Value</span>
                  </label>
                  <label className={styles.radioOption}>
                    <input
                      type="radio"
                      name="decision"
                      value="Correct"
                      checked={decisionType === "Correct"}
                      onChange={() => setDecisionType("Correct")}
                    />
                    <span>Correct Extracted Value</span>
                  </label>
                  <label className={styles.radioOption}>
                    <input
                      type="radio"
                      name="decision"
                      value="MarkUnsupported"
                      checked={decisionType === "MarkUnsupported"}
                      onChange={() => setDecisionType("MarkUnsupported")}
                    />
                    <span>Mark Unsupported / Reject</span>
                  </label>
                </div>
              </div>

              {decisionType === "Correct" && (
                <div className={styles.modalFieldGroup}>
                  <label>Corrected Value *</label>
                  <input
                    type="text"
                    required
                    value={correctedValue}
                    onChange={(e) => setCorrectedValue(e.target.value)}
                    className={styles.textInput}
                    placeholder="Enter accurate verified fact value"
                  />
                </div>
              )}

              <div className={styles.modalFieldGroup}>
                <label>Officer Audit Note / Reason</label>
                <textarea
                  value={officerReason}
                  onChange={(e) => setOfficerReason(e.target.value)}
                  className={styles.textareaInput}
                  placeholder="Provide audit rationale for verification decision"
                  rows={2}
                />
              </div>

              <div className={styles.modalActions}>
                <Button
                  variant="secondary"
                  type="button"
                  onClick={() => setSelectedField(null)}
                >
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  type="submit"
                  disabled={isActionInProgress}
                >
                  {isActionInProgress ? "Saving..." : "Save review"}
                </Button>
              </div>
            </form> : <div><p>This fact belongs to the published snapshot. Evidence viewing does not revise its review.</p><Button onClick={()=>setSelectedField(null)}>Close reviewed source</Button></div>}
          </div>
        </div>
      )}

      {/* Administrative Correction Request Modal */}
      {showCorrectionModal && (
        <div className={styles.modalBackdrop}>
          <div className={styles.modalCard}>
            <h3>Create Administrative Correction Request</h3>
            <p className={styles.modalSub}>
              Administrative correction notice shown in the applicant tracking workspace.
            </p>

            <form onSubmit={handleCreateCorrection} className={styles.verifyForm}>
              <div className={styles.modalFieldGroup}>
                <label>Required Action</label>
                <select
                  value={correctionAction}
                  onChange={(e) => setCorrectionAction(e.target.value)}
                  className={styles.selectInput}
                >
                  <option value="UploadSupportingDocument">Upload Supporting Document</option>
                  <option value="ReviseProposal">Revise Proposal Content / Section</option>
                </select>
              </div>

              {correctionAction === "UploadSupportingDocument" ? (
                <div className={styles.modalFieldGroup}>
                  <label>Document Classification Code</label>
                  <select
                    value={correctionCode}
                    onChange={(e) => setCorrectionCode(e.target.value)}
                    className={styles.selectInput}
                  >
                    <option value="SurveyPlan">SurveyPlan — Licensed Cadastral Survey Plan</option>
                    <option value="IdentityProof">IdentityProof — NIC, Passport, or Business Registration</option>
                    <option value="TitleDeed">TitleDeed — Registered Deed / Ownership Extract</option>
                    <option value="BusinessPlan">BusinessPlan — Agricultural / Business Feasibility</option>
                    <option value="EnvironmentalClearance">EnvironmentalClearance — Environmental Assessment</option>
                    <option value="ValuationReport">ValuationReport — Government Valuation</option>
                  </select>
                </div>
              ) : (
                <div className={styles.modalFieldGroup}>
                  <label>Proposal Section ID</label>
                  <input
                    type="text"
                    required
                    value={correctionSection}
                    onChange={(e) => setCorrectionSection(e.target.value)}
                    className={styles.textInput}
                    placeholder="e.g. land_location, expected_employment"
                  />
                </div>
              )}

              <div className={styles.modalFieldGroup}>
                <label>Applicant-Facing Instructions *</label>
                <textarea
                  required
                  value={correctionInstructions}
                  onChange={(e) => setCorrectionInstructions(e.target.value)}
                  className={styles.textareaInput}
                  placeholder="Provide clear, authoritative instructions to the applicant explaining what document or revision is required."
                  rows={3}
                />
              </div>

              <div className={styles.modalFieldGroup}>
                <label>Explicit due date</label>
                <input
                  type="date" required
                  value={correctionDueDate}
                  onChange={(e) => setCorrectionDueDate(e.target.value)}
                  className={styles.textInput}
                />
              </div>

              <div className={styles.modalActions}>
                <Button
                  variant="secondary"
                  type="button"
                  onClick={() => setShowCorrectionModal(false)}
                >
                  Cancel
                </Button>
                <Button
                  variant="primary"
                  type="submit"
                  disabled={isActionInProgress}
                >
                  {isActionInProgress ? "Creating..." : "Dispatch Correction Notice"}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
