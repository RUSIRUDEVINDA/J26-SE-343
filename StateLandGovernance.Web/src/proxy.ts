import {NextRequest,NextResponse} from "next/server";
import {applicantDestination,governanceAdmission,governanceCookie,type GovernanceAdmission} from "./modules/component-04/services/governanceRouteAccess";
export async function proxy(request:NextRequest){
 const token=request.cookies.get(governanceCookie)?.value;if(!token)return NextResponse.redirect(new URL("/sign-in",request.url));
 try{
  const response=await governanceAdmission(token);if(response.status===401)return NextResponse.redirect(new URL("/sign-in",request.url));if(response.status>=500)return new NextResponse("Governance authentication is temporarily unavailable.",{status:503,headers:{"Cache-Control":"no-store"}});const body=await response.json() as GovernanceAdmission;
  if(!response.ok||!body.allowed)return NextResponse.redirect(new URL(applicantDestination(body),request.url));
  if(request.nextUrl.pathname==="/component-04"||request.nextUrl.pathname==="/component-04/"){
   const redirect=NextResponse.redirect(new URL("/component-04/assessments",request.url));redirect.headers.set("Cache-Control","private, no-store");return redirect;
  }
  const headers=new Headers(request.headers);headers.set("x-c4-admitted","true");headers.set("x-c4-commissioner",body.isCommissioner===true?"true":"false");
  return NextResponse.next({request:{headers},headers:{"Cache-Control":"private, no-store"}});
 }catch{return new NextResponse("Governance authentication is temporarily unavailable.",{status:503,headers:{"Cache-Control":"no-store"}});}
}
export const config={matcher:["/component-04/:path*"]};
