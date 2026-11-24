# UI cleanup — remaining browser checks

Reuse healthy services on API5264, portal3000 and ML8104. No restart, SQL, migration, screening run or model inference is needed. Current API startup instructions remain in [the existing approved guide](../history-and-ds-form-fix/MANUAL_WALKTHROUGH.md). Never use older pre-security/pre-rename launchers.

1. Sign in with existing Commissioner credentials. `/component-04` must redirect to `/component-04/assessments`. Sidebar starts with Proposals, has no numbers/Overview, and Complaint Inbox opens `/component-04/central-complaints`. Inspect existing four queues without submitting any handling action.
2. Proposals: expand/collapse View Proposal Data; compare its width/alignment with Start Screening. Do not create a screening merely for this check. Inspect Regulatory Screening's Run, History/Reports, Back and Continue controls and their real disabled states. No tools/evidence disclosure should appear below the page.
3. History: inspect existing saved reports. Saved stays on one line, report links align, and narrow tables scroll horizontally. Named workflow reads “Kiwula Kale — Named route v2”. Open/refresh saved regulatory, conflict, early and anomaly results without Run/Retry. All nine anomaly features and original findings remain present.
4. Sign out: header and applicant My complaints Sign in links must have white text on red, visible focus/hover, adequate height and no clipped label. No yellow Signed in/Sign in banner. The applicant complaint link must retain its existing return destination after login. Real expiry/error notices and signout must remain available.
5. Applicant: own complaints/public history remain accessible outside C4. Direct C4 root, nested screening/report/evidence URLs must redirect to the applicant dashboard without governance content/navigation. Do not submit/retry a complaint.
6. Responsible DS: own early assessment retains evidence attachment/verification/history controls; Commissioner is read-only and another DS remains denied. Inspect existing controls only; do not attach artificial evidence or record a decision. Complaint handling fields remain stacked/full-width with previous textarea heights.
7. Repeat at desktop, tablet and mobile widths. Check sidebar active state, horizontal scroll, action wrapping, keyboard tab order, focus rings, loading/errors and signout/account-switch privacy. Record actual observations; these graphical checks have not been automated.

Backend package, database and model files were unchanged. No staging, commit or push occurred.
