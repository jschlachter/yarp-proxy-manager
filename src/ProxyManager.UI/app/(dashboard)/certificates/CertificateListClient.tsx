"use client";

import { useEffect, useState } from "react";
import { TriangleAlertIcon } from "lucide-react";
import CertificateList from "@/components/certificates/CertificateList";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import type { Certificate } from "@/types";

interface CertificateListClientProps {
  isAdmin: boolean;
}

export default function CertificateListClient({ isAdmin }: CertificateListClientProps) {
  const [certificates, setCertificates] = useState<Certificate[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  async function refresh() {
    setIsLoading(true);
    try {
      const response = await fetch("/manage/api/certificates");
      if (response.ok) {
        const data = (await response.json()) as { items: Certificate[] };
        setCertificates(data.items);
      }
    } finally {
      setIsLoading(false);
    }
  }

  useEffect(() => {
    refresh();
  }, []);

  const pendingCertificate = pendingDeleteId
    ? certificates.find((cert) => cert.id === pendingDeleteId)
    : undefined;
  const [deleteError, setDeleteError] = useState<string | undefined>();

  async function handleConfirmDelete() {
    if (!pendingDeleteId) return;
    const id = pendingDeleteId;
    setPendingDeleteId(null);
    setDeleteError(undefined);
    const response = await fetch(`/manage/api/certificates/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
    if (!response.ok) {
      setDeleteError("Failed to delete certificate");
      return;
    }
    await refresh();
  }

  if (isLoading) return null;

  return (
    <>
      {deleteError && (
        <div
          role="alert"
          className="mb-4 rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive"
        >
          {deleteError}
        </div>
      )}
      <CertificateList
        certificates={certificates}
        isAdmin={isAdmin}
        onDelete={setPendingDeleteId}
      />

      <Dialog open={!!pendingDeleteId} onOpenChange={(open) => !open && setPendingDeleteId(null)}>
        <DialogContent>
          <DialogHeader>
            <div className="flex items-center gap-3">
              <span className="flex h-9 w-9 shrink-0 items-center justify-center rounded-full bg-destructive/10 text-destructive ring-1 ring-destructive/20">
                <TriangleAlertIcon className="h-4.5 w-4.5" />
              </span>
              <DialogTitle>Delete Certificate</DialogTitle>
            </div>
            <DialogDescription>
              Are you sure you want to delete &ldquo;{pendingCertificate?.name}&rdquo;? This action cannot be undone.
            </DialogDescription>
          </DialogHeader>
          <DialogFooter>
            <Button variant="outline" onClick={() => setPendingDeleteId(null)}>
              Cancel
            </Button>
            <Button variant="destructive" onClick={handleConfirmDelete}>
              Delete
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
