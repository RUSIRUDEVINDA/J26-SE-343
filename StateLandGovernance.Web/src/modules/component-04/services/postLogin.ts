// Navigation only; all permissions remain enforced by the API.
export function c4PostLoginPath(user:{actorId:string;institution?:string},fallback:string){
 return user.actorId==="0ab669fe-d0ab-5695-ad55-10f6b3b5aab3"&&user.institution==="COMMISSIONER"?"/component-04/central-complaints":fallback;
}
