import { getSession } from "@/lib/auth";
import { listRoutes, createRoute } from "@/lib/proxy-manager-client";
import type { ProblemDetails } from "@/types";

function problemResponse(problem: ProblemDetails): Response {
  return Response.json(problem, {
    status: problem.status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

export async function GET(request: Request): Promise<Response> {
  const session = getSession(request.headers);

  const url = new URL(request.url);
  const page = parseInt(url.searchParams.get("page") ?? "1", 10);
  const pageSize = Math.min(parseInt(url.searchParams.get("pageSize") ?? "50", 10), 50);

  try {
    const result = await listRoutes(session, page, pageSize);
    return Response.json(result);
  } catch (err) {
    const problem = err as ProblemDetails;
    return problemResponse(
      problem?.status
        ? problem
        : {
            type: "https://tools.ietf.org/html/rfc9457",
            title: "Upstream Error",
            status: 502,
            detail: "Failed to reach ProxyManager API",
          }
    );
  }
}

export async function POST(request: Request): Promise<Response> {
  const session = getSession(request.headers);

  if (!session.isAdmin) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Forbidden",
      status: 403,
      detail: "Only administrators can create routes",
    });
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Bad Request",
      status: 400,
      detail: "Request body must be valid JSON",
    });
  }

  try {
    const created = await createRoute(
      session,
      body as Parameters<typeof createRoute>[1]
    );
    return Response.json(created, { status: 201 });
  } catch (err) {
    const problem = err as ProblemDetails;
    return problemResponse(
      problem?.status
        ? problem
        : {
            type: "https://tools.ietf.org/html/rfc9457",
            title: "Upstream Error",
            status: 502,
            detail: "Failed to reach ProxyManager API",
          }
    );
  }
}
