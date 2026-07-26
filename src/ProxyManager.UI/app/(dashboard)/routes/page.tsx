import Link from "next/link";
import { PlusIcon } from "lucide-react";
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
    <div className="mx-auto max-w-5xl p-6 sm:p-8 space-y-8">
      <div className="flex items-end justify-between gap-4">
        <div className="space-y-1">
          <h1 className="text-2xl font-bold tracking-tight text-gradient">
            Proxy Routes
          </h1>
          <p className="text-sm text-muted-foreground">
            Manage upstream destinations and the domains that reach them.
          </p>
        </div>
        {session.isAdmin && (
          <Link
            href="/routes/new"
            className="inline-flex items-center justify-center gap-1.5 rounded-lg brand-gradient px-4 py-2 text-sm font-medium text-primary-foreground shadow-lg shadow-primary/25 transition-all hover:shadow-primary/40 hover:-translate-y-0.5 active:translate-y-0"
          >
            <PlusIcon className="h-4 w-4" />
            Add Route
          </Link>
        )}
      </div>

      <RouteListClient isAdmin={session.isAdmin} initialPage={initialPage} />
    </div>
  );
}
