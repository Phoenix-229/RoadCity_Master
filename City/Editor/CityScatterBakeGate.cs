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
    /// <summary> RM-47's gate: proves the baked scatter pass without a scene. It asserts that placement is seeded from
    /// the city seed AND the element's identity (so a street's row is identical whether it is scattered first or last),
    /// that a shared global stream -- the thing RM-47 forbids -- WOULD have differed by visitation order; that linear
    /// scatter honours a metres spacing with fractional jitter; that area scatter honours instances-per-100m^2 with a
    /// minimum-separation rejection; that a geometry keep-out places nothing inside it while preserving survivor order;
    /// that preset thinning drops from the ordered list deterministically and reversibly (not a re-scatter); and that
    /// the whole pass is bit-identical on repeat (C-06). The bake-to-streaming-cell placement, prefab resolution, and
    /// the true geometry exclusion sources are the deferred half. Report lines are prefixed RCMSCATTER. </summary>
    public static class CityScatterBakeGate
    {
        [MenuItem("Window/Road City Master/Testing/City scatter bake (per-element seeding)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityScatterBakeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMSCATTER report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSCATTER BEGIN\n");
            List<string> failures = new List<string>();

            CaseOrderIndependentSeeding(builder, failures);
            CaseGlobalStreamWouldDiffer(builder, failures);
            CaseLinearSpacingAndJitter(builder, failures);
            CaseAreaDensityAndMinSeparation(builder, failures);
            CaseExclusionByGeometry(builder, failures);
            CaseThinningDropsFromList(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSCATTER FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSCATTER result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSCATTER END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A street's row is identical whether it is scattered first or last: the seed is per element, not a
        /// shared stream. Scatter A, then B, then A again -- the two A rows agree. </summary>
        private static void CaseOrderIndependentSeeding(StringBuilder builder, List<string> failures)
        {
            const ulong seed = 20260829UL;
            IList<double> a1 = ScatterBake.ScatterAlongLine(seed, "street.A", 120.0, 8.0, 0.3);
            IList<double> b = ScatterBake.ScatterAlongLine(seed, "street.B", 90.0, 6.0, 0.25);
            IList<double> a2 = ScatterBake.ScatterAlongLine(seed, "street.A", 120.0, 8.0, 0.3);
            Expect(b.Count > 0, "order: element B produced no instances (setup)", failures);
            Expect(ListsEqual(a1, a2), "order: the same street scattered before and after another disagreed", failures);
            builder.Append("RCMSCATTER ").Append("order-independent".PadRight(22))
                   .Append("same element -> same row regardless of scatter order\n");
        }

        /// <summary> The thing RM-47 forbids: a single shared stream advanced across elements IS order-dependent, so
        /// element B's positions would change with visitation order. We show the shared stream differs while the
        /// per-element bake does not -- the reason the rule exists. </summary>
        private static void CaseGlobalStreamWouldDiffer(StringBuilder builder, List<string> failures)
        {
            const ulong seed = 777UL;
            // A shared stream: draw A's 3 values then B's 3 (order 1), versus B's 3 then A's 3 (order 2).
            DeterministicRandom s1 = new DeterministicRandom(seed);
            GlobalDraws(ref s1, 3);                  // element A consumes the stream first
            List<double> bAfterA = GlobalDraws(ref s1, 3);
            DeterministicRandom s2 = new DeterministicRandom(seed);
            List<double> bFirst = GlobalDraws(ref s2, 3);
            bool sharedDiffers = !ListsEqual(bAfterA, bFirst);
            Expect(sharedDiffers, "global: a shared stream did NOT differ by order (the forbidden design should)", failures);

            // The per-element bake for B is identical in both orders (this is the fix).
            IList<double> perElemBefore = ScatterBake.ScatterAlongLine(seed, "street.B", 60.0, 10.0, 0.2);
            IList<double> unusedA = ScatterBake.ScatterAlongLine(seed, "street.A", 200.0, 5.0, 0.2);
            IList<double> perElemAfter = ScatterBake.ScatterAlongLine(seed, "street.B", 60.0, 10.0, 0.2);
            Expect(unusedA.Count > 0, "global: element A produced no instances (setup)", failures);
            Expect(ListsEqual(perElemBefore, perElemAfter),
                "global: the per-element bake changed with order (it must not)", failures);
            builder.Append("RCMSCATTER ").Append("global-stream-differs".PadRight(22))
                   .Append("shared stream is order-dependent; per-element bake is not\n");
        }

        /// <summary> Linear scatter honours a metres spacing with jitter as a fraction of the spacing. </summary>
        private static void CaseLinearSpacingAndJitter(StringBuilder builder, List<string> failures)
        {
            // length 100, spacing 10, jitter 0 -> exact centres 5,15,...,95 (ten instances).
            IList<double> exact = ScatterBake.ScatterAlongLine(1UL, "s.exact", 100.0, 10.0, 0.0);
            Expect(exact.Count == 10, "linear: spacing 10 over length 100 did not give 10 instances", failures);
            bool centresOk = true;
            for (int i = 0; i < exact.Count; i++) { if (Math.Abs(exact[i] - (5.0 + i * 10.0)) > 1e-9) { centresOk = false; } }
            Expect(centresOk, "linear: zero-jitter instances were not at the spacing centres", failures);

            // jitter 0.2 -> each instance within +/- 2 m of its centre, none outside [0, length].
            IList<double> jit = ScatterBake.ScatterAlongLine(1UL, "s.jit", 100.0, 10.0, 0.2);
            Expect(jit.Count == 10, "linear: jittered pass changed the instance count", failures);
            bool boundOk = true;
            for (int i = 0; i < jit.Count; i++)
            {
                double centre = 5.0 + i * 10.0;
                if (Math.Abs(jit[i] - centre) > 2.0 + 1e-9) { boundOk = false; }
                if (jit[i] < 0.0 || jit[i] > 100.0) { boundOk = false; }
            }
            Expect(boundOk, "linear: a jittered instance left its +/-(fraction*spacing) window or the line", failures);
            builder.Append("RCMSCATTER ").Append("linear-spacing".PadRight(22))
                   .Append("spacing in metres; jitter a fraction of spacing\n");
        }

        /// <summary> Area scatter honours instances-per-100m^2 with a minimum-separation rejection. </summary>
        private static void CaseAreaDensityAndMinSeparation(StringBuilder builder, List<string> failures)
        {
            // 20x20 = 400 m^2 at 5 per 100 m^2 -> target 20; small min separation lets it reach target.
            IList<ScatterPoint> dense = ScatterBake.ScatterOverArea(9UL, "area.dense", 20.0, 20.0, 5.0, 0.5);
            Expect(dense.Count <= 20, "area: produced more than the target instance count", failures);
            Expect(dense.Count >= 15, "area: fell well short of the target with a small separation", failures);
            Expect(MinSeparationHonoured(dense, 0.5), "area: two instances were closer than the minimum separation", failures);

            // A large separation forces heavy rejection -- far fewer than target, still spaced.
            IList<ScatterPoint> sparse = ScatterBake.ScatterOverArea(9UL, "area.sparse", 20.0, 20.0, 5.0, 8.0);
            Expect(sparse.Count < dense.Count, "area: a larger minimum separation did not reduce the count", failures);
            Expect(MinSeparationHonoured(sparse, 8.0), "area: sparse pass violated the minimum separation", failures);

            IList<ScatterPoint> none = ScatterBake.ScatterOverArea(9UL, "area.none", 20.0, 20.0, 0.0, 1.0);
            Expect(none.Count == 0, "area: zero density produced instances", failures);
            builder.Append("RCMSCATTER ").Append("area-density".PadRight(22))
                   .Append("instances per 100 m^2 with min-separation rejection\n");
        }

        /// <summary> A geometry keep-out places nothing inside it and preserves the order of the survivors. </summary>
        private static void CaseExclusionByGeometry(StringBuilder builder, List<string> failures)
        {
            IList<ScatterPoint> all = ScatterBake.ScatterOverArea(3UL, "area.excl", 30.0, 30.0, 8.0, 0.5);
            List<ScatterRect> keepOut = new List<ScatterRect> { new ScatterRect(10.0, 10.0, 20.0, 20.0) };
            IList<ScatterPoint> kept = ScatterBake.FilterExcluded(all, keepOut);

            bool noneInside = true;
            for (int i = 0; i < kept.Count; i++) { if (keepOut[0].Contains(kept[i].x, kept[i].z)) { noneInside = false; } }
            Expect(noneInside, "exclusion: an instance remained inside the geometry keep-out", failures);
            Expect(kept.Count <= all.Count, "exclusion: filtering somehow added instances", failures);

            // survivors are a subsequence of the input (order preserved)
            bool ordered = true; int j = 0;
            for (int i = 0; i < all.Count && j < kept.Count; i++)
            {
                if (all[i].x == kept[j].x && all[i].z == kept[j].z) { j++; }
            }
            Expect(j == kept.Count && ordered, "exclusion: survivor order did not match the input order", failures);
            builder.Append("RCMSCATTER ").Append("geometry-exclusion".PadRight(22))
                   .Append("keep-out from geometry; survivors keep their order\n");
        }

        /// <summary> Preset thinning drops from the ordered list -- deterministic and reversible -- not a re-scatter. </summary>
        private static void CaseThinningDropsFromList(StringBuilder builder, List<string> failures)
        {
            List<int> ordered = new List<int>();
            for (int i = 0; i < 10; i++) { ordered.Add(i); }

            IList<int> half = ScatterBake.Thin(ordered, 0.5);
            Expect(half.Count == 5, "thin: keeping half of 10 did not give 5", failures);
            Expect(IsSubsequence(ordered, half), "thin: the kept set was not a subset of the original in order", failures);

            IList<int> third = ScatterBake.Thin(ordered, 0.3);
            Expect(third.Count == 3, "thin: keeping 0.3 of 10 did not give 3", failures);
            Expect(IsSubsequence(ordered, third), "thin: the 0.3 set was not an ordered subset", failures);

            IList<int> all = ScatterBake.Thin(ordered, 1.0);
            Expect(all.Count == 10 && IsSubsequence(ordered, all),
                "thin: keeping 1.0 did not restore every instance (reversibility)", failures);
            IList<int> gone = ScatterBake.Thin(ordered, 0.0);
            Expect(gone.Count == 0, "thin: keeping 0.0 left instances", failures);

            IList<int> half2 = ScatterBake.Thin(ordered, 0.5);
            Expect(ListsEqualInt(half, half2), "thin: two identical thinnings disagreed (determinism)", failures);
            builder.Append("RCMSCATTER ").Append("thin-drops-list".PadRight(22))
                   .Append("thinning drops from the ordered list, reversibly\n");
        }

        /// <summary> The whole pass is bit-identical on repeat (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            IList<double> l1 = ScatterBake.ScatterAlongLine(42UL, "det.line", 77.0, 9.0, 0.3);
            IList<double> l2 = ScatterBake.ScatterAlongLine(42UL, "det.line", 77.0, 9.0, 0.3);
            IList<ScatterPoint> a1 = ScatterBake.ScatterOverArea(42UL, "det.area", 25.0, 18.0, 6.0, 1.0);
            IList<ScatterPoint> a2 = ScatterBake.ScatterOverArea(42UL, "det.area", 25.0, 18.0, 6.0, 1.0);
            bool same = ListsEqual(l1, l2) && a1.Count == a2.Count;
            if (same) { for (int i = 0; i < a1.Count; i++) { if (a1[i].x != a2[i].x || a1[i].z != a2[i].z) { same = false; } } }
            Expect(same, "determinism: two identical scatter passes disagreed", failures);
            builder.Append("RCMSCATTER ").Append("determinism".PadRight(22))
                   .Append("identical seed + inputs -> identical instance list\n");
        }

        // --- helpers ---

        /// <summary> Draw n values from a shared stream (the forbidden global-stream pattern, used only to show it is
        /// order-dependent). </summary>
        private static List<double> GlobalDraws(ref DeterministicRandom rng, int n)
        {
            List<double> vals = new List<double>(n);
            for (int i = 0; i < n; i++) { vals.Add(rng.NextDouble()); }
            return vals;
        }

        private static bool MinSeparationHonoured(IList<ScatterPoint> pts, double minSep)
        {
            double min2 = minSep * minSep;
            for (int i = 0; i < pts.Count; i++)
            {
                for (int k = i + 1; k < pts.Count; k++)
                {
                    double dx = pts[i].x - pts[k].x;
                    double dz = pts[i].z - pts[k].z;
                    if (dx * dx + dz * dz < min2 - 1e-9) { return false; }
                }
            }
            return true;
        }

        private static bool ListsEqual(IList<double> a, IList<double> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++) { if (a[i] != b[i]) { return false; } }
            return true;
        }

        private static bool ListsEqualInt(IList<int> a, IList<int> b)
        {
            if (a.Count != b.Count) { return false; }
            for (int i = 0; i < a.Count; i++) { if (a[i] != b[i]) { return false; } }
            return true;
        }

        private static bool IsSubsequence(IList<int> whole, IList<int> part)
        {
            int j = 0;
            for (int i = 0; i < whole.Count && j < part.Count; i++)
            {
                if (whole[i] == part[j]) { j++; }
            }
            return j == part.Count;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
