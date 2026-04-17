import { getSession } from "@/lib/auth";
import { removeMaintainer } from "@/lib/proxy-manager-client";
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

export async function DELETE(
  request: Request,
  { params }: { params: Promise<{ id: string; userId: string }> }
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

  const { id, userId } = await params;

  try {
    await removeMaintainer(session, id, userId);
    return new Response(null, { status: 204 });
  } catch (err) {
    return upstreamError(err);
  }
}
