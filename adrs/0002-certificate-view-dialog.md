# ADR 0002: Show certificate details in a dialog on the list page

**Status:** Accepted

## Context

The certificates page (`app/(dashboard)/certificates`) shows cards with the name, format, source,
file names and the "valid until" date. Users can't see the rest of a certificate's details, such as
the subject (CN), the subject alternative names, the validity start, the thumbprint and when it was
uploaded or last changed. The only other certificate screen is `/certificates/[id]`. That is the
admin edit form, and it takes the user off the list.

Users want a read-only **View** that keeps them on the list. The data is already in the browser:
`GET /manage/api/certificates` returns the full `CertificateDto` for every item (`subject`,
`subjectAlternativeNames`, `notBefore`, `notAfter`, `thumbprint`, `createdAt`, `updatedAt`,
`source`, `format` and the file names). `PassPhrase` is never included.

## Decision

- Add a **View** button to every `CertificateCard`. It shows for all users, not only admins, because
  viewing is read-only. That includes Let's Encrypt certificates, where Edit and Delete are disabled.
- Clicking it opens a `CertificateViewDialog` modal. It reuses the existing Base UI `Dialog` from
  `components/ui/dialog.tsx`, made wider than the default `sm:max-w-sm`.
- The dialog renders the `Certificate` object already held by `CertificateListClient`. It makes no
  extra fetch, and there are no backend or API changes.
- The URL doesn't change and there's no deep link.

## Alternatives

- **Expand the card inline to show the details.** This is the simplest option. It wasn't chosen:
  long SAN lists and thumbprints would make the list jumpy and hard to scan, and a dialog is already
  the pattern this page uses for delete confirmation.
- **Side sheet or drawer.** It fits long content well, but the repo has no sheet component. We'd have
  to add one only for this feature. A wide, scrollable dialog does the same job.
- **Intercepting or parallel route modal (`@modal/(.)certificates/[id]/view`).** This gives a
  shareable URL, but adds routing complexity. `/certificates/[id]` is already the edit page, so we'd
  need a second route. Nobody has asked for deep links.
- **Fetch `GET /certificates/{id}` when the dialog opens.** This isn't needed, because the list
  payload is identical to the single-item DTO.

## Consequences

- There are no API, DTO or database changes, so the feature ships as a UI-only change.
- The details are only as fresh as the last list load. That's fine, because certificates change
  rarely and the list reloads after every mutation.
- Details that aren't captured today, such as the issuer, serial number and key algorithm, can't
  be shown without backend work. That's out of scope here.
- The view isn't linkable. If deep links are needed later, a new ADR can replace this with a routed
  modal.
