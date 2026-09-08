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
    /// <summary> RM-31's gate: proves the variable-width profile that the median (and, after it, the taper,
    /// verge, ditch and edge beam) is built on behaves the way the spec's four requirements demand -- a
    /// median that widens from zero into a planted strip and narrows back to zero with NO KINK at either
    /// transition, no negative or emitted zero-width ribbon where it collapses to a line, carriageway edges
    /// that move OUTWARD as the median grows, a total-width contribution the terrain falloff can consume,
    /// and two identical bakes.
    ///
    /// It exercises the PURE core (<see cref="VariableWidthProfile"/> / <see cref="MedianProfile"/>); the
    /// swept mesh those values drive is confirmed in the scene, not asserted here. Report lines are prefixed
    /// RCMPROFILE. </summary>
    public static class CrossSectionProfileGate
    {
        // The test median: a 4 m planted median (2 m half-width) opening at 10 m and closing at 60 m along
        // a 70 m road, with an authored nose taper rate of 3 m of length per metre of half-width.
        private const float RoadLength = 70f;
        private const float MedianStart = 10f;
        private const float MedianEnd = 60f;
        private const float MedianHalfWidth = 2f;
        private const float NoseTaperRate = 3f;
        private const float BaseHalfCarriageway = 7f; // one 7 m half-carriageway when the median is absent
        private const float SampleStep = 0.5f;

        [MenuItem("Window/Road City Master/Testing/Cross-section variable-width profile")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.CrossSectionProfileGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMPROFILE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPROFILE BEGIN\n");
            List<string> failures = new List<string>();

            MedianProfile median = MedianProfile.Planted(MedianStart, MedianEnd, MedianHalfWidth, NoseTaperRate);

            CaseNoKink(median, builder, failures);
            CaseCleanZeroWidth(median, builder, failures);
            CaseCarriagewaysPushedOutward(median, builder, failures);
            CaseTotalWidthReported(median, builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPROFILE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPROFILE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPROFILE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The width curve is continuous in its first derivative: sampled densely, the slope of
        /// the median edge never jumps between adjacent stations -- a kink would show as a slope spike of
        /// order the slope itself, so a small tolerance catches it while passing the smooth curve. </summary>
        private static void CaseNoKink(MedianProfile median, StringBuilder builder, List<string> failures)
        {
            float prevSlope = median.width.WidthSlopeAt(0f, SampleStep);
            float maxJump = 0f;
            float atDist = 0f;
            for (float d = SampleStep; d <= RoadLength; d += SampleStep)
            {
                float slope = median.width.WidthSlopeAt(d, SampleStep);
                float jump = Mathf.Abs(slope - prevSlope);
                if (jump > maxJump) { maxJump = jump; atDist = d; }
                prevSlope = slope;
            }
            Expect(maxJump < 0.25f,
                "no-kink: slope jumps " + F(maxJump) + " at " + F(atDist) + " m (a crease in the edge)",
                failures);
            builder.Append("RCMPROFILE ").Append("no-kink".PadRight(20))
                   .Append("max slope jump ").Append(F(maxJump)).Append(" per ").Append(F(SampleStep))
                   .Append(" m\n");
        }

        /// <summary> Zero width degrades cleanly: the half-width is never negative anywhere, the element
        /// emits nothing at the two collapse points (start and end), and it DOES emit at the widest point --
        /// so the sweep draws a real ribbon in the middle and no degenerate face at the ends. </summary>
        private static void CaseCleanZeroWidth(MedianProfile median, StringBuilder builder, List<string> failures)
        {
            bool everNegative = false;
            float minH = float.MaxValue;
            for (float d = 0f; d <= RoadLength; d += SampleStep)
            {
                float h = median.width.HalfWidthAt(d);
                if (h < 0f) { everNegative = true; }
                if (h < minH) { minH = h; }
            }
            Expect(!everNegative, "zero-width: the half-width went negative (a mirrored/inverted ribbon)", failures);
            Expect(!median.width.EmitsAt(MedianStart),
                "zero-width: the element still emits at its start collapse point", failures);
            Expect(!median.width.EmitsAt(MedianEnd),
                "zero-width: the element still emits at its end collapse point", failures);
            float mid = (MedianStart + MedianEnd) * 0.5f;
            Expect(median.width.EmitsAt(mid),
                "zero-width: the element emits nothing at its widest point", failures);
            builder.Append("RCMPROFILE ").Append("clean-zero-width".PadRight(20))
                   .Append("min half-width ").Append(F(minH)).Append(" m, emits mid ")
                   .Append(median.width.EmitsAt(mid) ? "yes" : "no").Append("\n");
        }

        /// <summary> A widening median pushes the carriageways OUTWARD: the carriageway edge offset never
        /// decreases as the median opens across its nose, and at the widest point it stands exactly the
        /// median half-width beyond where it sits when the median is absent. </summary>
        private static void CaseCarriagewaysPushedOutward(MedianProfile median, StringBuilder builder,
            List<string> failures)
        {
            float mid = (MedianStart + MedianEnd) * 0.5f;
            float prev = median.width.CarriagewayEdgeOffsetAt(0f, BaseHalfCarriageway);
            bool monotonicUpToPeak = true;
            for (float d = SampleStep; d <= mid; d += SampleStep)
            {
                float edge = median.width.CarriagewayEdgeOffsetAt(d, BaseHalfCarriageway);
                if (edge + 1e-4f < prev) { monotonicUpToPeak = false; }
                prev = edge;
            }
            Expect(monotonicUpToPeak,
                "outward-push: the carriageway edge moved INWARD as the median widened", failures);
            float atStart = median.width.CarriagewayEdgeOffsetAt(MedianStart, BaseHalfCarriageway);
            float atPeak = median.width.CarriagewayEdgeOffsetAt(mid, BaseHalfCarriageway);
            Expect(Approx(atStart, BaseHalfCarriageway, 1e-3f),
                "outward-push: edge at the collapsed start " + F(atStart) + " is not the base half-carriageway "
                + F(BaseHalfCarriageway), failures);
            Expect(Approx(atPeak, BaseHalfCarriageway + MedianHalfWidth, 1e-3f),
                "outward-push: edge at the widest point " + F(atPeak) + " is not base + median half-width "
                + F(BaseHalfCarriageway + MedianHalfWidth), failures);
            builder.Append("RCMPROFILE ").Append("outward-push".PadRight(20))
                   .Append("edge ").Append(F(atStart)).Append(" -> ").Append(F(atPeak)).Append(" m\n");
        }

        /// <summary> The element reports its contribution to TOTAL road width so RM-38's falloff widens the
        /// cut with it: the contribution equals the full width (both sides) at every station, and at the
        /// widest point it is exactly twice the authored median half-width. </summary>
        private static void CaseTotalWidthReported(MedianProfile median, StringBuilder builder,
            List<string> failures)
        {
            bool matchesFullWidth = true;
            for (float d = 0f; d <= RoadLength; d += SampleStep)
            {
                if (!Approx(median.width.TotalWidthContributionAt(d), median.width.FullWidthAt(d), 1e-4f))
                {
                    matchesFullWidth = false;
                }
            }
            Expect(matchesFullWidth,
                "total-width: the reported contribution does not equal the element's full width", failures);
            float mid = (MedianStart + MedianEnd) * 0.5f;
            float peak = median.width.TotalWidthContributionAt(mid);
            Expect(Approx(peak, MedianHalfWidth * 2f, 1e-3f),
                "total-width: peak contribution " + F(peak) + " is not twice the authored half-width "
                + F(MedianHalfWidth * 2f), failures);
            builder.Append("RCMPROFILE ").Append("total-width".PadRight(20))
                   .Append("peak contribution ").Append(F(peak)).Append(" m\n");
        }

        /// <summary> Two bakes are identical: the same authored median sampled twice yields a byte-identical
        /// dump, the determinism the whole cross-section system rests on (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            string a = Sample(MedianProfile.Planted(MedianStart, MedianEnd, MedianHalfWidth, NoseTaperRate));
            string b = Sample(MedianProfile.Planted(MedianStart, MedianEnd, MedianHalfWidth, NoseTaperRate));
            Expect(a == b, "deterministic: two bakes of the same median differ", failures);
            builder.Append("RCMPROFILE ").Append("deterministic".PadRight(20))
                   .Append(a == b ? "identical" : "DIFFER").Append("\n");
        }

        /// <summary> A canonical, culture-invariant dump of the median's width and edge at every station,
        /// so two bakes can be compared byte for byte. </summary>
        private static string Sample(MedianProfile median)
        {
            StringBuilder sb = new StringBuilder();
            for (float d = 0f; d <= RoadLength; d += SampleStep)
            {
                sb.Append(F(d)).Append('|').Append(F(median.width.HalfWidthAt(d))).Append('|')
                  .Append(median.width.EmitsAt(d) ? '1' : '0').Append('|')
                  .Append(F(median.width.CarriagewayEdgeOffsetAt(d, BaseHalfCarriageway))).Append('\n');
            }
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
