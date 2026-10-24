import json
import urllib.request
import urllib.error

BASE_URL = "http://localhost:5264/api/governance-intelligence"

def post_json(endpoint, payload):
    url = f"{BASE_URL}/{endpoint}"
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        return e.code, error_body

def get_json(endpoint):
    url = f"{BASE_URL}/{endpoint}"
    req = urllib.request.Request(url)
    try:
        with urllib.request.urlopen(req) as resp:
            return resp.status, json.loads(resp.read().decode())
    except urllib.error.HTTPError as e:
        error_body = e.read().decode()
        return e.code, error_body

print("=== 1. TEST CLASSIFY COMPLAINT ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/complaints/SIM-COMP-001-01.json") as f:
    complaint_input = json.load(f)
status, res = post_json("classify-complaint", {
    "caseId": complaint_input["caseId"],
    "complaintText": complaint_input["complaintText"]
})
print(f"Status: {status}")
if status == 200:
    print(f"  Predicted Category: {res.get('predictedCategory')}")
    print(f"  Model Version: {res.get('modelVersion')}")
    print(f"  Class Probabilities: {json.dumps(res.get('classProbabilities'), indent=2)}")
else:
    print(f"  Error: {res}")

print("\n=== 2. TEST PREDICT WORKFLOW ANOMALY ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/workflows/SIM-C4-2026-005.json") as f:
    wf_input = json.load(f)
events_mapped = [
    {
        "eventSeq": e["event_seq"],
        "activity": e["activity"],
        "institution": e["institution"],
        "resource": e["resource"],
        "timestamp": e["timestamp"]
    }
    for e in wf_input["events"]
]
status, res = post_json("predict-workflow-anomaly", {
    "caseId": wf_input.get("case_id", wf_input.get("caseId")),
    "events": events_mapped
})
print(f"Status: {status}")
if status == 200:
    print(f"  Anomaly Flag: {res.get('isAnomaly')}")
    print(f"  Anomaly Score: {res.get('anomalyScore')}")
    print(f"  Threshold: {res.get('threshold')}")
    print(f"  Features: {json.dumps(res.get('features'), indent=2)}")
else:
    print(f"  Error: {res}")

print("\n=== 3. TEST SCREEN EARLY GOVERNANCE ===")
import uuid
with open("Component4_Synthetic_Integration_Seeds/inputs/screening_internal/SIM-C4-2026-001.json") as f:
    screening_input = json.load(f)
screening_input["assessmentId"] = str(uuid.uuid4())
status, res = post_json("screen-early-governance", screening_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Assessment ID: {res.get('assessmentId')}")
    print(f"  Overall Status: {res.get('overallStatus')}")
    print(f"  Findings Count: {len(res.get('findings', []))}")
else:
    print(f"  Error: {res}")

print("\n=== 4. TEST COMPLIANCE EVALUATION ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/compliance/SIM-C4-2026-001.json") as f:
    comp_input = json.load(f)
status, res = post_json("evaluate-compliance", comp_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Status: {res.get('status')}")
    print(f"  Deterministic Id: {res.get('deterministicEvaluationId')}")
    print(f"  Findings Count: {len(res.get('findings', []))}")
else:
    print(f"  Error: {res}")

print("\n=== 5. TEST CONFLICT DETECTION ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/conflicts/SIM-C4-2026-003.json") as f:
    conf_input = json.load(f)
status, res = post_json("detect-conflicts", conf_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Status: {res.get('status')}")
    print(f"  Detected Conflicts: {len(res.get('conflicts', []))}")
    if res.get('conflicts'):
        c0 = res['conflicts'][0]
        print(f"  Sample Conflict: {c0.get('conflictType')} | Severity: {c0.get('severity')} | {c0.get('explanation')}")
else:
    print(f"  Error: {res}")

print("\n=== 6. TEST RISK ASSESSMENT ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/risk/SIM-C4-2026-005.json") as f:
    risk_input = json.load(f)
status, res = post_json("evaluate-risk", risk_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Overall Risk Level: {res.get('overallRiskLevel')}")
    print(f"  Overall Risk Score: {res.get('overallRiskScore')}")
    print(f"  Triggered Indicators: {len(res.get('triggeredIndicators', []))}")
else:
    print(f"  Error: {res}")

print("\n=== 7. TEST CONSENSUS EVALUATION ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/consensus/SIM-C4-2026-002.json") as f:
    cons_input = json.load(f)
status, res = post_json("evaluate-consensus", cons_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Outcome: {res.get('outcome')}")
    print(f"  Summary: {res.get('summaryExplanation')}")
    print(f"  Quorum Satisfied: {res.get('quorumSatisfied')}")
else:
    print(f"  Error: {res}")

print("\n=== 8. TEST CONDITIONAL VERIFICATION ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/conditions/SIM-C4-2026-001.json") as f:
    cond_input = json.load(f)
status, res = post_json("verify-conditions", cond_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Outcome: {res.get('outcome')}")
    print(f"  Total Conditions: {res.get('totalConditionsCount')}")
    print(f"  Unsatisfied Mandatory: {res.get('unsatisfiedMandatoryCount')}")
else:
    print(f"  Error: {res}")

print("\n=== 9. TEST EXPLAINABILITY ===")
with open("Component4_Synthetic_Integration_Seeds/inputs/explain_starter/SIM-C4-2026-001.json") as f:
    expl_input = json.load(f)
status, res = post_json("explain", expl_input)
print(f"Status: {status}")
if status == 200:
    print(f"  Overall Severity: {res.get('overallSeverity')}")
    print(f"  Explanations Count: {len(res.get('explanations', []))}")
    if res.get('explanations'):
        e0 = res['explanations'][0]
        print(f"  Sample Explanation: {e0.get('title')} -> {e0.get('plainLanguageExplanation')}")
else:
    print(f"  Error: {res}")

print("\n=== 10. TEST DASHBOARD ===")
status, res = get_json("dashboard")
print(f"Status: {status}")
if status == 200:
    print(f"  Summary: {json.dumps(res.get('summary'), indent=2)}")
    print(f"  Total Cases: {len(res.get('cases', []))}")
else:
    print(f"  Error: {res}")

print("\n=== 11. TEST AUDIT RECEIPT & VERIFY ===")
import psycopg2
conn = psycopg2.connect("host=localhost port=5432 dbname=stateland_governance user=postgres password=1234")
cur = conn.cursor()
cur.execute('SELECT "Id" FROM governance_intelligence.governance_audit_records LIMIT 1;')
audit_row = cur.fetchone()
conn.close()

if audit_row:
    audit_id = audit_row[0]
    print(f"Using persisted Audit ID: {audit_id}")
    status, res = get_json(f"audits/{audit_id}/receipt")
    print(f"Receipt GET status: {status}")
    if status == 200:
        print(f"  Anchor Status: {res.get('anchorStatus')}")
        print(f"  Verification Status: {res.get('verificationStatus')}")
        print(f"  Explanation: {res.get('explanation')}")

    status, res = post_json(f"audits/{audit_id}/verify", {})
    print(f"Verify POST status: {status}")
    if status == 200:
        print(f"  Verification Status: {res.get('verificationStatus')}")
        print(f"  Explanation: {res.get('explanation')}")
else:
    print("No audit records found to test receipts.")
