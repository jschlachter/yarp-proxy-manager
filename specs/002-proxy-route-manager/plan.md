# Implementation Plan: Proxy Route Manager Application

**Branch**: `002-proxy-route-manager` | **Date**: 2026-04-05 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `specs/002-proxy-route-manager/spec.md`

## Summary

Build a browser-based web UI (Next.js 15, Tailwind CSS, shadcn/ui) hosted behind ProxyManager (YARP). ProxyManager owns the OIDC session with Authentik and forwards the authenticated user's identity and access token to the Next.js container via request headers. The UI reads routing configuration from the ProxyManager API and displays it as-is; ProxyManager is responsible for translating that data into its internal routing configuration. Maintainer assignment and audit logging are delegated to the ProxyManager API and will be surfaced in the UI once those API endpoints are available.

## Technical Context

**Language/Version**: TypeScript / Node.js 24 LTS
**Primary Dependencies**: Next.js 15 (App Router), shadcn/ui, Tailwind CSS 4
**Storage**: N/A — no local database; all data (routes, maintainer assignments, audit log) is owned by the ProxyManager API
**Testing**: Jest + React Testing Library (unit/component), Playwright (E2E)
**Target Platform**: Browser (served as Next.js Docker container, accessed via ProxyManager)
**Project Type**: Web application (frontend management UI)
**Performance Goals**: Initial page load < 3 s; all form submit/response cycles provide user feedback within 200 ms; list views paginated (max 50 per page)
**Constraints**: Minimal libraries (no auth library, no ORM, no database client, no component library beyond shadcn/ui, no state management library); must not be directly reachable outside the pod network; SSL terminated by ProxyManager
**Scale/Scope**: Internal tool; expected < 50 concurrent users

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Gate | Principle | Status | Notes |
|------|-----------|--------|-------|
| Tests written and confirmed failing before implementation | II. Testing Standards | ✅ Required | Jest + RTL for components; Playwright for critical flows; each task includes failing test first |
| API error responses use RFC 9457 Problem Details | III. User Experience Consistency | ✅ Required | Next.js Route Handler errors MUST return `application/problem+json` with `type`, `title`, `status`, `detail` fields |
| Performance goals and latency budgets documented in Technical Context | IV. Performance Requirements | ✅ Documented above | Page load < 3 s; feedback < 200 ms; Management API p95 < 200 ms (consumed, not owned) |
| No raw `IConfiguration` injection; `IOptions<T>` used throughout | I. Code Quality | N/A | C#-specific gate; this is a TypeScript project. Environment variables accessed via `process.env` in server-only contexts only (never in client components) |

### Constitutional Deviation — Runtime Technology

The constitution mandates ".NET 10.0 / ASP.NET Core" as the fixed runtime. This project is a **browser-based management UI**, a project type not in scope when the constitution was ratified for backend services. Using ASP.NET Core as a frontend rendering engine would contradict the constitution's intent (Principle I: simplest approach that satisfies requirements).

**Justification**: Next.js is the appropriate technology for a React-based web UI. The ProxyManager.API and ProxyManager backend remain .NET 10.0. A MINOR constitutional amendment is recommended to exempt frontend applications from the .NET runtime mandate.

**Complexity Tracking entry**: See below.

## Project Structure

### Documentation (this feature)

```text
specs/002-proxy-route-manager/
├── plan.md              ← this file
├── research.md          ← Phase 0 output
├── data-model.md        ← Phase 1 output
├── quickstart.md        ← Phase 1 output
├── contracts/
│   └── api-routes.md   ← Phase 1 output
└── tasks.md             ← Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
src/ProxyManager.UI/                  ← New Next.js application
├── app/
│   ├── (dashboard)/
│   │   ├── layout.tsx               ← Authenticated shell with sidebar
│   │   └── routes/
│   │       ├── page.tsx             ← Route list (P1)
│   │       ├── new/
│   │       │   └── page.tsx         ← Create route form
│   │       └── [id]/
│   │           └── page.tsx         ← Route detail / edit; maintainer panel (deferred)
│   ├── api/
│   │   └── routes/
│   │       ├── route.ts             ← GET list, POST create (delegates to ProxyManager API)
│   │       └── [id]/
│   │           └── route.ts         ← GET, PUT, DELETE (delegates to ProxyManager API)
│   └── layout.tsx                   ← Root layout (fonts, providers)
├── components/
│   ├── routes/
│   │   ├── RouteList.tsx
│   │   ├── RouteForm.tsx
│   │   └── RouteCard.tsx
│   └── ui/                          ← shadcn/ui generated components
├── lib/
│   ├── auth.ts                      ← Reads X-Auth-Sub / X-Auth-Groups / Authorization headers forwarded by ProxyManager
│   ├── proxy-manager-client.ts      ← ProxyManager API fetch wrapper; uses forwarded Bearer token
│   └── modules.ts                   ← Module registry for sidebar navigation
├── middleware.ts                    ← Rejects requests missing X-Auth-Sub header (defense in depth)
├── types/
│   └── index.ts                     ← ProxyRoute, ProxyCluster, UserSession
├── .env.example
├── next.config.ts
├── package.json
└── tsconfig.json

tests/
├── unit/                            ← Jest + RTL
│   ├── components/
│   └── lib/
└── e2e/                             ← Playwright
    └── routes.spec.ts
```

**ProxyManager configuration** (changes to existing `proxysettings.{Environment}.json`):

```jsonc
// New route + cluster added for the UI upstream
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
        "primary": { "Address": "http://proxymanager-ui:3000/" }
      }
    }
  }
}
```

**Structure Decision**: Single Next.js application in `src/ProxyManager.UI/`. No `(auth)` route group needed — ProxyManager handles unauthenticated users before any request reaches the container. The `middleware.ts` header check is a lightweight defense-in-depth guard. The `proxy-manager-client.ts` wrapper is the single point of integration with the ProxyManager API — when maintainer and audit endpoints are added, new methods are added here and new pages under `(dashboard)/` without structural changes.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Next.js (not ASP.NET Core) | Browser-based management UI requires a React frontend framework | ASP.NET Core Razor Pages/Blazor would contradict the simplest-approach principle for a React UI; mixing Blazor WASM into this stack adds more complexity than Next.js |
