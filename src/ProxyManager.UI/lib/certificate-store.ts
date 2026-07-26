import type { Certificate } from "@/types";
import { MOCK_CERTIFICATES } from "@/lib/mock-certificates";

export type CertificateInput = Omit<Certificate, "id" | "uploadedAt">;

let certificates: Certificate[] = [...MOCK_CERTIFICATES];
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((listener) => listener());
}

export const certificateStore = {
  subscribe(listener: () => void) {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },

  getSnapshot(): Certificate[] {
    return certificates;
  },

  getById(id: string): Certificate | undefined {
    return certificates.find((cert) => cert.id === id);
  },

  create(input: CertificateInput): Certificate {
    const certificate: Certificate = {
      ...input,
      id: crypto.randomUUID(),
      uploadedAt: new Date().toISOString(),
    };
    certificates = [...certificates, certificate];
    notify();
    return certificate;
  },

  update(id: string, input: CertificateInput): Certificate | undefined {
    let updated: Certificate | undefined;
    certificates = certificates.map((cert) => {
      if (cert.id !== id) return cert;
      updated = { ...cert, ...input };
      return updated;
    });
    notify();
    return updated;
  },

  remove(id: string) {
    certificates = certificates.filter((cert) => cert.id !== id);
    notify();
  },
};
