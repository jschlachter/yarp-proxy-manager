import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import RouteForm from "@/components/routes/RouteForm";
import type { ProxyHost } from "@/types";

const mockRoute: ProxyHost = {
  id: "route-1",
  domainNames: ["example.com"],
  destination: "http://backend:8080",
  isEnabled: true,
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
});
