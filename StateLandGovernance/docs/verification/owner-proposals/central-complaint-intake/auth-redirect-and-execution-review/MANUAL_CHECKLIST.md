# Manual checks — redirect correction only

Use current healthy services; no restart/SQL/model call is needed. Existing API startup guide remains in `../history-and-ds-form-fix/MANUAL_WALKTHROUGH.md`.

1. Sign out. Open `http://localhost:3000/workflow-governance/applicant/complaints`. Click the header Sign in. Confirm the sign-in URL carries the exact complaints return. Enter existing applicant credentials; verify My complaints opens, then refresh and inspect existing saved classification/public history.
2. Sign out and repeat using the Sign in button inside My complaints. Both paths must return to the same exact complaints URL. Try an incorrect password first; confirm error, unchanged return URL and no navigation.
3. Open plain `/sign-in` without a return value: applicant still lands on its existing cases dashboard. With the complaint return present, Commissioner/DS must keep their normal authorized routing rather than being forced into the applicant destination. External/protocol-relative return values must be ignored.
4. Applicant direct C4 root/nested report/evidence URLs remain denied. Commissioner can still view all four central queues and authorized saved reports. Do not submit complaint actions, screening, evidence or decisions for presentation checks.
5. Check keyboard focus, narrow layout, readable header/button labels, signout and account switching. These are not automated browser observations.
6. **Commissioner Early/Anomaly execution is still blocked.** Do not interpret this delivery as enabling Run. Resolve the execution and immutable input-source policy in STATUS_AND_EXECUTION_BOUNDARY.md first. No DS ownership bypass, old-route substitute or fabricated workflow is authorized by this checklist.
