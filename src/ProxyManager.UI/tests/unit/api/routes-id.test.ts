/**
 * @jest-environment node
 */

jest.mock("@/lib/auth");
jest.mock("@/lib/proxy-manager-client");

import { getSession } from "@/lib/auth";
import { getRoute, updateRoute, deleteRoute } from "@/lib/proxy-manager-client";
import { GET, PUT, DELETE } from "@/app/api/routes/[id]/route";
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

const notFoundProblem = {
  type: "https://tools.ietf.org/html/rfc9457",
  title: "Not Found",
  status: 404,
  detail: "Route not found",
};

const forbiddenProblem = {
  type: "https://tools.ietf.org/html/rfc9457",
  title: "Forbidden",
  status: 403,
  detail: "Insufficient permissions",
};

function makeRequest(method: string, id: string, body?: unknown): Request {
  const headers: Record<string, string> = {};
  if (body) headers["Content-Type"] = "application/json";
  return new Request(`http://localhost/api/routes/${id}`, {
    method,
    body: body ? JSON.stringify(body) : undefined,
    headers,
  });
}

const params = Promise.resolve({ id: "route-1" });

describe("GET /api/routes/[id]", () => {
  it("returns 200 with route data on success", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (getRoute as jest.Mock).mockResolvedValue(mockRoute);

    const response = await GET(makeRequest("GET", "route-1"), { params });
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body.id).toBe("route-1");
  });

  it("returns 404 Problem Details when route not found", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (getRoute as jest.Mock).mockRejectedValue(notFoundProblem);

    const response = await GET(makeRequest("GET", "missing"), { params: Promise.resolve({ id: "missing" }) });
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body.status).toBe(404);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });
});

describe("PUT /api/routes/[id]", () => {
  it("returns 200 with updated route on success", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (updateRoute as jest.Mock).mockResolvedValue({ ...mockRoute, name: "Updated" });

    const body = {
      name: "Updated",
      upstreamUrl: "http://backend:8080",
      hostnames: ["example.com"],
      isEnabled: true,
    };
    const response = await PUT(makeRequest("PUT", "route-1", body), { params });
    expect(response.status).toBe(200);
    const result = await response.json();
    expect(result.name).toBe("Updated");
  });

  it("passes through 403 Problem Details from ProxyManager API", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);
    (updateRoute as jest.Mock).mockRejectedValue(forbiddenProblem);

    const response = await PUT(makeRequest("PUT", "route-1", {}), { params });
    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });

  it("passes through 404 Problem Details from ProxyManager API", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (updateRoute as jest.Mock).mockRejectedValue(notFoundProblem);

    const response = await PUT(makeRequest("PUT", "route-1", {}), { params });
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body.status).toBe(404);
  });
});

describe("DELETE /api/routes/[id]", () => {
  it("returns 204 on successful deletion for admin", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (deleteRoute as jest.Mock).mockResolvedValue(undefined);

    const response = await DELETE(makeRequest("DELETE", "route-1"), { params });
    expect(response.status).toBe(204);
  });

  it("returns 403 Problem Details for non-admin", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);

    const response = await DELETE(makeRequest("DELETE", "route-1"), { params });
    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });

  it("passes through 404 Problem Details from ProxyManager API", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (deleteRoute as jest.Mock).mockRejectedValue(notFoundProblem);

    const response = await DELETE(makeRequest("DELETE", "missing"), { params: Promise.resolve({ id: "missing" }) });
    expect(response.status).toBe(404);
    const body = await response.json();
    expect(body.status).toBe(404);
  });
});
