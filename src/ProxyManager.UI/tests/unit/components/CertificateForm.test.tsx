import { render, screen, fireEvent, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import CertificateForm from "@/components/certificates/CertificateForm";
import type { Certificate } from "@/types";

const mockCertificate: Certificate = {
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
};

function makeFile(name: string, content = "file-bytes"): File {
  return new File([content], name, { type: "application/octet-stream" });
}

describe("CertificateForm", () => {
  describe("create mode (no initial data)", () => {
    it("renders required fields", () => {
      render(<CertificateForm onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Name")).toBeInTheDocument();
      expect(screen.getByLabelText("Certificate File")).toBeInTheDocument();
    });

    it("shows validation errors when submitted with empty required fields", async () => {
      render(<CertificateForm onSubmit={jest.fn()} />);
      fireEvent.click(screen.getByRole("button", { name: /upload/i }));
      await waitFor(() => {
        expect(screen.getAllByRole("alert").length).toBeGreaterThan(0);
      });
    });

    it("submits the real File object for the certificate file", async () => {
      const onSubmit = jest.fn();
      render(<CertificateForm onSubmit={onSubmit} />);

      await userEvent.type(screen.getByLabelText("Name"), "My Cert");
      const file = makeFile("cert.pfx");
      await userEvent.upload(screen.getByLabelText("Certificate File"), file);

      fireEvent.click(screen.getByRole("button", { name: /upload/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({
            name: "My Cert",
            format: "Pfx",
            certificateFile: file,
          })
        );
      });
    });

    it("submits both certificate and key files for PEM format", async () => {
      const onSubmit = jest.fn();
      render(<CertificateForm onSubmit={onSubmit} />);

      await userEvent.type(screen.getByLabelText("Name"), "PEM Cert");
      await userEvent.selectOptions(screen.getByLabelText("Format"), "Pem");

      const certFile = makeFile("cert.pem");
      const keyFile = makeFile("key.pem");
      await userEvent.upload(screen.getByLabelText("Certificate File"), certFile);
      await userEvent.upload(screen.getByLabelText("Key File (optional)"), keyFile);

      fireEvent.click(screen.getByRole("button", { name: /upload/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith(
          expect.objectContaining({
            format: "Pem",
            certificateFile: certFile,
            keyFile,
          })
        );
      });
    });

    it("disables the submit button and shows the submitting label while isSubmitting", () => {
      render(<CertificateForm onSubmit={jest.fn()} isSubmitting submittingLabel="Uploading…" />);
      const button = screen.getByRole("button", { name: "Uploading…" });
      expect(button).toBeDisabled();
    });
  });

  describe("edit mode (with initial data)", () => {
    it("pre-fills the name field and hides file inputs (assets are immutable)", () => {
      render(<CertificateForm initialData={mockCertificate} onSubmit={jest.fn()} />);
      expect(screen.getByLabelText("Name")).toHaveValue(mockCertificate.name);
      expect(screen.queryByLabelText("Certificate File")).not.toBeInTheDocument();
    });

    it("calls onSubmit with only name and passPhrase", async () => {
      const onSubmit = jest.fn();
      render(<CertificateForm initialData={mockCertificate} onSubmit={onSubmit} />);

      fireEvent.click(screen.getByRole("button", { name: /save/i }));

      await waitFor(() => {
        expect(onSubmit).toHaveBeenCalledWith({
          name: mockCertificate.name,
          passPhrase: undefined,
        });
      });
    });
  });

  describe("readOnly mode", () => {
    it("renders certificate details as display-only text", () => {
      render(<CertificateForm initialData={mockCertificate} onSubmit={jest.fn()} readOnly />);
      expect(screen.getByText(mockCertificate.name)).toBeInTheDocument();
      expect(screen.getByText(mockCertificate.certificateFileName)).toBeInTheDocument();
      expect(screen.queryByRole("button")).not.toBeInTheDocument();
    });

    it("flags an expired certificate", () => {
      const expired: Certificate = { ...mockCertificate, notAfter: "2020-01-01T00:00:00Z" };
      render(<CertificateForm initialData={expired} onSubmit={jest.fn()} readOnly />);
      expect(screen.getByText(/expired/i)).toBeInTheDocument();
    });
  });
});
