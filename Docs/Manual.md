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
while writing the fourteenth inspector.

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
| `Road` | `centerDivider` | Median type | What it is | dimensionless | 0 to 4 | AUTHORED: none, concrete barrier, barrier with glare screens, wire rope, or painted markers |
| `Road` | `medianWidth` | Median width | What it is | m | 0 to 30 m | AUTHORED ceiling: 0 is no physical median and rural depressed medians reach ~30 m. What a median of a given width can actually HOLD is not decided here -- MedianContent's cited band table owns that, from painted flush below 1.2 m up to trough-and-planting at 15 m |
| `Road` | `speedLimitKPH` | Design speed | What it is | km/h | 5 to 140 km/h | AUTHORED bounds spanning real posted limits, from ~5 km/h shared space to the 140 km/h of the fastest posted motorways; 0 or less falls back to the road class default |
| `Road` | `shoulderWidth` | Shoulder width | What it looks like | m | 0 to 4 m | AUTHORED: shoulder 0.6–3.0 m typical (AASHTO); bound raised for wide verges |
| `Road` | `maxGrade` | Max grade | What it does to the world | frac | 0 to 0.15 frac | AASHTO max grade 6–12% by terrain and design speed; 15% hard ceiling |
| `Road` | `matchTerrainSubtraction` | Terrain match drop | What it does to the world | m | 0 to 2 m | AUTHORED: vertical offset the carriageway sinks into matched terrain |
| `Road` | `roadDefinition` | Segment resolution | What is generated from it | m | 1 to 20 m | AUTHORED: metres per generated segment; lower is finer mesh at higher cost |
| `RoadTerrain` | `splatWidth` | Splat paint half-width | What it does to the world | m | 1 to 60 m | AUTHORED: paint distance from carriageway; RM-38 flags the 30 m default as over-wide |
| `RoadTerrain` | `splatResoWidth` | Splat resolution W | What it looks like | dimensionless | 512 to 4096 | AUTHORED: power-of-two splat map resolution |
| `RoadTerrain` | `splatResoHeight` | Splat resolution H | What it looks like | dimensionless | 512 to 4096 | AUTHORED: power-of-two splat map resolution |
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
