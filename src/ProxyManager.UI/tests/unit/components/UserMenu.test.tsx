import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import UserMenu from "@/components/UserMenu";

describe("UserMenu", () => {
  it("opens the menu and shows all three item labels", async () => {
    render(<UserMenu userName="Jane Doe" initials="JD" accountUrl="https://auth.example.com/if/user/" />);
    await userEvent.click(screen.getByText("Jane Doe"));
    expect(await screen.findByText("Edit Profile")).toBeInTheDocument();
    expect(screen.getByText("Change Password")).toBeInTheDocument();
    expect(screen.getByText("Logout")).toBeInTheDocument();
  });

  it("renders Edit Profile and Change Password as links to the account URL", async () => {
    render(<UserMenu userName="Jane Doe" initials="JD" accountUrl="https://auth.example.com/if/user/" />);
    await userEvent.click(screen.getByText("Jane Doe"));
    await screen.findByText("Edit Profile");

    const editProfile = screen.getByText("Edit Profile").closest("a");
    expect(editProfile).toHaveAttribute("href", "https://auth.example.com/if/user/");
    expect(editProfile).toHaveAttribute("target", "_blank");
    expect(editProfile).toHaveAttribute("rel", "noopener noreferrer");

    const changePassword = screen.getByText("Change Password").closest("a");
    expect(changePassword).toHaveAttribute("href", "https://auth.example.com/if/user/");
    expect(changePassword).toHaveAttribute("target", "_blank");
    expect(changePassword).toHaveAttribute("rel", "noopener noreferrer");
  });

  it("renders Logout as a same-tab link to /logout", async () => {
    render(<UserMenu userName="Jane Doe" initials="JD" accountUrl="https://auth.example.com/if/user/" />);
    await userEvent.click(screen.getByText("Jane Doe"));
    await screen.findByText("Logout");

    const logout = screen.getByText("Logout").closest("a");
    expect(logout).toHaveAttribute("href", "/logout");
    expect(logout).not.toHaveAttribute("target");
  });

  it("disables Edit Profile and Change Password when accountUrl is null, but keeps Logout enabled", async () => {
    render(<UserMenu userName="Jane Doe" initials="JD" accountUrl={null} />);
    await userEvent.click(screen.getByText("Jane Doe"));
    await screen.findByText("Edit Profile");

    const editProfile = screen.getByText("Edit Profile").closest("[data-slot='dropdown-menu-item']");
    const changePassword = screen.getByText("Change Password").closest("[data-slot='dropdown-menu-item']");
    const logout = screen.getByText("Logout").closest("[data-slot='dropdown-menu-item']");

    expect(editProfile).toHaveAttribute("data-disabled");
    expect(changePassword).toHaveAttribute("data-disabled");
    expect(logout).not.toHaveAttribute("data-disabled");
    expect(screen.getByText("Account settings not configured")).toBeInTheDocument();
  });
});
