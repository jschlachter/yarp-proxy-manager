export interface UserSession {
  userId: string;
  name: string;
  groups: string[];
  isAdmin: boolean;
  accessToken: string;
}

/** Server round-trips `Enum.ToString()` — "Manual" | "LetsEncrypt". */
export type TlsMode = "Manual" | "LetsEncrypt";

export interface ProxyHost {
  id: string;
  domainNames: string[];
  destination: string;
  isEnabled: boolean;
  certificateId?: string;
  tlsMode: TlsMode;
  healthCheck?: HealthCheck | null;
}

/** YARP registered policy names; the server round-trips them as-is (ADR 0003). */
export type ActiveHealthPolicy = "ConsecutiveFailures";
export type PassiveHealthPolicy = "TransportFailureRate";
export type AvailableDestinationsPolicy = "HealthyAndUnknown" | "HealthyOrPanic";

/** Active (probing) check. Null tuning fields mean YARP's default applies. */
export interface ActiveHealthCheck {
  policy: ActiveHealthPolicy;
  intervalSeconds: number | null;
  timeoutSeconds: number | null;
  path: string | null;
  query: string | null;
  healthAddress: string | null;
  consecutiveFailuresThreshold: number | null;
}

/** Passive (traffic-observing) check. Null tuning fields mean YARP's default applies. */
export interface PassiveHealthCheck {
  policy: PassiveHealthPolicy;
  reactivationPeriodSeconds: number | null;
  failureRateLimit: number | null;
}

/** A null `active`/`passive` means that check is off; both null clears the checks on update. */
export interface HealthCheck {
  availableDestinationsPolicy: AvailableDestinationsPolicy;
  active: ActiveHealthCheck | null;
  passive: PassiveHealthCheck | null;
}

export type DestinationHealth = "Healthy" | "Unhealthy" | "Unknown";

/** Live health of a proxy host, served by the proxy at /manage/api/health-states. */
export interface HealthState {
  proxyHostId: string;
  status: DestinationHealth;
  active: DestinationHealth | null;
  passive: DestinationHealth | null;
  checkedAt: string;
}

/** Server round-trips `Enum.ToString()` — "Pfx" | "Pem", not "PFX" | "PEM". */
export type CertificateFormat = "Pfx" | "Pem";

/** Server round-trips `Enum.ToString()` — "Manual" | "LetsEncrypt". */
export type CertificateSource = "Manual" | "LetsEncrypt";

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
  source: CertificateSource;
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
