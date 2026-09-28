"use client";

import { useEffect, useState } from "react";
import { apiFetch } from "@/lib/api-fetch";
import type { HealthState } from "@/types";

export const HEALTH_POLL_INTERVAL_MS = 10_000;

/**
 * Polls the proxy's live health endpoint (ADR 0003) on mount and every 10 s. A non-OK response or
 * network error leaves the map empty — e.g. the UI dev server running without the proxy in front.
 */
export function useHealthStates(): Map<string, HealthState> {
  const [states, setStates] = useState<Map<string, HealthState>>(() => new Map());

  useEffect(() => {
    let cancelled = false;

    async function poll() {
      let next = new Map<string, HealthState>();
      try {
        const response = await apiFetch("/manage/api/health-states", { cache: "no-store" });
        if (response.ok) {
          const list = (await response.json()) as HealthState[];
          next = new Map(list.map((s) => [s.proxyHostId, s]));
        }
      } catch {
        // Leave the map empty.
      }
      if (!cancelled) setStates(next);
    }

    poll();
    const timer = setInterval(poll, HEALTH_POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      clearInterval(timer);
    };
  }, []);

  return states;
}
