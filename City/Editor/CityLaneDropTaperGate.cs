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
    /// <summary> RM-27's gate: proves the lane-drop taper maths and the fit/flag decision without a scene. It
    /// asserts that the taper length matches the metric MUTCD/AASHTO statement exactly on both sides of the
    /// ~70 km/h breakpoint (L = W·S²/155 at/below, L = 0.62·W·S above); that a wider drop or a higher speed gives a
    /// longer taper; that the width narrows LINEARLY from full to full-minus-dropped over the taper and clamps
    /// outside it (the width-over-distance curve RM-31's sweep consumes); that a taper too long for its approach is
    /// SHORTENED to the available length and FLAGGED rather than silently kinked; and that the computation is
    /// bit-identical on repeat (C-06). Writing that curve onto the approach and running the corner builder is the
    /// deferred half. Report lines are prefixed RCMTAPER. </summary>
    public static class CityLaneDropTaperGate
    {
        [MenuItem("Window/Road City Master/Testing/City lane-drop taper")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityLaneDropTaperGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMTAPER report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMTAPER BEGIN\n");
            List<string> failures = new List<string>();

            CaseLowSpeedFormula(builder, failures);
            CaseHighSpeedFormula(builder, failures);
            CaseLongerWithWidthAndSpeed(builder, failures);
            CaseWidthNarrowsLinearly(builder, failures);
            CaseFitAndFlag(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMTAPER FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMTAPER result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMTAPER END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> At or below the breakpoint the taper length is L = W·S²/155 exactly; nothing dropped gives no
        /// taper. </summary>
        private static void CaseLowSpeedFormula(StringBuilder builder, List<string> failures)
        {
            const double w = 3.5;  // one lane width dropped
            const double s = 50.0; // below the 70 km/h breakpoint
            double expected = (w * s * s) / 155.0;
            double actual = LaneDropTaper.TaperLengthMetres(w, s);
            Expect(Math.Abs(actual - expected) < 1e-9,
                "low-speed: taper length did not equal W·S²/155 below the breakpoint", failures);
            Expect(Math.Abs(LaneDropTaper.TaperLengthMetres(0.0, s)) < 1e-12,
                "low-speed: dropping zero width still produced a taper", failures);
            builder.Append("RCMTAPER ").Append("low-speed-formula".PadRight(20))
                   .Append("L = W·S²/155 at/below the ~70 km/h breakpoint\n");
        }

        /// <summary> Above the breakpoint the taper length is L = 0.62·W·S exactly. </summary>
        private static void CaseHighSpeedFormula(StringBuilder builder, List<string> failures)
        {
            const double w = 3.5;
            const double s = 100.0; // above the 70 km/h breakpoint
            double expected = 0.62 * w * s;
            double actual = LaneDropTaper.TaperLengthMetres(w, s);
            Expect(Math.Abs(actual - expected) < 1e-9,
                "high-speed: taper length did not equal 0.62·W·S above the breakpoint", failures);
            // The exact breakpoint uses the low-speed statement (boundary is inclusive).
            double atBreak = LaneDropTaper.TaperLengthMetres(w, LaneDropTaper.LowSpeedThresholdKmh);
            double lowAtBreak = (w * LaneDropTaper.LowSpeedThresholdKmh * LaneDropTaper.LowSpeedThresholdKmh) / 155.0;
            Expect(Math.Abs(atBreak - lowAtBreak) < 1e-9,
                "high-speed: the exact breakpoint did not use the low-speed statement", failures);
            builder.Append("RCMTAPER ").Append("high-speed-formula".PadRight(20))
                   .Append("L = 0.62·W·S above the breakpoint\n");
        }

        /// <summary> More width dropped, or a higher design speed, both lengthen the taper. </summary>
        private static void CaseLongerWithWidthAndSpeed(StringBuilder builder, List<string> failures)
        {
            Expect(LaneDropTaper.TaperLengthMetres(7.0, 50.0) > LaneDropTaper.TaperLengthMetres(3.5, 50.0),
                "monotonic: dropping more width did not lengthen the taper", failures);
            Expect(LaneDropTaper.TaperLengthMetres(3.5, 60.0) > LaneDropTaper.TaperLengthMetres(3.5, 40.0),
                "monotonic: a higher design speed did not lengthen the taper", failures);
            builder.Append("RCMTAPER ").Append("monotonic".PadRight(20))
                   .Append("wider drop / faster speed -> longer taper\n");
        }

        /// <summary> The width narrows linearly from full to full-minus-dropped over the taper, and clamps to those
        /// values outside [0, L]. </summary>
        private static void CaseWidthNarrowsLinearly(StringBuilder builder, List<string> failures)
        {
            const double full = 14.0;
            const double dropped = 3.5;
            double length = LaneDropTaper.TaperLengthMetres(dropped, 50.0);
            Expect(Math.Abs(LaneDropTaper.TaperedWidthAt(full, dropped, length, 0.0) - full) < 1e-9,
                "linear: width at the taper start was not the full width", failures);
            Expect(Math.Abs(LaneDropTaper.TaperedWidthAt(full, dropped, length, length) - (full - dropped)) < 1e-9,
                "linear: width at the taper end was not full minus the dropped width", failures);
            Expect(Math.Abs(LaneDropTaper.TaperedWidthAt(full, dropped, length, length * 0.5)
                            - (full - dropped * 0.5)) < 1e-9,
                "linear: width at the taper midpoint was not the linear midpoint", failures);
            Expect(Math.Abs(LaneDropTaper.TaperedWidthAt(full, dropped, length, -5.0) - full) < 1e-9,
                "linear: width before the taper was not clamped to the full width", failures);
            Expect(Math.Abs(LaneDropTaper.TaperedWidthAt(full, dropped, length, length + 5.0)
                            - (full - dropped)) < 1e-9,
                "linear: width past the taper was not clamped to the narrowed width", failures);
            builder.Append("RCMTAPER ").Append("width-linear".PadRight(20))
                   .Append("full -> narrowed linearly over L, clamped outside\n");
        }

        /// <summary> A taper that fits its approach is used unchanged; one too long is shortened to the available
        /// length and flagged, never silently kinked. </summary>
        private static void CaseFitAndFlag(StringBuilder builder, List<string> failures)
        {
            double ideal = LaneDropTaper.TaperLengthMetres(3.5, 50.0); // ~56.5 m
            LaneDropTaper.TaperFit roomy = LaneDropTaper.FitToApproach(3.5, 50.0, ideal + 40.0);
            Expect(roomy.fits && Math.Abs(roomy.taperLengthMetres - ideal) < 1e-9,
                "fit: a taper with room to spare was altered or flagged", failures);

            LaneDropTaper.TaperFit cramped = LaneDropTaper.FitToApproach(3.5, 50.0, 20.0);
            Expect(!cramped.fits, "fit: a taper longer than its approach was not flagged", failures);
            Expect(Math.Abs(cramped.taperLengthMetres - 20.0) < 1e-9,
                "fit: the flagged taper was not shortened to the available approach length", failures);
            Expect(cramped.idealLengthMetres > cramped.availableLengthMetres,
                "fit: the flagged taper did not record that the ideal exceeded the available length", failures);
            builder.Append("RCMTAPER ").Append("fit-and-flag".PadRight(20))
                   .Append("fits unchanged, or shortened to approach and flagged\n");
        }

        /// <summary> The computation is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            double a = LaneDropTaper.TaperLengthMetres(3.5, 55.0);
            double b = LaneDropTaper.TaperLengthMetres(3.5, 55.0);
            Expect(a == b, "determinism: two identical taper computations produced different lengths", failures);
            builder.Append("RCMTAPER ").Append("determinism".PadRight(20))
                   .Append("identical inputs -> bit-identical taper length\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
