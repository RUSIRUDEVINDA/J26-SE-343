export function findingGroup(f:{isBlocking:boolean;requiresHumanReview:boolean}) {
 return f.isBlocking?"Issues":f.requiresHumanReview?"Review items":"Satisfied / advisory";
}
export function selectAssignedCase(ids:readonly string[],requested:string|null,saved:string|null){
 if(requested&&ids.includes(requested))return requested;
 if(saved&&ids.includes(saved))return saved;
 return ids[0]||"";
}
