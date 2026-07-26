import { HeartPulseIcon } from "lucide-react";

export default function HealthChecksPage() {
  return (
    <div className="mx-auto max-w-5xl p-6 sm:p-8 space-y-8">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight text-gradient">
          Health Checks
        </h1>
        <p className="text-sm text-muted-foreground">
          Monitor the availability of your upstream destinations.
        </p>
      </div>

      <div className="flex flex-col items-center justify-center gap-4 rounded-2xl border border-dashed border-border bg-card/40 py-20 text-center">
        <span className="flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary ring-1 ring-primary/20">
          <HeartPulseIcon className="h-6 w-6" />
        </span>
        <div className="space-y-1">
          <p className="font-medium">Coming soon</p>
          <p className="text-sm text-muted-foreground">
            Live health monitoring for your routes is on the way.
          </p>
        </div>
      </div>
    </div>
  );
}
