#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-69's gate: proves the asset-identity procedure held. Six cases. The register itself is well formed
    /// (every action kind carries a rule, and every typed GUID carries its argument); the tool still finds its own
    /// folder through the asmdef GUID, and the one script allowed to type that GUID still contains it; every asset
    /// under the tool has a `.meta` and every `.meta` has its asset, so nothing was moved by half; no two assets claim
    /// the same GUID; every GUID-shaped literal in the tool's own scripts is one the register sanctions; and no
    /// `guid:` reference inside the tool's serialised assets resolves to nothing, which is what a broken move or a
    /// recreated `.meta` leaves behind.
    ///
    /// The last case is the one that matters after a rename: a dead reference is invisible in the file system and only
    /// speaks up at import, so reading the files is the only way to find it before whoever opens the scene does.
    /// Report lines are prefixed RCMMETA. </summary>
    public static class AssetIdentityGate
    {
        /// <summary> Engine sentinels: `unity default resources`, `unity_builtin_extra` and their kin. They are real
        /// identities the editor resolves internally rather than assets in any project, so a reference sweep has to
        /// let them through or it reports the engine itself as missing. </summary>
        private static bool IsEngineSentinel(string guid)
        {
            if (guid.Length != 32) { return false; }
            for (int i = 0; i < 32; i++)
            {
                if (i == 16) { continue; }
                if (guid[i] != '0') { return false; }
            }
            return true;
        }

        [MenuItem("Window/Road City Master/Testing/Asset identity and GUID discipline")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.AssetIdentityGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMMETA BEGIN\n");
            List<string> failures = new List<string>();

            CaseRegisterIsWellFormed(builder, failures);
            CaseSelfLocationStillResolves(builder, failures);
            CaseEveryAssetPairsWithItsMeta(builder, failures);
            CaseNoDuplicateGuid(builder, failures);
            CaseOnlySanctionedLiterals(builder, failures);
            CaseNoReferenceResolvesToNothing(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMMETA FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMMETA result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMMETA END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }

        /// <summary> A path Unity ignores. RM-70's sweep needed the same answer, and it needed one more class of path
        /// than this gate did -- build output such as `obj/` and `Temp/` -- so the one implementation lives there and
        /// this gate calls it rather than keeping a second, narrower copy that would slowly stop agreeing. </summary>
        private static bool IsHiddenFromUnity(string fullPath, string root)
        {
            return ShipAuditSweeps.IsHiddenFromUnity(fullPath, root);
        }

        private static string ToolRoot()
        {
            return ShaderInventoryWriter.ToolRoot();
        }

        private static bool IsGuidShaped(string candidate)
        {
            if (candidate.Length != 32) { return false; }
            for (int i = 0; i < 32; i++)
            {
                char c = candidate[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) { return false; }
            }
            return true;
        }

        /// <summary> The register has to be complete before anything can be checked against it: a procedure missing the
        /// action someone is about to take is worse than no procedure, because it reads as permission. </summary>
        private static void CaseRegisterIsWellFormed(StringBuilder builder, List<string> failures)
        {
            IList<IdentityRule> rules = AssetIdentityRegister.Rules();
            Expect(rules.Count >= 10, "register: fewer than the ten rules the procedure needs", failures);

            IdentityAction[] kinds = (IdentityAction[])System.Enum.GetValues(typeof(IdentityAction));
            for (int k = 0; k < kinds.Length; k++)
            {
                bool covered = false;
                for (int i = 0; i < rules.Count; i++)
                {
                    if (rules[i].action == kinds[k]) { covered = true; break; }
                }
                Expect(covered, "register: no rule covers " + AssetIdentityRegister.ActionText(kinds[k]), failures);
            }

            for (int i = 0; i < rules.Count; i++)
            {
                string id = "register: rule " + i + " (" + AssetIdentityRegister.ActionText(rules[i].action) + ")";
                Expect(!string.IsNullOrEmpty(rules[i].rule), id + " has no rule text", failures);
                Expect(!string.IsNullOrEmpty(rules[i].why), id + " does not say why the engine requires it", failures);
                Expect(!string.IsNullOrEmpty(rules[i].cost), id + " does not say what getting it wrong costs", failures);
                Expect(rules[i].rule.IndexOf('|') < 0 && rules[i].why.IndexOf('|') < 0
                       && rules[i].cost.IndexOf('|') < 0, id + " contains a pipe, which breaks its table row", failures);
            }

            IList<SanctionedGuid> guids = AssetIdentityRegister.SanctionedGuids();
            Expect(guids.Count >= 7, "register: fewer sanctioned GUIDs than the source is known to contain", failures);
            for (int i = 0; i < guids.Count; i++)
            {
                Expect(IsGuidShaped(guids[i].guid), "register: sanctioned entry " + i + " is not a 32-digit lowercase "
                       + "hex GUID", failures);
                Expect(!string.IsNullOrEmpty(guids[i].names), "register: " + guids[i].guid + " does not name what it "
                       + "points at", failures);
                Expect(!string.IsNullOrEmpty(guids[i].typedIn), "register: " + guids[i].guid + " does not say where it "
                       + "is typed", failures);
                Expect(!string.IsNullOrEmpty(guids[i].whyTyped), "register: " + guids[i].guid + " does not argue why a "
                       + "literal rather than a reference", failures);
            }

            builder.Append("RCMMETA ").Append("register-shape".PadRight(22))
                   .Append(rules.Count).Append(" rules across all ").Append(kinds.Length)
                   .Append(" action kinds, ").Append(guids.Count).Append(" sanctioned GUIDs each argued\n");
        }

        /// <summary> The one lookup upstream of every asset the tool loads. If the asmdef's `.meta` was ever recreated
        /// this is the case that says so, and it says it by asking the same question `RoadEditorUtility` asks. </summary>
        private static void CaseSelfLocationStillResolves(StringBuilder builder, List<string> failures)
        {
            string path = AssetDatabase.GUIDToAssetPath(AssetIdentityRegister.AsmdefGuid);
            Expect(!string.IsNullOrEmpty(path), "self-location: the asmdef GUID resolves to no path at all, so the "
                   + "asmdef's .meta was recreated rather than carried", failures);
            Expect(path != null && path.EndsWith(AssetIdentityRegister.AsmdefRelativePath,
                   System.StringComparison.Ordinal), "self-location: the asmdef GUID resolves to '" + path
                   + "', which does not end in " + AssetIdentityRegister.AsmdefRelativePath, failures);

            string selfLocation = Path.Combine(ToolRoot(), AssetIdentityRegister.SelfLocationFile.Replace('/',
                                               Path.DirectorySeparatorChar));
            bool exists = File.Exists(selfLocation);
            Expect(exists, "self-location: " + AssetIdentityRegister.SelfLocationFile + " is missing, so the folder "
                   + "lookup it owns cannot be checked", failures);
            if (exists)
            {
                string source = File.ReadAllText(selfLocation);
                Expect(source.Contains(AssetIdentityRegister.AsmdefGuid), "self-location: "
                       + AssetIdentityRegister.SelfLocationFile + " no longer contains the asmdef GUID it looks up, so "
                       + "the register and the code have drifted apart", failures);
            }

            builder.Append("RCMMETA ").Append("self-location".PadRight(22))
                   .Append("asmdef GUID resolves to ").Append(string.IsNullOrEmpty(path) ? "<nothing>" : path)
                   .Append(", still typed in ").Append(AssetIdentityRegister.SelfLocationFile).Append("\n");
        }

        /// <summary> Both halves of every asset, or neither. A file with no `.meta` is an asset whose identity the next
        /// import will invent; an orphaned `.meta` is an identity waiting to be handed to whatever file lands on that
        /// path next, which is the failure that reads as data corruption rather than as a bad delete.
        ///
        /// The walk itself lives in RM-70's <see cref="ShipAuditSweeps.MetaPairing"/>, which is the same pairing sweep
        /// 37 performs over both trees. This gate had its own copy first; two copies of a rule is how two answers about
        /// it start to disagree, so the copy was removed rather than kept in step by hand. What stays here is this
        /// gate's own reading of the result -- and the folder half, which RM-70's version added: a folder with no
        /// `.meta` is the same broken identity as a file with none. </summary>
        private static void CaseEveryAssetPairsWithItsMeta(StringBuilder builder, List<string> failures)
        {
            SweepResult pairing = new SweepResult(37, "meta-pairing", "RoadCityMaster", string.Empty);
            ShipAuditSweeps.MetaPairing(ToolRoot(), pairing);

            List<string> named = new List<string>();
            for (int i = 0; i < pairing.findings.Count && named.Count < 8; i++)
            {
                named.Add(pairing.findings[i].file + " (" + pairing.findings[i].reason + ")");
            }

            Expect(pairing.findings.Count == 0, "pairing: " + pairing.findings.Count
                   + " broken asset/.meta pairings, first few: " + string.Join(", ", named.ToArray()), failures);
            Expect(pairing.itemsChecked > 0,
                   "pairing: the sweep examined nothing, so the tool root no longer resolves", failures);

            builder.Append("RCMMETA ").Append("meta-pairing".PadRight(22))
                   .Append(pairing.filesScanned).Append(" assets of ").Append(pairing.itemsChecked)
                   .Append(" files and folders examined, ").Append(pairing.findings.Count)
                   .Append(" broken pairings\n");
        }

        /// <summary> Reads the identity a `.meta` claims. Returns "" when the file carries no `guid:` line at all,
        /// which is itself a finding rather than something to guess a value for. </summary>
        private static string MetaGuid(string metaPath)
        {
            string[] lines = File.ReadAllLines(metaPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (!line.StartsWith("guid:", System.StringComparison.Ordinal)) { continue; }
                return line.Substring(5).Trim();
            }
            return "";
        }

        /// <summary> Two assets claiming one identity is what hand-authoring a `.meta` produces, and the editor
        /// resolves such a collision by picking one -- so the loser's references silently move to the winner. </summary>
        private static void CaseNoDuplicateGuid(StringBuilder builder, List<string> failures)
        {
            string root = ToolRoot();
            string[] all = Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories);
            Dictionary<string, string> owner = new Dictionary<string, string>();
            List<string> collisions = new List<string>();
            List<string> unnamed = new List<string>();
            int counted = 0;

            for (int i = 0; i < all.Length; i++)
            {
                if (IsHiddenFromUnity(all[i], root)) { continue; }
                string guid = MetaGuid(all[i]);
                if (guid.Length == 0)
                {
                    if (unnamed.Count < 5) { unnamed.Add(Path.GetFileName(all[i])); }
                    continue;
                }
                counted++;
                if (owner.ContainsKey(guid))
                {
                    if (collisions.Count < 5)
                    {
                        collisions.Add(guid + " claimed by both " + Path.GetFileName(owner[guid]) + " and "
                                       + Path.GetFileName(all[i]));
                    }
                    continue;
                }
                owner.Add(guid, all[i]);
            }

            Expect(unnamed.Count == 0, "duplicates: .meta files carrying no guid line: " + string.Join(", ",
                   unnamed.ToArray()), failures);
            Expect(collisions.Count == 0, "duplicates: " + string.Join("; ", collisions.ToArray()), failures);

            builder.Append("RCMMETA ").Append("no-duplicate-guid".PadRight(22))
                   .Append(counted).Append(" metas, ").Append(owner.Count).Append(" distinct GUIDs\n");
        }

        /// <summary> Collects every maximal run of 32 lowercase hex digits in a source file. A longer run is not a GUID
        /// and a shorter one is a word, so the run has to be exactly 32 with non-hex on both sides. </summary>
        private static void CollectGuidLiterals(string source, List<string> into)
        {
            int i = 0;
            while (i < source.Length)
            {
                char c = source[i];
                bool hex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!hex) { i++; continue; }
                int start = i;
                while (i < source.Length)
                {
                    char d = source[i];
                    if ((d >= '0' && d <= '9') || (d >= 'a' && d <= 'f')) { i++; continue; }
                    break;
                }
                if (i - start == 32) { into.Add(source.Substring(start, 32)); }
            }
        }

        /// <summary> Unity mints GUIDs; a script may quote one but never invent one. This case is what makes that a rule
        /// rather than an intention: any GUID-shaped literal the register has not argued for is a hand-authored
        /// identity until someone says otherwise in the register. </summary>
        private static void CaseOnlySanctionedLiterals(StringBuilder builder, List<string> failures)
        {
            string scripts = Path.Combine(ToolRoot(), "Scripts");
            HashSet<string> sanctioned = new HashSet<string>();
            IList<SanctionedGuid> guids = AssetIdentityRegister.SanctionedGuids();
            for (int i = 0; i < guids.Count; i++) { sanctioned.Add(guids[i].guid); }

            string[] sources = Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories);
            List<string> unsanctioned = new List<string>();
            int found = 0;

            for (int i = 0; i < sources.Length; i++)
            {
                List<string> literals = new List<string>();
                CollectGuidLiterals(File.ReadAllText(sources[i]), literals);
                for (int j = 0; j < literals.Count; j++)
                {
                    found++;
                    if (sanctioned.Contains(literals[j])) { continue; }
                    if (unsanctioned.Count < 5)
                    {
                        unsanctioned.Add(literals[j] + " in " + Path.GetFileName(sources[i]));
                    }
                }
            }

            Expect(unsanctioned.Count == 0, "literals: GUIDs typed into source that the register does not sanction: "
                   + string.Join(", ", unsanctioned.ToArray()), failures);

            builder.Append("RCMMETA ").Append("sanctioned-literals".PadRight(22))
                   .Append(found).Append(" GUID literals across ").Append(sources.Length)
                   .Append(" scripts, all ").Append(sanctioned.Count).Append(" sanctioned and argued\n");
        }

        /// <summary> The YAML-serialised asset kinds under this tool: the files that hold `guid:` references to other
        /// assets. Binary imports (FBX, PNG) hold their references in their `.meta`, and an extension missing from this
        /// set weakens the sweep rather than breaking it. </summary>
        private static bool IsSerialisedAsset(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".mat":
                case ".prefab":
                case ".asset":
                case ".unity":
                case ".controller":
                case ".anim":
                case ".flare":
                case ".shadergraph":
                case ".shadersubgraph":
                case ".terrainlayer":
                case ".physicmaterial":
                case ".physicsmaterial":
                case ".preset":
                case ".overridecontroller":
                case ".spriteatlas":
                case ".guiskin":
                case ".rendertexture":
                case ".cubemap":
                case ".mixer":
                case ".playable":
                    return true;
                default:
                    return false;
            }
        }

        /// <summary> The case that finds a broken move. Every `guid:` reference in the tool's serialised assets is asked
        /// of the AssetDatabase, which resolves package GUIDs and engine sentinels the way the running editor does --
        /// an offline text sweep cannot, and reports the packages as missing. A GUID that resolves to no path is a
        /// reference to an asset that is gone: silent in the file system, loud only at load. </summary>
        private static void CaseNoReferenceResolvesToNothing(StringBuilder builder, List<string> failures)
        {
            string root = ToolRoot();
            string[] all = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            Dictionary<string, string> firstSeenIn = new Dictionary<string, string>();
            int references = 0;
            int files = 0;

            for (int i = 0; i < all.Length; i++)
            {
                string file = all[i];
                if (IsHiddenFromUnity(file, root)) { continue; }
                if (!IsSerialisedAsset(file)) { continue; }
                files++;

                string[] lines = File.ReadAllLines(file);
                for (int l = 0; l < lines.Length; l++)
                {
                    int at = lines[l].IndexOf("guid: ", System.StringComparison.Ordinal);
                    while (at >= 0)
                    {
                        int start = at + 6;
                        if (start + 32 <= lines[l].Length)
                        {
                            string guid = lines[l].Substring(start, 32);
                            if (IsGuidShaped(guid))
                            {
                                references++;
                                if (!firstSeenIn.ContainsKey(guid))
                                {
                                    firstSeenIn.Add(guid, Path.GetFileName(file) + ":" + (l + 1));
                                }
                            }
                        }
                        at = lines[l].IndexOf("guid: ", start, System.StringComparison.Ordinal);
                    }
                }
            }

            List<string> dead = new List<string>();
            int resolved = 0;
            int sentinels = 0;
            foreach (KeyValuePair<string, string> pair in firstSeenIn)
            {
                if (IsEngineSentinel(pair.Key)) { sentinels++; continue; }
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(pair.Key))) { resolved++; continue; }
                if (dead.Count < 8) { dead.Add(pair.Key + " first seen at " + pair.Value); }
            }

            Expect(dead.Count == 0, "references: GUIDs that resolve to no asset, so a move or delete left them behind: "
                   + string.Join("; ", dead.ToArray()), failures);

            builder.Append("RCMMETA ").Append("no-dead-reference".PadRight(22))
                   .Append(references).Append(" references across ").Append(files).Append(" serialised assets, ")
                   .Append(resolved).Append(" distinct GUIDs resolve, ").Append(sentinels)
                   .Append(" engine sentinels skipped\n");
        }
    }
}
#endif
