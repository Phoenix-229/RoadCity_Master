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
    /// <summary> RM-39's gate: proves the cut-slope march without a scene. It asserts that the slope rise follows
    /// the authored run-per-rise ratio; that the outward march finds the catch point (daylight line) where the
    /// rising slope meets natural ground, and that the width is whatever the ratio needs -- a lower hill catches
    /// sooner than a higher one, never a fixed apron; that a slope which would run past the authored maximum height
    /// stops UNCAUGHT and reports the station rather than reshaping the hillside; that the two sides of one road
    /// are solved independently (one catches while the other is handed back); that the shared RM-34 end taper walks
    /// the catch point in so the cut daylights out of the ground instead of leaving a wall of earth; and that
    /// everything is bit-identical on repeat (C-06). The swept mesh, live terrain sampling and bench/retain hand-off
    /// are the deferred half. Report lines are prefixed RCMCUT. </summary>
    public static class CityCutSlopeGate
    {
        [MenuItem("Window/Road City Master/Testing/City cut slope (daylight line)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityCutSlopeGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMCUT report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCUT BEGIN\n");
            List<string> failures = new List<string>();

            CaseRiseFollowsRatio(builder, failures);
            CaseCatchIsDaylightLine(builder, failures);
            CaseSteepHillExceedsMax(builder, failures);
            CaseTwoSidesIndependent(builder, failures);
            CaseEndTaperDaylights(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCUT FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCUT result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCUT END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The slope rise follows the authored run-per-rise ratio, and the inverse recovers the run. </summary>
        private static void CaseRiseFollowsRatio(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass cls = CutSlopeClass.Default(); // 1.5 run per rise
            Expect(Math.Abs(CutSlope.RiseForRunMetres(3.0, cls) - 2.0) < 1e-9,
                "ratio: 3 m of run at 1.5:1 did not rise 2 m", failures);
            Expect(Math.Abs(CutSlope.RunForRiseMetres(2.0, cls) - 3.0) < 1e-9,
                "ratio: the inverse did not recover the run for a 2 m rise", failures);
            CutSlopeClass flat = new CutSlopeClass(); flat.slopeRunPerRise = 0f;
            Expect(Math.Abs(CutSlope.RiseForRunMetres(3.0, flat)) < 1e-12,
                "ratio: a non-positive ratio did not yield zero rise", failures);
            builder.Append("RCMCUT ").Append("rise-from-ratio".PadRight(22))
                   .Append("slope rise = run / (run per rise)\n");
        }

        /// <summary> The march finds the catch point, and the cut width is whatever the ratio needs: a lower hill
        /// catches sooner (narrower cut) than a higher one. Never a fixed apron. </summary>
        private static void CaseCatchIsDaylightLine(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass cls = CutSlopeClass.Default(); // ratio 1.5, step 0.5
            // Band edge at elevation 10, a flat hillside 2 m above it -> catch when rise reaches 2 -> run 3.0.
            double[] lowHill = Fill(10, 12.0);
            CutSlope.CatchResult low = CutSlope.MarchToCatch(10.0, lowHill, cls);
            Expect(low.caught && Math.Abs(low.catchRunMetres - 3.0) < 1e-9
                   && Math.Abs(low.catchRiseMetres - 2.0) < 1e-9,
                "catch: a 2 m hill did not catch at 3 m of run / 2 m of rise", failures);

            // A higher hillside (4 m above) needs 6 m of run -> a wider cut, proving width is not fixed.
            double[] highHill = Fill(20, 14.0);
            CutSlope.CatchResult high = CutSlope.MarchToCatch(10.0, highHill, cls);
            Expect(high.caught && Math.Abs(high.catchRunMetres - 6.0) < 1e-9,
                "catch: a taller hill did not produce a wider cut (fixed apron?)", failures);
            Expect(high.catchRunMetres > low.catchRunMetres,
                "catch: the taller hill did not catch farther out than the lower one", failures);
            builder.Append("RCMCUT ").Append("daylight-line".PadRight(22))
                   .Append("catch point found; width tracks the hill, not fixed\n");
        }

        /// <summary> A slope that would run past the authored maximum height stops UNCAUGHT and reports the station,
        /// rather than emitting an unbounded slope up the hillside. </summary>
        private static void CaseSteepHillExceedsMax(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass steep = new CutSlopeClass();
            steep.slopeRunPerRise = 1.5f;
            steep.stepMetres = 1f;
            steep.maxSlopeHeightMetres = 5f;
            // A wall of terrain far above the road: the slope never catches, so the ceiling must stop it.
            double[] cliff = Fill(20, 1000.0);
            CutSlope.CatchResult r = CutSlope.MarchToCatch(10.0, cliff, steep);
            // rise > 5 first at run 8 (rise 5.333), which is march step index 7 (run = 8 * 1 m).
            Expect(!r.caught && r.exceededMax && r.catchStepIndex == 7
                   && Math.Abs(r.catchRiseMetres - 5.0) < 1e-9 && Math.Abs(r.catchRunMetres - 8.0) < 1e-9,
                "max: an unbounded slope was not stopped and reported at the authored ceiling", failures);
            builder.Append("RCMCUT ").Append("max-height-refuses".PadRight(22))
                   .Append("past max height: stop uncaught, report the station\n");
        }

        /// <summary> The two sides of one road are independent problems: a valley gives one side a gentle catch and
        /// the other a slope that runs past the ceiling. </summary>
        private static void CaseTwoSidesIndependent(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass cls = new CutSlopeClass();
            cls.slopeRunPerRise = 1.5f;
            cls.stepMetres = 1f;
            cls.maxSlopeHeightMetres = 5f;
            double[] gentle = Fill(20, 12.0);    // 2 m above -> catches
            double[] cliff = Fill(20, 1000.0);   // exceeds the ceiling
            CutSlope.CatchResult left = CutSlope.MarchToCatch(10.0, gentle, cls);
            CutSlope.CatchResult right = CutSlope.MarchToCatch(10.0, cliff, cls);
            Expect(left.caught && !left.exceededMax, "sides: the gentle side did not catch", failures);
            Expect(!right.caught && right.exceededMax, "sides: the cliff side was not handed back", failures);
            builder.Append("RCMCUT ").Append("two-sides".PadRight(22))
                   .Append("each side solved on its own ground\n");
        }

        /// <summary> The shared RM-34 end taper walks the slope height to zero at the end so the cut daylights out
        /// of the ground rather than leaving a wall of earth across the road. </summary>
        private static void CaseEndTaperDaylights(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass cls = CutSlopeClass.Default(); // end taper 5 m
            Expect(Math.Abs(CutSlope.TaperedSlopeHeightMetres(4.0, 0.0, cls)) < 1e-9,
                "taper: the slope height was not zero at the very end", failures);
            Expect(Math.Abs(CutSlope.TaperedSlopeHeightMetres(4.0, 2.5, cls) - 2.0) < 1e-9,
                "taper: the slope height was not half-way up at half the transition", failures);
            Expect(Math.Abs(CutSlope.TaperedSlopeHeightMetres(4.0, 5.0, cls) - 4.0) < 1e-9,
                "taper: the slope height did not reach full value at the transition length", failures);
            Expect(Math.Abs(CutSlope.TaperedSlopeHeightMetres(4.0, 20.0, cls) - 4.0) < 1e-9,
                "taper: the slope height did not hold full value in the interior", failures);
            // The very same shared operation the retaining face uses (RM-34) -> no second taper.
            Expect(Math.Abs(EndTaper.TaperedDimensionMetres(4.0, 2.5, 5.0) - 2.0) < 1e-9,
                "taper: the shared end-taper did not agree with the cut-slope taper", failures);
            builder.Append("RCMCUT ").Append("end-taper".PadRight(22))
                   .Append("shared taper walks the catch point in at the ends\n");
        }

        /// <summary> The march is bit-identical on repeat -- pure arithmetic on terrain sampled in order (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            CutSlopeClass cls = CutSlopeClass.Default();
            double[] hill = { 11.0, 11.2, 11.5, 12.0, 12.4, 12.9, 13.5, 14.0 };
            CutSlope.CatchResult a = CutSlope.MarchToCatch(10.0, hill, cls);
            CutSlope.CatchResult b = CutSlope.MarchToCatch(10.0, hill, cls);
            Expect(a.caught == b.caught && a.catchStepIndex == b.catchStepIndex
                   && a.catchRunMetres == b.catchRunMetres && a.catchRiseMetres == b.catchRiseMetres,
                "determinism: two identical cut-slope marches differed", failures);
            builder.Append("RCMCUT ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical catch point\n");
        }

        /// <summary> A flat natural-terrain sample of a given elevation, length steps. </summary>
        private static double[] Fill(int steps, double elevation)
        {
            double[] a = new double[steps];
            for (int i = 0; i < steps; i++) { a[i] = elevation; }
            return a;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
