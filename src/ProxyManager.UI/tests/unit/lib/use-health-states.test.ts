import { act, renderHook } from "@testing-library/react";
import { HEALTH_POLL_INTERVAL_MS, useHealthStates } from "@/lib/use-health-states";
import type { HealthState } from "@/types";

const state: HealthState = {
  proxyHostId: "host-1",
  status: "Healthy",
  active: "Healthy",
  passive: null,
  checkedAt: "2026-09-27T12:00:00Z",
};

const okResponse = (body: unknown) => ({ ok: true, status: 200, json: () => Promise.resolve(body) });

describe("useHealthStates", () => {
  let fetchMock: jest.Mock;

  beforeEach(() => {
    jest.useFakeTimers();
    fetchMock = jest.fn().mockResolvedValue(okResponse([state]));
    global.fetch = fetchMock;
  });

  afterEach(() => {
    jest.useRealTimers();
  });

  it("fetches on mount and maps states by host id", async () => {
    const { result } = renderHook(() => useHealthStates());
    await act(async () => {});

    expect(fetchMock).toHaveBeenCalledWith("/manage/api/health-states", expect.anything());
    expect(result.current.get("host-1")).toEqual(state);
  });

  it("polls every 10 seconds", async () => {
    renderHook(() => useHealthStates());
    await act(async () => {});
    expect(fetchMock).toHaveBeenCalledTimes(1);

    await act(async () => {
      jest.advanceTimersByTime(HEALTH_POLL_INTERVAL_MS);
    });
    expect(fetchMock).toHaveBeenCalledTimes(2);

    await act(async () => {
      jest.advanceTimersByTime(HEALTH_POLL_INTERVAL_MS);
    });
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("stops polling on unmount", async () => {
    const { unmount } = renderHook(() => useHealthStates());
    await act(async () => {});
    unmount();

    jest.advanceTimersByTime(HEALTH_POLL_INTERVAL_MS * 3);

    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("returns an empty map when the endpoint is missing", async () => {
    fetchMock.mockResolvedValue({ ok: false, status: 404, json: () => Promise.resolve({}) });

    const { result } = renderHook(() => useHealthStates());
    await act(async () => {});

    expect(result.current.size).toBe(0);
  });

  it("returns an empty map on a network error", async () => {
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));

    const { result } = renderHook(() => useHealthStates());
    await act(async () => {});

    expect(result.current.size).toBe(0);
  });
});
