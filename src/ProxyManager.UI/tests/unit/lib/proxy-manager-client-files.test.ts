import type { UserSession, FileAsset, Certificate } from "@/types";
import {
  uploadFileAsset,
  listCertificates,
  createCertificate,
  updateCertificate,
  deleteCertificate,
} from "@/lib/proxy-manager-client";

const adminSession: UserSession = {
  userId: "user-1",
  name: "Admin",
  groups: ["proxy-admins"],
  isAdmin: true,
  accessToken: "test-token",
};

const mockCertificate: Certificate = {
  id: "cert-1",
  name: "My Cert",
  format: "Pfx",
  certificateAssetId: "asset-1",
  certificateFileName: "cert.pfx",
  subject: "CN=example.com",
  subjectAlternativeNames: [],
  notBefore: "2026-01-01T00:00:00Z",
  notAfter: "2027-01-01T00:00:00Z",
  thumbprint: "AABBCC",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const mockFileAsset: FileAsset = {
  id: "asset-1",
  assetType: "certificate",
  fileName: "cert.pfx",
  contentType: "application/x-pkcs12",
  sizeBytes: 1024,
  sha256: "a".repeat(64),
  status: "Staged",
  uploadedBy: "user-1",
  createdAt: "2026-01-01T00:00:00Z",
};

const originalEnv = process.env;

beforeEach(() => {
  process.env = {
    ...originalEnv,
    PROXY_MANAGER_API_URL: "http://api:5001",
    PROXY_MANAGER_FILES_URL: "http://files:5002",
  };
  global.fetch = jest.fn();
});

afterEach(() => {
  process.env = originalEnv;
  jest.restoreAllMocks();
});

describe("uploadFileAsset", () => {
  it("posts multipart/form-data without a manual Content-Type header", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve(mockFileAsset),
    });

    const file = new File(["bytes"], "cert.pfx", { type: "application/x-pkcs12" });
    const result = await uploadFileAsset(adminSession, file, "certificate");

    expect(global.fetch).toHaveBeenCalledWith(
      "http://files:5002/files?assetType=certificate",
      expect.objectContaining({
        method: "POST",
        headers: {
          Authorization: "Bearer test-token",
          "X-Requested-With": "proxymanager-ui",
        },
      })
    );

    const call = (global.fetch as jest.Mock).mock.calls[0][1];
    expect(call.headers["Content-Type"]).toBeUndefined();
    expect(call.body).toBeInstanceOf(FormData);
    expect(result.id).toBe("asset-1");
  });

  it("throws ProblemDetails on a non-OK response", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: false,
      status: 415,
      headers: new Headers({ "Content-Type": "application/problem+json" }),
      json: () =>
        Promise.resolve({
          type: "https://tools.ietf.org/html/rfc9457",
          title: "Unsupported content",
          status: 415,
          detail: "Extension not allowed",
        }),
    });

    const file = new File(["bytes"], "cert.txt");
    await expect(uploadFileAsset(adminSession, file, "certificate")).rejects.toMatchObject({
      status: 415,
    });
  });
});

describe("certificate CRUD", () => {
  it("listCertificates fetches GET /certificates", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve({ items: [mockCertificate], page: 1, pageSize: 50, totalCount: 1 }),
    });

    const result = await listCertificates(adminSession);

    expect(global.fetch).toHaveBeenCalledWith(
      "http://api:5001/certificates?page=1&pageSize=50",
      expect.any(Object)
    );
    expect(result.items).toHaveLength(1);
  });

  it("createCertificate sends POST /certificates with asset ids", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      status: 201,
      json: () => Promise.resolve(mockCertificate),
    });

    const body = {
      name: "My Cert",
      format: "Pfx",
      certificateAssetId: "asset-1",
    };
    await createCertificate(adminSession, body);

    expect(global.fetch).toHaveBeenCalledWith(
      "http://api:5001/certificates",
      expect.objectContaining({ method: "POST", body: JSON.stringify(body) })
    );
  });

  it("updateCertificate sends PUT /certificates/:id", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({
      ok: true,
      status: 200,
      json: () => Promise.resolve(mockCertificate),
    });

    await updateCertificate(adminSession, "cert-1", { name: "Renamed" });

    expect(global.fetch).toHaveBeenCalledWith(
      "http://api:5001/certificates/cert-1",
      expect.objectContaining({ method: "PUT" })
    );
  });

  it("deleteCertificate sends DELETE /certificates/:id", async () => {
    (global.fetch as jest.Mock).mockResolvedValue({ ok: true, status: 204 });

    await deleteCertificate(adminSession, "cert-1");

    expect(global.fetch).toHaveBeenCalledWith(
      "http://api:5001/certificates/cert-1",
      expect.objectContaining({ method: "DELETE" })
    );
  });
});
