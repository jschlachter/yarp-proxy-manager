"use client";

import { Badge } from "@/components/ui/badge";
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip";
import { cn } from "@/lib/utils";
import type { DestinationHealth, HealthState } from "@/types";

const STYLES: Record<DestinationHealth, { badge: string; dot: string }> = {
  Healthy: { badge: "bg-emerald-500/15 text-emerald-400", dot: "bg-emerald-400 animate-pulse" },
  Unhealthy: { badge: "bg-red-500/15 text-red-400", dot: "bg-red-400" },
  Unknown: { badge: "bg-amber-500/15 text-amber-400", dot: "bg-amber-400" },
};

/** Live health pill for a proxy host; renders nothing when there is no state (no checks, or no proxy data). */
export default function HealthBadge({ state }: { state?: HealthState }) {
  if (!state) return null;

  const style = STYLES[state.status] ?? STYLES.Unknown;

  return (
    <Tooltip>
      <TooltipTrigger
        render={<span />}
        className="inline-flex cursor-default rounded-4xl focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
        tabIndex={0}
        aria-label={`Health: ${state.status}`}
      >
        <Badge variant="outline" className={cn("gap-1.5 border-transparent", style.badge)}>
          <span className={cn("h-1.5 w-1.5 rounded-full", style.dot)} />
          {state.status}
        </Badge>
      </TooltipTrigger>
      <TooltipContent>
        <div className="space-y-0.5">
          {state.active && <p>Active: {state.active}</p>}
          {state.passive && <p>Passive: {state.passive}</p>}
          <p className="opacity-70">Checked {new Date(state.checkedAt).toLocaleTimeString()}</p>
        </div>
      </TooltipContent>
    </Tooltip>
  );
}
