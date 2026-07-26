"use client";

import Link from "next/link";
import { RouteIcon } from "lucide-react";
import RouteCard from "./RouteCard";
import type { ProxyHost } from "@/types";

interface RouteListProps {
  routes: ProxyHost[];
  total: number;
  page: number;
  pageSize: number;
  isAdmin: boolean;
  onDelete: (id: string) => void;
}

export default function RouteList({
  routes = [],
  total,
  page,
  pageSize,
  isAdmin,
  onDelete,
}: RouteListProps) {
  const totalPages = Math.ceil(total / pageSize);
  const showPagination = total > pageSize;

  if (routes.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center gap-4 rounded-2xl border border-dashed border-border bg-card/40 py-20 text-center">
        <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <RouteIcon className="h-6 w-6" />
        </span>
        <div className="space-y-1">
          <p className="font-medium">No routes configured yet</p>
          <p className="text-sm text-muted-foreground">
            Create your first route to start proxying traffic.
          </p>
        </div>
        {isAdmin && (
          <Link
            href="/routes/new"
            className="inline-flex items-center justify-center rounded-lg brand-gradient px-4 py-2 text-sm font-medium text-primary-foreground shadow-lg shadow-primary/25 transition-all hover:-translate-y-0.5"
            aria-label="Add Route"
          >
            Add Route
          </Link>
        )}
      </div>
    );
  }

  return (
    <div className="space-y-3">
      <div className="space-y-2">
        {routes.map((route) => (
          <RouteCard
            key={route.id}
            route={route}
            isAdmin={isAdmin}
            onDelete={onDelete}
          />
        ))}
      </div>
      {showPagination && (
        <nav aria-label="Pagination" className="flex items-center justify-center gap-2 pt-4">
          <Link
            href={`?page=${page - 1}`}
            aria-disabled={page <= 1}
            className={`rounded-md border px-3 py-1.5 text-sm ${
              page <= 1 ? "pointer-events-none opacity-50" : "hover:bg-accent"
            }`}
          >
            Previous
          </Link>
          <span className="text-sm text-muted-foreground">
            Page {page} of {totalPages}
          </span>
          <Link
            href={`?page=${page + 1}`}
            aria-disabled={page >= totalPages}
            className={`rounded-md border px-3 py-1.5 text-sm ${
              page >= totalPages
                ? "pointer-events-none opacity-50"
                : "hover:bg-accent"
            }`}
          >
            Next
          </Link>
        </nav>
      )}
    </div>
  );
}
