export type StatusTone="neutral"|"positive"|"warning"|"danger"|"active";
const labels:Record<string,string>={InsufficientInformation:"Insufficient information",RequiresHumanReview:"Human review required",NotApplicable:"Not applicable",NonCompliant:"Non-compliant",VerifiedPresent:"Verified present",VerifiedAbsent:"Verified absent",IssueFound:"Issue found",NoIssueFound:"No issue found",NotYetVerified:"Not yet verified",NoConflicts:"No conflicts returned",UnderReview:"Under review",InformationRequested:"Information requested",ReviewRecorded:"Review recorded",InProgress:"In progress"};
export function statusLabel(status:string){return labels[status]??status.replace(/([a-z])([A-Z])/g,"$1 $2");}
export function statusTone(status:string):StatusTone {
 if(["Compliant","Satisfied"].includes(status))return "positive";
 if(["NonCompliant","Failed"].includes(status))return "danger";
 if(["Missing","Unverified","Unavailable","InsufficientInformation","RequiresHumanReview","Conditional","Unknown","Interrupted"].includes(status))return "warning";
 if(["Pending","InProgress","Current","Running"].includes(status))return "active";
 return "neutral";
}
export function sriLankaTime(value:string){return new Intl.DateTimeFormat("en-GB",{dateStyle:"medium",timeStyle:"short",timeZone:"Asia/Colombo"}).format(new Date(value));}
