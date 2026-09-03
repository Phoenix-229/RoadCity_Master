# Road & City Master

Road network and city authoring for Unity. Derived from RoadArchitect (MIT).

## Status

Pre-release, under active development. See "Not in v1" before evaluating. This
is the `RoadCityMaster` package; the product name is **Road & City Master** in
prose and `RoadCityMaster` in code, folders, and assembly names.

## Requirements

- Unity 6000.4 or later — developed and verified on 6000.5.2f1. The floor is real, not
  cautious: the scripts call `PhysicsMaterial`, `FindObjectsByType`, `ContributeGI` and
  `detailResolution`, which are the Unity 6 spellings, and no older branch is kept
  behind a version guard. An editor below the floor fails to compile rather than
  degrading, and says so — see `PipelineVersionGate`.
- Universal Render Pipeline (URP) — the tool ships no Built-in RP materials
- Managed C# only; no native plug-ins, no external services

## Install

Copy the RoadCityMaster folder into your project's Assets folder. It compiles as two
assemblies, RoadCityMaster and RoadCityMaster.Editor, and adds no project-wide defines.

## Working

Each entry here is verifiable in the current tree.

- Self-test suite — `Window > Road City Master > Testing`. The headless gates
  assert the tool's rules and generation math and report `PASS`/`FAIL`; run them
  after any edit.
- Licensing and packaging tooling — `Window > Road City Master > Licensing`.
  Per-file licence-header stamping, the licence and notice files, and the
  package-export pre-flight (missing `.meta`, notice presence, undetermined
  licences) are all reachable and deterministic.

## In progress

Each entry names its task id, for example "Roundabouts — RM-28".

- The city layer over the inherited road/spline base — § 10 (RM-01 … RM-107).
- Roundabouts — RM-28.

## Not in v1

Copied verbatim from the scope list so the two cannot disagree.

- No interchanges
- No multi-lane circulatory spirals
- No rail crossings or turnouts
- No procedural buildings
- No generated wear
- No city node canvas

## Documentation

- `RoadCityMasterManual.html` — the manual: overview, procedures, the menu map, the city
  layer, the self-test suite, honest status, and licence/attribution. Open it from
  `Window > Road City Master > Open Manual`, or double-click it to read in a browser.
- `Docs/UPSTREAM-README.md` — the original RoadArchitect README, unmodified
- `UniverseCreator_Documentation.md` — integration with Universe Creator

## Licence

Upstream files remain under the MIT licence of the original project; see
[LICENSE-MIT.txt](LICENSE-MIT.txt) and [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
New work is proprietary; see [LICENSE.md](LICENSE.md), summarised in
[NOTICE.md](NOTICE.md). The vendor-baseline git tag marks the unmodified upstream
tree, so the boundary between the two is reproducible with `git diff`.
