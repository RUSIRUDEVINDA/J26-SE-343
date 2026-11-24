# Ordinary activation stopped — automatic startup migration gate

2026-10-23, Asia/Colombo. **NOT ACTIVATED. No ordinary writes or API replacement performed.** The user's persistent authorization and two-migration approval remains recorded; execution stopped at its explicit mandatory safety condition.

The reviewed `StateLandGovernance/src/Api/Program.cs:408` calls `LandIntelligenceDbContext.Database.Migrate()` in Development whenever CanConnect succeeds. The non-Development branch also calls Migrate at line425. No opt-out is present in this startup block. The approved candidate selects Development with a real read-only C1 connection.

Read-only connection protection blocks writes but does not prevent the prohibited automatic migration invocation. Starting the package would therefore violate the user's instruction **“Do not run automatic startup migrations”** and could attempt peer-module migration work. No failure was deliberately triggered to test that boundary.

Fresh local checks confirm the complete approved review, source and88-file package manifests match. This is a defect in the reviewed startup behavior, not confirmed source/package drift. Evidence: [ORDINARY_ACTIVATION_STARTUP_GATE_STOP.json](ORDINARY_ACTIVATION_STARTUP_GATE_STOP.json).

The remaining current-database/backup/schema/grant/fingerprint preflight was not reached; prior successful evidence remains historical. No ordinary SQL, schema/grant change, private environment/file change, Commissioner permission issuance, sign-in, complaint action, model inference, worker/ledger/Groq operation, API start/stop, commit or push occurred. Existing services were left untouched. No rollback is necessary because activation never began. The approved manifests/checkpoints/completion report are preserved without silently rewriting their hashes.

The smallest proposed correction is limited to the composition host: remove the automatic LandIntelligence migration invocations from startup, retain explicit operator-run migrations and all read-only connections, then rebuild/test/publish and review the changed package hashes. C1/C2/C3 module code/databases, the approved C4 SQL/grants and persistent permission design need no change. This correction has NOT been implemented as an unreviewed workaround.

Following the user's **“If a mandatory gate fails, STOP”** instruction, work stops here. Continuation requires approval of that narrowly scoped host-startup/package correction (or another explicitly reviewed way to prevent the startup migration invocation), followed by the full immediate activation preflight. Never bypass the gate by falsifying connection availability, weakening read-only protection, changing environment or launching an incompatible package.


Resolved by the separately approved host correction and successful ordinary activation. See [ORDINARY_ACTIVATED_AND_VERIFIED.md](ORDINARY_ACTIVATED_AND_VERIFIED.md). The stopped attempt above remains historical evidence.
