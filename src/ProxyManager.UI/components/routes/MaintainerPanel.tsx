"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Separator } from "@/components/ui/separator";
import type { MaintainerAssignment } from "@/types";

interface MaintainerPanelProps {
  routeId: string;
  maintainers: MaintainerAssignment[] | null;
  isAdmin: boolean;
  onAssign?: (userId: string) => Promise<void>;
  onRemove?: (userId: string) => Promise<void>;
}

export default function MaintainerPanel({
  maintainers,
  isAdmin,
  onAssign,
  onRemove,
}: MaintainerPanelProps) {
  const [assignInput, setAssignInput] = useState("");
  const [assignError, setAssignError] = useState<string | null>(null);

  // API not yet available — show stub callout
  if (maintainers === null) {
    return (
      <div className="rounded-md border border-muted bg-muted/50 p-4 text-sm text-muted-foreground">
        Maintainer management coming soon.
      </div>
    );
  }

  async function handleAssign() {
    if (!assignInput.trim()) return;
    setAssignError(null);
    try {
      await onAssign?.(assignInput.trim());
      setAssignInput("");
    } catch {
      setAssignError("Failed to assign maintainer");
    }
  }

  return (
    <div className="space-y-4">
      <h2 className="text-base font-semibold">Maintainers</h2>
      <Separator />

      {maintainers.length === 0 ? (
        <p className="text-sm text-muted-foreground">No maintainers assigned.</p>
      ) : (
        <ul className="space-y-2">
          {maintainers.map((m) => (
            <li key={m.userId} className="flex items-center justify-between text-sm">
              <span>{m.userName}</span>
              {isAdmin && (
                <Button
                  variant="ghost"
                  size="sm"
                  aria-label={`Remove ${m.userName}`}
                  onClick={() => onRemove?.(m.userId)}
                >
                  Remove
                </Button>
              )}
            </li>
          ))}
        </ul>
      )}

      {isAdmin && (
        <div className="flex gap-2">
          <Input
            placeholder="User ID to assign"
            value={assignInput}
            onChange={(e) => setAssignInput(e.target.value)}
            className="max-w-xs"
          />
          <Button onClick={handleAssign} aria-label="Assign maintainer">
            Assign
          </Button>
          {assignError && (
            <p className="text-sm text-destructive self-center">{assignError}</p>
          )}
        </div>
      )}
    </div>
  );
}
