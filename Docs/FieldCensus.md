# Field Census — the serialised authoring surface of Road & City Master

> **RM-50 deliverable.** A field inventory covering every serialised field on every runtime component the
> tool's inspectors expose: its declared type, its unit, its valid range, and whether it is shown to the
> author today. This document is the input to RM-51 (the inspector rebuild), RM-52 (the status panel), RM-64
> (the consistency pass) and RM-65 (the documentation pass). It is a companion to `ScanFindings.md` (RM-49);
> where the scan answered *what each file is*, this answers *what each field is*.
>
> **Scope.** The runtime components an author edits through an inspector: `RoadSystem`, `Road`, the spline
> carrier `SplineC` and its node `SplineN`, the preview carriers `SplineF`/`SplineI`, the junction
> `RoadIntersection`, and the two `[Serializable]` per-node makers drawn inline in the node inspector
> (`SplinatedMeshMaker`, `EdgeObjectMaker`), plus `RoadTerrain`. Pure-logic classes with no serialised
> authoring surface (`Construction*`, `RoadCreationT`, the threaded jobs, `RootUtils`) are out of scope — they
> hold no author-entered value to preserve. The new `City/Runtime` types are Module-6 code with no shipped
> pre-overhaul inspector to protect and are censused as they gain one.

## Why this census exists — the non-breaking contract

The risk in an inspector rewrite is not the UI, it is the serialisation underneath it. **Unity binds
serialised data by field name.** Rename a field and the author's authored value does not move to the new
name — it silently reverts to the type default, with no error in any log. A scene that built a motorway
yesterday opens tomorrow with its design speed back at zero and nobody is told. So the contract for the whole
§ 10.20 overhaul is stated here and gated at RM-50:

1. **Presentation may change freely.** Labels, grouping into foldouts, units, tooltips, inline validation,
   ordering — all of that is cosmetic to the serialised data and is the entire point of the overhaul.
2. **A field's name changes only with a migration.** Either a `[FormerlySerializedAs("oldName")]` attribute
   carrying the old name forward, or an explicit `ISerializationCallbackReceiver` / editor migration step.
   **Never a bare rename.** This census is the list of names that must be preserved.
3. **A field is never dropped to tidy the UI.** A serialised-but-unshown field is *surfaced and labelled*
   (RM-66's dead-code-is-wired-not-deleted rule), not removed. Removing a `[SerializeField]` drops the
   author's value exactly as a rename does.

**The gate's data half:** a scene saved before the overhaul and re-saved after it, with no author edits in
between, diffs clean — no changed values, no dropped properties. That scene-level diff is verified when the
overhaul lands (RM-51+); this document is the field-name manifest it is checked against. **The gate's naming
half:** every `[SerializeField]` and public instance field on the components above appears in a table below.

## How to read the tables

Each component is one table. Rows are in **declaration order** (the order Unity's default inspector draws
them, and the order the field appears in the `.unity`/`.prefab` YAML — which is what makes a diff legible).

| Column | Meaning |
|---|---|
| **Field** | The exact serialised identifier. This is the load-bearing string — the migration contract is about this column. |
| **Type** | The declared C# type, verbatim. |
| **Unit** | The real-world unit the value carries (C-15). `m` metres, `°` degrees, `m/s`→`km/h` speed, `%` gradient, `×` unitless ratio/multiplier, `frac` 0–1 fraction, `m²` area, `/km` count per kilometre, `idx` enum/index, `ref` object reference, `—` structural/no unit. **AUTHORED** where the unit is inferred from field name + usage rather than a written annotation in the source (the source annotates almost none). |
| **Range** | The valid range. A `[Range]` attribute is quoted as *attr*; a physically/standards-bounded range is given with its basis and marked **AUTHORED**; `—` where genuinely unbounded. |
| **Shown** | Whether an author sees it today. `Y` default-drawn (public/`[SerializeField]`, not hidden); `hidden` `[HideInInspector]`; `cache` serialised internal state not meant for authoring; `verify` drawn only if the custom editor emits it (confirmed against the editor file in RM-51). |

Where a field's unit or range is not written in the vendored source, the value here is the real-world-grounded
default this census adopts (C-15 / §10.18): it is marked **AUTHORED** so a reviewer sees it was a judgement,
not a reading. RM-51's tooltips and RM-65's documentation draw from these columns.

### `RoadSystem` (scene-wide build controller)

`RoadSystem` is the one-per-scene parent; its four fields are the global build switches every road obeys.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| isMultithreaded | bool | — | — | Y |
| isSavingMeshes | bool | — | — | Y |
| isAllowingRoadUpdates | bool | — | — | Y |
| editorPlayCamera | Camera | ref | — | verify — editor play-preview camera ref |

### `Road` (the per-road component)

103 public/serialised fields, by far the largest surface. They fall into five bands; the band is what RM-51's
foldouts will become. Fields marked `runtime` are `[System.NonSerialized]` — held only in play/editor state,
never written to the scene, so they carry no author value and are **outside** the migration contract (listed
here only because the gate says name every public field). `cache` fields ARE serialised but are derived/wiring
state, not author input — hidden, with reasons in the judgement list.

**Band 1 — generated wiring & identity (not author-entered).**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| MainMeshes | GameObject | ref | — | cache — generated child root |
| MeshRoad | GameObject | ref | — | cache — generated child |
| MeshShoR | GameObject | ref | — | cache — generated child |
| MeshShoL | GameObject | ref | — | cache — generated child |
| MeshiLanes | GameObject | ref | — | cache — generated child |
| MeshiLanes0 | GameObject | ref | — | cache — generated child |
| MeshiLanes1 | GameObject | ref | — | cache — generated child |
| MeshiLanes2 | GameObject | ref | — | cache — generated child |
| MeshiLanes3 | GameObject | ref | — | cache — generated child |
| MeshiMainPlates | GameObject | ref | — | cache — generated child |
| MeshiMarkerPlates | GameObject | ref | — | cache — generated child |
| editorTitleString | string | — | — | runtime |
| spline | SplineC | ref | — | cache — carrier, auto-wired at creation |
| MostRecentNodeCount | int | count | ≥ -1 | cache — change-detection |
| lastBuildUtcTicks | long | UTC ticks | ≥ 0; 0 means never built | cache — build stamp, written only by `ConstructionCleanup` (the one point every completing build reaches and no cancelled one does), read by the measurements foldout through `LastBuildLocalText()`. Not editable: it is an output of a build |
| splineObject | GameObject | ref | — | cache — carrier host |
| roadSystem | RoadSystem | ref | — | cache — parent, auto-wired |
| PiggyBacks | SplineC[] | ref | — | cache — attached secondary splines |
| isEditorProgressBar | bool | — | — | runtime |
| UID | string | — | — | cache — stable identity, not editable |
| TerrainHistory | List&lt;TerrainHistoryMaker&gt; | — | — | cache — terraform undo record |
| TerrainHistoryByteSize | string | — | — | cache — size read-out |
| isUpdatingSpline | bool | — | — | runtime |

**Band 2 — road geometry authoring (the fields touched on every road).** Units/ranges here are **AUTHORED**
(C-15): the source annotates none, so the range is the real-world envelope with its basis. RM-51 attaches
these as tooltips and inline limits.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| laneWidth | float | m | 2.5–5.0 m — **AUTHORED**, lane-width envelope (highway lane 2.7–3.75 m per AASHTO; upper bound widened for shared/rural lanes). This is the bound the inspector actually enforces — `InspectorFieldRegistry` is the machine-readable form of this row | Y |
| isShouldersEnabled | bool | — | — | Y |
| shoulderWidth | float | m | 0.0–4.0 m — **AUTHORED** authoring bound (0 means no shoulder at all; widened past the table for verges). The cited figures are per class in `City.RoadClassDefaults` and form TWO tiers with nothing in between: a 0.6–1.2 m edge strip (AASHTO rural collector/local 0.6–2.4 m; shoulders normally omitted on a curbed urban street) and a 3.0 m stopped-vehicle refuge (AASHTO minimum usable shoulder — the 2.44 m design vehicle plus a margin each side). RM-110/B-10 removed the half-widths (1.5, 2.0, 2.5 m): under ~3 m a stopped vehicle still overhangs the lane, and near half the lane width the strip is driven as a lane. Leaving the cited band is reported by the class-change override report, not clamped | Y |
| shoulderCrossSlope | float | fraction | 0.02–0.06 — **CITED**, AASHTO's shoulder cross-slope band, 0.04 the normal figure for a paved bituminous shoulder. The band is not written into the registry row: it reads `CarriagewaySection.ShoulderCrossSlopeMinimum/Maximum/Source`, the same constants `ShoulderFallMetres(width, crossSlope)` clamps to, so the range the inspector shows and the range the geometry enforces cannot differ. 0 or negative means the normal figure, not a flat shoulder, so a road serialised before RM-110 builds unchanged | Y |
| lanesForward | int | lanes | ≥1 — **AUTHORED**, a carriageway has at least one lane. Was `laneAmount` (directionless); RM-89 kept the serialised data via `FormerlySerializedAs`, so an old road's TOTAL arrives here and is split once on load | Y |
| lanesBackward | int | lanes | ≥−1 — **AUTHORED**, 0 on a one-way; −1 is the sentinel meaning "not yet split" and is replaced on load. **0 on a two-way is refused by a cross-field rule, not by this range** | Y |
| isOneWay | bool | — | — . Decides whether `lanesBackward = 0` is correct or an error | Y |
| roadClass | RoadClassEnum | idx | one of 13 declared design classes; each carries its own lane count, lane width, design speed and max grade with sources in `RoadClassDefaults` | Y |
| appliedRoadClass | RoadClassEnum | idx | same 13 — records which class's defaults were last written, so a later class change refreshes only fields left at the outgoing default | **N** (`HideInInspector`; author-editable would corrupt the merge) |
| settlementRole | SettlementRoleEnum | idx | none, main highway, intercities, intercities connecting towns, intercities connecting villages — **AUTHORED**, the orders of place a hand-drawn network connects. A THIRD axis, deliberately not merged with either of the others: `roadClass` above is the cross-section, `City.RoadClass` is the functional class of a street inside a settlement, and this is what the road is BETWEEN settlements. Read only by the siting pass, which is why `None` leaves a road built exactly as before | Y (picked from a list, never typed) |
| centerDivider | CenterDividerTypeEnum | idx | none, concrete barrier, barrier with glare screens, wire rope, painted markers. Only the physical types make `medianWidth` occupy section width | Y |
| medianWidth | float | m | 0–30 m — **AUTHORED** ceiling; 0 is no physical median, rural depressed medians reach ~30 m. What a median of a given width can HOLD is owned by `MedianContent`'s cited band table (painted flush below 1.2 m, kerbed island from 1.2 m, barrier from 2.4 m, trough from 9 m, trough-and-planting from 15 m), not by this range | Y |
| speedLimitKPH | float | km/h | 5–140 — **AUTHORED** bounds spanning real posted limits (shared space to the fastest posted motorway). Doubles as the DESIGN speed every taper length is computed from; ≤0 falls back to the class default. **A value above the class's own design speed plus one 20 km/h increment is refused by a cross-field rule** | Y |
| auxLanes | List&lt;AuxiliaryLaneAuthored&gt; | — | speed-change lanes, climbing lanes, turn bays and permanent widenings, each between two distances along the spline. Empty on a road of constant cross-section | Y (list) |
| isGeneratingRoadMesh | bool | — | — . False on the rail/ropeway/footway carrier classes: spline and edge objects, no carriageway | Y |
| roadDefinition | float | m | 1–20 m — **AUTHORED**, cross-section spacing along the spline (mesh resolution) | Y |
| roadCornerDefinition | bool | — | — | Y |
| isRoadCutsEnabled | bool | — | — | Y |
| isShoulderCutsEnabled | bool | — | — | Y |
| isDynamicCutsEnabled | bool | — | — | Y |
| isMaxGradeEnabled | bool | — | — | Y |
| maxGrade | float | frac (=%) | 0–0.15 — **AUTHORED**, 0.08 = 8 %; motorway ≤6 %, mountain roads to ~12–15 % | Y |
| isUsingDefaultMaterials | bool | — | — | Y |
| isAutoUpdatingInEditor | bool | — | — | Y |

**Band 3 — what the road does to the terrain.** Ranges **AUTHORED** from the terraform envelope.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| matchTerrainSubtraction | float | m | 0–2 m — **AUTHORED**, vertical seat offset the carriageway sinks into matched terrain | Y |
| isRaisingRoad | bool | — | — | Y |
| matchHeightsDistance | float | m | 0–200 m — **AUTHORED**, height-blend half-width (source comment: "width of height modification in meters") | Y |
| terrainFootprint.shoulderMetres | float | m | 0–3 m — **AUTHORED**, flat verge held beyond the outer edge of the built cross-section before the side slope breaks; graded shoulder exceeds usable shoulder and AASHTO's Roadside Design Guide rounds the break of slope, 0.6 m being ordinary berm practice; default 0.5 m. Nested in `City.TerrainFootprintClass`, registered by its dotted path. NOT `shoulderWidth`, which is emitted mesh | Y |
| batterRunPerRise | float | ×(run:rise) | 1–6 — **AUTHORED**, 1:1 rock cut to 6:1 recoverable embankment; AASHTO Roadside Design Guide recoverable ≤4:1, traversable to 3:1, critical steeper; `City.CutSlopeClass` carries 1.5:1 | Y |
| batterMaxReachMetres | float | m | 0–60 m — **AUTHORED** ceiling each side, the same bound the splat paint half-width carries; capped again at run time by half of `matchHeightsDistance`, which is what sizes the terraform write buffers | Y |
| clearDetailsDistance | float | m | 0–100 m — **AUTHORED**, detail-clear half-width | Y |
| clearDetailsDistanceHeight | float | m | 0–50 m — **AUTHORED**, vertical band for detail clear | Y |
| clearTreesDistance | float | m | 0–100 m — **AUTHORED**, tree-clear half-width | Y |
| clearTreesDistanceHeight | float | m | 0–100 m — **AUTHORED**, vertical band for tree clear | Y |
| isHeightModificationEnabled | bool | — | — | Y |
| isDetailModificationEnabled | bool | — | — | Y |
| isTreeModificationEnabled | bool | — | — | Y |
| isSavingTerrainHistoryOnDisk | bool | — | — | Y |
| magnitudeThreshold | float | m | 50–1000 m — **AUTHORED**, segment-magnitude cut-off for the terraform pass | Y |
| isGizmosEnabled | bool | — | — | Y |
| isUsingMultithreading | bool | — | — | Y |
| isSavingMeshes | bool | — | — | Y |
| isUsingMeshColliders | bool | — | — | Y |
| isStatic | bool | — | — | Y |
| isLightmapped | bool | — | — | Y |
| desiredRampHeight | float | m | 0–2 m — **AUTHORED**, ramp/kerb rise | Y |

**Band 4 — materials & physics (appearance/surface refs).** `tRoadMaterialDropdownOLD` is a migration shadow
of the dropdown and is hidden. The 16 material slots and 2 physic materials are references, no unit.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| roadMaterialDropdown | RoadMaterialDropdownEnum | idx | enum | Y |
| tRoadMaterialDropdownOLD | RoadMaterialDropdownEnum | idx | enum | cache — old-value shadow for change-detect |
| RoadMaterial1 … RoadMaterial4 | Material | ref | — | Y |
| RoadMaterialMarker1 … RoadMaterialMarker4 | Material | ref | — | Y |
| ShoulderMaterial1 … ShoulderMaterial4 | Material | ref | — | Y |
| ShoulderMaterialMarker1 … ShoulderMaterialMarker4 | Material | ref | — | Y |
| RoadPhysicMaterial | PhysicsMaterial / PhysicMaterial | ref | — | Y — type switches on `UNITY_6000_0_OR_NEWER` |
| ShoulderPhysicMaterial | PhysicsMaterial / PhysicMaterial | ref | — | Y — type switches on `UNITY_6000_0_OR_NEWER` |

*(The four numbered material families are collapsed one-per-row above for legibility; each is four distinct
serialised slots — `1`,`2`,`3`,`4` — and all four names must be preserved. 16 material fields + 2 physic = 18.)*

**Band 5 — construction/editor/runtime state (not author data).** `runtime` = `[System.NonSerialized]`
(outside the contract); `cache` = serialised derived state (hidden). The three gizmo colours and the two
editor-camera scalars are the only author-facing fields in this band.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| TerrainCalcsJob / RoadCalcsJob1 / RoadCalcsJob2 / RCS | Threading jobs, buffer | — | — | runtime |
| roadName | string | — | — | cache — display name |
| isProfiling | bool | — | — | Y — diagnostics toggle |
| isSkippingStore | bool | — | — | Y — diagnostics toggle |
| EditorConstructionStartTime | float | s | — | runtime |
| isEditorError | bool | — | — | cache |
| exceptionError | System.Exception | — | — | runtime |
| editorProgress | int | % | 0–100 | runtime |
| isUpdateRequired | bool | — | — | cache |
| isTriggeringGC | bool | — | — | cache |
| isEditorCameraMoving | bool | — | — | runtime |
| EditorCameraPos | float | — | — | runtime |
| EditorCameraMetersPerSecond | float | m/s | 1–200 m/s — **AUTHORED**, editor fly-cam speed | Y |
| isEditorCameraRotated | bool | — | — | Y |
| editorCameraOffset | Vector3 | m | — | runtime |
| editorPlayCamera | Camera | ref | — | runtime |
| EditorTTDList | List&lt;TempTerrainData&gt; | — | — | cache — in-flight terraform data |
| isEditorConstructing | bool | — | — | cache |
| editorConstructionID | int | idx | — | cache |
| isEditorSelected | bool | — | — | cache |
| isEditorMouseHittingTerrain | bool | — | — | cache |
| editorMousePos | Vector3 | m | — | cache |
| defaultNodeColor | Color | rgba | — | Y — gizmo colour |
| selectedColor | Color | rgba | — | Y — gizmo colour |
| newNodePreviewColor | Color | rgba | — | Y — gizmo colour |
| EditorCameraTimer | float | s | — | runtime |

### `SplineC` (the spline carrier)

**Census finding: `SplineC` has no author-facing serialised field.** All 32 are either the node structure, the
derived path cache, or navigation/connection data copied from `Road` and the intersection graph. An author
never types into `SplineC` directly — they edit `Road` and the nodes, and `SplineC` is regenerated. Every row
is therefore `cache`; the migration contract still protects the names (a rename still drops the value on load),
but none needs a label in RM-51. This is also where the closed-loop finding lives: **no `isClosed` field
exists here** (only the hardcoded local at `SplineC.cs:350`), which is why RM-28/61 must add one.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| nodes | List&lt;SplineN&gt; | ref | — | cache — node structure |
| splineRoot | GameObject | ref | — | cache |
| road | Road | ref | — | cache — back-reference |
| distance | float | m | ≥ -1 (−1 = dirty) | cache — total path length |
| cachedPoints | Vector3[] | m | — | cache — sampled path |
| previewSpline | SplineF | ref | — | cache |
| previewSplineInsert | SplineI | ref | — | cache |
| RoadWidth | float | m | — | cache — nav copy of road width |
| Lanes | int | lanes | — | cache — nav copy |
| connectedIDs | List&lt;int&gt; | idx | — | cache — nav graph |
| id | int | idx | — | cache |
| uID | string | — | — | cache — identity |
| BridgeParams | List&lt;KeyValuePair&lt;float,float&gt;&gt; | m,m | — | cache — bridge station spans |
| TunnelParams | List&lt;KeyValuePair&lt;float,float&gt;&gt; | m,m | — | cache — tunnel station spans |
| HeightHistory | List&lt;KeyValuePair&lt;float,float&gt;&gt; | m,m | — | cache |
| RoadDefKeysArray | int[] | idx | — | cache |
| RoadDefValuesArray | float[] | m | — | cache |
| editorOnlyLastNodeTimeSinceStartup | double | s | — | runtime |
| isSpecialStartControlNode | bool | — | — | cache — connection state |
| isSpecialEndControlNode | bool | — | — | cache — connection state |
| isSpecialEndNodeIsStartDelay | bool | — | — | cache |
| isSpecialEndNodeIsEndDelay | bool | — | — | cache |
| specialEndNodeDelayStart | float | m | 0–50 m — **AUTHORED** | cache |
| specialEndNodeDelayStartResult | float | m | — | cache |
| specialEndNodeDelayEnd | float | m | 0–50 m — **AUTHORED** | cache |
| specialEndNodeDelayEndResult | float | m | — | cache |
| specialEndNodeStartOtherSpline | SplineC | ref | — | cache |
| specialEndNodeEndOtherSpline | SplineC | ref | — | cache |
| RoadV0 … RoadV3 | Vector2 | m | — | cache — nav corner vertices (4 fields) |

### `SplineN` (the per-node component)

60 fields. Unlike `SplineC`, a handful of these ARE the author's per-node controls — node position, ease,
splination window, the per-node bridge/tunnel marks, and the two lists that hold the node's edge objects and
extruded meshes. The rest are derived geometry, connection graph, and generated-object wiring.

**Author-facing node controls.**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| pos | Vector3 | m | — | Y — node world position (RM-16 scene handle targets this) |
| easeIO | Vector2 | frac | 0–1 each — **AUTHORED**, ease in/out | Y |
| minSplination | float | frac | 0–1 — **AUTHORED**, splination window start | Y |
| maxSplination | float | frac | 0–1 — **AUTHORED**, splination window end | Y |
| isNeverIntersect | bool | — | — | Y — nav control |
| isIgnore | bool | — | — | Y — skip node in build |
| isTunnel | bool | — | — | Y — mark node as tunnel |
| isBridge | bool | — | — | Y — mark node as bridge |
| EdgeObjects | List&lt;EdgeObjectMaker&gt; | ref | — | Y — per-node edge objects (railings/signs), drawn inline |
| SplinatedObjects | List&lt;SplinatedMeshMaker&gt; | ref | — | Y — per-node extruded meshes, drawn inline |

**Derived geometry, graph & wiring (cache/runtime — not author input).**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| rot | Quaternion | — | — | cache — derived orientation |
| tangent | Vector3 | — | — | cache |
| dist | float | m | — | cache — along-spline distance |
| time | float | frac | 0–1 | cache |
| nextTime | float | frac | 0–1 | cache |
| nextTan | Vector3 | — | — | cache |
| oldTime | float | frac | — | cache |
| tempSegmentTime | float | frac | — | cache |
| isSpecialEndNode | bool | — | — | cache |
| specialNodeCounterpart | SplineN | ref | — | cache |
| specialNodeCounterpartMaster | SplineN | ref | — | cache |
| originalConnectionNodes | SplineN[] | ref | — | cache |
| isSpecialEndNodeIsStart | bool | — | — | cache |
| isSpecialEndNodeIsEnd | bool | — | — | cache |
| isSpecialIntersection | bool | — | — | cache |
| isSpecialRoadConnPrimary | bool | — | — | cache |
| isRoadCut | bool | — | — | cache |
| idOnSpline | int | idx | — | cache |
| spline | SplineC | ref | — | cache |
| uID | string | — | — | cache — identity |
| intersectionOtherNode | SplineN | ref | — | cache |
| gradeToNext / gradeToPrev | string | — | — | cache — grade read-out text |
| gradeToNextValue / gradeToPrevValue | float | % | — | cache — derived grade |
| initialRoadHeight | float | m | — | cache |
| isIntersection | bool | — | — | cache |
| isEndPoint | bool | — | — | cache |
| id | int | idx | — | cache |
| intersectionOtherNodeID | int | idx | — | cache |
| connectedID | List&lt;int&gt; | idx | — | cache — prev/next ids |
| connectedNode | List&lt;SplineN&gt; | ref | — | cache — prev/next nodes |
| isTunnelStart / isTunnelEnd / isTunnelMatched | bool | — | — | cache — derived tunnel pairing |
| tunnelCounterpartNode | SplineN | ref | — | cache |
| isBridgeStart / isBridgeEnd / isBridgeMatched | bool | — | — | cache — derived bridge pairing |
| bridgeCounterpartNode | SplineN | ref | — | cache |
| intersection | RoadIntersection | ref | — | cache |
| intersectionConstruction | iConstructionMaker | ref | — | cache |
| tTriList | List&lt;Construction3DTri&gt; | — | — | cache — gizmo geometry |
| tHMList | List&lt;Vector3&gt; | m | — | cache — gizmo geometry |
| roadCutWorld / shoulderCutRWorld / shoulderCutLWorld | GameObject | ref | — | cache — generated cut objects |
| roadCutMarker / shoulderCutRMarker / shoulderCutLMarker | GameObject | ref | — | cache — generated cut markers |

### `SplineF` / `SplineI` (editor preview carriers)

These two hold the transient add-node / insert-node preview while the author is drawing. They are editor
scaffolding: their only author-facing field is the gizmo-draw toggle. **Note for RM-28/61:** both carry an
`isClosed` field, but `SplineC` — the real carrier — does not; the closed flag lives only on the *preview*
today and is dropped when the preview is committed.

| Component | Field | Type | Unit | Range | Shown |
|---|---|---|---|---|---|
| SplineF | nodes | List&lt;SplinePreviewNode&gt; | ref | — | cache |
| SplineF | isClosed | bool | — | — | cache — preview-only closed flag |
| SplineF | distance | float | m | ≥ -1 | cache |
| SplineF | mousePos | Vector3 | m | — | runtime |
| SplineF | spline | SplineC | ref | — | cache |
| SplineF | isDrawingGizmos | bool | — | — | Y — gizmo toggle |
| SplineI | nodes | List&lt;SplinePreviewNode&gt; | ref | — | cache |
| SplineI | isClosed | bool | — | — | cache — preview-only closed flag |
| SplineI | distance | float | m | ≥ -1 | cache |
| SplineI | mousePos | Vector3 | m | — | runtime |
| SplineI | spline | SplineC | ref | — | cache |
| SplineI | actionNode | SplinePreviewNode | ref | — | cache |
| SplineI | isDrawingGizmos | bool | — | — | Y — gizmo toggle |

### `RoadIntersection` (the junction component)

110 fields — behaviour toggles, three families of material slots, a large derived corner-geometry block, and
the traffic-light settings. The `…` rows collapse a regular numbered/suffixed series (e.g. `1/2/3`, or the
four corners `RR/RL/LR/LL`); every suffix in the stated range is a distinct serialised field whose name is
preserved. Derived corner geometry is `cache`; the toggles, type dropdowns, material slots and light settings
are the author surface.

**Behaviour toggles, identity & type.**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| node1 / node2 | SplineN | ref | — | cache — the two roads meeting |
| node1uID / node2uID | string | — | — | cache |
| intersectionName | string | — | — | cache — identity |
| isSameSpline | bool | — | — | cache |
| isDrawingGizmo | bool | — | — | Y |
| isFlipped | bool | — | — | Y |
| isUsingDefaultMaterials | bool | — | — | Y |
| isAutoUpdatingIntersection | bool | — | — | Y |
| isNode2BLeftTurnLane / …BRightTurnLane / …FLeftTurnLane / …FRightTurnLane | bool | — | — | Y — turn-lane toggles (4) |
| isFirstSpecialFirst / …Last / isSecondSpecialFirst / …Last | bool | — | — | cache — connection state (4) |
| isCornerRR1Enabled … isCornerLL2Enabled | bool | — | — | Y — corner-enable toggles (8: RR/RL/LR/LL × 1/2) |
| intersectionWidth | int | m | — | cache — derived (source: "never written, only read") |
| lanesAmount | int | lanes | — | cache — from node1's road |
| ignoreSide | int | idx | -1..3 — **AUTHORED** | Y |
| ignoreCorner | int | idx | -1..3 — **AUTHORED** | Y |
| intersectionType | IntersectionTypeEnum | idx | enum | Y |
| intersectionStopType | iStopTypeEnum | idx | enum | Y |
| roadType | RoadTypeEnum | idx | enum | Y |
| lightType | LightTypeEnum | idx | enum | Y |

**Material slots (all author-facing references).**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| markerCenter1…3 | Material | ref | — | Y (3) |
| markerExtStretch1…3 | Material | ref | — | Y (3) |
| markerExtTiled1…3 | Material | ref | — | Y (3) |
| lane0Mat1/2, lane1Mat1/2, lane2Mat1/2, lane3Mat1/2 | Material | ref | — | Y (8) |
| lane1Mat1Disabled … lane3Mat2Disabled (incl. …DisabledActive, …DisabledActiveR) | Material | ref | — | Y (14) |

**Derived corner geometry (all `cache` — computed from the two roads, never author-entered).**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| cornerPoints | CornerPositionMaker[] | — | — | cache |
| cornerRR, cornerRROuter, cornerRRRampOuter … cornerLL, cornerLLOuter, cornerLLRampOuter | Vector3 | m | — | cache — 12 (RR/RL/LR/LL × inner/outer/rampOuter) |
| cornerRR2D / cornerRL2D / cornerLR2D / cornerLL2D | Vector2 | m | — | cache (4) |
| cornerLRCornerRR / cornerLLCornerRL / cornerLLCornerLR / cornerRLCornerRR | Vector3[] | m | — | cache — edge runs (4) |
| oddAngle / evenAngle | float | ° | — | cache — derived angles |
| maxInterDistance | float | m | — | cache |
| maxInterDistanceSQ | float | m² | — | cache |

**Junction tuning & heights.** Multipliers are unitless; the two large heights are sentinels/offsets.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| gradeMod | float | × | 0–1 — **AUTHORED**, corner grade multiplier | verify |
| gradeModNegative | float | × | 0–1 — **AUTHORED** | verify |
| scalingSense | float | × | 0–10 — **AUTHORED** | verify |
| height | float | m | — | cache — raise sentinel |
| signHeight | float | m | — | cache — sign offset sentinel |

**Traffic-light settings (author surface; timings in seconds).** Timings **AUTHORED** to real signal ranges.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| isLightsEnabled | bool | — | — | Y |
| isRegularPoleAlignment | bool | — | — | Y |
| isTrafficPoleStreetLight | bool | — | — | Y |
| isTrafficLightGray | bool | — | — | Y |
| isLeftTurnYieldOnGreen | bool | — | — | Y |
| streetLightRange | float | m | 0–100 m — **AUTHORED** | Y |
| streetLightIntensity | float | × | 0–8 — **AUTHORED** | Y |
| streetLightColor | Color | rgba | — | Y |
| lightsRR / lightsRL / lightsLL / lightsLR | TrafficLightController | ref | — | cache — generated controllers (4) |
| fixedTimeRegularLightLength | float | s | 5–120 s — **AUTHORED**, green phase | Y |
| fixedTimeLeftTurnLightLength | float | s | 3–30 s — **AUTHORED** | Y |
| fixedTimeAllRedLightLength | float | s | 0.5–3 s — **AUTHORED**, all-red clearance | Y |
| fixedTimeYellowLightLength | float | s | 3–6 s — **AUTHORED**, amber (ITE 3–6 s) | Y |
| fixedTimeSequenceList | List&lt;TrafficLightSequence&gt; | — | — | cache — generated phase plan |

### `SplinatedMeshMaker` (per-node extruded mesh — drawn inline in the node inspector)

101 fields on the `[Serializable]` maker. It is not a component; it is a serialised member of
`SplineN.SplinatedObjects`, so its field names are load-bearing for the scene **and** for the `.rao` library
round-trip (RM-48). `…` rows collapse regular families; each named suffix is a distinct preserved field.
`isToggled` is a foldout-state flag, not data. Ranges **AUTHORED** where the source annotates none.

**Flags & mode toggles (author-facing).**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| uID | string | — | — | cache — identity |
| isRequiringUpdate | bool | — | — | cache |
| isDefault | bool | — | — | Y — library default |
| isMaterialOverriden | bool | — | — | Y |
| isExactSplination / isMatchingRoadDefinition / isMatchingRoadIncrements | bool | — | — | Y (3) |
| isTrimStart / isTrimEnd | bool | — | — | Y (2) |
| isToggled | bool | — | — | cache — foldout state |
| isMatchingTerrain / isBridge | bool | — | — | Y (2) |
| isStretched / isStretchedOffset / isStretchedSize | bool | — | — | Y (3) |
| isBoxColliderFlippedOnX / …OnZ | bool | — | — | Y (2) |
| isStraightLineMatchStartEnd / isFlippingRotation / isStatic / isNotCenterMode | bool | — | — | Y (4) |

**Object/material/mesh references & source strings.**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| masterObjTrans | Transform | ref | — | cache |
| currentSplination / currentSplinationCap1 / currentSplinationCap2 | GameObject | ref | — | cache (3) |
| currentSplinationString / …Cap1String / …Cap2String | string | — | — | cache — asset ids (3) |
| Output | GameObject | ref | — | cache |
| mesh | Mesh | ref | — | cache |
| SplinatedMaterial1 / SplinatedMaterial2 | Material | ref | — | Y (2) |
| SplinatedMaterial1String / SplinatedMaterial2String | string | — | — | cache (2) |
| EndCapStart / EndCapEnd / EndCapStartOutput / EndCapEndOutput | GameObject | ref | — | cache (4) |
| EndCapStartString / EndCapEndString | string | — | — | cache (2) |
| spline | SplineC | ref | — | cache |
| node | SplineN | ref | — | cache |

**Geometry: offsets, curves, cutoff, timing, bounds, endcaps, endings, collision, naming.**

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| capHeightOffset1 / capHeightOffset2 | float | m | ±5 m — **AUTHORED** | Y (2) |
| minMaxMod | float | × | 0–0.1 — **AUTHORED**, min/max blend | Y |
| vertexMatchingPrecision | float | m | 0.001–0.05 m — **AUTHORED**, weld tolerance | Y |
| stretchUVThreshold | float | × | 0–1 — **AUTHORED** | Y |
| boxColliderOffset / boxColliderSize | Vector3 | m | — | Y (2) |
| horizontalCurve | AnimationCurve | frac | — | Y — lateral offset over length |
| horizontalSep | float | m | — | Y |
| verticalCurve | AnimationCurve | frac | — | Y — vertical offset over length |
| verticalRaise | float | m | — | Y |
| VerticalCutoff | float | m | — | Y |
| VerticalMeshCutoffOffset | float | m | — | Y |
| isVerticalCutoff / …Downwards / isVerticalMeshCutoffOppositeDir / isVerticalCutoffMatchingZero | bool | — | — | Y (4) |
| RoadRaise | float | m | — | Y |
| StartTime / EndTime | float | frac | 0–1 — **AUTHORED**, along-spline window | Y (2) |
| mMaxX / mMinX / mMaxY / mMinY / mMaxZ / mMinZ | float | m | — | cache — mesh bounds (6) |
| CustomRotation | Vector3 | ° | — | Y |
| StartPos / EndPos | Vector3 | m | — | cache (2) |
| Axis | AxisTypeEnum | idx | enum | Y |
| RepeatUVType | RepeatUVTypeEnum | idx | enum | Y |
| isEndCapCustomMatchStart / isEndObjectsMatchingGround | bool | — | — | Y (2) |
| EndCapCustomOffsetStart / …End | Vector3 | m | — | Y (2) |
| EndCapCustomRotOffsetStart / …End | Vector3 | ° | — | Y (2) |
| isStartDown / isStartTypeDownOverriden | bool | — | — | Y (2) |
| startTypeDownOverride | float | frac | 0–1 — **AUTHORED** | Y |
| isEndDown / isEndTypeDownOverriden | bool | — | — | Y (2) |
| endTypeDownOverride | float | frac | 0–1 — **AUTHORED** | Y |
| CollisionType | CollisionTypeEnum | idx | enum | Y |
| isCollisionConvex / isSimpleCollisionAutomatic / isCollisionExtrude / isCollisionTrigger | bool | — | — | Y (4) |
| CollisionBoxBL / BR / TL / TR | Vector3 | m | — | Y (4) |
| CollisionTriBL / BR / T | Vector3 | m | — | Y (3) |
| objectName | string | — | — | Y — library name |
| thumbString | string | — | — | cache — thumbnail id |
| desc / displayName | string | — | — | Y (2) |

The nested `[Serializable]` **`SplinatedMeshLibraryMaker`** (82 fields) mirrors this class for the on-disk
`.rao` library format — same names with a few casing shifts (`CapHeightOffset1`, `StartTypeDownOverride`) and
`int` in place of the enums (`Axis`, `RepeatUVType`, `CollisionType`). Its names are load-bearing for RM-48's
round-trip, not for scene serialisation; the mapping between the two is what RM-48 tests.

### `EdgeObjectMaker` (per-node edge objects — railings/signs, drawn inline in the node inspector)

47 fields on the `[Serializable]` maker; a member of `SplineN.EdgeObjects`. Same dual role as
`SplinatedMeshMaker` — scene serialisation **and** `.rao` round-trip. `meterSep` is the real placement control
(matches `ScatterBake.ScatterAlongLine`'s spacing, RM-47).

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| isRequiringUpdate | bool | — | — | cache |
| UID | string | — | — | cache — identity |
| node | SplineN | ref | — | cache |
| isDefault | bool | — | — | Y — library default |
| edgeObject | GameObject | ref | — | cache — source prefab ref |
| edgeObjectString | string | — | — | cache — asset id |
| isMaterialOverriden | bool | — | — | Y |
| edgeMaterial1 / edgeMaterial2 | Material | ref | — | Y (2) |
| edgeMaterial1String / edgeMaterial2String | string | — | — | cache (2) |
| isMatchingTerrain | bool | — | — | Y |
| isCombinedMesh / isCombinedMeshCollider | bool | — | — | Y (2) |
| masterObject | GameObject | ref | — | cache |
| edgeObjectLocations / edgeObjectRotations | List&lt;Vector3&gt; | m / ° | — | cache — baked placements (2) |
| edgeObjects | List&lt;GameObject&gt; | ref | — | cache — instantiated objects |
| subType | SignPlacementSubTypeEnum | idx | enum (Left/Right/…) | Y |
| meterSep | float | m | 0.5–100 m — **AUTHORED**, along-road spacing | Y |
| isToggled | bool | — | — | cache — foldout state |
| isBridge | bool | — | — | Y |
| horizontalSep | float | m | — | Y |
| horizontalCurve | AnimationCurve | frac | — | Y |
| verticalRaise | float | m | — | Y |
| verticalCurve | AnimationCurve | frac | — | Y |
| customRotation | Vector3 | ° | — | Y |
| isRotationAligning | bool | — | — | Y |
| isXRotationLocked / isYRotationLocked / isZRotationLocked | bool | — | — | Y (3) |
| isOncomingRotation / isStatic | bool | — | — | Y (2) |
| customScale | Vector3 | × | — | Y |
| startTime / endTime | float | frac | 0–1 — **AUTHORED** | Y (2) |
| singleOnlyBridgePercent | float | frac | 0–1 — **AUTHORED** | Y |
| startPos / endPos | Vector3 | m | — | cache (2) |
| isSingle | bool | — | — | Y |
| singlePosition | float | frac | 0–1 — **AUTHORED** | Y |
| isStartMatchRoadDefinition | bool | — | — | Y |
| startMatchRoadDef | float | m | — | Y |
| objectName | string | — | — | Y — library name |
| thumbString | string | — | — | cache |
| desc / displayName | string | — | — | Y (2) |

The nested `[Serializable]` **`EdgeObjectLibraryMaker`** (36 fields) mirrors this for the `.rao` format (same
names, `int` for `subType`); load-bearing for RM-48 round-trip, not scene serialisation.

### `RoadTerrain` (per-terrain splat/settings component)

12 fields. `uID` is the only `[SerializeField] [HideInInspector]` (backing the public `UID` property);
`terrain` is `[HideInInspector]`. The splat block is the author surface.

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| uID | int | — | — | hidden — `[SerializeField][HideInInspector]`, identity backing field |
| terrain | Terrain | ref | — | hidden — `[HideInInspector]`, auto-wired |
| splatResoWidth / splatResoHeight | int | px | 512–4096 — **AUTHORED**, power-of-two splat resolution | Y (2) |
| splatBackground / splatForeground | Color | rgba | — | Y (2) |
| splatWidth | float | m | 1–60 m — **AUTHORED** (RM-38 pins the 30 m default as over-wide) | Y |
| isSplatSkipBridges / isSplatSkipTunnels / isSplatSingleRoad | bool | — | — | Y (3) |
| splatSingleChoiceIndex | int | idx | — | Y |
| roadSingleChoiceUID | string | — | — | cache — selected road id |

## Fields serialised but not shown — the judgement list

RM-50 requires that serialised-but-unshown fields be called out, because they split into two kinds and the
split is a decision, not an oversight (RM-66):

- **`cache`** — internal state Unity must serialise (baked buffers, cached node counts, saved-mesh flags,
  history lists). These stay hidden; surfacing them would let an author corrupt derived state. Each is marked
  `cache` in the Shown column with a one-line reason in this section.
- **`surface`** — a real authoring control that was simply never drawn. These are wired up and labelled in
  RM-51, not deleted. Each is listed here with the reason it is worth surfacing.

### `cache` — stays hidden (surfacing it would corrupt derived state)

| Component | Fields | Why it stays hidden |
|---|---|---|
| `SplineC` | all 32 (`nodes`, `distance`, `cachedPointsA/B`, tangents, node counts, closed-loop working state) | Pure derived geometry rebuilt from `SplineN` on every road update; an author edit is silently overwritten next bake. |
| `Road` | `RCS` buffer refs, `nodes`/`spline` lists, `isTearingDown`, `editorConstructionCount`, undo/history counters, gizmo transient flags | Regenerated buffers and build bookkeeping; hand-editing desyncs the mesh from the node graph. |
| `RoadIntersection` | derived corner geometry (`cornerPoints`, fillet arcs, cached lane splits), light-cycle working state | Recomputed from the two crossing roads; the toggles/types above them are the real surface. |
| `SplinatedMeshMaker` / `EdgeObjectMaker` | baked vert/tri/uv buffers, cached bounds, per-instance placement lists | Output of the bake; the `.ram`/`.rao` library fields are the authored input. |
| `RoadTerrain` | `uID`, `terrain` | Identity backing field and auto-wired terrain ref; neither is an author choice. |

### `surface` — wire up in RM-51 (real controls that were simply never drawn)

| Component | Field | Unit / range | Why it is worth surfacing |
|---|---|---|---|
| `SplineC` | a real `isClosed` (does not yet exist — RM-28/61) | bool | Closed loops (roundabouts) are unreachable today; the preview carriers carry the flag but drop it on commit. |
| `Road` | `maxGrade` | `frac` 0–0.15 (**AUTHORED**, 8 % default) | A real-world road-design limit that currently only lives in code; belongs on the geometry foldout. |
| `Road` | `roadDefinition` | `m` 1–20 (**AUTHORED**) | Governs sampling density / real-world segment length; an author-facing quality-vs-cost dial. |
| `RoadTerrain` | `splatWidth` | `m` 1–60 (**AUTHORED**, RM-38 flags 30 m default as over-wide) | Directly sets how far the splat paints from the carriageway; a visible, tunable real-world width. |

No serialised field is proposed for deletion. Everything unshown is classified `cache` (kept hidden, reason
above) or `surface` (drawn in RM-51) — satisfying RM-66's dead-code-is-wired-not-deleted rule.

## The city layer (Module 6)

The `City/Runtime` types are Module-6 code with no shipped pre-overhaul inspector to protect, so they are
censused as they gain one (the scope note above). C-10 gave them one; this section is that census.

Two different kinds of thing are censused here:

* **`CityAuthoring`** is an ordinary MonoBehaviour, so its rows read exactly like the road tables above.
* **Node parameters** are not fields on a component at all. A city graph node carries a bag of named
  parameters declared by `CityNodeCatalog`, so the **Field** column is the parameter name as the catalogue
  writes it and the runner reads it, and a **Node** column carries the node kind those parameters belong to
  — which is also the key `InspectorFieldRegistry` files them under. One table with a Node column, rather
  than twenty tables of two rows each. Their **Shown** standing is `Y` throughout: a node parameter is typed
  by an author or it does nothing.

Where **Range** reads `—` the value genuinely has no limit, and that is a decision rather than an omission:
C-15's rule is to record no range instead of inventing one an author would then have to fight. Many city
parameters treat **zero as "say nothing here"** — the operator uses the figure its own class or archetype
carries — so their floor is 0 rather than the smallest useful value.

### `CityAuthoring` (the per-settlement component)

| Field | Type | Unit | Range | Shown |
|---|---|---|---|---|
| `graph` | `CityGraph` | `ref` | — | Y |
| `seed` | `int` | `—` | — | Y |
| `regionCentre` | `Transform` | `ref` | — | Y |
| `regionXZ` | `Rect` | `m` | — (floor enforced on the Scene handles: 10 m, **AUTHORED**) | Y |
| `prefabLibraries` | `List<CategoryContentSet>` | `ref` | — | Y |
| `archetypeOverride` | `SettlementArchetype` | `idx` | — | Y |
| `scanRoots` | `List<Transform>` | `ref` | — | Y |
| `cityData` | `CityData` | `ref` | — | Y |
| `sourceRoad` | `Road` | `ref` | — | Y |
| `sourceEnd` | `RoadEnd` | `idx` | — | Y |

### City graph node parameters (`CityNodeCatalog`)

| Node | Field | Type | Unit | Range | Shown |
|---|---|---|---|---|---|
| `WaterSurface` | `hasWater` | flag | `—` | — | Y |
| `WaterSurface` | `waterSurfaceY` | number | `m` | — (a world height; a valley lake sits below zero) | Y |
| `RoadClassSource` | `classId` | number | `idx` | ≥ 0 (`RoadClassCatalog`: Arterial 0, Collector 1, Local 2) | Y |
| `ZoneCatalogueSource` | `zoneId` | text | `ref` | — | Y |
| `SuitabilityRaster` | `metresPerCell` | number | `m` | 1–64 (**AUTHORED**, around the 16 m shipped) | Y |
| `SuitabilityRaster` | `slopeWeight` | number | `frac` | 0–1 (the five weights ship summing to 1.0; 0.45) | Y |
| `SuitabilityRaster` | `earthworkWeight` | number | `frac` | 0–1 (ships 0.25) | Y |
| `SuitabilityRaster` | `waterWeight` | number | `frac` | 0–1 (ships 0.15) | Y |
| `SuitabilityRaster` | `landUseWeight` | number | `frac` | 0–1 (ships 0.10) | Y |
| `SuitabilityRaster` | `networkWeight` | number | `frac` | 0–1 (ships 0.05) | Y |
| `CorridorSolve` | `toleranceFraction` | number | `frac` | 0–1 (`CorridorSolver.DefaultToleranceFraction` 0.05) | Y |
| `BlockInset` | `fallbackInsetMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 defers to the operator) | Y |
| `LotSubdivision` | `targetAreaM2` | number | `m²` | ≥ 0 (**AUTHORED**, 0 defers to the archetype) | Y |
| `LotSubdivision` | `splitJitter` | number | `frac` | 0–1 (**AUTHORED**, a share of the split's own spacing) | Y |
| `BuildableLand` | `sampleSpacingMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 defers to the operator) | Y |
| `ZonePaint` | `straddleTieFraction` | number | `frac` | 0–1 (**AUTHORED**, a share of one lot's area) | Y |
| `PrefabValidate` | `category` | text | `ref` | — | Y |
| `PrefabValidate` | `requester` | text | `—` | — | Y |
| `PolesAndSpans` | `runLengthMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 takes the street's own length) | Y |
| `Rail` | `designSpeedKmh` | number | `km/h` | ≥ 0 (**AUTHORED**, 0 defers to the track class) | Y |
| `Ropeway` | `runLengthMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 measures between the stations) | Y |
| `Ropeway` | `spanLengthMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 defers to the class) | Y |
| `FootprintPublish` | `metresPerPixel` | number | `m` | 0.5–16 (**AUTHORED**, around the 4 m shipped) | Y |
| `FootprintPublish` | `featherWidthMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 is a hard edge; 8 m shipped) | Y |
| `FootprintConformNode` | `cellAreaM2` | number | `m²` | ≥ 0 (**AUTHORED**, 0 reads the terrain's own cell) | Y |
| `ChunkGrid` | `chunkSizeMetres` | number | `m` | ≥ 1 (`CityChunkGrid` substitutes 1 m for a non-positive size) | Y |
| `SpawnTable` | `chunkSizeMetres` | number | `m` | ≥ 1 (same floor; must match the grid) | Y |
| `OverrideMarker` | `element` | text | `—` | — | Y |
| `OverrideMarker` | `id` | number | `idx` | ≥ 0 (**AUTHORED**, ids are handed out from zero) | Y |
| `OverrideMarker` | `note` | text | `—` | — | Y |
| `CommentGroup` | `title` | text | `—` | — | Y |
| `CommentGroup` | `body` | text | `—` | — | Y |
| `ElementPlacement` | `category` | text | `ref` | — | Y |

`ElementPlacement` carries three further parameters **per settlement order**, named by
`CityNodeCatalog.OrderParameterPrefix` — so the twelve rows below are one shape repeated four times, and the
registry generates them through that same method rather than typing the prefixes again.

| Node | Field | Type | Unit | Range | Shown |
|---|---|---|---|---|---|
| `ElementPlacement` | `capital.present` | flag | `—` | — | Y |
| `ElementPlacement` | `capital.perKilometreOfFrontage` | number | `/km` | ≥ 0 (**AUTHORED**, 0 places none) | Y |
| `ElementPlacement` | `capital.minSpacingMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 enforces no minimum) | Y |
| `ElementPlacement` | `city.present` | flag | `—` | — | Y |
| `ElementPlacement` | `city.perKilometreOfFrontage` | number | `/km` | ≥ 0 (**AUTHORED**, 0 places none) | Y |
| `ElementPlacement` | `city.minSpacingMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 enforces no minimum) | Y |
| `ElementPlacement` | `town.present` | flag | `—` | — | Y |
| `ElementPlacement` | `town.perKilometreOfFrontage` | number | `/km` | ≥ 0 (**AUTHORED**, 0 places none) | Y |
| `ElementPlacement` | `town.minSpacingMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 enforces no minimum) | Y |
| `ElementPlacement` | `village.present` | flag | `—` | — | Y |
| `ElementPlacement` | `village.perKilometreOfFrontage` | number | `/km` | ≥ 0 (**AUTHORED**, 0 places none) | Y |
| `ElementPlacement` | `village.minSpacingMetres` | number | `m` | ≥ 0 (**AUTHORED**, 0 enforces no minimum) | Y |

Fifty-five rows in total — ten on `CityAuthoring` and forty-five node parameters — which is every parameter
the catalogue declares. C-12's gate asserts that equality both ways round, so a parameter added to the
catalogue without a row here, or a row here naming a parameter no node has, is a failure rather than a
silence.



## Sign-off

This census names every `[SerializeField]` and public serialised field on the runtime components the
inspectors expose — RoadSystem (4), Road (103), SplineC (32), SplineN (60), SplineF/SplineI (13),
RoadIntersection (110), SplinatedMeshMaker (101 + nested 82), EdgeObjectMaker (47 + nested 36),
RoadTerrain (12) — with type, real-world unit, valid range (**AUTHORED** where a standard was adopted per
C-15), and shown/hidden status. It is the data-side half of the non-breaking contract's gate.

The city layer is censused on the same terms above: CityAuthoring (10) and the 45 node parameters the city
graph catalogue declares. Those 55 rows are not a migration contract — Module 6 has no shipped inspector to
protect — but they are the same table the drawers and the gate read, which is why they live in one document
rather than two.

**Deferred to end-of-topic (part 4):** the before/after scene diff — saving a scene with the current
inspectors, running RM-51's rebuilt inspectors over it, and confirming no serialised value changed and no
property was dropped — needs the RM-51 overhaul and a committed test scene, so it is verified once at the end
of the inspector topic rather than here.
