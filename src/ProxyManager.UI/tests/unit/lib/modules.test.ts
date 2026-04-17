import { MODULE_REGISTRY } from "@/lib/modules";

describe("MODULE_REGISTRY", () => {
  it("is an array", () => {
    expect(Array.isArray(MODULE_REGISTRY)).toBe(true);
  });

  it("each entry has required shape: label, href, icon, enabled", () => {
    for (const mod of MODULE_REGISTRY) {
      expect(typeof mod.label).toBe("string");
      expect(typeof mod.href).toBe("string");
      // Icons can be functions or forwardRef objects (lucide-react uses forwardRef)
      expect(["function", "object"]).toContain(typeof mod.icon);
      expect(typeof mod.enabled).toBe("boolean");
    }
  });

  it("contains at least the Routes module", () => {
    const routes = MODULE_REGISTRY.find((m) => m.href === "/routes");
    expect(routes).toBeDefined();
    expect(routes?.label).toBe("Routes");
    expect(routes?.enabled).toBe(true);
  });

  it("filtering enabled:true returns only enabled entries", () => {
    const enabled = MODULE_REGISTRY.filter((m) => m.enabled);
    expect(enabled.length).toBeGreaterThan(0);
    for (const mod of enabled) {
      expect(mod.enabled).toBe(true);
    }
  });

  it("adding a new entry does not mutate existing entries", () => {
    const before = MODULE_REGISTRY.map((m) => ({ ...m }));
    const copy = [...MODULE_REGISTRY];
    copy.push({
      label: "Test Module",
      href: "/test-module",
      icon: () => null,
      enabled: true,
    } as never);

    // Original registry unchanged
    expect(MODULE_REGISTRY).toHaveLength(before.length);
    for (let i = 0; i < before.length; i++) {
      expect(MODULE_REGISTRY[i].label).toBe(before[i].label);
      expect(MODULE_REGISTRY[i].href).toBe(before[i].href);
      expect(MODULE_REGISTRY[i].enabled).toBe(before[i].enabled);
    }
  });

  it("a disabled entry is filtered out when sidebar uses enabled:true filter", () => {
    // Simulate what the sidebar does
    const sidebar = MODULE_REGISTRY.filter((m) => m.enabled);
    const disabledEntries = MODULE_REGISTRY.filter((m) => !m.enabled);
    for (const disabled of disabledEntries) {
      expect(sidebar).not.toContain(disabled);
    }
  });
});
