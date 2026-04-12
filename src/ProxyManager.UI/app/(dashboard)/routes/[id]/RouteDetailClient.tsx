"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import RouteForm from "@/components/routes/RouteForm";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { ProxyHost, ProblemDetails } from "@/types";

type RouteFormPayload = Omit<ProxyHost, "id" | "createdAt" | "updatedAt">;

interface RouteDetailClientProps {
  id: string;
  isAdmin: boolean;
}

export default function RouteDetailClient({ id, isAdmin }: RouteDetailClientProps) {
  const router = useRouter();
  const [route, setRoute] = useState<ProxyHost | null>(null);
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
    loadRoute();
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
      <h1 className="text-xl font-semibold">{route.name}</h1>
      <RouteForm
        initialData={route}
        onSubmit={handleSubmit}
        readOnly={!isAdmin}
        submitLabel="Save Changes"
        error={error}
      />

      {isAdmin && (
        <div className="border-t pt-4">
          <Button
            variant="destructive"
            size="sm"
            onClick={() => setShowDeleteDialog(true)}
          >
            Delete Route
          </Button>
        </div>
      )}

      <Dialog open={showDeleteDialog} onOpenChange={setShowDeleteDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Delete Route</DialogTitle>
            <DialogDescription>
              Are you sure you want to delete &ldquo;{route.name}&rdquo;? This action cannot be undone.
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
