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
    /// <summary> RM-23's gate: proves the half of the transmission line that carries real maths and can be
    /// checked without a scene -- the catenary. It asserts where the bugs in a cable actually live: that the
    /// solved constant a satisfies the sag equation s = a*(cosh(L/2a) - 1) exactly (not a parabola); that a
    /// level span bottoms out at mid-span with the authored sag; that an INCLINED span reproduces both tower
    /// heights and puts its low point toward the LOWER tower, not mid-span (the general two-point case the spec
    /// insists on); that a larger sag ratio gives a slacker cable (smaller a, deeper sag); that tower-type
    /// selection follows the AUTHORED turn-angle threshold; that a span failing ground clearance is FLAGGED
    /// with the offending value rather than passed; and that the whole solve is bit-identical on repeat (C-06).
    /// The mesh and prefab placement are the deferred scene half. Report lines are prefixed RCMLINE. </summary>
    public static class CityTransmissionLineGate
    {
        [MenuItem("Window/Road City Master/Testing/City transmission line (catenary)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityTransmissionLineGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMLINE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMLINE BEGIN\n");
            List<string> failures = new List<string>();

            CaseSagEquationHolds(builder, failures);
            CaseLevelSpanSymmetric(builder, failures);
            CaseInclinedSpanLowPointShifts(builder, failures);
            CaseSagRatioSlackens(builder, failures);
            CaseTowerTypeByAngle(builder, failures);
            CaseClearanceFlagged(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMLINE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMLINE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMLINE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The solved constant a satisfies the real catenary sag equation, proving the solver hit the
        /// actual cosh curve and not a parabolic approximation. </summary>
        private static void CaseSagEquationHolds(StringBuilder builder, List<string> failures)
        {
            const double span = 300.0;
            const double ratio = 0.03;
            double a = TransmissionLine.SolveCatenaryConstant(span, ratio);
            double sagFromA = a * (Math.Cosh(span / (2.0 * a)) - 1.0);
            double sagTarget = ratio * span;
            Expect(Math.Abs(sagFromA - sagTarget) < 1e-6,
                "sag-eqn: solved a did not satisfy s = a(cosh(L/2a)-1)", failures);
            builder.Append("RCMLINE ").Append("sag-equation".PadRight(22))
                   .Append("a solves the exact catenary sag relation (not a parabola)\n");
        }

        /// <summary> A level span is symmetric: vertex at mid-span, and the mid-span dip equals the authored
        /// sag. </summary>
        private static void CaseLevelSpanSymmetric(StringBuilder builder, List<string> failures)
        {
            const double span = 300.0;
            const double h = 40.0;
            const double ratio = 0.03;
            Catenary cat = TransmissionLine.Solve(span, h, h, ratio);
            Expect(Math.Abs(cat.LowestX() - span * 0.5) < 1e-3,
                "level: the low point of a level span was not at mid-span", failures);
            double sag = h - cat.LowestHeight();
            Expect(Math.Abs(sag - ratio * span) < 1e-3,
                "level: the mid-span dip did not equal the authored sag", failures);
            builder.Append("RCMLINE ").Append("level-span".PadRight(22))
                   .Append("vertex at mid-span, dip = sag ratio x span\n");
        }

        /// <summary> The general two-point case: unequal tower heights are reproduced exactly, and the low point
        /// shifts toward the LOWER tower rather than sitting at mid-span. </summary>
        private static void CaseInclinedSpanLowPointShifts(StringBuilder builder, List<string> failures)
        {
            const double span = 300.0;
            const double h1 = 100.0; // lower tower at x = 0
            const double h2 = 140.0; // higher tower at x = span
            Catenary cat = TransmissionLine.Solve(span, h1, h2, 0.03);
            Expect(Math.Abs(cat.HeightAt(0.0) - h1) < 1e-6 && Math.Abs(cat.HeightAt(span) - h2) < 1e-6,
                "inclined: the two tower heights were not reproduced", failures);
            Expect(cat.LowestX() < span * 0.5,
                "inclined: the low point did not shift toward the lower tower", failures);
            builder.Append("RCMLINE ").Append("inclined-span".PadRight(22))
                   .Append("both heights met, low point toward the lower tower\n");
        }

        /// <summary> A slacker cable: a larger sag ratio gives a smaller catenary constant a and a deeper sag. </summary>
        private static void CaseSagRatioSlackens(StringBuilder builder, List<string> failures)
        {
            const double span = 300.0;
            double aTight = TransmissionLine.SolveCatenaryConstant(span, 0.02);
            double aSlack = TransmissionLine.SolveCatenaryConstant(span, 0.05);
            Expect(aSlack < aTight, "sag-ratio: a larger sag ratio did not lower the catenary constant", failures);
            builder.Append("RCMLINE ").Append("sag-ratio".PadRight(22))
                   .Append("larger ratio -> smaller a -> slacker cable\n");
        }

        /// <summary> Tower type follows the AUTHORED turn-angle threshold: below is a suspension tower, at or
        /// above is an angle tower. </summary>
        private static void CaseTowerTypeByAngle(StringBuilder builder, List<string> failures)
        {
            const double threshold = 5.0;
            Expect(TransmissionLine.ChooseTowerType(2.0, threshold) == TowerType.Suspension,
                "tower: a 2 deg turn was not a suspension tower", failures);
            Expect(TransmissionLine.ChooseTowerType(12.0, threshold) == TowerType.Angle,
                "tower: a 12 deg turn was not an angle tower", failures);
            Expect(TransmissionLine.ChooseTowerType(5.0, threshold) == TowerType.Angle,
                "tower: the threshold turn was not an angle tower (boundary is inclusive)", failures);
            builder.Append("RCMLINE ").Append("tower-type".PadRight(22))
                   .Append("suspension below threshold, angle at/above (AUTHORED)\n");
        }

        /// <summary> Ground clearance: the same span passes over open ground and is FLAGGED with its tightest
        /// value when the minimum is raised past what it can meet -- never silently flattened. </summary>
        private static void CaseClearanceFlagged(StringBuilder builder, List<string> failures)
        {
            const double span = 300.0;
            const double towerTop = 20.0;
            Catenary cat = TransmissionLine.Solve(span, towerTop, towerTop, 0.03); // ~11 m lowest over flat ground
            List<double> flatGround = new List<double>();
            for (int i = 0; i <= 10; i++) { flatGround.Add(0.0); }

            TransmissionLine.ClearanceResult ok = TransmissionLine.SpanClearance(cat, flatGround, 6.0);
            Expect(ok.clears, "clearance: a span with ~11 m of clearance failed a 6 m minimum", failures);

            TransmissionLine.ClearanceResult bad = TransmissionLine.SpanClearance(cat, flatGround, 15.0);
            Expect(!bad.clears, "clearance: a span short of a 15 m minimum was not flagged", failures);
            Expect(bad.worstClearanceMetres < 15.0 && bad.worstStationIndex >= 0,
                "clearance: the flagged span did not report the offending value and station", failures);
            builder.Append("RCMLINE ").Append("clearance".PadRight(22))
                   .Append("clears open ground, flags a too-tight minimum with its value\n");
        }

        /// <summary> The solve is bit-identical on repeat -- fixed iteration count, no clock, no hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            Catenary a = TransmissionLine.Solve(275.0, 33.0, 51.0, 0.035);
            Catenary b = TransmissionLine.Solve(275.0, 33.0, 51.0, 0.035);
            Expect(a.a == b.a && a.x0 == b.x0 && a.c == b.c,
                "determinism: two solves of the same span produced different curves", failures);
            builder.Append("RCMLINE ").Append("determinism".PadRight(22))
                   .Append("identical span -> bit-identical catenary\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
