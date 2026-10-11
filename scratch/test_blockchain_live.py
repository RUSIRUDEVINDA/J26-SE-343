import psycopg2
import urllib.request
import json
import uuid
from datetime import datetime, timezone

# 1. Connect to PostgreSQL
conn = psycopg2.connect(host="127.0.0.1", port=5432, dbname="stateland_governance", user="postgres", password="1234")
cur = conn.cursor()

# Find an audit record
cur.execute('SELECT "Id", "EngineType", "ActionName", "Status", "Details", "Timestamp" FROM governance_intelligence.governance_audit_records ORDER BY "Timestamp" DESC LIMIT 1')
row = cur.fetchone()
print("Found existing audit record:", row[0])
record_id = row[0]

# Query C# API receipt to get the exact canonical hash computed by GovernanceAuditHasher
req = urllib.request.Request(f"http://127.0.0.1:5264/api/governance-intelligence/audits/{record_id}/receipt")
res = urllib.request.urlopen(req)
receipt = json.loads(res.read().decode())
canonical_hash = receipt["recordHash"]
print(f"Audit ID: {record_id}")
print(f"Authoritative Canonical Hash (AUDIT-V1): {canonical_hash}")
print(f"Current verification status before anchoring: {receipt.get('verificationStatus')}")

# Anchor this record via Go blockchain service
anchor_payload = json.dumps({
    "auditRecordId": str(record_id),
    "recordHash": canonical_hash,
    "engineType": receipt.get("engineType", "RegulatoryCompliance"),
    "schemaVersion": "AUDIT-V1"
}).encode("utf-8")

anchor_req = urllib.request.Request("http://127.0.0.1:8550/api/v1/anchors", data=anchor_payload, headers={"Content-Type": "application/json"})
try:
    anchor_res = urllib.request.urlopen(anchor_req)
    anchor_json = json.loads(anchor_res.read().decode())
    print("\n[ANCHOR SUCCESS]")
    print(json.dumps(anchor_json, indent=2))
except Exception as e:
    print(f"\n[ANCHOR FAILED]: {e}")

# Now call verify on C# API
verify_req = urllib.request.Request(f"http://127.0.0.1:5264/api/governance-intelligence/audits/{record_id}/verify", data=b"", headers={"Content-Type": "application/json"})
verify_res = urllib.request.urlopen(verify_req)
verify_json = json.loads(verify_res.read().decode())
print("\n[VERIFY RESULT ON C# API]")
print(json.dumps(verify_json, indent=2))

conn.close()

