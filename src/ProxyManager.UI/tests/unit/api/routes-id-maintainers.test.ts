/**
 * @jest-environment node
 */

jest.mock("@/lib/auth");
jest.mock("@/lib/proxy-manager-client");

import { getSession } from "@/lib/auth";
import { listMaintainers, assignMaintainer, removeMaintainer } from "@/lib/proxy-manager-client";
import { GET, POST } from "@/app/api/routes/[id]/maintainers/route";
import { DELETE } from "@/app/api/routes/[id]/maintainers/[userId]/route";
import type { UserSession, MaintainerAssignment } from "@/types";

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

const mockMaintainer: MaintainerAssignment = {
  proxyHostId: "route-1",
  userId: "user-2",
  userName: "Jane Doe",
  assignedBy: "admin-1",
  assignedAt: "2026-04-01T00:00:00Z",
};

// ProxyManager.API returns 501 because these endpoints are not yet implemented upstream
const upstreamNotImplemented = {
  type: "https://tools.ietf.org/html/rfc9457",
  title: "Not Implemented",
  status: 501,
  detail: "Maintainer API not yet available",
};

const routeParams = Promise.resolve({ id: "route-1" });
const maintainerParams = Promise.resolve({ id: "route-1", userId: "user-2" });

function makeRequest(method: string, url: string, body?: unknown): Request {
  const headers: Record<string, string> = {};
  if (body) headers["Content-Type"] = "application/json";
  return new Request(url, {
    method,
    body: body ? JSON.stringify(body) : undefined,
    headers,
  });
}

describe("GET /api/routes/[id]/maintainers", () => {
  it("delegates to listMaintainers and returns 200 on success", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (listMaintainers as jest.Mock).mockResolvedValue([mockMaintainer]);

    const response = await GET(
      makeRequest("GET", "http://localhost/api/routes/route-1/maintainers"),
      { params: routeParams }
    );
    expect(response.status).toBe(200);
    const body = await response.json();
    expect(body).toHaveLength(1);
    expect(body[0].userId).toBe("user-2");
  });

  it("passes through 501 Problem Details when ProxyManager API returns not-implemented", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (listMaintainers as jest.Mock).mockRejectedValue(upstreamNotImplemented);

    const response = await GET(
      makeRequest("GET", "http://localhost/api/routes/route-1/maintainers"),
      { params: routeParams }
    );
    expect(response.status).toBe(501);
    const body = await response.json();
    expect(body.status).toBe(501);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });
});

describe("POST /api/routes/[id]/maintainers", () => {
  it("returns 403 Problem Details for non-admin without calling upstream", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);

    const response = await POST(
      makeRequest("POST", "http://localhost/api/routes/route-1/maintainers", { userId: "user-2" }),
      { params: routeParams }
    );
    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
    expect(assignMaintainer).not.toHaveBeenCalled();
  });

  it("delegates to assignMaintainer and returns 201 on success", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (assignMaintainer as jest.Mock).mockResolvedValue(mockMaintainer);

    const response = await POST(
      makeRequest("POST", "http://localhost/api/routes/route-1/maintainers", { userId: "user-2" }),
      { params: routeParams }
    );
    expect(response.status).toBe(201);
    const body = await response.json();
    expect(body.userId).toBe("user-2");
  });

  it("passes through 501 Problem Details when ProxyManager API returns not-implemented", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (assignMaintainer as jest.Mock).mockRejectedValue(upstreamNotImplemented);

    const response = await POST(
      makeRequest("POST", "http://localhost/api/routes/route-1/maintainers", { userId: "user-2" }),
      { params: routeParams }
    );
    expect(response.status).toBe(501);
    const body = await response.json();
    expect(body.status).toBe(501);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });
});

describe("DELETE /api/routes/[id]/maintainers/[userId]", () => {
  it("returns 403 Problem Details for non-admin without calling upstream", async () => {
    (getSession as jest.Mock).mockReturnValue(regularSession);

    const response = await DELETE(
      makeRequest("DELETE", "http://localhost/api/routes/route-1/maintainers/user-2"),
      { params: maintainerParams }
    );
    expect(response.status).toBe(403);
    const body = await response.json();
    expect(body.status).toBe(403);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
    expect(removeMaintainer).not.toHaveBeenCalled();
  });

  it("delegates to removeMaintainer and returns 204 on success", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (removeMaintainer as jest.Mock).mockResolvedValue(undefined);

    const response = await DELETE(
      makeRequest("DELETE", "http://localhost/api/routes/route-1/maintainers/user-2"),
      { params: maintainerParams }
    );
    expect(response.status).toBe(204);
  });

  it("passes through 501 Problem Details when ProxyManager API returns not-implemented", async () => {
    (getSession as jest.Mock).mockReturnValue(adminSession);
    (removeMaintainer as jest.Mock).mockRejectedValue(upstreamNotImplemented);

    const response = await DELETE(
      makeRequest("DELETE", "http://localhost/api/routes/route-1/maintainers/user-2"),
      { params: maintainerParams }
    );
    expect(response.status).toBe(501);
    const body = await response.json();
    expect(body.status).toBe(501);
    expect(response.headers.get("Content-Type")).toContain("application/problem+json");
  });
});
