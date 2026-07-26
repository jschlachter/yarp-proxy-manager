import type { Certificate } from "@/types";

export const MOCK_CERTIFICATES: Certificate[] = [
  {
    id: "cert-1",
    friendlyName: "Wildcard – *.west94.io",
    format: "PFX",
    certificateFileName: "wildcard-west94-io.pfx",
    hasPassphrase: true,
    uploadedAt: "2026-06-02T14:20:00.000Z",
  },
  {
    id: "cert-2",
    friendlyName: "Authentik SSO",
    format: "PEM",
    certificateFileName: "auth-west94-io.crt",
    keyFileName: "auth-west94-io.key",
    hasPassphrase: false,
    uploadedAt: "2026-05-14T09:05:00.000Z",
  },
];
