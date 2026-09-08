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
    /// <summary> RM-35's gate: proves the plan-view crossing detector, the deterministic raise decision, the
    /// clearance-through-gradient approach maths and the refusal path without a scene. It asserts that two roads
    /// crossing in plan with no shared node are detected while a shared-endpoint junction is NOT; that the minor
    /// road is the one raised (a motorway never ducks under a farm track) with a stable id tie-break at equal
    /// class; that the required approach on each side is the clearance divided by the class gradient (a little over
    /// 5 m at 6 % ≈ 90 m); that a crossing with enough approach becomes a flyover raised to the clearance while one
    /// without enough approach is REFUSED with the needed length quoted; that the crossing marks a bridge-span
    /// range clamped to the road; and that everything is bit-identical on repeat (C-06). Terrain fitting and the
    /// profile raise are the deferred half. Report lines are prefixed RCMCROSS. </summary>
    public static class CityGradeSeparationGate
    {
        [MenuItem("Window/Road City Master/Testing/City grade separation (flyover)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityGradeSeparationGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMCROSS report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMCROSS BEGIN\n");
            List<string> failures = new List<string>();

            CaseCrossingVsSharedNode(builder, failures);
            CaseMinorRoadRises(builder, failures);
            CaseApproachLengthFromGradient(builder, failures);
            CaseFlyoverOrRefusal(builder, failures);
            CaseBridgeSpanClamped(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMCROSS FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMCROSS result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMCROSS END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A plan cross with no shared node is detected at its interior; a shared endpoint (a junction)
        /// and a parallel pair are not crossings. </summary>
        private static void CaseCrossingVsSharedNode(StringBuilder builder, List<string> failures)
        {
            // A: (-10,0)->(10,0) ; B: (0,-10)->(0,10) -> cross at origin, t=u=0.5.
            GradeSeparation.PlanCrossing x =
                GradeSeparation.DetectCrossing(-10, 0, 10, 0, 0, -10, 0, 10);
            Expect(x.crosses && Math.Abs(x.xMetres) < 1e-9 && Math.Abs(x.zMetres) < 1e-9
                   && Math.Abs(x.paramAlongAb - 0.5) < 1e-9,
                "detect: an interior plan crossing was not found at the origin", failures);

            // Shared endpoint at (10,0): a junction, not a crossing.
            GradeSeparation.PlanCrossing node =
                GradeSeparation.DetectCrossing(-10, 0, 10, 0, 10, 0, 10, 10);
            Expect(!node.crosses, "detect: a shared-node junction was mistaken for a crossing", failures);

            // Parallel: never crosses.
            GradeSeparation.PlanCrossing par =
                GradeSeparation.DetectCrossing(-10, 0, 10, 0, -10, 5, 10, 5);
            Expect(!par.crosses, "detect: parallel roads were reported as crossing", failures);
            builder.Append("RCMCROSS ").Append("detect".PadRight(22))
                   .Append("interior cross found, shared node / parallel not\n");
        }

        /// <summary> The MINOR road rises; equal class falls to a stable id tie-break. </summary>
        private static void CaseMinorRoadRises(StringBuilder builder, List<string> failures)
        {
            // Rank 3 = motorway (major), rank 1 = track. Raise the track (index 1), never the motorway.
            Expect(GradeSeparation.ChooseRaisedRoad(3, 100, 1, 200) == 1,
                "priority: the major road was raised instead of the minor one", failures);
            Expect(GradeSeparation.ChooseRaisedRoad(1, 100, 3, 200) == 0,
                "priority: the minor road on side A was not the one raised", failures);
            // Equal class -> higher identifier rises, deterministically.
            Expect(GradeSeparation.ChooseRaisedRoad(2, 5, 2, 9) == 1
                   && GradeSeparation.ChooseRaisedRoad(2, 9, 2, 5) == 0,
                "priority: the equal-class tie-break was not a stable id rule", failures);
            builder.Append("RCMCROSS ").Append("priority".PadRight(22))
                   .Append("minor road rises, id tie-break at equal class\n");
        }

        /// <summary> Required approach on each side = clearance / gradient; a non-positive gradient never climbs. </summary>
        private static void CaseApproachLengthFromGradient(StringBuilder builder, List<string> failures)
        {
            // 5.1 m at 6 % -> 85 m each side.
            Expect(Math.Abs(GradeSeparation.RequiredApproachLengthMetres(5.1, 0.06) - 85.0) < 1e-9,
                "approach: 5.1 m at 6 % did not need 85 m of approach", failures);
            Expect(double.IsPositiveInfinity(GradeSeparation.RequiredApproachLengthMetres(5.1, 0.0)),
                "approach: a zero gradient did not report an unbuildable (infinite) approach", failures);
            builder.Append("RCMCROSS ").Append("approach".PadRight(22))
                   .Append("required length = clearance / class gradient\n");
        }

        /// <summary> Enough approach -> a flyover raised to the clearance; too little -> refused with the length. </summary>
        private static void CaseFlyoverOrRefusal(StringBuilder builder, List<string> failures)
        {
            GradeSeparationClass cls = GradeSeparationClass.Default(); // 5.1 m clearance
            // 90 m available at 6 % (needs 85) -> resolved, raised to 5.1.
            GradeSeparation.SeparationResolve ok =
                GradeSeparation.ResolveSeparation(cls.verticalClearanceMetres, 0.06, 90.0);
            Expect(ok.resolved && !ok.flagged && Math.Abs(ok.raiseHeightMetres - 5.1) < 1e-9,
                "flyover: a crossing with enough approach was not raised to the clearance", failures);

            // 40 m available (needs 85) -> refused, and the required 85 m is quoted.
            GradeSeparation.SeparationResolve no =
                GradeSeparation.ResolveSeparation(cls.verticalClearanceMetres, 0.06, 40.0);
            Expect(!no.resolved && no.flagged && Math.Abs(no.requiredApproachLengthMetres - 85.0) < 1e-9,
                "flyover: a crossing without enough approach was not refused with the needed length", failures);
            builder.Append("RCMCROSS ").Append("flyover-refusal".PadRight(22))
                   .Append("raise if approach fits, else refuse with length\n");
        }

        /// <summary> The bridge span is centred on the crossing and clamped to the road's length. </summary>
        private static void CaseBridgeSpanClamped(StringBuilder builder, List<string> failures)
        {
            double start, end;
            GradeSeparation.BridgeSpanMetres(200.0, 40.0, 500.0, out start, out end);
            Expect(Math.Abs(start - 180.0) < 1e-9 && Math.Abs(end - 220.0) < 1e-9,
                "bridge-span: an interior span was not centred on the crossing", failures);

            // Crossing near the end: the span clamps to the road length rather than running off the end.
            GradeSeparation.BridgeSpanMetres(490.0, 40.0, 500.0, out start, out end);
            Expect(Math.Abs(start - 470.0) < 1e-9 && Math.Abs(end - 500.0) < 1e-9,
                "bridge-span: a span near the end was not clamped to the road length", failures);
            builder.Append("RCMCROSS ").Append("bridge-span".PadRight(22))
                   .Append("span centred on the crossing, clamped to the road\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            GradeSeparation.PlanCrossing a =
                GradeSeparation.DetectCrossing(-7, -3, 11, 4, 2, -9, -1, 8);
            GradeSeparation.PlanCrossing b =
                GradeSeparation.DetectCrossing(-7, -3, 11, 4, 2, -9, -1, 8);
            double la = GradeSeparation.RequiredApproachLengthMetres(5.1, 0.055);
            double lb = GradeSeparation.RequiredApproachLengthMetres(5.1, 0.055);
            Expect(a.crosses == b.crosses && a.xMetres == b.xMetres && a.zMetres == b.zMetres && la == lb,
                "determinism: two identical grade-separation computations differed", failures);
            builder.Append("RCMCROSS ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical crossing/length\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
