# Third-Party Notices

Road & City Master incorporates third-party open-source code and open data. Each component is
listed below with its licence and where its full licence text can be read — reproduced inside this
repository where the licence requires the text to travel with the copies, and linked to its
canonical publication where the licence asks for a link instead. These components
remain governed by their own licences; the product EULA (`LICENSE.md`) does not supersede
them. See § 10.40 (RM-79/RM-80) for the licensing position this file records, and § 10.55
(RM-106) for the re-examination of that position after the upstream files were rewritten.

---

## Upstream road toolkit

- **Component:** the upstream road-generation toolkit that Road & City Master's road-building
  sources under `Scripts/` are derived from.
- **Copyright:** Copyright (c) 2018 MicroGSD LLC.
- **Licence:** MIT License.
- **Where to get the original:** anyone who wants the unmodified upstream files can download them
  from <https://github.com/FritzsHero/RoadArchitect>. The MIT licence permits that, and nothing in
  this product's own licence restricts it. What ships here is a modified derivative; the link is
  provided so the two can be told apart and compared.
- **Full licence text:** reproduced verbatim in `LICENSE-MIT.txt`, and again below. The
  copyright line naming the upstream author is part of the MIT condition and is never
  edited.
- **Scope in this product:** the 55 files that arrived in the upstream download, modified or
  not. They are enumerated by name in `UpstreamProvenanceRule` (`City/Runtime/Licensing/`)
  and each carries the MIT per-file header. The set is frozen: it is what the vendored
  download contained, and it cannot grow.
- **Not in scope:** every other file in this product, including the files authored for this
  project that live under `Scripts/` — the test gates, the registers and the cross-section
  profiles. Location does not decide this and never did; provenance does. Those files, and
  everything under `City/`, are proprietary under `LICENSE.md` and are not covered by this
  notice.
- **Re-tested after the overhaul (RM-106):** the vendored files were substantially rewritten
  for this product, and the licence position was then re-examined file by file rather than
  assumed to have changed. No file moved off MIT. Copyright reaches expression, not ideas —
  17 U.S.C. § 102(b) excludes any idea, procedure, process, system, method of operation,
  concept, principle or discovery — so replacing the arithmetic in a file does not replace
  its expression while its structure, its identifiers or its comments remain. Where a
  replacement is claimed, the claim is settled against a diff to the vendored baseline, not
  against a recollection; until that diff can be produced the notice is retained. Retaining
  it costs nothing that matters: the MIT licence does not restrict selling this product, does
  not reach the work authored for it, and obliges publishing nothing. It asks that the
  copyright line and the permission notice travel with the copies, which is what this file
  and the per-file headers do.

```text
MIT License

Copyright (c) 2018 MicroGSD LLC

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

---

## GeoNames populated-place data

- **Component:** the settlement fit table — `City/Runtime/Settlement/SettlementFitTable.cs` and any
  `SettlementFitTable` asset an author fits — whose population quantiles, nearest-neighbour spacing
  quantiles and rank-size exponents are derived from the GeoNames `cities1000` dump by
  `City/Editor/Import/GeoNamesImportWindow.cs`.
- **Copyright:** Copyright (c) GeoNames (Unxos GmbH, Switzerland).
- **Licence:** Creative Commons Attribution 4.0 International (CC BY 4.0).
- **Where to get the original:** the author downloads `cities1000.zip` from
  <https://download.geonames.org/export/dump/>; the column layout this product reads is the one
  documented in `readme.txt` in that same directory.
- **Full licence text:** published at <https://creativecommons.org/licenses/by/4.0/legalcode>. CC BY
  4.0 § 3(a)(1)(A)(iii) asks for a URI or hyperlink to the licence, which is why this entry links
  where the MIT entry above reproduces — MIT requires its text to travel with the copies, CC BY asks
  for the link.
- **Scope in this product:** the derived figures only. Per settlement order, and per timezone-region
  bucket as well as for the world: five population quantiles, the observed population range, three
  nearest-neighbour spacing quantiles, a rank-size exponent and the share of variance it explains.
  These are adapted material within the meaning of CC BY 4.0, so the asset records the dump file
  name, the UTC time of the fit and the records read and used, and the importer states in its own
  report that the two allometric exponents are NOT fitted from this source.
- **Not in scope:** the dump itself. No GeoNames file is redistributed with this product — not in the
  package, not in the repository, not in a build. The table that ships is derived from the authored
  order catalogue and says so (`SettlementFitTable.fittedFromRealDump` is false in it); it becomes a
  measurement only on the machine of an author who downloaded the dump themselves.
- **Not in scope either:** GeoNames names, identifiers and coordinates. None are stored and none are
  shipped, so no settlement this product places is a GeoNames place. What crosses over is the shape
  of four distributions, not the places that made them.
