import type { MaintainerAssignment, ProblemDetails, ProxyHost, UserSession } from "@/types";

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
  items: ProxyHost[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateRouteRequest {
  domainNames?: string[];
  destinationUri?: string;
  certificatePath?: string;
  certificateKeyPath?: string;
}

export interface UpdateRouteRequest {
  domainNames?: string[];
  destinationUri?: string;
  isEnabled?: boolean;
  certificatePath?: string;
  certificateKeyPath?: string;
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
  body: CreateRouteRequest
): Promise<ProxyHost> {
  return apiFetch<ProxyHost>(session, `/proxyHosts`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateRoute(
  session: UserSession,
  id: string,
  body: UpdateRouteRequest
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

export function listMaintainers(
  session: UserSession,
  routeId: string
): Promise<MaintainerAssignment[]> {
  return apiFetch<MaintainerAssignment[]>(
    session,
    `/proxyHosts/${encodeURIComponent(routeId)}/maintainers`
  );
}

export function assignMaintainer(
  session: UserSession,
  routeId: string,
  userId: string
): Promise<MaintainerAssignment> {
  return apiFetch<MaintainerAssignment>(
    session,
    `/proxyHosts/${encodeURIComponent(routeId)}/maintainers`,
    { method: "POST", body: JSON.stringify({ userId }) }
  );
}

export function removeMaintainer(
  session: UserSession,
  routeId: string,
  userId: string
): Promise<void> {
  return apiFetch<void>(
    session,
    `/proxyHosts/${encodeURIComponent(routeId)}/maintainers/${encodeURIComponent(userId)}`,
    { method: "DELETE" }
  );
}
