# Synthetic financial-document test pack

12 paired cases; each bank PDF has six monthly pages (Apr-Sep 2026), each CRIB-style PDF has three pages. S07/S08 also include image-only scanned PDFs. Institutions, names, IDs and references are fictional. Do not use these files as financial evidence.

## Use
1. Select a scenario folder and upload bank_statement.pdf and crib_style_report.pdf through the real document/OCR route.
2. For OCR-only tests use the *_scanned.pdf versions in S07/S08. These intentionally have no searchable text layer.
3. Compare extracted transactions, balances, identities, facility details, installment amounts, arrears and disputes with expected.json.
4. Treat verified_income_assumption_lkr as tester-supplied ground truth after classification/verification, not a number explicitly printed in the bank PDF. Lease requests exist in expected.json, not in the financial documents.
5. Read all pages: a second active facility is on CRIB page 3. Reading only the first two pages produces an incomplete debt total.

## Financial interpretation
Credits are not automatically income. Transfers, refunds and loan proceeds are excluded. Debits are not automatically expenses: debt repayment and cash withdrawal have separate classes. Variable business receipts are reduced by 15,000 monthly explicitly identified business supplies; other costs need human review. Principal outstanding is never used as monthly installment. Closed settled facilities contribute zero. Missing installments remain null. Disputed liabilities and identity mismatches require verification.

The current project research rule is floor(income * 0.60 to cents). Capacity compares existing verified monthly debt plus requested lease against that cap. This is not government policy, a bureau score, an approval prediction, or a production acceptance decision. S08 demonstrates that passing capacity does not clear adverse credit history. S11 requires an invalid-income outcome rather than a capacity call.

## Files
manifest.json: full ground truth; scenario_matrix.csv: compact summary; Sxx/expected.json: one case including every transaction and facility; TEST_GUIDE.pdf: readable case matrix; generate.py: deterministic generator. Re-run `python3 generate.py` with reportlab, pypdf, pypdfium2 and Pillow installed. It overwrites this generated pack and rebuilds the ZIP.

The generator validates all bank arithmetic, page counts, fixture identity and installment boundaries, and scanned text-layer absence. validation.json records the checks. This does not establish that the project parser or Azure OCR handles the files successfully; run those integration tests separately. Generated documents use English labels and fictional layouts inspired by the supplied samples; they do not reproduce official branding, multilingual headers or the complete bureau schema.
