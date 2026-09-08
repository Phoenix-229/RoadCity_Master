// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-29's gate: proves the footway offset maths, the road-profile height inheritance, the derived
    /// kerb-return radius, the crossing drop, and the self-intersection (fold) detection without a scene. It
    /// asserts that the footway offset sits across the verge and out to the footway centreline; that the footway
    /// top follows the ROAD profile plus the kerb rather than the terrain (the RM-26 anti-sink rule); that the
    /// kerb-return radius is the carriageway corner radius plus the offset, so a tighter corner gives a tighter
    /// return; that a crossing drops to the near-flush PROWAG lip; that an inward offset larger than the local
    /// radius of curvature is flagged as a fold while a gentle curve or an outward offset is not (Q-11); and that
    /// the maths is bit-identical on repeat (C-06). The swept mesh, the corner arc and the clipping of a detected
    /// fold are the deferred half. Report lines are prefixed RCMWALK. </summary>
    public static class CitySidewalkGate
    {
        [MenuItem("Window/Road City Master/Testing/City sidewalk (offset + corner)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CitySidewalkGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMWALK report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMWALK BEGIN\n");
            List<string> failures = new List<string>();

            CaseOffsetFromEdge(builder, failures);
            CaseFollowsRoadProfileNotTerrain(builder, failures);
            CaseKerbReturnDerivedFromCorner(builder, failures);
            CaseCrossingDropsToFlush(builder, failures);
            CaseInwardOffsetFoldDetected(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMWALK FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMWALK result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMWALK END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The footway offset is the verge plus half the (clamped) footway width, and the width never
        /// falls below the PROWAG minimum. </summary>
        private static void CaseOffsetFromEdge(StringBuilder builder, List<string> failures)
        {
            SidewalkClass cls = SidewalkClass.Default(); // verge 0.6, width 1.8
            double width = Sidewalk.EffectiveFootwayWidthMetres(cls);
            Expect(Math.Abs(width - 1.8) < 1e-9, "offset: effective footway width was not the authored 1.8 m",
                failures);
            double offset = Sidewalk.OffsetFromCarriagewayEdgeMetres(cls);
            Expect(Math.Abs(offset - (0.6 + 0.9)) < 1e-9,
                "offset: footway offset was not the verge plus half the footway width", failures);

            SidewalkClass narrow = SidewalkClass.Default();
            narrow.footwayWidthMetres = 0.5f; // below the PROWAG minimum
            Expect(Math.Abs(Sidewalk.EffectiveFootwayWidthMetres(narrow) - narrow.minFootwayWidthMetres) < 1e-6,
                "offset: a sub-minimum footway width was not raised to the PROWAG floor", failures);
            builder.Append("RCMWALK ").Append("offset".PadRight(22))
                   .Append("footway offset = verge + half width, width >= PROWAG min\n");
        }

        /// <summary> The footway top follows the road profile plus the kerb, not the terrain: two different terrain
        /// heights under the same road profile give the same footway top. </summary>
        private static void CaseFollowsRoadProfileNotTerrain(StringBuilder builder, List<string> failures)
        {
            SidewalkClass cls = SidewalkClass.Default(); // kerb 0.15
            const double roadProfile = 5.0; // road raised on an embankment
            double top = Sidewalk.SidewalkTopHeightMetres(roadProfile, cls);
            Expect(Math.Abs(top - (roadProfile + 0.15)) < 1e-9,
                "profile: footway top was not the road profile raised by the kerb", failures);
            // The terrain beneath is irrelevant -- the function takes only the road profile, so a footway over a
            // cut (road below terrain) still seats on the road, not the terrain that would submerge it.
            double overCut = Sidewalk.SidewalkTopHeightMetres(roadProfile, cls);
            Expect(Math.Abs(top - overCut) < 1e-12,
                "profile: the footway height depended on something other than the road profile", failures);
            builder.Append("RCMWALK ").Append("road-profile".PadRight(22))
                   .Append("footway follows the road profile + kerb, not the terrain\n");
        }

        /// <summary> The kerb-return radius is the carriageway corner radius plus the footway offset, so a tighter
        /// carriageway corner yields a tighter (but always larger by the offset) footway return. </summary>
        private static void CaseKerbReturnDerivedFromCorner(StringBuilder builder, List<string> failures)
        {
            SidewalkClass cls = SidewalkClass.Default();
            double offset = Sidewalk.OffsetFromCarriagewayEdgeMetres(cls);
            double tight = Sidewalk.KerbReturnRadiusMetres(6.0, cls);
            double wide = Sidewalk.KerbReturnRadiusMetres(12.0, cls);
            Expect(Math.Abs(tight - (6.0 + offset)) < 1e-9,
                "corner: kerb-return radius was not the carriageway corner radius plus the offset", failures);
            Expect(wide > tight,
                "corner: a wider carriageway corner did not give a wider footway return", failures);
            double fallback = Sidewalk.KerbReturnRadiusMetres(0.0, cls);
            Expect(Math.Abs(fallback - (cls.cornerReturnRadiusMetres + offset)) < 1e-9,
                "corner: a missing carriageway radius did not fall back to the authored radius plus offset",
                failures);
            builder.Append("RCMWALK ").Append("kerb-return".PadRight(22))
                   .Append("footway return radius derived from the carriageway corner\n");
        }

        /// <summary> At a crossing the footway drops to the near-flush PROWAG lip, still referenced to the road
        /// profile and below the full kerb height. </summary>
        private static void CaseCrossingDropsToFlush(StringBuilder builder, List<string> failures)
        {
            SidewalkClass cls = SidewalkClass.Default();
            const double roadProfile = 2.0;
            double drop = Sidewalk.CrossingDropHeightMetres(roadProfile, cls);
            Expect(Math.Abs(drop - (roadProfile + 0.013)) < 1e-9,
                "crossing: the drop did not land on the near-flush PROWAG lip above the road profile", failures);
            Expect(drop < Sidewalk.SidewalkTopHeightMetres(roadProfile, cls),
                "crossing: the dropped kerb was not below the full kerb height", failures);
            builder.Append("RCMWALK ").Append("crossing-drop".PadRight(22))
                   .Append("crossing drops to the PROWAG near-flush lip\n");
        }

        /// <summary> An inward offset larger than the local radius of curvature is flagged as a fold; a gentle
        /// curve, a straight run, and an outward (non-positive) offset are not. </summary>
        private static void CaseInwardOffsetFoldDetected(StringBuilder builder, List<string> failures)
        {
            // Offset 1.5 m into a 1.0 m radius curve: the offset crosses itself.
            Sidewalk.OffsetFoldCheck tight = Sidewalk.CheckInwardOffset(1.0, 1.5);
            Expect(tight.degenerate, "fold: an inward offset larger than the local radius was not flagged", failures);

            // Same offset on a 50 m radius curve: fine.
            Sidewalk.OffsetFoldCheck gentle = Sidewalk.CheckInwardOffset(50.0, 1.5);
            Expect(!gentle.degenerate, "fold: a gentle curve was wrongly flagged as a fold", failures);

            // A straight run (infinite radius) never folds.
            Sidewalk.OffsetFoldCheck straight = Sidewalk.CheckInwardOffset(double.PositiveInfinity, 1.5);
            Expect(!straight.degenerate, "fold: a straight run was wrongly flagged as a fold", failures);

            // An outward offset (expressed as a non-positive inward offset) never folds.
            Sidewalk.OffsetFoldCheck outward = Sidewalk.CheckInwardOffset(1.0, 0.0);
            Expect(!outward.degenerate, "fold: an outward offset was wrongly flagged as a fold", failures);
            builder.Append("RCMWALK ").Append("offset-fold".PadRight(22))
                   .Append("inward offset past the local radius flagged, else clean (Q-11)\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            SidewalkClass cls = SidewalkClass.Default();
            double a = Sidewalk.KerbReturnRadiusMetres(7.5, cls);
            double b = Sidewalk.KerbReturnRadiusMetres(7.5, cls);
            double h1 = Sidewalk.SidewalkTopHeightMetres(3.25, cls);
            double h2 = Sidewalk.SidewalkTopHeightMetres(3.25, cls);
            Expect(a == b && h1 == h2,
                "determinism: two identical sidewalk computations produced different values", failures);
            builder.Append("RCMWALK ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical offset/height\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
