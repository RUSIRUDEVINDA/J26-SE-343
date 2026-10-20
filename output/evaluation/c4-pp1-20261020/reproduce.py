"""Read-only scoring of frozen C4 artifacts; never fits, tunes or writes model files."""
from pathlib import Path
import sys,json,hashlib,platform
from datetime import datetime,timezone
ROOT=Path(__file__).resolve().parents[3]
ML=ROOT/'StateLandGovernance/src/Modules/GovernanceIntelligence/ML'
sys.path[:0]=[str(ML/'src'),str(ML)]
import numpy as np,pandas as pd,sklearn
from sklearn.metrics import classification_report,confusion_matrix,accuracy_score,roc_auc_score,average_precision_score
from predict_v1 import load_model_version
from anomaly_detection.bundle import load_trusted_local_bundle
from anomaly_detection.baseline import anomaly_scores,flags_from_scores,load_feature_matrix,classification_metrics

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8'))
def evaluate():
    artifact=load_model_version('v2');meta=artifact['metadata']
    # The existing experiment command fits a model. This runner only scores the frozen promoted artifact.
    data=ML/'data/state_land_complaints_development_530.csv'
    manifest=ML/'results/experiment6_arm_e_prospective_synthetic_test/prospective_population_manifest.csv'
    df=pd.read_csv(data,encoding='utf-8-sig');population=pd.read_csv(manifest)
    eligible=population[population.eligibility=='eligible']
    test=eligible.merge(df,on='Research_ID',how='left',validate='one_to_one')
    assert len(test)==69 and test.Canonical_English_Text.notna().all()
    pred=artifact['pipeline'].predict(test.Canonical_English_Text.tolist());labels=meta['label_taxonomy'];y=test.Final_Label.tolist()
    report=classification_report(y,pred,labels=labels,output_dict=True,zero_division=0)
    complaint={'title':'Complaint classifier v2','evaluationType':'Reproduced synthetic prospective held-out scoring','modelVersion':'v2','modelSha256':sha(artifact['joblib_path']),'population':{'size':69,'families':23,'trainingRows':425,'labelSource':'Reviewed prospective family decisions (Approved / Label_Change), Experiment 6 frozen manifest','split':'Prospective synthetic families excluded from training'},'accuracy':accuracy_score(y,pred),'macroF1':report['macro avg']['f1-score'],'weightedF1':report['weighted avg']['f1-score'],'perClass':{k:report[k] for k in labels},'labels':labels,'confusionMatrix':confusion_matrix(y,pred,labels=labels).tolist(),'inputs':{'datasetSha256':sha(data),'manifestSha256':sha(manifest)},'savedHistoricalAccuracy':meta['experiment6_prospective_synthetic_results']['accuracy'],'limitations':meta['limitations']}
    assert abs(complaint['accuracy']-complaint['savedHistoricalAccuracy'])<1e-12
    bundle=load_trusted_local_bundle();frozen=bundle.frozen_run;split=frozen.dataset_directory/'splits'
    X,_=load_feature_matrix(split/'X_test.csv');ys=pd.read_csv(split/'y_test.csv');truth=ys.synthetic_anomaly_label.to_numpy(dtype=int)
    scores=anomaly_scores(frozen.model,X);flags=flags_from_scores(scores,frozen.threshold)
    assert len(X)==108 and sum(truth)==28
    anomaly={'title':'Workflow Isolation Forest','evaluationType':'Reproduced reserved synthetic test scoring','modelVersion':bundle.bundle_version,'modelSha256':sha(frozen.model_path),'population':{'size':108,'reference':80,'perturbed':28,'labelSource':'Generator-designated reference and controlled perturbation labels','split':'Frozen reserved test split; validation-selected threshold unchanged'},'metrics':classification_metrics(truth,flags),'rocAuc':roc_auc_score(truth,scores),'averagePrecision':average_precision_score(truth,scores),'threshold':frozen.threshold,'scoreRange':[float(min(scores)),float(max(scores))],'strictFlagRule':'anomaly_score > threshold','inputs':{n:sha(split/n) for n in ['X_test.csv','y_test.csv','keys_test.csv','split_manifest.csv']},'limitations':['Synthetic held-out results, not real-world corruption detection accuracy.','Reference normality is generator-designated, not independently verified.','Repeated correction loop has zero test examples; excessive handoffs has two.','No feature-level causal explanations or missing-justification inference.']}
    return {'generatedAtUtc':datetime.now(timezone.utc).isoformat(),'fitPerformed':False,'thresholdTuned':False,'runtime':{'python':platform.python_version(),'scikitLearn':sklearn.__version__,'numpy':np.__version__},'complaint':complaint,'anomaly':anomaly}
if __name__=='__main__':
    result=evaluate();target=ROOT/'output/evaluation/c4-pp1-20261020/reproduced-evaluation.json'
    target.write_text(json.dumps(result,indent=2)+'\n',encoding='utf8')
    print(json.dumps({'report':str(target),'complaintAccuracy':result['complaint']['accuracy'],'anomalyMetrics':result['anomaly']['metrics']},indent=2))
