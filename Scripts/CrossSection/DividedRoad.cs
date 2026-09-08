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


namespace RoadCityMaster.CrossSection
{
    /// <summary> One point across a road's cross-section at a station: how far it sits from the alignment
    /// (signed -- negative is the left side) and how high it sits ABOVE THE ALIGNMENT'S OWN elevation, both
    /// in metres. Heights are RELATIVE, which is what keeps this whole file free of a spline: the sweep adds
    /// the alignment's elevation and rotation, so nothing here needs a scene to be checked. </summary>
    public struct SectionPoint
    {
        /// <summary> Signed distance from the alignment in metres; negative is the left side. </summary>
        public float lateral;

        /// <summary> Height in metres relative to the alignment's elevation at the same station. </summary>
        public float height;

        public SectionPoint(float lateral, float height)
        {
            this.lateral = lateral;
            this.height = height;
        }
    }

    /// <summary> RM-59's authored description of a divided road: ONE alignment, two carriageways, each with
    /// its own lateral offset from that alignment and its own vertical profile.
    ///
    /// Both lateral quantities are RM-31 <see cref="VariableWidthProfile"/> curves rather than constants,
    /// because a median that widens for a turning lane, a carriageway that flares, and a pair that converge
    /// back to an undivided section are the same mechanism -- a width curve -- applied to the offset instead
    /// of to a profile edge. Using RM-31 also inherits its guarantees: a curve with flat or smooth tangents
    /// is continuous in its first derivative, so the carriageway edge has no crease at a control point, and a
    /// collapsed width degrades to a line rather than to an inverted face.
    ///
    /// The geometry this describes, left to right at any station: the left carriageway's outer edge, the left
    /// carriageway, its inner edge at -<c>medianHalfGap</c>, the median, the right carriageway's inner edge at
    /// +<c>medianHalfGap</c>, the right carriageway, its outer edge. Shoulders sit outside both outer edges and
    /// are carried here because RM-38's terraform footprint is the union of the whole assembly.
    ///
    /// AUTHORED, NOT GENERATED throughout -- an author draws one road and edits these curves. </summary>
    [Serializable]
    public struct DividedRoadProfile
    {
        /// <summary> Distance in metres from the alignment out to EACH carriageway's inner edge, as a function
        /// of station. This is the median's half-width, which is exactly what RM-31's profile means by a
        /// half-width, so the type is used with its own semantics rather than reinterpreted. Zero everywhere
        /// makes the road undivided. </summary>
        public VariableWidthProfile medianHalfGap;

        /// <summary> HALF the width of ONE carriageway in metres, as a function of station. A carriageway is
        /// symmetric about its own centreline -- which sits at medianHalfGap + this value -- so again this is
        /// RM-31's half-width with its documented meaning. </summary>
        public VariableWidthProfile carriagewayHalfWidth;

        /// <summary> The LEFT carriageway's own vertical profile: metres of height at its inner edge relative
        /// to the alignment's elevation, against station. Null reads as zero. This is the field that lets a
        /// divided road sit split-level on a side slope, which a single surface with a painted stripe cannot
        /// express at all. </summary>
        public AnimationCurve leftHeightOffset;

        /// <summary> The RIGHT carriageway's own vertical profile, same convention. </summary>
        public AnimationCurve rightHeightOffset;

        /// <summary> The LEFT carriageway's cross-slope as a decimal (0.02 = 2%), measured FROM ITS INNER EDGE
        /// OUTWARD and positive when the surface falls away from the median. That reference point is the whole
        /// reason the undivided case falls out of this one: with a zero median gap and equal cross-slopes, two
        /// carriageways each falling away from a shared inner edge ARE a crowned road. RM-40 overwrites this
        /// per carriageway on a curve, because the two sit at different radii and so need different banking.
        /// </summary>
        public float leftCrossSlope;

        /// <summary> The RIGHT carriageway's cross-slope, same convention. </summary>
        public float rightCrossSlope;

        /// <summary> Shoulder width in metres outside EACH outer edge. Not part of the emitted surface here;
        /// carried because RM-38's flattened band and blend must consume the whole footprint, and a divided
        /// road that reports one carriageway's width blends into the median gap and fills it with spoil.
        /// </summary>
        public float shoulderWidthMetres;

        /// <summary> How many intervals the sweep cuts across ONE carriageway. The undivided reduction gives
        /// twice this many across the whole road, which is why the reference section is asked for 2n. </summary>
        public int pointsPerCarriageway;

        /// <summary> Two points closer together than this, in metres, in BOTH lateral offset and height, are
        /// the same vertex and are emitted once. This is what makes the zero-gap case a true reduction rather
        /// than a road with a doubled seam down the middle. Points that share an offset but NOT a height are
        /// kept apart deliberately -- that is a split-level pair meeting at a retaining face, not a seam.
        /// </summary>
        public float coalesceTolerance;
    }

    /// <summary> The cross-section at one station: the ordered points from the far left to the far right, plus
    /// the widths RM-38 and the median content (RM-60) read off it. Ordered so a sweep can stitch consecutive
    /// stations into a surface without sorting anything. </summary>
    public struct DividedSection
    {
        /// <summary> Left to right across the road. Null only when <see cref="refused"/>. </summary>
        public SectionPoint[] points;

        /// <summary> Points contributed by the left carriageway, then the median, then the right carriageway.
        /// A merged shared vertex counts once, against whichever part reached it first, so the three always
        /// sum to <c>points.Length</c>. </summary>
        public int leftCount;
        public int medianCount;
        public int rightCount;

        /// <summary> Index of each carriageway's INNER edge -- the two vertices the median is stretched between,
        /// which is what RM-60's trough, barrier and planting all attach to. When the median has closed the two
        /// carriageways share one vertex and both indices point at it, so a consumer never has to know whether a
        /// merge happened to find the edge it wants. </summary>
        public int leftInnerEdgeIndex;
        public int rightInnerEdgeIndex;

        /// <summary> Index of the median floor vertex at the alignment, or -1 where there is no median. </summary>
        public int medianFloorIndex;

        /// <summary> Outer edge to outer edge in metres: both carriageways plus the median. </summary>
        public float totalWidthMetres;

        /// <summary> The same plus both shoulders -- the width RM-38 must flatten and blend from. </summary>
        public float footprintWidthMetres;

        /// <summary> How many coincident vertices were merged. Two on a zero-gap section (the shared inner
        /// edge) is the reduction; a nonzero count anywhere else is worth looking at. </summary>
        public int coalescedPoints;

        /// <summary> True when no section could be built, with <see cref="refusal"/> saying why in words. A
        /// refusal is not an empty section: the sweep must skip the station and report, not emit a sliver.
        /// </summary>
        public bool refused;
        public string refusal;
    }

    /// <summary> The result of checking ONE carriageway's gradient against its road class's maximum. Held per
    /// carriageway because a divided road can be legal on one side and illegal on the other -- the two have
    /// independent vertical profiles, so one answer for the pair would hide the failing side. </summary>
    public struct GradientVerdict
    {
        /// <summary> Whether every sampled station is within the limit. </summary>
        public bool withinLimit;

        /// <summary> The steepest gradient found, SIGNED (rise/run), so a report can say which way it falls.
        /// </summary>
        public float worstGradient;

        /// <summary> The station in metres where <see cref="worstGradient"/> was measured. </summary>
        public float worstStation;

        /// <summary> The limit it was measured against, echoed so a message can quote both numbers. </summary>
        public float limit;
    }


    /// <summary> The result of checking an authored divided-to-undivided transition length. The tool refuses a
    /// transition that is too short and quotes the length that would work, rather than shortening the taper
    /// silently -- a shift crammed into too little distance is the crease this whole file exists to avoid.
    /// </summary>
    public struct TransitionVerdict
    {
        public bool acceptable;
        public float authoredLengthMetres;
        public float minimumLengthMetres;

        /// <summary> How far each carriageway must move sideways -- the median half-gap being closed. </summary>
        public float lateralShiftMetres;

        /// <summary> Empty when acceptable; otherwise the sentence to show the author. </summary>
        public string refusal;
    }

    /// <summary> RM-59's verifiable core: a divided road as ONE alignment carrying TWO carriageways, each with
    /// its own station-varying lateral offset and its own vertical profile.
    ///
    /// The claim this file has to earn is that divided mode is a GENERALISATION and not a second code path.
    /// <see cref="Section"/> is the only emitter; <see cref="PlainSection"/> is the textbook definition of an
    /// undivided crowned road, written as simply as it can be and used by nothing but the gate. Closing the
    /// median gap and levelling the two vertical profiles makes the general emitter agree with that
    /// definition, point for point and count for count -- which is the property RM-59 asks for, and it is an
    /// assertion about two independent computations rather than a tautology about one.
    ///
    /// What lives here: the cross-section at a station, the widths RM-38's footprint must consume, a gradient
    /// check PER CARRIAGEWAY against the road class maximum, the first-derivative check that keeps the offset
    /// curve from creasing the edge, and the divided-to-undivided transition with its length checked against a
    /// real taper formula rather than accepted on trust. What does not: the swept mesh, the alignment itself,
    /// and interchanges -- a divided road meeting a divided road is ramps, weaving and gore areas, logged as
    /// out of scope for v1 in § 10.9 rather than half-attempted. </summary>
    public static class DividedRoad
    {
        /// <summary> A profile that holds one half-width at every station. <c>AnimationCurve.Constant</c> is
        /// flat, so its first derivative is zero everywhere and the C1 check passes by construction. </summary>
        public static VariableWidthProfile ConstantHalfWidth(float halfWidthMetres)
        {
            VariableWidthProfile p;
            p.halfWidthOverDistance = AnimationCurve.Constant(0f, 1f, halfWidthMetres);
            p.minEmitHalfWidth = 0.001f;
            return p;
        }

        /// <summary> An AUTHORED rural divided highway, from the AASHTO Green Book's metric figures (C-15):
        /// 3.6 m lanes, two per carriageway so one carriageway is 7.2 m wide, a 9.0 m depressed median (inside
        /// the 3-18 m rural range), 3.0 m outside shoulders and a 2% normal crown. Verify against the adopted
        /// edition before shipping; these are stated so the tool runs on real numbers, not on guesses. </summary>
        public static DividedRoadProfile Default()
        {
            DividedRoadProfile p;
            p.medianHalfGap = ConstantHalfWidth(4.5f);
            p.carriagewayHalfWidth = ConstantHalfWidth(3.6f);
            p.leftHeightOffset = null;
            p.rightHeightOffset = null;
            p.leftCrossSlope = 0.02f;
            p.rightCrossSlope = 0.02f;
            p.shoulderWidthMetres = 3.0f;
            p.pointsPerCarriageway = 4;
            p.coalesceTolerance = 1e-4f;
            return p;
        }
        /// <summary> The same road, undivided: no median gap, one crowned surface of the given TOTAL width.
        /// Built through the same profile type on purpose -- there is no separate undivided description to
        /// drift from this one. Total width is four half-widths because each carriageway is half the road.
        /// </summary>
        public static DividedRoadProfile Undivided(float totalWidthMetres, float crossSlope,
            float shoulderWidthMetres, int pointsPerCarriageway)
        {
            DividedRoadProfile p = Default();
            p.medianHalfGap = ConstantHalfWidth(0f);
            p.carriagewayHalfWidth = ConstantHalfWidth(totalWidthMetres * 0.25f);
            p.leftCrossSlope = crossSlope;
            p.rightCrossSlope = crossSlope;
            p.shoulderWidthMetres = shoulderWidthMetres;
            p.pointsPerCarriageway = pointsPerCarriageway;
            return p;
        }

        /// <summary> The cross-section at one station, left to right. The left carriageway is walked from its
        /// outer edge inward, then the median, then the right carriageway outward, so consecutive stations
        /// stitch without sorting. Heights are taken from each carriageway's OWN vertical profile at its inner
        /// edge and fall away by that carriageway's own cross-slope, which is what allows the two to sit at
        /// different elevations with the median absorbing the difference. </summary>
        public static DividedSection Section(DividedRoadProfile p, float station)
        {
            DividedSection s = new DividedSection();
            s.leftInnerEdgeIndex = -1;
            s.rightInnerEdgeIndex = -1;
            s.medianFloorIndex = -1;
            int n = p.pointsPerCarriageway > 0 ? p.pointsPerCarriageway : 1;
            float tol = p.coalesceTolerance > 0f ? p.coalesceTolerance : 1e-4f;
            float halfWidth = p.carriagewayHalfWidth.HalfWidthAt(station);

            if (!p.carriagewayHalfWidth.EmitsAt(station))
            {
                s.refused = true;
                s.refusal = "at station " + Metres(station) + " a carriageway is only " + Metres(halfWidth)
                    + " of half-width, which has collapsed to a line, so no surface is emitted there. Widen the "
                    + "carriageway or end the road before this station.";
                return s;
            }

            float gap = p.medianHalfGap.EmitsAt(station) ? p.medianHalfGap.HalfWidthAt(station) : 0f;
            float carriageway = halfWidth * 2f;
            float step = carriageway / n;
            float leftHeight = p.leftHeightOffset == null ? 0f : p.leftHeightOffset.Evaluate(station);
            float rightHeight = p.rightHeightOffset == null ? 0f : p.rightHeightOffset.Evaluate(station);
            List<SectionPoint> points = new List<SectionPoint>((n + 1) * 2 + 1);
            int coalesced = 0;
            for (int i = 0; i <= n; i++)
            {
                float fromInnerEdge = (n - i) * step;
                Append(points, -(gap + carriageway) + i * step,
                    leftHeight - p.leftCrossSlope * fromInnerEdge, tol, ref coalesced);
            }
            s.leftCount = points.Count;
            s.leftInnerEdgeIndex = points.Count - 1;

            // A straight median floor already follows both inner edges, so it needs no interior point of its
            // own; the one at the alignment exists because that is where RM-60 sinks the drainage trough. With
            // no gap there is no median at all, and the two inner edges are one vertex.
            if (gap > tol)
            {
                Append(points, 0f, (leftHeight + rightHeight) * 0.5f, tol, ref coalesced);
                s.medianFloorIndex = points.Count - 1;
            }
            s.medianCount = points.Count - s.leftCount;

            int beforeRight = points.Count;
            for (int j = 0; j <= n; j++)
            {
                float fromInnerEdge = j * step;
                Append(points, gap + fromInnerEdge,
                    rightHeight - p.rightCrossSlope * fromInnerEdge, tol, ref coalesced);
                // Whether or not the first right point merged into the left inner edge, the vertex that IS the
                // right inner edge is the last one in the list at this moment.
                if (j == 0) { s.rightInnerEdgeIndex = points.Count - 1; }
            }
            s.rightCount = points.Count - beforeRight;

            s.points = points.ToArray();
            s.coalescedPoints = coalesced;
            s.totalWidthMetres = 2f * (gap + carriageway);
            s.footprintWidthMetres = s.totalWidthMetres
                + 2f * (p.shoulderWidthMetres > 0f ? p.shoulderWidthMetres : 0f);
            return s;
        }

        /// <summary> Appends a point unless it is the one just appended. Both coordinates have to agree: two
        /// points at the same offset with DIFFERENT heights are a split-level pair meeting at a face, and
        /// merging them would silently delete the retaining wall between the carriageways. </summary>
        private static void Append(List<SectionPoint> points, float lateral, float height, float tolerance,
            ref int coalesced)
        {
            if (points.Count > 0)
            {
                SectionPoint last = points[points.Count - 1];
                if (Mathf.Abs(last.lateral - lateral) <= tolerance
                    && Mathf.Abs(last.height - height) <= tolerance)
                {
                    coalesced++;
                    return;
                }
            }
            points.Add(new SectionPoint(lateral, height));
        }
        /// <summary> The textbook undivided road, written as plainly as possible: a crown at the alignment
        /// falling away at one cross-slope to each edge, sampled at evenly spaced offsets. This is the
        /// REFERENCE DEFINITION, not a second production path -- <see cref="Section"/> is what the sweep calls,
        /// and nothing but RM-59's gate calls this. Its whole purpose is to be simple enough to read as
        /// obviously correct, so that the general emitter agreeing with it MEANS something. </summary>
        public static SectionPoint[] PlainSection(float halfWidthMetres, float crossSlope, int intervals)
        {
            int n = intervals > 0 ? intervals : 1;
            SectionPoint[] points = new SectionPoint[n + 1];
            float step = (halfWidthMetres * 2f) / n;
            for (int i = 0; i <= n; i++)
            {
                float lateral = -halfWidthMetres + i * step;
                points[i] = new SectionPoint(lateral, -crossSlope * Mathf.Abs(lateral));
            }
            return points;
        }

        /// <summary> Whether two sections are the same road: the same number of vertices, in the same order, at
        /// the same places. The tolerance is real and deliberate -- the general emitter reaches the far edge by
        /// stepping outward from the median while the reference steps across from the left edge, so the two
        /// accumulate rounding differently and agreeing to the last bit of a float was never the claim. A
        /// tolerance of 0.1 mm is far below anything a vertex position can mean, while a changed COUNT or ORDER
        /// -- which is what a second code path actually produces -- fails immediately. </summary>
        public static bool SectionsMatch(SectionPoint[] a, SectionPoint[] b, float tolerance,
            out string difference)
        {
            if (a == null || b == null)
            {
                difference = "one of the two sections was not built at all";
                return false;
            }
            if (a.Length != b.Length)
            {
                difference = "vertex counts differ: " + a.Length + " against " + b.Length;
                return false;
            }
            for (int i = 0; i < a.Length; i++)
            {
                if (Mathf.Abs(a[i].lateral - b[i].lateral) > tolerance
                    || Mathf.Abs(a[i].height - b[i].height) > tolerance)
                {
                    difference = "vertex " + i + " sits at (" + Metres(a[i].lateral) + ", "
                        + Metres(a[i].height) + ") against (" + Metres(b[i].lateral) + ", "
                        + Metres(b[i].height) + ")";
                    return false;
                }
            }
            difference = "";
            return true;
        }
        /// <summary> Outer edge to outer edge at a station: both carriageways plus the median. </summary>
        public static float TotalWidthAt(DividedRoadProfile p, float station)
        {
            float gap = p.medianHalfGap.EmitsAt(station) ? p.medianHalfGap.HalfWidthAt(station) : 0f;
            return 2f * (gap + p.carriagewayHalfWidth.HalfWidthAt(station) * 2f);
        }

        /// <summary> The width RM-38 must flatten and blend from: the total plus both shoulders. A divided road
        /// that reports one carriageway here gets a cut that stops short of the median, and the untouched strip
        /// between the two cuts fills with spoil. </summary>
        public static float FootprintWidthAt(DividedRoadProfile p, float station)
        {
            return TotalWidthAt(p, station)
                + 2f * (p.shoulderWidthMetres > 0f ? p.shoulderWidthMetres : 0f);
        }

        /// <summary> ONE carriageway's longitudinal gradient at a station: the slope of the alignment's own
        /// elevation PLUS that carriageway's height offset, by central difference. The offset is what makes
        /// this a per-carriageway question -- on a side slope the two profiles diverge, and the steeper side is
        /// not necessarily the one the alignment describes. </summary>
        public static float CarriagewayGradientAt(AnimationCurve alignmentElevation, AnimationCurve heightOffset,
            float station, float differenceStep)
        {
            float h = differenceStep > 1e-4f ? differenceStep : 1e-4f;
            float before = ElevationAt(alignmentElevation, heightOffset, station - h);
            float after = ElevationAt(alignmentElevation, heightOffset, station + h);
            return (after - before) / (2f * h);
        }

        private static float ElevationAt(AnimationCurve alignmentElevation, AnimationCurve heightOffset,
            float station)
        {
            float value = alignmentElevation == null ? 0f : alignmentElevation.Evaluate(station);
            if (heightOffset != null) { value += heightOffset.Evaluate(station); }
            return value;
        }

        /// <summary> Checks ONE carriageway against its road class's maximum gradient over a station range,
        /// reporting the steepest sample and where it was. Called once per carriageway: a divided road can be
        /// legal on one side and illegal on the other, and a single verdict for the pair would hide that.
        /// </summary>
        public static GradientVerdict CheckGradient(AnimationCurve alignmentElevation,
            AnimationCurve heightOffset, float startStation, float endStation, float sampleStep,
            float maxGradient)
        {
            GradientVerdict v = new GradientVerdict();
            v.limit = maxGradient;
            v.worstStation = startStation;
            float step = sampleStep > 1e-3f ? sampleStep : 1e-3f;
            for (float station = startStation; station <= endStation + 1e-4f; station += step)
            {
                float gradient = CarriagewayGradientAt(alignmentElevation, heightOffset, station, step * 0.5f);
                if (Mathf.Abs(gradient) > Mathf.Abs(v.worstGradient))
                {
                    v.worstGradient = gradient;
                    v.worstStation = station;
                }
            }
            v.withinLimit = Mathf.Abs(v.worstGradient) <= maxGradient + 1e-6f;
            return v;
        }
        /// <summary> The largest change in the offset curve's slope between consecutive samples, and where it
        /// happens. An offset curve that is continuous in its first derivative gives a small value that shrinks
        /// with the sample step; a kink gives a jump that does not shrink, and puts a visible crease down the
        /// carriageway edge at that control point. Measured on RM-31's own slope so the two tasks agree about
        /// what a crease is. </summary>
        public static float MaxOffsetSlopeJump(VariableWidthProfile profile, float startStation,
            float endStation, float sampleStep, out float atStation)
        {
            float step = sampleStep > 1e-3f ? sampleStep : 1e-3f;
            float previous = profile.WidthSlopeAt(startStation, step);
            float worst = 0f;
            atStation = startStation;
            for (float station = startStation + step; station <= endStation + 1e-4f; station += step)
            {
                float slope = profile.WidthSlopeAt(station, step);
                float jump = Mathf.Abs(slope - previous);
                if (jump > worst) { worst = jump; atStation = station; }
                previous = slope;
            }
            return worst;
        }

        /// <summary> The shortest transition, in metres, over which each carriageway may be shifted sideways by
        /// <paramref name="lateralShiftMetres"/> at a design speed. The MUTCD taper relation this uses now lives in
        /// exactly one place, <see cref="RoadCityMaster.LaneModel.TaperLengthMetres"/>, and this method delegates to it
        /// rather than keeping a second copy: a divided-road transition and a lane taper describe the same physical act
        /// -- working an offset off over a length at a speed -- so two formulae could only ever disagree. That move also
        /// corrected the high-speed constant from a rounded 0.6 to the exact 0.62 the metric table prints, which is
        /// 1/1.609344 and is derived in full at the owning method; a rounded-down constant shortens a safety taper.
        /// Signature and meaning are unchanged, so every existing caller and gate still reads the same thing. </summary>
        public static float MinimumTransitionLengthMetres(float lateralShiftMetres, float designSpeedKmh)
        {
            return (float)LaneModel.TaperLengthMetres(lateralShiftMetres, designSpeedKmh);
        }

        /// <summary> Checks an AUTHORED divided-to-undivided transition length against that minimum. Too short
        /// is REFUSED with both numbers quoted, rather than accepted and creased or silently lengthened: the
        /// author asked for a specific station to stop being divided at, and moving it without saying so is the
        /// worse of the two failures. </summary>
        public static TransitionVerdict CheckTransition(float authoredLengthMetres, float lateralShiftMetres,
            float designSpeedKmh)
        {
            TransitionVerdict v = new TransitionVerdict();
            v.authoredLengthMetres = authoredLengthMetres;
            v.lateralShiftMetres = lateralShiftMetres;
            v.minimumLengthMetres = MinimumTransitionLengthMetres(lateralShiftMetres, designSpeedKmh);
            v.acceptable = authoredLengthMetres >= v.minimumLengthMetres - 1e-4f;
            v.refusal = v.acceptable ? "" : "the divided section closes over " + Metres(authoredLengthMetres)
                + ", which is too short to move each carriageway " + Metres(lateralShiftMetres)
                + " sideways at " + Number(designSpeedKmh) + " km/h"
                + ". Lengthen the transition to at least " + Metres(v.minimumLengthMetres)
                + ", or end the divided section further from the junction.";
            return v;
        }
        /// <summary> The median half-gap curve for a road that is divided up to <paramref name="dividedEnd"/>
        /// and undivided after the transition. Flat tangents on every key make the closing ramp an eased
        /// smoothstep -- continuous in its first derivative, zero slope where it meets each flat -- which is
        /// what keeps the carriageway edge from creasing where the median starts to close. The pavement narrows
        /// by the median's width across the transition, because that is what closing a median does; it does not
        /// silently keep the total width by widening the lanes. </summary>
        public static VariableWidthProfile TransitionToUndivided(float medianHalfGapMetres, float dividedEnd,
            float transitionLengthMetres)
        {
            float length = transitionLengthMetres > 0.01f ? transitionLengthMetres : 0.01f;
            VariableWidthProfile p;
            p.halfWidthOverDistance = new AnimationCurve(
                new Keyframe(0f, medianHalfGapMetres, 0f, 0f),
                new Keyframe(dividedEnd, medianHalfGapMetres, 0f, 0f),
                new Keyframe(dividedEnd + length, 0f, 0f, 0f));
            p.minEmitHalfWidth = 0.001f;
            return p;
        }

        /// <summary> Whether the two carriageways have come back to the SAME elevation by a station. A road that
        /// reports itself undivided while its two vertical profiles still differ has a step down its centreline,
        /// which is the one way the transition can be geometrically legal and still wrong. </summary>
        public static bool HeightsMeetAt(DividedRoadProfile p, float station, float toleranceMetres)
        {
            float left = p.leftHeightOffset == null ? 0f : p.leftHeightOffset.Evaluate(station);
            float right = p.rightHeightOffset == null ? 0f : p.rightHeightOffset.Evaluate(station);
            return Mathf.Abs(left - right) <= toleranceMetres;
        }

        private static string Metres(float value)
        {
            return Number(value) + " m";
        }

        private static string Number(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }

}
