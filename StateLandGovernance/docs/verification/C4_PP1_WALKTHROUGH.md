# C4 PP1 walkthrough — 10–15 minutes

This is a local Development demonstration. Input simulations do not establish real evidence clearance or a completed application. Ordinary uploaded OCR, reviews and completeness are preserved. No browser token editing is required.

## Startup — one command per step

Run from the workspace root. Keep existing database data. If an ordinary service is already running, reuse it rather than starting a second copy.

1. First-time isolated ML environment (the explicit interpreter avoids this machine's broken `py -3.11` registration):

   ```powershell
   & "$env:LOCALAPPDATA/Programs/Python/Python311/python.exe" -m venv .tmp/c4-ml-venv
   ```

2. Install the existing requirements, without modifying them:

   ```powershell
   & .tmp/c4-ml-venv/Scripts/python.exe -m pip install -r StateLandGovernance/src/Modules/GovernanceIntelligence/ML/requirements.txt
   ```

3. In a dedicated terminal, verify the frozen bundle and start the existing service:

   ```powershell
   powershell -File StateLandGovernance/tools/development/Start-C4Ml.ps1
   ```

4. In another terminal, inspect actual health:

   ```powershell
   Invoke-RestMethod http://127.0.0.1:8104/health
   ```

5. Start the ordinary API with its existing explicit local opt-in:

   ```powershell
   powershell -File StateLandGovernance/tools/development/Start-LocalTestApi.ps1 -EnableLocalTestSignIn
   ```

6. Start Next.js from the root in another terminal:

   ```powershell
   npm.cmd --prefix StateLandGovernance.Web run dev -- --webpack --hostname 127.0.0.1 --port 3000
   ```

7. Optional terminal evaluation reproduction, without training:

   ```powershell
   & .tmp/c4-ml-venv/Scripts/python.exe output/evaluation/c4-pp1-20261020/reproduce.py
   ```

The service was successfully restored in this environment. Existing artifact checksums remain unchanged. `runtime-packages.txt` in the evaluation directory captures this tested installation; the repository's lower-bounded requirements can resolve differently in future installations. Do not run training/promotion scripts to repair an inference failure.

## Exact clicks and talking points

| Time | Clicks | What to explain / inspect |
| --- | --- | --- |
| 0–1 min | Open `http://127.0.0.1:3000/sign-in`; sign in **ds / Local-Test-2026!**. Open Governance Intelligence. Select **SL-LMS-2026-745967**. | Server-owned local identity and durable assignment; not production authentication. Selection persists between C4 screens and refresh, but grants no access. |
| 1–4 min | **Continue screening → Development input controls**. Select the no-conflict context, explicitly consent to synthetic governance/complaint coverage, then **Check prerequisites → Run consolidated screening**. Open **Regulatory → Conflict → Early Governance → Summary**; click a check on the left. | One real consolidated request. Section status is distinct from overall status. This research catalogue evaluates **one** regulatory rule, not all law. Exact sources/versions are expandable. A reused persisted response is labelled as reused. |
| 4–5 min | Select overlap, consent again, check prerequisites, run. Open Conflict, select the blocking check. Afterwards restore no-conflict using the same explicit steps if desired. | Actual C4 Blocked/Cleared assessments; scenario selection does not promise an outcome. No routing, approval or real evidence clearance occurs. |
| 5–6 min | **Assessment history → View assessment** on the other result. Inspect date, recorded evidence/scenario version and current/historical marker. Close with Escape. Refresh. | Historical detail is a separate modal and does not overwrite current screening state. Prior versions remain persisted. |
| 6–8 min | **Development inputs**: select Complete, consent, **Run actual early governance engine**. Repeat with reviewed concerns and incomplete evidence. Click returned indicator rows. | Actual Clear / ReviewRequired / InsufficientInformation results; unresolved evidence remains unresolved. These individual evaluations persist separately from consolidated screening. Load their history on Assessment history. |
| 8–9 min | **Complaint intelligence**. Enter fictional English complaint text, consent, **Run complaint classifier**. Then load classifier history and refresh/reload. | Actual v2 category and returned class probabilities. Example: fictional unpaid annual rentals. Closed-set classification always chooses a known category; it neither verifies an allegation nor registers an operational complaint. |
| 9–11 min | **Post-workflow analysis**: select ordinary completed trace, consent, **Run actual post-workflow model**. Repeat with delays/decision revisions. Review timeline, score, threshold and nine returned features. | These are C3-shaped input traces, not fictitious completed C3 executions. Distinct DS Final Notification is preserved. Both tested traces were flagged; do not change them to force a normal result. The scores are not probabilities or causal explanations. Raw predictions are transient. The uploaded case has no eligible persisted completed-workflow projection. |
| 11–12 min | **Evaluation evidence**. Inspect classifier confusion matrix, per-class metrics, anomaly metrics and Sources & versions. Optionally run step 7 above. | Newly reproduced frozen-model scoring, distinct from saved historical evaluation. Synthetic populations and limitations are visible. No tuning or training occurred. |
| 12–13 min | **Governance follow-up**; inspect missing prerequisites. **Integrity and audit → Load case audit history → Read actual receipt**. Explicitly request verification if needed. | Actual readiness and registered AUDIT-V2 receipt. Ledger is unavailable/NotAnchored, with no transaction or block. A digest is not anchoring. No anchor request is offered without its missing operational contract. |
| 13–15 min | Sign out. Sign in as **external** or **applicant** and open the same case screening URL. | Actual officer-screening denial; institutional accounts retain only their restricted requests. Sign-out and session expiry use the existing server session mechanism. |

Case URL: `/component-04/screening/cases/c7ce679c-3559-4eac-b531-4f885a197bf1`. It is the explicitly designated synthetic uploaded Proposal, not a real-world application. Do not designate a real application just to enable testing. The same screen requires current reviewed facts, exact snapshot, both completeness gates and the trusted assignment. No fact verification, snapshot publication or completeness confirmation is performed by this walkthrough.

Successful operational complaint review/resolution, persisted scoring without a trusted projection, C3 manifest registration and blockchain anchoring remain missing contracts/runtime dependencies. See [unapplied owner proposals](C4_PP1_UNAPPLIED_BACKEND_PROPOSALS.md).
