export function findingGroup(f:{isBlocking:boolean;requiresHumanReview:boolean;status?:string}) {
 if(f.isBlocking)return "Issues";
 if(f.status==="NotApplicable")return "Not applicable";
 if(f.status==="InsufficientInformation")return "Review items";
 return f.isBlocking?"Issues":f.requiresHumanReview?"Review items":"Satisfied / advisory";
}
export function selectAssignedCase(ids:readonly string[],requested:string|null,saved:string|null){
 if(requested&&ids.includes(requested))return requested;
 if(saved&&ids.includes(saved))return saved;
 return ids[0]||"";
}

export function findingTone(f:{isBlocking:boolean;requiresHumanReview:boolean;status?:string}) {
 if(f.isBlocking)return "danger";
 if(f.status==="InsufficientInformation")return "warning";
 if(f.status==="NotApplicable")return "neutral";
 return f.requiresHumanReview?"warning":"success";
}
export function findingStatusLabel(status:string) {
 return status==="InsufficientInformation"?"Insufficient information ? unresolved":status==="NotApplicable"?"Not applicable":status;
}
