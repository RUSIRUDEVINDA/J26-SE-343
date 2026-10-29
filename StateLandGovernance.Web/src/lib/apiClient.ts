import type { ApiErrorResponse } from "@/shared/types/common";
import {
  SESSION_EXPIRED_DETAIL,
  SESSION_EXPIRED_TITLE,
  ensureApplicantAccessToken,
  ensureAuthorityAccessToken,
  ensureOfficerAccessToken,
  isDemoOwnerHeaderEnabled,
  landIntelligenceBearerHeaders,
  sendWithSessionRetry,
} from "../modules/component-01-land-intelligence/utils/landIntelligenceSession";

export type LandIntelligenceApiAuthRole =
  | "applicant"
  | "officer"
  | "authority"
  | "none";
import { landIntelligenceOwnerHeaders } from "../modules/component-01-land-intelligence/utils/landIntelligenceOwner";

const baseUrl = process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "").replace("http://127.0.0.1:", "http://localhost:") ?? "";

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
  if (authRole === "none") {
    return null;
  }
  if (authRole === "officer") {
    return await ensureOfficerAccessToken();
  }
  if (authRole === "authority") {
    return await ensureAuthorityAccessToken();
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

function flattenApiErrors(errors: unknown): string[] | undefined {
  if (!errors) {
    return undefined;
  }
  if (Array.isArray(errors)) {
    const list = errors.filter((item): item is string => typeof item === "string");
    return list.length > 0 ? list : undefined;
  }
  if (typeof errors !== "object") {
    return undefined;
  }

  const messages: string[] = [];
  for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
    const entries = Array.isArray(value) ? value : [value];
    for (const entry of entries) {
      if (typeof entry !== "string" || !entry.trim()) {
        continue;
      }
      if (key === "input" || key === "$") {
        messages.push(entry);
        continue;
      }
      const field = key
        .replace(/^\$\./, "")
        .replace(/([A-Z])/g, " $1")
        .replace(/[._]/g, " ")
        .trim()
        .toLowerCase();
      messages.push(field ? `${field}: ${entry}` : entry);
    }
  }
  return messages.length > 0 ? messages : undefined;
}

async function parseError(response: Response): Promise<ApiClientError> {
  try {
    const body = (await response.json()) as ApiErrorResponse & {
      errors?: unknown;
    };
    const errors = flattenApiErrors(body.errors);

    return new ApiClientError({
      status: body.status ?? response.status,
      title: body.title ?? response.statusText,
      detail:
        body.detail ??
        errors?.[0] ??
        "Request failed.",
      errors,
    });
  } catch {
    return new ApiClientError({
      status: response.status,
      title: response.statusText,
      detail: "Request failed.",
    });
  }
}

function sessionExpiredError(status: number): ApiClientError {
  return new ApiClientError({
    status,
    title: SESSION_EXPIRED_TITLE,
    detail: SESSION_EXPIRED_DETAIL,
  });
}

/**
 * Sends a request with the role's token. On the first 401/403 the research-demo
 * token is renewed and the request is retried exactly once. Unusable sessions
 * are cleared. Returns only successful responses; failures throw
 * ApiClientError. Role authorization is still enforced by the API.
 */
async function authorizedFetch(
  url: string,
  buildInit: () => Promise<RequestInit>,
  authRole: LandIntelligenceApiAuthRole,
): Promise<Response> {
  const { response, retried, renewalFailed } = await sendWithSessionRetry(
    authRole,
    async () => fetch(url, await buildInit()),
  );

  if (response.ok) {
    return response;
  }

  // 401 after renewal (or no renewal possible) means the session is unusable.
  if (response.status === 401 && (retried || renewalFailed)) {
    throw sessionExpiredError(401);
  }

  throw await parseError(response);
}

export async function apiGet<T>(
  path: string,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await authorizedFetch(
    url,
    async () => ({
      ...init,
      method: "GET",
      headers: await mergeHeaders(
        {
          Accept: "application/json",
          ...init?.headers,
        },
        authRole,
      ),
    }),
    authRole,
  );

  if (response.status === 204) {
    return undefined as T;
  }

  const text = await response.text();
  if (!text) {
    return undefined as T;
  }

  return JSON.parse(text) as T;
}

export async function apiDownloadBlob(
  path: string,
  authRole: LandIntelligenceApiAuthRole = "applicant",
  init?: RequestInit,
): Promise<Blob> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await authorizedFetch(
    url,
    async () => ({
      ...init,
      method: "GET",
      headers: await mergeHeaders(init?.headers, authRole),
    }),
    authRole,
  );

  return await response.blob();
}

/** GET that maps HTTP 204 / empty body to null. */
export async function apiGetOrNull<T>(
  path: string,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T | null> {
  const result = await apiGet<T | undefined>(path, init, authRole);
  return result ?? null;
}

export async function apiPost<T>(
  path: string,
  body: unknown,
  init?: RequestInit,
  authRole: LandIntelligenceApiAuthRole = "applicant",
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  if (init?.signal?.aborted) {
    throw new DOMException("The request was aborted.", "AbortError");
  }

  const response = await authorizedFetch(
    url,
    async () => ({
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
    }),
    authRole,
  );

  return (await response.json()) as T;
}

export async function apiPostFormData<T>(
  path: string,
  formData: FormData,
  init?: RequestInit,
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  let response: Response;

  try {
    response = await authorizedFetch(
      url,
      async () => ({
        ...init,
        method: "POST",
        headers: await mergeHeaders({
          Accept: "application/json",
          ...init?.headers,
        }),
        body: formData,
      }),
      "applicant",
    );
  } catch (err) {
    if (err instanceof ApiClientError) {
      throw err;
    }
    throw new ApiClientError({
      status: 0,
      title: "Network Error",
      detail:
        "Unable to reach the OCR API. Check that the ASP.NET Core API is running.",
    });
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

  const response = await authorizedFetch(
    url,
    async () => ({
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
    }),
    authRole,
  );

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

  await authorizedFetch(
    url,
    async () => ({
      ...init,
      method: "DELETE",
      headers: await mergeHeaders(
        {
          Accept: "application/json",
          ...init?.headers,
        },
        authRole,
      ),
    }),
    authRole,
  );
}
