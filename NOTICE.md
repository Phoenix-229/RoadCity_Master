# Road & City Master — Notice

Road & City Master is a commercial product built on open-source foundations.

- **The product as a whole** is proprietary and is governed by `LICENSE.md` (the End User
  Licence Agreement).
- **The upstream road toolkit** it is derived from is MIT licensed. Its verbatim licence
  text is in `LICENSE-MIT.txt`, and every third-party component is itemised in
  `THIRD-PARTY-NOTICES.md`.
- **The boundary is legible per file.** Every source file carries one of two headers: a
  proprietary header (files authored for this project, under `City/`) or an MIT header
  (upstream files, modified or not). See § 10.40 for the rule that decides which.

## What you may and may not do

You may use Road & City Master to build and ship your own games and applications,
including modifying it for that purpose. You may not redistribute or resell the product
itself — in source or compiled form — as a standalone product or as part of a competing
tool. The MIT-licensed upstream files retain the rights their licence grants; nothing in
`LICENSE.md` restricts those.

## Vendor baseline

The provenance of the upstream files is anchored to the unmodified vendor tree recorded at
the `vendor-baseline` tag (see § 10.39 / RM-78). This is the evidence of what arrived under
MIT versus what was authored for this project.

- **vendor-baseline commit:** `<pending — recorded when the repository is bootstrapped; RM-78 git writes are on hold>`

> The commit hash above is intentionally unfilled: creating the repository, the baseline
> commit, and the annotated `vendor-baseline` tag are git-write operations that are held
> pending explicit authorisation (RM-78). The baseline must be taken from the pristine
> upstream copy, not from the current working tree, which has already been modified across
> § 10. When the tag is created, its commit hash is recorded here and in `CHANGELOG.md`.
