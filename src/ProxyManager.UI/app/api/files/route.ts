import { getSession } from "@/lib/auth";
import type { ProblemDetails } from "@/types";

function problemResponse(problem: ProblemDetails): Response {
  return Response.json(problem, {
    status: problem.status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

function getFilesBaseUrl(): string | undefined {
  return process.env.PROXY_MANAGER_FILES_URL?.replace(/\/$/, "");
}

/**
 * Forwards the upload to ProxyManager.Files. Piping `request.body` directly into an outgoing
 * `fetch` (true zero-buffer streaming) reproducibly returns a bare 405 from Next.js 16.2.9's own
 * Route Handler dispatch, in both `next dev` and `next start` — not a Turbopack-dev-only quirk.
 * Falls back to `request.formData()`, which Next.js already buffers internally; re-emitted as a
 * fresh `FormData` for the upstream `fetch`. Bounded by ProxyManager.Files' own `MaxUploadBytes`
 * cap (10MB default) — see docs/files-service-plan.md Phase 3.
 */
export async function POST(request: Request): Promise<Response> {
  const session = getSession(request.headers);

  if (!session.isAdmin) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Forbidden",
      status: 403,
      detail: "Only administrators can upload files",
    });
  }

  const url = new URL(request.url);
  const assetType = url.searchParams.get("assetType");
  if (!assetType) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Bad Request",
      status: 400,
      detail: "'assetType' query parameter is required",
    });
  }

  const contentType = request.headers.get("Content-Type");
  if (!contentType?.includes("multipart/form-data")) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Bad Request",
      status: 400,
      detail: "Request must be multipart/form-data",
    });
  }

  const filesBaseUrl = getFilesBaseUrl();
  if (!filesBaseUrl) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Upstream Error",
      status: 502,
      detail: "PROXY_MANAGER_FILES_URL environment variable is not set",
    });
  }

  let incoming: FormData;
  try {
    incoming = await request.formData();
  } catch {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Bad Request",
      status: 400,
      detail: "Malformed multipart/form-data body",
    });
  }

  const file = incoming.get("file");
  if (!(file instanceof File)) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Bad Request",
      status: 400,
      detail: "No file part found in the request",
    });
  }

  const outgoing = new FormData();
  outgoing.append("file", file, file.name);

  try {
    const upstream = await fetch(
      `${filesBaseUrl}/files?assetType=${encodeURIComponent(assetType)}`,
      {
        method: "POST",
        headers: {
          Authorization: `Bearer ${session.accessToken}`,
          // Required by YARP's filesRoute CSRF match — see proxysettings.json. Harmless when
          // PROXY_MANAGER_FILES_URL points directly at the Files container instead of through YARP.
          "X-Requested-With": "proxymanager-ui",
        },
        body: outgoing,
      }
    );

    const body = await upstream.text();
    return new Response(body, {
      status: upstream.status,
      headers: {
        "Content-Type": upstream.headers.get("Content-Type") ?? "application/json",
      },
    });
  } catch {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Upstream Error",
      status: 502,
      detail: "Failed to reach ProxyManager.Files",
    });
  }
}
