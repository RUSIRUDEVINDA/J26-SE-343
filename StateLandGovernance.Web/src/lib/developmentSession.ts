export type DevelopmentUser = { username: string; displayName: string; role: string; institution?: string; actorId: string; caseId: string; reference: string; home: string };
export type DevelopmentSession = { accessToken: string; expiresAtUtc: string; user: DevelopmentUser };
const key = "slg.localDevelopmentSession";
let enabled: Promise<boolean> | undefined;
export function developmentApiBase() { return process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "") || "http://localhost:5264"; }
export async function developmentEnabled() {
  if (typeof window === "undefined") return false;
  enabled ??= fetch(`${developmentApiBase()}/api/development-auth/status`).then(r => r.ok ? r.json() : {enabled:false}).then(s => s.enabled === true).catch(() => { enabled=undefined; return false; });
  return enabled;
}
export function readDevelopmentSession(): DevelopmentSession | null {
  if (typeof window === "undefined" || !window.sessionStorage) return null;
  try {
    const value = JSON.parse(window.sessionStorage.getItem(key) || "null") as DevelopmentSession | null;
    if (!value || !value.accessToken || !Number.isFinite(Date.parse(value.expiresAtUtc)) || Date.parse(value.expiresAtUtc) <= Date.now()) { window.sessionStorage.removeItem(key); return null; }
    return value;
  } catch { window.sessionStorage.removeItem(key); return null; }
}
export function clearDevelopmentSession() {
  window.sessionStorage.removeItem(key);
  window.sessionStorage.removeItem("component01.landIntelligence.officerAccessToken");
  window.sessionStorage.removeItem("component01.landIntelligence.applicantAccessToken");
  window.dispatchEvent(new Event("development-session-changed"));
}
export async function developmentToken() { return { enabled: await developmentEnabled(), token: readDevelopmentSession()?.accessToken ?? null }; }
export async function signInDevelopment(username:string,password:string) {
  const response=await fetch(`${developmentApiBase()}/api/development-auth/sign-in`,{method:"POST",headers:{"Content-Type":"application/json"},body:JSON.stringify({username,password})});
  if (!response.ok) throw new Error(response.status===401 ? "Incorrect local test username or password." : "Local Development sign-in is unavailable. Enable it on the local API.");
  const session=await response.json() as DevelopmentSession; clearDevelopmentSession(); window.sessionStorage.setItem(key,JSON.stringify(session));
  window.dispatchEvent(new Event("development-session-changed")); return session;
}
export async function signOutDevelopment() {
  const session=readDevelopmentSession();
  try {
    if(session) {
      const response=await fetch(`${developmentApiBase()}/api/development-auth/sign-out`,{method:"POST",headers:{Authorization:`Bearer ${session.accessToken}`}});
      if (!response.ok && response.status!==401) throw new Error("Server sign-out failed. Retry before sharing this browser.");
    }
  } finally { clearDevelopmentSession(); }
}
