import styles from "./MeasuredWorkflowFeatures.module.css";
const fields=[
 ["eventCount","Events","count"],["elapsedDays","Elapsed time","days"],
 ["maxGapHours","Longest gap","hours"],["meanGapHours","Mean gap","hours"],
 ["uniqueActivities","Unique activities","count"],["repeatedActivityCount","Repeated activities","count"],
 ["resourceHandoffs","Resource handoffs","count"],["institutionSwitches","Institution switches","count"],
 ["distinctResources","Distinct resources","count"]
] as const;
type Metrics=Record<typeof fields[number][0],number>&{modelVersion:string;anomalyScore:number;threshold:number;flagged?:boolean;modelArtifactSha256?:string|null;inputSha256:string;advisoryNote?:string};
export function MeasuredWorkflowFeatures({result}:{result:Metrics}){
 return <><section className={styles.scorePanel} aria-label="Anomaly analysis summary"><p className={styles.flag}>{result.flagged===true?"Unusual workflow activity flagged":result.flagged===false?"No unusual workflow activity flagged":"Flag status unavailable"}</p><dl className={styles.scores}><div><dt>Anomaly score</dt><dd>{result.anomalyScore.toFixed(3)}</dd></div><div><dt>Detection threshold</dt><dd>{result.threshold.toFixed(3)}</dd></div></dl><p>Rounded display; exact values are in technical details. The anomaly score is not a probability of misconduct.</p></section><section aria-label="Measured workflow features"><h4>Measured workflow features</h4><dl className={styles.grid}>{fields.map(([key,label,unit])=><div key={key}><dt>{label}</dt><dd>{result[key]} <span>{unit}</span></dd></div>)}</dl></section>
 <details className={styles.technical}><summary>Model and technical details</summary><dl><dt>Model</dt><dd>Isolation Forest · {result.modelVersion}</dd><dt>Anomaly score</dt><dd>{result.anomalyScore}</dd><dt>Threshold</dt><dd>{result.threshold}</dd><dt>Artifact SHA256</dt><dd>{result.modelArtifactSha256??"Unavailable"}</dd><dt>Input SHA256</dt><dd>{result.inputSha256}</dd></dl>{result.advisoryNote&&<p>{result.advisoryNote}</p>}</details></>;
}
