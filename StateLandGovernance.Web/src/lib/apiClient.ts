import type { ApiErrorResponse } from "@/shared/types/common";

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

export async function apiGet<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${ensureBaseUrl()}${path.startsWith("/") ? path : `/${path}`}`;
  const response = await fetch(url, {
    ...init,
    method: "GET",
    headers: {
      Accept: "application/json",
      ...init?.headers,
    },
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
    headers: {
      Accept: "application/json",
      "Content-Type": "application/json",
      ...init?.headers,
    },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw await parseError(response);
  }
  return (await response.json()) as T;
}
