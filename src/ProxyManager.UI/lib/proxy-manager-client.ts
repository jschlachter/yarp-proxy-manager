import type {
  Certificate,
  FileAsset,
  MaintainerAssignment,
  ProblemDetails,
  ProxyHost,
  UserSession,
} from "@/types";

function getBaseUrl(): string {
  const url = process.env.PROXY_MANAGER_API_URL;
  if (!url) {
    throw new Error("PROXY_MANAGER_API_URL environment variable is not set");
  }
  return url.replace(/\/$/, "");
}

function getFilesBaseUrl(): string {
  const url = process.env.PROXY_MANAGER_FILES_URL;
  if (!url) {
    throw new Error("PROXY_MANAGER_FILES_URL environment variable is not set");
  }
  return url.replace(/\/$/, "");
}

async function toProblem(response: Response): Promise<ProblemDetails> {
  const contentType = response.headers.get("Content-Type") ?? "";
  if (contentType.includes("problem+json") || contentType.includes("application/json")) {
    return (await response.json()) as ProblemDetails;
  }
  return {
    type: "https://tools.ietf.org/html/rfc9457",
    title: "Upstream Error",
    status: response.status,
    detail: await response.text(),
  };
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
    throw await toProblem(response);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

/**
 * Multipart uploads must NOT set Content-Type manually — the boundary lives in that
 * header and only `fetch` (from the `FormData` body) can generate it correctly.
 */
export async function uploadFileAsset(
  session: UserSession,
  file: File,
  assetType: string
): Promise<FileAsset> {
  const form = new FormData();
  form.append("file", file, file.name);

  const url = `${getFilesBaseUrl()}/files?assetType=${encodeURIComponent(assetType)}`;
  const response = await fetch(url, {
    method: "POST",
    headers: {
      Authorization: `Bearer ${session.accessToken}`,
      // Required by YARP's filesRoute CSRF match — see proxysettings.json.
      "X-Requested-With": "proxymanager-ui",
    },
    body: form,
  });

  if (!response.ok) {
    throw await toProblem(response);
  }

  return response.json() as Promise<FileAsset>;
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
}

export interface UpdateRouteRequest {
  domainNames?: string[];
  destinationUri?: string;
  isEnabled?: boolean;
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

export interface PaginatedCertificates {
  items: Certificate[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CreateCertificateRequest {
  name: string;
  format: string;
  certificateAssetId: string;
  keyAssetId?: string;
  passPhrase?: string;
}

export interface UpdateCertificateRequest {
  name?: string;
  passPhrase?: string;
}

export function listCertificates(
  session: UserSession,
  page = 1,
  pageSize = 50
): Promise<PaginatedCertificates> {
  return apiFetch<PaginatedCertificates>(
    session,
    `/certificates?page=${page}&pageSize=${pageSize}`
  );
}

export function getCertificate(session: UserSession, id: string): Promise<Certificate> {
  return apiFetch<Certificate>(session, `/certificates/${encodeURIComponent(id)}`);
}

export function createCertificate(
  session: UserSession,
  body: CreateCertificateRequest
): Promise<Certificate> {
  return apiFetch<Certificate>(session, `/certificates`, {
    method: "POST",
    body: JSON.stringify(body),
  });
}

export function updateCertificate(
  session: UserSession,
  id: string,
  body: UpdateCertificateRequest
): Promise<Certificate> {
  return apiFetch<Certificate>(session, `/certificates/${encodeURIComponent(id)}`, {
    method: "PUT",
    body: JSON.stringify(body),
  });
}

export function deleteCertificate(session: UserSession, id: string): Promise<void> {
  return apiFetch<void>(session, `/certificates/${encodeURIComponent(id)}`, {
    method: "DELETE",
  });
}
