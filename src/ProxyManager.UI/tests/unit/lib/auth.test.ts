import { getSession } from "@/lib/auth";

function makeHeaders(entries: Record<string, string>): Headers {
  return new Headers(entries);
}

const originalEnv = process.env;

beforeEach(() => {
  jest.resetModules();
  process.env = { ...originalEnv };
});

afterEach(() => {
  process.env = originalEnv;
});

describe("getSession", () => {
  describe("header parsing", () => {
    it("reads userId from X-Auth-Sub", () => {
      const headers = makeHeaders({ "X-Auth-Sub": "user-123" });
      const session = getSession(headers);
      expect(session.userId).toBe("user-123");
    });

    it("parses groups from X-Auth-Groups as comma-separated list", () => {
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        "X-Auth-Groups": "group-a, group-b, group-c",
      });
      const session = getSession(headers);
      expect(session.groups).toEqual(["group-a", "group-b", "group-c"]);
    });

    it("returns empty groups array when X-Auth-Groups is absent", () => {
      const headers = makeHeaders({ "X-Auth-Sub": "user-123" });
      const session = getSession(headers);
      expect(session.groups).toEqual([]);
    });

    it("strips 'Bearer ' prefix from Authorization header", () => {
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        Authorization: "Bearer my-token-value",
      });
      const session = getSession(headers);
      expect(session.accessToken).toBe("my-token-value");
    });

    it("returns raw value when Authorization lacks 'Bearer ' prefix", () => {
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        Authorization: "raw-token",
      });
      const session = getSession(headers);
      expect(session.accessToken).toBe("raw-token");
    });

    it("returns empty accessToken when Authorization header is absent", () => {
      const headers = makeHeaders({ "X-Auth-Sub": "user-123" });
      const session = getSession(headers);
      expect(session.accessToken).toBe("");
    });
  });

  describe("admin group detection", () => {
    it("sets isAdmin true when user is in ADMIN_GROUP_CLAIM group", () => {
      process.env.ADMIN_GROUP_CLAIM = "proxy-admins";
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        "X-Auth-Groups": "proxy-admins,other-group",
      });
      const session = getSession(headers);
      expect(session.isAdmin).toBe(true);
    });

    it("sets isAdmin false when user is not in ADMIN_GROUP_CLAIM group", () => {
      process.env.ADMIN_GROUP_CLAIM = "proxy-admins";
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        "X-Auth-Groups": "other-group",
      });
      const session = getSession(headers);
      expect(session.isAdmin).toBe(false);
    });

    it("defaults to 'proxy-admins' when ADMIN_GROUP_CLAIM is unset", () => {
      delete process.env.ADMIN_GROUP_CLAIM;
      const headers = makeHeaders({
        "X-Auth-Sub": "user-123",
        "X-Auth-Groups": "proxy-admins",
      });
      const session = getSession(headers);
      expect(session.isAdmin).toBe(true);
    });
  });

  describe("dev-mode override", () => {
    beforeEach(() => {
      Object.defineProperty(process.env, "NODE_ENV", {
        value: "development",
        writable: true,
      });
    });

    it("uses DEV_AUTH_* env vars when X-Auth-Sub is absent in development", () => {
      process.env.DEV_AUTH_SUB = "dev-user";
      process.env.DEV_AUTH_GROUPS = "proxy-admins,dev-team";
      process.env.DEV_AUTH_TOKEN = "dev-token";
      process.env.ADMIN_GROUP_CLAIM = "proxy-admins";

      const session = getSession(new Headers());
      expect(session.userId).toBe("dev-user");
      expect(session.groups).toEqual(["proxy-admins", "dev-team"]);
      expect(session.accessToken).toBe("dev-token");
      expect(session.isAdmin).toBe(true);
    });

    it("does NOT use DEV_AUTH_* when X-Auth-Sub is present", () => {
      process.env.DEV_AUTH_SUB = "dev-user";
      const headers = makeHeaders({ "X-Auth-Sub": "real-user" });
      const session = getSession(headers);
      expect(session.userId).toBe("real-user");
    });
  });

  describe("missing-header behavior in production", () => {
    beforeEach(() => {
      Object.defineProperty(process.env, "NODE_ENV", {
        value: "production",
        writable: true,
      });
    });

    it("returns empty userId when X-Auth-Sub is missing", () => {
      const session = getSession(new Headers());
      expect(session.userId).toBe("");
    });

    it("does NOT use DEV_AUTH_* vars in production", () => {
      process.env.DEV_AUTH_SUB = "dev-user";
      const session = getSession(new Headers());
      expect(session.userId).toBe("");
    });
  });
});
