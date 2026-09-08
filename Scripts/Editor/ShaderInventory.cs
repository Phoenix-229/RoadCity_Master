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
    /// <summary> RM-67: which pipeline every render asset in the tool targets, and the evidence for saying so.
    ///
    /// There is deliberately no <c>Unknown</c> member on <see cref="PipelineTarget"/>. RM-67's gate requires that
    /// nothing is marked unknown at sign-off, and a gate that checks for a token the type cannot hold is a gate that
    /// can never fail: the guarantee belongs in the type instead.
    /// </summary>
    public enum PipelineTarget
    {
        /// <summary> Renders only under the Built-in render pipeline. Under URP it renders magenta or unlit. </summary>
        BuiltIn = 0,
        /// <summary> Targets the Universal render pipeline. </summary>
        Urp = 1,
        /// <summary> Carries no pipeline-specific declaration; correct under either. </summary>
        PipelineAgnostic = 2,
    }


    public enum RenderAssetKind
    {
        Shader = 0,
        MaterialFamily = 1,
        LensFlare = 2,
        /// <summary> An HLSL file the shaders `#include`. It is not an asset a material can reference, but it is code
        /// the port owns, so leaving it out of the inventory would understate what a URP upgrade has to survive.
        /// </summary>
        HlslInclude = 3,
    }


    /// <summary> One row of the inventory: an asset, or a family of materials counted exactly. </summary>
    public sealed class RenderAssetEntry
    {
        public readonly string asset;
        public readonly RenderAssetKind kind;
        /// <summary> How many assets this row accounts for. 1 for a single shader or flare; the counted membership
        /// for a material family. The families partition the tree, so these sum to the material total. </summary>
        public readonly int count;
        public readonly PipelineTarget target;
        /// <summary> Why that target, read out of the asset itself rather than inferred from its folder. </summary>
        public readonly string targetEvidence;
        /// <summary> What reaches it, with the file and line that does the reaching. </summary>
        public readonly string usedBy;
        /// <summary> The task that owns the port, or the reason none is owed. </summary>
        public readonly string portOwner;

        public RenderAssetEntry(string asset, RenderAssetKind kind, int count, PipelineTarget target,
            string targetEvidence, string usedBy, string portOwner)
        {
            this.asset = asset;
            this.kind = kind;
            this.count = count;
            this.target = target;
            this.targetEvidence = targetEvidence;
            this.usedBy = usedBy;
            this.portOwner = portOwner;
        }

        public string CountText { get { return count.ToString(CultureInfo.InvariantCulture); } }
    }


    /// <summary> The inventory itself. Ten shaders, three HLSL includes, eight material families that partition all
    /// 471 materials, and both halves of the lens flare -- every row marked Built-in, URP or pipeline-agnostic with the
    /// declaration that proves it.
    ///
    /// The finding the counts carry, as RM-67 found the tree: every material in it referenced one of exactly TWO
    /// Built-in engine shaders, <c>fileID 46</c> (Standard) or <c>fileID 45</c> (Standard, specular setup), both inside
    /// Unity's built-in resources GUID <c>0000000000000000f000000000000000</c>. Not one material referenced any of the
    /// tool's own ten shaders, and no code calls <c>Shader.Find</c> on their names, so the custom shaders are
    /// unreachable as shipped -- which is why the port converted the MATERIALS and did not try to route them onto the
    /// custom shaders instead.
    ///
    /// The <c>PipelineTarget</c> on each row is the LIVE state after RM-68: the ten shaders declare
    /// <c>UniversalPipeline</c>, the 471 materials sit on URP's Lit, and the flare has a <c>LensFlareDataSRP</c>
    /// successor. The Built-in declaration each row USED to carry is kept inside its evidence prose, because that is
    /// the record of what the conversion had to reproduce and deleting it would leave the port unaudited.
    ///
    /// The integer counts stay frozen at RM-67's snapshot of 2026-08-31 -- <see cref="OnStandard"/> and
    /// <see cref="OnStandardSpecular"/> are the pre-port census, deliberately constants rather than a live disk count,
    /// because a denominator that moves while the port runs cannot be used to tell how much of the port is done.
    /// <c>MaterialPortGate</c> measures the converted tree against exactly those two numbers.
    /// </summary>
    public static class ShaderInventory
    {
        /// <summary> Every material under Materials/, counted from disk. The family rows must sum to this. </summary>
        public const int MaterialTotal = 471;
        /// <summary> Materials that were on Built-in Standard, fileID 46, before RM-68 rewrote them. Kept as the
        /// pre-port census `MaterialPortGate` measures the conversion against. </summary>
        public const int OnStandard = 356;
        /// <summary> Materials that were on Built-in Standard specular setup, fileID 45, before RM-68. </summary>
        public const int OnStandardSpecular = 115;
        /// <summary> Shader graphs in the tool. Counted, and zero: there is no graph to port. </summary>
        public const int ShaderGraphCount = 0;
        /// <summary> Standalone .hlsl or .cginc includes in the tool. Zero before RM-68; three after it, because the
        /// port pushed every line of shading into `Shaders/Include/` so the ten shaders cannot drift apart. </summary>
        public const int HlslIncludeCount = 3;

        private const string PortedEvidence =
            "URP shader: `Tags { \"RenderPipeline\" = \"UniversalPipeline\" }`, a `UniversalForward` pass shading "
            + "through `Shaders/Include/RoadSurfaceForwardPass.hlsl`, and `ShadowCaster`, `DepthOnly`, `DepthNormals` "
            + "and `Meta` passes taken from the URP package rather than hand-copied";

        private const string UnreachableUse =
            "nothing -- its GUID has zero hits in any .mat, .prefab, .unity or .asset under Assets/, and no `Shader."
            + "Find` call exists anywhere in the tool";

        private const string PortedByRm68 =
            "RM-68 -- ported in place, so the file keeps its own GUID and `.meta`; the Built-in CGPROGRAM and its "
            + "`#pragma surface surf BlinnPhong` are gone from the file entirely";

        /// <summary> Every material family now points at the same shader, so the sentence that says so is written
        /// once. The Built-in `fileID` each family used to carry is kept in its own row, because that is the audit
        /// trail for what the conversion had to reproduce. </summary>
        private const string PortedMaterialEvidence =
            "on `Universal Render Pipeline/Lit` (`guid: 933532a4fcc9baf4fa0491de14d08ed7`) after "
            + "`Scripts/Editor/MaterialPortOperator.cs` rewrote each `.mat` in place, keeping its own GUID and "
            + "`.meta` so every path- and name-based reference below still resolves. Before the port: ";

        private const string PortedMaterialOwner =
            "RM-68 -- converted, not re-authored: albedo, normal, occlusion, emission and the specular-setup metals' "
            + "gloss were carried into `_BaseMap`, `_BumpMap`, `_OcclusionMap`, `_EmissionMap` and `_Smoothness`, and "
            + "`_ALPHATEST_ON` / `_ALPHABLEND_ON` became `_Surface` plus `_Blend` plus `_AlphaClip`";

        private static List<RenderAssetEntry> _all;

        public static IList<RenderAssetEntry> All()
        {
            if (_all == null) { _all = Build(); }
            return new List<RenderAssetEntry>(_all);
        }

        public static string TargetText(PipelineTarget target)
        {
            if (target == PipelineTarget.Urp) { return "URP"; }
            if (target == PipelineTarget.PipelineAgnostic) { return "pipeline-agnostic"; }
            return "Built-in";
        }

        public static string KindText(RenderAssetKind kind)
        {
            if (kind == RenderAssetKind.Shader) { return "shader"; }
            if (kind == RenderAssetKind.LensFlare) { return "lens flare"; }
            if (kind == RenderAssetKind.HlslInclude) { return "HLSL include"; }
            return "material family";
        }

        public static int CountWith(PipelineTarget target)
        {
            int total = 0;
            foreach (RenderAssetEntry e in All()) { if (e.target == target) { total++; } }
            return total;
        }

        /// <summary> Assets accounted for, summing the family counts. </summary>
        public static int MaterialsAccountedFor()
        {
            int total = 0;
            foreach (RenderAssetEntry e in All())
            {
                if (e.kind == RenderAssetKind.MaterialFamily) { total += e.count; }
            }
            return total;
        }

        private static void AddShaders(List<RenderAssetEntry> list)
        {
            AddPortedShader(list, "Shaders/RoadShader.shader", "RoadCityMaster/Roads/Road Shader", 78,
                "keeps `Offset -1, -1` in its forward, depth and depth-normals passes and takes smoothness from the "
                + "base map's alpha through `_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A`");
            AddPortedShader(list, "Shaders/RoadShaderDX11.shader", "RoadCityMaster/DX11/Road Shader", 74,
                "carries no polygon offset, which is what makes it the surface for a road nothing is laid over");
            AddPortedShader(list, "Shaders/RoadShaderLow.shader", "RoadCityMaster/Roads/Road Shader Low Quality", 59,
                "keeps `Offset 1, 1` -- positive, pushing away from the camera, so a full-quality road wins where the "
                + "two overlap -- and `Queue Geometry+10`");
            AddPortedShader(list, "Shaders/RoadShaderLowDX11.shader", "RoadCityMaster/DX11/Road Shader Low Quality", 51,
                "keeps `Queue Geometry+10` and carries no polygon offset");
            AddPortedShader(list, "Shaders/ShoulderDirt.shader", "RoadCityMaster/Roads/Road Shoulder Shader", 68,
                "keeps `Offset -1, -1` and defaults smoothness to 0.15 for dry unsealed ground");
            AddPortedShader(list, "Shaders/ShoulderDirtDX11.shader", "RoadCityMaster/DX11/Road Shoulder Shader", 58,
                "defaults smoothness to 0.15 and carries no polygon offset");

            AddPortedShader(list, "Shaders/StandardOffset.shader", "RoadCityMaster/CutoutMarker", 84,
                "keeps `Offset -3, -2` and clips on alpha in every pass including `ShadowCaster` and `Meta`, with "
                + "`AlphaToMask On` for the edge the clip would otherwise leave stepped");
            AddPortedShader(list, "Shaders/StandardTiling.shader", "RoadCityMaster/Standard-Tiling", 74,
                "is the plain opaque tiling surface its name describes, with no offset and no alpha clip, because the "
                + "Built-in file was a byte-for-byte copy of StandardOffset.shader apart from its first line");

            AddPortedShader(list, "Shaders/TransparentWithShadows.shader", "RoadCityMaster/TranShadow", 76,
                "blends `SrcAlpha OneMinusSrcAlpha` with `ZWrite Off`, casts a clipped shadow, and no longer carries "
                + "the Built-in second pass that faked shadow reception by compositing black alpha");
            AddPortedShader(list, "Shaders/TransparentWithShadowsZ.shader", "RoadCityMaster/TranShadowMarker", 77,
                "blends transparently with `Offset -3, -2` and declares no `ShadowCaster`, because paint on a road "
                + "would cast its shadow onto the surface it is painted on");
        }

        /// <summary> One ported shader. The line number is where the shared variant set is pulled in, which is the line
        /// that proves the file shades through URP's forward path and not a Built-in lighting model. </summary>
        private static void AddPortedShader(List<RenderAssetEntry> list, string path, string declaredName,
            int pragmaLine, string kept)
        {
            list.Add(new RenderAssetEntry(path, RenderAssetKind.Shader, 1, PipelineTarget.Urp,
                PortedEvidence + ", pulled in at line " + pragmaLine.ToString(CultureInfo.InvariantCulture)
                + " by `#include_with_pragmas`; declares itself `Shader \"" + declaredName + "\"` and " + kept,
                UnreachableUse, PortedByRm68));
        }

        private static void AddMaterialFamilies(List<RenderAssetEntry> list)
        {
            list.Add(new RenderAssetEntry("Materials/ -- road and shoulder surfaces", RenderAssetKind.MaterialFamily, 8,
                PipelineTarget.Urp,
                PortedMaterialEvidence
                + "Road1 and Shoulder1 on `fileID: 46` (Standard); BrickRoad, CobblestoneRoad, DirtRoad, Shoulder2, "
                + "Shoulder3 and RumbleStrip on `fileID: 45` (Standard, specular setup). Road1 also serialised "
                + "`m_ShaderKeywords: _EMISSION _NORMALMAP`, both of which URP's Lit reads under the same names",
                "`Scripts/Road.cs:1506` (Road1) and `:1537` (Shoulder1) are the defaults a new road gets; `:1601`, "
                + "`:1606` and `:1611` load DirtRoad, BrickRoad and CobblestoneRoad by road type",
                PortedMaterialOwner + ". These eight are the surfaces an author sees first, so they were the port's "
                + "front line"));

            list.Add(new RenderAssetEntry("Materials/ -- concrete", RenderAssetKind.MaterialFamily, 12,
                PipelineTarget.Urp,
                PortedMaterialEvidence + "Concrete2Diffuse on `fileID: 46`; Concrete1 to Concrete11 on `fileID: 45`",
                "`Materials/Concrete2.mat` is named by PATH in 100 `.rao` library entries and at "
                + "`Scripts/Editor/SplineNEditor.cs:1635` and `:1741`; Concrete11 in 12 entries. The literal "
                + "`Concrete2` also appears inside 101 FBX files, so those slots resolve it BY NAME (RM-48)",
                PortedMaterialOwner + ". Concrete2 is the most-referenced material in the tool by a wide margin, which "
                + "is exactly why it was rewritten in place instead of replaced"));

            list.Add(new RenderAssetEntry("Materials/ -- barriers, rails and metals", RenderAssetKind.MaterialFamily,
                20, PipelineTarget.Urp,
                PortedMaterialEvidence
                + "Steel1 to Steel7, Galv1 to Galv5, Metal1, Metal2, Railing4, Railing7, Railing8, WBeam, WBeamMetal "
                + "and CableBarrier, every one on `fileID: 45` (Standard, specular setup), so each one's specular "
                + "colour and gloss had to become URP's metallic workflow rather than being copied across",
                "named by PATH in the `.rao` libraries -- Steel7 in 20 entries, Railing4 in 16, Railing7 in 8, "
                + "Metal2 in 6, WBeam in 5 -- and Steel7 also at `Scripts/Editor/SplineNEditor.cs:1932` and `:1936`. "
                + "`WBeam` appears inside 4 FBX files, so those resolve it by name",
                PortedMaterialOwner + ". A specular-setup metal is the case where a line-by-line port looks wrong "
                + "fastest, so the operator derives metallic and smoothness instead of transcribing them"));

            list.Add(new RenderAssetEntry("Materials/ -- tunnel", RenderAssetKind.MaterialFamily, 2,
                PipelineTarget.Urp,
                PortedMaterialEvidence + "Tunnel and TunnelLights, both `fileID: 45` (Standard, specular setup)",
                "named by PATH in 3 `.rao` entries each; TunnelLights is the emissive surface that pairs with the two "
                + "`Flares/SodiumBulb` rows below, and the surface and the flare were ported in the same pass",
                PortedMaterialOwner + ". TunnelLights keeps its emission, which is what makes the flare read as a lit "
                + "bulb rather than a sprite hanging in the air"));

            list.Add(new RenderAssetEntry("Materials/ -- attenuator and barrel", RenderAssetKind.MaterialFamily, 2,
                PipelineTarget.Urp,
                PortedMaterialEvidence + "Atten and RoadBarrel, both `fileID: 45` (Standard, specular setup)",
                "Atten is reached only through the `.rao` extruded-object libraries and author selection; RoadBarrel "
                + "pairs with `Materials/Signs/RoadConBarrel.mat` in the work-zone objects",
                PortedMaterialOwner + ". Two assets, one repeat of the same conversion the twenty metals took"));

            list.Add(new RenderAssetEntry("Materials/Markers/ -- lane and intersection markings",
                RenderAssetKind.MaterialFamily, 74, PipelineTarget.Urp,
                PortedMaterialEvidence
                + "all 74 on `fileID: 45` (Standard, specular setup), and the family that carries the tool's "
                + "transparency: 37 materials serialised `_ALPHATEST_ON`, 35 serialised `_ALPHABLEND_ON`, and 2 carried "
                + "neither keyword (InterLaneDisabledR and RoadDetailOverlay1), so the family needed three recipes "
                + "rather than one and the operator branches on which keyword each file actually had",
                "`Scripts/Road.cs:1279` to `:1281` load WhiteYellowDouble in its 2L, 4L and 6L forms and `:1514` to "
                + "`:1530` the TireMarks set; `Scripts/Intersections.cs` and `IntersectionObjects.cs` load the "
                + "`Inter*` markings. Only ONE of the 74 is referenced by GUID from any prefab or scene",
                PortedMaterialOwner + ". Markings are the most sensitive to a port, and `_ALPHABLEND_ON` has no URP "
                + "counterpart keyword at all: the 35 that had it now carry `_Surface: 1` with `_Blend: 0` and the "
                + "transparent queue the operator sets explicitly, because URP does not derive the queue on load"));

            list.Add(new RenderAssetEntry("Materials/Signs/ -- signals and street furniture",
                RenderAssetKind.MaterialFamily, 17, PipelineTarget.Urp,
                PortedMaterialEvidence
                + "all 17 on `fileID: 46` (Standard); 15 of the 17 ARE referenced by GUID from prefabs, the highest "
                + "proportion in the tree, so an in-place rewrite was the only conversion those prefabs survive",
                "`Scripts/IntersectionObjects.cs:532` composes `Materials/Signs/InterTLB` + number + `.mat`, so the "
                + "signal-head materials are reached by a BUILT path; RoadReflector is named in 14 `.rao` entries",
                PortedMaterialOwner + ". The composed path means a rename breaks it with no compile error, which is "
                + "the second reason nothing here was replaced with a new asset"));

            list.Add(new RenderAssetEntry("Materials/FedSigns/ -- federal sign faces",
                RenderAssetKind.MaterialFamily, 336, PipelineTarget.Urp,
                PortedMaterialEvidence
                + "all 336 on `fileID: 46` (Standard). This one family is 71 percent of every material in the tool",
                "NONE of the 336 is referenced by GUID from any prefab, scene or asset. Twelve are named by PATH in "
                + "the `EOMSign*.rao` edge-object libraries as each library entry's default face; the remaining 324 "
                + "are an author-facing library the author selects from on the placed sign",
                PortedMaterialOwner + ". The largest count and the cheapest conversion: one repeatable albedo recipe "
                + "applied 336 times by the operator, not 336 decisions"));
        }

        private static List<RenderAssetEntry> Build()
        {
            List<RenderAssetEntry> list = new List<RenderAssetEntry>();
            AddShaders(list);
            AddIncludes(list);
            AddMaterialFamilies(list);
            AddFlares(list);
            return list;
        }

        /// <summary> The three HLSL files the ten shaders share. They are the reason a URP upgrade has one place to
        /// break instead of ten. </summary>
        private static void AddIncludes(List<RenderAssetEntry> list)
        {
            list.Add(new RenderAssetEntry("Shaders/Include/RoadSurfaceInput.hlsl", RenderAssetKind.HlslInclude, 1,
                PipelineTarget.Urp,
                "declares the `UnityPerMaterial` constant buffer and builds URP's `SurfaceData`; includes URP's own "
                + "`ShaderLibrary/Core.hlsl` and `ShaderLibrary/SurfaceInput.hlsl`, and exposes "
                + "`InitializeStandardLitSurfaceData` under that exact name so URP's shipped `LitMetaPass.hlsl` links",
                "every pass of all ten: " + TenShaders,
                "RM-68 -- written by the port; there was no Built-in equivalent to convert"));
            list.Add(new RenderAssetEntry("Shaders/Include/RoadSurfaceForwardPass.hlsl", RenderAssetKind.HlslInclude, 1,
                PipelineTarget.Urp,
                "the shared `UniversalForward` pass: `UniversalFragmentPBR`, `MixFog`, `OutputAlpha`, `SAMPLE_GI` and "
                + "`SAMPLE_SHADOWMASK` from URP's `ShaderLibrary/Lighting.hlsl`. Its structs are named "
                + "`RoadAttributes` and `RoadVaryings` so they cannot collide with the shipped passes' own",
                "the `ForwardLit` pass of all ten: " + TenShaders,
                "RM-68 -- written by the port; it replaces ten copies of a Built-in surface function"));
            list.Add(new RenderAssetEntry("Shaders/Include/RoadSurfaceForwardPragmas.hlsl", RenderAssetKind.HlslInclude,
                1, PipelineTarget.Urp,
                "the shared `multi_compile` variant set, pulled in with `#include_with_pragmas`; carries "
                + "`_CLUSTER_LIGHT_LOOP`, which is the current name of what URP called `_FORWARD_PLUS`",
                "the `#include_with_pragmas` line of all ten: " + TenShaders,
                "RM-68 -- written by the port; a variant set the ten shaders must agree on has one owner"));
        }

        /// <summary> The ten shader files by name, so the three include rows above cite files a reader can open rather
        /// than saying "all ten shaders" and leaving them to be found. Named once instead of three times because the
        /// set is one fact: an eleventh shader is a change to this line, not to three cells that would then disagree.
        /// </summary>
        private const string TenShaders =
            "`Shaders/RoadShader.shader`, `Shaders/RoadShaderDX11.shader`, `Shaders/RoadShaderLow.shader`, "
            + "`Shaders/RoadShaderLowDX11.shader`, `Shaders/ShoulderDirt.shader`, `Shaders/ShoulderDirtDX11.shader`, "
            + "`Shaders/StandardOffset.shader`, `Shaders/StandardTiling.shader`, "
            + "`Shaders/TransparentWithShadows.shader`, `Shaders/TransparentWithShadowsZ.shader`";

        /// <summary> The lens flare, both halves of it: the Built-in asset the port could not convert in place, and the
        /// URP asset that replaced it. </summary>
        private static void AddFlares(List<RenderAssetEntry> list)
        {
            list.Add(new RenderAssetEntry("Flares/SodiumBulb.flare", RenderAssetKind.LensFlare, 1,
                PipelineTarget.BuiltIn,
                "the Built-in lens flare asset class, `--- !u!121` `Flare` with `m_FlareTexture`, `m_Elements` and "
                + "`m_UseFog`; URP draws nothing for `Light.flare`, and no in-place edit fixes that because the asset "
                + "CLASS is what URP does not read",
                "nothing any more -- `Scripts/IntersectionObjects.cs` no longer assigns `Light.flare`; the file is kept "
                + "as the source of record for every number `Scripts/SodiumFlare.cs` carried across",
                "RM-68 -- superseded rather than converted, which is why both rows exist"));
            list.Add(new RenderAssetEntry("Flares/SodiumBulb.asset", RenderAssetKind.LensFlare, 1,
                PipelineTarget.Urp,
                "a `LensFlareDataSRP` minted by Unity through `Scripts/Editor/FlarePortOperator.cs`, holding one "
                + "procedural `SRPLensFlareType.Circle` element in place of the Built-in file's image -- the amber "
                + "lived in the `.tga`'s pigment and a tint can only darken an image, so an LED luminaire's white "
                + "bloom has to be drawn rather than sampled -- additive and unmodulated by light colour as "
                + "`m_UseLightColor: 0` required",
                "`Scripts/SodiumFlare.cs` loads it and attaches a `LensFlareComponentSRP`, called from "
                + "`Scripts/IntersectionObjects.cs` for every point light on a street-lamp prefab",
                "RM-68 -- done here rather than deferred to RM-91, which now has nothing left to port"));
        }


        public static string Table()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("| Asset | Kind | Count | Pipeline | Evidence | Used by | Port |\n");
            builder.Append("| --- | --- | --- | --- | --- | --- | --- |\n");
            IList<RenderAssetEntry> all = All();
            for (int i = 0; i < all.Count; i++)
            {
                RenderAssetEntry e = all[i];
                builder.Append("| `").Append(e.asset).Append("` | ").Append(KindText(e.kind))
                       .Append(" | ").Append(e.CountText)
                       .Append(" | ").Append(TargetText(e.target))
                       .Append(" | ").Append(e.targetEvidence)
                       .Append(" | ").Append(e.usedBy)
                       .Append(" | ").Append(e.portOwner).Append(" |");
                if (i < all.Count - 1) { builder.Append("\n"); }
            }
            return builder.ToString();
        }
    }
}
#endif
