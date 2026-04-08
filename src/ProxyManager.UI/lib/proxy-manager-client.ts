import type { ProblemDetails, ProxyHost, UserSession } from "@/types";

function getBaseUrl(): string {
  const url = process.env.PROXY_MANAGER_API_URL;
  if (!url) {
    throw new Error("PROXY_MANAGER_API_URL environment variable is not set");
  }
  return url.replace(/\/$/, "");
}

async function apiFetch<T>(
  session: UserSession,
  path: string,
  init?: RequestInit
): Promise<T> {
  const url = `${getBaseUrl()}${path}`;
  const response = await fetch(url, {
    ...init,
    headers: {
      "Content-Type": "application/json",
      Authorization: `Bearer ${session.accessToken}`,
      ...(init?.headers as Record<string, string> | undefined),
    },
  });

  if (!response.ok) {
    const contentType = response.headers.get("Content-Type") ?? "";
    if (contentType.includes("problem+json") || contentType.includes("application/json")) {
      const problem = (await response.json()) as ProblemDetails;
      throw problem;
    }
    const problem: ProblemDetails = {
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Upstream Error",
      status: response.status,
      detail: await response.text(),
    };
    throw problem;
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

export interface PaginatedRoutes {
  routes: ProxyHost[];
  page: number;
  pageSize: number;
  total: number;
}

export function listRoutes(
  session: UserSession,
  page = 1,
  pageSize = 50
): Promise<PaginatedRoutes> {
  return apiFetch<PaginatedRoutes>(
    session,
    `/proxyHosts?page=${page}&pageSize=${pageSize}`
  );
}

export function getRoute(session: UserSession, id: string): Promise<ProxyHost> {
  return apiFetch<ProxyHost>(session, `/proxyHosts/${encodeURIComponent(id)}`);
}

export function createRoute(
  session: UserSession,
  body: Omit<ProxyHost, "id" | "createdAt" | "updatedAt">
): Promise<ProxyHost> {
  return apiFetch<ProxyHost>(session, `/proxyHosts`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateRoute(
  session: UserSession,
  id: string,
  body: Omit<ProxyHost, "id" | "createdAt" | "updatedAt">
): Promise<ProxyHost> {
  return apiFetch<ProxyHost>(session, `/proxyHosts/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteRoute(session: UserSession, id: string): Promise<void> {
  return apiFetch<void>(session, `/proxyHosts/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}
