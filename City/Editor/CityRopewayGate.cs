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
    /// <summary> RM-36's gate: proves the ropeway core without a scene, and proves it by REUSE -- the rope is
    /// RM-23's catenary, the tower type is RM-23's turn-angle rule, and the clearance report is RM-23's
    /// SpanClearance, so this gate mostly asserts that the ropeway wraps them correctly and adds only cabin
    /// spacing and the "no road mesh" carrier answer. It asserts that the rope between two tower tops sags below
    /// the straight chord (a catenary, not a taut line); that a straight node gets a suspension tower and a turn
    /// an angle tower; that cabins are evenly spaced at the authored interval with at least one on any line; that
    /// a span passing over a ridge is FLAGGED with the worst station named while a clear span passes; that a
    /// ropeway generates NO road mesh; and that everything is bit-identical on repeat (C-06). Terminal stations,
    /// the rope tube mesh and prefab placement are the deferred half. Report lines are prefixed RCMROPE. </summary>
    public static class CityRopewayGate
    {
        [MenuItem("Window/Road City Master/Testing/City ropeway (cable car clearance)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityRopewayGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMROPE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMROPE BEGIN\n");
            List<string> failures = new List<string>();

            CaseRopeIsCatenary(builder, failures);
            CaseTowerTypeFollowsTurn(builder, failures);
            CaseCabinsEvenlySpaced(builder, failures);
            CaseClearanceFlagsRidge(builder, failures);
            CaseNoRoadMesh(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMROPE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMROPE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMROPE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The rope between two tower tops sags below the straight chord: it is RM-23's catenary, not a
        /// taut straight line. </summary>
        private static void CaseRopeIsCatenary(StringBuilder builder, List<string> failures)
        {
            RopewayClass cls = RopewayClass.Default();
            Catenary rope = Ropeway.SolveRope(400.0, 100.0, 100.0, cls); // level span
            double chordMid = 100.0; // straight line between equal-height towers
            Expect(rope.LowestHeight() < chordMid - 1.0,
                "rope: a level span did not sag below the straight chord (not a catenary)", failures);
            // On a level span the vertex is at mid-span.
            Expect(Math.Abs(rope.LowestX() - 200.0) < 1e-6,
                "rope: the level-span low point was not at mid-span", failures);
            builder.Append("RCMROPE ").Append("rope-catenary".PadRight(22))
                   .Append("rope sags below the chord (RM-23 catenary reused)\n");
        }

        /// <summary> A straight-through node gets a suspension tower; a real turn gets an angle tower. </summary>
        private static void CaseTowerTypeFollowsTurn(StringBuilder builder, List<string> failures)
        {
            RopewayClass cls = RopewayClass.Default(); // threshold 8 deg
            Expect(Ropeway.TowerTypeAtNode(2.0, cls) == TowerType.Suspension,
                "tower: a nearly straight node did not get a suspension tower", failures);
            Expect(Ropeway.TowerTypeAtNode(30.0, cls) == TowerType.Angle,
                "tower: a turning node did not get an angle tower", failures);
            builder.Append("RCMROPE ").Append("tower-type".PadRight(22))
                   .Append("straight -> suspension, turn -> angle (RM-23 rule)\n");
        }

        /// <summary> Cabins are evenly spaced at the authored interval and a line always carries at least one. </summary>
        private static void CaseCabinsEvenlySpaced(StringBuilder builder, List<string> failures)
        {
            RopewayClass cls = RopewayClass.Default(); // spacing 60 m
            // 300 m line: floor(300/60)+1 = 6 cabins at 0,60,...,300.
            Expect(Ropeway.CabinCount(300.0, cls) == 6, "cabins: a 300 m line at 60 m did not fit 6 cabins",
                failures);
            Expect(Math.Abs(Ropeway.CabinStationMetres(0, cls)) < 1e-9
                   && Math.Abs(Ropeway.CabinStationMetres(3, cls) - 180.0) < 1e-9,
                "cabins: cabin stations were not the even authored spacing", failures);
            // A short line still carries the one boarding cabin; a zero line carries none.
            Expect(Ropeway.CabinCount(10.0, cls) == 1 && Ropeway.CabinCount(0.0, cls) == 0,
                "cabins: the minimum-one / none-on-empty rule did not hold", failures);
            builder.Append("RCMROPE ").Append("cabins".PadRight(22))
                   .Append("cabins even at the authored spacing, >= 1 per line\n");
        }

        /// <summary> A span whose ground rises into a ridge mid-span is flagged with the worst station named; a
        /// span over lower ground clears. </summary>
        private static void CaseClearanceFlagsRidge(StringBuilder builder, List<string> failures)
        {
            RopewayClass cls = RopewayClass.Default(); // min clearance 8 m
            Catenary rope = Ropeway.SolveRope(400.0, 100.0, 100.0, cls); // dips to ~88 m at mid-span

            // A ridge that reaches 95 m at mid-span leaves < 8 m clearance -> flagged there.
            double[] ridge = { 40, 60, 95, 60, 40 };
            TransmissionLine.ClearanceResult hit = Ropeway.SpanClearance(rope, ridge, cls);
            Expect(!hit.clears && hit.worstStationIndex == 2,
                "clearance: a rope passing over a ridge was not flagged at the ridge station", failures);

            // Low ground the whole way clears comfortably.
            double[] valley = { 40, 40, 40, 40, 40 };
            TransmissionLine.ClearanceResult clear = Ropeway.SpanClearance(rope, valley, cls);
            Expect(clear.clears, "clearance: a rope high over the ground was wrongly flagged", failures);
            builder.Append("RCMROPE ").Append("clearance".PadRight(22))
                   .Append("ridge span flagged at its station, clear span passes\n");
        }

        /// <summary> A ropeway is a carrier that generates no road mesh -- the RM-25 shared answer. </summary>
        private static void CaseNoRoadMesh(StringBuilder builder, List<string> failures)
        {
            Expect(!RopewayClass.Default().generatesRoadMesh,
                "carrier: a ropeway was set to generate a road mesh", failures);
            builder.Append("RCMROPE ").Append("no-road-mesh".PadRight(22))
                   .Append("ropeway is a carrier with road mesh off\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            RopewayClass cls = RopewayClass.Default();
            Catenary a = Ropeway.SolveRope(523.0, 120.0, 260.0, cls);
            Catenary b = Ropeway.SolveRope(523.0, 120.0, 260.0, cls);
            Expect(a.a == b.a && a.x0 == b.x0 && a.c == b.c
                   && Ropeway.CabinStationMetres(7, cls) == Ropeway.CabinStationMetres(7, cls),
                "determinism: two identical rope solves produced different values", failures);
            builder.Append("RCMROPE ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical rope/cabins\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
