# Component 2 Deterministic Scoring Input

The deterministic scorer accepts `FinancialFeasibilityScoringInput`. Its property names include their units so the engine does not have to infer whether a monetary value is monthly, total, or a ratio.

| Property | Type | Unit / allowed value | Scoring role |
| :--- | :--- | :--- | :--- |
| `ApplicationId` | `string` | Lease application identifier | Persisted on the assessment. |
| `ApplicantId` | `string` | Applicant identifier | Persisted separately from the application ID. |
| `AverageMonthlyIncomeLkr` | `decimal` | LKR/month, greater than zero | Debt-service ratio denominator. |
| `IncomeConsistencyRatio` | `decimal` | 0-1 | Multiplied by the 25-point weight. |
| `RequestedMonthlyLeasePaymentLkr` | `decimal` | LKR/month, greater than zero | Used directly in debt-service and liquidity calculations. |
| `MonthlyDebtObligationsLkr` | `decimal` | LKR/month, zero or greater | Used directly; no percentage-of-total-debt conversion is allowed. |
| `AverageAccountBalanceLkr` | `decimal` | LKR, zero or greater | Liquidity-buffer numerator. |
| `OverdraftCountInEvidenceWindow` | `int` | Count, zero or greater; V1 window is 6 months | Applies the approved frequent-overdraft penalty. |
| `CreditRiskGrade` | `CreditRiskGrade` | A, B, C, D, or E | Approved credit-history points. |
| `HasDefaultHistory` | `bool` | true/false | Applies the approved default-history penalty. |

## Explicit non-inputs for V1 deterministic scoring

The following extracted values may remain available as evidence or for future separately governed models, but they do not affect `component-2-financial-feasibility-v1`:

- Savings-to-income ratio
- Total active debt without a verified monthly repayment amount
- Recent credit inquiries
- Employment type and tenure

Adding any of these to the deterministic score requires a new reviewed contract version, documentation, and boundary tests.
