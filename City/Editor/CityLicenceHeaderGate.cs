// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-80's headless proof: the per-file header transform stamps the correct header, is idempotent,
    /// never flips a file that already declares itself, refuses the ambiguous both-headers state, and round-trips
    /// through RM-79's reader so a stamped file classifies to the licence it was stamped with. The physical run
    /// over the tree (`LicenceHeaderOperator.StampAll`) is author-invoked and the presence enforcement is RM-70;
    /// this gate proves the mechanism they both rely on. </summary>
    public static class CityLicenceHeaderGate
    {
        private const string Prefix = "RCMLICHEADER";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Licence headers")]
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

            CaseStampsMissingHeaderByPath(failures);
            CaseIdempotentAndNoDouble(failures);
            CaseNeverFlipsDeterminateHeader(failures);
            CaseConflictingRefused(failures);
            CaseHeaderForRoundTrips(failures);
            CaseStampIsDeterministic(failures);
            CaseTemplatesCarryCorrectAnchors(failures);

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

        /// <summary> A headerless file gets the header its path implies: a City/ file becomes proprietary, a
        /// vendor Scripts/ file becomes upstream MIT, the original code is preserved below the header, and the
        /// stamped text reads back as the matching determinate state. </summary>
        private static void CaseStampsMissingHeaderByPath(List<string> failures)
        {
            string body = "using System;\nclass A {}\n";
            bool changed;
            string note;
            string prop = LicenceHeaderOperator.Stamp("City/Runtime/Data/Foo.cs", body, out changed, out note);
            Expect(changed, "a City/ file with no header must be changed.", failures);
            Expect(LicenceRule.HeaderStateOf(prop) == LicenceHeaderState.ProprietaryOnly,
                "a stamped City/ file must read as ProprietaryOnly.", failures);
            Expect(prop.EndsWith(body), "the stamp must preserve the original code below the header.", failures);

            string mit = LicenceHeaderOperator.Stamp("Scripts/Road.cs", body, out changed, out note);
            Expect(changed && LicenceRule.HeaderStateOf(mit) == LicenceHeaderState.MitModifiedOnly,
                "a stamped vendor Scripts/ file must read as MitModifiedOnly.", failures);
        }

        /// <summary> Stamping an already-stamped file is a no-op: no change, no second header, still the same
        /// determinate state. </summary>
        private static void CaseIdempotentAndNoDouble(List<string> failures)
        {
            string body = "using System;\n";
            bool changed;
            string note;
            string once = LicenceHeaderOperator.Stamp("City/Runtime/Foo.cs", body, out changed, out note);
            string twice = LicenceHeaderOperator.Stamp("City/Runtime/Foo.cs", once, out changed, out note);
            Expect(!changed, "stamping an already-headed file must not change it.", failures);
            Expect(once == twice, "stamping twice must be identical (no doubled header).", failures);
            Expect(LicenceRule.HeaderStateOf(twice) == LicenceHeaderState.ProprietaryOnly,
                "the twice-stamped file must still read ProprietaryOnly.", failures);
        }

        /// <summary> The transform never flips a file that already declares itself: a vendor-path file already
        /// carrying the proprietary header keeps it, even though the path alone would say upstream MIT. </summary>
        private static void CaseNeverFlipsDeterminateHeader(List<string> failures)
        {
            string already = LicenceHeaderOperator.ProprietaryHeader + "using System;\n";
            bool changed;
            string note;
            string result = LicenceHeaderOperator.Stamp("Scripts/NewGizmo.cs", already, out changed, out note);
            Expect(!changed, "a file already carrying a determinate header must be left unchanged.", failures);
            Expect(LicenceRule.HeaderStateOf(result) == LicenceHeaderState.ProprietaryOnly,
                "the header must not be flipped by the path rule.", failures);
        }

        /// <summary> A file carrying both headers is refused, never silently repaired: RM-79 names it the
        /// forbidden ambiguous state that a human must resolve. </summary>
        private static void CaseConflictingRefused(List<string> failures)
        {
            string both = LicenceHeaderOperator.ProprietaryHeader + LicenceHeaderOperator.MitHeader
                + "using System;\n";
            bool changed;
            string note;
            string result = LicenceHeaderOperator.Stamp("City/Runtime/Foo.cs", both, out changed, out note);
            Expect(!changed, "a conflicting file must not be modified.", failures);
            Expect(note.StartsWith("refused"), "a conflicting file must be refused by name.", failures);
            Expect(result == both, "a refused file's text must be returned unchanged.", failures);
        }

        /// <summary> HeaderFor and RM-79's reader are two ends of one contract: the header written for a licence
        /// reads back as exactly that licence's determinate state, and the two headers are distinct. </summary>
        private static void CaseHeaderForRoundTrips(List<string> failures)
        {
            Expect(LicenceRule.HeaderStateOf(LicenceHeaderOperator.HeaderFor(SourceLicence.ProprietaryNew))
                == LicenceHeaderState.ProprietaryOnly, "the proprietary header must read ProprietaryOnly.", failures);
            Expect(LicenceRule.HeaderStateOf(LicenceHeaderOperator.HeaderFor(SourceLicence.UpstreamMit))
                == LicenceHeaderState.MitModifiedOnly, "the MIT header must read MitModifiedOnly.", failures);
            Expect(LicenceHeaderOperator.ProprietaryHeader != LicenceHeaderOperator.MitHeader,
                "the two headers must be distinct.", failures);
        }

        /// <summary> C-06: the transform is deterministic -- same inputs, byte-identical output and decision. </summary>
        private static void CaseStampIsDeterministic(List<string> failures)
        {
            string body = "using System;\nclass B {}\n";
            bool c1, c2;
            string n1, n2;
            string r1 = LicenceHeaderOperator.Stamp("Scripts/A.cs", body, out c1, out n1);
            string r2 = LicenceHeaderOperator.Stamp("Scripts/A.cs", body, out c2, out n2);
            Expect(r1 == r2 && c1 == c2 && n1 == n2, "Stamp must be deterministic for identical inputs.", failures);
        }

        /// <summary> The templates carry exactly the anchors RM-79 reads and point at the right files -- the
        /// proprietary header at LICENSE.md, the MIT header at LICENSE-MIT.txt -- and neither contains the other's
        /// anchor, so a correctly stamped file is never misread as Conflicting. </summary>
        private static void CaseTemplatesCarryCorrectAnchors(List<string> failures)
        {
            string prop = LicenceHeaderOperator.ProprietaryHeader;
            string mit = LicenceHeaderOperator.MitHeader;
            Expect(prop.Contains("authored for this project") && prop.Contains("LICENSE.md"),
                "the proprietary header must carry its anchor and point at LICENSE.md.", failures);
            Expect(mit.Contains("Upstream file, MIT licensed") && mit.Contains("LICENSE-MIT.txt"),
                "the MIT header must carry its anchor and point at LICENSE-MIT.txt.", failures);
            Expect(!prop.Contains("Upstream file, MIT licensed") && !mit.Contains("authored for this project"),
                "neither header may contain the other's anchor phrase.", failures);
        }
    }
}
#endif
