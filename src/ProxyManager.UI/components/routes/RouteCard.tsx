import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import type { ProxyHost } from "@/types";

interface RouteCardProps {
  route: ProxyHost;
  isAdmin: boolean;
  isMaintainer?: boolean;
  onDelete: (id: string) => void;
}

export default function RouteCard({ route, isAdmin, isMaintainer = false, onDelete }: RouteCardProps) {
  return (
    <div className="group relative flex items-start justify-between gap-4 overflow-hidden rounded-xl border border-border bg-card/80 p-4 backdrop-blur-sm transition-all hover:border-primary/40 hover:shadow-lg hover:shadow-primary/5 hover:-translate-y-0.5">
      <span
        className={cn(
          "absolute inset-y-0 left-0 w-1 transition-opacity",
          route.isEnabled ? "brand-gradient opacity-100" : "bg-muted-foreground/30 opacity-60"
        )}
      />
      <div className="min-w-0 flex-1 space-y-2 pl-2">
        <div className="flex items-center gap-2.5">
          <span className="font-semibold tracking-tight truncate">
            {route.domainNames[0] ?? route.destination}
          </span>
          <Badge
            variant="outline"
            className={cn(
              "gap-1.5 border-transparent",
              route.isEnabled
                ? "bg-success/15 text-success"
                : "bg-muted text-muted-foreground"
            )}
          >
            <span
              className={cn(
                "h-1.5 w-1.5 rounded-full",
                route.isEnabled ? "bg-success animate-pulse" : "bg-muted-foreground"
              )}
            />
            {route.isEnabled ? "Enabled" : "Disabled"}
          </Badge>
        </div>
        <p className="text-sm text-muted-foreground truncate font-mono">
          <span className="text-primary/70">→</span> {route.destination}
        </p>
        <div className="flex flex-wrap gap-1.5">
          {route.domainNames.map((hostname) => (
            <span
              key={hostname}
              className="rounded-md border border-border/60 bg-muted/60 px-2 py-0.5 text-xs font-mono text-foreground/80"
            >
              {hostname}
            </span>
          ))}
        </div>
      </div>
      {(isAdmin || isMaintainer) && (
        <div className="flex shrink-0 gap-2 opacity-70 transition-opacity group-hover:opacity-100">
          <Link
            href={`/routes/${route.id}`}
            className="inline-flex items-center justify-center rounded-lg border border-input bg-background/50 px-3 py-1.5 text-sm font-medium transition-colors hover:border-primary/40 hover:bg-accent hover:text-accent-foreground"
            aria-label="Edit"
          >
            Edit
          </Link>
          {isAdmin && (
            <Button
              variant="destructive"
              size="sm"
              onClick={() => onDelete(route.id)}
              aria-label="Delete"
            >
              Delete
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
