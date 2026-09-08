// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-89's lane model, kept as PURE arithmetic so the whole cross-section can be proven before any
    /// carriageway mesh exists. The tool has never had a direction on a lane: <c>laneAmount</c> was a single
    /// directionless total, which is why a two-lane approach could not be matched to a four-lane one (RM-90) and why
    /// no marking pass could say where the centre line goes. This holds the three facts that fix that -- how many
    /// lanes run each way, how wide one lane is, and what physically separates the two directions -- and lays them
    /// out as a contiguous band list whose arithmetic a headless gate can check.
    ///
    /// Conventions, fixed here so every consumer reads the same section:
    ///   * The spline IS the cross-section centre line. With a constant cross-section the section is symmetric about
    ///     x = 0, so the left edge sits at -Width/2 and the right edge at +Width/2 whatever the lane counts are. An
    ///     auxiliary lane breaks that symmetry deliberately (see <see cref="LaneModel.BandsAt"/>), which is why the
    ///     edges are reported rather than derived once one is open.
    ///   * Bands run LEFT to RIGHT: left shoulder, the backward lanes, the median, the forward lanes, right shoulder.
    ///   * Lanes are indexed from the CENTRE outward (index 0 is the lane against the centre line or median), because
    ///     that is the index a marking pass needs: lane 0's inner edge is where the centre line is painted.
    ///   * Backward lanes carry direction -1, forward lanes +1, shoulders and the median 0.
    /// No band overlaps its neighbour and none leaves a gap: each band starts exactly where the previous one ended,
    /// which is what <see cref="LaneModel.IsContiguousBetween"/> proves rather than assumes. </summary>
    public enum LaneBandKind
    {
        Shoulder = 0,
        Lane = 1,
        Median = 2,
        /// <summary> An auxiliary lane present only over part of the road — a speed-change lane at an interchange
        /// diverge or merge, or a climbing lane on a grade. Its width varies with distance along the road. </summary>
        Auxiliary = 3
    }


    /// <summary> One lateral band of the cross-section, measured in metres from the spline centre line. </summary>
    public readonly struct LaneBand
    {
        public readonly LaneBandKind Kind;

        /// <summary> +1 forward, -1 backward, 0 for a band that carries no traffic (shoulder, median). </summary>
        public readonly int Direction;

        /// <summary> Index of this lane within its direction, counted from the centre line outward (0 = innermost).
        /// -1 when the band is not a lane. </summary>
        public readonly int IndexFromCentre;

        public readonly double Left;
        public readonly double Right;

        public LaneBand(LaneBandKind kind, int direction, int indexFromCentre, double left, double right)
        {
            Kind = kind;
            Direction = direction;
            IndexFromCentre = indexFromCentre;
            Left = left;
            Right = right;
        }

        public double Width { get { return Right - Left; } }

        public double Centre { get { return (Left + Right) * 0.5; } }
    }


    /// <summary> Where a roadside or median object attaches laterally. Anything placed along a road -- guard rail,
    /// concrete barrier, lamp column, median planting, sign -- is positioned from one of these, never from a hard-coded
    /// offset, because the section is not the same width all the way along a road that widens or carries a speed-change
    /// lane. Anchoring is what makes a rail follow the pavement out and back in again and stop dead where the road
    /// ends.
    ///
    /// This enum settles the LATERAL question only. Where along a road a run of objects stops and restarts -- skipping
    /// an intersection so a median barrier does not stand in the middle of the crossing road and close it -- is already
    /// owned by <c>RoadCityMaster.CrossSection.FurnitureRunPlanner</c> and its exclusion spans, and is deliberately not
    /// duplicated here. Lateral offset from this enum, longitudinal extent from that planner, one owner each. </summary>
    public enum LaneAnchor
    {
        /// <summary> The spline itself. On an undivided road this is the painted centre line. </summary>
        CentreLine = 0,
        /// <summary> Outermost left extent of the built section, shoulders included. Where a guard rail or a verge lamp
        /// column belongs. </summary>
        LeftEdge = 1,
        RightEdge = 2,
        /// <summary> Middle of the physical median between the opposing carriageways -- the usual place for a central
        /// lamp column lighting both directions, and for a barrier or planted strip. </summary>
        MedianCentre = 3,
        /// <summary> The median's two faces, for a rail or kerb set against one carriageway rather than centred. </summary>
        MedianLeft = 4,
        MedianRight = 5,
        /// <summary> Edge of the running surface -- the last lane or auxiliary lane, shoulders EXCLUDED. A kerb line or
        /// a lane-edge marking belongs here, not at the section edge. </summary>
        LeftCarriagewayEdge = 6,
        RightCarriagewayEdge = 7
    }


    /// <summary> Why a lane count alone is not enough: at a grade-separated interchange the through lanes carry
    /// straight on while a ramp climbs away, and an EXTRA lane opens shortly before the gore so diverging traffic can
    /// slow without holding up the mainline. That extra lane exists only between two distances, so the cross-section
    /// changes along the road. This names the four real kinds. </summary>
    public enum AuxLaneKind
    {
        None = 0,
        /// <summary> Speed-change lane before a diverge (a ramp exit / flyover off-slip): tapers open, runs at full
        /// width to the gore, and ends there because the traffic in it has left the road. </summary>
        Deceleration = 1,
        /// <summary> Speed-change lane after a merge (a ramp entry): starts at full width where the ramp joins and
        /// tapers closed as its traffic moves into the through lanes. </summary>
        Acceleration = 2,
        /// <summary> A climbing (truck) lane on a sustained grade: tapers open, runs the length of the grade, tapers
        /// closed. This is what an odd lane total on a two-way road usually is. </summary>
        Climbing = 3,
        /// <summary> A PERMANENT lane addition -- a road widening from, say, two lanes to four. Physically identical to
        /// a speed-change lane at its upstream end (a taper opens the new lane over a real length) but it never closes
        /// again, so the road simply carries more lanes downstream. Modelling a widening this way is what stops a lane
        /// count from jumping discontinuously: the extra lane grows from zero width to full width over the taper, the
        /// same way asphalt is actually laid. </summary>
        Widening = 4,
        /// <summary> A turn bay at an intersection approach: the surplus lanes of a wider road where it meets a
        /// narrower one. It opens on a taper, holds full width to the stop line, and ends there because its traffic has
        /// turned off. This is how a four-lane arterial meets a two-lane collector in the real world -- the arterial is
        /// not narrowed and the collector is not widened; the mismatch becomes turning capacity. </summary>
        TurnBay = 5
    }


    /// <summary> One auxiliary lane: a lane that exists over a stretch of the road rather than its whole length, with
    /// a linear taper at each end. All four distances are measured along the spline in metres and must be
    /// non-decreasing: taper opens over [Start, Full], holds full width over [Full, EndFull], closes over
    /// [EndFull, End]. A deceleration lane simply has EndFull == End (it ends at the gore, no closing taper); an
    /// acceleration lane has Start == Full. The lane opens on ONE side of the section, outboard of that direction's
    /// outermost through lane and inboard of its shoulder, which is where a real speed-change lane goes. </summary>
    public readonly struct AuxiliaryLane
    {
        public readonly AuxLaneKind Kind;

        /// <summary> +1 to open outboard of the forward lanes, -1 outboard of the backward lanes. </summary>
        public readonly int Direction;

        public readonly double Start;
        public readonly double Full;
        public readonly double EndFull;
        public readonly double End;

        public AuxiliaryLane(AuxLaneKind kind, int direction, double start, double full, double endFull, double end)
        {
            Kind = kind;
            Direction = direction >= 0 ? 1 : -1;
            Start = start;
            Full = full < start ? start : full;
            EndFull = endFull < full ? full : endFull;
            End = end < endFull ? endFull : end;
        }

        public static AuxiliaryLane None { get { return new AuxiliaryLane(AuxLaneKind.None, 1, 0, 0, 0, 0); } }

        /// <summary> A permanent widening: the road gains a lane on one side at <paramref name="startM"/> and keeps it.
        /// The taper length is not a guess -- it comes from <see cref="LaneModel.TaperLengthMetres"/> for the lane width
        /// and design speed, so a 3.6 m lane opening at 50 km/h takes 58 m to come in and one at 100 km/h takes 223 m,
        /// exactly as the standard requires. This is the call that makes a two-lane road become four-lane naturally
        /// instead of the count jumping at a node. </summary>
        public static AuxiliaryLane Widening(int direction, double startM, double laneWidthM, double designSpeedKPH)
        {
            double open = startM + LaneModel.TaperLengthMetres(laneWidthM, designSpeedKPH);
            return new AuxiliaryLane(AuxLaneKind.Widening, direction, startM, open, open, open);
        }

        /// <summary> A deceleration (exit) lane ending at the gore: it holds full width for
        /// <paramref name="fullLengthM"/> ahead of the gore so exiting traffic can slow off the mainline, and the taper
        /// that opens it sits upstream of that. </summary>
        public static AuxiliaryLane Deceleration(int direction, double goreM, double fullLengthM,
            double laneWidthM, double designSpeedKPH)
        {
            return Bay(AuxLaneKind.Deceleration, direction, goreM, fullLengthM, laneWidthM, designSpeedKPH);
        }

        /// <summary> A turn bay ending at a stop line. Same shape as a deceleration lane -- open on a taper, hold full
        /// width for the storage length, stop -- because that is physically what it is. </summary>
        public static AuxiliaryLane TurnBay(int direction, double stopLineM, double storageLengthM,
            double laneWidthM, double designSpeedKPH)
        {
            return Bay(AuxLaneKind.TurnBay, direction, stopLineM, storageLengthM, laneWidthM, designSpeedKPH);
        }

        /// <summary> An acceleration (entry) lane starting at the merge nose at full width and tapering closed once its
        /// traffic has reached mainline speed. </summary>
        public static AuxiliaryLane Acceleration(int direction, double noseM, double fullLengthM,
            double laneWidthM, double designSpeedKPH)
        {
            double full = fullLengthM < 0.0 ? 0.0 : fullLengthM;
            double endFull = noseM + full;
            return new AuxiliaryLane(AuxLaneKind.Acceleration, direction, noseM, noseM, endFull,
                endFull + LaneModel.TaperLengthMetres(laneWidthM, designSpeedKPH));
        }

        private static AuxiliaryLane Bay(AuxLaneKind kind, int direction, double endM, double fullLengthM,
            double laneWidthM, double designSpeedKPH)
        {
            double full = fullLengthM < 0.0 ? 0.0 : fullLengthM;
            double holdFrom = endM - full;
            double start = holdFrom - LaneModel.TaperLengthMetres(laneWidthM, designSpeedKPH);
            if (start < 0.0) { start = 0.0; }
            if (holdFrom < start) { holdFrom = start; }
            return new AuxiliaryLane(kind, direction, start, holdFrom, endM, endM);
        }

        /// <summary> How much of a lane width this auxiliary lane occupies at a distance along the road: zero outside
        /// its extent, the tapered fraction inside a taper, the full lane width in between. A
        /// <see cref="AuxLaneKind.Widening"/> is open-ended by definition -- the road genuinely has the extra lane from
        /// the end of its taper onward -- so it ignores the two closing distances rather than requiring the caller to
        /// pass the road's length as an end. </summary>
        public double WidthAt(double distance, double laneWidthM)
        {
            if (Kind == AuxLaneKind.None || laneWidthM <= 0.0) { return 0.0; }
            if (distance <= Start) { return 0.0; }
            if (distance < Full)
            {
                double run = Full - Start;
                return run <= 0.0 ? laneWidthM : laneWidthM * ((distance - Start) / run);
            }
            if (Kind == AuxLaneKind.Widening) { return laneWidthM; }
            // Holding full width is tested BEFORE the end, because a bay that stops at a stop line has no closing
            // taper at all (EndFull == End) and is a full lane wide right up to the line -- that is what a turn bay
            // is. Testing the end first would report the bay as absent at the one station that matters most.
            if (distance <= EndFull) { return laneWidthM; }
            if (distance >= End) { return 0.0; }
            double close = End - EndFull;
            return close <= 0.0 ? 0.0 : laneWidthM * ((End - distance) / close);
        }
    }


    /// <summary> The serialisable face of <see cref="AuxiliaryLane"/>. The model struct is readonly, and Unity cannot
    /// serialise readonly fields, so an authored auxiliary lane is stored as this mutable class on the road and
    /// converted to the immutable struct whenever the section is evaluated. Only <see cref="System.SerializableAttribute"/>
    /// is used, which is a base-library attribute, so this file still pulls in no UnityEngine type and the whole lane
    /// model stays reachable from a headless gate. </summary>
    [System.Serializable]
    public class AuxiliaryLaneAuthored
    {
        public AuxLaneKind kind = AuxLaneKind.None;

        /// <summary> +1 opens the lane outboard of the forward lanes, -1 outboard of the backward lanes. </summary>
        public int direction = 1;

        /// <summary> Distances along the road in metres: the taper opens over [start, full], full width holds over
        /// [full, endFull], the taper closes over [endFull, end]. A widening ignores the last two. </summary>
        public float start = 0f;
        public float full = 0f;
        public float endFull = 0f;
        public float end = 0f;

        public AuxiliaryLane ToModel()
        {
            return new AuxiliaryLane(kind, direction, start, full, endFull, end);
        }

        public static AuxiliaryLaneAuthored From(AuxiliaryLane lane)
        {
            var a = new AuxiliaryLaneAuthored();
            a.kind = lane.Kind;
            a.direction = lane.Direction;
            a.start = (float)lane.Start;
            a.full = (float)lane.Full;
            a.endFull = (float)lane.EndFull;
            a.end = (float)lane.End;
            return a;
        }

        /// <summary> Converts an authored list to model structs, skipping null and inactive entries. Returns null when
        /// nothing is authored, which is the cheap path the constant-cross-section case takes. </summary>
        public static List<AuxiliaryLane> ToModel(IList<AuxiliaryLaneAuthored> authored)
        {
            if (authored == null || authored.Count == 0) { return null; }
            List<AuxiliaryLane> lanes = null;
            for (int i = 0; i < authored.Count; i++)
            {
                if (authored[i] == null || authored[i].kind == AuxLaneKind.None) { continue; }
                if (lanes == null) { lanes = new List<AuxiliaryLane>(authored.Count); }
                lanes.Add(authored[i].ToModel());
            }
            return lanes;
        }
    }


    /// <summary> The pure lane-model arithmetic. No UnityEngine type appears here, so every number below is
    /// reachable from a headless gate. </summary>
    public static class LaneModel
    {
        /// <summary> Sentinel stored in a road's backward lane count meaning "this value has not been split yet":
        /// either the road was serialised before RM-89 (its old <c>laneAmount</c> total arrived through
        /// <c>[FormerlySerializedAs]</c> in the forward count) or the component was just constructed. Negative is
        /// safe as a sentinel because a real backward count is never below zero. </summary>
        public const int Unsplit = -1;

        /// <summary> True when a road's counts still hold a pre-RM-89 TOTAL rather than a per-direction pair. </summary>
        public static bool NeedsSplit(int lanesBackward)
        {
            return lanesBackward < 0;
        }

        /// <summary> Splits a directionless lane TOTAL into per-direction counts. A one-way road runs every lane the
        /// same way and has no opposing lanes at all. A two-way road needs at least one lane each way, so a stored
        /// total below two is raised to two rather than producing a direction with no lane. An ODD total on a two-way
        /// road is real -- it is a climbing or passing lane -- and the extra lane goes to the forward direction, which
        /// is the direction such a lane is added in. </summary>
        public static void SplitTotal(int total, bool oneWay, out int forward, out int backward)
        {
            if (oneWay)
            {
                forward = total < 1 ? 1 : total;
                backward = 0;
                return;
            }

            int t = total < 2 ? 2 : total;
            forward = (t + 1) / 2;
            backward = t - forward;
        }

        /// <summary> The lane total the two per-direction counts represent -- what the old <c>laneAmount</c> meant. </summary>
        public static int Total(int forward, int backward)
        {
            int f = forward < 0 ? 0 : forward;
            int b = backward < 0 ? 0 : backward;
            return f + b;
        }

        /// <summary> How long a taper must be to open or close a lane of a given width at a given design speed, in
        /// metres. A lane may NOT appear or vanish abruptly: the offset is worked off over a real length, which is why
        /// this returns a number instead of the caller inventing one.
        ///
        /// MUTCD Table 6C-4, metric formulae (L in metres, W = offset width in metres, S = speed in km/h):
        ///     S &lt;= 60 km/h : L = W * S^2 / 155
        ///     S &gt;= 70 km/h : L = 0.62 * W * S
        /// Both constants are the exact conversions of the customary relations rather than remembered figures. The
        /// customary high-speed rule is L(ft) = W(ft) * S(mph); substituting W(ft) = W/0.3048, S(mph) = S/1.609344 and
        /// L = 0.3048 * L(ft) leaves L = W * S / 1.609344 = 0.6214 * W * S, which is the 0.62 the metric table prints.
        /// The low-speed rule L(ft) = W(ft) * S(mph)^2 / 60 converts the same way to L = W * S^2 / (1.609344^2 * 60)
        /// = W * S^2 / 155.4. The 61-69 km/h gap in the published table is resolved upward to the high-speed formula,
        /// which is the longer and therefore the safer of the two. A full merging/diverging taper is L; a lateral SHIFT
        /// that adds no lane is half of it (<see cref="ShiftingTaperLengthMetres"/>).
        ///
        /// This is the ONE place the taper relation lives. <c>CrossSection.DividedRoad.MinimumTransitionLengthMetres</c>
        /// delegates here rather than carrying its own copy, so a divided-road transition and a lane taper can never
        /// disagree about how long a given shift takes at a given speed.
        ///
        /// Worked check against the customary table: a 3.6 m lane at 100 km/h gives 0.62 * 3.6 * 100 = 223 m, and the
        /// customary row for a 12 ft lane at 60 mph gives 12 * 60 = 720 ft = 219 m. The same 3.6 m lane at 40 km/h
        /// gives 3.6 * 1600 / 155 = 37 m, which is the order of the "roughly 30 m" an urban flyover diverge shows on
        /// the ground -- the observation and the standard agree, and the standard is what is used. </summary>
        public static double TaperLengthMetres(double offsetWidthM, double designSpeedKPH)
        {
            if (offsetWidthM <= 0.0 || designSpeedKPH <= 0.0) { return 0.0; }
            if (designSpeedKPH <= 60.0) { return offsetWidthM * designSpeedKPH * designSpeedKPH / 155.0; }
            return 0.62 * offsetWidthM * designSpeedKPH;
        }

        /// <summary> A shifting taper: the carriageway moves sideways by an offset without gaining or losing a lane.
        /// MUTCD Table 6C-4 gives this as half the merging taper length. </summary>
        public static double ShiftingTaperLengthMetres(double offsetWidthM, double designSpeedKPH)
        {
            return TaperLengthMetres(offsetWidthM, designSpeedKPH) * 0.5;
        }

        /// <summary> Matches one approach's lane count to another's at an intersection, in the way a junction is
        /// actually built. Unequal counts are NORMAL -- a four-lane arterial crossing a two-lane collector is the
        /// commonest junction there is -- so this never fails and never rewrites either road. The lanes that carry
        /// straight through are the SMALLER of the two counts; the wider approach's remaining lanes are surplus, and
        /// surplus lanes at a junction are turn bays (<see cref="AuxLaneKind.TurnBay"/>) which end at the stop line.
        /// The narrow approach is left alone: widening it to match would be inventing pavement that is not there.
        /// A caller wanting a turn bay on the narrow leg adds one explicitly as a flare, which is a separate decision
        /// from making the arithmetic close. </summary>
        public static void MatchApproach(int lanesThisLeg, int lanesOtherLeg, out int through, out int surplus)
        {
            int a = lanesThisLeg < 0 ? 0 : lanesThisLeg;
            int b = lanesOtherLeg < 0 ? 0 : lanesOtherLeg;
            through = a < b ? a : b;
            surplus = a - through;
        }

        /// <summary> A median exists only where there are two directions to separate AND the separation is physical.
        /// Painted markers separate directions without occupying width, so they contribute nothing to the section --
        /// which is why the divided test is a caller's decision about the divider KIND, passed in here as a bool, and
        /// a median width of zero is treated as no median however it was configured. </summary>
        public static double MedianWidth(int backward, bool dividerIsPhysical, double medianWidthM)
        {
            if (backward < 1 || !dividerIsPhysical || medianWidthM <= 0.0) { return 0.0; }
            return medianWidthM;
        }

        /// <summary> The carriageway width: the running lanes only, excluding shoulders and any median. This is what
        /// the tool has always called the road width. </summary>
        public static double CarriagewayWidth(int forward, int backward, double laneWidthM)
        {
            return Total(forward, backward) * laneWidthM;
        }

        /// <summary> The whole cross-section width: lanes, plus the median between the directions, plus a shoulder on
        /// each side when shoulders are enabled. This is the number the band list must add up to exactly. </summary>
        public static double CrossSectionWidth(int forward, int backward, double laneWidthM,
            bool shouldersEnabled, double shoulderWidthM, double medianWidthM)
        {
            double shoulders = shouldersEnabled && shoulderWidthM > 0.0 ? shoulderWidthM * 2.0 : 0.0;
            return CarriagewayWidth(forward, backward, laneWidthM) + medianWidthM + shoulders;
        }

        /// <summary> Lays the section out left to right as contiguous bands measured from the spline centre line, with
        /// no auxiliary lane. </summary>
        public static List<LaneBand> Bands(int forward, int backward, double laneWidthM,
            bool shouldersEnabled, double shoulderWidthM, double medianWidthM)
        {
            double left, right;
            return BandsAt(0.0, AuxiliaryLane.None, forward, backward, laneWidthM,
                shouldersEnabled, shoulderWidthM, medianWidthM, out left, out right);
        }

        /// <summary> The section at a distance along the road with a single auxiliary lane -- the common case of one
        /// speed-change lane or one turn bay. Delegates to the list form. </summary>
        public static List<LaneBand> BandsAt(double distance, AuxiliaryLane aux, int forward, int backward,
            double laneWidthM, bool shouldersEnabled, double shoulderWidthM, double medianWidthM,
            out double leftEdge, out double rightEdge)
        {
            var one = aux.Kind == AuxLaneKind.None ? null : new List<AuxiliaryLane>(1) { aux };
            return BandsAt(distance, one, forward, backward, laneWidthM, shouldersEnabled, shoulderWidthM,
                medianWidthM, out leftEdge, out rightEdge);
        }

        /// <summary> The section at a distance along the road, including every auxiliary lane open there. More than one
        /// may be: a road widening from two lanes to four gains one lane in each direction, so it carries one widening
        /// per side, and an interchange approach can hold a turn bay and a deceleration lane at once.
        ///
        /// Each auxiliary lane is inserted outboard of its direction's outermost through lane and inboard of that side's
        /// shoulder, and the section grows on THAT side only: the through lanes stay at exactly the offsets they have
        /// where nothing is open beside them, which is how a real speed-change lane behaves -- the mainline does not
        /// shift sideways as a ramp taper opens. That is why the section is not symmetric about the spline once an
        /// auxiliary lane is present, so the left and right edges are reported rather than assumed. Auxiliary bands are
        /// emitted in list order, so a given input always produces the same section (C-06). </summary>
        public static List<LaneBand> BandsAt(double distance, IList<AuxiliaryLane> aux, int forward, int backward,
            double laneWidthM, bool shouldersEnabled, double shoulderWidthM, double medianWidthM,
            out double leftEdge, out double rightEdge)
        {
            int f = forward < 0 ? 0 : forward;
            int b = backward < 0 ? 0 : backward;
            double shoulder = shouldersEnabled && shoulderWidthM > 0.0 ? shoulderWidthM : 0.0;
            double baseWidth = CrossSectionWidth(f, b, laneWidthM, shouldersEnabled, shoulderWidthM, medianWidthM);

            int auxCount = aux == null ? 0 : aux.Count;
            double auxLeftWidth = 0.0;
            double auxRightWidth = 0.0;
            for (int i = 0; i < auxCount; i++)
            {
                double w = aux[i].WidthAt(distance, laneWidthM);
                if (w <= 0.0) { continue; }
                if (aux[i].Direction < 0) { auxLeftWidth += w; } else { auxRightWidth += w; }
            }

            leftEdge = -baseWidth * 0.5 - auxLeftWidth;
            rightEdge = leftEdge + baseWidth + auxLeftWidth + auxRightWidth;

            var bands = new List<LaneBand>(f + b + auxCount + 3);
            double x = leftEdge;

            if (shoulder > 0.0)
            {
                bands.Add(new LaneBand(LaneBandKind.Shoulder, 0, -1, x, x + shoulder));
                x += shoulder;
            }

            // Left-side auxiliary lanes, outermost first: the taper opens against the shoulder and works inward, so the
            // band nearest the through lanes is the one listed last.
            for (int i = auxCount - 1; i >= 0; i--)
            {
                if (aux[i].Direction >= 0) { continue; }
                double w = aux[i].WidthAt(distance, laneWidthM);
                if (w <= 0.0) { continue; }
                bands.Add(new LaneBand(LaneBandKind.Auxiliary, -1, -1, x, x + w));
                x += w;
            }

            // Backward lanes, outermost first: index b-1 down to 0, so index 0 finishes against the centre.
            for (int i = b - 1; i >= 0; i--)
            {
                bands.Add(new LaneBand(LaneBandKind.Lane, -1, i, x, x + laneWidthM));
                x += laneWidthM;
            }

            if (medianWidthM > 0.0)
            {
                bands.Add(new LaneBand(LaneBandKind.Median, 0, -1, x, x + medianWidthM));
                x += medianWidthM;
            }

            // Forward lanes, innermost first: index 0 starts against the centre and the count runs outward.
            for (int i = 0; i < f; i++)
            {
                bands.Add(new LaneBand(LaneBandKind.Lane, 1, i, x, x + laneWidthM));
                x += laneWidthM;
            }

            // Right-side auxiliary lanes, innermost first, so list order runs outward from the through lanes.
            for (int i = 0; i < auxCount; i++)
            {
                if (aux[i].Direction < 0) { continue; }
                double w = aux[i].WidthAt(distance, laneWidthM);
                if (w <= 0.0) { continue; }
                bands.Add(new LaneBand(LaneBandKind.Auxiliary, 1, -1, x, x + w));
                x += w;
            }

            if (shoulder > 0.0)
            {
                bands.Add(new LaneBand(LaneBandKind.Shoulder, 0, -1, x, x + shoulder));
            }

            return bands;
        }

        /// <summary> Proves a symmetric band list tiles the section exactly, from -Width/2 to +Width/2. </summary>
        public static bool IsContiguous(IList<LaneBand> bands, double totalWidth, double tolerance,
            out string firstFault)
        {
            return IsContiguousBetween(bands, -totalWidth * 0.5, totalWidth * 0.5, tolerance, out firstFault);
        }

        /// <summary> Proves a band list tiles the section between two named edges: it starts at the left edge, each
        /// band begins exactly where the previous one ended, no band has zero or negative width, and the last one ends
        /// at the right edge. The edges are passed in rather than derived, because a section carrying an auxiliary lane
        /// is deliberately not symmetric about the spline. A failure names the first fault so a gate can print which
        /// band broke rather than only that something did. </summary>
        public static bool IsContiguousBetween(IList<LaneBand> bands, double expectedLeft, double expectedRight,
            double tolerance, out string firstFault)
        {
            firstFault = null;
            if (bands == null || bands.Count == 0)
            {
                firstFault = "the section has no bands at all";
                return false;
            }

            if (Abs(bands[0].Left - expectedLeft) > tolerance)
            {
                firstFault = "band 0 starts at " + F(bands[0].Left) + ", expected " + F(expectedLeft);
                return false;
            }

            for (int i = 1; i < bands.Count; i++)
            {
                if (Abs(bands[i].Left - bands[i - 1].Right) > tolerance)
                {
                    firstFault = "band " + i.ToString(CultureInfo.InvariantCulture) + " starts at "
                        + F(bands[i].Left) + " but band " + (i - 1).ToString(CultureInfo.InvariantCulture)
                        + " ended at " + F(bands[i - 1].Right)
                        + (bands[i].Left > bands[i - 1].Right ? " (gap)" : " (overlap)");
                    return false;
                }
                if (bands[i].Width <= 0.0)
                {
                    firstFault = "band " + i.ToString(CultureInfo.InvariantCulture) + " has width "
                        + F(bands[i].Width);
                    return false;
                }
            }

            double actualRight = bands[bands.Count - 1].Right;
            if (Abs(actualRight - expectedRight) > tolerance)
            {
                firstFault = "the section ends at " + F(actualRight) + ", expected " + F(expectedRight);
                return false;
            }

            return true;
        }

        /// <summary> Resolves a placement anchor to a lateral offset from the spline, in metres, for the section handed
        /// in. Every roadside and median object -- guard rail, concrete barrier, lamp post, median planting -- must be
        /// positioned from one of these rather than from a fixed offset, because the section width CHANGES along a road
        /// that widens or carries a speed-change lane. A rail placed at a constant offset would end up standing inside
        /// the carriageway the moment the road gains a lane; anchored to <see cref="LaneAnchor.LeftEdge"/> it moves out
        /// with the pavement and always stops exactly where the road stops. Returns false when the section has no such
        /// anchor, which is the honest answer for a median anchor on an undivided road -- there is nowhere to put it. </summary>
        public static bool TryAnchorOffset(IList<LaneBand> bands, LaneAnchor anchor, out double offset)
        {
            offset = 0.0;
            if (bands == null || bands.Count == 0) { return false; }

            switch (anchor)
            {
                case LaneAnchor.CentreLine:
                    offset = 0.0;
                    return true;
                case LaneAnchor.LeftEdge:
                    offset = bands[0].Left;
                    return true;
                case LaneAnchor.RightEdge:
                    offset = bands[bands.Count - 1].Right;
                    return true;
            }

            if (anchor == LaneAnchor.LeftCarriagewayEdge || anchor == LaneAnchor.RightCarriagewayEdge)
            {
                bool found = false;
                double left = 0.0;
                double right = 0.0;
                for (int i = 0; i < bands.Count; i++)
                {
                    if (bands[i].Kind != LaneBandKind.Lane && bands[i].Kind != LaneBandKind.Auxiliary) { continue; }
                    if (!found) { left = bands[i].Left; found = true; }
                    right = bands[i].Right;
                }
                if (!found) { return false; }
                offset = anchor == LaneAnchor.LeftCarriagewayEdge ? left : right;
                return true;
            }

            LaneBand median;
            if (!TryMedianBand(bands, out median)) { return false; }
            if (anchor == LaneAnchor.MedianLeft) { offset = median.Left; return true; }
            if (anchor == LaneAnchor.MedianRight) { offset = median.Right; return true; }
            offset = median.Centre;
            return true;
        }

        /// <summary> The median band, when the section has one. A physical median is the strip between the opposing
        /// carriageways, and in the real world it is where the guard rail, the concrete barrier, the planting and very
        /// often the lamp columns go -- one column on the centre line lighting both directions. Undivided roads have no
        /// median band at all, so this returns false rather than an empty strip a caller might build on. </summary>
        public static bool TryMedianBand(IList<LaneBand> bands, out LaneBand median)
        {
            median = default(LaneBand);
            if (bands == null) { return false; }
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Kind != LaneBandKind.Median) { continue; }
                median = bands[i];
                return true;
            }
            return false;
        }

        /// <summary> A deterministic one-line rendering of a section, for the C-06 repeat check and for a gate report
        /// that shows the arithmetic instead of asserting it. </summary>
        public static string Signature(IList<LaneBand> bands)
        {
            var sb = new StringBuilder();
            if (bands == null) { return string.Empty; }
            for (int i = 0; i < bands.Count; i++)
            {
                LaneBand n = bands[i];
                if (i > 0) { sb.Append(' '); }
                if (n.Kind == LaneBandKind.Lane)
                {
                    sb.Append(n.Direction > 0 ? "F" : "B");
                    sb.Append(n.IndexFromCentre.ToString(CultureInfo.InvariantCulture));
                }
                else if (n.Kind == LaneBandKind.Auxiliary)
                {
                    sb.Append(n.Direction > 0 ? "AUXF" : "AUXB");
                }
                else
                {
                    sb.Append(n.Kind == LaneBandKind.Median ? "MED" : "SHD");
                }
                sb.Append('[').Append(F(n.Left)).Append("..").Append(F(n.Right)).Append(']');
            }
            return sb.ToString();
        }

        private static double Abs(double v) { return v < 0.0 ? -v : v; }

        private static string F(double v) { return v.ToString("0.###", CultureInfo.InvariantCulture); }
    }
}
