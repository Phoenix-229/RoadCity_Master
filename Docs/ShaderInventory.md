# Shaders and materials: what this tool renders with, and what it renders with under URP

RM-67. This document is the render-side inventory of Road & City Master: every shader, material, shader graph and
render-related asset in the tool, each marked with the pipeline it targets and the declaration in the asset that proves
it. RM-68 ports from this table. It is not a plan — it is what is on disk today.

**The pipeline counts below are RM-67's snapshot, taken 2026-08-31, of the tree as it was found.** They are the
denominator RM-68 works against, not a live tally: every material RM-68 converts leaves the two Built-in fileIDs, so
after the port the counts here describe where the port started rather than where the tree is. The live, post-conversion
state is RM-68's own register and gate. The one place this document is kept live is the repaired-reference table at the
end, because a fix that stops being true is a regression rather than history.

Four of its tables are generated from registers in code and spliced in between markers. Edit
`Scripts/Editor/ShaderInventory.cs` and `Scripts/Editor/PipelineAudit.cs`, then run
**Window > Road City Master > Documentation > Write the shader and material inventory**. `RCMSHADER` fails if a table
here has drifted from its register.

## The two findings that set the size of the port

**The tool's own ten shaders are unreachable as shipped.** Every one of them is a Built-in surface or hand-written
forward/deferred shader, and every one of them has zero references: their GUIDs appear in no `.mat`, `.prefab`,
`.unity` or `.asset` under `Assets/`, and no `Shader.Find` call exists anywhere in the tool. Nothing renders with them
today, so porting them is not what makes the tool work under URP.

**All 471 materials sit on exactly two Built-in engine shaders.** 356 on fileID 46 (Standard) and 115 on fileID 45
(Standard, specular setup), both inside Unity's built-in resources GUID `0000000000000000f000000000000000`. That is
the port: one conversion recipe applied to two source shaders, repeated across eight material families, not ten shader
rewrites. There are zero shader graphs and zero standalone `.hlsl` or `.cginc` includes in the tree — both counted, not
assumed.

## Two different causes of the same magenta

A material renders magenta when its shader is missing under the active pipeline, and *also* when the material itself is
null. Both look identical in the Scene view, and this tool produces both:

- a Built-in shader under URP — the pipeline case, which RM-68 fixes by porting;
- a material loaded from a string path that does not resolve — the wiring case, listed in
  [Materials something loads that were not there](#materials-something-loads-that-were-not-there), which no amount of
  shader porting will fix.

Separating them before the port starts is the point of auditing both in one pass. A port that chases the second cause
with shader edits will "fix" it by accident at best.

## The inventory

Ten shaders, eight material families that partition all 471 materials, and one lens flare. Every row is marked
Built-in, URP or pipeline-agnostic — never unknown. That is a guarantee of the type rather than of this document:
`PipelineTarget` has no `Unknown` member, so a row cannot claim one, and `RCMSHADER` fails if a fourth member is ever
added.

<!-- inventory-table:begin (generated from ShaderInventory.Table(); edit the register, not this) -->

| Asset | Kind | Count | Pipeline | Evidence | Used by | Port |
| --- | --- | --- | --- | --- | --- | --- |
| `Shaders/RoadShader.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 78 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/Roads/Road Shader"` and keeps `Offset -1, -1` in its forward, depth and depth-normals passes and takes smoothness from the base map's alpha through `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/RoadShaderDX11.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 74 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/DX11/Road Shader"` and carries no polygon offset, which is what makes it the surface for a road nothing is laid over | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/RoadShaderLow.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 59 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/Roads/Road Shader Low Quality"` and keeps `Offset 1, 1` -- positive, pushing away from the camera, so a full-quality road wins where the two overlap -- and `Queue Geometry+10` | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/RoadShaderLowDX11.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 51 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/DX11/Road Shader Low Quality"` and keeps `Queue Geometry+10` and carries no polygon offset | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/ShoulderDirt.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 68 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/Roads/Road Shoulder Shader"` and keeps `Offset -1, -1` and defaults smoothness to 0.15 for dry unsealed ground | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/ShoulderDirtDX11.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 58 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/DX11/Road Shoulder Shader"` and defaults smoothness to 0.15 and carries no polygon offset | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/StandardOffset.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 84 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/CutoutMarker"` and keeps `Offset -3, -2` and clips on alpha in every pass including `ShadowCaster` and `Meta`, with `AlphaToMask On` for the edge the clip would otherwise leave stepped | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/StandardTiling.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 74 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/Standard-Tiling"` and is the plain opaque tiling surface its name describes, with no offset and no alpha clip, because the Built-in file was a byte-for-byte copy of StandardOffset.shader apart from its first line | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/TransparentWithShadows.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 76 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/TranShadow"` and blends `SrcAlpha OneMinusSrcAlpha` with `ZWrite Off`, casts a clipped shadow, and no longer carries the Built-in second pass that faked shadow reception by compositing black alpha | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/TransparentWithShadowsZ.shader` | shader | 1 | URP | URP shader: `Tags { "RenderPipeline" = "UniversalPipeline" }`, a `UniversalForward` pass shading through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` and `Meta` passes taken from the URP package rather than hand-copied, pulled in at line 77 by `#include_with_pragmas`; declares itself `Shader "RoadCityMaster/TranShadowMarker"` and blends transparently with `Offset -3, -2` and declares no `ShadowCaster`, because paint on a road would cast its shadow onto the surface it is painted on | nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader.Find` call exists anywhere in the tool | RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its `#pragma surface surf BlinnPhong` are gone from the file entirely |
| `Shaders/Include/RoadSurfaceInput.hlsl` | HLSL include | 1 | URP | declares the `UnityPerMaterial` constant buffer and builds URP's `SurfaceData`; includes URP's own `ShaderLibrary/Core.hlsl` and `ShaderLibrary/SurfaceInput.hlsl`, and exposes `InitializeStandardLitSurfaceData` under that exact name so URP's shipped `LitMetaPass.hlsl` links | every pass of all ten: `Shaders/RoadShader.shader`, `Shaders/RoadShaderDX11.shader`, `Shaders/RoadShaderLow.shader`, `Shaders/RoadShaderLowDX11.shader`, `Shaders/ShoulderDirt.shader`, `Shaders/ShoulderDirtDX11.shader`, `Shaders/StandardOffset.shader`, `Shaders/StandardTiling.shader`, `Shaders/TransparentWithShadows.shader`, `Shaders/TransparentWithShadowsZ.shader` | RM-68 -- written by the port; there was no Built-in equivalent to convert |
| `Shaders/Include/RoadSurfaceForwardPass.hlsl` | HLSL include | 1 | URP | the shared `UniversalForward` pass: `UniversalFragmentPBR`, `MixFog`, `OutputAlpha`, `SAMPLE_GI` and `SAMPLE_SHADOWMASK` from URP's `ShaderLibrary/Lighting.hlsl`. Its structs are named `RoadAttributes` and `RoadVaryings` so they cannot collide with the shipped passes' own | the `ForwardLit` pass of all ten: `Shaders/RoadShader.shader`, `Shaders/RoadShaderDX11.shader`, `Shaders/RoadShaderLow.shader`, `Shaders/RoadShaderLowDX11.shader`, `Shaders/ShoulderDirt.shader`, `Shaders/ShoulderDirtDX11.shader`, `Shaders/StandardOffset.shader`, `Shaders/StandardTiling.shader`, `Shaders/TransparentWithShadows.shader`, `Shaders/TransparentWithShadowsZ.shader` | RM-68 -- written by the port; it replaces ten copies of a Built-in surface function |
| `Shaders/Include/RoadSurfaceForwardPragmas.hlsl` | HLSL include | 1 | URP | the shared `multi_compile` variant set, pulled in with `#include_with_pragmas`; carries `_CLUSTER_LIGHT_LOOP`, which is the current name of what URP called `_FORWARD_PLUS` | the `#include_with_pragmas` line of all ten: `Shaders/RoadShader.shader`, `Shaders/RoadShaderDX11.shader`, `Shaders/RoadShaderLow.shader`, `Shaders/RoadShaderLowDX11.shader`, `Shaders/ShoulderDirt.shader`, `Shaders/ShoulderDirtDX11.shader`, `Shaders/StandardOffset.shader`, `Shaders/StandardTiling.shader`, `Shaders/TransparentWithShadows.shader`, `Shaders/TransparentWithShadowsZ.shader` | RM-68 -- written by the port; a variant set the ten shaders must agree on has one owner |
| `Materials/ -- road and shoulder surfaces` | material family | 8 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: Road1 and Shoulder1 on `fileID: 46` (Standard); BrickRoad, CobblestoneRoad, DirtRoad, Shoulder2, Shoulder3 and RumbleStrip on `fileID: 45` (Standard, specular setup). Road1 also serialised `m_ShaderKeywords: _EMISSION _NORMALMAP`, both of which URP's Lit reads under the same names | `Scripts/Road.cs:1506` (Road1) and `:1537` (Shoulder1) are the defaults a new road gets; `:1601`, `:1606` and `:1611` load DirtRoad, BrickRoad and CobblestoneRoad by road type | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. These eight are the surfaces an author sees first, so they were the port's front line |
| `Materials/ -- concrete` | material family | 12 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: Concrete2Diffuse on `fileID: 46`; Concrete1 to Concrete11 on `fileID: 45` | `Materials/Concrete2.mat` is named by PATH in 100 `.rao` library entries and at `Scripts/Editor/SplineNEditor.cs:1635` and `:1741`; Concrete11 in 12 entries. The literal `Concrete2` also appears inside 101 FBX files, so those slots resolve it BY NAME (RM-48) | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. Concrete2 is the most-referenced material in the tool by a wide margin, which is exactly why it was rewritten in place instead of replaced |
| `Materials/ -- barriers, rails and metals` | material family | 20 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: Steel1 to Steel7, Galv1 to Galv5, Metal1, Metal2, Railing4, Railing7, Railing8, WBeam, WBeamMetal and CableBarrier, every one on `fileID: 45` (Standard, specular setup), so each one's specular colour and gloss had to become URP's metallic workflow rather than being copied across | named by PATH in the `.rao` libraries -- Steel7 in 20 entries, Railing4 in 16, Railing7 in 8, Metal2 in 6, WBeam in 5 -- and Steel7 also at `Scripts/Editor/SplineNEditor.cs:1932` and `:1936`. `WBeam` appears inside 4 FBX files, so those resolve it by name | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. A specular-setup metal is the case where a line-by-line port looks wrong fastest, so the operator derives metallic and smoothness instead of transcribing them |
| `Materials/ -- tunnel` | material family | 2 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: Tunnel and TunnelLights, both `fileID: 45` (Standard, specular setup) | named by PATH in 3 `.rao` entries each; TunnelLights is the emissive surface that pairs with the two `Flares/SodiumBulb` rows below, and the surface and the flare were ported in the same pass | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. TunnelLights keeps its emission, which is what makes the flare read as a lit bulb rather than a sprite hanging in the air |
| `Materials/ -- attenuator and barrel` | material family | 2 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: Atten and RoadBarrel, both `fileID: 45` (Standard, specular setup) | Atten is reached only through the `.rao` extruded-object libraries and author selection; RoadBarrel pairs with `Materials/Signs/RoadConBarrel.mat` in the work-zone objects | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. Two assets, one repeat of the same conversion the twenty metals took |
| `Materials/Markers/ -- lane and intersection markings` | material family | 74 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: all 74 on `fileID: 45` (Standard, specular setup), and the family that carries the tool's transparency: 37 materials serialised `_ALPHATEST_ON`, 35 serialised `_ALPHABLEND_ON`, and 2 carried neither keyword (InterLaneDisabledR and RoadDetailOverlay1), so the family needed three recipes rather than one and the operator branches on which keyword each file actually had | `Scripts/Road.cs:1279` to `:1281` load WhiteYellowDouble in its 2L, 4L and 6L forms and `:1514` to `:1530` the TireMarks set; `Scripts/Intersections.cs` and `IntersectionObjects.cs` load the `Inter*` markings. Only ONE of the 74 is referenced by GUID from any prefab or scene | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. Markings are the most sensitive to a port, and `_ALPHABLEND_ON` has no URP counterpart keyword at all: the 35 that had it now carry `_Surface: 1` with `_Blend: 0` and the transparent queue the operator sets explicitly, because URP does not derive the queue on load |
| `Materials/Signs/ -- signals and street furniture` | material family | 17 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: all 17 on `fileID: 46` (Standard); 15 of the 17 ARE referenced by GUID from prefabs, the highest proportion in the tree, so an in-place rewrite was the only conversion those prefabs survive | `Scripts/IntersectionObjects.cs:532` composes `Materials/Signs/InterTLB` + number + `.mat`, so the signal-head materials are reached by a BUILT path; RoadReflector is named in 14 `.rao` entries | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. The composed path means a rename breaks it with no compile error, which is the second reason nothing here was replaced with a new asset |
| `Materials/FedSigns/ -- federal sign faces` | material family | 336 | URP | on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after `Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and `.meta` so every path- and name-based reference below still resolves. Before the port: all 336 on `fileID: 46` (Standard). This one family is 71 percent of every material in the tool | NONE of the 336 is referenced by GUID from any prefab, scene or asset. Twelve are named by PATH in the `EOMSign*.rao` edge-object libraries as each library entry's default face; the remaining 324 are an author-facing library the author selects from on the placed sign | RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and `_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`. The largest count and the cheapest conversion: one repeatable albedo recipe applied 336 times by the operator, not 336 decisions |
| `Flares/SodiumBulb.flare` | lens flare | 1 | Built-in | the Built-in lens flare asset class, `--- !u!121` `Flare` with `m_FlareTexture`, `m_Elements` and `m_UseFog`; URP draws nothing for `Light.flare`, and no in-place edit fixes that because the asset CLASS is what URP does not read | nothing any more -- `Scripts/IntersectionObjects.cs` no longer assigns `Light.flare`; the file is kept as the source of record for every number `Scripts/SodiumFlare.cs` carried across | RM-68 -- superseded rather than converted, which is why both rows exist |
| `Flares/SodiumBulb.asset` | lens flare | 1 | URP | a `LensFlareDataSRP` minted by Unity through `Scripts/Editor/FlarePortOperator.cs`, holding one procedural `SRPLensFlareType.Circle` element in place of the Built-in file's image -- the amber lived in the `.tga`'s pigment and a tint can only darken an image, so an LED luminaire's white bloom has to be drawn rather than sampled -- additive and unmodulated by light colour as `m_UseLightColor: 0` required | `Scripts/SodiumFlare.cs` loads it and attaches a `LensFlareComponentSRP`, called from `Scripts/IntersectionObjects.cs` for every point light on a street-lamp prefab | RM-68 -- done here rather than deferred to RM-91, which now has nothing left to port |
<!-- inventory-table:end -->

## The project this ports into

"URP" is not a specification. A port validated against one URP version can fail on another, and variant stripping can
remove a variant a shader relies on and leave a material that works in the editor and not in a build (Q-20). So the
target is read out of this project's own files, with the line each value came from, rather than assumed:

<!-- pipeline-facts-table:begin (generated from PipelineAudit.FactsTable(); edit the register, not this) -->

| Setting | Value in this project | Read from |
| --- | --- | --- |
| Universal render pipeline package | 17.5.0 | `Packages/manifest.json:3` and `Packages/packages-lock.json:55` |
| URP config package | com.unity.render-pipelines.universal-config 17.5.0 | `Packages/packages-lock.json:64` to `:65` |
| Render pipeline asset in force | `Assets/UniverseCreator/Settings/UC_URP_Pipeline.asset` | `ProjectSettings/GraphicsSettings.asset:49`, `m_CustomRenderPipeline` guid `7a2f2dcaa3b8d5d47a898e35c7cdfbd2` |
| Renderer | Universal renderer, `m_RendererType: 1` | `UC_URP_Pipeline.asset:17` |
| SRP batcher | enabled, `m_UseSRPBatcher: 1` | `UC_URP_Pipeline.asset:72` |
| MSAA | `m_MSAA: 1` | `UC_URP_Pipeline.asset:28` |
| Terrain holes | supported, `m_SupportsTerrainHoles: 1` | `UC_URP_Pipeline.asset:25` |
| Main light shadows | supported at 2048, `m_MainLightShadowsSupported: 1` | `UC_URP_Pipeline.asset:45` to `:46` |
| Additional light shadows | NOT supported, `m_AdditionalLightShadowsSupported: 0` | `UC_URP_Pipeline.asset:49` |
| Shadow distance and cascades | 50 m, one cascade, `m_ShadowDistance: 50` and `m_ShadowCascadeCount: 1` | `UC_URP_Pipeline.asset:57` to `:58` |
| Strip unused shader variants | ON, `m_StripUnusedVariants: 1` | `Assets/UniversalRenderPipelineGlobalSettings.asset:23` |
| Strip runtime debug shaders | ON, `m_StripRuntimeDebugShaders: 1` | `Assets/UniversalRenderPipelineGlobalSettings.asset:19` |
| Strip screen coordinate override variants | ON, `m_StripScreenCoordOverrideVariants: 1` | `Assets/UniversalRenderPipelineGlobalSettings.asset:24` |
| Strip unused post-processing variants | off, `m_StripUnusedPostProcessingVariants: 0` | `Assets/UniversalRenderPipelineGlobalSettings.asset:22` |
| Built-in stripping settings | all left automatic: `m_LightmapStripping: 0`, `m_FogStripping: 0`, `m_InstancingStripping: 0`, `m_BrgStripping: 0` | `ProjectSettings/GraphicsSettings.asset:55` to `:58` |
| Always-included shaders | three, all from Unity's built-in resources; none of the tool's own | `ProjectSettings/GraphicsSettings.asset:29` to `:32` |

<!-- pipeline-facts-table:end -->

The three that most often break a port when they are assumed instead of read: the URP version itself
(**17.5.0**), `m_StripUnusedVariants: 1`, and `m_AdditionalLightShadowsSupported: 0` — a ported shader that adds an
additional-light shadow path is writing code this project's pipeline asset will never run.

## Gotchas already paid for

Each of these cost this project a compile cycle or a wrong-looking frame once. They are recorded so RM-68 starts from
them:

<!-- gotchas-table:begin (generated from PipelineAudit.GotchasTable(); edit the register, not this) -->

| Rule | What it costs when rediscovered |
| --- | --- |
| A URP shader block opens `HLSLPROGRAM` and closes `ENDHLSL`. A `CGPROGRAM` / `ENDCG` pair is a Built-in tell, and all ten shaders in this tool have one | the whole file fails to compile, and the error names the closing token rather than the opening one |
| A pass name may not be empty. `Name ""` is a PARSE error, not a warning | the shader is dropped and every material on it renders magenta, which reads as a pipeline problem |
| A custom `ShadowCaster` pass must declare its own vertex and fragment entry points and pragma set; there is no Built-in pass to inherit | shadows disappear silently -- the material renders correctly and casts nothing |
| A cutout material needs its keyword and its surface state set together -- `_ALPHATEST_ON` alone is not enough in URP, which also reads `_Surface` and the render queue override | the material renders opaque, so 37 of the 74 marking materials would lose their cutout |

<!-- gotchas-table:end -->

## Materials something loads that were not there

Found by the audit rather than looked for: these four paths are loaded by name at build time and none of them resolved.
Three were absent assets; the fourth was a wrong path to an asset that exists elsewhere, in the very library entry
RM-66 marked *wire up*. **All four are fixed.** The three absent materials were authored as URP Lit against one asphalt
recipe, and the wrong path was corrected in the `.rao` entry rather than by moving the asset, because the asset was
never the problem. `RCMSHADER` case 7 now requires each path to resolve **and** each material to name URP's Lit shader,
so an entry cannot sit in this table describing work that was not done, and a material cannot be repaired by leaving it
on a Built-in shader.

<!-- missing-material-table:begin (generated from PipelineAudit.MissingTable(); edit the register, not this) -->

| Path loaded | Loaded by | What was actually wrong | Fix applied |
| --- | --- | --- | --- |
| `Materials/RoadIntersection.mat` | `Scripts/Intersections.cs:639`, through `RoadEditorUtility.SetRoadMaterial` | the intersection surface itself. Nothing of that name existed under `Materials/`, so the renderer was given null on every intersection build | AUTHORED as URP Lit opaque against the asphalt recipe -- `Textures/Road1Diffuse` and `Road1Normal` at 4x4, `_BaseColor` 0.7238806 grey copied from `Materials/Road1.mat` so the intersection reads as the same asphalt as the road that meets it, smoothness 0.2, metallic 0. No `.meta` was hand-written: Unity mints the GUID on import |
| `Materials/InterText.mat` | `Scripts/Intersections.cs:777`, through `RoadEditorUtility.SetRoadMaterial` | the four `outer` apron quads that extend each intersection into its approach legs -- built by `CreateIntersectionMeshOuterInternal`, UV 0..1 per quad. Absent, so the apron was built and then rendered with no material | AUTHORED as URP Lit opaque on the same asphalt recipe, but at `Road1.mat`'s own 4x2 albedo and 8x4 normal tiling, because the apron runs ALONG each leg and must not show a tiling seam where it meets the road surface |
| `Materials/InterMainPlate1.mat` | `Scripts/RoadConstructorBufferMaker.cs:1650` and `:1677` | the intersection main plate, both the back and front mesh halves. Absent | AUTHORED as URP Lit opaque on the asphalt recipe at 4x4, identical to the intersection surface, because the plate and the surface are the same pavement seen from two mesh halves |
| `Materials/Markers/RoadDetailOverlay1.mat` | `Editor/Library/ExtrudedObjects/ESOSidewalk.rao:9`, `<SplinatedMaterial2String>` | a WRONG path rather than a missing asset: the entry named `Materials/RoadDetailOverlay1.mat` while the file has always been at `Materials/Markers/RoadDetailOverlay1.mat`. RM-29's sidewalk wiring loads this entry, so it got a null second material -- the one item RM-66 marked wire up meeting the one path that was wrong | FIXED IN PLACE: the `.rao` entry now names `Assets/RoadCityMaster/Materials/Markers/RoadDetailOverlay1.mat`. No asset was authored and none moved, because the asset was never the problem -- one library string was |

<!-- missing-material-table:end -->

## The libraries, which are the port's blind spot

The 110 `.rao` files under `Editor/Library/` are XML, so no `.mat` sweep and no GUID search can see them. They name
prefabs, thumbnails and materials by **path** as text, and `Scripts/EdgeObjectMaker.cs:433`,
`Scripts/SplinatedMeshMaker.cs:741` and `:808` resolve each one through `EngineIntegration.LoadAssetFromPath<T>`, which
returns null for a path that no longer exists. A null there is not an error: the entry loads, the object it should have
carried is simply absent, and nothing is logged.

Every path in every library was read and resolved against the project. **275 distinct paths, 29 of them materials.** All
29 materials are on URP Lit after RM-68, and one prefab path was wrong:
`Editor/Library/ExtrudedObjects/ESORailing5.rao` lines 105 and 106 named
`Assets/RoadCityMaster/Prefabs/Railing/Railing5Post.prefab`, where the folder on disk is `Railings`. Railing #5 had been
silently building with no end cap at either end. **Fixed in place**, the same way `ESOSidewalk.rao` was: the library
string was corrected and no asset was moved, because the asset was never the problem.

`RCMSHADER` case 13 now walks all 110 libraries on every run, requires every `Assets/…` path in them to load, and
requires every material among them to be on `Universal Render Pipeline/Lit` — asked of the loaded `Material.shader`
rather than matched against a GUID literal, because a material that loads has a real shader object to ask.

## Sign-off

The bar RM-67 closes against: every shader and material in the tree is named; each is marked Built-in, URP or
pipeline-agnostic with the evidence read out of the asset; nothing is marked unknown; and the material families sum to
the counted total so the inventory describes the whole tree rather than a sample.

What this document does *not* claim: that any of it renders correctly. A register can prove a claim is evidenced, that a
path resolves and that a material names URP's Lit shader rather than a Built-in one; only the editor and a build can
prove a pixel. The four broken material references above are fixed and checked as fixed. RM-68 owns the port and did all
three parts of it in one pass -- the surfaces and markings, the ten shaders, and the lens flare, which could not be
converted in place because URP reads nothing from `Light.flare` and so needed a `LensFlareDataSRP` minted beside it.
RM-91, which was written to own the flare, has nothing left to port.
