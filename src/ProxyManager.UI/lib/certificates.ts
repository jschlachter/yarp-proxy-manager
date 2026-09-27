export type ExpiryStatus = "valid" | "expiring" | "critical" | "expired";

const DAY_MS = 24 * 60 * 60 * 1000;

/** Extracts the CN value from a .NET X500DistinguishedName string such as `CN=example.com, O=Acme`. */
export function getCommonName(subject: string): string | undefined {
  const parts = subject.match(/(?:[^,"]|"(?:[^"]|"")*")+/g) ?? [];
  for (const part of parts) {
    const [key, ...rest] = part.split("=");
    if (key.trim().toUpperCase() !== "CN") continue;
    const value = rest.join("=").trim();
    return value.startsWith('"') && value.endsWith('"')
      ? value.slice(1, -1).replace(/""/g, '"')
      : value;
  }
  return undefined;
}

export function getExpiryStatus(notAfter: string, now = Date.now()): ExpiryStatus {
  const remaining = new Date(notAfter).getTime() - now;
  if (remaining < 0) return "expired";
  if (remaining <= 7 * DAY_MS) return "critical";
  if (remaining <= 30 * DAY_MS) return "expiring";
  return "valid";
}

const relativeFormat = new Intl.RelativeTimeFormat("en", { numeric: "auto" });

export function formatRelativeDays(date: string, now = Date.now()): string {
  const days = Math.round((new Date(date).getTime() - now) / DAY_MS);
  return relativeFormat.format(days, "day");
}
