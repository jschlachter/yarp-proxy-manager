import Link from "next/link";
import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
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
  const initialPage = parseInt(resolvedParams.page ?? "1", 10);

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

      <RouteListClient isAdmin={session.isAdmin} initialPage={initialPage} />
    </div>
  );
}
