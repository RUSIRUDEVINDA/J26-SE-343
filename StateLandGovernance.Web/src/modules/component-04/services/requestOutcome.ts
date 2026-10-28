export class C4RequestError extends Error{
 readonly outcome:"rejected"|"uncertain";readonly status?:number;
 constructor(message:string,outcome:"rejected"|"uncertain",status?:number){super(message);this.name="C4RequestError";this.outcome=outcome;this.status=status;}
}
export function definitelyRejected(error:unknown){return error instanceof C4RequestError&&error.outcome==="rejected";}
export async function confirmedJson<T>(response:Response,write:boolean):Promise<T>{
 if(!response.ok){
  const rejected=response.status>=400&&response.status<500&&response.status!==408;
  let detail="";try{const body=await response.json();detail=body.message||body.detail||body.title||"";}catch{}
  const fallback=response.status===413?"File exceeds the permitted upload size.":response.status===401?"Session expired. Sign in again.":response.status===403||response.status===404?"Permission denied or this record is no longer eligible for your account.":response.status===415?"Unsupported file type.":"Server request failed.";
  throw new C4RequestError(typeof detail==="string"&&detail?detail.slice(0,600):fallback,write&&!rejected?"uncertain":"rejected",response.status);
 }
 try{return await response.json() as T;}catch{throw new C4RequestError("The server response could not be confirmed. Refresh saved history before replaying.",write?"uncertain":"rejected");}
}
