"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  ACTIVE_HEALTH_POLICIES,
  PASSIVE_HEALTH_POLICIES,
  type HealthCheckErrors,
  type HealthCheckFormState,
} from "@/lib/health-checks";
import { cn } from "@/lib/utils";
import type { ActiveHealthPolicy, AvailableDestinationsPolicy, PassiveHealthPolicy } from "@/types";

interface HealthCheckFieldsProps {
  value: HealthCheckFormState;
  onChange: (value: HealthCheckFormState) => void;
  errors?: HealthCheckErrors;
}

type TextField = {
  [K in keyof HealthCheckFormState]: HealthCheckFormState[K] extends string ? K : never;
}[keyof HealthCheckFormState];

const selectClass =
  "h-8 w-full rounded-lg border border-input bg-transparent px-2.5 py-1 text-base outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 md:text-sm dark:bg-input/30";

const AVAILABLE_DESTINATION_OPTIONS: { value: AvailableDestinationsPolicy; label: string }[] = [
  { value: "HealthyAndUnknown", label: "Stop traffic when unhealthy" },
  { value: "HealthyOrPanic", label: "Keep sending traffic" },
];

/** Controlled editor for a proxy host's YARP health checks (ADR 0003). */
export default function HealthCheckFields({ value, onChange, errors = {} }: HealthCheckFieldsProps) {
  const set = (patch: Partial<HealthCheckFormState>) => onChange({ ...value, ...patch });

  function field(key: TextField, label: string, opts: { placeholder?: string; help?: string; inputMode?: "numeric" | "decimal" } = {}) {
    const id = `hc-${key}`;
    const errorId = `${id}-error`;
    return (
      <div className="space-y-1.5">
        <Label htmlFor={id}>{label}</Label>
        <Input
          id={id}
          value={value[key]}
          onChange={(e) => set({ [key]: e.target.value })}
          placeholder={opts.placeholder}
          inputMode={opts.inputMode}
          aria-invalid={!!errors[key]}
          aria-describedby={errors[key] ? errorId : undefined}
        />
        {opts.help && <p className="text-xs text-muted-foreground">{opts.help}</p>}
        {errors[key] && (
          <p id={errorId} role="alert" className="text-sm text-destructive">
            {errors[key]}
          </p>
        )}
      </div>
    );
  }

  function panel(enabled: boolean, toggleId: string, title: string, description: string, onToggle: (on: boolean) => void, children: React.ReactNode) {
    return (
      <div
        className={cn(
          "rounded-lg border p-4 transition-colors",
          enabled ? "border-primary/40 bg-primary/5" : "border-input bg-background/50"
        )}
      >
        <div className="flex items-start gap-2">
          <input
            id={toggleId}
            type="checkbox"
            checked={enabled}
            onChange={(e) => onToggle(e.target.checked)}
            className="mt-0.5 h-4 w-4 rounded border-input accent-primary"
          />
          <div>
            <Label htmlFor={toggleId}>{title}</Label>
            <p className="text-xs text-muted-foreground">{description}</p>
          </div>
        </div>
        {enabled && <div className="mt-4 grid gap-4 sm:grid-cols-2">{children}</div>}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      {panel(
        value.activeEnabled,
        "hc-activeEnabled",
        "Active health check",
        "Periodically probe a health endpoint on the backend.",
        (on) => set({ activeEnabled: on }),
        <>
          <div className="space-y-1.5 sm:col-span-2">
            <Label htmlFor="hc-activePolicy">Active policy</Label>
            <select
              id="hc-activePolicy"
              value={value.activePolicy}
              onChange={(e) => set({ activePolicy: e.target.value as ActiveHealthPolicy })}
              className={selectClass}
            >
              {ACTIVE_HEALTH_POLICIES.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
          </div>
          {field("intervalSeconds", "Interval (s)", { placeholder: "15", inputMode: "numeric" })}
          {field("timeoutSeconds", "Timeout (s)", { placeholder: "10", inputMode: "numeric" })}
          {field("path", "Path", { placeholder: "/health" })}
          {field("query", "Query", { placeholder: "?ready=true" })}
          {field("healthAddress", "Health address", {
            placeholder: "http://backend:8081",
            help: "Probe this address instead of the destination",
          })}
          {field("consecutiveFailuresThreshold", "Failure threshold", { placeholder: "2", inputMode: "numeric" })}
        </>
      )}

      {panel(
        value.passiveEnabled,
        "hc-passiveEnabled",
        "Passive health check",
        "Watch proxied traffic for transport failures.",
        (on) => set({ passiveEnabled: on }),
        <>
          <div className="space-y-1.5 sm:col-span-2">
            <Label htmlFor="hc-passivePolicy">Passive policy</Label>
            <select
              id="hc-passivePolicy"
              value={value.passivePolicy}
              onChange={(e) => set({ passivePolicy: e.target.value as PassiveHealthPolicy })}
              className={selectClass}
            >
              {PASSIVE_HEALTH_POLICIES.map((p) => (
                <option key={p.value} value={p.value}>
                  {p.label}
                </option>
              ))}
            </select>
          </div>
          {field("reactivationPeriodSeconds", "Reactivation period (s)", { placeholder: "Default", inputMode: "numeric" })}
          {field("failureRateLimit", "Failure rate limit (0–1)", { placeholder: "0.3", inputMode: "decimal" })}
        </>
      )}

      {(value.activeEnabled || value.passiveEnabled) && (
        <div className="space-y-1.5">
          <Label id="hc-availableDestinationsPolicy-label">When the backend is unhealthy</Label>
          <div
            role="radiogroup"
            aria-labelledby="hc-availableDestinationsPolicy-label"
            className="inline-flex rounded-lg border border-input bg-background/50 p-1"
          >
            {AVAILABLE_DESTINATION_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                role="radio"
                aria-checked={value.availableDestinationsPolicy === option.value}
                onClick={() => set({ availableDestinationsPolicy: option.value })}
                className={cn(
                  "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
                  value.availableDestinationsPolicy === option.value
                    ? "brand-gradient text-primary-foreground shadow-sm"
                    : "text-muted-foreground hover:text-foreground"
                )}
              >
                {option.label}
              </button>
            ))}
          </div>
          <p className="text-xs text-muted-foreground">
            With &ldquo;Stop traffic when unhealthy&rdquo;, requests to an unhealthy backend return 503 &mdash; a
            wrong health path takes this route offline. &ldquo;Keep sending traffic&rdquo; forwards requests anyway.
          </p>
        </div>
      )}
    </div>
  );
}
