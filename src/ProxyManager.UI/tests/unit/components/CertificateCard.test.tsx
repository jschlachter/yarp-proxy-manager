import { render, screen, fireEvent } from "@testing-library/react";
import CertificateCard from "@/components/certificates/CertificateCard";
import type { Certificate } from "@/types";

const manualCertificate: Certificate = {
  id: "cert-1",
  name: "Wildcard – *.example.com",
  format: "Pfx",
  certificateAssetId: "asset-1",
  certificateFileName: "wildcard.pfx",
  subject: "CN=*.example.com",
  subjectAlternativeNames: ["*.example.com"],
  notBefore: "2026-01-01T00:00:00Z",
  notAfter: "2027-01-01T00:00:00Z",
  thumbprint: "AABBCC",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
  source: "Manual",
};

const letsEncryptCertificate: Certificate = {
  ...manualCertificate,
  id: "cert-2",
  name: "example.com",
  source: "LetsEncrypt",
};

describe("CertificateCard", () => {
  describe("Source badge", () => {
    it("renders a Manual badge for a manually-uploaded certificate", () => {
      render(<CertificateCard certificate={manualCertificate} isAdmin onDelete={jest.fn()} />);
      expect(screen.getByText("Manual")).toBeInTheDocument();
      expect(screen.queryByText("Let's Encrypt")).not.toBeInTheDocument();
    });

    it("renders a Let's Encrypt badge for a machine-issued certificate", () => {
      render(<CertificateCard certificate={letsEncryptCertificate} isAdmin onDelete={jest.fn()} />);
      expect(screen.getByText("Let's Encrypt")).toBeInTheDocument();
    });
  });

  describe("manual actions", () => {
    it("shows an active Edit link and Delete button for a manual certificate", () => {
      render(<CertificateCard certificate={manualCertificate} isAdmin onDelete={jest.fn()} />);
      expect(screen.getByRole("link", { name: "Edit" })).toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Delete" })).not.toBeDisabled();
    });

    it("calls onDelete when Delete is clicked for a manual certificate", () => {
      const onDelete = jest.fn();
      render(<CertificateCard certificate={manualCertificate} isAdmin onDelete={onDelete} />);
      fireEvent.click(screen.getByRole("button", { name: "Delete" }));
      expect(onDelete).toHaveBeenCalledWith("cert-1");
    });

    it("disables Edit and Delete actions for a Let's Encrypt certificate", () => {
      render(<CertificateCard certificate={letsEncryptCertificate} isAdmin onDelete={jest.fn()} />);
      expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
      expect(screen.getByRole("button", { name: "Edit" })).toBeDisabled();
      expect(screen.getByRole("button", { name: "Delete" })).toBeDisabled();
    });

    it("does not call onDelete when Delete is clicked for a Let's Encrypt certificate", () => {
      const onDelete = jest.fn();
      render(<CertificateCard certificate={letsEncryptCertificate} isAdmin onDelete={onDelete} />);
      fireEvent.click(screen.getByRole("button", { name: "Delete" }));
      expect(onDelete).not.toHaveBeenCalled();
    });

    it("hides actions entirely for non-admin viewers regardless of source", () => {
      render(
        <CertificateCard certificate={letsEncryptCertificate} isAdmin={false} onDelete={jest.fn()} />
      );
      expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
      expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
      expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
    });
  });
});
