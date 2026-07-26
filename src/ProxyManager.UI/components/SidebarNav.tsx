"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { MODULE_REGISTRY } from "@/lib/modules";
import { cn } from "@/lib/utils";

export function SidebarNav() {
  const pathname = usePathname();
  const enabledModules = MODULE_REGISTRY.filter((m) => m.enabled);

  return (
    <nav className="flex-1 px-3 py-4 space-y-1">
      {enabledModules.map((mod) => {
        const Icon = mod.icon;
        const isActive =
          pathname === mod.href || pathname.startsWith(`${mod.href}/`);
        return (
          <Link
            key={mod.href}
            href={mod.href}
            aria-current={isActive ? "page" : undefined}
            className={cn(
              "group relative flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-all",
              isActive
                ? "bg-sidebar-accent text-sidebar-accent-foreground shadow-sm"
                : "text-sidebar-foreground/70 hover:bg-sidebar-accent/60 hover:text-sidebar-foreground"
            )}
          >
            <span
              className={cn(
                "absolute left-0 top-1/2 h-5 -translate-y-1/2 rounded-full brand-gradient transition-all",
                isActive ? "w-1 opacity-100" : "w-0 opacity-0"
              )}
            />
            <Icon
              className={cn(
                "h-4 w-4 shrink-0 transition-colors",
                isActive
                  ? "text-primary"
                  : "text-sidebar-foreground/50 group-hover:text-sidebar-foreground"
              )}
            />
            {mod.label}
          </Link>
        );
      })}
    </nav>
  );
}
