import { render, screen } from "@testing-library/react";
import MaintainerPanel from "@/components/routes/MaintainerPanel";
import type { MaintainerAssignment } from "@/types";

const mockMaintainers: MaintainerAssignment[] = [
  {
    proxyHostId: "route-1",
    userId: "user-2",
    userName: "Jane Doe",
    assignedBy: "admin-1",
    assignedAt: "2026-04-01T00:00:00Z",
  },
];

describe("MaintainerPanel", () => {
  it("renders assigned maintainer list when data is provided", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={mockMaintainers} isAdmin={false} />);
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
  });

  it("shows Assign Maintainer form for admin", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={mockMaintainers} isAdmin={true} />);
    expect(screen.getByRole("button", { name: /assign/i })).toBeInTheDocument();
  });

  it("does not show Assign Maintainer form for non-admin", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={mockMaintainers} isAdmin={false} />);
    expect(screen.queryByRole("button", { name: /assign/i })).not.toBeInTheDocument();
  });

  it("shows Remove button per maintainer for admin", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={mockMaintainers} isAdmin={true} />);
    expect(screen.getByRole("button", { name: /remove/i })).toBeInTheDocument();
  });

  it("handles empty maintainer list gracefully", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={[]} isAdmin={true} />);
    expect(screen.getByText(/no maintainers/i)).toBeInTheDocument();
  });

  it("shows stub callout when maintainers is null (API not yet available)", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={null} isAdmin={true} />);
    expect(screen.getByText(/coming soon/i)).toBeInTheDocument();
  });

  it("renders nothing visible for non-admin when maintainers is null", () => {
    render(<MaintainerPanel routeId="route-1" maintainers={null} isAdmin={false} />);
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
