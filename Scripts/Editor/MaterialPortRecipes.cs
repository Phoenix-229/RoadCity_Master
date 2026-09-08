// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-68: the surface state a material is in, read from the Built-in Standard shader's own `_Mode`
    /// float rather than guessed from its keywords. `_Mode` is authoritative because a stale keyword can disagree
    /// with it, and the render queue and the `RenderType` tag both follow `_Mode`. </summary>
    public enum PortSurface
    {
        /// <summary> `_Mode: 0`. No queue override, no `RenderType` tag, `_SrcBlend 1` / `_DstBlend 0`, ZWrite on. </summary>
        Opaque = 0,
        /// <summary> `_Mode: 1`. Queue 2450, `RenderType: TransparentCutout`, `_ALPHATEST_ON`, ZWrite on. </summary>
        Cutout = 1,
        /// <summary> `_Mode: 2`. Queue 3000, `RenderType: Transparent`, `_ALPHABLEND_ON`, `_SrcBlend 5` /
        /// `_DstBlend 10`, ZWrite OFF. Built-in calls this Fade, and it fades specular along with albedo. </summary>
        Fade = 2,
    }


    /// <summary> Which of Standard's two shaders a material sits on, which decides URP's `_WorkflowMode`. </summary>
    public enum PortWorkflow
    {
        /// <summary> Built-in `fileID: 46`, Standard. URP `_WorkflowMode: 1`, no `_SPECULAR_SETUP`. </summary>
        Metallic = 0,
        /// <summary> Built-in `fileID: 45`, Standard (specular setup). URP `_WorkflowMode: 0` plus
        /// `_SPECULAR_SETUP`, and `_SpecColor` / `_SpecGlossMap` carry the reflectance instead of `_Metallic`. </summary>
        Specular = 1,
    }


    /// <summary> One recipe: every material that shares a family, a surface state, a workflow and a keyword state,
    /// converted the same way. The recipe is the unit of review -- 471 materials are 24 decisions, not 471. </summary>
    public sealed class MaterialRecipe
    {
        public readonly string id;
        public readonly string family;
        public readonly string members;
        public readonly int count;
        public readonly PortSurface surface;
        public readonly PortWorkflow workflow;
        /// <summary> The Built-in keyword line, verbatim from `m_ShaderKeywords`. Empty means the line is blank. </summary>
        public readonly string builtInKeywords;
        /// <summary> The keyword line the ported material carries. </summary>
        public readonly string urpKeywords;
        /// <summary> Why this recipe exists as its own row, and what the port must preserve. </summary>
        public readonly string intent;
        /// <summary> Where the numbers come from: measured out of the asset, or AUTHORED with the reason. </summary>
        public readonly string provenance;

        public MaterialRecipe(string id, string family, string members, int count, PortSurface surface,
            PortWorkflow workflow, string builtInKeywords, string urpKeywords, string intent, string provenance)
        {
            this.id = id;
            this.family = family;
            this.members = members;
            this.count = count;
            this.surface = surface;
            this.workflow = workflow;
            this.builtInKeywords = builtInKeywords;
            this.urpKeywords = urpKeywords;
            this.intent = intent;
            this.provenance = provenance;
        }

        public string CountText { get { return count.ToString(CultureInfo.InvariantCulture); } }
    }


    /// <summary> One row of the Built-in to URP translation: what changes name, what changes meaning, and what has
    /// no counterpart at all. Every row is a rule the conversion operator implements, so a reader can check the
    /// operator against this table rather than against memory of what URP does. </summary>
    public sealed class PortMapping
    {
        public readonly string builtIn;
        public readonly string urp;
        public readonly string note;

        public PortMapping(string builtIn, string urp, string note)
        {
            this.builtIn = builtIn;
            this.urp = urp;
            this.note = note;
        }
    }


    /// <summary> RM-68's register: how all 471 Built-in materials become URP Lit materials, as 24 recipes.
    ///
    /// The counts here were measured off disk on 2026-08-31, the same census RM-67 snapshots, and they must sum to
    /// <see cref="RecipeTotal"/>. The census that shapes them is the Standard shader's `_Mode` float, not the keyword
    /// line: 397 of the 471 are `_Mode: 0` opaque, 37 are `_Mode: 1` cutout and 36 are `_Mode: 2` fade, and the render
    /// queue census agrees exactly -- 401 at `-1`, 37 at `2450`, 36 at `3000` (the 401 counts the four materials
    /// already carrying URP Lit).
    ///
    /// One material, `Materials/Markers/RoadDetailOverlay1.mat`, is ALREADY on URP Lit: RM-67 ported it in place to
    /// prove the wiring fix it belongs to. It keeps its recipe row so the sum still describes the whole tree.
    ///
    /// Nothing in this file converts anything. It is the reviewable statement of intent that
    /// <see cref="MaterialPortOperator"/> executes, and `RCMPORT` checks the two against each other.
    /// </summary>
    public static class MaterialPortRecipes
    {
        /// <summary> Every material in the tool, the same denominator RM-67 records. Recipe counts sum to this. </summary>
        public const int RecipeTotal = 471;

        /// <summary> Materials at `_Mode: 0`, measured off the Built-in assets. This is a count of assets that HAVE a
        /// `_Mode` -- `RoadDetailOverlay1` was already on URP Lit and carries none. </summary>
        public const int ModeOpaqueCount = 397;

        /// <summary> Materials the recipes make opaque: the 397 measured at `_Mode: 0` plus `RoadDetailOverlay1`, which
        /// is opaque by URP's own `_Surface: 0` / `_AlphaClip: 0` and has no `_Mode` to be measured at. 398 + 37 + 36
        /// is the 471 the port covers. </summary>
        public const int OpaqueCount = ModeOpaqueCount + AlreadyPorted;
        /// <summary> Materials `_Mode: 1`, measured. Every one of them is under `Materials/Markers/`. </summary>
        public const int CutoutCount = 37;
        /// <summary> Materials `_Mode: 2`, measured. 35 markings plus `RumbleStrip`. </summary>
        public const int FadeCount = 36;

        /// <summary> Already on URP Lit before the operator runs: `Markers/RoadDetailOverlay1.mat`. </summary>
        public const int AlreadyPorted = 1;

        /// <summary> Materials RM-67 AUTHORED to repair the three string paths that resolved to nothing:
        /// `RoadIntersection`, `InterText` and `InterMainPlate1`. They were born on URP Lit, so they are not part of
        /// the 471 the port converts -- but they are part of the tree the operator walks. </summary>
        public const int AuthoredByRepair = 3;

        /// <summary> Every material asset under `Materials/` today. The operator checks the folder against this, so a
        /// material that disappears is a failure rather than a smaller report. </summary>
        public const int TreeTotal = RecipeTotal + AuthoredByRepair;

        /// <summary> URP's cutout render queue, matching Built-in's own 2450 so sorting does not change. </summary>
        public const int CutoutQueue = 2450;
        /// <summary> URP's transparent render queue, matching Built-in's own 3000. </summary>
        public const int FadeQueue = 3000;

        private const string DielectricNote =
            "AUTHORED: metallic left at whatever the asset already carried, because Standard's own `_Metallic` is the "
            + "measured value and URP reads the same 0..1 dielectric scale";

        private static List<MaterialRecipe> _all;
        private static List<PortMapping> _mappings;

        public static IList<MaterialRecipe> All()
        {
            if (_all == null) { _all = Build(); }
            return new List<MaterialRecipe>(_all);
        }

        public static IList<PortMapping> Mappings()
        {
            if (_mappings == null) { _mappings = BuildMappings(); }
            return new List<PortMapping>(_mappings);
        }

        public static string SurfaceText(PortSurface surface)
        {
            if (surface == PortSurface.Cutout) { return "cutout"; }
            if (surface == PortSurface.Fade) { return "fade"; }
            return "opaque";
        }

        public static string WorkflowText(PortWorkflow workflow)
        {
            return workflow == PortWorkflow.Specular ? "specular" : "metallic";
        }

        /// <summary> The sum the register must equal. `RCMPORT` fails if it drifts from <see cref="RecipeTotal"/>. </summary>
        public static int TotalCovered()
        {
            int total = 0;
            foreach (MaterialRecipe r in All()) { total += r.count; }
            return total;
        }

        public static int CountWith(PortSurface surface)
        {
            int total = 0;
            foreach (MaterialRecipe r in All()) { if (r.surface == surface) { total += r.count; } }
            return total;
        }

        public static int CountWith(PortWorkflow workflow)
        {
            int total = 0;
            foreach (MaterialRecipe r in All()) { if (r.workflow == workflow) { total += r.count; } }
            return total;
        }

        private static void AddRoadSurfaces(List<MaterialRecipe> list)
        {
            list.Add(new MaterialRecipe("R1", "road and shoulder surfaces", "`Road1`", 1,
                PortSurface.Opaque, PortWorkflow.Metallic, "_EMISSION _NORMALMAP", "_EMISSION _NORMALMAP",
                "the default asphalt every new road gets, and the material the three authored intersection surfaces "
                + "were matched to. `_EMISSION` is carried forward rather than dropped because dropping a keyword is a "
                + "visible change even when `_EmissionColor` is black, and URP reads the same keyword",
                "measured: `_BaseColor` 0.7238806 grey, `_Glossiness` and albedo tiling read out of "
                + "`Materials/Road1.mat`; " + DielectricNote));
            list.Add(new MaterialRecipe("R2", "road and shoulder surfaces", "`Shoulder1`", 1,
                PortSurface.Opaque, PortWorkflow.Metallic, "_NORMALMAP", "_NORMALMAP",
                "the default shoulder, the only other root material on Standard metallic. It meets `Road1` along every "
                + "road edge, so the two must agree on smoothness or the seam reads as a wet strip",
                "measured out of `Materials/Shoulder1.mat`; " + DielectricNote));
            list.Add(new MaterialRecipe("R3", "road and shoulder surfaces",
                "`BrickRoad`, `CobblestoneRoad`, `DirtRoad`", 3,
                PortSurface.Opaque, PortWorkflow.Specular, "_NORMALMAP", "_SPECULAR_SETUP _NORMALMAP",
                "the three road types `Scripts/Road.cs:1601` to `:1611` load by road type. Specular setup, so "
                + "`_SpecColor` carries the reflectance and `_Metallic` is not the control -- reading it as metallic is "
                + "the mistake that makes cobble look like sheet metal",
                "measured per asset: `_SpecColor` and `_Glossiness` copied unchanged, `_WorkflowMode` set to 0"));
            list.Add(new MaterialRecipe("R4", "road and shoulder surfaces", "`Shoulder2`, `Shoulder3`", 2,
                PortSurface.Opaque, PortWorkflow.Specular, "", "_SPECULAR_SETUP _NORMALMAP",
                "the two shoulders that reached the port BROKEN, which is why they are the one recipe that changes what "
                + "it found rather than preserving it. Both had an empty `_BaseMap` and `_MainTex`, and their `_BumpMap` "
                + "and `_ParallaxMap` held GUIDs that resolve to nothing -- so Built-in rendered them as flat 0.85 grey "
                + "with a black `_SpecColor`, a dielectric with zero reflectance, which no real surface is. Preserving "
                + "that is preserving a defect, so each was given the surfacing it is for: `Shoulder2` the portland "
                + "cement concrete shoulder, `Shoulder3` the compacted aggregate one",
                // AUTHORED in capitals, and it is the one row in the register where the shout is load-bearing. Every
                // other recipe's numbers can be checked by opening the asset; this one's cannot, because what the asset
                // carried was a black `_SpecColor` and four map slots pointing at nothing. Written in lower case it read
                // as prose and scanned past, which is precisely the value a reviewer must not scan past.
                "AUTHORED in full -- there was nothing measurable here to preserve. The values come from the two "
                + "shoulder surfacings AASHTO treats as standard beside the paved one `Shoulder1` "
                + "already carries. `Shoulder2` takes `Concrete1Diffuse`/`Concrete1Normal` at 0.25 smoothness, the "
                + "broom-and-tine finish of a PCC shoulder; `Shoulder3` takes `DirtRoadDiffuse`/`DirtRoadNormal` at 0.12, "
                + "loose aggregate scattering at every angle. Both keep `_Metallic 0` and take `_SpecColor` 0.04, the "
                + "dielectric F0 concrete and stone share; `_BaseColor` moves to the 0.7238806 grey `Road1` and "
                + "`Shoulder1` already use, because a shoulder meets the travelled way along its whole length and a "
                + "different tint reads as a seam. Normals tile at 2x albedo, the ratio `Shoulder1` uses. The dead "
                + "`_ParallaxMap` is cleared rather than repointed: the tool ships no height map and `_PARALLAXMAP` was "
                + "never enabled"));
            list.Add(new MaterialRecipe("R5", "road and shoulder surfaces", "`RumbleStrip`", 1,
                PortSurface.Fade, PortWorkflow.Specular, "_ALPHABLEND_ON _NORMALMAP",
                "_SURFACE_TYPE_TRANSPARENT _NORMALMAP",
                "the only transparent material outside `Markers/`, and the reason the fade recipe cannot be described "
                + "as a markings-only case. `_ALPHABLEND_ON` has no URP keyword: the state moves into `_Surface 1` plus "
                + "`_Blend 0` and the blend factors",
                "measured: `_Mode: 2`, queue 3000, `_SrcBlend 5`, `_DstBlend 10`, `_ZWrite 0`"));
        }

        private static void AddConcreteAndMetals(List<MaterialRecipe> list)
        {
            list.Add(new MaterialRecipe("R6", "concrete", "`Concrete2Diffuse`", 1,
                PortSurface.Opaque, PortWorkflow.Metallic, "_EMISSION", "_EMISSION",
                "the diffuse-only concrete, and the third and last root material on Standard metallic. No normal map "
                + "keyword and none assigned, so it is the one concrete that stays flat under grazing light",
                "measured: on `fileID: 46`, confirmed by name against the three root metallic assets"));
            list.Add(new MaterialRecipe("R7", "concrete", "`Concrete2`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "_EMISSION _NORMALMAP",
                "_SPECULAR_SETUP _EMISSION _NORMALMAP",
                "the most-referenced material in the tool: 100 `.rao` entries name it by path and 101 FBX files name it "
                + "by name (RM-48). It is the one conversion where a wrong value shows up in the most places at once",
                "measured out of `Materials/Concrete2.mat`, every value copied rather than restyled"));
            list.Add(new MaterialRecipe("R8", "concrete", "`Concrete1`, `Concrete3` to `Concrete11`", 10,
                PortSurface.Opaque, PortWorkflow.Specular, "_NORMALMAP", "_SPECULAR_SETUP _NORMALMAP",
                "the concrete library an author picks kerb and barrier faces from. Ten assets in one identical state, "
                + "so one recipe applied ten times -- not ten judgements",
                "measured: all ten on `fileID: 45` with the same keyword line; per-asset colours and maps preserved"));

            list.Add(new MaterialRecipe("R9", "barriers, rails and metals", "`Railing4`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "_EMISSION _NORMALMAP",
                "_SPECULAR_SETUP _EMISSION _NORMALMAP",
                "the one metal carrying `_EMISSION`, named in 16 `.rao` entries. Its emission keyword is preserved for "
                + "the same reason as `Road1`'s",
                "measured out of `Materials/Railing4.mat`"));
            list.Add(new MaterialRecipe("R10", "barriers, rails and metals",
                "`Steel1` to `Steel7`, `Galv1` to `Galv5`, `Metal1`, `Metal2`, `Railing7`, `Railing8`, `WBeam`, "
                + "`WBeamMetal`, `CableBarrier`", 19,
                PortSurface.Opaque, PortWorkflow.Specular, "_NORMALMAP", "_SPECULAR_SETUP _NORMALMAP",
                "the guardrail and cable-barrier metals. Specular setup is doing real work here: a galvanised W-beam is "
                + "a bright wide-lobe reflector, and re-expressing it as metallic 1 loses the wide lobe and reads as "
                + "chrome",
                "measured: all 19 on `fileID: 45`; `_SpecColor` and `_Glossiness` copied per asset, `_Metallic` "
                + "untouched because specular setup ignores it"));

            list.Add(new MaterialRecipe("R11", "tunnel", "`Tunnel`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "_NORMALMAP", "_SPECULAR_SETUP _NORMALMAP",
                "the tunnel bore lining, named in 3 `.rao` entries",
                "measured out of `Materials/Tunnel.mat`"));
            list.Add(new MaterialRecipe("R12", "tunnel", "`TunnelLights`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "", "_SPECULAR_SETUP",
                "the emissive-looking luminaire surface -- and its keyword line is BLANK, so its glow was never "
                + "`_EMISSION` at all: it was the `Flares/SodiumBulb.flare` Built-in flare, which RM-68 replaced with "
                + "the `LensFlareDataSRP` at `Flares/SodiumBulb.asset`. Turning "
                + "`_EMISSION` on here would hide a flare regression behind a material change",
                "measured: keyword line blank, on `fileID: 45`"));
            list.Add(new MaterialRecipe("R13", "attenuator and barrel", "`Atten`, `RoadBarrel`", 2,
                PortSurface.Opaque, PortWorkflow.Specular, "_NORMALMAP", "_SPECULAR_SETUP _NORMALMAP",
                "the work-zone pair, in one identical state",
                "measured: both on `fileID: 45` with `_NORMALMAP`"));
        }

        private static void AddMarkings(List<MaterialRecipe> list)
        {
            list.Add(new MaterialRecipe("R14", "lane and intersection markings", "37 cutout markings under "
                + "`Materials/Markers/`, `InterLaneDisabled` and its `Outer`, `-4L` and `-6L` forms among them", 37,
                PortSurface.Cutout, PortWorkflow.Specular, "_ALPHATEST_ON", "_SPECULAR_SETUP _ALPHATEST_ON",
                "painted markings with hard edges. URP needs the keyword AND the surface state together: `_AlphaClip 1`, "
                + "`_Surface 0`, `RenderType: TransparentCutout` and queue 2450, plus `_AlphaToMask 1` so the cut edge "
                + "survives MSAA. `_ALPHATEST_ON` alone renders them opaque, which is the gotcha RM-67 recorded",
                "measured: all 37 at `_Mode: 1`, queue 2450, `_Cutoff: 0.5`, `_SrcBlend 1`, `_DstBlend 0`, `_ZWrite 1`; "
                + "`_Cutoff` copied per asset rather than assumed to be 0.5 everywhere"));
            list.Add(new MaterialRecipe("R15", "lane and intersection markings", "35 blended markings, the "
                + "`InterCenter-Both-*` set among them", 35,
                PortSurface.Fade, PortWorkflow.Specular, "_ALPHABLEND_ON", "_SPECULAR_SETUP _SURFACE_TYPE_TRANSPARENT",
                "worn and faded markings that read through to the asphalt. This is the recipe with no keyword "
                + "counterpart at all, so the whole state is carried by floats: `_Surface 1`, `_Blend 0`, `_SrcBlend 5`, "
                + "`_DstBlend 10`, `_ZWrite 0`, queue 3000, `RenderType: Transparent`. "
                + "`_BlendModePreserveSpecular` is set to 0, not URP's own default of 1, because Built-in Fade fades "
                + "specular along with albedo and preserving it makes a worn line glint where it used to dim",
                "measured: all 35 at `_Mode: 2`, queue 3000, `_SrcBlend 5`, `_DstBlend 10`, `_ZWrite 0`"));
            list.Add(new MaterialRecipe("R16", "lane and intersection markings", "`InterLaneDisabledR`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "", "_SPECULAR_SETUP",
                "the one opaque material in a folder of 74 transparent ones, and the reason this family needs three "
                + "surface recipes rather than one. Converting it as cutout would put it in queue 2450 and change how "
                + "it sorts against the markings it sits beside",
                "measured: `_Mode: 0`, queue `-1`, keyword line blank"));
            list.Add(new MaterialRecipe("R17", "lane and intersection markings", "`RoadDetailOverlay1`", 1,
                PortSurface.Opaque, PortWorkflow.Specular, "", "",
                "ALREADY PORTED. RM-67 converted it in place while fixing the `ESOSidewalk.rao` path that reaches it, "
                + "so the operator must find it already on URP Lit and skip it rather than convert it twice. It keeps "
                + "its row so the register still sums to the whole tree",
                "measured: now `m_Shader` guid `933532a4fcc9baf4fa0491de14d08ed7`, `_WorkflowMode: 0`, keyword line "
                + "blank; its `.meta` was never touched so every reference to it survived"));
        }

        private static void AddSigns(List<MaterialRecipe> list)
        {
            list.Add(new MaterialRecipe("R18", "signals and street furniture", "9 of the 17 `Materials/Signs/` "
                + "materials, `RoadReflector` among them", 9,
                PortSurface.Opaque, PortWorkflow.Metallic, "_NORMALMAP", "_NORMALMAP",
                "the plain normal-mapped street furniture. Metallic workflow, so `_Metallic` and `_MetallicGlossMap` "
                + "are the controls and `_SpecColor` is inert",
                "measured: all 9 on `fileID: 46` with `_NORMALMAP`"));
            list.Add(new MaterialRecipe("R19", "signals and street furniture",
                "`GreenBlinder`, `RoadConBarrel`, `TrafficCone`", 3,
                PortSurface.Opaque, PortWorkflow.Metallic, "_NORMALMAP _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                "_NORMALMAP _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A",
                "smoothness read from the ALBEDO alpha channel, not the metallic map. URP keeps the same keyword but "
                + "also reads `_SmoothnessTextureChannel`, which must be set to 1 in the same pass -- the keyword alone "
                + "leaves smoothness coming from the wrong channel and a traffic cone turns glossy",
                "measured: all 3 on `fileID: 46` with that exact keyword pair"));
            list.Add(new MaterialRecipe("R20", "signals and street furniture", "`InterTLB2`, `TrafficLightMainB`", 2,
                PortSurface.Opaque, PortWorkflow.Metallic, "_DETAIL_MULX2 _NORMALMAP",
                "_DETAIL_MULX2 _NORMALMAP",
                "the two signal bodies using a detail albedo overlay in multiply-x2 mode. URP Lit supports the same "
                + "keyword, and `_DetailAlbedoMap`, `_DetailMask` and `_DetailAlbedoMapScale` all carry across by name",
                "measured: both on `fileID: 46` with that exact keyword pair"));
            list.Add(new MaterialRecipe("R21", "signals and street furniture",
                "`TrafficLightMain`, `TrafficLightMainLY`", 2,
                PortSurface.Opaque, PortWorkflow.Metallic, "_EMISSION _NORMALMAP", "_EMISSION _NORMALMAP",
                "the two lit signal heads, the only emissive materials in the folder. `Scripts/IntersectionObjects.cs:"
                + "532` composes `Materials/Signs/InterTLB` plus a number plus `.mat`, so a rename here breaks a BUILT "
                + "path with no compile error -- the port renames nothing",
                "measured: both on `fileID: 46` with `_EMISSION _NORMALMAP`"));
            list.Add(new MaterialRecipe("R22", "signals and street furniture", "`SignYieldOnGreen`", 1,
                PortSurface.Opaque, PortWorkflow.Metallic, "", "",
                "the one sign face in the folder with a blank keyword line",
                "measured: on `fileID: 46`, keyword line blank"));
        }

        private static void AddFederalSignFaces(List<MaterialRecipe> list)
        {
            list.Add(new MaterialRecipe("R23", "federal sign faces", "335 of the 336 `Materials/FedSigns/` "
                + "materials, across the `330`, `396`, `617`, `861`, `988`, `Diamond` and `Square` sets", 335,
                PortSurface.Opaque, PortWorkflow.Metallic, "_NORMALMAP", "_NORMALMAP",
                "71 percent of every material in the tool, and the cheapest recipe in the register: one identical state "
                + "repeated 335 times. Opaque, metallic, normal-mapped -- a sign face is a retroreflective sheet on a "
                + "flat plate, so there is no cutout and nothing transparent to preserve",
                "measured: all 335 on `fileID: 46` with `_NORMALMAP`, `_Mode: 0`, queue `-1`"));
            list.Add(new MaterialRecipe("R24", "federal sign faces", "`FedSigns/SignDiamond`", 1,
                PortSurface.Opaque, PortWorkflow.Metallic, "", "",
                "the single outlier in 336, keyword line blank. It is the parent-folder asset the `Diamond` sets sit "
                + "under, and it is called out here so a reader does not read `all 336 identical` and stop checking",
                "measured: on `fileID: 46`, keyword line blank; the only `FedSigns` material not carrying `_NORMALMAP`"));
        }

        private static List<MaterialRecipe> Build()
        {
            List<MaterialRecipe> list = new List<MaterialRecipe>();
            AddRoadSurfaces(list);
            AddConcreteAndMetals(list);
            AddMarkings(list);
            AddSigns(list);
            AddFederalSignFaces(list);
            return list;
        }

        private static List<PortMapping> BuildMappings()
        {
            List<PortMapping> list = new List<PortMapping>();
            list.Add(new PortMapping("`m_Shader: {fileID: 46 or 45, guid: " + PipelineAudit.BuiltInResourceShaderGuid
                + "}`", "`{fileID: 4800000, guid: " + PipelineAudit.UrpLitShaderGuid + "}`",
                "the swap itself, done by assigning the Shader OBJECT that `Shader.Find(\""
                + PipelineAudit.UrpLitShaderName + "\")` returns. The guid is never written as text, and the "
                + "material's own `.meta` is never touched, so every reference by guid survives"));
            list.Add(new PortMapping("`fileID: 46` (Standard)", "`_WorkflowMode: 1`, no `_SPECULAR_SETUP`",
                "metallic workflow. `_Metallic` and `_MetallicGlossMap` are the controls"));
            list.Add(new PortMapping("`fileID: 45` (Standard, specular setup)",
                "`_WorkflowMode: 0` plus the `_SPECULAR_SETUP` keyword",
                "115 of the 471 materials. Miss the keyword and URP reads `_Metallic` instead of `_SpecColor`, which "
                + "turns every guardrail and every marking dark"));
            list.Add(new PortMapping("`_Color`", "`_BaseColor`",
                "URP's own name. `_Color` is kept as well, because URP Lit still declares it as an obsolete alias and "
                + "dropping it loses the value on any tool that still reads it"));
            list.Add(new PortMapping("`_MainTex`", "`_BaseMap`",
                "same texture in both slots for the same reason as `_Color`"));
            list.Add(new PortMapping("`_Glossiness`", "`_Smoothness`",
                "renamed, same 0..1 meaning. `_Glossiness` is kept as the alias"));
            list.Add(new PortMapping("`_GlossyReflections`", "`_EnvironmentReflections`",
                "inverted NAME, not inverted meaning: 1 still means reflections on"));
            list.Add(new PortMapping("`_Mode: 0` (Opaque)",
                "`_Surface: 0`, `_Blend: 0`, `_AlphaClip: 0`, `_SrcBlend: 1`, `_DstBlend: 0`, `_ZWrite: 1`, "
                + "`m_CustomRenderQueue: -1`, no `RenderType` tag",
                "397 of the 471. The Built-in assets carry no `RenderType` tag at all in this state, so none is added"));
            list.Add(new PortMapping("`_Mode: 1` (Cutout) with `_ALPHATEST_ON`",
                "`_ALPHATEST_ON` kept, plus `_AlphaClip: 1`, `_Surface: 0`, `_AlphaToMask: 1`, "
                + "`RenderType: TransparentCutout`, queue " + CutoutQueue.ToString(CultureInfo.InvariantCulture),
                "the keyword alone is not enough in URP -- this is the rule that decides whether 37 markings keep their "
                + "cut edges"));
            list.Add(new PortMapping("`_Mode: 2` (Fade) with `_ALPHABLEND_ON`",
                "NO keyword counterpart. `_SURFACE_TYPE_TRANSPARENT`, `_Surface: 1`, `_Blend: 0`, `_SrcBlend: 5`, "
                + "`_DstBlend: 10`, `_SrcBlendAlpha: 1`, `_DstBlendAlpha: 10`, `_ZWrite: 0`, "
                + "`_BlendModePreserveSpecular: 0`, `RenderType: Transparent`, queue "
                + FadeQueue.ToString(CultureInfo.InvariantCulture),
                "36 materials. `_BlendModePreserveSpecular` is deliberately 0 rather than URP's default 1, because "
                + "Built-in Fade fades specular with albedo"));
            list.Add(new PortMapping("`_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`",
                "same keyword plus `_SmoothnessTextureChannel: 1`",
                "URP reads the float as well as the keyword; setting one without the other is silent"));
            list.Add(new PortMapping("`_METALLICGLOSSMAP` or `_SPECGLOSSMAP`", "`_METALLICSPECGLOSSMAP`",
                "URP declares one keyword for both maps and reads which it is from `_WorkflowMode`. Two Built-in names "
                + "collapse to one, and keeping either original name leaves the map unsampled. No material in this tool "
                + "carries either keyword today, and the rule is implemented because a re-authored material can"));
            list.Add(new PortMapping("`_DETAIL_MULX2`, `_EMISSION`, `_NORMALMAP`, `_PARALLAXMAP`",
                "the same keyword names, unchanged",
                "carried across verbatim. A blank Built-in keyword line stays blank, even where the texture slot it "
                + "would enable is filled -- enabling it is an improvement, and an improvement is a change"));
            list.Add(new PortMapping("nothing in Built-in",
                "`_ReceiveShadows: 1`, `_QueueOffset: 0`, `_ClearCoatMask: 0`, `_ClearCoatSmoothness: 0`, "
                + "`_AddPrecomputedVelocity: 0`, `_XRMotionVectorsPass: 1`, `_Cull: 2`",
                "URP-only properties with no Built-in source, written at URP's own defaults so the material inspector "
                + "opens in a valid state"));
            return list;
        }

        /// <summary> A blank keyword line printed so a reader can tell it apart from a missing cell. </summary>
        public static string KeywordText(string keywords)
        {
            return string.IsNullOrEmpty(keywords) ? "(blank)" : "`" + keywords + "`";
        }

        public static string Table()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Recipe | Family | Members | Count | Surface | Workflow | Built-in keywords | "
                + "URP keywords | Intent | Values from |\n");
            builder.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");
            IList<MaterialRecipe> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                MaterialRecipe r = all[i];
                builder.Append("| ").Append(r.id)
                       .Append(" | ").Append(r.family)
                       .Append(" | ").Append(r.members)
                       .Append(" | ").Append(r.CountText)
                       .Append(" | ").Append(SurfaceText(r.surface))
                       .Append(" | ").Append(WorkflowText(r.workflow))
                       .Append(" | ").Append(KeywordText(r.builtInKeywords))
                       .Append(" | ").Append(KeywordText(r.urpKeywords))
                       .Append(" | ").Append(r.intent)
                       .Append(" | ").Append(r.provenance).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }

        public static string MappingTable()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Built-in | URP Lit | Why it matters |\n");
            builder.Append("| --- | --- | --- |\n");
            IList<PortMapping> all = Mappings();
            for (int i = 0; i < all.Count; i++)
            {
                builder.Append("| ").Append(all[i].builtIn).Append(" | ").Append(all[i].urp)
                       .Append(" | ").Append(all[i].note).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }
    }
}
#endif
