# Component 2 CRIB extraction contract

Reference: CRIB MyReport Plus Reference Guide v2.0 (1 March 2024), particularly
the scoring, credit facility, repayment schedule and inquiry sections. The supplied
Consumer Credit Report Plus scan was inspected for layout only. No personal report
data is included in code, fixtures or this document.

## Meaning of extracted fields

- `CreditRiskGrade` preserves the bureau label: A1-A3, B1-B3, C1-C3, D1-D3,
  E1-E3 or XX. Legacy single-letter synthetic fixtures remain supported.
- `NormalizedCreditRiskGrade` maps the letter family to the existing **research**
  scoring bands. This is a project mapping, not a CRIB endorsement of lease
  eligibility. XX remains unknown and blocks the current scoring contract.
- `CreditScore` is an integer from 250 to 900 when available.
- `BureauProbabilityOfDefaultPercent` is a percentage in the bureau report,
  not the model's lease approval probability.
- `OutstandingBalanceLkr` is a uniquely labelled total current balance. It is
  never converted to a monthly payment.
- `ReportedMonthlyPaymentsLkr` currently accepts only an explicitly reviewed
  monthly-debt label. An OCR summary showing zero does not establish zero debt.
- `ActiveLoanObligations` is retained as a nullable compatibility field and is
  not populated by the corrected parser because its units/meaning were ambiguous.
- `DefaultHistoryIndicator` is nullable. The official report has facility statuses
  and payment history, not this boolean. An explicit reviewed/synthetic label is
  supported; absent data stays unknown. No default threshold is invented.
- Lender inquiries and self-inquiries are separate nullable counts. Active disputes
  trigger review. Unknown is distinct from zero.

## Assessment boundary

The existing command retains an explicit, verified monthly debt input. It does not
use CRIB balances, limits or aggregate historical tables as debt-service inputs.
Unknown default history, XX/unsupported grades and known active disputes block
automatic scoring. The original subgrade is included in the score explanation.
Missing optional CRIB still needs a separately approved scoring policy; this change
does not silently award credit-history points or change the scoring weights.

## Extraction and validation limitations

The parser reads labelled OCR text. It does not yet interpret arbitrary Azure table
cells, associate rotated/two-up pages, or reconstruct repayment histories. Repeated
numeric labels remain unknown rather than choosing or summing them arbitrarily.
Facility ownership, status, currency, reporting date and repayment frequency must
be reviewed before calculating monthly obligations. Guarantor liabilities must be
kept distinct from direct debt; credit card/on-demand obligations require evidence
of the applicable payment rule. Do not invent a balance percentage.

Tests use artificial labelled text. They prove parsing and scoring safeguards,
not live Azure OCR accuracy. The existing mock-document bypass remains unchanged.
The next extraction milestone needs anonymized Azure table output and manually
verified ground truth, including missing data, XX, joint/guarantor facilities,
closed facilities, disputes and non-monthly repayment schedules.
