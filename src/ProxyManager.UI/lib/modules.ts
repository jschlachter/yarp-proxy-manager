import type { ComponentType } from "react";
import { RouteIcon } from "lucide-react";

export interface Module {
  label: string;
  href: string;
  icon: ComponentType<{ className?: string }>;
  enabled: boolean;
}

export const MODULE_REGISTRY: Module[] = [
  {
    label: "Routes",
    href: "/routes",
    icon: RouteIcon,
    enabled: true,
  },
];
