# Tasks: Proxy Route Manager Application

**Input**: Design documents from `/specs/002-proxy-route-manager/`
**Prerequisites**: plan.md ✅, spec.md ✅, research.md ✅, data-model.md ✅, contracts/api-routes.md ✅, quickstart.md ✅

**Tests**: Included — required by plan.md constitution check (Jest + RTL failing tests before implementation; Playwright for critical flows).

**Organization**: Tasks grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies on incomplete tasks)
- **[Story]**: Which user story this task belongs to (US1, US2, US3)
- Paths are relative to the repository root

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the Next.js 15 project, tooling, and supporting configuration. No user-story work can begin until this phase is complete.

- [x] T001 Create Next.js 15 app with TypeScript and Tailwind CSS 4 in `src/ProxyManager.UI` using `npx create-next-app@latest src/ProxyManager.UI --app --ts --tailwind` (Node 24 LTS, App Router, no src directory)
- [x] T002 Initialize shadcn/ui in `src/ProxyManager.UI` using `npx shadcn-ui@latest init` and add components: Button, Input, Label, Table, Badge, Card, Dialog, DropdownMenu, Separator, Tooltip
- [x] T003 [P] Configure Jest + React Testing Library: add `jest.config.ts`, `jest.setup.ts`, and update `package.json` test scripts in `src/ProxyManager.UI` (`npm test`, `npm run test:coverage`)
- [x] T004 [P] Configure Playwright: create `src/ProxyManager.UI/playwright.config.ts` with baseURL `http://localhost:3000`, install Chromium; add `npm run test:e2e` script to `src/ProxyManager.UI/package.json`
- [x] T005 [P] Create `src/ProxyManager.UI/.env.example` with `PROXY_MANAGER_API_URL` and `ADMIN_GROUP_CLAIM` (and dev-mode overrides `DEV_AUTH_SUB`, `DEV_AUTH_GROUPS`, `DEV_AUTH_TOKEN`) per research.md §6
- [x] T006 [P] Configure `src/ProxyManager.UI/next.config.ts`: disable `x-powered-by`, set `output: 'standalone'` for container builds, expose `ADMIN_GROUP_CLAIM` as a server-only env var (never `NEXT_PUBLIC_`)
- [x] T007 Remove create-next-app boilerplate: delete default `src/ProxyManager.UI/app/page.tsx` content, clear `src/ProxyManager.UI/app/globals.css` to base Tailwind 4 `@import`, update `src/ProxyManager.UI/app/layout.tsx` root metadata

**Checkpoint**: `npm run dev` starts without errors; `npm test` runs (no tests yet); `npm run test:e2e` runs Playwright with no spec files.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented — auth header reading, the API client, module registry, and shared layouts all block every story.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [x] T008 Define TypeScript runtime types `UserSession`, `ProxyHost`, and future stubs `MaintainerAssignment`, `AuditEntry` (with JSDoc `@future` note) in `src/ProxyManager.UI/types/index.ts` — shapes must match data-model.md exactly
- [x] T009 Implement `src/ProxyManager.UI/lib/auth.ts`: export `getSession(headers: Headers): UserSession` that reads `X-Auth-Sub`, `X-Auth-Groups`, `Authorization` headers, derives `isAdmin` from `ADMIN_GROUP_CLAIM`, and in `NODE_ENV=development` synthesizes from `DEV_AUTH_*` env vars when `X-Auth-Sub` is absent
- [x] T010 Implement `src/ProxyManager.UI/middleware.ts`: use Next.js `NextResponse` to reject requests missing `X-Auth-Sub` header with `401` in production; pass through in development with missing header (dev-mode override handled by `lib/auth.ts`)
- [x] T011 Implement `src/ProxyManager.UI/lib/proxy-manager-client.ts`: thin fetch wrapper that accepts a `UserSession`, sets `Authorization: Bearer <accessToken>` on all outbound calls to `PROXY_MANAGER_API_URL`, and surfaces ProxyManager API errors as typed `ProblemDetails` objects
- [x] T012 Create `src/ProxyManager.UI/lib/modules.ts`: export `MODULE_REGISTRY: Module[]` where `Module = { label: string; href: string; icon: React.ComponentType; enabled: boolean }`. Add the Routes module entry. Sidebar must filter by `enabled: true`.
- [x] T013 Create `src/ProxyManager.UI/app/layout.tsx` root layout: Inter font, `<html lang="en">`, `<body>`, Tailwind 4 `@theme` tokens in `src/ProxyManager.UI/app/globals.css`
- [x] T014 Create `src/ProxyManager.UI/app/(dashboard)/layout.tsx`: sidebar component that iterates `MODULE_REGISTRY` (enabled only) to render navigation links; renders `{children}` in the main content area; reads `UserSession` server-side to display logged-in user identity
- [x] T015 Add YARP UI route and cluster to `src/ProxyManager/proxysettings.Development.json` and `src/ProxyManager/proxysettings.json` per plan.md — route `ui-route` matching `/manage/{**catch-all}`, cluster `ui-cluster` targeting `http://localhost:3000/` (dev) / `http://proxymanager-ui:3000/` (prod), with `X-Auth-Sub`, `X-Auth-Groups`, and `Authorization` header transforms and `AuthenticatedUsersOnly` policy

**Checkpoint**: Foundation ready — sidebar renders, auth headers are parsed, `proxy-manager-client.ts` compiles, module registry drives navigation.

---

## Phase 3: User Story 1 — View and Manage Proxy Routes (Priority: P1) 🎯 MVP

**Goal**: Administrators can list all proxy routes, create new routes, edit existing routes, and delete routes. All changes persist via the ProxyManager API.

**Independent Test**: Navigate to `/manage/routes`, verify the list loads. Create a new route, confirm it appears. Edit a field, confirm the change persists. Delete the route, confirm it disappears and is absent on reload.

### Tests for User Story 1 ⚠️ Write FIRST — confirm FAILING before implementing

- [x] T016 [P] [US1] Write failing unit tests for `lib/auth.ts` covering: header parsing, admin group detection, dev-mode override, and missing-header behavior in `src/ProxyManager.UI/tests/unit/lib/auth.test.ts`
- [x] T017 [P] [US1] Write failing unit tests for `lib/proxy-manager-client.ts` covering: successful fetch delegation, `Authorization` header forwarding, and ProxyManager API error surfacing as `ProblemDetails` in `src/ProxyManager.UI/tests/unit/lib/proxy-manager-client.test.ts`
- [x] T018 [P] [US1] Write failing component tests for `RouteCard`: renders route name, upstream URL, status badge, and action buttons; snapshot baseline in `src/ProxyManager.UI/tests/unit/components/RouteCard.test.tsx`
- [x] T019 [P] [US1] Write failing component tests for `RouteList`: renders list of `ProxyHost` items via `RouteCard`; shows empty state when array is empty; pagination controls shown when total > pageSize in `src/ProxyManager.UI/tests/unit/components/RouteList.test.tsx`
- [x] T020 [P] [US1] Write failing component tests for `RouteForm`: renders all required fields (routeId, clusterId, match.hosts, match.path); shows validation errors on empty submit; calls onSubmit with correct payload on valid input in `src/ProxyManager.UI/tests/unit/components/RouteForm.test.tsx`
- [x] T021 [P] [US1] Write failing route handler tests for `GET /api/routes` (delegates to client, returns paginated list) and `POST /api/routes` (admin-only, returns 403 Problem Details for non-admin, 201 on success) in `src/ProxyManager.UI/tests/unit/api/routes.test.ts`
- [x] T022 [P] [US1] Write failing route handler tests for `GET`, `PUT`, and `DELETE /api/routes/[id]`: 404 Problem Details on not-found, 403 Problem Details on insufficient permissions, correct success shapes (200/204) in `src/ProxyManager.UI/tests/unit/api/routes-id.test.ts`
- [x] T023 [US1] Write Playwright E2E spec covering: list loads with route rows; create new route via form; edit existing route and verify updated value; delete route and verify absence in `src/ProxyManager.UI/tests/e2e/routes.spec.ts`

### Implementation for User Story 1

- [x] T024 [P] [US1] Add `listRoutes`, `getRoute`, `createRoute`, `updateRoute`, and `deleteRoute` methods to `src/ProxyManager.UI/lib/proxy-manager-client.ts`, each accepting `UserSession` and forwarding the Bearer token; return typed `ProxyHost` or `ProblemDetails`
- [x] T025 [P] [US1] Implement BFF `GET /api/routes` (paginated list from ProxyManager API) and `POST /api/routes` (admin-only create; return 403 Problem Details if `!session.isAdmin`; forward validation errors as 422 Problem Details) in `src/ProxyManager.UI/app/api/routes/route.ts`
- [x] T026 [US1] Implement BFF `GET /api/routes/[id]`, `PUT /api/routes/[id]`, and `DELETE /api/routes/[id]` (admin-only delete; pass-through 403/404 from ProxyManager API as Problem Details) in `src/ProxyManager.UI/app/api/routes/[id]/route.ts`
- [x] T027 [P] [US1] Create `src/ProxyManager.UI/components/routes/RouteCard.tsx`: displays `name`, `upstreamUrl`, `isEnabled` status badge, `hostnames`; shows Edit and Delete buttons (Delete always rendered but may be hidden based on role — handled in T037)
- [x] T028 [P] [US1] Create `src/ProxyManager.UI/components/routes/RouteList.tsx`: renders `RouteCard` for each `ProxyHost`; shows empty state message and "Add Route" CTA when list is empty; pagination controls when `total > pageSize`
- [x] T029 [US1] Create `src/ProxyManager.UI/components/routes/RouteForm.tsx`: controlled form for `routeId` (kebab-case), `clusterId`, `match.hosts` (comma-separated), `match.path`, `transforms` (JSON textarea); client-side required field validation with inline error messages; calls `onSubmit(payload)` on valid submit; `readOnly` prop makes all fields display-only text (used in US2)
- [x] T030 [US1] Create routes list page in `src/ProxyManager.UI/app/(dashboard)/routes/page.tsx`: server component fetches `GET /api/routes` (via `fetch` with forwarded session cookie/headers), passes data to `RouteList`; handles ProxyManager API unavailability with a user-facing error banner (never silent)
- [x] T031 [US1] Create new route page in `src/ProxyManager.UI/app/(dashboard)/routes/new/page.tsx`: server component checks `session.isAdmin` (redirect to `/manage/routes` if not admin); renders `RouteForm` with `POST /api/routes` action; on success redirects to route list; on error shows Problem Details `detail` field as form-level error
- [x] T032 [US1] Create route detail/edit page in `src/ProxyManager.UI/app/(dashboard)/routes/[id]/page.tsx`: server component fetches `GET /api/routes/[id]`, renders `RouteForm` in edit mode with `PUT /api/routes/[id]` action; Delete button triggers `DELETE /api/routes/[id]` with confirmation dialog (shadcn/ui Dialog); page also hosts `MaintainerPanel` placeholder (US2 — renders nothing until T041)
- [x] T033 [US1] Add loading skeletons (shadcn/ui Skeleton) for route list and detail pages using Next.js `loading.tsx` convention in `src/ProxyManager.UI/app/(dashboard)/routes/loading.tsx` and `src/ProxyManager.UI/app/(dashboard)/routes/[id]/loading.tsx`

**Checkpoint**: User Story 1 fully functional. Admins can CRUD routes end-to-end. All unit tests pass; E2E spec passes.

---

## Phase 4: User Story 2 — Assign and Manage Route Maintainers (Priority: P2)

**Goal**: Admins can view and manage route maintainer assignments. Maintainers see edit-only access for their routes; non-maintainers see read-only fields. Maintainer CRUD BFF handlers are implemented as stubs (upstream ProxyManager API endpoints are not yet available).

**Independent Test**: Log in as admin, assign a user as maintainer for a route via the MaintainerPanel. Log in as that maintainer, navigate to the route, verify fields are editable and Delete is hidden. Navigate to a route not assigned to the maintainer and verify all fields are read-only.

**Note**: Full maintainer CRUD requires ProxyManager API to implement the deferred endpoints (see contracts/api-routes.md §Deferred Endpoints). BFF handlers are implemented now and return `501 Not Implemented` until the upstream is available.

### Tests for User Story 2 ⚠️ Write FIRST — confirm FAILING before implementing

- [x] T034 [P] [US2] Write failing component tests for role-based rendering in `RouteCard` and `RouteForm`: admin sees Delete button and all edit controls; maintainer sees edit controls but no Delete; non-maintainer sees read-only form and no action buttons in `src/ProxyManager.UI/tests/unit/components/RouteCard.test.tsx` and `RouteForm.test.tsx`
- [x] T035 [P] [US2] Write failing unit tests for maintainer BFF handlers: `GET /api/routes/[id]/maintainers` returns 501 Problem Details; `POST /api/routes/[id]/maintainers` returns 501; `DELETE /api/routes/[id]/maintainers/[userId]` returns 501; admin check applied before 501 in `src/ProxyManager.UI/tests/unit/api/routes-id-maintainers.test.ts`
- [x] T036 [P] [US2] Write failing component tests for `MaintainerPanel`: renders assigned maintainer list when data is provided; shows "Assign Maintainer" form for admin; shows nothing for non-admin; handles empty maintainer list gracefully in `src/ProxyManager.UI/tests/unit/components/MaintainerPanel.test.tsx`
- [x] T037 [US2] Write Playwright E2E spec for RBAC: verify admin sees Delete button on route list; verify non-admin (no matching group) sees read-only form on route detail; verify `POST /api/routes` returns 403 for non-admin in `src/ProxyManager.UI/tests/e2e/rbac.spec.ts`

### Implementation for User Story 2

- [x] T038 [US2] Update `src/ProxyManager.UI/components/routes/RouteCard.tsx`: accept `isAdmin: boolean` prop; hide Delete button and "Add Route" link when `!isAdmin` (admin check already derived from `UserSession` in the parent page)
- [x] T039 [US2] Update `src/ProxyManager.UI/components/routes/RouteForm.tsx`: when `readOnly: true` prop is set, render all fields as `<p>` display elements instead of `<input>`/`<textarea>`; hide submit button
- [x] T040 [US2] Update route list page `src/ProxyManager.UI/app/(dashboard)/routes/page.tsx` and route detail page `src/ProxyManager.UI/app/(dashboard)/routes/[id]/page.tsx`: pass `session.isAdmin` (and future `isMaintainer`) down to `RouteCard` and `RouteForm`; render `RouteForm` in `readOnly` mode for non-admin, non-maintainer users
- [x] T041 [US2] Implement BFF `GET /api/routes/[id]/maintainers` and `POST /api/routes/[id]/maintainers` route handlers delegating to `listMaintainers`/`assignMaintainer` in `proxy-manager-client.ts`; passes through upstream errors (501 from ProxyManager API until deferred endpoints are implemented) in `src/ProxyManager.UI/app/api/routes/[id]/maintainers/route.ts`
- [x] T042 [US2] Implement BFF `DELETE /api/routes/[id]/maintainers/[userId]` delegating to `removeMaintainer` in `proxy-manager-client.ts`; passes through upstream errors in `src/ProxyManager.UI/app/api/routes/[id]/maintainers/[userId]/route.ts`
- [x] T043 [US2] Create `src/ProxyManager.UI/components/routes/MaintainerPanel.tsx`: displays list of assigned maintainers (`MaintainerAssignment[]`); renders "Assign Maintainer" input and assign button for admin; renders "Remove" button per maintainer for admin; renders nothing when data is `null` (deferred API); handles 501 stub gracefully by showing "Maintainer management coming soon" callout
- [x] T044 [US2] Integrate `MaintainerPanel` into `src/ProxyManager.UI/app/(dashboard)/routes/[id]/page.tsx`: attempt `GET /api/routes/[id]/maintainers`; render panel with data on success or with `null` (stub message) on 501

**Checkpoint**: Role enforcement is live — admins see full controls, others see appropriate read-only views. Maintainer panel renders stub message. All unit and E2E tests for US2 pass.

---

## Phase 5: User Story 3 — Modular Feature Integration (Priority: P3)

**Goal**: The module registry pattern is verified end-to-end. A developer can add a new module (page + registry entry) without touching existing route management or maintainer code.

**Independent Test**: Add a new entry to `lib/modules.ts` and a corresponding page. Verify it appears in the sidebar. Verify existing route management pages work identically. Remove the module registry entry and verify the nav entry disappears.

### Tests for User Story 3 ⚠️ Write FIRST — confirm FAILING before implementing

- [ ] T045 [P] [US3] Write failing unit tests for `lib/modules.ts`: registry export is an array; only `enabled: true` entries render in navigation; adding a new entry does not mutate existing entries in `src/ProxyManager.UI/tests/unit/lib/modules.test.ts`
- [ ] T046 [US3] Write Playwright E2E spec: add a second module (Health Checks placeholder) to the registry; verify it appears in sidebar alongside Routes; navigate to it; navigate back to Routes; verify Routes page behaves identically in `src/ProxyManager.UI/tests/e2e/modules.spec.ts`

### Implementation for User Story 3

- [ ] T047 [P] [US3] Add a `HealthChecks` placeholder module entry (`enabled: true`) to `src/ProxyManager.UI/lib/modules.ts` with appropriate label, href `/health-checks`, and icon
- [ ] T048 [P] [US3] Create `src/ProxyManager.UI/app/(dashboard)/health-checks/page.tsx`: minimal page rendering "Health Checks — coming soon" content; demonstrates that a new module requires only a page + registry entry with zero changes to existing modules
- [ ] T049 [US3] Verify sidebar in `src/ProxyManager.UI/app/(dashboard)/layout.tsx` dynamically reflects `MODULE_REGISTRY` (no hardcoded nav links); set `enabled: false` on Health Checks entry and confirm it disappears from sidebar without code changes elsewhere

**Checkpoint**: Module system verified end-to-end. New modules add/remove cleanly via registry. Existing route and maintainer functionality unaffected.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Container image, deployment config, coverage validation, and quickstart verification.

- [ ] T050 [P] Create `src/ProxyManager.UI/Containerfile` using Node 24 Alpine multi-stage build (deps → builder → runner); `output: 'standalone'` enabled in `next.config.ts` (T006); run as non-root `nextjs` user; expose port 3000
- [ ] T051 [P] Create `systemd/proxymanager-ui.container` Podman Quadlet unit file following the pattern of existing containers; attach to `proxymanager.network`; pass `PROXY_MANAGER_API_URL` and `ADMIN_GROUP_CLAIM` env vars; do NOT publish port 3000 externally
- [ ] T052 Run `npm run test:coverage` in `src/ProxyManager.UI` and confirm overall coverage ≥ 80%; address any gaps in unit or component coverage
- [ ] T053 Validate all steps in `specs/002-proxy-route-manager/quickstart.md` work from a clean checkout: install, configure `.env.local`, configure proxysettings, start dev server, run unit tests, run E2E tests
- [ ] T054 Update `CLAUDE.md` `Active Technologies` section to reflect finalized stack: remove draft entries for `002-proxy-route-manager`, add confirmed: `TypeScript / Node.js 24 LTS + Next.js 15 (App Router), shadcn/ui, Tailwind CSS 4, Jest + RTL, Playwright`

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 completion — **BLOCKS all user stories**
- **User Story 1 (Phase 3)**: Depends on Phase 2 — **MVP scope**
- **User Story 2 (Phase 4)**: Depends on Phase 2; integrates with Phase 3 components
- **User Story 3 (Phase 5)**: Depends on Phase 2; independent of Phase 3 and 4
- **Polish (Phase 6)**: Depends on all desired user story phases complete

### User Story Dependencies

- **US1 (P1)**: No dependency on US2 or US3 — implement first
- **US2 (P2)**: Extends US1 components (`RouteCard`, `RouteForm`, route detail page) — requires US1 complete
- **US3 (P3)**: Independent of US1 and US2 — can be done in parallel with US2

### Within Each User Story

- Tests MUST be written and **confirmed failing** before implementation begins
- Types/interfaces before services
- Services (`proxy-manager-client.ts`) before BFF route handlers
- BFF route handlers before page components
- Page components before E2E tests can pass

### Parallel Opportunities

- Phase 1: T003, T004, T005, T006 can all run in parallel after T001
- Phase 2: T008–T014 can all run in parallel (different files); T015 is independent
- Phase 3 tests (T016–T022): all [P], launch together before any implementation
- Phase 3 implementation: T024 and T025 can run in parallel; T026 depends on T024; T027 and T028 are parallel; T029 depends on T027/T028
- Phase 4 tests (T034–T037): all [P]
- Phase 5: T047 and T048 can run in parallel after T045

---

## Parallel Example: User Story 1

```bash
# Step 1: Write all tests in parallel (must fail before proceeding)
Task T016: auth.ts unit tests
Task T017: proxy-manager-client.ts unit tests
Task T018: RouteCard component tests
Task T019: RouteList component tests
Task T020: RouteForm component tests
Task T021: /api/routes handler tests
Task T022: /api/routes/[id] handler tests

# Step 2: Run tests — confirm ALL FAIL

# Step 3: Implement foundation (parallel)
Task T024: proxy-manager-client.ts CRUD methods
Task T025: BFF GET/POST /api/routes handler
Task T027: RouteCard component
Task T028: RouteList component

# Step 4: Implement dependent items (sequential)
Task T026: BFF GET/PUT/DELETE /api/routes/[id]  ← after T024
Task T029: RouteForm component                   ← after T027, T028
Task T030: Routes list page                      ← after T025, T028
Task T031: New route page                        ← after T025, T029
Task T032: Route detail page                     ← after T026, T029
Task T033: Loading skeletons                     ← after T030-T032
Task T023: E2E spec                              ← after T030-T032 pass
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (**CRITICAL** — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run `npm test` and `npm run test:e2e`; manually test CRUD flow via ProxyManager
5. Deploy or demo if ready

### Incremental Delivery

1. Phase 1 + Phase 2 → Foundation ready
2. Phase 3 (US1) → CRUD routes → **Demo (MVP!)**
3. Phase 4 (US2) → Role enforcement + maintainer panel → **Demo**
4. Phase 5 (US3) → Module system validated → **Demo**
5. Phase 6 → Production-ready container and deployment

### Parallel Team Strategy

With multiple developers (after Phase 2 complete):

- **Developer A**: User Story 1 (routes CRUD)
- **Developer B**: User Story 3 (module system — independent of US1/US2)
- **Developer C**: User Story 2 begins after US1 components exist

---

## Notes

- `[P]` tasks operate on different files with no dependencies on incomplete tasks
- `[Story]` label maps each task to a specific user story for traceability
- Each story is independently completable and testable
- Tests must fail before implementation — enforced by plan.md constitution check
- Commit after each task or logical group
- Maintainer CRUD (US2) is scaffolded now; full functionality activates when ProxyManager API implements the deferred endpoints (contracts/api-routes.md §Deferred Endpoints)
- Environment variables (`PROXY_MANAGER_API_URL`, `ADMIN_GROUP_CLAIM`) are server-only — never exposed as `NEXT_PUBLIC_`
- Dev-mode header injection (`DEV_AUTH_*` env vars) is compiled out in production builds via `NODE_ENV` check in `middleware.ts` and `lib/auth.ts`
