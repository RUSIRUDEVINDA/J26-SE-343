/**
 * Land Intelligence research-demo session helpers.
 *
 * NON-PRODUCTION: tokens are minted by the development/research endpoint
 * (`POST /api/v1/land/auth/session`) and cached in sessionStorage. This is NOT
 * production authentication or RBAC. The API still enforces role policies and
 * the research-demo gate on every request; this module never widens access.
 * When the demo gate is disabled server-side, renewal fails and cached tokens
 * are cleared.
 */
import { getLandIntelligenceOwnerId } from "./landIntelligenceOwner";

const APPLICANT_TOKEN_KEY = "component01.landIntelligence.applicantAccessToken";
const OFFICER_TOKEN_KEY = "component01.landIntelligence.officerAccessToken";
/** Fixed demo officer subject — mint JWT via POST /api/v1/land/auth/session. */
const DEMO_OFFICER_SUBJECT_ID = "a1111111-1111-4111-8111-111111111111";

const AUTHORITY_TOKEN_KEY = "component01.landIntelligence.authorityAccessToken";

export type LandIntelligenceSessionRole =
  | "applicant"
  | "officer"
  | "authority"
  | "none";

type SessionProfile = "applicant" | "officer" | "authority" | "internal";

/** Treat tokens expiring within this window as already expired. */
export const JWT_EXPIRY_SKEW_MS = 30_000;

export const SESSION_EXPIRED_TITLE = "Session expired";
export const SESSION_EXPIRED_DETAIL =
  "Your session has expired and could not be renewed. Refresh the page and try again.";

const TOKEN_KEYS: Record<
  Exclude<LandIntelligenceSessionRole, "none">,
  string
> = {
  applicant: APPLICANT_TOKEN_KEY,
  officer: OFFICER_TOKEN_KEY,
  authority: AUTHORITY_TOKEN_KEY,
};

function apiBaseUrl(): string {
  const base = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
  if (!base) {
    throw new Error("NEXT_PUBLIC_API_BASE_URL is not configured.");
  }
  return base;
}

function readToken(storageKey: string): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const value = window.sessionStorage.getItem(storageKey);
  return value && value.length > 0 ? value : null;
}

function writeToken(storageKey: string, token: string): void {
  window.sessionStorage.setItem(storageKey, token);
}

function decodeBase64Url(segment: string): string | null {
  try {
    const normalized = segment.replace(/-/g, "+").replace(/_/g, "/");
    const padded = normalized + "=".repeat((4 - (normalized.length % 4)) % 4);
    const binary = atob(padded);
    const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
    return new TextDecoder().decode(bytes);
  } catch {
    return null;
  }
}

/**
 * Reads the `exp` claim (seconds since epoch) from a JWT payload and returns
 * it in milliseconds. Returns null when the token is malformed or has no `exp`.
 * The signature is NOT verified — the API remains the authority.
 */
export function getJwtExpiryMs(token: string): number | null {
  const parts = token.split(".");
  if (parts.length !== 3) {
    return null;
  }
  const json = decodeBase64Url(parts[1]);
  if (!json) {
    return null;
  }
  try {
    const payload = JSON.parse(json) as { exp?: unknown };
    return typeof payload.exp === "number" && Number.isFinite(payload.exp)
      ? payload.exp * 1000
      : null;
  } catch {
    return null;
  }
}

/**
 * True when the token cannot be used: malformed, or `exp` is in the past
 * (within the skew window). A well-formed token without `exp` is not treated
 * as expired.
 */
export function isJwtExpired(token: string, nowMs: number = Date.now()): boolean {
  const parts = token.split(".");
  if (parts.length !== 3 || !decodeBase64Url(parts[1])) {
    return true;
  }
  const expiryMs = getJwtExpiryMs(token);
  if (expiryMs === null) {
    return false;
  }
  return expiryMs - JWT_EXPIRY_SKEW_MS <= nowMs;
}

export function clearLandIntelligenceRoleToken(
  role: LandIntelligenceSessionRole,
): void {
  if (role === "none" || typeof window === "undefined") {
    return;
  }
  window.sessionStorage.removeItem(TOKEN_KEYS[role]);
}

export function clearAllLandIntelligenceTokens(): void {
  clearLandIntelligenceRoleToken("applicant");
  clearLandIntelligenceRoleToken("officer");
  clearLandIntelligenceRoleToken("authority");
}

async function mintDevelopmentSession(
  profile: SessionProfile,
  subjectId?: string,
): Promise<string> {
  const response = await fetch(`${apiBaseUrl()}/api/v1/land/auth/session`, {
    method: "POST",
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
    },
    body: JSON.stringify({
      profile,
      subjectId: subjectId ?? null,
    }),
  });

  if (!response.ok) {
    throw new Error(
      `Could not obtain a Land Intelligence access token (${response.status}).`,
    );
  }

  const body = (await response.json()) as { accessToken: string };
  return body.accessToken;
}

async function mintForRole(
  role: Exclude<LandIntelligenceSessionRole, "none">,
): Promise<string> {
  if (role === "applicant") {
    return await mintDevelopmentSession("applicant", getLandIntelligenceOwnerId());
  }
  if (role === "officer") {
    return await mintDevelopmentSession("officer", DEMO_OFFICER_SUBJECT_ID);
  }
  return await mintDevelopmentSession(role);
}

/**
 * Returns a usable cached token for the role, discarding it when expired, or
 * mints a new one. Returns null when no token can be obtained.
 */
async function ensureRoleAccessToken(
  role: Exclude<LandIntelligenceSessionRole, "none">,
): Promise<string | null> {
  if (typeof window === "undefined") {
    return null;
  }

  const existing = readToken(TOKEN_KEYS[role]);
  if (existing) {
    if (!isJwtExpired(existing)) {
      return existing;
    }
    clearLandIntelligenceRoleToken(role);
  }

  try {
    const token = await mintForRole(role);
    writeToken(TOKEN_KEYS[role], token);
    return token;
  } catch {
    return null;
  }
}

export function ensureApplicantAccessToken(): Promise<string | null> {
  return ensureRoleAccessToken("applicant");
}

export function ensureOfficerAccessToken(): Promise<string | null> {
  return ensureRoleAccessToken("officer");
}

export function ensureAuthorityAccessToken(): Promise<string | null> {
  return ensureRoleAccessToken("authority");
}

const pendingRenewals = new Map<string, Promise<string | null>>();

/**
 * Discards the cached token and mints a fresh one for the same role.
 * On failure the unusable token is cleared and null is returned.
 * Concurrent renewals for one role share a single mint request.
 */
export function renewLandIntelligenceRoleToken(
  role: LandIntelligenceSessionRole,
): Promise<string | null> {
  if (role === "none" || typeof window === "undefined") {
    return Promise.resolve(null);
  }

  const pending = pendingRenewals.get(role);
  if (pending) {
    return pending;
  }

  clearLandIntelligenceRoleToken(role);
  const renewal = ensureRoleAccessToken(role)
    .then((token) => {
      if (!token) {
        clearLandIntelligenceRoleToken(role);
      }
      return token;
    })
    .finally(() => {
      pendingRenewals.delete(role);
    });
  pendingRenewals.set(role, renewal);
  return renewal;
}

/** Only authentication-style failures for a real role, and only once. */
export function shouldRenewSessionAfterFailure(
  status: number,
  role: LandIntelligenceSessionRole,
  alreadyRetried: boolean,
): boolean {
  return (
    !alreadyRetried && role !== "none" && (status === 401 || status === 403)
  );
}

export interface SessionRetryResult<R> {
  response: R;
  /** True when a renewal + second attempt was made. */
  retried: boolean;
  /** True when renewal produced no token (unusable session was cleared). */
  renewalFailed: boolean;
}

/**
 * Sends a request; on the first 401/403 renews the role token and sends
 * exactly one more attempt. Never loops: the second response is returned as-is.
 */
export async function sendWithSessionRetry<R extends { status: number }>(
  role: LandIntelligenceSessionRole,
  send: () => Promise<R>,
  renew: (role: LandIntelligenceSessionRole) => Promise<string | null> =
    renewLandIntelligenceRoleToken,
  clear: (role: LandIntelligenceSessionRole) => void =
    clearLandIntelligenceRoleToken,
): Promise<SessionRetryResult<R>> {
  const first = await send();
  if (!shouldRenewSessionAfterFailure(first.status, role, false)) {
    return { response: first, retried: false, renewalFailed: false };
  }

  const token = await renew(role);
  if (!token) {
    clear(role);
    return { response: first, retried: false, renewalFailed: true };
  }

  const second = await send();
  if (second.status === 401) {
    clear(role);
  }
  return { response: second, retried: true, renewalFailed: false };
}

export function landIntelligenceBearerHeaders(
  token: string | null,
): Record<string, string> {
  if (!token) {
    return {};
  }
  return { Authorization: `Bearer ${token}` };
}

export function isDemoOwnerHeaderEnabled(): boolean {
  return (
    process.env.NEXT_PUBLIC_LAND_INTELLIGENCE_DEMO_OWNER_HEADER === "true"
  );
}
