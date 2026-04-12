import { render, screen } from "@testing-library/react";
import RouteCard from "@/components/routes/RouteCard";
import type { ProxyHost } from "@/types";

const mockRoute: ProxyHost = {
  id: "route-1",
  name: "My Service",
  upstreamUrl: "http://backend:8080",
  hostnames: ["my-service.example.com"],
  isEnabled: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

describe("RouteCard", () => {
  it("renders the route name", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getByText("My Service")).toBeInTheDocument();
  });

  it("renders the upstream URL", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getByText("http://backend:8080")).toBeInTheDocument();
  });

  it("renders an enabled status badge when isEnabled is true", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getByText(/enabled/i)).toBeInTheDocument();
  });

  it("renders a disabled status badge when isEnabled is false", () => {
    const disabledRoute = { ...mockRoute, isEnabled: false };
    render(<RouteCard route={disabledRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getByText(/disabled/i)).toBeInTheDocument();
  });

  it("renders the hostnames", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getByText("my-service.example.com")).toBeInTheDocument();
  });

  it("renders Edit and Delete buttons for admin users", () => {
    render(<RouteCard route={mockRoute} isAdmin={true} onDelete={() => {}} />);
    expect(screen.getByRole("link", { name: /edit/i })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /delete/i })).toBeInTheDocument();
  });

  it("does not render Delete button for non-admin users", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("matches snapshot", () => {
    const { container } = render(
      <RouteCard route={mockRoute} isAdmin={true} onDelete={() => {}} />
    );
    expect(container).toMatchSnapshot();
  });
});
