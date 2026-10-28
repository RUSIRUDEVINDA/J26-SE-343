export const governanceCookie="slg.c4RouteSession";
export type GovernanceAdmission={allowed:boolean;applicant?:boolean;home?:string;isCommissioner?:boolean};
export function governanceApiBase(){return (process.env.C4_INTERNAL_API_BASE_URL||process.env.NEXT_PUBLIC_API_BASE_URL||"http://localhost:5264").replace(/\/$/,"");}
export async function governanceAdmission(token:string){return fetch(governanceApiBase()+"/api/governance-intelligence/access",{headers:{Authorization:`Bearer ${token}`},cache:"no-store",signal:AbortSignal.timeout(8000)});}
export function applicantDestination(value:GovernanceAdmission){return value.applicant===true?"/workflow-governance/applicant/cases":"/sign-in";}
