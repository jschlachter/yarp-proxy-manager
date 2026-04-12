import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RouteForm from "@/components/routes/RouteForm";
import type { ProxyHost } from "@/types";

const mockRoute: ProxyHost = {
  id: "route-1",
  name: "My Service",
  upstreamUrl: "http://backend:8080",
  hostnames: ["example.com"],
  isEnabled: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

describe("RouteForm", () => {
  describe("create mode (no initial data)", () => {
    it("renders required fields", () => {
      render(<RouteForm onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Name")).toBeInTheDocument();
      expect(screen.getByLabelText("Upstream URL")).toBeInTheDocument();
      expect(screen.getByLabelText("Hostnames")).toBeInTheDocument();
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

      await userEvent.type(screen.getByLabelText("Name"), "New Route");
      await userEvent.type(screen.getByLabelText("Upstream URL"), "http://backend:8080");
      await userEvent.type(screen.getByLabelText("Hostnames"), "example.com");

      fireEvent.click(screen.getByRole("button", { name: /save|submit|create/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({
            name: "New Route",
            upstreamUrl: "http://backend:8080",
          })
        );
      });
    });
  });

  describe("edit mode (with initial data)", () => {
    it("pre-fills fields with existing route data", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Name")).toHaveValue("My Service");
      expect(screen.getByLabelText("Upstream URL")).toHaveValue("http://backend:8080");
    });
  });

  describe("readOnly mode", () => {
    it("renders fields as display-only when readOnly is true", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly />);
      // In readOnly mode, inputs should be absent or disabled
      const nameInput = screen.queryByRole("textbox", { name: /name/i });
      if (nameInput) {
        expect(nameInput).toHaveAttribute("disabled");
      } else {
        expect(screen.getByText("My Service")).toBeInTheDocument();
      }
    });

    it("hides the submit button in readOnly mode", () => {
      render(<RouteForm initialData={mockRoute} onSubmit={jest.fn()} readOnly />);
      expect(screen.queryByRole("button", { name: /save|submit|update/i })).not.toBeInTheDocument();
    });
  });
});
