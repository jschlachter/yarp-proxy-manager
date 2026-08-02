export interface UserSession {
  userId: string;
  name: string;
  groups: string[];
  isAdmin: boolean;
  accessToken: string;
}

export interface ProxyHost {
  id: string;
  domainNames: string[];
  destination: string;
  isEnabled: boolean;
  certificateId?: string;
}

/** Server round-trips `Enum.ToString()` — "Pfx" | "Pem", not "PFX" | "PEM". */
export type CertificateFormat = "Pfx" | "Pem";

export interface Certificate {
  id: string;
  name: string;
  format: CertificateFormat;
  certificateAssetId: string;
  keyAssetId?: string;
  certificateFileName: string;
  keyFileName?: string;
  subject: string;
  subjectAlternativeNames: string[];
  notBefore: string;
  notAfter: string;
  thumbprint: string;
  createdAt: string;
  updatedAt: string;
}

export type FileAssetStatus = "Staged" | "Committed" | "Deleted";

export interface FileAsset {
  id: string;
  assetType: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  status: FileAssetStatus;
  ownerType?: string;
  ownerId?: string;
  uploadedBy: string;
  createdAt: string;
  committedAt?: string;
}

/** @future — pending ProxyManager API implementation */
export interface MaintainerAssignment {
  proxyHostId: string;
  userId: string;
  userName: string;
  assignedBy: string;
  assignedAt: string;
}

/** @future — pending ProxyManager API implementation */
export interface AuditEntry {
  id: string;
  occurredAt: string;
  actorId: string;
  actorName: string;
  action:
    | "host.create"
    | "host.update"
    | "host.delete"
    | "maintainer.assign"
    | "maintainer.remove";
  proxyHostId: string;
  proxyHostName: string;
  detail: Record<string, unknown> | null;
}

export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
}
