// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RoadCityMaster.CrossSection;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-13's gate: proves the two genuinely NEW streetscape rules behave, without a scene. The
    /// kerb/footway/verge are profile elements swept by the existing cross-section machinery (RM-31) and the
    /// furniture is placed by the existing EdgeObjectMaker -- neither is re-implemented, so neither is what a
    /// headless gate can assert; what it CAN assert is that furniture spacing QUANTISES to whole intervals
    /// (no half-gap at the end of a run), that furniture is EXCLUDED from driveways/crossings/junction
    /// corners, and that the authored streetscape numbers are self-consistent (crossing kerb not taller than
    /// the kerb, footway crossfall drains toward the kerb without exceeding the accessibility maximum).
    /// The swept meshes themselves are the author's scene check. Report lines are prefixed RCMSTREET. </summary>
    public static class StreetscapeGate
    {
        [MenuItem("Window/Road City Master/Testing/Streetscape furniture and profile")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.StreetscapeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMSTREET report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSTREET BEGIN\n");
            List<string> failures = new List<string>();

            CaseQuantiseWholeIntervals(builder, failures);
            CaseExclusion(builder, failures);
            CaseProfileConsistency(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSTREET FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSTREET result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSTREET END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A 103 m run at a desired 25 m spacing must land on whole intervals: the last post sits
        /// exactly on the run end (no half-gap) and the actual spacing stays close to the desired value. </summary>
        private static void CaseQuantiseWholeIntervals(StringBuilder builder, List<string> failures)
        {
            const float runLength = 103f;
            const float desired = 25f;
            List<float> stations = FurnitureRunPlanner.PlanStations(runLength, desired, null);
            float actual = FurnitureRunPlanner.QuantisedSpacing(runLength, desired);

            Expect(stations.Count >= 2, "quantise: fewer than two posts placed on the run", failures);
            Expect(Approx(stations[0], 0f, 1e-3f), "quantise: first post is not at the run start", failures);
            Expect(Approx(stations[stations.Count - 1], runLength, 1e-3f),
                "quantise: last post " + F(stations[stations.Count - 1]) + " is not on the run end "
                + F(runLength) + " (a half-gap)", failures);
            Expect(Mathf.Abs(actual - desired) <= desired * 0.25f,
                "quantise: actual spacing " + F(actual) + " strays too far from desired " + F(desired), failures);
            builder.Append("RCMSTREET ").Append("quantise".PadRight(18))
                   .Append("posts ").Append(stations.Count).Append("  actual spacing ").Append(F(actual))
                   .Append(" m (desired ").Append(F(desired)).Append(")\n");
        }

        /// <summary> With a crossing occupying [50,53] m of the run, no post may land inside it -- the lamp
        /// column in a vehicle crossover is the failure this rule prevents. </summary>
        private static void CaseExclusion(StringBuilder builder, List<string> failures)
        {
            const float runLength = 103f;
            const float desired = 25f;
            List<Vector2> spans = new List<Vector2> { new Vector2(50f, 53f) };
            List<float> stations = FurnitureRunPlanner.PlanStations(runLength, desired, spans);

            bool anyInside = false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (FurnitureRunPlanner.IsExcluded(stations[i], spans)) { anyInside = true; }
            }
            Expect(!anyInside, "exclusion: a post was placed inside an exclusion span", failures);
            // The un-excluded run put a post at 51.5 m; with the span it must be gone.
            List<float> unExcluded = FurnitureRunPlanner.PlanStations(runLength, desired, null);
            Expect(stations.Count == unExcluded.Count - 1,
                "exclusion: expected exactly one post removed by the crossing span", failures);
            builder.Append("RCMSTREET ").Append("exclusion".PadRight(18))
                   .Append("posts ").Append(unExcluded.Count).Append(" -> ").Append(stations.Count)
                   .Append(" (crossing [50,53] cleared)\n");
        }

        /// <summary> The AUTHORED default streetscape is self-consistent; a footway crossfall above the
        /// accessibility maximum is caught with a numbered reason. </summary>
        private static void CaseProfileConsistency(StringBuilder builder, List<string> failures)
        {
            StreetscapeProfile ok = StreetscapeProfile.Default();
            string reason;
            Expect(ok.IsConsistent(out reason), "profile: the default streetscape is inconsistent (" + reason + ")",
                failures);

            StreetscapeProfile steep = StreetscapeProfile.Default();
            steep.footwayCrossfall = 0.06f; // above the 2.5% accessibility cap
            string steepReason;
            Expect(!steep.IsConsistent(out steepReason) && steepReason.Contains("accessibility"),
                "profile: an over-steep crossfall was not caught with an accessibility reason", failures);
            builder.Append("RCMSTREET ").Append("profile".PadRight(18)).Append("default ")
                   .Append(reason).Append("; steep -> ").Append(steepReason).Append("\n");
        }

        /// <summary> The planner is deterministic: the same run planned twice yields identical stations
        /// (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            List<Vector2> spans = new List<Vector2> { new Vector2(50f, 53f) };
            string a = Dump(FurnitureRunPlanner.PlanStations(103f, 25f, spans));
            string b = Dump(FurnitureRunPlanner.PlanStations(103f, 25f, spans));
            Expect(a == b, "deterministic: two identical runs produced different stations", failures);
            builder.Append("RCMSTREET ").Append("deterministic".PadRight(18))
                   .Append(a == b ? "identical" : "DIFFER").Append("\n");
        }

        private static string Dump(List<float> stations)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < stations.Count; i++) { sb.Append(F(stations[i])).Append('|'); }
            return sb.ToString();
        }

        private static string F(float f)
        {
            return f.ToString("0.####", CultureInfo.InvariantCulture);
        }

        private static bool Approx(float a, float b, float eps)
        {
            return Mathf.Abs(a - b) <= eps;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
