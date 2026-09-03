#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
using RoadCityMaster.City;
using RoadCityMaster.CrossSection;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-96's gate: proves the carriageway has a real cross-section rather than a constant width and a flat
    /// top.
    ///
    /// Three claims, and one rule that matters more than the three.
    ///
    /// The CROWN is not asserted against a number typed here -- it is read from the surface asset, so the gate proves
    /// the section takes its cross-fall from the surface it is built with and that a sealed road and a gravel road
    /// therefore differ. A gate that hard-coded 0.02 would pass forever while the surface asset was ignored.
    ///
    /// The RUNOFF LENGTH is the number that decides whether banking is engineering or a node property. It is derived
    /// from the maximum relative gradient, so the gate asserts three responses the relation demands -- longer for a
    /// faster road, longer for more banking, longer for more lanes rotated -- plus the identity that makes the outer
    /// edge's whole transition a single straight line, L_t / L_r = e_NC / e_d. That identity is also why RM-40's
    /// existing rotation function can be called for the outer edge instead of a second copy being written here, and the
    /// gate checks the delegation agrees with the piecewise construction it replaces.
    ///
    /// The relative-gradient table itself is transcribed and cannot be derived, so it is checked for the SHAPE the
    /// published table has: strictly decreasing, in two straight segments. A mistyped digit breaks the segment and the
    /// gate names the speed. The lane adjustment factor, by contrast, IS derivable, and the gate asserts the derivation
    /// reproduces all six published values -- so that column is computed, not remembered.
    ///
    /// The rule is the last case. Every kerb, marking and barrier reads
    /// <see cref="CarriagewaySection.SurfaceHeightAtOffsetMetres"/> at its own offset, and the gate compares an edge
    /// element's height against the surface's own vertex at the same station through the entire transition. The failure
    /// this prevents is not dramatic: a kerb computing its own tilt from an older copy of the formula sits a fraction of
    /// a degree off the surface, and a hairline of background shows between them at grazing angles on a curve. It is
    /// invisible in a screenshot and unmistakable in motion, and it comes back every time someone fixes the section
    /// without fixing the copy.
    ///
    /// Report lines are prefixed RCMSECTION. </summary>
    public static class CarriagewaySectionGate
    {
        [MenuItem("Window/Road City Master/Testing/Carriageway section (crown, runoff, curve widening)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.CarriagewaySectionGate.RunBatch. Exits 1 on failure. </summary>
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
            builder.Append("RCMSECTION BEGIN\n");
            List<string> failures = new List<string>();

            CaseTheStraightTakesItsCrossFallFromTheSurfaceAndDrains(builder, failures);
            CaseTheRelativeGradientTableHasThePublishedShape(builder, failures);
            CaseTheDerivedLaneFactorReproducesThePublishedColumn(builder, failures);
            CaseTheRunoffLengthRespondsAsTheRelationRequires(builder, failures);
            CaseTheRunoutFollowsTheRunoffRatio(builder, failures);
            CaseTheOuterEdgeTransitionIsOneStraightLineAndIsRmFortys(builder, failures);
            CaseNoStationShowsACrossFallDiscontinuity(builder, failures);
            CaseFullBankingIsReachedExactlyAtTheCurve(builder, failures);
            CaseCurveWideningAppearsBelowItsThresholdAndVanishesAbove(builder, failures);
            CaseAnEdgeElementInheritsTheRotatedSection(builder, failures);
            CaseTheShoulderFallsAtItsOwnCrossSlope(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSECTION FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSECTION result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSECTION END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        // ---- the crown ----

        /// <summary> On a straight the surface carries the cross-fall of the SURFACE ASSET, not a number written here,
        /// and it sheds water to both edges. Both surface types are exercised, and they are required to differ: if the
        /// section ever stopped reading the asset, a sealed and an unsealed road would come out identical and this case
        /// is what notices. </summary>
        private static void CaseTheStraightTakesItsCrossFallFromTheSurfaceAndDrains(StringBuilder builder,
            List<string> failures)
        {
            SurfaceTypeClass sealedSurface = SurfaceTypeClass.Sealed();
            SurfaceTypeClass unsealed = SurfaceTypeClass.Unsealed();

            double sealedFall = CarriagewaySection.NormalCrossSlope(sealedSurface);
            double unsealedFall = CarriagewaySection.NormalCrossSlope(unsealed);

            Expect(Math.Abs(sealedFall - sealedSurface.crownCrossSlope) < 1e-12,
                Fmt("crown: the sealed cross-fall {0:R} is not the surface asset's own {1:R}",
                    sealedFall, sealedSurface.crownCrossSlope), failures);
            Expect(Math.Abs(unsealedFall - unsealed.crownCrossSlope) < 1e-12,
                Fmt("crown: the unsealed cross-fall {0:R} is not the surface asset's own {1:R}",
                    unsealedFall, unsealed.crownCrossSlope), failures);
            Expect(unsealedFall > sealedFall,
                Fmt("crown: unsealed ({0:R}) does not drain harder than sealed ({1:R}), so the surface asset is being "
                    + "ignored", unsealedFall, sealedFall), failures);

            const double halfWidth = 7.0;
            SectionSlopes straight = new SectionSlopes(-sealedFall, -sealedFall);
            DrainageVerdict verdict = CarriagewaySection.Drainage(straight);
            Expect(verdict.DrainsToBothEdges,
                "crown: a straight sealed section does not drain to both edges", failures);

            double outerEdge = CarriagewaySection.SurfaceHeightAtOffsetMetres(halfWidth, straight);
            double innerEdge = CarriagewaySection.SurfaceHeightAtOffsetMetres(-halfWidth, straight);
            double crownPoint = CarriagewaySection.SurfaceHeightAtOffsetMetres(0.0, straight);
            Expect(Math.Abs(crownPoint) < 1e-12, "crown: the axis of rotation is not at zero height", failures);
            Expect(outerEdge < 0.0 && innerEdge < 0.0,
                Fmt("crown: an edge is not below the crown ({0:R} / {1:R})", outerEdge, innerEdge), failures);
            Expect(Math.Abs(outerEdge - innerEdge) < 1e-12,
                Fmt("crown: the straight section is not symmetric ({0:R} against {1:R})", outerEdge, innerEdge),
                failures);
            Expect(Math.Abs(outerEdge + (halfWidth * sealedFall)) < 1e-12,
                Fmt("crown: the edge drop {0:R} m is not half-width times cross-fall", outerEdge), failures);

            builder.Append("RCMSECTION ").Append("crown from surface".PadRight(26))
                   .Append(Fmt("sealed {0:R}  unsealed {1:R}  edge drop {2:F4} m over {3:F1} m",
                       sealedFall, unsealedFall, -outerEdge, halfWidth)).Append("\n");
        }

        // ---- the two design tables: one checked for shape, one derived ----

        /// <summary> The maximum relative gradient cannot be derived -- it is a judgement about what a driver tolerates
        /// -- so it is checked for the shape the published table has instead: strictly decreasing with speed, and made of
        /// two straight segments, 0.05 percent per 10 km/h up to 80 km/h and 0.03 percent per 10 km/h from 80 to 130.
        /// A single mistyped digit breaks one of those steps and this case names the speed it broke at. That is the most
        /// a transcribed table can be held to, and it is considerably more than nothing. </summary>
        private static void CaseTheRelativeGradientTableHasThePublishedShape(StringBuilder builder,
            List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double[] speeds = cls.tabulatedSpeedsKmh;
            double[] gradients = cls.maximumRelativeGradientPercent;

            Expect(speeds.Length == gradients.Length,
                "relative gradient: the speed and gradient arrays are different lengths (setup)", failures);
            int n = Math.Min(speeds.Length, gradients.Length);

            for (int i = 1; i < n; i++)
            {
                if (gradients[i] < gradients[i - 1]) { continue; }
                failures.Add(Fmt("relative gradient: {0:F0} km/h ({1:R}%) is not below {2:F0} km/h ({3:R}%)",
                    speeds[i], gradients[i], speeds[i - 1], gradients[i - 1]));
            }

            for (int i = 1; i < n; i++)
            {
                double speedStep = speeds[i] - speeds[i - 1];
                if (speedStep <= 0.0) { continue; }
                double perTenKmh = (gradients[i - 1] - gradients[i]) * 10.0 / speedStep;
                double expected = speeds[i] <= 80.0 ? 0.05 : 0.03;
                if (Math.Abs(perTenKmh - expected) < 0.0051) { continue; }
                failures.Add(Fmt("relative gradient: the step to {0:F0} km/h falls {1:F3}% per 10 km/h, and the "
                    + "published segment falls {2:F2}%", speeds[i], perTenKmh, expected));
            }

            double at95 = CarriagewaySection.MaximumRelativeGradientPercent(95.0, cls);
            Expect(at95 < 0.47 && at95 > 0.44,
                Fmt("relative gradient: 95 km/h returns {0:R}%, which is not between the 90 and 100 km/h rows", at95),
                failures);
            Expect(Math.Abs(CarriagewaySection.MaximumRelativeGradientPercent(10.0, cls) - gradients[0]) < 1e-12,
                "relative gradient: below the table the first row is not held flat", failures);
            Expect(Math.Abs(CarriagewaySection.MaximumRelativeGradientPercent(200.0, cls) - gradients[n - 1]) < 1e-12,
                "relative gradient: above the table the last row is not held flat", failures);

            builder.Append("RCMSECTION ").Append("relative gradient".PadRight(26))
                   .Append(Fmt("{0} rows, {1:R}% at {2:F0} to {3:R}% at {4:F0} km/h, 95 km/h interpolates {5:F3}%",
                       n, gradients[0], speeds[0], gradients[n - 1], speeds[n - 1], at95)).Append("\n");
        }

        /// <summary> The runoff adjustment factor is DERIVED from b_w = (1 + 0.5(n - 1)) / n, and the derivation is
        /// required to reproduce all six published values to within their own rounding. This is RM-95's treatment
        /// applied to a second table: once the relation is shown to reproduce the printed column, the column stops being
        /// data that can rot and becomes a check on the relation. </summary>
        private static void CaseTheDerivedLaneFactorReproducesThePublishedColumn(StringBuilder builder,
            List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double[] lanes = cls.publishedLanesRotated;
            double[] published = cls.publishedLaneRotationFactors;

            Expect(lanes.Length == published.Length,
                "lane factor: the lane and factor arrays are different lengths (setup)", failures);
            int n = Math.Min(lanes.Length, published.Length);
            double worst = 0.0;
            double worstAt = 0.0;

            for (int i = 0; i < n; i++)
            {
                double derived = CarriagewaySection.LaneRotationFactor(lanes[i]);
                double error = Math.Abs(derived - published[i]);
                if (error > worst) { worst = error; worstAt = lanes[i]; }
                if (error < 0.005) { continue; }
                failures.Add(Fmt("lane factor: {0:F1} lanes derives {1:F4} and the published value is {2:R}",
                    lanes[i], derived, published[i]));
            }

            Expect(Math.Abs(CarriagewaySection.LaneRotationFactor(1.0) - 1.0) < 1e-12,
                "lane factor: one rotated lane is not 1.0, so a single-lane rotation is being adjusted", failures);
            for (int i = 1; i < n; i++)
            {
                Expect(CarriagewaySection.LaneRotationFactor(lanes[i])
                       < CarriagewaySection.LaneRotationFactor(lanes[i - 1]),
                    Fmt("lane factor: {0:F1} lanes is not below {1:F1} lanes", lanes[i], lanes[i - 1]), failures);
            }

            builder.Append("RCMSECTION ").Append("lane factor derived".PadRight(26))
                   .Append(Fmt("{0} published values reproduced, worst error {1:F4} at {2:F1} lanes",
                       n, worst, worstAt)).Append("\n");
        }

        // ---- the lengths ----

        /// <summary> The runoff length is a length, not a preference: L_r = (w n1 e_d / delta) b_w. The three responses
        /// the relation demands are asserted -- a faster road gets a longer runoff because delta shrinks, more banking
        /// gets a longer one because the edge has further to climb, and more rotated lanes get a longer one because the
        /// edge is further from the axis. Any one of those coming out backwards means the length is being read from the
        /// wrong end of a table, and a road that develops its banking backwards is one an author feels immediately and
        /// cannot describe. </summary>
        private static void CaseTheRunoffLengthRespondsAsTheRelationRequires(StringBuilder builder,
            List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();

            double baseline = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, 0.06, 100.0, cls);
            double faster = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, 0.06, 130.0, cls);
            double slower = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, 0.06, 50.0, cls);
            double moreBanking = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, 0.08, 100.0, cls);
            double moreLanes = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 3.0, 0.06, 100.0, cls);
            double narrower = CarriagewaySection.SuperelevationRunoffLengthMetres(3.0, 2.0, 0.06, 100.0, cls);

            Expect(faster > baseline,
                Fmt("runoff: 130 km/h ({0:F1} m) is not longer than 100 km/h ({1:F1} m)", faster, baseline), failures);
            Expect(slower < baseline,
                Fmt("runoff: 50 km/h ({0:F1} m) is not shorter than 100 km/h ({1:F1} m)", slower, baseline), failures);
            Expect(moreBanking > baseline,
                Fmt("runoff: 8% banking ({0:F1} m) is not longer than 6% ({1:F1} m)", moreBanking, baseline), failures);
            Expect(moreLanes > baseline,
                Fmt("runoff: three rotated lanes ({0:F1} m) is not longer than two ({1:F1} m)", moreLanes, baseline),
                failures);
            Expect(narrower < baseline,
                Fmt("runoff: 3.0 m lanes ({0:F1} m) is not shorter than 3.6 m lanes ({1:F1} m)", narrower, baseline),
                failures);

            // The worked case, spelled out so the report carries a number a reader can check against the standard's
            // own runoff table: 3.6 m lanes, two rotated, 6% banking, 100 km/h, where delta is 0.44%.
            //   L_r = (3.6 x 2 x 0.06 / 0.0044) x 0.75 = 98.18 x 0.75 = 73.6 m.
            Expect(Math.Abs(baseline - 73.6) < 1.0,
                Fmt("runoff: the worked 100 km/h case gives {0:F2} m and the relation gives 73.6 m", baseline),
                failures);
            Expect(CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, 0.0, 100.0, cls) == 0.0,
                "runoff: an unbanked road is given a runoff length", failures);

            builder.Append("RCMSECTION ").Append("runoff length".PadRight(26))
                   .Append(Fmt("100 km/h {0:F1} m, 130 km/h {1:F1} m, 50 km/h {2:F1} m, 3 lanes {3:F1} m",
                       baseline, faster, slower, moreLanes)).Append("\n");
        }

        /// <summary> The tangent runout is not a second authored length: L_t / L_r = e_NC / e_d, because both develop at
        /// the same relative gradient and the runout is only the part of that same edge-profile line lying between normal
        /// crown and zero. The schedule's stations are checked against the two lengths at the same time, since a
        /// correctly computed length placed at the wrong station produces banking that arrives inside the curve. </summary>
        private static void CaseTheRunoutFollowsTheRunoffRatio(StringBuilder builder, List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;

            double runoff = CarriagewaySection.SuperelevationRunoffLengthMetres(3.6, 2.0, banking, 100.0, cls);
            double runout = CarriagewaySection.TangentRunoutLengthMetres(crown, banking, runoff);

            double wantedRatio = crown / banking;
            double actualRatio = runoff > 0.0 ? runout / runoff : 0.0;
            Expect(Math.Abs(actualRatio - wantedRatio) < 1e-9,
                Fmt("runout: the runout/runoff ratio is {0:R} and the crown/banking ratio is {1:R}",
                    actualRatio, wantedRatio), failures);
            Expect(runout < runoff,
                Fmt("runout: {0:F1} m is not shorter than the runoff {1:F1} m", runout, runoff), failures);

            TransitionSchedule schedule = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0,
                crown, cls);
            Expect(Math.Abs(schedule.RunoffLengthMetres - runoff) < 1e-12
                   && Math.Abs(schedule.RunoutLengthMetres - runout) < 1e-12,
                "runout: the schedule's lengths are not the ones the relations give", failures);
            Expect(Math.Abs(schedule.EntryRunoffStartStation - (500.0 - runoff)) < 1e-9,
                Fmt("runout: the runoff starts at {0:F2} and should start a runoff before the curve",
                    schedule.EntryRunoffStartStation), failures);
            Expect(Math.Abs(schedule.EntryRunoutStartStation - (500.0 - runoff - runout)) < 1e-9,
                "runout: the runout does not start a runout before the runoff", failures);
            Expect(Math.Abs(schedule.ExitRunoutEndStation - (800.0 + runoff + runout)) < 1e-9,
                "runout: the exit transition is not the mirror of the entry", failures);
            Expect(schedule.EntryRunoutStartStation < schedule.CurveStartStation
                   && schedule.CurveEndStation < schedule.ExitRunoutEndStation,
                "runout: the transition does not lie outside the curve", failures);

            builder.Append("RCMSECTION ").Append("runout ratio".PadRight(26))
                   .Append(Fmt("runout {0:F2} m / runoff {1:F2} m = {2:F4} = crown {3:F4} / banking {4:R}",
                       runout, runoff, actualRatio, crown, banking)).Append("\n");
        }

        // ---- the transition ----

        /// <summary> Two claims in one case, because they are the same claim. The outer edge's cross-slope across the
        /// whole runout-plus-runoff is a SINGLE straight line -- checked by second differences over 400 stations -- and
        /// that is exactly why RM-40's existing rotation function can be called for it instead of a second copy being
        /// written here. The independent piecewise construction (crown to zero over the runout, then zero to full banking
        /// over the runoff) is computed in this gate and required to agree with what the model returns. If the two lengths
        /// ever stopped obeying L_t / L_r = e_NC / e_d, the delegation would silently start returning the wrong slope and
        /// this is the case that catches it. </summary>
        private static void CaseTheOuterEdgeTransitionIsOneStraightLineAndIsRmFortys(StringBuilder builder,
            List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;
            TransitionSchedule schedule = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0,
                crown, cls);

            const int samples = 400;
            double span = schedule.CurveStartStation - schedule.EntryRunoutStartStation;
            double step = span / samples;
            double[] outer = new double[samples + 1];
            double worstAgainstPiecewise = 0.0;

            for (int i = 0; i <= samples; i++)
            {
                double station = schedule.EntryRunoutStartStation + (i * step);
                outer[i] = CarriagewaySection.SlopesAtStation(station, schedule, banking, crown).OuterRate;

                // The piecewise construction the delegation stands in for, built here from first principles.
                double travelled = station - schedule.EntryRunoutStartStation;
                double piecewise;
                if (travelled <= schedule.RunoutLengthMetres)
                {
                    double t = schedule.RunoutLengthMetres > 0.0 ? travelled / schedule.RunoutLengthMetres : 1.0;
                    piecewise = -crown + (t * crown);
                }
                else
                {
                    double u = (travelled - schedule.RunoutLengthMetres) / schedule.RunoffLengthMetres;
                    if (u > 1.0) { u = 1.0; }
                    piecewise = u * banking;
                }
                double error = Math.Abs(piecewise - outer[i]);
                if (error > worstAgainstPiecewise) { worstAgainstPiecewise = error; }
            }

            Expect(worstAgainstPiecewise < 1e-9,
                Fmt("outer edge: RM-40's rotation and the piecewise construction disagree by {0:R}",
                    worstAgainstPiecewise), failures);

            double worstCurvature = 0.0;
            for (int i = 1; i < samples; i++)
            {
                double second = Math.Abs(outer[i + 1] - (2.0 * outer[i]) + outer[i - 1]);
                if (second > worstCurvature) { worstCurvature = second; }
            }
            Expect(worstCurvature < 1e-9,
                Fmt("outer edge: the transition is not one straight line, worst second difference {0:R}",
                    worstCurvature), failures);

            Expect(Math.Abs(outer[0] + crown) < 1e-9,
                Fmt("outer edge: the transition starts at {0:R} instead of the normal crown", outer[0]), failures);
            Expect(Math.Abs(outer[samples] - banking) < 1e-9,
                Fmt("outer edge: the transition ends at {0:R} instead of full banking", outer[samples]), failures);

            builder.Append("RCMSECTION ").Append("outer edge one line".PadRight(26))
                   .Append(Fmt("{0:R} to {1:R} over {2:F1} m, curvature {3:E1}, RM-40 agrees to {4:E1}",
                       outer[0], outer[samples], span, worstCurvature, worstAgainstPiecewise)).Append("\n");
        }

        /// <summary> No station anywhere in the transition shows a cross-fall discontinuity, on either edge, entering or
        /// leaving. This is the case that a naive implementation fails: it is easy to develop the outer edge properly and
        /// leave the inner edge stepping from normal crown straight to full banking at the curve, which is a visible
        /// crease down the inside lane and an audible one in a vehicle. The sweep runs 20 m of straight either side, so
        /// the joins into and out of the plain crown are inside the sample. </summary>
        private static void CaseNoStationShowsACrossFallDiscontinuity(StringBuilder builder, List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;
            TransitionSchedule schedule = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0,
                crown, cls);

            const double step = 0.25;
            double from = schedule.EntryRunoutStartStation - 20.0;
            double to = schedule.ExitRunoutEndStation + 20.0;
            int steps = (int)Math.Floor(((to - from) / step) + 1e-9);

            double worstOuterJump = 0.0;
            double worstInnerJump = 0.0;
            double worstOuterAt = 0.0;
            double worstInnerAt = 0.0;
            SectionSlopes previous = CarriagewaySection.SlopesAtStation(from, schedule, banking, crown);

            for (int i = 1; i <= steps; i++)
            {
                double station = from + (i * step);
                SectionSlopes here = CarriagewaySection.SlopesAtStation(station, schedule, banking, crown);
                double outerJump = Math.Abs(here.OuterRate - previous.OuterRate);
                double innerJump = Math.Abs(here.InnerRate - previous.InnerRate);
                if (outerJump > worstOuterJump) { worstOuterJump = outerJump; worstOuterAt = station; }
                if (innerJump > worstInnerJump) { worstInnerJump = innerJump; worstInnerAt = station; }
                previous = here;
            }

            // The most either rate may change over a quarter of a metre is the relative gradient itself, which at
            // 0.44% is 0.0044 per metre, so 0.0011 over the step. A factor of two leaves room for the inner edge, which
            // develops over the runoff alone and therefore a little faster than the outer edge's average.
            double permitted = (cls.maximumRelativeGradientPercent[0] / 100.0) * step * 2.0;
            Expect(worstOuterJump < permitted,
                Fmt("continuity: the outer cross-fall jumps {0:R} at station {1:F2}, above the permitted {2:R}",
                    worstOuterJump, worstOuterAt, permitted), failures);
            Expect(worstInnerJump < permitted,
                Fmt("continuity: the inner cross-fall jumps {0:R} at station {1:F2}, above the permitted {2:R}",
                    worstInnerJump, worstInnerAt, permitted), failures);

            builder.Append("RCMSECTION ").Append("no discontinuity".PadRight(26))
                   .Append(Fmt("{0} stations, worst outer step {1:E1}, worst inner step {2:E1}, permitted {3:E1}",
                       steps + 1, worstOuterJump, worstInnerJump, permitted)).Append("\n");
        }

        /// <summary> Full banking is reached at the curve and not inside it, and the section there is a PLANE -- the inner
        /// rate is the negative of the outer. Before the curve it is not yet a plane, which is correct: the crown line
        /// survives until the last station of the runoff, and asserting otherwise would be asserting that the road is
        /// banked before it needs to be. </summary>
        private static void CaseFullBankingIsReachedExactlyAtTheCurve(StringBuilder builder, List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;
            TransitionSchedule schedule = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0,
                crown, cls);

            SectionSlopes atCurve = CarriagewaySection.SlopesAtStation(schedule.CurveStartStation, schedule,
                banking, crown);
            Expect(Math.Abs(atCurve.OuterRate - banking) < 1e-9 && Math.Abs(atCurve.InnerRate + banking) < 1e-9,
                Fmt("banking: the section at the curve is ({0:R}, {1:R}) instead of a plane at {2:R}",
                    atCurve.OuterRate, atCurve.InnerRate, banking), failures);

            SectionSlopes justBefore = CarriagewaySection.SlopesAtStation(schedule.CurveStartStation - 1.0, schedule,
                banking, crown);
            Expect(justBefore.OuterRate < banking - 1e-6,
                Fmt("banking: a metre before the curve the outer rate is already {0:R}", justBefore.OuterRate),
                failures);
            Expect(justBefore.InnerRate > -banking + 1e-6,
                Fmt("banking: a metre before the curve the inner rate is already {0:R}", justBefore.InnerRate),
                failures);

            SectionSlopes onStraight = CarriagewaySection.SlopesAtStation(schedule.EntryRunoutStartStation - 50.0,
                schedule, banking, crown);
            Expect(Math.Abs(onStraight.OuterRate + crown) < 1e-12
                   && Math.Abs(onStraight.InnerRate + crown) < 1e-12,
                "banking: the straight before the transition is not a plain crown", failures);
            Expect(CarriagewaySection.Drainage(onStraight).DrainsToBothEdges
                   && !CarriagewaySection.Drainage(atCurve).DrainsToBothEdges,
                "banking: drainage is not reported as both-edges on the straight and single-edge in the curve",
                failures);

            builder.Append("RCMSECTION ").Append("full banking at curve".PadRight(26))
                   .Append(Fmt("plane at station {0:F2}, still developing at {1:F2} ({2:R} outer)",
                       schedule.CurveStartStation, schedule.CurveStartStation - 1.0, justBefore.OuterRate))
                   .Append("\n");
        }

        // ---- curve widening ----

        /// <summary> A tight bend is wider than the straight either side of it, and a gentle one is not. The widening is
        /// quoted in metres, it falls monotonically as the radius opens, and above the threshold radius it is exactly
        /// zero rather than a few stray centimetres -- the published rule disregards anything under 0.6 m, and building
        /// it anyway would show up as a wobble in the edge line rather than as a wider road.
        ///
        /// The two sample radii are placed relative to the threshold rather than fixed, and the vehicle is asserted to be
        /// the one RM-97's catalogue publishes: the four figures here were once transcribed independently under the name
        /// "Single-unit truck" with a 2.6 m track width, and the catalogue's SU-9 is 2.4 m. Two copies of a vehicle table
        /// agreed until one of them was looked up, and 0.2 m of track is 0.4 m of widening on a two-lane road.
        ///
        /// The last assertion is the one that stops a NaN reaching a mesh: at a radius below the wheelbase the swept-path
        /// relation has no solution because the vehicle physically cannot make the turn, and the model must return a
        /// finite, large widening there instead of a square root of a negative number. </summary>
        private static void CaseCurveWideningAppearsBelowItsThresholdAndVanishesAbove(StringBuilder builder,
            List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            SweptPathVehicle vehicle = cls.Vehicle();
            const int lanes = 2;
            const double laneWidth = 3.6;
            const double speed = 60.0;

            // The vehicle is RM-97's, not this class's. Asserted rather than assumed, because these four figures were
            // once transcribed here independently and disagreed with the catalogue by 0.2 m of track width.
            DesignVehicle catalogued = DesignVehicles.For(cls.designVehicleKind);
            Expect(vehicle.Name == catalogued.Name
                   && vehicle.TrackWidthMetres == catalogued.WidthMetres
                   && vehicle.WheelbaseMetres == catalogued.EffectiveWheelbaseMetres
                   && vehicle.FrontOverhangMetres == catalogued.FrontOverhangMetres,
                Fmt("widening: the section's vehicle is {0} ({1:R} m track, {2:R} m wheelbase, {3:R} m overhang) but the "
                    + "catalogue's {4} is {5:R} / {6:R} / {7:R}", vehicle.Name, vehicle.TrackWidthMetres,
                    vehicle.WheelbaseMetres, vehicle.FrontOverhangMetres, catalogued.Name, catalogued.WidthMetres,
                    catalogued.EffectiveWheelbaseMetres, catalogued.FrontOverhangMetres), failures);

            // The threshold is found first and the two sample radii are placed either side of it. Fixed radii would be a
            // statement about one particular vehicle; naming a different design vehicle in the class moves the threshold
            // by tens of metres, and this case has to keep testing the RULE rather than a vehicle it was tuned against.
            double threshold = CarriagewaySection.WideningThresholdRadiusMetres(lanes, laneWidth, speed, vehicle, cls);
            Expect(threshold > 20.0 && threshold < 2000.0,
                Fmt("widening: the threshold radius {0:F2} m is outside anything a road can express", threshold),
                failures);

            double tight = CarriagewaySection.CurveWideningMetres(lanes, laneWidth, threshold - 20.0, speed, vehicle,
                cls);
            double gentle = CarriagewaySection.CurveWideningMetres(lanes, laneWidth, threshold + 20.0, speed, vehicle,
                cls);
            Expect(tight >= cls.minimumWideningMetres,
                Fmt("widening: {0:F1} m inside the threshold a {1:F1} m radius gets {2:F3} m, below the {3:R} m the rule "
                    + "keeps", 20.0, threshold - 20.0, tight, cls.minimumWideningMetres), failures);
            Expect(gentle == 0.0,
                Fmt("widening: {0:F1} m radius is outside the threshold and still gets {1:F3} m", threshold + 20.0,
                    gentle), failures);

            double previous = double.MaxValue;
            for (double radius = 60.0; radius <= 1000.0 + 1e-9; radius += 20.0)
            {
                double required = CarriagewaySection.TravelledWayWidthOnCurveMetres(lanes, laneWidth, radius, speed,
                    vehicle, cls);
                if (required < previous) { previous = required; continue; }
                failures.Add(Fmt("widening: the required width at {0:F0} m radius ({1:F4} m) is not below the "
                    + "previous radius's", radius, required));
                previous = required;
            }

            Expect(CarriagewaySection.CurveWideningMetres(lanes, laneWidth, threshold - 2.0, speed, vehicle, cls) > 0.0,
                "widening: two metres inside the threshold radius there is no widening", failures);
            Expect(CarriagewaySection.CurveWideningMetres(lanes, laneWidth, threshold + 2.0, speed, vehicle, cls)
                   == 0.0,
                "widening: two metres outside the threshold radius there is still widening", failures);

            double impossible = CarriagewaySection.CurveWideningMetres(lanes, laneWidth,
                vehicle.WheelbaseMetres * 0.5, speed, vehicle, cls);
            Expect(!double.IsNaN(impossible) && !double.IsInfinity(impossible) && impossible > 0.0,
                Fmt("widening: a radius inside the wheelbase returns {0:R} instead of a finite large widening",
                    impossible), failures);

            builder.Append("RCMSECTION ").Append("curve widening".PadRight(26))
                   .Append(Fmt("threshold R={0:F1} m for the {1}: {2} m of widening 20 m inside it, none 20 m outside",
                       threshold, vehicle.Name, tight.ToString("F3", CultureInfo.InvariantCulture))).Append("\n");
        }

        // ---- the rule: one owner of the section ----

        /// <summary> Every attached element inherits the rotated section instead of computing its own. Four elements at
        /// four lateral offsets -- an inside kerb, an inside lane line, an outside lane line and an outside barrier --
        /// are compared against the SURFACE's own vertex heights at the same station, through the entire transition, by
        /// interpolating the surface's point list at the element's offset. Agreement is required to 0.01 mm, which is far
        /// below anything a mesh can express and far above floating-point noise.
        ///
        /// The failure this prevents is quiet. A kerb that computes its own tilt from an older copy of the formula sits a
        /// fraction of a degree off the surface; on a banked curve that is a hairline of background between the two, and
        /// it is invisible in a still and unmistakable in motion. One owner is what makes it impossible rather than
        /// fixed. </summary>
        private static void CaseAnEdgeElementInheritsTheRotatedSection(StringBuilder builder, List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;
            const double halfWidth = 7.2;
            TransitionSchedule schedule = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0,
                crown, cls);

            double[] offsets = { -halfWidth, -3.6, 3.6, halfWidth };
            string[] names = { "inside barrier", "inside lane line", "outside lane line", "outside barrier" };
            double worst = 0.0;
            string worstWhat = "none";
            double worstAt = 0.0;
            int compared = 0;

            for (double station = schedule.EntryRunoutStartStation - 10.0;
                 station <= schedule.ExitRunoutEndStation + 10.0; station += 1.0)
            {
                SectionSlopes slopes = CarriagewaySection.SlopesAtStation(station, schedule, banking, crown);
                SectionPoint[] surface = CarriagewaySection.SurfaceSection(halfWidth, slopes, 48);

                for (int k = 0; k < offsets.Length; k++)
                {
                    double element = CarriagewaySection.AttachedElementHeightMetres(offsets[k], slopes);
                    double fromSurface = HeightFromSectionPoints(surface, offsets[k]);
                    double error = Math.Abs(element - fromSurface);
                    compared++;
                    if (error <= worst) { continue; }
                    worst = error; worstWhat = names[k]; worstAt = station;
                }
            }

            Expect(worst < 1e-5,
                Fmt("inheritance: the {0} sits {1:R} m off the surface at station {2:F1}", worstWhat, worst, worstAt),
                failures);

            builder.Append("RCMSECTION ").Append("elements inherit".PadRight(26))
                   .Append(Fmt("{0} comparisons across the transition, worst disagreement {1:E1} m", compared, worst))
                   .Append("\n");
        }

        /// <summary> The surface's own height at an arbitrary lateral offset, read out of its point list by linear
        /// interpolation. Deliberately NOT a call to the section function: the whole point of the case above is to compare
        /// the element against the built surface rather than against the same formula twice. </summary>
        private static double HeightFromSectionPoints(SectionPoint[] points, double lateral)
        {
            if (points == null || points.Length == 0) { return 0.0; }
            if (lateral <= points[0].lateral) { return points[0].height; }
            int last = points.Length - 1;
            if (lateral >= points[last].lateral) { return points[last].height; }
            for (int i = 1; i <= last; i++)
            {
                if (lateral > points[i].lateral) { continue; }
                double span = points[i].lateral - points[i - 1].lateral;
                if (span <= 0.0) { return points[i].height; }
                double t = (lateral - points[i - 1].lateral) / span;
                return points[i - 1].height + (t * (points[i].height - points[i - 1].height));
            }
            return points[last].height;
        }

        /// <summary> RM-94: a shoulder's outer edge drops by its OWN width taken down its OWN cross-slope, which is
        /// why one hand-found constant could not serve it. The ramp vertices used to be lowered by a fixed 0.10 m --
        /// right for the 2.5 m shoulder it was fitted against and wrong for every other width, by more than the whole
        /// drop at 1 m. What is asserted here is that the replacement moves with the width, that it agrees with the
        /// surface rather than re-deriving it, and that a mistyped slope cannot flatten the shoulder into a pond or
        /// tip it past what a vehicle pulling onto it will tolerate. </summary>
        private static void CaseTheShoulderFallsAtItsOwnCrossSlope(StringBuilder builder, List<string> failures)
        {
            // The figure the old constant was fitted to, and the width it was wrong for.
            double wide = CarriagewaySection.ShoulderFallMetres(2.5);
            double narrow = CarriagewaySection.ShoulderFallMetres(1.0);
            Expect(Math.Abs(wide - 0.10) < 1e-12,
                Fmt("shoulder: a 2.5 m shoulder at the normal 4% did not fall 0.100 m ({0:R})", wide), failures);
            Expect(Math.Abs(narrow - 0.04) < 1e-12,
                Fmt("shoulder: a 1 m shoulder at the normal 4% did not fall 0.040 m ({0:R})", narrow), failures);
            Expect(narrow < wide,
                "shoulder: the drop did not move with the width -- it is still behaving like a constant", failures);

            // A slope over a width is linear in the width: twice the shoulder, twice the drop.
            double twice = CarriagewaySection.ShoulderFallMetres(5.0);
            Expect(Math.Abs(twice - (2.0 * wide)) < 1e-12,
                Fmt("shoulder: doubling the width did not double the drop ({0:R} against {1:R})", twice, 2.0 * wide),
                failures);

            // It cannot disagree with the surface: the same width and slope put through THE ONE PLACE a height is
            // computed must give the same number, because that is what the drop is routed through.
            double[] widths = new double[] { 1.0, 1.8, 2.5, 3.0 };
            double[] slopes = new double[] { 0.02, 0.03, 0.04, 0.06 };
            for (int i = 0; i < widths.Length; i++)
            {
                for (int j = 0; j < slopes.Length; j++)
                {
                    double fall = CarriagewaySection.ShoulderFallMetres(widths[i], slopes[j]);
                    double surface = CarriagewaySection.SurfaceHeightAtOffsetMetres(
                        widths[i], new SectionSlopes(slopes[j], slopes[j]));
                    Expect(Math.Abs(fall - surface) < 1e-12,
                        Fmt("shoulder: the drop at {0:F1} m / {1:P0} disagreed with the surface ({2:R} against {3:R})",
                            widths[i], slopes[j], fall, surface), failures);
                    Expect(fall > 0.0,
                        Fmt("shoulder: the drop at {0:F1} m / {1:P0} was not a positive magnitude the caller can "
                            + "subtract", widths[i], slopes[j]), failures);
                }
            }

            // A mistyped slope is clamped into the AASHTO band rather than built.
            double tooFlat = CarriagewaySection.ShoulderFallMetres(2.5, 0.001);
            double tooSteep = CarriagewaySection.ShoulderFallMetres(2.5, 0.5);
            Expect(Math.Abs(tooFlat - (2.5 * CarriagewaySection.ShoulderCrossSlopeMinimum)) < 1e-12,
                Fmt("shoulder: a slope below the band was not lifted to the 2% minimum ({0:R})", tooFlat), failures);
            Expect(Math.Abs(tooSteep - (2.5 * CarriagewaySection.ShoulderCrossSlopeMaximum)) < 1e-12,
                Fmt("shoulder: a slope above the band was not held at the 6% maximum ({0:R})", tooSteep), failures);
            // Nothing asked for, and nothing sensible asked for, both fall back to the normal figure.
            Expect(Math.Abs(CarriagewaySection.ShoulderFallMetres(2.5, 0.0) - wide) < 1e-12
                   && Math.Abs(CarriagewaySection.ShoulderFallMetres(2.5, -0.04) - wide) < 1e-12,
                "shoulder: a zero or negative cross-slope did not fall back to the normal 4%", failures);

            // A road with no shoulder has nothing to fall across, and must not hand back an infinity or a rise.
            Expect(CarriagewaySection.ShoulderFallMetres(0.0) == 0.0
                   && CarriagewaySection.ShoulderFallMetres(-2.5) == 0.0,
                "shoulder: a road with no shoulder was given a drop", failures);

            // The band itself, and the note saying where it comes from.
            Expect(CarriagewaySection.ShoulderCrossSlopeMinimum < CarriagewaySection.ShoulderCrossSlopeNormal
                   && CarriagewaySection.ShoulderCrossSlopeNormal < CarriagewaySection.ShoulderCrossSlopeMaximum,
                "shoulder: the normal cross-slope fell outside its own permitted band", failures);
            Expect(!string.IsNullOrEmpty(CarriagewaySection.ShoulderCrossSlopeSource),
                "shoulder: the cross-slope band lost the note saying where it is sourced from", failures);

            builder.Append("RCMSECTION ").Append("shoulder fall".PadRight(26))
                   .Append(Fmt("drop follows width x slope ({0:F3} m at 2.5 m, {1:F3} m at 1 m), clamped 2%-6%",
                       wide, narrow))
                   .Append("\n");
        }

        // ---- C-06 ----

        /// <summary> The same inputs give the same section twice: lengths, slopes, the point list and the widening. The
        /// model holds no state and the bisection in the threshold search is the only iterative part, so this case is
        /// mostly a guard against a future edit introducing a cached field. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            CarriagewaySectionClass cls = CarriagewaySectionClass.Default();
            double crown = CarriagewaySection.NormalCrossSlope(SurfaceTypeClass.Sealed());
            const double banking = 0.06;
            SweptPathVehicle vehicle = cls.Vehicle();

            TransitionSchedule a = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0, crown, cls);
            TransitionSchedule b = CarriagewaySection.Schedule(500.0, 800.0, 3.6, 2.0, banking, 100.0, crown, cls);
            Expect(Math.Abs(a.RunoffLengthMetres - b.RunoffLengthMetres) < 1e-12
                   && Math.Abs(a.RunoutLengthMetres - b.RunoutLengthMetres) < 1e-12,
                "determinism: the schedule lengths changed between calls", failures);

            SectionPoint[] first = CarriagewaySection.SurfaceSection(7.2,
                CarriagewaySection.SlopesAtStation(a.EntryRunoffStartStation + 12.0, a, banking, crown), 48);
            SectionPoint[] second = CarriagewaySection.SurfaceSection(7.2,
                CarriagewaySection.SlopesAtStation(a.EntryRunoffStartStation + 12.0, a, banking, crown), 48);
            Expect(first.Length == second.Length, "determinism: the section point count changed between calls",
                failures);
            int n = Math.Min(first.Length, second.Length);
            for (int i = 0; i < n; i++)
            {
                if (first[i].height == second[i].height && first[i].lateral == second[i].lateral) { continue; }
                failures.Add(Fmt("determinism: section point {0} differs between calls ({1:R} against {2:R})",
                    i, first[i].height, second[i].height));
                break;
            }

            double w1 = CarriagewaySection.WideningThresholdRadiusMetres(2, 3.6, 60.0, vehicle, cls);
            double w2 = CarriagewaySection.WideningThresholdRadiusMetres(2, 3.6, 60.0, vehicle, cls);
            Expect(w1 == w2,
                Fmt("determinism: the threshold radius changed between calls ({0:R} against {1:R})", w1, w2), failures);

            builder.Append("RCMSECTION ").Append("determinism".PadRight(26))
                   .Append(Fmt("{0} section points identical on repeat, threshold {1:F3} m repeatable", n, w1))
                   .Append("\n");
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
