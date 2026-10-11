import type { ApiErrorResponse } from "@/shared/types/common";
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

function mergeHeaders(init?: HeadersInit): HeadersInit {
  return {
    ...landIntelligenceOwnerHeaders(),
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
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await fetch(url, {
    ...init,
    method: "GET",
    headers: mergeHeaders({
      Accept: "application/json",
      ...init?.headers,
    }),
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
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await fetch(url, {
    ...init,
    method: "POST",
    headers: mergeHeaders({
      Accept: "application/json",
      "Content-Type": "application/json",
      ...init?.headers,
    }),
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
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  let response: Response;

  try {
    response = await fetch(url, {
      ...init,
      method: "POST",
      headers: mergeHeaders({
        Accept: "application/json",
        ...init?.headers,
      }),
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
): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await fetch(url, {
    ...init,
    method: "PUT",
    headers: mergeHeaders({
      Accept: "application/json",
      "Content-Type": "application/json",
      ...init?.headers,
    }),
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
): Promise<void> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;

  const response = await fetch(url, {
    ...init,
    method: "DELETE",
    headers: mergeHeaders({
      Accept: "application/json",
      ...init?.headers,
    }),
  });

  if (!response.ok) {
    throw await parseError(response);
  }
}