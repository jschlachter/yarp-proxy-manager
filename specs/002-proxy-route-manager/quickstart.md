# Quickstart: Proxy Route Manager UI

**Feature**: `002-proxy-route-manager` | **Date**: 2026-04-05

## Prerequisites

- Node.js 24 LTS
- A running ProxyManager instance (handles auth and TLS — see root CLAUDE.md)
- A running ProxyManager.API instance (see root CLAUDE.md)

> **No database. No auth library.** Authentication is handled by ProxyManager (YARP). The UI reads identity from forwarded request headers. All persistent data is owned by the ProxyManager API.

---

## Setup

### 1. Install Dependencies

```bash
cd src/ProxyManager.UI
npm install
```

### 2. Configure Environment

```bash
cp .env.example .env.local
```

Only two variables are required:

```env
# Internal URL of ProxyManager.API (pod network)
PROXY_MANAGER_API_URL=http://localhost:5001

# Authentik group name that grants admin access (must match ProxyManager config)
ADMIN_GROUP_CLAIM=proxy-admins
```

No `AUTH_*` secrets. OIDC credentials live in the ProxyManager container only.

### 3. Configure ProxyManager to Forward Auth Headers

Add the UI route to `proxysettings.Development.json` (and the production equivalent):

```jsonc
{
  "Routes": {
    "ui-route": {
      "ClusterId": "ui-cluster",
      "Match": { "Path": "/manage/{**catch-all}" },
      "Transforms": [
        { "PathRemovePrefix": "/manage" },
        { "RequestHeader": "X-Auth-Sub",    "Set": "{auth.sub}" },
        { "RequestHeader": "X-Auth-Groups", "Set": "{auth.groups}" },
        { "RequestHeader": "Authorization", "Set": "Bearer {auth.access_token}" }
      ],
      "AuthorizationPolicy": "RequireAuthenticatedUser"
    }
  },
  "Clusters": {
    "ui-cluster": {
      "Destinations": {
        "primary": { "Address": "http://localhost:3000/" }
      }
    }
  }
}
```

The UI is then accessible at `https://<proxy-host>/manage/` in development.

### 4. Start Development Server

```bash
npm run dev
# Runs on http://localhost:3000 (accessed via ProxyManager at /manage)
```

---

## Local Development Without ProxyManager

To develop the UI without routing all traffic through ProxyManager, inject the auth headers directly using a `.env.local` override and a dev-mode header injection middleware:

```env
# .env.local (dev only)
DEV_AUTH_SUB=local-user-001
DEV_AUTH_GROUPS=proxy-admins
DEV_AUTH_TOKEN=dev-token
```

`middleware.ts` checks for `NODE_ENV === 'development'` and synthesizes the auth headers from these env vars when `X-Auth-Sub` is absent. This path is compiled out in production builds.

---

## Running Tests

```bash
# Unit + component tests
npm test

# With coverage (≥80% required)
npm run test:coverage

# E2E tests (requires dev server running)
npm run test:e2e
```

---

## Building for Production

```bash
npm run build
npm start
```

---

## Container Build (Podman)

```bash
# From repo root
podman build -t proxymanager-ui:latest src/ProxyManager.UI/

# Run — only two env vars needed
podman run \
  -e PROXY_MANAGER_API_URL=http://proxymanager-api:5001 \
  -e ADMIN_GROUP_CLAIM=proxy-admins \
  proxymanager-ui:latest
```

The container listens on port 3000 internally. It must only be reachable from the ProxyManager container via the pod network — never exposed directly to external traffic.

---

## SSL Certificates

SSL is terminated by ProxyManager (YARP). No certificate configuration is needed here. The shared `/certs/` volume is mounted on the ProxyManager container only.

---

## Adding a New Module

1. Create a new folder under `src/ProxyManager.UI/app/(dashboard)/your-module/`
2. Add `page.tsx` (and optionally `layout.tsx`) for the module's UI
3. Add an entry to `src/ProxyManager.UI/lib/modules.ts`:
   ```typescript
   { label: 'Your Module', href: '/your-module', icon: YourIcon }
   ```
4. The sidebar renders from this registry automatically — no other files to change.

---

## Enabling Maintainer Assignment and Audit Log

These features are deferred until ProxyManager API implements the corresponding endpoints. When ready:

1. Add new methods to `lib/proxy-manager-client.ts` (the forwarded `Authorization` header is already available)
2. Add BFF route handlers under `app/api/routes/[id]/maintainers/` and `app/api/audit/`
3. Add UI pages under `app/(dashboard)/`
4. Add entries to `lib/modules.ts` for any new top-level navigation items

No structural changes to existing code are required.

---

## Deployment (Quadlet/systemd)

Add a `proxymanager-ui.container` unit file to `systemd/` following the same pattern as existing containers. Connect to `proxymanager.network` so the container is reachable by ProxyManager on the internal pod network. Do **not** publish port 3000 externally.
