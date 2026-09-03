#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-68's gate: proves the port happened and that it happened by the register rather than by hand.
    ///
    /// Twelve cases. The 24 recipes sum to the 471 materials RM-67 counted; the recipes' own surface and workflow
    /// censuses agree with the measured `_Mode` split and with RM-67's two engine-shader counts; every recipe names its
    /// members, says what it is for, and marks its values measured-with-source or AUTHORED; the mapping table carries
    /// the four rules a URP port fails on; not one material under `Materials/` is still on the Built-in resource
    /// shader, checked twice -- once through the AssetDatabase and once by reading the raw `.mat` text, because a
    /// material can be re-serialised without being re-imported; the 37 cutouts are still cutouts and the 36 fades are
    /// still fades AFTER the port, read back off the URP properties rather than the Built-in ones; both tables are well
    /// formed; and `Docs/MaterialPort.md` still matches the register. Report lines are prefixed RCMPORT.
    ///
    /// What it does NOT prove: that a ported material looks like what it replaced. A gate can prove the shader, the
    /// surface state and the keyword line; only the editor and a build can prove a pixel. The screenshots are the
    /// author's, and RM-68 says so in its own document.
    /// </summary>
    public static class MaterialPortGate
    {
        [MenuItem("Window/Road City Master/Testing/Material port against the tree")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.MaterialPortGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPORT BEGIN\n");
            List<string> failures = new List<string>();

            CaseRecipesSumToTheTree(builder, failures);
            CaseSurfaceCensusMatchesTheRecipes(builder, failures);
            CaseWorkflowCensusMatchesTheInventory(builder, failures);
            CaseEveryRecipeIsReviewable(builder, failures);
            CaseEveryValueIsMeasuredOrAuthored(builder, failures);
            CaseMappingCoversTheHardRules(builder, failures);
            CaseNothingIsStillOnBuiltIn(builder, failures);
            CaseSurfaceStateSurvivedThePort(builder, failures);
            CaseWorkflowSurvivedThePort(builder, failures);
            CaseNoMaterialTextNamesTheBuiltInGuid(builder, failures);
            CaseTablesAreWellFormed(builder, failures);
            CaseDocumentMatchesRegisters(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPORT FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPORT result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPORT END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }

        /// <summary> Case 1: the recipes account for every material RM-67 counted, and no recipe id repeats. A port
        /// register that covers 460 of 471 is a register with eleven materials nobody decided about. </summary>
        private static void CaseRecipesSumToTheTree(StringBuilder builder, List<string> failures)
        {
            int covered = MaterialPortRecipes.TotalCovered();
            builder.Append("RCMPORT case1  recipes ").Append(MaterialPortRecipes.All().Count)
                   .Append("  covering ").Append(covered).Append(" of ")
                   .Append(MaterialPortRecipes.RecipeTotal).Append(" materials\n");
            Expect(covered == MaterialPortRecipes.RecipeTotal,
                "case1: the recipes cover " + covered + " materials and the tool has "
                + MaterialPortRecipes.RecipeTotal, failures);
            Expect(MaterialPortRecipes.RecipeTotal == ShaderInventory.MaterialTotal,
                "case1: RM-68's total " + MaterialPortRecipes.RecipeTotal + " disagrees with RM-67's counted total "
                + ShaderInventory.MaterialTotal, failures);

            List<string> seen = new List<string>();
            foreach (MaterialRecipe recipe in MaterialPortRecipes.All())
            {
                Expect(!seen.Contains(recipe.id), "case1: recipe id '" + recipe.id + "' appears twice", failures);
                seen.Add(recipe.id);
                Expect(recipe.count > 0, "case1: recipe " + recipe.id + " covers no materials", failures);
            }
        }

        /// <summary> Case 2: the register's own surface split is the one measured off `_Mode`. </summary>
        private static void CaseSurfaceCensusMatchesTheRecipes(StringBuilder builder, List<string> failures)
        {
            int opaque = MaterialPortRecipes.CountWith(PortSurface.Opaque);
            int cutout = MaterialPortRecipes.CountWith(PortSurface.Cutout);
            int fade = MaterialPortRecipes.CountWith(PortSurface.Fade);
            builder.Append("RCMPORT case2  opaque ").Append(opaque).Append("  cutout ").Append(cutout)
                   .Append("  fade ").Append(fade).Append("\n");
            Expect(opaque == MaterialPortRecipes.OpaqueCount,
                "case2: the recipes make " + opaque + " materials opaque and " + MaterialPortRecipes.ModeOpaqueCount
                + " were measured at `_Mode: 0` plus " + MaterialPortRecipes.AlreadyPorted
                + " already on URP Lit and opaque", failures);
            Expect(cutout == MaterialPortRecipes.CutoutCount,
                "case2: the recipes make " + cutout + " materials cutout and `_Mode: 1` was measured on "
                + MaterialPortRecipes.CutoutCount, failures);
            Expect(fade == MaterialPortRecipes.FadeCount,
                "case2: the recipes make " + fade + " materials fade and `_Mode: 2` was measured on "
                + MaterialPortRecipes.FadeCount, failures);
            Expect(opaque + cutout + fade == MaterialPortRecipes.RecipeTotal,
                "case2: the three surface states sum to " + (opaque + cutout + fade) + " and not "
                + MaterialPortRecipes.RecipeTotal, failures);
        }

        /// <summary> Case 3: the register's workflow split is RM-67's two engine-shader counts, so a material cannot
        /// have been quietly moved from specular setup to metallic on the way through. </summary>
        private static void CaseWorkflowCensusMatchesTheInventory(StringBuilder builder, List<string> failures)
        {
            int metallic = MaterialPortRecipes.CountWith(PortWorkflow.Metallic);
            int specular = MaterialPortRecipes.CountWith(PortWorkflow.Specular);
            builder.Append("RCMPORT case3  metallic ").Append(metallic).Append(" (fileID 46)  specular ")
                   .Append(specular).Append(" (fileID 45)\n");
            Expect(metallic == ShaderInventory.OnStandard,
                "case3: the recipes convert " + metallic + " materials as metallic and RM-67 counted "
                + ShaderInventory.OnStandard + " on `fileID: 46`", failures);
            Expect(specular == ShaderInventory.OnStandardSpecular,
                "case3: the recipes convert " + specular + " materials as specular setup and RM-67 counted "
                + ShaderInventory.OnStandardSpecular + " on `fileID: 45`", failures);
        }

        /// <summary> Case 4: a recipe a reviewer cannot read is not a decision. Each one has to name its members, say
        /// which family it is, and say what the conversion is for. </summary>
        private static void CaseEveryRecipeIsReviewable(StringBuilder builder, List<string> failures)
        {
            int reviewable = 0;
            foreach (MaterialRecipe recipe in MaterialPortRecipes.All())
            {
                bool complete = !string.IsNullOrEmpty(recipe.id)
                    && !string.IsNullOrEmpty(recipe.family)
                    && !string.IsNullOrEmpty(recipe.members)
                    && !string.IsNullOrEmpty(recipe.intent)
                    && recipe.intent.Length > 20;
                Expect(complete, "case4: recipe '" + recipe.id + "' does not name its family, its members and its "
                    + "purpose", failures);
                if (complete) { reviewable++; }
            }
            builder.Append("RCMPORT case4  ").Append(reviewable).Append(" of ")
                   .Append(MaterialPortRecipes.All().Count).Append(" recipes name members, family and purpose\n");
        }

        /// <summary> Case 5: C-15. Every value either came off the asset or is AUTHORED with the reason, and a recipe
        /// that says neither is a guess wearing a register's clothes. </summary>
        private static void CaseEveryValueIsMeasuredOrAuthored(StringBuilder builder, List<string> failures)
        {
            int measured = 0;
            int authored = 0;
            foreach (MaterialRecipe recipe in MaterialPortRecipes.All())
            {
                string source = recipe.provenance == null ? "" : recipe.provenance;
                bool saysMeasured = source.Contains("measured");
                bool saysAuthored = source.Contains("AUTHORED");
                Expect(saysMeasured || saysAuthored,
                    "case5: recipe '" + recipe.id + "' says neither measured nor AUTHORED", failures);
                Expect(source.Length > 20,
                    "case5: recipe '" + recipe.id + "' gives no source for its values", failures);
                if (saysMeasured) { measured++; }
                if (saysAuthored) { authored++; }
            }
            builder.Append("RCMPORT case5  measured ").Append(measured).Append("  AUTHORED ").Append(authored)
                   .Append("  of ").Append(MaterialPortRecipes.All().Count).Append(" recipes\n");
        }

        /// <summary> Case 6: the four rules a hand-written URP port gets wrong. Built-in's Fade keyword has no URP
        /// counterpart and has to become blend floats; specular setup needs `_SPECULAR_SETUP` or every guardrail goes
        /// dark; cutout needs `_AlphaClip` as well as the keyword; and Built-in's Fade dims specular, which URP's
        /// default does not. If the table stops naming one of them, the operator is being checked against nothing.
        /// </summary>
        private static void CaseMappingCoversTheHardRules(StringBuilder builder, List<string> failures)
        {
            StringBuilder text = new StringBuilder();
            foreach (PortMapping mapping in MaterialPortRecipes.Mappings())
            {
                text.Append(mapping.builtIn).Append(' ').Append(mapping.urp).Append(' ').Append(mapping.note)
                    .Append('\n');
            }
            string all = text.ToString();

            string[] rules = new string[]
            {
                "_ALPHABLEND_ON", "_SPECULAR_SETUP", "_ALPHATEST_ON", "_BlendModePreserveSpecular",
                "_WorkflowMode", "_BaseColor", "_BaseMap", "_METALLICSPECGLOSSMAP"
            };
            int named = 0;
            foreach (string rule in rules)
            {
                bool present = all.Contains(rule);
                Expect(present, "case6: the mapping table never mentions `" + rule + "`", failures);
                if (present) { named++; }
            }
            builder.Append("RCMPORT case6  ").Append(MaterialPortRecipes.Mappings().Count)
                   .Append(" mapping rules, naming ").Append(named).Append(" of ").Append(rules.Length)
                   .Append(" hard cases\n");
        }

        /// <summary> Case 7: read the tree back. Every material under `Materials/` names URP's Lit shader, by the GUID
        /// of the shader asset it points at rather than by a display name, and none is on the Built-in resource shader
        /// or on anything else. This is the case that proves the port ran rather than that it was planned. </summary>
        private static void CaseNothingIsStillOnBuiltIn(StringBuilder builder, List<string> failures)
        {
            PortEndState state = MaterialPortOperator.ReadEndState();
            builder.Append("RCMPORT case7  materials ").Append(state.total).Append("  on URP Lit ")
                   .Append(state.onUrpLit).Append("  on Built-in ").Append(state.onBuiltIn).Append("  elsewhere ")
                   .Append(state.onOther).Append("\n");
            Expect(state.total == MaterialPortRecipes.TreeTotal,
                "case7: the tree holds " + state.total + " materials and RM-68 counted "
                + MaterialPortRecipes.TreeTotal, failures);
            Expect(state.onBuiltIn == 0,
                "case7: " + state.onBuiltIn + " materials are still on the Built-in resource shader `"
                + PipelineAudit.BuiltInResourceShaderGuid + "`; run Window > Road City Master > Materials > Port to "
                + "URP -- convert in place", failures);
            Expect(state.onOther == 0,
                "case7: " + state.onOther + " materials are on neither URP Lit nor the Built-in resource shader",
                failures);
            Expect(state.onUrpLit == MaterialPortRecipes.TreeTotal,
                "case7: " + state.onUrpLit + " materials name `" + PipelineAudit.UrpLitShaderName + "` and the tree "
                + "holds " + MaterialPortRecipes.TreeTotal, failures);
        }

        /// <summary> Case 8: the surface states survived, read off URP's `_Surface` and `_AlphaClip` rather than off
        /// Built-in's `_Mode`, so a port that moved the shader and dropped the transparency would fail here. The 401
        /// counts the four materials that were already on URP Lit before the port, all four opaque. </summary>
        private static void CaseSurfaceStateSurvivedThePort(StringBuilder builder, List<string> failures)
        {
            PortEndState state = MaterialPortOperator.ReadEndState();
            int expectedOpaque = MaterialPortRecipes.OpaqueCount + MaterialPortRecipes.AuthoredByRepair;
            builder.Append("RCMPORT case8  opaque ").Append(state.opaque).Append("  cutout ").Append(state.cutout)
                   .Append("  fade ").Append(state.fade).Append("  (read off `_Surface` and `_AlphaClip`)\n");
            Expect(state.cutout == MaterialPortRecipes.CutoutCount,
                "case8: " + state.cutout + " materials are cutout after the port and " + MaterialPortRecipes.CutoutCount
                + " were cutout before it", failures);
            Expect(state.fade == MaterialPortRecipes.FadeCount,
                "case8: " + state.fade + " materials are transparent after the port and "
                + MaterialPortRecipes.FadeCount + " were at `_Mode: 2` before it", failures);
            Expect(state.opaque == expectedOpaque,
                "case8: " + state.opaque + " materials are opaque after the port and " + expectedOpaque
                + " were expected", failures);
            Expect(state.opaque + state.cutout + state.fade == state.total,
                "case8: the three surface states sum to " + (state.opaque + state.cutout + state.fade)
                + " and the tree holds " + state.total, failures);
        }

        /// <summary> Case 9: the workflow split survived, read off URP's `_WorkflowMode`. RM-67's 115 specular-setup
        /// materials are still specular; the 356 metallic ones plus RM-67's three authored repairs -- measured at
        /// `_WorkflowMode: 1` -- make 359. A material moved from specular to metallic loses its `_SpecColor` and turns
        /// dark, which no shader check would catch. </summary>
        private static void CaseWorkflowSurvivedThePort(StringBuilder builder, List<string> failures)
        {
            PortEndState state = MaterialPortOperator.ReadEndState();
            int expectedMetallic = ShaderInventory.OnStandard + MaterialPortRecipes.AuthoredByRepair;
            builder.Append("RCMPORT case9  metallic ").Append(state.metallic).Append("  specular ")
                   .Append(state.specular).Append("  (read off `_WorkflowMode`)\n");
            Expect(state.specular == ShaderInventory.OnStandardSpecular,
                "case9: " + state.specular + " materials are specular setup after the port and "
                + ShaderInventory.OnStandardSpecular + " were on `fileID: 45` before it", failures);
            Expect(state.metallic == expectedMetallic,
                "case9: " + state.metallic + " materials are metallic after the port and " + expectedMetallic
                + " were expected", failures);
            Expect(state.metallic + state.specular == state.total,
                "case9: the two workflows sum to " + (state.metallic + state.specular) + " and the tree holds "
                + state.total, failures);
        }

        /// <summary> Case 10: the same claim as case 7, made a second way. A `Material` object is what Unity has in
        /// memory, and a re-serialised asset can disagree with it until the next import. So read the raw `.mat` text off
        /// disk and refuse to find the Built-in resource GUID anywhere in it -- not on `m_Shader`, not on a stale
        /// reference further down the file. Absolute path composed from <see cref="Application.dataPath"/> (C-04).
        /// </summary>
        private static void CaseNoMaterialTextNamesTheBuiltInGuid(StringBuilder builder, List<string> failures)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            List<string> paths = MaterialPortOperator.MaterialPaths();
            List<string> offenders = new List<string>();
            int read = 0;

            foreach (string assetPath in paths)
            {
                string full = Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) { failures.Add("case10: '" + assetPath + "' is not on disk"); continue; }
                string text = File.ReadAllText(full);
                read++;
                if (text.Contains(PipelineAudit.BuiltInResourceShaderGuid)) { offenders.Add(assetPath); }
            }

            builder.Append("RCMPORT case10 read ").Append(read).Append(" `.mat` files as text; ")
                   .Append(offenders.Count).Append(" name the Built-in resource GUID\n");
            Expect(read == MaterialPortRecipes.TreeTotal,
                "case10: read " + read + " material files and the tree holds " + MaterialPortRecipes.TreeTotal,
                failures);
            for (int i = 0; i < offenders.Count && i < 6; i++)
            {
                failures.Add("case10: '" + offenders[i] + "' still names `"
                    + PipelineAudit.BuiltInResourceShaderGuid + "` in its serialised text");
            }
            Expect(offenders.Count == 0,
                "case10: " + offenders.Count + " material files still name the Built-in resource shader in text",
                failures);
        }

        /// <summary> Case 11: both generated tables are well formed, so a register row that lost a cell is caught here
        /// rather than by a reader wondering which column they are reading. </summary>
        private static void CaseTablesAreWellFormed(StringBuilder builder, List<string> failures)
        {
            CheckTable(builder, failures, "recipes", MaterialPortRecipes.Table(), 11,
                MaterialPortRecipes.All().Count);
            CheckTable(builder, failures, "mapping", MaterialPortRecipes.MappingTable(), 4,
                MaterialPortRecipes.Mappings().Count);
        }

        private static void CheckTable(StringBuilder builder, List<string> failures, string name, string table,
            int pipesPerLine, int rows)
        {
            string[] lines = table.Split('\n');
            Expect(lines.Length == rows + 2,
                "case11: the " + name + " table has " + lines.Length + " lines for " + rows + " rows", failures);
            Expect(lines.Length > 1 && lines[1].StartsWith("| ---", System.StringComparison.Ordinal),
                "case11: the " + name + " table has no separator row", failures);

            for (int i = 0; i < lines.Length; i++)
            {
                int pipes = 0;
                foreach (char c in lines[i]) { if (c == '|') { pipes++; } }
                Expect(pipes == pipesPerLine,
                    "case11: " + name + " line " + (i + 1) + " has " + pipes + " pipes, not " + pipesPerLine,
                    failures);
            }

            builder.Append("RCMPORT case11 the ").Append(name).Append(" table is well formed (").Append(rows)
                   .Append(" rows, ").Append(pipesPerLine - 1).Append(" columns)\n");
        }

        /// <summary> Case 12: `Docs/MaterialPort.md` holds what the register generates, and its hand-written half still
        /// states the four things a reader needs and cannot get from a table: that the port edits materials in place,
        /// which shader they land on, the two departures from URP's defaults, and that `TunnelLights` looking dead is a
        /// flare regression rather than a material one. Read-only: it reports drift and names the menu item that fixes
        /// it. </summary>
        private static void CaseDocumentMatchesRegisters(StringBuilder builder, List<string> failures)
        {
            string path = MaterialPortWriter.DocPath();
            int matched = 0;
            for (int i = 0; i < MaterialPortWriter.TableCount; i++)
            {
                string onDisk, message;
                if (!MaterialPortWriter.ReadTableFrom(path, i, out onDisk, out message))
                {
                    failures.Add("case12: " + message);
                    continue;
                }

                if (TerrainFeatureTableWriter.Normalise(onDisk)
                    == TerrainFeatureTableWriter.Normalise(MaterialPortWriter.WantedBody(i)))
                {
                    matched++;
                    continue;
                }
                failures.Add("case12: table " + i + " in '" + path + "' has drifted from the register; run Window > "
                    + "Road City Master > Documentation > Write the material port record");
            }

            string contents, readMessage;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out contents, out readMessage))
            {
                failures.Add("case12: " + readMessage);
                builder.Append("RCMPORT case12 the record could not be read\n");
                return;
            }

            string[] claims = new string[]
            {
                PipelineAudit.UrpLitShaderName, PipelineAudit.BuiltInResourceShaderGuid,
                "_BlendModePreserveSpecular", "_AlphaToMask", "TunnelLights", "in place", "RCMPORT"
            };
            int stated = 0;
            foreach (string claim in claims)
            {
                bool present = contents.Contains(claim);
                Expect(present, "case12: the record never states '" + claim + "'", failures);
                if (present) { stated++; }
            }

            builder.Append("RCMPORT case12 ").Append(matched).Append(" of ").Append(MaterialPortWriter.TableCount)
                   .Append(" tables match the register; ").Append(stated).Append(" of ").Append(claims.Length)
                   .Append(" written claims present\n");
        }
    }
}
#endif
