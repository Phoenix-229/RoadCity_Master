// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Turns an authored plan into staked geometry: corners in, pieces out, with the reasons written down.
    ///
    /// The resolver is where the design STANDARD is applied, and it is applied from one place so the tool cannot hold two
    /// opinions about the same law. The minimum radius comes from RM-40's own implementation of e + f = V^2/(127 R) --
    /// this file does not repeat that relation, it calls it -- and the transition length comes from Shortt's comfort
    /// criterion together with the superelevation runoff the same class already declares, because a transition shorter
    /// than the runoff would have the road still rotating after the curve has begun.
    ///
    /// Nothing here refuses. A radius below the standard is resolved, fitted, and REPORTED, exactly as RM-74's forbidden
    /// ground is priced and drawn: an author building a mountain hairpin for a game is not making a mistake, and a tool
    /// that blocks them is wrong. An author who did not notice they built one is making a mistake, and a tool that stays
    /// quiet is worse. </summary>
    public static class AlignmentResolver
    {
        /// <summary> Resolve a plan. The road class supplies its own hard minimum radius and the superelevation class
        /// the design relation; either may be null, in which case what remains is used and the legend says so.
        ///
        /// The staking itself is <see cref="ResolveToStandard"/>. This method is the ROAD entry to it: it works out the
        /// two governing numbers from the road's own classes and hands them over. Splitting it that way is what lets a
        /// railway be staked by this same code without a <see cref="RoadClass"/> anywhere near it -- rail's minimum
        /// radius and rail's transition length come from CANT, not from side friction, and a rail curve judged against
        /// the road relation is the cross-wiring <see cref="RailTrackClass"/> was written to prevent. </summary>
        public static AlignmentSolution Resolve(HorizontalAlignment plan, RoadClass cls, SuperelevationClass super)
        {
            SuperelevationClass design = super != null ? super : SuperelevationClass.Default();
            double speed = plan != null && plan.designSpeedKmh > 0f ? plan.designSpeedKmh : 0.0;
            double minimumRadius = MinimumRadiusMetres(speed, cls, design);

            AlignmentSolution solution = ResolveToStandard(plan, minimumRadius,
                delegate(double radius) { return DefaultTransitionLengthMetres(speed, radius, design); });
            if (plan == null || plan.PointCount < 2) { return solution; }

            solution.legend = LegendFor(plan, cls, solution.curves, solution, minimumRadius, speed, design,
                super != null);
            return solution;
        }


        /// <summary> Stake a plan against a standard given as NUMBERS: the tightest radius the design allows, and a
        /// rule saying how long a transition a given radius wants. No class of any kind reaches it, which is the
        /// point of it -- the plane geometry of tangents, transitions and arcs is the same whether the thing staked
        /// carries lorries or trains, while the STANDARD it is judged by is not, and only one of those two is worth
        /// writing twice.
        ///
        /// <paramref name="transitionLengthForRadius"/> is consulted only for a corner whose authored transition length
        /// is negative, i.e. one the author asked the tool to work out. It is a function rather than a number since the
        /// answer depends on the radius, and it is pure in its argument plus what the caller closed over, so a
        /// repeat call is bit-identical (C-06). Null is allowed and means "no transition unless the author stated one",
        /// which is the correct reading of a caller that has no comfort criterion to offer.
        ///
        /// The legend is left EMPTY here. A legend is written in the vocabulary of the thing being staked -- side
        /// friction and runoff for a road, cant and deficiency for a railway -- so it belongs to whichever caller knows
        /// which of those it is. </summary>
        public static AlignmentSolution ResolveToStandard(HorizontalAlignment plan, double minimumRadiusMetres,
            Func<double, double> transitionLengthForRadius)
        {
            AlignmentSolution solution = new AlignmentSolution();
            if (plan == null || plan.PointCount < 2)
            {
                solution.legend = "no alignment: a plan needs at least two points, because one point has no direction";
                return solution;
            }

            List<AlignmentCurve> curves = ResolveCurves(plan, minimumRadiusMetres, transitionLengthForRadius);
            FlagOverlaps(plan, curves);
            BuildElements(plan, curves, solution);
            solution.curves = curves;
            return solution;
        }

        /// <summary> The tightest radius this road may hold: the greater of what the class declares outright and what the
        /// design speed demands through the published relation. The GREATER, because both are minima and satisfying one
        /// does not excuse the other -- a class whose minimum is 30 m does not make 30 m safe at 90 km/h. </summary>
        public static double MinimumRadiusMetres(double designSpeedKmh, RoadClass cls, SuperelevationClass design)
        {
            double byClass = cls != null && cls.minCurveRadius > 0f ? cls.minCurveRadius : 0.0;
            if (design == null || designSpeedKmh <= 0.0) { return byClass; }

            double f = Superelevation.SideFrictionFactor(designSpeedKmh, design);
            double bySpeed = Superelevation.MinRadiusForDesignSpeedMetres(designSpeedKmh, design, f);
            if (double.IsInfinity(bySpeed) || double.IsNaN(bySpeed) || bySpeed <= 0.0) { return byClass; }
            return bySpeed > byClass ? bySpeed : byClass;
        }

        /// <summary> The transition length to use when the author did not state one: the longer of what comfort asks for
        /// (Shortt) and what the superelevation runoff needs. The longer of the two, because a transition shorter than
        /// the runoff would leave the carriageway still rotating after the curve had begun, which is the one thing the
        /// transition exists to prevent. </summary>
        public static double DefaultTransitionLengthMetres(double designSpeedKmh, double radiusMetres,
            SuperelevationClass design)
        {
            double comfort = AlignmentGeometry.ComfortTransitionLengthMetres(designSpeedKmh, radiusMetres,
                AlignmentGeometry.TransitionComfortRateMetresPerSecondCubed);
            double runoff = design != null && design.runoffLengthMetres > 0f ? design.runoffLengthMetres : 0.0;
            return comfort > runoff ? comfort : runoff;
        }

        private static bool LegDirection(AlignmentPoint from, AlignmentPoint to, out double dirX, out double dirZ)
        {
            dirX = 0.0;
            dirZ = 0.0;
            if (from == null || to == null) { return false; }
            double vx = (double)to.xMetres - from.xMetres;
            double vz = (double)to.zMetres - from.zMetres;
            double length = Math.Sqrt(vx * vx + vz * vz);
            if (length <= 1e-6) { return false; }
            dirX = vx / length;
            dirZ = vz / length;
            return true;
        }

        private static double LegLengthMetres(AlignmentPoint from, AlignmentPoint to)
        {
            if (from == null || to == null) { return 0.0; }
            double vx = (double)to.xMetres - from.xMetres;
            double vz = (double)to.zMetres - from.zMetres;
            return Math.Sqrt(vx * vx + vz * vz);
        }

        /// <summary> One curve per interior corner, in order. The first and last points are ENDS, not corners: a road
        /// starts pointing somewhere, and there is no second straight at the start to intersect with. </summary>
        private static List<AlignmentCurve> ResolveCurves(HorizontalAlignment plan, double minimumRadius,
            Func<double, double> transitionLengthForRadius)
        {
            List<AlignmentCurve> curves = new List<AlignmentCurve>();
            for (int i = 1; i < plan.points.Count - 1; i++)
            {
                AlignmentCurve c = new AlignmentCurve();
                c.pointIndex = i;
                c.minimumRadiusMetres = minimumRadius;

                double inX;
                double inZ;
                double outX;
                double outZ;
                // Both legs are resolved unconditionally rather than through `&&`: short-circuiting would leave
                // `outX`/`outZ` unassigned on the false branch, which the compiler is right to reject even though the
                // guard below never reads them. LegDirection is pure arithmetic, so calling it either way costs
                // nothing and the meaning is unchanged.
                bool haveInLeg = LegDirection(plan.points[i - 1], plan.points[i], out inX, out inZ);
                bool haveOutLeg = LegDirection(plan.points[i], plan.points[i + 1], out outX, out outZ);
                bool haveLegs = haveInLeg && haveOutLeg;
                if (!haveLegs)
                {
                    c.negligible = true;
                    curves.Add(c);
                    continue;
                }

                double dot = inX * outX + inZ * outZ;
                if (dot > 1.0) { dot = 1.0; }
                if (dot < -1.0) { dot = -1.0; }
                c.deflectionRadians = Math.Acos(dot);
                c.turnSign = (inX * outZ - inZ * outX) >= 0.0 ? 1 : -1;

                double radius = plan.points[i].radiusMetres > 0f ? plan.points[i].radiusMetres : minimumRadius;
                if (c.deflectionRadians < AlignmentGeometry.NegligibleDeflectionRadians || radius <= 0.0)
                {
                    c.negligible = true;
                    c.turnSign = 0;
                    curves.Add(c);
                    continue;
                }

                c.radiusMetres = radius;
                c.belowMinimumRadius = radius < minimumRadius - 1e-9;

                double ls = plan.points[i].transitionLengthMetres >= 0f
                    ? plan.points[i].transitionLengthMetres
                    : (transitionLengthForRadius != null ? transitionLengthForRadius(radius) : 0.0);

                // Two transitions cannot turn through more than the corner does. The longest pair that fits leaves no
                // arc at all -- a real curve form, not a failure -- so the transition is cut to that and said so.
                double longest = radius * c.deflectionRadians;
                if (ls > longest)
                {
                    ls = longest;
                    c.transitionShortened = true;
                }
                c.transitionLengthMetres = ls;

                double arcAngle = c.deflectionRadians
                    - 2.0 * AlignmentGeometry.TransitionAngleRadians(ls, radius);
                if (arcAngle < 0.0) { arcAngle = 0.0; }
                c.arcLengthMetres = radius * arcAngle;
                c.tangentLengthMetres = AlignmentGeometry.TotalTangentLengthMetres(radius, c.deflectionRadians, ls);
                c.totalLengthMetres = 2.0 * ls + c.arcLengthMetres;
                curves.Add(c);
            }
            return curves;
        }

        /// <summary> Mark curves that reach further back than the straight leading to them is long. Two adjacent curves
        /// share a straight, so each may take only part of it; when their tangent distances together exceed it, the plan
        /// asks for a road that turns into its own previous bend. Reported on BOTH curves, because either radius could
        /// be the one the author wants to change. </summary>
        private static void FlagOverlaps(HorizontalAlignment plan, List<AlignmentCurve> curves)
        {
            for (int k = 0; k < curves.Count; k++)
            {
                AlignmentCurve c = curves[k];
                if (c.negligible) { continue; }
                int i = c.pointIndex;

                double legBefore = LegLengthMetres(plan.points[i - 1], plan.points[i]);
                double legAfter = LegLengthMetres(plan.points[i], plan.points[i + 1]);
                double takenBefore = k > 0 ? curves[k - 1].tangentLengthMetres : 0.0;
                double takenAfter = k + 1 < curves.Count ? curves[k + 1].tangentLengthMetres : 0.0;

                if (c.tangentLengthMetres + takenBefore > legBefore + 1e-9
                    || c.tangentLengthMetres + takenAfter > legAfter + 1e-9)
                {
                    c.overlapsNeighbour = true;
                    curves[k] = c;
                }
            }
        }

        private static AlignmentElement MakeElement(AlignmentElementKind kind, double startStation, double length,
            double radius, double transitionLength, int turnSign, double anchorX, double anchorZ,
            double headingX, double headingZ)
        {
            AlignmentElement e = new AlignmentElement();
            e.kind = kind;
            e.startStationMetres = startStation;
            e.lengthMetres = length < 0.0 ? 0.0 : length;
            e.radiusMetres = radius;
            e.transitionLengthMetres = transitionLength;
            e.turnSign = turnSign;
            e.anchorXMetres = anchorX;
            e.anchorZMetres = anchorZ;
            e.headingX = headingX;
            e.headingZ = headingZ;
            return e;
        }

        /// <summary> Chain the pieces: straight, transition, arc, transition, straight, in station order.
        ///
        /// Each curve is anchored where its GEOMETRY puts it -- the tangent point a distance T back from the corner --
        /// and never where the previous piece happened to end. When two curves overlap, the straight between them is
        /// clamped to zero length rather than run backwards, and the second curve still begins at its own tangent point;
        /// the plan then has a visible discontinuity, which is the truth about an overlapping plan and is what the legend
        /// has already said. Sliding the curve to hide it would produce a road whose bend is not on the radius the
        /// inspector is quoting. </summary>
        private static void BuildElements(HorizontalAlignment plan, List<AlignmentCurve> curves,
            AlignmentSolution solution)
        {
            int last = plan.points.Count - 1;
            double cursorX = plan.points[0].xMetres;
            double cursorZ = plan.points[0].zMetres;
            double headX;
            double headZ;
            if (!LegDirection(plan.points[0], plan.points[1], out headX, out headZ)) { return; }

            double station = 0.0;
            for (int k = 0; k < curves.Count; k++)
            {
                AlignmentCurve c = curves[k];
                if (c.negligible || c.radiusMetres <= 0.0) { continue; }
                int i = c.pointIndex;

                double inX;
                double inZ;
                double outX;
                double outZ;
                if (!LegDirection(plan.points[i - 1], plan.points[i], out inX, out inZ)) { continue; }
                if (!LegDirection(plan.points[i], plan.points[i + 1], out outX, out outZ)) { continue; }

                double tsX = plan.points[i].xMetres - inX * c.tangentLengthMetres;
                double tsZ = plan.points[i].zMetres - inZ * c.tangentLengthMetres;
                double straight = (tsX - cursorX) * headX + (tsZ - cursorZ) * headZ;
                if (straight < 0.0) { straight = 0.0; }
                solution.elements.Add(MakeElement(AlignmentElementKind.Tangent, station, straight, 0.0, 0.0, 0,
                    cursorX, cursorZ, headX, headZ));
                station += straight;

                double ls = c.transitionLengthMetres;
                double scX = tsX;
                double scZ = tsZ;
                double scHeadX = inX;
                double scHeadZ = inZ;
                if (ls > 0.0)
                {
                    AlignmentElement entry = MakeElement(AlignmentElementKind.EntryTransition, station, ls,
                        c.radiusMetres, ls, c.turnSign, tsX, tsZ, inX, inZ);
                    solution.elements.Add(entry);
                    station += ls;
                    AlignmentGeometry.PointAt(entry, ls, out scX, out scZ);
                    AlignmentGeometry.HeadingAt(entry, ls, out scHeadX, out scHeadZ);
                }

                if (c.arcLengthMetres > 0.0)
                {
                    solution.elements.Add(MakeElement(AlignmentElementKind.Arc, station, c.arcLengthMetres,
                        c.radiusMetres, ls, c.turnSign, scX, scZ, scHeadX, scHeadZ));
                    station += c.arcLengthMetres;
                }

                double stX = plan.points[i].xMetres + outX * c.tangentLengthMetres;
                double stZ = plan.points[i].zMetres + outZ * c.tangentLengthMetres;
                if (ls > 0.0)
                {
                    solution.elements.Add(MakeElement(AlignmentElementKind.ExitTransition, station, ls,
                        c.radiusMetres, ls, c.turnSign, stX, stZ, outX, outZ));
                    station += ls;
                }

                cursorX = stX;
                cursorZ = stZ;
                headX = outX;
                headZ = outZ;
            }

            double tail = ((double)plan.points[last].xMetres - cursorX) * headX
                + ((double)plan.points[last].zMetres - cursorZ) * headZ;
            if (tail < 0.0) { tail = 0.0; }
            solution.elements.Add(MakeElement(AlignmentElementKind.Tangent, station, tail, 0.0, 0.0, 0,
                cursorX, cursorZ, headX, headZ));
            solution.lengthMetres = station + tail;
        }

        private static string Number(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        private static string Degrees(double radians)
        {
            return (radians * 180.0 / Math.PI).ToString("0.#", CultureInfo.InvariantCulture);
        }

        /// <summary> One curve in words, radius first. This is the sentence the node shows, and it quotes the RADIUS
        /// rather than a curvature or a quality score because the radius is the number a road engineer, a highway code
        /// and an author all argue in. Everything else on the line is what that radius implies. </summary>
        public static string CurveReadout(AlignmentCurve c)
        {
            if (c.negligible)
            {
                return "corner " + c.pointIndex.ToString(CultureInfo.InvariantCulture)
                    + ": barely turns, so no curve was cut -- the road runs through it straight";
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("corner ").Append(c.pointIndex.ToString(CultureInfo.InvariantCulture));
            sb.Append(": radius ").Append(Number(c.radiusMetres)).Append(" m turning ");
            sb.Append(c.turnSign > 0 ? "left" : "right").Append(" through ");
            sb.Append(Degrees(c.deflectionRadians)).Append(" degrees");
            sb.Append("\n  ").Append(Number(c.transitionLengthMetres)).Append(" m transition, ");
            sb.Append(Number(c.arcLengthMetres)).Append(" m arc, ");
            sb.Append(Number(c.transitionLengthMetres)).Append(" m transition -- ");
            sb.Append(Number(c.totalLengthMetres)).Append(" m of curve reaching ");
            sb.Append(Number(c.tangentLengthMetres)).Append(" m back from the corner");

            if (c.belowMinimumRadius)
            {
                sb.Append("\n  tighter than the ").Append(Number(c.minimumRadiusMetres))
                    .Append(" m this design speed demands: drivable, and signposted in reality");
            }
            if (c.transitionShortened)
            {
                sb.Append("\n  the transition was cut to fit the corner's ").Append(Degrees(c.deflectionRadians))
                    .Append(" degrees, leaving no constant-radius arc between the two halves");
            }
            if (c.overlapsNeighbour)
            {
                sb.Append("\n  this curve reaches further back than the straight before or after it is long, ")
                    .Append("so it overlaps its neighbour");
            }
            return sb.ToString();
        }

        /// <summary> The plan in words: what it is, what standard was applied, and every corner that argues with it.
        ///
        /// The middle part is what stops this being decoration. A curvature ribbon shows an author WHERE the road bends;
        /// only the legend can tell them that the tightest bend allowed at this speed is 194 m and that corner 3 is 40 m.
        /// </summary>
        public static string LegendFor(HorizontalAlignment plan, RoadClass cls, List<AlignmentCurve> curves,
            AlignmentSolution solution, double minimumRadius, double designSpeedKmh, SuperelevationClass design,
            bool designClassGiven)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Alignment for ");
            sb.Append(cls != null && !string.IsNullOrEmpty(cls.displayName) ? cls.displayName : "no road class");
            sb.Append("\n  ").Append(plan != null ? plan.SummaryLine() : "no plan");
            sb.Append("\n  ").Append(CurveCount(curves).ToString(CultureInfo.InvariantCulture))
                .Append(" curves over ").Append(Number(solution != null ? solution.lengthMetres : 0.0))
                .Append(" m of road");

            if (designSpeedKmh > 0.0 && minimumRadius > 0.0)
            {
                sb.Append("\n  at ").Append(Number(designSpeedKmh))
                    .Append(" km/h the tightest radius allowed is ").Append(Number(minimumRadius))
                    .Append(" m, from e + f = V^2 / (127 R)");
            }
            else
            {
                sb.Append("\n  no minimum radius could be worked out: ")
                    .Append(designSpeedKmh > 0.0 ? "the class declares none and the design set is empty"
                        : "no design speed was given");
            }

            sb.Append("\n  transitions from Shortt's criterion at C = ")
                .Append(Number(AlignmentGeometry.TransitionComfortRateMetresPerSecondCubed))
                .Append(" m/s^3");
            if (design != null && design.runoffLengthMetres > 0f)
            {
                sb.Append(", never shorter than the ").Append(Number(design.runoffLengthMetres))
                    .Append(" m superelevation runoff");
            }
            if (!designClassGiven)
            {
                sb.Append("\n  no superelevation class was given, so the shipped design set was used");
            }

            int flagged = 0;
            if (curves != null)
            {
                for (int k = 0; k < curves.Count; k++)
                {
                    AlignmentCurve c = curves[k];
                    if (!c.belowMinimumRadius && !c.transitionShortened && !c.overlapsNeighbour) { continue; }
                    flagged++;
                    sb.Append("\n  ").Append(CurveReadout(c).Replace("\n  ", "\n    "));
                }
            }
            if (flagged == 0)
            {
                sb.Append("\n  every curve holds the standard, and every one of them warns rather than blocks in any case");
            }
            return sb.ToString();
        }

        private static int CurveCount(List<AlignmentCurve> curves)
        {
            if (curves == null) { return 0; }
            int count = 0;
            for (int k = 0; k < curves.Count; k++)
            {
                if (!curves[k].negligible) { count++; }
            }
            return count;
        }

        /// <summary> The world point at a station, or false off the plan. </summary>
        public static bool PointAtStation(AlignmentSolution solution, double stationMetres,
            out double xMetres, out double zMetres)
        {
            xMetres = 0.0;
            zMetres = 0.0;
            if (solution == null) { return false; }
            double into;
            int k = solution.ElementAt(stationMetres, out into);
            if (k < 0) { return false; }
            AlignmentGeometry.PointAt(solution.elements[k], into, out xMetres, out zMetres);
            return true;
        }

        /// <summary> The heading at a station as a unit XZ direction, or false off the plan. </summary>
        public static bool HeadingAtStation(AlignmentSolution solution, double stationMetres,
            out double headingX, out double headingZ)
        {
            headingX = 0.0;
            headingZ = 0.0;
            if (solution == null) { return false; }
            double into;
            int k = solution.ElementAt(stationMetres, out into);
            if (k < 0) { return false; }
            AlignmentGeometry.HeadingAt(solution.elements[k], into, out headingX, out headingZ);
            return true;
        }

        /// <summary> Signed curvature in 1/m at a station: positive left, negative right, zero on a straight and zero
        /// off the plan. Zero off the plan rather than a refusal because a ribbon sampling past the end should draw
        /// nothing, not a spike. </summary>
        public static double CurvatureAtStation(AlignmentSolution solution, double stationMetres)
        {
            if (solution == null) { return 0.0; }
            double into;
            int k = solution.ElementAt(stationMetres, out into);
            if (k < 0) { return 0.0; }
            return AlignmentGeometry.CurvatureAt(solution.elements[k], into);
        }

        /// <summary> The radius in force at a station, in metres, or positive infinity on a straight. Infinity rather
        /// than zero, because a straight IS a curve of infinite radius and reporting zero would make it read as the
        /// tightest thing on the road. </summary>
        public static double RadiusAtStation(AlignmentSolution solution, double stationMetres)
        {
            double curvature = CurvatureAtStation(solution, stationMetres);
            if (curvature < 0.0) { curvature = -curvature; }
            return curvature <= 1e-12 ? double.PositiveInfinity : 1.0 / curvature;
        }
    }
}
