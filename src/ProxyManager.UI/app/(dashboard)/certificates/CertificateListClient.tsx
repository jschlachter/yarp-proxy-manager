"use client";

import { useState, useSyncExternalStore } from "react";
import { TriangleAlertIcon } from "lucide-react";
import { certificateStore } from "@/lib/certificate-store";
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

interface CertificateListClientProps {
  isAdmin: boolean;
}

export default function CertificateListClient({ isAdmin }: CertificateListClientProps) {
  const certificates = useSyncExternalStore(
    certificateStore.subscribe,
    certificateStore.getSnapshot,
    certificateStore.getSnapshot
  );
  const [pendingDeleteId, setPendingDeleteId] = useState<string | null>(null);

  const pendingCertificate = pendingDeleteId
    ? certificateStore.getById(pendingDeleteId)
    : undefined;

  function handleConfirmDelete() {
    if (pendingDeleteId) certificateStore.remove(pendingDeleteId);
    setPendingDeleteId(null);
  }

  return (
    <>
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
              Are you sure you want to delete &ldquo;{pendingCertificate?.friendlyName}&rdquo;? This action cannot be undone.
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
