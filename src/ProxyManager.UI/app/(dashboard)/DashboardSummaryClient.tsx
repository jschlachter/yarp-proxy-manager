"use client";

import { useEffect, useState, useCallback } from "react";
import Link from "next/link";
import { RouteIcon, ShieldCheckIcon, HeartPulseIcon } from "lucide-react";
import type { ProblemDetails } from "@/types";
import type { PaginatedRoutes, PaginatedCertificates } from "@/lib/proxy-manager-client";

function pluralize(count: number, singular: string): string {
  return `${count} ${singular}${count === 1 ? "" : "s"}`;
}

export default function DashboardSummaryClient() {
  const [routeCount, setRouteCount] = useState<number | null>(null);
  const [certificateCount, setCertificateCount] = useState<number | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [fetchError, setFetchError] = useState<string | null>(null);

  const loadSummary = useCallback(async () => {
    setIsLoading(true);
    setFetchError(null);
    try {
      const [routesResponse, certificatesResponse] = await Promise.all([
        fetch("/manage/api/routes?page=1&pageSize=1"),
        fetch("/manage/api/certificates?page=1&pageSize=1"),
      ]);

      if (!routesResponse.ok) {
        const problem = (await routesResponse.json()) as ProblemDetails;
        setFetchError(problem.detail ?? "Unable to load dashboard summary.");
        return;
      }
      if (!certificatesResponse.ok) {
        const problem = (await certificatesResponse.json()) as ProblemDetails;
        setFetchError(problem.detail ?? "Unable to load dashboard summary.");
        return;
      }

      const routes = (await routesResponse.json()) as PaginatedRoutes;
      const certificates = (await certificatesResponse.json()) as PaginatedCertificates;
      setRouteCount(routes.totalCount);
      setCertificateCount(certificates.totalCount);
    } catch {
      setFetchError(
        "Unable to load dashboard summary. The ProxyManager API may be unavailable."
      );
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSummary();
  }, [loadSummary]);

  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
      <Link
        href="/routes"
        className="rounded-2xl border border-border bg-card/40 p-5 flex flex-row items-center justify-between gap-3 transition-colors hover:bg-card/70"
      >
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <RouteIcon className="h-5 w-5" />
        </span>
        <div className="text-right">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">Loading dashboard...</p>
          ) : fetchError ? (
            <p className="text-2xl font-bold">—</p>
          ) : (
            <p className="text-2xl font-bold whitespace-nowrap">{pluralize(routeCount ?? 0, "Route")}</p>
          )}
        </div>
      </Link>

      <Link
        href="/certificates"
        className="rounded-2xl border border-border bg-card/40 p-5 flex flex-row items-center justify-between gap-3 transition-colors hover:bg-card/70"
      >
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <ShieldCheckIcon className="h-5 w-5" />
        </span>
        <div className="text-right">
          {isLoading ? (
            <p className="text-sm text-muted-foreground">Loading dashboard...</p>
          ) : fetchError ? (
            <p className="text-2xl font-bold">—</p>
          ) : (
            <p className="text-2xl font-bold whitespace-nowrap">{pluralize(certificateCount ?? 0, "Certificate")}</p>
          )}
        </div>
      </Link>

      <Link
        href="/health-checks"
        className="rounded-2xl border border-dashed border-border bg-card/40 p-5 flex flex-row items-center justify-between gap-3 transition-colors hover:bg-card/70"
      >
        <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <HeartPulseIcon className="h-5 w-5" />
        </span>
        <div className="space-y-1 text-right">
          <p className="text-sm text-muted-foreground">Health Checks</p>
          <p className="text-sm font-medium text-muted-foreground">Not yet configured</p>
        </div>
      </Link>

      {fetchError && (
        <div
          role="alert"
          className="sm:col-span-2 lg:col-span-3 rounded-md border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive"
        >
          {fetchError}
        </div>
      )}
    </div>
  );
}
