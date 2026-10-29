import {NextRequest,NextResponse} from "next/server";
import {governanceAdmission,governanceCookie,type GovernanceAdmission} from "@/modules/component-04/services/governanceRouteAccess";
// Existing bearer only: HttpOnly admission cookie, live-validated on every C4 route.
export async function POST(request:NextRequest){
 if(request.headers.get("origin")!==request.nextUrl.origin)return new NextResponse(null,{status:403});
 const header=request.headers.get("authorization")??"";if(!header.startsWith("Bearer ")||header.length>8000)return new NextResponse(null,{status:401});
 try{const admission=await governanceAdmission(header.slice(7));const body=await admission.json() as GovernanceAdmission;
  if(!(admission.ok&&body.allowed||admission.status===403&&body.applicant))return new NextResponse(null,{status:admission.status===401?401:503});
  const response=NextResponse.json({ready:true},{headers:{"Cache-Control":"no-store"}});response.cookies.set(governanceCookie,header.slice(7),{httpOnly:true,sameSite:"strict",secure:request.nextUrl.protocol==="https:",path:"/component-04",maxAge:28800});return response;
 }catch{return new NextResponse(null,{status:503});}
}
