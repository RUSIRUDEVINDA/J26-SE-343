import {officerApi} from "./officerFlows";
export type SequenceReadiness={earlyRequestId:string|null;regulatoryEvaluationId:string|null;canRunRegulatory:boolean;canProceedToConflict:boolean;holds:string[]};
export const sequenceQuery=(early?:string|null,regulatory?:string|null,conflict?:string|null)=>{const q=new URLSearchParams();if(early)q.set("earlyRequestId",early);if(regulatory)q.set("regulatoryEvaluationId",regulatory);if(conflict)q.set("conflictReportId",conflict);return q.toString();};
export const readSequence=(early?:string|null,regulatory?:string|null)=>officerApi<SequenceReadiness>("officer-sequence?"+sequenceQuery(early,regulatory));
export function sequenceHref(stage:"early"|"regulatory"|"conflict",early?:string|null,regulatory?:string|null,conflict?:string|null){const query=sequenceQuery(early,regulatory,conflict);return `/component-04/assessments/${stage}${query?"?"+query:""}`;}
