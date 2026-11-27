<!--
PR title must follow Conventional Commits; it becomes the squash commit on main.
  <type>(<scope>): <Imperative summary>
  e.g. feat(ui): Add certificate expiry badge
Types: feat, fix, docs, refactor, perf, test, build, ci, chore, revert
Scopes: proxy, api, files, ui, core, infra, deploy, deps, ci, docs
Add ! after the scope for breaking changes, e.g. feat(api)!: ...
-->

## Summary

<!-- What does this change and why? -->

Closes #

## Type of change

- [ ] Bug fix (non-breaking change that fixes an issue)
- [ ] New feature (non-breaking change that adds functionality)
- [ ] Breaking change (changes config keys, env vars, API contracts, database schema in an incompatible way, or Quadlet units)
- [ ] Refactor or performance (no behaviour change)
- [ ] Documentation
- [ ] Build, CI, or dependencies

## Related ADR

<!-- Link the ADR under adrs/ if this change implements one, or "n/a". -->

## How was this tested?

<!-- Unit/integration tests, manual steps, screenshots for UI changes. -->

## Checklist

- [ ] New or changed behaviour has tests tagged `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]` (Jest/Playwright for the UI)
- [ ] `dotnet build ProxyManager.slnx` and the unit tests pass locally (`npm test` for UI changes)
- [ ] An EF Core migration is included if the schema changed (`ProxyManager.Infrastructure` or `ProxyManager.Files`)
- [ ] New or changed config, env vars, or Quadlet units are documented (README, `systemd/proxymanager.env.example`)
- [ ] No secrets, credentials, or site-specific hostnames are committed
- [ ] For breaking changes: the PR description explains the migration path
