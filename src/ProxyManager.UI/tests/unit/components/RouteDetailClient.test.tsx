import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RouteDetailClient from "@/app/(dashboard)/routes/[id]/RouteDetailClient";
import type { ProxyHost } from "@/types";

const mockPush = jest.fn();
const mockReplace = jest.fn();
const mockRefresh = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush, replace: mockReplace, refresh: mockRefresh }),
}));

const mockRoute: ProxyHost = {
  id: "route-abc",
  domainNames: ["svc.example.com"],
  destination: "http://svc-backend:8080",
  isEnabled: true,
};

beforeEach(() => {
  global.fetch = jest.fn();
  mockPush.mockReset();
  mockReplace.mockReset();
  mockRefresh.mockReset();
});

afterEach(() => {
  jest.resetAllMocks();
});

function mockFetchRoute(route: ProxyHost = mockRoute) {
  (global.fetch as jest.Mock).mockImplementation((url: string) => {
    if ((url as string).includes("/maintainers")) {
      return Promise.resolve({ ok: false, status: 501, json: () => Promise.resolve({}) });
    }
    return Promise.resolve({ ok: true, json: () => Promise.resolve(route) });
  });
}

describe("RouteDetailClient", () => {
  it("renders loading state initially", () => {
    (global.fetch as jest.Mock).mockReturnValue(new Promise(() => {}));
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    expect(screen.getByText(/loading/i)).toBeInTheDocument();
  });

  it("renders the route form after loading", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    await waitFor(() => expect(screen.getByDisplayValue("svc.example.com")).toBeInTheDocument());
  });

  it("redirects to /routes on 404", async () => {
    (global.fetch as jest.Mock).mockImplementation((url: string) => {
      if ((url as string).includes("/maintainers")) {
        return Promise.resolve({ ok: false, status: 501, json: () => Promise.resolve({}) });
      }
      return Promise.resolve({ ok: false, status: 404, json: () => Promise.resolve({ detail: "Not found" }) });
    });
    render(<RouteDetailClient id="not-found" isAdmin={true} />);
    await waitFor(() => expect(mockReplace).toHaveBeenCalledWith("/routes"));
  });

  it("shows error banner when load fails with non-404", async () => {
    (global.fetch as jest.Mock).mockImplementation((url: string) => {
      if ((url as string).includes("/maintainers")) {
        return Promise.resolve({ ok: false, status: 501, json: () => Promise.resolve({}) });
      }
      return Promise.resolve({ ok: false, status: 500, json: () => Promise.resolve({ detail: "Server error" }) });
    });
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
  });

  it("does not render Delete button for non-admin", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={false} />);
    // In readOnly mode, the route domain appears as text (not an input value)
    await waitFor(() => expect(screen.getAllByText("svc.example.com").length).toBeGreaterThan(0));
    expect(screen.queryByRole("button", { name: /delete route/i })).not.toBeInTheDocument();
  });

  it("opens confirm dialog when Delete Route is clicked", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    await waitFor(() => screen.getByRole("button", { name: /delete route/i }));
    await userEvent.click(screen.getByRole("button", { name: /delete route/i }));
    await waitFor(() => expect(screen.getByRole("dialog")).toBeInTheDocument());
  });

  it("shows the Delete Route button for admin once route loads", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    await waitFor(() => screen.getByRole("button", { name: /delete route/i }));
    expect(screen.getByRole("button", { name: /delete route/i })).toBeInTheDocument();
  });

  it("shows error and keeps dialog closed on failed delete", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={true} />);
    await waitFor(() => screen.getByRole("button", { name: /delete route/i }));
    await userEvent.click(screen.getByRole("button", { name: /delete route/i }));
    await waitFor(() => screen.getByRole("dialog"));

    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({ detail: "Delete failed" }),
    });
    await userEvent.click(screen.getAllByRole("button", { name: /^delete$/i })[0]);
    await waitFor(() => expect(screen.getByText("Delete failed")).toBeInTheDocument());
  });

  it("renders form in readOnly mode for non-admin", async () => {
    mockFetchRoute();
    render(<RouteDetailClient id="route-abc" isAdmin={false} />);
    await waitFor(() => expect(screen.getAllByText("svc.example.com").length).toBeGreaterThan(0));
    expect(screen.queryByRole("button", { name: /save changes/i })).not.toBeInTheDocument();
  });
});
