import Link from "next/link";
import { headers } from "next/headers";
import { getSession } from "@/lib/auth";
import { MODULE_REGISTRY } from "@/lib/modules";

export default async function DashboardLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const headersList = await headers();
  const session = getSession(headersList);

  const enabledModules = MODULE_REGISTRY.filter((m) => m.enabled);

  return (
    <div className="flex h-full min-h-screen">
      <aside className="w-60 shrink-0 border-r bg-sidebar flex flex-col">
        <div className="px-4 py-4 border-b">
          <span className="font-semibold text-sm text-sidebar-foreground">
            Proxy Manager
          </span>
        </div>
        <nav className="flex-1 px-2 py-4 space-y-1">
          {enabledModules.map((mod) => {
            const Icon = mod.icon;
            return (
              <Link
                key={mod.href}
                href={mod.href}
                className="flex items-center gap-2 rounded-md px-3 py-2 text-sm text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground transition-colors"
              >
                <Icon className="h-4 w-4 shrink-0" />
                {mod.label}
              </Link>
            );
          })}
        </nav>
        <div className="px-4 py-3 border-t text-xs text-muted-foreground truncate">
          {session.userId || "Unknown user"}
        </div>
      </aside>
      <main className="flex-1 overflow-auto">{children}</main>
    </div>
  );
}
