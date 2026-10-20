"""Render the explicit synthetic proposal source; no API, database, OCR or AI calls.
Requires PyMuPDF, Pillow and numpy. Run from the repository root.
"""
from pathlib import Path
import csv, hashlib, html, io, json
import numpy as np
import pymupdf as fitz
from PIL import Image

ROOT=Path("output/pdf/synthetic-proposal-12-acre")
source=json.loads((ROOT/"proposal-source.json").read_text(encoding="utf8"))
regular=Path("C:/Windows/Fonts/segoeui.ttf")
bold=Path("C:/Windows/Fonts/segoeuib.ttf")
fonts={"regular":fitz.Font(fontfile=str(regular)),"bold":fitz.Font(fontfile=str(bold))}
W,H=595.276,841.89
INK=(.12,.19,.17); GREEN=(.12,.30,.25)

def wrap(text,width,size,weight="regular"):
    result=[]
    for para in str(text).split("\n"):
        line=""
        for word in para.split():
            candidate=(line+" "+word).strip()
            if fonts[weight].text_length(candidate,fontsize=size)>width and line:
                result.append(line);line=word
            else:line=candidate
        result.append(line)
    return result

def text(page,x,y,value,size=10,weight="regular",color=INK):
    page.insert_text((x,y),str(value),fontname="Fbold" if weight=="bold" else "Fregular",fontsize=size,color=color)

def draw(number, spec, scale):
    doc=fitz.open();p=doc.new_page(width=W,height=H)
    p.insert_font(fontname="Fregular",fontfile=str(regular));p.insert_font(fontname="Fbold",fontfile=str(bold))
    p.draw_rect(fitz.Rect(0,0,W,36),color=None,fill=GREEN)
    text(p,44,23,source["label"],9.3,"bold",(1,1,1))
    text(p,44,60,"LANTERN FIELD | SYNTHETIC PROPOSAL",9,"bold",GREEN)
    text(p,44,87,spec["title"],17,"bold")
    y=104.;size=10.2*scale;lead=size*1.35;width=W-88
    for block in spec["blocks"]:
        kind=block["kind"]
        if kind=="heading":
            y+=8
            for line in wrap(block["text"],width,11.1*scale,"bold"):
                text(p,44,y+11,line,11.1*scale,"bold",GREEN);y+=15*scale
            y+=3
        elif kind=="paragraph":
            for line in wrap(block["text"],width,size):text(p,44,y+size,line,size);y+=lead
            y+=7
        elif kind=="table":
            rows=[block["headers"]]+block["rows"];n=len(rows[0]);colWidths=([width*.32,width*.68] if n==2 else [width/n]*n);tableSize=9.3*scale;tableLead=tableSize*1.3
            for i,row in enumerate(rows):
                cells=[wrap(cell,colWidths[j]-12,tableSize,"bold" if i==0 else "regular") for j,cell in enumerate(row)]
                height=max(map(len,cells))*tableLead+12
                p.draw_rect(fitz.Rect(44,y,W-44,y+height),color=(.77,.81,.79),fill=(.91,.94,.92) if i==0 else ((.975,.98,.975) if i%2 else (1,1,1)),width=.5)
                x=44
                for j,lines in enumerate(cells):
                    if j:p.draw_line((x,y),(x,y+height),color=(.80,.83,.81),width=.5)
                    for k,line in enumerate(lines):text(p,x+6,y+7+tableSize+k*tableLead,line,tableSize,"bold" if i==0 else "regular")
                    x+=colWidths[j]
                y+=height
            y+=9
        elif kind=="diagram":
            boxHeight=58*scale
            for row in block["boxes"]:
                for j,value in enumerate(row):
                    x=44+j*width/2
                    p.draw_rect(fitz.Rect(x,y,x+width/2-6,y+boxHeight),color=GREEN,fill=(.94,.96,.94),width=.9)
                    for k,line in enumerate(value.split("\n")):text(p,x+12,y+22+k*14*scale,line,10*scale,"bold" if k==0 else "regular")
                y+=boxHeight+7
            y+=6
    if y>790:doc.close();return None,y
    p.draw_line((44,802),(W-44,802),color=(.7,.75,.72),width=.6)
    text(p,44,818,"Version 1.0 | 19 October 2026 | Research artefact; not legal evidence",8)
    text(p,W-99,818,f"{number} / {len(source['pages'])}",8,"bold")
    return doc,y

native=fitz.open();layout=[]
for index,spec in enumerate(source["pages"],1):
    for scale in [1,.98,.96,.94,.92,.90]:
        page,y=draw(index,spec,scale)
        if page is not None:break
    if page is None:raise RuntimeError(f"Page {index} exceeds bounds: {y}")
    native.insert_pdf(page);page.close();layout.append({"page":index,"fontScale":scale,"bodyBottom":round(y,2)})
vector=ROOT/"SYNTHETIC_RESEARCH_12_ACRE_text.pdf";native.save(vector,garbage=4,deflate=True)
scan=fitz.open();previews=Path(".tmp/synthetic-proposal-preview");previews.mkdir(exist_ok=True)
rng=np.random.default_rng(20261019)
for i,page in enumerate(native):
    pix=page.get_pixmap(matrix=fitz.Matrix(200/72,200/72),colorspace=fitz.csGRAY,alpha=False)
    data=np.frombuffer(pix.samples,dtype=np.uint8).reshape(pix.height,pix.width).astype(np.int16)
    # Very subtle paper texture; no skew/blur or simulated handwritten signature.
    data=np.clip(data-2+rng.integers(-1,2,size=data.shape,dtype=np.int16),0,255).astype(np.uint8)
    im=Image.fromarray(data);stream=io.BytesIO();im.save(stream,format="JPEG",quality=90,dpi=(200,200))
    target=scan.new_page(width=W,height=H);target.insert_image(target.rect,stream=stream.getvalue())
    if i in [0,4,7,10]:im.resize((827,1170)).save(previews/f"page-{i+1}.png")
scanned=ROOT/"SYNTHETIC_RESEARCH_12_ACRE_scanned.pdf";scan.save(scanned,garbage=4,deflate=True)
assert all(len(p.get_text().strip())==0 for p in scan),"Scanned PDF must be image-only for manual OCR testing"
assert all(source["label"] in p.get_text() for p in native),"Every text source page must carry the synthetic label"
# Check arithmetic, coverage identity and dimensions without feeding answers into any service.
c=source["financialChecks"]
assert sum(c["initialCapitalItems"])==c["establishmentCapital"]
assert c["establishmentCapital"]+c["openingWorkingCapital"]==c["initialInvestment"]==c["financingEquity"]+c["loans"]+c["grants"]
assert sum(c["monthlyCosts"])==c["monthlyTotal"] and c["monthlyTotal"]*12==c["annualTotal"] and c["monthlyTotal"]*6==c["openingWorkingCapital"]
assert sum(c["acreAllocations"])==12 and 108000*100+32000*100==c["annualSales"]
assert len(source["coverage"])==37 and len(source["expected17Fields"])==17
# Editable HTML mirrors each page/section and table; source JSON is also fully editable.
parts=["<!doctype html><html lang='en'><meta charset='utf-8'><title>Synthetic 12-acre proposal - editable source</title><style>@page{size:A4;margin:14mm}body{font:10pt 'Segoe UI',sans-serif;color:#20352d}article{break-after:page}header{background:#214e40;color:white;padding:9px;font-weight:bold;font-size:10pt}h1{font-size:18pt}h2{font-size:12pt;color:#214e40}p{line-height:1.4}table{border-collapse:collapse;width:100%;margin:8px 0}th,td{border:1px solid #bdc9c1;padding:6px;text-align:left;vertical-align:top}th{background:#edf3ef}.diagram td{height:55px;background:#f4f7f3}footer{font-size:8pt;border-top:1px solid #ccc;margin-top:12px;padding-top:8px}</style>"]
for i,spec in enumerate(source["pages"],1):
    parts.extend(["<article><header>"+html.escape(source["label"])+"</header><h1>"+html.escape(spec["title"])+"</h1>"])
    for block in spec["blocks"]:
        if block["kind"]=="heading":parts.append("<h2>"+html.escape(block["text"])+"</h2>")
        elif block["kind"]=="paragraph":parts.append("<p>"+html.escape(block["text"]).replace("\n","<br>")+"</p>")
        elif block["kind"]=="table":parts.append("<table><thead><tr>"+"".join("<th>"+html.escape(str(c))+"</th>" for c in block["headers"])+"</tr></thead><tbody>"+"".join("<tr>"+"".join("<td>"+html.escape(str(c))+"</td>" for c in row)+"</tr>" for row in block["rows"])+"</tbody></table>")
        else:parts.append("<table class='diagram'>"+"".join("<tr>"+"".join("<td>"+html.escape(c).replace("\n","<br>")+"</td>" for c in row)+"</tr>" for row in block["boxes"])+"</table>")
    parts.append(f"<footer>Version 1.0 | 19 October 2026 | {i} / 11 | Synthetic research only</footer></article>")
(ROOT/"proposal-editable.html").write_text("\n".join(parts)+"</html>",encoding="utf8")
coverage=["# Requirement coverage — synthetic research proposal","","Active template: STD-PROP-2026-V1 / 1.0 (37 requirements). Page numbers below refer to both supplied PDFs; semantic grouping is not legal approval.","","| # | Requirement ID | Page | Section / treatment |","| --- | --- | --- | --- |"]
with (ROOT/"requirement-coverage.csv").open("w",encoding="utf-8-sig",newline="") as f:
    writer=csv.writer(f);writer.writerow(["order","requirementId","page","section"])
    for index,(code,binding) in enumerate(source["coverage"].items(),1):
        writer.writerow([index,code,binding["page"],binding["section"]]);coverage.append(f"| {index} | {code} | {binding['page']} | {binding['section']} |")
coverage.extend(["","Conditional treatment: existing business registration (p2) and additional future buildings (p3) have explicit inapplicability declarations. Initial buildings (p2), raw materials (p5) and domestic marketing (p6) have concrete details. Foreign-market marketing is explicitly inapplicable (p6). All observations still require actual OCR/grounding and officer review. Synthetic NIC/contact/typed-signature values must never be confirmed as genuine government identity or legal execution."])
(ROOT/"requirement-coverage.md").write_text("\n".join(coverage)+"\n",encoding="utf8")
(ROOT/"expected-17-fields.json").write_text(json.dumps(source["expected17Fields"],indent=2,ensure_ascii=False)+"\n",encoding="utf8")
fieldPages=[1,1,1,1,2,2,2,2,2,2,1,8,8,5,1,11,11]
fields=["# Expected extraction values — comparison guide only","","These are expected candidates for a manual OCR/Gemini test, not actual extraction results, verified facts or runtime seeds. Wording may vary; preserve exact quantities and source pages. Do not paste this file into the database.","","| Field | Expected value | Primary page |","| --- | --- | --- |"]
for (code,value),page in zip(source["expected17Fields"].items(),fieldPages):fields.append(f"| {code} | {value} | {page} |")
fields.extend(["","C4 normalization: 12 acres = 4.85622770688 hectares; 15 years; investment 12000000 LKR. ProposalVersion must remain the uploaded document-version UUID, not this reference or a fingerprint. C4 source pages must come from the actual reviewed run. Province/GN, parcel/evidence coverage and effective policy versions come from the separately selected scenario, not additional mandatory extraction fields.","","The legal/statutory-basis answer explicitly states that no verified law or approval is claimed. Do not turn that into an invented citation. If the extractor returns NoCandidate or Unsupported here, inspect the source and preserve that distinction; this field is not automatically a mandatory proposal section."])
(ROOT/"expected-17-fields.md").write_text("\n".join(fields)+"\n",encoding="utf8")
manifest={"label":source["label"],"pageCount":len(native),"scannedDpi":200,"scannedImageOnly":True,"coverageCount":37,"expectedFieldCount":17,"arithmeticChecksPassed":True,"layout":layout,"artifacts":{p.name:{"bytes":p.stat().st_size,"sha256":hashlib.sha256(p.read_bytes()).hexdigest()} for p in [vector,scanned]}}
(ROOT/"artifact-validation.json").write_text(json.dumps(manifest,indent=2)+"\n",encoding="utf8")
print(json.dumps(manifest,indent=2))
