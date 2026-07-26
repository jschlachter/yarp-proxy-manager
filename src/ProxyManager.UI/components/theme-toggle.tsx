"use client";
import { useTheme } from "next-themes";
import { Sun, Moon, Monitor } from "lucide-react";
import { useEffect, useState } from "react";

const themes = ["system", "light", "dark"] as const;
type Theme = (typeof themes)[number];

const icons: Record<Theme, React.ElementType> = {
  system: Monitor,
  light: Sun,
  dark: Moon,
};

const labels: Record<Theme, string> = {
  system: "System",
  light: "Light",
  dark: "Dark",
};

export function ThemeToggle() {
  const { theme, setTheme } = useTheme();
  const [mounted, setMounted] = useState(false);
  useEffect(() => setMounted(true), []);

  if (!mounted) return <div className="h-8 w-8" />;

  const current: Theme = (themes.includes(theme as Theme) ? theme : "system") as Theme;
  const next = themes[(themes.indexOf(current) + 1) % themes.length];
  const Icon = icons[current];

  return (
    <button
      onClick={() => setTheme(next)}
      title={`Theme: ${labels[current]} — click for ${labels[next]}`}
      aria-label={`Theme: ${labels[current]} — click for ${labels[next]}`}
      className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg border border-sidebar-border
                 text-sidebar-foreground/70 hover:text-sidebar-foreground
                 hover:bg-sidebar-accent hover:border-primary/40 transition-colors cursor-pointer"
    >
      <span key={current} className="animate-theme-spin inline-flex">
        <Icon size={15} />
      </span>
    </button>
  );
}
