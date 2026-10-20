"""Isolated acceptance API driver. Fixed loopback test host; never reads .env or real cases.
Run phases once in order; evidence is appended and preserved under ignored .tmp.
"""
import sys, json, pathlib, datetime, io, re
import requests

ROOT=pathlib.Path.cwd()
CASE='fac70000-0000-4000-8000-000000000001'
BASE='http://127.0.0.1:5764/api/workflow-governance/cases/'+CASE
run=sys.argv[2] if len(sys.argv)>2 else None
if run: assert re.fullmatch(r'[a-zA-Z0-9_-]+',run),'Use a simple isolated evidence-run name'
OUT=ROOT/(f'.tmp/stabilization-acceptance-runs/{run}/api-evidence.json' if run else '.tmp/stabilization-acceptance-final/api-evidence.json')
OUT.parent.mkdir(parents=True,exist_ok=True)
E=json.loads(OUT.read_text()) if OUT.exists() else {'label':'ISOLATED TEST ONLY; actual C4 deterministic engine; synthetic facts/policy/spatial coverage; no real approval or delivery','responses':[]}
metadata=requests.get('http://127.0.0.1:5764/acceptance',timeout=15).json()
assert metadata['caseId']==CASE and metadata['database'].startswith('stabilization_acceptance_')
assert E.get('database',metadata['database'])==metadata['database'],'Use a separate evidence-run name for a different isolated database'
E['database']=metadata['database']
def call(method,path='',body=None,actor='OFFICER',files=None,data=None,expect=200):
    r=requests.request(method,BASE+path,headers={'Authorization':'Bearer ISOLATED-TEST-'+actor},json=body,files=files,data=data,timeout=120)
    try: value=r.json()
    except ValueError: value={'bytes':len(r.content),'contentType':r.headers.get('Content-Type')}
    E['responses'].append({'method':method,'path':path,'testActor':actor,'status':r.status_code,'response':value})
    OUT.write_text(json.dumps(E,indent=2)+'\n')
    assert r.status_code==expect,(method,path,r.status_code,str(value)[:1800])
    return value
def get(path='',actor='OFFICER'): return call('GET',path,actor=actor)
def post(path,body=None,actor='OFFICER',**kwargs): return call('POST',path,body,actor,**kwargs)
def pdf(title):
    text='BT /F1 10 Tf 40 790 Td (ISOLATED TEST DOCUMENT - NOT REAL APPROVAL) Tj 0 -30 Td ('+title.replace('(','').replace(')','')+') Tj ET'
    objects=['<< /Type /Catalog /Pages 2 0 R >>','<< /Type /Pages /Kids [3 0 R] /Count 1 >>',
        '<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>',
        '<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>',f'<< /Length {len(text)} >>\nstream\n{text}\nendstream']
    doc='%PDF-1.4\n'; offsets=[]
    for i,obj in enumerate(objects): offsets.append(len(doc)); doc+=f'{i+1} 0 obj\n{obj}\nendobj\n'
    start=len(doc); doc+='xref\n0 6\n0000000000 65535 f \n'+''.join(f'{n:010} 00000 n \n' for n in offsets)
    return (doc+f'trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n{start}\n%%EOF\n').encode('ascii')
def upload(path,actor,data=None,title='Synthetic acceptance evidence'):
    return post(path,actor=actor,files={'file':('ISOLATED_TEST.pdf',pdf(title),'application/pdf')},data=data)
def future(days=30): return (datetime.datetime.now(datetime.timezone.utc)+datetime.timedelta(days=days)).isoformat().replace('+00:00','Z')

phase=sys.argv[1]
if phase=='correction':
    before=post('/completeness/documents',{})
    assert before['outcome']!='Complete'
    correction=post('/corrections',{'requiredAction':'UploadSupportingDocument','requiredClassificationCode':'SurveyPlan','officerInstructions':'ISOLATED TEST: supply synthetic survey evidence','dueDateUtc':future()})
    response=upload('/corrections/'+correction['id']+'/respond','APPLICANT',{'expectedRevision':correction['revision'],'applicantResponseNotes':'ISOLATED TEST submission'})
    assert response['status']=='SubmittedAwaitingReview'
    reviewed=post('/corrections/'+correction['id']+'/review',{'expectedRevision':response['revision'],'accept':True,'reason':'ISOLATED TEST exact version reviewed'})
    assert reviewed['status']=='AcceptedAwaitingReassessment'
    assessment=post('/completeness/documents',{})
    proposal=next(x for x in assessment['classifiedDocuments'] if x['documentBinding']['governedDocumentId']=='fac70000-0000-4000-8000-000000000004')
    assessment=post('/completeness/documents/'+assessment['id']+'/review',{'classifiedDocumentId':proposal['id'],'documentId':proposal['documentBinding']['governedDocumentId'],
        'expectedAssessmentRevision':assessment['revision'],'decision':'Confirmed','reason':None})
    assert assessment['outcome']=='Complete',assessment
    completeness=get('/completeness'); p=completeness['proposalContent']
    detail=get()
    confirmed=post('/completeness/proposal/'+p['id']+'/confirm',{'expectedCaseRevision':detail['revision'],'notes':'ISOLATED TEST reviewed synthetic template'})
    assert confirmed['isConfirmed']
elif phase=='correction-review-resume':
    completeness=get('/completeness'); assessment=completeness['documentCompleteness']
    proposal=next(x for x in assessment['classifiedDocuments'] if x['documentBinding']['governedDocumentId']=='fac70000-0000-4000-8000-000000000004')
    assessment=post('/completeness/documents/'+assessment['id']+'/review',{'classifiedDocumentId':proposal['id'],'documentId':proposal['documentBinding']['governedDocumentId'],
        'expectedAssessmentRevision':assessment['revision'],'decision':'Confirmed','reason':None})
    assert assessment['outcome']=='Complete'
    detail=get(); p=completeness['proposalContent']
    confirmed=post('/completeness/proposal/'+p['id']+'/confirm',{'expectedCaseRevision':detail['revision'],'notes':'ISOLATED TEST reviewed synthetic template'})
    assert confirmed['isConfirmed']
elif phase=='screening':
    detail=get(); readiness=get('/screening')
    assert not readiness['missingInputs'],readiness
    state=post('/screening',{'verifiedFactSnapshotId':detail['currentVerifiedFactSnapshotId'],'expectedCaseRevision':detail['revision']})
    assert state['isCurrent'],state
    result=state['latestAssessment']['assessment']; assert result['provenance']['isDevelopmentFixture']
    assert result['overallStatus'] not in ['Blocked','InsufficientInformation'],result
    print(json.dumps({'assessmentId':result['assessmentId'],'overallStatus':result['overallStatus'],'proposalVersion':result['proposalVersion'],'actualEngine':result['provenance']}))
elif phase=='planning':
    detail=get()
    state=post('/workflow-plan/generate',{'verifiedFactSnapshotId':detail['currentVerifiedFactSnapshotId'],'expectedCaseRevision':detail['revision']})
    assert state['activePlan']['status']=='Draft'
    state=post('/workflow-plan/'+state['activePlan']['id']+'/approve',{'verifiedFactSnapshotId':detail['currentVerifiedFactSnapshotId'],'expectedCaseRevision':state['caseRevision'],'expectedPlanRevision':state['activePlan']['revision']})
    assert state['activePlan']['status']=='Approved'
    state=post('/workflow-execution/start',{'planId':state['activePlan']['id'],'expectedPlanRevision':state['activePlan']['revision'],'expectedCaseRevision':state['caseRevision']})
    assert state['status']=='Active'
elif phase=='execution':
    def stage(code): return next(x for x in get('/workflow-execution')['stages'] if x['code']==code)
    def decide(code,outcome,revision=False):
        s=stage(code); actor=s['institution']; path='/institutional-requests/'+s['id']; e=get('/workflow-execution')
        if not revision: post(path+'/start',{'expectedExecutionRevision':e['revision']},actor)
        doc=upload(path+'/documents',actor,title='ISOLATED TEST '+code+' '+outcome)
        e=get('/workflow-execution'); s=stage(code)
        return post(path+'/decision',{'expectedExecutionRevision':e['revision'],'outcome':outcome,'reason':'ISOLATED TEST explicit '+outcome,
            'conditions':['ISOLATED TEST fulfill reviewed condition'] if outcome=='ApprovedWithConditions' else [],
            'documents':[{'documentId':doc['documentId'],'versionId':doc['versionId']}],
            'priorDecisionId':s['decision']['id'] if revision else None,'revisionReason':'ISOLATED TEST corrected evidence review' if revision else None},actor)
    decide('CABINET','Approved'); decide('EXTERNAL_TEST_A','Rejected')
    state=get('/workflow-execution'); ds=stage('DS')
    post('/institutional-requests/'+ds['id']+'/start',{'expectedExecutionRevision':state['revision']},'DS',expect=409)
    progress=get('/workflow-progress',actor='APPLICANT'); assert progress['blockingRejections']
    rejected=call('GET','/workflow-execution/rejected',actor='COMMISSIONER') if False else requests.get('http://127.0.0.1:5764/api/workflow-governance/cases/workflow-execution/rejected',headers={'Authorization':'Bearer ISOLATED-TEST-COMMISSIONER'}).json()
    assert CASE in rejected
    E['commissionerRejectedCases']=rejected
    decide('EXTERNAL_TEST_A','ApprovedWithConditions',True)
    e=get('/workflow-execution')
    post('/workflow-execution/departments',{'expectedExecutionRevision':e['revision'],'expectedCaseRevision':e['caseRevision'],'institution':'TEST_B','reason':'ISOLATED TEST DS requested additional review'},'DS')
    e=get('/workflow-execution')
    post('/workflow-execution/plans/'+e['activePlanId']+'/approve',{'expectedCaseRevision':e['caseRevision'],'expectedPlanRevision':e['activePlanRevision'],'expectedExecutionRevision':e['revision']})
    decide('EXTERNAL_TEST_B','Approved'); decide('DS','Approved'); decide('PROVINCIAL_LC','Rejected'); decide('COMMISSIONER','Approved')
    e=get('/workflow-execution'); assert e['status']=='Completed'
    assert len(e['planHistory'])==1
elif phase=='fulfillment':
    state=get('/fulfillment')
    requirements=[{'code':r['code'],'dueAtUtc':future(),'actualTitle':'ISOLATED TEST TITLE '+r['code'] if r['titlePending'] else None,
                   'actualTitleSourceReference':'ISOLATED TEST ONLY; not owner approved' if r['titlePending'] else None} for r in state['unconfigured']]
    state=post('/fulfillment/configure',{'expectedCaseRevision':state['caseRevision'],'version':'ISOLATED_TEST_1','requirements':requirements})
    for r in state['requirements']:
        if r['code']=='DS_RELEVANT_LAND_REPORT':
            ds=next(s for s in get('/workflow-execution')['stages'] if s['code']=='DS')
            post('/fulfillment/'+r['id']+'/lease-recommendation',{'expectedRevision':r['revision'],'dsDecisionId':ds['decision']['id'],'recommendLease':True,'reason':'ISOLATED TEST DS evidence'},'DS')
            r=next(x for x in get('/fulfillment')['requirements'] if x['id']==r['id'])
        upload('/fulfillment/'+r['id']+'/submit',r['institution'],{'expectedRevision':r['revision'],'notes':'ISOLATED TEST upload'})
        r=next(x for x in get('/fulfillment')['requirements'] if x['id']==r['id'])
        if r['code']=='DS_RELEVANT_LAND_REPORT':
            for institution in ['PROVINCIAL_LC','COMMISSIONER']:
                post('/fulfillment/'+r['id']+'/forward-receipt',{'expectedRevision':r['revision'],'reason':'ISOLATED TEST exact version receipt'},institution)
                r=next(x for x in get('/fulfillment')['requirements'] if x['id']==r['id'])
        post('/fulfillment/'+r['id']+'/review',{'expectedRevision':r['revision'],'accept':True,'reason':'ISOLATED TEST explicit review'})
    state=get('/fulfillment')
    state=post('/fulfillment/final-record',{'expectedCaseRevision':state['caseRevision']})
    for condition in state['conditions']:
        state=get('/fulfillment')
        post('/fulfillment/conditions/'+condition['id']+'/fulfill',{'expectedCaseRevision':state['caseRevision'],'reason':'ISOLATED TEST condition evidence review','evidenceReference':'test:source:condition'},condition['institution'])
    state=get('/fulfillment'); assert state['readyForHandoff']
    handoff=get('/handoff'); assert not handoff['canPrepare'] # No actual downstream contract is fabricated.
elif phase=='retrieve':
    e=get('/workflow-execution'); f=get('/fulfillment'); p=get('/workflow-plan'); s=get('/screening')
    assert e['status']=='Completed' and s['isCurrent'] and f['readyForHandoff']
    assert len(p['history'])==2 and len(e['planHistory'])==1
    assert any(len(x['decisionHistory'])==1 for x in e['stages'])
    E['restartRetrieval']={'executionId':e['executionId'],'executionRevision':e['revision'],'planId':e['planId'],
        'assessmentId':s['latestAssessment']['assessment']['assessmentId'],'fulfilledRequirements':len(f['requirements']),
        'plans':len(p['history']),'decisionRevisions':sum(len(x['decisionHistory']) for x in e['stages'])}
elif phase=='permissions':
    call('GET','',actor='UNKNOWN',expect=401)
    call('GET','',actor='TEST_A',expect=403)
    call('GET','/screening',actor='APPLICANT',expect=403)
    e=get('/workflow-execution'); other=next(s for s in e['stages'] if s['institution']=='TEST_B')
    call('GET','/institutional-requests/'+other['id'],actor='TEST_A',expect=403)
    post('/workflow-execution/start',{'planId':e['planId'],'expectedPlanRevision':e['activePlanRevision'],'expectedCaseRevision':e['caseRevision']},'APPLICANT',expect=403)
    cabinet=next(s for s in e['stages'] if s['institution']=='CABINET')
    stale=post('/institutional-requests/'+cabinet['id']+'/start',{'expectedExecutionRevision':0},'CABINET',expect=409)
    assert 'revision is stale' in stale.get('detail','')
else: raise ValueError(phase)
E.setdefault('completedPhases',[]).append(phase); OUT.write_text(json.dumps(E,indent=2)+'\n')
print('PASS ISOLATED TEST PHASE:',phase)
