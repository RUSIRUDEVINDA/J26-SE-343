// Exact allowlist: never treat query-string paths or browser claims as authority.
const complaintPath = "/workflow-governance/applicant/complaints";
export function applicantComplaintSignIn(pathname:string|null|undefined,returnTo:string|null=null):string {
  return pathname===complaintPath||pathname==="/sign-in"&&returnTo===complaintPath?`/sign-in?returnTo=${encodeURIComponent(complaintPath)}`:"/sign-in";
}
export function applicantComplaintReturn(user: {home?: string}, candidate: string | null): string | null {
  return user.home?.startsWith("/workflow-governance/applicant/") && candidate === complaintPath
    ? complaintPath : null;
}
