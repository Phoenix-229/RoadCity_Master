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
    /// <summary> RM-38's gate: proves the corrected footprint maths without a scene -- three independent widths
    /// and a creaseless blend. It asserts that the flattened, blend and paint widths are separate quantities and
    /// that the paint width is NOT the deform width; that the flattened band follows the emitted cross-section
    /// (RM-31, per station) rather than a per-road constant, so a wide flare flattens wider and a narrow lane
    /// narrower; that the blend is a smoothstep that reaches natural height with zero slope at both ends (no
    /// crease) and is monotonic; that the deformed height is flat over the band, blends to natural, then stays
    /// natural; that the splat stops at the paint width, narrow when sealed and wide when unsealed; and that
    /// everything is bit-identical on repeat (C-06). Wiring into the live terraform writer and the before/after
    /// screenshots are the deferred half. Report lines are prefixed RCMFOOT. </summary>
    public static class CityTerrainFootprintGate
    {
        [MenuItem("Window/Road City Master/Testing/City terrain footprint (three widths)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityTerrainFootprintGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMFOOT report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMFOOT BEGIN\n");
            List<string> failures = new List<string>();

            CaseThreeWidthsSeparate(builder, failures);
            CaseFlattenedFollowsCrossSection(builder, failures);
            CaseSmoothstepNoCrease(builder, failures);
            CaseDeformedHeightProfile(builder, failures);
            CasePaintStopsAtPaintWidth(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMFOOT FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMFOOT result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMFOOT END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The three widths are distinct and the paint width is not the deform (blend) width. </summary>
        private static void CaseThreeWidthsSeparate(StringBuilder builder, List<string> failures)
        {
            TerrainFootprintClass cls = TerrainFootprintClass.Default(); // shoulder 0.5, blend 8, paint 0.5/4
            double flattened = TerrainFootprint.FlattenedHalfWidthMetres(3.5, cls); // 4.0
            double blendOuter = TerrainFootprint.BlendOuterHalfWidthMetres(flattened, cls); // 12.0
            double paintSealed = TerrainFootprint.PaintHalfWidthMetres(flattened, true, cls); // 4.5
            Expect(Math.Abs(flattened - 4.0) < 1e-9, "widths: flattened was not cross-section + shoulder", failures);
            Expect(Math.Abs(blendOuter - 12.0) < 1e-9, "widths: blend outer was not flattened + blend distance",
                failures);
            Expect(Math.Abs(paintSealed - 4.5) < 1e-9 && Math.Abs(paintSealed - blendOuter) > 1.0,
                "widths: the paint width was wired to the deform width instead of its own margin", failures);
            builder.Append("RCMFOOT ").Append("three-widths".PadRight(22))
                   .Append("flatten / blend / paint are separate quantities\n");
        }

        /// <summary> The flattened band follows the emitted cross-section, not a per-road constant: a wide flare
        /// flattens wider than a narrow lane. </summary>
        private static void CaseFlattenedFollowsCrossSection(StringBuilder builder, List<string> failures)
        {
            TerrainFootprintClass cls = TerrainFootprintClass.Default();
            double narrow = TerrainFootprint.FlattenedHalfWidthMetres(3.5, cls);   // a plain lane pair
            double flare = TerrainFootprint.FlattenedHalfWidthMetres(11.0, cls);   // a roundabout approach flare
            Expect(flare > narrow + 5.0,
                "cross-section: a wide flare did not flatten wider than a narrow lane (still a constant?)",
                failures);
            builder.Append("RCMFOOT ").Append("follows-section".PadRight(22))
                   .Append("flattened band tracks RM-31 width per station\n");
        }

        /// <summary> The blend is a smoothstep: 0 and 1 at the edges, monotonic, and zero-slope at both ends. </summary>
        private static void CaseSmoothstepNoCrease(StringBuilder builder, List<string> failures)
        {
            Expect(Math.Abs(TerrainFootprint.Smoothstep(4.0, 12.0, 4.0)) < 1e-12
                   && Math.Abs(TerrainFootprint.Smoothstep(4.0, 12.0, 12.0) - 1.0) < 1e-12,
                "smoothstep: the falloff was not exactly 0 and 1 at the edges", failures);
            Expect(Math.Abs(TerrainFootprint.Smoothstep(4.0, 12.0, 8.0) - 0.5) < 1e-9,
                "smoothstep: the midpoint was not 0.5", failures);
            // Numeric slope near each end must be ~0 (a linear ramp would have a constant non-zero slope).
            double eps = 1e-4;
            double slopeLow = (TerrainFootprint.Smoothstep(4.0, 12.0, 4.0 + eps)
                               - TerrainFootprint.Smoothstep(4.0, 12.0, 4.0)) / eps;
            double slopeHigh = (TerrainFootprint.Smoothstep(4.0, 12.0, 12.0)
                                - TerrainFootprint.Smoothstep(4.0, 12.0, 12.0 - eps)) / eps;
            Expect(Math.Abs(slopeLow) < 1e-3 && Math.Abs(slopeHigh) < 1e-3,
                "smoothstep: the falloff did not meet the ends tangentially (a crease)", failures);
            // Monotonic increasing across the blend.
            bool monotonic = true;
            double prev = -1.0;
            for (double x = 4.0; x <= 12.0; x += 0.5)
            {
                double s = TerrainFootprint.Smoothstep(4.0, 12.0, x);
                if (s < prev - 1e-12) { monotonic = false; }
                prev = s;
            }
            Expect(monotonic, "smoothstep: the falloff was not monotonic", failures);
            builder.Append("RCMFOOT ").Append("smoothstep".PadRight(22))
                   .Append("blend meets both ends tangentially, monotonic\n");
        }

        /// <summary> The deformed height is flat over the band, blends to natural, then stays natural. </summary>
        private static void CaseDeformedHeightProfile(StringBuilder builder, List<string> failures)
        {
            const double road = 50.0, natural = 42.0, flat = 4.0, outer = 12.0;
            Expect(Math.Abs(TerrainFootprint.DeformedHeightMetres(road, natural, 0.0, flat, outer) - road) < 1e-9,
                "height: the centre was not at road height", failures);
            Expect(Math.Abs(TerrainFootprint.DeformedHeightMetres(road, natural, 4.0, flat, outer) - road) < 1e-9,
                "height: the flattened edge was not still flat at road height", failures);
            Expect(Math.Abs(TerrainFootprint.DeformedHeightMetres(road, natural, 12.0, flat, outer) - natural) < 1e-9,
                "height: the blend did not reach natural height by the outer edge", failures);
            Expect(Math.Abs(TerrainFootprint.DeformedHeightMetres(road, natural, 30.0, flat, outer) - natural) < 1e-9,
                "height: the ground beyond the blend was not left at natural height", failures);
            double mid = TerrainFootprint.DeformedHeightMetres(road, natural, 8.0, flat, outer);
            Expect(mid < road && mid > natural, "height: the blend midpoint was not between road and natural",
                failures);
            builder.Append("RCMFOOT ").Append("height-profile".PadRight(22))
                   .Append("flat over band, blends to natural, then natural\n");
        }

        /// <summary> The splat stops at the paint width; sealed paints narrower than unsealed. </summary>
        private static void CasePaintStopsAtPaintWidth(StringBuilder builder, List<string> failures)
        {
            TerrainFootprintClass cls = TerrainFootprintClass.Default();
            double flattened = TerrainFootprint.FlattenedHalfWidthMetres(3.5, cls); // 4.0
            double sealedW = TerrainFootprint.PaintHalfWidthMetres(flattened, true, cls);   // 4.5
            double unsealedW = TerrainFootprint.PaintHalfWidthMetres(flattened, false, cls); // 8.0
            Expect(unsealedW > sealedW, "paint: an unsealed road did not paint wider than a sealed one", failures);
            Expect(TerrainFootprint.IsPaintedAt(4.4, sealedW) && !TerrainFootprint.IsPaintedAt(6.0, sealedW),
                "paint: a sealed road's dirt did not stop at its narrow verge", failures);
            Expect(TerrainFootprint.IsPaintedAt(6.0, unsealedW),
                "paint: an unsealed road did not paint out to its dust margin", failures);
            builder.Append("RCMFOOT ").Append("paint-width".PadRight(22))
                   .Append("splat stops at paint width; sealed narrow, unsealed wide\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            double a = TerrainFootprint.DeformedHeightMetres(50.0, 42.0, 7.3, 4.0, 12.0);
            double b = TerrainFootprint.DeformedHeightMetres(50.0, 42.0, 7.3, 4.0, 12.0);
            double s1 = TerrainFootprint.Smoothstep(4.0, 12.0, 9.1);
            double s2 = TerrainFootprint.Smoothstep(4.0, 12.0, 9.1);
            Expect(a == b && s1 == s2, "determinism: two identical footprint computations differed", failures);
            builder.Append("RCMFOOT ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical height/falloff\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
