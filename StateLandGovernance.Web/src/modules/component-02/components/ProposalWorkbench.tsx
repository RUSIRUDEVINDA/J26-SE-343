"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { apiGet, apiPost, apiDownload } from "@/lib/apiClient";
import s from "./FinancialFlow.module.css";

type Reference = { id: string; document: string; role: string; pdfPage: number; pdfPages: number[]; summary: string };
export type ProposalDraft = { proposalId: string; generationMode: string; templateVersion: string; text: string; warnings: string[];
  sections: { number: number; title: string; content: string; missingFields: string[]; references: Reference[]; suggestedNarrative: string | null }[] };
export type ProposalInput = { applicationId: string; projectName: string; facts: Record<string, string>; budget: Record<string, string>; useAi: boolean; language: string };
export const initialProposalInput: ProposalInput = { applicationId: "", projectName: "", facts: {}, budget: {}, useAi: false, language: "en" };
type Requirements = { aiConfigured: boolean; templateVersion: string; sections: { number: number; title: string; sinhalaTitle: string; fields: { key: string; label: string }[] }[] };

export function ProposalWorkbench({ reviewedFinancials, purpose, setPurpose, input, setInput, proposal, onGenerated, onInvalidate }: {
  reviewedFinancials: Record<string, unknown> | null; purpose: string; setPurpose: (value: string) => void; input: ProposalInput; setInput: (value: ProposalInput) => void;
  proposal: ProposalDraft | null; onGenerated: (value: ProposalDraft) => void; onInvalidate: () => void;
}) {
  const [requirements, setRequirements] = useState<Requirements | null>(null);
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    let active = true;
    apiGet<Requirements>("/api/lease-feasibility/proposals/requirements").then(value => { if (active) setRequirements(value); })
      .catch(error => { if (active) setError(error instanceof Error ? error.message : "Could not load proposal requirements."); });
    return () => { active = false; };
  }, []);
  const update = (next: ProposalInput) => { setInput(next); onInvalidate(); };
  const generate = async () => {
    setBusy(true); setError("");
    try {
      const budget = Object.fromEntries(Object.entries(input.budget).map(([key, value]) => [key, value.trim() ? Number(value) : null]));
      const result = await apiPost<ProposalDraft>("/api/lease-feasibility/proposals/generate", { ...input, budget, purpose, reviewedFinancials });
      onGenerated(result);
    } catch (error) { setError(error instanceof Error ? error.message : "Proposal generation failed."); }
    finally { setBusy(false); }
  };
  return <>
    <section className={s.card}><h2>Project proposal</h2><p>The Word and printable PDF outputs follow the Department form, with tables, writing lines, diagram spaces and the closing signature block.</p><button className={s.button} type="button" onClick={async () => { try { await apiDownload("/api/lease-feasibility/proposals/export/docx", { text: "", blankForm: true }, "land-department-blank-form.docx"); } catch (error) { setError(error instanceof Error ? error.message : "Blank form export failed"); } }}>Download blank Department form</button><p>Complete the Department&apos;s 14 sections. Unknown information remains flagged. Budgets are project estimates; reference examples do not establish approval outcomes.</p>
      <p className={s.muted}>{reviewedFinancials ? "Your user-confirmed financial assessment will be recalculated and included separately from project sales." : "No confirmed financial assessment is included. Complete financial review and assessment to add it."}</p>
      <fieldset disabled={busy} style={{ border: 0, padding: 0, minWidth: 0 }}>
        <div className={s.columns}>
          <label className={s.field}>Application reference<input maxLength={120} value={input.applicationId} onChange={e => update({ ...input, applicationId: e.target.value })} /></label>
          <label className={s.field}>Project name<input maxLength={200} value={input.projectName} onChange={e => update({ ...input, projectName: e.target.value })} /></label>
        </div>
        <label className={s.field}>Proposed lease purpose<textarea maxLength={2000} rows={3} value={purpose} onChange={e => { setPurpose(e.target.value); onInvalidate(); }} /></label>
        <label className={s.field}>Draft language<select value={input.language} onChange={e => update({ ...input, language: e.target.value })}><option value="en">English</option><option value="si">Sinhala headings / Sinhala AI narrative</option></select></label>
        {requirements?.sections.map(section => <details key={section.number} style={{ margin: "16px 0" }}><summary><strong>{section.number}. {input.language === "si" ? section.sinhalaTitle : section.title}</strong> · {section.fields.filter(f => input.facts[f.key]?.trim()).length}/{section.fields.length} fields entered</summary>
          {section.fields.map(field => <label className={s.field} key={field.key} style={{ marginTop: 12 }}>{field.label}<textarea rows={2} maxLength={4000} value={input.facts[field.key] || ""} onChange={e => update({ ...input, facts: { ...input.facts, [field.key]: e.target.value } })} placeholder={["employment", "materials", "materialCosts", "salesPlan", "workingCapital", "investmentPlan", "schedule"].includes(field.key) ? "For table rows, separate columns with | and put each row on a new line. Free text is preserved as notes. Leave unknown cells empty." : "Leave unknown information blank"} /></label>)}
        </details>)}
        <h3>Project financial estimates (LKR)</h3><p>Annual projections use 12 identical operating months. Explain seasonal harvests, production cycles and startup delays in sections 6 and 11.</p>
        {[["initialInvestment", "Total initial investment"], ["ownFunding", "Planned own funding"], ["monthlySales", "Monthly sales revenue"], ["monthlyProductionCosts", "Monthly production costs"], ["monthlyOperatingCosts", "Monthly operating costs (excluding production costs)"]].map(([key, label]) =>
          <label key={key} className={s.field}>{label}<input type="number" min="0" max="1000000000000" step="0.01" value={input.budget[key] || ""} placeholder="Unknown" onChange={e => update({ ...input, budget: { ...input.budget, [key]: e.target.value } })} /></label>)}
        <label className={s.checkbox}><input type="checkbox" checked={input.useAi} disabled={!requirements?.aiConfigured} onChange={e => update({ ...input, useAi: e.target.checked })} /> Use AI narrative suggestions. Project statements are sent to the configured model; review all returned suggestions.</label>
        <p className={s.muted}>{requirements?.aiConfigured ? "AI drafting is configured." : "AI drafting is not configured. Source-linked structured drafting is available."} Personal identity fields and original financial documents are excluded from the AI request. Avoid entering personal details in project narrative fields.</p>
        <button type="button" className={s.button} disabled={!requirements || busy || !input.applicationId.trim() || !input.projectName.trim() || !purpose.trim()} onClick={generate}>{busy ? "Preparing proposal…" : input.useAi ? "Generate with retrieved references" : "Generate structured draft"}</button>
      </fieldset>
      {error && <p role="alert" className={s.error}>{error}</p>}
      {proposal && <div className={s.success} role="status"><span>14-section draft prepared · {proposal.sections.reduce((n, section) => n + section.missingFields.length, 0)} missing items.</span><Link className={s.button} href="/component-02/proposal-editor">Review draft</Link></div>}
    </section>
    {proposal && <ProposalEvidence proposal={proposal} />}
  </>;
}

export function ProposalEvidence({ proposal }: { proposal: ProposalDraft }) {
  return <section className={s.card}><h2>Requirements and retrieved sources</h2><p>{proposal.generationMode === "retrieval-augmented-ai" ? "Retrieved references supported AI narrative suggestions. Every suggestion needs review." : "Structured draft using reviewed references. No language model was run."}</p>
    {proposal.sections.map(section => <details key={section.number} style={{ marginBottom: 12 }}><summary>{section.number}. {section.title} · {section.missingFields.length} missing</summary>
      {section.missingFields.length > 0 && <p className={s.error}>To complete: {section.missingFields.join("; ")}</p>}
      {section.references.map(reference => <p key={reference.id}><strong>[{reference.id}] {reference.document} · PDF pages {(reference.pdfPages || [reference.pdfPage]).join(", ")}</strong><br />{reference.role === "requirement" ? "Department requirement" : "Example; approval outcome unverified"}<br />{reference.summary}</p>)}
    </details>)}
    {proposal.warnings.map(warning => <p key={warning} className={s.muted}>{warning}</p>)}
  </section>;
}
