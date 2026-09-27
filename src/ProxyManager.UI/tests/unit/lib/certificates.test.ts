import { formatRelativeDays, getCommonName, getExpiryStatus } from "@/lib/certificates";

describe("getCommonName", () => {
  it.each([
    ["CN=example.com", "example.com"],
    ["CN=example.com, O=Acme, C=US", "example.com"],
    ["O=Acme, CN=*.example.com, C=US", "*.example.com"],
    ["C=US, O=Acme, CN=last.example.com", "last.example.com"],
  ])("reads the CN from %s", (subject, expected) => {
    expect(getCommonName(subject)).toBe(expected);
  });

  it("returns undefined when there is no CN", () => {
    expect(getCommonName("O=Acme, C=US")).toBeUndefined();
  });

  it("handles a quoted CN containing a comma", () => {
    expect(getCommonName('CN="Acme, Inc", O=Acme')).toBe("Acme, Inc");
  });
});

describe("getExpiryStatus", () => {
  const now = Date.parse("2026-06-01T00:00:00Z");
  const day = 24 * 60 * 60 * 1000;
  const at = (ms: number) => new Date(now + ms).toISOString();

  it.each([
    [31 * day, "valid"],
    [30 * day, "expiring"],
    [8 * day, "expiring"],
    [7 * day, "critical"],
    [-1000, "expired"],
  ])("returns the status for %d ms remaining", (remaining, expected) => {
    expect(getExpiryStatus(at(remaining), now)).toBe(expected);
  });
});

describe("formatRelativeDays", () => {
  const now = Date.parse("2026-06-01T00:00:00Z");

  it("formats future and past dates", () => {
    expect(formatRelativeDays("2026-06-11T00:00:00Z", now)).toBe("in 10 days");
    expect(formatRelativeDays("2026-05-29T00:00:00Z", now)).toBe("3 days ago");
  });
});
