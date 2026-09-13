import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import { ThemeToggle } from "@/components/theme-toggle";
import { SidebarNav } from "@/components/SidebarNav";
import { UserMenu } from "@/components/UserMenu";
import { WaypointsIcon } from "lucide-react";

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const headersList = await headers();
  const session = getSession(headersList);
  const userName = session.name || "Unknown user";
  const initials = userName
    .split(/\s+/)
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
  const accountUrl = process.env.AUTHENTIK_ACCOUNT_URL ?? null;

  return (
    <div className="flex h-full min-h-screen">
      <aside className="w-64 shrink-0 border-r border-sidebar-border bg-sidebar/70 backdrop-blur-xl flex flex-col">
        <div className="px-4 py-5 flex items-center gap-3">
          <span className="flex h-9 w-9 items-center justify-center rounded-xl brand-gradient shadow-lg shadow-primary/25">
            <WaypointsIcon className="h-5 w-5 text-primary-foreground" />
          </span>
          <div className="flex flex-col leading-tight">
            <span className="font-semibold text-sm text-sidebar-foreground">
              Proxy Manager
            </span>
            <span className="text-[11px] text-muted-foreground">
              Route control
            </span>
          </div>
        </div>

        <SidebarNav />

        <div className="mx-3 mb-3 rounded-xl border border-sidebar-border bg-sidebar-accent/40 px-3 py-2.5 flex items-center justify-between gap-2">
          <UserMenu userName={userName} initials={initials} accountUrl={accountUrl} />
          <ThemeToggle />
        </div>
      </aside>
      <main className="flex-1 overflow-auto">{children}</main>
    </div>
  );
}
