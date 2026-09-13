"use client";

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";

interface UserMenuProps {
  userName: string;
  initials: string;
  accountUrl: string | null;
}

export function UserMenu({ userName, initials, accountUrl }: UserMenuProps) {
  const accountDisabled = accountUrl === null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="flex items-center gap-2.5 min-w-0 rounded-md outline-none focus-visible:ring-2 focus-visible:ring-ring">
        <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg brand-gradient text-xs font-semibold text-primary-foreground">
          {initials || "?"}
        </span>
        <span className="text-xs font-medium text-sidebar-foreground truncate">
          {userName}
        </span>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" side="top">
        <DropdownMenuGroup>
          {accountDisabled && (
            <DropdownMenuLabel>Account settings not configured</DropdownMenuLabel>
          )}
          <DropdownMenuItem
            disabled={accountDisabled}
            title={accountDisabled ? "Account settings URL is not configured" : undefined}
            render={<a href={accountUrl ?? undefined} target="_blank" rel="noopener noreferrer" />}
          >
            Edit Profile
          </DropdownMenuItem>
          <DropdownMenuItem
            disabled={accountDisabled}
            title={accountDisabled ? "Account settings URL is not configured" : undefined}
            render={<a href={accountUrl ?? undefined} target="_blank" rel="noopener noreferrer" />}
          >
            Change Password
          </DropdownMenuItem>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem render={<a href="/logout" />}>Logout</DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export default UserMenu;
