"use client";

import { useRouter } from "next/navigation";
import RouteList from "@/components/routes/RouteList";
import type { ProxyHost } from "@/types";

interface RouteListClientProps {
  routes: ProxyHost[];
  total: number;
  page: number;
  pageSize: number;
  isAdmin: boolean;
}

export default function RouteListClient({
  routes,
  total,
  page,
  pageSize,
  isAdmin,
}: RouteListClientProps) {
  const router = useRouter();

  async function handleDelete(id: string) {
    if (!confirm("Are you sure you want to delete this route?")) return;

    const response = await fetch(`/api/routes/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });

    if (response.ok) {
      router.refresh();
    } else {
      const problem = await response.json();
      alert(problem.detail ?? "Failed to delete route");
    }
  }

  return (
    <RouteList
      routes={routes}
      total={total}
      page={page}
      pageSize={pageSize}
      isAdmin={isAdmin}
      onDelete={handleDelete}
    />
  );
}
