# Synthetic Dataset Schema: Lease Feasibility

This document outlines the schema for generating synthetic historical lease applications and financial profiles. In alignment with the project's data conventions (as seen in `LandIntelligenceSeedData.cs`), all mocked data records or descriptions must be clearly marked with the `[SYNTHETIC]` tag to prevent confusion with real government or citizen data.

## Schema Definition

The synthetic dataset combines applicant demographics, financial profile features, lease request terms, and historical outcomes into a single flat structure (e.g., for CSV export/import and machine learning model training).

| Column Name | Data Type | Description | Example Value |
| :--- | :--- | :--- | :--- |
| `ApplicationId` | `Guid` | Unique identifier for the application. | `550e8400-e29b-41d4-a716-446655440000` |
| `Metadata_Tag` | `String` | Required flag indicating mock data. | `[SYNTHETIC] Test Record` |
| `Applicant_Age` | `Integer` | Demographic: Applicant's age. | `34` |
| `Applicant_Occupation` | `String` | Demographic: Job sector or title. | `[SYNTHETIC] Commercial Farmer` |
| `Applicant_Region` | `String` | Demographic: Province or district. | `[SYNTHETIC] Western Province` |
| `Fin_AvgMonthlyIncome` | `Decimal` | Profile: Average monthly income (LKR). | `150000.00` |
| `Fin_IncomeConsistency` | `Decimal` | Profile: Score from 0.0 to 1.0. | `0.85` |
| `Fin_EmploymentTenure` | `Integer` | Profile: Months in current employment. | `48` |
| `Fin_AvgAccountBalance` | `Decimal` | Profile: 6-month average balance. | `500000.00` |
| `Fin_OverdraftFrequency` | `Integer` | Profile: Overdrafts in last 12 months. | `1` |
| `Fin_SavingsToIncome` | `Decimal` | Profile: Savings buffer ratio. | `3.33` |
| `Fin_CreditRiskGrade` | `String` | Profile: CRIB synthesized grade (A-E). | `B` |
| `Fin_ActiveLoanObligations`| `Decimal` | Profile: Total active debt monthly payments.| `25000.00` |
| `Fin_DefaultHistory` | `Boolean` | Profile: Has past loan defaults? | `False` |
| `Lease_RequestedArea` | `Decimal` | Terms: Size of land requested (Hectares). | `2.5` |
| `Lease_RequestedDuration` | `Integer` | Terms: Lease duration in years. | `33` |
| `Lease_LandUseType` | `String` | Terms: Intended use (e.g., Agricultural). | `[SYNTHETIC] Agricultural` |
| `Outcome_IsApproved` | `Boolean` | Target: Historical decision outcome. | `True` |

## CSV Format Example

Below is a snippet demonstrating how the synthetic CSV dataset should be structured and populated:

```csv
ApplicationId,Metadata_Tag,Applicant_Age,Applicant_Occupation,Applicant_Region,Fin_AvgMonthlyIncome,Fin_IncomeConsistency,Fin_EmploymentTenure,Fin_AvgAccountBalance,Fin_OverdraftFrequency,Fin_SavingsToIncome,Fin_CreditRiskGrade,Fin_ActiveLoanObligations,Fin_DefaultHistory,Lease_RequestedArea,Lease_RequestedDuration,Lease_LandUseType,Outcome_IsApproved
10000001-0000-0000-0000-000000000000,"[SYNTHETIC]",45,"[SYNTHETIC] Hotel Owner","[SYNTHETIC] Southern",850000.00,0.92,120,4500000.00,0,5.29,"A",150000.00,False,1.5,50,"[SYNTHETIC] Tourism",True
10000002-0000-0000-0000-000000000000,"[SYNTHETIC]",28,"[SYNTHETIC] Retail Trader","[SYNTHETIC] Central",90000.00,0.60,14,50000.00,4,0.55,"D",45000.00,True,0.2,10,"[SYNTHETIC] Commercial",False
```

## Usage Guidelines
1. **Model Training:** This synthetic schema is primarily used to train the Predictive Lease Approval Model and validate the Financial Feasibility (A-E) scoring logic.
2. **Safety:** The `Metadata_Tag` and `[SYNTHETIC]` prefixes on string values guarantee that if test data accidentally bleeds into reporting or production, it is immediately identifiable.
