#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-81's headless proof of the packaging rules: the chosen distribution form is stated (not
    /// defaulted), the four RM-80 notice files are named and distinct, the GUID rule (every imported file needs a
    /// paired `.meta`) is expressed correctly, and the notice-file membership test is exact. The clean-project
    /// install-and-compile clause of the deliverable is author-verified; this gate proves the rules the exporter
    /// enforces before it writes. </summary>
    public static class CityPackageGate
    {
        private const string Prefix = "RCMPACKAGE";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Packaging rules")]
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
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CaseFormIsStatedWithReasoning(failures);
            CaseNoticeFilesAreTheFourAndDistinct(failures);
            CaseMetaRuleAndNoticeMembership(failures);
            CaseRulesAreDeterministic(failures);

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

        /// <summary> The distribution form is a stated decision (full source) carrying its reasoning, not a
        /// silent default. </summary>
        private static void CaseFormIsStatedWithReasoning(List<string> failures)
        {
            Expect(PackageRule.ChosenForm == DistributionForm.FullSource,
                "the chosen distribution form must be full source.", failures);
            Expect(!string.IsNullOrEmpty(PackageRule.Rationale()) && PackageRule.Rationale().Length > 40,
                "the chosen form must carry a stated rationale.", failures);
        }

        /// <summary> The required notice files are exactly the four RM-80 files, with no duplicate. </summary>
        private static void CaseNoticeFilesAreTheFourAndDistinct(List<string> failures)
        {
            IReadOnlyList<string> req = PackageRule.RequiredNoticeFiles();
            Expect(req.Count == 4, "there must be exactly four required notice files.", failures);
            HashSet<string> set = new HashSet<string>();
            for (int i = 0; i < req.Count; i++) { set.Add(req[i]); }
            Expect(set.Count == 4, "the four notice files must be distinct.", failures);
            Expect(set.Contains("LICENSE.md") && set.Contains("LICENSE-MIT.txt")
                && set.Contains("THIRD-PARTY-NOTICES.md") && set.Contains("NOTICE.md"),
                "the notice files must be LICENSE.md, LICENSE-MIT.txt, THIRD-PARTY-NOTICES.md, NOTICE.md.", failures);
        }

        /// <summary> Every imported file needs a paired `.meta` except a `.meta` itself; and the notice-membership
        /// test recognises exactly the four files. </summary>
        private static void CaseMetaRuleAndNoticeMembership(List<string> failures)
        {
            Expect(PackageRule.RequiresMeta("City/Runtime/Foo.cs"), "a .cs file must require a .meta.", failures);
            Expect(PackageRule.RequiresMeta("LICENSE.md"), "a shipped text file must require a .meta.", failures);
            Expect(PackageRule.RequiresMeta("Textures/Icon.png"), "a shipped asset must require a .meta.", failures);
            Expect(!PackageRule.RequiresMeta("City/Runtime/Foo.cs.meta"), "a .meta must not require its own .meta.",
                failures);
            Expect(PackageRule.IsNoticeFile("NOTICE.md") && PackageRule.IsNoticeFile("LICENSE-MIT.txt"),
                "the notice-membership test must recognise the notice files.", failures);
            Expect(!PackageRule.IsNoticeFile("README.md") && !PackageRule.IsNoticeFile("Road.cs"),
                "a non-notice file must not be counted as a notice file.", failures);
        }

        /// <summary> C-06: the rules return the same answers every call. </summary>
        private static void CaseRulesAreDeterministic(List<string> failures)
        {
            IReadOnlyList<string> a = PackageRule.RequiredNoticeFiles();
            IReadOnlyList<string> b = PackageRule.RequiredNoticeFiles();
            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++) { if (a[i] != b[i]) { same = false; } }
            Expect(same, "the required-notice-file list must be deterministic.", failures);
            Expect(PackageRule.RequiresMeta("X.cs") == PackageRule.RequiresMeta("X.cs")
                && PackageRule.Rationale() == PackageRule.Rationale(),
                "the packaging rules must be deterministic.", failures);
        }
    }
}
#endif
