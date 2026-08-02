import { getSession } from "@/lib/auth";
import { getCertificate, updateCertificate, deleteCertificate } from "@/lib/proxy-manager-client";
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
    const certificate = await getCertificate(session, id);
    return Response.json(certificate);
  } catch (err) {
    return upstreamError(err);
  }
}

export async function PUT(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
): Promise<Response> {
  const session = getSession(request.headers);

  if (!session.isAdmin) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Forbidden",
      status: 403,
      detail: "Only administrators can update certificates",
    });
  }

  const { id } = await params;

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
    const updated = await updateCertificate(
      session,
      id,
      body as Parameters<typeof updateCertificate>[2]
    );
    return Response.json(updated);
  } catch (err) {
    return upstreamError(err);
  }
}

export async function DELETE(
  request: Request,
  { params }: { params: Promise<{ id: string }> }
): Promise<Response> {
  const session = getSession(request.headers);

  if (!session.isAdmin) {
    return problemResponse({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Forbidden",
      status: 403,
      detail: "Only administrators can delete certificates",
    });
  }

  const { id } = await params;

  try {
    await deleteCertificate(session, id);
    return new Response(null, { status: 204 });
  } catch (err) {
    return upstreamError(err);
  }
}
