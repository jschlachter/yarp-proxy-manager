import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RouteListClient from "@/app/(dashboard)/routes/RouteListClient";
import type { PaginatedRoutes } from "@/lib/proxy-manager-client";

jest.mock("next/navigation", () => ({
  useRouter: () => ({ refresh: jest.fn(), push: jest.fn() }),
}));

const mockRoutes: PaginatedRoutes = {
  items: [
    { id: "r1", domainNames: ["svc.example.com"], destination: "http://svc:8080", isEnabled: true },
  ],
  totalCount: 1,
  page: 1,
  pageSize: 50,
};

beforeEach(() => {
  global.fetch = jest.fn();
});

afterEach(() => {
  jest.resetAllMocks();
});

describe("RouteListClient", () => {
  it("renders loading state initially", () => {
    (global.fetch as jest.Mock).mockReturnValue(new Promise(() => {}));
    render(<RouteListClient isAdmin={true} />);
    expect(screen.getByText(/loading routes/i)).toBeInTheDocument();
  });

  it("renders routes after successful fetch", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockRoutes),
    });
    render(<RouteListClient isAdmin={true} />);
    await waitFor(() => expect(screen.getAllByText("svc.example.com").length).toBeGreaterThan(0));
  });

  it("renders error banner when fetch returns non-ok", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({ detail: "API unavailable" }),
    });
    render(<RouteListClient isAdmin={true} />);
    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByRole("alert")).toHaveTextContent("API unavailable");
  });

  it("renders error banner on network error", async () => {
    (global.fetch as jest.Mock).mockRejectedValueOnce(new Error("Network error"));
    render(<RouteListClient isAdmin={true} />);
    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
  });

  it("shows delete button for admin", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockRoutes),
    });
    render(<RouteListClient isAdmin={true} />);
    await waitFor(() => expect(screen.getAllByText("svc.example.com").length).toBeGreaterThan(0));
    expect(screen.getByRole("button", { name: /delete/i })).toBeInTheDocument();
  });

  it("does not show delete button for non-admin", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: true,
      json: () => Promise.resolve(mockRoutes),
    });
    render(<RouteListClient isAdmin={false} />);
    await waitFor(() => expect(screen.getAllByText("svc.example.com").length).toBeGreaterThan(0));
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("calls DELETE endpoint and reloads on delete confirmation", async () => {
    const routesFetch = jest.fn()
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockRoutes) })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve({ ...mockRoutes, items: [] }) });
    const deleteFetch = jest.fn().mockResolvedValueOnce({ ok: true });

    (global.fetch as jest.Mock)
      .mockImplementationOnce(routesFetch)
      .mockImplementationOnce(deleteFetch)
      .mockImplementationOnce(routesFetch);

    jest.spyOn(window, "confirm").mockReturnValueOnce(true);

    render(<RouteListClient isAdmin={true} />);
    await waitFor(() => screen.getByRole("button", { name: /delete/i }));
    await userEvent.click(screen.getByRole("button", { name: /delete/i }));

    await waitFor(() => expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining("routes/r1"),
      expect.objectContaining({ method: "DELETE" })
    ));
  });
});
