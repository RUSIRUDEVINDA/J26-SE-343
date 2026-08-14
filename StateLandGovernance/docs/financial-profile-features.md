# Financial Profile Feature Set

This document outlines the `FinancialProfile` feature set used by the AI-Driven Lease Feasibility Assessment model to evaluate an applicant's creditworthiness and assign an A-E eligibility score. The features are modeled at a granular level, akin to the observation and evidence records found in `GovernanceRiskEvaluationInput.cs`.

## 1. Income & Employment Observations
Extracted primarily from employment and revenue documents to assess basic repayment capacity and stability.

| Field Name | Type | Source Document | Why it Matters |
| :--- | :--- | :--- | :--- |
| `ApplicantId` | `string` | All Documents | Primary identifier linking the financial data to the lease applicant. |
| `ObservationId` | `string` | System Generated | Unique identifier for this specific income observation. |
| `AverageMonthlyIncome` | `decimal` | Salary Slips, Income Records | Establishes the baseline capacity to meet regular lease payment obligations. |
| `IncomeConsistencyScore` | `decimal` | Salary Slips, Bank Statements | Measures the variance in income over time; highly volatile income increases lease default risk. |
| `EmploymentTenureMonths` | `int` | Salary Slips, HR Letters | Longer tenure indicates job stability and reliable future cash flow. |
| `BusinessRevenueGrowth` | `decimal` | Income Records (Commercial) | Assesses the financial trajectory for commercial lease applicants. |

## 2. Bank Transaction Evidence
Extracted from banking statements to evaluate liquidity, cash flow management, and spending behavior.

| Field Name | Type | Source Document | Why it Matters |
| :--- | :--- | :--- | :--- |
| `EvidenceId` | `string` | System Generated | Unique identifier for the bank transaction evidence record. |
| `AverageAccountBalance` | `decimal` | Bank Statements | Indicates liquidity and the applicant's ability to cover upfront lease deposits and initial fees. |
| `OverdraftFrequency` | `int` | Bank Statements | High frequency of overdrafts signals poor cash flow management and higher risk of missed payments. |
| `SavingsToIncomeRatio` | `decimal` | Bank Statements, Salary Slips | Reflects financial discipline and the existence of a buffer for unexpected financial shocks. |
| `Timestamp` | `DateTime` | Bank Statements | Provides temporal context to evaluate recent financial behavior versus historical trends. |

## 3. Credit Risk Observations
Extracted from national credit bureau reports to assess historical debt management and systemic financial risk.

| Field Name | Type | Source Document | Why it Matters |
| :--- | :--- | :--- | :--- |
| `ObservationId` | `string` | System Generated | Unique identifier for the credit risk observation. |
| `CreditRiskGrade` | `string` | CRIB Reports | A synthesized metric of overall creditworthiness based on nationwide credit history. |
| `ActiveLoanObligations` | `decimal` | CRIB Reports | Determines the applicant's existing debt burden, which impacts their Debt-to-Income (DTI) ratio. |
| `DefaultHistoryIndicator` | `bool` | CRIB Reports | Flags past loan defaults or non-performing assets, which strongly correlates with a D or E high-risk score. |
| `RecentCreditInquiries` | `int` | CRIB Reports | Numerous recent inquiries may indicate financial distress or an over-reliance on credit. |

## 4. Aggregated Financial Feasibility Evaluation
The composite aggregate record that feeds into the A-E classification model.

| Field Name | Type | Source Document | Why it Matters |
| :--- | :--- | :--- | :--- |
| `EvaluationId` | `string` | System Generated | Unique identifier for the overall financial profile evaluation. |
| `DebtToIncomeRatio` | `decimal` | Aggregated (Income + CRIB) | A critical standard metric; a high DTI significantly lowers the chance of lease feasibility approval. |
| `CompositeEligibilityScore` | `string` | Model Output (A-E) | The final AI-generated feasibility score indicating strong eligibility (A/B), moderate (C), or high risk (D/E). |
| `EvaluationTimestamp` | `DateTime` | System Generated | Tracks when the feasibility snapshot was calculated for audit and governance purposes. |
