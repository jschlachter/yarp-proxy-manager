# Contributing to YARP Proxy Manager

Thanks for your interest in contributing. This guide covers how to propose changes and what a pull request needs to be merged.

## Before you start

- **Bugs:** open an issue with the bug report form. Search existing issues first.
- **Features and larger changes:** open a feature request and agree on the problem before writing code. If the change involves a real design decision, it's recorded as an ADR under [`adrs/`](adrs/) (see [Architecture decisions](#architecture-decisions)).
- **Small fixes** (typos, docs, obvious bugs): go ahead and open a pull request directly.
- **Security issues:** don't open a public issue. See [SECURITY.md](SECURITY.md).
- **Questions:** use [GitHub Discussions](https://github.com/jschlachter/yarp-proxy-manager/discussions).

## Development setup

The [README](README.md#getting-started) covers prerequisites, running the services from source, and local TLS. In short:

```bash
dotnet build ProxyManager.slnx
dotnet test ProxyManager.slnx -- --filter-trait "Category=Unit"   # what CI runs

cd src/ProxyManager.UI
npm ci
npm test
```

Integration tests (`Category=Integration`) start PostgreSQL with Testcontainers and need a running Podman or Docker engine.

The default configuration uses the maintainer's hostnames (`proxy-manager.west94.io`, `auth.west94.io`). Override `Management:Hosts` and the `Authentication` settings with your own values in user secrets or environment variables; don't commit them.

## Making changes

1. Fork the repository and create a branch from `main` (e.g. `feat/certificate-expiry-badge`, `fix/route-reload`).
2. Keep each pull request focused on one change.
3. Add or update tests for any behaviour you change.
4. Update the docs (README, `systemd/proxymanager.env.example`) when you add or change configuration, environment variables, or Quadlet units.
5. Open a pull request and fill in the template.

### Code conventions

- **C#** (`src/**/*.cs`): file-scoped namespaces, primary constructors, `IServiceCollection` extension methods to keep `Program.cs` short, the Options pattern instead of injecting `IConfiguration`, and `TypedResults` in endpoints. The full rules are in [`.claude/rules/csharp-rules.md`](.claude/rules/csharp-rules.md). `.editorconfig` covers formatting.
- **Tests:** xunit v3 on Microsoft.Testing.Platform. Tag every test class with `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]`.
- **UI:** Next.js 16 App Router; see [`.claude/rules/nextjs.rules.md`](.claude/rules/nextjs.rules.md) and `src/ProxyManager.UI/AGENTS.md`.
- **Database:** schema changes need an EF Core migration:
  ```bash
  dotnet ef migrations add <Name> --project src/ProxyManager.Infrastructure   # or src/ProxyManager.Files
  ```
- **Secrets:** never commit secrets, credentials, or site-specific hostnames. Deployment secrets are Podman secrets; local ones go in user secrets or environment variables.

## Commit and pull request titles

Pull requests are **squash-merged**, and the PR title becomes the commit message on `main`. Commits on your branch can be anything, but the **PR title** must follow [Conventional Commits](https://www.conventionalcommits.org/). A CI check enforces this.

```
<type>(<scope>): <Summary in the imperative mood>
```

Examples:

```
feat(ui): Add certificate expiry badge
fix(proxy): Reload database routes after a RabbitMQ reconnect
docs: Explain the management host setting
feat(api)!: Rename the proxyhosts health check fields
```

**Types**

| Type | Use for |
|---|---|
| `feat` | A new feature |
| `fix` | A bug fix |
| `docs` | Documentation only |
| `refactor` | Code change that neither fixes a bug nor adds a feature |
| `perf` | Performance improvement |
| `test` | Adding or fixing tests |
| `build` | Build system, container images, project files |
| `ci` | GitHub Actions workflows |
| `chore` | Maintenance that doesn't fit elsewhere |
| `revert` | Reverting an earlier commit |

**Scopes** (optional, but please use one when the change is confined to one area)

| Scope | Area |
|---|---|
| `proxy` | `src/ProxyManager` (YARP, TLS, OIDC sign-in) |
| `api` | `src/ProxyManager.API` |
| `files` | `src/ProxyManager.Files` |
| `ui` | `src/ProxyManager.UI` |
| `core` | `src/ProxyManager.Core` |
| `infra` | `src/ProxyManager.Infrastructure` (EF Core, migrations) |
| `deploy` | `systemd/`, `scripts/`, Containerfiles |
| `deps` | Dependency updates |
| `ci` | Workflows |
| `docs` | Documentation |

**Subject:** start with a capital letter, use the imperative mood ("Add", not "Added"), no trailing period. If the change implements an ADR, add it at the end: `feat: Add proxy host health checks (ADR 0003)`.

**Breaking changes:** add `!` after the scope and explain the migration in the PR description. Treat a change as breaking if it renames or removes a configuration key or environment variable, changes an API contract, needs manual data migration, or changes the Quadlet units or secrets an existing deployment relies on.

## Architecture decisions

Significant design decisions are recorded as ADRs in [`adrs/`](adrs/), numbered sequentially (`NNNN-short-title.md`), often with a companion `NNNN-short-title.plan.md` implementation plan. Read the existing ones for the format. Open the ADR as its own pull request, or as the first commit of the implementing pull request, so the decision can be discussed before the code is reviewed.

## Review

The maintainer reviews every pull request. CI must pass (build, unit tests, PR title). Expect review comments; push follow-up commits to the same branch rather than force-pushing, which makes re-review easier.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
