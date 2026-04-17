import type { UserSession, ProxyHost, MaintainerAssignment } from "@/types";
import {
  listRoutes,
  getRoute,
  createRoute,
  updateRoute,
  deleteRoute,
  listMaintainers,
  assignMaintainer,
  removeMaintainer,
} from "@/lib/proxy-manager-client";

const adminSession: UserSession = {
  userId: "user-1",
  groups: ["proxy-admins"],
  isAdmin: true,
  accessToken: "test-token",
};

const mockRoute: ProxyHost = {
  id: "route-1",
  domainNames: ["example.com"],
  destination: "http://backend:8080",
  isEnabled: true,
};

const originalEnv = process.env;

beforeEach(() => {
  process.env = { ...originalEnv, PROXY_MANAGER_API_URL: "http://api:5001" };
  global.fetch = jest.fn();
});

afterEach(() => {
  process.env = originalEnv;
  jest.restoreAllMocks();
});

describe("proxy-manager-client", () => {
  describe("listRoutes", () => {
    it("fetches GET /proxyHosts with Authorization header", async () => {
      const paginatedResponse = {
        items: [mockRoute],
        page: 1,
        pageSize: 50,
        totalCount: 1,
      };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(paginatedResponse),
      });

      const result = await listRoutes(adminSession);

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts?page=1&pageSize=50",
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: "Bearer test-token",
          }),
        })
      );
      expect(result.items).toHaveLength(1);
      expect(result.totalCount).toBe(1);
    });

    it("forwards the Bearer token from session", async () => {
      const sessionWithToken: UserSession = { ...adminSession, accessToken: "special-token" };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve({ items: [], page: 1, pageSize: 50, totalCount: 0 }),
      });

      await listRoutes(sessionWithToken);

      expect(global.fetch).toHaveBeenCalledWith(
        expect.any(String),
        expect.objectContaining({
          headers: expect.objectContaining({
            Authorization: "Bearer special-token",
          }),
        })
      );
    });
  });

  describe("getRoute", () => {
    it("fetches GET /proxyHosts/:id", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockRoute),
      });

      const result = await getRoute(adminSession, "route-1");

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1",
        expect.any(Object)
      );
      expect(result.id).toBe("route-1");
    });
  });

  describe("createRoute", () => {
    it("sends POST /proxyHosts with route body", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 201,
        json: () => Promise.resolve(mockRoute),
      });

      const body = {
        domainNames: ["example.com"],
        destinationUri: "http://backend:8080",
      };
      await createRoute(adminSession, body);

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify(body),
        })
      );
    });
  });

  describe("updateRoute", () => {
    it("sends PUT /proxyHosts/:id with route body", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve(mockRoute),
      });

      const body = {
        domainNames: ["updated.com"],
        destinationUri: "http://backend:9090",
        isEnabled: false,
      };
      await updateRoute(adminSession, "route-1", body);

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1",
        expect.objectContaining({ method: "PUT" })
      );
    });
  });

  describe("deleteRoute", () => {
    it("sends DELETE /proxyHosts/:id", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 204,
      });

      await deleteRoute(adminSession, "route-1");

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("listMaintainers", () => {
    it("fetches GET /proxyHosts/:id/maintainers", async () => {
      const mockMaintainer: MaintainerAssignment = {
        proxyHostId: "route-1",
        userId: "user-2",
        userName: "Jane Doe",
        assignedBy: "admin-1",
        assignedAt: "2026-04-01T00:00:00Z",
      };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 200,
        json: () => Promise.resolve([mockMaintainer]),
      });

      const result = await listMaintainers(adminSession, "route-1");

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1/maintainers",
        expect.objectContaining({
          headers: expect.objectContaining({ Authorization: "Bearer test-token" }),
        })
      );
      expect(result).toHaveLength(1);
      expect(result[0].userId).toBe("user-2");
    });
  });

  describe("assignMaintainer", () => {
    it("sends POST /proxyHosts/:id/maintainers with userId", async () => {
      const mockMaintainer: MaintainerAssignment = {
        proxyHostId: "route-1",
        userId: "user-2",
        userName: "Jane Doe",
        assignedBy: "admin-1",
        assignedAt: "2026-04-01T00:00:00Z",
      };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        status: 201,
        json: () => Promise.resolve(mockMaintainer),
      });

      await assignMaintainer(adminSession, "route-1", "user-2");

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1/maintainers",
        expect.objectContaining({
          method: "POST",
          body: JSON.stringify({ userId: "user-2" }),
        })
      );
    });
  });

  describe("removeMaintainer", () => {
    it("sends DELETE /proxyHosts/:id/maintainers/:userId", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({ ok: true, status: 204 });

      await removeMaintainer(adminSession, "route-1", "user-2");

      expect(global.fetch).toHaveBeenCalledWith(
        "http://api:5001/proxyHosts/route-1/maintainers/user-2",
        expect.objectContaining({ method: "DELETE" })
      );
    });
  });

  describe("API error surfacing as ProblemDetails", () => {
    it("throws ProblemDetails on non-OK response with problem+json content type", async () => {
      const problem = {
        type: "https://tools.ietf.org/html/rfc9457",
        title: "Not Found",
        status: 404,
        detail: "Route not found",
      };
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 404,
        headers: new Headers({ "Content-Type": "application/problem+json" }),
        json: () => Promise.resolve(problem),
      });

      await expect(getRoute(adminSession, "missing")).rejects.toMatchObject({
        status: 404,
        title: "Not Found",
      });
    });

    it("constructs a ProblemDetails on non-OK response without problem+json", async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        status: 503,
        headers: new Headers({ "Content-Type": "text/plain" }),
        text: () => Promise.resolve("Service unavailable"),
      });

      await expect(listRoutes(adminSession)).rejects.toMatchObject({
        status: 503,
        title: "Upstream Error",
      });
    });
  });
});
