import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import NewRouteClient from "@/app/(dashboard)/routes/new/NewRouteClient";

const mockPush = jest.fn();

jest.mock("next/navigation", () => ({
  useRouter: () => ({ push: mockPush }),
}));

beforeEach(() => {
  global.fetch = jest.fn();
  mockPush.mockReset();
});

afterEach(() => {
  jest.resetAllMocks();
});

describe("NewRouteClient", () => {
  it("renders the RouteForm", () => {
    render(<NewRouteClient />);
    expect(screen.getByRole("button", { name: /create route/i })).toBeInTheDocument();
  });

  it("navigates to /routes on successful creation", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({ ok: true });

    render(<NewRouteClient />);

    await userEvent.type(screen.getByLabelText(/destination url/i), "http://backend:9000");
    await userEvent.type(screen.getByLabelText(/domain names/i), "my.example.com");
    await userEvent.click(screen.getByRole("button", { name: /create route/i }));

    await waitFor(() => expect(mockPush).toHaveBeenCalledWith("/routes"));
  });

  it("shows error message on failed creation", async () => {
    (global.fetch as jest.Mock).mockResolvedValueOnce({
      ok: false,
      json: () => Promise.resolve({ detail: "Route already exists" }),
    });

    render(<NewRouteClient />);

    await userEvent.type(screen.getByLabelText(/destination url/i), "http://backend:9000");
    await userEvent.type(screen.getByLabelText(/domain names/i), "dup.example.com");
    await userEvent.click(screen.getByRole("button", { name: /create route/i }));

    await waitFor(() => expect(screen.getByText("Route already exists")).toBeInTheDocument());
    expect(mockPush).not.toHaveBeenCalled();
  });
});
