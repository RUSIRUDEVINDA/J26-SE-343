// Navigation only; all permissions remain enforced by the API.
export function canShowGovernanceNavigation(user:{home?:string}|null|undefined){return !!user&&!user.home?.startsWith("/workflow-governance/applicant/");}
export function c4PostLoginPath(user:{actorId:string;institution?:string;home?:string},fallback:string){
 if(user.home?.startsWith("/workflow-governance/applicant/"))return user.home;
 return user.actorId==="0ab669fe-d0ab-5695-ad55-10f6b3b5aab3"&&user.institution==="COMMISSIONER"?"/component-04":fallback;
}
