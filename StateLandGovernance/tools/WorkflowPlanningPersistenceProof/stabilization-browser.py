"""Actual isolated Next.js UI against the acceptance API; no network interception or mock responses."""
from playwright.sync_api import sync_playwright
import json, pathlib, requests, re

CASE='fac70000-0000-4000-8000-000000000001'
ROOT=pathlib.Path('.tmp/stabilization-acceptance-final'); ROOT.mkdir(exist_ok=True,parents=True)
proof={'label':'ISOLATED TEST ONLY; actual Next.js browser and C3/C4 API; test identities and mock spatial evidence','caseId':CASE,'assertions':{}}
headers={'Authorization':'Bearer ISOLATED-TEST-OFFICER'}
execution=requests.get('http://127.0.0.1:5764/api/workflow-governance/cases/'+CASE+'/workflow-execution',headers=headers).json()
with sync_playwright() as p:
    browser=p.chromium.launch(headless=True,channel='msedge')
    context=browser.new_context(viewport={'width':1500,'height':1100})
    context.add_init_script("if(!sessionStorage.getItem('component01.landIntelligence.officerAccessToken'))sessionStorage.setItem('component01.landIntelligence.officerAccessToken','ISOLATED-TEST-OFFICER');sessionStorage.setItem('component01.landIntelligence.applicantAccessToken','ISOLATED-TEST-APPLICANT');")
    page=context.new_page(); errors=[]; posts=[]; apis=[]
    page.on('pageerror',lambda e:errors.append(str(e)))
    page.on('request',lambda r:posts.append(r.url) if r.method=='POST' else None)
    page.on('response',lambda r:apis.append({'url':r.url,'status':r.status}) if '/api/workflow-governance/' in r.url else None)
    page.goto('http://127.0.0.1:3001/workflow-governance/cases/'+CASE,wait_until='networkidle')
    jurisdiction=page.get_by_text('Jurisdiction:',exact=True).locator('..')
    jurisdiction.wait_for()
    proof['assertions']['missingJurisdictionIsNotInvented']='Not recorded' in jurisdiction.inner_text() and 'Western' not in jurisdiction.inner_text()
    page.get_by_role('button',name='C4 Screening',exact=True).click()
    page.get_by_text('Development mock spatial inputs; ISOLATED TEST ONLY',exact=False).first.wait_for()
    proof['assertions']['mockSpatialLabelVisible']=True
    page.screenshot(path=str(ROOT/'browser-screening.png'),full_page=True)
    page.get_by_role('button',name='Workflow Plan',exact=True).click()
    page.get_by_text('CABINET',exact=False).first.wait_for()
    proof['assertions']['cabinetAndPersistedPlanVisible']='PROVINCIAL_LC' in page.inner_text('body')
    page.screenshot(path=str(ROOT/'browser-plan.png'),full_page=True)
    page.get_by_role('button',name='Institutional execution',exact=True).click()
    page.get_by_text('Completed',exact=False).first.wait_for()
    text=page.inner_text('body')
    proof['assertions']['executionCompletedAndAddedDepartmentVisible']='TEST_B' in text and 'Completed' in text
    proof['assertions']['decisionRevisionAndRegionalRejectionHistoryVisible']='Rejected' in text and 'ApprovedWithConditions' in text
    proof['assertions']['regionalNonRecommendationDoesNotClaimAnOngoingRejectionGate']="Regional non-recommendation retained" in text and "Required dependent approvals remain blocked" not in text
    page.screenshot(path=str(ROOT/'browser-execution.png'),full_page=True)
    page.get_by_role('button',name='Document fulfillment',exact=True).click()
    page.get_by_text('Actual downstream recipient contract/version and authenticated transport are not configured.',exact=True).wait_for()
    proof['assertions']['fulfillmentAndTruthfulHandoffBlockVisible']='Fulfilled' in page.inner_text('body')
    page.screenshot(path=str(ROOT/'browser-fulfillment.png'),full_page=True)
    # Switch the isolated session to an external institutional identity; full workspace access must fail.
    stage=next(s for s in execution['stages'] if s['institution']=='TEST_A')
    page.evaluate("sessionStorage.setItem('component01.landIntelligence.officerAccessToken','ISOLATED-TEST-TEST_A')")
    page.goto('http://127.0.0.1:3001/workflow-governance/institutional/'+CASE+'/'+stage['id'],wait_until='networkidle')
    page.get_by_role('heading',name='Restricted institutional request',exact=True).wait_for()
    page.get_by_role('heading',name=re.compile(r'^TEST_A')).wait_for()
    text=page.inner_text('body')
    proof['assertions']['restrictedInstitutionViewVisible']='TEST_A' in text and 'TEST_B' not in text
    page.screenshot(path=str(ROOT/'browser-institution.png'),full_page=True)
    proof['pageErrors']=errors; proof['viewingPostRequests']=posts; proof['apiResponses']=apis
    browser.close()
proof['assertions']['noPostsWhileViewing']=not posts
(ROOT/'browser-evidence.json').write_text(json.dumps(proof,indent=2)+'\n')
print(json.dumps(proof['assertions']))
assert all(proof['assertions'].values()),proof
