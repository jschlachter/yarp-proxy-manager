import Link from "next/link";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { ProxyHost } from "@/types";

interface RouteCardProps {
  route: ProxyHost;
  isAdmin: boolean;
  onDelete: (id: string) => void;
}

export default function RouteCard({ route, isAdmin, onDelete }: RouteCardProps) {
  return (
    <div className="flex items-start justify-between rounded-lg border p-4">
      <div className="min-w-0 flex-1 space-y-1">
        <div className="flex items-center gap-2">
          <span className="font-medium truncate">{route.name}</span>
          <Badge variant={route.isEnabled ? "default" : "secondary"}>
            {route.isEnabled ? "Enabled" : "Disabled"}
          </Badge>
        </div>
        <p className="text-sm text-muted-foreground truncate">{route.upstreamUrl}</p>
        <div className="flex flex-wrap gap-1">
          {route.hostnames.map((hostname) => (
            <span
              key={hostname}
              className="rounded bg-muted px-1.5 py-0.5 text-xs font-mono"
            >
              {hostname}
            </span>
          ))}
        </div>
      </div>
      {isAdmin && (
        <div className="ml-4 flex shrink-0 gap-2">
          <Link
            href={`/routes/${route.id}`}
            className="inline-flex items-center justify-center rounded-md border border-input bg-background px-3 py-1.5 text-sm font-medium shadow-xs hover:bg-accent hover:text-accent-foreground"
            aria-label="Edit"
          >
            Edit
          </Link>
          <Button
            variant="destructive"
            size="sm"
            onClick={() => onDelete(route.id)}
            aria-label="Delete"
          >
            Delete
          </Button>
        </div>
      )}
    </div>
  );
}
