// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-6, third bullet. HOW a railway reaches one settlement: as a line that already passes through
    /// it, as a branch built to reach it, or not at all.
    ///
    /// Three cases and not two, because "has rail" and "is on the main line" are different facts and the tool has to be
    /// able to say the second one is false while the first is true. A settlement the author's line runs straight through
    /// gets a through station; a settlement the line misses gets a branch and a terminus; a settlement nothing can reach
    /// within its own fitted track budget gets neither, and says so rather than being handed a branch from the horizon.
    /// </summary>
    public enum SettlementRailApproach
    {
        /// <summary> Nothing reaches this settlement. Either the fit says it is unserved, or the nearest main line is
        /// further away than the settlement's own fitted track length can justify. </summary>
        Unserved = 0,

        /// <summary> The hand-placed main line crosses the settlement boundary and runs through it. The city's rail is
        /// the author's rail continued -- there is nothing to build but the stopping places on it. </summary>
        ThroughLine = 1,

        /// <summary> No main line passes, so the settlement is reached by a branch that leaves the author's line at a
        /// junction and ends at a terminus inside the settlement. </summary>
        Terminus = 2
    }


    /// <summary> B-27 / D-6. Where the railway meets one settlement, and every figure the answer was reached from.
    ///
    /// It carries a REFUSAL rather than a bool, on the doctrine every other computing type in this folder follows: a
    /// resolver that cannot answer says why in words the inspector can print. "This settlement is off the network" and
    /// "you handed me no main line to test against" are the same answer through a bool and completely different things
    /// -- one is a village, the other is an unwired port. </summary>
    [Serializable]
    public struct SettlementRailEntryReport
    {
        /// <summary> Whether the resolver could answer at all. False means <see cref="refusal"/> is set and every figure
        /// below is meaningless -- NOT that the settlement is unserved, which is
        /// <see cref="SettlementRailApproach.Unserved"/> on a resolved report. </summary>
        public bool resolved;

        /// <summary> Why it could not answer, empty when it could. </summary>
        public string refusal;

        /// <summary> The decision: through line, terminus, or nothing. </summary>
        public SettlementRailApproach approach;

        /// <summary> Where the main line first crosses INTO the settlement, and where it last leaves. Both are on the
        /// boundary of the footprint, both carry the interpolated top-of-rail height of the line at that point, and both
        /// are meaningless unless <see cref="approach"/> is <see cref="SettlementRailApproach.ThroughLine"/>.
        ///
        /// First-in and last-out rather than a list of every crossing, because a line that clips a corner of the
        /// footprint twice has entered the settlement once as far as a station is concerned. The count is kept
        /// separately for the author who needs to know the line wanders. </summary>
        public Vector3 entryPoint;
        public Vector3 exitPoint;

        /// <summary> How much track lies INSIDE the footprint, in metres, summed over every stretch of it. This is the
        /// run D-8's stations are spaced along, and it is measured in three dimensions -- along the rail as it climbs --
        /// rather than on the plan, because a station spacing measured on a map is short on a graded line. </summary>
        public float insideLengthMetres;

        /// <summary> How many times the line crossed the boundary. Two for the ordinary case of a line running straight
        /// through. More means the line wanders in and out, which is not an error but is worth showing: it is usually a
        /// footprint drawn around a bend the line takes outside the settlement. </summary>
        public int boundaryCrossings;

        /// <summary> Where the branch leaves the main line, when one is built, and which sample of the main line it left
        /// from. The junction is ON the author's line, which is what makes the branch a continuation of it rather than a
        /// second network that happens to be nearby. </summary>
        public Vector3 junctionPoint;
        public int junctionSampleIndex;

        /// <summary> Where the branch ends, when one is built: on the edge of the settlement's core, approached from the
        /// junction. See <see cref="SettlementRailEntry.CoreAreaFraction"/> for why a terminus stops at the core's edge
        /// rather than running to the middle. </summary>
        public Vector3 terminusPoint;

        /// <summary> How long the branch is on the plan, in metres, from junction to terminus. Straight-line: the
        /// planner that turns this into track will lengthen it slightly as it rounds the turnout, and reporting the
        /// planner's figure here would mean reporting a number this resolver did not produce. </summary>
        public float spurLengthMetres;

        /// <summary> How far the settlement's centre is from the nearest point on the main line, in plan metres. Kept
        /// even when a through line was found -- it is zero-ish then -- because it is the figure a branch is judged
        /// against and an author reading a refusal needs to see it beside the budget. </summary>
        public float centreToLineMetres;

        /// <summary> The longest branch this settlement could justify, in metres, and the number
        /// <see cref="spurLengthMetres"/> was tested against. See
        /// <see cref="SettlementRailEntry.Resolve(Rect, IList{RailAlignmentSample}, SettlementRailReport, double)"/> for
        /// why the budget is the settlement's own fitted track length rather than a typed constant. </summary>
        public float spurBudgetMetres;

        /// <summary> One line, in the register's voice: what reaches this settlement, and the figures behind it.
        /// </summary>
        public string Describe()
        {
            if (!resolved)
            {
                return "rail entry: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            if (approach == SettlementRailApproach.ThroughLine)
            {
                string wander = boundaryCrossings > 2
                    ? " The line crosses the boundary " + boundaryCrossings + " times, so it wanders in and out."
                    : string.Empty;
                return "rail entry: the main line runs through, " + Figure(insideLengthMetres, " m")
                       + " of it inside the settlement." + wander;
            }
            if (approach == SettlementRailApproach.Terminus)
            {
                return "rail entry: no main line passes, so a branch of " + Figure(spurLengthMetres, " m")
                       + " leaves the line " + Figure(centreToLineMetres, " m") + " away and ends at a terminus. The"
                       + " settlement's fitted track length allows " + Figure(spurBudgetMetres, " m") + ".";
            }
            return "rail entry: nothing reaches this settlement. The nearest main line is "
                   + Figure(centreToLineMetres, " m") + " away and its fitted track length allows a branch of "
                   + Figure(spurBudgetMetres, " m") + ", so a line to it would be a line to nowhere.";
        }

        private static string Figure(float value, string unit)
        {
            return value.ToString(Mathf.Abs(value) < 10f ? "0.0" : "0", CultureInfo.InvariantCulture) + unit;
        }
    }


    /// <summary> B-27 / D-6, third bullet. Answers where a railway ENTERS a settlement: the boundary crossing of the
    /// hand-placed main line, and the terminus where no main line passes.
    ///
    /// A static resolver, not a component: it computes and refuses, it never mutates a scene, never touches an asset and
    /// never draws. What it produces is geometry an author can read as numbers, plus -- through
    /// <see cref="BuildSpurAlignment"/> -- a <see cref="HorizontalAlignment"/> that D-5's own planner and builder turn
    /// into track. That last part is the whole design: a branch to a terminus is NOT built by a second track builder
    /// written here. It is authored as an alignment and handed to <c>RailAlignmentPlanner</c>, so it is banked, graded,
    /// clearance-checked and refused by exactly the same code that judges the author's own line. A settlement's branch
    /// that could be built on a gradient the main line would have been refused for is not a feature.
    ///
    /// WHY THE FOOTPRINT IS A RECT AND THE LINE IS A POLYLINE. The settlement footprint is
    /// <c>CityAuthoring.TryRegionWorldXZ</c>'s world rect -- the thing an author drags in the Scene view -- and the main
    /// line is the swept samples of a planned <c>RailAlignment</c>. So the question "where does it enter" is a segment
    /// clipped against a rectangle, once per sample pair, which is Liang-Barsky: it is exact, it has no tolerance to
    /// tune, and it answers with the parameters along the segment rather than with a hit point that would have to be
    /// projected back onto the line to be useful.
    ///
    /// WHY THERE IS NO SEED. Nothing here is drawn. Whether the line passes through the footprint is a fact about two
    /// pieces of geometry the author placed, and where a branch would leave it is the nearest point on that line --
    /// also a fact. C-06 holds trivially: same footprint, same line, same answer, every bake. The one random thing in
    /// D-6, whether the settlement is served at all, was drawn upstream by
    /// <see cref="SettlementRailPresence"/> and arrives here as a decided report. </summary>
    public static class SettlementRailEntry
    {
        #region "The two figures the geometry rests on"
        /// <summary> How much of a settlement's area counts as its CORE, which is where a terminus stops.
        ///
        /// A terminus does not run to the middle of a town, and that is not a stylistic choice -- it is what the ground
        /// costs. The great termini were built at the edge of the built-up area of their day because the land inside it
        /// was already occupied and the railway companies were priced, or legislated, out of the centre: London's ring
        /// of them -- Euston, King's Cross, Paddington, Waterloo -- sits on the Victorian core's edge and not in it, and
        /// the same pattern holds wherever a line arrived after the town did. So the branch stops when it reaches the
        /// core rather than driving through it.
        ///
        /// A HALF is used because it is arithmetic rather than a preference: the core is the central rectangle holding
        /// half the footprint's AREA, so the linear scale is the square root of a half and the number below is the only
        /// one that has to be agreed. Taking half by area rather than by width is what keeps the rule the same shape on
        /// a long thin settlement as on a square one. </summary>
        public const double CoreAreaFraction = 0.5;

        /// <summary> The turnout a branch leaves the main line on, as the 1:n of its crossing angle. 1:9 is the ordinary
        /// main-line turnout for a diverging route at moderate speed -- shallower than the 1:6 of a yard connection,
        /// steeper than the 1:12 and above reserved for high-speed diverging junctions.
        ///
        /// It is here because a branch that left the main line at whatever angle the terminus happened to lie in would
        /// be a kink in the author's track, not a junction. See <see cref="TurnoutLeadMetres"/> for how the ratio
        /// becomes a length. </summary>
        public const double DefaultTurnoutRatio = 9.0;

        /// <summary> The distance between the centre lines of two adjacent tracks on plain line, in metres. 4.0 m is the
        /// ordinary figure on a modern double-track railway -- wide enough for the kinematic envelopes of two passing
        /// vehicles plus the space a person needs to stand clear. It is the sideways distance a diverging track has to
        /// gain before it is a separate track rather than a track fouling the one it left, which is what makes it the
        /// right multiplicand for the turnout ratio. </summary>
        public const double DefaultTrackCentreSpacingMetres = 4.0;

        /// <summary> How far a branch runs alongside the main line before it may turn away: the length a 1:n turnout
        /// needs to move a track sideways by one track spacing, which is n times that spacing.
        ///
        /// 1:9 over 4.0 m centres is 36 m, and that is the whole derivation -- a 1:n crossing gains one unit sideways
        /// for every n along, so gaining a track spacing takes n spacings of length. The alternative was to let the
        /// branch turn at the junction itself, which puts the full deflection of the branch into a single corner of the
        /// author's main line and produces a curve no train could take at line speed. </summary>
        public static double TurnoutLeadMetres(double turnoutRatio, double trackCentreSpacingMetres)
        {
            double ratio = turnoutRatio > 0.0 ? turnoutRatio : DefaultTurnoutRatio;
            double spacing = trackCentreSpacingMetres > 0.0
                ? trackCentreSpacingMetres : DefaultTrackCentreSpacingMetres;
            return ratio * spacing;
        }
        #endregion


        #region "Resolving"
        /// <summary> Answers where the railway enters this settlement, given the settlement's own rail report so the
        /// branch can be judged against the track length that report fitted.
        ///
        /// THE BUDGET IS THE FIT'S OWN NUMBER, which is the point of this overload. "How long a branch is too long" is
        /// exactly the kind of figure that would otherwise be typed into a constant and defended afterwards, and D-6's
        /// second bullet already fitted the answer: <see cref="SettlementRailReport.trackLengthMetres"/> is how much
        /// track this settlement's size implies. A branch longer than the whole of that is a line to nowhere BY THE
        /// SETTLEMENT'S OWN FITTED FIGURES -- it would be a settlement whose approach is longer than its railway -- so
        /// that is where the line is drawn, and no new constant is invented to draw it.
        ///
        /// An UNSERVED settlement is answered <see cref="SettlementRailApproach.Unserved"/> without testing any
        /// geometry. The presence curve already decided it has no railway; finding that the author's main line happens
        /// to clip its corner does not give it one, and building a station there because of an accident of where a line
        /// was drawn is how a village ends up with an intercity terminus. </summary>
        public static SettlementRailEntryReport Resolve(Rect footprintWorldXZ, IList<RailAlignmentSample> mainLine,
                                                        SettlementRailReport rail, double turnoutRatio)
        {
            SettlementRailEntryReport report = new SettlementRailEntryReport();

            if (!rail.resolved)
            {
                report.refusal = "the settlement's rail presence was never resolved, so there is no railway to bring in"
                                 + " -- " + (string.IsNullOrEmpty(rail.refusal) ? "no reason given" : rail.refusal);
                return report;
            }
            if (!rail.served)
            {
                report.resolved = true;
                report.approach = SettlementRailApproach.Unserved;
                report.spurBudgetMetres = 0f;
                report.centreToLineMetres = PlanDistanceToLine(footprintWorldXZ, mainLine);
                return report;
            }
            return Resolve(footprintWorldXZ, mainLine, rail.trackLengthMetres, turnoutRatio);
        }

        /// <summary> Answers where the railway enters this settlement, against an explicit branch budget in metres.
        ///
        /// The overload above is the one a bake uses; this one exists for a caller that has a footprint and a line but
        /// no fitted report -- a gate, a test, an author probing a line by hand -- and for the case where the budget
        /// should come from somewhere other than the fit. A budget of zero or less means no branch may be built at all,
        /// which is a legitimate answer and not a refusal: it says this settlement takes the line if the line comes to
        /// it, and gets nothing if it does not. </summary>
        public static SettlementRailEntryReport Resolve(Rect footprintWorldXZ, IList<RailAlignmentSample> mainLine,
                                                        double spurBudgetMetres, double turnoutRatio)
        {
            SettlementRailEntryReport report = new SettlementRailEntryReport();
            report.spurBudgetMetres = (float)Math.Max(0.0, spurBudgetMetres);

            if (footprintWorldXZ.width <= 0f || footprintWorldXZ.height <= 0f)
            {
                report.refusal = "the settlement footprint measures " + footprintWorldXZ.width.ToString("0.##")
                                 + " m by " + footprintWorldXZ.height.ToString("0.##") + " m, so it encloses no ground"
                                 + " and there is no boundary for a railway to cross";
                return report;
            }
            if (mainLine == null || mainLine.Count < 2)
            {
                report.refusal = "there is no planned main line to test against: a railway entering a settlement is the"
                                 + " author's own line crossing its boundary, and with fewer than two samples there is"
                                 + " no line to cross anything";
                return report;
            }

            // Every stretch of the line that lies inside the footprint, in one pass over the sample pairs.
            report.resolved = true;
            bool insideAtLastSample = Contains(footprintWorldXZ, mainLine[0].position);
            bool haveEntry = false;

            for (int i = 0; i + 1 < mainLine.Count; i++)
            {
                Vector3 a = mainLine[i].position;
                Vector3 b = mainLine[i + 1].position;

                double t0, t1;
                if (!ClipSegmentToRect(a, b, footprintWorldXZ, out t0, out t1))
                {
                    insideAtLastSample = false;
                    continue;
                }

                Vector3 from = Vector3.Lerp(a, b, (float)t0);
                Vector3 to = Vector3.Lerp(a, b, (float)t1);
                report.insideLengthMetres += Vector3.Distance(from, to);

                // A crossing is a change of state, so it is counted where the clipped portion starts short of the
                // segment's own start -- or ends short of its end -- rather than once per segment that touches the
                // rectangle. A line running through four sample pairs inside the footprint has crossed twice, not eight
                // times.
                if (t0 > 0.0 || !insideAtLastSample)
                {
                    report.boundaryCrossings++;
                    if (!haveEntry) { report.entryPoint = from; haveEntry = true; }
                }
                if (t1 < 1.0)
                {
                    report.boundaryCrossings++;
                    report.exitPoint = to;
                    insideAtLastSample = false;
                }
                else
                {
                    report.exitPoint = to;
                    insideAtLastSample = true;
                }
            }

            if (haveEntry || report.insideLengthMetres > 0f)
            {
                report.approach = SettlementRailApproach.ThroughLine;
                if (!haveEntry) { report.entryPoint = mainLine[0].position; }
                report.centreToLineMetres = 0f;
                return report;
            }

            // The line misses the settlement entirely, so the question becomes whether a branch to it is justified.
            return ResolveTerminus(footprintWorldXZ, mainLine, report, turnoutRatio);
        }

        /// <summary> The no-main-line-passes case: a branch from the nearest point on the author's line to a terminus on
        /// the edge of the settlement's core.
        ///
        /// THE JUNCTION IS THE NEAREST POINT ON THE LINE, which is the shortest branch that reaches this settlement at
        /// all, and is what a railway company building a branch would have surveyed for: the connection was made where
        /// it was cheapest to make, then the branch ran to the town. Taking the nearest point to the settlement's CENTRE
        /// rather than to its boundary is deliberate -- it is stable while an author drags the footprint's edges, and it
        /// is the same point the terminus is aimed from, so the branch and the point it starts at cannot disagree.
        ///
        /// THE TERMINUS IS WHERE THAT BRANCH REACHES THE CORE, not the centre: see <see cref="CoreAreaFraction"/>.
        /// </summary>
        private static SettlementRailEntryReport ResolveTerminus(Rect footprintWorldXZ,
            IList<RailAlignmentSample> mainLine, SettlementRailEntryReport report, double turnoutRatio)
        {
            Vector3 centre = new Vector3(footprintWorldXZ.center.x, 0f, footprintWorldXZ.center.y);

            int bestIndex;
            Vector3 nearest;
            float planDistance;
            NearestOnLine(mainLine, centre, out nearest, out bestIndex, out planDistance);

            report.junctionPoint = nearest;
            report.junctionSampleIndex = bestIndex;
            report.centreToLineMetres = planDistance;

            // The core: the central rectangle holding CoreAreaFraction of the footprint's area, so its linear scale is
            // the square root of that fraction.
            float coreScale = (float)Math.Sqrt(CoreAreaFraction);
            Rect core = new Rect(0f, 0f, footprintWorldXZ.width * coreScale, footprintWorldXZ.height * coreScale);
            core.center = footprintWorldXZ.center;

            // Where the branch, aimed from the junction at the centre, first reaches the core. The junction is outside
            // the footprint here -- this method is only reached when the line missed it -- so the entry parameter is the
            // near side of the core and never the far one.
            Vector3 aim = new Vector3(centre.x, nearest.y, centre.z);
            double t0, t1;
            Vector3 terminus = aim;
            if (ClipSegmentToRect(nearest, aim, core, out t0, out t1))
            {
                terminus = Vector3.Lerp(nearest, aim, (float)t0);
            }
            report.terminusPoint = terminus;

            float spur = Vector3.Distance(new Vector3(nearest.x, 0f, nearest.z),
                                          new Vector3(terminus.x, 0f, terminus.z));
            report.spurLengthMetres = spur;
            report.approach = spur > 0f && spur <= report.spurBudgetMetres
                ? SettlementRailApproach.Terminus : SettlementRailApproach.Unserved;
            return report;
        }
        #endregion


        #region "The branch, as an alignment D-5 can plan"
        /// <summary> The branch as a <see cref="HorizontalAlignment"/>: three points, junction to terminus, ready for
        /// <c>RailAlignmentPlanner.Plan</c>.
        ///
        /// THREE POINTS AND NOT TWO. A two-point alignment is a straight line from the junction to the terminus, which
        /// puts the whole angle between the main line and the branch into the junction itself -- a kink in the author's
        /// track that no turnout could be built as. So the second point is set one turnout lead along the main line's
        /// own direction: the branch leaves parallel to the line it is leaving, and the deflection lands on the middle
        /// point where the planner rounds it into a curve with a radius, a transition and a cant like any other. That is
        /// the difference between a junction and a corner.
        ///
        /// Returns null when the report is not a terminus, which is the honest answer rather than an empty alignment: a
        /// through line has no branch to build, and an unserved settlement has no branch it is allowed. </summary>
        public static HorizontalAlignment BuildSpurAlignment(SettlementRailEntryReport report,
            IList<RailAlignmentSample> mainLine, double turnoutRatio, double trackCentreSpacingMetres,
            float designSpeedKmh)
        {
            if (!report.resolved || report.approach != SettlementRailApproach.Terminus) { return null; }
            if (mainLine == null || mainLine.Count < 2) { return null; }

            int index = report.junctionSampleIndex;
            if (index < 0) { index = 0; }
            if (index >= mainLine.Count) { index = mainLine.Count - 1; }

            // The main line's direction at the junction, flattened: the branch leaves along the line on the plan, and
            // the grade it then takes is the vertical designer's answer, not this one's.
            Vector3 tangent = mainLine[index].tangent;
            Vector3 along = new Vector3(tangent.x, 0f, tangent.z);
            if (along.sqrMagnitude < 1e-8f)
            {
                int other = index + 1 < mainLine.Count ? index + 1 : index - 1;
                Vector3 span = mainLine[other].position - mainLine[index].position;
                along = new Vector3(span.x, 0f, span.z);
            }
            if (along.sqrMagnitude < 1e-8f) { return null; }
            along = along.normalized;

            // The branch leaves in whichever of the two directions along the line points towards the terminus, so a
            // junction on a line running the other way does not send the branch away from the settlement it serves.
            Vector3 toTerminus = new Vector3(report.terminusPoint.x - report.junctionPoint.x, 0f,
                                             report.terminusPoint.z - report.junctionPoint.z);
            if (Vector3.Dot(along, toTerminus) < 0f) { along = -along; }

            double lead = TurnoutLeadMetres(turnoutRatio, trackCentreSpacingMetres);
            Vector3 diverge = report.junctionPoint + along * (float)lead;

            HorizontalAlignment spur = new HorizontalAlignment();
            spur.designSpeedKmh = designSpeedKmh > 0f ? designSpeedKmh : 50f;
            spur.points.Add(new AlignmentPoint(report.junctionPoint.x, report.junctionPoint.z));
            spur.points.Add(new AlignmentPoint(diverge.x, diverge.z));
            spur.points.Add(new AlignmentPoint(report.terminusPoint.x, report.terminusPoint.z));
            return spur;
        }
        #endregion


        #region "Geometry, pure"
        /// <summary> Whether a world point is inside the footprint on the plan. The rect's y is world Z, which is the
        /// convention <c>CityAuthoring.TryRegionWorldXZ</c> writes and every reader of it has to share. </summary>
        public static bool Contains(Rect footprintWorldXZ, Vector3 world)
        {
            return world.x >= footprintWorldXZ.xMin && world.x <= footprintWorldXZ.xMax
                   && world.z >= footprintWorldXZ.yMin && world.z <= footprintWorldXZ.yMax;
        }

        /// <summary> Clip one segment to one rectangle on the plan, Liang-Barsky, and hand back the two parameters along
        /// the segment that bound the part inside. False when none of it is inside.
        ///
        /// Parameters rather than points, because the caller needs to know WHERE ALONG the segment the crossing fell --
        /// to interpolate the rail height at it, and to tell a segment that starts outside from one that starts inside.
        /// A clipper that returned two points would have thrown that away and the caller would be projecting them back
        /// onto the segment to recover it.
        ///
        /// Exact and tolerance-free: the only comparison is against zero, and the degenerate case -- a segment parallel
        /// to an edge -- is decided by which side of that edge it lies on rather than by a distance test that would need
        /// an epsilon nobody could pick. </summary>
        public static bool ClipSegmentToRect(Vector3 a, Vector3 b, Rect rect, out double t0, out double t1)
        {
            t0 = 0.0;
            t1 = 1.0;
            double dx = b.x - a.x;
            double dz = b.z - a.z;

            if (!ClipEdge(-dx, a.x - rect.xMin, ref t0, ref t1)) { return false; }
            if (!ClipEdge(dx, rect.xMax - a.x, ref t0, ref t1)) { return false; }
            if (!ClipEdge(-dz, a.z - rect.yMin, ref t0, ref t1)) { return false; }
            if (!ClipEdge(dz, rect.yMax - a.z, ref t0, ref t1)) { return false; }
            return t1 >= t0;
        }

        /// <summary> One of Liang-Barsky's four half-plane tests. <paramref name="p"/> is the segment's rate of approach
        /// to the edge and <paramref name="q"/> its starting clearance from it: p zero means the segment is parallel to
        /// that edge, and then the whole segment is in or out according to the sign of q alone. </summary>
        private static bool ClipEdge(double p, double q, ref double t0, ref double t1)
        {
            if (p == 0.0) { return q >= 0.0; }
            double t = q / p;
            if (p < 0.0)
            {
                if (t > t1) { return false; }
                if (t > t0) { t0 = t; }
                return true;
            }
            if (t < t0) { return false; }
            if (t < t1) { t1 = t; }
            return true;
        }

        /// <summary> The point on the sampled line closest to a world point on the plan, the index of the sample the
        /// segment it fell on starts at, and the plan distance to it.
        ///
        /// Measured on the PLAN and not in three dimensions, because "how far is this town from the railway" is a
        /// question about the map: a line 200 m away and 40 m higher is 200 m away, and taking the height difference
        /// into the distance would make a branch up a valley side read as longer than the ground it covers. The height
        /// is still carried on the returned point, because the branch has to start at the rail head. </summary>
        public static void NearestOnLine(IList<RailAlignmentSample> line, Vector3 world, out Vector3 nearest,
            out int sampleIndex, out float planDistanceMetres)
        {
            nearest = Vector3.zero;
            sampleIndex = 0;
            planDistanceMetres = float.MaxValue;
            if (line == null || line.Count == 0) { planDistanceMetres = 0f; return; }
            if (line.Count == 1)
            {
                nearest = line[0].position;
                planDistanceMetres = PlanDistance(nearest, world);
                return;
            }

            for (int i = 0; i + 1 < line.Count; i++)
            {
                Vector3 a = line[i].position;
                Vector3 b = line[i + 1].position;
                double dx = b.x - a.x;
                double dz = b.z - a.z;
                double lengthSquared = dx * dx + dz * dz;

                double t = 0.0;
                if (lengthSquared > 0.0)
                {
                    t = ((world.x - a.x) * dx + (world.z - a.z) * dz) / lengthSquared;
                    if (t < 0.0) { t = 0.0; }
                    if (t > 1.0) { t = 1.0; }
                }

                Vector3 candidate = Vector3.Lerp(a, b, (float)t);
                float distance = PlanDistance(candidate, world);
                if (distance >= planDistanceMetres) { continue; }
                planDistanceMetres = distance;
                nearest = candidate;
                sampleIndex = i;
            }
        }

        /// <summary> The plan distance from a settlement's centre to the nearest point on the line, or zero when there
        /// is no line to measure against. Used on the unserved path, where the geometry is not resolved but the figure
        /// is still worth reporting. </summary>
        private static float PlanDistanceToLine(Rect footprintWorldXZ, IList<RailAlignmentSample> mainLine)
        {
            if (mainLine == null || mainLine.Count == 0) { return 0f; }
            Vector3 centre = new Vector3(footprintWorldXZ.center.x, 0f, footprintWorldXZ.center.y);
            Vector3 nearest;
            int index;
            float distance;
            NearestOnLine(mainLine, centre, out nearest, out index, out distance);
            return distance;
        }

        /// <summary> Distance between two world points on the plan, ignoring height. </summary>
        private static float PlanDistance(Vector3 a, Vector3 b)
        {
            double dx = a.x - b.x;
            double dz = a.z - b.z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
        #endregion
    }
}
