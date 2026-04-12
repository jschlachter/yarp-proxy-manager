"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import type { ProxyHost } from "@/types";

type RouteFormPayload = Omit<ProxyHost, "id" | "createdAt" | "updatedAt">;

interface RouteFormProps {
  initialData?: ProxyHost;
  onSubmit: (payload: RouteFormPayload) => void;
  readOnly?: boolean;
  submitLabel?: string;
  error?: string;
}

interface FormErrors {
  name?: string;
  upstreamUrl?: string;
  hostnames?: string;
}

export default function RouteForm({
  initialData,
  onSubmit,
  readOnly = false,
  submitLabel = initialData ? "Save Changes" : "Create Route",
  error,
}: RouteFormProps) {
  const [name, setName] = useState(initialData?.name ?? "");
  const [upstreamUrl, setUpstreamUrl] = useState(initialData?.upstreamUrl ?? "");
  const [hostnamesRaw, setHostnamesRaw] = useState(
    initialData?.hostnames.join(", ") ?? ""
  );
  const [pathPrefix, setPathPrefix] = useState(initialData?.pathPrefix ?? "");
  const [isEnabled, setIsEnabled] = useState(initialData?.isEnabled ?? true);
  const [errors, setErrors] = useState<FormErrors>({});

  function validate(): FormErrors {
    const errs: FormErrors = {};
    if (!name.trim()) errs.name = "Name is required";
    if (!upstreamUrl.trim()) errs.upstreamUrl = "Upstream URL is required";
    if (!hostnamesRaw.trim()) errs.hostnames = "At least one hostname is required";
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
    const hostnames = hostnamesRaw
      .split(",")
      .map((h) => h.trim())
      .filter(Boolean);
    onSubmit({ name, upstreamUrl, hostnames, pathPrefix: pathPrefix || undefined, isEnabled });
  }

  if (readOnly && initialData) {
    return (
      <div className="space-y-4">
        <div>
          <Label>Name</Label>
          <p className="mt-1 text-sm">{initialData.name}</p>
        </div>
        <div>
          <Label>Upstream URL</Label>
          <p className="mt-1 text-sm">{initialData.upstreamUrl}</p>
        </div>
        <div>
          <Label>Hostnames</Label>
          <p className="mt-1 text-sm">{initialData.hostnames.join(", ")}</p>
        </div>
        {initialData.pathPrefix && (
          <div>
            <Label>Path Prefix</Label>
            <p className="mt-1 text-sm">{initialData.pathPrefix}</p>
          </div>
        )}
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
        <Label htmlFor="name">Name</Label>
        <Input
          id="name"
          value={name}
          onChange={(e) => setName(e.target.value)}
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? "name-error" : undefined}
        />
        {errors.name && (
          <p id="name-error" role="alert" className="text-sm text-destructive">
            {errors.name}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="upstreamUrl">Upstream URL</Label>
        <Input
          id="upstreamUrl"
          value={upstreamUrl}
          onChange={(e) => setUpstreamUrl(e.target.value)}
          placeholder="http://backend:8080"
          aria-invalid={!!errors.upstreamUrl}
          aria-describedby={errors.upstreamUrl ? "upstream-error" : undefined}
        />
        {errors.upstreamUrl && (
          <p id="upstream-error" role="alert" className="text-sm text-destructive">
            {errors.upstreamUrl}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="hostnames">Hostnames</Label>
        <Input
          id="hostnames"
          value={hostnamesRaw}
          onChange={(e) => setHostnamesRaw(e.target.value)}
          placeholder="example.com, other.example.com"
          aria-invalid={!!errors.hostnames}
          aria-describedby={errors.hostnames ? "hostnames-error" : undefined}
        />
        <p className="text-xs text-muted-foreground">Comma-separated list of hostnames</p>
        {errors.hostnames && (
          <p id="hostnames-error" role="alert" className="text-sm text-destructive">
            {errors.hostnames}
          </p>
        )}
      </div>

      <div className="space-y-1.5">
        <Label htmlFor="pathPrefix">Path Prefix (optional)</Label>
        <Input
          id="pathPrefix"
          value={pathPrefix}
          onChange={(e) => setPathPrefix(e.target.value)}
          placeholder="/api"
        />
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
