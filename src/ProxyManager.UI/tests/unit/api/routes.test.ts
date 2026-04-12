/**
 * @jest-environment node
 */

jest.mock("@/lib/auth");
jest.mock("@/lib/proxy-manager-client");

import { getSession } from "@/lib/auth";
import { listRoutes, createRoute } from "@/lib/proxy-manager-client";
import { GET, POST } from "@/app/api/routes/route";
import type { UserSession, ProxyHost } from "@/types";

const adminSession: UserSession = {
  userId: "admin-1",
  groups: ["proxy-admins"],
  isAdmin: true,
  accessToken: "admin-token",
};

const regularSession: UserSession = {
  userId: "user-1",
  groups: ["viewers"],
  isAdmin: false,
  accessToken: "user-token",
};

const mockRoute: ProxyHost = {
  id: "route-1",
  name: "My Service",
  upstreamUrl: "http://backend:8080",
  hostnames: ["example.com"],
  isEnabled: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function makeRequest(method: string, body?: unknown): Request {
  const headers: Record<string, string> = {};
  if (body) headers["Content-Type"] = "application/json";
  return new Request("http://localhost/api/routes", {
    method,
    body: body ? JSON.stringify(body) : undefined,
    headers,
  });
}

describe("GET /api/routes", () => {
  it("delegates to listRoutes client and returns paginated list", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (listRoutes as jest.Mock).mockResolvedValue({
      routes: [mockRoute],
      page: 1,
      pageSize: 50,
      total: 1,
    });

    const response = await GET(makeRequest("GET"));

    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.routes).toHaveLength(1);
    expect(body.total).toBe(1);
  });

  it("returns 200 for any authenticated user", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);
    (listRoutes as jest.Mock).mockResolvedValue({
      routes: [],
      page: 1,
      pageSize: 50,
      total: 0,
    });

    const response = await GET(makeRequest("GET"));
    expect(response.status).toBe(200);
  });
});

describe("POST /api/routes", () => {
  it("returns 403 Problem Details for non-admin", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);

    const response = await POST(makeRequest("POST", { name: "test" }));

    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });

  it("returns 201 on successful creation for admin", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (createRoute as jest.Mock).mockResolvedValue(mockRoute);

    const routeBody = {
      name: "My Service",
      upstreamUrl: "http://backend:8080",
      hostnames: ["example.com"],
      isEnabled: true,
    };
    const response = await POST(makeRequest("POST", routeBody));

    expect(response.status).toBe(201);
    const body = await response.json();
    expect(body.id).toBe("route-1");
  });

  it("forwards 422 validation errors from ProxyManager API as Problem Details", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (createRoute as jest.Mock).mockRejectedValue({
      type: "https://tools.ietf.org/html/rfc9457",
      title: "Validation Error",
      status: 422,
      detail: "Name is required",
    });

    const response = await POST(makeRequest("POST", {}));

    expect(response.status).toBe(422);
    const body = await response.json();
    expect(body.status).toBe(422);
  });
});
