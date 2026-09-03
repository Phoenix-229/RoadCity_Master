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
    /// <summary> RM-40's gate: proves the superelevation relation and its transition without a scene. It asserts
    /// that the side-friction factor interpolates the tabulated AASHTO values and clamps at the ends; that the
    /// required banking follows e = V^2/(127 R) - f; that a straight (no radius) shows only normal crown; that a
    /// curve too tight for e_max is refused with the minimum radius that would work; that the banking develops over
    /// a runoff from crown to full value before the curve midpoint, holds, and returns with no discontinuity; that
    /// the zero-cross-slope station inside the runoff is found and a coincident near-zero grade is flagged as the
    /// real drainage defect; that a rail alignment is refused outright (RM-25 owns cant); and that everything is
    /// bit-identical on repeat (C-06). Feeding the rotation into RM-31's cross-section is the deferred half. Report
    /// lines are prefixed RCMSUPER. </summary>
    public static class CitySuperelevationGate
    {
        [MenuItem("Window/Road City Master/Testing/City superelevation (banking through curves)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CitySuperelevationGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMSUPER report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSUPER BEGIN\n");
            List<string> failures = new List<string>();

            CaseSideFrictionFromTable(builder, failures);
            CaseRequiredFromRelation(builder, failures);
            CaseStraightNormalCrown(builder, failures);
            CaseExceedsMaxReportsRadius(builder, failures);
            CaseRunoffNoDiscontinuity(builder, failures);
            CaseZeroCrossSlopeDrainage(builder, failures);
            CaseRailRefused(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSUPER FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSUPER result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSUPER END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The side-friction factor interpolates the tabulated AASHTO values and clamps at both ends. </summary>
        private static void CaseSideFrictionFromTable(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default();
            Expect(Math.Abs(Superelevation.SideFrictionFactor(100.0, cls) - 0.12) < 1e-9,
                "friction: the tabulated value at 100 km/h was not returned exactly", failures);
            Expect(Math.Abs(Superelevation.SideFrictionFactor(45.0, cls) - 0.165) < 1e-9,
                "friction: the midpoint between 40 and 50 km/h did not interpolate to 0.165", failures);
            Expect(Math.Abs(Superelevation.SideFrictionFactor(10.0, cls) - 0.17) < 1e-9
                   && Math.Abs(Superelevation.SideFrictionFactor(200.0, cls) - 0.09) < 1e-9,
                "friction: values off the ends of the table did not clamp", failures);
            builder.Append("RCMSUPER ").Append("side-friction".PadRight(22))
                   .Append("f interpolated from the AASHTO table, clamped\n");
        }

        /// <summary> The required banking follows e = V^2/(127 R) - f from the published relation. </summary>
        private static void CaseRequiredFromRelation(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default();
            double f = Superelevation.SideFrictionFactor(100.0, cls); // 0.12
            double required = Superelevation.RequiredSuperelevation(100.0, 500.0, f);
            // 100^2 / (127 * 500) - 0.12 = 0.15748 - 0.12 = 0.03748
            Expect(Math.Abs(required - 0.03748) < 1e-4,
                "relation: e did not match V^2/(127 R) - f for a 500 m curve at 100 km/h", failures);
            Superelevation.BankingResult r = Superelevation.ResolveBanking(AlignmentClass.Highway, 100.0, 500.0, cls);
            Expect(r.feasible && !r.exceedsMax && Math.Abs(r.appliedSuperelevation - required) < 1e-9,
                "relation: a curve within e_max was not applied at its required banking", failures);
            builder.Append("RCMSUPER ").Append("relation".PadRight(22))
                   .Append("e = V^2/(127 R) - f, applied within e_max\n");
        }

        /// <summary> A straight (no curvature) shows normal crown only: no banking. </summary>
        private static void CaseStraightNormalCrown(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default();
            Superelevation.BankingResult r = Superelevation.ResolveBanking(AlignmentClass.Highway, 100.0, 100000.0, cls);
            Expect(r.feasible && Math.Abs(r.appliedSuperelevation) < 1e-12,
                "straight: a near-straight alignment demanded banking instead of normal crown", failures);
            Expect(Math.Abs(Superelevation.RequiredSuperelevation(100.0, 0.0, 0.12)) < 1e-12,
                "straight: a zero radius (a straight) did not resolve to zero banking", failures);
            builder.Append("RCMSUPER ").Append("straight-crown".PadRight(22))
                   .Append("a straight banks nothing -- normal crown only\n");
        }

        /// <summary> A curve too tight for e_max is refused and quotes the minimum radius that would work. </summary>
        private static void CaseExceedsMaxReportsRadius(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default(); // e_max 0.06
            Superelevation.BankingResult r = Superelevation.ResolveBanking(AlignmentClass.Highway, 100.0, 200.0, cls);
            // required 0.2737 > 0.06 -> exceeds; R_min = 10000 / (127 * 0.18) = 437.45 m
            Expect(r.exceedsMax && !r.feasible && Math.Abs(r.appliedSuperelevation - 0.06) < 1e-9,
                "max: a curve past e_max was not flagged and clamped", failures);
            Expect(Math.Abs(r.minRadiusForDesignSpeedMetres - 437.45) < 0.1,
                "max: the quoted minimum radius was not V^2/(127 (e_max + f))", failures);
            // A curve exactly at that minimum radius fits (required == e_max, not over).
            Superelevation.BankingResult atMin = Superelevation.ResolveBanking(AlignmentClass.Highway, 100.0,
                r.minRadiusForDesignSpeedMetres, cls);
            Expect(atMin.feasible && Math.Abs(atMin.appliedSuperelevation - 0.06) < 1e-4,
                "max: the quoted minimum radius did not itself fit within e_max", failures);
            builder.Append("RCMSUPER ").Append("exceeds-max".PadRight(22))
                   .Append("too-tight curve refused, minimum radius quoted\n");
        }

        /// <summary> The banking develops over a runoff from crown to full value before the curve midpoint, holds
        /// through the curve, and returns after -- continuous throughout. </summary>
        private static void CaseRunoffNoDiscontinuity(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default(); // runoff 40 m
            const double e = 0.05, start = 100.0, end = 300.0;
            double atDevelopStart = Superelevation.SuperelevationAtStation(60.0, start, end, e, cls);
            double atRunoffMid = Superelevation.SuperelevationAtStation(80.0, start, end, e, cls);
            double atCurveStart = Superelevation.SuperelevationAtStation(100.0, start, end, e, cls);
            double atCurveMid = Superelevation.SuperelevationAtStation(200.0, start, end, e, cls);
            double atCurveEnd = Superelevation.SuperelevationAtStation(300.0, start, end, e, cls);
            double atReturnMid = Superelevation.SuperelevationAtStation(320.0, start, end, e, cls);
            double atDevelopEnd = Superelevation.SuperelevationAtStation(340.0, start, end, e, cls);
            Expect(Math.Abs(atDevelopStart) < 1e-9 && Math.Abs(atDevelopEnd) < 1e-9,
                "runoff: the ends of the runoff were not at normal crown (zero banking)", failures);
            Expect(Math.Abs(atCurveStart - e) < 1e-9 && Math.Abs(atCurveMid - e) < 1e-9
                   && Math.Abs(atCurveEnd - e) < 1e-9,
                "runoff: full banking was not held from curve start (before midpoint) through the curve", failures);
            Expect(Math.Abs(atRunoffMid - 0.025) < 1e-9 && Math.Abs(atReturnMid - 0.025) < 1e-9,
                "runoff: the development was not a continuous linear ramp on both sides", failures);
            builder.Append("RCMSUPER ").Append("runoff".PadRight(22))
                   .Append("crown -> full before mid -> crown, continuous\n");
        }

        /// <summary> The zero-cross-slope station inside the runoff is found, and a coincident near-zero grade is
        /// the real (screenshot-invisible) drainage defect. </summary>
        private static void CaseZeroCrossSlopeDrainage(StringBuilder builder, List<string> failures)
        {
            const double crown = 0.02, e = 0.05;
            double zeroFrac = Superelevation.ZeroCrossSlopeFraction(crown, e); // 0.02 / 0.07 = 0.2857
            Expect(Math.Abs(zeroFrac - (0.02 / 0.07)) < 1e-9,
                "drainage: the zero-cross-slope fraction was not crown/(crown+e)", failures);
            Expect(Math.Abs(Superelevation.OuterEdgeCrossSlopeThroughRunoff(zeroFrac, crown, e)) < 1e-9,
                "drainage: the outer-edge cross-slope was not zero at that fraction", failures);
            Expect(Math.Abs(Superelevation.OuterEdgeCrossSlopeThroughRunoff(0.0, crown, e) + crown) < 1e-9
                   && Math.Abs(Superelevation.OuterEdgeCrossSlopeThroughRunoff(1.0, crown, e) - e) < 1e-9,
                "drainage: the cross-slope did not run from -crown to +e across the runoff", failures);
            Expect(Superelevation.HasFlatDrainageDefect(0.0, 0.0, 0.005),
                "drainage: zero cross-slope with zero grade was not flagged", failures);
            Expect(!Superelevation.HasFlatDrainageDefect(0.0, 0.03, 0.005),
                "drainage: zero cross-slope with real grade was wrongly flagged", failures);
            builder.Append("RCMSUPER ").Append("drainage-flag".PadRight(22))
                   .Append("zero cross-slope + flat grade caught, else not\n");
        }

        /// <summary> A rail alignment is refused outright -- highway superelevation must not be applied to cant. </summary>
        private static void CaseRailRefused(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default();
            Superelevation.BankingResult r = Superelevation.ResolveBanking(AlignmentClass.Rail, 100.0, 200.0, cls);
            Expect(r.refusedRailAlignment && !r.feasible,
                "rail: a rail alignment was not refused (cant is RM-25's, not this formula)", failures);
            builder.Append("RCMSUPER ").Append("rail-refused".PadRight(22))
                   .Append("rail alignment refused; cant belongs to RM-25\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            SuperelevationClass cls = SuperelevationClass.Default();
            Superelevation.BankingResult a = Superelevation.ResolveBanking(AlignmentClass.Highway, 90.0, 350.0, cls);
            Superelevation.BankingResult b = Superelevation.ResolveBanking(AlignmentClass.Highway, 90.0, 350.0, cls);
            double s1 = Superelevation.SuperelevationAtStation(85.0, 100.0, 300.0, 0.05, cls);
            double s2 = Superelevation.SuperelevationAtStation(85.0, 100.0, 300.0, 0.05, cls);
            Expect(a.appliedSuperelevation == b.appliedSuperelevation
                   && a.minRadiusForDesignSpeedMetres == b.minRadiusForDesignSpeedMetres && s1 == s2,
                "determinism: two identical superelevation computations differed", failures);
            builder.Append("RCMSUPER ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical banking\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
