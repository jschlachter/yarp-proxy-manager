import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RouteForm from "@/components/routes/RouteForm";
import type { ProxyHost } from "@/types";

const mockRoute: ProxyHost = {
  id: "route-1",
  domainNames: ["example.com"],
  destination: "http://backend:8080",
  isEnabled: true,
  tlsMode: "Manual",
};

describe("RouteForm", () => {
  describe("create mode (no initial data)", () => {
    it("renders required fields", () => {
      render(<RouteForm onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Destination URL")).toBeInTheDocument();
      expect(screen.getByLabelText("Domain Names")).toBeInTheDocument();
    });

    it("shows validation errors when submitted with empty required fields", async () => {
      render(<RouteForm onSubmit={jest.fn()} />);
      fireEvent.click(screen.getByRole("button", { name: /save|submit|create/i }));
      await waitFor(() => {
        expect(screen.getAllByRole("alert").length).toBeGreaterThan(0);
      });
    });

    it("calls onSubmit with correct payload on valid input", async () => {
      const onSubmit = jest.fn();
      render(<RouteForm onSubmit={onSubmit} />);

      await userEvent.type(screen.getByLabelText("Destination URL"), "http://backend:8080");
      await userEvent.type(screen.getByLabelText("Domain Names"), "example.com");

      fireEvent.click(screen.getByRole("button", { name: /save|submit|create/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({
            destinationUri: "http://backend:8080",
            domainNames: ["example.com"],
          })
        );
      });
    });
  });

  describe("edit mode (with initial data)", () => {
    it("pre-fills fields with existing route data", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Destination URL")).toHaveValue("http://backend:8080");
    });
  });

  describe("TLS mode selector", () => {
    it("defaults to Manual when creating a new route", () => {
      render(<RouteForm onSubmit={jest.fn()} />);
      expect(screen.getByRole("radio", { name: "Manual" })).toHaveAttribute("aria-checked", "true");
      expect(screen.getByRole("radio", { name: "Let's Encrypt" })).toHaveAttribute(
        "aria-checked",
        "false"
      );
    });

    it("pre-selects the existing route's TLS mode", () => {
      render(
        <RouteForm
          initialData={{ ...mockRoute, tlsMode: "LetsEncrypt" }}
          onSubmit={jest.fn()}
        />
      );
      expect(screen.getByRole("radio", { name: "Let's Encrypt" })).toHaveAttribute(
        "aria-checked",
        "true"
      );
    });

    it("submits the selected TLS mode", async () => {
      const onSubmit = jest.fn();
      render(<RouteForm onSubmit={onSubmit} />);

      await userEvent.type(screen.getByLabelText("Destination URL"), "http://backend:8080");
      await userEvent.type(screen.getByLabelText("Domain Names"), "example.com");
      fireEvent.click(screen.getByRole("radio", { name: "Let's Encrypt" }));
      fireEvent.click(screen.getByRole("button", { name: /save|submit|create/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({ tlsMode: "LetsEncrypt" })
        );
      });
    });

    it("shows the TLS mode as read-only text in readOnly mode", () => {
      render(
        <RouteForm
          initialData={{ ...mockRoute, tlsMode: "LetsEncrypt" }}
          onSubmit={jest.fn()}
          readOnly
        />
      );
      expect(screen.queryByRole("radiogroup")).not.toBeInTheDocument();
      expect(screen.getByText("Let's Encrypt")).toBeInTheDocument();
    });
  });

  describe("readOnly mode", () => {
    it("renders fields as display-only when readOnly is true", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly />);
      const urlInput = screen.queryByRole("textbox", { name: /destination url/i });
      if (urlInput) {
        expect(urlInput).toHaveAttribute("disabled");
      } else {
        expect(screen.getByText("http://backend:8080")).toBeInTheDocument();
      }
    });

    it("hides the submit button in readOnly mode", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly />);
      expect(screen.queryByRole("button", { name: /save|submit|update/i })).not.toBeInTheDocument();
    });
  });

  describe("role-based rendering (US2 RBAC)", () => {
    it("admin (readOnly=false) sees editable inputs and submit button", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly={false} />);
      expect(screen.getByLabelText("Destination URL")).not.toHaveAttribute("disabled");
      expect(screen.getByRole("button", { name: /save|submit|create/i })).toBeInTheDocument();
    });

    it("non-admin non-maintainer (readOnly=true) sees display-only text and no submit", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly={true} />);
      expect(screen.queryByRole("button", { name: /save|submit|create/i })).not.toBeInTheDocument();
      expect(screen.getByText("http://backend:8080")).toBeInTheDocument();
    });
  });

  describe("health checks", () => {
    const routeWithChecks: ProxyHost = {
      ...mockRoute,
      healthCheck: {
        availableDestinationsPolicy: "HealthyOrPanic",
        active: {
          policy: "ConsecutiveFailures",
          intervalSeconds: 20,
          timeoutSeconds: null,
          path: "/health",
          query: null,
          healthAddress: null,
          consecutiveFailuresThreshold: 3,
        },
        passive: null,
      },
    };

    async function fillRequired() {
      await userEvent.type(screen.getByLabelText("Destination URL"), "http://backend:8080");
      await userEvent.type(screen.getByLabelText("Domain Names"), "example.com");
    }

    it("shows active fields and a single-option policy dropdown once enabled", async () => {
      render(<RouteForm onSubmit={jest.fn()} />);
      expect(screen.queryByLabelText("Interval (s)")).not.toBeInTheDocument();

      await userEvent.click(screen.getByLabelText("Active health check"));

      expect(screen.getByLabelText("Interval (s)")).toBeInTheDocument();
      const policy = screen.getByLabelText("Active policy");
      expect(Array.from((policy as HTMLSelectElement).options).map((o) => o.text)).toEqual([
        "ConsecutiveFailuresHealthPolicy",
      ]);
      expect(screen.getByRole("radio", { name: "Stop traffic when unhealthy" })).toHaveAttribute(
        "aria-checked",
        "true"
      );
    });

    it("submits the configured healthCheck", async () => {
      const onSubmit = jest.fn();
      render(<RouteForm onSubmit={onSubmit} />);
      await fillRequired();

      await userEvent.click(screen.getByLabelText("Active health check"));
      await userEvent.type(screen.getByLabelText("Path"), "/health");
      await userEvent.click(screen.getByLabelText("Passive health check"));
      await userEvent.type(screen.getByLabelText("Failure rate limit (0–1)"), "0.4");
      fireEvent.click(screen.getByRole("button", { name: /create/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({
            healthCheck: {
              availableDestinationsPolicy: "HealthyAndUnknown",
              active: {
                policy: "ConsecutiveFailures",
                intervalSeconds: null,
                timeoutSeconds: null,
                path: "/health",
                query: null,
                healthAddress: null,
                consecutiveFailuresThreshold: null,
              },
              passive: { policy: "TransportFailureRate", reactivationPeriodSeconds: null, failureRateLimit: 0.4 },
            },
          })
        );
      });
    });

    it("pre-fills the fields of a host that has checks", () => {
      render(<RouteForm initialData={routeWithChecks} onSubmit={jest.fn()} />);

      expect(screen.getByLabelText("Active health check")).toBeChecked();
      expect(screen.getByLabelText("Interval (s)")).toHaveValue("20");
      expect(screen.getByLabelText("Path")).toHaveValue("/health");
      expect(screen.getByLabelText("Passive health check")).not.toBeChecked();
      expect(screen.getByRole("radio", { name: "Keep sending traffic" })).toHaveAttribute("aria-checked", "true");
    });

    it("blocks submit with an inline error for an invalid rate limit", async () => {
      const onSubmit = jest.fn();
      render(<RouteForm onSubmit={onSubmit} />);
      await fillRequired();

      await userEvent.click(screen.getByLabelText("Passive health check"));
      await userEvent.type(screen.getByLabelText("Failure rate limit (0–1)"), "1.5");
      fireEvent.click(screen.getByRole("button", { name: /create/i }));

      expect(await screen.findByText("Must be between 0 and 1 (exclusive)")).toBeInTheDocument();
      expect(onSubmit).not.toHaveBeenCalled();
    });

    it("renders a summary in the read-only view", () => {
      render(<RouteForm initialData={routeWithChecks} onSubmit={jest.fn()} readOnly />);

      expect(
        screen.getByText("Active: ConsecutiveFailuresHealthPolicy (every 20s, /health, threshold 3)")
      ).toBeInTheDocument();
      expect(screen.getByText("Keeps sending traffic when unhealthy")).toBeInTheDocument();
    });

    it("shows Off in the read-only view when there are no checks", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly />);

      expect(screen.getByText("Off")).toBeInTheDocument();
    });
  });
});
