"""Audit and prepare Wolf's supplied synthetic workflow CSVs; never fit a model.
Usage: python prepare_synthetic_v1.py INPUT_ZIP OUTPUT_DIRECTORY
Python 3.10+, standard library only. Existing output directories are rejected.
"""
import collections as C
import csv
import datetime as dt
import hashlib
import io
import json
import math
from pathlib import Path
import re
import shutil
import statistics
import sys
import zipfile

FEATURES = ['event_count','elapsed_days','max_gap_hours','mean_gap_hours',
            'unique_activities','repeated_activity_count','resource_handoffs',
            'institution_switches','distinct_resources']
SUFFIX = re.compile(r' \((Repeated|Re-submission|Correction Requested)\)$')
SEED = 'wolf-synthetic-v1-20261008'
EXPECTED = {'case_features.csv','event_log.csv','routing_config.json'}

def sha(b): return hashlib.sha256(b).hexdigest()
def readcsv(b): return list(csv.DictReader(io.StringIO(b.decode('utf-8-sig'))))
def writecsv(path, rows, cols):
    with path.open('w', encoding='utf-8', newline='') as f:
        w=csv.DictWriter(f,fieldnames=cols); w.writeheader(); w.writerows(rows)
def dump(path,obj): path.write_text(json.dumps(obj,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def canon(a): return SUFFIX.sub('',a)
def stamp(s): return dt.datetime.fromisoformat(s)
def vector(events,canonical=False):
    times=[stamp(e['timestamp']) for e in events]
    gaps=[(b-a).total_seconds()/3600 for a,b in zip(times,times[1:])]
    activities=[canon(e['activity']) if canonical else e['activity'] for e in events]
    resources=[(e['institution'],e['resource']) if canonical else e['resource'] for e in events]
    return dict(zip(FEATURES,[len(events),(times[-1]-times[0]).total_seconds()/86400,
        max(gaps),statistics.mean(gaps),len(set(activities)),len(events)-len(set(activities)),
        sum(a!=b for a,b in zip(resources,resources[1:])),
        sum(a['institution']!=b['institution'] for a,b in zip(events,events[1:])),len(set(resources))]))

def main(src,out):
    src=Path(src);out=Path(out)
    if out.exists(): raise FileExistsError('Refusing to overwrite '+str(out))
    if sha(src.read_bytes())!='adf77147bdfae5b1149a72e6d70766778d99ae291369c0953ff1485dd039568c':
        raise ValueError('Input ZIP differs from audited release; inspect and version it separately')
    with zipfile.ZipFile(src) as z:
        if set(z.namelist())!=EXPECTED: raise ValueError('Unexpected ZIP entries; inspect before processing')
        raw={n:z.read(n) for n in sorted(EXPECTED)}
    cases=readcsv(raw['case_features.csv']);events=readcsv(raw['event_log.csv']);config=json.loads(raw['routing_config.json'])
    assert len(cases)==500 and len(events)==8242, 'This preparation release targets the audited input'
    assert len({c['case_id'] for c in cases})==len(cases)
    assert len({(e['case_id'],e['event_seq']) for e in events})==len(events)
    assert all(all(str(v).strip() for v in e.values()) for e in events)
    groups=C.defaultdict(list)
    for e in events: groups[e['case_id']].append(e)
    assert set(groups)=={c['case_id'] for c in cases}
    out.mkdir(parents=True);(out/'originals').mkdir();(out/'audit').mkdir();(out/'prepared').mkdir();(out/'splits').mkdir()
    for n,b in raw.items(): (out/'originals'/n).write_bytes(b)
    shutil.copyfile(__file__,out/'prepare_synthetic_v1.py')
    issues=[];reconciliation=[];catalog=[];features=[];accepted_events=[];quarantine_events=[];fingerprints={};prefixes={}
    core=['DS Application Received','DS Initial Review','PLC/DLC Review','PLC/DLC Recommendation','LCGD Case Intake','LCGD Review','LCGD Lease Decision','DS Final Notification']
    for c in sorted(cases,key=lambda c:c['case_id']):
        cid=c['case_id']; es=sorted(groups[cid],key=lambda e:int(e['event_seq']));times=[stamp(e['timestamp']) for e in es]
        assert [int(e['event_seq']) for e in es]==list(range(1,len(es)+1))
        assert all(t.tzinfo is None for t in times), 'Do not mix timezone interpretations'
        assert c['label'] in ['reference-normal','controlled-perturbation']
        assert (c['label']=='reference-normal')==(c['perturbation_type']=='none')
        assert c['legal_route'] in config['legal_routes'] and c['province'] in config['provinces']
        assert c['land_purpose'] in config['land_purposes'] and c['applicant_type'] in config['applicant_types']
        assert float(c['land_extent_perches'])>0
        for b in ['inter_provincial','required_higher_approval','registration_requirement']: assert c[b] in ['True','False']
        rawacts=[e['activity'] for e in es];required=core+[x+y for x in config['legal_routes'][c['legal_route']]['clearances'] for y in [' Requested',' Issued']]
        if c['required_higher_approval']=='True': required+=['Higher Approval Requested','Higher Approval Granted']
        if c['registration_requirement']=='True':required+=['Registration Requested','Registration Completed']
        assert all(a in rawacts for a in required)
        assert ('Higher Approval Granted' in rawacts)==(c['required_higher_approval']=='True')
        assert ('Registration Completed' in rawacts)==(c['registration_requirement']=='True')
        assert rawacts.count('DS Final Notification')==1
        reasons=[]
        for i,(a,b) in enumerate(zip(times,times[1:]),1):
            if b<a:
                reasons.append('NON_MONOTONIC_EVENT_TIME')
                issues.append(dict(case_id=cid,issue='NON_MONOTONIC_EVENT_TIME',event_seq=i+1,detail=f'Event {i+1} precedes event {i} by {(a-b).total_seconds()/3600:.9f} hours'))
        final_index=rawacts.index('DS Final Notification');final_time=times[final_index]
        for i,t in enumerate(times):
            if t>final_time:
                reasons.append('EVENT_AFTER_FINAL_NOTIFICATION')
                issues.append(dict(case_id=cid,issue='EVENT_AFTER_FINAL_NOTIFICATION',event_seq=i+1,detail='Case requires review: timestamp later than final notification; no timestamp repaired'))
        if final_index!=len(es)-1: reasons.append('FINAL_NOTIFICATION_NOT_LAST')
        supplied=vector(es)
        for k,v in supplied.items():
            tolerance=0.005000001 if k in ['elapsed_days','max_gap_hours','mean_gap_hours'] else 0
            if abs(float(c[k])-v)>tolerance:
                reconciliation.append(dict(case_id=cid,feature=k,supplied_value=c[k],recomputed_in_event_sequence=v,absolute_difference=abs(float(c[k])-v),case_quarantined=bool(reasons)))
        # Behaviour-based exact groups exclude case ID; offsets detect time-shifted exact clones.
        behaviour=[[e['activity'],e['institution'],e['resource'],round((t-times[0]).total_seconds(),6)] for e,t in zip(es,times)]
        fingerprint=sha(json.dumps(behaviour,separators=(',',':')).encode());fingerprints[cid]=fingerprint
        prefixes[cid]=sha(json.dumps([[e['activity'],e['institution'],e['resource'],e['timestamp']] for e in es[:2]]).encode())
        row={k:v for k,v in c.items() if k not in FEATURES}
        row.update(record_origin='user_supplied_synthetic',label_scope='generator_scenario_label_only',
                   timestamp_basis='naive_unspecified_timezone',source_parent_case_id='',source_family_id='',
                   lineage_status='not_supplied',exact_trace_group=fingerprint,
                   preparation_status='QUARANTINED' if reasons else 'ACCEPTED_SYNTHETIC',
                   exclusion_reasons=';'.join(sorted(set(reasons))))
        catalog.append(row)
        if reasons:
            quarantine_events+=es
        else:
            v=vector(es,True)
            assert all(math.isfinite(float(x)) and x>=0 for x in v.values())
            assert v['event_count']==v['unique_activities']+v['repeated_activity_count']
            features.append(dict(case_id=cid,**v))
            for e in es:
                m=SUFFIX.search(e['activity'])
                accepted_events.append(dict(e,activity_canonical=canon(e['activity']),simulation_marker=m.group(1) if m else '',record_origin='user_supplied_synthetic'))
    # No inferred parent claims. Exact clones would need group allocation before this release.
    assert len(set(fingerprints.values()))==len(cases), 'Duplicate trace group: revise splitting'
    assert len(set(prefixes.values()))==len(cases), 'Shared exact prefix: inspect lineage before splitting'
    accepted=[r for r in catalog if r['preparation_status']=='ACCEPTED_SYNTHETIC']
    quarantine=[r for r in catalog if r['preparation_status']=='QUARANTINED']
    assert len(accepted)==456 and len(quarantine)==44
    rank=lambda r:sha((SEED+fingerprints[r['case_id']]).encode())
    ref=sorted([r for r in accepted if r['label']=='reference-normal'],key=rank)
    assignments={r['case_id']:('train' if i<240 else 'validation' if i<320 else 'test') for i,r in enumerate(ref)}
    for typ in sorted({r['perturbation_type'] for r in accepted if r['label']=='controlled-perturbation'}):
        subset=sorted([r for r in accepted if r['perturbation_type']==typ],key=rank)
        cut=max(1,len(subset)//2)
        for i,r in enumerate(subset): assignments[r['case_id']]='validation' if i<cut else 'test'
    manifest=[]
    for r in catalog:
        manifest.append({k:r[k] for k in ['case_id','exact_trace_group','label','perturbation_type']}|{'split':assignments.get(r['case_id'],'quarantine'),'model_fit_eligible':assignments.get(r['case_id'])=='train'})
    featuremap={r['case_id']:r for r in features};eventcols=list(events[0]);catcols=list(catalog[0]);manifestcols=list(manifest[0])
    writecsv(out/'prepared/case_catalog.csv',catalog,catcols)
    writecsv(out/'prepared/case_features.csv',features,['case_id']+FEATURES)
    writecsv(out/'prepared/event_log.csv',accepted_events,eventcols+['activity_canonical','simulation_marker','record_origin'])
    writecsv(out/'audit/quarantined_cases.csv',quarantine,catcols)
    writecsv(out/'audit/quarantined_events.csv',quarantine_events,eventcols)
    writecsv(out/'audit/event_issues.csv',issues,['case_id','issue','event_seq','detail'])
    writecsv(out/'audit/feature_reconciliation.csv',reconciliation,['case_id','feature','supplied_value','recomputed_in_event_sequence','absolute_difference','case_quarantined'])
    writecsv(out/'splits/split_manifest.csv',manifest,manifestcols)
    for split in ['train','validation','test']:
        mr=sorted([r for r in manifest if r['split']==split],key=lambda r:r['case_id'])
        writecsv(out/f'splits/X_{split}.csv',[{k:featuremap[r['case_id']][k] for k in FEATURES} for r in mr],FEATURES)
        writecsv(out/f'splits/keys_{split}.csv',[{'case_id':r['case_id']} for r in mr],['case_id'])
        if split!='train':writecsv(out/f'splits/y_{split}.csv',[{'case_id':r['case_id'],'synthetic_anomaly_label':int(r['label']=='controlled-perturbation'),'perturbation_type':r['perturbation_type']} for r in mr],['case_id','synthetic_anomaly_label','perturbation_type'])
    defs={
      'event_count':'Number of accepted events in case.',
      'elapsed_days':'Last minus first timestamp in original event_seq order, divided by 86400 seconds.',
      'max_gap_hours':'Maximum adjacent timestamp difference in event_seq order / 3600.',
      'mean_gap_hours':'Arithmetic mean of adjacent timestamp differences / 3600.',
      'unique_activities':'Number of distinct activity_canonical values.',
      'repeated_activity_count':'event_count minus unique_activities; extra occurrences, not number of loops.',
      'resource_handoffs':'Adjacent changes of the (institution, resource) pair.',
      'institution_switches':'Adjacent changes of institution.',
      'distinct_resources':'Distinct (institution, resource) pairs; shared raw resource names are institution-scoped.'}
    dump(out/'feature_contract.json',dict(model_features=FEATURES,definitions=defs,scoring_scope='complete accepted synthetic case only; not validated for in-flight prefixes',normalization='Strip only terminal (Repeated), (Re-submission), (Correction Requested) in derived activity_canonical; preserve raw activity. This is generator-specific, not a production taxonomy.',excluded_inputs=['case_id','label','perturbation_type','simulation_marker','raw_activity_text','resource_identifiers','source_parent_case_id','source_family_id','exact_trace_group','split','preparation_status','legal_route','land_purpose','province','applicant_type','land_extent_perches','inter_provincial','required_higher_approval','registration_requirement'],missing_value_policy='Reject incomplete accepted input; do not impute from all splits.',precision='Unrounded recomputed floats; originals used 2 decimals.',timezone='Unspecified naive timestamps; no UTC assignment or business-calendar correction.'))
    splitcounts={s:dict(C.Counter(r['perturbation_type'] for r in manifest if r['split']==s)) for s in ['train','validation','test','quarantine']}
    stats=dict(status='PREPARED_FOR_SYNTHETIC_ONLY_EXPERIMENTS',source_zip_sha256=sha(src.read_bytes()),source_hashes={n:sha(b) for n,b in raw.items()},source_cases=len(cases),source_events=len(events),accepted_cases=len(accepted),accepted_events=len(accepted_events),quarantined_cases=len(quarantine),quarantined_events=len(quarantine_events),case_reason_counts=dict(C.Counter(reason for r in quarantine for reason in r['exclusion_reasons'].split(';'))),feature_mismatch_cells=len(reconciliation),feature_mismatch_cases=len({r['case_id'] for r in reconciliation}),feature_mismatches_by_column=dict(C.Counter(r['feature'] for r in reconciliation)),split_counts=splitcounts,split_method='Deterministic SHA256 ranking using seed and exact relative-time trace fingerprint. Reference cases: 240/80/80; accepted perturbations split by type into validation/test. No model outcomes used.',split_seed=SEED,known_exact_trace_overlap=0,known_exact_first_two_event_overlap=0,lineage_provenance='Generator code, generation seed, latent families and parent traces were not supplied. Exact overlap checks cannot establish independent generation.',outside_08_17_events=sum(not 8<=stamp(e['timestamp']).hour<17 for e in events),weekend_events=sum(stamp(e['timestamp']).weekday()>=5 for e in events),model_fit_performed=False)
    originalmap={c['case_id']:c for c in cases}
    stats['derived_definition_change_case_counts']={k:sum(float(r[k])!=float(originalmap[r['case_id']][k]) for r in features) for k in ['unique_activities','repeated_activity_count','resource_handoffs','distinct_resources']}
    stats['retained_cases_with_raw_feature_mismatch']=len({r['case_id'] for r in reconciliation if not r['case_quarantined']})
    stats['split_route_counts']={s:dict(C.Counter(originalmap[r['case_id']]['legal_route'] for r in manifest if r['split']==s)) for s in ['train','validation','test']}
    assert len({tuple(r[k] for k in FEATURES) for r in features})==len(features), 'Feature duplicate groups require split review'
    stats['exact_duplicate_prepared_feature_vectors']=0
    dump(out/'audit/audit_summary.json',stats)
    readme=f'''# Synthetic workflow dataset v1

Status: prepared for synthetic-only, completed-case Isolation Forest experiments. No model has been trained. This is not actual departmental data or a production validation dataset.

## Population
Original: {len(cases)} cases / {len(events)} events. Prepared: {len(accepted)} cases / {len(accepted_events)} events. Quarantined: {len(quarantine)} cases / {len(quarantine_events)} events. All original bytes are preserved in originals/.

36 cases have backward timestamps in event-sequence order. Another 8 have post-notification events with final notification not last. These 44 cases are excluded, not repaired or relabelled. Post-notification events may be meaningful in another protocol, but cannot silently be treated as completed application traces here. Do not sort away chronological defects or invent replacement dates.

{len(reconciliation)} supplied feature cells across {len({r['case_id'] for r in reconciliation})} cases disagree with calculations in supplied event_seq order beyond 2-decimal rounding tolerance. Prepared features are recomputed, never copied. Audit mismatch figures use raw activity/resource semantics; canonicalization and institution-scoped resources are separately documented definition changes.

All 19 raw-feature mismatch cases fall in quarantine. Among accepted cases, canonical activity definitions change unique/repeated counts in 33 cases; institution-scoped resource identity changes handoffs in 120 cases and distinct-resource counts in 299. These are deliberate feature-definition changes, not claims that the original raw-name calculations were arithmetically wrong. Raw identifiers and activities remain intact. There are no exact duplicate prepared numerical feature vectors.

## Files and use
- prepared/event_log.csv: accepted events with unchanged timestamps, sequence and raw text; derived canonical activity and explicit synthetic provenance.
- prepared/case_features.csv: case key plus nine recomputed numerical features.
- prepared/case_catalog.csv: all 500 case metadata rows, scenario labels, quality flags and unknown lineage.
- splits/X_train.csv: numerical inputs only, 240 synthetic reference cases. Do not train on y, keys, IDs or scenario metadata.
- splits/X_validation.csv and X_test.csv: 108 rows each; keys and synthetic labels stored separately in matching row order.
- splits/split_manifest.csv: fixed assignments for all 500 cases, including quarantine.
- feature_contract.json: exact definitions and excluded columns.
- audit/: row-level defects, quarantine, feature mismatches and counts.
- originals/routing_config.json: user-supplied simulation settings preserved, NOT a verified legal routing specification.

Use validation for parameter/threshold decisions and test once after freezing them. Do not use the 20% injected source prevalence as a real-world contamination estimate. Fit any learned preprocessing on train only. Keep raw activity strings and ID digits out of the model: IDs encode scenario ranges and several activities explicitly name injections. Scoring supports complete traces only; online/prefix scoring requires a separate protocol.

## Interpretation limits
Reference-normal means generator-designated reference, not independently verified normality. Anomaly labels describe controlled synthetic scenarios, not corruption or legal noncompliance. Evaluation metrics will measure this simulator only. All 400 reference cases have no repeated raw activities, which can make injected repetition artificially easy. Dataset has {stats['outside_08_17_events']} events outside 08:00–17:00 and {stats['weekend_events']} weekend events; do not interpret these as officer attendance violations. No business calendar or timezone was supplied.

The field legal_route equals land_purpose throughout; these are four simulation scenario categories, not verified legal bases. Higher-approval/registration probabilities are simulation parameters, not law. Conditional activities match this supplied config, but domain validity remains unverified. Resource identity semantics are incomplete; pair institution with resource when counting changes.

No generator, generation seed, latent family ID or parent-case mapping was supplied. We found no exact relative-time trace duplicates and no identical first-two-event signatures. This does NOT prove independent families. If generator lineage becomes available, rebuild grouped splits before claims. No paired helped/hurt comparison against original parent cases is supported.

After quarantine there are only 1 repeated-correction case and 3 excessive-handoff cases. Repeated-correction is validation-only; test has no coverage of that type. Do not claim reliable detection across all five perturbation types. The 44 excluded cases should be regenerated from the original generator with valid insertion timing, preserving parent/family IDs, before broader evaluation. No human-review approval has been assumed.

## Reproduce
Run: python prepare_synthetic_v1.py INPUT_ZIP NEW_OUTPUT_DIRECTORY
Python 3.10+ standard library only. Existing output directories are rejected. This script is specific to the audited 500-case input; it does not silently accept changed populations. Split seed: {SEED}. Source hashes and output checksums are recorded.
'''
    (out/'README.md').write_text(readme,encoding='utf-8')
    # Practical post-write validation: no fitted objects, cross-split keys disjoint, numeric arrays align.
    ids_by_split={s:{r['case_id'] for r in manifest if r['split']==s} for s in ['train','validation','test']}
    assert all(not(ids_by_split[a]&ids_by_split[b]) for a,b in [('train','validation'),('train','test'),('validation','test')])
    assert all(r['label']=='reference-normal' for r in manifest if r['split']=='train')
    for s in ids_by_split:
        xr=readcsv((out/f'splits/X_{s}.csv').read_bytes());kr=readcsv((out/f'splits/keys_{s}.csv').read_bytes())
        assert len(xr)==len(kr)==len(ids_by_split[s])
        assert list(xr[0])==FEATURES
        assert all(math.isfinite(float(v)) and float(v)>=0 for row in xr for v in row.values())
        if s!='train':assert [r['case_id'] for r in kr]==[r['case_id'] for r in readcsv((out/f'splits/y_{s}.csv').read_bytes())]
    assert len(accepted_events)+len(quarantine_events)==len(events)
    assert all((out/'originals'/n).read_bytes()==b for n,b in raw.items())
    dump(out/'checksums.json',{str(p.relative_to(out)):sha(p.read_bytes()) for p in sorted(out.rglob('*')) if p.is_file()})
    print(json.dumps(stats,indent=2))

if __name__=='__main__':main(*sys.argv[1:])
