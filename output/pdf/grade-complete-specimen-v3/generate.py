"""Synthetic test documents following the supplied ten-page specimen's structure.
Run with Python packages reportlab, pypdf, pypdfium2 and Pillow.
"""
from pathlib import Path
from decimal import Decimal, ROUND_HALF_UP, ROUND_FLOOR
import json, csv, calendar, math, zipfile
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor as H
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader

ROOT=Path(__file__).resolve().parent
D=lambda x: Decimal(str(x))
fmt=lambda x:f'{D(x):,.2f}'
INK=H('#343434'); GRID=H('#d4d4d4'); TEAL=H('#159daf')
GRADES=[f'{f}{i}' for f in 'ABCDE' for i in range(1,4)]+['XX']
SCORES=[810,740,650,605,594,583,576,566,555,540,525,505,475,380,260,None]
PROBS=[.5,1.1,2.6,6,8,11,15,20,25,42,49,57,68,85,99,None]
PROFILES={'A':(200000,20000,160000,0),'B':(150000,25000,80000,0),'C':(100000,25000,80000,15),'D':(100000,35000,80000,95),'E':(80000,40000,35000,120),'X':(120000,0,80000,0)}
COLORS={'A':'#28bb22','B':'#7acb5e','C':'#ffbd12','D':'#ed9100','E':'#d92120','X':'#999999'}

def text(c,x,y,s,size=8,bold=False,color=INK,font=None):
 c.setFillColor(color); c.setFont(font or ('Helvetica-Bold' if bold else 'Helvetica'),size); c.drawString(x,y,str(s))

def rule(c,y,x=23,w=549):
 c.setStrokeColor(GRID); c.setLineWidth(.45); c.line(x,y,x+w,y)

def header(c,g,n,total,title):
 # Comparable position/scale to the reference; original fictional wordmark.
 text(c,23,804,'TCB',21,True,H('#547fa7')); text(c,76,804,'iReport',20,True,H('#e51d29')); text(c,147,804,'plus',19,True,TEAL); text(c,185,804,'- Consumer',12,True)
 for x,label,val in [(337,'Reference Number',f'TEST-{g}-20261001'),(438,'Requested by','TEST0001'),(503,'Report Created','01-Oct-2026')]:
  text(c,x,815,label,5.7,True); text(c,x,804,val,6)
 # The document identification is separate from the original wordmark.
 text(c,24,787,'SYNTHETIC TEST DOCUMENT | FICTIONAL BUREAU | NOT VALID FOR FINANCIAL USE',7,True,H('#bf642a'))
 for y,label,val in [(773,'Searched Name',f'TEST NIMAL PERERA {g}'),(761,'Searched ID No',f'TEST-ID-{g}'),(749,'Searched Address','10 EXAMPLE LANE, TEST CITY')]:
  text(c,43,y,label,7.2); text(c,123,y,'- '+val,7.3,True)
 c.setStrokeColor(TEAL); c.circle(29,769,3,fill=0,stroke=1); c.arc(24,751,34,765,0,180)
 c.saveState(); c.translate(298,388); c.rotate(46); c.setFillColor(H('#d5d5d5')); c.setFont('Helvetica',29); c.drawCentredString(0,0,'SYNTHETIC CRIB-STYLE TEST REPORT'); c.restoreState()
 text(c,24,725,title,10,True); rule(c,713)
 text(c,24,22,'TCB: fictional institution | Test fixture | No official certification',6.5)
 text(c,504,22,f'Page {n} | {total}',7,H('#828282'))

def section(c,y,title):
 text(c,25,y,'> '+title,8.2,bold=False,color=H('#777777')); return y-12

def table(c,y,headers,rows,widths,row_h=18,size=7):
 x0=23; w=sum(widths)
 for i,row in enumerate([headers]+rows):
  if i==0: c.setFillColor(H('#ededed')); c.rect(x0,y-row_h,w,row_h,fill=1,stroke=0)
  rule(c,y-row_h,x0,w); x=x0
  for cell,col_w in zip(row,widths):
   s=str(cell); font='Helvetica-Bold' if i==0 else 'Helvetica'
   if c.stringWidth(s,font,size)>col_w-8: raise ValueError(f'Cell overflow {s} in {col_w}')
   text(c,x+4,y-row_h*.69,s,size,i==0); x+=col_w
  y-=row_h
 return y

def panel(c,x,y,w,h,title):
 c.setStrokeColor(GRID); c.setLineWidth(.5); c.rect(x,y-h,w,h,stroke=1,fill=0); text(c,x+8,y-17,'> '+title,8,color=H('#777777'))

def tick(c,x,y,days):
 color=H('#24b81a' if days<=29 else '#f09a00' if days<=89 else '#cf2020')
 c.setFillColor(color); c.circle(x,y,4,fill=1,stroke=0)
 if days<=29:
  c.setStrokeColor(H('#ffffff')); c.setLineWidth(.8); p=c.beginPath(); p.moveTo(x-2,y); p.lineTo(x-.5,y-1.5); p.lineTo(x+2,y+2); c.drawPath(p)
 else: text(c,x-1,y-2,'!',6,True,H('#ffffff'))

def facility_data(g,debt,ndia):
 if g=='XX': return []
 return [dict(index=i,entity_code=f'C{i+1}',facility_id=f'TEST-FAC-{g}-{i}',institution_category='LCB',member=f'TL00{i}',branch='001',type='LOAN' if i==1 else 'LEAS',status='ACTV',ownership='OWN',currency='LKR',amount_granted=str(D(debt)*D(r)*36),current_balance=str(D(debt)*D(r)*24),arrears=str(D(debt)*D(r)*(D(3) if ndia>=90 else D('.5') if ndia else D(0)) if i==1 else D(0)),installment=str(D(debt)*D(r)),repayment_type='MNLY',ndia=ndia if i==1 else 0) for i,r in [(1,'.6'),(2,'.4')]]

def history(g):
 return [0]*12 if g[0] in 'ABX' else [0,0,0,0,0,10,35,0,0,15,0,15] if g[0]=='C' else [0,0,10,20,0,0,30,40,60,75,90,95] if g[0]=='D' else [30,60,90,120,0,0,30,60,90,120,120,120]

def bank(g,income,debt,opening):
 folder=ROOT/g; folder.mkdir(exist_ok=True); c=canvas.Canvas(str(folder/'bank_statement.pdf'),pagesize=A4)
 c.setTitle(f'{g} Synthetic Bank Statement - April to September 2026'); balance=D(opening); months=[]; daily=[]
 for page,month in enumerate(range(4,10),1):
  c.setFillColor(H('#fff0f0')); c.roundRect(32,658,244,123,13,fill=1,stroke=0)
  text(c,48,764,'TO:',8,True)
  for y,s in [(746,f'MR. TEST NIMAL PERERA {g}'),(729,'10, EXAMPLE LANE'),(712,'TEST CITY'),(695,'SRI LANKA')]: text(c,60,y,s,8,True,font='Courier-Bold')
  path=c.beginPath()
  for i in range(8):
   a=math.pi/8+i*math.pi/4; x=353+24*math.cos(a); y=758+24*math.sin(a)
   if i==0:path.moveTo(x,y)
   else:path.lineTo(x,y)
  path.close(); c.setStrokeColor(H('#f18d32')); c.setLineWidth(1.7); c.drawPath(path)
  text(c,341,751,'HB',14,True,H('#e07821')); text(c,389,766,'Horizon Bank PLC',14,True)
  text(c,389,752,'Fictional institution - test account',7)
  text(c,389,739,'Head Office: 1 Example Square',7); text(c,389,726,'Test City, Sri Lanka',7)
  text(c,329,705,'STATEMENT OF ACCOUNT',11,True); text(c,329,688,'CREDIT BALANCE INDICATED AS Cr',7)
  text(c,24,811,'SYNTHETIC TEST DOCUMENT - NOT VALID FOR FINANCIAL USE',8,True,H('#bf642a'))
  text(c,33,639,f'ACCOUNT NO : TEST-ACCT-{g}     LKR',8,True,font='Courier-Bold')
  text(c,33,623,f'CUSTOMER ID: TEST-ID-{g}',7); text(c,389,638,f'PAGE_NO: {page}',8,True,font='Courier-Bold')
  text(c,82,598,f'FOR THE PERIOD : 01-{month:02d}-2026 TO {calendar.monthrange(2026,month)[1]}-{month:02d}-2026',7.5,True,font='Courier-Bold')
  text(c,409,617,'DATE: 01-10-2026',7.5,True,font='Courier-Bold')
  # Merchant-level expenses produce 25+ rows, not a short illustrative table.
  tx=[]
  def add(day,desc,dr=0,cr=0,kind='personal_expense'):
   tx.append(dict(date=f'2026-{month:02d}-{day:02d}',particulars=desc,debit_lkr=str(D(dr)),credit_lkr=str(D(cr)),classification=kind))
  merchants=['POS TEST GROCERY','ECOM TEST TRANSPORT','POS TEST PHARMACY','POS TEST CAFE','BILL TEST UTILITY','POS TEST MARKET']
  for i,day in enumerate(range(3,25)): add(day,merchants[i%6]+f' R{month:02d}{i+1:03d}',700 if i<20 else 2000)
  add(28,'NSC SAL '+calendar.month_abbr[month].upper()+' TEST EMPLOYER',cr=income,kind='qualifying_income')
  if debt:
   add(29,'STO TEST LENDER A',dr=D(debt)*D('.6'),kind='debt_payment'); add(29,'STO TEST LENDER B',dr=D(debt)*D('.4'),kind='debt_payment')
  add(29,'STO TEST RENT',dr=20000)
  add(30 if month!=2 else 28,'IB OWN ACCOUNT TRANSFER',dr=D(income)-D(debt)-D(38000),kind='internal_transfer')
  for i in range(35): c.setFillColor(H('#ffe5dd') if i%2==0 else H('#fff3f0')); c.rect(23,548-i*14,549,12,fill=1,stroke=0)
  # A pale original geometric bank watermark, behind live statement text.
  c.saveState(); c.setFillColor(H('#fff0da')); c.setFont('Helvetica-Bold',112); c.drawCentredString(298,290,'HB'); c.restoreState()
  c.setStrokeColor(H('#8b8b8b')); c.setLineWidth(.4); c.rect(23,563,549,19,fill=0,stroke=1)
  for x,s in [(36,'DATE'),(106,'PARTICULARS'),(330,'DEBITS'),(406,'CREDITS'),(498,'BALANCE')]: text(c,x,570,s,7.5,True)
  start=balance; drsum=crsum=D(0); y=552
  def row(date,desc,dr,cr,bal):
   nonlocal y
   text(c,36,y,date,7,True,font='Courier-Bold'); text(c,106,y,desc,7,True,font='Courier-Bold'); c.setFillColor(INK); c.setFont('Courier-Bold',7)
   for x,s in [(379,dr),(454,cr),(563,bal)]:c.drawRightString(x,y,s)
   y-=14
  row(f'01{calendar.month_abbr[month].upper()}2026','B/F','','',fmt(start)+'Cr')
  daybalances=[]; pos=0
  for day in range(1,calendar.monthrange(2026,month)[1]+1):
   while pos<len(tx) and int(tx[pos]['date'][-2:])==day:
    t=tx[pos]; dr=D(t['debit_lkr']); cr=D(t['credit_lkr']); balance+=cr-dr; drsum+=dr; crsum+=cr; t['balance_lkr']=str(balance)
    row(f'{day:02d}{calendar.month_abbr[month].upper()}2026',t['particulars'],fmt(dr) if dr else '',fmt(cr) if cr else '',fmt(balance)+'Cr'); pos+=1
   daybalances.append(str(balance)); daily.append(balance)
  assert pos==len(tx) and balance==start and drsum==crsum
  row(f'{calendar.monthrange(2026,month)[1]}{calendar.month_abbr[month].upper()}2026','C/F','','',fmt(balance)+'Cr')
  rule(c,y+6,106,456); row('','TOTAL',fmt(drsum),fmt(crsum),'')
  text(c,24,53,'This statement is generated for software testing. All names, accounts and transactions are fictional.',7)
  text(c,24,22,f'SYNTHETIC | {g} | Horizon Bank is fictional',7); text(c,503,22,f'Page {page} / 6',7)
  months.append(dict(month=f'2026-{month:02d}',opening_lkr=str(start),closing_lkr=str(balance),total_debits_lkr=str(drsum),total_credits_lkr=str(crsum),daily_closing_balances_lkr=daybalances,transactions=tx)); c.showPage()
 c.save(); return months,(sum(daily)/len(daily)).quantize(D('.01'),rounding=ROUND_HALF_UP)

def crib(g,score,pd,fac,ndia):
 c=canvas.Canvas(str(ROOT/g/'crib_report.pdf'),pagesize=A4); c.setTitle(f'{g} Synthetic CRIB-style report - specimen structure')
 total=sum(D(f['current_balance']) for f in fac); installments=sum(D(f['installment']) for f in fac); arrears=sum(D(f['arrears']) for f in fac); hist=history(g)
 def new(n,title): header(c,g,n,10,title)
 # 1: dashboard, same order and grouping as the specimen.
 new(1,'Dashboard'); panel(c,23,703,264,75,'Scoring'); panel(c,299,703,273,75,'Disputes')
 for x,value,label in [(58,'N/A' if score is None else score,'Score'),(130,g,'Risk Grade')]: text(c,x,663,value,12,True); text(c,x,645,label,7)
 c.setStrokeColor(H(COLORS[g[0]]));c.setLineWidth(1.6);c.arc(123,655,156,682,0,180)
 c.setStrokeColor(INK);c.setLineWidth(2);path=c.beginPath();path.moveTo(218,667);path.lineTo(231,667);path.moveTo(226,672);path.lineTo(231,667);path.lineTo(226,662);c.drawPath(path);text(c,212,645,'Trend',7)
 for x,v,label in [(319,0,'Active Disputes'),(411,0,'False Disputes'),(494,0,'Registered in Court')]: text(c,x,663,v,11,True); text(c,x,645,label,6.3)
 panel(c,23,614,549,115,'Payment Profile Status')
 pairs=[('Open Credit Facility',len(fac),'Closed Credit Facility',0 if g=='XX' else 1),('Total Amount in Arrears','LKR '+fmt(arrears),'Number of Different Creditors',len(fac)),('The Highest Current NDIA',ndia,'Total Current Balance','LKR '+fmt(total)),('Total Amount Granted in Open Credit Facilities','LKR '+fmt(sum(D(f['amount_granted']) for f in fac)),'Total Installment Amount','LKR '+fmt(installments)),('The Highest NDIA in 12 Months',max(hist),'','')]
 for i,(a,b,d,e) in enumerate(pairs):
  y=580-i*15; text(c,31,y,a,6.6); text(c,243,y,b,6.8,True); text(c,302,y,d,6.6); text(c,486,y,e,6.8,True)
 panel(c,23,485,549,104,'Payment Profile')
 for i,m in enumerate(['Oct','Nov','Dec','Jan','Feb','Mar','Apr','May','Jun','Jul','Aug','Sep']):text(c,79+i*40,448,m,7)
 text(c,34,422,'2025/26',7)
 for i,days in enumerate(hist):
  if g=='XX':text(c,80+i*40,422,'N/A',6.2)
  else:tick(c,88+i*40,424,days)
 text(c,31,400,'Green: 0-29 days | Amber: 30-89 days | Red: 90+ days | Grey: no data',6.5)
 for y,title,labels in [(368,'Utilities',[('Number of Utility Accounts','0'),('Number of Different Providers','0'),('Total Current Amount in Arrears','LKR 0.00'),('Highest Current NDIA','0')]),(286,'Insurances',[('Number of Insurance Policies','0'),('Number of Claims','0'),('Total Current Amount in Arrears','LKR 0.00'),('Highest Current NDIA','0')])]:
  panel(c,23,y,549,68,title)
  for i,(a,b) in enumerate(labels):text(c,32+(i%2)*270,y-34-(i//2)*16,a,7); text(c,224+(i%2)*270,y-34-(i//2)*16,b,7,True)
 panel(c,23,205,549,58,'Dishonoured Cheques'); text(c,34,171,'Number: 0',7); text(c,194,171,'Weeks Since Last: N/A',7); text(c,380,171,'Total Amount: LKR 0.00',7)
 for x,title,a,b in [(23,'Relations','Related Subjects: 0','Involved Subjects: 0'),(210,'Inquiries','Self Inquiries (6 months): 1','Inquiring Members (6 months): 2'),(397,'Collaterals','Credit facilities with','collateral: '+('2' if fac else '0'))]:
  panel(c,x,133,175,72,title); text(c,x+9,97,a,6.6); text(c,x+9,82,b,6.6)
 c.showPage()
 # 2: subject information and change histories.
 new(2,'Subject Information')
 rows=[['Full Name',f'TEST NIMAL PERERA {g}','Date of Birth','01-Jan-1990 (fictional)'],['Gender','Male','Citizenship','Sri Lankan'],['Employment','Employed','Profession','Test Analyst'],['Employer Name','TEST EXAMPLE SERVICES','Business Name','N/A'],['Mail Address','10 EXAMPLE LANE','Permanent Address','10 EXAMPLE LANE'],['City','TEST CITY','City','TEST CITY'],['Postal Code','TEST-001','Country','Sri Lanka'],['Mobile Phone','Not supplied','Email','Not supplied'],['Identity','TEST-ID-'+g,'Passport','Not supplied']]
 y=table(c,699,['Basic Information','Value','Additional Information','Value'],rows,[98,175,108,168],23,7)
 y=section(c,y-28,'History of Distinct Names'); y=table(c,y,['Reporting Member','Changed Value','Valid From','Valid To'],[['TEST LENDER A',f'TEST NIMAL PERERA {g}','01-Apr-2026','Current']],[145,224,90,90],21,7)
 y=section(c,y-30,'Other Historical Changes'); table(c,y,['Category','Changed Value','Valid From','Valid To'],[['Mail Address','10 EXAMPLE LANE, TEST CITY','01-Apr-2026','Current'],['Permanent Address','10 EXAMPLE LANE, TEST CITY','01-Apr-2026','Current']],[112,257,90,90],21,7)
 c.showPage()
 # 3: scoring, trend history, reason codes and the actual sample's family legend.
 new(3,'Scoring'); y=table(c,699,['Score','Risk Grade','Trend','Probability of Default'],[['N/A' if score is None else score,g,'Stable','N/A' if pd is None else f'{pd:.2f}% (synthetic)']],[118,118,118,195],26,9)
 y=section(c,y-25,'Score History'); graph_top=y-10; graph_bottom=graph_top-130
 rule(c,graph_bottom,56,492)
 for val in [250,400,550,700,900]:
  yy=graph_bottom+(val-250)/650*130; text(c,25,yy-2,val,6,color=H('#999999'))
 if score is not None:
  vals=[score]*6
  c.setStrokeColor(TEAL); c.setLineWidth(1.8); path=c.beginPath()
  for i,v in enumerate(vals):
   x=78+i*86; yy=graph_bottom+(v-250)/650*130
   if i==0:path.moveTo(x,yy)
   else:path.lineTo(x,yy)
  c.drawPath(path)
  for i,v in enumerate(vals):
   c.setFillColor(H('#ffffff')); c.circle(78+i*86,graph_bottom+(v-250)/650*130,3,fill=1,stroke=1)
 else: text(c,165,graph_bottom+60,'XX - insufficient information for a score',9)
 for i,m in enumerate(['Apr 26','May 26','Jun 26','Jul 26','Aug 26','Sep 26']):text(c,65+i*86,graph_bottom-15,m,7)
 table(c,graph_bottom-30,['Month','Apr 26','May 26','Jun 26','Jul 26','Aug 26','Sep 26'],[['Score']+(['N/A']*6 if score is None else [str(v) for v in vals]),['Risk Grade']+[g]*6,['PD (%)']+[str(pd) if pd is not None else 'N/A']*6],[105]+[74]*6,18,7)
 y=section(c,graph_bottom-125,'Top Reason Codes')
 reasons=[['SYN1','Synthetic bureau subgrade explicitly assigned for testing'],['SYN2',{'A':'Current facilities and consistent payment history','B':'Timely repayment with moderate financial commitments','C':'Irregular repayments recorded in the evidence window','D':'High arrears and deteriorating repayment behaviour','E':'Current arrears and historical negatively closed facility','X':'Insufficient credit information - no automatic scoring'}[g[0]]]]
 y=table(c,y,['Reason Code','Description'],reasons,[105,444],18,7)
 y=section(c,y-24,'Scoring Legend'); xx=[36,142,248,354,460,566]; bounds=[250,500,550,580,610,900]
 for i,fam in enumerate('EDCBA'):
  c.setStrokeColor(H(COLORS[fam])); c.setLineWidth(3); c.line(xx[i],y-16,xx[i+1],y-16); text(c,(xx[i]+xx[i+1])/2-3,y-36,fam,8,True)
 for x,v in zip(xx,bounds):
  c.setStrokeColor(GRID); c.setFillColor(H('#ffffff')); c.setLineWidth(.8); c.circle(x,y-16,11,fill=1,stroke=1); text(c,x-8,y-19,v,6.5)
 yy=y-62
 for label,risk,desc in [('A1-A3','Very Low Risk','Based on credit histories'),('B1-B3','Low Risk','Based on timely payment behaviour'),('C1-C3','Average Risk','Irregular repayments or over-indebtedness'),('D1-D3','High Risk','High probability of defaulting on a credit facility'),('E1-E3','Very High Risk','Default or extreme irregularities in servicing credit'),('XX','Not Scored','Insufficient information in the database')]:
  c.setFillColor(H(COLORS[label[0]])); c.circle(29,yy+2,4,fill=1,stroke=0); text(c,40,yy,label,7,True); text(c,110,yy,risk,7,True); text(c,210,yy,desc,7); yy-=18
 text(c,24,yy-4,'Family legend follows supplied specimen; subgrade cutoffs and PD calibration are not asserted.',6.4)
 c.showPage()
 # 4: Debtor/guarantor summaries by financial institution sector.
 new(4,'Credit Facility'); y=section(c,699,'Debtor Summary per Sector - Last 5 Years (Amounts in LKR)')
 heads=['Institution','Open','Closed','In Arrears','Balance','Granted','Arrears','Max NDIA']; widths=[89,42,43,61,79,83,80,72]
 row=['LCB',len(fac),0 if g=='XX' else 1,1 if arrears else 0,fmt(total),fmt(sum(D(f['amount_granted']) for f in fac)),fmt(arrears),ndia]
 y=table(c,y,heads,[row,['LFC',0,0,0,'0.00','0.00','0.00',0],['Total',len(fac),0 if g=='XX' else 1,1 if arrears else 0,fmt(total),fmt(sum(D(f['amount_granted']) for f in fac)),fmt(arrears),ndia]],widths,24,6.7)
 y=section(c,y-35,'Guarantor Summary per Sector - Last 5 Years (Amounts in LKR)'); table(c,y,heads,[['LCB',0,0,0,'0.00','0.00','0.00',0],['LFC',0,0,0,'0.00','0.00','0.00',0]],widths,24,6.7)
 c.showPage()
 # 5: facility column matrix, not stacked cards.
 new(5,'List of All Credit Facilities')
 fields=[('Institution Category','institution_category'),('Member','member'),('Branch','branch'),('Credit Facility Type','type'),('Credit Facility Status','status'),('Ownership Indicator','ownership'),('Currency','currency'),('Amount Granted/Limit','amount_granted'),('Current Balance','current_balance'),('Amount in Arrears','arrears'),('Installment Amount','installment'),('Repayment Type','repayment_type'),('Entity Code','entity_code')]
 rows=[]
 for label,key in fields:rows.append([label]+[(fmt(f[key]) if key in ['amount_granted','current_balance','arrears','installment'] else f[key]) for f in fac])
 if fac:
  rows.extend([['Last Update Date','30-Sep-2026','30-Sep-2026'],['First Disbursement Date','01-Apr-2025','01-Apr-2025'],['Last Payment Date','29-Sep-2026','29-Sep-2026'],['Amount Written Off','0.00','0.00'],['Purpose of Credit Facility','09:01:001','01:02:003'],['Security Coverage','FS','FS'],['Guarantee Coverage','NG','NG']]); y=table(c,699,['Credit Facility Index','1','2'],rows,[253,148,148],18,7)
 else:y=table(c,699,['Credit Facility Index','Information'],[['No facilities','No credit history recorded in this fixture']],[253,296],23,8)
 y=section(c,y-23,'Payment 1-2 - NDIA history')
 if fac:table(c,y,['Month','Facility 1','Facility 2'],[[m+' 2026',history(g)[6+i],0] for i,m in enumerate(['Apr','May','Jun','Jul','Aug','Sep'])],[253,148,148],21,7)
 else:text(c,26,y-14,'No payment records available.',8)
 c.showPage()
 # 6: closed facilities and historical aggregate tables.
 new(6,'Closed Credit Facilities and Historical Summary'); y=section(c,699,'Summary of Credit Facilities (Last 5 Years)')
 closed=[] if g=='XX' else [dict(type='LOAN',status='Written off / closed' if g[0]=='E' else 'Settled / closed',granted='150000.00',current='0.00',writeoff='50000.00' if g[0]=='E' else '0.00',closed_date='31-Dec-2025')]
 y=table(c,y,['Period','Debtor Facilities','Amount Granted','Guarantor Facilities','Granted as Guarantor'],[['2026-09',len(fac),fmt(sum(D(f['amount_granted']) for f in fac)),0,'0.00'],['2025-12',len(closed),'150,000.00' if closed else '0.00',0,'0.00']],[83,112,116,116,122],24,7)
 y=section(c,y-33,'Details of Closed Credit Facilities - Last 5 Years')
 y=table(c,y,['Facility Type','Closure Status','Granted','Current Balance','Written Off'],[[r['type'],r['status'],fmt(r['granted']),fmt(r['current']),fmt(r['writeoff'])] for r in closed] or [['No record','N/A','0.00','0.00','0.00']],[83,142,108,108,108],25,7)
 if g[0]=='E':text(c,26,y-20,'Historical event: negatively closed facility with a written-off amount of LKR 50,000.00.',7)
 y=section(c,y-60,'Non-LKR Credit Facilities'); table(c,y,['Currency','Debtor Facilities','Current Balance'],[['None',0,'0.00']],[183,183,183],22,7)
 c.showPage()
 # 7: related/involved entities, inquiries, disputes.
 new(7,'Relations, Inquiries and Disputes'); y=section(c,699,'List of All Credit Relations'); y=table(c,y,['Entity Code','Role of Subject','Related Entity','Validity'],[['None','No related subjects','N/A','N/A']],[103,183,173,90],23,7)
 y=section(c,y-25,'List of All Involvements'); y=table(c,y,['Entity Code','Type of Relation','Involved Entity','Validity'],[['None','No involvements','N/A','N/A']],[103,183,173,90],23,7)
 y=section(c,y-25,'Inquiries - Last 6 Months'); y=table(c,y,['Self Inquiries','Inquiring Members'],[[1,2]],[274,275],23,8)
 y=table(c,y-15,['Member','Common for Inquiry','Date of Inquiry'],[['TEST LENDER A','Evaluating a new facility','01-Apr-2026'],['TEST LENDER B','Evaluating a new facility','03-Apr-2026']],[154,270,125],23,7)
 y=section(c,y-30,'Disputes'); table(c,y,['Category','Credit Facility','Other Data','Cheques','Utilities','Insurance','In Court'],[['Active',0,0,0,0,0,0],['Closed',0,0,0,0,0,0]],[105]+[74]*6,23,7)
 c.showPage()
 # 8: dispute lists and dishonoured cheque details.
 new(8,'Dispute Details and Dishonoured Cheques'); y=section(c,699,'List of All Disputes'); y=table(c,y,['Dispute ID','Subject','Status','Last Update'],[['None','No disputes recorded','N/A','N/A']],[103,260,90,96],23,7)
 y=section(c,y-30,'Dishonoured Cheques'); y=table(c,y,['Number of Cheques','Weeks Since Last','Total Amount'],[[0,'N/A','LKR 0.00']],[183]*3,23,8)
 y=section(c,y-30,'List of All Dishonoured Cheques'); table(c,y,['Entity','Cheque Date','Cheque No','Amount','Reason','Member'],[['None','N/A','N/A','0.00','No events','N/A']],[68,91,99,91,110,90],23,7)
 c.showPage()
 # 9: utilities/insurance summaries and legend definitions.
 new(9,'Utilities, Insurances and Explanations'); y=section(c,699,'Utilities'); y=table(c,y,['Utility Accounts','Different Providers','Total Arrears','Highest NDIA'],[[0,0,'LKR 0.00',0]],[125,155,149,120],24,8)
 y=section(c,y-30,'Insurances'); y=table(c,y,['Insurance Policies','Number of Claims','Total Arrears','Highest NDIA'],[[0,0,'LKR 0.00',0]],[125,155,149,120],24,8)
 y=section(c,y-30,'Explanations'); y=table(c,y,['Payment Status','Meaning'],[['0-29 days','Current / low delinquency band'],['30-89 days','Intermediate delinquency band'],['90+ days','Severe delinquency band'],['N/A','No data available'],['NDIA','Number of Days in Arrears'],['R/A','Reply Available']],[153,396],23,8)
 y=section(c,y-30,'Institution Category and Facility Type'); table(c,y,['Code','Description'],[['LCB','Licensed Commercial Bank'],['LFC','Licensed Finance Company'],['LOAN','Loan'],['LEAS','Leasing'],['CRCD','Credit Card'],['OVDR','Overdraft'],['ACTV','Active']],[153,396],23,8)
 c.showPage()
 # 10: code explanations and test disclaimer, matching reference placement/order.
 new(10,'Explanations and Disclaimer'); y=table(c,699,['Field','Code','Meaning'],[['Ownership Indicator','OWN','Owner / individual borrower'],['Ownership Indicator','JNT','Joint borrower'],['Ownership Indicator','GRT','Guaranteed'],['Currency','LKR','Sri Lankan Rupee'],['Repayment Type','MNLY','Monthly'],['Repayment Type','DMND','On demand'],['Security Coverage','FS','Fully secured'],['Security Coverage','PS','Partly secured'],['Security Coverage','US','Unsecured'],['Guarantee Coverage','NG','Not guaranteed']],[175,85,289],27,8)
 y=section(c,y-35,'Disclaimer')
 lines=['This is a synthetic software-testing fixture, not a report issued by CRIB or a lender.','It follows the section order and table structure of the supplied ten-page specimen.','All names, identifiers, account references, facilities and transactions are fictional.','Bureau scores, subgrades, probabilities and reason codes are assigned test values.','The family legend is transcribed from the sample; exact subgrade cutoffs are not claimed.','No official score model, probability calibration or bureau endorsement is represented.','A CRIB risk grade is distinct from the project lease-feasibility grade.','Expected project results require verified inputs supplied in expected.json.','Outstanding principal is not a monthly debt obligation. Transfers are not income.','Use every report page when extracting facilities and payment history.']
 for i,s in enumerate(lines):text(c,29,y-16-i*16,s,7.5)
 c.showPage(); c.save(); return closed

def evaluate(g,income,debt,balance):
 if g=='XX':return dict(lease_grade=None,total_score=None,action='ManualVerificationRequired',reason='XX is insufficient information; current scoring contract requires A-E.')
 lease=D(20000); ratio=(D(debt)+lease)/D(income); liquidity=D(balance)/lease
 dsr=35 if ratio<=D('.2') else 25 if ratio<=D('.35') else 10 if ratio<=D('.5') else 0
 liq=20 if liquidity>=6 else 15 if liquidity>=3 else 5 if liquidity>=1 else 0
 credit={'A':20,'B':15,'C':5,'D':0,'E':0}[g[0]]; penalty=-50 if g[0]=='E' else 0
 score=max(0,min(100,dsr+25+liq+credit+penalty)); grade='A' if score>=85 else 'B' if score>=70 else 'C' if score>=50 else 'D' if score>=30 else 'E'
 action={'A':'FastTrack','B':'Proceed','C':'ManualReview','D':'Escalate','E':'Reject'}[grade]; cap=(D(income)*D('.6')).quantize(D('.01'),rounding=ROUND_FLOOR); within=D(debt)+lease<=cap
 if not within and action!='Reject':action='Escalate'
 return dict(lease_grade=grade,total_score=score,action=action,debt_service_ratio=str(ratio),liquidity_buffer_months=str(liquidity),breakdown=dict(debt_service=dsr,income_consistency=25,liquidity=liq,credit_history=credit,penalty=penalty),maximum_total_payments_lkr=str(cap),available_lease_lkr=str(max(D(0),cap-D(debt))),is_within_capacity=within)

def main():
 manifest=[]
 for g,score,pd in zip(GRADES,SCORES,PROBS):
  income,debt,opening,ndia=PROFILES[g[0]]; bankdata,balance=bank(g,income,debt,opening); fac=facility_data(g,debt,ndia); closed=crib(g,score,pd,fac,ndia); result=evaluate(g,income,debt,balance)
  if g!='XX':assert result['lease_grade']==g[0],(g,result)
  data=dict(scenario_id=g,bureau_subgrade=g,bureau_score=score,bureau_probability_of_default_percent=pd,bureau_values_are_assigned_synthetic=True,exact_subgrade_cutoffs_not_claimed=True,identity=f'TEST-ID-{g}',bank_statement_months=bankdata,credit_facilities=fac,closed_facilities=closed,scoring_contract='component-2-financial-feasibility-v1',verified_input_assumptions=dict(ApplicationId='TEST-APP-'+g,ApplicantId='TEST-ID-'+g,AverageMonthlyIncomeLkr=str(income),IncomeConsistencyRatio='1',RequestedMonthlyLeasePaymentLkr='20000',MonthlyDebtObligationsLkr=str(debt),AverageAccountBalanceLkr=str(balance),AverageBalanceMethod='Average of every daily closing balance, 1 Apr to 30 Sep 2026',OverdraftCountInEvidenceWindow=0,CreditRiskGrade=None if g=='XX' else g[0],HasDefaultHistory=g[0]=='E'),expected_result=result,notes='Income consistency and default-history interpretation are explicit reviewer-verified fixture assumptions; PDFs alone do not complete the current OCR mapping.')
  (ROOT/g/'expected.json').write_text(json.dumps(data,indent=2)); manifest.append(data)
 (ROOT/'manifest.json').write_text(json.dumps(manifest,indent=2))
 with (ROOT/'grade_matrix.csv').open('w',newline='') as f:
  w=csv.writer(f);w.writerow(['Bureau subgrade','Bureau score (synthetic)','Income','Monthly debt','Requested lease','Daily average balance','Project score','Project grade','Action'])
  for e in manifest:
   a=e['verified_input_assumptions']; r=e['expected_result']; w.writerow([e['bureau_subgrade'],e['bureau_score'],a['AverageMonthlyIncomeLkr'],a['MonthlyDebtObligationsLkr'],20000,a['AverageAccountBalanceLkr'],r['total_score'],r['lease_grade'],r['action']])
 # A compact delivery guide with all cases in a single table.
 c=canvas.Canvas(str(ROOT/'GRADE_SCENARIO_GUIDE.pdf'),pagesize=A4)
 text(c,23,807,'GRADE-COMPLETE FINANCIAL TEST FIXTURES',16,True)
 text(c,23,782,'16 paired scenarios | Six bank pages + ten CRIB-style pages per case',10)
 text(c,23,759,'SYNTHETIC - FOR SOFTWARE TESTING ONLY',9,True,H('#bf642a'))
 rows=[]
 for e in manifest:
  a=e['verified_input_assumptions'];r=e['expected_result'];rows.append([e['bureau_subgrade'],str(e['bureau_score']) if e['bureau_score'] else 'N/A',fmt(a['AverageMonthlyIncomeLkr']),fmt(a['MonthlyDebtObligationsLkr']),r['total_score'] if r['total_score'] is not None else 'N/A',r['lease_grade'] or 'Unknown',r['action'] if r['lease_grade'] else 'Verify manually'])
 table(c,738,['CRIB','Score*','Income LKR','Debt/month','Points','Lease','Action'],rows,[43,51,97,96,48,51,163],23,7.4)
 lines=['* Bureau scores and probabilities are assigned synthetic values, not official calibration.','Every A1-A3 / B1-B3 / C1-C3 / D1-D3 / E1-E3 subgrade and XX has a paired folder.','Project lease grades are derived separately using the checked-in V1 scoring contract.','Family A: 100 / FastTrack. B: 80 / Proceed. C: 55 / ManualReview.','Family D: 40 / Escalate. E: 0 / Reject. XX: verify manually; no A-E grade assigned.','All requests use LKR 20,000 per month, supplied in expected.json rather than bank PDFs.','Bank arithmetic and daily average balances are independently recorded in ground truth.','Income consistency and default history are explicit reviewer assumptions for fixture scoring.','The CRIB structure follows the supplied specimen; fictional branding identifies the copies.','Run the real OCR/verification pipeline separately. These files do not prove parser success.']
 for i,s in enumerate(lines):text(c,23,312-i*18,s,8)
 c.showPage();c.save()
 (ROOT/'README.md').write_text('''# Grade-complete specimen-based financial PDF pack

16 folders: A1 A2 A3 B1 B2 B3 C1 C2 C3 D1 D2 D3 E1 E2 E3 XX.
Each contains bank_statement.pdf (Apr-Sep 2026, six pages), crib_report.pdf (ten pages, supplied specimen section order), and expected.json. manifest.json has all ground truth; grade_matrix.csv is the case index; GRADE_SCENARIO_GUIDE.pdf is the readable overview.

The previous layouts were simplified. This version explicitly follows the specimen's dashboard, subject information, scoring/history/legend, sector summaries, facility column matrix and payments, closed/historical facilities, relations/inquiries/disputes, cheque details, utilities/insurance/explanations, and code/disclaimer pages. The second uploaded PDF is a photographed/two-up report variant; this pack uses the specimen's clean one-page-per-sheet format. Banking layout follows the two uploaded screenshots: address box, compact monospaced rows, salmon rules, dates, debits/credits, Cr balances, B/F and C/F.

CRIB subgrades and lease grades are separate. All 15 bureau subgrades plus XX are explicit assigned fixtures; official subgrade cutoff ranges and PD calibration are not represented. Scores/PD are synthetic. The family legend labels/boundaries follow the specimen. Synthetic reason codes use SYN prefixes. Risk grade is shown in the report; expected lease grade stays outside financial documents. Family-matched scenarios happen to produce lease A, B, C, D, E under the verified fixture inputs; no universal bureau-to-lease mapping is implied.

The project scorer uses debt PLUS requested lease divided by monthly income. Bank average balance is the average daily closing balance of all 183 days in Apr-Sep 2026. Qualifying income comes only from salary. Own-account transfers are excluded. Monthly income consistency 1 is an explicit verified scenario assumption, and E's default-history boolean is a reviewer-confirmed assumption supported by its synthetic negatively closed/written-off facility. Principal is never converted to installment. XX cannot be automatically scored.

Run python3 generate.py with reportlab, pypdf installed to regenerate. Integration OCR/parser success remains untested; the current parser needs labelled/verified evidence and does not reconstruct all table relationships. Use the actual PDF route rather than a mock bypass for integration tests. validation.json records the fixture checks. No original customer information or official logos are embedded.
''')
 checks={'paired_scenarios':16,'pdf_count':33,'bureau_subgrades':GRADES,'project_grades_covered':['A','B','C','D','E'],'crib_pages_each':10,'bank_pages_each':6,'all_bank_running_balances_valid':True,'all_monthly_totals_valid':True,'daily_balance_average_verified':True,'xx_has_no_automatic_grade':True,'grade_oracle':'Python transcription of checked-in C# scoring contract; see engine_validation.json if present','live_ocr_tested':False}
 for e in manifest:
  g=e['scenario_id']
  for name,n in [('bank_statement.pdf',6),('crib_report.pdf',10)]:
   reader=PdfReader(ROOT/g/name); assert len(reader.pages)==n; assert all('SYNTHETIC TEST DOCUMENT' in (p.extract_text() or '') for p in reader.pages)
  for month in e['bank_statement_months']:
   running=D(month['opening_lkr']);cr=dr=D(0)
   for t in month['transactions']:
    cr+=D(t['credit_lkr']);dr+=D(t['debit_lkr']);running+=D(t['credit_lkr'])-D(t['debit_lkr']);assert running==D(t['balance_lkr'])
   assert running==D(month['closing_lkr']) and cr==D(month['total_credits_lkr']) and dr==D(month['total_debits_lkr'])
 for e in manifest:
  daily=[D(v) for m in e['bank_statement_months'] for v in m['daily_closing_balances_lkr']]
  assert len(daily)==183 and min(daily)>=0
  assert (sum(daily)/len(daily)).quantize(D('.01'),rounding=ROUND_HALF_UP)==D(e['verified_input_assumptions']['AverageAccountBalanceLkr'])
 (ROOT/'validation.json').write_text(json.dumps(checks,indent=2)); package();print(json.dumps(checks,indent=2))

def package():
 with zipfile.ZipFile(ROOT.parent/'grade-complete-specimen-v3.zip','w',zipfile.ZIP_DEFLATED) as z:
  for f in sorted(ROOT.rglob('*')):
   if f.is_file() and '__pycache__' not in f.parts:z.write(f,f.relative_to(ROOT.parent))

if __name__=='__main__':main()
