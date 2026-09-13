import type { ComponentType } from "react";
import {
  LayoutDashboardIcon,
  RouteIcon,
  HeartPulseIcon,
  ShieldCheckIcon,
} from "lucide-react";

export interface Module {
  label: string;
  href: string;
  icon: ComponentType<{ className?: string }>;
  enabled: boolean;
}

export const MODULE_REGISTRY: Module[] = [
  {
    label: "Dashboard",
    href: "/",
    icon: LayoutDashboardIcon,
    enabled: true,
  },
  {
    label: "Routes",
    href: "/routes",
    icon: RouteIcon,
    enabled: true,
  },
  {
    label: "Health Checks",
    href: "/health-checks",
    icon: HeartPulseIcon,
    enabled: true,
  },
  {
    label: "Certificates",
    href: "/certificates",
    icon: ShieldCheckIcon,
    enabled: true,
  },
];
