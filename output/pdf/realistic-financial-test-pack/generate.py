"""Deterministic synthetic fixtures. Requires reportlab, pypdf, pypdfium2, Pillow.
Run: python3 realistic_layout.py. No original customer data is used.
"""
from pathlib import Path
from decimal import Decimal, ROUND_FLOOR
import calendar, json, csv, zipfile, io
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image, ImageEnhance, ImageFilter, ImageOps, ImageDraw

ROOT = Path(__file__).resolve().parent
D = lambda n: Decimal(str(n))
money = lambda n: f'{D(n):,.2f}'
CASES = [
 ('S01','Stable salary, low debt',[150000]*6,20000,25000,'clean'),
 ('S02','Multiple verified income sources',[120000]*6,30000,35000,'multi'),
 ('S03','Variable self-employed income',[80000,140000,60000,180000,90000,110000],25000,30000,'variable'),
 ('S04','Existing debt exceeds capacity',[100000]*6,70000,10000,'high_debt'),
 ('S05','Exactly at the 60 percent boundary',[100000]*6,45000,15000,'clean'),
 ('S06','One cent above the boundary',[100000]*6,45000,15000.01,'clean'),
 ('S07','Transfers and reversals are not income',[80000]*6,15000,20000,'transfers'),
 ('S08','Current arrears and adverse history',[180000]*6,35000,25000,'arrears'),
 ('S09','Disputed facility with uncertain liability',[140000]*6,25000,30000,'dispute'),
 ('S10','Monthly installment missing',[130000]*6,30000,25000,'missing'),
 ('S11','No qualifying income; savings and loan proceeds',[0]*6,10000,5000,'zero'),
 ('S12','Applicant identity mismatch',[160000]*6,20000,30000,'identity'),
]
BLUE=HexColor('#173c56'); PALE=HexColor('#f2f6f8'); ORANGE=HexColor('#c55a32')

def page(c, title, case, sub, number, total):
 c.setFillColor(BLUE); c.rect(0,790,595,52,fill=1,stroke=0)
 c.setFillColor(HexColor('#ffffff')); c.setFont('Helvetica-Bold',17); c.drawString(30,815,title)
 c.setFont('Helvetica',9); c.drawString(30,800,sub)
 c.setFillColor(ORANGE); c.setFont('Helvetica-Bold',10); c.drawString(30,774,'SYNTHETIC TEST DOCUMENT - NOT VALID FOR FINANCIAL USE')
 c.saveState(); c.translate(297,400); c.rotate(40); c.setFillColor(HexColor('#e8edf0')); c.setFont('Helvetica-Bold',43); c.drawCentredString(0,0,'SYNTHETIC / TEST ONLY'); c.restoreState()
 c.setFillColor(BLUE); c.setFont('Helvetica',8); c.drawString(30,24,f'{case} | Fictional data | LKR | Generated 01-Oct-2026'); c.drawRightString(565,24,f'Page {number} / {total}')

def line(c, y, text, bold=False, size=10):
 c.setFillColor(BLUE); c.setFont('Helvetica-Bold' if bold else 'Helvetica',size); c.drawString(30,y,text)

def table(c,y,headers,rows,widths,size=8):
 x0=30; h=25
 for idx,row in enumerate([headers]+rows):
  c.setFillColor(BLUE if idx==0 else (PALE if idx%2 else HexColor('#ffffff'))); c.rect(x0,y-h,sum(widths),h,fill=1,stroke=0)
  x=x0; c.setFont('Helvetica-Bold' if idx==0 else 'Helvetica',size); c.setFillColor(HexColor('#ffffff') if idx==0 else BLUE)
  for cell,w in zip(row,widths):
   text=str(cell)
   if c.stringWidth(text,c._fontname,size)>w-10: raise ValueError(f'Cell too wide: {text}')
   c.drawString(x+5,y-16,text); x+=w
  y-=h
 return y

def transactions(case, month, income, debt, kind):
 rows=[]
 def add(day,label,debit=0,credit=0,classification='expense'):
  rows.append(dict(date=f'2026-{month:02d}-{day:02d}',particulars=label,debit_lkr=money(debit),credit_lkr=money(credit),classification=classification))
 if kind=='multi':
  add(2,'SALARY - FICTIONAL EMPLOYER',credit=80000,classification='qualifying_income'); add(5,'RENT - TEST TENANCY',credit=25000,classification='qualifying_income'); add(8,'FREELANCE - TEST INVOICE',credit=15000,classification='qualifying_income')
 elif kind=='variable':
  add(2,'CUSTOMER RECEIPTS - TEST A',credit=D(income)*D('.6'),classification='business_receipts'); add(8,'CUSTOMER RECEIPTS - TEST B',credit=D(income)*D('.4'),classification='business_receipts')
 elif income:
  add(2,'SALARY - FICTIONAL EMPLOYER',credit=income,classification='qualifying_income')
 if kind in ('transfers','zero'):
  add(10,'OWN ACCOUNT TRANSFER IN',credit=90000,classification='internal_transfer'); add(11,'ECOM TEST PURCHASE',debit=2000); add(12,'ECOM REVERSAL',credit=2000,classification='reversal'); add(13,'LOAN DISBURSEMENT TEST',credit=150000,classification='loan_proceeds')
 add(15,'AUTOPAY - TEST LOAN A',debit=D(debt)*D('.6'),classification='debt_payment'); add(17,'AUTOPAY - TEST LOAN B',debit=D(debt)*D('.4'),classification='debt_payment')
 add(19,'GROCERIES TEST SHOP',debit=6500); add(20,'UTILITIES TEST BILL',debit=4500)
 if kind=='variable': add(22,'BUSINESS SUPPLIES TEST',debit=15000,classification='business_expense')
 add(24,'CASH WITHDRAWAL',debit=3000,classification='cash_withdrawal'); add(26,'BANK SERVICE FEE',debit=250)
 return rows

def bank_pdf(case,name,incomes,debt,kind):
 dest=ROOT/case/'bank_statement.pdf'; c=canvas.Canvas(str(dest),pagesize=A4); c.setTitle(f'{case} Synthetic Bank Statement')
 balance=D(1000000); months=[]
 for p,(month,inc) in enumerate(zip(range(4,10),incomes),1):
  page(c,'TEST HORIZON BANK','%s'%case,'Statement of account | Fictional institution | No official logo',p,6)
  line(c,748,f'Account holder: TEST APPLICANT {case}'); line(c,730,f'Customer ID: TEST-ID-{case}    Account: TEST-ACCT-{case}')
  line(c,712,'Address: 10 Example Lane, Test City, Sri Lanka')
  line(c,689,f'Period: 01-{month:02d}-2026 to {calendar.monthrange(2026,month)[1]}-{month:02d}-2026    Currency: LKR',True)
  opening=balance; rows=transactions(case,month,inc,debt,kind); display=[[f'01-{month:02d}-2026','OPENING BALANCE','','',money(opening)+' Cr']]
  credits=debits=D(0)
  for r in rows:
   dr=D(r['debit_lkr'].replace(',','')); cr=D(r['credit_lkr'].replace(',','')); balance+=cr-dr; credits+=cr; debits+=dr; r['balance_lkr']=str(balance)
   display.append([r['date'],r['particulars'],money(dr) if dr else '',money(cr) if cr else '',money(balance)+' Cr'])
  y=table(c,660,['DATE','PARTICULARS','DEBITS','CREDITS','BALANCE'],display,[68,212,77,77,101],7)
  y-=22; line(c,y,f'Total debits: {money(debits)}    Total credits: {money(credits)}',True); line(c,y-20,f'Closing balance: {money(balance)} Cr',True)
  line(c,y-46,'Balance carried forward to the next statement period.',size=9)
  months.append(dict(month=f'2026-{month:02d}',opening_lkr=str(opening),credits_lkr=str(credits),debits_lkr=str(debits),closing_lkr=str(balance),transactions=rows))
  assert opening+credits-debits==balance
  c.showPage()
 c.save(); return months

def crib_pdf(case,income,debt,kind):
 dest=ROOT/case/'crib_style_report.pdf'; c=canvas.Canvas(str(dest),pagesize=A4); c.setTitle(f'{case} Synthetic CRIB-style report')
 ident=f'TEST-ID-{case}' if kind!='identity' else 'TEST-ID-OTHER-PERSON'
 facilities=[]
 for i,ratio in enumerate([D('.6'),D('.4')],1):
  installment=D(debt)*ratio
  facilities.append(dict(facility_id=f'TEST-FAC-{case}-{i}',status='Active',outstanding_lkr=str(installment*24),monthly_installment_lkr=None if kind=='missing' and i==2 else str(installment),arrears_lkr='45000' if kind=='arrears' and i==1 else '0',days_in_arrears=95 if kind=='arrears' and i==1 else 0,disputed=kind=='dispute' and i==2))
 total=sum(D(f['outstanding_lkr']) for f in facilities); monthly=None if kind=='missing' else D(debt)
 page(c,'TEST CREDIT BUREAU',case,'CRIB-style consumer credit report | Fictional bureau',1,3)
 line(c,747,f'Reference: TEST-REPORT-{case}    Report created: 01-Oct-2026',True)
 line(c,726,f'Searched name: TEST APPLICANT {case if kind!="identity" else "OTHER PERSON"}')
 line(c,705,f'Searched ID: {ident}'); line(c,684,'Dashboard',True,13)
 table(c,669,['PAYMENT PROFILE STATUS','VALUE'],[
  ['Open credit facilities','2'],['Closed credit facilities','1'],['Number of different creditors','2'],['Total current balance (principal)',money(total)],['Total monthly installment amount','Not reported' if monthly is None else money(monthly)],['Total amount in arrears',money(45000 if kind=='arrears' else 0)],['Highest current NDIA (days)','95' if kind=='arrears' else '0'],['Highest NDIA in last 12 months','120' if kind=='arrears' else '0'],['Active disputes','1' if kind=='dispute' else '0'],['Bureau score / risk grade','Not assigned in this synthetic fixture']], [345,190],9)
 line(c,370,'Payment profile - last six months',True,12)
 table(c,351,['FACILITY','APR','MAY','JUN','JUL','AUG','SEP'],[
  ['TEST LOAN A']+(['0','0','30','60','90','95'] if kind=='arrears' else ['0']*6),['TEST LOAN B']+['0']*6],[169]+[61]*6,8)
 line(c,246,'Legend: 0 = current; positive values = days in arrears.',size=9)
 line(c,221,'Dispute status is recorded separately from payment history.',size=9)
 line(c,194,'No real bureau score, risk grade, or approval outcome is claimed.',size=9)
 c.showPage()
 page(c,'TEST CREDIT BUREAU',case,'Subject information and first credit facility',2,3)
 line(c,747,'Subject information',True,13)
 table(c,730,['FIELD','VALUE'],[['Name',f'TEST APPLICANT {case if kind!="identity" else "OTHER PERSON"}'],['Identity',ident],['Address','10 Example Lane, Test City'],['Record purpose','Synthetic testing only']],[180,355],10)
 def facility(y,f):
  line(c,y,f'Credit facility: {f["facility_id"]}',True,12)
  return table(c,y-18,['FIELD','VALUE'],[['Reporting institution','TEST LENDER A' if f==facilities[0] else 'TEST LENDER B'],['Facility type','Personal loan'],['Ownership','Individual borrower'],['Status',f['status']],['Currency','LKR'],['Current balance / outstanding principal',money(f['outstanding_lkr'])],['Monthly installment amount','Not reported' if f['monthly_installment_lkr'] is None else money(f['monthly_installment_lkr'])],['Amount in arrears',money(f['arrears_lkr'])],['Current NDIA (days)',str(f['days_in_arrears'])],['Dispute status','Active - liability under review' if f['disputed'] else 'No active dispute']],[290,245],9)
 facility(590,facilities[0]); c.showPage()
 page(c,'TEST CREDIT BUREAU',case,'Remaining facilities and credit history',3,3)
 y=facility(744,facilities[1]); line(c,y-30,'Closed facility',True,12)
 table(c,y-48,['FACILITY','STATUS','PRINCIPAL','INSTALLMENT'],[['TEST CLOSED LOAN','Closed / settled','0.00','0.00']],[190,155,95,95],8)
 line(c,y-106,'Historical events and record notes',True,12)
 line(c,y-127,'Historical default: '+('Recorded 120-day delinquency in Dec-2025.' if kind=='arrears' else 'None recorded in this fixture.'),size=9)
 line(c,y-146,'Dishonoured cheques: '+('1 synthetic event.' if kind=='arrears' else '0 recorded.'),size=9)
 line(c,y-165,'Utilities / insurance accounts: Not supplied in this fixture.',size=9)
 line(c,y-184,'Inquiries in six months: 2 synthetic lender inquiries.',size=9)
 line(c,y-212,'End of report - all 3 pages required for complete facility extraction.',True,9)
 c.showPage(); c.save(); return facilities,ident

def scan(source,dest):
 doc=pdfium.PdfDocument(source); pages=[]
 for p in doc:
  im=p.render(scale=1.8).to_pil().convert('RGB')
  im=ImageEnhance.Contrast(im).enhance(.82).rotate(.45,resample=Image.Resampling.BICUBIC,expand=False,fillcolor='white').filter(ImageFilter.GaussianBlur(.25))
  pages.append(im)
 pages[0].save(dest,'PDF',save_all=True,append_images=pages[1:],resolution=129.6)

def main():
 expected=[]
 for case,name,incomes,debt,lease,kind in CASES:
  (ROOT/case).mkdir(exist_ok=True)
  months=bank_pdf(case,name,incomes,debt,kind); facilities,ident=crib_pdf(case,incomes,debt,kind)
  # Variable-business case has 15,000 monthly business costs; cash withdrawals
  # are not automatically classified as an additional business expense.
  monthly=[max(D(0),D(v)-(D(15000) if kind=='variable' else D(0))) for v in incomes]
  income=sum(monthly)/6; limit=(income*D('.6')).quantize(D('.01'),rounding=ROUND_FLOOR)
  missing=kind=='missing'; review=kind in ['variable','arrears','dispute','missing','zero','identity']
  computable=income>0 and not missing and kind!='identity' and kind!='dispute'
  capacity=(D(debt)+D(lease)<=limit) if computable else None
  notes={'variable':'Net business income assumes only the explicitly identified business costs; require income verification.','arrears':'Capacity passes but current arrears and adverse history require separate credit-risk review.','dispute':'Do not treat disputed liability as resolved; numeric capacity is withheld pending verification.','missing':'Bank autopay is corroboration only; do not fill the unreported CRIB installment silently.','zero':'No qualifying income. Capacity constructor rejects income <= 0; do not return a positive capacity decision.','identity':'Bank and bureau identities differ. Stop cross-document aggregation until resolved.','transfers':'Exclude own-account transfers, loan disbursements and purchase reversals from qualifying income.'}.get(kind,'Capacity outcome is an arithmetic research-rule result, not a loan or land-lease approval.')
  e=dict(scenario_id=case,scenario=name,currency='LKR',bank_identity=f'TEST-ID-{case}',crib_identity=ident,identity_matches=kind!='identity',verified_income_assumption_lkr=str(income),monthly_qualifying_income_assumptions_lkr=[str(v) for v in monthly],document_reported_monthly_debt_lkr=None if missing else str(D(debt)),generator_hidden_monthly_debt_lkr=str(D(debt)),requested_monthly_lease_lkr=str(D(lease)),research_rule='component-2-monthly-payment-capacity-60-v1',maximum_total_payments_lkr=str(limit) if income>0 else None,available_lease_lkr=str(max(D(0),limit-D(debt))) if computable else None,is_within_limit=capacity,requires_manual_review=review,expected_action='manual_review' if review else ('capacity_pass' if capacity else 'capacity_exceeded'),notes=notes,facilities=facilities,bank_months=months)
  (ROOT/case/'expected.json').write_text(json.dumps(e,indent=2)); expected.append(e)
 for case in ['S07','S08']:
  for stem in ['bank_statement','crib_style_report']: scan(str(ROOT/case/(stem+'.pdf')),str(ROOT/case/(stem+'_scanned.pdf')))
 (ROOT/'manifest.json').write_text(json.dumps(expected,indent=2))
 fields=['scenario_id','scenario','verified_income_assumption_lkr','document_reported_monthly_debt_lkr','requested_monthly_lease_lkr','is_within_limit','requires_manual_review','expected_action']
 with (ROOT/'scenario_matrix.csv').open('w',newline='') as f:
  writer=csv.DictWriter(f,fields,extrasaction='ignore'); writer.writeheader(); writer.writerows(expected)
 readme='''# Synthetic financial-document test pack

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
manifest.json: full ground truth; scenario_matrix.csv: compact summary; Sxx/expected.json: one case including every transaction and facility; TEST_GUIDE.pdf: readable case matrix; realistic_layout.py: sample-inspired layout generator; generate.py: scenario data and checks. Re-run `python3 realistic_layout.py` with reportlab, pypdf, pypdfium2 and Pillow installed. It overwrites this generated pack and rebuilds the ZIP.

The generator validates all bank arithmetic, page counts, fixture identity and installment boundaries, and scanned text-layer absence. validation.json records the checks. This does not establish that the project parser or Azure OCR handles the files successfully; run those integration tests separately. Generated documents use English labels and fictional layouts inspired by the supplied samples; they do not reproduce official branding, multilingual headers or the complete bureau schema.
'''
 (ROOT/'README.md').write_text(readme)
 c=canvas.Canvas(str(ROOT/'TEST_GUIDE.pdf'),pagesize=A4)
 page(c,'FINANCIAL DOCUMENT TEST PACK','GUIDE','12 synthetic scenarios | Bank + CRIB-style pairs',1,2)
 line(c,744,'Scenario matrix',True,14)
 rows=[]
 for e in expected: rows.append([e['scenario_id'],money(e['verified_income_assumption_lkr']),money(e['document_reported_monthly_debt_lkr']) if e['document_reported_monthly_debt_lkr'] else 'Missing',money(e['requested_monthly_lease_lkr']),e['expected_action'].replace('_',' ')])
 table(c,720,['CASE','INCOME*','DEBT / MONTH','LEASE REQUEST','EXPECTED ACTION'],rows,[45,105,110,110,165],8)
 line(c,366,'* Monthly income assumptions for verified test ground truth.',size=9)
 y=341
 for e in expected:
  line(c,y,e['scenario_id']+'  '+e['scenario'],size=9); y-=19
 c.showPage(); page(c,'FINANCIAL DOCUMENT TEST PACK','GUIDE','How to run and interpret the test fixtures',2,2)
 lines=[('1. Choose a paired scenario', 'Each Sxx folder contains a 6-page bank PDF, a 3-page credit report,','and expected.json with transaction-level and facility-level ground truth.'),('2. Exercise your document pipeline','Upload the original PDFs for layout/table extraction. S07 and S08','include image-only scanned versions for OCR. Read all report pages.'),('3. Compare extraction with expected.json','Check money-in, money-out, running balances, monthly installments,','outstanding principal, arrears, dispute status and identity matching.'),('4. Classify and verify income','Exclude own-account transfers, loan proceeds and reversed purchases.','Business income assumes the explicit 15,000 monthly supplies cost.'),('5. Apply the project research rule','Cap = floor(verified monthly income x 0.60 to cents). Compare existing','verified debt plus requested lease. No bureau or approval grade is assigned.'),('6. Keep review cases unresolved','Missing installments, disputes, no income and identity mismatch are','not normal capacity passes. Adverse history needs separate risk review.'),('7. Understand the scope of validation','Arithmetic and generated PDFs are checked; this pack does not prove','live Azure OCR, production parser success or real approval accuracy.'),('8. Add more scenarios','Edit CASES in generate.py and run it with its Python dependencies.','All files remain synthetic; no original personal identifiers are copied.')]
 y=741
 for head,a,b in lines:
  line(c,y,head,True,12); line(c,y-21,a,size=10); line(c,y-38,b,size=10); y-=78
 c.showPage(); c.save()
 checks={'scenario_count':12,'pdf_count':29,'bank_pages_each':6,'crib_pages_each':3,'scanned_variant_count':4,'all_running_balances_valid':True,'all_month_totals_valid':True,'boundary_exact_pass':expected[4]['is_within_limit'] is True,'boundary_one_cent_fail':expected[5]['is_within_limit'] is False,'integration_ocr_tested':False}
 assert checks['boundary_exact_pass'] and checks['boundary_one_cent_fail']
 for e in expected:
  case=e['scenario_id']; assert len(PdfReader(ROOT/case/'bank_statement.pdf').pages)==6; assert len(PdfReader(ROOT/case/'crib_style_report.pdf').pages)==3
  for m in e['bank_months']:
   running=D(m['opening_lkr'])
   for t in m['transactions']:
    running+=D(t['credit_lkr'].replace(',',''))-D(t['debit_lkr'].replace(',','')); assert running==D(t['balance_lkr'])
   assert running==D(m['closing_lkr'])
  for file in (ROOT/case).glob('*scanned.pdf'): assert all(not (p.extract_text() or '').strip() for p in PdfReader(file).pages)
  for file in (ROOT/case).glob('*.pdf'):
   if 'scanned' not in file.name: assert all('SYNTHETIC TEST DOCUMENT' in (p.extract_text() or '') for p in PdfReader(file).pages)
 (ROOT/'validation.json').write_text(json.dumps(checks,indent=2))
 with zipfile.ZipFile(ROOT.parent/'realistic-financial-test-pack.zip','w',zipfile.ZIP_DEFLATED) as z:
  for f in sorted(ROOT.rglob('*')):
   if f.is_file(): z.write(f,f.relative_to(ROOT.parent))
 print(json.dumps(checks,indent=2))

if __name__=='__main__': main()
