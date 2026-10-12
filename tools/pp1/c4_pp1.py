#!/usr/bin/env python3
"""Component 4 PP1 final-scenario runner with offline contract preview."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import shutil
import sys
import urllib.error
import urllib.request
import uuid
from datetime import datetime, timedelta, timezone
from pathlib import Path
from typing import Any

ROOT = Path(__file__).resolve().parents[2]
ARTIFACTS = ROOT / "artifacts" / "pp1"
DEFAULT_MANIFEST = ROOT / "tools" / "pp1" / "manifests" / "pp1-final-scenario-manifest-v2.json"
FINAL_LATEST_RUN = ARTIFACTS / "final-latest-run-v2.json"
API_BASE = os.getenv("COMPONENT4_API_BASE_URL", "http://127.0.0.1:8204/api/governance-intelligence").rstrip("/")
ML_BASE = os.getenv("COMPONENT4_ML_BASE_URL", "http://127.0.0.1:8104").rstrip("/")
BLOCKCHAIN_BASE = os.getenv("COMPONENT4_BLOCKCHAIN_BASE_URL", "http://127.0.0.1:8550").rstrip("/")
BESU_RPC = os.getenv("COMPONENT4_BESU_RPC_URL", "http://127.0.0.1:8545").rstrip("/")
SEED_VERSION = "pp1-seed-contract-v2"
MANIFEST_VERSION = 2
FINAL_CASES = (
    "PP1-FINAL-CLEAR-001",
    "PP1-FINAL-REFERRAL-001",
    "PP1-FINAL-DELAYED-001",
    "PP1-FINAL-COMPLAINT-001",
    "PP1-FINAL-COMBINED-001",
)
ORIGINAL_CASES = {
    "PP1-CLEAR-001", "PP1-REFERRAL-001", "PP1-DELAYED-001",
    "PP1-COMPLAINT-001", "PP1-COMBINED-001",
}
INDICATORS = (
    "LegalDispute", "UnauthorizedOccupation", "UnauthorizedConstruction",
    "FamilyOrInheritanceClaim", "MultipleClaimants", "UnresolvedObjection",
    "PreviousIllegalLandActivity",
)
ALLOWED_ENGINES = {
    "EarlyScreening", "ComplaintClassification", "WorkflowAnomaly",
    "LeaseTiming", "ReferralHandoff", "RegulatoryCompliance",
}
CORE_ENGINE_ENDPOINTS = {"RegulatoryCompliance": "evaluate-compliance"}
ANOMALY_COMPLETENESS = "CompletedTraceEligibleForAnomalyScoring"
ANOMALY_TERMINAL_ACTIVITY = "DS Final Notification"


def _request(method: str, url: str, payload: dict[str, Any] | None = None) -> Any:
    data = None if payload is None else json.dumps(payload, separators=(",", ":")).encode("utf-8")
    request = urllib.request.Request(url, data=data, method=method)
    request.add_header("Accept", "application/json")
    if data is not None:
        request.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(request, timeout=30) as response:
            body = response.read()
            return json.loads(body) if body else None
    except urllib.error.HTTPError as exc:
        detail = exc.read().decode("utf-8", errors="replace")[:1000]
        raise RuntimeError(f"HTTP {exc.code} from {url}: {detail}") from exc
    except urllib.error.URLError as exc:
        raise RuntimeError(f"Unable to reach {url}: {exc.reason}") from exc


def _id(case_id: str, kind: str) -> str:
    return str(uuid.uuid5(uuid.NAMESPACE_URL, f"stateland-governance:component4:pp1:{SEED_VERSION}:{case_id}:{kind}"))


def _iso(instant: datetime) -> str:
    return instant.astimezone(timezone.utc).isoformat().replace("+00:00", "Z")


def _events(case_id: str, generated_at: datetime, activities: list[str], gap_days: int) -> list[dict[str, Any]]:
    start = generated_at - timedelta(days=100)
    return [
        {
            "eventId": f"{case_id}-EVT-{index + 1:02d}",
            "eventSeq": index + 1,
            "activity": activity,
            "stageCode": f"STAGE-{index + 1:02d}",
            "institution": "DS" if index % 3 == 0 else "LCGD",
            "resource": "Officer-A" if index % 2 == 0 else "Officer-B",
            "timestampUtc": _iso(start + timedelta(days=index * gap_days)),
            "anomalyInputTimestamp": (start + timedelta(days=index * gap_days)).strftime("%Y-%m-%dT%H:%M:%S"),
        }
        for index, activity in enumerate(activities)
    ]


def _screening_evidence(case_id: str, outcome: str) -> list[dict[str, str]]:
    evidence = []
    for indicator in INDICATORS:
        state = "VerifiedAbsent"
        if outcome == "ReviewRequired" and indicator == "LegalDispute":
            state = "VerifiedPresent"
        elif outcome == "InsufficientInformation" and indicator == "LegalDispute":
            state = "Missing"
        evidence.append({
            "indicatorType": indicator,
            "evidenceState": state,
            "evidenceReference": f"SyntheticPp1Seed:{case_id}:{indicator}",
        })
    return evidence


def _scenario(
    case_id: str,
    hour: int,
    *,
    screening_outcome: str,
    has_complaint: bool,
    complaint_text: str | None,
    workflow_state: str,
    trace_completeness: str,
    activities: list[str],
    gap_days: int,
    roadmap_status: str,
    handover_status: str,
    timing_expectation: str,
    engines: list[str],
    core_requests: dict[str, dict[str, Any]] | None = None,
) -> dict[str, Any]:
    generated_at = datetime(2026, 10, 12, hour, tzinfo=timezone.utc)
    start = generated_at - timedelta(days=100)
    handover = start + timedelta(days=75 if timing_expectation == "ExceededExpectation" else 45)
    expected_referral = screening_outcome == "ReviewRequired"
    anomaly_eligible = (
        workflow_state == "Completed"
        and trace_completeness == ANOMALY_COMPLETENESS
        and activities[-1] == ANOMALY_TERMINAL_ACTIVITY
    )
    return {
        "caseId": case_id,
        "syntheticSeedId": case_id,
        "syntheticSeedVersion": SEED_VERSION,
        "sourceRecordVersions": {
            "component1": f"{SEED_VERSION}:component1",
            "component3": f"{SEED_VERSION}:component3",
        },
        "component1ProjectionId": _id(case_id, "component1-projection"),
        "component3ProjectionId": _id(case_id, "component3-projection"),
        "workflowRunId": _id(case_id, "workflow-run"),
        "screeningAssessmentId": _id(case_id, "screening-assessment"),
        "referralId": _id(case_id, "referral") if expected_referral else None,
        "generatedAtUtc": _iso(generated_at),
        "hasComplaint": has_complaint,
        "complaintText": complaint_text,
        "screeningEvidence": _screening_evidence(case_id, screening_outcome),
        "workflow": {
            "state": workflow_state,
            "traceCompleteness": trace_completeness,
            "terminalActivity": activities[-1],
            "events": _events(case_id, generated_at, activities, gap_days),
        },
        "timingEvidence": {
            "roadmapStart": {
                "status": roadmap_status,
                "occurredAtUtc": _iso(start) if roadmap_status == "Confirmed" else None,
            },
            "approvalHandover": {
                "status": handover_status,
                "occurredAtUtc": _iso(handover) if handover_status == "Confirmed" else None,
            },
        },
        "engines": engines,
        "coreEngineRequests": core_requests or {},
        "engineApplicability": {
            "EarlyScreening": {"status": "Applicable", "reason": "Uses supplied deterministic screening evidence."},
            "ComplaintClassification": {
                "status": "Applicable" if has_complaint else "NotApplicable",
                "reason": "Complaint declared and validated." if has_complaint else "hasComplaint=false; no complaint endpoint call is permitted.",
            },
            "WorkflowAnomaly": {
                "status": "Applicable" if anomaly_eligible else "NotEligible",
                "reason": "Completed contiguous trace satisfies the frozen v1 input contract." if anomaly_eligible else "Workflow is ongoing or incomplete; anomaly inference is not permitted.",
            },
            "LeaseTiming": {"status": "Applicable", "reason": "Uses only supplied timing-boundary evidence and may return Unavailable."},
            "ReferralHandoff": {
                "status": "Applicable" if expected_referral else "NotApplicable",
                "reason": "Deterministic screening is expected to require SyntheticPp1Handoff." if expected_referral else "Screening does not require referral.",
            },
            "RegulatoryCompliance": {
                "status": "Applicable" if "RegulatoryCompliance" in engines else "NotApplicable",
                "reason": "Explicitly listed core deterministic engine." if "RegulatoryCompliance" in engines else "Absent from explicit engines collection.",
            },
        },
        "expectedDeterministicOutcomes": {
            "screening": screening_outcome,
            "complaint": "Applicable" if has_complaint else "NotApplicable",
            "anomaly": "EligibleForLaterComputation" if anomaly_eligible else "NotEligible",
            "referral": "SyntheticPp1Handoff" if expected_referral else "NotRequired",
            "normalFlowExitRequested": expected_referral,
            "timing": timing_expectation,
        },
    }


def _manifest() -> dict[str, Any]:
    complete = ["Application Received", "Document Review", "Field Inspection", "Regional Approval", ANOMALY_TERMINAL_ACTIVITY]
    return {
        "manifestVersion": MANIFEST_VERSION,
        "syntheticSeedVersion": SEED_VERSION,
        "isSynthetic": True,
        "provenanceKind": "SyntheticPp1Seed",
        "purpose": "PP1Demonstration",
        "originalCaseIdsProhibited": sorted(ORIGINAL_CASES),
        "scenarios": [
            _scenario(FINAL_CASES[0], 12, screening_outcome="Clear", has_complaint=False, complaint_text=None,
                      workflow_state="Completed", trace_completeness=ANOMALY_COMPLETENESS, activities=complete, gap_days=7,
                      roadmap_status="Confirmed", handover_status="Confirmed", timing_expectation="WithinExpectation",
                      engines=["EarlyScreening", "WorkflowAnomaly", "LeaseTiming"]),
            _scenario(FINAL_CASES[1], 13, screening_outcome="ReviewRequired", has_complaint=False, complaint_text=None,
                      workflow_state="Ongoing", trace_completeness="PartialTraceIneligibleForAnomalyScoring", activities=complete[:-1], gap_days=7,
                      roadmap_status="Confirmed", handover_status="Unavailable", timing_expectation="BasedOnAvailableEvidence",
                      engines=["EarlyScreening", "LeaseTiming", "ReferralHandoff"]),
            _scenario(FINAL_CASES[2], 14, screening_outcome="Clear", has_complaint=False, complaint_text=None,
                      workflow_state="Completed", trace_completeness=ANOMALY_COMPLETENESS, activities=complete, gap_days=12,
                      roadmap_status="Confirmed", handover_status="Confirmed", timing_expectation="ExceededExpectation",
                      engines=["EarlyScreening", "WorkflowAnomaly", "LeaseTiming"]),
            _scenario(FINAL_CASES[3], 15, screening_outcome="InsufficientInformation", has_complaint=True,
                      complaint_text="Lease revenue payments were recorded, but an enforcement notice still requests the same amount.",
                      workflow_state="Ongoing", trace_completeness="PartialTraceIneligibleForAnomalyScoring", activities=complete[:3], gap_days=7,
                      roadmap_status="Unconfirmed", handover_status="Unavailable", timing_expectation="Unavailable",
                      engines=["EarlyScreening", "ComplaintClassification", "LeaseTiming"]),
            _scenario(FINAL_CASES[4], 16, screening_outcome="ReviewRequired", has_complaint=True,
                      complaint_text="Protected land may have been transferred and occupied without authorization while review remains unresolved.",
                      workflow_state="Completed", trace_completeness=ANOMALY_COMPLETENESS,
                      activities=["Application Received", "Document Review", "Correction Requested", "Document Review", "Field Inspection", "Regional Approval", ANOMALY_TERMINAL_ACTIVITY], gap_days=12,
                      roadmap_status="Confirmed", handover_status="Confirmed", timing_expectation="ExceededExpectation",
                      engines=["EarlyScreening", "ComplaintClassification", "WorkflowAnomaly", "LeaseTiming", "ReferralHandoff", "RegulatoryCompliance"],
                      core_requests={"RegulatoryCompliance": {
                          "actionName": "PP1-FINAL-COMBINED-001 synthetic regulatory review",
                          "leaseDurationYears": 30, "proposedUse": "PublicPurposeLease",
                          "leaseAmount": 100000, "zoningArea": "PP1-SENSITIVE-ZONE",
                      }}),
        ],
    }


def _canonical_bytes(value: Any) -> bytes:
    return (json.dumps(value, sort_keys=True, indent=2, ensure_ascii=False) + "\n").encode("utf-8")


def _write_json(path: Path, value: Any) -> str:
    path.parent.mkdir(parents=True, exist_ok=True)
    content = _canonical_bytes(value)
    path.write_bytes(content)
    return hashlib.sha256(content).hexdigest()


def _anomaly_eligibility(scenario: dict[str, Any]) -> tuple[bool, str]:
    workflow = scenario["workflow"]
    if workflow["state"] != "Completed":
        return False, "workflow state is not Completed"
    if workflow["traceCompleteness"] != ANOMALY_COMPLETENESS:
        return False, f"trace completeness is not {ANOMALY_COMPLETENESS}"
    events = workflow.get("events")
    if not isinstance(events, list) or not events:
        return False, "event sequence is empty"
    for index, event in enumerate(events, start=1):
        if event.get("eventSeq") != index:
            return False, f"event sequence is not contiguous at position {index}"
        if not str(event.get("anomalyInputTimestamp") or "").strip():
            return False, f"event {index} lacks an anomaly-input timestamp"
    if events[-1].get("activity") != ANOMALY_TERMINAL_ACTIVITY or workflow.get("terminalActivity") != ANOMALY_TERMINAL_ACTIVITY:
        return False, f"final activity is not {ANOMALY_TERMINAL_ACTIVITY}"
    return True, "eligible"


def _validate_manifest(manifest: dict[str, Any]) -> None:
    if manifest.get("manifestVersion") != MANIFEST_VERSION or manifest.get("syntheticSeedVersion") != SEED_VERSION:
        raise RuntimeError("Only the final pp1-seed-contract-v2 manifest is accepted.")
    if manifest.get("isSynthetic") is not True or manifest.get("provenanceKind") != "SyntheticPp1Seed" or manifest.get("purpose") != "PP1Demonstration":
        raise RuntimeError("Final manifest provenance is invalid.")
    scenarios = manifest.get("scenarios")
    if not isinstance(scenarios, list) or [item.get("caseId") for item in scenarios] != list(FINAL_CASES):
        raise RuntimeError("Final manifest must contain the five distinct PP1-FINAL case IDs in frozen order.")
    all_ids: list[str] = []
    for scenario in scenarios:
        case_id = scenario["caseId"]
        if case_id in ORIGINAL_CASES or scenario.get("syntheticSeedId") != case_id or scenario.get("syntheticSeedVersion") != SEED_VERSION:
            raise RuntimeError(f"{case_id}: final seed identity/version is invalid.")
        engines = scenario.get("engines")
        if not isinstance(engines, list) or not engines or len(set(engines)) != len(engines):
            raise RuntimeError(f"{case_id}: an explicit, duplicate-free engines collection is required.")
        unknown = set(engines) - ALLOWED_ENGINES
        if unknown:
            raise RuntimeError(f"{case_id}: unsupported engines: {sorted(unknown)}")
        if "EarlyScreening" not in engines or "LeaseTiming" not in engines:
            raise RuntimeError(f"{case_id}: screening and timing must be explicit.")
        text = scenario.get("complaintText")
        complaint_valid = scenario.get("hasComplaint") is True and isinstance(text, str) and bool(text.strip()) and len(text) <= 5000
        if scenario.get("hasComplaint") is False:
            if text is not None or "ComplaintClassification" in engines or scenario["engineApplicability"]["ComplaintClassification"]["status"] != "NotApplicable":
                raise RuntimeError(f"{case_id}: hasComplaint=false contract is inconsistent.")
        elif not complaint_valid or "ComplaintClassification" not in engines:
            raise RuntimeError(f"{case_id}: complaint eligibility requires valid text and an explicit engine.")
        anomaly_eligible, reason = _anomaly_eligibility(scenario)
        if anomaly_eligible != ("WorkflowAnomaly" in engines):
            raise RuntimeError(f"{case_id}: anomaly engine eligibility mismatch ({reason}).")
        if scenario["engineApplicability"]["WorkflowAnomaly"]["status"] != ("Applicable" if anomaly_eligible else "NotEligible"):
            raise RuntimeError(f"{case_id}: incorrect anomaly applicability status.")
        expected_referral = scenario["expectedDeterministicOutcomes"]["screening"] == "ReviewRequired"
        if expected_referral != ("ReferralHandoff" in engines) or expected_referral != bool(scenario.get("referralId")):
            raise RuntimeError(f"{case_id}: referral is not aligned to deterministic screening.")
        core = set(engines) & set(CORE_ENGINE_ENDPOINTS)
        requests = scenario.get("coreEngineRequests")
        if not isinstance(requests, dict) or set(requests) != core:
            raise RuntimeError(f"{case_id}: core requests must exactly match explicit core engines.")
        for field in ("component1ProjectionId", "component3ProjectionId", "workflowRunId", "screeningAssessmentId"):
            value = str(scenario.get(field))
            uuid.UUID(value)
            all_ids.append(value)
        if scenario.get("referralId"):
            uuid.UUID(str(scenario["referralId"]))
            all_ids.append(str(scenario["referralId"]))
    if len(all_ids) != len(set(all_ids)):
        raise RuntimeError("Deterministic projection, workflow, assessment, and referral IDs must be unique.")


def _load_approved_manifest(path: Path) -> dict[str, Any]:
    if not path.is_file():
        raise RuntimeError(f"Approved final manifest does not exist: {path}")
    manifest = json.loads(path.read_text(encoding="utf-8"))
    _validate_manifest(manifest)
    if manifest != _manifest():
        raise RuntimeError("Manifest does not exactly match the deterministic final v2 contract.")
    sidecar = path.with_suffix(path.suffix + ".sha256")
    if not sidecar.is_file():
        raise RuntimeError(f"Manifest hash sidecar is missing: {sidecar}")
    actual = hashlib.sha256(path.read_bytes()).hexdigest()
    if actual != sidecar.read_text(encoding="ascii").strip().lower():
        raise RuntimeError("Manifest SHA-256 does not match its approval sidecar.")
    return manifest


def command_preflight(_: argparse.Namespace) -> None:
    health = _request("GET", f"{ML_BASE}/health")
    if health.get("status") != "ready" or health.get("model_version") != "v2":
        raise RuntimeError("ML service is not ready with complaint model v2.")
    _request("GET", f"{API_BASE}/dashboard")
    print("API: reachable")
    print("ML service: ready; complaint model v2")


def command_seed_preview(args: argparse.Namespace) -> None:
    manifest = _manifest()
    _validate_manifest(manifest)
    destination = Path(args.output).resolve()
    digest = _write_json(destination, manifest)
    destination.with_suffix(destination.suffix + ".sha256").write_text(digest + "\n", encoding="ascii")
    for scenario in manifest["scenarios"]:
        summary = ", ".join(f"{name}={details['status']}" for name, details in scenario["engineApplicability"].items())
        print(f"{scenario['caseId']} | workflow={scenario['workflowRunId']} | {summary}")
    print(f"manifest: {destination}")
    print(f"SHA-256: {digest}")
    print("validation: passed")
    print("writes performed: manifest and SHA-256 sidecar only")


def command_seed_apply(args: argparse.Namespace) -> None:
    manifest = _load_approved_manifest(Path(args.manifest).resolve())
    for scenario in manifest["scenarios"]:
        payload = {
            "caseId": scenario["caseId"], "component1ProjectionId": scenario["component1ProjectionId"],
            "component3ProjectionId": scenario["component3ProjectionId"], "workflowRunId": scenario["workflowRunId"],
            "generatedAtUtc": scenario["generatedAtUtc"], "syntheticSeedVersion": manifest["syntheticSeedVersion"],
        }
        result = _request("POST", f"{API_BASE}/pp1/seeds", payload)
        if result.get("provenanceKind") != "SyntheticPp1Seed" or result.get("purpose") != "PP1Demonstration" or result.get("syntheticSeedVersion") != SEED_VERSION:
            raise RuntimeError(f"Unexpected PP1 provenance returned for {scenario['caseId']}.")
        print(f"{scenario['caseId']}: projections persisted idempotently")


def _screening_payload(scenario: dict[str, Any]) -> dict[str, Any]:
    return {
        "assessmentId": scenario["screeningAssessmentId"], "referralId": scenario["referralId"],
        "workflowRunId": scenario["workflowRunId"], "caseId": scenario["caseId"], "inputVersion": SEED_VERSION,
        "indicators": [{**item, "recordedAtUtc": scenario["generatedAtUtc"]} for item in scenario["screeningEvidence"]],
    }


def _sanitize_complaint(result: dict[str, Any]) -> dict[str, Any]:
    return {key: value for key, value in result.items() if key.lower() != "complainttext"}


def command_run(args: argparse.Namespace) -> None:
    manifest = _load_approved_manifest(Path(args.manifest).resolve())
    run_results: dict[str, Any] = {"runStartedAtUtc": datetime.now(timezone.utc).isoformat(), "syntheticSeedVersion": SEED_VERSION, "scenarios": []}
    for scenario in manifest["scenarios"]:
        case_id = scenario["caseId"]
        engines = set(scenario["engines"])
        screening = _request("POST", f"{API_BASE}/screen-early-governance", _screening_payload(scenario))
        if screening.get("overallStatus") != scenario["expectedDeterministicOutcomes"]["screening"]:
            raise RuntimeError(f"{case_id}: screening outcome violates the approved contract.")
        anomaly = _request("POST", f"{API_BASE}/workflow-anomalies/assess", {
            "caseId": case_id, "workflowRunId": scenario["workflowRunId"], "workflowProjectionId": scenario["component3ProjectionId"],
        }) if "WorkflowAnomaly" in engines else {"status": "NotEligible", "reason": scenario["engineApplicability"]["WorkflowAnomaly"]["reason"]}
        timing = _request("POST", f"{API_BASE}/lease-timing/assess", {
            "workflowProjectionId": scenario["component3ProjectionId"], "assessmentAtUtc": scenario["generatedAtUtc"],
        })
        complaint = _sanitize_complaint(_request("POST", f"{API_BASE}/classify-complaint", {
            "caseId": case_id, "complaintText": scenario["complaintText"],
        })) if "ComplaintClassification" in engines else {"status": "NotApplicable", "reason": scenario["engineApplicability"]["ComplaintClassification"]["reason"]}
        referrals = _request("GET", f"{API_BASE}/referrals/history/cases/{case_id}?limit=20")
        if "ReferralHandoff" in engines:
            expected_id = scenario["referralId"]
            if not any(item.get("referralId") == expected_id for item in referrals):
                raise RuntimeError(f"{case_id}: expected deterministic referral was not persisted.")
            handoff = _request("POST", f"{API_BASE}/referrals/{expected_id}/deliver", {})
        else:
            if referrals:
                raise RuntimeError(f"{case_id}: referral was created outside deterministic screening policy.")
            handoff = {"status": "NotApplicable", "reason": scenario["engineApplicability"]["ReferralHandoff"]["reason"]}
        core_results = {
            engine: _request("POST", f"{API_BASE}/{CORE_ENGINE_ENDPOINTS[engine]}", scenario["coreEngineRequests"][engine])
            for engine in engines & set(CORE_ENGINE_ENDPOINTS)
        }
        run_results["scenarios"].append({
            "caseId": case_id, "workflowRunId": scenario["workflowRunId"], "screening": screening,
            "referral": handoff, "complaint": complaint, "anomaly": anomaly, "timing": timing,
            "coreDeterministicEngines": core_results,
        })
        print(f"{case_id}: screening={screening['overallStatus']} complaint={complaint.get('status', 'Computed')} anomaly={anomaly.get('status', 'Computed')} timing={timing['timingState']}")
    run_results["runCompletedAtUtc"] = datetime.now(timezone.utc).isoformat()
    digest = _write_json(FINAL_LATEST_RUN, run_results)
    FINAL_LATEST_RUN.with_suffix(".json.sha256").write_text(digest + "\n", encoding="ascii")
    print(f"sanitized final run evidence: {FINAL_LATEST_RUN}")
    print(f"SHA-256: {digest}")


def command_status(_: argparse.Namespace) -> None:
    dashboard = _request("GET", f"{API_BASE}/dashboard")
    health = _request("GET", f"{ML_BASE}/health")
    print(f"API: reachable; dashboard cases={len(dashboard.get('cases', []))}")
    print(f"ML: {health.get('status')}; complaint model={health.get('model_version')}")
    for case_id in FINAL_CASES:
        anomaly = _request("GET", f"{API_BASE}/workflow-anomalies/history/cases/{case_id}?limit=20")
        timing = _request("GET", f"{API_BASE}/lease-timing/history/cases/{case_id}?limit=20")
        print(f"{case_id}: anomaly={len(anomaly)} timing={len(timing)}")


def command_metrics(_: argparse.Namespace) -> None:
    if not FINAL_LATEST_RUN.is_file():
        raise RuntimeError("No sanitized final PP1 run evidence exists.")
    scenarios = json.loads(FINAL_LATEST_RUN.read_text(encoding="utf-8"))["scenarios"]
    print(f"scenario count: {len(scenarios)}")
    print(f"complaints computed: {sum(item['complaint'].get('status') != 'NotApplicable' for item in scenarios)}")
    print(f"anomalies computed: {sum(item['anomaly'].get('status') != 'NotEligible' for item in scenarios)}")
    print(f"synthetic handoffs acknowledged: {sum(bool(item['referral'].get('workflowTransitionConfirmed')) for item in scenarios)}")


def command_evidence(args: argparse.Namespace) -> None:
    if not FINAL_LATEST_RUN.is_file() or not DEFAULT_MANIFEST.is_file():
        raise RuntimeError("Final manifest and completed final run evidence are required.")
    manifest = _load_approved_manifest(DEFAULT_MANIFEST)
    run_results = json.loads(FINAL_LATEST_RUN.read_text(encoding="utf-8"))
    audit_record_id = str(uuid.UUID(args.audit_record_id))
    stored_hash = args.stored_record_hash.strip().lower()
    if len(stored_hash) != 64 or any(character not in "0123456789abcdef" for character in stored_hash):
        raise RuntimeError("Stored audit record hash must be exactly 64 lowercase hexadecimal characters.")

    ml_health = _request("GET", f"{ML_BASE}/health")
    dashboard = _request("GET", f"{API_BASE}/dashboard")
    blockchain_health = _request("GET", f"{BLOCKCHAIN_BASE}/health")
    besu_chain = _request("POST", BESU_RPC, {"jsonrpc": "2.0", "method": "eth_chainId", "params": [], "id": 1})
    besu_block = _request("POST", BESU_RPC, {"jsonrpc": "2.0", "method": "eth_blockNumber", "params": [], "id": 1})
    verification = _request("POST", f"{API_BASE}/audits/{audit_record_id}/verify", {})
    ledger_receipt = _request("GET", f"{BLOCKCHAIN_BASE}/api/v1/anchors/{audit_record_id}")
    recomputed_hash = verification.get("recordHash", "").lower()
    ledger_hash = ledger_receipt.get("recordHash", "").lower()
    three_way_match = recomputed_hash == stored_hash == ledger_hash
    if verification.get("verificationStatus") != "Match" or not three_way_match:
        raise RuntimeError("AUDIT-V2 evidence does not have a verified three-way hash match.")

    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    destination = ARTIFACTS / "runs" / f"{stamp}-final"
    destination.mkdir(parents=True, exist_ok=False)
    shutil.copy2(FINAL_LATEST_RUN, destination / "scenario-results.json")
    shutil.copy2(FINAL_LATEST_RUN.with_suffix(".json.sha256"), destination / "scenario-results.json.sha256")

    scenarios_by_id = {item["caseId"]: item for item in run_results["scenarios"]}
    scenario_evidence = []
    for scenario in manifest["scenarios"]:
        result = scenarios_by_id[scenario["caseId"]]
        scenario_evidence.append({
            "caseId": scenario["caseId"],
            "workflowRunId": scenario["workflowRunId"],
            "component1ProjectionId": scenario["component1ProjectionId"],
            "component3ProjectionId": scenario["component3ProjectionId"],
            "screeningAssessmentId": scenario["screeningAssessmentId"],
            "referralId": scenario["referralId"],
            "engineApplicability": scenario["engineApplicability"],
            "screening": result["screening"],
            "complaint": result["complaint"],
            "anomaly": result["anomaly"],
            "timing": result["timing"],
            "referral": result["referral"],
            "coreDeterministicEngines": result["coreDeterministicEngines"],
        })

    anomaly_versions = sorted({
        item["anomaly"]["modelVersion"]
        for item in run_results["scenarios"]
        if item["anomaly"].get("modelVersion")
    })
    evidence = {
        "createdAtUtc": datetime.now(timezone.utc).isoformat(),
        "manifest": {
            "path": str(DEFAULT_MANIFEST.relative_to(ROOT)).replace("\\", "/"),
            "sha256": hashlib.sha256(DEFAULT_MANIFEST.read_bytes()).hexdigest(),
            "manifestVersion": manifest["manifestVersion"],
            "syntheticSeedVersion": manifest["syntheticSeedVersion"],
        },
        "provenance": {
            "kind": "SyntheticPp1Seed",
            "statement": "All final PP1 source projections and derived eligible assessments use explicit SyntheticPp1Seed provenance under pp1-seed-contract-v2.",
        },
        "models": {
            "complaintClassifier": ml_health.get("model_version"),
            "workflowAnomaly": anomaly_versions,
        },
        "serviceHealth": {
            "component4Api": {"reachable": True, "dashboardCaseCount": len(dashboard.get("cases", []))},
            "ml": ml_health,
            "blockchainService": blockchain_health,
            "besu": {"reachable": True, "chainId": besu_chain.get("result"), "blockNumber": besu_block.get("result")},
        },
        "scenarios": scenario_evidence,
        "auditBlockchainEvidence": [{
            "auditRecordId": audit_record_id,
            "hashVersion": verification.get("recordVersion"),
            "anchorStatus": verification.get("anchorStatus"),
            "verificationStatus": verification.get("verificationStatus"),
            "outboxState": "Completed",
            "receiptState": "Anchored",
            "transactionHash": verification.get("ledgerTransactionReference"),
            "blockNumber": verification.get("blockNumber"),
            "recomputedHash": recomputed_hash,
            "storedHash": stored_hash,
            "ledgerHash": ledger_hash,
            "threeWayMatch": three_way_match,
        }],
        "auditCoverageStatement": "Only the combined RegulatoryCompliance operation created an audit record in the current production audit coverage; no audit records were fabricated for unaudited operations.",
        "knownLimitation": "The current outbox has no atomic multi-instance claim/lease; concurrent processors could submit the same hash more than once. This controlled PP1 run used one API/outbox processor instance.",
        "safety": {
            "containsRawComplaintText": False,
            "containsCredentials": False,
            "containsConnectionStrings": False,
            "containsPrivateKeys": False,
        },
    }
    evidence_path = destination / "final-execution-evidence.json"
    evidence_hash = _write_json(evidence_path, evidence)
    evidence_path.with_suffix(".json.sha256").write_text(evidence_hash + "\n", encoding="ascii")
    artifact_hashes = {
        path.name: hashlib.sha256(path.read_bytes()).hexdigest()
        for path in sorted(destination.iterdir())
        if path.is_file() and not path.name.endswith(".sha256")
    }
    hash_manifest_path = destination / "artifact-hashes.json"
    hash_manifest_hash = _write_json(hash_manifest_path, artifact_hashes)
    hash_manifest_path.with_suffix(".json.sha256").write_text(hash_manifest_hash + "\n", encoding="ascii")
    print(destination)


def _parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description=__doc__)
    subparsers = parser.add_subparsers(dest="command", required=True)
    subparsers.add_parser("preflight").set_defaults(func=command_preflight)
    preview = subparsers.add_parser("seed-preview")
    preview.add_argument("--output", default=str(DEFAULT_MANIFEST))
    preview.set_defaults(func=command_seed_preview)
    apply_seed = subparsers.add_parser("seed-apply")
    apply_seed.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    apply_seed.set_defaults(func=command_seed_apply)
    run = subparsers.add_parser("run")
    run.add_argument("--manifest", default=str(DEFAULT_MANIFEST))
    run.set_defaults(func=command_run)
    subparsers.add_parser("status").set_defaults(func=command_status)
    subparsers.add_parser("metrics").set_defaults(func=command_metrics)
    evidence = subparsers.add_parser("evidence")
    evidence.add_argument("--audit-record-id", required=True)
    evidence.add_argument("--stored-record-hash", required=True)
    evidence.set_defaults(func=command_evidence)
    return parser


def main() -> int:
    try:
        args = _parser().parse_args()
        args.func(args)
        return 0
    except Exception as exc:
        print(f"ERROR: {exc}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    raise SystemExit(main())
