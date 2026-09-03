#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using RoadCityMaster.City;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-70's gate: the ship-audit sweeps § 7 defines, run over BOTH trees and reported per tree.
    ///
    /// The task asks for the host's sweeps to be "extended" to cover the renamed tool. Two facts decided how: sweeps 33
    /// through 39 are specification text with no implementation on either side, and R-5 forbids an RM-nn task from
    /// writing anything under the host's folder. So the sweeps are implemented HERE, parameterised by tree, and the
    /// host tree is walked read-only. The deliverable is unchanged -- results per folder, a failure naming which tree it
    /// came from -- and nothing in the forbidden tree is touched to get it.
    ///
    /// Sweep 40 was added afterwards and is not from § 7: it holds the tool's build path to the by-reference material
    /// spellings, after nineteen sites across seven files were found handing the author's material ASSETS to the
    /// instancing accessors. It is swept on the tool tree and delegated on the host's, for the reason stated where it
    /// is constructed.
    ///
    /// Two rules from the task are load-bearing and are asserted rather than assumed. A sweep that matched zero items
    /// FAILS, because a path that stops resolving after a rename otherwise reports a silent pass. And a deliberately
    /// introduced violation must be caught and NAMED, which the last three cases prove with synthetic inputs the
    /// scanners cannot tell from real ones -- an absolute path, a missing `.meta`, and a content entry pointing at
    /// something nobody validated.
    /// </summary>
    public static class ShipAuditGate
    {
        private const string Prefix = "RCMSWEEP";

        [MenuItem("Window/Road City Master/Testing/Ship audit sweeps over both trees", false, 2260)]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            if (passed) { Debug.Log(report); } else { Debug.LogError(report); }
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSWEEP BEGIN\n");

            List<SweepResult> results = ShipAuditSweeps.RunBothTrees();

            CaseTreeRootsResolve(builder, failures);
            CaseEverySweepIsPresentPerTree(builder, failures, results);
            CaseNoSweepMatchedNothing(builder, failures, results);
            CaseToolTreeIsClean(builder, failures, results);
            CaseHostTreeIsCleanOrDeferred(builder, failures, results);
            CaseDelegatedSweepsNameALiveOwner(builder, failures, results);
            CaseRoadChecksNameALiveOwner(builder, failures);
            CaseIntroducedAbsolutePathIsCaught(builder, failures);
            CaseIntroducedMetaBreakIsCaught(builder, failures);
            CaseIntroducedUnvalidatedContentIsRefused(builder, failures);

            builder.Append("RCMSWEEP per-tree report\n");
            builder.Append(ShipAuditSweeps.Report(results));

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMSWEEP FAIL  ").Append(failures[i]).Append('\n');
            }
            builder.Append("RCMSWEEP result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append('\n');
            builder.Append("RCMSWEEP END");
            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Case 1. Both folders the sweep claims to walk exist, and they are not the same folder. The second
        /// half matters because a broken anchor that silently collapsed both roots onto one path would otherwise show up
        /// as two identical clean reports. </summary>
        private static void CaseTreeRootsResolve(StringBuilder builder, List<string> failures)
        {
            string tool = ShipAuditSweeps.ToolTreeRoot();
            string host = ShipAuditSweeps.HostTreeRoot();
            Expect(Directory.Exists(tool), "roots: the tool tree does not exist at " + ShipAuditSweeps.TreeLabel(tool),
                failures);
            Expect(Directory.Exists(host), "roots: the host tree does not exist at " + ShipAuditSweeps.TreeLabel(host),
                failures);
            Expect(!string.Equals(tool, host, StringComparison.OrdinalIgnoreCase),
                "roots: both trees resolved to the same folder, so per-tree reporting means nothing", failures);
            builder.Append("RCMSWEEP case 1 roots  tool ").Append(ShipAuditSweeps.TreeLabel(tool))
                   .Append("  host ").Append(ShipAuditSweeps.TreeLabel(host)).Append('\n');
        }

        /// <summary> Case 2. Sweeps 33 to 40 all appear, for both trees, with no gap. A sweep that quietly stopped being
        /// constructed would otherwise pass by not existing. </summary>
        private static void CaseEverySweepIsPresentPerTree(StringBuilder builder, List<string> failures,
            List<SweepResult> results)
        {
            string[] trees = { ShipAuditSweeps.TreeLabel(ShipAuditSweeps.ToolTreeRoot()),
                               ShipAuditSweeps.TreeLabel(ShipAuditSweeps.HostTreeRoot()) };
            for (int t = 0; t < trees.Length; t++)
            {
                for (int id = 33; id <= 40; id++)
                {
                    bool found = false;
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (results[i].tree == trees[t] && results[i].id == id) { found = true; break; }
                    }
                    Expect(found, "coverage: sweep " + id + " produced no row for tree " + trees[t], failures);
                }
            }
            Expect(results.Count == 18, "coverage: expected nine rows per tree over two trees, got " + results.Count,
                failures);
            builder.Append("RCMSWEEP case 2 coverage  rows ").Append(results.Count).Append("  sweeps 33..40 x 2 trees\n");
        }

        /// <summary> Case 3. The rule the task spells out: a sweep that matched nothing is a FAILURE. It applies only to
        /// rows this walk actually performed -- a delegated row reports its owner instead of a count, and demanding a
        /// count from it would be demanding a number nobody here computed. </summary>
        private static void CaseNoSweepMatchedNothing(StringBuilder builder, List<string> failures,
            List<SweepResult> results)
        {
            int swept = 0;
            int delegated = 0;
            for (int i = 0; i < results.Count; i++)
            {
                SweepResult r = results[i];
                if (!r.Swept) { delegated++; continue; }
                swept++;
                Expect(r.itemsChecked > 0,
                    "zero-item: sweep " + r.id + " " + r.title + " on tree " + r.tree
                    + " checked nothing, so its path no longer resolves", failures);
                Expect(r.filesScanned > 0,
                    "zero-item: sweep " + r.id + " " + r.title + " on tree " + r.tree + " opened no files", failures);
            }
            Expect(swept == 13, "zero-item: expected thirteen swept rows across both trees, got " + swept, failures);
            Expect(delegated == 5, "zero-item: expected five delegated rows across both trees, got " + delegated,
                failures);
            builder.Append("RCMSWEEP case 3 zero-item  swept ").Append(swept)
                   .Append("  delegated ").Append(delegated).Append('\n');
        }

        /// <summary> Case 4. Zero findings in the tool tree. Deferrals are counted and printed but are not findings:
        /// each one names the reason its line stands, and a reason is what separates an exemption from an excuse. </summary>
        private static void CaseToolTreeIsClean(StringBuilder builder, List<string> failures,
            List<SweepResult> results)
        {
            string tree = ShipAuditSweeps.TreeLabel(ShipAuditSweeps.ToolTreeRoot());
            int found = 0;
            int allowed = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].tree != tree) { continue; }
                found += results[i].findings.Count;
                allowed += results[i].deferrals.Count;
                for (int f = 0; f < results[i].findings.Count; f++)
                {
                    failures.Add("tool tree: sweep " + results[i].id + " " + results[i].title + " -> "
                        + results[i].findings[f].ToString());
                }
            }
            Expect(found == 0, "tool tree: " + found + " sweep findings, listed above", failures);
            builder.Append("RCMSWEEP case 4 tool tree  found ").Append(found)
                   .Append("  allowed-with-reason ").Append(allowed).Append('\n');
        }

        /// <summary> Case 5. The host tree, walked read-only. Findings here are reported and named but R-5 forbids this
        /// module fixing them, so the assertion is the honest one: a host finding fails this gate ONLY when it is not
        /// already a recorded deferral. Today every host occurrence is one of the two prose lines the rename recorded,
        /// so the count is zero and the case is a real assertion rather than a formality. </summary>
        private static void CaseHostTreeIsCleanOrDeferred(StringBuilder builder, List<string> failures,
            List<SweepResult> results)
        {
            string tree = ShipAuditSweeps.TreeLabel(ShipAuditSweeps.HostTreeRoot());
            int found = 0;
            int allowed = 0;
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].tree != tree) { continue; }
                found += results[i].findings.Count;
                allowed += results[i].deferrals.Count;
                for (int f = 0; f < results[i].findings.Count; f++)
                {
                    failures.Add("host tree: sweep " + results[i].id + " " + results[i].title
                        + " is not a recorded deferral -> " + results[i].findings[f].ToString());
                }
            }
            Expect(found == 0, "host tree: " + found + " unrecorded findings, listed above", failures);
            Expect(allowed > 0,
                "host tree: the two R-5 deferrals the rename recorded were not seen, so either the files moved or the "
                + "sweep is no longer reaching them", failures);
            builder.Append("RCMSWEEP case 5 host tree  found ").Append(found)
                   .Append("  deferred-under-R5 ").Append(allowed).Append('\n');
        }

        /// <summary> Case 6. Every delegated row names a real owner. For the tool tree that owner is a gate in this
        /// assembly, and it must still be discoverable by the suite -- a delegated check whose owner fell out of the
        /// suite is worse than an unowned one, because the routing note makes it look covered. </summary>
        private static void CaseDelegatedSweepsNameALiveOwner(StringBuilder builder, List<string> failures,
            List<SweepResult> results)
        {
            int toolSideOwners = 0;
            for (int i = 0; i < results.Count; i++)
            {
                SweepResult r = results[i];
                if (r.Swept) { continue; }
                Expect(r.owner.Length > 0, "delegation: sweep " + r.id + " on " + r.tree + " named no owner", failures);
                if (r.owner == ShipAuditSweeps.HostOwnerName) { continue; }
                toolSideOwners++;
                Expect(OwnerGateExists(r.owner),
                    "delegation: sweep " + r.id + " on " + r.tree + " names owner " + r.owner
                    + ", which is not a gate this tool still reports and can still run", failures);
            }
            Expect(toolSideOwners == 2,
                "delegation: expected two tool-side delegated owners, got " + toolSideOwners, failures);
            builder.Append("RCMSWEEP case 6 delegation  tool-side owners checked ").Append(toolSideOwners).Append('\n');
        }

        /// <summary> Case 7. The road-specific checks RM-70 names -- RM-48's prefab contract, RM-54's hierarchy, RM-55's
        /// refusal guarantee, RM-56's per-category state and RM-69's GUID integrity -- each still have a live owner.
        /// This is why the sweep does not re-implement them: one fact, one owner. </summary>
        private static void CaseRoadChecksNameALiveOwner(StringBuilder builder, List<string> failures)
        {
            IList<string[]> checks = ShipAuditSweeps.RoadChecks();
            Expect(checks.Count == 5, "road checks: expected the five RM-70 names, got " + checks.Count, failures);
            for (int i = 0; i < checks.Count; i++)
            {
                Expect(OwnerGateExists(checks[i][2]),
                    "road checks: " + checks[i][0] + " (" + checks[i][1] + ") names owner " + checks[i][2]
                    + ", which is not a discoverable gate", failures);
            }
            builder.Append("RCMSWEEP case 7 road checks  routed ").Append(checks.Count).Append('\n');
        }

        /// <summary> Case 8. The first of the three negative controls RM-70 asks for: an absolute path introduced on
        /// purpose is caught and NAMED. It is fed as text rather than written to a file, so the control cannot leave a
        /// violation behind if the gate throws halfway through.
        ///
        /// Five lines, chosen so the case fails if the scanner gets either loose or strict: a comment naming a drive
        /// letter must NOT be reported, a verbatim path and an escaped profile path must BOTH be, a project-relative
        /// path must not, and a URL must not -- that last one is the false positive a naive drive-letter test produces
        /// on every `http://` in the tree. </summary>
        private static void CaseIntroducedAbsolutePathIsCaught(StringBuilder builder, List<string> failures)
        {
            const string drive = "C";
            // Split for the same reason `drive` is a variable: sweep 35 reads THIS file as well, and a control that
            // spells its own needle out whole reports the control itself as a breach. The sweep sees the assembled
            // string at run time, which is what is being measured; the source line never carries it.
            const string profile = "Us" + "ers";
            string synthetic =
                "// a comment naming " + drive + ":/somewhere is documentation, not a breach\n"
                + "        string a = @\"" + drive + ":/Content/road.asset\";\n"
                + "        string b = \"" + drive + ":\\\\" + profile + "\\\\someone\\\\road.asset\";\n"
                + "        string c = Normalise(\"Assets/RoadCityMaster/Prefabs/Kerb.prefab\");\n"
                + "        string d = \"http://example.invalid/x\";\n";

            SweepResult probe = new SweepResult(35, "absolute-path", "negative-control", string.Empty);
            ShipAuditSweeps.ScanAbsolutePath("Negative/Control.cs", synthetic, probe);

            Expect(probe.findings.Count == 2,
                "control: an introduced absolute path was not caught exactly twice, got " + probe.findings.Count
                + " findings", failures);
            bool named = false;
            for (int i = 0; i < probe.findings.Count; i++)
            {
                if (probe.findings[i].line == 2 && probe.findings[i].file == "Negative/Control.cs") { named = true; }
            }
            Expect(named, "control: the caught absolute path did not name its file and line", failures);
            Expect(!probe.Passed, "control: a sweep with findings reported itself as passed", failures);
            builder.Append("RCMSWEEP case 8 control absolute-path  caught ").Append(probe.findings.Count)
                   .Append(" of 2 planted, 3 innocents left alone\n");
        }

        /// <summary> Case 9. The second control: a broken `.meta` pairing is caught, both ways round. The probe folder's
        /// name begins with a dot, so Unity does not import it and the tree's real asset database is never touched; it
        /// is removed in a `finally` so a throw cannot leave it behind. </summary>
        private static void CaseIntroducedMetaBreakIsCaught(StringBuilder builder, List<string> failures)
        {
            string probeRoot = Path.Combine(ShipAuditSweeps.ToolTreeRoot(), ".rcmsweepcontrol");
            int found = 0;
            bool sawMissing = false;
            bool sawOrphan = false;
            try
            {
                if (Directory.Exists(probeRoot)) { Directory.Delete(probeRoot, true); }
                Directory.CreateDirectory(probeRoot);
                File.WriteAllText(Path.Combine(probeRoot, "Paired.txt"), "an asset that has its identity");
                File.WriteAllText(Path.Combine(probeRoot, "Paired.txt.meta"), "guid: not a real guid");
                File.WriteAllText(Path.Combine(probeRoot, "Unpaired.txt"), "an asset with no identity yet");
                File.WriteAllText(Path.Combine(probeRoot, "Vanished.txt.meta"), "guid: an identity with no asset");

                SweepResult probe = new SweepResult(37, "meta-pairing", "negative-control", string.Empty);
                ShipAuditSweeps.MetaPairing(probeRoot, probe);
                found = probe.findings.Count;
                for (int i = 0; i < probe.findings.Count; i++)
                {
                    string reason = probe.findings[i].reason;
                    if (probe.findings[i].file.EndsWith("Unpaired.txt", StringComparison.Ordinal)
                        && reason.IndexOf("no .meta", StringComparison.Ordinal) >= 0) { sawMissing = true; }
                    if (probe.findings[i].file.EndsWith("Vanished.txt.meta", StringComparison.Ordinal)
                        && reason.IndexOf("orphan", StringComparison.Ordinal) >= 0) { sawOrphan = true; }
                }
            }
            catch (IOException error)
            {
                failures.Add("control: the meta-pairing control could not run: " + error.Message);
            }
            finally
            {
                try { if (Directory.Exists(probeRoot)) { Directory.Delete(probeRoot, true); } }
                catch (IOException) { }
            }

            Expect(sawMissing, "control: an asset with no `.meta` was not caught and named", failures);
            Expect(sawOrphan, "control: an orphaned `.meta` was not caught and named", failures);
            Expect(found == 2, "control: the meta control planted two breaks and the sweep found " + found, failures);
            Expect(!Directory.Exists(probeRoot), "control: the meta control left its probe folder behind", failures);
            builder.Append("RCMSWEEP case 9 control meta-pairing  caught ").Append(found)
                   .Append(" of 2 planted, probe folder removed\n");
        }

        /// <summary> Case 10. The third control, and the one the task words as "a null prefab reference in a category
        /// set". A category entry holds asset PATHS rather than object references, so the equivalent breach is an entry
        /// naming content nobody has validated -- which is also what a dead path degrades into. RM-55's resolver must
        /// REFUSE it, name the category and name the requester; a resolver that placed it would be substituting content
        /// silently, which is the one behaviour that layer exists to prevent.
        ///
        /// This is a routing check as much as a control: it proves the owner named in case 7 actually behaves the way
        /// the routing note claims, without this file re-implementing any of it. </summary>
        private static void CaseIntroducedUnvalidatedContentIsRefused(StringBuilder builder, List<string> failures)
        {
            const string category = "StreetTree";
            const string requester = "RCMSWEEP negative control";

            CategoryContentSet set = ScriptableObject.CreateInstance<CategoryContentSet>();
            try
            {
                CategoryContentEntry entry = CategoryContentEntry.Empty(category);
                entry.assetPaths = new string[] { "Assets/RoadCityMaster/Prefabs/NothingValidatedThis.prefab" };
                set.SetEntry(entry);

                ContentValidationCache cache = new ContentValidationCache();
                ContentCatalog catalog = set.BuildCatalog(cache);
                ContentResolution resolution = ContentResolver.Resolve(catalog, category, requester, 1UL);

                Expect(!resolution.resolved,
                    "control: an unvalidated content entry resolved instead of being refused", failures);
                Expect(resolution.refusal.reason == ContentRefusalReason.FailedValidation,
                    "control: the refusal named reason " + resolution.refusal.reason
                    + " rather than a validation failure", failures);
                Expect(resolution.refusal.category == category && resolution.refusal.requester == requester,
                    "control: the refusal did not name both the category and the requester", failures);

                string described = resolution.refusal.Describe();
                Expect(described.IndexOf(category, StringComparison.Ordinal) >= 0,
                    "control: the refusal's own description does not name the category", failures);
                builder.Append("RCMSWEEP case 10 control content  refused: ").Append(described).Append('\n');
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(set);
            }
        }


        /// actually reports that prefix, and the type named by that file is discoverable the way the suite discovers
        /// gates. Both halves are needed. The signature alone would accept a gate that stopped reporting the prefix the
        /// routing note cites; the prefix alone would accept a file that no longer compiles into a runnable gate.
        ///
        /// The prefix is read from source text rather than from a constant because the gates in this tool write their
        /// prefix inline -- `builder.Append("RCMPREFAB BEGIN\n")` -- so a constant to reflect over mostly does not
        /// exist. Reading the line the gate actually emits is the stronger check anyway. </summary>
        private static bool OwnerGateExists(string prefix)
        {
            string typeName = TypeReportingPrefix(prefix);
            if (typeName == null) { return false; }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int a = 0; a < assemblies.Length; a++)
            {
                string assemblyName = assemblies[a].GetName().Name;
                if (assemblyName != "RoadCityMaster" && assemblyName != "RoadCityMaster.Editor") { continue; }

                Type[] types;
                try { types = assemblies[a].GetTypes(); }
                catch (ReflectionTypeLoadException error) { types = error.Types; }

                for (int t = 0; t < types.Length; t++)
                {
                    if (types[t] == null || types[t].Name != typeName) { continue; }
                    MethodInfo run = types[t].GetMethod("Run", BindingFlags.Public | BindingFlags.Static,
                        null, new Type[] { typeof(string).MakeByRefType() }, null);
                    if (run != null && run.ReturnType == typeof(bool)) { return true; }
                }
            }
            return false;
        }

        /// <summary> The type name of the file that emits `<prefix> BEGIN`, or null when no file does. The file name is
        /// the type name by this tool's convention, which every gate follows. </summary>
        private static string TypeReportingPrefix(string prefix)
        {
            string needle = "\"" + prefix + " BEGIN";
            List<string> sources = ShipAuditSweeps.SourceFiles(ShipAuditSweeps.ToolTreeRoot());
            for (int i = 0; i < sources.Count; i++)
            {
                string text;
                try { text = File.ReadAllText(sources[i]); }
                catch (IOException) { continue; }
                if (text.IndexOf(needle, StringComparison.Ordinal) < 0) { continue; }
                return Path.GetFileNameWithoutExtension(sources[i]);
            }
            return null;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
