# Material port: Built-in Standard to URP Lit

RM-68. Every material in Road & City Master was authored against the Built-in render pipeline's Standard shader, and
this project renders with URP 17.5.0. A Built-in shader under URP draws magenta or unlit, so every one of the tool's
471 materials had to move to `Universal Render Pipeline/Lit`. This document records how, and the register it was done
from.

RM-67 is the audit that sized this work: the inventory of what exists, which pipeline each asset targets, and the
evidence for saying so. Read it first — `ShaderInventory.md`. Its Built-in counts are a snapshot dated 2026-08-31, the
denominator this port converts against.

## In place, never replaced

The conversion assigns the URP Lit shader object to the **existing** material asset and re-saves it. Nothing is
created, deleted, renamed, and no `.meta` is written or hand-authored.

That is not tidiness. 336 federal sign faces are reached by a composed string path, `Concrete2` is resolved **by name**
inside 101 FBX files (RM-48), and `Scripts/IntersectionObjects.cs:532` builds `Materials/Signs/InterTLB` + number +
`.mat` at runtime. A freshly created material with a new GUID would satisfy any shader check and break every one of
those references, with no compile error to show for it. Unity mints GUIDs; this port mints none.

## What decides a recipe

Four things, measured off each asset rather than inferred from its folder:

- **The shader it sits on.** Built-in `fileID: 46` is Standard, `fileID: 45` is Standard (specular setup). That
  decides URP's `_WorkflowMode`, and getting it wrong turns every guardrail and every road marking dark.
- **`_Mode`.** Standard's own surface-mode float, and the authority on surface state — a keyword line can be stale.
  Measured: **397** materials at `_Mode: 0` opaque, **37** at `_Mode: 1` cutout, **36** at `_Mode: 2` fade. The render
  queue census agrees exactly: 401 at `-1`, 37 at `2450`, 36 at `3000`, where the 401 counts the four materials that
  already carried URP Lit.
- **The keyword line, verbatim.** A blank Built-in keyword line stays blank even where the texture slot it would
  enable is filled — two shoulders and one marking carry a `_BumpMap` that Built-in never sampled. Enabling it would
  be an improvement, and an improvement is a change.
- **Which family it belongs to**, so 471 materials are 24 reviewable decisions rather than 471.

## The recipes

24 recipes, and their counts sum to 471. The census they rest on was read off disk on 2026-08-31.

<!-- port-recipes-table:begin — generated from MaterialPortRecipes.All(); edit the register, not this table -->

| Recipe | Family | Members | Count | Surface | Workflow | Built-in keywords | URP keywords | Intent | Values from |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| R1 | road and shoulder surfaces | `Road1` | 1 | opaque | metallic | `_EMISSION _NORMALMAP` | `_EMISSION _NORMALMAP` | the default asphalt every new road gets, and the material the three authored intersection surfaces were matched to. `_EMISSION` is carried forward rather than dropped because dropping a keyword is a visible change even when `_EmissionColor` is black, and URP reads the same keyword | measured: `_BaseColor` 0.7238806 grey, `_Glossiness` and albedo tiling read out of `Materials/Road1.mat`; AUTHORED: metallic left at whatever the asset already carried, because Standard's own `_Metallic` is the measured value and URP reads the same 0..1 dielectric scale |
| R2 | road and shoulder surfaces | `Shoulder1` | 1 | opaque | metallic | `_NORMALMAP` | `_NORMALMAP` | the default shoulder, the only other root material on Standard metallic. It meets `Road1` along every road edge, so the two must agree on smoothness or the seam reads as a wet strip | measured out of `Materials/Shoulder1.mat`; AUTHORED: metallic left at whatever the asset already carried, because Standard's own `_Metallic` is the measured value and URP reads the same 0..1 dielectric scale |
| R3 | road and shoulder surfaces | `BrickRoad`, `CobblestoneRoad`, `DirtRoad` | 3 | opaque | specular | `_NORMALMAP` | `_SPECULAR_SETUP _NORMALMAP` | the three road types `Scripts/Road.cs:1601` to `:1611` load by road type. Specular setup, so `_SpecColor` carries the reflectance and `_Metallic` is not the control -- reading it as metallic is the mistake that makes cobble look like sheet metal | measured per asset: `_SpecColor` and `_Glossiness` copied unchanged, `_WorkflowMode` set to 0 |
| R4 | road and shoulder surfaces | `Shoulder2`, `Shoulder3` | 2 | opaque | specular | (blank) | `_SPECULAR_SETUP _NORMALMAP` | the two shoulders that reached the port BROKEN, which is why they are the one recipe that changes what it found rather than preserving it. Both had an empty `_BaseMap` and `_MainTex`, and their `_BumpMap` and `_ParallaxMap` held GUIDs that resolve to nothing -- so Built-in rendered them as flat 0.85 grey with a black `_SpecColor`, a dielectric with zero reflectance, which no real surface is. Preserving that is preserving a defect, so each was given the surfacing it is for: `Shoulder2` the portland cement concrete shoulder, `Shoulder3` the compacted aggregate one | AUTHORED in full -- there was nothing measurable here to preserve. The values come from the two shoulder surfacings AASHTO treats as standard beside the paved one `Shoulder1` already carries. `Shoulder2` takes `Concrete1Diffuse`/`Concrete1Normal` at 0.25 smoothness, the broom-and-tine finish of a PCC shoulder; `Shoulder3` takes `DirtRoadDiffuse`/`DirtRoadNormal` at 0.12, loose aggregate scattering at every angle. Both keep `_Metallic 0` and take `_SpecColor` 0.04, the dielectric F0 concrete and stone share; `_BaseColor` moves to the 0.7238806 grey `Road1` and `Shoulder1` already use, because a shoulder meets the travelled way along its whole length and a different tint reads as a seam. Normals tile at 2x albedo, the ratio `Shoulder1` uses. The dead `_ParallaxMap` is cleared rather than repointed: the tool ships no height map and `_PARALLAXMAP` was never enabled |
| R5 | road and shoulder surfaces | `RumbleStrip` | 1 | fade | specular | `_ALPHABLEND_ON _NORMALMAP` | `_SURFACE_TYPE_TRANSPARENT _NORMALMAP` | the only transparent material outside `Markers/`, and the reason the fade recipe cannot be described as a markings-only case. `_ALPHABLEND_ON` has no URP keyword: the state moves into `_Surface 1` plus `_Blend 0` and the blend factors | measured: `_Mode: 2`, queue 3000, `_SrcBlend 5`, `_DstBlend 10`, `_ZWrite 0` |
| R6 | concrete | `Concrete2Diffuse` | 1 | opaque | metallic | `_EMISSION` | `_EMISSION` | the diffuse-only concrete, and the third and last root material on Standard metallic. No normal map keyword and none assigned, so it is the one concrete that stays flat under grazing light | measured: on `fileID: 46`, confirmed by name against the three root metallic assets |
| R7 | concrete | `Concrete2` | 1 | opaque | specular | `_EMISSION _NORMALMAP` | `_SPECULAR_SETUP _EMISSION _NORMALMAP` | the most-referenced material in the tool: 100 `.rao` entries name it by path and 101 FBX files name it by name (RM-48). It is the one conversion where a wrong value shows up in the most places at once | measured out of `Materials/Concrete2.mat`, every value copied rather than restyled |
| R8 | concrete | `Concrete1`, `Concrete3` to `Concrete11` | 10 | opaque | specular | `_NORMALMAP` | `_SPECULAR_SETUP _NORMALMAP` | the concrete library an author picks kerb and barrier faces from. Ten assets in one identical state, so one recipe applied ten times -- not ten judgements | measured: all ten on `fileID: 45` with the same keyword line; per-asset colours and maps preserved |
| R9 | barriers, rails and metals | `Railing4` | 1 | opaque | specular | `_EMISSION _NORMALMAP` | `_SPECULAR_SETUP _EMISSION _NORMALMAP` | the one metal carrying `_EMISSION`, named in 16 `.rao` entries. Its emission keyword is preserved for the same reason as `Road1`'s | measured out of `Materials/Railing4.mat` |
| R10 | barriers, rails and metals | `Steel1` to `Steel7`, `Galv1` to `Galv5`, `Metal1`, `Metal2`, `Railing7`, `Railing8`, `WBeam`, `WBeamMetal`, `CableBarrier` | 19 | opaque | specular | `_NORMALMAP` | `_SPECULAR_SETUP _NORMALMAP` | the guardrail and cable-barrier metals. Specular setup is doing real work here: a galvanised W-beam is a bright wide-lobe reflector, and re-expressing it as metallic 1 loses the wide lobe and reads as chrome | measured: all 19 on `fileID: 45`; `_SpecColor` and `_Glossiness` copied per asset, `_Metallic` untouched because specular setup ignores it |
| R11 | tunnel | `Tunnel` | 1 | opaque | specular | `_NORMALMAP` | `_SPECULAR_SETUP _NORMALMAP` | the tunnel bore lining, named in 3 `.rao` entries | measured out of `Materials/Tunnel.mat` |
| R12 | tunnel | `TunnelLights` | 1 | opaque | specular | (blank) | `_SPECULAR_SETUP` | the emissive-looking luminaire surface -- and its keyword line is BLANK, so its glow was never `_EMISSION` at all: it was the `Flares/SodiumBulb.flare` Built-in flare, which RM-68 replaced with the `LensFlareDataSRP` at `Flares/SodiumBulb.asset`. Turning `_EMISSION` on here would hide a flare regression behind a material change | measured: keyword line blank, on `fileID: 45` |
| R13 | attenuator and barrel | `Atten`, `RoadBarrel` | 2 | opaque | specular | `_NORMALMAP` | `_SPECULAR_SETUP _NORMALMAP` | the work-zone pair, in one identical state | measured: both on `fileID: 45` with `_NORMALMAP` |
| R14 | lane and intersection markings | 37 cutout markings under `Materials/Markers/`, `InterLaneDisabled` and its `Outer`, `-4L` and `-6L` forms among them | 37 | cutout | specular | `_ALPHATEST_ON` | `_SPECULAR_SETUP _ALPHATEST_ON` | painted markings with hard edges. URP needs the keyword AND the surface state together: `_AlphaClip 1`, `_Surface 0`, `RenderType: TransparentCutout` and queue 2450, plus `_AlphaToMask 1` so the cut edge survives MSAA. `_ALPHATEST_ON` alone renders them opaque, which is the gotcha RM-67 recorded | measured: all 37 at `_Mode: 1`, queue 2450, `_Cutoff: 0.5`, `_SrcBlend 1`, `_DstBlend 0`, `_ZWrite 1`; `_Cutoff` copied per asset rather than assumed to be 0.5 everywhere |
| R15 | lane and intersection markings | 35 blended markings, the `InterCenter-Both-*` set among them | 35 | fade | specular | `_ALPHABLEND_ON` | `_SPECULAR_SETUP _SURFACE_TYPE_TRANSPARENT` | worn and faded markings that read through to the asphalt. This is the recipe with no keyword counterpart at all, so the whole state is carried by floats: `_Surface 1`, `_Blend 0`, `_SrcBlend 5`, `_DstBlend 10`, `_ZWrite 0`, queue 3000, `RenderType: Transparent`. `_BlendModePreserveSpecular` is set to 0, not URP's own default of 1, because Built-in Fade fades specular along with albedo and preserving it makes a worn line glint where it used to dim | measured: all 35 at `_Mode: 2`, queue 3000, `_SrcBlend 5`, `_DstBlend 10`, `_ZWrite 0` |
| R16 | lane and intersection markings | `InterLaneDisabledR` | 1 | opaque | specular | (blank) | `_SPECULAR_SETUP` | the one opaque material in a folder of 74 transparent ones, and the reason this family needs three surface recipes rather than one. Converting it as cutout would put it in queue 2450 and change how it sorts against the markings it sits beside | measured: `_Mode: 0`, queue `-1`, keyword line blank |
| R17 | lane and intersection markings | `RoadDetailOverlay1` | 1 | opaque | specular | (blank) | (blank) | ALREADY PORTED. RM-67 converted it in place while fixing the `ESOSidewalk.rao` path that reaches it, so the operator must find it already on URP Lit and skip it rather than convert it twice. It keeps its row so the register still sums to the whole tree | measured: now `m_Shader` guid `933532a4fcc9baf4fa0491de14d08ed7`, `_WorkflowMode: 0`, keyword line blank; its `.meta` was never touched so every reference to it survived |
| R18 | signals and street furniture | 9 of the 17 `Materials/Signs/` materials, `RoadReflector` among them | 9 | opaque | metallic | `_NORMALMAP` | `_NORMALMAP` | the plain normal-mapped street furniture. Metallic workflow, so `_Metallic` and `_MetallicGlossMap` are the controls and `_SpecColor` is inert | measured: all 9 on `fileID: 46` with `_NORMALMAP` |
| R19 | signals and street furniture | `GreenBlinder`, `RoadConBarrel`, `TrafficCone` | 3 | opaque | metallic | `_NORMALMAP _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` | `_NORMALMAP _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` | smoothness read from the ALBEDO alpha channel, not the metallic map. URP keeps the same keyword but also reads `_SmoothnessTextureChannel`, which must be set to 1 in the same pass -- the keyword alone leaves smoothness coming from the wrong channel and a traffic cone turns glossy | measured: all 3 on `fileID: 46` with that exact keyword pair |
| R20 | signals and street furniture | `InterTLB2`, `TrafficLightMainB` | 2 | opaque | metallic | `_DETAIL_MULX2 _NORMALMAP` | `_DETAIL_MULX2 _NORMALMAP` | the two signal bodies using a detail albedo overlay in multiply-x2 mode. URP Lit supports the same keyword, and `_DetailAlbedoMap`, `_DetailMask` and `_DetailAlbedoMapScale` all carry across by name | measured: both on `fileID: 46` with that exact keyword pair |
| R21 | signals and street furniture | `TrafficLightMain`, `TrafficLightMainLY` | 2 | opaque | metallic | `_EMISSION _NORMALMAP` | `_EMISSION _NORMALMAP` | the two lit signal heads, the only emissive materials in the folder. `Scripts/IntersectionObjects.cs:532` composes `Materials/Signs/InterTLB` plus a number plus `.mat`, so a rename here breaks a BUILT path with no compile error -- the port renames nothing | measured: both on `fileID: 46` with `_EMISSION _NORMALMAP` |
| R22 | signals and street furniture | `SignYieldOnGreen` | 1 | opaque | metallic | (blank) | (blank) | the one sign face in the folder with a blank keyword line | measured: on `fileID: 46`, keyword line blank |
| R23 | federal sign faces | 335 of the 336 `Materials/FedSigns/` materials, across the `330`, `396`, `617`, `861`, `988`, `Diamond` and `Square` sets | 335 | opaque | metallic | `_NORMALMAP` | `_NORMALMAP` | 71 percent of every material in the tool, and the cheapest recipe in the register: one identical state repeated 335 times. Opaque, metallic, normal-mapped -- a sign face is a retroreflective sheet on a flat plate, so there is no cutout and nothing transparent to preserve | measured: all 335 on `fileID: 46` with `_NORMALMAP`, `_Mode: 0`, queue `-1` |
| R24 | federal sign faces | `FedSigns/SignDiamond` | 1 | opaque | metallic | (blank) | (blank) | the single outlier in 336, keyword line blank. It is the parent-folder asset the `Diamond` sets sit under, and it is called out here so a reader does not read `all 336 identical` and stop checking | measured: on `fileID: 46`, keyword line blank; the only `FedSigns` material not carrying `_NORMALMAP` |
<!-- port-recipes-table:end -->

## Built-in to URP, rule by rule

Every row here is a rule `MaterialPortOperator` implements, so the operator can be checked against this table rather
than against memory of what URP does.

<!-- port-mapping-table:begin — generated from MaterialPortRecipes.Mappings(); edit the register, not this table -->

| Built-in | URP Lit | Why it matters |
| --- | --- | --- |
| `m_Shader: {fileID: 46 or 45, guid: 0000000000000000f000000000000000}` | `{fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7}` | the swap itself, done by assigning the Shader OBJECT that `Shader.Find("Universal Render Pipeline/Lit")` returns. The guid is never written as text, and the material's own `.meta` is never touched, so every reference by guid survives |
| `fileID: 46` (Standard) | `_WorkflowMode: 1`, no `_SPECULAR_SETUP` | metallic workflow. `_Metallic` and `_MetallicGlossMap` are the controls |
| `fileID: 45` (Standard, specular setup) | `_WorkflowMode: 0` plus the `_SPECULAR_SETUP` keyword | 115 of the 471 materials. Miss the keyword and URP reads `_Metallic` instead of `_SpecColor`, which turns every guardrail and every marking dark |
| `_Color` | `_BaseColor` | URP's own name. `_Color` is kept as well, because URP Lit still declares it as an obsolete alias and dropping it loses the value on any tool that still reads it |
| `_MainTex` | `_BaseMap` | same texture in both slots for the same reason as `_Color` |
| `_Glossiness` | `_Smoothness` | renamed, same 0..1 meaning. `_Glossiness` is kept as the alias |
| `_GlossyReflections` | `_EnvironmentReflections` | inverted NAME, not inverted meaning: 1 still means reflections on |
| `_Mode: 0` (Opaque) | `_Surface: 0`, `_Blend: 0`, `_AlphaClip: 0`, `_SrcBlend: 1`, `_DstBlend: 0`, `_ZWrite: 1`, `m_CustomRenderQueue: -1`, no `RenderType` tag | 397 of the 471. The Built-in assets carry no `RenderType` tag at all in this state, so none is added |
| `_Mode: 1` (Cutout) with `_ALPHATEST_ON` | `_ALPHATEST_ON` kept, plus `_AlphaClip: 1`, `_Surface: 0`, `_AlphaToMask: 1`, `RenderType: TransparentCutout`, queue 2450 | the keyword alone is not enough in URP -- this is the rule that decides whether 37 markings keep their cut edges |
| `_Mode: 2` (Fade) with `_ALPHABLEND_ON` | NO keyword counterpart. `_SURFACE_TYPE_TRANSPARENT`, `_Surface: 1`, `_Blend: 0`, `_SrcBlend: 5`, `_DstBlend: 10`, `_SrcBlendAlpha: 1`, `_DstBlendAlpha: 10`, `_ZWrite: 0`, `_BlendModePreserveSpecular: 0`, `RenderType: Transparent`, queue 3000 | 36 materials. `_BlendModePreserveSpecular` is deliberately 0 rather than URP's default 1, because Built-in Fade fades specular with albedo |
| `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A` | same keyword plus `_SmoothnessTextureChannel: 1` | URP reads the float as well as the keyword; setting one without the other is silent |
| `_METALLICGLOSSMAP` or `_SPECGLOSSMAP` | `_METALLICSPECGLOSSMAP` | URP declares one keyword for both maps and reads which it is from `_WorkflowMode`. Two Built-in names collapse to one, and keeping either original name leaves the map unsampled. No material in this tool carries either keyword today, and the rule is implemented because a re-authored material can |
| `_DETAIL_MULX2`, `_EMISSION`, `_NORMALMAP`, `_PARALLAXMAP` | the same keyword names, unchanged | carried across verbatim. A blank Built-in keyword line stays blank, even where the texture slot it would enable is filled -- enabling it is an improvement, and an improvement is a change |
| nothing in Built-in | `_ReceiveShadows: 1`, `_QueueOffset: 0`, `_ClearCoatMask: 0`, `_ClearCoatSmoothness: 0`, `_AddPrecomputedVelocity: 0`, `_XRMotionVectorsPass: 1`, `_Cull: 2` | URP-only properties with no Built-in source, written at URP's own defaults so the material inspector opens in a valid state |
<!-- port-mapping-table:end -->

## Three decisions that are not URP's defaults

- **`_BlendModePreserveSpecular: 0`** on all 36 fade materials, where a new URP material would carry 1. Built-in's
  Fade mode fades specular along with albedo; preserving specular makes a worn lane line glint where it used to dim.
- **`_AlphaToMask: 1`** on all 37 cutout materials. URP's cutout path needs it for the cut edge to survive MSAA, and
  Built-in expressed the same thing through its own alpha-test pass state.
- **Keyword lines copied, not corrected.** `_EMISSION` is carried forward on `Road1`, `Concrete2`, `Railing4`,
  `Concrete2Diffuse`, `TrafficLightMain` and `TrafficLightMainLY` even where `_EmissionColor` is black, because
  dropping a keyword is a visible change and URP reads the same keyword name.

## What this document does not claim

That the ported materials look right. A register can prove a material names URP's Lit shader, that its surface state
and keyword line are the ones its Built-in original carried, and that no material anywhere in the tool still points at
`0000000000000000f000000000000000`. Only the editor and a build can prove a pixel. The before-and-after screenshots
under the project's own lighting are the author's to take, and they are the last item RM-68 owes.

`TunnelLights` is the case worth watching there: its keyword line is blank, so its glow was never `_EMISSION` at all —
it was the Built-in `Flares/SodiumBulb.flare` that URP does not draw, and RM-68 replaced it in the same pass with the
`LensFlareDataSRP` at `Flares/SodiumBulb.asset`, attached by `LensFlareComponentSRP` instead of by the dead
`Light.flare` field. A material that looks dead in the tunnel after this port is a flare regression, not a material one.

## Gate

`RCMPORT`, in `MaterialPortGate.cs`. Run it from
**Window > Road City Master > Testing > Material port against the tree**.
