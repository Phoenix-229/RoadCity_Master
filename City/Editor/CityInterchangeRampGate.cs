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
    /// <summary> The interchange-ramp gate: proves the ramp half of a grade-separated interchange without a scene.
    ///
    /// It asserts that a diverge's auxiliary lane opens the authored 30 m ahead of the gore with the PUBLISHED taper
    /// upstream of that (never an authored taper); that a merge holds its longer acceleration length and tapers
    /// closed after it; that the mainline auxiliary lane comes back as the same AuxiliaryLane record the
    /// cross-section already consumes rather than a second mechanism; that the climb length is the height over the
    /// gradient and that a stricter ROAD CLASS gradient wins over the ramp ceiling; that the three independent
    /// minimums are computed separately and the LONGEST governs, with the governing constraint reported; that a
    /// radius below the design-speed minimum and an unclimbable gradient are both REFUSED with the numbers quoted;
    /// that the resolved gradient never exceeds the ceiling; that the centreline stations are even rather than
    /// ending on a stub; and that everything repeats bit-identically (C-06).
    ///
    /// The swept ramp mesh, its terrain fitting, the gore markings and the nose barrier are the deferred scene half.
    /// Report lines are prefixed RCMRAMP. </summary>
    public static class CityInterchangeRampGate
    {
        [MenuItem("Window/Road City Master/Testing/City interchange ramp")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityInterchangeRampGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMRAMP report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMRAMP BEGIN\n");
            List<string> failures = new List<string>();

            CaseDivergeAuxiliaryLane(builder, failures);
            CaseMergeAuxiliaryLane(builder, failures);
            CaseAuxiliaryLaneIsTheSharedRecord(builder, failures);
            CaseClimbLengthAndStricterClass(builder, failures);
            CaseTurnByAlignmentKind(builder, failures);
            CaseLongestConstraintGoverns(builder, failures);
            CaseRefusals(builder, failures);
            CaseCentrelineStationsAndGrade(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMRAMP FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMRAMP result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMRAMP END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A diverge holds full width for the authored 30 m ahead of the gore, and the taper that opens
        /// that lane is the PUBLISHED length for the lane width and mainline speed -- 223 m for a 3.6 m lane at
        /// 100 km/h -- sitting upstream of the full-width part. </summary>
        private static void CaseDivergeAuxiliaryLane(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();
            double taperStart, fullWidth, gore;
            InterchangeRamp.DivergeAuxiliaryLaneStations(1000.0, 3.6, 100.0, cls,
                out taperStart, out fullWidth, out gore);

            Expect(Math.Abs(gore - 1000.0) < 1e-9, "diverge: the gore station moved", failures);
            Expect(Math.Abs(fullWidth - 970.0) < 1e-9,
                "diverge: full width was not reached the authored 30 m ahead of the gore", failures);

            double published = LaneModel.TaperLengthMetres(3.6, 100.0);
            Expect(Math.Abs((fullWidth - taperStart) - published) < 1e-9,
                "diverge: the opening taper was not the published length", failures);
            Expect(published > 200.0 && published < 240.0,
                "diverge: the published 3.6 m @ 100 km/h taper left its expected 223 m neighbourhood", failures);
            Expect(taperStart < fullWidth && fullWidth < gore,
                "diverge: the three stations were not in travel order", failures);

            builder.Append("RCMRAMP ").Append("diverge".PadRight(22))
                   .Append("30 m full width to the gore, published taper upstream\n");
        }

        /// <summary> A merge holds its longer acceleration length after the nose and then tapers closed. The
        /// acceleration length must EXCEED the deceleration one -- joining needs more room than leaving. </summary>
        private static void CaseMergeAuxiliaryLane(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();
            Expect(cls.auxiliaryLaneAfterMergeMetres > cls.auxiliaryLaneBeforeDivergeMetres,
                "merge: the acceleration length was not longer than the deceleration length", failures);

            double nose, endFull, taperEnd;
            InterchangeRamp.MergeAuxiliaryLaneStations(500.0, 3.6, 80.0, cls,
                out nose, out endFull, out taperEnd);

            Expect(Math.Abs(nose - 500.0) < 1e-9, "merge: the nose station moved", failures);
            Expect(Math.Abs(endFull - 560.0) < 1e-9,
                "merge: full width did not run the authored 60 m past the nose", failures);
            Expect(Math.Abs((taperEnd - endFull) - LaneModel.TaperLengthMetres(3.6, 80.0)) < 1e-9,
                "merge: the closing taper was not the published length", failures);
            Expect(nose < endFull && endFull < taperEnd,
                "merge: the three stations were not in travel order", failures);

            builder.Append("RCMRAMP ").Append("merge".PadRight(22))
                   .Append("60 m full width past the nose, published taper closing\n");
        }

        /// <summary> The mainline lane a ramp asks for is the SAME AuxiliaryLane record the cross-section already
        /// consumes: a diverge yields a Deceleration bay ending at the gore, a merge an Acceleration lane opening at
        /// the nose. If this ever became a bespoke type, the ramp would have grown a second lane mechanism. </summary>
        private static void CaseAuxiliaryLaneIsTheSharedRecord(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();

            AuxiliaryLane exit = InterchangeRamp.MainlineAuxiliaryLane(RampMovement.Diverge, 1, 1000.0, 3.6, 100.0, cls);
            Expect(exit.Kind == AuxLaneKind.Deceleration,
                "shared record: a diverge did not produce a Deceleration lane", failures);
            Expect(Math.Abs(exit.End - 1000.0) < 1e-9,
                "shared record: the deceleration lane did not end at the gore", failures);
            Expect(Math.Abs(exit.EndFull - exit.End) < 1e-9,
                "shared record: the deceleration lane tapered after the gore instead of ending there", failures);

            AuxiliaryLane entry = InterchangeRamp.MainlineAuxiliaryLane(RampMovement.Merge, 1, 500.0, 3.6, 80.0, cls);
            Expect(entry.Kind == AuxLaneKind.Acceleration,
                "shared record: a merge did not produce an Acceleration lane", failures);
            Expect(Math.Abs(entry.Start - 500.0) < 1e-9 && Math.Abs(entry.Full - 500.0) < 1e-9,
                "shared record: the acceleration lane did not start at full width at the nose", failures);
            Expect(entry.End > entry.EndFull,
                "shared record: the acceleration lane never tapered closed", failures);

            builder.Append("RCMRAMP ").Append("shared record".PadRight(22))
                   .Append("Deceleration at the gore, Acceleration at the nose\n");
        }

        /// <summary> Climb length is the height over the gradient, and a STRICTER road class gradient beats the ramp
        /// ceiling -- a project holding its roads to 4 % does not get 8 % ramps by accident. A looser class gradient
        /// does not loosen the ramp. </summary>
        private static void CaseClimbLengthAndStricterClass(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();

            // Ramp ceiling 8 %: 5.1 m of clearance needs 63.75 m.
            double atCeiling = InterchangeRamp.ClimbLengthMetres(5.1, 0.0, cls);
            Expect(Math.Abs(atCeiling - 63.75) < 1e-9,
                "climb: 5.1 m at the 8 % ramp ceiling was not 63.75 m", failures);

            // A stricter 4 % class gradient wins: 127.5 m.
            double stricter = InterchangeRamp.ClimbLengthMetres(5.1, 0.04, cls);
            Expect(Math.Abs(stricter - 127.5) < 1e-9,
                "climb: a stricter road class gradient did not override the ramp ceiling", failures);

            // A looser 12 % class gradient must NOT loosen the ramp.
            double looser = InterchangeRamp.ClimbLengthMetres(5.1, 0.12, cls);
            Expect(Math.Abs(looser - atCeiling) < 1e-9,
                "climb: a looser road class gradient loosened the ramp ceiling", failures);

            Expect(InterchangeRamp.ClimbLengthMetres(0.0, 0.0, cls) == 0.0,
                "climb: no height to gain did not give a zero climb length", failures);

            InterchangeRampClass flat = InterchangeRampClass.Default();
            flat.maxRampGradientFraction = 0.0;
            Expect(double.IsPositiveInfinity(InterchangeRamp.ClimbLengthMetres(5.1, 0.0, flat)),
                "climb: a non-positive gradient did not demand an infinite ramp", failures);

            builder.Append("RCMRAMP ").Append("climb".PadRight(22))
                   .Append("height / gradient, stricter class wins, flat is infinite\n");
        }

        /// <summary> The three alignment families turn through different angles for the same pair of roads, which is
        /// what makes a loop ramp long even between two close nodes. </summary>
        private static void CaseTurnByAlignmentKind(StringBuilder builder, List<string> failures)
        {
            Expect(Math.Abs(InterchangeRamp.AlignmentTurnDegrees(RampAlignmentKind.Direct, 90.0) - 90.0) < 1e-9,
                "turn: a direct ramp did not turn through the angle between the roads", failures);
            Expect(Math.Abs(InterchangeRamp.AlignmentTurnDegrees(RampAlignmentKind.SemiDirect, 90.0) - 270.0) < 1e-9,
                "turn: a semi-direct ramp did not add the half turn", failures);
            Expect(Math.Abs(InterchangeRamp.AlignmentTurnDegrees(RampAlignmentKind.Loop, 90.0) - 270.0) < 1e-9,
                "turn: a loop ramp did not take the long way round", failures);
            // Handedness is dropped and reflex angles fold: -90 and 270 read as 90.
            Expect(Math.Abs(InterchangeRamp.AlignmentTurnDegrees(RampAlignmentKind.Direct, -90.0) - 90.0) < 1e-9
                   && Math.Abs(InterchangeRamp.AlignmentTurnDegrees(RampAlignmentKind.Direct, 270.0) - 90.0) < 1e-9,
                "turn: handedness or a reflex angle changed the turn magnitude", failures);

            // A 50 m radius through 270 degrees is 236 m of pavement.
            double arc = InterchangeRamp.CurveArcLengthMetres(50.0, 270.0);
            Expect(arc > 235.0 && arc < 236.5,
                "turn: a 50 m radius loop's arc length left its expected 236 m neighbourhood", failures);
            Expect(InterchangeRamp.CurveArcLengthMetres(0.0, 270.0) == 0.0
                   && InterchangeRamp.CurveArcLengthMetres(50.0, 0.0) == 0.0,
                "turn: a degenerate curve did not give a zero arc", failures);

            builder.Append("RCMRAMP ").Append("turn".PadRight(22))
                   .Append("direct / semi-direct / loop turns, arc from radius\n");
        }

        /// <summary> The three minimums are independent and the LONGEST governs. Each is made to win in turn, and
        /// the governing constraint is reported so the author knows which one they are fighting. </summary>
        private static void CaseLongestConstraintGoverns(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();
            SuperelevationClass super = SuperelevationClass.Default();
            double minRadius = InterchangeRamp.MinimumRadiusMetres(cls, super);

            // PLAN governs: two nodes 600 m apart, a small climb, a gentle direct turn.
            InterchangeRamp.RampResolve plan = InterchangeRamp.ResolveRamp(600.0, 5.1, minRadius,
                RampAlignmentKind.Direct, 20.0, 0.0, cls, super);
            Expect(plan.resolved && plan.governingConstraint == 0
                   && Math.Abs(plan.rampLengthMetres - 600.0) < 1e-9,
                "governing: the plan distance did not govern when it was the longest", failures);

            // CLIMB governs: nodes almost on top of each other, a tall climb, no turn.
            InterchangeRamp.RampResolve climb = InterchangeRamp.ResolveRamp(10.0, 5.1, minRadius,
                RampAlignmentKind.Direct, 0.0, 0.0, cls, super);
            Expect(climb.resolved && climb.governingConstraint == 1
                   && Math.Abs(climb.rampLengthMetres - 63.75) < 1e-9,
                "governing: the climb did not govern for two near nodes with height to gain", failures);

            // CURVE governs: near nodes, a small climb, a loop on a large radius.
            InterchangeRamp.RampResolve curve = InterchangeRamp.ResolveRamp(10.0, 1.0, 120.0,
                RampAlignmentKind.Loop, 90.0, 0.0, cls, super);
            Expect(curve.resolved && curve.governingConstraint == 2,
                "governing: a loop ramp's arc did not govern its length", failures);
            Expect(curve.rampLengthMetres > curve.planDistanceMetres
                   && curve.rampLengthMetres > curve.climbLengthMetres,
                "governing: the governing length was not the longest of the three", failures);

            // Every quoted length is filled in whichever governs -- that is what makes the report actionable.
            Expect(plan.climbLengthMetres > 0.0 && plan.curveLengthMetres > 0.0 && plan.radiusMetres > 0.0,
                "governing: a resolved ramp did not quote all three minimums", failures);

            builder.Append("RCMRAMP ").Append("governing".PadRight(22))
                   .Append("plan / climb / curve each win in turn, longest governs\n");
        }

        /// <summary> The two refusals, each naming its numbers, and the guarantee that a resolved ramp's gradient is
        /// at or below the ceiling by construction. </summary>
        private static void CaseRefusals(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();
            SuperelevationClass super = SuperelevationClass.Default();
            double minRadius = InterchangeRamp.MinimumRadiusMetres(cls, super);

            Expect(minRadius >= cls.minRampRadiusMetres - 1e-9,
                "refuse: the minimum radius fell below the authored house minimum", failures);

            InterchangeRamp.RampResolve tight = InterchangeRamp.ResolveRamp(400.0, 5.1, minRadius * 0.5,
                RampAlignmentKind.Direct, 90.0, 0.0, cls, super);
            Expect(!tight.resolved && tight.flagged && tight.message.Length > 0,
                "refuse: a radius below the design-speed minimum was not refused", failures);
            Expect(tight.message.Contains("km/h") && tight.message.Contains("minimum"),
                "refuse: the tight-radius refusal did not quote the speed and the minimum", failures);

            InterchangeRampClass flat = InterchangeRampClass.Default();
            flat.maxRampGradientFraction = 0.0;
            InterchangeRamp.RampResolve unclimbable = InterchangeRamp.ResolveRamp(400.0, 5.1,
                InterchangeRamp.MinimumRadiusMetres(flat, super), RampAlignmentKind.Direct, 90.0, 0.0, flat, super);
            Expect(!unclimbable.resolved && unclimbable.flagged,
                "refuse: an unclimbable gradient was not refused", failures);
            Expect(unclimbable.message.Contains("cannot climb"),
                "refuse: the unclimbable refusal did not say so", failures);

            // Resolved gradient never exceeds the ceiling: the length was chosen to make that true.
            InterchangeRamp.RampResolve ok = InterchangeRamp.ResolveRamp(10.0, 5.1, minRadius,
                RampAlignmentKind.Direct, 0.0, 0.0, cls, super);
            Expect(ok.resolved && ok.gradientFraction <= cls.maxRampGradientFraction + 1e-9,
                "refuse: a resolved ramp climbed steeper than the ceiling", failures);

            builder.Append("RCMRAMP ").Append("refuse".PadRight(22))
                   .Append("tight radius and unclimbable grade refused with numbers\n");
        }

        /// <summary> Centreline stations are EVEN rather than ending on a stub -- the same ceiling-then-re-divide rule
        /// the bridge supports use -- and the grade line is straight and clamped at both ends. </summary>
        private static void CaseCentrelineStationsAndGrade(StringBuilder builder, List<string> failures)
        {
            // 100 m at no more than 30 m: four even bays of 25 m, not 30/30/30/10.
            List<double> stations = InterchangeRamp.CentrelineStationsMetres(100.0, 30.0);
            Expect(stations.Count == 5, "stations: 100 m at 30 m spacing did not give five stations", failures);
            bool even = true;
            for (int i = 1; i < stations.Count; i++)
            {
                if (Math.Abs((stations[i] - stations[i - 1]) - 25.0) > 1e-9) { even = false; }
            }
            Expect(even, "stations: the bays were not made even", failures);
            Expect(Math.Abs(stations[0]) < 1e-9 && Math.Abs(stations[stations.Count - 1] - 100.0) < 1e-9,
                "stations: the ramp ends were not both stations", failures);
            Expect(InterchangeRamp.CentrelineStationsMetres(0.0, 30.0).Count == 0,
                "stations: a zero-length ramp produced stations", failures);

            // Straight grade, clamped outside.
            Expect(Math.Abs(InterchangeRamp.ElevationAtStationMetres(0.0, 10.0, 5.1, 100.0) - 10.0) < 1e-9,
                "grade: the low end was not the start elevation", failures);
            Expect(Math.Abs(InterchangeRamp.ElevationAtStationMetres(50.0, 10.0, 5.1, 100.0) - 12.55) < 1e-9,
                "grade: mid-ramp was not half the height gained", failures);
            Expect(Math.Abs(InterchangeRamp.ElevationAtStationMetres(100.0, 10.0, 5.1, 100.0) - 15.1) < 1e-9,
                "grade: the high end did not reach the full height", failures);
            Expect(Math.Abs(InterchangeRamp.ElevationAtStationMetres(500.0, 10.0, 5.1, 100.0) - 15.1) < 1e-9
                   && Math.Abs(InterchangeRamp.ElevationAtStationMetres(-500.0, 10.0, 5.1, 100.0) - 10.0) < 1e-9,
                "grade: a station past an end extrapolated off the structure", failures);

            builder.Append("RCMRAMP ").Append("stations".PadRight(22))
                   .Append("even bays, straight grade clamped at both ends\n");
        }

        /// <summary> Same inputs, same numbers and the same message text (C-06). The message is built with
        /// invariant-culture formatting so it does not drift with the machine's locale. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            InterchangeRampClass cls = InterchangeRampClass.Default();
            SuperelevationClass super = SuperelevationClass.Default();
            double minRadius = InterchangeRamp.MinimumRadiusMetres(cls, super);

            InterchangeRamp.RampResolve a = InterchangeRamp.ResolveRamp(240.0, 5.1, minRadius,
                RampAlignmentKind.Loop, 90.0, 0.05, cls, super);
            InterchangeRamp.RampResolve b = InterchangeRamp.ResolveRamp(240.0, 5.1, minRadius,
                RampAlignmentKind.Loop, 90.0, 0.05, cls, super);
            Expect(a.resolved == b.resolved
                   && a.rampLengthMetres == b.rampLengthMetres
                   && a.governingConstraint == b.governingConstraint
                   && a.gradientFraction == b.gradientFraction,
                "determinism: two identical resolves disagreed", failures);

            InterchangeRamp.RampResolve t1 = InterchangeRamp.ResolveRamp(400.0, 5.1, 1.0,
                RampAlignmentKind.Direct, 90.0, 0.0, cls, super);
            InterchangeRamp.RampResolve t2 = InterchangeRamp.ResolveRamp(400.0, 5.1, 1.0,
                RampAlignmentKind.Direct, 90.0, 0.0, cls, super);
            Expect(string.Equals(t1.message, t2.message, StringComparison.Ordinal),
                "determinism: two identical refusals produced different text", failures);

            List<double> s1 = InterchangeRamp.CentrelineStationsMetres(137.0, 20.0);
            List<double> s2 = InterchangeRamp.CentrelineStationsMetres(137.0, 20.0);
            bool same = s1.Count == s2.Count;
            for (int i = 0; same && i < s1.Count; i++) { if (s1[i] != s2[i]) { same = false; } }
            Expect(same, "determinism: two identical station lists disagreed", failures);

            builder.Append("RCMRAMP ").Append("determinism".PadRight(22))
                   .Append("resolves, refusal text and stations all repeat\n");
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
