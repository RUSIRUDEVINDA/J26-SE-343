import csv
import random
import uuid
import os
from faker import Faker

fake = Faker()

def get_credit_grade_and_defaults():
    """Returns a synthetic credit grade and default history boolean based on reasonable distributions."""
    val = random.random()
    if val < 0.2:
        return 'A', False
    elif val < 0.45:
        return 'B', False
    elif val < 0.7:
        return 'C', random.random() < 0.1 # 10% chance of default for C
    elif val < 0.85:
        return 'D', random.random() < 0.4 # 40% chance of default for D
    else:
        return 'E', random.random() < 0.8 # 80% chance of default for E

def generate_data(num_rows):
    data = []
    grade_counts = {'A': 0, 'B': 0, 'C': 0, 'D': 0, 'E': 0}
    approval_counts = {True: 0, False: 0}

    land_use_types = ['Agricultural', 'Commercial', 'Industrial', 'Residential', 'Tourism', 'Conservation']
    durations = [5, 10, 20, 33, 50, 99]

    for _ in range(num_rows):
        app_id = str(uuid.uuid4())
        metadata_tag = "[SYNTHETIC]"
        age = random.randint(21, 70)
        
        # Ensure string attributes are prepended with [SYNTHETIC] to follow conventions
        occupation = f"[SYNTHETIC] {fake.job()}"
        region = f"[SYNTHETIC] {fake.city()}"
        
        grade, default_history = get_credit_grade_and_defaults()
        
        # Build financial profiles that logically correlate with the assigned grade and approval outcome
        if grade in ['A', 'B']:
            income = round(random.uniform(100000, 1500000), 2)
            consistency = round(random.uniform(0.75, 1.0), 2)
            tenure = random.randint(36, 300)
            balance = round(income * random.uniform(2.0, 10.0), 2)
            overdrafts = random.randint(0, 1)
            loans = round(income * random.uniform(0.0, 0.3), 2)
            # A/B grades have high approval rates
            is_approved = random.random() < 0.95
            
        elif grade == 'C':
            income = round(random.uniform(50000, 600000), 2)
            consistency = round(random.uniform(0.5, 0.8), 2)
            tenure = random.randint(12, 120)
            balance = round(income * random.uniform(0.5, 3.0), 2)
            overdrafts = random.randint(0, 4)
            loans = round(income * random.uniform(0.2, 0.5), 2)
            # C grades have mixed approval rates
            is_approved = random.random() < 0.60
            
        else: # D, E
            income = round(random.uniform(25000, 250000), 2)
            consistency = round(random.uniform(0.2, 0.6), 2)
            tenure = random.randint(1, 48)
            balance = round(income * random.uniform(0.0, 1.0), 2)
            overdrafts = random.randint(2, 12)
            loans = round(income * random.uniform(0.4, 0.8), 2)
            # D/E grades are mostly rejected
            is_approved = random.random() < 0.10

        savings_ratio = round(balance / income, 2) if income > 0 else 0
        area = round(random.uniform(0.1, 25.0), 2)
        duration = random.choice(durations)
        land_use = f"[SYNTHETIC] {random.choice(land_use_types)}"

        row = [
            app_id, metadata_tag, age, occupation, region,
            income, consistency, tenure, balance, overdrafts, savings_ratio,
            grade, loans, default_history, area, duration, land_use, is_approved
        ]
        data.append(row)
        
        grade_counts[grade] += 1
        approval_counts[is_approved] += 1

    return data, grade_counts, approval_counts

def main():
    headers = [
        "ApplicationId", "Metadata_Tag", "Applicant_Age", "Applicant_Occupation",
        "Applicant_Region", "Fin_AvgMonthlyIncome", "Fin_IncomeConsistency",
        "Fin_EmploymentTenure", "Fin_AvgAccountBalance", "Fin_OverdraftFrequency",
        "Fin_SavingsToIncome", "Fin_CreditRiskGrade", "Fin_ActiveLoanObligations",
        "Fin_DefaultHistory", "Lease_RequestedArea", "Lease_RequestedDuration",
        "Lease_LandUseType", "Outcome_IsApproved"
    ]

    # Ensure target directory exists
    output_dir = os.path.dirname(os.path.abspath(__file__))
    os.makedirs(output_dir, exist_ok=True)
    out_file = os.path.join(output_dir, 'synthetic_applicants.csv')

    num_rows = 1000
    print(f"Generating {num_rows} synthetic applicant rows...")
    data, grades, approvals = generate_data(num_rows)

    with open(out_file, 'w', newline='', encoding='utf-8') as f:
        writer = csv.writer(f)
        writer.writerow(headers)
        writer.writerows(data)

    print(f"\n[+] Data successfully written to: {out_file}")
    
    print("\n--- Label-Balance Sanity Check ---")
    print("Grade Distribution:")
    for g, count in sorted(grades.items()):
        print(f"  {g}: {count} ({count/num_rows*100:.1f}%)")
    
    print("\nHistorical Outcome Split:")
    for outcome, count in approvals.items():
        label = "Approved" if outcome else "Rejected"
        print(f"  {label}: {count} ({count/num_rows*100:.1f}%)")

if __name__ == '__main__':
    main()
