"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ProxyHost } from "@/types";
import type { CreateRouteRequest, UpdateRouteRequest } from "@/lib/proxy-manager-client";

type RouteFormPayload = CreateRouteRequest & UpdateRouteRequest;

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
  const [errors, setErrors] = useState<FormErrors>({});

  function validate(): FormErrors {
    const errs: FormErrors = {};
    if (!destinationUri.trim()) errs.destinationUri = "Destination URL is required";
    if (!domainNamesRaw.trim()) errs.domainNames = "At least one domain name is required";
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
    onSubmit({ domainNames, destinationUri, isEnabled });
  }

  if (readOnly && initialData) {
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
          className="h-4 w-4 rounded border-input"
        />
        <Label htmlFor="isEnabled">Enabled</Label>
      </div>

      <Button type="submit">{submitLabel}</Button>
    </form>
  );
}
