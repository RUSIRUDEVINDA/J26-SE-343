# Financial Feasibility Grading Rubric

Based on the configurable, weighted-scoring pattern established by `GovernanceRiskEngineOptions`, this document defines the scoring mechanics, criteria weights, and grading bands for the AI-Driven Lease Feasibility Assessment model.

## 1. Configurable Options Pattern

The feasibility scoring engine uses an options record to inject configurable thresholds and weights, ensuring the scoring logic remains deterministic, testable, and easily adjustable without changing core business logic.

```csharp
/// <summary>
/// Configurable scoring options for the Financial Feasibility Assessment Engine.
/// </summary>
public sealed record FinancialFeasibilityEngineOptions(
    decimal MaximumAcceptableDti = 0.50m,
    int DebtToIncomeWeight = 35,
    int IncomeConsistencyWeight = 25,
    int LiquidityBufferWeight = 20,
    int CreditHistoryWeight = 20,
    int DefaultHistoryPenalty = -50,
    int FrequentOverdraftPenalty = -15,
    int OverdraftThresholdCount = 3
)
{
    public static FinancialFeasibilityEngineOptions Default { get; } = new();
}
```

## 2. A-E Grading Bands

The total base score adds up to 100 points, with penalties applied for significant negative indicators (e.g., past defaults). The final numeric score is mapped to the A-E classification system as required by the TAF.

| Grade | Score Range | Feasibility Status | Action |
| :--- | :--- | :--- | :--- |
| **A** | 85 - 100 | Strong Eligibility | Fast-track for automated generative proposal creation. |
| **B** | 70 - 84 | Strong Eligibility | Proceed with generative proposal creation. |
| **C** | 50 - 69 | Moderate Risk | Requires manual review or conditional lease terms (e.g., higher deposit). |
| **D** | 30 - 49 | High Risk | Flag for escalation; likely rejection unless strong mitigations exist. |
| **E** | < 30 | High Risk | Immediate rejection; significant financial distress or severe default history. |

## 3. Per-Criterion Weights

The system evaluates the `FinancialProfile` evidence records against the following criteria to build the score:

### Positive Score Contributions (Max 100)
- **Debt-to-Income (DTI) Ratio (Up to 35 pts):** 
  - `< 20%` = 35 pts
  - `20% - 35%` = 25 pts
  - `36% - 50%` = 10 pts
  - `> 50%` = 0 pts
- **Income Consistency (Up to 25 pts):** Based on the `IncomeConsistencyScore` over 12 months. Highly stable income yields full points.
- **Liquidity / Savings Buffer (Up to 20 pts):** Evaluates if the average bank balance can cover 3-6 months of lease payments.
- **Credit Risk Grade (Up to 20 pts):** Based on CRIB reports. 
  - `Excellent` = 20 pts
  - `Good` = 15 pts
  - `Fair` = 5 pts

### Negative Penalties
- **Default History Penalty (-50 pts):** Applied if a `DefaultHistoryIndicator` is true.
- **Frequent Overdraft Penalty (-15 pts):** Applied if `OverdraftFrequency` exceeds the `OverdraftThresholdCount`.

---

## 4. Worked Numeric Example

**Applicant Profile:**
- DTI Ratio: 28% (Yields **25 points**)
- Income Consistency: Stable salaried employee (Yields **25 points**)
- Liquidity Buffer: Covers 4 months of payments (Yields **15 points**)
- Credit Grade: Good (Yields **15 points**)
- Penalties: 4 overdrafts in 6 months (Exceeds threshold of 3, applies **-15 points** penalty). No defaults.

**Score Calculation:**
`25 (DTI) + 25 (Income) + 15 (Liquidity) + 15 (Credit) - 15 (Overdraft Penalty) = 65 points`

**Final Grade:**
A score of **65** falls into the **C (Moderate Risk)** band. 

*Conclusion:* The applicant has steady income and manageable debt but struggles slightly with cash flow (indicated by overdrafts). The application is flagged for manual review or may require a larger upfront deposit to offset the moderate liquidity risk.
