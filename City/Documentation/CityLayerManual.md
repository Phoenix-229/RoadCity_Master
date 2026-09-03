# The City Layer — Road & City Master Manual

> **Status.** This chapter documents the *shipped* city layer, not the plan it was built from. Every
> backticked identifier below is a real type, field, or method in the code under `Assets/RoadCityMaster/City/`.
> Where the shipped behaviour differs from the original design, the shipped behaviour is what is written here
> and the divergence is noted in `CHANGELOG.md`.

The city layer turns a street network into a saved, shippable city: blocks, lots, placed buildings, and a
bake that freezes the result so it opens at play time with no generator running. It lives entirely under one
folder — `City/` — so removing that folder returns the plain road tool.

---

## 1. The data model, and what "saved versus derived" means

One city is one `CityData` asset (menu **Road City Master ▸ City Data**). It holds:

| Field | Kind | What it is |
|---|---|---|
| `seed` | **saved** | the generator input; the same seed reproduces the same derived city |
| `generatorVersion` | **saved** | semantic version of the code that produced the city |
| `schemaVersion` | **saved** | the data-shape version this asset was written under |
| `nodes`, `edges` | **saved** | the street-network graph (`NetworkNode`, `NetworkEdge`); never recomputed |
| `blocks` | **derived** except overridden | the blocks the network encloses (`Block`) |
| `lots` | **derived** except overridden | the lots blocks subdivide into (`Lot`) |
| `buildings` | **saved** | the placed buildings (`BuildingInstance`); never recomputed |

**Derived does not mean absent from disk.** § 10.3 requires a baked city to open and enter play mode with no
generator running, so the derived geometry is serialised into the asset at bake time. "Derived" is about what
a *re-bake recomputes*, not what is persisted.

The operational definition of the boundary is one method: `ClearDerived`. It removes exactly the
non-overridden `blocks` and `lots` — what a re-bake would recompute — and keeps the `seed`, the network, the
`buildings`, and every block or lot whose `isOverridden` flag is set. A re-bake calls `ClearDerived` and then
re-derives, so an author's overrides survive while stale derived geometry does not.

<!-- APPEND_MARKER -->

---

## 2. Selecting and overriding an element by hand

Selection is **hierarchical**: one click on a spot selects the broadest element there first, and each repeat
click at the same spot descends one level. The levels, broadest to narrowest, are the `CityElementKind`
values: `Block` → `Lot` → `LotCorner` → `NetworkNode`, then it wraps back to the block. Clicking a *different*
spot resets to that target's broadest element rather than continuing to descend.

A selection is a `CitySelection` value — a `kind`, a `primaryId`, and a `cornerIndex`. It identifies an
element by its stable `id` (or corner index), never by list position, so a selection survives the reordering a
re-bake causes. `CitySelectionResolver.ResolveClick` is the pure resolver behind the scene handles: give it
the city, the click point in XZ, the current selection, and a pick radius, and it returns the next selection.
Picking is deterministic and order-independent — when two elements overlap a click, the lowest `id` wins
regardless of list order.

Editing an element goes through `CityEditOps`:

- `MarkOverridden` sets the `isOverridden` flag on the selected `Block` or `Lot`, so a re-bake preserves it. A
  `NetworkNode` is graph *input*, not derived geometry, so it is not overridable and the call returns false.
- `MoveLotCorner` moves exactly one lot corner to a new XZ position, keeps that corner's height, marks the lot
  overridden, and rejects an out-of-range corner index. Only the dragged corner moves; its neighbours hold.
- `Revert` clears the override, so the next re-bake recomputes the element from the seed and network.

The rule to remember: **an edit sets `isOverridden`; a revert clears it.** That flag is the entire join
between hand-editing and re-baking.

---

## 3. The order of operations, and why it is fixed

The bake runs its stages in one canonical order, enforced rather than merely documented. The order is the
integer order of the `BakeStage` enum, and `CityBakePipeline.CanonicalOrder` is that sequence:

1. `LoadOrGenerate` — generate the city from the seed, or load the saved `CityData`.
2. `PublishFootprints` — publish street/pad footprints for the terrain to conform under.
3. `ConformTerrain` — let the terrain conform beneath those footprints.
4. `BakeScatter` — bake scatter with the city's footprints excluded.
5. `ProduceRuntime` — produce the runtime representation (the spawn table, § 5).

Each stage's output is the next stage's input, so running them out of order produces a world that is
individually correct and collectively wrong — trees in the street, or buildings on unconformed ground. That is
why the pad conform comes *after* the road corridor is published and scatter bakes *last*.
`CityBakePipeline.IsInCanonicalOrder` checks an observed run: the stage indices must strictly increase, so an
out-of-order stage or a repeated stage is caught, while **skipping** a stage is allowed (a stage that is not
built yet is simply omitted, not a violation).

---

## 4. The bake, the stamp, and the staleness warning

The bake is **explicit and never implicit**: it never runs on scene save, on play, on import, or on a value
change. An implicit bake would either cost an author minutes of unasked work on a slider nudge or — worse —
silently hand a different city to whoever opens the scene on a machine with slightly different content.

The bake writes an audit `BakeStamp` into the output, carrying `seed`, `generatorVersion`, `schemaVersion`,
`bakedAtUtc`, and `sourceCityAssetGuid`. `bakedAtUtc` is **informational only** — it lets an author tell
whether baked content predates a change they remember, and it must never feed a generation decision, or
determinism is gone.

On opening a scene, the tool compares the stamp against the current code with `BakeStamp.Evaluate`, which
returns a `BakeStaleness` verdict and performs **no** bake:

| Verdict | Meaning |
|---|---|
| `UpToDate` | the stamp matches the current generator and schema |
| `StaleGenerator` | baked by an older generator; a re-bake would change it — warn, name both versions, offer |
| `NewerGenerator` | baked by a newer generator than this code — do not touch it silently |
| `StaleSchema` | the saved shape predates the current schema; migrate or refuse |

A schema mismatch takes precedence. Version comparison is `BakeStamp.CompareSemanticVersions`, a numeric
per-field compare — so `1.10` is newer than `1.9` (a lexical compare would get this backwards) and `1.2`
equals `1.2.0`. The tool **warns and offers**; it never re-bakes on its own. Silently re-baking is the
implicit behaviour this design forbids; silently doing nothing leaves an author debugging a discrepancy the
tool already knew about.

**No generator runs at play time.** `CityGenerationGuard` is the single gate every generation entry point
calls first via `RequireGenerationAllowed`. In edit mode it is open; a runtime loader closes it on entering
play mode (`EnterPlayMode`), and any generation attempt there is refused and recorded in `BlockedAttempts` and
`LastBlockedContext`, so the self-test can prove no generator ran rather than assume it.

---

## 5. Runtime form: the spawn table and streaming

The baked runtime form is a **prefab-instance spawn table**. The bake writes the `CityData` asset plus a
`CitySpawnTable`: one `CitySpawnItem` per saved building, holding the building index and the `ChunkCoord` its
position falls in. The building already carries its category, variant, and world transform (`prefabCategory`,
`variantIndex`, `position`, `rotation`, `scale`), so the table is a thin index and duplicates nothing. The LOD
band is deliberately *not* baked — it is chosen at play time from camera distance.

Streaming is tuned by `CityStreamingConfig` (all values AUTHORED — there is no physically "correct" LOD
distance, only one that measures well on the target device):

- `chunkSizeMetres` — the square streaming grid cell size; a building's cell is `CityChunkGrid.ChunkOf` of its
  position.
- `loadRingRadius` — the Chebyshev ring of resident cells around the camera (radius 2 → a 5×5 neighbourhood).
- `nearMaxMetres`, `midMaxMetres`, `farMaxMetres` — the outer distances of the three `LodBand` values
  (`Near`, `Mid`, `Far`); `BandForDistance` selects the band, and past the far distance the chunk is not
  loaded at all.
- `collidersEnabledByBand` — colliders get their own per-band field, because they are the item most often left
  on everywhere and are expensive in a way a triangle count does not show. `Near` on, `Mid`/`Far` off by
  default.
- `instantiationBudgetPerFrame` — the load-spike guard. `CityStreamingScheduler` is a FIFO queue drained at
  most this many items per `Pump`, so streaming a chunk of a hundred buildings spreads over frames instead of
  hitching one.

---

## 6. Refusal messages — reading them and acting on them

The city layer refuses by design rather than silently omitting content, and **every refusal quotes a number**:
the quantity, its measured value, and the limit it broke. A refusal an author cannot interrogate becomes "the
generator just doesn't build there" — the report that gets a safety check disabled.

Two verdict types carry these messages, both with an always-set `reason` string:

- **Buildable-land test** → `LotVerdict`. Its `result` is a `Buildability`: `Buildable` (build as-is),
  `Reshaped` (a steep part failed but a large-enough remnant survives — build on `reshapedPolygon`), or
  `Rejected` (no remedy, or too little survived). The verdict also reports `maxSlope`, `meanSlope`, and
  `reliefMetres`, and the `reason` names which quantity broke which limit — e.g. *"max slope 33% exceeds zone
  max 25%"*.
- **Placement** → `PlacementVerdict`. Its `outcome` is a `PlacementOutcome`: `Placed` (the `instance` is the
  building record) or `Refused` (the building broke a zoning limit or would not fit). Again `reason` names the
  quantity, its value, and the limit — e.g. *"footprint 640 m^2 exceeds coverage limit 400 m^2"*.

When a lot is empty, read the `reason`. It tells you whether to change the zone, adjust the lot, or accept the
refusal — the tool's strictness is guidance, not an obstacle.

---

## 7. The determinism contract

**Same seed, same inputs, same city — byte for byte.** Per-element randomness is derived from the city `seed`
and a *stable* element identity (a lowest node index, never a list position) through `CitySeed`, so a change
upstream does not silently reshuffle everything downstream. The generation assembly bans `UnityEngine.Random`,
`Random.Range`, `DateTime.Now`, `Guid.NewGuid`, and `GetHashCode()` — the last because `string.GetHashCode` is
not stable across processes, which produces the worst bug signature there is: a city that reproduces all
afternoon and is different tomorrow.

The one honest caveat: changing `generatorVersion` may change the result. That is expected, and it is exactly
why the bake records the version in the `BakeStamp` — so the tool can warn you before the change happens
rather than leave you to discover it.

---

## 8. Terrain: connected mode, standalone mode, and what each can do

The tool works two ways, and **neither one is a degraded version of the other**.

**Connected.** A terrain provider is present in the scene. That component owns the terrain, so every change the road
needs is *requested* from it rather than written behind its back. The reason this is worth the indirection is
reversibility: the provider knows what the natural ground was, so deleting a road restores real terrain instead of
replaying a copy the road tool kept and hoped was still valid.

**Standalone.** No provider is present. The tool shapes the terrain itself, exactly as it always did — it reads and
writes the native `Terrain` tiles in the scene directly. This is a fully supported mode, not a fallback.

The mode is decided **once**, from what is actually in the scene, when the scene loads — and again when you press
*Check this scene for a terrain provider*. It is never re-decided per road, and no code path anywhere in the tool
asks "is a provider connected?" a second time. To connect one, add the Universe Creator terrain link component to
the object that generates your terrain.

A feature that needs a provider never silently does nothing: it is greyed out with the sentence below next to it.

<!-- availability-table:begin (generated from TerrainFeatureAvailability — regenerate with
     Window/Road City Master/Documentation/Write the terrain availability table; do not edit by hand) -->

| Feature | Availability | Standalone | Connected |
| --- | --- | --- | --- |
| Road cut into terrain | better connected | the terrain under the road is lowered or raised directly, on whichever terrain tiles are in the scene | the cut is requested from the provider, which shapes it into ground it owns and keeps it through regeneration |
| Terrain restored when a road is deleted | better connected | the heightfield stored before the road was built is written back, so the restore is only as good as that stored copy | the provider is told which ground changed and regenerates its natural surface there, with no stored copy to go stale |
| Grass and detail cleared along the road | both modes | detail layers under the road are cleared and restored on delete | the same clearing, announced to the provider so it does not repopulate the corridor |
| Trees removed from the road corridor | both modes | trees standing in the corridor are removed and put back on delete | the same removal, announced with the corridor so the provider leaves it clear |
| Terrain texture painted under the road | both modes | the splat map is painted under the road surface | the same painting, announced to the provider with the painted region |
| Tunnel cavity kept open through terrain | needs a provider | the tunnel shell is placed and terrain holes are punched, but nothing holds them open if the terrain is regenerated -- to keep a cavity open, connect a terrain provider by adding the Universe Creator terrain link component to the object that generates your terrain | the opening is held by the provider and survives regeneration; it is released when the tunnel is deleted |
| Bridge deck carried over terrain | better connected | the deck spans the ground untouched and the abutments and pier footings are terraformed once; whether a pier lands in water can only be answered for terrain tiles in the scene | the footings are requested as held ground the provider keeps flat, and the provider answers what each support stands on, including ground no tile covers yet |
| Building pads held against a shared heightfield | needs a provider | a pad is terraformed once and stays until something else changes that ground -- to hold a pad against regeneration, connect a terrain provider by adding the Universe Creator terrain link component to the object that generates your terrain | the pad is a held request, so the provider keeps that ground flat and gives it back on delete |
| Building and roads across streamed terrain chunks | needs a provider | only terrain tiles already loaded in the scene can be read or changed; a road leaving them stops affecting the ground -- for streamed terrain, connect a terrain provider by adding the Universe Creator terrain link component to the object that generates your terrain | the provider reports which chunks are active, and ground outside them is requested rather than assumed absent |
| Roads following the natural ground | better connected | height and slope are sampled from the terrain tiles in the scene | height and slope come from the provider, which knows ground that has not been built into a tile yet |
| City blocks, lots and street furniture | both modes | generated from the road network; the terrain is only read, never owned | identical, and the ground it sits on is requested through the same gateway as everything else |

<!-- availability-table:end -->

"Better connected" is a real category and is stated rather than glossed: the feature works standalone, and what you
give up is named. Vague wording here is what makes people think a working mode is broken.

**One change to the standalone path.** The terrain footprint is narrower than it was in earlier versions, because one
width was doing three jobs. See the changelog for the before and after. Everything else on the standalone path is
unchanged.

## 9. Where every feature's geometry comes from

This tool has exactly four mesh-building components, all of them part of the road construction chain it is built on:
**SplineC** produces the alignment (position, tangent, arc-length), **RoadConstructorBufferMaker** produces the road
surface itself, **SplinatedMeshMaker** sweeps a cross-section along a curve, and **EdgeObjectMaker** places objects at a
spacing along one. The city layer adds no fifth: closed areas and terrain heightfields go through its two operators, and
everything else in the list below produces no geometry at all and says so.

This matters to you because a second mesh builder would cost you twice for every fix. Every arc-length correction, LOD
change, UV convention and determinism guarantee would have to be made in two places, and the second copy falls behind
quietly. The symptom you would see is a kerb or a sidewalk whose texture stretches through a curve while the road beside
it looks right — a bespoke mesher that never received the fix the road surface got.

So each feature declares two things separately: the **shape** of what it delivers, and the **component** that builds it.
A sweep or a spacing goes to the chain, an area or a heightfield goes to the city operators, data goes nowhere. Declaring
the shape separately is what makes the table checkable rather than decorative: the build fails if any row's component
does not follow from its shape, and it fails if any feature in the range is missing a row. Two components on one row is
an honest answer, not a hedge — a bridge deck is a sweep whose piers are spaced objects.

<!-- provenance-table:begin (generated from GeometryProvenanceRegister — regenerate with
     Window/Road City Master/Documentation/Write the geometry provenance table; do not edit by hand) -->

| Task | Feature | Shape | Produced by | Also | Basis |
| --- | --- | --- | --- | --- | --- |
| RM-26 | Road terminus treatment | spaced objects | EdgeObjectMaker (placed objects) | - | The treatment is a set of discrete objects -- barrier, terminal, marker, buffer -- placed at stations computed from the road's end. Placement at a spacing along a curve is exactly what EdgeObjectMaker does; the carriageway being terminated is still built by the road surface path. |
| RM-27 | Lane-count mismatch: the merge taper | road surface | RoadConstructorBufferMaker (road surface) | - | A taper is the carriageway's own width changing over a length, so it is width input to the surface builder and not a separate object. Building it anywhere else would put two different sets of edge vertices on the same road and leave a seam wherever they disagreed. |
| RM-28 | Roundabout as a junction type | closed area | City area operator (10.4) | RoadConstructorBufferMaker (road surface) | The circulatory way is a closed loop and SplineC does not support one, so the loop cannot be a road: the annulus and the central island are area work through § 10.4's operators. The approach legs and their flares are ordinary carriageway and stay with the surface builder. |
| RM-29 | Sidewalk, including corners | swept profile | SplinatedMeshMaker (swept section) | - | A footway with a kerb is a fixed cross-section swept along an offset alignment. Turning a corner is an alignment problem -- a corner radius on the curve -- not a reason to build a polygon by hand. |
| RM-30 | Culverts and roadside ditches | swept profile | SplinatedMeshMaker (swept section) | EdgeObjectMaker (placed objects) | The ditch is a trapezoidal section swept along the road, so it sweeps. The barrels, headwalls and aprons are discrete objects at crossing stations, so they are placed. Two producers here is the honest answer, not a hedge. |
| RM-31 | Variable-width cross-section elements | swept profile | SplinatedMeshMaker (swept section) | - | The sweeper already samples the profile once per station, so a width that varies with station is data for the existing sweep rather than a new kind of geometry. |
| RM-32 | Pavement markings | swept profile | SplinatedMeshMaker (swept section) | EdgeObjectMaker (placed objects) | Longitudinal lines are thin ribbons swept along the alignment and must follow the same arc-length mapping as the surface under them, or the dashes drift out of step through curves. Symbols -- arrows, stop lines, legends -- are single objects at a station, so they are placed. |
| RM-33 | Bridge types beyond the flyover | swept profile | SplinatedMeshMaker (swept section) | EdgeObjectMaker (placed objects) | A deck is a cross-section swept along the crossing alignment; piers, towers and bearings repeat at a spacing underneath it. This is the row that proves a feature can honestly have two producers: forcing one answer would either sweep the piers or place the deck, and both are wrong. |
| RM-34 | Retaining structure on elevated road edges | swept profile | SplinatedMeshMaker (swept section) | - | A wall is a section -- face, footing, coping -- swept along the road edge, with its height driven per station by the drop to the ground. Sweeping it keeps it welded to the edge it retains. |
| RM-35 | Automatic grade separation when two roads cross | alignment | SplineC (alignment) | - | Separation is achieved by moving node elevations so the two alignments clear each other. The deliverable is the changed curve, which is SplineC's; the carriageways then rebuild themselves from it, so nothing here produces a surface of its own. |
| RM-36 | Aerial ropeway: cable car and gondola lines | spaced objects | EdgeObjectMaker (placed objects) | SplinatedMeshMaker (swept section) | Towers and cabins repeat along the line, so they are placed. The cable itself is a tube swept along a catenary alignment -- a sweep with an unusual vertical curve, which is still a sweep. |
| RM-37 | Tunnel cavity requests, portals, and the underwater seal | swept profile | SplinatedMeshMaker (swept section) | City heightfield operator (10.4) | The lining is a bore section swept along the alignment. The portal's notch into the hillside is ground, not lining, so it is heightfield work through § 10.4's operators. |
| RM-38 | Fix the terrain footprint: the road moves too much dirt | heightfield | City heightfield operator (10.4) | - | The whole subject of the task is which terrain samples change and by how much, so it is heightfield work by definition. No mesh is produced: the ground is Unity's Terrain. |
| RM-39 | Cut slopes that daylight into the ground | heightfield | City heightfield operator (10.4) | - | Daylighting means walking outward from the road edge until the slope meets the existing ground, and writing the samples in between. That is a heightfield operation, not a wall to be swept. |
| RM-40 | Superelevation: banking through curves | road surface | RoadConstructorBufferMaker (road surface) | - | Banking rotates the carriageway's cross-section about its crown, which changes the surface's own vertex heights. Anything swept alongside -- kerbs, markings, barriers -- inherits the rotation from the surface rather than computing it a second time. |
| RM-41 | Unsealed roads as a first-class surface type | road surface | RoadConstructorBufferMaker (road surface) | - | An unsealed road is the same carriageway with a different width, crown, edge treatment and material. It is a parameter set for the surface builder; a second builder for gravel would be the exact duplication this rule forbids. |
| RM-42 | One zone taxonomy, and what a zone carries | data only | none | - | A taxonomy is a shared vocabulary read by the things that do build. It has no vertices, and saying so plainly is more useful than pretending every task is a mesher. |
| RM-43 | Transport hubs as authored anchors | closed area | City area operator (10.4) | - | A hub's apron or platform slab is a closed footprint, not a sweep along a curve, so it goes to the area operators. Its hard connection requirements are satisfied by ordinary roads built the ordinary way. |
| RM-44 | Paint the settlement footprint as a mask | data only | none | - | The deliverable is a mask the author paints. It decides where later tasks build, and builds nothing itself. |
| RM-45 | The density and zone masks: what the paint means | data only | none | - | Reading a channel and stating what its value means is interpretation, not geometry. |
| RM-46 | Conform the painted footprint to water and slope | data only | none | - | It reads the heightfield and the water surfaces and then edits the MASK. Reading terrain is not heightfield work; writing terrain would be, and this task writes none. |
| RM-47 | Street furniture and vegetation resolved at bake time | spaced objects | EdgeObjectMaker (placed objects) | City area operator (10.4) | Furniture along a street is spacing along a curve. Scatter inside a block is a density over an area, which is why the area operators appear as the second producer instead of a bespoke scatterer. |
| RM-48 | Import contract and validator for user prefabs | data only | none | - | A contract states what an incoming prefab must satisfy and a validator checks it. Both inspect geometry someone else authored; neither creates any. |
| RM-49 | Full-tree scan of the vendored road tool | data only | none | - | The output is written findings. This register is one of them. |
| RM-50 | Field census and the non-breaking contract | data only | none | - | Naming every serialised field and promising not to break it is a data contract. |
| RM-51 | Rebuild the inspector: grouping, units, validation | data only | none | - | An inspector edits values. The values then reach the builders above. |
| RM-52 | The status panel: mode, provider, last build, refusals | data only | none | - | A panel that reports state. Its whole value is that it does not change anything it reports. |
| RM-53 | Connection indicator on the hierarchy row | data only | none | - | A drawn marker in the editor's own hierarchy window is not scene geometry. |
| RM-54 | A stated City sub-hierarchy for generated objects | data only | none | - | It creates empty parent transforms in an agreed order. A transform carries no vertices; what gets parented into it was built by one of the producers above. |
| RM-55 | Fail closed: no grey box, no silent substitution | data only | none | - | The resolver's entire purpose is to REFUSE rather than invent geometry, so a row claiming it produced any would contradict the task. Its own gate scans this tree for primitive-cube creation for exactly the same reason, and that scan is why this sentence does not spell the call out. |
| RM-56 | Show where every category's content comes from | data only | none | - | It lists and validates the author's own assets. |
| RM-57 | Route terrain modification through the provider | heightfield | City heightfield operator (10.4) | - | This is the one file allowed to write TerrainData, so it is the heightfield producer by construction. When a provider is connected it writes nothing and hands the regions over instead, which is a different destination for the same operation, not a different kind of geometry. |
| RM-58 | Keep the standalone path working, and say when a provider is needed | data only | none | - | A per-feature availability table plus the sentence that tells the author what to connect. |
| RM-59 | A divided road is two carriageways | swept profile | SplinatedMeshMaker (swept section) | RoadConstructorBufferMaker (road surface) | What RM-59 delivers is ONE cross-section emitter that produces the same vertices for a divided and an undivided road, which makes it profile data for the sweep. The two carriageway surfaces themselves are still the surface builder's, which is why it is named second. |
| RM-60 | What is in the median: trough, barrier ends, planting | spaced objects | EdgeObjectMaker (placed objects) | SplinatedMeshMaker (swept section) | Barriers, their terminals and planting are objects placed along the median at a spacing. The drainage trough is a channel section swept along it. The width band decides which of the two applies. |
| RM-61 | Shape-flexible roundabout: ellipse or control polygon | closed area | City area operator (10.4) | - | The shape is a closed curve by definition and SplineC cannot close, so the circulatory way and island are area work. Everything the task actually computes -- per-station curvature, entry skew, splitter length -- is measurement on that closed curve, and measurement produces no vertices at all. |

<!-- provenance-table:end -->

