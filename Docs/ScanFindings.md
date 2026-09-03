# Road & City Master — Full-Tree Scan Findings

> **RM-49 deliverable.** The mandatory full-tree scan that gates RM-06 and everything after it. This document
> names every file in the vendored road tool tree: for every runtime C# file what it owns and what depends on it;
> for every editor file how it is guarded; for every asset folder what references it; for every shader which
> pipeline it targets; and the defects the author has already reported, pinned to code. A reviewer picking any
> file at random finds it named here.
>
> **Scope of this scan.** The vendored road tool proper — `Scripts/` (runtime), `Scripts/Editor/`, and the asset
> folders (`Flares`, `ManualImages`, `Materials`, `Mesh`, `Physics`, `Prefabs`, `Shaders`, `Textures`,
> `Editor/Library`). The `City/` subtree is the Module 6 city layer added ON TOP of the tool and is documented by
> its own gates (RCMZONE … RCMSCATTER, RCMPREFAB), not re-scanned here.
>
> **Keep it in file order** so it can be diffed as the tree changes, and **update it as part of any task that
> changes what it describes** — a findings document that drifts from the tree is worse than none, because it will
> be trusted.

## Why this is a gate, not a task

This module is an upgrade to **somebody else's working code** — sixty-odd files that already ship a functioning
road tool, with an author who has been explicit that breaking it is not acceptable. Every expensive mistake
available here comes from writing before reading. The scan has four passes, each producing a section below,
because "read the code" without an output is not verifiable:

1. **Structural** — every runtime file with its namespace, its type, the types it references, and what depends on
   it. This is what makes the namespace census real rather than assumed.
2. **Editor-boundary** — how each `Scripts/Editor/` file is guarded. The current convention is a whole-file
   `#if UNITY_EDITOR`, which is why the single-assembly setup is not broken today; RM-06 replaces that convention
   with a compiler-enforced boundary rather than fixing a bug.
3. **Content** — asset folders, `.rao` object libraries, prefabs, materials and shaders, and which code path loads
   each, since a "dead" asset is often a live one loaded by string path.
4. **Defect** — every behaviour the author has already reported, with the file and method that produces it, so a
   fix is pinned to code before anyone proposes one.

## Two process traps already found by reading (recorded so no later task re-learns them)

- **The single asmdef's `.meta` GUID is load-bearing.** `RoadEditorUtility` self-locates through
  `AssetDatabase.GUIDToAssetPath`, so **recreating** the asmdef instead of **renaming** it breaks the tool in a way
  that looks like a missing file. Renames preserve the `.meta`; deletion-and-recreate does not.
- **The folder was untracked in git at vendor import.** A bad rename had no undo at that point. (The project is
  now under git inside `Assets/`, so deletions are recoverable — but the trap is why the rename discipline in the
  RM-69 `.meta`/GUID task exists.)

Both were found by reading. Neither would have been found by starting to code.

## Pass 1 — Structural (runtime C#)

Every non-editor C# file under `Scripts/`, in file order. "Depends on it" names the principal callers; "leaf" means
nothing in the tool calls it except entry points. All are `namespace RoadCityMaster` unless a sub-namespace is shown.

#### Scripts/ (root)

| File | namespace | primary type(s) | references | what depends on it |
|---|---|---|---|---|
| Construction.cs | RoadCityMaster | `static Construction` | Vector3 math, SplineC | RoadConstructorBufferMaker, Intersections, SplinatedMeshMaker |
| Construction2DRect.cs | RoadCityMaster | `Construction2DRect` | Vector2 | Terraforming bounds, Construction |
| Construction3DTri.cs | RoadCityMaster | `Construction3DTri` | Vector3 | Construction, intersection triangulation |
| EdgeObjectMaker.cs | RoadCityMaster.EdgeObjects | `EdgeObjectMaker`, `EdgeObjectLibraryMaker` | RootUtils, EngineIntegration, .rao IO | Road, RoadUtility, Wizard, SaveWindow |
| EngineIntegration.cs | RoadCityMaster | `static EngineIntegration` | AssetDatabase/Resources | IntersectionObjects, EdgeObjectMaker, RootUtils (asset load) |
| iConstructionMaker.cs | RoadCityMaster | `iConstructionMaker` | Construction | Road construction pipeline |
| IntersectionObjects.cs | RoadCityMaster | `static IntersectionObjects` | EngineIntegration, prefab/material paths | RoadIntersection |
| Intersections.cs | RoadCityMaster | `static Intersections` | Construction, SplineC | RoadIntersection, RoadConstructorBufferMaker |
| Navigation.cs | RoadCityMaster | `static Navigation` | Road, SplineC | RoadSystem / runtime queries (leaf-ish) |
| RenderQueue.cs | **(none)** | `static RenderQueue` | Material | material sort order (util). **No namespace declared — finding.** |
| RigidBody.cs | RoadCityMaster | `RigidBody : MonoBehaviour` | UnityEngine physics | runtime vehicle component (leaf) |
| Road.cs | RoadCityMaster | `Road : MonoBehaviour`, `enum RoadMaterialDropdownEnum` | SplineC, RoadConstructorBufferMaker, Terraforming, EdgeObjectMaker | RoadEditor, RoadSystem, Terraforming — the central type |
| RoadAutomation.cs | RoadCityMaster.Roads | `static RoadAutomation` | Road, RoadSystem, SplineC | EditorMenu, external creation API |
| RoadCalcs1.cs | RoadCityMaster.Threading | `RoadCalcs1 : ThreadedJob` | Road, Construction | RoadCreationT |
| RoadCalcs2.cs | RoadCityMaster.Threading | `RoadCalcs2 : ThreadedJob` | Road, Construction | RoadCreationT |
| RoadConstructorBufferMaker.cs | RoadCityMaster | `RoadConstructorBufferMaker`, `enum SaveMeshTypeEnum` | Construction, Road, mesh buffers | Road (mesh build), RoadCreationT |
| RoadCreationT.cs | RoadCityMaster.Threading | `static RoadCreationT` | RoadCalcs1/2, ThreadedJob | Road |
| RoadEditorUtility.cs | RoadCityMaster | `static RoadEditorUtility` | **AssetDatabase.GUIDToAssetPath (self-locate)** | RootUtils, all editor scripts. **Load-bearing `.meta` GUID.** |
| RoadIntersection.cs | RoadCityMaster | `RoadIntersection : MonoBehaviour` + 4 enums | Intersections, IntersectionObjects, TrafficLightController | RoadIntersectionEditor, RoadSystem |
| RoadSystem.cs | RoadCityMaster | `RoadSystem : MonoBehaviour` | Road, RoadIntersection | RoadSystemEditor — scene root manager |
| RoadTerrainInfo.cs | RoadCityMaster | `RoadTerrainInfo` | Terrain | Terraforming, Road terrain sampling |
| RoadUtility.cs | RoadCityMaster | 5 enums (Railing/CenterDivider/EndCap…) + util | RootUtils, EdgeObjectMaker | Road, editors, EdgeObjectMaker |
| RootUtils.cs | RoadCityMaster | `static RootUtils` | XmlSerializer, RoadEditorUtility | EdgeObjectMaker, SplinatedMeshMaker, RoadUtility, Wizard |
| SplinatedMeshMaker.cs | RoadCityMaster.Splination | `SplinatedMeshMaker`, `SplinatedMeshLibraryMaker`, 3 enums | RootUtils, /Mesh/ paths | Road (extrusions), Wizard, SaveWindow |
| ThreadedJob.cs | RoadCityMaster.Threading | `ThreadedJob` | System.Threading | base of RoadCalcs1/2, TerrainCalcs |
| TrafficLightController.cs | RoadCityMaster | `TrafficLightController` + 4 enums | RoadIntersection | RoadIntersection, TrafficLightSequence |
| TrafficLightSequence.cs | RoadCityMaster | `TrafficLightSequence` | TrafficLightController | RoadIntersection |
| UnitTests.cs | RoadCityMaster.Tests | `static UnitTests` | Road, SplineC, Construction | EditorMenu — the existing self-test RM-70 extends |
| WizardObject.cs | RoadCityMaster | `WizardObject`, `WizardObjectLibrary` | RootUtils | Wizard editor |

#### Scripts/CrossSection/ (Module 6 additions, `namespace RoadCityMaster.CrossSection`)

| File | primary type(s) | references | what depends on it |
|---|---|---|---|
| FurnitureRunPlanner.cs | `static FurnitureRunPlanner` | StreetscapeProfile | StreetscapeGate (editor), streetscape placement |
| MedianProfile.cs | `struct MedianProfile` | — | VariableWidthProfile, CrossSectionProfileGate |
| StreetscapeProfile.cs | `struct StreetscapeProfile` | — | FurnitureRunPlanner, StreetscapeGate |
| VariableWidthProfile.cs | `enum MedianKind`, `struct VariableWidthProfile` | MedianProfile | CrossSectionProfileGate, divided-carriageway core |

#### Scripts/Spline/

| File | primary type(s) | references | what depends on it |
|---|---|---|---|
| SplineC.cs | `SplineC : MonoBehaviour` | SplineN, Construction | Road (its spline), Intersections, SplineCEditor |
| SplineF.cs | `SplineF : MonoBehaviour` | — | SplineFEditor (stub inspector) |
| SplineI.cs | `SplineI : MonoBehaviour` | — | SplineIEditor (stub inspector) |
| SplineN.cs | `SplineN : MonoBehaviour` | RootUtils (uID) | SplineC (its nodes), SplineNEditor |
| SplinePreviewNode.cs | `SplinePreviewNode` | SplineN | SplineC preview path |

#### Scripts/Terrain/

| File | namespace | primary type(s) | references | what depends on it |
|---|---|---|---|---|
| RoadTerrain.cs | RoadCityMaster | `RoadTerrain : MonoBehaviour` | Terrain, splatWidth=30 | TerrainEditor, Road (splatmap) |
| Terraforming.cs | RoadCityMaster | `static Terraforming`, `TempTerrainData` | RoadTerrainInfo, Construction2DRect | Road, TerraformingThreaded |
| TerraformingThreaded.cs | RoadCityMaster.Threading | `static TerraformingThreaded`, `TerrainBoundsMaker` | Road.RoadWidth(), TerrainCalcs | Road terrain-modification pipeline |
| TerrainCalcs.cs | RoadCityMaster.Threading | `TerrainCalcs : ThreadedJob` | TerrainCalcsStatic | TerraformingThreaded |
| TerrainCalcsStatic.cs | RoadCityMaster.Threading | `static TerrainCalcsStatic` | Road, heightmap math | TerrainCalcs, Terraforming |
| TerrainHistoryMaker.cs | RoadCityMaster | `TerrainHistoryMaker`, `TerrainTreeInstance` | Terrain, serialization | Road (terrain undo) |
| TerrainHistoryUtility.cs | RoadCityMaster | `static TerrainHistoryUtility`, `sealed VersionDeserializationBinder` | BinaryFormatter | TerrainHistoryMaker |

#### Two structural questions the scan had to answer

**1. Can `Road` carry a spline with mesh generation switched OFF? — NO.** `Road.cs` exposes toggles for shoulders
(`isShouldersEnabled`, 55), cuts (`isRoadCutsEnabled`/`isShoulderCutsEnabled`, 60–61) and terrain edits
(`isHeightModificationEnabled`/`isDetailModificationEnabled`/`isTreeModificationEnabled`, 78–80), and `isSavingMeshes`
(86) only controls asset serialisation while `isUsingMeshColliders` (87) only controls the collider — there is **no
flag that suppresses the carriageway mesh itself**. The road surface is always built through
`RoadConstructorBufferMaker`. **Consequence:** RM-23 (busway), RM-25 (rail) and RM-36 (ropeway) that want a spline
PATH without a road mesh need a **new lightweight carrier type** (or to drive `SplineC` directly), not a mesh-off Road.

**2. Does `SplineC` support a CLOSED LOOP? — NO, not as an exposed capability.** The only loop handling is a single
**hardcoded local** `bool isClosed = false;` in the node-setup routine (`SplineC.cs:350`), which the step calculation
(351) and the last-node rotation (372–375) read but which is never set true and is not a serialized field. The rest
of the spline evaluation is open-ended (first/last node special-cased). **Consequence:** RM-28 / RM-61 (roundabout as
a closed ring) must **thread a real `isClosed` flag through `SplineC`'s evaluation and wrap the interpolation**, or use
a dedicated closed-curve carrier — reusing an existing flag is not available.

## Pass 2 — Editor-boundary (`Scripts/Editor/`)

#### Scripts/Editor/

| File | namespace | primary type(s) | guard | what it owns |
|---|---|---|---|---|
| AssemblyBoundaryAudit.cs | RoadCityMaster | `static AssemblyBoundaryAudit` | whole-file #if UNITY_EDITOR | editor/player assembly-boundary audit + menu/headless entry |
| CrossSectionProfileGate.cs | RoadCityMaster | `static CrossSectionProfileGate` | whole-file #if UNITY_EDITOR | headless test gate for variable-width median profile |
| EditorMenu.cs | RoadCityMaster | `EditorMenu` | whole-file #if UNITY_EDITOR | top-level menu items (create/add/update, tests, export) |
| EditorProgressWindow.cs | RoadCityMaster | `EditorProgressWindow : EditorWindow` | whole-file #if UNITY_EDITOR | progress-bar utility window |
| EditorUtilities.cs | RoadCityMaster | `EditorUtilities` | whole-file #if UNITY_EDITOR | shared editor GUI/texture/manual helpers |
| HelpWindow.cs | RoadCityMaster | `HelpWindow : EditorWindow` | whole-file #if UNITY_EDITOR | help/links window |
| ObjExporter.cs | RoadCityMaster | `ObjExporter : ScriptableObject` | whole-file #if UNITY_EDITOR (after header comment) | OBJ/MTL mesh export + export menu items |
| RoadEditor.cs | RoadCityMaster | `RoadEditor : Editor` (Road) | whole-file #if UNITY_EDITOR | Road custom inspector |
| RoadIntersectionEditor.cs | RoadCityMaster | `RoadIntersectionEditor : Editor` | whole-file #if UNITY_EDITOR | Intersection inspector + scene GUI |
| RoadSystemEditor.cs | RoadCityMaster | `RoadSystemEditor : Editor` | whole-file #if UNITY_EDITOR | RoadSystem custom inspector |
| SaveWindow.cs | RoadCityMaster | `SaveWindow : EditorWindow` | whole-file #if UNITY_EDITOR | save-to-library dialog (extrusion/edge/group) |
| SplineCEditor.cs | RoadCityMaster | `SplineCEditor : Editor` (SplineC) | whole-file #if UNITY_EDITOR | spline node-browser inspector |
| SplineFEditor.cs | RoadCityMaster | `SplineFEditor : Editor` (SplineF) | whole-file #if UNITY_EDITOR | empty/stub custom inspector |
| SplineIEditor.cs | RoadCityMaster | `SplineIEditor : Editor` (SplineI) | whole-file #if UNITY_EDITOR | empty/stub custom inspector |
| SplineNEditor.cs | RoadCityMaster | `SplineNEditor : Editor` (SplineN) | whole-file #if UNITY_EDITOR | node inspector (custom serialization for undo) |
| StreetscapeGate.cs | RoadCityMaster | `static StreetscapeGate` | whole-file #if UNITY_EDITOR | headless test gate for furniture spacing/streetscape |
| TerrainEditor.cs | RoadCityMaster | `TerrainEditor : Editor` (RoadTerrain) | whole-file #if UNITY_EDITOR | terrain splatmap-generation inspector |
| Wizard.cs | RoadCityMaster | `Wizard : EditorWindow` | whole-file #if UNITY_EDITOR | extrusion/edge/group library wizard window |

**Editor-boundary finding.** The whole-file `#if UNITY_EDITOR … #endif` convention holds across all 18 files: each
opens with `#if UNITY_EDITOR` on the first source line and closes with `#endif` as the last line, so no file wraps
only part of its contents and none relies on the asmdef alone. `ObjExporter.cs` is the sole cosmetic deviation — its
guard opens on line 9 after a descriptive header comment, but the whole code body still sits inside it. Because every
type is source-excluded from non-editor compilation regardless of which assembly it lands in, the single-assembly
setup cannot leak an editor-only symbol into a player build; even a file that drifted into the runtime assembly would
compile its `UnityEditor` references out. `AssemblyBoundaryAudit.cs` polices the same boundary from the assembly-graph
side, so the hand-wrapped guards and the asmdef split reinforce each other. **RM-06** replaces this convention with a
compiler-enforced boundary — it is a hardening, not a bug fix.

## Pass 3 — Content (asset folders, shaders, object libraries)

#### Asset folders

| Folder | holds | which code path loads it |
|---|---|---|
| `Flares` | 1 `.flare` + 1 `.tga` (`SodiumBulb`) | `IntersectionObjects.cs:796` path `"/Flares/SodiumBulb.flare"` (street-light flare) |
| `ManualImages` | 60 images | **No C# reference** — only `RoadArchitectManual.htm` `<img>` links. Doc-only asset, not code- or GUID-loaded. |
| `Materials` | 471 `.mat` | path strings in `RoadIntersection.cs`/`IntersectionObjects.cs` (`/Materials/Signs/…`, `/Materials/Markers/…`) + GUID from prefabs/FBX |
| `Mesh` | 187 FBX | `SplinatedMeshMaker.cs:3194` `/Mesh/` extrusion sources; individual meshes GUID-referenced by prefabs |
| `Physics` | 4 `.physicMaterial` | `Road.cs:1394-1448` loads `/Physics/Pavement.physicMaterial`, `/Physics/Dirt.physicMaterial` (default collider mats) |
| `Prefabs` | 49 `.prefab` | path strings in `IntersectionObjects.cs`, `EngineIntegration.cs` (`/Prefabs/Signs/…`) + GUID |
| `Shaders` | 10 `.shader` | GUID-referenced by the `.mat` files under `Materials/` |
| `Textures` | 506 `.png` | no folder-path string — GUID-referenced inside `.mat` + FBX slots (verify: GUID-referenced, **not dead**) |
| `Editor/Icons` | 17 `.png` | path in `TerrainEditor.cs` (`/Editor/Icons/FlexBG.png`…), `SplineNEditor.cs` (`popupbg.png`…) |
| `Editor/Library/EdgeObjects` | 30 `.rao` (`EOM…`) | `EdgeObjectMaker.cs` `Path.Combine(libraryPath,"EdgeObjects")` |
| `Editor/Library/ExtrudedObjects` | 23 `.rao` (`ESO…`) | `SplinatedMeshMaker.cs` `Path.Combine(libraryPath,"ExtrudedObjects")` |
| `Editor/Library/Groups/Default` | 57 `.rao` | `RoadUtility.cs` + `Editor/Wizard.cs` `Path.Combine(libraryPath,"Groups","Default")` — road-group presets |
| `Editor/Library/Thumbs` | 61 `.png` | `thumbString` paths saved into `.rao` records (`EdgeObjectMaker.cs`, `SaveWindow.cs`) |
| `Editor/Library/W` | 1 `.txt` | working/scratch dir: `EdgeObjectMaker.cs:215`, `SplinatedMeshMaker.cs:387` write temp `.rao` |

#### Shaders — all 10 are Built-in pipeline; every one renders magenta under URP and needs porting (RM-67/68)

| Shader file | ShaderLab name | pipeline | evidence |
|---|---|---|---|
| RoadShader.shader | `RoadCityMaster/Roads/Road Shader` | Built-in | `CGPROGRAM` + `#pragma surface surf BlinnPhong`, `FallBack "Diffuse"` |
| RoadShaderDX11.shader | `RoadCityMaster/DX11/Road Shader` | Built-in (DX11) | `#pragma surface surf BlinnPhong` |
| RoadShaderLow.shader | `RoadCityMaster/Roads/Road Shader Low Quality` | Built-in | `#pragma surface surf BlinnPhong`, `FallBack "VertexLit"` |
| RoadShaderLowDX11.shader | `RoadCityMaster/DX11/Road Shader Low Quality` | Built-in (DX11) | `#pragma surface surf BlinnPhong` |
| ShoulderDirt.shader | `RoadCityMaster/Roads/Road Shoulder Shader` | Built-in | `#pragma surface surf BlinnPhong` |
| ShoulderDirtDX11.shader | `RoadCityMaster/DX11/Road Shoulder Shader` | Built-in (DX11) | `#pragma surface surf BlinnPhong` |
| StandardOffset.shader | `RoadCityMaster/CutoutMarker` | Built-in | Standard copy: `LightMode ForwardBase/Add`, `UnityStandardCoreForward.cginc` |
| StandardTiling.shader | `RoadCityMaster/Standard-Tiling` | Built-in | Standard copy: `LightMode ForwardBase/Add`, `UnityStandardCoreForward.cginc` |
| TransparentWithShadows.shader | `RoadCityMaster/TranShadow` | Built-in | `surf BlinnPhong alpha … fullforwardshadows`; `exclude_renderers d3d11`; legacy `UNITY_MATRIX_MVP` |
| TransparentWithShadowsZ.shader | `RoadCityMaster/TranShadowMarker` | Built-in | `surf BlinnPhong alpha … fullforwardshadows`; legacy `UNITY_MATRIX_MVP` |

#### Object libraries (.rao)

110 `.rao` files under `Editor/Library`, across three loaders: 30 in `EdgeObjects/` (`EOM…`, e.g.
`EOMStreetLightSingle.rao`), 23 in `ExtrudedObjects/` (`ESO…`, e.g. `ESORailing1.rao`, `ESOSidewalk.rao`), and 57 in
`Groups/Default/`. Each `.rao` is an XML-serialized object definition: `EdgeObjects` records serialize
`EdgeObjectMaker.EdgeObjectData` (a prefab reference as an asset-path string `edgeObjectString` plus a `thumbString`
into `Library/Thumbs`); `ExtrudedObjects` records serialize `SplinatedMeshMaker` extrusion definitions;
`Groups/Default` records are saved road-group presets. They are read via `RootUtils` XML deserialization
(`XmlSerializer`/`File.OpenText`, `RootUtils.cs:122-179`), base dir from `RootUtils.GetDirLibraryBase()` →
`RoadEditorUtility.GetBasePathForIO()`; referenced prefabs/thumbnails resolve through
`EngineIntegration.LoadAssetFromPath<GameObject>()`.

## Pass 4 — Defects the author has already reported, pinned to code

| Defect | file : anchor | mechanism |
|---|---|---|
| **Over-wide terraform — the road "moves far too much dirt" (RM-38)** | `Terrain/TerraformingThreaded.cs:158`; `Terrain/TerrainCalcsStatic.cs:19` (commented `tDistance = RoadWidth()*2f`); `Terrain/RoadTerrain.cs:27` (`splatWidth = 30f`) | The terrain-modification extent is derived as a multiple of `Road.RoadWidth()` (`Sep = RoadWidth()*0.5f` plus the cut/falloff band), so the flattened/splatted band is far wider than the carriageway footprint. RM-38 narrows this to the true edge-of-formation. |
| **Awkward road bending — poor horizontal alignment (RM-75, § 10.36)** | `Spline/SplineC.cs:353-379` | Node orientation is computed per-node with `Quaternion.LookRotation` to the next node and `Quaternion.identity` at the ends; there is no proper horizontal-curve (spiral/arc) fit, so tight or unevenly spaced nodes read as kinks. RM-75 adds real horizontal alignment on top of guided placement. |
| **`RenderQueue.cs` declares no namespace** (structural hygiene) | `Scripts/RenderQueue.cs` | The only runtime file outside `namespace RoadCityMaster`; a global-namespace type risks collision. Noted for the RM-06/RM-69 boundary+rename work; not behaviour-affecting today. |

## Explicit callouts carried by later tasks

- **`Road` cannot suppress its mesh** → RM-23 / RM-25 / RM-36 need a new spline carrier (Pass 1, question 1).
- **`SplineC` has no exposed closed loop** → RM-28 / RM-61 must thread an `isClosed` flag through the spline
  evaluation or add a closed-curve carrier (Pass 1, question 2).
- **All 10 shaders are Built-in pipeline** → RM-67 audits and RM-68 ports them to URP; a Built-in shader renders
  magenta under URP rather than failing loudly.
- **Content is models, not prefabs** (49 `.prefab` + 187 FBX under `Mesh/`) and **materials resolve by name/GUID
  through `.mat` files** → the RM-48 prefab validator must accept FBX roots (`t:GameObject`) and must not call a
  name-wired material an orphan on GUID evidence alone. (Both already written into RM-48.)
- **`.rao` libraries and many assets load by string path**, not only GUID → a "dead" folder is often live; the
  RM-66 dead-code task must treat a zero-GUID-hit folder as *verify*, not *delete*.

## Sign-off

The scan is **complete**. Four passes were run and each produced its section above:
- **Structural** — all 45 runtime C# files under `Scripts/` (root 29, CrossSection 4, Spline 5, Terrain 7) named
  with namespace, primary type(s), references and dependents.
- **Editor-boundary** — all 18 `Scripts/Editor/` files named with their `#if UNITY_EDITOR` guard (convention holds).
- **Content** — all asset folders (`Flares`, `ManualImages`, `Materials`, `Mesh`, `Physics`, `Prefabs`, `Shaders`,
  `Textures`, `Editor/Icons`, and the four `Editor/Library` sub-folders), all 10 shaders, and the 110 `.rao`
  libraries named with their loading code path.
- **Defect** — the author-reported defects pinned to file and method.

A reviewer picking any file in the vendored tool at random finds it named here. **RM-06 and everything after it may
now start.** This document is the module's memory: keep it in file order, and update it as part of any task that
changes what it describes.
