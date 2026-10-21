"""Generate sample-inspired layouts while retaining conspicuous synthetic marks.
Run python3 realistic_layout.py. Uses generate.py for scenario data/validation.
"""
import io, math
from pathlib import Path
import generate as g
from reportlab.pdfgen import canvas
from reportlab.lib.colors import HexColor as H
from reportlab.lib.pagesizes import A4
from pypdf import PdfReader, PdfWriter

INK=H('#252525'); GREY=H('#dddddd'); ORANGE=H('#e48b38')

def txt(c,x,y,s,size=9,bold=False,font=None):
 c.setFillColor(INK); c.setFont(font or ('Helvetica-Bold' if bold else 'Helvetica'),size); c.drawString(x,y,str(s))

def mark(c,case,n,total):
 c.setFillColor(H('#fff0e3')); c.rect(20,805,555,20,fill=1,stroke=0)
 txt(c,29,811,'SYNTHETIC TEST DOCUMENT - NOT VALID FOR FINANCIAL USE',8,True)
 c.saveState(); c.translate(299,386); c.rotate(43); c.setFillColor(H('#ededed')); c.setFont('Helvetica-Bold',34); c.drawCentredString(0,0,'SYNTHETIC - TEST ONLY'); c.restoreState()
 c.setStrokeColor(H('#999999')); c.setLineWidth(.3); c.line(22,38,573,38)
 txt(c,24,24,f'{case} | FICTIONAL DATA | Test copy - no official validity',7)
 txt(c,503,24,f'Page {n} / {total}',7)

def bureau_page(c,title,case,sub,n,total):
 if case=='GUIDE': return OLD_PAGE(c,title,case,sub,n,total)
 mark(c,case,n,total)
 txt(c,24,779,'TCB',22,True); txt(c,80,784,'Consumer Credit Report Plus',12,True)
 txt(c,80,769,'Test Credit Bureau | Fictional institution',8)
 txt(c,376,785,'Reference Number',6,True); txt(c,472,785,'Report Created',6,True)
 txt(c,376,773,f'TEST-REPORT-{case}',7); txt(c,472,773,'01-Oct-2026',7)
 c.setStrokeColor(GREY); c.line(24,757,572,757)

def report_table(c,y,headers,rows,widths,size=8):
 h=25; x0=30; width=sum(widths)
 for i,row in enumerate([headers]+rows):
  c.setFillColor(H('#e6e6e6') if i==0 else H('#fafafa') if i%2 else H('#ffffff')); c.rect(x0,y-h,width,h,fill=1,stroke=0)
  c.setStrokeColor(H('#d5d5d5')); c.setLineWidth(.35); c.rect(x0,y-h,width,h,fill=0,stroke=1)
  x=x0
  for cell,w in zip(row,widths):
   if c.stringWidth(str(cell),'Helvetica-Bold' if i==0 else 'Helvetica',size)>w-10: raise ValueError(str(cell))
   txt(c,x+5,y-16,cell,size,i==0); x+=w
  y-=h
 return y

def bank(case,name,incomes,debt,kind):
 c=canvas.Canvas(str(g.ROOT/case/'bank_statement.pdf'),pagesize=A4)
 c.setTitle(f'{case} Realistic synthetic bank statement'); balance=g.D(1000000); months=[]
 for n,(month,inc) in enumerate(zip(range(4,10),incomes),1):
  mark(c,case,n,6)
  c.setFillColor(H('#fff0f0')); c.roundRect(30,653,260,120,13,fill=1,stroke=0)
  txt(c,45,757,'TO:',9,True)
  for y,s in [(738,f'MR. TEST APPLICANT {case}'),(721,'10, EXAMPLE LANE'),(704,'TEST CITY'),(687,'SRI LANKA')]: txt(c,60,y,s,9,True,'Courier-Bold')
  # Original geometric monogram for the fictional bank, not a real bank logo.
  path=c.beginPath()
  for i in range(8):
   a=math.pi/8+i*math.pi/4; x=354+25*math.cos(a); y=747+25*math.sin(a)
   if i==0: path.moveTo(x,y)
   else: path.lineTo(x,y)
  path.close(); c.setStrokeColor(ORANGE); c.setLineWidth(2); c.drawPath(path)
  txt(c,340,740,'HB',15,True); txt(c,388,754,'Horizon Bank PLC',16,True)
  txt(c,388,738,'Fictional banking institution',8); txt(c,388,723,'Registered office: Test City',8)
  txt(c,330,699,'STATEMENT OF ACCOUNT',12,True)
  txt(c,330,682,'CURRENCY: LKR',8,True); txt(c,330,669,'CREDIT BALANCES INDICATED AS Cr',7)
  txt(c,31,632,f'ACCOUNT NO: TEST-ACCT-{case}',9,True,'Courier-Bold')
  txt(c,31,616,f'CUSTOMER ID: TEST-ID-{case}',8)
  txt(c,372,632,f'PAGE_NO: {n}',9,font='Courier-Bold')
  txt(c,78,590,f'FOR THE PERIOD: 01-{month:02d}-2026 TO {g.calendar.monthrange(2026,month)[1]}-{month:02d}-2026',8,True,'Courier-Bold')
  txt(c,405,610,'DATE: 01-10-2026',8,True,'Courier-Bold')
  # Continuous pale salmon ruling resembles the supplied statement layout.
  for i in range(34):
   c.setFillColor(H('#ffe6df') if i%2==0 else H('#fff5f2')); c.rect(24,549-i*14,547,12,fill=1,stroke=0)
  c.setFillColor(H('#ffffff')); c.setStrokeColor(H('#777777')); c.setLineWidth(.4); c.rect(24,563,547,20,fill=1,stroke=1)
  xs=[34,105,356,428,563]
  for x,s in zip([34,105,315,389,481],['DATE','PARTICULARS','DEBITS','CREDITS','BALANCE']): txt(c,x,570,s,8,True)
  opening=balance; rows=g.transactions(case,month,inc,debt,kind); credits=debits=g.D(0)
  display=[(f'01{g.calendar.month_abbr[month].upper()}2026','B/F','','',g.money(balance)+'Cr')]
  for r in rows:
   dr=g.D(r['debit_lkr'].replace(',','')); cr=g.D(r['credit_lkr'].replace(',','')); balance+=cr-dr; credits+=cr; debits+=dr; r['balance_lkr']=str(balance)
   d=f'{int(r["date"][-2:]):02d}{g.calendar.month_abbr[month].upper()}2026'
   display.append((d,r['particulars'],g.money(dr) if dr else '',g.money(cr) if cr else '',g.money(balance)+'Cr'))
  display.append((f'{g.calendar.monthrange(2026,month)[1]}{g.calendar.month_abbr[month].upper()}2026','C/F','','',g.money(balance)+'Cr'))
  y=552
  for date,label,dr,cr,bal in display:
   txt(c,34,y,date,7,font='Courier-Bold'); txt(c,105,y,label,7,font='Courier-Bold')
   c.setFont('Courier-Bold',7); c.setFillColor(INK)
   for x,v in zip([375,454,563],[dr,cr,bal]): c.drawRightString(x,y,v)
   y-=14
  c.setStrokeColor(INK); c.setDash(2,2); c.line(101,y+6,563,y+6); c.setDash()
  txt(c,105,y-8,'TOTAL:',8,True,'Courier-Bold'); c.setFont('Courier-Bold',8); c.drawRightString(375,y-8,g.money(debits)); c.drawRightString(454,y-8,g.money(credits))
  txt(c,30,57,'Please check all entries. This is a computer-generated synthetic statement.',7)
  months.append(dict(month=f'2026-{month:02d}',opening_lkr=str(opening),credits_lkr=str(credits),debits_lkr=str(debits),closing_lkr=str(balance),transactions=rows))
  assert opening+credits-debits==balance
  c.showPage()
 c.save(); return months

def panel(c,x,y,w,h,title):
 c.setStrokeColor(GREY); c.setLineWidth(.55); c.rect(x,y-h,w,h,fill=0,stroke=1)
 c.setFillColor(H('#f5f5f5')); c.rect(x,y-23,w,23,fill=1,stroke=0); txt(c,x+10,y-15,title,10,True)

def bureau(case,incomes,debt,kind):
 facilities,ident=OLD_CRIB(case,incomes,debt,kind)
 original=PdfReader(g.ROOT/case/'crib_style_report.pdf'); buf=io.BytesIO(); c=canvas.Canvas(buf,pagesize=A4)
 bureau_page(c,'',case,'',1,3)
 txt(c,30,739,'Searched Name:',8,True); txt(c,112,739,f'TEST APPLICANT {case if kind!="identity" else "OTHER PERSON"}',9,True)
 txt(c,30,722,'Searched ID No:',8,True); txt(c,112,722,ident,9)
 txt(c,30,700,'Dashboard',12,True)
 panel(c,30,686,256,77,'Scoring'); panel(c,300,686,265,77,'Disputes')
 txt(c,44,644,'N/A',16,True); txt(c,106,644,'N/A',16,True); txt(c,173,644,'--',16,True)
 for x,s in [(44,'Score'),(106,'Risk grade'),(173,'Trend')]: txt(c,x,624,s,8)
 for x,value,label in [(313,'1' if kind=='dispute' else '0','Active disputes'),(402,'0','False disputes'),(489,'0','In court')]: txt(c,x,644,value,16,True); txt(c,x,624,label,7)
 panel(c,30,594,535,151,'Payment Profile Status')
 entries=[(42,554,'Open Credit Facility','2'),(309,554,'Closed Credit Facility','1'),(42,532,'Total Amount in Arrears',g.money(45000 if kind=='arrears' else 0)),(309,532,'Different Creditors','2'),(42,510,'Highest Current NDIA','95' if kind=='arrears' else '0'),(309,510,'Total Current Balance',g.money(sum(g.D(f['outstanding_lkr']) for f in facilities))),(42,488,'Highest NDIA in 12 Months','120' if kind=='arrears' else '0'),(309,488,'Total Installment Amount','Not reported' if kind=='missing' else g.money(debt))]
 for x,y,k,v in entries: txt(c,x,y,k,8); txt(c,x,y-12,v,9,True)
 panel(c,30,429,535,103,'Payment Profile')
 xs=[170,230,290,350,410,470]
 for x,m in zip(xs,['Apr','May','Jun','Jul','Aug','Sep']): txt(c,x,390,m,8)
 for row,y in enumerate([367,345]):
  txt(c,43,y-3,f'Loan {"A" if row==0 else "B"} | 2026',8)
  for x,days in zip(xs,[0,0,30,60,90,95] if row==0 and kind=='arrears' else [0]*6):
   c.setFillColor(H('#efbf66') if days else H('#72ae88')); c.circle(x+8,y,9,fill=1,stroke=0); txt(c,x+2,y-3,str(days),7,True)
 txt(c,31,312,'Payment profile values represent days in arrears; 0 = current.',7)
 panel(c,30,297,535,58,'Utilities'); txt(c,43,257,'Accounts: Not supplied',8); txt(c,310,257,'Arrears: Not supplied',8)
 panel(c,30,227,535,58,'Insurances'); txt(c,43,187,'Policies: Not supplied',8); txt(c,310,187,'Claims: Not supplied',8)
 panel(c,30,157,535,59,'Dishonoured Cheques'); txt(c,43,117,'Number of cheques: '+('1' if kind=='arrears' else '0'),8); txt(c,310,117,'Amount: Not supplied',8)
 txt(c,31,76,'Scores and grades are unassigned. Figures are fictional test records.',7)
 c.showPage(); c.save(); out=PdfWriter(); out.add_page(PdfReader(buf).pages[0])
 for p in original.pages[1:]: out.add_page(p)
 with (g.ROOT/case/'crib_style_report.pdf').open('wb') as f: out.write(f)
 return facilities,ident

OLD_PAGE=g.page; OLD_CRIB=g.crib_pdf
g.page=bureau_page; g.table=report_table; g.bank_pdf=bank; g.crib_pdf=bureau
if __name__=='__main__': g.main()
