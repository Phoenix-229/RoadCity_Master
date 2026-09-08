// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-79's headless proof of the licensing position: every source file in the tool tree resolves to
    /// exactly one licence, and the rule that decides it is pure, total and deterministic. The classification rule
    /// and the two header templates are proven against hand cases; then the whole real tree is enumerated and every
    /// `.cs` is shown to classify to exactly one of {UpstreamMit, ProprietaryNew} with the partition non-empty on
    /// both sides. Header COVERAGE on disk is reported, not asserted -- writing the headers is RM-80's job and
    /// enforcing their presence is RM-70's, so this gate stays green while that work is still owed. </summary>
    public static class CityLicenceGate
    {
        private const string Prefix = "RCMLICENCE";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Licence resolution")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
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
            _cases = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CasePathRuleIsTotalAndCorrect(failures);
            CaseHeaderStatesReadCorrectly(failures);
            CaseHeaderIsAuthoritativeOverPath(failures);
            CaseExpectedAnchorRoundTrips(failures);
            CaseResolveIsDeterministic(failures);
            CaseEveryFileInTreeResolvesToOneLicence(failures, sb);

            for (int i = 0; i < failures.Count; i++) { sb.AppendLine(Prefix + " FAIL   " + failures[i]); }
            sb.AppendLine(Prefix + " result " + (failures.Count == 0 ? "PASS" : "FAIL") + "  cases " + _cases
                + "  failures " + failures.Count);
            sb.Append(Prefix + " END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(message); }
        }

        /// <summary> The provenance rule returns exactly one licence for every path: the frozen vendor filename is MIT,
        /// every other source is project-authored, and an empty path never throws. </summary>
        private static void CasePathRuleIsTotalAndCorrect(List<string> failures)
        {
            Expect(LicenceRule.ClassifyByPath("City/Runtime/Data/PrefabManifest.cs") == SourceLicence.ProprietaryNew,
                "a City/ file must classify proprietary.", failures);
            Expect(LicenceRule.ClassifyByPath("city/editor/foo.cs") == SourceLicence.ProprietaryNew,
                "the City segment test must be case-insensitive.", failures);
            Expect(LicenceRule.ClassifyByPath("City") == SourceLicence.ProprietaryNew,
                "the bare City folder must classify proprietary.", failures);
            Expect(LicenceRule.ClassifyByPath("Scripts/Road.cs") == SourceLicence.UpstreamMit,
                "a frozen vendor Scripts/ file must classify upstream MIT by provenance.", failures);
            Expect(LicenceRule.ClassifyByPath("README.md") == SourceLicence.ProprietaryNew,
                "an unlisted root file must classify as project-authored.", failures);
            Expect(LicenceRule.ClassifyByPath("") == SourceLicence.ProprietaryNew,
                "an empty path must resolve, not throw.", failures);
            Expect(LicenceRule.ClassifyByPath("Scripts/Cityscape/thing.cs") == SourceLicence.ProprietaryNew,
                "an unlisted Scripts/ file must classify as project-authored.", failures);
        }

        /// <summary> The header reader tells the four states apart from the two RM-79 anchor phrases: proprietary,
        /// MIT-modified, both (Conflicting), and neither (Missing). </summary>
        private static void CaseHeaderStatesReadCorrectly(List<string> failures)
        {
            string prop = "// New file, authored for this project -- proprietary\n// see LICENSE.md.\n";
            string mit = "// Upstream file, MIT licensed -- see RoadCityMaster/LICENSE-MIT.txt\n"
                + "// not superseded by LICENSE.md.\n";
            Expect(LicenceRule.HeaderStateOf(prop) == LicenceHeaderState.ProprietaryOnly,
                "the proprietary header must read as ProprietaryOnly.", failures);
            Expect(LicenceRule.HeaderStateOf(mit) == LicenceHeaderState.MitModifiedOnly,
                "the MIT-modified header must read as MitModifiedOnly even though it also mentions LICENSE.md.",
                failures);
            Expect(LicenceRule.HeaderStateOf(prop + mit) == LicenceHeaderState.Conflicting,
                "a file carrying both headers must read as Conflicting.", failures);
            Expect(LicenceRule.HeaderStateOf("using System;\n") == LicenceHeaderState.Missing,
                "a file with no licence header must read as Missing.", failures);
        }

        /// <summary> When a file's header is present and unambiguous it overrides the path: a proprietary header on a
        /// vendor path resolves proprietary+determinate, an MIT header on a City path resolves upstream+determinate.
        /// A Missing or Conflicting header falls back to the path rule but is never determinate -- RM-80 still owes
        /// the header, and a file carrying both headers is the forbidden ambiguous state. </summary>
        private static void CaseHeaderIsAuthoritativeOverPath(List<string> failures)
        {
            string prop = "// authored for this project\n";
            string mit = "// Upstream file, MIT licensed\n";

            FileLicenceResult a = LicenceRule.Resolve("Scripts/NewGizmo.cs", prop);
            Expect(a.licence == SourceLicence.ProprietaryNew && a.determinate,
                "a proprietary header on a vendor path must resolve proprietary and determinate.", failures);

            FileLicenceResult b = LicenceRule.Resolve("City/Runtime/Vendored.cs", mit);
            Expect(b.licence == SourceLicence.UpstreamMit && b.determinate,
                "an MIT header on a City path must resolve upstream MIT and determinate.", failures);

            FileLicenceResult c = LicenceRule.Resolve("City/Runtime/Foo.cs", "using System;\n");
            Expect(c.licence == SourceLicence.ProprietaryNew && !c.determinate,
                "a Missing header must fall back to the path rule but stay not-determinate.", failures);

            FileLicenceResult d = LicenceRule.Resolve("Scripts/Foo.cs", prop + mit);
            Expect(d.header == LicenceHeaderState.Conflicting && !d.determinate,
                "a Conflicting header must never be determinate.", failures);
        }

        /// <summary> The anchor RM-80 is told to write for a given licence is the very phrase this rule reads back:
        /// a file carrying only ExpectedHeaderAnchor(licence) classifies to exactly that licence's header state.
        /// This is the contract that keeps writer (RM-80), checker (RM-70) and rule on one template. </summary>
        private static void CaseExpectedAnchorRoundTrips(List<string> failures)
        {
            string propHeader = "// " + LicenceRule.ExpectedHeaderAnchor(SourceLicence.ProprietaryNew) + "\n";
            string mitHeader = "// " + LicenceRule.ExpectedHeaderAnchor(SourceLicence.UpstreamMit) + "\n";
            Expect(LicenceRule.HeaderStateOf(propHeader) == LicenceHeaderState.ProprietaryOnly,
                "the proprietary anchor must round-trip to ProprietaryOnly.", failures);
            Expect(LicenceRule.HeaderStateOf(mitHeader) == LicenceHeaderState.MitModifiedOnly,
                "the upstream-MIT anchor must round-trip to MitModifiedOnly.", failures);
            Expect(LicenceRule.ExpectedHeaderAnchor(SourceLicence.ProprietaryNew)
                != LicenceRule.ExpectedHeaderAnchor(SourceLicence.UpstreamMit),
                "the two anchors must be distinct.", failures);
        }

        /// <summary> C-06: resolving the same (path, text) twice gives byte-identical answers -- the rule has no
        /// hidden clock, RNG or hash-order dependence. </summary>
        private static void CaseResolveIsDeterministic(List<string> failures)
        {
            string text = "// authored for this project\nusing System;\n";
            FileLicenceResult r1 = LicenceRule.Resolve("City/Runtime/Data/PrefabManifest.cs", text);
            FileLicenceResult r2 = LicenceRule.Resolve("City/Runtime/Data/PrefabManifest.cs", text);
            Expect(r1.licence == r2.licence && r1.header == r2.header && r1.determinate == r2.determinate
                && r1.note == r2.note, "Resolve must be deterministic for identical inputs.", failures);
        }

        /// <summary> The provable-now core of RM-79: enumerate every `.cs` in the real tool tree and show the path
        /// rule assigns each to exactly one of the two licences, with both partitions non-empty (there IS
        /// proprietary City work and there IS vendor MIT code). Header coverage on disk is REPORTED, not asserted --
        /// writing the per-file headers is RM-80 and enforcing their presence is RM-70, so a Missing header is
        /// counted, never failed on. </summary>
        private static void CaseEveryFileInTreeResolvesToOneLicence(List<string> failures, StringBuilder sb)
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");
            List<string> files = EnumerateSourceFiles(root);

            int total = files.Count, prop = 0, mit = 0;
            int determinate = 0, missing = 0, conflicting = 0, unreadable = 0;
            for (int i = 0; i < files.Count; i++)
            {
                string rel = ToRelative(root, files[i]);
                SourceLicence byPath = LicenceRule.ClassifyByPath(rel);
                if (byPath == SourceLicence.ProprietaryNew) { prop++; } else { mit++; }

                string text;
                try { text = File.ReadAllText(files[i]); }
                catch { unreadable++; continue; }
                LicenceHeaderState h = LicenceRule.HeaderStateOf(text);
                if (h == LicenceHeaderState.Conflicting) { conflicting++; }
                else if (h == LicenceHeaderState.Missing) { missing++; }
                else { determinate++; }
            }

            Expect(total > 0, "the tool tree must contain at least one source file to classify.", failures);
            Expect(prop + mit == total, "every file must classify to exactly one licence (partition is total).",
                failures);
            Expect(prop > 0, "the proprietary partition must be non-empty (City work exists).", failures);
            Expect(mit > 0, "the upstream-MIT partition must be non-empty (vendor code exists).", failures);
            Expect(missing == 0, "every source file must carry its expected licence header; missing " + Num(missing),
                failures);
            Expect(conflicting == 0, "no source file may carry conflicting licence headers; conflicting "
                + Num(conflicting), failures);
            Expect(unreadable == 0, "every source file must be readable for licence verification; unreadable "
                + Num(unreadable), failures);

            sb.AppendLine(Prefix + " tree   files " + Num(total) + "  proprietary " + Num(prop)
                + "  upstreamMit " + Num(mit));
            sb.AppendLine(Prefix + " header determinate " + Num(determinate) + "  missing " + Num(missing)
                + "  conflicting " + Num(conflicting) + "  unreadable " + Num(unreadable)
                + "  (coverage asserted; vendor provenance selects the licence)");
        }

        private static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        /// <summary> Self-contained `.cs` enumeration for the runtime-assembly gate (it cannot reach the editor
        /// assembly's ShipAuditSweeps). Recursive, skips Unity-hidden folders (`.` prefix or `~` suffix) and this
        /// gate's own file so the rule is proven against the shipped tree, sorted Ordinal for determinism. </summary>
        private static List<string> EnumerateSourceFiles(string root)
        {
            List<string> found = new List<string>();
            if (!Directory.Exists(root)) { return found; }
            Collect(root, found, "CityLicenceGate.cs");
            found.Sort(System.StringComparer.Ordinal);
            return found;
        }

        private static void Collect(string dir, List<string> found, string selfName)
        {
            foreach (string file in Directory.GetFiles(dir, "*.cs"))
            {
                if (Path.GetFileName(file) == selfName) { continue; }
                found.Add(file);
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (string.IsNullOrEmpty(name) || name[0] == '.' || name.EndsWith("~")) { continue; }
                Collect(sub, found, selfName);
            }
        }

        private static string ToRelative(string root, string full)
        {
            string r = root.Replace('\\', '/');
            string f = full.Replace('\\', '/');
            if (f.StartsWith(r, System.StringComparison.OrdinalIgnoreCase))
            {
                string rel = f.Substring(r.Length);
                if (rel.Length > 0 && rel[0] == '/') { rel = rel.Substring(1); }
                return rel;
            }
            return f;
        }
    }
}
#endif
