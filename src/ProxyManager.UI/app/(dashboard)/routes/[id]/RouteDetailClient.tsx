"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeftIcon } from "lucide-react";
import RouteForm from "@/components/routes/RouteForm";
import MaintainerPanel from "@/components/routes/MaintainerPanel";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { ProxyHost, MaintainerAssignment, ProblemDetails } from "@/types";
import type { UpdateRouteRequest } from "@/lib/proxy-manager-client";

type RouteFormPayload = UpdateRouteRequest;

interface RouteDetailClientProps {
  id: string;
  isAdmin: boolean;
}

export default function RouteDetailClient({ id, isAdmin }: RouteDetailClientProps) {
  const router = useRouter();
  const [route, setRoute] = useState<ProxyHost | null>(null);
  const [maintainers, setMaintainers] = useState<MaintainerAssignment[] | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [error, setError] = useState<string | undefined>();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  useEffect(() => {
    async function loadRoute() {
      try {
        const response = await fetch(`/manage/api/routes/${encodeURIComponent(id)}`);
        if (response.status === 404) {
          router.replace("/routes");
          return;
        }
        if (!response.ok) {
          const problem = (await response.json()) as ProblemDetails;
          setLoadError(problem.detail ?? "Unable to load route.");
          return;
        }
        setRoute((await response.json()) as ProxyHost);
      } catch {
        setLoadError("Unable to load route. The ProxyManager API may be unavailable.");
      } finally {
        setIsLoading(false);
      }
    }

    async function loadMaintainers() {
      try {
        const response = await fetch(`/manage/api/routes/${encodeURIComponent(id)}/maintainers`);
        if (response.ok) {
          const data = (await response.json()) as MaintainerAssignment[];
          setMaintainers(data);
        }
        // 501 = API not yet available; leave maintainers as null (shows stub message)
      } catch {
        // Network error — leave as null
      }
    }

    loadRoute();
    loadMaintainers();
  }, [id, router]);

  async function handleSubmit(payload: RouteFormPayload) {
    setError(undefined);
    const response = await fetch(`/manage/api/routes/${encodeURIComponent(id)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    if (response.ok) {
      router.refresh();
    } else {
      const problem = (await response.json()) as ProblemDetails;
      setError(problem.detail ?? "Failed to update route");
    }
  }

  async function handleDelete() {
    const response = await fetch(`/manage/api/routes/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });

    if (response.ok) {
      router.push("/routes");
    } else {
      const problem = (await response.json()) as ProblemDetails;
      setError(problem.detail ?? "Failed to delete route");
      setShowDeleteDialog(false);
    }
  }

  if (isLoading) {
    return <div className="text-sm text-muted-foreground">Loading...</div>;
  }

  if (loadError) {
    return (
      <div
        role="alert"
        className="rounded-md border border-destructive/50 bg-destructive/10 p-4 text-sm text-destructive"
      >
        {loadError}
      </div>
    );
  }

  if (!route) return null;

  return (
    <div className="space-y-6">
      <Link
        href="/routes"
        className="inline-flex items-center gap-1.5 text-sm text-muted-foreground transition-colors hover:text-foreground"
      >
        <ArrowLeftIcon className="h-4 w-4" />
        Back to routes
      </Link>
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">
          {route.domainNames[0] ?? route.destination}
        </h1>
        <p className="text-sm text-muted-foreground font-mono">{route.destination}</p>
      </div>

      <div className="rounded-xl border border-border bg-card/80 p-6 backdrop-blur-sm">
        <RouteForm
          initialData={route}
          onSubmit={handleSubmit}
          readOnly={!isAdmin}
          submitLabel="Save Changes"
          error={error}
        />

        {isAdmin && (
          <div className="mt-6 border-t border-border pt-4">
            <Button
              variant="destructive"
              size="sm"
              onClick={() => setShowDeleteDialog(true)}
            >
              Delete Route
            </Button>
          </div>
        )}
      </div>

      <MaintainerPanel
        routeId={id}
        maintainers={maintainers}
        isAdmin={isAdmin}
      />

      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Route</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete &ldquo;{route.domainNames[0] ?? route.destination}&rdquo;? This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setShowDeleteDialog(false)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleDelete}>
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
