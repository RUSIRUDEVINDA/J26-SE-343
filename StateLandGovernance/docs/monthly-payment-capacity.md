# Component 2 monthly payment capacity

Configured research rule: all existing monthly debt payments plus the proposed government land lease installment must be no more than 60% of verified total monthly income. This document does not establish government policy.

`maximum total monthly payments = floor(income × 0.60 to cents)`

`available lease installment = max(0, maximum total payments − existing monthly debt payments)`

Income 100,000 LKR and existing repayments 45,000 LKR leave 15,000 LKR for a monthly lease installment. Exactly 15,000 passes the capacity rule; 15,000.01 exceeds it. Passing capacity does not imply approval.

Include credit card payments, loans, finance repayments and other monthly debt commitments. Outstanding principal balances are not monthly installments. Human review must establish complete repayment amounts; unknown amounts must not be silently replaced by zero. Income must cover all verified sources without double-counting transfers or treating every bank credit as income.

The assessment command accepts `VerifiedTotalMonthlyIncomeLkr` for the reviewed all-source total. When omitted, it retains the existing salary-only input for backward compatibility. Bank and salary extraction remain prototype summary parsing; this rule does not make original-document mapping complete.

Assessments now carry `PaymentCapacity`, including the distinct rule version, budget, available installment, excess and within-limit status. Scoring weights and eligibility grades remain unchanged. Exceeding the capacity rule escalates the recommended action unless the score already recommends rejection. Existing debt above the allowance shows zero available capacity and the debt excess.

The Extracted Data and Assessment screens calculate capacity from entered fields. This UI calculation is live; the score is still a labelled static sample and is not connected to the backend handler. Capacity does not invent lease terms or automatically submit a proposal. Repository implementation and upload-to-review integration are still outstanding.
