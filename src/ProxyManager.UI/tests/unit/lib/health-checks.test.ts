import { toFormState, toPayload, validateHealthCheck } from "@/lib/health-checks";
import type { HealthCheck } from "@/types";

const full: HealthCheck = {
  availableDestinationsPolicy: "HealthyOrPanic",
  active: {
    policy: "ConsecutiveFailures",
    intervalSeconds: 15,
    timeoutSeconds: 10,
    path: "/health",
    query: "?deep=1",
    healthAddress: "http://probe:9000",
    consecutiveFailuresThreshold: 2,
  },
  passive: { policy: "TransportFailureRate", reactivationPeriodSeconds: 120, failureRateLimit: 0.3 },
};

describe("toFormState / toPayload", () => {
  it("round-trips a full config", () => {
    expect(toPayload(toFormState(full))).toEqual(full);
  });

  it("turns blank fields into null", () => {
    const state = { ...toFormState(), activeEnabled: true, passiveEnabled: true, path: "  " };

    expect(toPayload(state)).toEqual({
      availableDestinationsPolicy: "HealthyAndUnknown",
      active: {
        policy: "ConsecutiveFailures",
        intervalSeconds: null,
        timeoutSeconds: null,
        path: null,
        query: null,
        healthAddress: null,
        consecutiveFailuresThreshold: null,
      },
      passive: { policy: "TransportFailureRate", reactivationPeriodSeconds: null, failureRateLimit: null },
    });
  });

  it("gives the clearing payload when both checks are off", () => {
    expect(toPayload({ ...toFormState(full), activeEnabled: false, passiveEnabled: false })).toEqual({
      availableDestinationsPolicy: "HealthyOrPanic",
      active: null,
      passive: null,
    });
  });

  it("defaults to HealthyAndUnknown with both checks off", () => {
    const state = toFormState(null);
    expect(state.availableDestinationsPolicy).toBe("HealthyAndUnknown");
    expect(state.activeEnabled).toBe(false);
    expect(state.passiveEnabled).toBe(false);
  });
});

describe("validateHealthCheck", () => {
  const enabled = { ...toFormState(), activeEnabled: true, passiveEnabled: true };

  it("accepts blanks and boundary values", () => {
    expect(validateHealthCheck(enabled)).toEqual({});
    expect(
      validateHealthCheck({
        ...enabled,
        intervalSeconds: "1",
        timeoutSeconds: "1",
        path: "/",
        query: "?",
        healthAddress: "https://probe",
        consecutiveFailuresThreshold: "1",
        reactivationPeriodSeconds: "1",
        failureRateLimit: "0.5",
      })
    ).toEqual({});
  });

  it.each([
    ["intervalSeconds", "0"],
    ["intervalSeconds", "1.5"],
    ["timeoutSeconds", "-3"],
    ["path", "health"],
    ["query", "a=1"],
    ["healthAddress", "probe:9000"],
    ["healthAddress", "ftp://probe"],
    ["consecutiveFailuresThreshold", "0"],
    ["reactivationPeriodSeconds", "0"],
    ["failureRateLimit", "0"],
    ["failureRateLimit", "1"],
    ["failureRateLimit", "abc"],
  ] as const)("rejects %s = %s", (field, value) => {
    expect(validateHealthCheck({ ...enabled, [field]: value })).toHaveProperty(field);
  });

  it("ignores fields of a disabled check", () => {
    expect(
      validateHealthCheck({ ...toFormState(), path: "bad", failureRateLimit: "5" })
    ).toEqual({});
  });
});
