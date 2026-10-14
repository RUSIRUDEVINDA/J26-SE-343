# Component 2 Deterministic Financial Feasibility Contract

`LeaseFeasibilityScoringContract.Component2V1` is the single executable contract for the deterministic Component 2 score. The engine must not introduce thresholds, conversions, weights, penalties, or actions outside this contract.

Contract version: `component-2-financial-feasibility-v1`

## Input units and formulae

| Input | Unit | Rule |
| :--- | :--- | :--- |
| Average monthly income | LKR/month | Must be greater than zero. |
| Requested monthly lease payment | LKR/month | Must be greater than zero. This is supplied explicitly; savings are not used as a proxy. |
| Monthly debt obligations | LKR/month | Must be zero or greater. It must be obtained from an approved monthly repayment source. Total debt is not converted to a monthly amount. |
| Income consistency | Ratio from 0 to 1 | Supplied by an approved upstream calculation; the deterministic engine does not synthesize it. |
| Average account balance | LKR | Must be zero or greater. |
| Overdraft count | Count in the latest 6-month evidence window | A penalty applies only when the count exceeds 3. |
| Credit risk grade | Enum A-E | Normalized from the CRIB result before scoring. |
| Default history | Boolean | Applies the approved default penalty when true. |

The debt-service ratio is:

`(monthly debt obligations + requested monthly lease payment) / average monthly income`

The liquidity buffer is:

`average account balance / requested monthly lease payment`

## Positive score: 100 points maximum

### Debt-service ratio: 35 points

| Ratio | Points |
| :--- | ---: |
| `<= 20%` | 35 |
| `> 20%` and `<= 35%` | 25 |
| `> 35%` and `<= 50%` | 10 |
| `> 50%` | 0 |

### Income consistency: 25 points

`income consistency ratio x 25`, rounded to two decimal places using midpoint-away-from-zero rounding.

### Liquidity buffer: 20 points

| Requested lease-payment coverage | Points |
| :--- | ---: |
| `>= 6 months` | 20 |
| `>= 3 months` and `< 6 months` | 15 |
| `>= 1 month` and `< 3 months` | 5 |
| `< 1 month` | 0 |

### Credit history: 20 points

| CRIB grade | Points |
| :--- | ---: |
| A | 20 |
| B | 15 |
| C | 5 |
| D or E | 0 |

## Approved penalties

| Indicator | Penalty |
| :--- | ---: |
| Default history is true | -50 |
| More than 3 overdrafts in the latest 6 months | -15 |

The final score is clamped to 0-100. Recent credit inquiries, employment tenure, employment type, and savings-to-income ratio do not change this deterministic score because Component 2 V1 does not approve weights or penalties for them.

## Grades and actions

| Grade | Score | Deterministic action |
| :--- | :--- | :--- |
| A | 85-100 | `FastTrack` |
| B | 70-84.99 | `Proceed` |
| C | 50-69.99 | `ManualReview` |
| D | 30-49.99 | `Escalate` |
| E | 0-29.99 | `Reject` |

## Worked example

Inputs:

- Average monthly income: LKR 100,000
- Monthly debt obligations: LKR 18,000
- Requested monthly lease payment: LKR 10,000
- Income consistency ratio: 1.00
- Average account balance: LKR 40,000
- CRIB grade: B
- Four overdrafts in the latest six months
- No default history

Calculation:

- Debt-service ratio: `(18,000 + 10,000) / 100,000 = 28%` -> 25 points
- Income consistency: `1.00 x 25` -> 25 points
- Liquidity buffer: `40,000 / 10,000 = 4 months` -> 15 points
- Credit grade B -> 15 points
- Four overdrafts -> -15 points
- Total: `25 + 25 + 15 + 15 - 15 = 65`

Result: grade C, action `ManualReview`.

## Identity and time rules

- `ApplicationId` identifies the lease application.
- `ApplicantId` identifies the person or organization applying and must never be substituted for `ApplicationId`.
- The application layer obtains one `DateTimeOffset` from the injected `TimeProvider`. The engine passes that timestamp to the assessment, which stores it in UTC.
