# Road & City Master — the manual

The road and city tool that ships inside Universe Creator. This is the tool's own manual: what it does, every
author-facing field with its unit and valid range, every refusal it can give you and what to do about it, and every
number it takes from a published standard, with the edition.

**Status of this document.** The three reference tables below — fields, refusals, standards — are **generated** from the
registers the tool actually ships (`InspectorFieldRegistry`, `ManualReferences`) and spliced in between markers by
`ToolManualWriter`. `ToolManualGate` (`RCMMANUAL`) fails the build if this file and those registers disagree, so a field
cannot be documented under a name or unit the inspector does not use. The prose is written by hand. Where a feature's
scene half is not yet wired, the feature list says so in the same sentence as the feature.

## Contents

- [What this tool is, and what it is called](#what-this-tool-is-and-what-it-is-called)
- [What it does not do](#what-it-does-not-do)
- [Build a divided road with a median barrier](#build-a-divided-road-with-a-median-barrier)
- [Place a roundabout](#place-a-roundabout)
- [Connect a terrain provider](#connect-a-terrain-provider)
- [Diagnose a refusal](#diagnose-a-refusal)
- [Fields: units and valid ranges](#fields-units-and-valid-ranges)
- [Refusals: every message, and what to do about it](#refusals-every-message-and-what-to-do-about-it)
- [Cited standards, with the edition used](#cited-standards-with-the-edition-used)
- [The menu: what is in it, and where](#the-menu-what-is-in-it-and-where)
- [Licence, attribution and the rename](#licence-attribution-and-the-rename)

## What this tool is, and what it is called

A spline-based road system with intersections, bridges, extruded edge objects and terrain interaction, extended with a
city layer: a street network, block subdivision into lots, a buildable-land test, building placement, and streetscape as
part of the road cross-section rather than as a second mesh pipeline.

The **display name** is **Road & City Master**. It appears in window titles, menu paths, inspector headers and help
text, and it comes from one constant so it cannot drift. The **identifier** is `RoadCityMaster`: the folder under
`Assets/`, the two assembly definitions, and the namespaces. An assembly name cannot carry a space or an ampersand,
which is the whole reason there are two names rather than one.

Everything the tool does is reached from **Window ▸ Road City Master**. There is no separate top-level menu.

The tool has two modes and neither is a degraded version of the other. **With a terrain provider connected**, every
terrain modification is *requested* through the host, so deleting a road restores real ground instead of a stored copy.
**With no provider**, the tool shapes the terrain itself, exactly as it always did. The mode is decided once per scene
from what is actually in the scene, and the status panel says which mode you are in.

## What it does not do

Half of this tool's scope decisions were exclusions, and they are here rather than in a roadmap because a roadmap is
where nobody reads them. Read this list once and stop looking for these features:

- **no interchanges** — grade separation between two crossing roads, yes; a diamond, cloverleaf or directional
  interchange with ramps, no. Build those from separate roads by hand.
- **no multi-lane circulatory spirals** — a roundabout's circulatory carriageway is single-lane in its geometry model.
  Spiral lane markings and multi-lane entry path alignment are outside it.
- **no rail crossings or turnouts** — rail track and station placement exist as a track type; a level crossing with a
  road, and any turnout, switch or junction in the track, do not.
- **no procedural buildings** — buildings are *placed*, from prefabs you supply. Nothing generates a facade, a floor
  plan or a roof.
- **no generated wear** — no procedural cracking, patching, rutting, staining or repair scars. Wear is in the material
  you assign, not in the mesh.
- **no city node canvas in v1** — the city is authored in the scene and in inspectors. There is no graph or node canvas
  for laying out a city, and building one is not deferred work, it is out of scope.

## Build a divided road with a median barrier

A divided road here is **two carriageways**, not one wide one with a stripe down it. That distinction is the reason the
median can carry a barrier, a trough or planting, and the reason the two sides can sit at different heights.

1. **Create the road.** Window ▸ Road City Master ▸ create a road system if the scene has none, then add a road to it.
   You get a `Road` with a spline and, at first, a single carriageway.
2. **Set what it is.** In the road's inspector, the first group — *What it is* — carries the identity fields: lane width
   and lane count. Set the design class first if your project has RM-88's class taxonomy wired; the class supplies the
   defaults for everything below it, and overriding a default is then a visible decision rather than a blank field.
3. **Divide it.** Turn on the divided-carriageway option. The road becomes two carriageways with **independent vertical
   and lateral offsets** — that is the point of dividing it, and it is what lets the uphill side climb while the downhill
   side stays low, the way a real divided highway crosses a side slope.
4. **Set the median width.** The median is a variable-width cross-section element: it can taper, so it can open out at a
   junction and close again. Width is in metres, and the width you set is the *clear* median, between the two inner
   carriageway edges.
5. **Put something in the median.** Three choices, and they are exclusive along any one stretch: a **drainage trough**, a
   **barrier**, or **planting**. Pick barrier for a high-speed road.
6. **Terminate the barrier run.** A barrier run needs an end treatment at both ends. If the run has no length for one,
   the tool refuses with `BARRIER-RUN-WITHOUT-TERMINAL` rather than leaving a blunt end in open ground — a blunt end is a
   struck-object hazard, and it is the single most common defect in a hand-built barrier.
7. **Build.** The status panel above the inspector groups reports the mode, the provider, the last build and every
   outstanding refusal. Read it before you go looking in the console.

**Status.** The divided-carriageway geometry and the median contents are RM-59 and RM-60; the median's variable width is
RM-31. Their solvers and gates are in place. The inspector controls that expose road class, lane counts, median and
design speed are RM-93, and the legacy road inspectors move onto the modern drawer in RM-102 — until then some of these
fields are reached through the existing inspector layout rather than the four groups described above.

## Place a roundabout

A roundabout is a junction type, not a decoration: the tool solves its geometry rather than dropping a ring mesh.

1. **Select the junction.** Place or select the intersection where the roads meet.
2. **Choose roundabout as the junction type.** The approaches you already have become entries.
3. **Set the inscribed circle diameter.** This is the single most load-bearing number in a roundabout, and it is the one
   the design guide fixes first. Everything else — entry radius, exit radius, splitter island length, circulatory
   width — is solved from it and from the approach angles.
4. **Shape it if you need to.** The circulatory carriageway does not have to be a circle. It can be an ellipse or a
   control polygon, and when you change its shape the **entry geometry is re-solved** against the new shape rather than
   left pointing at a circle that is no longer there.
5. **Read the measured values.** The solved corner radius, the tightest circulatory radius and its station, the inscribed
   circle diameter and the entry path radius are shown **disabled**, under the group *Measured*. They are results. If a
   measured value is not what you want, change an input, not the result.
6. **Watch for `RADIUS-BELOW-MINIMUM`.** If an entry path radius comes out under the minimum for the approach's design
   speed, the tool refuses that entry rather than building a curve nobody can take at the posted speed.

**Status.** The roundabout junction type is RM-28 and the shape-flexible solver with re-solved entry geometry is RM-61;
both cores and their gates are in place. The scene half — the mesh, the handles you drag the control polygon with — is
the deferred half named in those tasks.

## Connect a terrain provider

Connecting a provider changes who owns the ground. It is worth doing, and it is worth understanding before you do it.

1. **Put the host's provider in the scene.** Universe Creator supplies one. The road tool does not create it, look for it
   by name, or fall back to a copy of it.
2. **Open the status panel** on the road system. It states the mode in one sentence: whether a provider is connected, and
   which one. This is the only place you need to check — if the panel says no provider, no amount of correct road setup
   will make a provider-only feature work.
3. **Build a road.** Every terrain modification now goes out as a *request*: flatten this band, blend to natural ground
   here, keep trees off this footprint, open a cavity through the ground for this tunnel. The provider decides how to
   satisfy the request against its own chunk grid.
4. **Delete the road.** With a provider, the ground comes back as **real ground**, regenerated by the host, not as a
   stored copy of the heights the road overwrote. That is the substantive reason to connect one.
5. **Check the availability table** for anything that did not happen. Every feature that needs a provider says so in one
   sentence naming what to connect, in the inspector and in the manual. Nothing that needs a provider silently does
   nothing — if it needs one and there is none, you get `PROVIDER-REQUIRED`.

With **no** provider, the tool shapes the terrain itself and the whole standalone path keeps working. The one thing that
changes is that deleting a road restores the heights the tool saved, not ground the host regenerates.

**Status.** Routing every terrain modification through the provider is RM-57; the standalone path and the stated
availability table are RM-58; the road tool's side of the handshake and its failure states are RM-73. The provider
methods themselves — `SampleHeight`, `SampleNormal`, `GetActiveChunkBounds`, `NotifyCutRegion`, `RequestTunnelOpening`,
`ReleaseTunnelOpening` — are live on the host side.

## Diagnose a refusal

This tool refuses. It does not clamp your value quietly, substitute content you did not choose, or build a
half-correct thing and hope you notice. That is deliberate: a silent clamp is a bug you find three weeks later in a
screenshot, and a substituted prefab looks like a mistake in your own art.

So a refusal is a diagnosis, and it comes with four things: **where** it appears, **what** it says, **why** the tool
refuses, and **how** to resolve it.

**Where.** There is one rule and it has no exceptions. A refusal about **one object** appears **on that object**, in its
inspector, at the field that caused it. The **aggregate** — how many refusals this build produced, of which kinds —
appears in the road system's **status panel**. Never both, never neither. If you are hunting for a message, those are the
only two places it can be.

**What.** The wording is fixed and comes from one register, so the message in the inspector, the message in the status
panel and the message in the table below are the same string. A message names the limit *and its source*: not "value too
low" but "below the minimum radius for 50 km/h in the adopted standard". You are told where the number came from.

**Why and how.** That is the table further down: one row per refusal, with the two or three ways out. Start with the
refusal id — the upper-case token like `TUNNEL-COVER-INSUFFICIENT` — and read across.

Two habits make this fast. Read the **status panel first**, because it tells you how many refusals there are before you
start looking for one. And treat a refusal about a **measured** value as a refusal about its **inputs**: measured values
are drawn disabled for exactly this reason.

**Status.** The status panel is RM-52 and the routing rule is `RefusalRouting.Decide` from RM-64, a total function, so
the "never both, never neither" promise is a property of the code rather than something an implementer has to remember
while writing the next inspector.

## Fields: units and valid ranges

The most common question about a road tool is what a field expects. This table answers it for every field the tool's
field registry describes, and it is generated from that registry: the `Field` column is the **serialised** name, so it is
what you see in a `.unity` diff and what a script would reference, and the *Author sees* column is the label drawn beside
it. A range's source is prefixed `AUTHORED` when the project set the bound rather than a standard — the number is
deliberate, not a guess, and the distinction is the whole point of recording the source.

Out-of-range values are refused **at the field**, with a message naming the limit and its source. Nothing is clamped
behind your back.

<!-- fields-table:begin — generated from InspectorFieldRegistry via ManualReferences. Do not edit by hand. -->

| Component | Field | Author sees | Group | Unit | Valid range | Where the limit comes from |
| --- | --- | --- | --- | --- | --- | --- |
| `Road` | `laneWidth` | Lane width | What it is | m | 2.5 to 5 m | AUTHORED: highway lane 2.7–3.75 m (AASHTO); bound widened for shared/rural lanes |
| `Road` | `lanesForward` | Lanes forward | What it is | dimensionless | at least 1 | AUTHORED: a carriageway has at least one lane |
| `Road` | `lanesBackward` | Lanes backward | What it is | dimensionless | at least -1 | AUTHORED: 0 on a one-way; -1 means unsplit legacy data the model halves on load |
| `Road` | `isOneWay` | One-way | What it is | dimensionless | 0 to 1 | AUTHORED: a carriageway either carries both directions or one; there is no third state |
| `Road` | `roadClass` | Road class | What it is | dimensionless | 0 to 12 | AUTHORED: one of the declared design classes; each carries its own lane, width, speed and grade defaults with their sources in RoadClassDefaults |
| `Road` | `settlementRole` | Settlement role | What it is | dimensionless | 0 to 4 | AUTHORED: none, main highway, intercities, intercities connecting towns, or intercities connecting villages -- the orders of place a hand-drawn network connects |
| `Road` | `centerDivider` | Median type | What it is | dimensionless | 0 to 4 | AUTHORED: none, concrete barrier, barrier with glare screens, wire rope, or painted markers |
| `Road` | `medianWidth` | Median width | What it is | m | 0 to 30 m | AUTHORED ceiling: 0 is no physical median and rural depressed medians reach ~30 m. What a median of a given width can actually HOLD is not decided here -- MedianContent's cited band table owns that, from painted flush below 1.2 m up to trough-and-planting at 15 m |
| `Road` | `speedLimitKPH` | Design speed | What it is | km/h | 5 to 140 km/h | AUTHORED bounds spanning real posted limits, from ~5 km/h shared space to the 140 km/h of the fastest posted motorways; 0 or less falls back to the road class default |
| `Road` | `shoulderWidth` | Shoulder width | What it looks like | m | 0 to 4 m | AUTHORED authoring bound; the cited tiers are 0.6–1.2 m edge strip and 3.0 m refuge (AASHTO), per class in City.RoadClassDefaults; widened here for verges |
| `Road` | `shoulderCrossSlope` | Shoulder cross-fall | What it looks like | fraction | 0.02 to 0.06 fraction | AASHTO: a paved shoulder is cross-sloped 2% to 6% and never flatter than the travelled way it adjoins; 4% is the usual bituminous figure |
| `Road` | `desiredRampHeight` | Ramp height | What it looks like | m | 0 to 2 m | AUTHORED: 0–2 m ramp and kerb rise, the band recorded for this field in the field census; 0 leaves a vertical edge and the tool's own default is 0.35 m |
| `Road` | `maxGrade` | Max grade | What it does to the world | fraction | 0 to 0.15 fraction | AASHTO max grade 6–12% by terrain and design speed; 15% hard ceiling |
| `Road` | `matchTerrainSubtraction` | Terrain match drop | What it does to the world | m | 0 to 2 m | AUTHORED: vertical offset the carriageway sinks into matched terrain |
| `Road` | `terrainFootprint.shoulderMetres` | Flat verge beyond section | What it does to the world | m | 0 to 3 m | AUTHORED: the graded shoulder is wider than the usable one, and AASHTO's Roadside Design Guide rounds the break of slope rather than cutting it square, so there is always some flat ground beyond the surfaced edge; 0.6 m is ordinary practice for the berm before an embankment slope and the tool's own default is 0.5 m |
| `Road` | `batterRunPerRise` | Batter gradient | What it does to the world | × | 1 to 6 × | AUTHORED bounds, from a 1:1 rock cut to a 6:1 fully recoverable embankment; AASHTO's Roadside Design Guide is what makes the flat end matter -- 4:1 and flatter is recoverable, 3:1 traversable but not recoverable, steeper than 3:1 critical. 1.5:1 is ordinary stable-soil practice and is the figure City.CutSlopeClass carries |
| `Road` | `batterMaxReachMetres` | Batter max reach | What it does to the world | m | 0 to 60 m | AUTHORED ceiling each side, the same 60 m bound the splat paint half-width carries; it is capped again at run time by half of Match heights distance, which is the figure that sizes the write buffers |
| `Road` | `roadDefinition` | Segment resolution | What is generated from it | m | 1 to 20 m | AUTHORED: metres per generated segment; lower is finer mesh at higher cost |
| `RoadTerrain` | `splatWidth` | Splat paint half-width | What it does to the world | m | 1 to 60 m | AUTHORED: paint distance from carriageway; RM-38 flags the 30 m default as over-wide |
| `RoadTerrain` | `splatResoWidth` | Splat resolution W | What it looks like | dimensionless | 512 to 4096 | AUTHORED: power-of-two splat map resolution |
| `RoadTerrain` | `splatResoHeight` | Splat resolution H | What it looks like | dimensionless | 512 to 4096 | AUTHORED: power-of-two splat map resolution |
| `RoadIntersection` | `junctionType` | Junction form | What it is | idx | 0 to 3 idx | DERIVED: JunctionTypeEnum member count |
| `RoadIntersection` | `roundaboutInscribedDiameterMetres` | Inscribed circle diameter | What it looks like | m | 13 to 80 m | NCHRP 672: mini-roundabout 13-27 m, single-lane 27-46 m; upper bound AUTHORED for multilane |
| `RoadIntersection` | `roundaboutEntryWidthMetres` | Entry width | What it looks like | m | 4 to 5 m | NCHRP 672 single-lane entry width range |
| `RoadIntersection` | `roundaboutSplitterIslandLengthMetres` | Splitter island length | What it looks like | m | at least 15 m | NCHRP 672: at least 15 m, desirably about 30 m |
| `RoadIntersection` | `interchangeVerticalClearanceMetres` | Vertical clearance | What it does to the world | m | 4.9 to 8 m | AUTHORED to AASHTO highway-over-highway practice (~5.0 m plus resurfacing allowance); rail and electrified rail are greater |
| `RoadIntersection` | `rampAlignment` | Ramp alignment | What is generated from it | idx | 0 to 2 idx | DERIVED: RampAlignmentKind member count |
| `RoadIntersection` | `rampRadiusMetres` | Ramp radius | What is generated from it | m | at least 89.4774516821761 m | DERIVED: InterchangeRamp.MinimumRadiusMetres, which is the larger of the class's 50 m house minimum and the radius the ramp design speed physically allows -- 89.5 m at 50 km/h, so the physics governs and quoting the house 50 m here would advertise a floor the resolver refuses |
| `RoadIntersection` | `flyoverSpanLengthMetres` | Deck span | What is generated from it | m | at least 7 m | AUTHORED: at least the narrowest crossing carriageway (two 3.5 m lanes) or the deck cannot span it |
| `CityAuthoring` | `graph` | Graph | What it is | ref | unbounded | no source recorded |
| `CityAuthoring` | `seed` | Seed | What it is | dimensionless | unbounded | no source recorded |
| `CityAuthoring` | `regionCentre` | Region centre | What it is | ref | unbounded | no source recorded |
| `CityAuthoring` | `authoredAlignment` | Authored alignment | What it is | ref | unbounded | no source recorded |
| `CityAuthoring` | `regionXZ` | Region (local X/Z) | What it is | m | unbounded | no source recorded |
| `CityAuthoring` | `sourceRoad` | Source road | What it is | ref | unbounded | no source recorded |
| `CityAuthoring` | `sourceEnd` | Source end | What it is | dimensionless | unbounded | no source recorded |
| `CityAuthoring` | `archetypeOverride` | Archetype override | What it is | dimensionless | unbounded | no source recorded |
| `CityAuthoring` | `prefabLibraries` | Prefab libraries | What it looks like | ref | unbounded | no source recorded |
| `CityAuthoring` | `scanRoots` | Scan roots | What it looks like | ref | unbounded | no source recorded |
| `CityAuthoring` | `cityData` | City data | What is generated from it | ref | unbounded | no source recorded |
| `WaterSurface` | `hasWater` | Has water | What it is | dimensionless | unbounded | no source recorded |
| `WaterSurface` | `waterSurfaceY` | Water level | What it is | m | unbounded | no source recorded |
| `RoadClassSource` | `classId` | Road class | What it is | idx | at least 0 idx | RoadClassCatalog's own ids, which start at Arterial (0); Collector is 1 and Local is 2 |
| `ZoneCatalogueSource` | `zoneId` | Zone | What it is | ref | unbounded | no source recorded |
| `SuitabilityRaster` | `metresPerCell` | Cell size | What is generated from it | m | 1 to 64 m | AUTHORED band around the 16 m the catalogue ships: below a metre the raster is finer than the terrain it samples and costs memory for no new information, above 64 m a whole city block falls inside one cell and the ranking can no longer tell one site from its neighbour |
| `SuitabilityRaster` | `slopeWeight` | Slope weight | What is generated from it | fraction | 0 to 1 fraction | a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one ships 0.45 |
| `SuitabilityRaster` | `earthworkWeight` | Earthwork weight | What is generated from it | fraction | 0 to 1 fraction | a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one ships 0.25 |
| `SuitabilityRaster` | `waterWeight` | Water weight | What is generated from it | fraction | 0 to 1 fraction | a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one ships 0.15 |
| `SuitabilityRaster` | `landUseWeight` | Land use weight | What is generated from it | fraction | 0 to 1 fraction | a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one ships 0.1 |
| `SuitabilityRaster` | `networkWeight` | Network weight | What is generated from it | fraction | 0 to 1 fraction | a share of one decision: the five suitability weights the catalogue ships sum to 1.0, and this one ships 0.05 |
| `CorridorSolve` | `toleranceFraction` | Corridor tolerance | What is generated from it | fraction | 0 to 1 fraction | a fraction of the corridor's own cost, so one is the whole of it; the solver ships 0.05 |
| `BlockInset` | `fallbackInsetMetres` | Fallback inset | What is generated from it | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `LotSubdivision` | `targetAreaM2` | Target lot area | What is generated from it | m² | at least 0 m² | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `LotSubdivision` | `splitJitter` | Split jitter | What is generated from it | fraction | 0 to 1 fraction | AUTHORED: a fraction of the split's own spacing, so one whole lot width is the most a split can be moved and still be the same split |
| `BuildableLand` | `sampleSpacingMetres` | Sample spacing | What is generated from it | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ZonePaint` | `straddleTieFraction` | Straddle tie | What is generated from it | fraction | 0 to 1 fraction | AUTHORED: a share of one lot's area, so the whole lot is the most that can lie on either side of a zone boundary |
| `PrefabValidate` | `category` | Category | What it looks like | ref | unbounded | no source recorded |
| `PrefabValidate` | `requester` | Requester | What it looks like | dimensionless | unbounded | no source recorded |
| `PolesAndSpans` | `runLengthMetres` | Run length | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `Rail` | `designSpeedKmh` | Design speed | What it looks like | km/h | at least 0 km/h | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `Ropeway` | `runLengthMetres` | Run length | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `Ropeway` | `spanLengthMetres` | Span length | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `FootprintPublish` | `metresPerPixel` | Footprint resolution | What it does to the world | m | 0.5 to 16 m | AUTHORED band around the 4 m the catalogue ships: a pixel finer than half a metre resolves a kerb the conform cannot honour anyway, and one coarser than 16 m is wider than a street, so the footprint would stop describing the thing it is masking |
| `FootprintPublish` | `featherWidthMetres` | Feather width | What it does to the world | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `FootprintConformNode` | `cellAreaM2` | Cell area | What it does to the world | m² | at least 0 m² | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `HandPlacedScan` | `produceGroundRequests` | Ask for levelling | What it does to the world | dimensionless | unbounded | no source recorded |
| `HandPlacedScan` | `supportToleranceMetres` | Support tolerance | What it does to the world | m | 0.001 to 0.15 m | GroundDriftClass.supportToleranceMetres is 0.025 m, the finished-formation earthworks tolerance; the ceiling is GroundDriftClass.visibleMismatchMetres, because a support tolerance looser than the visible one would swap the two verdicts round |
| `HandPlacedScan` | `visibleMismatchMetres` | Visible mismatch | What it does to the world | m | 0.01 to 1 m | GroundDriftClass.visibleMismatchMetres is 0.15 m, one kerb upstand; AUTHORED band around it, since a gap of a whole metre is a storey and stops being a mismatch |
| `HandPlacedScan` | `maximumStationSpacingMetres` | Station spacing ceiling | What is generated from it | m | 0.5 to 20 m | the floor is FootprintScanClass.minimumStationSpacingMetres, which is one post of a 1025-resolution heightmap over a 500 m tile; the ceiling is GroundDriftClass.stationSpacingMetres, past which an object would be sampled more coarsely than a road |
| `HandPlacedScan` | `maximumObjects` | Object limit | What is generated from it | dimensionless | at least 1 | AUTHORED: a sweep looks at at least one object, and there is no ceiling because reaching the limit is reported rather than silently accepted |
| `HandPlacedScan` | `includeInterior` | Sample the inside | What is generated from it | dimensionless | unbounded | no source recorded |
| `HandPlacedScan` | `contactMode` | How it meets the ground | What it is | dimensionless | unbounded | no source recorded |
| `HandPlacedScan` | `elevationRule` | Level a pad is cut to | What it does to the world | dimensionless | unbounded | no source recorded |
| `ChunkGrid` | `chunkSizeMetres` | Chunk size | What is generated from it | m | at least 1 m | CityChunkGrid substitutes 1 m for a non-positive chunk size, so one metre is the smallest edge the grid itself will honour; the streaming config ships 100 m |
| `SpawnTable` | `chunkSizeMetres` | Chunk size | What is generated from it | m | at least 1 m | CityChunkGrid substitutes 1 m for a non-positive chunk size, so one metre is the smallest edge the grid itself will honour; the streaming config ships 100 m |
| `OverrideMarker` | `element` | Element | What is generated from it | dimensionless | unbounded | no source recorded |
| `OverrideMarker` | `id` | Element id | What is generated from it | idx | at least 0 idx | AUTHORED: element ids are handed out from zero upward, so a negative id names nothing the bake could find |
| `OverrideMarker` | `note` | Note | What is generated from it | dimensionless | unbounded | no source recorded |
| `CommentGroup` | `title` | Title | What is generated from it | dimensionless | unbounded | no source recorded |
| `CommentGroup` | `body` | Body | What is generated from it | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `category` | Category | What it looks like | ref | unbounded | no source recorded |
| `ElementPlacement` | `alignToSurfaceNormal` | Lie on the slope | What it looks like | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `capital.present` | Capital: present | What it looks like | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `capital.perKilometreOfFrontage` | Capital: per km of frontage | What it looks like | /km | at least 0 /km | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `capital.minSpacingMetres` | Capital: minimum spacing | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `capital.embedDepthMetres` | Capital: embed depth | What it looks like | m | -1 to 0.15 m | GroundSeating.DeriveEmbedFromFootprint is -1 and GroundSeatingClass clamps at 0.150 m, which is GroundDriftClass.visibleMismatchMetres -- roughly one kerb upstand |
| `ElementPlacement` | `city.present` | City: present | What it looks like | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `city.perKilometreOfFrontage` | City: per km of frontage | What it looks like | /km | at least 0 /km | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `city.minSpacingMetres` | City: minimum spacing | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `city.embedDepthMetres` | City: embed depth | What it looks like | m | -1 to 0.15 m | GroundSeating.DeriveEmbedFromFootprint is -1 and GroundSeatingClass clamps at 0.150 m, which is GroundDriftClass.visibleMismatchMetres -- roughly one kerb upstand |
| `ElementPlacement` | `town.present` | Town: present | What it looks like | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `town.perKilometreOfFrontage` | Town: per km of frontage | What it looks like | /km | at least 0 /km | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `town.minSpacingMetres` | Town: minimum spacing | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `town.embedDepthMetres` | Town: embed depth | What it looks like | m | -1 to 0.15 m | GroundSeating.DeriveEmbedFromFootprint is -1 and GroundSeatingClass clamps at 0.150 m, which is GroundDriftClass.visibleMismatchMetres -- roughly one kerb upstand |
| `ElementPlacement` | `village.present` | Village: present | What it looks like | dimensionless | unbounded | no source recorded |
| `ElementPlacement` | `village.perKilometreOfFrontage` | Village: per km of frontage | What it looks like | /km | at least 0 /km | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `village.minSpacingMetres` | Village: minimum spacing | What it looks like | m | at least 0 m | AUTHORED: a length, an area, a speed or a rate is never negative, and zero is the parameter's own documented way of saying the operator should use the figure its class or archetype carries |
| `ElementPlacement` | `village.embedDepthMetres` | Village: embed depth | What it looks like | m | -1 to 0.15 m | GroundSeating.DeriveEmbedFromFootprint is -1 and GroundSeatingClass clamps at 0.150 m, which is GroundDriftClass.visibleMismatchMetres -- roughly one kerb upstand |
| `RoadPowerLine` | `settings.enabled` | Carries a line | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.circuit` | Circuit class | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.side` | Frontage side | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.offsetBeyondEdgeMetres` | Offset beyond carriageway | What it looks like | m | 0.5 to 9 m | AASHTO Roadside Design Guide clear zone: 0.5 m is a pole set behind an urban kerb, and 9 m covers the widest clear zone tabulated for a high-speed, high-volume road on fill |
| `RoadPowerLine` | `settings.startStationMetres` | Run starts at | What it looks like | m | at least 0 m | AUTHORED: stations are measured from the road's own start |
| `RoadPowerLine` | `settings.endStationMetres` | Run ends at | What it looks like | m | at least -1 m | AUTHORED: -1 means run to the end of the road |
| `RoadPowerLine` | `settings.electrical.spanLengthMetres` | Span length | What it looks like | m | 30 to 90 m | Overhead distribution practice: 40 m nominal, down to 30 m on dense urban frontage and up to 90 m on open rural line |
| `RoadPowerLine` | `settings.phaseCount` | Phases | What it looks like | dimensionless | 1 to 3 | Utility practice: an overhead distribution circuit is single-phase, two-phase or three-phase |
| `RoadPowerLine` | `settings.secondaryRack` | Secondary rack | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.communicationStrung` | Communication strand | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.transformerEveryNPoles` | Transformer every | What it looks like | dimensionless | at least 0 | AUTHORED: 0 places none; a pole-mounted distribution transformer commonly serves four to ten spans of secondary |
| `RoadPowerLine` | `settings.guyTurnDegrees` | Guy above turn of | What it looks like | ° | 0 to 90 ° | AUTHORED: below the threshold an intermediate pole is in balance and needs no guy; 0 guys every pole and 90 guys none but the two ends |
| `RoadPowerLine` | `centrelineSampleSpacingMetres` | Curve sample spacing | What it looks like | m | 1 to 25 m | AUTHORED: fine enough that a span end lands on the curve at any radius a road is built to, coarse enough that a kilometre of road is hundreds of points and not thousands |
| `RoadPowerLine` | `settings.timberMaterial` | Timber material | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.hardwareMaterial` | Hardware material | What it looks like | dimensionless | unbounded | no source recorded |
| `RoadPowerLine` | `settings.conductorMaterial` | Conductor material | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.enabled` | Carries a railway | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.trackCount` | Tracks | What it looks like | dimensionless | 1 to 2 | AUTHORED: one or two tracks staked from one centreline; three or more is a yard, which is laid out from its own geometry rather than offset from a line |
| `RailAlignment` | `settings.electrified` | Overhead line | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.sampleSpacingMetres` | Sample spacing | What it looks like | m | 0.5 to 25 m | AUTHORED: fine enough that the tightest curve any class allows is cut into chords whose departure from the arc is under the ballast's own tolerance, coarse enough that ten kilometres of mainline is thousands of samples and not hundreds of thousands |
| `RailAlignment` | `settings.track.minimumRadiusMetres` | Minimum curve radius | What it is | m | 18 to 7000 m | BOStrab 18 m exceptional for street tramway at the low end; UIC 703 gives 4000 m for 300 km/h and the widest built high-speed radii reach about 7000 m |
| `RailAlignment` | `settings.track.maxGradientPerMille` | Ruling gradient | What it is | ‰ | 0 to 60 ‰ | Mainline ruling gradients sit near 15 per mille; BOStrab allows 40 per mille normal and 60 exceptional for a street tramway, which is the steepest adhesion railway in ordinary practice |
| `RailAlignment` | `settings.track.designSpeedKmh` | Design speed | What it is | km/h | 20 to 350 km/h | Street tramway running speeds start near 20 km/h; 350 km/h is the top of built high-speed practice |
| `RailAlignment` | `settings.track.maxAppliedCantMillimetres` | Maximum cant | What it is | mm | 0 to 200 mm | EN 13803: 150 mm is the common standard-gauge maximum and 180 mm is permitted on ballasted high-speed track; 200 mm is above anything in ordinary practice |
| `RailAlignment` | `settings.track.maxCantDeficiencyMillimetres` | Maximum cant deficiency | What it is | mm | 0 to 180 mm | EN 13803: about 110 mm for conventional passenger stock, falling to 100 mm above 200 km/h; up to 180 mm for tilting stock |
| `RailAlignment` | `settings.track.maxCantRateMillimetresPerSecond` | Maximum cant rate | What it is | dimensionless | 20 to 70 | Common practice: 55 mm/s permissible and 35 mm/s desirable for conventional stock, tightened to about 50 mm/s on high-speed line |
| `RailAlignment` | `settings.track.maxCantDeficiencyRateMillimetresPerSecond` | Maximum deficiency rate | What it is | dimensionless | 20 to 70 | Common practice: about 55 mm/s for conventional stock, tightened to 50 mm/s above 200 km/h |
| `RailAlignment` | `settings.track.maxCantGradientMillimetresPerMetre` | Maximum cant gradient | What it is | dimensionless | 0.5 to 4 | Common practice: 2.5 mm/m is 1 in 400, the usual permissible maximum, against 1 in 600 desirable; high-speed line tightens to 2 mm/m |
| `RailAlignment` | `settings.track.trackGaugeMetres` | Track gauge | What it is | m | 0.6 to 1.7 m | 1.435 m is international standard gauge; 0.6 m covers narrow-gauge industrial track and 1.668 m Iberian broad gauge |
| `RailAlignment` | `settings.track.cantFormulaGaugeMillimetres` | Cant formula gauge | What it is | mm | 600 to 1800 mm | 1500 mm for standard gauge: the 1435 mm nominal gauge plus about 65 mm across the two rail heads, which is the rail-centre to rail-centre distance the equilibrium-cant equation is written with |
| `RailAlignment` | `settings.formation.trackCentresMetres` | Track centres | What it looks like | m | 3 to 5 m | 3.0 m for double-track street tramway, 4.0 m conventional mainline, 4.5 m LGV practice |
| `RailAlignment` | `settings.formation.sleeperSpacingMetres` | Sleeper spacing | What it looks like | m | 0.5 to 0.8 m | Standard-gauge practice: sleepers are laid at about 600 to 700 mm centres |
| `RailAlignment` | `settings.formation.sleeperLengthMetres` | Sleeper length | What it looks like | m | 1.5 to 3 m | Standard gauge: 2.6 m mainline concrete or timber sleeper; tramway and narrow-gauge sleepers are shorter |
| `RailAlignment` | `settings.formation.sleeperWidthMetres` | Sleeper width | What it looks like | m | 0.15 to 0.35 m | Standard-gauge practice: about 250 to 300 mm across the sleeper |
| `RailAlignment` | `settings.formation.sleeperHeightMetres` | Sleeper depth | What it looks like | m | 0.12 to 0.3 m | Standard-gauge practice: about 150 to 250 mm deep |
| `RailAlignment` | `settings.formation.ballastShoulderMetres` | Ballast shoulder | What it looks like | m | 0.2 to 0.6 m | Standard-gauge practice: about 300 to 400 mm of shoulder beyond the sleeper end, widened to about 500 mm on high-speed line |
| `RailAlignment` | `settings.formation.ballastDepthMetres` | Ballast depth | What it looks like | m | 0.15 to 0.45 m | Standard-gauge practice: about 250 to 300 mm under the sleeper, deepened to about 350 mm on high-speed line |
| `RailAlignment` | `settings.formation.ballastSideSlopeRun` | Ballast side slope | What it looks like | × | 1 to 2.5 × | Ballast is shaped to about 1 in 1.5, which is near the angle of repose of crushed stone |
| `RailAlignment` | `settings.formation.railHeightMetres` | Rail height | What it looks like | m | 0.1 to 0.2 m | UIC 60 is 172 mm tall; Ri60 grooved tramway rail is 180 mm |
| `RailAlignment` | `settings.formation.railHeadWidthMetres` | Rail head width | What it looks like | m | 0.04 to 0.09 m | UIC 60 head is 72 mm across; Ri60's running head is 56 mm |
| `RailAlignment` | `settings.formation.railFootWidthMetres` | Rail foot width | What it looks like | m | 0.1 to 0.2 m | UIC 60 foot is 150 mm across; Ri60's is 180 mm |
| `RailAlignment` | `settings.formation.loadingGaugeHeightMetres` | Loading gauge height | What it does to the world | m | 3.5 to 7 m | UIC GC is 4.65 m above rail; a container or piggyback envelope reaches about 6.15 m, and a tram envelope is nearer 4.4 m |
| `RailAlignment` | `settings.formation.contactWireHeightMetres` | Contact wire height | What it does to the world | m | 4 to 6.5 m | EN 50119: 5.08 m nominal on high-speed line, about 5.3 m on conventional 25 kV line, and about 5.5 m for a tramway trolley wire over a public road |
| `RailAlignment` | `settings.formation.electricalClearanceMetres` | Electrical clearance | What it does to the world | m | 0.1 to 0.5 m | EN 50119 static air clearance: about 270 mm at 25 kV AC, about 150 mm at 750 V DC |
| `RailAlignment` | `settings.formation.flangewayWidthMetres` | Flangeway width | What it looks like | m | 0.03 to 0.11 m | Level-crossing practice: 65 to 100 mm of flangeway beside a flat-bottom rail; a grooved tramway rail's own groove is about 42 mm |
| `RailAlignment` | `settings.formation.flangewayDepthMetres` | Flangeway depth | What it looks like | m | 0.03 to 0.08 m | Level-crossing practice: about 40 to 50 mm below the rail head; a grooved rail's groove is about 38 mm |
| `RailAlignment` | `settings.formation.crossingMarginMetres` | Crossing margin | What it looks like | m | 0 to 2 m | AUTHORED: the crossing surface is carried a little past the road edge so a vehicle's wheel is on the panel before it reaches the rail |
| `RailAlignment` | `settings.formation.maxEmbankmentMetres` | Maximum embankment | What it does to the world | m | 1 to 20 m | AUTHORED: past a certain height the real answer to a valley is a viaduct rather than more fill; street track sits on almost none |
| `RailAlignment` | `settings.formation.maxCuttingMetres` | Maximum cutting | What it does to the world | m | 1 to 20 m | AUTHORED: past a certain depth the real answer to a hill is a tunnel rather than a deeper cutting |
| `RailAlignment` | `roadSampleSpacingMetres` | Road sample spacing | What it looks like | m | 1 to 25 m | AUTHORED: matched to the railway's own default sampling so neither side of a level crossing is the coarse one |
| `RailAlignment` | `settings.ballastMaterial` | Ballast material | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.sleeperMaterial` | Sleeper material | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.railMaterial` | Rail material | What it looks like | dimensionless | unbounded | no source recorded |
| `RailAlignment` | `settings.crossingMaterial` | Crossing panel material | What it looks like | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.enabled` | Carries a line | What it looks like | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.electrical.conductorName` | Conductor | What it is | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.electrical.spanLengthMetres` | Span length | What it is | m | 80 to 600 m | sub-transmission lattice spans run 200-400 m typically; 80 m is a tight angle span on a corner and 600 m is a long river or valley crossing before it wants a special anchor tower |
| `TransmissionLineRun` | `settings.electrical.sagRatio` | Sag ratio | What it is | fraction | 0.01 to 0.08 fraction | published tabulated sag is 2-5 % of span at everyday temperature; below 1 % is a tension no conductor is strung to and above 8 % is a hot-day or ice-load extreme, not a still-air figure |
| `TransmissionLineRun` | `settings.electrical.shieldWireSagRatio` | Earth-wire sag ratio | What it is | fraction | 0.01 to 0.05 fraction | earth-wire sag is a separate authored cable figure; 1-4 % is the common still-air range for a shield wire, and the shipped 2 % default sits inside it |
| `TransmissionLineRun` | `settings.electrical.minGroundClearanceMetres` | Ground clearance | What it is | m | 5.5 to 12 m | statutory HV clearance to open ground is jurisdictional and voltage-dependent, roughly 6-10 m for sub-transmission; the default is 7.5 m |
| `TransmissionLineRun` | `settings.electrical.angleTowerThresholdDeg` | Angle-tower threshold | What it is | ° | 1 to 30 ° | AUTHORED: below a degree every slight bend would call for a braced angle tower; beyond about 30 degrees even a heavy suspension tower cannot hold the sideways pull, so the choice is moot |
| `TransmissionLineRun` | `settings.phaseCount` | Phases | What it is | dimensionless | 1 to 3 | a single-circuit horizontal tower carries the three phases of one circuit across its one arm; a count of one or two is a partial or split build of that same arm |
| `TransmissionLineRun` | `settings.tower.baseWidthMetres` | Base width | What it looks like | m | 3 to 12 m | a lattice tower's base is roughly a sixth to an eighth of its height; 3-12 m spans a short sub-transmission tower to a tall heavy-angle one |
| `TransmissionLineRun` | `settings.tower.conductorCrossArmHeightMetres` | Cross-arm height | What it looks like | m | 10 to 45 m | sub-transmission cross-arms sit about 15-30 m above the base; 10-45 m covers a short tower to a tall crossing tower |
| `TransmissionLineRun` | `settings.tower.crossArmHalfLengthMetres` | Cross-arm half-length | What it looks like | m | 2 to 10 m | half an arm holding three phases at 4 m spacing with an end margin is about 4.6 m; 2-10 m spans a narrow to a wide horizontal arm |
| `TransmissionLineRun` | `settings.tower.phaseSpacingMetres` | Phase spacing | What it looks like | m | 2 to 8 m | horizontal phase-to-phase spacing is about 3.5-4.5 m at 132 kV to hold the air gap under conductor swing; it widens with voltage |
| `TransmissionLineRun` | `settings.tower.shieldWirePeakAboveCrossArmMetres` | Earth-wire peak | What it looks like | m | 1 to 8 m | the peak has to stand high enough that the line down to the outer phase keeps it inside the roughly 30-degree protective angle; 3.6 m over a 9 m arm does that |
| `TransmissionLineRun` | `settings.tower.suspensionInsulatorLengthMetres` | Suspension insulator | What it looks like | m | 0.8 to 4 m | an I-string of about 9-10 cap-and-pin discs at 146 mm plus end hardware is about 1.5-1.7 m; it lengthens with voltage |
| `TransmissionLineRun` | `settings.tower.strainInsulatorLengthMetres` | Strain insulator | What it looks like | m | 1 to 6 m | a tension string is longer than a suspension one but lies along the pull; about 2-3 m at sub-transmission voltages |
| `TransmissionLineRun` | `settings.tower.legFaceMetres` | Leg face | What it looks like | m | 0.05 to 0.5 m | AUTHORED: the corner leg is the heaviest angle section on the tower, a few tens of millimetres to about half a metre across the face on a large tower |
| `TransmissionLineRun` | `settings.tower.braceFaceMetres` | Brace face | What it looks like | m | 0.03 to 0.3 m | AUTHORED: the bracing is lighter than the legs it ties, typically half to two-thirds their face |
| `TransmissionLineRun` | `settings.tower.conductorDiameterMetres` | Conductor diameter | What it looks like | m | 0.005 to 0.05 m | a medium ACSR such as Lynx is about 19.5 mm; 5-50 mm spans a light conductor to a large high-voltage or bundled one |
| `TransmissionLineRun` | `settings.tower.shieldWireDiameterMetres` | Earth-wire diameter | What it looks like | m | 0.003 to 0.03 m | a 7-strand steel earth wire is about 9.8 mm; an OPGW with fibre inside is a little larger |
| `TransmissionLineRun` | `settings.tower.wireSides` | Wire sides | What it looks like | dimensionless | 3 to 16 | AUTHORED mesh tessellation: three is the leanest tube that still reads as round in silhouette, sixteen is smoother than a cable is ever seen at line distances |
| `TransmissionLineRun` | `settings.tower.shieldWireCount` | Earth wires | What it looks like | dimensionless | 1 to 2 | one earth wire is the ordinary single-circuit case; two are used where a wider protective angle or an OPGW-plus-earth pair is wanted |
| `TransmissionLineRun` | `settings.steelMaterial` | Steel material | What it looks like | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.insulatorMaterial` | Insulator material | What it looks like | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.conductorMaterial` | Conductor material | What it looks like | dimensionless | unbounded | no source recorded |
| `TransmissionLineRun` | `settings.shieldWireMaterial` | Earth-wire material | What it looks like | dimensionless | unbounded | no source recorded |
<!-- fields-table:end -->

The registry currently describes the constrained, value-bearing fields — the ones a wrong number breaks something with.
The full per-field census of all 110 serialised fields lives in `FieldCensus.md`, and RM-102 moves the remaining legacy
inspectors onto this registry, at which point every field an author can edit appears here rather than only these.

## Refusals: every message, and what to do about it

Read [Diagnose a refusal](#diagnose-a-refusal) first for where these appear. This is the reference: the id, the exact
wording, why the tool refuses instead of clamping, and the ways out.

<!-- refusals-table:begin — generated from ManualReferences. Do not edit by hand. -->

| Refusal | What the author sees | Why the tool refuses | How to resolve it |
| --- | --- | --- | --- |
| `BARRIER-RUN-WITHOUT-TERMINAL` | This median barrier run has no terminal at its start. A barrier that stops in open ground is more dangerous than no barrier, so the run was not built. | RM-60 requires a proper end treatment on every barrier run and RM-76 owns run termination in general. A blunt end is a struck-object hazard, which is why this refuses rather than trimming the run. | 1) Extend the run to a point where a terminal fits. 2) Choose an end treatment the run has length for (crash cushion, flared terminal, buried end). 3) Remove the barrier from this median segment and use a trough or planting instead. |
| `GRADE-ABOVE-MAXIMUM` | The profile between these two stations needs a grade above the maximum for this road class. The vertical alignment was not solved. | RM-95 solves grades and vertical curves against the adopted standard's per-class maximum, and `Road.maxGrade` carries the same ceiling on the inspector. A road steeper than its class cannot be driven at the class's design speed, so the tool refuses instead of quietly exceeding it. | 1) Add horizontal distance between the two elevations. 2) Lower the design speed, and with it the class, for this length of road. 3) Cut or fill the terrain so the two ends are closer in height. |
| `GRADE-SEPARATION-NO-APPROACH` | These two roads cross without touching, but there is not enough clear road either side of the crossing to raise one over the other. The crossing was left as it is and flagged. | RM-35 measures the real available approach length on each side before it separates a crossing, because the raise needs a vertical curve at no more than the class's maximum gradient. Building the bridge anyway would put a kink in the road at the abutment. | 1) Move one of the roads so the crossing has more clear approach. 2) Separate the crossing by hand with a bridge whose approach you control. 3) Turn the crossing into an at-grade junction instead. |
| `INTERSECTION-SIGHT-TRIANGLE-BLOCKED` | The sight triangle on this approach is obstructed. The intersection was built and the obstruction reported rather than silently accepted. | RM-97 solves the intersection sight triangle from the adopted standard's leg relations. The tool reports rather than moves an author's object, because what is inside the triangle is the author's content and only the author knows what may be removed. | 1) Move or remove the object inside the triangle. 2) Lower the design speed on the approach, which shortens the legs. 3) Accept the obstruction deliberately and record it, so the report stops being a surprise. |
| `LOT-NOT-BUILDABLE` | This lot fails the buildable-land test on slope, water or an existing footprint. No building was placed on it. | RM-11 tests every lot before RM-12 places anything, so a building never ends up part-submerged or cantilevered off a bank. A rejected lot stays a lot -- it is not deleted -- so the author can see which land the city declined. | 1) Reshape the lot, or let RM-11 reshape it, until it clears the slope threshold. 2) Flatten the ground under it deliberately with a building pad (RM-22). 3) Leave it as open space, which is what a real subdivision does with unbuildable land. |
| `PREFAB-CATEGORY-EMPTY` | Nothing is assigned to this content category, so nothing was placed. The tool does not substitute content you did not choose. | RM-55 is the module's fail-closed rule: an empty category produces a refusal and a count, never a stand-in shape and never the nearest category's content. A stand-in looks like a bug in your art, and you find it late. | 1) Assign prefabs to the category from RM-77's list of what the system needs. 2) Turn the feature off, so the category is not asked for. 3) Point the category at another category's content deliberately, which is a choice and is recorded. |
| `PROVIDER-REQUIRED` | This feature needs a terrain provider connected. Connect one, or use the standalone equivalent named in the availability table. | RM-58 keeps the standalone path fully working and states, in one sentence per feature, what a feature needs. Nothing that needs a provider is allowed to silently do nothing -- that is the failure mode the availability table exists to prevent. | 1) Connect the host's terrain provider to the road system. 2) Use the standalone equivalent the availability table names for this feature. 3) Leave the feature off; the rest of the tool is unaffected. |
| `RADIUS-BELOW-MINIMUM` | This curve's radius is below the minimum for its design speed in the adopted standard. The alignment was not solved through it. | RM-75 solves horizontal alignment from the minimum-radius relation e + f = V^2 / (127 x R), and RM-61 applies the same rule to a roundabout's entry geometry. A curve tighter than the minimum cannot be driven at the speed the class advertises, so the tool refuses instead of banking it further. | 1) Increase the radius of the curve. 2) Lower the design speed, or the road class, for this length of road. 3) Raise the superelevation rate, up to the maximum the standard allows for the class. |
| `SUPERELEVATION-ABOVE-MAXIMUM` | Banking this curve for its design speed needs a cross-slope above the authored maximum rate. The curve was not banked. | RM-40 derives the rate from e + f = V^2 / (127 x R) with the side-friction values of the same edition, and will not exceed the maximum rate the standard sets for the road's environment. Over-banking a curve is unsafe for slow and stopped vehicles, which is why the ceiling is not negotiable. | 1) Increase the radius, which lowers the rate the relation asks for. 2) Lower the design speed for this length of road. 3) Accept the maximum rate and the lower cornering speed that comes with it. |
| `TAPER-NO-APPROACH-LENGTH` | The lane count changes here, but there is not enough road before the junction to run a merge taper. The taper was shortened to the available length and the junction flagged. | RM-27 passes the measured available approach length in rather than assuming one, and reports the ideal against the achieved length. A taper compressed into no distance is a lane that ends abruptly, so the shortening is reported rather than hidden. | 1) Move the lane-count change further back from the junction. 2) Reduce the difference in lane count across the change. 3) Accept the shortened taper knowingly; the junction stays flagged so it is not forgotten. |
| `TERMINUS-UNTREATED` | This road ends in mid-air with no terminus treatment. No end was built. | RM-26 requires a road to end in something real -- a turning head, a stub with a barrier, a fade into an unsealed track. A carriageway that simply stops is the single most visible defect in a generated network, and it is cheap to refuse and expensive to notice later. | 1) Choose a terminus treatment for the end node. 2) Connect the end to another road, so it is a junction rather than an end. 3) Extend the road until it leaves the built area, where an untreated end is not visible. |
| `TUNNEL-COVER-INSUFFICIENT` | There is not enough ground above this tunnel to make a cavity here. No opening was requested and no trench was cut instead. | RM-37 asks the provider for a cavity through the ground rather than cutting a trench, and a cavity with too little cover has no roof. Cutting a trench as a fallback is what the old behaviour did, and it is what this refusal exists to stop. | 1) Lower the road, so there is more ground above it. 2) Raise the terrain over the alignment deliberately. 3) Build the crossing as a cutting, or as a bridge over the obstacle, instead of a tunnel. |

<!-- refusals-table:end -->

## Cited standards, with the edition used

Nothing in this tool's geometry is invented. This table is how you check that without having been in the design
conversations: the feature, the quantity taken from the source, the source, and the edition it was read from.

Two rows carry a real edition, because § 10 names those documents outright. The rest point at "the standard the project
adopts", and that adoption is still an open decision — **Q-13** for the geometric design standard, **Q-14** for the
marking standard. Those rows say `NOT ADOPTED` rather than naming a plausible edition, because a wrong edition is the one
documentation defect an auditor can catch and nobody else can. Where a candidate is named, it is labelled a candidate: no
quantity in the shipped tool is read from it until the decision is made.

In this table `V^2` means V squared, `x` a multiplication, and `+/-` a plus-or-minus.

<!-- standards-table:begin — generated from ManualReferences. Do not edit by hand. -->

| Task | Feature | Quantity taken from it | Source | Edition used |
| --- | --- | --- | --- | --- |
| RM-28 | Roundabout as a junction type | Every geometric quantity: inscribed circle diameter, entry and exit radii, splitter island, circulatory width | NCHRP Report 672, Roundabouts: An Informational Guide (AASHTO-aligned) | NCHRP Report 672 (2010), the second edition of the informational guide |
| RM-30 | Culverts and roadside ditches | Culvert barrel sizing; Manning's equation for open-channel flow in the ditch | FHWA Hydraulic Design Series No. 5, Hydraulic Design of Highway Culverts (HDS-5) | HDS-5 third edition, FHWA-HIF-12-026 (2012) |
| RM-32 | Pavement markings | Dash lengths, gap ratios and colours | The marking standard the author selects: MUTCD (US and Canada), UK Traffic Signs Manual Chapter 5, or the Vienna Convention family | NOT ADOPTED -- Q-14 (marking standard) is open |
| RM-40 | Superelevation through curves | e + f = V^2 / (127 x R) with e the cross-slope as a decimal, f the side-friction factor, V in km/h, R in metres; and the side-friction values of the same edition | Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-41 | Unsealed roads as a surface type | The crown figure for an unsealed road, which is meaningfully steeper than a sealed road's crown | The low-volume or gravel-road manual the project adopts | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-43 | Transport hubs: the airport case | Approach and obstacle-limitation surfaces, as published slope-and-dimension surfaces | The aerodrome standard the project adopts (candidate: ICAO Annex 14, Volume I, Aerodrome Design and Operations) | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-61 | Shape-flexible roundabout | Inscribed circle diameter and entry geometry, re-solved for a non-circular circulatory carriageway | The roundabout design guide the project adopts (NCHRP Report 672, as RM-28) | NCHRP Report 672 (2010), the second edition of the informational guide |
| RM-74 | Suitability and corridor map | Weighted-overlay suitability analysis, least-cost path and cost-distance corridor analysis, and the corridor-selection stage of highway route location | Established GIS and landscape-planning methods (a method, not a numbered standard); the route-location stage belongs to the adopted geometric design standard | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-75 | Horizontal alignment | The tangent-curve-transition model, and the minimum-radius relation e + f = V^2 / (127 x R) solved for R | Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-95 | Vertical alignment | L = K x A; the per-design-speed K values for crests and sags; the per-class maximum grades; and SSD = 0.278 x V x t + V^2 / (254 x (f +/- G)) | Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-96 | Carriageway cross-section | Normal cross-fall by surface type, the maximum relative gradient governing superelevation runoff, the runoff and runout length relations, and the curve-widening relation | Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets | NOT ADOPTED -- Q-13 (geometric design standard) is open |
| RM-97 | Intersection geometry | Design-vehicle dimensions and minimum turning radii, the corner-radius selection relation, turn-lane length and taper relations, and the sight-triangle leg relations | Candidate: AASHTO, A Policy on Geometric Design of Highways and Streets | NOT ADOPTED -- Q-13 (geometric design standard) is open |

<!-- standards-table:end -->

Twelve rows for the twelve `Cite` lines in the module's own § 10. The count is asserted, so a thirteenth cite added later
without a row here fails the build rather than quietly escaping the audit.

## The menu: what is in it, and where

Every command this tool adds lives under one root, **Window ▸ Road & City Master**, in the submenus below. The counts are
**reflected from the `[MenuItem]` attributes themselves**, not counted by hand: a path spelled as a literal, as a
`const string`, or as a const built from another const all count identically, and the number changes the moment a new
command compiles. Five of the paths are declared as a `const string` and referenced by name, which is why a hand count of
this tree is short by construction — and why two sections of an earlier manual disagreed about the total.

`Testing` holds one item per gate, and **every gate has its own item**, so any single gate can be run alone instead of
behind the whole sweep. That claim is asserted rather than stated: `RCMMANUAL case12` fails if a gate is added without
one.

<!-- menus-table:begin — generated by reflecting over every [MenuItem] in the tool's two assemblies. Do not edit by hand. -->

| Submenu | Items |
| --- | --- |
| (top level) | 6 |
| Documentation | 8 |
| Licensing | 2 |
| Setup | 6 |
| Testing | 105 |
| **Total** | **127** |
<!-- menus-table:end -->

## Licence, attribution and the rename

This tool is a **fork of an MIT-licensed open-source Unity asset**, vendored into Universe Creator and renamed. Getting
this section wrong is the one documentation defect with consequences outside the project, so it is stated plainly.

**The original project.** *Road Architect* for Unity, by **MicroGSD LLC**, open-sourced by its creators and later
maintained as a community fork. Original repository: <https://github.com/MicroGSD/RoadArchitect>. Community fork this
tree derives from: <https://github.com/FritzsHero/RoadArchitect>.

**The licence.** MIT License, `Copyright (c) 2018 MicroGSD LLC`. The full text is in `LICENSE.md` at the tool root and it
is unchanged. MIT requires that the copyright notice and the permission notice be included in all copies or substantial
portions of the software, and that requirement travels with this fork: `LICENSE.md` ships, and it is not to be removed,
shortened or re-headed. MIT permits the fork, the rename, the modifications and the redistribution — what it does **not**
permit is dropping the notice.

**The rename.** The folder, both assembly definitions and the namespaces are `RoadCityMaster`; the display name in menus,
window titles and inspector headers is **Road & City Master**. The rename is a rename of *this fork*, not a claim on the
original work: the original project's name still identifies the original project, and the attribution above is where it
belongs. Universe Creator's own scripts carry **no** third-party product name at all, and the tool's own scripts carry
only the display name, from one constant.

**What this covers.** The C# in this tree, the shaders, and the documentation. The included art — meshes, textures,
materials, prefabs — came with the original asset under the same licence; anything added since is the project's own. The
per-file attribution that survives refactoring, and the re-test of the licence position once the legacy files have been
rewritten, are RM-80 and RM-106.

---

*Generated tables in this document are spliced from the tool's own registers. To regenerate them:*
**Window ▸ Road City Master ▸ Documentation ▸ Write the manual's generated tables.**
*If the file and the registers disagree, `RCMMANUAL` fails the build.*
