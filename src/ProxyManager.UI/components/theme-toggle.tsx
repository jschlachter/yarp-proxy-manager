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

  if (!mounted) return <div className="h-6 w-16" />;

  const current: Theme = (themes.includes(theme as Theme) ? theme : "system") as Theme;
  const next = themes[(themes.indexOf(current) + 1) % themes.length];
  const Icon = icons[current];

  return (
    <button
      onClick={() => setTheme(next)}
      title={`Theme: ${labels[current]} — click for ${labels[next]}`}
      className="flex items-center gap-1.5 rounded-md px-2 py-1 text-xs
                 text-sidebar-foreground/60 hover:text-sidebar-foreground
                 hover:bg-sidebar-accent transition-colors cursor-pointer"
    >
      <span key={current} className="animate-theme-spin inline-flex">
        <Icon size={13} />
      </span>
      <span>{labels[current]}</span>
    </button>
  );
}
