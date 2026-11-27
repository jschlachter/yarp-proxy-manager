# Security Policy

YARP Proxy Manager is an internet-facing reverse proxy that terminates TLS, holds OIDC sessions, and stores private keys for certificates. Please treat security issues accordingly.

## Reporting a vulnerability

**Don't open a public issue, discussion, or pull request for a security problem.**

Report it privately through GitHub's [private vulnerability reporting](https://github.com/jschlachter/yarp-proxy-manager/security/advisories/new) (Security tab → "Report a vulnerability").

Please include:

- The component affected (proxy, management API, files service, UI, deployment units/scripts)
- The version (image tag or commit SHA)
- Steps to reproduce or a proof of concept
- The impact as you understand it

You can expect an acknowledgement within 7 days. Once the issue is confirmed, a fix is prepared in a private advisory and the advisory is published with the release that fixes it. Reporters are credited unless they ask not to be.

## Supported versions

Only the latest release on `main` receives security fixes.

## Scope

In scope:

- Authentication or authorization bypass (OIDC cookie session, JWT Bearer, `X-Files-Service-Token`, the management-host restriction)
- Exposure of private keys, certificates, tokens, or secrets
- Request smuggling, header injection, or routing flaws in the proxy
- Injection, SSRF, or path traversal in the API, files service, or UI
- Insecure defaults in the shipped Quadlet units or deployment scripts

Out of scope:

- Vulnerabilities in a backend service that YARP Proxy Manager proxies to
- Issues that require an already-compromised host or admin account
- Misconfiguration of your own OIDC provider
- The PostgreSQL, RabbitMQ, and RustFS ports published by the lab Quadlet units (documented in `systemd/TODO.md`); do not expose them publicly
