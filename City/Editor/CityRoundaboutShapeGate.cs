// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-61's headless gate: proves the circulatory way is genuinely SHAPE-FLEXIBLE rather than a circle with
    /// extra fields. Every case here is written so it can only pass if the quantity was re-derived per station:
    ///
    /// - a circle must read the SAME radius at every station (so the walk is not accidentally reporting garbage);
    /// - an ellipse must read b^2/a at its tight end and a^2/b at its blunt end, and must read the same two numbers
    ///   after the whole shape is rotated (a quantity that changes when you rotate the axes is an artefact, not a
    ///   measurement);
    /// - a tight oval must be REFUSED with the station and the radius in the message, because that is the only form of
    ///   refusal a person can act on;
    /// - the entry angle must be exactly square on a circle at every bearing and measurably skewed on an oval, which is
    ///   the difference between checking against the local tangent and checking against the radial;
    /// - the splitter island must come out DIFFERENT on two legs of the same oval with the same leg run;
    /// - the fastest-path radius must rise with loop size until it fails the deflection cap;
    /// - the implied speed must satisfy the SAME published relation the forward direction uses;
    /// - the polygon case must be closed and C2 across its segment joins, and must not interpolate its control points;
    /// - resolving a shape, changing it, and changing it back must reproduce the first answer exactly.
    ///
    /// Prefix RCMSHAPE. </summary>
    public static class CityRoundaboutShapeGate
    {
        [MenuItem("Window/Road City Master/Testing/Roundabout shape (per-station radius / entries / refusals)")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder log = new StringBuilder();
            log.AppendLine("RCMSHAPE begin");

            CaseCircleHasOneRadiusEverywhere(failures, log);
            CaseEllipseSweepsBetweenItsTwoLimits(failures, log);
            CaseTightEndIsRefusedNamingStationAndRadius(failures, log);
            CaseEntryIsSquareOnACircleAndSkewedOnAnOval(failures, log);
            CaseSplitterIsResolvedPerLeg(failures, log);
            CaseDeflectionFailsOnAFlatLoop(failures, log);
            CaseImpliedSpeedIsTheOneLawSolvedBackwards(failures, log);
            CasePolygonIsClosedSmoothAndNotInterpolating(failures, log);
            CaseDegenerateShapesRefuseInsteadOfMeasuring(failures, log);
            CaseShapeChangeResolvesEverything(failures, log);

            const int cases = 10;
            for (int i = 0; i < failures.Count; i++) { log.AppendLine("RCMSHAPE FAIL " + failures[i]); }
            bool pass = failures.Count == 0;
            log.AppendLine("RCMSHAPE result " + (pass ? "PASS" : "FAIL") + "  cases "
                + cases.ToString(CultureInfo.InvariantCulture) + "  failures "
                + failures.Count.ToString(CultureInfo.InvariantCulture));
            report = log.ToString();
            return pass;
        }

        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition) { failures.Add(failureMessage); }
        }

        private static string F(double value)
        {
            if (double.IsPositiveInfinity(value)) { return "inf"; }
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
        /// <summary> A circle is the degenerate case of the shape-flexible walk, and it is the control: if the walk
        /// cannot read one constant radius on a circle, nothing it reads on an oval means anything. Also checks the
        /// measured quantities against the closed forms a circle DOES have -- perimeter 2*pi*r, both calipers 2r, the
        /// centroid back at the authored centre -- and that the station bookkeeping lines up with the arc length. </summary>
        private static void CaseCircleHasOneRadiusEverywhere(List<string> failures, StringBuilder log)
        {
            RoundaboutShape shape = RoundaboutShape.Circle(Vector2.zero, 20.0);
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();
            ShapedRoundabout.CurvatureWalk w = ShapedRoundabout.WalkCurvature(shape, cls, banking);

            Expect(w.shapeUsable, "RCMSHAPE circle reported an unusable shape.", failures);
            Expect(w.accepted, "RCMSHAPE a 20 m circle was refused against a 12 m minimum: " + w.refusal, failures);
            Expect(Math.Abs(w.tightestRadiusMetres - 20.0) < 1e-6,
                "RCMSHAPE circle tightest radius " + F(w.tightestRadiusMetres) + " m, expected 20.", failures);
            Expect(Math.Abs(w.widestRadiusMetres - 20.0) < 1e-6,
                "RCMSHAPE circle widest radius " + F(w.widestRadiusMetres) + " m, expected 20.", failures);

            double expectedPerimeter = 2.0 * Math.PI * 20.0;
            Expect(Math.Abs(w.perimeterMetres - expectedPerimeter) / expectedPerimeter < 1e-5,
                "RCMSHAPE circle perimeter " + F(w.perimeterMetres) + " m, expected " + F(expectedPerimeter) + ".",
                failures);
            int expectedStations = (int)Math.Ceiling(w.perimeterMetres / ShapedRoundabout.CurvatureStationStepMetres
                - 1e-9);
            Expect(w.stations == expectedStations, "RCMSHAPE circle walked " + w.stations + " stations, expected "
                + expectedStations + " at a " + F(ShapedRoundabout.CurvatureStationStepMetres) + " m step.", failures);

            RoundaboutShape.CaliperReading calipers = shape.MeasureCalipers();
            Expect(Math.Abs(calipers.widestMetres - 40.0) < 1e-3 && Math.Abs(calipers.narrowestMetres - 40.0) < 1e-3,
                "RCMSHAPE circle calipers read " + F(calipers.widestMetres) + " / " + F(calipers.narrowestMetres)
                + " m, expected 40 both ways.", failures);
            Expect(Math.Abs(shape.MeasuredInscribedCircleDiameterMetres() - 40.0) < 1e-3,
                "RCMSHAPE circle measured ICD " + F(shape.MeasuredInscribedCircleDiameterMetres()) + " m.", failures);

            PlanXZ centroid = shape.MeasuredCentroid();
            Expect(centroid.Length < 1e-6, "RCMSHAPE circle centroid drifted to (" + F(centroid.x) + ", "
                + F(centroid.z) + ").", failures);
            Expect(Math.Abs(shape.ArcLengthTo(0.25) - expectedPerimeter * 0.25) < 1e-3,
                "RCMSHAPE circle arc length to t=0.25 was " + F(shape.ArcLengthTo(0.25)) + " m.", failures);
            Expect(Math.Abs(shape.ParameterAtArcLength(expectedPerimeter * 0.5) - 0.5) < 1e-6,
                "RCMSHAPE circle parameter at half the perimeter was "
                + F(shape.ParameterAtArcLength(expectedPerimeter * 0.5)) + ".", failures);
            log.AppendLine("RCMSHAPE circle r=20: radius constant, perimeter " + F(w.perimeterMetres) + " m, "
                + w.stations + " stations, implied speed " + F(w.impliedSpeedAtTightestKmh) + " km/h.");
        }

        /// <summary> An ellipse has NO radius, and that is the point of RM-61. Its radius of curvature must sweep from
        /// b^2/a at the ends of the major axis -- the tight end, where a design vehicle runs out of room -- up to a^2/b
        /// at the ends of the minor axis, and the walk must find both. Then the whole shape is rotated 37 degrees: the
        /// two readings must not move, because a radius that depends on how the shape is turned is an artefact of the
        /// parameterisation rather than a measurement of the ground. </summary>
        private static void CaseEllipseSweepsBetweenItsTwoLimits(List<string> failures, StringBuilder log)
        {
            const double a = 30.0;
            const double b = 18.0;
            double tightExpected = b * b / a;
            double bluntExpected = a * a / b;
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();

            RoundaboutShape shape = RoundaboutShape.Ellipse(Vector2.zero, a, b, 0.0);
            ShapedRoundabout.CurvatureWalk w = ShapedRoundabout.WalkCurvature(shape, cls, banking);
            Expect(Math.Abs(w.tightestRadiusMetres - tightExpected) < 0.01,
                "RCMSHAPE ellipse tightest radius " + F(w.tightestRadiusMetres) + " m, expected b^2/a = "
                + F(tightExpected) + ".", failures);
            Expect(Math.Abs(w.widestRadiusMetres - bluntExpected) < 0.5,
                "RCMSHAPE ellipse widest radius " + F(w.widestRadiusMetres) + " m, expected a^2/b = "
                + F(bluntExpected) + ".", failures);

            double half = w.perimeterMetres * 0.5;
            bool atAnEnd = w.tightestStationMetres < 1.0 || Math.Abs(w.tightestStationMetres - half) < 1.0
                || Math.Abs(w.tightestStationMetres - w.perimeterMetres) < 1.0;
            Expect(atAnEnd, "RCMSHAPE ellipse tightest station " + F(w.tightestStationMetres)
                + " m is not at an end of the major axis (half perimeter " + F(half) + " m).", failures);

            int distinct = 0;
            double previous = double.NaN;
            double[] stations;
            double[] parameters;
            double perimeter;
            int count = shape.UniformStations(1.0, out stations, out parameters, out perimeter);
            for (int i = 0; i < count; i++)
            {
                double radius = shape.CurvatureRadiusAt(parameters[i]);
                if (double.IsNaN(previous) || Math.Abs(radius - previous) > 1e-9) { distinct++; }
                previous = radius;
            }
            Expect(distinct > 50, "RCMSHAPE ellipse produced only " + distinct
                + " distinct radii over " + count + " stations; the walk is not per-station.", failures);

            RoundaboutShape rotated = RoundaboutShape.Ellipse(new Vector2(140f, -85f), a, b, 37.0);
            ShapedRoundabout.CurvatureWalk turned = ShapedRoundabout.WalkCurvature(rotated, cls, banking);
            Expect(Math.Abs(turned.tightestRadiusMetres - w.tightestRadiusMetres) < 1e-6,
                "RCMSHAPE rotating the ellipse 37 deg moved the tightest radius from " + F(w.tightestRadiusMetres)
                + " to " + F(turned.tightestRadiusMetres) + " m.", failures);
            Expect(Math.Abs(turned.widestRadiusMetres - w.widestRadiusMetres) < 1e-6,
                "RCMSHAPE rotating the ellipse moved the widest radius from " + F(w.widestRadiusMetres) + " to "
                + F(turned.widestRadiusMetres) + " m.", failures);
            RoundaboutShape.CaliperReading turnedCalipers = rotated.MeasureCalipers();
            Expect(Math.Abs(turnedCalipers.widestMetres - 2.0 * a) < 0.02
                && Math.Abs(turnedCalipers.narrowestMetres - 2.0 * b) < 0.02,
                "RCMSHAPE rotated ellipse calipers read " + F(turnedCalipers.widestMetres) + " / "
                + F(turnedCalipers.narrowestMetres) + " m, expected " + F(2.0 * a) + " / " + F(2.0 * b) + ".",
                failures);
            log.AppendLine("RCMSHAPE ellipse 30x18: radius " + F(w.tightestRadiusMetres) + " to "
                + F(w.widestRadiusMetres) + " m over " + distinct + " distinct readings; rotation-invariant.");
        }
        private static string N3(double value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary> The refusal RM-61 exists to produce. A 40 x 12 oval is tight enough at its ends that no design
        /// vehicle can track it, and the refusal must NAME the station in metres and the radius it read there together
        /// with the minimum -- all three, in the message, because a builder standing on site needs to know where to
        /// widen. The boundary is exercised in both directions so the check cannot be a constant. </summary>
        private static void CaseTightEndIsRefusedNamingStationAndRadius(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();

            RoundaboutShape tight = RoundaboutShape.Ellipse(Vector2.zero, 40.0, 12.0, 0.0);
            ShapedRoundabout.CurvatureWalk w = ShapedRoundabout.WalkCurvature(tight, cls, banking);
            Expect(!w.accepted, "RCMSHAPE a 40x12 oval (3.6 m at the ends) was accepted against a 12 m minimum.",
                failures);
            Expect(w.refusal.Contains(N3(w.tightestRadiusMetres)), "RCMSHAPE refusal does not name the radius it read ("
                + N3(w.tightestRadiusMetres) + "): " + w.refusal, failures);
            Expect(w.refusal.Contains("station " + N3(w.tightestStationMetres) + " m"),
                "RCMSHAPE refusal does not name the station: " + w.refusal, failures);
            Expect(w.refusal.Contains(N3(cls.minCirculatoryRadiusMetres)),
                "RCMSHAPE refusal does not name the class minimum: " + w.refusal, failures);
            Expect(Math.Abs(w.tightestRadiusMetres - 144.0 / 40.0) < 0.01, "RCMSHAPE 40x12 oval read "
                + F(w.tightestRadiusMetres) + " m at its tight end, expected 3.6.", failures);

            RoundaboutShape roomy = RoundaboutShape.Ellipse(Vector2.zero, 25.0, 22.0, 0.0);
            ShapedRoundabout.CurvatureWalk fine = ShapedRoundabout.WalkCurvature(roomy, cls, banking);
            Expect(fine.accepted && fine.refusal.Length == 0, "RCMSHAPE a 25x22 oval (19.4 m at the ends) was refused: "
                + fine.refusal, failures);

            RoundaboutShape justUnder = RoundaboutShape.Ellipse(Vector2.zero, 30.0, 18.9, 0.0);
            RoundaboutShape justOver = RoundaboutShape.Ellipse(Vector2.zero, 30.0, 19.0, 0.0);
            ShapedRoundabout.CurvatureWalk under = ShapedRoundabout.WalkCurvature(justUnder, cls, banking);
            ShapedRoundabout.CurvatureWalk over = ShapedRoundabout.WalkCurvature(justOver, cls, banking);
            Expect(!under.accepted, "RCMSHAPE 30x18.9 (" + F(under.tightestRadiusMetres)
                + " m) should fall just under the 12 m minimum.", failures);
            Expect(over.accepted, "RCMSHAPE 30x19 (" + F(over.tightestRadiusMetres)
                + " m) should clear the 12 m minimum.", failures);
            log.AppendLine("RCMSHAPE tight-end refusal: " + w.refusal);
        }

        /// <summary> The difference between checking an entry against the RADIAL and checking it against the LOCAL
        /// TANGENT. On a circle the two are the same and every bearing comes out exactly square, so the check must read
        /// zero skew at eight scattered bearings. On a 40 x 20 oval a leg arriving on the 45 degree bearing meets a
        /// tangent that is NOT perpendicular to its approach: the analytic angle there is 59.03 deg, i.e. 30.97 deg off
        /// square, which just breaks the authored 30 deg limit -- so the same class accepts the circle and refuses the
        /// oval leg, and the local radius at that station is neither of the oval's two extremes. </summary>
        private static void CaseEntryIsSquareOnACircleAndSkewedOnAnOval(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            RoundaboutShape circle = RoundaboutShape.Circle(new Vector2(-60f, 210f), 20.0);
            PlanXZ circleCentre = circle.MeasuredCentroid();
            double[] bearings = { 0.0, 17.0, 45.0, 90.0, 123.0, 180.0, 270.0, 333.0 };
            double worstSkew = 0.0;
            for (int i = 0; i < bearings.Length; i++)
            {
                ShapedRoundabout.EntrySolution e = ShapedRoundabout.SolveEntry(circle, cls, i, bearings[i], 60.0,
                    circleCentre);
                Expect(e.solved, "RCMSHAPE circle leg on bearing " + F(bearings[i]) + " deg failed to meet the kerb.",
                    failures);
                if (e.skewFromPerpendicularDeg > worstSkew) { worstSkew = e.skewFromPerpendicularDeg; }
                Expect(e.perpendicularEnough, "RCMSHAPE circle leg on bearing " + F(bearings[i]) + " deg read "
                    + F(e.angleToCirculatoryTangentDeg) + " deg to the tangent.", failures);
                Expect(Math.Abs(e.localRadiusMetres - 20.0) < 1e-4, "RCMSHAPE circle leg local radius "
                    + F(e.localRadiusMetres) + " m, expected 20.", failures);
            }
            Expect(worstSkew < 1e-6, "RCMSHAPE worst circle entry skew was " + F(worstSkew)
                + " deg; a radial leg into a circle is square by construction.", failures);

            RoundaboutShape oval = RoundaboutShape.Ellipse(Vector2.zero, 40.0, 20.0, 0.0);
            PlanXZ ovalCentre = oval.MeasuredCentroid();
            ShapedRoundabout.EntrySolution square0 = ShapedRoundabout.SolveEntry(oval, cls, 0, 0.0, 90.0, ovalCentre);
            ShapedRoundabout.EntrySolution square90 = ShapedRoundabout.SolveEntry(oval, cls, 1, 90.0, 90.0, ovalCentre);
            Expect(square0.skewFromPerpendicularDeg < 1e-5 && square90.skewFromPerpendicularDeg < 1e-5,
                "RCMSHAPE oval legs on the axis ends read skews " + F(square0.skewFromPerpendicularDeg) + " / "
                + F(square90.skewFromPerpendicularDeg) + " deg; both should be square.", failures);

            ShapedRoundabout.EntrySolution skewed = ShapedRoundabout.SolveEntry(oval, cls, 2, 45.0, 90.0, ovalCentre);
            Expect(Math.Abs(skewed.angleToCirculatoryTangentDeg - 59.03) < 0.05,
                "RCMSHAPE oval leg at 45 deg meets the tangent at " + F(skewed.angleToCirculatoryTangentDeg)
                + " deg, expected 59.03.", failures);
            Expect(!skewed.perpendicularEnough, "RCMSHAPE oval leg at 45 deg is " + F(skewed.skewFromPerpendicularDeg)
                + " deg off square and was accepted against a " + F(cls.maxEntrySkewFromPerpendicularDeg)
                + " deg limit.", failures);
            Expect(skewed.refusal.Contains(N3(skewed.angleToCirculatoryTangentDeg)),
                "RCMSHAPE skew refusal does not quote the angle: " + skewed.refusal, failures);
            Expect(Math.Abs(skewed.localRadiusMetres - 62.694) < 0.05, "RCMSHAPE oval leg at 45 deg read local radius "
                + F(skewed.localRadiusMetres) + " m, expected 62.694 (neither 10 nor 80).", failures);
            log.AppendLine("RCMSHAPE entries: circle square at 8 bearings; oval at 45 deg "
                + F(skewed.angleToCirculatoryTangentDeg) + " deg to the local tangent, local radius "
                + F(skewed.localRadiusMetres) + " m.");
        }
        /// <summary> The splitter island is RE-SOLVED per leg, not copied from the class. Four legs with the SAME run
        /// back from the centre meet a 40 x 20 oval at wildly different distances out -- 40 m on the long axis, 20 m on
        /// the short -- so the island left over differs by 20 m between neighbours. Then the run is shortened until the
        /// long-axis leg is short of island while its neighbour still has room, and the refusal must explain WHY by
        /// quoting how far out the kerb line sits at that bearing. </summary>
        private static void CaseSplitterIsResolvedPerLeg(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            RoundaboutShape oval = RoundaboutShape.Ellipse(Vector2.zero, 40.0, 20.0, 0.0);
            PlanXZ centre = oval.MeasuredCentroid();

            ShapedRoundabout.EntrySolution onLong = ShapedRoundabout.SolveEntry(oval, cls, 0, 0.0, 60.0, centre);
            ShapedRoundabout.EntrySolution onShort = ShapedRoundabout.SolveEntry(oval, cls, 1, 90.0, 60.0, centre);
            ShapedRoundabout.EntrySolution opposite = ShapedRoundabout.SolveEntry(oval, cls, 2, 180.0, 60.0, centre);

            Expect(Math.Abs(onLong.splitterLengthMetres - 20.0) < 0.01, "RCMSHAPE long-axis leg splitter "
                + F(onLong.splitterLengthMetres) + " m, expected 60 - 40 = 20.", failures);
            Expect(Math.Abs(onShort.splitterLengthMetres - 40.0) < 0.01, "RCMSHAPE short-axis leg splitter "
                + F(onShort.splitterLengthMetres) + " m, expected 60 - 20 = 40.", failures);
            Expect(Math.Abs(onLong.splitterLengthMetres - opposite.splitterLengthMetres) < 1e-6,
                "RCMSHAPE opposed legs on the same axis got different splitters: " + F(onLong.splitterLengthMetres)
                + " vs " + F(opposite.splitterLengthMetres) + " m.", failures);
            Expect(onLong.splitterAdequate && onShort.splitterAdequate,
                "RCMSHAPE 20 m and 40 m splitters should both clear the "
                + F(cls.minSplitterIslandLengthMetres) + " m minimum.", failures);
            Expect(Math.Abs(PlanXZ.Dot(onShort.splitterAxisXZ, new PlanXZ(0.0, 1.0)) - 1.0) < 1e-6,
                "RCMSHAPE splitter axis on the 90 deg leg is not the radial at that bearing.", failures);

            ShapedRoundabout.EntrySolution shortRun = ShapedRoundabout.SolveEntry(oval, cls, 0, 0.0, 50.0, centre);
            ShapedRoundabout.EntrySolution shortRunNeighbour = ShapedRoundabout.SolveEntry(oval, cls, 1, 90.0, 50.0,
                centre);
            Expect(!shortRun.splitterAdequate, "RCMSHAPE a 10 m splitter was accepted against a "
                + F(cls.minSplitterIslandLengthMetres) + " m minimum.", failures);
            Expect(shortRunNeighbour.splitterAdequate, "RCMSHAPE the short-axis neighbour (30 m) was refused too; the"
                + " island is not being re-solved per leg.", failures);
            Expect(shortRun.refusal.Contains(N3(shortRun.outerDistanceFromCentreMetres)),
                "RCMSHAPE splitter refusal does not say how far out the kerb line sits: " + shortRun.refusal, failures);
            Expect(shortRun.refusal.Contains(N3(cls.minSplitterIslandLengthMetres)),
                "RCMSHAPE splitter refusal does not quote the minimum: " + shortRun.refusal, failures);
            log.AppendLine("RCMSHAPE splitters on one oval with one leg run: " + F(onLong.splitterLengthMetres)
                + " m on the long axis, " + F(onShort.splitterLengthMetres) + " m on the short.");
        }

        /// <summary> Deflection is the thing that makes a roundabout slow traffic, and it is a function of SIZE: the
        /// bigger and flatter the loop, the straighter the fastest path through the entry. The three-point circle is
        /// checked against a hand-worked value on a 20 m circle (30.33 m), then walked up through six loop sizes: R1
        /// must rise every time, must clear the 100 m cap on the compact loops, and must break it on the big ones. A
        /// check that passed everywhere would prove nothing, so both verdicts are required to occur. </summary>
        private static void CaseDeflectionFailsOnAFlatLoop(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            double[] radii = { 15.0, 20.0, 30.0, 50.0, 80.0, 120.0 };
            double previous = 0.0;
            int adequate = 0;
            int inadequate = 0;
            StringBuilder line = new StringBuilder();
            for (int i = 0; i < radii.Length; i++)
            {
                RoundaboutShape circle = RoundaboutShape.Circle(Vector2.zero, radii[i]);
                ShapedRoundabout.EntrySolution e = ShapedRoundabout.SolveEntry(circle, cls, 0, 0.0,
                    radii[i] + 40.0, circle.MeasuredCentroid());
                Expect(e.fastestPathRadiusMetres > previous, "RCMSHAPE R1 did not rise from radius "
                    + F(radii[i - 1 < 0 ? 0 : i - 1]) + " to " + F(radii[i]) + " m: " + F(previous) + " then "
                    + F(e.fastestPathRadiusMetres) + " m.", failures);
                previous = e.fastestPathRadiusMetres;
                if (e.deflectionAdequate) { adequate++; } else { inadequate++; }
                if (Math.Abs(radii[i] - 20.0) < 1e-9)
                {
                    Expect(Math.Abs(e.fastestPathRadiusMetres - 30.33) < 0.1, "RCMSHAPE R1 on a 20 m circle was "
                        + F(e.fastestPathRadiusMetres) + " m, hand-worked value 30.33.", failures);
                }
                if (Math.Abs(radii[i] - 120.0) < 1e-9)
                {
                    Expect(!e.deflectionAdequate && e.refusal.Contains("does not deflect"),
                        "RCMSHAPE a 120 m circle (R1 " + F(e.fastestPathRadiusMetres)
                        + " m) should fail deflection with a message saying so: " + e.refusal, failures);
                }
                line.Append(F(radii[i])).Append(":").Append(F(e.fastestPathRadiusMetres)).Append(' ');
            }
            Expect(adequate > 0 && inadequate > 0, "RCMSHAPE deflection came out " + adequate + " adequate / "
                + inadequate + " inadequate; both verdicts must occur or the cap is untested.", failures);
            log.AppendLine("RCMSHAPE R1 by loop radius: " + line.ToString().TrimEnd());
        }

        /// <summary> The implied circulatory speed must be the tool's ONE speed-radius law, solved backwards, not a
        /// second copy of the arithmetic. Three things are proved: the returned speed is a true fixed point of
        /// V^2 = 127 R (e + f(V)); feeding it back through `RequiredSuperelevation` returns the banking that was asked
        /// for; and the roundabout walk's reported speed is bit-for-bit the same call, with the circulatory cross-slope
        /// entered negated because the loop drains outward. Degenerate inputs return zero rather than a NaN. </summary>
        private static void CaseImpliedSpeedIsTheOneLawSolvedBackwards(List<string> failures, StringBuilder log)
        {
            SuperelevationClass banking = SuperelevationClass.Default();
            double[] radii = { 12.0, 15.0, 20.0, 30.0, 50.0, 100.0 };
            double[] slopes = { -0.02, 0.0, 0.04 };
            for (int s = 0; s < slopes.Length; s++)
            {
                double previous = 0.0;
                for (int i = 0; i < radii.Length; i++)
                {
                    double speed = Superelevation.SpeedFromRadiusKmh(radii[i], slopes[s], banking);
                    double f = Superelevation.SideFrictionFactor(speed, banking);
                    double residual = Math.Abs(speed * speed - 127.0 * radii[i] * (slopes[s] + f));
                    Expect(residual / (speed * speed) < 1e-9, "RCMSHAPE speed " + F(speed) + " km/h at radius "
                        + F(radii[i]) + " m and e=" + F(slopes[s]) + " is not a fixed point (residual "
                        + F(residual) + ").", failures);
                    double back = Superelevation.RequiredSuperelevation(speed, radii[i], f);
                    double expected = slopes[s] > 0.0 ? slopes[s] : 0.0;
                    Expect(Math.Abs(back - expected) < 1e-9, "RCMSHAPE round trip through RequiredSuperelevation gave "
                        + F(back) + ", expected " + F(expected) + ".", failures);
                    Expect(speed > previous, "RCMSHAPE speed did not rise with radius at e=" + F(slopes[s]) + ": "
                        + F(previous) + " then " + F(speed) + " km/h.", failures);
                    previous = speed;
                }
            }
            Expect(Superelevation.SpeedFromRadiusKmh(0.0, 0.0, banking) == 0.0
                && Superelevation.SpeedFromRadiusKmh(-5.0, 0.0, banking) == 0.0,
                "RCMSHAPE a non-positive radius should imply no speed at all.", failures);
            Expect(Superelevation.SpeedFromRadiusKmh(20.0, -0.2, banking) == 0.0,
                "RCMSHAPE adverse banking beyond the friction available should imply no speed, not a NaN.", failures);

            RoundaboutClass cls = RoundaboutClass.Default();
            RoundaboutShape oval = RoundaboutShape.Ellipse(Vector2.zero, 34.0, 26.0, 0.0);
            ShapedRoundabout.CurvatureWalk w = ShapedRoundabout.WalkCurvature(oval, cls, banking);
            double delegated = Superelevation.SpeedFromRadiusKmh(w.tightestRadiusMetres,
                -cls.circulatoryCrossSlopeOutward, banking);
            Expect(w.impliedSpeedAtTightestKmh == delegated, "RCMSHAPE the walk reported "
                + F(w.impliedSpeedAtTightestKmh) + " km/h but the law returns " + F(delegated)
                + "; the relation has been copied instead of called.", failures);
            log.AppendLine("RCMSHAPE implied speed: fixed point holds over 18 radius/slope pairs; oval 34x26 tightest "
                + F(w.tightestRadiusMetres) + " m implies " + F(w.impliedSpeedAtTightestKmh) + " km/h.");
        }
        /// <summary> The control-polygon kind is a CLOSED, curvature-continuous loop that deliberately does NOT pass
        /// through its control points. That last part is the honest cost of the choice and is asserted, not hidden: a
        /// square hull at +/-25 m starts the curve at (-16.67, 16.67), nearly 12 m from the nearest corner it was drawn
        /// from. Curvature is sampled either side of every segment joint, because a merely C1 fit (joined arcs, or
        /// Catmull-Rom through the points) would JUMP there and a curvature walk over it would accept or refuse
        /// depending on which side of a joint a station happened to land. </summary>
        private static void CasePolygonIsClosedSmoothAndNotInterpolating(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();
            List<Vector2> hull = new List<Vector2>
            {
                new Vector2(25f, 25f), new Vector2(-25f, 25f), new Vector2(-25f, -25f), new Vector2(25f, -25f)
            };
            RoundaboutShape polygon = RoundaboutShape.Polygon(hull);
            string reason;
            Expect(polygon.IsUsable(out reason), "RCMSHAPE a four-point control polygon was rejected: " + reason,
                failures);

            PlanXZ start = polygon.PointAt(0.0);
            Expect(Math.Abs(start.x + 100.0 / 6.0) < 1e-9 && Math.Abs(start.z - 100.0 / 6.0) < 1e-9,
                "RCMSHAPE polygon start " + F(start.x) + ", " + F(start.z) + " is not the B-spline value "
                + F(-100.0 / 6.0) + ", " + F(100.0 / 6.0) + ".", failures);
            double nearestHull = double.MaxValue;
            for (int i = 0; i < hull.Count; i++)
            {
                double d = PlanXZ.Distance(start, PlanXZ.FromVector2(hull[i]));
                if (d < nearestHull) { nearestHull = d; }
            }
            Expect(nearestHull > 5.0, "RCMSHAPE the curve came within " + F(nearestHull)
                + " m of a control point; it is being treated as interpolating when it is not.", failures);

            Expect(PlanXZ.Distance(polygon.PointAt(0.0), polygon.PointAt(1.0)) < 1e-9,
                "RCMSHAPE the control polygon does not close: t=0 and t=1 are apart.", failures);
            for (int j = 0; j < hull.Count; j++)
            {
                double joint = (double)j / hull.Count;
                double before = polygon.CurvatureRadiusAt(joint - 1e-4 + (j == 0 ? 1.0 : 0.0));
                double after = polygon.CurvatureRadiusAt(joint + 1e-4);
                double relative = Math.Abs(before - after) / after;
                Expect(relative < 0.005, "RCMSHAPE curvature jumps across joint " + j + ": " + F(before) + " m then "
                    + F(after) + " m (" + F(relative * 100.0) + " %). The fit is not curvature-continuous.", failures);
            }

            PlanXZ centroid = polygon.MeasuredCentroid();
            Expect(centroid.Length < 1e-4, "RCMSHAPE the measured centroid of a symmetric hull sits at " + F(centroid.x)
                + ", " + F(centroid.z) + " rather than the origin.", failures);
            double perimeter = polygon.PerimeterMetres();
            Expect(perimeter > 0.0 && perimeter < 200.0, "RCMSHAPE polygon perimeter " + F(perimeter)
                + " m should be positive and inside the 200 m hull.", failures);

            List<double> bearings = new List<double> { 0.0, 90.0, 180.0, 270.0 };
            ShapedRoundabout.ShapedResolve r = ShapedRoundabout.Resolve(polygon, cls, banking, bearings, null);
            Expect(r.entries.Length == 4, "RCMSHAPE the polygon resolve produced " + r.entries.Length
                + " entries for four legs.", failures);
            for (int i = 0; i < r.entries.Length; i++)
            {
                Expect(r.entries[i].solved && !double.IsNaN(r.entries[i].localRadiusMetres),
                    "RCMSHAPE leg " + i + " on the control polygon did not resolve to a station.", failures);
            }
            log.AppendLine("RCMSHAPE control polygon: perimeter " + F(perimeter) + " m inside a 200 m hull, start "
                + F(nearestHull) + " m off the nearest control point, curvature continuous across all four joints.");
        }
        /// <summary> A shape that cannot be a loop is REFUSED with a reason, never measured anyway. Two control points
        /// cannot close, an ellipse with a zero axis is a line, a zero-radius circle is a point, and a null shape is
        /// nothing at all -- each must come back unusable, with an empty entry list and no NaN masquerading as a
        /// measurement. A leg asked for against such a shape says it never met the kerb line instead of returning a
        /// station it invented. </summary>
        private static void CaseDegenerateShapesRefuseInsteadOfMeasuring(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();
            List<double> bearings = new List<double> { 0.0, 120.0, 240.0 };

            RoundaboutShape twoPoints = RoundaboutShape.Polygon(new List<Vector2>
                { new Vector2(0f, 0f), new Vector2(20f, 0f) });
            RoundaboutShape flatEllipse = RoundaboutShape.Ellipse(Vector2.zero, 30.0, 0.0, 0.0);
            RoundaboutShape pointCircle = RoundaboutShape.Circle(Vector2.zero, 0.0);
            RoundaboutShape negative = RoundaboutShape.Ellipse(Vector2.zero, 30.0, -8.0, 0.0);
            RoundaboutShape[] bad = { twoPoints, flatEllipse, pointCircle, negative };
            string[] names = { "two control points", "zero-minor ellipse", "zero-radius circle", "negative minor axis" };

            for (int i = 0; i < bad.Length; i++)
            {
                string reason;
                Expect(!bad[i].IsUsable(out reason), "RCMSHAPE " + names[i] + " was reported usable.", failures);
                Expect(reason != null && reason.Length > 0, "RCMSHAPE " + names[i]
                    + " was refused without saying why.", failures);
                ShapedRoundabout.ShapedResolve r = ShapedRoundabout.Resolve(bad[i], cls, banking, bearings, null);
                Expect(!r.accepted && r.refusal.StartsWith("Shape cannot be walked"), "RCMSHAPE " + names[i]
                    + " resolved to: '" + r.refusal + "'.", failures);
                Expect(!r.curvature.shapeUsable && r.entries.Length == 0, "RCMSHAPE " + names[i]
                    + " still produced " + r.entries.Length + " entries.", failures);
                Expect(!double.IsNaN(r.perimeterMetres) && !double.IsNaN(r.narrowestAxisMetres)
                    && !double.IsNaN(r.impliedCirculatorySpeedKmh), "RCMSHAPE " + names[i]
                    + " reported a NaN measurement.", failures);
                ShapedRoundabout.EntrySolution e = ShapedRoundabout.SolveEntry(bad[i], cls, 0, 0.0, 60.0,
                    new PlanXZ(0.0, 0.0));
                Expect(!e.solved && !e.accepted && e.refusal.Contains("degenerate"), "RCMSHAPE a leg against "
                    + names[i] + " came back as '" + e.refusal + "'.", failures);
            }

            ShapedRoundabout.ShapedResolve nothing = ShapedRoundabout.Resolve(null, cls, banking, bearings, null);
            Expect(!nothing.accepted && nothing.refusal.Contains("no shape supplied"),
                "RCMSHAPE a null shape resolved to: '" + nothing.refusal + "'.", failures);
            log.AppendLine("RCMSHAPE four degenerate shapes and a null shape all refused with a reason, no entries, "
                + "no NaN.");
        }

        /// <summary> Editing the shape must RE-SOLVE everything, and re-solving must be deterministic. One shape object
        /// is measured as a 26 m circle, mutated into a 44 x 18 oval (every derived quantity has to move), then mutated
        /// straight back -- and the third resolve must equal the first FIELD FOR FIELD, exactly, not nearly. That is
        /// two properties in one: nothing is cached from the oval, and two identical inputs give bit-identical outputs
        /// (C-06). Comparisons are `==` on purpose; a tolerance here would hide exactly the staleness being hunted. </summary>
        private static void CaseShapeChangeResolvesEverything(List<string> failures, StringBuilder log)
        {
            RoundaboutClass cls = RoundaboutClass.Default();
            SuperelevationClass banking = SuperelevationClass.Default();
            List<double> bearings = new List<double> { 0.0, 75.0, 160.0, 250.0 };
            RoundaboutShape shape = RoundaboutShape.Circle(Vector2.zero, 26.0);

            ShapedRoundabout.ShapedResolve first = ShapedRoundabout.Resolve(shape, cls, banking, bearings, null);
            shape.kind = RoundaboutShapeKind.Ellipse;
            shape.semiMajorMetres = 44.0;
            shape.semiMinorMetres = 18.0;
            ShapedRoundabout.ShapedResolve oval = ShapedRoundabout.Resolve(shape, cls, banking, bearings, null);
            shape.kind = RoundaboutShapeKind.Circle;
            shape.semiMajorMetres = 26.0;
            shape.semiMinorMetres = 26.0;
            ShapedRoundabout.ShapedResolve back = ShapedRoundabout.Resolve(shape, cls, banking, bearings, null);

            Expect(oval.perimeterMetres != first.perimeterMetres
                && oval.curvature.tightestRadiusMetres != first.curvature.tightestRadiusMetres
                && oval.narrowestAxisMetres != first.narrowestAxisMetres,
                "RCMSHAPE the oval measured the same as the circle; the shape edit did not reach the measurements.",
                failures);

            Expect(back.perimeterMetres == first.perimeterMetres, "RCMSHAPE perimeter after the round trip "
                + F(back.perimeterMetres) + " m, first pass " + F(first.perimeterMetres) + " m.", failures);
            Expect(back.narrowestAxisMetres == first.narrowestAxisMetres
                && back.widestAxisMetres == first.widestAxisMetres
                && back.measuredInscribedCircleDiameterMetres == first.measuredInscribedCircleDiameterMetres,
                "RCMSHAPE the measured axes did not return to the circle's values.", failures);
            Expect(back.curvature.tightestRadiusMetres == first.curvature.tightestRadiusMetres
                && back.curvature.tightestStationMetres == first.curvature.tightestStationMetres
                && back.curvature.widestRadiusMetres == first.curvature.widestRadiusMetres
                && back.curvature.stations == first.curvature.stations,
                "RCMSHAPE the curvature walk did not return to the circle's values.", failures);
            Expect(back.impliedCirculatorySpeedKmh == first.impliedCirculatorySpeedKmh
                && back.accepted == first.accepted && back.refusal == first.refusal,
                "RCMSHAPE the verdict or implied speed changed across the round trip.", failures);
            Expect(back.entries.Length == first.entries.Length, "RCMSHAPE entry count changed across the round trip.",
                failures);
            for (int i = 0; i < first.entries.Length && i < back.entries.Length; i++)
            {
                ShapedRoundabout.EntrySolution a = first.entries[i];
                ShapedRoundabout.EntrySolution b = back.entries[i];
                Expect(a.stationMetres == b.stationMetres && a.localRadiusMetres == b.localRadiusMetres
                    && a.angleToCirculatoryTangentDeg == b.angleToCirculatoryTangentDeg
                    && a.fastestPathRadiusMetres == b.fastestPathRadiusMetres
                    && a.splitterLengthMetres == b.splitterLengthMetres
                    && a.outerDistanceFromCentreMetres == b.outerDistanceFromCentreMetres
                    && a.accepted == b.accepted && a.refusal == b.refusal,
                    "RCMSHAPE leg " + i + " did not re-solve to the same numbers after the shape round trip.",
                    failures);
                Expect(oval.entries[i].stationMetres != a.stationMetres
                    || oval.entries[i].localRadiusMetres != a.localRadiusMetres,
                    "RCMSHAPE leg " + i + " read the same station AND radius on the oval as on the circle.", failures);
            }
            log.AppendLine("RCMSHAPE circle -> 44x18 oval -> circle: every measurement and all four entries identical "
                + "to the first pass, and every leg moved on the oval.");
        }
    }
}
#endif
