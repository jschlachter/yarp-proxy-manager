"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Separator } from "@/components/ui/separator";
import HealthCheckFields from "@/components/routes/HealthCheckFields";
import {
  ACTIVE_HEALTH_POLICIES,
  PASSIVE_HEALTH_POLICIES,
  toFormState,
  toPayload,
  validateHealthCheck,
  type HealthCheckErrors,
} from "@/lib/health-checks";
import { cn } from "@/lib/utils";
import type { HealthCheck, ProxyHost, TlsMode } from "@/types";
import type { CreateRouteRequest, UpdateRouteRequest } from "@/lib/proxy-manager-client";

export type RouteFormPayload = CreateRouteRequest & UpdateRouteRequest;

interface RouteFormProps {
  initialData?: ProxyHost;
  onSubmit: (payload: RouteFormPayload) => void;
  readOnly?: boolean;
  submitLabel?: string;
  error?: string;
}

interface FormErrors {
  destinationUri?: string;
  domainNames?: string;
  healthCheck?: HealthCheckErrors;
}

const policyLabel = (options: { value: string; label: string }[], value: string) =>
  options.find((o) => o.value === value)?.label ?? value;

/** One summary line per enabled check, listing only the fields that are set. */
function healthCheckSummary(healthCheck: HealthCheck): string[] {
  const lines: string[] = [];
  const { active, passive } = healthCheck;
  if (active) {
    const parts = [
      active.intervalSeconds != null && `every ${active.intervalSeconds}s`,
      active.timeoutSeconds != null && `timeout ${active.timeoutSeconds}s`,
      (active.path || active.query) && `${active.path ?? ""}${active.query ?? ""}`,
      active.healthAddress && `via ${active.healthAddress}`,
      active.consecutiveFailuresThreshold != null && `threshold ${active.consecutiveFailuresThreshold}`,
    ].filter(Boolean);
    lines.push(`Active: ${policyLabel(ACTIVE_HEALTH_POLICIES, active.policy)}${parts.length ? ` (${parts.join(", ")})` : ""}`);
  }
  if (passive) {
    const parts = [
      passive.reactivationPeriodSeconds != null && `reactivate after ${passive.reactivationPeriodSeconds}s`,
      passive.failureRateLimit != null && `rate limit ${passive.failureRateLimit}`,
    ].filter(Boolean);
    lines.push(`Passive: ${policyLabel(PASSIVE_HEALTH_POLICIES, passive.policy)}${parts.length ? ` (${parts.join(", ")})` : ""}`);
  }
  if (lines.length > 0)
    lines.push(
      healthCheck.availableDestinationsPolicy === "HealthyOrPanic"
        ? "Keeps sending traffic when unhealthy"
        : "Stops traffic when unhealthy"
    );
  return lines;
}

export default function RouteForm({
  initialData,
  onSubmit,
  readOnly = false,
  submitLabel = initialData ? "Save Changes" : "Create Route",
  error,
}: RouteFormProps) {
  const [destinationUri, setDestinationUri] = useState(initialData?.destination ?? "");
  const [domainNamesRaw, setDomainNamesRaw] = useState(
    initialData?.domainNames.join(", ") ?? ""
  );
  const [isEnabled, setIsEnabled] = useState(initialData?.isEnabled ?? true);
  const [tlsMode, setTlsMode] = useState<TlsMode>(initialData?.tlsMode ?? "Manual");
  const [healthCheck, setHealthCheck] = useState(() => toFormState(initialData?.healthCheck));
  const [errors, setErrors] = useState<FormErrors>({});

  function validate(): FormErrors {
    const errs: FormErrors = {};
    if (!destinationUri.trim()) errs.destinationUri = "Destination URL is required";
    if (!domainNamesRaw.trim()) errs.domainNames = "At least one domain name is required";
    const healthErrs = validateHealthCheck(healthCheck);
    if (Object.keys(healthErrs).length > 0) errs.healthCheck = healthErrs;
    return errs;
  }

  function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const errs = validate();
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }
    setErrors({});
    const domainNames = domainNamesRaw
      .split(",")
      .map((h) => h.trim())
      .filter(Boolean);
    // Always send healthCheck so turning both checks off on an edit clears them.
    onSubmit({ domainNames, destinationUri, isEnabled, tlsMode, healthCheck: toPayload(healthCheck) });
  }

  if (readOnly && initialData) {
    const healthSummary = initialData.healthCheck ? healthCheckSummary(initialData.healthCheck) : [];
    return (
      <div className="space-y-4">
        <div>
          <Label>Destination URL</Label>
          <p className="mt-1 text-sm">{initialData.destination}</p>
        </div>
        <div>
          <Label>Domain Names</Label>
          <p className="mt-1 text-sm">{initialData.domainNames.join(", ")}</p>
        </div>
        <div>
          <Label>Status</Label>
          <p className="mt-1 text-sm">{initialData.isEnabled ? "Enabled" : "Disabled"}</p>
        </div>
        <div>
          <Label>TLS Mode</Label>
          <p className="mt-1 text-sm">
            {initialData.tlsMode === "LetsEncrypt" ? "Let's Encrypt" : "Manual"}
          </p>
        </div>
        <div>
          <Label>Health checks</Label>
          {healthSummary.length > 0 ? (
            <ul className="mt-1 space-y-0.5 text-sm">
              {healthSummary.map((line) => (
                <li key={line}>{line}</li>
              ))}
            </ul>
          ) : (
            <p className="mt-1 text-sm">Off</p>
          )}
        </div>
      </div>
    );
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4">
      {error && (
        <div role="alert" className="rounded-md border border-destructive/50 bg-destructive/10 p-3 text-sm text-destructive">
          {error}
        </div>
      )}

      <div className="space-y-1.5">
        <Label htmlFor="destinationUri">Destination URL</Label>
        <Input
          id="destinationUri"
          value={destinationUri}
          onChange={(e) => setDestinationUri(e.target.value)}
          placeholder="http://backend:8080"
          aria-invalid={!!errors.destinationUri}
          aria-describedby={errors.destinationUri ? "destination-error" : undefined}
        />
        {errors.destinationUri && (
          <p id="destination-error" role="alert" className="text-sm text-destructive">
            {errors.destinationUri}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="domainNames">Domain Names</Label>
        <Input
          id="domainNames"
          value={domainNamesRaw}
          onChange={(e) => setDomainNamesRaw(e.target.value)}
          placeholder="example.com, other.example.com"
          aria-invalid={!!errors.domainNames}
          aria-describedby={errors.domainNames ? "domainnames-error" : undefined}
        />
        <p className="text-xs text-muted-foreground">Comma-separated list of domain names</p>
        {errors.domainNames && (
          <p id="domainnames-error" role="alert" className="text-sm text-destructive">
            {errors.domainNames}
          </p>
        )}
      </div>

      <div className="flex items-center gap-2">
        <input
          id="isEnabled"
          type="checkbox"
          checked={isEnabled}
          onChange={(e) => setIsEnabled(e.target.checked)}
          className="h-4 w-4 rounded border-input accent-primary"
        />
        <Label htmlFor="isEnabled">Enabled</Label>
      </div>

      <div className="space-y-1.5">
        <Label id="tlsMode-label">TLS Mode</Label>
        <div
          role="radiogroup"
          aria-labelledby="tlsMode-label"
          className="inline-flex rounded-lg border border-input bg-background/50 p-1"
        >
          <button
            type="button"
            role="radio"
            aria-checked={tlsMode === "Manual"}
            onClick={() => setTlsMode("Manual")}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              tlsMode === "Manual"
                ? "brand-gradient text-primary-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            )}
          >
            Manual
          </button>
          <button
            type="button"
            role="radio"
            aria-checked={tlsMode === "LetsEncrypt"}
            onClick={() => setTlsMode("LetsEncrypt")}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              tlsMode === "LetsEncrypt"
                ? "brand-gradient text-primary-foreground shadow-sm"
                : "text-muted-foreground hover:text-foreground"
            )}
          >
            Let&apos;s Encrypt
          </button>
        </div>
        <p className="text-xs text-muted-foreground">
          Let&apos;s Encrypt automatically issues and renews a certificate for this route&apos;s domains.
        </p>
      </div>

      <Separator />

      <div className="space-y-3">
        <div>
          <h3 className="text-sm font-semibold">Health checks</h3>
          <p className="text-xs text-muted-foreground">
            Optional. Leave a field blank to use YARP&apos;s default.
          </p>
        </div>
        <HealthCheckFields value={healthCheck} onChange={setHealthCheck} errors={errors.healthCheck} />
      </div>

      <Button type="submit">{submitLabel}</Button>
    </form>
  );
}
