# C4 Step 9B — attachment-grant path gate stopped

2026-10-24, Asia/Colombo. **ACTIVATION STOPPED.** No grant, service start, application process, Git operation or ordinary business write occurred in this continuation.

The exact reviewed artifact remains readable to the current operator at:

```text
D:\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\AI-Driven-Digital-Lease-Governance-for-State-Land-Management\StateLandGovernance\docs\verification\owner-proposals\central-complaint-intake\step1-early-governance-20261024\REVIEWED_ADDITIONAL_PERMISSIONS.sql
```

It is 271 characters long and has SHA256 `59c21d8cd227490d8c00fe5a279b5dcc5af71cd4d00209ff6086a2a9103e9674`, independently recorded in `FINAL_REVIEW_SHA256.json`. Its complete content is the approved single statement:

```sql
GRANT SELECT, INSERT ON TABLE governance_intelligence.officer_investigation_attachments TO slg_c4_regulatory_runtime;
```

The active Windows account can read the file and its ACL permits read access. PostgreSQL 18.6 `psql` nevertheless rejected the standard resolved absolute path with `No such file or directory`. It also rejected the Windows extended-length form; its diagnostic normalized that input to `//?/D:/...` and returned the same error. Therefore this is a client-side path-length/extended-path handling gate, not a missing, changed, inaccessible, or unreviewed SQL artifact.

The final read-only state remains `state_land_governance_c4`, OID `34144`, 42 tables and 23 migrations, with both approved migration IDs present. `officer_investigation_attachments` has zero rows and `slg_c4_regulatory_runtime` has no privileges on it. The API, frontend and ML ports (5264, 3000 and 8104) were not listening. Frozen complaint-v2 and anomaly-v1 hashes still match their reviewed values.

Do not rerun either migration, use a substitute grant file, broaden rights, or start services. Continuing requires review of a safe way for `psql` to consume this same verified artifact without its unsupported long path, followed by a fresh grant-only preflight.
