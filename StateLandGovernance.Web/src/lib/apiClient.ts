import type { ApiErrorResponse } from "@/shared/types/common";
import {
  ensureApplicantAccessToken,
  ensureOfficerAccessToken,
  isDemoOwnerHeaderEnabled,
  landIntelligenceBearerHeaders,
} from "@/modules/component-01-land-intelligence/utils/landIntelligenceSession";

export type LandIntelligenceApiAuthRole = "applicant" | "officer" | "none";
import { landIntelligenceOwnerHeaders } from "@/modules/component-01-land-intelligence/utils/landIntelligenceOwner";

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "") ?? "";

export class ApiClientError extends Error {
  readonly status: number;
  readonly title: string;
  readonly detail: string;
  readonly errors?: string[];

  constructor(payload: ApiErrorResponse) {
    super(payload.detail || payload.title);
    this.name = "ApiClientError";
    this.status = payload.status;
    this.title = payload.title;
    this.detail = payload.detail;
    this.errors = payload.errors ?? undefined;
  }
}

function ensureBaseUrl(): string {
  if (!baseUrl) {
    throw new Error(
      "NEXT_PUBLIC_API_BASE_URL is not configured. Copy .env.local.example to .env.local.",
    );
  }
  return baseUrl;
}

async function resolveAccessToken(
  authRole: LandIntelligenceApiAuthRole,
): Promise<string | null> {
  if (authRole === "none") {
    return null;
  }
  if (authRole === "officer") {
    return await ensureOfficerAccessToken();
  }
  return await ensureApplicantAccessToken();
}

async function mergeHeaders(
  init?: HeadersInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<HeadersInit> {
  const accessToken = await resolveAccessToken(authRole);
  const demoOwner =
    authRole === "applicant" &&
    isDemoOwnerHeaderEnabled() &&
    !accessToken
      ? landIntelligenceOwnerHeaders()
      : {};

  return {
    ...landIntelligenceBearerHeaders(accessToken),
    ...demoOwner,
    ...init,
  };
}

async function parseError(response: Response): Promise<ApiClientError> {
  try {
    const body = (await response.json()) as ApiErrorResponse;
    return new ApiClientError({
      status: body.status ?? response.status,
      title: body.title ?? response.statusText,
      detail: body.detail ?? "Request failed.",
      errors: body.errors,
    });
  } catch {
    return new ApiClientError({
      status: response.status,
      title: response.statusText,
      detail: "Request failed.",
    });
  }
}

export async function apiGet<T>(
  path: string,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  const response = await fetch(url, {
    ...init,
    method: "GET",
    headers: await mergeHeaders(
      {
        Accept: "application/json",
        ...init?.headers,
      },
      authRole,
    ),
  });
  if (!response.ok) {
    throw await parseError(response);
  }
  return (await response.json()) as T;
}

export async function apiPost<T>(
  path: string,
  body: unknown,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  const response = await fetch(url, {
    ...init,
    method: "POST",
    headers: await mergeHeaders(
      {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...init?.headers,
      },
      authRole,
    ),
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw await parseError(response);
  }
  return (await response.json()) as T;
}

export async function apiPut<T>(
  path: string,
  body: unknown,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  const response = await fetch(url, {
    ...init,
    method: "PUT",
    headers: await mergeHeaders(
      {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...init?.headers,
      },
      authRole,
    ),
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw await parseError(response);
  }
  if (response.status === 204) {
    return undefined as T;
  }
  return (await response.json()) as T;
}

export async function apiDelete(
  path: string,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<void> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  const response = await fetch(url, {
    ...init,
    method: "DELETE",
    headers: await mergeHeaders(
      {
        Accept: "application/json",
        ...init?.headers,
      },
      authRole,
    ),
  });
  if (!response.ok) {
    throw await parseError(response);
  }
}
