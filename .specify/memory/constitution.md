<!--
Sync Impact Report
==================
Version change: N/A (initial) → 1.0.0
Modified principles: N/A — initial ratification
Added sections:
  - Core Principles (4 principles as requested):
      I. Code Quality
      II. Testing Standards
      III. User Experience Consistency
      IV. Performance Requirements
  - Technology & Architecture Standards
  - Development Workflow
  - Governance
Removed sections: N/A — initial document
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ — Constitution Check gates documented in Governance; plan template already has gate section
  - .specify/templates/spec-template.md ✅ — Success Criteria and Requirements sections align with UX/Performance principles
  - .specify/templates/tasks-template.md ✅ — Phase structure and test-first ordering supports Principles II and I
Deferred TODOs: None
-->

# YARP Proxy Manager Constitution

## Core Principles

### I. Code Quality

All code MUST adhere to Microsoft's C# coding conventions and the .NET Runtime coding style
as documented in CLAUDE.md. Complexity MUST be justified; prefer the simplest approach that
satisfies requirements (YAGNI).

- All public APIs MUST include XML documentation comments.
- Methods exceeding 40 lines MUST be refactored unless they constitute a single algorithmic unit.
- Dependencies MUST be injected via constructor; `new` for infrastructure objects is forbidden
  in domain and service code.
- `async void` is forbidden; use `async Task`.
- Raw `IConfiguration` injection into services is forbidden; use `IOptions<T>`.
- File-scoped namespaces and primary constructors MUST be used where applicable (.NET 8+).

**Rationale**: Consistent, readable code reduces defects, accelerates onboarding, and keeps
maintenance cost predictable as the proxy surface grows.

### II. Testing Standards

Every feature MUST be covered by automated tests before merging to `main`. Tests MUST be
written before implementation (Red-Green-Refactor); PRs with failing or absent tests MUST NOT
be merged.

- **Unit tests**: MUST cover all domain logic and service methods in `ProxyManager.Core`.
- **Integration tests**: MUST cover each API endpoint and each YARP route configuration change.
- **Contract tests**: MUST cover RabbitMQ message schemas and inter-service API contracts.
- Line coverage for new code MUST reach ≥ 80%, measured via
  `dotnet test --collect:"XPlat Code Coverage"`.

**Rationale**: The proxy is critical infrastructure; regressions in routing or authentication
silently break all downstream services. Tests are the only reliable quality gate.

### III. User Experience Consistency

All UI interactions and API responses MUST follow consistent, predictable patterns to reduce
operator error under production pressure.

- API errors MUST use RFC 9457 Problem Details (`application/problem+json`).
- All list endpoints MUST support pagination; unbounded result sets are forbidden.
- UI state transitions MUST provide loading indicators; silent blocking calls are forbidden.
- Authentication failures MUST redirect cleanly through the OIDC flow; raw 401 responses
  MUST NOT be surfaced directly to browser users.
- All management actions (create, update, delete route) MUST provide explicit success or
  failure feedback within 3 seconds.

**Rationale**: Operators manage live proxy routes under time pressure. Consistent, predictable
UX prevents misconfiguration that would affect all proxied services.

### IV. Performance Requirements

The proxy and management API MUST meet the following baselines at all times:

- YARP routing added overhead MUST remain < 5 ms p99 for proxied requests.
- Management API endpoints MUST respond within 200 ms p95 under normal operating load.
- ProxyManager startup time (including TLS certificate loading) MUST NOT exceed 10 seconds.
- Memory growth under sustained load MUST remain flat; no measurable leak over a 24-hour run.

Performance regressions detected via benchmarks or load tests MUST be resolved before any
affected code is merged.

**Rationale**: The proxy sits on the critical path for every user request to every downstream
service. Latency and memory regressions compound at scale.

## Technology & Architecture Standards

The following technology choices are fixed and MUST NOT be replaced without a major
constitutional amendment (version bump to next MAJOR):

- **Runtime**: .NET 10.0 / ASP.NET Core
- **Reverse Proxy**: YARP (`Yarp.ReverseProxy` 2.x)
- **Logging**: Serilog — console + rolling daily file, 10 MB limit, 30-file retention
- **Authentication**: Authentik OIDC (cookie sessions in ProxyManager; JWT Bearer in
  ProxyManager.API)
- **Messaging**: RabbitMQ for pub/sub inter-service communication
- **Deployment**: Podman Quadlet (systemd-managed containers)
- **Database**: PostgreSQL (accessed via `ProxyManager.Infrastructure`)

New libraries MUST be justified in the PR description with benchmark or gap analysis evidence.
Duplicating functionality already provided by the stack above is forbidden.

## Development Workflow

All feature development MUST follow the speckit workflow in order:

1. `/speckit.specify` — create or update the feature specification.
2. `/speckit.plan` — produce the implementation plan; Constitution Check gates MUST pass.
3. `/speckit.tasks` — generate dependency-ordered task list.
4. `/speckit.implement` — execute tasks; each task MUST be committed individually.

**Constitution Check gates** (required in every `plan.md` before Phase 0 research):

| Gate | Principle |
|------|-----------|
| Tests written and confirmed failing before implementation begins | II. Testing Standards |
| API error responses use RFC 9457 Problem Details | III. User Experience Consistency |
| Performance goals and latency budgets documented in Technical Context | IV. Performance Requirements |
| No raw `IConfiguration` injection; `IOptions<T>` used throughout | I. Code Quality |

PRs MUST NOT merge until all applicable gates are verified. Code reviews MUST explicitly
address each gate.

## Governance

This constitution supersedes all other project practices and documentation. In the event of a
conflict, the constitution takes precedence.

**Amendment procedure**:

1. Open a PR with the proposed changes to `.specify/memory/constitution.md`.
2. Describe the motivation, impact on existing features, and any required migration plan.
3. Bump `CONSTITUTION_VERSION` according to semantic versioning:
   - **MAJOR**: Backward-incompatible removal or redefinition of a principle.
   - **MINOR**: New principle or section added, or materially expanded guidance.
   - **PATCH**: Clarifications, wording improvements, typo fixes.
4. Set `LAST_AMENDED_DATE` to the PR merge date (ISO format YYYY-MM-DD).
5. Propagate changes to dependent templates and update the Sync Impact Report.

**Compliance review**: Every PR description MUST include a "Constitution Check" section
confirming compliance with all four principles. Reviewers MUST reject PRs that omit this
section or leave gates unverified.

**Versioning policy**: MAJOR bumps require team consensus. MINOR and PATCH bumps require at
least one additional reviewer beyond the author.

**Version**: 1.0.0 | **Ratified**: 2026-03-21 | **Last Amended**: 2026-03-21
