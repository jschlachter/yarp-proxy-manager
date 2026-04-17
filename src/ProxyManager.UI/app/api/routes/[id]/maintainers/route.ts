import { getSession } from "@/lib/auth";
import { listMaintainers, assignMaintainer } from "@/lib/proxy-manager-client";
import type { ProblemDetails } from "@/types";

function problemResponse(problem: ProblemDetails): Response {
  return Response.json(problem, {
    status: problem.status,
    headers: { "Content-Type": "application/problem+json" },
  });
}

function upstreamError(err: unknown): Response {
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

export async function GET(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
): Promise<Response> {
  const session = getSession(request.headers);
  const { id } = await params;

  try {
    const maintainers = await listMaintainers(session, id);
    return Response.json(maintainers);
  } catch (err) {
    return upstreamError(err);
  }
}

export async function POST(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
): Promise<Response> {
  const session = getSession(request.headers);

  if (!session.isAdmin) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Forbidden",
      status: 403,
      detail: "Only administrators can manage maintainers",
    });
  }

  const { id } = await params;

  let body: { userId?: string };
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

  if (!body.userId) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Unprocessable Entity",
      status: 422,
      detail: "userId is required",
    });
  }

  try {
    const created = await assignMaintainer(session, id, body.userId);
    return Response.json(created, { status: 201 });
  } catch (err) {
    return upstreamError(err);
  }
}
