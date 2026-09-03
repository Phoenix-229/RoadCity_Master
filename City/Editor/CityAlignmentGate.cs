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
    /// <summary> RM-75's gate: an alignment is geometry, so it is checked against the geometry's own published relations.
    ///
    /// The temptation with a curve fitter is to test that it produced points. Points are exactly what a broken fitter
    /// produces too -- a transition with its shift term dropped still draws a smooth-looking bend, a left curve built
    /// with the handedness reversed still draws a curve, and a chord step chosen by eye still draws a road. So none of
    /// the cases below asks whether something came out. They ask the questions that can actually be wrong: whether
    /// T, L, M, E, p, k and Ls are the PUBLISHED quantities, whether curvature rises linearly across a transition and
    /// holds constant across the arc, whether the pieces JOIN, whether a right curve is the mirror of a left one,
    /// whether the fitted polyline holds the tolerance it was given as a MEASUREMENT, and whether guided placement is
    /// genuinely inert when it is switched off.
    ///
    /// Two of them are worth reading twice. The closed-form case compares against numbers worked out from the relations
    /// by hand -- 200 tan 15 degrees, not TangentLengthMetres called twice -- because a test that recomputes the
    /// implementation's own arithmetic proves only that it is stable, not that it is right (C-15). And the continuity
    /// case walks the whole plan a metre at a time and measures the step: that is the measurement that fails if the
    /// entry transition, the arc and the exit transition were each built correctly in their own frame and then joined
    /// wrongly, which is the likeliest way this geometry can be broken while still looking right on screen.
    ///
    /// Every fixture is analytic. No scene, no terrain asset, no provider, and nothing created. </summary>
    public static class CityAlignmentGate
    {
        private const string Prefix = "RCMALIGN";

        [MenuItem("Window/Road City Master/Testing/Horizontal alignment")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError(Prefix + " FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();

            CaseCircularClosedFormsMatchTheRelations(failures);
            CaseTransitionTermsMatchTheirSeries(failures);
            CaseTangentDistanceCollapsesWithoutATransition(failures);
            CaseComfortLengthIsShorttsCriterion(failures);
            CaseTransitionCurvatureRisesLinearlyToTheArc(failures);
            CaseChordStepIsTheInvertedMidOrdinate(failures);
            CaseMinimumRadiusIsTheStricterOfTheTwo(failures);
            CaseAPlanStartsAndEndsWhereItWasAuthored(failures);
            CaseStationsAndLengthsAgree(failures);
            CaseTheResolvedPlanIsContinuous(failures);
            CaseALeftCurveMirrorsARightOne(failures);
            CaseFitHoldsTheStatedTolerance(failures);
            CaseFitSpacingIsBoundedAndHasNoDuplicates(failures);
            CaseUnderRadiusIsReportedNotRefused(failures);
            CaseGuidedPlacementOffLeavesTheNodeAlone(failures);
            CaseGuidedPlacementSnapsOnlyWithinItsTolerance(failures);
            CaseTwoResolvesAgreeExactly(failures);
            CaseNothingIsCreated(failures);

            StringBuilder sb = new StringBuilder();
            sb.Append(Prefix).Append(" BEGIN\n");
            for (int i = 0; i < failures.Count; i++)
            {
                sb.Append(Prefix).Append(" FAIL   ").Append(failures[i]).Append('\n');
            }
            sb.Append(Prefix).Append(failures.Count == 0 ? " result PASS" : " result FAIL")
                .Append("  cases 18  failures ").Append(failures.Count).Append('\n');
            sb.Append(Prefix).Append(" END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }

        private static bool Near(double a, double b, double tolerance)
        {
            return Math.Abs(a - b) <= tolerance;
        }

        /// <summary> Numbers in failure text, invariant (C-06): a gate line that reads "53,59" on one machine and
        /// "53.59" on another is a diff nobody can compare. </summary>
        private static string Num(double value)
        {
            if (double.IsInfinity(value)) { return "no limit"; }
            return value.ToString("0.#####", CultureInfo.InvariantCulture);
        }

        #region "Fixtures -- authored plans, hand-made pieces and a hand-made corridor"
        /// <summary> An ordinary rural road: two corners of the same radius turning OPPOSITE ways, on legs long enough
        /// that neither curve reaches into its neighbour. Opposite ways deliberately -- a fixture that only ever turned
        /// left would let a handedness error through every case in the file. </summary>
        private static HorizontalAlignment RuralPlan()
        {
            HorizontalAlignment plan = new HorizontalAlignment();
            plan.designSpeedKmh = 80f;
            plan.chordToleranceMetres = 0.05f;
            plan.points.Add(new AlignmentPoint(0f, 0f));
            plan.points.Add(new AlignmentPoint(600f, 0f, 400f, -1f));
            plan.points.Add(new AlignmentPoint(1200f, 500f, 400f, -1f));
            plan.points.Add(new AlignmentPoint(1900f, 500f));
            return plan;
        }

        /// <summary> The same plan reflected in Z. Every left curve becomes a right one and nothing else changes, which
        /// is what makes "the two are mirror images" an assertion about handedness and nothing else. </summary>
        private static HorizontalAlignment Mirrored(HorizontalAlignment source)
        {
            HorizontalAlignment plan = new HorizontalAlignment();
            plan.designSpeedKmh = source.designSpeedKmh;
            plan.chordToleranceMetres = source.chordToleranceMetres;
            for (int i = 0; i < source.points.Count; i++)
            {
                AlignmentPoint p = source.points[i];
                plan.points.Add(new AlignmentPoint(p.xMetres, -p.zMetres, p.radiusMetres, p.transitionLengthMetres));
            }
            return plan;
        }

        /// <summary> A 40 m corner authored on a 100 km/h road: far tighter than that speed tolerates. The gate expects
        /// it to be DRAWN and REPORTED, because this tool warns and does not block. </summary>
        private static HorizontalAlignment TightPlan()
        {
            HorizontalAlignment plan = new HorizontalAlignment();
            plan.designSpeedKmh = 100f;
            plan.points.Add(new AlignmentPoint(0f, 0f));
            plan.points.Add(new AlignmentPoint(300f, 0f, 40f, -1f));
            plan.points.Add(new AlignmentPoint(600f, 300f));
            return plan;
        }

        private static RoadClass GateClass()
        {
            RoadClass cls = new RoadClass();
            cls.id = 7;
            cls.displayName = "gate alignment class";
            return cls;
        }

        /// <summary> One piece, built by hand rather than resolved, so a curvature or heading assertion is about that
        /// piece's own arithmetic and not about the chain that produced it. Anchored at the origin, heading +X. </summary>
        private static AlignmentElement Piece(AlignmentElementKind kind, double lengthMetres, double radiusMetres,
            double transitionLengthMetres, int turnSign)
        {
            AlignmentElement e = new AlignmentElement();
            e.kind = kind;
            e.startStationMetres = 0.0;
            e.lengthMetres = lengthMetres;
            e.radiusMetres = radiusMetres;
            e.transitionLengthMetres = transitionLengthMetres;
            e.turnSign = turnSign;
            e.anchorXMetres = 0.0;
            e.anchorZMetres = 0.0;
            e.headingX = 1.0;
            e.headingZ = 0.0;
            return e;
        }

        /// <summary> A raster with one cheap row through expensive ground. The cheap row is the only route the solver
        /// can prefer, so the cells guided placement should snap to are known before it runs -- which makes the guidance
        /// assertions about the answer rather than about the arithmetic that produced it. </summary>
        private static SuitabilityRasterClass Channel(int widthCells, int depthCells, int cheapRow)
        {
            SuitabilityRasterClass surface = new SuitabilityRasterClass(widthCells, depthCells, 10f);
            for (int cz = 0; cz < depthCells; cz++)
            {
                for (int cx = 0; cx < widthCells; cx++)
                {
                    int index = SuitabilityRaster.Index(surface, cx, cz);
                    if (index < 0) { continue; }
                    surface.cost[index] = cz == cheapRow ? 0f : 0.9f;
                    surface.binding[index] = (byte)(cz == cheapRow ? SuitabilityFactor.None : SuitabilityFactor.Slope);
                }
            }
            return surface;
        }
        #endregion

        #region "The published relations"
        /// <summary> T, L, M and E for a 200 m radius through 30 degrees, against figures worked out from the relations
        /// by hand: T = 200 tan 15 = 53.58984 m, L = 200 pi/6 = 104.71976 m, M = 200(1 - cos 15) = 6.81483 m and
        /// E = 200(sec 15 - 1) = 7.05524 m. The external distance is also asked for a corner that turns back on itself,
        /// where the secant diverges: the honest answer is that the curve never rejoins its tangents, and a large finite
        /// number there would be a fabrication an author could not distinguish from a real one. </summary>
        private static void CaseCircularClosedFormsMatchTheRelations(List<string> failures)
        {
            double r = 200.0;
            double delta = Math.PI / 6.0;

            double t = AlignmentGeometry.TangentLengthMetres(r, delta);
            double l = AlignmentGeometry.ArcLengthMetres(r, delta);
            double m = AlignmentGeometry.MidOrdinateMetres(r, delta);
            double e = AlignmentGeometry.ExternalMetres(r, delta);
            double back = AlignmentGeometry.ExternalMetres(r, Math.PI);

            Expect(Near(t, 53.589838, 1e-5),
                "tangent distance on 200 m through 30 degrees should be 53.58984 m, is " + Num(t), failures);
            Expect(Near(l, 104.719755, 1e-5),
                "arc length on 200 m through 30 degrees should be 104.71976 m, is " + Num(l), failures);
            Expect(Near(m, 6.814835, 1e-5),
                "mid-ordinate on 200 m through 30 degrees should be 6.81483 m, is " + Num(m), failures);
            Expect(Near(e, 7.055236, 1e-5),
                "external distance on 200 m through 30 degrees should be 7.05524 m, is " + Num(e), failures);
            Expect(double.IsPositiveInfinity(back),
                "a corner turning back on itself has no finite external distance, reported " + Num(back), failures);
        }

        /// <summary> The transition's three terms for Ls = 60 m on R = 300 m, each worked out by hand: theta_s = 60/600
        /// = 0.1 rad, p = 3600/7200 = 0.5 m and k = 30 - 216000/21600000 = 29.99 m.
        ///
        /// Then the cross-check that matters most: the series' own offset at l = Ls must come out at four times the
        /// shift, y(Ls) = 4p, which is the published relation between two quantities this file computes by completely
        /// different routes -- one a truncated Fresnel series, the other a closed form. If either is wrong they disagree,
        /// and no amount of drawing would show it. </summary>
        private static void CaseTransitionTermsMatchTheirSeries(List<string> failures)
        {
            double ls = 60.0;
            double r = 300.0;

            double theta = AlignmentGeometry.TransitionAngleRadians(ls, r);
            double p = AlignmentGeometry.TransitionShiftMetres(ls, r);
            double k = AlignmentGeometry.TransitionTangentAdvanceMetres(ls, r);
            double along;
            double offset;
            AlignmentGeometry.TransitionLocal(ls, r, ls, out along, out offset);

            Expect(Near(theta, 0.1, 1e-12), "a 60 m transition on 300 m turns through 0.1 rad, turns " + Num(theta),
                failures);
            Expect(Near(p, 0.5, 1e-12), "the shift for 60 m on 300 m is 0.5 m, is " + Num(p), failures);
            Expect(Near(k, 29.99, 1e-9), "the tangent advance for 60 m on 300 m is 29.99 m, is " + Num(k), failures);
            Expect(Near(along, 59.94003, 1e-4),
                "the series' along-distance at the end of that transition is 59.94003 m, is " + Num(along), failures);
            Expect(Near(offset, 4.0 * p, 2e-3),
                "the series' offset at the end of a transition should be four times the shift, " + Num(4.0 * p)
                + " m, is " + Num(offset), failures);
        }

        /// <summary> Ts = (R + p) tan(delta/2) + k must collapse to R tan(delta/2) when there is no transition, so the
        /// tool holds ONE tangent-distance formula rather than two code paths that can drift apart. With Ls = 60 m on
        /// R = 200 m through 30 degrees the hand figure is (200.75)(0.2679492) + 29.9775 = 83.76830 m, and it must be
        /// LONGER than the plain 53.58984 m -- a transitioned curve reaches further back from the corner, which is why
        /// adding transitions to a tight plan is what makes its curves start overlapping. </summary>
        private static void CaseTangentDistanceCollapsesWithoutATransition(List<string> failures)
        {
            double plain = AlignmentGeometry.TotalTangentLengthMetres(200.0, Math.PI / 6.0, 0.0);
            double spiralled = AlignmentGeometry.TotalTangentLengthMetres(200.0, Math.PI / 6.0, 60.0);

            Expect(Near(plain, 53.589838, 1e-5),
                "with no transition the tangent distance is the plain 53.58984 m, is " + Num(plain), failures);
            Expect(Near(spiralled, 83.768300, 1e-4),
                "with a 60 m transition on 200 m through 30 degrees it is 83.76830 m, is " + Num(spiralled), failures);
            Expect(spiralled > plain, "a transitioned curve must reach further back than a plain one, " + Num(spiralled)
                + " m against " + Num(plain) + " m", failures);
        }

        /// <summary> Shortt's comfort criterion, Ls = v^3/(C R). At 90 km/h (v = 25 m/s) on a 400 m radius at
        /// C = 0.5 m/s^3 the hand figure is 15625/200 = 78.125 m.
        ///
        /// Then the two proportionalities, which are what separate this relation from a plausible-looking wrong one:
        /// doubling the radius must HALVE the length, and doubling the speed must multiply it by EIGHT. A file that had
        /// dropped the cube to a square would pass the first check and fail the second. </summary>
        private static void CaseComfortLengthIsShorttsCriterion(List<string> failures)
        {
            double ls = AlignmentGeometry.ComfortTransitionLengthMetres(90.0, 400.0, 0.5);
            double wider = AlignmentGeometry.ComfortTransitionLengthMetres(90.0, 800.0, 0.5);
            double faster = AlignmentGeometry.ComfortTransitionLengthMetres(180.0, 400.0, 0.5);
            double straight = AlignmentGeometry.ComfortTransitionLengthMetres(90.0, 0.0, 0.5);

            Expect(Near(ls, 78.125, 1e-6),
                "Shortt at 90 km/h on 400 m with C = 0.5 gives 78.125 m, gives " + Num(ls), failures);
            Expect(Near(wider, ls * 0.5, 1e-9), "twice the radius needs half the transition, " + Num(ls * 0.5)
                + " m, needs " + Num(wider) + " m", failures);
            Expect(Near(faster, ls * 8.0, 1e-6), "the relation is cubic in speed, so twice the speed needs eight times "
                + "the transition, " + Num(ls * 8.0) + " m, needs " + Num(faster) + " m", failures);
            Expect(straight == 0.0, "a straight needs no transition, was given " + Num(straight) + " m", failures);
        }

        /// <summary> Curvature across the three pieces of a curve: zero at the straight end of a transition, half the
        /// arc's halfway along it, the arc's own at the join, constant across the arc, and back to zero at the far end of
        /// the exit transition. That progression IS the transition -- a clothoid is defined as the curve whose curvature
        /// rises linearly with distance -- so a file that had it rising with anything else would draw a bend that still
        /// looked like a bend and would jerk the wheel where it met the arc.
        ///
        /// The sign is checked too, on the same arc built the other way round: a right-hand curve must report NEGATIVE
        /// curvature, because a ribbon that coloured both hands the same could not distinguish a reverse curve from a
        /// compound one, and a banking pass reading it would lift the wrong edge. </summary>
        private static void CaseTransitionCurvatureRisesLinearlyToTheArc(List<string> failures)
        {
            double r = 300.0;
            double ls = 60.0;
            AlignmentElement entry = Piece(AlignmentElementKind.EntryTransition, ls, r, ls, 1);
            AlignmentElement arc = Piece(AlignmentElementKind.Arc, 120.0, r, ls, 1);
            AlignmentElement exit = Piece(AlignmentElementKind.ExitTransition, ls, r, ls, 1);
            AlignmentElement straight = Piece(AlignmentElementKind.Tangent, 100.0, 0.0, 0.0, 0);
            AlignmentElement rightArc = Piece(AlignmentElementKind.Arc, 120.0, r, ls, -1);

            Expect(Near(AlignmentGeometry.CurvatureAt(entry, 0.0), 0.0, 1e-12),
                "a transition leaves the straight with no curvature, has "
                + Num(AlignmentGeometry.CurvatureAt(entry, 0.0)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(entry, ls * 0.5), 0.5 / r, 1e-12),
                "halfway along a transition the curvature is half the arc's, " + Num(0.5 / r) + ", is "
                + Num(AlignmentGeometry.CurvatureAt(entry, ls * 0.5)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(entry, ls), 1.0 / r, 1e-12),
                "a transition must arrive at exactly the arc's curvature " + Num(1.0 / r) + ", arrives at "
                + Num(AlignmentGeometry.CurvatureAt(entry, ls)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(arc, 0.0), 1.0 / r, 1e-12)
                && Near(AlignmentGeometry.CurvatureAt(arc, 120.0), 1.0 / r, 1e-12),
                "an arc's curvature is constant along it, reads " + Num(AlignmentGeometry.CurvatureAt(arc, 0.0))
                + " then " + Num(AlignmentGeometry.CurvatureAt(arc, 120.0)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(exit, 0.0), 1.0 / r, 1e-12),
                "an exit transition begins at the arc's curvature, begins at "
                + Num(AlignmentGeometry.CurvatureAt(exit, 0.0)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(exit, ls), 0.0, 1e-12),
                "an exit transition ends straight, ends at " + Num(AlignmentGeometry.CurvatureAt(exit, ls)), failures);
            Expect(AlignmentGeometry.CurvatureAt(straight, 50.0) == 0.0,
                "a straight has no curvature anywhere, reports "
                + Num(AlignmentGeometry.CurvatureAt(straight, 50.0)), failures);
            Expect(Near(AlignmentGeometry.CurvatureAt(rightArc, 60.0), -1.0 / r, 1e-12),
                "a right-hand arc's curvature is negative, " + Num(-1.0 / r) + ", is "
                + Num(AlignmentGeometry.CurvatureAt(rightArc, 60.0)), failures);
        }

        /// <summary> The chord step must be the step whose OWN mid-ordinate is the tolerance. That is checked by taking
        /// the step back through the mid-ordinate relation and expecting the tolerance out again, which is a genuine
        /// round trip through two separately written formulas rather than a restatement of one. For R = 400 m at 5 cm the
        /// hand figure is 400 * 2 acos(1 - 0.000125) = 12.64924 m. </summary>
        private static void CaseChordStepIsTheInvertedMidOrdinate(List<string> failures)
        {
            double r = 400.0;
            double tolerance = 0.05;
            double step = AlignmentGeometry.ChordStepMetres(r, tolerance);
            double departure = AlignmentGeometry.MidOrdinateMetres(r, step / r);
            double tighter = AlignmentGeometry.ChordStepMetres(r, 0.01);

            Expect(Near(step, 12.64924, 1e-3),
                "the 5 cm chord step on a 400 m radius is 12.64924 m, is " + Num(step), failures);
            Expect(Near(departure, tolerance, 1e-9), "a chord of that step must depart from the arc by exactly the "
                + Num(tolerance) + " m asked for, departs by " + Num(departure) + " m", failures);
            Expect(tighter < step, "a tighter tolerance must sample more closely, 1 cm gives " + Num(tighter)
                + " m against 5 cm giving " + Num(step) + " m", failures);
            Expect(AlignmentGeometry.ChordStepMetres(0.0, tolerance) == 0.0,
                "a straight has no chord step, was given " + Num(AlignmentGeometry.ChordStepMetres(0.0, tolerance)),
                failures);
        }

        /// <summary> The minimum radius is the STRICTER of two independent minima: what the design speed demands through
        /// e + f = V^2/(127 R), and what the road class itself will not go below. At 100 km/h with the shipped 6 %
        /// maximum superelevation and the table's f = 0.12 the relation gives 10000/(127 x 0.18) = 437.4 m, well above an
        /// ordinary class's 30 m, so the speed governs. Turn it around -- a class that refuses anything under 600 m,
        /// authored at 30 km/h where the relation asks for only 31 m -- and the class must win instead. A file that took
        /// one of the two and ignored the other passes exactly one of these halves. </summary>
        private static void CaseMinimumRadiusIsTheStricterOfTheTwo(List<string> failures)
        {
            SuperelevationClass design = SuperelevationClass.Default();
            RoadClass ordinary = GateClass();
            double speedGoverned = AlignmentResolver.MinimumRadiusMetres(100.0, ordinary, design);

            RoadClass sweepingOnly = GateClass();
            sweepingOnly.minCurveRadius = 600f;
            double classGoverned = AlignmentResolver.MinimumRadiusMetres(30.0, sweepingOnly, design);

            Expect(Near(speedGoverned, 437.4, 1.0), "at 100 km/h the published relation demands about 437.4 m, demands "
                + Num(speedGoverned) + " m", failures);
            Expect(speedGoverned > ordinary.minCurveRadius, "at 100 km/h the speed is the stricter of the two and must "
                + "win over the class's own " + Num(ordinary.minCurveRadius) + " m, reported " + Num(speedGoverned)
                + " m", failures);
            Expect(Near(classGoverned, 600.0, 1e-6), "at 30 km/h the class is the stricter of the two and must win: "
                + "600 m, reported " + Num(classGoverned) + " m", failures);
        }
        #endregion

        #region "A resolved plan"
        /// <summary> A resolved plan must begin and end exactly where it was authored, and be SHORTER than its own
        /// outline in between. The rural fixture's outline is 600 + 781.03 + 700 = 2081.03 m and the straight line from
        /// end to end is 1964.69 m, so a plan whose length fell outside that window would be one whose curves were cut
        /// somewhere other than at the corners they belong to. </summary>
        private static void CaseAPlanStartsAndEndsWhereItWasAuthored(List<string> failures)
        {
            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());

            double x0;
            double z0;
            double x1;
            double z1;
            bool gotStart = AlignmentResolver.PointAtStation(solution, 0.0, out x0, out z0);
            bool gotEnd = AlignmentResolver.PointAtStation(solution, solution.lengthMetres, out x1, out z1);

            Expect(solution.elements.Count > 0, "a four-point plan must resolve to geometry, resolved to "
                + solution.elements.Count + " pieces", failures);
            Expect(solution.curves.Count == 2, "two interior corners give two curves, gave " + solution.curves.Count,
                failures);
            Expect(gotStart && Near(x0, 0.0, 1e-6) && Near(z0, 0.0, 1e-6),
                "the plan must start at the authored (0, 0), starts at (" + Num(x0) + ", " + Num(z0) + ")", failures);
            Expect(gotEnd && Near(x1, 1900.0, 1e-6) && Near(z1, 500.0, 1e-6),
                "the plan must end at the authored (1900, 500), ends at (" + Num(x1) + ", " + Num(z1) + ")", failures);
            Expect(solution.lengthMetres < 2081.03 && solution.lengthMetres > 1964.68,
                "curves cut corners, so the plan must be shorter than its 2081.03 m outline and longer than the "
                + "1964.69 m straight line between its ends, is " + Num(solution.lengthMetres) + " m", failures);
        }

        /// <summary> The stations must be a single continuous measure: every piece starting where the previous one ended,
        /// and the total equal to the sum of the pieces. Then the two questions a station can answer wrongly -- a station
        /// before the start and a station past the end are both OFF the plan, and must be answered with -1 rather than
        /// clamped to an end, because a caller that got the last point back would draw a road carrying on past where it
        /// stops. </summary>
        private static void CaseStationsAndLengthsAgree(List<string> failures)
        {
            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());

            double sum = 0.0;
            bool chained = true;
            for (int i = 0; i < solution.elements.Count; i++)
            {
                if (!Near(solution.elements[i].startStationMetres, sum, 1e-6)) { chained = false; }
                sum += solution.elements[i].lengthMetres;
            }

            double into;
            int before = solution.ElementAt(-1.0, out into);
            int after = solution.ElementAt(solution.lengthMetres + 1.0, out into);
            int middle = solution.ElementAt(solution.lengthMetres * 0.5, out into);

            Expect(chained, "each piece must start at the station the previous one ended at", failures);
            Expect(Near(sum, solution.lengthMetres, 1e-6), "the plan's length must be the sum of its pieces, "
                + Num(sum) + " m, is " + Num(solution.lengthMetres) + " m", failures);
            Expect(before == -1, "a station before the start is off the plan, was answered with piece " + before,
                failures);
            Expect(after == -1, "a station past the end is off the plan and must not be clamped to the last point, was "
                + "answered with piece " + after, failures);
            Expect(middle >= 0 && into >= 0.0 && into <= solution.elements[middle].lengthMetres + 1e-9,
                "a station inside the plan must land inside its own piece, landed " + Num(into) + " m into piece "
                + middle, failures);
        }

        /// <summary> The joint test, and the most valuable case in the file. Each of the three pieces of a curve is built
        /// in its own frame -- the entry transition from the tangent point, the arc from the shifted circle, the exit
        /// transition BACKWARDS from its own straight end -- and each can be individually correct while the chain that
        /// joins them is wrong. So the plan is walked a metre at a time and the step is MEASURED: a metre of station must
        /// be a metre of ground, and the heading must not jump. A mis-anchored exit transition shows here as a step of
        /// metres, and shows nowhere else except as a kink an author would have to notice by eye. </summary>
        private static void CaseTheResolvedPlanIsContinuous(List<string> failures)
        {
            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());

            double worstStep = 0.0;
            double worstTurn = 0.0;
            double px = 0.0;
            double pz = 0.0;
            double phx = 0.0;
            double phz = 0.0;
            bool first = true;

            for (double s = 0.0; s <= solution.lengthMetres; s += 1.0)
            {
                double x;
                double z;
                double hx;
                double hz;
                if (!AlignmentResolver.PointAtStation(solution, s, out x, out z)) { continue; }
                if (!AlignmentResolver.HeadingAtStation(solution, s, out hx, out hz)) { continue; }
                if (!first)
                {
                    double step = Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                    if (Math.Abs(step - 1.0) > worstStep) { worstStep = Math.Abs(step - 1.0); }
                    double dot = phx * hx + phz * hz;
                    if (1.0 - dot > worstTurn) { worstTurn = 1.0 - dot; }
                }
                px = x;
                pz = z;
                phx = hx;
                phz = hz;
                first = false;
            }

            Expect(worstStep < 0.01, "a metre of station must be a metre of ground everywhere on the plan; the worst "
                + "step was out by " + Num(worstStep) + " m, which is a joint that does not meet", failures);
            Expect(worstTurn < 1e-4, "the heading must not jump between pieces; the worst one-metre turn lost "
                + Num(worstTurn) + " of unit dot product", failures);
        }

        /// <summary> Reflect the plan in Z and every curve must become its own mirror: the same length, the same stations,
        /// the opposite hand, and the same points with their Z negated. This is the case that catches a handedness error,
        /// which is the defect a curve fitter is likeliest to ship -- a road built with left and right swapped looks
        /// entirely correct until it is compared with the plan it was drawn from. The readout is checked too, because a
        /// geometry that turns the right way while its inspector says "left" is only half a fix. </summary>
        private static void CaseALeftCurveMirrorsARightOne(List<string> failures)
        {
            HorizontalAlignment plan = RuralPlan();
            AlignmentSolution left = AlignmentResolver.Resolve(plan, GateClass(), SuperelevationClass.Default());
            AlignmentSolution right = AlignmentResolver.Resolve(Mirrored(plan), GateClass(),
                SuperelevationClass.Default());

            bool signsFlipped = left.curves.Count == right.curves.Count && left.curves.Count == 2;
            for (int i = 0; i < left.curves.Count && i < right.curves.Count; i++)
            {
                if (left.curves[i].turnSign + right.curves[i].turnSign != 0) { signsFlipped = false; }
            }

            double worst = 0.0;
            for (double s = 0.0; s <= left.lengthMetres; s += 5.0)
            {
                double lx;
                double lz;
                double rx;
                double rz;
                if (!AlignmentResolver.PointAtStation(left, s, out lx, out lz)) { continue; }
                if (!AlignmentResolver.PointAtStation(right, s, out rx, out rz)) { continue; }
                double apart = Math.Max(Math.Abs(lx - rx), Math.Abs(lz + rz));
                if (apart > worst) { worst = apart; }
            }

            Expect(signsFlipped, "reflecting a plan must reverse the hand of every curve", failures);
            Expect(Near(left.lengthMetres, right.lengthMetres, 1e-9), "a reflected plan is exactly as long, "
                + Num(left.lengthMetres) + " m against " + Num(right.lengthMetres) + " m", failures);
            Expect(worst < 1e-6, "a reflected plan must be the mirror image station for station; the worst "
                + "disagreement was " + Num(worst) + " m", failures);
            Expect(left.curves.Count == 2 && right.curves.Count == 2
                && AlignmentResolver.CurveReadout(left.curves[0]).Contains("turning left")
                && AlignmentResolver.CurveReadout(right.curves[0]).Contains("turning right"),
                "the readout must name which way the road turns, and name it the same way the geometry does", failures);
        }
        #endregion

        #region "The fit, the warnings and the guidance"
        /// <summary> "Fitted densely enough" has to be a MEASUREMENT or it is a hope. The fitter evaluates the true curve
        /// at the midpoint of every chord and compares it with the chord's own midpoint, so the tolerance is checked
        /// against the geometry rather than assumed from the step. Both a fine and a coarse fit must hold their own
        /// figure, and the fine one must use MORE points -- a fitter that ignored the tolerance would pass the first half
        /// of that and fail the second. </summary>
        private static void CaseFitHoldsTheStatedTolerance(List<string> failures)
        {
            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());
            AlignmentFit fine = AlignmentFitter.Fit(solution, 0.05);
            AlignmentFit coarse = AlignmentFitter.Fit(solution, 0.20);

            Expect(fine.PointCount > 2, "a two-curve plan needs more than its end points, produced " + fine.PointCount,
                failures);
            Expect(fine.withinTolerance && fine.worstDepartureMetres <= 0.05 + 1e-9,
                "the fitted line must hold the 5 cm it was asked for; the measured worst departure was "
                + Num(fine.worstDepartureMetres) + " m", failures);
            Expect(coarse.withinTolerance && coarse.worstDepartureMetres <= 0.20 + 1e-9,
                "the same plan at 20 cm must hold 20 cm; the measured worst departure was "
                + Num(coarse.worstDepartureMetres) + " m", failures);
            Expect(fine.PointCount > coarse.PointCount, "a tighter tolerance must sample more densely: 5 cm gave "
                + fine.PointCount + " points and 20 cm gave " + coarse.PointCount, failures);
            Expect(fine.stationsMetres.Count == fine.PointCount, "every point must carry the station it came from, "
                + fine.PointCount + " points against " + fine.stationsMetres.Count + " stations", failures);
            Expect(fine.report.Contains("worst departure"),
                "the fit must report its measured departure rather than assert its own density", failures);
        }

        /// <summary> Two things about the fitted points that a spline cannot survive. No two may COINCIDE, because two
        /// nodes in the same place have no direction between them and the curve through them is undefined -- that is
        /// exactly what a zero-length piece produces if it is sampled instead of stepped over. And no gap may exceed the
        /// authored ceiling, which exists so an author can grab a long straight somewhere other than its ends.
        ///
        /// The Y check belongs here too: the fitter leaves height at zero deliberately, because this is the HORIZONTAL
        /// alignment and whoever installs the points samples the ground through the terrain reader. A fitter that had
        /// quietly baked a height in would give the alignment a hidden dependency on terrain. </summary>
        private static void CaseFitSpacingIsBoundedAndHasNoDuplicates(List<string> failures)
        {
            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());
            AlignmentFit fit = AlignmentFitter.Fit(solution, 0.05);

            double longestGap = 0.0;
            double closestPair = double.PositiveInfinity;
            bool stationsRise = true;
            bool flat = true;
            for (int i = 0; i < fit.points.Count; i++)
            {
                if (fit.points[i].y != 0f) { flat = false; }
                if (i == 0) { continue; }
                double gap = fit.stationsMetres[i] - fit.stationsMetres[i - 1];
                if (gap <= 0.0) { stationsRise = false; }
                if (gap > longestGap) { longestGap = gap; }
                double dx = (double)fit.points[i].x - fit.points[i - 1].x;
                double dz = (double)fit.points[i].z - fit.points[i - 1].z;
                double apart = Math.Sqrt(dx * dx + dz * dz);
                if (apart < closestPair) { closestPair = apart; }
            }

            Expect(longestGap <= AlignmentFitter.MaximumStepMetres + 1e-6, "no gap may exceed the authored "
                + Num(AlignmentFitter.MaximumStepMetres) + " m ceiling, the longest was " + Num(longestGap) + " m",
                failures);
            Expect(closestPair > 1e-3, "two fitted points in the same place leave the spline with no tangent there; "
                + "the closest pair was " + Num(closestPair) + " m apart", failures);
            Expect(stationsRise, "the stations must rise along the plan, and one did not", failures);
            Expect(flat, "the fitter leaves height at zero for the installer to sample, and a point came back lifted",
                failures);
        }

        /// <summary> A 40 m corner authored on a 100 km/h road is a mistake, and this tool's settled answer to a mistake
        /// is to DRAW it and SAY SO. So the case asserts both halves: that the geometry came out, and that the curve
        /// carries the radius the speed demanded next to the one it was given, in a sentence with both numbers in it. The
        /// other half matters as much -- an ordinary 400 m curve at 80 km/h must NOT be flagged, or the warning becomes
        /// noise an author learns to ignore. </summary>
        private static void CaseUnderRadiusIsReportedNotRefused(List<string> failures)
        {
            AlignmentSolution tight = AlignmentResolver.Resolve(TightPlan(), GateClass(),
                SuperelevationClass.Default());
            AlignmentSolution ordinary = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());
            string readout = tight.curves.Count == 1 ? AlignmentResolver.CurveReadout(tight.curves[0]) : string.Empty;

            Expect(tight.elements.Count > 0 && tight.lengthMetres > 0.0,
                "a curve too tight for its speed is still drawn -- this tool warns and does not block; the plan "
                + "resolved to " + tight.elements.Count + " pieces over " + Num(tight.lengthMetres) + " m", failures);
            Expect(tight.curves.Count == 1 && tight.curves[0].belowMinimumRadius,
                "a 40 m corner on a 100 km/h road must be reported as below the minimum radius", failures);
            Expect(tight.curves.Count == 1 && tight.curves[0].minimumRadiusMetres > tight.curves[0].radiusMetres,
                "the curve must carry the radius the speed demanded beside the one it was given", failures);
            Expect(readout.Contains("tighter than the"),
                "the readout must say so in words an author can act on, says: " + readout, failures);
            Expect(tight.legend.Length > 0, "a plan's legend is never empty", failures);
            Expect(ordinary.curves.Count == 2 && !ordinary.curves[0].belowMinimumRadius
                && !ordinary.curves[1].belowMinimumRadius,
                "a 400 m curve at 80 km/h is inside the standard and must not be flagged, or the warning becomes noise",
                failures);
        }

        /// <summary> Guided placement is OPT-IN, and this is the case that proves it. With the switch off -- and with a
        /// solved map and a solved corridor sitting right there, so the only thing stopping a snap is the switch -- the
        /// suggestion must be the author's own position to the bit, the movement must be zero, and the outcome must NAME
        /// itself as disabled rather than merely happen to agree. A guidance layer that quietly nudged a node it had
        /// decided not to snap would be indistinguishable from a bug. A null plan is the same answer: the author's hand
        /// wins, because that is the safe direction to fail in. </summary>
        private static void CaseGuidedPlacementOffLeavesTheNodeAlone(List<string> failures)
        {
            SuitabilityRasterClass map = Channel(24, 24, 5);
            CorridorPlan corridor = CorridorSolver.Solve(map, 0, 5, 23, 5, 0.0);
            HorizontalAlignment plan = RuralPlan();

            GuidanceResult off = CorridorGuidance.Suggest(plan, map, corridor, 105.0, 60.0);
            GuidanceResult noPlan = CorridorGuidance.Suggest(null, map, corridor, 105.0, 60.0);

            Expect(off.outcome == GuidanceOutcome.Disabled, "with guided placement off the outcome must name itself "
                + "disabled, named " + off.outcome, failures);
            Expect(off.xMetres == 105.0 && off.zMetres == 60.0, "with guided placement off the node stays exactly where "
                + "the author put it, came back at (" + Num(off.xMetres) + ", " + Num(off.zMetres) + ")", failures);
            Expect(off.movedMetres == 0.0, "a disabled suggestion moves nothing, moved " + Num(off.movedMetres) + " m",
                failures);
            Expect(off.sentence.Length > 0, "every suggestion carries a sentence naming its reason", failures);
            Expect(noPlan.outcome == GuidanceOutcome.Disabled && noPlan.xMetres == 105.0 && noPlan.zMetres == 60.0,
                "a plan that does not exist cannot have asked for guidance, so the author's position must survive it",
                failures);
        }

        /// <summary> Guidance ON: a node inside the tolerance is pulled onto the cheapest line, and a node outside it is
        /// left alone WITH its distance still quoted. Both halves matter -- a snap that fired at any distance would drag
        /// a node across the map, and a refusal that said nothing would look like a missing corridor. </summary>
        private static void CaseGuidedPlacementSnapsOnlyWithinItsTolerance(List<string> failures)
        {
            SuitabilityRasterClass map = Channel(24, 24, 5);
            CorridorPlan corridor = CorridorSolver.Solve(map, 0, 5, 23, 5, 0.0);
            HorizontalAlignment plan = RuralPlan();
            plan.guidedPlacementEnabled = true;
            plan.guidedSnapToleranceMetres = 24f;

            Expect(corridor.reached, "the fixture corridor must solve, or the two halves below prove nothing", failures);

            // The cheap row is cz = 5, whose cell centres sit at z = 55 on a 10 m raster at origin zero. A node at
            // (105, 60) is 5 m off that line and in a different cell; a node at (105, 175) is 120 m off it.
            GuidanceResult near = CorridorGuidance.Suggest(plan, map, corridor, 105.0, 60.0);
            GuidanceResult far = CorridorGuidance.Suggest(plan, map, corridor, 105.0, 175.0);

            Expect(near.outcome == GuidanceOutcome.MovedToCheapest, "a node 5 m from the cheapest line is inside the "
                + "24 m snap and must be pulled onto it, outcome " + near.outcome, failures);
            Expect(Near(near.xMetres, 105.0, 1e-9) && Near(near.zMetres, 55.0, 1e-9), "the snap target is the cheapest "
                + "line's own cell centre at (105, 55), landed at (" + Num(near.xMetres) + ", " + Num(near.zMetres)
                + ")", failures);
            Expect(Near(near.movedMetres, 5.0, 1e-9) && Near(near.metresToCheapestLine, 5.0, 1e-9), "the move and the "
                + "reported distance are the same 5 m; moved " + Num(near.movedMetres) + " m, reported "
                + Num(near.metresToCheapestLine) + " m", failures);
            Expect(near.relativeCost > 0.0, "a snap quotes what a metre of road costs where the node ended up, quoted "
                + Num(near.relativeCost), failures);

            Expect(far.outcome != GuidanceOutcome.MovedToCheapest, "a node 120 m from the cheapest line is beyond the "
                + "24 m snap and must not be moved, outcome " + far.outcome, failures);
            Expect(far.xMetres == 105.0 && far.zMetres == 175.0, "a refused suggestion "
                + "returns the author's own position, came back at (" + Num(far.xMetres) + ", " + Num(far.zMetres)
                + ")", failures);
            Expect(far.movedMetres == 0.0, "a refused suggestion moves nothing, moved " + Num(far.movedMetres) + " m",
                failures);
            Expect(!double.IsInfinity(far.metresToCheapestLine) && far.metresToCheapestLine > 24.0, "the distance to "
                + "the cheapest line is reported even when nothing snapped -- a number is what tells an author the "
                + "corridor is over there rather than absent; reported " + Num(far.metresToCheapestLine), failures);
            Expect(far.sentence.Length > 0 && near.sentence.Length > 0, "both a snap and a refusal carry a sentence",
                failures);

            GuidanceResult noCorridor = CorridorGuidance.Suggest(plan, map, new CorridorPlan(), 105.0, 60.0);
            Expect(noCorridor.outcome == GuidanceOutcome.NoMap && noCorridor.xMetres == 105.0
                && noCorridor.zMetres == 60.0, "with nothing solved to be guided by, guidance says so and leaves the "
                + "node alone, outcome " + noCorridor.outcome, failures);
        }

        /// <summary> C-06: the same plan resolved and fitted twice must agree to the last bit. Components are compared
        /// with == rather than Vector3's own operator, which forgives a difference of a fraction of a millimetre -- and a
        /// fraction of a millimetre of drift per run is exactly the defect this case exists to catch. </summary>
        private static void CaseTwoResolvesAgreeExactly(List<string> failures)
        {
            AlignmentSolution first = AlignmentResolver.Resolve(RuralPlan(), GateClass(), SuperelevationClass.Default());
            AlignmentSolution again = AlignmentResolver.Resolve(RuralPlan(), GateClass(), SuperelevationClass.Default());
            AlignmentFit fitFirst = AlignmentFitter.Fit(first, 0.05);
            AlignmentFit fitAgain = AlignmentFitter.Fit(again, 0.05);

            Expect(first.elements.Count == again.elements.Count && first.lengthMetres == again.lengthMetres,
                "two resolves must produce the same pieces and the same length; " + first.elements.Count + " pieces of "
                + Num(first.lengthMetres) + " m became " + again.elements.Count + " of " + Num(again.lengthMetres)
                + " m", failures);
            Expect(fitFirst.PointCount == fitAgain.PointCount, "two fits must produce the same number of points, "
                + fitFirst.PointCount + " then " + fitAgain.PointCount, failures);

            int differing = 0;
            int limit = Math.Min(fitFirst.PointCount, fitAgain.PointCount);
            for (int i = 0; i < limit; i++)
            {
                if (fitFirst.points[i].x != fitAgain.points[i].x || fitFirst.points[i].y != fitAgain.points[i].y
                    || fitFirst.points[i].z != fitAgain.points[i].z
                    || fitFirst.stationsMetres[i] != fitAgain.stationsMetres[i])
                {
                    differing++;
                }
            }
            Expect(differing == 0, "two fits of the same plan must agree bit for bit; " + differing
                + " of " + limit + " points differed", failures);
            Expect(first.legend == again.legend && fitFirst.report == fitAgain.report,
                "and the words they produce must be identical text, not merely equivalent", failures);
        }


        /// <summary> The whole of RM-75 is arithmetic: resolving, fitting and being guided create nothing. Whoever
        /// installs the points owns the undo step, and this case is what keeps that true after a later edit. </summary>
        private static void CaseNothingIsCreated(List<string> failures)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int before = scene.IsValid() ? scene.rootCount : -1;

            AlignmentSolution solution = AlignmentResolver.Resolve(RuralPlan(), GateClass(),
                SuperelevationClass.Default());
            AlignmentFit fit = AlignmentFitter.Fit(solution, 0.05);
            SuitabilityRasterClass map = Channel(24, 24, 5);
            CorridorPlan corridor = CorridorSolver.Solve(map, 0, 5, 23, 5, 0.0);
            HorizontalAlignment guided = RuralPlan();
            guided.guidedPlacementEnabled = true;
            CorridorGuidance.Suggest(guided, map, corridor, 105.0, 60.0);
            Expect(fit.PointCount > 0, "the fixture work should have run, so the count below means something", failures);

            int after = scene.IsValid() ? scene.rootCount : -1;
            Expect(before == after, "resolving, fitting and being guided should create no scene object; " + before
                + " roots became " + after, failures);
            Expect(NoMethodNamedLike(typeof(AlignmentFit),
                new string[] { "Apply", "Commit", "Install", "Create", "Spawn", "Write" }),
                "a fit should carry no way to install itself onto a spline", failures);
            Expect(NoMethodNamedLike(typeof(HorizontalAlignment),
                new string[] { "Apply", "Commit", "Install", "Create", "Spawn" }),
                "an authored plan should carry no way to build the road it describes", failures);
        }


        /// <summary> Whether a type's public methods avoid a set of verbs. Reflection rather than a source scan, so it
        /// still holds after a rename or across a partial class. </summary>
        private static bool NoMethodNamedLike(Type type, string[] verbs)
        {
            System.Reflection.MethodInfo[] methods = type.GetMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                for (int v = 0; v < verbs.Length; v++)
                {
                    if (methods[i].Name.Contains(verbs[v])) { return false; }
                }
            }
            return true;
        }
        #endregion
    }
}
#endif
