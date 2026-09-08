// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> The tree as it stands right now, counted off the material assets themselves. This is what a gate can
    /// assert against: the register says what the port should produce, and these are the numbers it did. </summary>
    public sealed class PortEndState
    {
        public readonly int total;
        public readonly int onUrpLit;
        public readonly int onBuiltIn;
        public readonly int onOther;
        public readonly int opaque;
        public readonly int cutout;
        public readonly int fade;
        public readonly int metallic;
        public readonly int specular;

        public PortEndState(int total, int onUrpLit, int onBuiltIn, int onOther, int opaque, int cutout, int fade,
            int metallic, int specular)
        {
            this.total = total;
            this.onUrpLit = onUrpLit;
            this.onBuiltIn = onBuiltIn;
            this.onOther = onOther;
            this.opaque = opaque;
            this.cutout = cutout;
            this.fade = fade;
            this.metallic = metallic;
            this.specular = specular;
        }
    }


    /// <summary> RM-68: converts every Built-in Standard material in the tool to URP Lit IN PLACE, by the rules
    /// <see cref="MaterialPortRecipes"/> states.
    ///
    /// In place matters more than it sounds. The conversion assigns the URP Lit Shader OBJECT to the existing
    /// <see cref="Material"/> asset and re-saves it, so the asset keeps its own GUID and its own `.meta`. Nothing here
    /// creates a material, deletes one, renames one or writes a `.meta`, because 336 federal sign faces are reached by
    /// path and 101 FBX files resolve `Concrete2` BY NAME (RM-48) -- a new asset with a new GUID would satisfy a
    /// shader check and break every one of those references.
    ///
    /// The operator reads each material's Built-in state BEFORE the swap, because `_Mode`, `_Color`, `_MainTex` and
    /// `_Glossiness` either do not exist on URP Lit or mean something under a different name, and the values are gone
    /// once the shader changes. `_Mode` is the authority on surface state: a keyword line can be stale, and one
    /// material in `Markers/` is opaque in a folder of 73 transparent ones.
    ///
    /// An unrecognised keyword is a FAILURE, not a silent drop. If a material carries a Built-in keyword this table
    /// does not translate, the run reports it and converts nothing for that asset, so a state nobody mapped cannot be
    /// lost by being ignored.
    /// </summary>
    public static class MaterialPortOperator
    {
        /// <summary> Built-in Standard's surface-mode float. Absent on URP Lit, so it is read before the swap. </summary>
        private const string ModeProperty = "_Mode";

        // RM-108 folded the three-row "Materials" submenu into "Setup", where the other once-per-project operators
        // already lived. A submenu of its own implied materials were a place an author works; they are not -- porting to
        // URP happens once, on import, and never again in a project's life. The priorities are unchanged, so the two
        // rows keep their order and stay grouped away from the licensing and document rows by Unity's 11-apart
        // separator rule.
        private const string MenuDryRun = "Window/Road City Master/Setup/Port to URP -- dry run";
        private const string MenuConvert = "Window/Road City Master/Setup/Port to URP -- convert in place";

        [MenuItem(MenuDryRun, false, 400)]
        public static void DryRunFromMenu()
        {
            string report;
            bool ok = Run(false, out report);
            Debug.Log(report);
            if (!ok) { Debug.LogWarning("RCMPORTOP dry run reported failures; see the report above."); }
        }

        [MenuItem(MenuConvert, false, 401)]
        public static void ConvertFromMenu()
        {
            string report;
            bool ok = Run(true, out report);
            Debug.Log(report);
            if (!ok) { Debug.LogWarning("RCMPORTOP conversion reported failures; see the report above."); }
        }

        /// <summary> Batchmode entry point: converts, logs the report and exits non-zero on any failure. </summary>
        public static void ConvertBatch()
        {
            string report;
            bool ok = Run(true, out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary> Batchmode entry point that changes nothing, for checking the plan against the tree. </summary>
        public static void DryRunBatch()
        {
            string report;
            bool ok = Run(false, out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary> The whole of RM-68 in one headless run, in the only order that works: convert the tree, mint the URP
        /// lens flare the Built-in `.flare` cannot be, regenerate every generated document from its register, then run
        /// the `RCMPORT` gate against what is now on disk. Exits non-zero if any of the four reports a failure, so a
        /// half-done port cannot pass for a done one. </summary>
        public static void PortAndRecordBatch()
        {
            string portReport;
            bool ported = Run(true, out portReport);
            Debug.Log(portReport);

            string flareReport;
            bool flared = FlarePortOperator.Mint(out flareReport);
            Debug.Log(flareReport);

            // Every generated document, not just this task's: RM-68 changed the shader inventory rows and the
            // disposition entry for the flare as well as the port record, and a register edited in code with its
            // document left unwritten reads as a gate failure rather than as the missing click it is.
            string writeMessage;
            bool written = RcmDocumentWriters.WriteAll(out writeMessage);
            Debug.Log(writeMessage);

            string gateReport;
            bool passed = MaterialPortGate.Run(out gateReport);
            Debug.Log(gateReport);

            EditorApplication.Exit(ported && flared && written && passed ? 0 : 1);
        }

        /// <summary> The tool's own `Materials` folder, in AssetDatabase form. `GetBasePath` hands back a path built
        /// by <c>Path.GetDirectoryName</c>, which uses the platform separator, and `AssetDatabase` only accepts
        /// forward slashes -- so the separators are normalised rather than assumed. </summary>
        public static string MaterialsFolder()
        {
            string root = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(root)) { return ""; }
            return root.Replace('\\', '/') + "/Materials";
        }

        /// <summary> Every material asset in the tool, in a stable ordinal order so two runs report the same
        /// sequence (C-06). </summary>
        public static List<string> MaterialPaths()
        {
            List<string> paths = new List<string>();
            string folder = MaterialsFolder();
            if (string.IsNullOrEmpty(folder)) { return paths; }
            string[] guids = AssetDatabase.FindAssets("t:Material", new string[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(path) && path.EndsWith(".mat", System.StringComparison.Ordinal))
                {
                    paths.Add(path);
                }
            }
            paths.Sort(System.StringComparer.Ordinal);
            return paths;
        }

        /// <summary> Which Standard shader a material sits on, from the shader's own NAME rather than a fileID: a
        /// fileID is not reachable from a <see cref="Material"/>, and the two names are unambiguous. </summary>
        private static bool ReadWorkflow(Shader shader, out PortWorkflow workflow)
        {
            workflow = PortWorkflow.Metallic;
            if (shader == null) { return false; }
            if (shader.name == "Standard") { workflow = PortWorkflow.Metallic; return true; }
            if (shader.name == "Standard (Specular setup)") { workflow = PortWorkflow.Specular; return true; }
            return false;
        }

        /// <summary> Surface state from `_Mode`, falling back to the keyword line only when the float is absent. </summary>
        private static PortSurface ReadSurface(Material material)
        {
            if (material.HasProperty(ModeProperty))
            {
                float mode = material.GetFloat(ModeProperty);
                if (mode > 1.5f) { return PortSurface.Fade; }
                if (mode > 0.5f) { return PortSurface.Cutout; }
                return PortSurface.Opaque;
            }
            if (material.IsKeywordEnabled("_ALPHATEST_ON")) { return PortSurface.Cutout; }
            if (material.IsKeywordEnabled("_ALPHABLEND_ON")) { return PortSurface.Fade; }
            if (material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON")) { return PortSurface.Fade; }
            return PortSurface.Opaque;
        }

        /// <summary> Translates one Built-in keyword. Returns false for anything the table does not know, so the
        /// caller can refuse the asset instead of dropping a state nobody mapped. `urp` comes back empty for the two
        /// blend keywords, whose state moves into floats rather than into a keyword. </summary>
        private static bool TranslateKeyword(string builtIn, out string urp)
        {
            urp = "";
            if (builtIn == "_NORMALMAP") { urp = "_NORMALMAP"; return true; }
            if (builtIn == "_EMISSION") { urp = "_EMISSION"; return true; }
            if (builtIn == "_DETAIL_MULX2") { urp = "_DETAIL_MULX2"; return true; }
            if (builtIn == "_ALPHATEST_ON") { urp = "_ALPHATEST_ON"; return true; }
            if (builtIn == "_PARALLAXMAP") { urp = "_PARALLAXMAP"; return true; }
            if (builtIn == "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A")
            {
                urp = "_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A";
                return true;
            }
            if (builtIn == "_SPECULARHIGHLIGHTS_OFF") { urp = "_SPECULARHIGHLIGHTS_OFF"; return true; }
            if (builtIn == "_GLOSSYREFLECTIONS_OFF") { urp = "_ENVIRONMENTREFLECTIONS_OFF"; return true; }
            if (builtIn == "_METALLICGLOSSMAP") { urp = "_METALLICSPECGLOSSMAP"; return true; }
            if (builtIn == "_SPECGLOSSMAP") { urp = "_METALLICSPECGLOSSMAP"; return true; }
            if (builtIn == "_ALPHABLEND_ON") { return true; }
            if (builtIn == "_ALPHAPREMULTIPLY_ON") { return true; }
            return false;
        }

        /// <summary> Writes the surface state URP reads. Every one of these floats is part of the state: URP does not
        /// derive the blend factors or the queue from `_Surface` on an already-serialised material, which is why a
        /// cutout that only gets its keyword back renders opaque. </summary>
        private static void ApplySurface(Material material, PortSurface surface)
        {
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlendAlpha", 1f);
            if (surface == PortSurface.Fade)
            {
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_SrcBlend", 5f);
                material.SetFloat("_DstBlend", 10f);
                material.SetFloat("_DstBlendAlpha", 10f);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_AlphaToMask", 0f);
                material.SetFloat("_BlendModePreserveSpecular", 0f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = MaterialPortRecipes.FadeQueue;
                return;
            }

            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SrcBlend", 1f);
            material.SetFloat("_DstBlend", 0f);
            material.SetFloat("_DstBlendAlpha", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_BlendModePreserveSpecular", 1f);
            if (surface == PortSurface.Cutout)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_AlphaToMask", 1f);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = MaterialPortRecipes.CutoutQueue;
                return;
            }

            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_AlphaToMask", 0f);
            material.SetOverrideTag("RenderType", "");
            material.renderQueue = -1;
        }

        /// <summary> Reads the Built-in values that do not survive the swap, assigns the URP Lit shader object to the
        /// SAME asset, then writes those values back under URP's names. The asset's GUID and `.meta` are untouched
        /// throughout: this is an edit of a material, not a replacement of one. </summary>
        private static void Convert(Material material, Shader lit, PortWorkflow workflow, PortSurface surface,
            List<string> urpKeywords)
        {
            Texture albedo = material.HasProperty("_MainTex") ? material.GetTexture("_MainTex") : null;
            Vector2 albedoScale = material.HasProperty("_MainTex")
                ? material.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 albedoOffset = material.HasProperty("_MainTex")
                ? material.GetTextureOffset("_MainTex") : Vector2.zero;
            Color albedoTint = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white;
            float smoothness = material.HasProperty("_Glossiness") ? material.GetFloat("_Glossiness") : 0.5f;
            float reflections = material.HasProperty("_GlossyReflections")
                ? material.GetFloat("_GlossyReflections") : 1f;
            float cutoff = material.HasProperty("_Cutoff") ? material.GetFloat("_Cutoff") : 0.5f;

            material.shader = lit;

            material.SetTexture("_BaseMap", albedo);
            material.SetTextureScale("_BaseMap", albedoScale);
            material.SetTextureOffset("_BaseMap", albedoOffset);
            material.SetColor("_BaseColor", albedoTint);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_EnvironmentReflections", reflections);
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_WorkflowMode", workflow == PortWorkflow.Specular ? 0f : 1f);

            ApplySurface(material, surface);

            material.SetFloat("_ReceiveShadows", 1f);
            material.SetFloat("_QueueOffset", 0f);
            material.SetFloat("_ClearCoatMask", 0f);
            material.SetFloat("_ClearCoatSmoothness", 0f);
            material.SetFloat("_AddPrecomputedVelocity", 0f);
            material.SetFloat("_XRMotionVectorsPass", 1f);
            material.SetFloat("_Cull", 2f);

            material.shaderKeywords = urpKeywords.ToArray();
            if (urpKeywords.Contains("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A"))
            {
                material.SetFloat("_SmoothnessTextureChannel", 1f);
            }

            EditorUtility.SetDirty(material);
        }

        /// <summary> The URP keyword list for one material: every translated Built-in keyword, plus the two URP
        /// keywords that carry state Built-in expressed some other way. </summary>
        private static bool BuildKeywords(Material material, PortWorkflow workflow, PortSurface surface,
            List<string> urpKeywords, out string unknown)
        {
            unknown = "";
            string[] builtIn = material.shaderKeywords;
            for (int i = 0; i < builtIn.Length; i++)
            {
                string translated;
                if (!TranslateKeyword(builtIn[i], out translated))
                {
                    unknown = builtIn[i];
                    return false;
                }
                if (translated.Length > 0 && !urpKeywords.Contains(translated)) { urpKeywords.Add(translated); }
            }
            if (workflow == PortWorkflow.Specular && !urpKeywords.Contains("_SPECULAR_SETUP"))
            {
                urpKeywords.Add("_SPECULAR_SETUP");
            }
            if (surface == PortSurface.Fade && !urpKeywords.Contains("_SURFACE_TYPE_TRANSPARENT"))
            {
                urpKeywords.Add("_SURFACE_TYPE_TRANSPARENT");
            }
            urpKeywords.Sort(System.StringComparer.Ordinal);
            return true;
        }

        /// <summary> The whole pass. With <paramref name="apply"/> false nothing is written and the report says what
        /// WOULD happen, which is the only way to review 470 conversions before they land. </summary>
        public static bool Run(bool apply, out string report)
        {
            StringBuilder builder = new StringBuilder();
            List<string> failures = new List<string>();
            builder.Append("RCMPORTOP BEGIN  mode=").Append(apply ? "convert-in-place" : "dry-run").Append("\n");

            Shader lit = Shader.Find(PipelineAudit.UrpLitShaderName);
            if (lit == null)
            {
                failures.Add("the shader '" + PipelineAudit.UrpLitShaderName + "' did not resolve, so nothing was "
                    + "touched. Either URP is not installed or its Lit shader failed to compile");
            }

            List<string> paths = MaterialPaths();
            builder.Append("RCMPORTOP folder ").Append(MaterialsFolder()).Append("  materials ")
                   .Append(paths.Count.ToString(CultureInfo.InvariantCulture)).Append("\n");
            if (paths.Count != MaterialPortRecipes.TreeTotal)
            {
                failures.Add("the folder holds " + paths.Count.ToString(CultureInfo.InvariantCulture)
                    + " materials, and the register expects "
                    + MaterialPortRecipes.TreeTotal.ToString(CultureInfo.InvariantCulture)
                    + " (" + MaterialPortRecipes.RecipeTotal.ToString(CultureInfo.InvariantCulture)
                    + " ported plus " + MaterialPortRecipes.AuthoredByRepair.ToString(CultureInfo.InvariantCulture)
                    + " authored by RM-67). A count that moved means an asset was added, moved or lost");
            }

            int converted = 0;
            int already = 0;
            int opaque = 0;
            int cutout = 0;
            int fade = 0;
            int metallic = 0;
            int specular = 0;

            for (int i = 0; i < paths.Count && lit != null; i++)
            {
                string path = paths[i];
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    failures.Add("'" + path + "' would not load as a Material");
                    continue;
                }
                if (material.shader == lit)
                {
                    already++;
                    builder.Append("RCMPORTOP skip   ").Append(path).Append("  already on URP Lit\n");
                    continue;
                }

                PortWorkflow workflow;
                if (!ReadWorkflow(material.shader, out workflow))
                {
                    failures.Add("'" + path + "' is on shader '"
                        + (material.shader == null ? "(none)" : material.shader.name)
                        + "', which is neither Built-in Standard nor URP Lit, so no recipe covers it");
                    continue;
                }

                PortSurface surface = ReadSurface(material);
                List<string> keywords = new List<string>();
                string unknown;
                if (!BuildKeywords(material, workflow, surface, keywords, out unknown))
                {
                    failures.Add("'" + path + "' carries the Built-in keyword '" + unknown + "', which this operator "
                        + "does not translate. It was left alone rather than converted with a state dropped");
                    continue;
                }

                if (surface == PortSurface.Cutout) { cutout++; }
                else if (surface == PortSurface.Fade) { fade++; }
                else { opaque++; }
                if (workflow == PortWorkflow.Specular) { specular++; } else { metallic++; }

                if (apply) { Convert(material, lit, workflow, surface, keywords); }
                converted++;
                builder.Append("RCMPORTOP ").Append(apply ? "port   " : "plan   ").Append(path)
                       .Append("  ").Append(MaterialPortRecipes.SurfaceText(surface))
                       .Append(" ").Append(MaterialPortRecipes.WorkflowText(workflow))
                       .Append("  keywords=").Append(keywords.Count == 0 ? "(blank)" : string.Join(" ",
                           keywords.ToArray())).Append("\n");
            }

            if (apply && converted > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            builder.Append("RCMPORTOP census surfaces  opaque ")
                   .Append(opaque.ToString(CultureInfo.InvariantCulture)).Append("  cutout ")
                   .Append(cutout.ToString(CultureInfo.InvariantCulture)).Append("  fade ")
                   .Append(fade.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append("RCMPORTOP census workflow  metallic ")
                   .Append(metallic.ToString(CultureInfo.InvariantCulture)).Append("  specular ")
                   .Append(specular.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append("RCMPORTOP counts  ").Append(apply ? "converted " : "planned ")
                   .Append(converted.ToString(CultureInfo.InvariantCulture)).Append("  already-urp ")
                   .Append(already.ToString(CultureInfo.InvariantCulture)).Append("\n");

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMPORTOP FAIL   ").Append(failures[i]).Append("\n");
            }
            builder.Append("RCMPORTOP result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append("\n");
            builder.Append("RCMPORTOP END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Counts the tree as it stands, without changing anything. The shader is identified by the GUID of
        /// the shader ASSET each material points at, so the count is about what was serialised rather than about which
        /// shader object happens to be loaded. </summary>
        public static PortEndState ReadEndState()
        {
            List<string> paths = MaterialPaths();
            int onUrpLit = 0;
            int onBuiltIn = 0;
            int onOther = 0;
            int opaque = 0;
            int cutout = 0;
            int fade = 0;
            int metallic = 0;
            int specular = 0;

            for (int i = 0; i < paths.Count; i++)
            {
                Material material = AssetDatabase.LoadAssetAtPath<Material>(paths[i]);
                if (material == null) { onOther++; continue; }

                string shaderPath = material.shader == null ? "" : AssetDatabase.GetAssetPath(material.shader);
                string guid = string.IsNullOrEmpty(shaderPath) ? "" : AssetDatabase.AssetPathToGUID(shaderPath);
                if (guid == PipelineAudit.UrpLitShaderGuid) { onUrpLit++; }
                else if (guid == PipelineAudit.BuiltInResourceShaderGuid) { onBuiltIn++; }
                else { onOther++; }

                bool isFade = material.HasProperty("_Surface") && material.GetFloat("_Surface") > 0.5f;
                bool isCutout = !isFade && material.HasProperty("_AlphaClip")
                    && material.GetFloat("_AlphaClip") > 0.5f;
                if (isFade) { fade++; }
                else if (isCutout) { cutout++; }
                else { opaque++; }

                bool isSpecular = material.HasProperty("_WorkflowMode")
                    && material.GetFloat("_WorkflowMode") < 0.5f;
                if (isSpecular) { specular++; } else { metallic++; }
            }

            return new PortEndState(paths.Count, onUrpLit, onBuiltIn, onOther, opaque, cutout, fade, metallic,
                specular);
        }
    }
}
#endif
