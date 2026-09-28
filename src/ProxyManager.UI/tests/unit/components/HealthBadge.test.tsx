import { render, screen } from "@testing-library/react";
import HealthBadge from "@/components/routes/HealthBadge";
import type { HealthState } from "@/types";

const base: HealthState = {
  proxyHostId: "host-1",
  status: "Healthy",
  active: "Healthy",
  passive: "Unknown",
  checkedAt: "2026-09-27T12:00:00Z",
};

describe("HealthBadge", () => {
  it.each([
    ["Healthy", "text-emerald-400"],
    ["Unhealthy", "text-red-400"],
    ["Unknown", "text-amber-400"],
  ] as const)("shows %s with its colour", (status, colour) => {
    render(<HealthBadge state={{ ...base, status }} />);

    const label = screen.getByText(status);
    expect(label).toHaveClass(colour);
    expect(screen.getByLabelText(`Health: ${status}`)).toBeInTheDocument();
  });

  it("renders nothing without a state", () => {
    const { container } = render(<HealthBadge />);

    expect(container).toBeEmptyDOMElement();
  });
});
