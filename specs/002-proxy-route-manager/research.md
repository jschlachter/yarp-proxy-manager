# Research: Proxy Route Manager Application

**Feature**: `002-proxy-route-manager` | **Date**: 2026-04-05

## Decision Log

### 1. Authentication Strategy

**Decision**: Trusted forwarded headers from ProxyManager — no auth library in the UI

**Rationale**: ProxyManager (YARP) already owns the OIDC session with Authentik for all browser users. Hosting the UI behind ProxyManager means the proxy handles authentication, token refresh, and redirect-on-401 before any request reaches the Next.js container. Adding Auth.js v5 would duplicate the OIDC client, create a second Authentik application registration, and add a dependency that serves no purpose not already covered by the existing proxy session.

ProxyManager forwards the authenticated user's identity to the Next.js container via YARP request header transforms:

| Header | Value | Purpose |
|---|---|---|
| `X-Auth-Sub` | Authentik `sub` claim | User identity |
| `X-Auth-Groups` | Comma-separated group names | Admin role determination |
| `Authorization` | `Bearer <access_token>` | Credential for ProxyManager.API calls |

The Next.js `lib/auth.ts` reads these headers from the incoming server request. `middleware.ts` rejects any request missing `X-Auth-Sub` as a defense-in-depth guard (pod networking already prevents direct access).

**Alternatives considered**:
- `next-auth@5.x` (Auth.js v5) with Authentik provider: Eliminated because ProxyManager already handles the full OIDC flow. Two OIDC clients for the same session is unnecessary duplication and contradicts the minimal-libraries constraint.
- `openid-client` (manual OIDC): Same objection; even more code.

**Admin role detection**: `lib/auth.ts` checks whether `X-Auth-Groups` contains the configured admin group name (default: `proxy-admins`). This mirrors how ProxyManager.API validates the same claim from the JWT.

**YARP transform configuration** (added to `proxysettings.{Environment}.json`):
```jsonc
"Transforms": [
  { "RequestHeader": "X-Auth-Sub",    "Set": "{auth.sub}" },
  { "RequestHeader": "X-Auth-Groups", "Set": "{auth.groups}" },
  { "RequestHeader": "Authorization", "Set": "Bearer {auth.access_token}" }
]
```

---

### 2. Modular Architecture (Next.js Route Groups)

**Decision**: Single `(dashboard)` route group with per-module subdirectories

**Rationale**: No `(auth)` route group is needed — ProxyManager handles unauthenticated users before requests reach the container. Route groups create layout boundaries without polluting URLs. Each functional module (routes, future maintainer management, future audit) is an isolated folder under `(dashboard)/`. Adding a new module requires only creating a new subfolder and a `page.tsx`.

**Module registry pattern**: `lib/modules.ts` exports an array of `{ label, href, icon }` objects. The sidebar iterates this array. Enabling/disabling a module means adding/removing its entry from the registry — no conditional rendering scattered through components.

---

### 3. shadcn/ui + Tailwind CSS 4

**Decision**: shadcn/ui (latest) initialized with Tailwind CSS 4

**Rationale**: shadcn/ui components are copied directly into the project (no runtime dependency), which satisfies the minimal-libraries constraint — only the components actually used are included. Tailwind CSS 4 requires no `tailwind.config.ts`; theme tokens are defined via `@theme` in `globals.css`.

**Initialization sequence**:
```bash
npx create-next-app@latest src/ProxyManager.UI --app --ts --tailwind
cd src/ProxyManager.UI
npx shadcn-ui@latest init
```

**Animation**: Tailwind v4 removed `tailwindcss-animate` plugin. Use `tw-animate-css` or CSS custom properties directly for any animation needs.

---

### 4. SSL Certificate Handling

**Decision**: SSL termination is handled by ProxyManager (YARP). The UI container runs plain HTTP internally and is only reachable via the ProxyManager pod network.

**Rationale**: Centralizing TLS at the YARP layer is consistent with the existing architecture. The UI does not need to load or manage certificates. The shared certificate location (`/certs/`) is mounted on the ProxyManager container, not the UI container.

---

### 5. ProxyManager API Integration

**Decision**: Thin fetch wrapper in `lib/proxy-manager-client.ts` — no SDK, no generated client

**Rationale**: The ProxyManager API surface consumed by the UI is small (list routes, create, update, delete). The `Authorization: Bearer` header forwarded by ProxyManager is read from the incoming request and passed through to ProxyManager.API calls — the UI acts as an authenticated proxy on behalf of the user with zero token management of its own.

**Base URL**: `PROXY_MANAGER_API_URL` environment variable (internal pod network, e.g., `http://proxymanager-api:5001`)

**Deferred operations**: Maintainer assignment and audit log endpoints are not yet implemented in ProxyManager.API. When available, new methods are added to `proxy-manager-client.ts` and new pages under `(dashboard)/`. No structural changes required.

---

### 6. Environment Variables (Runtime)

Only two variables are required. No auth secrets live in the UI container.

| Variable | Purpose |
|---|---|
| `PROXY_MANAGER_API_URL` | Internal URL of ProxyManager.API |
| `ADMIN_GROUP_CLAIM` | Authentik group name that grants admin role (default: `proxy-admins`) |

All OIDC secrets (`AUTH_*`) remain in the ProxyManager container only.
