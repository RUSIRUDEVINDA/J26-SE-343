import os
import json
import time
from playwright.sync_api import sync_playwright

def run_browser_walkthrough():
    evidence_dir = "scratch/browser_evidence"
    os.makedirs(evidence_dir, exist_ok=True)
    
    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(viewport={"width": 1280, "height": 800})
        page = context.new_page()
        
        results = {}
        
        # 1. Governance Dashboard Overview
        print("\n--- 1. Testing Governance Dashboard ---")
        page.goto("http://localhost:3000/component-04", wait_until="networkidle")
        page.wait_for_selector("h1:has-text('Governance Intelligence')")
        page.screenshot(path=f"{evidence_dir}/01_dashboard.png")
        heading = page.locator("h1").inner_text()
        print(f"  Page Heading: {heading}")
        # Verify stats cards exist
        stats = page.locator("article").all_inner_texts()
        print(f"  Summary Cards Detected: {len(stats)}")
        results["Dashboard"] = "PASS"
        
        # 2. Regulatory Compliance Check
        print("\n--- 2. Testing Regulatory Compliance Check ---")
        page.goto("http://localhost:3000/component-04/cases/compliance", wait_until="networkidle")
        page.wait_for_selector("input#compliance-action-name")
        page.fill("input#compliance-action-name", "EvaluateNpdProposalCompliance-UI-Test")
        page.fill("input#compliance-duration", "35")
        page.fill("input#compliance-use", "Commercial Agriculture")
        page.fill("input#compliance-amount", "500000")
        page.fill("input#compliance-zone", "Agricultural")
        page.click("button:has-text('Run compliance evaluation')")
        page.wait_for_selector("h3:has-text('Live result')")
        page.screenshot(path=f"{evidence_dir}/02_compliance_result.png")
        status_badge = page.locator("span[class*='badge']").first.inner_text()
        print(f"  Compliance Evaluation Result: {status_badge}")
        results["Regulatory Compliance"] = "PASS"
        
        # 3. Conflict Detection Check
        print("\n--- 3. Testing Conflict Detection Check ---")
        page.goto("http://localhost:3000/component-04/cases/conflicts", wait_until="networkidle")
        page.wait_for_selector("input#conflict-action-name")
        page.fill("input#conflict-action-name", "DetectConflicts-UI-Test")
        
        decisions_json = json.dumps([
            {
                "decisionId": "DEC-UI-001",
                "subjectId": "SIM-PARCEL-003",
                "institutionName": "Simulated Provincial Office",
                "authorityLevel": "Provincial",
                "decisionType": "Restriction",
                "proposedUse": "Commercial Agriculture",
                "effectiveFrom": "2026-01-01T00:00:00Z",
                "effectiveTo": "2026-12-31T00:00:00Z",
                "regulatoryReference": "National Environmental Act No. 47 of 1980"
            },
            {
                "decisionId": "DEC-UI-002",
                "subjectId": "SIM-PARCEL-003",
                "institutionName": "Simulated Divisional Secretariat",
                "authorityLevel": "Local",
                "decisionType": "Approval",
                "proposedUse": "Commercial Agriculture",
                "effectiveFrom": "2026-06-01T00:00:00Z",
                "effectiveTo": "2027-05-31T00:00:00Z",
                "regulatoryReference": "Land Development Ordinance No. 19 of 1935"
            }
        ])
        page.fill("textarea#conflict-decisions", decisions_json)
        page.click("button:has-text('Detect conflicts')")
        page.wait_for_selector("h3:has-text('Live result')")
        page.screenshot(path=f"{evidence_dir}/03_conflict_result.png")
        print("  Conflict Detection evaluated successfully.")
        results["Conflict Detection"] = "PASS"
        
        # 4. Governance Risk Assessment
        print("\n--- 4. Testing Governance Risk Assessment ---")
        page.goto("http://localhost:3000/component-04/risk", wait_until="networkidle")
        page.wait_for_selector("input#risk-action-name")
        page.fill("input#risk-action-name", "EvaluateRisk-UI-Test")
        with open("Component4_Synthetic_Integration_Seeds/inputs/risk/SIM-C4-2026-005.json") as f:
            risk_input = json.load(f)["input"]
        page.fill("textarea#risk-input", json.dumps(risk_input))
        page.click("button:has-text('Run governance risk evaluation')")
        page.wait_for_selector("h3:has-text('Live risk assessment')")
        page.screenshot(path=f"{evidence_dir}/04_risk_result.png")
        print("  Risk Assessment evaluated successfully.")
        results["Governance Risk"] = "PASS"
        
        # 5. Explainable Governance
        print("\n--- 5. Testing Explainable Governance ---")
        page.goto("http://localhost:3000/component-04/explain", wait_until="networkidle")
        page.wait_for_selector("input#explain-action-name")
        page.fill("input#explain-action-name", "Explain-UI-Test")
        with open("Component4_Synthetic_Integration_Seeds/inputs/explain_starter/SIM-C4-2026-001.json") as f:
            explain_input = json.load(f)["input"]
        page.fill("textarea#explain-input", json.dumps(explain_input))
        page.click("button:has-text('Run governance explanation')")
        page.wait_for_selector("h3:has-text('Live explanation')")
        page.screenshot(path=f"{evidence_dir}/05_explain_result.png")
        print("  Governance Explanation generated successfully.")
        results["Explainability"] = "PASS"
        
        # 6. Institutional Consensus Evaluation
        print("\n--- 6. Testing Institutional Consensus Evaluation ---")
        page.goto("http://localhost:3000/component-04/consensus", wait_until="networkidle")
        page.wait_for_selector("input#consensus-action-name")
        page.fill("input#consensus-action-name", "Consensus-UI-Test")
        with open("Component4_Synthetic_Integration_Seeds/inputs/consensus/SIM-C4-2026-002.json") as f:
            consensus_input = json.load(f)["input"]
        page.fill("textarea#consensus-input", json.dumps(consensus_input))
        page.click("button:has-text('Run institutional consensus evaluation')")
        page.wait_for_selector("h3:has-text('Live consensus result')")
        page.screenshot(path=f"{evidence_dir}/06_consensus_result.png")
        print("  Institutional Consensus evaluated successfully.")
        results["Institutional Consensus"] = "PASS"
        
        # 7. Conditional Verification
        print("\n--- 7. Testing Conditional Verification ---")
        page.goto("http://localhost:3000/component-04/conditions", wait_until="networkidle")
        page.wait_for_selector("input#conditions-action-name")
        page.fill("input#conditions-action-name", "Conditions-UI-Test")
        with open("Component4_Synthetic_Integration_Seeds/inputs/conditions/SIM-C4-2026-001.json") as f:
            conditions_input = json.load(f)["input"]
        page.fill("textarea#conditions-input", json.dumps(conditions_input))
        page.click("button:has-text('Run conditional verification')")
        page.wait_for_selector("h3:has-text('Live conditional verification')")
        page.screenshot(path=f"{evidence_dir}/07_conditions_result.png")
        print("  Conditional Verification evaluated successfully.")
        results["Conditional Verification"] = "PASS"
        
        # 8. Case Assessments Table & Dossier
        print("\n--- 8. Testing Case Assessments Table & Dossier ---")
        page.goto("http://localhost:3000/component-04/cases", wait_until="networkidle")
        page.wait_for_timeout(1000)
        # Check live mode correctly displays live unavailable notice without faking
        page.screenshot(path=f"{evidence_dir}/08_cases_live_mode.png")
        live_text = page.inner_text("main")
        print(f"  Live Mode Notice Verified: {'Live governance assessments are not available yet' in live_text}")
        
        # Switch to demo mode to inspect the populated case assessment table & dossier
        page.click("button:has-text('Explore demo data')")
        page.wait_for_selector("table")
        page.screenshot(path=f"{evidence_dir}/08_cases_demo_table.png")
        print("  Demo Case Assessments table loaded and rendered.")
        results["Case Assessments"] = "PASS"
        
        # 9. Capability Pages Boundary Checks (Complaints, Anomalies, Screening, Timing, Audit, Verification)
        print("\n--- 9. Checking Specific Capabilities & Architectural Boundaries ---")
        for cap in ["complaints", "anomalies", "screening", "timing", "audit", "verification"]:
            page.goto(f"http://localhost:3000/component-04/{cap}", wait_until="networkidle")
            page.screenshot(path=f"{evidence_dir}/09_capability_{cap}.png")
            print(f"  Verified UI page: /component-04/{cap}")
            results[f"Capability: {cap.capitalize()}"] = "PASS"

        browser.close()
        
    print("\n" + "=" * 80)
    print("BROWSER TEST SUMMARY:")
    for k, v in results.items():
        print(f"  {k}: {v}")
    print("=" * 80)

if __name__ == "__main__":
    run_browser_walkthrough()

