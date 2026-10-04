import type {
  ActiveHealthPolicy,
  AvailableDestinationsPolicy,
  HealthCheck,
  PassiveHealthPolicy,
} from "@/types";

/** Dropdown sources: the value is YARP's policy name, the label its class name (ADR 0003). */
export const ACTIVE_HEALTH_POLICIES: { value: ActiveHealthPolicy; label: string }[] = [
  { value: "ConsecutiveFailures", label: "ConsecutiveFailuresHealthPolicy" },
];

export const PASSIVE_HEALTH_POLICIES: { value: PassiveHealthPolicy; label: string }[] = [
  { value: "TransportFailureRate", label: "TransportFailureRateHealthPolicy" },
];

/** Form-side health check state. Tuning fields are raw input strings; blank means YARP's default. */
export interface HealthCheckFormState {
  availableDestinationsPolicy: AvailableDestinationsPolicy;
  activeEnabled: boolean;
  activePolicy: ActiveHealthPolicy;
  intervalSeconds: string;
  timeoutSeconds: string;
  path: string;
  query: string;
  healthAddress: string;
  consecutiveFailuresThreshold: string;
  passiveEnabled: boolean;
  passivePolicy: PassiveHealthPolicy;
  reactivationPeriodSeconds: string;
  failureRateLimit: string;
}

export type HealthCheckErrors = Partial<Record<keyof HealthCheckFormState, string>>;

const str = (value: string | number | null | undefined) => (value == null ? "" : String(value));

export function toFormState(healthCheck?: HealthCheck | null): HealthCheckFormState {
  const active = healthCheck?.active;
  const passive = healthCheck?.passive;
  return {
    availableDestinationsPolicy: healthCheck?.availableDestinationsPolicy ?? "HealthyAndUnknown",
    activeEnabled: !!active,
    activePolicy: active?.policy ?? ACTIVE_HEALTH_POLICIES[0].value,
    intervalSeconds: str(active?.intervalSeconds),
    timeoutSeconds: str(active?.timeoutSeconds),
    path: str(active?.path),
    query: str(active?.query),
    healthAddress: str(active?.healthAddress),
    consecutiveFailuresThreshold: str(active?.consecutiveFailuresThreshold),
    passiveEnabled: !!passive,
    passivePolicy: passive?.policy ?? PASSIVE_HEALTH_POLICIES[0].value,
    reactivationPeriodSeconds: str(passive?.reactivationPeriodSeconds),
    failureRateLimit: str(passive?.failureRateLimit),
  };
}

const text = (value: string) => value.trim() || null;
const num = (value: string) => (value.trim() ? Number(value) : null);

/** Builds the request payload. Both checks off yields `{ active: null, passive: null }`, which clears them. */
export function toPayload(state: HealthCheckFormState): HealthCheck {
  return {
    availableDestinationsPolicy: state.availableDestinationsPolicy,
    active: state.activeEnabled
      ? {
          policy: state.activePolicy,
          intervalSeconds: num(state.intervalSeconds),
          timeoutSeconds: num(state.timeoutSeconds),
          path: text(state.path),
          query: text(state.query),
          healthAddress: text(state.healthAddress),
          consecutiveFailuresThreshold: num(state.consecutiveFailuresThreshold),
        }
      : null,
    passive: state.passiveEnabled
      ? {
          policy: state.passivePolicy,
          reactivationPeriodSeconds: num(state.reactivationPeriodSeconds),
          failureRateLimit: num(state.failureRateLimit),
        }
      : null,
  };
}

function isWholeAtLeast(value: string, min: number) {
  const n = Number(value);
  return Number.isInteger(n) && n >= min;
}

function isHttpUrl(value: string) {
  try {
    const url = new URL(value);
    return url.protocol === "http:" || url.protocol === "https:";
  } catch {
    return false;
  }
}

/** Mirrors the server's domain rules so the form can show errors inline. Only enabled checks are validated. */
export function validateHealthCheck(state: HealthCheckFormState): HealthCheckErrors {
  const errs: HealthCheckErrors = {};
  const filled = (value: string) => value.trim() !== "";

  if (state.activeEnabled) {
    for (const key of ["intervalSeconds", "timeoutSeconds"] as const) {
      if (filled(state[key]) && !isWholeAtLeast(state[key], 1))
        errs[key] = "Must be a whole number of seconds greater than 0";
    }
    if (filled(state.path) && !state.path.trim().startsWith("/")) errs.path = "Path must start with /";
    if (filled(state.query) && !state.query.trim().startsWith("?")) errs.query = "Query must start with ?";
    if (filled(state.healthAddress) && !isHttpUrl(state.healthAddress.trim()))
      errs.healthAddress = "Must be an absolute http or https URL";
    if (filled(state.consecutiveFailuresThreshold) && !isWholeAtLeast(state.consecutiveFailuresThreshold, 1))
      errs.consecutiveFailuresThreshold = "Must be a whole number of at least 1";
  }

  if (state.passiveEnabled) {
    if (filled(state.reactivationPeriodSeconds) && !isWholeAtLeast(state.reactivationPeriodSeconds, 1))
      errs.reactivationPeriodSeconds = "Must be a whole number of seconds greater than 0";
    if (filled(state.failureRateLimit)) {
      const rate = Number(state.failureRateLimit);
      if (!(rate > 0 && rate < 1)) errs.failureRateLimit = "Must be between 0 and 1 (exclusive)";
    }
  }

  return errs;
}
