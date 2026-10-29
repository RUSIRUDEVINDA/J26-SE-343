# Step 7B launcher validation

2026-10-24, Asia/Colombo. All launcher validation used the retained isolated PostgreSQL container on 127.0.0.1:57657 or a read-only ordinary preflight. No ordinary service was started and no ordinary write occurred.

| Scenario | Result |
| --- | --- |
| Isolated restore with both migrations, attachment table and INSERT authority | Passed validation-only mode; no process started |
| Ordinary 41-table/21-migration target | Rejected before startup: post-migration contract was not exact |
| Isolated Step 1-only restore | Rejected before startup: post-migration contract was not exact |
| Isolated role without attachment INSERT | Rejected before startup: post-migration contract was not exact |
| Invalid candidate path | Rejected before connection/startup |

The temporary no-INSERT isolated role and its grants were removed. The isolated container was stopped after validation. Separate Step 7A evidence remains the source of the full isolated authentication and C4 HTTP behavior proof. The 88-file package reached isolated health on port 5807; its migration history stayed at 23. The temporary package process was stopped.
