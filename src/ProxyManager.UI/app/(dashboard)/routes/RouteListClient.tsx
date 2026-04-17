"use client";

import { useEffect, useState, useCallback } from "react";
import { useRouter } from "next/navigation";
import RouteList from "@/components/routes/RouteList";
import type { ProxyHost, ProblemDetails } from "@/types";
import type { PaginatedRoutes } from "@/lib/proxy-manager-client";

interface RouteListClientProps {
  isAdmin: boolean;
  initialPage?: number;
}

export default function RouteListClient({ isAdmin, initialPage = 1 }: RouteListClientProps) {
  const router = useRouter();
  const [routeData, setRouteData] = useState<PaginatedRoutes | null>(null);
  const [fetchError, setFetchError] = useState<string | null>(null);
  const [page, setPage] = useState(initialPage);
  const [isLoading, setIsLoading] = useState(true);

  const loadRoutes = useCallback(async (pageNum: number) => {
    setIsLoading(true);
    setFetchError(null);
    try {
      const response = await fetch(`/manage/api/routes?page=${pageNum}&pageSize=50`);
      if (!response.ok) {
        const problem = (await response.json()) as ProblemDetails;
        setFetchError(problem.detail ?? "Unable to load routes.");
        return;
      }
      setRouteData((await response.json()) as PaginatedRoutes);
    } catch {
      setFetchError("Unable to load routes. The ProxyManager API may be unavailable.");
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    loadRoutes(page);
  }, [loadRoutes, page]);

  async function handleDelete(id: string) {
    if (!confirm("Are you sure you want to delete this route?")) return;

    const response = await fetch(`/manage/api/routes/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });

    if (response.ok) {
      router.refresh();
      loadRoutes(page);
    } else {
      const problem = (await response.json()) as ProblemDetails;
      alert(problem.detail ?? "Failed to delete route");
    }
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading routes...</div>;
  }

  if (fetchError) {
    return (
      <div
        role="alert"
        className="rounded-md border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive"
      >
        {fetchError}
      </div>
    );
  }

  if (!routeData) return null;

  return (
    <RouteList
      routes={routeData.items}
      total={routeData.totalCount}
      page={routeData.page}
      pageSize={routeData.pageSize}
      isAdmin={isAdmin}
      onDelete={handleDelete}
    />
  );
}
