import sys
from pathlib import Path
sys.path.insert(0, str(Path("src").resolve()))

from predict_v1 import load_versioned_artifact, predict_complaint

LABELS = [
    "Administrative / Procedural / Integrity",
    "Lease Revenue / Payment / Enforcement",
    "Unauthorized Allocation / Transfer / Use",
    "Protected / Environmental Lease Misuse",
]

print("Loading artifact in fresh process...")
artifact = load_versioned_artifact()
ver = artifact["model_version"]
classes = artifact["pipeline"].named_steps["clf"].classes_.tolist()
print(f"Model version: {ver}")
print(f"Fitted classes: {classes}")
print()

complaints = [
    ("SMOKE-001", "The officer handling my fictional state-land lease application demanded an unofficial cash payment before forwarding the file."),
    ("SMOKE-002", "Lease revenue collected from the fictional lessee was not deposited into the designated government treasury account."),
    ("SMOKE-003", "A parcel of state land was allocated to a fictional company without any competitive selection or public tender process."),
    ("SMOKE-004", "A protected wetland reserve in a fictional district was leased for commercial construction without environmental clearance."),
]

all_ok = True
for case_id, text in complaints:
    result = predict_complaint(text, artifact, case_id=case_id)
    probs  = result["class_probabilities"]
    total  = sum(probs.values())
    valid_label  = result["predicted_category"] in LABELS
    all_finite   = all(p == p and abs(p) != float("inf") for p in probs.values())
    sum_ok       = abs(total - 1.0) < 1e-3
    version_ok   = result["model_version"] == "v1"
    status = "PASS" if (valid_label and all_finite and sum_ok and version_ok) else "FAIL"
    if status == "FAIL":
        all_ok = False
    print(f"[{status}] {case_id}: {result['predicted_category']}")
    print(f"       prob_sum={total:.6f}  finite={all_finite}  valid={valid_label}  ver_ok={version_ok}")
    top = sorted(probs.items(), key=lambda x: -x[1])[:2]
    for lbl, p in top:
        print(f"       {lbl}: {p:.4f}")
    print()

print("Smoke test complete:", "ALL PASS" if all_ok else "FAILURES DETECTED")
sys.exit(0 if all_ok else 1)
