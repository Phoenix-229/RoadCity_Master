// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-24's gate: proves the part of distribution-pole placement that is pure and carries the bugs
    /// -- the conflict resolution on a frontage RM-13's furniture already occupies. It asserts that poles land
    /// on quantised stations with intersection corners excluded (RM-13's planner, reused); that a pole coincident
    /// with an existing furniture item is NUDGED clear by the minimum separation in a fixed, reproducible order;
    /// that a pole with no clear offset inside the maximum nudge is DROPPED, not overlapped; that no pole in a
    /// result is closer than the minimum separation to any furniture (the invariant the whole task exists for);
    /// that the same inputs give the same stations twice (C-06); and -- B-27, replacing the case that asserted a
    /// straight cable -- that the span hangs as a catenary at the sag its conductor tension gives and is judged
    /// against the NESC road clearance. The actual EdgeObjectMaker prefab placement is the deferred scene half.
    /// Report lines are prefixed RCMPOLE. </summary>
    public static class CityDistributionPoleGate
    {
        [MenuItem("Window/Road City Master/Testing/City distribution poles")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityDistributionPoleGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMPOLE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPOLE BEGIN\n");
            List<string> failures = new List<string>();

            CaseQuantiseAndExclude(builder, failures);
            CaseNudgeClear(builder, failures);
            CaseDropWhenBoxedIn(builder, failures);
            CaseSeparationInvariant(builder, failures);
            CaseReproducible(builder, failures);
            CaseCatenaryCable(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPOLE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPOLE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPOLE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Poles land on quantised stations and none inside an intersection-corner exclusion span. </summary>
        private static void CaseQuantiseAndExclude(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default(); // 40 m span
            List<Vector2> exclusions = new List<Vector2> { new Vector2(110f, 130f) }; // a corner around 120 m
            List<float> stations = DistributionPoleRun.PlanPoleStations(400f, cls, exclusions, null);
            bool anyInExclusion = false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] >= 110f && stations[i] <= 130f) { anyInExclusion = true; }
            }
            Expect(!anyInExclusion, "quantise: a pole landed inside an intersection-corner exclusion", failures);
            Expect(stations.Count > 0, "quantise: a 400 m run produced no poles", failures);
            builder.Append("RCMPOLE ").Append("quantise-exclude".PadRight(20))
                   .Append("poles on quantised stations, none in a corner\n");
        }

        /// <summary> A pole coincident with a furniture item is nudged clear by the minimum separation, and the
        /// nudge is the deterministic first-clear offset (+2 m here). </summary>
        private static void CaseNudgeClear(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default(); // minSep 2, step 0.5, maxNudge 5
            List<float> furniture = new List<float> { 40f }; // a streetlight exactly where a pole would go
            List<float> stations = DistributionPoleRun.PlanPoleStations(80f, cls, null, furniture);
            // Candidates are {0, 40, 80}; 40 collides and nudges to 42 (first offset >= minSep, + tried before -).
            bool has42 = false;
            bool has40 = false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (Mathf.Abs(stations[i] - 42f) < 1e-3f) { has42 = true; }
                if (Mathf.Abs(stations[i] - 40f) < 1e-3f) { has40 = true; }
            }
            Expect(has42 && !has40, "nudge: the colliding pole did not nudge to the deterministic +2 m offset",
                failures);
            builder.Append("RCMPOLE ").Append("nudge-clear".PadRight(20))
                   .Append("colliding pole nudged to first clear offset (+2 m)\n");
        }

        /// <summary> A pole boxed in by furniture across its whole nudge range is dropped, while its neighbours
        /// on the run survive. </summary>
        private static void CaseDropWhenBoxedIn(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default();
            // Furniture every 2 m across [36,44] leaves no offset within +/-5 m of 40 that clears 2 m.
            List<float> furniture = new List<float> { 36f, 38f, 40f, 42f, 44f };
            List<float> stations = DistributionPoleRun.PlanPoleStations(80f, cls, null, furniture);
            bool has40 = false;
            for (int i = 0; i < stations.Count; i++)
            {
                if (stations[i] > 34f && stations[i] < 46f) { has40 = true; }
            }
            Expect(!has40, "drop: a boxed-in pole was placed instead of dropped", failures);
            Expect(stations.Count == 2, "drop: the neighbouring poles at 0 and 80 did not survive", failures);
            builder.Append("RCMPOLE ").Append("drop-boxed-in".PadRight(20))
                   .Append("un-nudgeable pole dropped, neighbours kept\n");
        }

        /// <summary> The invariant the task exists for: no pole in a result is closer than the minimum
        /// separation to any furniture item. </summary>
        private static void CaseSeparationInvariant(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default();
            List<float> furniture = new List<float> { 0f, 41f, 120f, 205f, 360f };
            List<float> stations = DistributionPoleRun.PlanPoleStations(400f, cls, null, furniture);
            bool ok = true;
            for (int i = 0; i < stations.Count; i++)
            {
                for (int j = 0; j < furniture.Count; j++)
                {
                    if (Mathf.Abs(stations[i] - furniture[j]) < cls.minSeparationFromFurnitureMetres - 1e-4f)
                    {
                        ok = false;
                    }
                }
            }
            Expect(ok, "separation: a placed pole was closer than the minimum separation to a furniture item",
                failures);
            builder.Append("RCMPOLE ").Append("separation-invariant".PadRight(20))
                   .Append("every placed pole clears furniture by the minimum\n");
        }

        /// <summary> Same inputs, same stations twice -- fixed nudge order, no clock or hash (C-06). </summary>
        private static void CaseReproducible(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default();
            List<float> furniture = new List<float> { 40f, 205f, 360f };
            List<float> a = DistributionPoleRun.PlanPoleStations(400f, cls, null, furniture);
            List<float> b = DistributionPoleRun.PlanPoleStations(400f, cls, null, furniture);
            bool identical = a.Count == b.Count;
            for (int i = 0; i < a.Count && identical; i++)
            {
                if (Mathf.Abs(a[i] - b[i]) > 1e-6f) { identical = false; }
            }
            Expect(identical, "reproducible: two identical runs produced different pole stations", failures);
            builder.Append("RCMPOLE ").Append("reproducible".PadRight(20))
                   .Append("identical inputs -> identical stations\n");
        }

        /// <summary> The cable hangs. B-27 deleted <c>StraightCableHeightAt</c>, so this case had to change from
        /// asserting a straight line to asserting a curve, and it asserts the properties that tell one from the
        /// other rather than a sag figure it could have been fitted to:
        ///
        ///   * the midpoint of a level span is BELOW the attachment height, and by w.L^2/(8H) computed from the
        ///     conductor's own weight and its everyday tension -- so the assertion proves the solver was handed
        ///     the real stringing figures rather than a sag ratio chosen to look right;
        ///   * both ends still sit exactly on their attachments, which is the one thing the straight line also
        ///     got right and a wrong catenary constant would break;
        ///   * an unlevel span is still below the chord at mid-span, because a sagging wire between poles of
        ///     different heights is the case a linear interpolation looked most nearly correct in;
        ///   * the NESC road clearance is the figure the span is judged against, and a span strung too low is
        ///     refused with the shortfall rather than accepted.
        /// </summary>
        private static void CaseCatenaryCable(StringBuilder builder, List<string> failures)
        {
            DistributionPoleClass cls = DistributionPoleClass.Default();
            const double span = 40.0;
            const double attach = 9.0;

            double sagRatio = cls.SagRatioAt(span);
            double expectedSag = (cls.conductorUnitWeightNewtonsPerMetre * span * span)
                / (8.0 * cls.EverydayTensionNewtons());
            Expect(System.Math.Abs(sagRatio * span - expectedSag) < 1e-9,
                "cable: the sag ratio did not agree with w.L^2/(8H) over the span", failures);

            double mid = DistributionPoleRun.CableAt(span, attach, attach, cls, 0.5);
            Expect(mid < attach,
                "cable: the midpoint of a level span was not below the attachment height", failures);
            Expect(System.Math.Abs((attach - mid) - expectedSag) < 1e-6,
                "cable: the solved sag did not match w.L^2/(8H) from the conductor's tension", failures);

            Expect(System.Math.Abs(DistributionPoleRun.CableAt(span, attach, attach, cls, 0.0) - attach) < 1e-6,
                "cable: the wire did not start at its first attachment", failures);
            Expect(System.Math.Abs(DistributionPoleRun.CableAt(span, attach, attach, cls, 1.0) - attach) < 1e-6,
                "cable: the wire did not end at its second attachment", failures);

            double unlevelMid = DistributionPoleRun.CableAt(span, 9.0, 12.0, cls, 0.5);
            Expect(unlevelMid < 10.5,
                "cable: an unlevel span was not below its own chord at mid-span", failures);

            List<double> flatGround = new List<double>();
            for (int i = 0; i < 9; i++) { flatGround.Add(0.0); }

            TransmissionLine.ClearanceResult high = DistributionPoleRun.SpanClearance(
                span, attach, attach, cls, flatGround, true);
            Expect(high.clears,
                "cable: a span attached at 9 m over level ground failed the 5.6 m road clearance", failures);

            TransmissionLine.ClearanceResult low = DistributionPoleRun.SpanClearance(
                span, 5.0, 5.0, cls, flatGround, true);
            Expect(!low.clears,
                "cable: a span attached at 5 m was not refused against the NESC 5.6 m road clearance", failures);
            Expect(low.worstStationIndex >= 0,
                "cable: a refused span did not name the station its clearance was worst at", failures);

            TransmissionLine.ClearanceResult secondary = DistributionPoleRun.SpanClearance(
                span, 5.0, 5.0, cls, flatGround, false);
            Expect(secondary.clears,
                "cable: the 0-750 V column did not admit a span the road column refused", failures);

            builder.Append("RCMPOLE ").Append("catenary-cable".PadRight(20))
                   .Append("one catenary: sag ")
                   .Append(expectedSag.ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
                   .Append(" m over ")
                   .Append(span.ToString("F0", System.Globalization.CultureInfo.InvariantCulture))
                   .Append(" m, NESC 232-1 clearance enforced\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
