import { act, fireEvent, render, screen, within } from "@testing-library/react";
import CertificateViewDialog from "@/components/certificates/CertificateViewDialog";
import type { Certificate } from "@/types";

const DAY = 24 * 60 * 60 * 1000;

const pemCertificate: Certificate = {
  id: "cert-1",
  name: "Example PEM",
  format: "Pem",
  certificateAssetId: "asset-1",
  keyAssetId: "asset-2",
  certificateFileName: "example.crt",
  keyFileName: "example.key",
  subject: "CN=example.com, O=Acme",
  subjectAlternativeNames: ["example.com", "www.example.com", "api.example.com"],
  notBefore: "2026-01-01T00:00:00Z",
  notAfter: new Date(Date.now() + 90 * DAY).toISOString(),
  thumbprint: "0123456789ABCDEF0123456789ABCDEF01234567",
  createdAt: "2026-01-02T03:04:05Z",
  updatedAt: "2026-02-03T04:05:06Z",
  source: "Manual",
};

const pfxCertificate: Certificate = {
  ...pemCertificate,
  id: "cert-2",
  format: "Pfx",
  keyAssetId: undefined,
  keyFileName: undefined,
  certificateFileName: "example.pfx",
};

function renderDialog(certificate: Certificate | undefined) {
  return render(<CertificateViewDialog certificate={certificate} onOpenChange={jest.fn()} />);
}

describe("CertificateViewDialog", () => {
  it("renders the certificate details", () => {
    renderDialog(pemCertificate);

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(screen.getByText("Common name").nextSibling).toHaveTextContent("example.com");
    expect(screen.getByText("CN=example.com, O=Acme")).toBeInTheDocument();
    const sans = within(screen.getByText("Alternative names").nextElementSibling as HTMLElement);
    for (const san of pemCertificate.subjectAlternativeNames) {
      expect(sans.getByText(san)).toBeInTheDocument();
    }
    for (const date of [pemCertificate.notBefore, pemCertificate.notAfter, pemCertificate.createdAt]) {
      expect(screen.getByTitle(date)).toHaveTextContent(new Date(date).toLocaleString());
    }
    expect(screen.getByText(pemCertificate.thumbprint)).toBeInTheDocument();
    expect(screen.getByText("Key file")).toBeInTheDocument();
    expect(screen.getByText("example.key")).toBeInTheDocument();
  });

  it("does not render a key file row for a PFX certificate", () => {
    renderDialog(pfxCertificate);
    expect(screen.getByText("example.pfx")).toBeInTheDocument();
    expect(screen.queryByText("Key file")).not.toBeInTheDocument();
  });

  it("shows Expired for an expired certificate", () => {
    renderDialog({ ...pemCertificate, notAfter: new Date(Date.now() - DAY).toISOString() });
    expect(screen.getAllByText("Expired").length).toBeGreaterThan(0);
  });

  it("shows the critical badge for a certificate expiring in 5 days", () => {
    renderDialog({ ...pemCertificate, notAfter: new Date(Date.now() + 5 * DAY).toISOString() });
    expect(screen.getAllByText("Critical").length).toBeGreaterThan(0);
    expect(screen.getByText("(in 5 days)")).toBeInTheDocument();
  });

  it("copies the thumbprint to the clipboard", async () => {
    const writeText = jest.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });

    renderDialog(pemCertificate);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "Copy thumbprint" }));
    });

    expect(writeText).toHaveBeenCalledWith(pemCertificate.thumbprint);
    expect(screen.getByText("Copied")).toBeInTheDocument();
  });

  it("renders nothing when no certificate is selected", () => {
    renderDialog(undefined);
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
