export interface UserSession {
  userId: string;
  groups: string[];
  isAdmin: boolean;
  accessToken: string;
}

export interface ProxyHost {
  id: string;
  name: string;
  upstreamUrl: string;
  hostnames: string[];
  pathPrefix?: string;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
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
