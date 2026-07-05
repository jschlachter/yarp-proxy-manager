---
name: upgrade-dependencies
description: Check for and apply outdated NuGet package upgrades across the ProxyManager solution using dotnet-outdated. Use when asked to upgrade dependencies, update NuGet packages, check for outdated packages, or bump package versions.
---

Upgrades NuGet dependencies for `ProxyManager.sln` using the `dotnet-outdated` global tool
(`dotnet-outdated-tool`, invoked as `dotnet outdated`). Driven via
`.claude/skills/upgrade-dependencies/driver.sh`, which wraps report/upgrade/verify into three
commands so major-version bumps (breaking changes) are never applied silently.

All paths below are relative to the repo root.

## Prerequisites

`dotnet-outdated-tool` must be installed as a global tool:

```bash
dotnet tool install --global dotnet-outdated-tool
```

Verify it's present:

```bash
dotnet tool list -g | grep dotnet-outdated
```

## Run (agent path)

```bash
# 1. See what's outdated (no changes made)
.claude/skills/upgrade-dependencies/driver.sh report

# 2. Apply minor/patch upgrades only (default — major bumps are skipped)
.claude/skills/upgrade-dependencies/driver.sh upgrade

# 2b. Include major-version bumps too (review the diff carefully afterward)
.claude/skills/upgrade-dependencies/driver.sh upgrade --majors

# 3. Confirm the solution still builds and tests pass
.claude/skills/upgrade-dependencies/driver.sh verify
```

`report` runs `dotnet outdated ProxyManager.sln` and lists every outdated package per project,
color-coded by semver impact (patch/minor/major), without touching any `.csproj`.

`upgrade` (no flag) runs `dotnet outdated ProxyManager.sln -vl Major -u` — this locks each
package to its current major version, so only minor/patch upgrades are auto-applied. Packages
with only a major bump available (e.g. a `5.x -> 6.x` jump) are left untouched and still show up
in the next `report`.

`upgrade --majors` runs `dotnet outdated ProxyManager.sln -u` with no lock, applying every
upgrade including majors. Only use this when you intend to review the resulting diff and update
any breaking-change usages — see Gotchas.

`verify` runs `dotnet build ProxyManager.sln` followed by
`dotnet test ProxyManager.sln --filter "Category=Unit"` — unit tests only. Integration tests
(under each project's `Integration/` folder) are excluded because they depend on Testcontainers
spinning up real PostgreSQL containers, which are currently broken/flaky in this environment and
tracked as a separate fix; a dependency upgrade shouldn't be blocked on them.

After `upgrade`, inspect the diff before committing:

```bash
git diff --stat
git diff -- '*.csproj'
```

If `verify` fails, either fix the breakage or revert the specific package bump:

```bash
git checkout -- <path-to-csproj>
```

## Test

```bash
.claude/skills/upgrade-dependencies/driver.sh verify
```

Expected: `dotnet build` succeeds with 0 errors, then `dotnet test --filter "Category=Unit"` runs
only the `[Trait("Category", "Unit")]`-tagged tests across `tests/ProxyManager.API.Tests` and
`tests/ProxyManager.Core.Tests` (68 + 14 tests as of this writing) — all pass in under a second.
Integration tests are not run by `verify`.

## Gotchas

- **`WolverineFx` and `WolverineFx.RabbitMQ` are pinned behind a major bump** (`5.22.0 -> 6.16.0`
  as of this writing) — `upgrade` (default) correctly skips them. Wolverine major versions
  routinely carry breaking API changes; treat this one as a separate, deliberate upgrade task,
  not something to fold into a routine dependency bump.
- **`coverlet.collector` also has a major bump pending** (`8.0.1 -> 10.0.1`) and is skipped by
  default for the same reason — verify coverage collection still works if you take it with
  `--majors`.
- **`-vl Major` means "lock the major version" (i.e. forbid major bumps), not "only apply major
  bumps"** — the flag name reads backwards at first glance. Confirmed by testing: with `-vl Major`,
  WolverineFx 5.x and coverlet 8.x were left alone while everything else moved to its latest
  minor/patch.
- **`dotnet outdated` mutates `.csproj` files directly and immediately** — there's no dry-run
  upgrade mode short of `report` (which makes no changes) vs `upgrade` (which does). Always run
  `report` first and check `git diff` after `upgrade` before running `verify`/committing.
- **`ProxyManager.API.Tests` integration tests are currently broken/excluded on purpose** — they
  spin up Testcontainers (PostgreSQL) and fail with `Docker API responded with
  status code='InternalServerError' ... Disk quota exceeded` (or `bind: address already in use`) in
  this environment. That's why `verify` filters to `Category=Unit` instead of running the whole
  suite — don't widen the filter back out until the Testcontainers/Docker issue is fixed
  separately. If you do run the full suite manually and see these specific Docker errors, that's
  the known issue, not something a dependency upgrade broke.
