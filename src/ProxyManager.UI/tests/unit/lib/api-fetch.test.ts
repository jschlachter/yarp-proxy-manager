import { apiFetch } from "@/lib/api-fetch";
import { navigateTo } from "@/lib/navigation";

jest.mock("@/lib/navigation");

beforeEach(() => {
  window.history.pushState({}, "", "/routes?page=2");
  jest.mocked(navigateTo).mockClear();
});

describe("apiFetch", () => {
  it("redirects to /login with the current page as returnUrl on 401", async () => {
    global.fetch = jest.fn().mockResolvedValue({ status: 401 });

    const response = await apiFetch("/manage/api/routes");

    expect(response.status).toBe(401);
    expect(navigateTo).toHaveBeenCalledWith("/login?returnUrl=%2Froutes%3Fpage%3D2");
  });

  it("passes a successful response through untouched", async () => {
    const ok = { status: 200, ok: true };
    global.fetch = jest.fn().mockResolvedValue(ok);

    const response = await apiFetch("/manage/api/routes", { method: "GET" });

    expect(response).toBe(ok);
    expect(global.fetch).toHaveBeenCalledWith("/manage/api/routes", { method: "GET" });
    expect(navigateTo).not.toHaveBeenCalled();
  });
});
