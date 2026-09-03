# Disposition list — RM-66

Every item RM-49's structural and content passes flagged as unreferenced, with the **four searches** recorded and a
judgement: **remove**, **wire up**, or **keep with reason**. Nothing here is deleted by reading it. The list is the
decision record; each removal is a separate, revertible change, and the rule is **one change per disposition group**
with a compile and a suite run after each.

**The table below is generated.** It is spliced in from `DispositionRegister` by `DispositionDocWriter`, and
`DispositionGate` (`RCMDEAD`) fails the build if this file and that register disagree. Edit the register, not the table.

## Why four searches and not one

This tree contains both failure modes already, which is why the bar is four searches per item and not a single
reference sweep.

The loud failure is deleting something live. `Prefabs/Sidewalk.prefab` has **zero** GUID references anywhere under
`Assets/` — no code, no scene, no prefab, no serialised asset — and is named as a plain asset-path string inside
`Editor/Library/ExtrudedObjects/ESOSidewalk.rao`. `Flares/SodiumBulb.flare` is worse: `Prefabs/TunnelLight.prefab`
records `m_Flare: {fileID: 0}`, so the obvious evidence says nothing uses it, while `IntersectionObjects.cs` assigns it
at build time from a concatenated path. And the `.rao` libraries themselves are found by **filename convention** —
`"ESO" + objectName + ".rao"` — so no reference of any kind names those files; renaming one breaks it with no compile
error. RM-48 adds the matching trap for materials: an FBX slot resolves a material **by name**, so a zero-hit GUID
sweep does not prove a material is orphaned.

The quiet failure is the opposite, and RM-29's sidewalk is it: present, unreferenced by code, and plainly a feature an
author would ask for. Deleting it because nothing points at it would remove a feature the tool nearly has, when the
correct action is wiring and an inspector field.

So each item carries all four, and each is recorded even when the answer is "not applicable":

1. a **code search** across every `.cs` in the tool, with comments read as comments and not as references;
2. a **scene and prefab search** by GUID, because a serialised reference is a real dependency a code search cannot see;
3. a **serialised asset search** over the `.rao` object libraries and anything else that names assets;
4. a **string or name search**, because anything loaded by path, composed by convention, or resolved by name survives
   all three previous checks and breaks at runtime.

Only an item that fails all four is a candidate for removal, and `RCMDEAD` enforces exactly that: a `remove` requires
every search negative **and** the two that can always run — code, and string or name — to have actually run rather than
been recorded as not applicable.

## Sequencing

The pass runs **after** § 10's feature work, not before, because those tasks bring currently-unreferenced code back
into use — RM-31's profile machinery, RM-29's sidewalk, the object library's less-used entries — and a pass run first
would delete precisely those.

`RCMDEAD` also asserts that every path in the table **still exists on disk**, including the ones marked for removal.
That is deliberate: it proves the list describes the tree as it is, and that no file was deleted ahead of the change
that removes it and the compile and suite run that follow.

## The list

<!-- disposition-table:begin — generated from DispositionRegister. Do not edit by hand. -->

| Item | Kind | Code search | Scene and prefab search | Serialised asset search | String or name search | Disposition | Why |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `Prefabs/Sidewalk.prefab` | asset | no reference -- no `.cs` file names the prefab; the only `Sidewalk` code hits are the new City layer's own `City/Runtime/Network/Sidewalk.cs`, `CitySidewalkGate.cs` and `GeometryProvenance.cs` | no reference -- GUID `e531c3de6aaa1ac42ae95709665658ca` has zero hits in any `.prefab`, `.unity` or `.asset` under `Assets/` | no reference -- no `.rao` library carries the GUID; the libraries are XML and store paths, not GUIDs | referenced -- HIT: `Editor/Library/ExtrudedObjects/ESOSidewalk.rao` line 3, `<CurrentSplinationString>Assets/RoadCityMaster/Prefabs/Sidewalk.prefab` | **wire up** | The reference case. Load-bearing by string path, so a GUID sweep would call it orphaned and deleting it would break the sidewalk library entry silently. RM-29 owns the wiring and the inspector field that makes it reachable without hand-loading the library. |
| `Mesh/SidewalkCap.FBX` | asset | no reference -- no `.cs` file names the mesh | no reference -- no prefab or scene under `Assets/` references its GUID | no reference -- no serialised asset references its GUID | referenced -- HIT: `ESOSidewalk.rao` lines 4 and 5, `<CurrentSplinationCap1String>` and `Cap2String`, both `Assets/RoadCityMaster/Mesh/SidewalkCap.FBX` | **wire up** | The end cap of the same feature. It travels with RM-29's sidewalk: wiring one without the other produces an extrusion with open ends, which is the defect RM-26 refuses elsewhere. |
| `Editor/Library/ExtrudedObjects/ESOSidewalk.rao` | asset | no reference -- no `.cs` file names the file; `SplinatedMeshMaker.cs:347` and `:353` BUILD the name as `"ESO" + objectName + ".rao"`, so the literal never appears | not applicable -- a `.rao` is an XML library file, never referenced from a scene or prefab -- it is read by the load window, not serialised into a GameObject | no reference -- no other `.rao` references it; each is independent | referenced -- HIT by convention: `SplinatedMeshMaker.cs:347/353/357` compose the path from the object name, and `EdgeObjectMaker.cs:267` enumerates the library folder by filename prefix | **wire up** | Discovered by filename convention, so RENAMING it breaks the entry with no compile error. RM-29's wiring must reach it through the same convention rather than a new hard-coded path. |
| `Editor/Library/Thumbs/Sidewalk.png` | asset | no reference -- no `.cs` file names the image | no reference -- no prefab or scene references its GUID | referenced -- HIT: `ESOSidewalk.rao` line 172, `<thumbString>Assets/RoadCityMaster/Editor/Library/Thumbs/Sidewalk.png` | referenced -- read back through `EdgeObjectMaker.thumbString` and written by `Scripts/Editor/SaveWindow.cs:53` via `EngineIntegration.GetAssetPath`, i.e. a stored path string, never a typed reference | **keep with reason** | The library window's thumbnail for the sidewalk entry. Small, live, and referenced only by a stored path -- exactly the shape of asset a reference sweep deletes. |
| `RoadCityMaster.asmdef` | asset | no reference -- no `.cs` file names the asmdef | no reference -- no scene or prefab references it; an asmdef is never a serialised dependency | no reference -- no `.rao` or `ScriptableObject` names it | referenced -- HIT: `Scripts/RoadEditorUtility.cs:35` passes the literal GUID `e9c7aa1199abeb64c82a4831b3c7286f` -- the GUID of `RoadCityMaster.asmdef.meta` -- to `AssetDatabase.GUIDToAssetPath`, then takes the tool's base path from the result | **keep with reason** | The single most load-bearing file in the tree with no ordinary reference of any kind. The whole tool self-locates through it, so moving the folder is safe and renaming or regenerating the `.meta` is not. Every path-loaded asset below depends on this one lookup succeeding. |
| `Flares/SodiumBulb.flare` | asset | no reference -- RM-49 found `Scripts/IntersectionObjects.cs:796`, `light.flare = EngineIntegration.LoadAssetFromPath<Flare>(basePath + "/Flares/SodiumBulb.flare")`. RM-68 deleted that line: URP reads nothing from `Light.flare`, so it loaded an asset to assign it to a dead field. No `.cs` file names the `.flare` any more | no reference -- `Prefabs/TunnelLight.prefab` line 56 has `m_Flare: {fileID: 0}` -- the prefab never carried it, which is why deleting the assignment left no prefab to repair | no reference -- no serialised asset references its GUID `d3b176ff91de5b14db8c62f5ec3f211a` | referenced -- HIT as prose, not as a load: `Scripts/SodiumFlare.cs` names `Flares/SodiumBulb.flare` in its header as the source of every constant it carries, and `Scripts/Editor/ShaderInventory.cs` inventories it as the superseded row beside `Flares/SodiumBulb.asset` | **keep with reason** | Superseded by RM-68 rather than deleted. The `LensFlareDataSRP` at `Flares/SodiumBulb.asset` now draws the bulb, and this file is where every number in it came from -- element size 25, `m_UseLightColor: 0`, `m_Rotate: 0`, `m_UseFog: 1`. Deleting it would leave the ported values with no source, and it costs nothing on disk because no code path loads it. |
| `RoadCityMaster.RigidBody` | type | referenced -- HIT: `Scripts/IntersectionObjects.cs:479`, `_obj.AddComponent<RoadCityMaster.RigidBody>()` -- added at build time, so no field and no inspector names the type | no reference -- no prefab or scene carries the component; it is only ever added in code | no reference -- no serialised asset names it | no reference -- no string or `AddComponent("RigidBody")` by name; the generic overload is used | **keep with reason** | Live, but only just: its whole remaining behaviour is `Awake()` destroying the `Rigidbody` on the same object. That is worth a comment rather than a deletion, because the removal it performs is what keeps dropped intersection objects from falling. RM-102 may fold it into the placer. |
| `RigidBody.minCollisionVelocity` | field | no reference -- the only read is `Scripts/RigidBody.cs:33`, which is INSIDE the commented-out block spanning lines 22 to 77; no live line reads it | no reference -- no `.prefab`, `.unity` or `.asset` under `Assets/` serialises `minCollisionVelocity`, so no saved value would be lost | no reference -- no serialised library names it | no reference -- no string names it; it is not read through `SerializedObject.FindProperty` either | **remove** | Scaffolding, not an unfinished feature: the abandoned collision-sleep experiment that reads it is commented out above it. The field and that block are ONE disposition group -- removing the field alone would leave a comment referring to something that no longer exists. |
| `Navigation.InitResetNavigationData` | method | referenced -- HIT, but internal only: `Scripts/Navigation.cs:11`, the first statement of `UpdateConnectedNodes()`. No caller outside the file exists anywhere under `Assets/` | not applicable -- a static method cannot be serialised into a scene or prefab | not applicable -- a static method cannot appear in a serialised asset | no reference -- no `Invoke`, `SendMessage` or reflection call names it | **keep with reason** | Keep the code, narrow the promise: it is used, so it is not dead, but `public` advertises an entry point nothing outside the file uses. Its visibility is the finding, not its existence. |
| `RoadCityMaster.Navigation` | type | referenced -- HIT: `Scripts/Intersections.cs:337`, `Navigation.UpdateConnectedNodes()`. The other apparent hit, `Scripts/Spline/SplineN.cs:50`, is the comment `//Navigation:` and is NOT a reference | not applicable -- a static class cannot be serialised | not applicable -- a static class cannot appear in a serialised asset | no reference -- no string or reflection use | **keep with reason** | Live. Recorded because a case-insensitive or comment-blind scan reports two references here and one of them is prose -- the same mistake that makes a scan's raw count untrustworthy as evidence. |
| `RoadCityMaster.ThreadedJob` | type | referenced -- HIT three times as a BASE CLASS: `Scripts/RoadCalcs1.cs:6`, `Scripts/RoadCalcs2.cs:6` and `Scripts/Terrain/TerrainCalcs.cs:6` | no reference -- not a component; nothing serialises it | no reference -- no serialised asset names it | no reference -- no string or reflection use | **keep with reason** | Live, and recorded because inheritance is the reference shape a call-site search misses: nothing ever writes `ThreadedJob.` anywhere, so a member-usage scan reports it as unused. |
| `RoadCityMaster.RenderQueue` | type | no reference -- zero hits outside its own declaration: `\bRenderQueue\b` across every `.cs` in the tool returns only `Scripts/RenderQueue.cs` itself, and no constant of it is read anywhere | not applicable -- a static class of `const int` cannot be serialised | no reference -- no serialised asset names it | no reference -- no material, shader or string names it; queue values are set through `material.renderQueue` | **remove** | An abandoned utility carried in from another project, not an unfinished feature of this one: its layers are `ParallaxLayers`, `Tracks`, `Routes` and `IndicatorRings`, which belong to a 2D map-style renderer. Its own comment says setting `material.renderQueue` directly is easier, and that is what RM-68's URP port does. Nothing in the road tool loses a capability. |
| `Materials/Concrete2Diffuse.mat` | asset | no reference -- no `.cs` file names the material | no reference -- GUID `a09bfe33f517abd47820653eeb108e28` has zero hits in any `.prefab` or `.unity` under `Assets/` | no reference -- no `.rao` library names it; the sidewalk and road entries name `Road1.mat` and `RoadDetailOverlay1.mat` | no reference -- no FBX in the tree contains the literal `Concrete2Diffuse`, so no FBX slot resolves it BY NAME -- the check RM-48 requires before any material may be called orphaned | **remove** | The one material that survives all four searches, and it survives the name check RM-48 added as well. `Concrete2.mat` exists beside it and is referenced; this is a leftover duplicate whose name reads like a texture. Removal is deferred to RM-67's shader inventory, which reads every material in the tree anyway and is the cheapest place to be certain. |
<!-- disposition-table:end -->

Three items are marked for removal, three for wiring up, and seven for keeping with a stated reason. The keeps
outnumber the removals, which `RCMDEAD` also checks: in a tree where assets load by string path, a disposition list
that is mostly deletions is evidence about the pass rather than about the tree.
