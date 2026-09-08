// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
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
    /// <summary> RM-67's gate: proves the shader and material inventory is an audit and not an assumption.
    ///
    /// Thirteen cases. Every row carries evidence read out of the asset; every row is marked Built-in, URP or
    /// pipeline-agnostic and the type itself cannot hold "unknown"; the material families PARTITION the tree, summing
    /// to the counted total, and the two engine-shader counts sum to the same number; all ten of the tool's shaders are
    /// named and on disk; the lens flare is named and on disk; every "used by" cell either cites a file or states
    /// plainly that nothing reaches the asset; the four broken material references are each recorded with their fix and
    /// every one of them now resolves on disk and names URP's Lit shader; the pipeline facts carry the URP version and
    /// the stripping flags, each with the file and
    /// line read from; the ShaderLab gotchas cover the four this project has already paid for; all four tables are well
    /// formed; Docs/ShaderInventory.md still matches the registers; the URP Lit GUID this file compares raw `.mat`
    /// text against is still the GUID the shader of that name actually has, so a stale literal fails instead of
    /// silently passing; and every asset path in all 110 `.rao` libraries loads, with every material among them on URP
    /// Lit -- the one part of the port that lives in XML rather than in a `.mat`. Report lines are prefixed RCMSHADER.
    ///
    /// What it does NOT prove: that a ported shader renders correctly. A gate can prove a claim is evidenced and can
    /// prove a path resolves; only the editor and a build can prove a pixel. RM-68 owns the port itself.
    /// </summary>
    public static class ShaderInventoryGate
    {
        [MenuItem("Window/Road City Master/Testing/Shader and material inventory against the project")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.ShaderInventoryGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMSHADER BEGIN\n");
            List<string> failures = new List<string>();

            CaseEveryRowCarriesEvidence(builder, failures);
            CaseNothingIsUnknown(builder, failures);
            CaseFamiliesPartitionTheTree(builder, failures);
            CaseAllTenShadersAreNamedAndOnDisk(builder, failures);
            CaseTheFlareIsNamedAndOnDisk(builder, failures);
            CaseEveryUseIsCitedOrDeclaredUnreachable(builder, failures);
            CaseMissingMaterialsAreReallyFixed(builder, failures);
            CasePipelineFactsAreReadFromTheProject(builder, failures);
            CaseGotchasCoverTheFourPaidFor(builder, failures);
            CaseTablesAreWellFormed(builder, failures);
            CaseDocumentMatchesRegisters(builder, failures);
            CaseUrpLitGuidStillResolves(builder, failures);
            CaseLibraryPathsResolveAndAreOnUrp(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSHADER FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSHADER result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSHADER END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }

        private static bool CitesALocation(string text)
        {
            if (string.IsNullOrEmpty(text)) { return false; }
            string[] extensions = new string[]
            {
                ".cs", ".rao", ".prefab", ".unity", ".asset", ".mat", ".shader", ".flare", ".json", ".meta", ".fbx",
                ".FBX",
            };
            foreach (string extension in extensions)
            {
                if (text.IndexOf(extension, System.StringComparison.Ordinal) >= 0) { return true; }
            }
            return false;
        }

        /// <summary> Case 1. An inventory row with no evidence is the failure this task exists to prevent: a pipeline
        /// claim that reads as read-out-of-the-asset and was guessed from a folder name. </summary>
        private static void CaseEveryRowCarriesEvidence(StringBuilder builder, List<string> failures)
        {
            IList<RenderAssetEntry> all = ShaderInventory.All();
            Expect(all.Count >= 23,
                "case1: only " + all.Count + " rows in the inventory; ten shaders, three HLSL includes, eight material "
                + "families and both halves of the lens flare is twenty-three", failures);

            List<string> seen = new List<string>();
            foreach (RenderAssetEntry e in all)
            {
                Expect(!string.IsNullOrEmpty(e.asset), "case1: a row has no asset name", failures);
                Expect(e.count >= 1, "case1: '" + e.asset + "' accounts for " + e.count + " assets", failures);
                Expect(e.targetEvidence != null && e.targetEvidence.Length > 40,
                    "case1: '" + e.asset + "' gives no evidence for its pipeline target", failures);
                Expect(e.usedBy != null && e.usedBy.Length > 20,
                    "case1: '" + e.asset + "' does not say what reaches it", failures);
                Expect(e.portOwner != null && e.portOwner.Length > 20,
                    "case1: '" + e.asset + "' names no port owner and no reason none is owed", failures);
                Expect(e.asset.IndexOf('|') < 0 && e.targetEvidence.IndexOf('|') < 0
                    && e.usedBy.IndexOf('|') < 0 && e.portOwner.IndexOf('|') < 0,
                    "case1: '" + e.asset + "' has a pipe in one of its cells", failures);
                Expect(!seen.Contains(e.asset), "case1: '" + e.asset + "' appears twice", failures);
                seen.Add(e.asset);
            }

            builder.Append("RCMSHADER case1 ").Append(all.Count)
                   .Append(" rows, each with evidence, a use and a port owner\n");
        }

        /// <summary> Case 2. RM-67's sign-off bar. The guarantee is in the TYPE: <see cref="PipelineTarget"/> has no
        /// Unknown member, so a row cannot claim one. The gate proves the enum still has exactly the three members it
        /// was designed with, because a later member is how this bar would quietly stop holding, and proves no cell
        /// hedges in prose either. </summary>
        private static void CaseNothingIsUnknown(StringBuilder builder, List<string> failures)
        {
            System.Array members = System.Enum.GetValues(typeof(PipelineTarget));
            Expect(members.Length == 3,
                "case2: PipelineTarget has " + members.Length + " members; three (Built-in, URP, pipeline-agnostic) is "
                + "what makes 'nothing is unknown' unfalsifiable-by-typo", failures);
            foreach (string name in System.Enum.GetNames(typeof(PipelineTarget)))
            {
                Expect(name.IndexOf("Unknown", System.StringComparison.OrdinalIgnoreCase) < 0,
                    "case2: PipelineTarget has a member named '" + name + "'", failures);
            }

            string[] hedges = new string[] { "unknown", "TBD", "probably", "not sure", "unclear", "maybe", "assume" };
            int builtIn = 0;
            string builtInRow = "";
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                string row = e.asset + " " + e.targetEvidence + " " + e.usedBy + " " + e.portOwner;
                foreach (string hedge in hedges)
                {
                    Expect(row.IndexOf(hedge, System.StringComparison.OrdinalIgnoreCase) < 0,
                        "case2: '" + e.asset + "' hedges with '" + hedge + "'", failures);
                }
                Expect(ShaderInventory.TargetText(e.target).Length > 0,
                    "case2: '" + e.asset + "' has no printable target", failures);
                if (e.target == PipelineTarget.BuiltIn) { builtIn++; builtInRow = e.asset; }
            }

            // Post-RM-68 invariant. Exactly one row may still read Built-in: the `.flare`, which URP cannot read at all
            // and so was superseded by a `LensFlareDataSRP` rather than converted. Every other row went to URP in place.
            // A second Built-in row means either the port regressed a file or a new Built-in asset entered the tree.
            Expect(builtIn == 1,
                "case2: " + builtIn + " of " + ShaderInventory.All().Count + " rows read Built-in; after RM-68 exactly "
                + "one may -- the superseded Flares/SodiumBulb.flare, kept as the record of what the port carried "
                + "across", failures);
            Expect(builtIn != 1 || builtInRow.EndsWith(".flare", System.StringComparison.Ordinal),
                "case2: the one Built-in row is '" + builtInRow + "'; the only asset class URP cannot read in place is "
                + "the .flare", failures);
            builder.Append("RCMSHADER case2 three targets in the type, no Unknown member, no hedged cell, ")
                   .Append(ShaderInventory.All().Count - builtIn).Append(" rows URP and ").Append(builtIn)
                   .Append(" superseded Built-in as audited\n");
        }

        /// <summary> Case 3. The families are a PARTITION, not a sample: their counts sum to every material counted on
        /// disk, and the two engine shaders those materials sit on sum to the same total. Either sum drifting means the
        /// inventory has stopped describing the whole tree. </summary>
        private static void CaseFamiliesPartitionTheTree(StringBuilder builder, List<string> failures)
        {
            int accounted = ShaderInventory.MaterialsAccountedFor();
            Expect(accounted == ShaderInventory.MaterialTotal,
                "case3: the family rows account for " + accounted + " materials, not " + ShaderInventory.MaterialTotal,
                failures);
            Expect(ShaderInventory.OnStandard + ShaderInventory.OnStandardSpecular == ShaderInventory.MaterialTotal,
                "case3: " + ShaderInventory.OnStandard + " on Standard plus " + ShaderInventory.OnStandardSpecular
                + " on specular setup is not " + ShaderInventory.MaterialTotal, failures);
            Expect(ShaderInventory.ShaderGraphCount == 0 && ShaderInventory.HlslIncludeCount == 3,
                "case3: the counted totals are " + ShaderInventory.ShaderGraphCount + " shader graphs and "
                + ShaderInventory.HlslIncludeCount + " HLSL includes; RM-68 left no graph and wrote exactly three "
                + "includes under Shaders/Include/, and every include is a file a URP upgrade can break", failures);

            int includes = 0;
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                if (e.kind != RenderAssetKind.HlslInclude) { continue; }
                includes++;
                Expect(e.asset.EndsWith(".hlsl", System.StringComparison.Ordinal),
                    "case3: include row '" + e.asset + "' does not name a .hlsl file", failures);
                Expect(e.target == PipelineTarget.Urp,
                    "case3: include row '" + e.asset + "' is not marked URP, and no Built-in include survived the port",
                    failures);
            }
            Expect(includes == ShaderInventory.HlslIncludeCount,
                "case3: " + includes + " include rows for a counted total of " + ShaderInventory.HlslIncludeCount,
                failures);

            int families = 0;
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                if (e.kind != RenderAssetKind.MaterialFamily) { continue; }
                families++;
                Expect(e.asset.StartsWith("Materials/", System.StringComparison.Ordinal),
                    "case3: family '" + e.asset + "' does not name a folder under Materials/", failures);
            }
            Expect(families >= 8, "case3: only " + families + " material families for " + accounted + " materials",
                failures);

            builder.Append("RCMSHADER case3 ").Append(families).Append(" families partition all ")
                   .Append(accounted).Append(" materials; ").Append(ShaderInventory.OnStandard).Append(" on fileID 46 + ")
                   .Append(ShaderInventory.OnStandardSpecular).Append(" on fileID 45 is the same total\n");
        }

        /// <summary> Case 4. All ten of the tool's own shaders are in the inventory, each declaring a shader NAME (what
        /// a material or a Shader.Find would ask for) and each present on disk. A shader missing from the table is a
        /// shader RM-68 will not port. </summary>
        private static void CaseAllTenShadersAreNamedAndOnDisk(StringBuilder builder, List<string> failures)
        {
            string root = ShaderInventoryWriter.ToolRoot();
            int shaders = 0, onDisk = 0;
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                if (e.kind != RenderAssetKind.Shader) { continue; }
                shaders++;
                Expect(e.count == 1, "case4: shader row '" + e.asset + "' claims " + e.count + " assets", failures);
                Expect(e.asset.EndsWith(".shader", System.StringComparison.Ordinal),
                    "case4: shader row '" + e.asset + "' does not name a .shader file", failures);
                Expect(e.targetEvidence.IndexOf("Shader \"", System.StringComparison.Ordinal) >= 0,
                    "case4: '" + e.asset + "' does not record the shader NAME it declares, which is what a material "
                    + "asks for", failures);

                string full = Path.Combine(root, e.asset.Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(full)) { onDisk++; }
                else { failures.Add("case4: '" + e.asset + "' is in the inventory but not on disk at '" + full + "'"); }
            }

            Expect(shaders == 10,
                "case4: " + shaders + " shader rows; the tool ships ten .shader files under Shaders/", failures);
            builder.Append("RCMSHADER case4 ").Append(shaders).Append(" shaders, ").Append(onDisk)
                   .Append(" of them on disk, each recording the name it declares\n");
        }

        /// <summary> Case 5. The lens flare is a render asset the inventory would otherwise miss, because it is neither
        /// a shader nor a material. It is also the one asset RM-68 could not convert in place: URP reads nothing from
        /// `Light.flare`, so there are two rows on disk -- the superseded Built-in `.flare` that nothing loads any more,
        /// and the `LensFlareDataSRP` `.asset` minted in its place. Both files must exist: the `.asset` because it is
        /// what draws, and the `.flare` because it is the record of every number the port carried across. </summary>
        private static void CaseTheFlareIsNamedAndOnDisk(StringBuilder builder, List<string> failures)
        {
            string root = ShaderInventoryWriter.ToolRoot();
            int flares = 0, builtInFlares = 0, urpFlares = 0;
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                if (e.kind != RenderAssetKind.LensFlare) { continue; }
                flares++;
                Expect(e.asset.EndsWith(".flare", System.StringComparison.Ordinal)
                    || e.asset.EndsWith(".asset", System.StringComparison.Ordinal),
                    "case5: flare row '" + e.asset + "' names neither the Built-in .flare nor the URP .asset", failures);
                Expect(e.portOwner.IndexOf("RM-68", System.StringComparison.Ordinal) >= 0,
                    "case5: '" + e.asset + "' does not point at RM-68, which did the flare port", failures);

                if (e.target == PipelineTarget.BuiltIn)
                {
                    builtInFlares++;
                    Expect(e.usedBy.StartsWith("nothing", System.StringComparison.Ordinal),
                        "case5: the Built-in flare '" + e.asset + "' still claims a user; URP draws nothing for "
                        + "`Light.flare`, so any code that still assigned it would be dead code", failures);
                }
                else
                {
                    urpFlares++;
                    Expect(e.targetEvidence.IndexOf("LensFlareDataSRP", System.StringComparison.Ordinal) >= 0,
                        "case5: the URP flare '" + e.asset + "' does not record the asset class URP actually reads",
                        failures);
                    Expect(CitesALocation(e.usedBy),
                        "case5: the URP flare '" + e.asset + "' does not cite the file that attaches it", failures);
                }

                string full = Path.Combine(root, e.asset.Replace('/', Path.DirectorySeparatorChar));
                Expect(File.Exists(full),
                    "case5: '" + e.asset + "' is in the inventory but not on disk at '" + full + "'", failures);
            }

            Expect(flares == 2, "case5: " + flares + " lens flare rows; the port leaves two, the superseded Built-in "
                + "asset and the URP asset that replaced it", failures);
            Expect(builtInFlares == 1 && urpFlares == 1,
                "case5: " + builtInFlares + " Built-in and " + urpFlares + " URP flare rows; one of each is what a "
                + "supersede-in-place-impossible port leaves behind", failures);
            builder.Append("RCMSHADER case5 both flare rows inventoried and on disk: the superseded .flare and the "
                + "LensFlareDataSRP that draws, both owned by RM-68\n");
        }

        /// <summary> Case 6. Every row says what reaches it, and says it in a checkable way: either it cites a file, or
        /// it states plainly that nothing does. "Used by: the roads" is the cell that makes an inventory useless.
        /// </summary>
        private static void CaseEveryUseIsCitedOrDeclaredUnreachable(StringBuilder builder, List<string> failures)
        {
            int cited = 0, unreachable = 0;
            foreach (RenderAssetEntry e in ShaderInventory.All())
            {
                bool declaresNothing = e.usedBy.StartsWith("nothing", System.StringComparison.Ordinal);
                bool citesAFile = CitesALocation(e.usedBy);
                Expect(declaresNothing || citesAFile,
                    "case6: '" + e.asset + "' neither cites a file nor states that nothing reaches it", failures);
                if (declaresNothing) { unreachable++; } else if (citesAFile) { cited++; }

                Expect(e.portOwner.IndexOf("RM-", System.StringComparison.Ordinal) >= 0,
                    "case6: '" + e.asset + "' names no RM task in its port cell", failures);
            }

            Expect(unreachable >= 1,
                "case6: nothing is recorded as unreachable, although none of the tool's ten shaders is referenced by "
                + "any material -- that finding is the port's whole shape", failures);
            Expect(cited >= 8,
                "case6: only " + cited + " rows cite a file; every material family is reached from somewhere nameable",
                failures);
            builder.Append("RCMSHADER case6 ").Append(cited).Append(" rows cite a file, ").Append(unreachable)
                   .Append(" state plainly that nothing reaches them\n");
        }

        /// <summary> Case 7. The missing-material list is the half of this audit that is falsifiable against the disk:
        /// every path in it must genuinely NOT resolve, and each must name the code that loads it. A null material
        /// renders magenta, which is the same symptom a Built-in shader gives under URP from a different cause -- so an
        /// entry that has quietly been fixed must leave this table rather than sit in it looking like a port bug.
        /// </summary>
        /// <summary> The four broken material references, checked as FIXED rather than as found. Each recorded path must
        /// now resolve on disk, and each authored material must be on URP's Lit shader rather than on the Built-in
        /// Standard fileID it would have inherited by being copied from a neighbour. The wrong-path one is checked at
        /// the library entry itself, because moving the asset was never the fix. </summary>
        private static void CaseMissingMaterialsAreReallyFixed(StringBuilder builder, List<string> failures)
        {
            string root = ShaderInventoryWriter.ToolRoot();
            IList<MissingMaterialReference> all = PipelineAudit.MissingReferences();
            Expect(all.Count >= 4,
                "case7: " + all.Count + " broken material references; four were found by the audit", failures);

            foreach (MissingMaterialReference m in all)
            {
                Expect(m.path.EndsWith(".mat", System.StringComparison.Ordinal),
                    "case7: '" + m.path + "' does not name a .mat", failures);
                Expect(CitesALocation(m.loadedBy),
                    "case7: '" + m.path + "' does not name the file that loads it", failures);
                Expect(m.note != null && m.note.Length > 30,
                    "case7: '" + m.path + "' does not say what was actually wrong", failures);
                Expect(m.fix != null && m.fix.Length > 40,
                    "case7: '" + m.path + "' does not say what was done about it", failures);
                Expect(m.path.IndexOf('|') < 0 && m.loadedBy.IndexOf('|') < 0 && m.note.IndexOf('|') < 0
                    && m.fix.IndexOf('|') < 0,
                    "case7: '" + m.path + "' has a pipe in one of its cells", failures);

                string full = Path.Combine(root, m.path.Replace('/', Path.DirectorySeparatorChar));
                Expect(File.Exists(full),
                    "case7: '" + m.path + "' is recorded as fixed but is NOT on disk at '" + full + "'", failures);
                if (File.Exists(full))
                {
                    string body = File.ReadAllText(full);
                    Expect(body.IndexOf(PipelineAudit.UrpLitShaderGuid, System.StringComparison.Ordinal) >= 0,
                        "case7: '" + m.path + "' does not reference URP's Lit shader ("
                        + PipelineAudit.UrpLitShaderGuid + "); a material this port authored or repaired may not be "
                        + "left on a Built-in shader", failures);
                    Expect(body.IndexOf(PipelineAudit.BuiltInResourceShaderGuid, System.StringComparison.Ordinal) < 0,
                        "case7: '" + m.path + "' still references a Built-in engine shader GUID", failures);
                }
            }

            string sidewalk = Path.Combine(root, Path.Combine("Editor", Path.Combine("Library",
                Path.Combine("ExtrudedObjects", "ESOSidewalk.rao"))));
            Expect(File.Exists(sidewalk), "case7: '" + sidewalk + "' is not on disk", failures);
            if (File.Exists(sidewalk))
            {
                string rao = File.ReadAllText(sidewalk);
                Expect(rao.IndexOf("Materials/Markers/RoadDetailOverlay1.mat", System.StringComparison.Ordinal) >= 0,
                    "case7: ESOSidewalk.rao does not name the sidewalk's second material at its real location under "
                    + "Materials/Markers/", failures);
                Expect(rao.IndexOf("Materials/RoadDetailOverlay1.mat", System.StringComparison.Ordinal) < 0,
                    "case7: ESOSidewalk.rao still names Materials/RoadDetailOverlay1.mat, which does not exist; the "
                    + "wrong path is back", failures);
            }

            builder.Append("RCMSHADER case7 ").Append(all.Count)
                   .Append(" broken material references, each naming its loader, its cause and its fix; every path now "
                   + "resolves and every one of them is on URP Lit\n");
        }

        /// <summary> The one typed GUID in this port, checked against the shader it claims to name. `PipelineAudit`
        /// records URP Lit's GUID as a literal because a gate that reads raw `.mat` text has no Shader object to ask;
        /// this case asks Unity for the shader BY NAME, takes the GUID of whatever it hands back, and requires the two
        /// to agree. A literal that goes stale when the package moves therefore fails loudly instead of quietly
        /// passing a check that can no longer match anything. </summary>
        private static void CaseUrpLitGuidStillResolves(StringBuilder builder, List<string> failures)
        {
            Shader lit = Shader.Find(PipelineAudit.UrpLitShaderName);
            Expect(lit != null,
                "case12: Shader.Find could not resolve '" + PipelineAudit.UrpLitShaderName + "'; URP's Lit shader is "
                + "the target every ported material is written against", failures);
            if (lit == null)
            {
                builder.Append("RCMSHADER case12 URP Lit did not resolve by name\n");
                return;
            }

            string path = AssetDatabase.GetAssetPath(lit);
            Expect(!string.IsNullOrEmpty(path),
                "case12: URP Lit resolved by name but has no asset path", failures);
            string guid = string.IsNullOrEmpty(path) ? "" : AssetDatabase.AssetPathToGUID(path);
            Expect(guid == PipelineAudit.UrpLitShaderGuid,
                "case12: URP Lit at '" + path + "' has GUID '" + guid + "', but PipelineAudit.UrpLitShaderGuid says '"
                + PipelineAudit.UrpLitShaderGuid + "'; the literal every ported .mat carries is stale", failures);

            Expect(PipelineAudit.BuiltInResourceShaderGuid != PipelineAudit.UrpLitShaderGuid,
                "case12: the Built-in and URP shader GUID constants are the same string", failures);

            builder.Append("RCMSHADER case12 URP Lit resolves by name to ").Append(path)
                   .Append(", GUID matches the recorded literal\n");
        }

        /// <summary> A source that names a line, not just a file. "GraphicsSettings.asset" is a gesture; ":49" is
        /// something a reader can open and disagree with. </summary>
        private static bool CitesALine(string source)
        {
            if (string.IsNullOrEmpty(source)) { return false; }
            for (int i = 0; i < source.Length - 1; i++)
            {
                if (source[i] == ':' && source[i + 1] >= '0' && source[i + 1] <= '9') { return true; }
            }
            return false;
        }

        /// <summary> Case 8. "URP" is not a specification (Q-20). The port targets THIS project's URP version, THIS
        /// pipeline asset and THIS stripping state, so every fact carries the file and line it was read from, and the
        /// three facts a port fails on when they are assumed -- the version, variant stripping, and which shadow
        /// features are even enabled -- must all be present. </summary>
        private static void CasePipelineFactsAreReadFromTheProject(StringBuilder builder, List<string> failures)
        {
            IList<PipelineFact> all = PipelineAudit.Facts();
            Expect(all.Count >= 16,
                "case8: " + all.Count + " pipeline facts; the audit read sixteen out of the project", failures);

            StringBuilder joined = new StringBuilder();
            foreach (PipelineFact f in all)
            {
                Expect(!string.IsNullOrEmpty(f.setting), "case8: a fact has no setting name", failures);
                Expect(!string.IsNullOrEmpty(f.value), "case8: '" + f.setting + "' has no value", failures);
                Expect(CitesALine(f.source),
                    "case8: '" + f.setting + "' cites no file and line; a pipeline fact without a source is an "
                    + "assumption in a table", failures);
                Expect(f.setting.IndexOf('|') < 0 && f.value.IndexOf('|') < 0 && f.source.IndexOf('|') < 0,
                    "case8: '" + f.setting + "' has a pipe in one of its cells", failures);
                joined.Append(f.setting).Append(' ').Append(f.value).Append(' ').Append(f.source).Append('\n');
            }

            string text = joined.ToString();
            string[] wanted = new string[]
            {
                PipelineAudit.TargetUrpVersion, "m_StripUnusedVariants", "m_AdditionalLightShadowsSupported",
                "m_RendererType", "m_UseSRPBatcher", "UC_URP_Pipeline.asset",
            };
            foreach (string phrase in wanted)
            {
                Expect(text.IndexOf(phrase, System.StringComparison.Ordinal) >= 0,
                    "case8: the facts never record '" + phrase + "'", failures);
            }

            builder.Append("RCMSHADER case8 ").Append(all.Count).Append(" pipeline facts, each with a file and line; ")
                   .Append("target URP ").Append(PipelineAudit.TargetUrpVersion)
                   .Append(", variant stripping and the shadow feature set all recorded\n");
        }

        /// <summary> Case 9. The four gotchas this project has already paid for once. They are in the register so RM-68
        /// starts from them instead of rediscovering them one compile error at a time -- and each records what the
        /// rediscovery COSTS, because a rule with no cost attached is one a hurried port skips. </summary>
        private static void CaseGotchasCoverTheFourPaidFor(StringBuilder builder, List<string> failures)
        {
            IList<PortGotcha> all = PipelineAudit.Gotchas();
            Expect(all.Count >= 4, "case9: " + all.Count + " gotchas; four were paid for", failures);

            StringBuilder joined = new StringBuilder();
            foreach (PortGotcha g in all)
            {
                Expect(g.rule != null && g.rule.Length > 40, "case9: a gotcha states no rule", failures);
                Expect(g.cost != null && g.cost.Length > 30,
                    "case9: '" + g.rule + "' does not say what rediscovering it costs", failures);
                Expect(g.rule.IndexOf('|') < 0 && g.cost.IndexOf('|') < 0,
                    "case9: a gotcha has a pipe in one of its cells", failures);
                joined.Append(g.rule).Append(' ').Append(g.cost).Append('\n');
            }

            string text = joined.ToString();
            string[] wanted = new string[] { "ENDHLSL", "CGPROGRAM", "Name \"\"", "ShadowCaster", "_ALPHATEST_ON" };
            foreach (string phrase in wanted)
            {
                Expect(text.IndexOf(phrase, System.StringComparison.Ordinal) >= 0,
                    "case9: the gotchas never mention '" + phrase + "'", failures);
            }

            builder.Append("RCMSHADER case9 ").Append(all.Count)
                   .Append(" gotchas: block terminator, empty pass name, custom ShadowCaster, cutout keyword plus "
                   + "surface state -- each with its cost\n");
        }

        /// <summary> Case 10. All four tables, one pipe count each. A row that loses a pipe merges two cells, which in
        /// this document silently attaches one asset's evidence to another asset's pipeline claim. </summary>
        private static void CaseTablesAreWellFormed(StringBuilder builder, List<string> failures)
        {
            CheckTable(builder, failures, "inventory", ShaderInventory.Table(), 8, ShaderInventory.All().Count);
            CheckTable(builder, failures, "pipeline facts", PipelineAudit.FactsTable(), 4,
                PipelineAudit.Facts().Count);
            CheckTable(builder, failures, "gotchas", PipelineAudit.GotchasTable(), 3, PipelineAudit.Gotchas().Count);
            CheckTable(builder, failures, "repaired materials", PipelineAudit.MissingTable(), 5,
                PipelineAudit.MissingReferences().Count);
        }

        private static void CheckTable(StringBuilder builder, List<string> failures, string name, string table,
            int pipesPerLine, int rows)
        {
            string[] lines = table.Split('\n');
            Expect(lines.Length == rows + 2,
                "case10: the " + name + " table has " + lines.Length + " lines for " + rows + " rows", failures);
            Expect(lines.Length > 1 && lines[1].StartsWith("| ---", System.StringComparison.Ordinal),
                "case10: the " + name + " table has no separator row", failures);

            for (int i = 0; i < lines.Length; i++)
            {
                int pipes = 0;
                foreach (char c in lines[i]) { if (c == '|') { pipes++; } }
                Expect(pipes == pipesPerLine,
                    "case10: " + name + " line " + (i + 1) + " has " + pipes + " pipes, not " + pipesPerLine, failures);
            }

            builder.Append("RCMSHADER case10 the ").Append(name).Append(" table is well formed (").Append(rows)
                   .Append(" rows, ").Append(pipesPerLine - 1).Append(" columns)\n");
        }

        /// <summary> Case 11. The document on disk holds what the four registers generate, and its hand-written half
        /// still states the port's target, the two magenta causes, and the sign-off bar. Read-only: the gate reports
        /// drift and names the menu item that fixes it. </summary>
        private static void CaseDocumentMatchesRegisters(StringBuilder builder, List<string> failures)
        {
            string path = ShaderInventoryWriter.DocPath();
            int matched = 0;
            for (int i = 0; i < ShaderInventoryWriter.TableCount; i++)
            {
                string onDisk, message;
                if (!ShaderInventoryWriter.ReadTableFrom(path, i, out onDisk, out message))
                {
                    failures.Add("case11: " + message);
                    continue;
                }

                bool matches = TerrainFeatureTableWriter.Normalise(onDisk)
                    == TerrainFeatureTableWriter.Normalise(ShaderInventoryWriter.WantedBody(i));
                if (matches) { matched++; continue; }
                failures.Add("case11: table " + i + " in '" + path + "' has drifted from its register; run Window > "
                    + "Road City Master > Documentation > Write the shader and material inventory");
            }

            string contents, readMessage;
            if (!ShaderInventoryWriter.ReadWholeDoc(path, out contents, out readMessage))
            {
                failures.Add("case11: " + readMessage);
                builder.Append("RCMSHADER case11 the inventory document could not be read\n");
                return;
            }

            string[] wanted = new string[]
            {
                PipelineAudit.TargetUrpVersion, "magenta", "fileID 46", "fileID 45", "RM-68", "RM-91",
                "nothing is marked unknown",
            };
            foreach (string phrase in wanted)
            {
                Expect(contents.IndexOf(phrase, System.StringComparison.Ordinal) >= 0,
                    "case11: the document never says '" + phrase + "'", failures);
            }

            builder.Append("RCMSHADER case11 ").Append(matched).Append(" of ")
                   .Append(ShaderInventoryWriter.TableCount)
                   .Append(" tables on disk match their registers, and the prose states the target version, both "
                   + "magenta causes and the sign-off bar\n");
        }

        /// <summary> One element of a `.rao` library that names an asset. The libraries are XML with one element per
        /// line, so a line-based read is exact here and needs no XML reader: an entry either has
        /// `&lt;Tag&gt;Assets/...&lt;/Tag&gt;` on one line or is the self-closing empty form, which carries no path.
        /// </summary>
        private static bool LibraryAssetPath(string line, out string tag, out string value)
        {
            tag = "";
            value = "";
            int open = line.IndexOf('<');
            if (open < 0) { return false; }
            int openEnd = line.IndexOf('>', open + 1);
            if (openEnd < 0) { return false; }
            int closeStart = line.IndexOf("</", openEnd + 1, System.StringComparison.Ordinal);
            if (closeStart < 0) { return false; }

            tag = line.Substring(open + 1, openEnd - open - 1);
            value = line.Substring(openEnd + 1, closeStart - openEnd - 1).Trim();
            return value.StartsWith("Assets/", System.StringComparison.Ordinal);
        }

        /// <summary> Case 13. The `.rao` libraries under `Editor/Library/` are the half of this port that no `.mat` edit
        /// can prove. They name prefabs, thumbnails and materials by PATH -- `EdgeObjectMaker.cs:433` and
        /// `SplinatedMeshMaker.cs:741` and `:808` all resolve them through
        /// `EngineIntegration.LoadAssetFromPath&lt;T&gt;`, which returns null for a path that no longer exists and drops
        /// that piece of the entry with no error anywhere. That is how `ESORailing5.rao` carried
        /// `Prefabs/Railing/Railing5Post.prefab` -- a folder named `Railings` on disk -- and lost both of its end caps
        /// silently; RM-68 fixed the path and this case is what stops it coming back.
        ///
        /// Two things are checked on every path: that it loads at all, and, for the ones naming a `.mat`, that the
        /// material it reaches is on URP's Lit shader. The second is the port's own reach test. A library entry pointing
        /// at an unconverted material renders magenta at the exact moment an author drags that entry onto a road, which
        /// is far from the port that caused it, so the check belongs here rather than in a screenshot.
        ///
        /// The shader is compared by NAME through the loaded material rather than by GUID against raw text, because a
        /// material that loads has a real `Shader` object to ask and asking it cannot go stale the way case 12's literal
        /// can. </summary>
        private static void CaseLibraryPathsResolveAndAreOnUrp(StringBuilder builder, List<string> failures)
        {
            string libraryRoot = Path.Combine(ShaderInventoryWriter.ToolRoot(),
                Path.Combine("Editor", "Library"));
            if (!Directory.Exists(libraryRoot))
            {
                failures.Add("case13: the library folder is not on disk at '" + libraryRoot + "'");
                builder.Append("RCMSHADER case13 the library folder could not be read\n");
                return;
            }

            string[] libraries = Directory.GetFiles(libraryRoot, "*.rao", SearchOption.AllDirectories);
            Expect(libraries.Length >= 110,
                "case13: " + libraries.Length + " .rao libraries under Editor/Library/; the tool ships 110 across "
                + "EdgeObjects, ExtrudedObjects and Groups/Default, and a missing file is a missing library entry",
                failures);

            int paths = 0, materials = 0, prefabs = 0, onUrp = 0;
            for (int f = 0; f < libraries.Length; f++)
            {
                string name = Path.GetFileName(libraries[f]);
                string[] lines = File.ReadAllLines(libraries[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string tag, value;
                    if (!LibraryAssetPath(lines[i], out tag, out value)) { continue; }
                    paths++;

                    UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(value);
                    if (asset == null)
                    {
                        failures.Add("case13: " + name + " line " + (i + 1) + " <" + tag + "> names '" + value
                            + "', which does not load; LoadAssetFromPath returns null and the library entry drops that "
                            + "piece with no error to show for it");
                        continue;
                    }

                    if (value.EndsWith(".prefab", System.StringComparison.Ordinal)) { prefabs++; }
                    if (!value.EndsWith(".mat", System.StringComparison.Ordinal)) { continue; }

                    materials++;
                    Material material = asset as Material;
                    if (material == null || material.shader == null)
                    {
                        failures.Add("case13: " + name + " line " + (i + 1) + " <" + tag + "> names '" + value
                            + "', which loads but is not a material with a shader");
                        continue;
                    }

                    if (material.shader.name == PipelineAudit.UrpLitShaderName) { onUrp++; continue; }
                    failures.Add("case13: " + name + " line " + (i + 1) + " <" + tag + "> reaches '" + value
                        + "', which is on shader '" + material.shader.name + "' rather than '"
                        + PipelineAudit.UrpLitShaderName + "'; RM-68 converted every material in the tree, and a "
                        + "library entry reaching an unconverted one renders magenta the moment it is dragged onto a "
                        + "road");
                }
            }

            builder.Append("RCMSHADER case13 ").Append(libraries.Length).Append(" .rao libraries, ").Append(paths)
                   .Append(" asset paths all resolving (").Append(prefabs).Append(" prefab references, ")
                   .Append(materials).Append(" material references, ").Append(onUrp)
                   .Append(" of them on URP Lit)\n");
        }
    }
}
#endif
