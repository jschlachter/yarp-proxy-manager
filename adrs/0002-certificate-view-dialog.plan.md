# Plan: Certificate view dialog (ADR 0002)

**Status:** Implemented

## Goal

On `/certificates`, every card has a **View** button for all users. It opens a read-only modal on
top of the list with these fields:

- Name, with badges for format (PEM/PFX), source (Manual/Let's Encrypt) and status
  (Valid / Expires soon / Critical / Expired)
- Common name (the CN parsed from `subject`) and the full subject DN
- Subject alternative names, as a list (or "None")
- Valid from (`notBefore`) and expires (`notAfter`), with a relative "in N days" or "N days ago"
- Uploaded (`createdAt`) and last updated (`updatedAt`)
- SHA-1 thumbprint in monospace, with a copy button
- Certificate file name, and the key file name when there is one

Closing the dialog (with Esc, the close button or a click on the backdrop) returns the user to the
list in the same state.

**Out of scope:** backend, DTO or API changes; new certificate fields (issuer, serial, key
algorithm); deep links or URL changes; editing from the dialog; and changes to the
`/certificates/[id]` edit page.

## Tasks

### 1. Formatting helpers — [x] done

- Add `lib/certificates.ts` with these pure helpers:
  - `getCommonName(subject: string): string | undefined`. Parses `CN=` out of the .NET
    `X500DistinguishedName` string, for example `CN=example.com, O=Acme`, and handles a quoted value
    that contains a comma.
  - `getExpiryStatus(notAfter: string, now = Date.now()): "valid" | "expiring" | "critical" | "expired"`.
    "critical" means 7 days or fewer and "expiring" means 30 days or fewer. The more severe status
    wins.
  - `formatRelativeDays(date: string, now = Date.now()): string`. Uses `Intl.RelativeTimeFormat`.
- Switch `CertificateCard`'s inline `expired` check to `getExpiryStatus` so the two screens agree.

**Verify:** add `tests/unit/lib/certificates.test.ts` covering:
- A CN in first, middle and last position.
- No CN.
- A quoted CN with a comma.
- The expiry boundaries: 31 days, 30 days, 8 days, 7 days and one second past.

### 2. `CertificateViewDialog` component — [x] done

- Add `components/certificates/CertificateViewDialog.tsx` with the props
  `{ certificate: Certificate | undefined; onOpenChange: (open: boolean) => void }`. It is open when
  `certificate` is set.
- Build it from `Dialog`, `DialogContent`, `DialogHeader`, `DialogTitle` and `DialogDescription` in
  `components/ui/dialog.tsx`. Pass `className="sm:max-w-lg max-h-[85vh] overflow-y-auto"` to
  `DialogContent`.
- Lay out the body as a `<dl>` definition list grouped into three sections: Identity (CN, subject,
  SANs), Validity (from, expires, status) and Files & metadata (files, thumbprint, uploaded,
  updated). Use `Separator` between the sections.
- Render SANs as `font-mono` chips, matching the file-name chips in `CertificateCard`.
- Format dates with `toLocaleString()` and put the ISO value in `title`.
- The copy button calls `navigator.clipboard.writeText(thumbprint)` and shows "Copied" for about 2 s.
- Follow the existing dark and vibrant styling: the status badge colors match the card (emerald,
  amber, orange and destructive). The Expires row also shows the relative "in N days" text, so
  a user can see how urgent it is without working out the dates.

**Verify:** add `tests/unit/components/CertificateViewDialog.test.tsx` checking:
- It renders the CN, every SAN, both validity dates, the created date and the thumbprint for a
  PEM fixture with a key file.
- A PFX fixture doesn't render a key-file row.
- An expired fixture shows "Expired", and a fixture expiring in 5 days shows the critical badge.
- Clicking copy calls `navigator.clipboard.writeText` with the thumbprint (mocked).
- Nothing renders when `certificate` is `undefined`.

### 3. Wire View into the card and list — [x] done

- `CertificateCard`: add an `onView(id)` prop and a `View` button (`variant="outline"`, `size="sm"`,
  `aria-label="View"`). Move the action container out of the `isAdmin` guard so View always renders.
  Edit and Delete stay admin-only, with their current Let's Encrypt handling.
- `CertificateList`: pass `onView` through.
- `CertificateListClient`: add `const [viewId, setViewId] = useState<string | null>(null)`, derive the
  certificate from `certificates`, and render `<CertificateViewDialog>` next to the delete dialog.

**Verify:**
- Extend `tests/unit/components/CertificateCard.test.tsx`:
  - View shows for `isAdmin=false` and `isAdmin=true`.
  - View is enabled for Let's Encrypt certificates.
  - Clicking View calls `onView` with the id.
  - Non-admins still don't see Edit or Delete.
- Run `npm test`.

### 4. End-to-end check — [x] done

- In `tests/e2e/certificates.spec.ts`, extend the existing upload, list and delete test. After the
  upload, click **View** on the new card, assert that the dialog shows the CN and the expiry date,
  press Esc, and assert that the URL is still `/certificates` and the list is visible.

**Verify:**
- Run `npm run test:e2e`.
- Manually, with agent-browser against `npm run dev`: open the dialog for a Let's Encrypt
  certificate and for a manual certificate with many SANs. Confirm that it scrolls inside the
  viewport, reads well in the dark theme, and works at a phone width of 375 px.

## Decisions

- **Expiring-soon threshold:** 30 days, which matches when Let's Encrypt renews.
- **Critical threshold:** 7 days or fewer. This shows as an orange "Critical" badge.
- **Issuer:** out of scope. It isn't stored today, and adding it needs its own ADR.

## Implementation notes

- **Initial focus:** the dialog passes `initialFocus` to its own popup. Base UI's default focuses the
  first tabbable element (the thumbprint Copy button), which scrolled long SAN lists to the bottom on
  open.
- **E2E delete confirm:** the existing `button:text('Delete'):visible` confirm click matched the first
  visible Delete on the page, a disabled card button whenever a Let's Encrypt certificate exists. It
  is now scoped to the dialog.
- **E2E fixture:** `tests/e2e/fixtures/test-cert.pem` is gitignored (`*.pem`). The spec header now
  gives the `openssl` command that generates it (CN=localhost, which the View step asserts).
- **How it was verified:** the full stack (YARP proxy + Authentik, API, Files, Postgres, RustFS)
  wasn't available, so `npm run test:e2e` and the manual check ran against `next dev` behind a
  scratch stub. The stub served the API and Files endpoints, parsed uploaded PEMs with Node's
  `X509Certificate`, and stripped the `/manage` prefix as YARP does. `agent-browser` isn't installed,
  so the manual check used Playwright screenshots in dark mode at 1280 px and 375 px.

- **Review:** one finding was accepted. The dialog now keeps rendering the last certificate it showed
  after it closes, so the close fade and zoom play like the delete dialog's instead of snapping shut.
