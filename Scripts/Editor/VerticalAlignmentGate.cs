#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.City;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-95's gate: proves the vertical alignment is real engineering rather than a table typed from memory.
    ///
    /// The central case is the one that would be easiest to leave out. <see cref="VerticalAlignment"/> computes stopping
    /// sight distance from the AASHTO relation and rounds up to the next 5 m; the standard's PUBLISHED design table is
    /// held separately in <see cref="VerticalDesignClass.publishedStoppingSightMetres"/>; and this gate asserts the two
    /// agree at every one of the ten tabulated design speeds. If the reaction time, the deceleration coefficient or
    /// either metric constant were mistyped, that agreement would break somewhere along the table and this gate would
    /// name the speed. That is the difference between citing a standard and remembering one, and RM-94's census is why
    /// the distinction is worth a gate: transcribed numbers are exactly the population that turns out to be three magic
    /// constants encoding one real one.
    ///
    /// The K values are NOT asserted against a transcribed column, deliberately. Published K tables are rounded
    /// inconsistently -- up in some rows, to a decimal place in the lowest -- and asserting integers recalled from a
    /// printed page would be the very thing this module refuses to do. Instead the gate asserts the two things that are
    /// actually checkable: that the eye, object, headlight and beam-divergence figures reproduce the standard's own
    /// published SIMPLIFICATIONS of the relations (the 658 denominator for crests, the 120 + 3.5S for sags), and that
    /// each K round-trips back to the sight distance that produced it. A K table cannot be right in any other sense.
    ///
    /// Report lines are prefixed RCMVERTICAL. </summary>
    public static class VerticalAlignmentGate
    {
        [MenuItem("Window/Road City Master/Testing/Vertical alignment (grades, crests, sags, sight distance)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.VerticalAlignmentGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMVERTICAL BEGIN\n");
            List<string> failures = new List<string>();

            CaseTheRelationReproducesThePublishedSightTable(builder, failures);
            CaseTheHeightsReproduceTheStandardsOwnConstants(builder, failures);
            CaseEveryKRoundTripsToItsSightDistance(builder, failures);
            CaseSightDistanceLengthensDownhill(builder, failures);
            CaseAnOverSteepGradeIsReportedWithItsStationAndLimit(builder, failures);
            CaseAShortCurveIsReportedWithTheLengthItNeeds(builder, failures);
            CaseADeficientSightDistanceIsReported(builder, failures);
            CaseTheProfileHasNoKinkAndNoReversal(builder, failures);
            CaseADraggedNodeMovesTheNumbersSmoothly(builder, failures);
            CaseOverlappingCurvesAreClippedAndSaidSo(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMVERTICAL FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMVERTICAL result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMVERTICAL END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        // ---- the cited relation against the cited table ----

        /// <summary> SSD = 0.278 V t + V-squared / (254 (f +/- G)), rounded up to the next 5 m, equals the standard's
        /// published design stopping-sight distance at all ten tabulated speeds. </summary>
        private static void CaseTheRelationReproducesThePublishedSightTable(StringBuilder builder,
            List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            Expect(design.tabulatedSpeedsKmh.Length == design.publishedStoppingSightMetres.Length,
                "sight-table: the speed and published-distance arrays are different lengths (setup)", failures);

            int matched = 0;
            int n = Math.Min(design.tabulatedSpeedsKmh.Length, design.publishedStoppingSightMetres.Length);
            for (int i = 0; i < n; i++)
            {
                double speed = design.tabulatedSpeedsKmh[i];
                double derived = VerticalAlignment.DesignStoppingSightMetres(speed, design);
                double published = design.publishedStoppingSightMetres[i];
                if (Math.Abs(derived - published) < 1e-9) { matched++; continue; }
                failures.Add(Fmt("sight-table: at {0:0} km/h the relation gives {1:0.0} m but the published design "
                    + "value is {2:0.0} m -- one of the reaction time, the deceleration coefficient or a metric "
                    + "constant is wrong", speed, derived, published));
            }
            builder.Append("RCMVERTICAL ").Append("published-sight-table".PadRight(26))
                   .Append(matched).Append(" of ").Append(n)
                   .Append(" design speeds reproduce the published SSD exactly\n");
        }

        /// <summary> The eye, object, headlight and beam figures reproduce the standard's own printed simplifications:
        /// 200 (sqrt(1.08) + sqrt(0.60)) squared is 658, and 200 x 0.60 with 200 x tan(1 degree) is 120 + 3.5 S. If a
        /// height were mistyped these constants would drift, and every K in the module with them. </summary>
        private static void CaseTheHeightsReproduceTheStandardsOwnConstants(StringBuilder builder,
            List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();

            double roots = Math.Sqrt(design.driverEyeHeightMetres) + Math.Sqrt(design.objectHeightMetres);
            double crestDenominator = 200.0 * roots * roots;
            Expect(Math.Abs(crestDenominator - 658.0) < 0.5,
                Fmt("constants: the crest denominator from the published eye ({0:0.00} m) and object ({1:0.00} m) "
                    + "heights is {2:0.0}, but the standard prints 658",
                    design.driverEyeHeightMetres, design.objectHeightMetres, crestDenominator), failures);

            double sagConstant = 200.0 * design.headlightHeightMetres;
            Expect(Math.Abs(sagConstant - 120.0) < 1e-9,
                Fmt("constants: the sag constant term is {0:0.0}, but 200 x the published 0.60 m headlight height "
                    + "is 120", sagConstant), failures);

            double sagSlope = 200.0 * Math.Tan(design.headlightBeamDivergenceDegrees * Math.PI / 180.0);
            Expect(Math.Abs(sagSlope - 3.5) < 0.02,
                Fmt("constants: the sag slope term is {0:0.000}, but 200 x tan(1 degree) is the 3.5 the standard "
                    + "prints", sagSlope), failures);

            builder.Append("RCMVERTICAL ").Append("standard-constants".PadRight(26))
                   .Append(Fmt("crest 200(sqrt h1 + sqrt h2)^2 = {0:0.0}, sag 200h + 200 tan b S = {1:0.0} + {2:0.00}S",
                       crestDenominator, sagConstant, sagSlope)).Append("\n");
        }

        /// <summary> Every K, inverted, gives back the sight distance that produced it. A K value cannot be correct in
        /// any sense other than this one, and the inversion is what <see cref="VerticalAlignment.CheckStoppingSight"/>
        /// relies on to say how much sight distance a curve as built actually offers. </summary>
        private static void CaseEveryKRoundTripsToItsSightDistance(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            double worstCrest = 0.0;
            double worstSag = 0.0;

            for (int i = 0; i < design.tabulatedSpeedsKmh.Length; i++)
            {
                double speed = design.tabulatedSpeedsKmh[i];
                double sight = VerticalAlignment.DesignStoppingSightMetres(speed, design);

                // A curve of exactly the required length delivers exactly the required K, so the reported available
                // sight distance must come back as the design distance.
                const double a = 4.0;   // any grade change; K = L / A cancels it out again.
                double crestLength = VerticalAlignment.CrestK(sight, design) * a;
                ProfileFinding crest = VerticalAlignment.CheckStoppingSight(0.0, crestLength, 2.0, -2.0, speed, design);
                double crestError = Math.Abs(crest.Value - sight);
                if (crestError > worstCrest) { worstCrest = crestError; }
                Expect(crestError < 1e-6,
                    Fmt("k-roundtrip: at {0:0} km/h a crest built to its required length reports {1:0.000} m of sight "
                        + "distance instead of the {2:0.0} m it was sized for", speed, crest.Value, sight), failures);

                double sagLength = VerticalAlignment.SagK(sight, design) * a;
                ProfileFinding sag = VerticalAlignment.CheckStoppingSight(0.0, sagLength, -2.0, 2.0, speed, design);
                double sagError = Math.Abs(sag.Value - sight);
                if (sagError > worstSag) { worstSag = sagError; }
                Expect(sagError < 1e-6,
                    Fmt("k-roundtrip: at {0:0} km/h a sag built to its required length reports {1:0.000} m of sight "
                        + "distance instead of the {2:0.0} m it was sized for", speed, sag.Value, sight), failures);
            }

            builder.Append("RCMVERTICAL ").Append("k-roundtrip".PadRight(26))
                   .Append(Fmt("worst crest error {0:0.0e+0} m, worst sag error {1:0.0e+0} m over 10 speeds",
                       worstCrest, worstSag)).Append("\n");
        }

        /// <summary> The +/- G term is not decoration: braking downhill takes longer, so a falling grade must LENGTHEN
        /// the requirement and a rising one shorten it. Getting this sign backwards is a silent error that only shows up
        /// as a road that passes its checks and kills people on the downgrade, so it gets its own case. </summary>
        private static void CaseSightDistanceLengthensDownhill(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            double level = VerticalAlignment.StoppingSightDistanceMetres(100.0, 0.0, design);
            double downhill = VerticalAlignment.StoppingSightDistanceMetres(100.0, -6.0, design);
            double uphill = VerticalAlignment.StoppingSightDistanceMetres(100.0, 6.0, design);

            Expect(downhill > level + 1.0,
                Fmt("grade-term: a 6% downgrade needs {0:0.0} m against {1:0.0} m level -- the grade term's sign is "
                    + "inverted", downhill, level), failures);
            Expect(uphill < level - 1.0,
                Fmt("grade-term: a 6% upgrade needs {0:0.0} m against {1:0.0} m level -- the grade term's sign is "
                    + "inverted", uphill, level), failures);

            builder.Append("RCMVERTICAL ").Append("grade-term".PadRight(26))
                   .Append(Fmt("at 100 km/h: {0:0.0} m uphill, {1:0.0} m level, {2:0.0} m downhill",
                       uphill, level, downhill)).Append("\n");
        }

        // ---- the three reports the task asks for by name ----

        /// <summary> A grade steeper than its class's maximum is reported at the offending station, with the grade, the
        /// limit and the class named. The limit comes from RoadClassDefaults, so no grade figure is restated. </summary>
        private static void CaseAnOverSteepGradeIsReportedWithItsStationAndLimit(StringBuilder builder,
            List<string> failures)
        {
            RoadClassProfile motorway = RoadClassDefaults.For(Road.RoadClassEnum.Motorway);
            double limitPercent = motorway.MaxGrade * 100.0;
            Expect(limitPercent > 0.0,
                "over-steep: the Motorway class declares no maximum grade, so nothing can be checked against it "
                + "(setup)", failures);

            double tooSteep = limitPercent + 2.0;
            ProfileFinding bad = VerticalAlignment.CheckGrade(214.0, tooSteep, Road.RoadClassEnum.Motorway);
            Expect(!bad.Ok, Fmt("over-steep: a {0:0.0}% grade passed a {1:0.0}% limit", tooSteep, limitPercent),
                failures);
            if (!string.IsNullOrEmpty(bad.Message))
            {
                Expect(bad.Message.Contains("214"),
                    "over-steep: the report does not say where -- the station is missing from '" + bad.Message + "'",
                    failures);
                Expect(bad.Message.Contains("Motorway"),
                    "over-steep: the report does not name the road class it applied", failures);
                Expect(bad.Message.Contains(motorway.MaxGradeCite.Reference),
                    "over-steep: the report does not carry the limit's citation, so the author cannot check it",
                    failures);
            }
            else
            {
                failures.Add("over-steep: the finding failed but carries no message");
            }

            // A falling grade of the same magnitude is equally illegal: the check is on magnitude, not on sign.
            ProfileFinding falling = VerticalAlignment.CheckGrade(214.0, -tooSteep, Road.RoadClassEnum.Motorway);
            Expect(!falling.Ok, "over-steep: a downgrade past the limit passed, so the check is signed", failures);

            ProfileFinding fine = VerticalAlignment.CheckGrade(10.0, limitPercent - 0.5, Road.RoadClassEnum.Motorway);
            Expect(fine.Ok, "over-steep: a grade inside the limit was reported as a failure", failures);

            builder.Append("RCMVERTICAL ").Append("grade-limit".PadRight(26))
                   .Append(Fmt("Motorway limit {0:0.0}%, a {1:0.0}% grade is refused with its station and citation",
                       limitPercent, tooSteep)).Append("\n");
        }

        /// <summary> A curve shorter than L = K x A is reported with the length it needs, and the report names which
        /// curve kind it was measuring so the author knows which requirement bit. </summary>
        private static void CaseAShortCurveIsReportedWithTheLengthItNeeds(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            const double speed = 100.0;
            const double gIn = 3.0;
            const double gOut = -3.0;    // a crest, A = 6%.

            double required = VerticalAlignment.RequiredCurveLengthMetres(gIn, gOut, speed, design);
            Expect(required > 0.0, "short-curve: a 6% grade change requires no curve at all (setup)", failures);

            ProfileFinding bad = VerticalAlignment.CheckCurveLength(88.0, required * 0.5, gIn, gOut, speed, design);
            Expect(!bad.Ok, "short-curve: a curve at half its required length passed", failures);
            if (!string.IsNullOrEmpty(bad.Message))
            {
                Expect(bad.Message.Contains("crest"),
                    "short-curve: the report does not say it was a crest, so the author cannot tell which requirement "
                    + "applied", failures);
                Expect(bad.Message.Contains("88"),
                    "short-curve: the report does not say where the curve is", failures);
                Expect(bad.Limit > bad.Value,
                    "short-curve: the required length is not greater than the actual, so the finding is incoherent",
                    failures);
            }
            else
            {
                failures.Add("short-curve: the finding failed but carries no message");
            }

            ProfileFinding fine = VerticalAlignment.CheckCurveLength(88.0, required, gIn, gOut, speed, design);
            Expect(fine.Ok, "short-curve: a curve at exactly its required length was reported as too short", failures);

            // The 0.6V floor bites where L = K x A would not: a grade change so small that the parabola would be
            // shorter than three seconds of travel still gets a curve.
            double tiny = VerticalAlignment.RequiredCurveLengthMetres(0.1, 0.0, speed, design);
            Expect(tiny >= design.minimumCurveLengthPerKph * speed - 1e-9,
                Fmt("short-curve: a 0.1% grade change produced a {0:0.0} m curve, below the {1:0.0} m minimum length",
                    tiny, design.minimumCurveLengthPerKph * speed), failures);

            builder.Append("RCMVERTICAL ").Append("curve-length".PadRight(26))
                   .Append(Fmt("a 6% crest at 100 km/h needs {0:0.0} m; the 0.6V floor holds a flat change at {1:0.0} m",
                       required, tiny)).Append("\n");
        }

        /// <summary> A curve that is long enough for its grade change but still hides the road is reported with both the
        /// available and the required distance. The two checks are genuinely different: length is measured against the
        /// design table, sight distance against the LOCAL grade, and a crest at the bottom of a downgrade fails the
        /// second while passing the first. </summary>
        private static void CaseADeficientSightDistanceIsReported(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            const double speed = 100.0;
            const double gIn = 2.0;
            const double gOut = -6.0;    // a crest onto a steep downgrade, A = 8%.

            double required = VerticalAlignment.StoppingSightDistanceMetres(speed, gOut, design);
            double flat = VerticalAlignment.StoppingSightDistanceMetres(speed, 0.0, design);
            Expect(required > flat,
                "deficient-sight: the downgrade did not raise the requirement, so the case proves nothing (setup)",
                failures);

            // Size the crest for LEVEL sight distance, which is what a design table alone would give, then check it
            // against the requirement on the grade it actually lands on.
            double lengthForFlat = VerticalAlignment.CrestK(flat, design)
                * VerticalAlignment.AlgebraicDifferencePercent(gIn, gOut);
            ProfileFinding bad = VerticalAlignment.CheckStoppingSight(300.0, lengthForFlat, gIn, gOut, speed, design);
            Expect(!bad.Ok,
                Fmt("deficient-sight: a crest sized for the level requirement ({0:0.0} m) passed on a {1:0.0}% "
                    + "downgrade that needs {2:0.0} m", flat, gOut, required), failures);
            if (!string.IsNullOrEmpty(bad.Message))
            {
                Expect(bad.Message.Contains("300"),
                    "deficient-sight: the report does not say where", failures);
                Expect(bad.Value > 0.0 && bad.Limit > bad.Value,
                    Fmt("deficient-sight: the finding reports {0:0.0} m available against {1:0.0} m required, which is "
                        + "not a deficiency", bad.Value, bad.Limit), failures);
            }
            else
            {
                failures.Add("deficient-sight: the finding failed but carries no message");
            }

            // Sized for the real requirement, it passes.
            double lengthForGrade = VerticalAlignment.CrestK(required, design)
                * VerticalAlignment.AlgebraicDifferencePercent(gIn, gOut);
            ProfileFinding fine = VerticalAlignment.CheckStoppingSight(300.0, lengthForGrade, gIn, gOut, speed, design);
            Expect(fine.Ok,
                Fmt("deficient-sight: a crest sized for the on-grade requirement still reports only {0:0.0} m against "
                    + "{1:0.0} m", fine.Value, fine.Limit), failures);

            // A tangent grade cannot fail: there is no profile in the way.
            ProfileFinding tangent = VerticalAlignment.CheckStoppingSight(0.0, 0.0, -3.0, -3.0, speed, design);
            Expect(tangent.Ok, "deficient-sight: a straight grade was reported as hiding the road", failures);

            builder.Append("RCMVERTICAL ").Append("sight-distance".PadRight(26))
                   .Append(Fmt("level needs {0:0.0} m, the same crest on a {1:0.0}% downgrade needs {2:0.0} m and is "
                       + "refused", flat, gOut, required)).Append("\n");
        }

        // ---- the continuity claim ----

        /// <summary> The task's central behavioural claim: a vertically dragged node produces a CONTINUOUS grade -- no
        /// kink, no reversal. Proved by sampling the finished profile at 1 m and differentiating numerically: the grade
        /// between consecutive samples may only change by the parabola's own rate, and a kink would show up as an
        /// instant jump of the whole algebraic difference. It must also never leave the band its two grades bound, which
        /// is what a reversal would look like. </summary>
        private static void CaseTheProfileHasNoKinkAndNoReversal(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            const double speed = 80.0;
            const double step = 1.0;

            List<VerticalPoint> vpis = new List<VerticalPoint>
            {
                new VerticalPoint(0.0, 0.0),
                new VerticalPoint(200.0, 12.0),    // +6%
                new VerticalPoint(500.0, 12.0),    //  0%  -- a crest
                new VerticalPoint(800.0, 0.0),     // -4%  -- a second crest
            };

            List<VerticalPoint> samples = VerticalAlignment.Sample(vpis, step, speed, design);
            Expect(samples.Count > 100, "continuity: too few samples to differentiate (setup)", failures);

            double worstJump = 0.0;
            double worstJumpStation = 0.0;
            double previousGrade = double.NaN;
            double lowest = double.MaxValue;
            double highest = double.MinValue;

            for (int i = 1; i < samples.Count; i++)
            {
                double run = samples[i].StationMetres - samples[i - 1].StationMetres;
                if (run <= 1e-9) { continue; }
                double grade = ((samples[i].ElevationMetres - samples[i - 1].ElevationMetres) / run) * 100.0;
                if (grade < lowest) { lowest = grade; }
                if (grade > highest) { highest = grade; }

                if (!double.IsNaN(previousGrade))
                {
                    double jump = Math.Abs(grade - previousGrade);
                    if (jump > worstJump) { worstJump = jump; worstJumpStation = samples[i].StationMetres; }
                }
                previousGrade = grade;
            }

            // The steepest legitimate rate of change here is A / L. The longest A is 6% over the shortest curve the
            // clipper allows, so half a percent per metre is a generous ceiling that a kink of 6% or 4% cannot hide in.
            Expect(worstJump < 0.5,
                Fmt("continuity: the grade jumps {0:0.00} percentage points between two 1 m samples at station "
                    + "{1:0.0} m -- that is a kink, not a curve", worstJump, worstJumpStation), failures);

            // No sampled grade may exceed the band the authored grades bound. Outside it means the parabola overshot,
            // which is the reversal the task asks about.
            Expect(lowest > -4.0 - 0.2 && highest < 6.0 + 0.2,
                Fmt("continuity: sampled grades run from {0:0.00}% to {1:0.00}%, outside the -4% to +6% the authored "
                    + "profile bounds -- a curve has reversed", lowest, highest), failures);

            // The ends are pinned: fitting must not move the author's own vertical points.
            Expect(Math.Abs(samples[0].ElevationMetres - 0.0) < 1e-9
                && Math.Abs(samples[samples.Count - 1].ElevationMetres - 0.0) < 1e-9,
                "continuity: the profile's first or last elevation moved, so the fit is not anchored to the author's "
                + "own points", failures);

            builder.Append("RCMVERTICAL ").Append("continuity".PadRight(26))
                   .Append(Fmt("{0} samples over 800 m, worst grade step {1:0.000} pp, band {2:0.00}% to {3:0.00}%",
                       samples.Count, worstJump, lowest, highest)).Append("\n");
        }

        /// <summary> Dragging a node vertically moves the reported numbers SMOOTHLY -- the task's other continuity claim,
        /// and a different one from the shape of a single profile. This is about the model's response to EDITING: nudging
        /// a node up by 10 cm at a time must move the crest's crown by a bounded amount each time and always upwards,
        /// never snapping as some internal branch flips.
        ///
        /// The drag deliberately crosses the point where the required curve stops fitting between its neighbours and the
        /// clipper takes over -- at 80 km/h with 200 m tangents that happens at about 7.8 m of lift. The response's
        /// RATE genuinely changes there, because the curve has stopped growing, and that is correct rather than a
        /// defect; so the assertion is on the response's SIZE, which must stay bounded and signed. Asserting a constant
        /// rate would have failed on a model that is behaving properly, which is a worse outcome than not checking. </summary>
        private static void CaseADraggedNodeMovesTheNumbersSmoothly(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            const double speed = 80.0;
            const double dragStep = 0.1;

            double previousCrown = double.NaN;
            double biggestResponse = 0.0;
            double smallestResponse = double.MaxValue;
            double first = 0.0;
            double last = 0.0;
            int steps = 0;

            for (double lift = 6.0; lift <= 10.0 + 1e-9; lift += dragStep)
            {
                List<VerticalPoint> vpis = new List<VerticalPoint>
                {
                    new VerticalPoint(0.0, 0.0),
                    new VerticalPoint(200.0, lift),
                    new VerticalPoint(400.0, 0.0),
                };
                double crown = VerticalAlignment.ElevationAt(vpis, 200.0, speed, design);
                if (steps == 0) { first = crown; }
                last = crown;

                if (!double.IsNaN(previousCrown))
                {
                    double response = crown - previousCrown;
                    if (response > biggestResponse) { biggestResponse = response; }
                    if (response < smallestResponse) { smallestResponse = response; }
                }
                previousCrown = crown;
                steps++;
            }

            Expect(steps > 30, "dragged-node: too few drag steps to see a discontinuity (setup)", failures);
            Expect(smallestResponse > 0.0,
                Fmt("dragged-node: one 0.1 m upward drag moved the crown by {0:0.0000} m -- the crown went down, or "
                    + "stopped responding", smallestResponse), failures);
            Expect(biggestResponse < dragStep * 1.5,
                Fmt("dragged-node: one 0.1 m drag moved the crown by {0:0.0000} m, more than the drag itself -- the "
                    + "model snaps somewhere instead of responding smoothly", biggestResponse), failures);
            Expect(last > first,
                Fmt("dragged-node: lifting the node from 6 m to 10 m moved the crown from {0:0.000} m to {1:0.000} m, "
                    + "which is not upwards", first, last), failures);

            builder.Append("RCMVERTICAL ").Append("dragged-node".PadRight(26))
                   .Append(Fmt("{0} drags of 0.1 m across the clip boundary, crown {1:0.000} m to {2:0.000} m, "
                       + "response {3:0.0000} to {4:0.0000} m", steps, first, last, smallestResponse, biggestResponse))
                   .Append("\n");
        }

        /// <summary> Two vertical points close together cannot each get a full-length parabola, or the curves overlap and
        /// produce exactly the discontinuity the model exists to prevent. The clipper shortens them -- and says so,
        /// because a shortened curve is a road that does not meet the standard and hiding that would defeat the whole
        /// model. </summary>
        private static void CaseOverlappingCurvesAreClippedAndSaidSo(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            const double speed = 100.0;

            // 40 m tangents at 100 km/h: the 0.6V floor alone wants 60 m, so both curves must be clipped.
            List<VerticalPoint> tight = new List<VerticalPoint>
            {
                new VerticalPoint(0.0, 0.0),
                new VerticalPoint(40.0, 2.0),
                new VerticalPoint(80.0, 2.0),
                new VerticalPoint(120.0, 0.0),
            };

            Expect(VerticalAlignment.CurveWasClipped(tight, 1, speed, design),
                "clipping: a curve between 40 m tangents at 100 km/h was not reported as clipped, so the model is "
                + "claiming a standard it did not meet", failures);
            Expect(VerticalAlignment.CurveWasClipped(tight, 2, speed, design),
                "clipping: the second tight curve was not reported as clipped", failures);

            double used = VerticalAlignment.ClippedCurveLength(tight, 1,
                VerticalAlignment.GradePercent(tight[0], tight[1]),
                VerticalAlignment.GradePercent(tight[1], tight[2]), speed, design);
            Expect(used <= 40.0 + 1e-9,
                Fmt("clipping: the clipped length is {0:0.0} m, longer than the 40 m tangent it has to fit in -- "
                    + "adjacent parabolas will overlap", used), failures);

            // Sampling a tight profile must still be continuous, which is the point of clipping at all.
            List<VerticalPoint> samples = VerticalAlignment.Sample(tight, 0.5, speed, design);
            double worstJump = 0.0;
            double previous = double.NaN;
            for (int i = 1; i < samples.Count; i++)
            {
                double run = samples[i].StationMetres - samples[i - 1].StationMetres;
                if (run <= 1e-9) { continue; }
                double grade = ((samples[i].ElevationMetres - samples[i - 1].ElevationMetres) / run) * 100.0;
                if (!double.IsNaN(previous))
                {
                    double jump = Math.Abs(grade - previous);
                    if (jump > worstJump) { worstJump = jump; }
                }
                previous = grade;
            }
            Expect(worstJump < 1.0,
                Fmt("clipping: the clipped profile still jumps {0:0.00} percentage points between samples, so the "
                    + "curves are overlapping after all", worstJump), failures);

            // Given room, nothing is clipped.
            List<VerticalPoint> roomy = new List<VerticalPoint>
            {
                new VerticalPoint(0.0, 0.0),
                new VerticalPoint(600.0, 12.0),
                new VerticalPoint(1400.0, 12.0),
                new VerticalPoint(2000.0, 0.0),
            };
            Expect(!VerticalAlignment.CurveWasClipped(roomy, 1, speed, design),
                "clipping: a curve with 600 m of tangent either side was reported as clipped", failures);

            builder.Append("RCMVERTICAL ").Append("curve-clipping".PadRight(26))
                   .Append(Fmt("40 m tangents clip to {0:0.0} m and stay continuous ({1:0.00} pp worst step)",
                       used, worstJump)).Append("\n");
        }

        /// <summary> The model is identical on repeat (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            VerticalDesignClass design = VerticalDesignClass.Default();
            List<VerticalPoint> vpis = new List<VerticalPoint>
            {
                new VerticalPoint(0.0, 0.0),
                new VerticalPoint(250.0, 9.0),
                new VerticalPoint(600.0, 3.0),
            };
            List<VerticalPoint> a = VerticalAlignment.Sample(vpis, 2.0, 90.0, design);
            List<VerticalPoint> b = VerticalAlignment.Sample(vpis, 2.0, 90.0, design);

            Expect(a.Count == b.Count, "determinism: the sample count changed between calls", failures);
            int n = Math.Min(a.Count, b.Count);
            for (int i = 0; i < n; i++)
            {
                if (Math.Abs(a[i].ElevationMetres - b[i].ElevationMetres) < 1e-12) { continue; }
                failures.Add(Fmt("determinism: sample {0} differs between calls ({1:R} against {2:R})",
                    i, a[i].ElevationMetres, b[i].ElevationMetres));
                break;
            }

            Expect(Math.Abs(VerticalAlignment.DesignStoppingSightMetres(100.0, design)
                    - VerticalAlignment.DesignStoppingSightMetres(100.0, design)) < 1e-12,
                "determinism: the design sight distance changed between calls", failures);

            builder.Append("RCMVERTICAL ").Append("determinism".PadRight(26))
                   .Append(a.Count).Append(" samples identical on repeat\n");
        }

        // ---- helpers ----

        private static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
