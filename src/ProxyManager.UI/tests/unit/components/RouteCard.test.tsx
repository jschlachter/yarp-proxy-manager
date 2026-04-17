import { render, screen } from "@testing-library/react";
import RouteCard from "@/components/routes/RouteCard";
import type { ProxyHost } from "@/types";

const mockRoute: ProxyHost = {
  id: "route-1",
  domainNames: ["my-service.example.com"],
  destination: "http://backend:8080",
  isEnabled: true,
};

describe("RouteCard", () => {
  it("renders the primary domain name as the title", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getAllByText("my-service.example.com").length).toBeGreaterThan(0);
  });

  it("renders the destination URL", () => {
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

  it("renders the domain names", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} onDelete={() => {}} />);
    expect(screen.getAllByText("my-service.example.com").length).toBeGreaterThan(0);
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

  it("shows Edit button for maintainer (non-admin) but no Delete", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} isMaintainer={true} onDelete={() => {}} />);
    expect(screen.getByRole("link", { name: /edit/i })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("hides all action buttons for non-admin, non-maintainer users", () => {
    render(<RouteCard route={mockRoute} isAdmin={false} isMaintainer={false} onDelete={() => {}} />);
    expect(screen.queryByRole("link", { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /delete/i })).not.toBeInTheDocument();
  });

  it("matches snapshot", () => {
    const { container } = render(
      <RouteCard route={mockRoute} isAdmin={true} onDelete={() => {}} />
    );
    expect(container).toMatchSnapshot();
  });
});
