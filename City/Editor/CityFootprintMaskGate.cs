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
    /// <summary> RM-44's gate: proves the footprint mask's coordinate, sampling and feather rules without a scene. It
    /// asserts that the world extent is reported in metres from the pixel count and metres-per-pixel; that sampling
    /// outside the bounds CLAMPS to the edge rather than wrapping; that the recorded nearest and bilinear modes
    /// genuinely disagree at a boundary (so the mode must be recorded, not assumed); that the feather is a
    /// reproducible ramp over an authored width and a zero width is a hard edge; that inside/outside bounds is
    /// correct; and that sampling is bit-identical on repeat (C-06). The Handles brush (paint / erase / feather in
    /// world units) and the serialised asset format are the deferred editor half. Report lines are prefixed
    /// RCMMASK. </summary>
    public static class CityFootprintMaskGate
    {
        [MenuItem("Window/Road City Master/Testing/City footprint mask (sampling and feather)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityFootprintMaskGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMMASK report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMMASK BEGIN\n");
            List<string> failures = new List<string>();

            CaseWorldExtentInMetres(builder, failures);
            CaseClampNotWrap(builder, failures);
            CaseNearestVsBilinearDiffer(builder, failures);
            CaseFeatherReproducible(builder, failures);
            CaseInsideBounds(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMMASK FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMMASK result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMMASK END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A 4x4 mask at 4 m per pixel is 16 m x 16 m of ground -- the extent is metres, not pixels. </summary>
        private static void CaseWorldExtentInMetres(StringBuilder builder, List<string> failures)
        {
            FootprintMaskClass m = new FootprintMaskClass(4, 4, 4f);
            Expect(Math.Abs(FootprintMask.WorldWidthMetres(m) - 16.0) < 1e-9
                   && Math.Abs(FootprintMask.WorldDepthMetres(m) - 16.0) < 1e-9,
                "extent: a 4x4 mask at 4 m/px was not reported as 16 m x 16 m", failures);
            builder.Append("RCMMASK ").Append("extent".PadRight(22))
                   .Append("world extent reported in metres (px * m/px)\n");
        }

        /// <summary> Sampling outside the bounds clamps to the edge pixel; it never wraps to the opposite side. </summary>
        private static void CaseClampNotWrap(StringBuilder builder, List<string> failures)
        {
            FootprintMaskClass m = SplitMask(MaskSampling.Bilinear); // left cols 1, right cols 0
            // Far to the left clamps to the left edge (1); far to the right clamps to the right edge (0).
            Expect(Math.Abs(FootprintMask.Sample(m, -100.0, 8.0) - 1.0) < 1e-9,
                "clamp: sampling far left did not clamp to the left edge value", failures);
            Expect(Math.Abs(FootprintMask.Sample(m, 100.0, 8.0) - 0.0) < 1e-9,
                "clamp: sampling far right did not clamp to the right edge value (wrapped?)", failures);
            builder.Append("RCMMASK ").Append("clamp-not-wrap".PadRight(22))
                   .Append("outside the bounds clamps to the edge, never wraps\n");
        }

        /// <summary> Nearest and bilinear genuinely disagree at a boundary, so the mode must be recorded. </summary>
        private static void CaseNearestVsBilinearDiffer(StringBuilder builder, List<string> failures)
        {
            FootprintMaskClass nearest = SplitMask(MaskSampling.Nearest);
            FootprintMaskClass bilinear = SplitMask(MaskSampling.Bilinear);
            // The boundary between the value-1 column and the value-0 column sits at world x = 8.
            double n = FootprintMask.Sample(nearest, 8.0, 8.0);
            double b = FootprintMask.Sample(bilinear, 8.0, 8.0);
            Expect(Math.Abs(n - 0.0) < 1e-9, "sampling: nearest at the boundary was not the stepped value", failures);
            Expect(Math.Abs(b - 0.5) < 1e-9, "sampling: bilinear at the boundary was not the blended value", failures);
            Expect(Math.Abs(n - b) > 0.25, "sampling: nearest and bilinear did not disagree at the boundary", failures);
            builder.Append("RCMMASK ").Append("sampling-recorded".PadRight(22))
                   .Append("nearest steps, bilinear blends -> mode must be recorded\n");
        }

        /// <summary> The feather is a reproducible ramp over an authored width; a zero width is a hard edge. </summary>
        private static void CaseFeatherReproducible(StringBuilder builder, List<string> failures)
        {
            // Width 8 m: -4 -> 0, 0 -> 0.5, +4 -> 1.
            Expect(Math.Abs(FootprintMask.FeatherCoverage(-4.0, 8.0) - 0.0) < 1e-9
                   && Math.Abs(FootprintMask.FeatherCoverage(0.0, 8.0) - 0.5) < 1e-9
                   && Math.Abs(FootprintMask.FeatherCoverage(4.0, 8.0) - 1.0) < 1e-9,
                "feather: the ramp did not run 0 -> 0.5 -> 1 across the authored width", failures);
            // Reproducible: same inputs, same output.
            Expect(FootprintMask.FeatherCoverage(1.3, 8.0) == FootprintMask.FeatherCoverage(1.3, 8.0),
                "feather: the ramp was not reproducible for identical inputs", failures);
            // Zero width is a hard edge.
            Expect(Math.Abs(FootprintMask.FeatherCoverage(0.01, 0.0) - 1.0) < 1e-9
                   && Math.Abs(FootprintMask.FeatherCoverage(-0.01, 0.0) - 0.0) < 1e-9,
                "feather: a zero feather width was not a hard edge", failures);
            builder.Append("RCMMASK ").Append("feather".PadRight(22))
                   .Append("feather is a reproducible ramp over an authored width\n");
        }

        /// <summary> Inside/outside the ground extent is correct. </summary>
        private static void CaseInsideBounds(StringBuilder builder, List<string> failures)
        {
            FootprintMaskClass m = new FootprintMaskClass(4, 4, 4f); // 16 m x 16 m at origin
            Expect(FootprintMask.IsInsideWorldBounds(m, 8.0, 8.0), "bounds: a point inside was reported outside", failures);
            Expect(!FootprintMask.IsInsideWorldBounds(m, -1.0, 8.0)
                   && !FootprintMask.IsInsideWorldBounds(m, 8.0, 20.0),
                "bounds: a point outside was reported inside", failures);
            builder.Append("RCMMASK ").Append("bounds".PadRight(22))
                   .Append("inside / outside the ground extent is correct\n");
        }

        /// <summary> Sampling is bit-identical on repeat -- pure data (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            FootprintMaskClass a = SplitMask(MaskSampling.Bilinear);
            FootprintMaskClass b = SplitMask(MaskSampling.Bilinear);
            bool same = true;
            for (double x = 0.0; x <= 16.0; x += 2.0)
            {
                if (FootprintMask.Sample(a, x, 8.0) != FootprintMask.Sample(b, x, 8.0)) { same = false; }
            }
            Expect(same, "determinism: two identical masks sampled differently", failures);
            builder.Append("RCMMASK ").Append("determinism".PadRight(22))
                   .Append("identical mask -> identical samples\n");
        }

        /// <summary> A 4x4 mask, 4 m/px, origin 0: left two columns 1.0, right two columns 0.0 (a vertical split). </summary>
        private static FootprintMaskClass SplitMask(MaskSampling sampling)
        {
            FootprintMaskClass m = new FootprintMaskClass(4, 4, 4f);
            m.sampling = sampling;
            for (int z = 0; z < 4; z++)
            {
                for (int x = 0; x < 4; x++)
                {
                    m.coverage[z * 4 + x] = x < 2 ? 1f : 0f;
                }
            }
            return m;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
