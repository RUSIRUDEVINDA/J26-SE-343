"use client";

import Link from "next/link";
import { apiUpload, apiPost, apiGet, apiDownload } from "@/lib/apiClient";
import { usePathname } from "next/navigation";
import { createContext, useContext, useState, useEffect, type ReactNode } from "react";
import { ArrowLeft, ArrowRight, Check, CheckCircle2, ChevronRight, CircleHelp, Download, FileText, FolderOpen, Info, ShieldCheck, Sparkles, AlertTriangle, X } from "lucide-react";
import { AppHeader } from "@/shared/components/AppHeader";
import { navigation, pathFor, screenCopy, type Screen } from "../types/flow";
import s from "./FinancialFlow.module.css";
import { ProposalWorkbench, ProposalEvidence, initialProposalInput, type ProposalInput, type ProposalDraft } from "./ProposalWorkbench";
import { LeaseCapacityPanel } from "./LeaseCapacityPanel";

const documentKinds = ["Bank statements", "Salary / income evidence", "CRIB report"] as const;
const initialFields = { income: "", balance: "", debt: "", lease: "", consistency: "", grade: "Not supplied", overdrafts: "", defaultHistory: "" };
type UploadLimits = { maximumPdfPages: number; maximumPdfBytes: number; maximumImageBytes: number };
type UploadValidation = { fileName: string; format: string; pages: number; batches: number; bytes: number };
type ExtractionBatch = { startPage: number; endPage: number; complete: boolean; attempts: number; error: string | null };
type Extraction = { fieldSources: Record<string, string>; totalPages: number; isComplete: boolean; pageNumbers: number[]; tablePageNumbers: number[][]; batches: ExtractionBatch[]; kind: string; fileName: string; text: string; pagesRead: number; tables: string[][][]; fields: Partial<Fields>; reviewReasons: string[] };
type Fields = typeof initialFields;
type Assessment = { grade: string; action: string; contractVersion: string; scoreBreakdown: Record<string, number> };
type FlowState = {
  proposal: ProposalDraft | null; setProposal: (value: ProposalDraft | null) => void;
  proposalInput: ProposalInput; setProposalInput: (value: ProposalInput) => void;
  fields: Fields; setFields: (fields: Fields) => void;
  fieldSources: Record<string, string>; setFieldSources: (sources: Record<string, string>) => void;
  assessment: Assessment | null; setAssessment: (value: Assessment | null) => void;
  limits: UploadLimits | null; limitsError: string; validation: Record<string, UploadValidation>; setValidation: (value: Record<string, UploadValidation>) => void;
  files: Record<string, File>; setFiles: (files: Record<string, File>) => void;
  results: Extraction[]; completeEvidence: boolean; processing: boolean; processingErrors: string[]; process: () => Promise<void>;
  confirmed: boolean; setConfirmed: (value: boolean) => void;
  purpose: string; setPurpose: (value: string) => void;
  draft: string; setDraft: (value: string) => void;
  saved: string; setSaved: (value: string) => void;
};
const Context = createContext<FlowState | null>(null);
const money = (value: string | number) => value === "" ? "Needs review" : `LKR ${Number(value).toLocaleString("en-LK", { maximumFractionDigits: 0 })}`;
const cases = [
  { id: "DEMO-021", purpose: "Small-scale cultivation", outcome: "Approved", grade: "B", description: "Clear purpose, an itemized operating budget and documented recurring income.", similarity: "Strong match" },
  { id: "DEMO-034", purpose: "Agricultural development", outcome: "Further review", grade: "C", description: "Income evidence needed clarification and the repayment plan was incomplete.", similarity: "Related case" },
  { id: "DEMO-048", purpose: "Community nursery", outcome: "Approved", grade: "B", description: "A phased implementation plan connected the proposed use to available resources.", similarity: "Related case" },
];
function useFlow() { const state = useContext(Context); if (!state) throw new Error("Financial flow provider is missing"); return state; }

export function FinancialFlowLayout({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const [fields, setFields] = useState<Fields>(initialFields);
  const [fieldSources, setFieldSources] = useState<Record<string, string>>({});
  const [assessment, setAssessment] = useState<Assessment | null>(null);
  const [limits, setLimits] = useState<UploadLimits | null>(null);
  const [limitsError, setLimitsError] = useState("");
  const [validation, setValidation] = useState<Record<string, UploadValidation>>({});
  useEffect(() => {
    let active = true;
    apiGet<UploadLimits>("/api/lease-feasibility/documents/limits").then(value => { if (active) setLimits(value); }).catch(() => { if (active) setLimitsError("Upload validation is unavailable. Start the API and reload this page before choosing documents."); });
    return () => { active = false; };
  }, []);
  const [files, setFiles] = useState<Record<string, File>>({});
  const [results, setResults] = useState<Extraction[]>([]);
  const [processing, setProcessing] = useState(false);
  const [processingErrors, setProcessingErrors] = useState<string[]>([]);
  const changeFiles = (next: Record<string, File>) => {
    setProposal(null); setDraft(""); setSaved(""); setAssessment(null); setFiles(next); setFields(initialFields); setFieldSources({}); setResults([]); setProcessingErrors([]); setConfirmed(false);
  };
  const process = async () => {
    if (processing) return;
    setProposal(null); setDraft(""); setSaved(""); setAssessment(null); setProcessing(true); setConfirmed(false); setFields(initialFields); setProcessingErrors([]);
    const previous = results.filter(result => result.isComplete);
    const extracted: Extraction[] = [];
    setResults(previous);
    const errors: string[] = [];
    try {
      for (const [label, file] of Object.entries(files)) {
        const kind = label === "Bank statements" ? "bank" : label === "CRIB report" ? "crib" : "salary";
        const existing = previous.find(result => result.kind === kind);
        if (existing) {
          try { extracted.push(await apiPost<Extraction>("/api/lease-feasibility/documents/map", existing)); }
          catch (error) { extracted.push(existing); errors.push(`${file.name}: ${error instanceof Error ? error.message : "Field mapping failed"}`); }
          continue;
        }
        const body = new FormData(); body.append("file", file);
        body.append("kind", kind);
        try { extracted.push(await apiUpload<Extraction>("/api/lease-feasibility/documents/extract", body)); }
        catch (error) { errors.push(`${file.name}: ${error instanceof Error ? error.message : "Extraction failed"}`); }
      }
      if (!Object.keys(files).length) errors.push("Select documents before processing.");
      // Keep the statement's income when present. Salary evidence fills an unknown income only.
      const next = { ...initialFields };
      const sources: Record<string, string> = {};
      for (const result of [...extracted].filter(item => item.isComplete).sort((a, b) => Number(b.kind === "bank") - Number(a.kind === "bank"))) for (const [key, value] of Object.entries(result.fields)) {
        if (key in next && value !== undefined && (key !== "income" || !next.income)) { next[key as keyof Fields] = value; sources[key] = result.fieldSources?.[key] || "Extracted document summary; verify the original."; }
      }
      setFields(next); setFieldSources(sources); setResults(extracted); setProcessingErrors(errors);
    } finally { setProcessing(false); }
  };
  const [confirmed, setConfirmed] = useState(false);
  const [purpose, setPurpose] = useState("Small-scale cultivation");
  const [draft, setDraft] = useState("");
  const [saved, setSaved] = useState("");
  const [proposal, setProposal] = useState<ProposalDraft | null>(null);
  const [proposalInput, setProposalInput] = useState<ProposalInput>(initialProposalInput);
  const completeEvidence = results.length > 0 && results.length === Object.keys(files).length && results.every(result => result.isComplete) && processingErrors.length === 0 && !processing;
  const current = pathname.split("/")[2] || "overview";
  const active = current === "ocr" || current === "document-issues" ? "documents" : current === "escalation" ? "assessment" : current === "proposal-editor" ? "proposal-generator" : current;
  return <Context.Provider value={{ proposal, setProposal, proposalInput, setProposalInput, fields, setFields, fieldSources, setFieldSources, limits, limitsError, validation, setValidation, assessment, setAssessment, files, setFiles: changeFiles, results, completeEvidence, processing, processingErrors, process, confirmed, setConfirmed, purpose, setPurpose, draft, setDraft, saved, setSaved }}>
    <div className={s.shell}>
      <AppHeader />
      <div className={s.workspace}>
        <aside className={s.sidebar}>
          <Link href="/" className={s.back}><ArrowLeft size={14} /> All modules</Link>
          <p className={s.navTitle}>FINANCIAL INTELLIGENCE</p>
          <nav aria-label="Component 2 navigation">{navigation.map(item => <Link key={item.screen} href={pathFor(item.screen)} className={`${s.navLink} ${active === item.screen ? s.active : ""}`} aria-current={active === item.screen ? "page" : undefined}><span className={s.dot} />{item.label}</Link>)}</nav>
          <div className={s.sidebarNote}><ShieldCheck size={20} /><strong>Your evidence, your review</strong><p>Confirm financial information before using an assessment or proposal.</p></div>
        </aside>
        <main className={s.main}>
          <div className={s.preview}><Info size={16} /><span><strong>Document review</strong> · Process selected files to read their contents. Verify extracted fields. Proposal drafts use Department requirements and reviewed references. Prediction remains a preview.</span></div>
          {children}
        </main>
      </div>
    </div>
  </Context.Provider>;
}
function Go({ to, children, secondary = false }: { to: Screen; children: ReactNode; secondary?: boolean }) { return <Link className={`${s.button} ${secondary ? s.secondary : ""}`} href={pathFor(to)}>{children}<ArrowRight size={15} /></Link>; }
function Card({ title, children, className = "" }: { title?: string; children: ReactNode; className?: string }) { return <section className={`${s.card} ${className}`}>{title && <h2>{title}</h2>}{children}</section>; }
function Tag({ children, tone = "neutral" }: { children: ReactNode; tone?: "neutral" | "good" | "warning" }) { return <span className={`${s.tag} ${tone === "good" ? s.good : tone === "warning" ? s.warning : ""}`}>{children}</span>; }
function Metric({ label, value, hint }: { label: string; value: string; hint?: string }) { return <div className={s.metric}><p>{label}</p><strong>{value}</strong>{hint && <small>{hint}</small>}</div>; }
function Notice({ children, danger = false }: { children: ReactNode; danger?: boolean }) { return <div className={`${s.notice} ${danger ? s.danger : ""}`}><AlertTriangle size={19} /><div>{children}</div></div>; }


export function FinancialScreen({ screen }: { screen: Screen }) {
  const flow = useFlow();
  const [notice, setNotice] = useState("");
  const [search, setSearch] = useState("");
  const [outcome, setOutcome] = useState("All outcomes");
  const [selectedCase, setSelectedCase] = useState<string | null>(null);
  const [reviewed, setReviewed] = useState(false);
  const [proposalPreview, setProposalPreview] = useState("");
  const [validating, setValidating] = useState(false);
  const chooseFile = async (kind: string, file: File) => {
    const limit = /\.pdf$/i.test(file.name) ? flow.limits?.maximumPdfBytes : flow.limits?.maximumImageBytes;
    if (!limit) { setError("Upload limits are unavailable. Start the API before choosing documents."); return; }
    if (file.size === 0 || file.size > limit || !/\.(pdf|png|jpe?g)$/i.test(file.name)) { setError(`${file.name}: choose a non-empty PDF up to ${flow.limits!.maximumPdfBytes / 1024 / 1024} MB or PNG/JPEG up to ${flow.limits!.maximumImageBytes / 1024 / 1024} MB.`); return; }
    setValidating(true); setError("");
    try {
      const body = new FormData(); body.append("file", file);
      const checked = await apiUpload<UploadValidation>("/api/lease-feasibility/documents/validate", body);
      flow.setFiles({ ...flow.files, [kind]: file });
      flow.setValidation({ ...flow.validation, [kind]: checked });
    } catch (error) { setError(`${file.name}: ${error instanceof Error ? error.message : "Document validation failed"}`); }
    finally { setValidating(false); }
  };
  const [assessing, setAssessing] = useState(false);
  const assess = async () => {
    setAssessing(true); setError(""); flow.setAssessment(null);
    try {
      const numeric = (value: string) => value.trim() === "" ? null : Number(value);
      flow.setAssessment(await apiPost<Assessment>("/api/lease-feasibility/assessment", {
        income: numeric(flow.fields.income), balance: numeric(flow.fields.balance), debt: numeric(flow.fields.debt),
        lease: numeric(flow.fields.lease), consistency: numeric(flow.fields.consistency), overdrafts: numeric(flow.fields.overdrafts),
        grade: flow.fields.grade, defaultHistory: flow.fields.defaultHistory === "" ? null : flow.fields.defaultHistory === "true", confirmed: flow.confirmed,
      }));
    } catch (error) { setError(error instanceof Error ? error.message : "Assessment failed"); }
    finally { setAssessing(false); }
  };
  const [error, setError] = useState("");
  const [title, subtitle] = screenCopy[screen];
  const setField = (key: keyof Fields, value: string) => { flow.setProposal(null); flow.setDraft(""); flow.setSaved(""); flow.setFields({ ...flow.fields, [key]: value }); flow.setConfirmed(false); flow.setAssessment(null); flow.setFieldSources({ ...flow.fieldSources, [key]: "Entered manually; verify against your supporting evidence." }); };
  const exportDraft = () => {
    const content = flow.saved || flow.draft;
    if (!content.trim()) { setNotice("Create a draft in Proposal Generator first."); return; }
    const url = URL.createObjectURL(new Blob([content], { type: "text/plain;charset=utf-8" }));
    const anchor = document.createElement("a"); anchor.href = url; anchor.download = "lease-proposal-draft.txt"; anchor.click(); URL.revokeObjectURL(url);
    setNotice("Draft exported for independent review.");
  };
  return <>
    <header className={s.heading}><div><p className={s.eyebrow}>COMPONENT 02 / {screen === "overview" ? "WORKSPACE" : "FINANCIAL INTELLIGENCE"}</p><h1>{title}</h1><p>{subtitle}</p></div><Tag>Session review</Tag></header>
    {notice && <div className={s.toast} role="status">{notice}<button aria-label="Dismiss message" onClick={() => setNotice("")}><X size={16} /></button></div>}
    {screen === "overview" && <>
      <div className={s.metrics}><Metric label="Assessment status" value={flow.confirmed ? "Fields verified" : "Awaiting verification"} hint="Uploaded document evidence" /><Metric label="Financial eligibility" value={flow.assessment ? `${flow.assessment.grade} · ${flow.assessment.scoreBreakdown.totalScore} / 100` : "Not assessed"} hint="Calculated after verification" /><Metric label="Supporting evidence" value={`${flow.results.filter(result => result.isComplete).length} document(s) complete`} hint="Review extraction completeness" /><Metric label="Proposal status" value={flow.saved ? "Draft saved" : "Not started"} hint="Independent review required" /></div>
      <div className={s.columns}><Card title="Your financial intelligence journey"><div className={s.steps}>{[["01", "Financial documents", "Upload and organize supporting evidence", "documents"], ["02", "Verify financial profile", "Review extraction and confirm important fields", "extracted-data"], ["03", "Financial assessment", "Understand eligibility and contributing factors", "assessment"], ["04", "Build your proposal", "Prepare, review and improve your application", "proposal-generator"]].map(([number, label, hint, to]) => <Link className={s.step} key={number} href={pathFor(to as Screen)}><span className={s.stepNumber}>{number}</span><div><strong>{label}</strong><small>{hint}</small></div><ChevronRight size={18} /></Link>)}</div></Card><Card title="Application at a glance"><dl className={s.details}><dt>Application reference</dt><dd>Session only</dd><dt>Proposed use</dt><dd>{flow.purpose}</dd><dt>Applicant type</dt><dd>Individual applicant</dd><dt>Evidence window</dt><dd>Verify statement dates</dd></dl><div className={s.divider} /><p className={s.muted}>Start with your documents, then review the financial profile before preparing a proposal.</p><Go to="documents">Review financial documents</Go></Card></div>
      <Card title="Decision support with a clear evidence trail"><div className={s.threeColumns}>{[[ShieldCheck, "Verified inputs", "Financial evidence stays separate from assumptions."], [CircleHelp, "Explainable results", "See the factors behind each assessment."], [Sparkles, "Reviewable proposals", "Every draft remains subject to your review."]].map(([Icon, label, text], index) => { const Component = Icon as typeof ShieldCheck; return <div className={s.feature} key={index}><Component size={22} /><strong>{label as string}</strong><p>{text as string}</p></div>; })}</div></Card>
    </>}
    {screen === "documents" && <>
      <div className={s.columns}><Card title="Upload supporting documents"><p className={s.muted}>Select a PDF, PNG or JPEG. Selected files are sent to the server for format and page checks; OCR starts only when you choose to process them. Files and results are kept for this browser session only.</p><p className={s.muted}>{flow.limits ? `PDF: up to ${flow.limits.maximumPdfPages} pages and ${flow.limits.maximumPdfBytes / 1024 / 1024} MB per file. PNG/JPEG: up to ${flow.limits.maximumImageBytes / 1024 / 1024} MB per image. Longer PDFs take longer to process.` : "Checking upload limits…"}</p>{flow.limitsError && <p role="alert" className={s.error}>{flow.limitsError}</p>}{validating && <p role="status">Checking document format, size and page count…</p>}<div className={s.uploadList}>{documentKinds.map((kind, index) => <div className={s.uploadRow} key={kind}><div className={s.fileIcon}><FileText size={23} /></div><div className={s.fileInfo}><strong>{kind}</strong><small>{flow.files[kind]?.name || (index === 2 ? "Optional supporting credit evidence" : index === 0 ? "Statements covering the evidence period" : "Salary slips or business income evidence")}</small>{flow.files[kind] && flow.validation[kind] && <small>{flow.validation[kind].pages} page(s) · {(flow.validation[kind].bytes / 1024 / 1024).toFixed(2)} MB · Validated</small>}</div><label className={s.fileButton}>{flow.files[kind] ? "Replace file" : "Choose file"}<input aria-label={kind} disabled={flow.processing || validating || !flow.limits} type="file" accept=".pdf,.png,.jpg,.jpeg" onChange={event => { const file = event.target.files?.[0]; event.target.value = ""; if (file) void chooseFile(kind, file); }} /></label>{flow.files[kind] && <button className={s.iconButton} disabled={flow.processing || validating} aria-label={`Remove ${kind}`} onClick={() => flow.setFiles(Object.fromEntries(Object.entries(flow.files).filter(([key]) => key !== kind)))}><X size={15} /></button>}</div>)}</div>{error && <p role="alert" className={s.error}>{error}</p>}<div className={s.actions}>{Object.keys(flow.files).length > 0 && !validating ? <Go to="ocr">Process selected documents</Go> : <button className={s.button} disabled>Select valid documents to continue</button>}<Go to="document-issues" secondary>Review document issues</Go></div></Card><Card title="Before you continue"><ul className={s.checkList}><li><Check size={16} /> Complete and readable pages</li><li><Check size={16} /> Consistent evidence periods</li><li><Check size={16} /> Appropriate income evidence</li><li><Check size={16} /> Permission to process documents</li></ul><Notice>CRIB is optional. A missing report must not be interpreted as poor credit or zero debt.</Notice></Card></div>
    </>}
    {screen === "ocr" && <Card title="Document extraction"><p className={s.muted}>Upload the selected files to read their text and tables. Missing or uncertain values remain blank for review.</p><button className={s.button} disabled={flow.processing || !Object.keys(flow.files).length} onClick={() => void flow.process()}>{flow.processing ? "Reading documents…" : flow.results.some(result => !result.isComplete) || flow.processingErrors.length ? "Retry incomplete documents" : "Upload and extract documents"}</button><p role="status">{flow.processing ? "OCR is processing all pages in batches of up to two. Keep this session open; longer PDFs take longer." : `${flow.results.filter(result => result.isComplete).length} of ${Object.keys(flow.files).length} document(s) complete.`}</p>{flow.processingErrors.map(message => <p key={message} role="alert" className={s.error}>{message}</p>)}{flow.results.map(result => <div key={result.kind}><h3>{result.fileName}: {result.pagesRead} / {result.totalPages} pages · {result.isComplete ? "Complete" : "Incomplete"}</h3>{result.batches.map(batch => <p key={batch.startPage} className={batch.complete ? s.muted : s.error}>Pages {batch.startPage}–{batch.endPage}: {batch.complete ? "Read" : batch.error} · {batch.attempts} attempt(s)</p>)}</div>)}{flow.results.some(result => !result.isComplete) && <p role="alert" className={s.error}>Some pages could not be read. Retry extraction before confirming or assessing this profile.</p>}{flow.results.length > 0 && <Go to="extracted-data">Review extracted documents</Go>}<Go to="documents" secondary>Review selected files</Go></Card>}
    {(screen === "extracted-data" || screen === "assessment") && <LeaseCapacityPanel income={flow.fields.income} debt={flow.fields.debt} lease={flow.fields.lease} />}
    {screen === "extracted-data" && <><div className={s.metrics}><Metric label="Monthly income" value={money(flow.fields.income)} /><Metric label="Monthly debt" value={money(flow.fields.debt)} /><Metric label="Requested lease payment" value={money(flow.fields.lease)} /></div><div className={s.columns}><Card title="Financial profile — verify against documents"><button className={s.button} disabled={flow.processing || !flow.results.some(result => result.isComplete)} onClick={() => void flow.process()}>{flow.processing ? "Mapping document fields…" : "Fill fields from extracted tables"}</button><p className={s.muted}>Refresh suggested fields from documents already read; completed documents do not need another OCR upload. This replaces edited values, so review the suggestions again.</p><div className={s.fieldGrid}>{Object.entries({ income: "Average monthly income (LKR)", balance: "Average account balance (LKR)", debt: "Verified monthly debt (LKR)", lease: "Requested monthly lease payment (LKR)", consistency: "Income consistency ratio (0–1)", overdrafts: "Verified overdraft count in evidence window" }).map(([key, label]) => <label className={s.field} key={key}>{label}<input type="number" min="0" max={key === "consistency" ? "1" : undefined} step={key === "overdrafts" ? "1" : "0.01"} value={flow.fields[key as keyof Fields]} onChange={e => setField(key as keyof Fields, e.target.value)} /><small>{flow.fieldSources[key] || (key === "lease" ? "Enter the proposed land lease installment; it is not extracted from bank or CRIB reports." : key === "consistency" ? "Requires a reviewed income-consistency calculation; no ratio is guessed." : "No reliable mapped value yet. Review the document evidence below.")}</small></label>)}<label className={s.field}>Original CRIB risk grade<select value={flow.fields.grade} onChange={e => setField("grade", e.target.value)}>{["A1", "A2", "A3", "B1", "B2", "B3", "C1", "C2", "C3", "D1", "D2", "D3", "E1", "E2", "E3", "XX", "Not supplied"].map(grade => <option key={grade}>{grade}</option>)}</select><small>{flow.fieldSources.grade || "Read from the current CRIB score table when identifiable; otherwise verify manually."}</small></label><label className={s.field}>Verified default history<select value={flow.fields.defaultHistory} onChange={e => setField("defaultHistory", e.target.value)}><option value="">Needs review</option><option value="true">Confirmed default history</option><option value="false">Confirmed no default history</option></select></label></div><label className={s.checkbox}><input type="checkbox" disabled={!flow.completeEvidence} checked={flow.confirmed} onChange={e => { flow.setConfirmed(e.target.checked); flow.setAssessment(null); }} /> I have verified these fields against the uploaded documents.</label><p className={s.muted}>Every selected document must finish extraction before confirmation. Missing values need your review; they are never replaced with sample amounts. Enter the requested lease payment separately.</p><Go to="assessment">View assessment</Go></Card><Card title="Items needing confirmation"><Notice>Outstanding debt is not a monthly payment. Confirm active facilities and repayment schedules.</Notice><dl className={s.details}><dt>CRIB grade</dt><dd>Keep the original subgrade</dd><dt>XX / missing evidence</dt><dd>Requires review</dd><dt>Default history</dt><dd>Verify repayment evidence</dd><dt>Disputed records</dt><dd>Review before assessment</dd></dl><Go to="document-issues" secondary>Review evidence issues</Go></Card></div></>}
    {screen === "extracted-data" && <Card title="Extracted document evidence">{flow.processingErrors.map(message => <p key={message} role="alert" className={s.error}>{message}</p>)}{!flow.results.length && <p>No documents have been extracted. Go to Document Processing and upload your selected files.</p>}{flow.results.map(result => <section key={result.kind}><h3>{result.fileName} · {result.pagesRead} / {result.totalPages} pages read · {result.isComplete ? "Complete" : "Incomplete"}</h3>{result.reviewReasons.map(reason => <p key={reason} className={s.muted}>{reason}</p>)}<details><summary>Read extracted text</summary><pre style={{ whiteSpace: "pre-wrap", overflowWrap: "anywhere" }}>{result.text}</pre></details>{result.tables.map((table, index) => <div className={s.tableWrap} key={index}><table><caption>Extracted table {index + 1} · original page(s) {result.tablePageNumbers[index]?.join(", ") || "Unknown"}</caption><tbody>{table.map((row, r) => <tr key={r}>{row.map((cell, c) => <td key={c}>{cell}</td>)}</tr>)}</tbody></table></div>)}</section>)}</Card>}
    {screen === "document-issues" && <Card title="Document review issues">{flow.processingErrors.map(message => <p role="alert" key={message} className={s.error}>{message}</p>)}{flow.results.map(result => <div key={result.kind}><h3>{result.fileName}</h3>{result.reviewReasons.map(reason => <p key={reason}>{reason}</p>)}</div>)}{!flow.results.length && <p>Process your selected documents to identify evidence that needs review.</p>}<Go to="documents">Review documents</Go></Card>}
    {(screen === "assessment" || screen === "escalation") && <Card title="Financial assessment"><p>Calculate an advisory eligibility score from the verified fields using the research scoring contract.</p><button className={s.button} disabled={assessing || !flow.confirmed || flow.processing || !flow.completeEvidence} onClick={() => void assess()}>{assessing ? "Calculating…" : "Calculate verified assessment"}</button>{!flow.completeEvidence && <p role="alert">Complete document extraction before assessment.</p>}{!flow.confirmed && <p>Verify your financial fields and confirm them before assessment.</p>}{error && <p role="alert" className={s.error}>{error}</p>}{flow.assessment && <><h3>Eligibility grade {flow.assessment.grade} · {flow.assessment.scoreBreakdown.totalScore} / 100</h3><p>{flow.assessment.action} · {flow.assessment.contractVersion}</p><dl className={s.details}>{Object.entries(flow.assessment.scoreBreakdown).map(([key, value]) => <div key={key}><dt>{key.replace(/([A-Z])/g, " $1")}</dt><dd>{value}</dd></div>)}</dl><p className={s.muted}>Session assessment for human review. This result has not been saved or submitted.</p></>}<Go to="extracted-data">Review financial profile</Go></Card>}
    {screen === "prediction" && <><div className={s.resultColumns}><section className={s.scoreCard}><p>ESTIMATED APPROVAL · ILLUSTRATION</p><strong className={s.bigPercent}>76%</strong><h2>Sample prediction display</h2><span>No model has been run</span><div className={s.scoreDivider} /><small>Illustrative UI values, not calibrated probabilities</small></section><Card title="Prediction interpretation">{[["Approval likely", 76], ["Further review", 18], ["Rejection risk", 6]].map(([label, percent], index) => <div className={s.probability} key={label}><strong>{label}</strong><div className={s.bar}><span className={index === 1 ? s.amberBar : index === 2 ? s.redBar : ""} style={{ width: `${percent}%` }} /></div><span>{percent}%</span></div>)}<Notice>These values are design examples. Historical outcomes and model validation are required before real predictions.</Notice></Card></div><div className={s.columns}><Card title="Example prediction factors"><dl className={s.details}><dt>Recurring income</dt><dd>Supporting evidence</dd><dt>Debt service capacity</dt><dd>Review monthly obligations</dd><dt>Financial eligibility</dt><dd>Separate scoring output</dd><dt>Missing credit evidence</dt><dd>Uncertainty, not rejection</dd></dl></Card><Card title="Continue with the evidence"><Go to="historical-cases">View similar sample cases</Go><div className={s.divider} /><Go to="proposal-generator" secondary>Continue to proposal</Go></Card></div></>}
    {screen === "historical-cases" && <Card title="Comparable cases — fictional examples"><div className={s.filters}><label className={s.field}>Search sample cases<input placeholder="Search by purpose or reference" value={search} onChange={e => setSearch(e.target.value)} /></label><label className={s.field}>Decision outcome<select value={outcome} onChange={e => setOutcome(e.target.value)}>{["All outcomes", "Approved", "Further review"].map(value => <option key={value}>{value}</option>)}</select></label></div><div className={s.tableWrap}><table><thead><tr><th>Case reference</th><th>Proposed use</th><th>Financial grade</th><th>Outcome</th><th>Similarity</th><th><span className={s.srOnly}>Details</span></th></tr></thead><tbody>{cases.filter(item => `${item.id} ${item.purpose}`.toLowerCase().includes(search.toLowerCase()) && (outcome === "All outcomes" || item.outcome === outcome)).map(item => <tr key={item.id}><td>{item.id}</td><td>{item.purpose}</td><td>{item.grade}</td><td><Tag tone={item.outcome === "Approved" ? "good" : "warning"}>{item.outcome}</Tag></td><td>{item.similarity}</td><td><button className={s.textButton} onClick={() => setSelectedCase(item.id)}>View case</button></td></tr>)}</tbody></table></div>{!cases.some(item => `${item.id} ${item.purpose}`.toLowerCase().includes(search.toLowerCase()) && (outcome === "All outcomes" || item.outcome === outcome)) && <p className={s.empty}>No sample cases match your search.</p>}{selectedCase && <div className={s.caseDetail}><button className={s.iconButton} aria-label="Close case details" onClick={() => setSelectedCase(null)}><X size={16} /></button><h3>{selectedCase} · Sample case insight</h3><p>{cases.find(item => item.id === selectedCase)?.description}</p><small>Fictional example. No retrieval engine was used.</small></div>}<div className={s.actions}><Go to="proposal-generator">Use insights to prepare a draft</Go></div></Card>}
    {screen === "proposal-generator" && <ProposalWorkbench reviewedFinancials={flow.confirmed && flow.assessment ? { income: Number(flow.fields.income), balance: Number(flow.fields.balance), debt: Number(flow.fields.debt), lease: Number(flow.fields.lease), consistency: Number(flow.fields.consistency), overdrafts: Number(flow.fields.overdrafts), grade: flow.fields.grade, defaultHistory: flow.fields.defaultHistory === "true", confirmed: true } : null} purpose={flow.purpose} setPurpose={flow.setPurpose} input={flow.proposalInput} setInput={flow.setProposalInput} proposal={flow.proposal} onGenerated={value => { flow.setProposal(value); flow.setDraft(value.text); flow.setSaved(""); setReviewed(false); }} onInvalidate={() => { flow.setProposal(null); flow.setDraft(""); flow.setSaved(""); setReviewed(false); }} />}
    {screen === "proposal-editor" && <div className={s.columns}><Card title="Your proposal draft">{flow.draft ? <><label className={s.field}>Draft content<textarea className={s.editor} value={flow.draft} onChange={e => { flow.setDraft(e.target.value); flow.setSaved(""); }} rows={17} /></label><div className={s.actions}><button className={s.button} onClick={() => { if (!flow.draft.trim()) { setNotice("Enter draft content before saving."); return; } flow.setSaved(flow.draft); setNotice("Draft saved for this session. Reloading clears it."); }}>Save draft</button><Go to="proposal-ready" secondary>Review readiness</Go></div></> : <div className={s.empty}><FolderOpen size={30} /><p>No proposal draft yet.</p><Go to="proposal-generator">Create a proposal draft</Go></div>}</Card><Card title="Recommendations to consider">{[["Clarify the operating budget", "List startup and recurring costs with evidence."], ["Add a phased timeline", "Connect milestones to available resources."], ["Verify financial statements", "Use confirmed facts and distinguish estimates."]].map(([label, hint]) => <div className={s.recommendation} key={label}><Sparkles size={16} /><div><strong>{label}</strong><p>{hint}</p></div></div>)}<Tag>Static review prompts</Tag><div className={s.divider} /><Go to="explainable-ai" secondary>Understand the evidence</Go></Card></div>}
    {screen === "explainable-ai" && (flow.proposal ? <ProposalEvidence proposal={flow.proposal} /> : <Card title="No generated proposal"><p>Generate a proposal to inspect its requirements, missing fields and retrieved references.</p><Go to="proposal-generator">Generate proposal</Go></Card>)}
    {screen === "proposal-editor" && flow.proposal && <ProposalEvidence proposal={flow.proposal} />}
    {screen === "proposal-ready" && <>{flow.proposal && <Notice>{flow.proposal.sections.reduce((total, section) => total + section.missingFields.length, 0)} items were flagged when generated. Editing text does not automatically revalidate this checklist. Export is a draft for review.</Notice>}<div className={s.readiness}><CheckCircle2 size={28} /><div><h2>{flow.saved ? "Your draft is saved for review" : "Complete your proposal draft"}</h2><p>{flow.saved ? "Review the checklist before exporting. Submission is not connected." : "Create and save a proposal in the editor before exporting."}</p></div><Tag tone={flow.saved ? "good" : "warning"}>{flow.saved ? "Draft available" : "Draft pending"}</Tag></div><div className={s.columns}><Card title="Final review checklist"><ul className={s.checkList}>{["Applicant statements are accurate", "Financial evidence has been independently verified", "Estimates and assumptions are clearly identified", "Required documents and approvals are attached", "All proposal sections have been reviewed"].map(label => <li key={label}><CheckCircle2 size={17} />{label}</li>)}</ul><label className={s.checkbox}><input type="checkbox" checked={reviewed} onChange={e => setReviewed(e.target.checked)} /> I have reviewed the proposal draft checklist.</label></Card><Card title="Draft actions"><dl className={s.details}><dt>Version</dt><dd>Session draft</dd><dt>Saved content</dt><dd>{flow.saved ? "Available" : "Not saved"}</dd><dt>Submission status</dt><dd>Not submitted</dd></dl><div className={s.actions}><button className={s.button} onClick={exportDraft} disabled={!flow.saved || !reviewed}><Download size={16} />Export draft (.txt)</button><button className={s.button} disabled={!flow.saved || !reviewed} onClick={async () => { try { await apiDownload("/api/lease-feasibility/proposals/export/docx", { text: flow.saved }, "land-department-project-proposal.docx"); } catch (error) { setNotice(error instanceof Error ? error.message : "Export failed"); } }}>Export Word (.docx)</button><button className={s.button} disabled={!flow.saved || !reviewed} onClick={async () => { try { const preview = await apiPost<{ html: string }>("/api/lease-feasibility/proposals/export/preview", { text: flow.saved }); setProposalPreview(preview.html); } catch (error) { setNotice(error instanceof Error ? error.message : "Preview failed"); } }}>Preview / Print PDF</button><Go to="proposal-editor" secondary>Edit proposal</Go></div><p className={s.muted}>Drafts are kept in memory only. Export before leaving or reloading this session.</p><button className={s.disabledButton} disabled>Submission available after integration</button></Card></div></>}
    {screen === "proposal-ready" && proposalPreview && <Card title="Department proposal form preview"><p>Use Print / Save PDF inside the preview to print the form pages.</p><iframe title="Department proposal form preview" srcDoc={proposalPreview} sandbox="allow-scripts allow-modals" style={{ width: "100%", height: 1100, border: "1px solid #ddd" }} /></Card>}
    <footer className={s.footer}><ShieldCheck size={14} /> Financial intelligence supports review. Final decisions remain with authorized officers.</footer>
  </>;
}
