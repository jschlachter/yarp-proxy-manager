"use client";

import { useState } from "react";
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
import type { ProxyHost } from "@/types";

type RouteFormPayload = Omit<ProxyHost, "id" | "createdAt" | "updatedAt">;

interface RouteDetailClientProps {
  route: ProxyHost;
  isAdmin: boolean;
}

export default function RouteDetailClient({ route, isAdmin }: RouteDetailClientProps) {
  const router = useRouter();
  const [error, setError] = useState<string | undefined>();
  const [showDeleteDialog, setShowDeleteDialog] = useState(false);

  async function handleSubmit(payload: RouteFormPayload) {
    setError(undefined);
    const response = await fetch(`/api/routes/${encodeURIComponent(route.id)}`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    });

    if (response.ok) {
      router.refresh();
    } else {
      const problem = await response.json();
      setError(problem.detail ?? "Failed to update route");
    }
  }

  async function handleDelete() {
    const response = await fetch(`/api/routes/${encodeURIComponent(route.id)}`, {
      method: "DELETE",
    });

    if (response.ok) {
      router.push("/routes");
    } else {
      const problem = await response.json();
      setError(problem.detail ?? "Failed to delete route");
      setShowDeleteDialog(false);
    }
  }

  return (
    <div className="space-y-6">
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
