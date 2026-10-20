import { developmentToken, readDevelopmentSession, clearDevelopmentSession } from "./developmentSession";
import type { ApiErrorResponse } from "@/shared/types/common";
import {
  ensureApplicantAccessToken,
  ensureOfficerAccessToken,
  isDemoOwnerHeaderEnabled,
  landIntelligenceBearerHeaders,
} from "../modules/component-01-land-intelligence/utils/landIntelligenceSession";

export type LandIntelligenceApiAuthRole = "applicant" | "officer" | "none";
import { landIntelligenceOwnerHeaders } from "../modules/component-01-land-intelligence/utils/landIntelligenceOwner";

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
  if (baseUrl) {
    return baseUrl;
  }

  if (
    process.env.NODE_ENV === "development" ||
    (typeof window !== "undefined" &&
      (window.location.hostname === "localhost" ||
        window.location.hostname === "127.0.0.1"))
  ) {
    return "http://localhost:5264";
  }

  throw new Error(
    "NEXT_PUBLIC_API_BASE_URL is not configured. Copy .env.local.example to .env.local.",
  );
}

async function resolveAccessToken(
  authRole: LandIntelligenceApiAuthRole,
): Promise<string | null> {
  if (authRole === "none") return typeof window !== "undefined" ? readDevelopmentSession()?.accessToken ?? null : null;
  const local = await developmentToken();
  if (local.enabled) return local.token;
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

export async function parseError(response: Response): Promise<ApiClientError> {
  if (response.status === 401 && typeof window !== "undefined" && readDevelopmentSession()) clearDevelopmentSession();
  let body: Record<string, unknown> = {};
  try { const parsed = await response.json(); body = parsed && typeof parsed === "object" ? parsed : {}; } catch { /* Empty HTTP error responses are valid. */ }
  const reference = typeof body.traceId === "string" ? body.traceId : response.headers.get("X-Correlation-ID");
  const fallback = response.status === 401 ? "Sign in to continue."
    : response.status === 403 ? "Required access or case/institution assignment is missing. Ask the case administrator to verify your access."
    : response.status >= 500 ? `The service could not complete this request.${reference ? ` Error reference: ${reference}` : " Please contact support."}`
    : "The request could not be completed. Check the case prerequisites and try again.";
  return new ApiClientError({ status: response.status,
    title: typeof body.title === "string" ? body.title : response.statusText,
    detail: response.status === 401 || response.status === 403 || response.status >= 500 ? fallback
      : typeof body.detail === "string" ? body.detail : typeof body.error === "string" ? body.error : fallback,
    errors: Array.isArray(body.errors) ? body.errors : undefined });
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
  const headers = await mergeHeaders(
    {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...init?.headers,
    },
    authRole,
  );

  if (init?.signal?.aborted) {
    throw new DOMException("The request was aborted.", "AbortError");
  }

  const response = await fetch(url, {
    ...init,
    method: "POST",
    headers,
    body: JSON.stringify(body),
  });

  if (!response.ok) {
    throw await parseError(response);
  }

  return (await response.json()) as T;
}

export async function apiPostFormData<T>(
  path: string,
  formData: FormData,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  let response: Response;

  try {
    response = await fetch(url, {
      ...init,
      method: "POST",
      headers: await mergeHeaders({
        Accept: "application/json",
        ...init?.headers,
      }, authRole),
      body: formData,
    });
  } catch {
    throw new ApiClientError({
      status: 0,
      title: "Network Error",
      detail:
        "Unable to reach the OCR API. Check that the ASP.NET Core API is running.",
    });
  }

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

export async function apiDownloadBlob(
  path: string,
  authRole: LandIntelligenceApiAuthRole = "officer",
  init?: RequestInit,
): Promise<Blob> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await fetch(url, {
    ...init,
    method: "GET",
    headers: await mergeHeaders(
      {
        Accept: "*/*",
        ...init?.headers,
      },
      authRole
    ),
  });

  if (!response.ok) {
    throw await parseError(response);
  }

  return await response.blob();
}
