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
    /// <summary> RM-25's gate: proves the pure rail-geometry constraint set -- the part that carries the real maths
    /// and the rejection path, checkable without a scene. It asserts that the equilibrium cant satisfies the
    /// textbook rail formula E = (G · V²)/(127 · R) exactly (the analogue of road superelevation, not a guess);
    /// that a tighter curve wants more cant at a fixed speed; that applied cant is capped at the class maximum and
    /// the shortfall shows up as cant deficiency; that a curve at or above the minimum radius is accepted while a
    /// tighter one is REJECTED with a message NAMING the radius it needed (a kinked rail must never reach a
    /// screenshot); that the rail gradient limit is its own, gentler number and not the road's; and that the whole
    /// computation is bit-identical on repeat (C-06). The cross-section sweep along SplineC via SplinatedMeshMaker
    /// and the station prefab placement are the deferred scene half. Report lines are prefixed RCMRAIL. </summary>
    public static class CityRailTrackGate
    {
        [MenuItem("Window/Road City Master/Testing/City rail track (cant + radius)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityRailTrackGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMRAIL report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMRAIL BEGIN\n");
            List<string> failures = new List<string>();

            CaseCantFormulaHolds(builder, failures);
            CaseTighterCurveWantsMoreCant(builder, failures);
            CaseAppliedCantCappedAndDeficiency(builder, failures);
            CaseMinimumRadiusRejects(builder, failures);
            CaseGradientIsRailsOwn(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMRAIL FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMRAIL result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMRAIL END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The equilibrium cant matches the textbook rail formula E = (G · V²)/(127 · R) exactly, proving
        /// the computation is the real method and not an approximation. </summary>
        private static void CaseCantFormulaHolds(StringBuilder builder, List<string> failures)
        {
            const double gauge = 1500.0;
            const double speed = 120.0;
            const double radius = 800.0;
            double expected = (gauge * speed * speed) / (127.0 * radius);
            double actual = RailTrack.EquilibriumCantMillimetres(speed, radius, gauge);
            Expect(Math.Abs(actual - expected) < 1e-9,
                "cant-eqn: equilibrium cant did not satisfy E = (G V^2)/(127 R)", failures);
            Expect(Math.Abs(RailTrack.EquilibriumCantMillimetres(speed, 0.0, gauge)) < 1e-12,
                "cant-eqn: a straight (R<=0) did not want zero cant", failures);
            builder.Append("RCMRAIL ").Append("cant-formula".PadRight(22))
                   .Append("equilibrium cant solves E=(G V^2)/(127 R) (real formula)\n");
        }

        /// <summary> At a fixed speed a tighter curve (smaller radius) wants more cant -- the 1/R relationship. </summary>
        private static void CaseTighterCurveWantsMoreCant(StringBuilder builder, List<string> failures)
        {
            const double gauge = 1500.0;
            const double speed = 120.0;
            double wide = RailTrack.EquilibriumCantMillimetres(speed, 1200.0, gauge);
            double tight = RailTrack.EquilibriumCantMillimetres(speed, 400.0, gauge);
            Expect(tight > wide, "cant-radius: a tighter curve did not want more cant", failures);
            builder.Append("RCMRAIL ").Append("cant-vs-radius".PadRight(22))
                   .Append("tighter radius -> more equilibrium cant\n");
        }

        /// <summary> Applied cant is capped at the class maximum, and when the equilibrium cant exceeds that cap the
        /// shortfall appears as cant deficiency (equilibrium - applied), never as an over-banked track. </summary>
        private static void CaseAppliedCantCappedAndDeficiency(StringBuilder builder, List<string> failures)
        {
            RailTrackClass cls = RailTrackClass.Default(); // maxAppliedCant 150 mm, gauge 1500
            // A tight, fast curve whose equilibrium cant exceeds the 150 mm cap.
            const double speed = 160.0;
            const double radius = 500.0;
            double equilibrium = RailTrack.EquilibriumCantMillimetres(speed, radius, cls.cantFormulaGaugeMillimetres);
            double applied = RailTrack.AppliedCantMillimetres(speed, radius, cls);
            double deficiency = RailTrack.CantDeficiencyMillimetres(speed, radius, cls);
            Expect(equilibrium > cls.maxAppliedCantMillimetres,
                "cap: the chosen curve was not tight/fast enough to exceed the cant cap (test setup)", failures);
            Expect(Math.Abs(applied - cls.maxAppliedCantMillimetres) < 1e-9,
                "cap: applied cant was not clamped to the class maximum", failures);
            Expect(Math.Abs(deficiency - (equilibrium - cls.maxAppliedCantMillimetres)) < 1e-9,
                "cap: cant deficiency did not equal equilibrium minus the applied cap", failures);
            builder.Append("RCMRAIL ").Append("applied-cant-cap".PadRight(22))
                   .Append("applied cant capped, shortfall shows as deficiency\n");
        }

        /// <summary> A curve at or above the minimum radius is accepted; a tighter one is rejected, and the rejection
        /// message names the radius it needed. </summary>
        private static void CaseMinimumRadiusRejects(StringBuilder builder, List<string> failures)
        {
            RailTrackClass cls = RailTrackClass.Default(); // minimum 300 m
            RailTrack.RadiusCheck ok = RailTrack.CheckRadius(500.0, cls);
            RailTrack.RadiusCheck boundary = RailTrack.CheckRadius(300.0, cls);
            RailTrack.RadiusCheck tooTight = RailTrack.CheckRadius(150.0, cls);
            Expect(ok.accepted, "radius: a 500 m curve above the 300 m minimum was rejected", failures);
            Expect(boundary.accepted, "radius: the exact 300 m minimum was rejected (boundary is inclusive)",
                failures);
            Expect(!tooTight.accepted, "radius: a 150 m curve below the minimum was accepted (would kink)", failures);
            string message = RailTrack.DescribeRadiusRejection(tooTight);
            Expect(message.Contains("150") && message.Contains("300"),
                "radius: the rejection message did not name the requested radius and the minimum", failures);
            builder.Append("RCMRAIL ").Append("min-radius".PadRight(22))
                   .Append("tighter-than-minimum curve refused, naming the radius needed\n");
        }

        /// <summary> The rail gradient limit is its OWN, gentler number: a gradient the rail rejects (steeper than
        /// its ruling per-mille) is far below a typical road's percent limit, proving the two are not wired
        /// together. </summary>
        private static void CaseGradientIsRailsOwn(StringBuilder builder, List<string> failures)
        {
            RailTrackClass cls = RailTrackClass.Default(); // 15 per mille = 1.5 %
            Expect(RailTrack.MeetsGradient(10.0, cls), "gradient: a 10 per-mille climb was rejected", failures);
            Expect(!RailTrack.MeetsGradient(40.0, cls),
                "gradient: a 40 per-mille (4 %) climb was accepted -- that is a road gradient, not rail", failures);
            Expect(!RailTrack.MeetsGradient(-40.0, cls),
                "gradient: a 40 per-mille descent was accepted (absolute-value safety failed)", failures);
            // 15 per mille = 1.5 %, an order of magnitude below a road's tens-of-percent limit -- the whole point.
            Expect(cls.maxGradientPerMille / 10.0 < 5.0,
                "gradient: the rail ruling gradient is not an order of magnitude gentler than a road's", failures);
            builder.Append("RCMRAIL ").Append("gradient-own".PadRight(22))
                   .Append("rail ruling gradient is its own, gentler number\n");
        }

        /// <summary> The whole computation is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            RailTrackClass cls = RailTrackClass.Default();
            double a1 = RailTrack.EquilibriumCantMillimetres(133.0, 650.0, cls.cantFormulaGaugeMillimetres);
            double a2 = RailTrack.EquilibriumCantMillimetres(133.0, 650.0, cls.cantFormulaGaugeMillimetres);
            double d1 = RailTrack.CantDeficiencyMillimetres(133.0, 650.0, cls);
            double d2 = RailTrack.CantDeficiencyMillimetres(133.0, 650.0, cls);
            Expect(a1 == a2 && d1 == d2,
                "determinism: two identical cant computations produced different values", failures);
            builder.Append("RCMRAIL ").Append("determinism".PadRight(22))
                   .Append("identical curve -> bit-identical cant\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
