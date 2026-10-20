import { developmentToken } from "../../../lib/developmentSession";
import { getLandIntelligenceOwnerId } from "./landIntelligenceOwner";

const APPLICANT_TOKEN_KEY = "component01.landIntelligence.applicantAccessToken";
const OFFICER_TOKEN_KEY = "component01.landIntelligence.officerAccessToken";

type SessionProfile = "applicant" | "officer" | "internal";

function apiBaseUrl(): string {
  const base = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "");
  if (!base) {
    if (typeof window !== "undefined" && ["localhost", "127.0.0.1"].includes(window.location.hostname)) return "http://localhost:5264";
    throw new Error("NEXT_PUBLIC_API_BASE_URL is not configured.");
  }
  return base;
}

function readToken(storageKey: string): string | null {
  if (typeof window === "undefined") {
    return null;
  }
  const value = window.sessionStorage.getItem(storageKey);
  if (!value) return null;
  try {
    const payload = JSON.parse(atob(value.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    if (typeof payload.exp !== "number" || payload.exp * 1000 <= Date.now() + 30000) {
      window.sessionStorage.removeItem(storageKey); return null;
    }
  } catch { window.sessionStorage.removeItem(storageKey); return null; }
  return value;
}

function writeToken(storageKey: string, token: string): void {
  window.sessionStorage.setItem(storageKey, token);
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

export async function ensureApplicantAccessToken(): Promise<string | null> {
  const local = await developmentToken();
  if (local.enabled) return local.token;
  if (typeof window === "undefined") {
    return null;
  }

  const existing = readToken(APPLICANT_TOKEN_KEY);
  if (existing) {
    return existing;
  }

  try {
    const token = await mintDevelopmentSession(
      "applicant",
      getLandIntelligenceOwnerId(),
    );
    writeToken(APPLICANT_TOKEN_KEY, token);
    return token;
  } catch {
    return null;
  }
}

export async function ensureOfficerAccessToken(): Promise<string | null> {
  const local = await developmentToken();
  if (local.enabled) return local.token;
  if (typeof window === "undefined") {
    return null;
  }

  const existing = readToken(OFFICER_TOKEN_KEY);
  if (existing) {
    return existing;
  }

  try {
    const token = await mintDevelopmentSession("officer");
    writeToken(OFFICER_TOKEN_KEY, token);
    return token;
  } catch {
    return null;
  }
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
