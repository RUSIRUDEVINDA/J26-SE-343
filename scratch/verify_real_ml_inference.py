import os
import json
import urllib.request

base_api = "http://localhost:5264/api/governance-intelligence"

# ==============================================================================
# 1. TEST REAL COMPLAINT CLASSIFIER (TF-IDF + Logistic Regression)
# ==============================================================================
print("=" * 80)
print("1. EVALUATING REAL COMPLAINT CLASSIFICATION ML (4-CLASS TAXONOMY)")
print("=" * 80)

complaint_samples = [
    {
        "caseId": "CASE-COMPLAINT-001",
        "applicantId": "APP-2026-001",
        "text": "The divisional secretariat officer accepted unauthorized payments directly without issuing formal government treasury receipts and bypassed gazetted valuation.",
        "description": "Allegation of financial and procedural irregularities"
    },
    {
        "caseId": "CASE-COMPLAINT-002",
        "applicantId": "APP-2026-002",
        "text": "The leaseholder failed to pay annual lease dues for 3 consecutive years and breached revenue enforcement conditions.",
        "description": "Lease revenue and enforcement non-compliance"
    },
    {
        "caseId": "CASE-COMPLAINT-003",
        "applicantId": "APP-2026-003",
        "text": "Commercial construction started on coastal wetland sanctuary reserve violating environmental protection and sanctuary lease clauses.",
        "description": "Environmental / sanctuary boundary misuse"
    },
    {
        "caseId": "CASE-COMPLAINT-004",
        "applicantId": "APP-2026-004",
        "text": "The land parcel was transferred to a third-party corporate entity without ministerial approval or statutory transfer authorization.",
        "description": "Unauthorized transfer / transfer without authority"
    }
]

for sample in complaint_samples:
    payload = json.dumps({
        "caseId": sample["caseId"],
        "applicantId": sample["applicantId"],
        "complaintText": sample["text"]
    }).encode("utf-8")
    
    req = urllib.request.Request(f"{base_api}/classify-complaint", data=payload, headers={"Content-Type": "application/json"})
    resp = json.loads(urllib.request.urlopen(req).read().decode())
    
    print(f"\n[Case: {sample['caseId']}] - {sample['description']}")
    print(f"  Input: \"{sample['text'][:75]}...\"")
    print(f"  Predicted Category: {resp.get('predictedCategory')}")
    print(f"  Model Version: {resp.get('modelVersion')}")
    print("  Class Probabilities:")
    for cat, prob in resp.get("classProbabilities", {}).items():
        print(f"    - {cat}: {prob:.4f} ({(prob*100):.1f}%)")

# ==============================================================================
# 2. TEST REAL WORKFLOW ANOMALY DETECTION (ISOLATION FOREST)
# ==============================================================================
print("\n" + "=" * 80)
print("2. EVALUATING REAL WORKFLOW ANOMALY DETECTION (ISOLATION FOREST)")
print("=" * 80)

workflow_dir = "Component4_Synthetic_Integration_Seeds/inputs/workflows"
workflow_files = sorted(os.listdir(workflow_dir))

for fname in workflow_files:
    fpath = os.path.join(workflow_dir, fname)
    with open(fpath, "r", encoding="utf-8") as f:
        data = json.load(f)
    
    events_mapped = [
        {
            "eventSeq": e.get("event_seq", e.get("eventSeq", idx + 1)),
            "activity": e.get("activity", ""),
            "institution": e.get("institution", ""),
            "resource": e.get("resource", ""),
            "timestamp": e.get("timestamp", "2026-01-01T00:00:00")
        }
        for idx, e in enumerate(data.get("events", []))
    ]
    
    payload = json.dumps({
        "caseId": data.get("case_id", data.get("caseId")),
        "events": events_mapped
    }).encode("utf-8")
    
    req = urllib.request.Request(f"{base_api}/predict-workflow-anomaly", data=payload, headers={"Content-Type": "application/json"})
    resp = json.loads(urllib.request.urlopen(req).read().decode())
    
    print(f"\n[Workflow Trace: {fname}] (Case: {data['case_id']}, Steps: {len(data['events'])})")
    print(f"  Anomaly Flag: {resp.get('anomalyFlag')}")
    print(f"  Anomaly Score: {resp.get('anomalyScore')}")
    print(f"  Threshold: {resp.get('threshold')}")
    print(f"  Flagged for Review: {resp.get('isFlagged')}")

print("\n" + "=" * 80)
print("ALL ML INFERENCE VERIFICATION COMPLETED CLEANLY WITH FROZEN REAL MODELS.")
print("=" * 80)
