import { notFound } from "next/navigation";
import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import { getRoute } from "@/lib/proxy-manager-client";
import type { ProblemDetails } from "@/types";
import RouteDetailClient from "./RouteDetailClient";

export default async function RouteDetailPage({
  params,
}: {
  params: Promise<{ id: string }>;
}) {
  const { id } = await params;
  const headersList = await headers();
  const session = getSession(headersList);

  let route;
  try {
    route = await getRoute(session, id);
  } catch (err) {
    const problem = err as ProblemDetails;
    if (problem?.status === 404) {
      notFound();
    }
    throw err;
  }

  return (
    <div className="p-6 max-w-xl space-y-6">
      <h1 className="text-xl font-semibold">{route.name}</h1>
      <RouteDetailClient route={route} isAdmin={session.isAdmin} />
      {/* MaintainerPanel placeholder — rendered in Phase 4 (T044) */}
    </div>
  );
}
