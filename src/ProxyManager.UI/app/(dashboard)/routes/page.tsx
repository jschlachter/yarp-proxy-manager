import Link from "next/link";
import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import { listRoutes } from "@/lib/proxy-manager-client";
import RouteListClient from "./RouteListClient";

interface SearchParams {
  page?: string;
}

export default async function RoutesPage({
  searchParams,
}: {
  searchParams: Promise<SearchParams>;
}) {
  const resolvedParams = await searchParams;
  const headersList = await headers();
  const session = getSession(headersList);
  const page = parseInt(resolvedParams.page ?? "1", 10);

  let routeData: Awaited<ReturnType<typeof listRoutes>> | null = null;
  let fetchError: string | null = null;

  try {
    routeData = await listRoutes(session, page);
  } catch (error) {
    console.log("Error fetching routes:", error);
    fetchError = "Unable to load routes. The ProxyManager API may be unavailable.";
  }

  return (
    <div className="p-6 space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">Proxy Routes</h1>
        {session.isAdmin && (
          <Link
            href="/routes/new"
            className="inline-flex items-center justify-center rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground shadow hover:bg-primary/90"
          >
            Add Route
          </Link>
        )}
      </div>

      {fetchError && (
        <div
          role="alert"
          className="rounded-md border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive"
        >
          {fetchError}
        </div>
      )}

      {routeData && (
        <RouteListClient
          routes={routeData.routes}
          total={routeData.total}
          page={routeData.page}
          pageSize={routeData.pageSize}
          isAdmin={session.isAdmin}
        />
      )}
    </div>
  );
}
