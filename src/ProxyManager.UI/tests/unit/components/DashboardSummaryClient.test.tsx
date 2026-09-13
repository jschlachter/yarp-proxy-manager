import { render, screen, waitFor } from "@testing-library/react";
import DashboardSummaryClient from "@/app/(dashboard)/DashboardSummaryClient";
import type { PaginatedRoutes, PaginatedCertificates } from "@/lib/proxy-manager-client";

const mockRoutes: PaginatedRoutes = {
  items: [],
  page: 1,
  pageSize: 1,
  totalCount: 7,
};

const mockCertificates: PaginatedCertificates = {
  items: [],
  page: 1,
  pageSize: 1,
  totalCount: 3,
};

beforeEach(() => {
  global.fetch = jest.fn();
});

afterEach(() => {
  jest.resetAllMocks();
});

describe("DashboardSummaryClient", () => {
  it("renders loading state initially", () => {
    (global.fetch as jest.Mock).mockReturnValue(new Promise(() => {}));
    render(<DashboardSummaryClient />);
    expect(screen.getAllByText(/loading dashboard/i).length).toBeGreaterThan(0);
  });

  it("shows the Health Checks placeholder immediately, even while loading", () => {
    (global.fetch as jest.Mock).mockReturnValue(new Promise(() => {}));
    render(<DashboardSummaryClient />);
    expect(screen.getByText(/not yet configured/i)).toBeInTheDocument();
  });

  it("renders route and certificate counts after successful fetch", async () => {
    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockRoutes) })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockCertificates) });

    render(<DashboardSummaryClient />);

    await waitFor(() => expect(screen.getByText("7 Routes")).toBeInTheDocument());
    expect(screen.getByText("3 Certificates")).toBeInTheDocument();
  });

  it("still shows the Health Checks placeholder after fetch resolves", async () => {
    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockRoutes) })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockCertificates) });

    render(<DashboardSummaryClient />);

    await waitFor(() => expect(screen.getByText("7 Routes")).toBeInTheDocument());
    expect(screen.getByText(/not yet configured/i)).toBeInTheDocument();
  });

  it("renders error banner when a fetch returns non-ok", async () => {
    (global.fetch as jest.Mock)
      .mockResolvedValueOnce({ ok: false, json: () => Promise.resolve({ detail: "API unavailable" }) })
      .mockResolvedValueOnce({ ok: true, json: () => Promise.resolve(mockCertificates) });

    render(<DashboardSummaryClient />);

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
    expect(screen.getByRole("alert")).toHaveTextContent("API unavailable");
  });

  it("renders error banner on network error", async () => {
    (global.fetch as jest.Mock).mockRejectedValueOnce(new Error("Network error"));

    render(<DashboardSummaryClient />);

    await waitFor(() => expect(screen.getByRole("alert")).toBeInTheDocument());
  });
});
