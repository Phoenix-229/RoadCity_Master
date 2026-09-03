#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-89's gate: proves the lane model is a real cross-section rather than a lane COUNT with a
    /// width multiplied onto it. The tool has always stored one directionless `laneAmount`; RM-89 replaced it
    /// with a per-direction pair plus a median, which makes three things assertable that were not before --
    /// which side of the centre line a lane is on, that a one-way has no opposing lanes and therefore no
    /// median, and that the bands laid out across the section add up to the section width with no gap and no
    /// overlap.
    ///
    /// The arithmetic here is not circular. <see cref="LaneModel.CrossSectionWidth"/> adds up lanes, median and
    /// shoulders from the authored numbers directly; <see cref="LaneModel.BandsAt"/> lays out the bands
    /// left-to-right by walking the section. They are written independently, so a band list that agrees with
    /// the width sum agrees for a reason.
    ///
    /// It also measures the behaviour the earlier model had no vocabulary for at all: a road expanding from two
    /// lanes to four over a real MUTCD taper rather than at a step (the through lanes must NOT shift sideways
    /// while that taper opens -- pavement is added at the edge, traffic does not swerve), unequal approach legs
    /// at a junction resolving to the smaller through count with the surplus becoming turn bays, and the
    /// lateral anchors furniture is placed against, including the honest refusal when an author asks for a
    /// median anchor on a road that has no median.
    ///
    /// Report lines are prefixed RCMLANE. Determinism is C-06. </summary>
    public static class LaneSectionGate
    {
        // The test sections are AUTHORED urban/rural numbers, not arbitrary ones: 3.5 m lane (AASHTO urban
        // arterial), 3.6 m lane where a taper is measured (the width the MUTCD relation is worked against),
        // 2.5 m outside shoulder, 4 m median -- wide enough to stand a barrier or a planted strip in.
        private const double Lane = 3.5;
        private const double TaperLane = 3.6;
        private const double Shoulder = 2.5;
        private const double Median = 4.0;
        private const double Tol = 1e-9;

        [MenuItem("Window/Road City Master/Testing/Lane section (per-node cross-section)", false, 1089)]
        private static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMLANE BEGIN\n");
            List<string> failures = new List<string>();

            CaseOneWayHasNoOpposingLanes(builder, failures);
            CaseDividedPutsOpposingLanesEitherSideOfTheMedian(builder, failures);
            CaseLegacyTotalSplits(builder, failures);
            CaseWideningOpensOverATaper(builder, failures);
            CaseUnequalApproachLegs(builder, failures);
            CaseAnchors(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMLANE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMLANE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMLANE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A one-way street: every lane points forward, there are no opposing lanes, and so there is
        /// nothing for a median to separate -- an authored median width must not appear in the section. That is
        /// the rule the single-count model could not express, because a total of 3 said nothing about whether
        /// the road was one-way with 3 lanes or two-way with an odd split. </summary>
        private static void CaseOneWayHasNoOpposingLanes(StringBuilder builder, List<string> failures)
        {
            int forward, backward;
            LaneModel.SplitTotal(3, true, out forward, out backward);
            Expect(forward == 3 && backward == 0,
                "one-way: a 3-lane total must split 3 forward / 0 backward, got "
                + forward + " / " + backward + ".", failures);

            // An author has typed a median width even though the road is one-way. The model must ignore it.
            double median = LaneModel.MedianWidth(backward, true, Median);
            Expect(median == 0.0,
                "one-way: an authored " + F(Median) + " m median must collapse to 0 with no opposing lanes, got "
                + F(median) + " m.", failures);

            double width = LaneModel.CrossSectionWidth(forward, backward, Lane, true, Shoulder, median);
            List<LaneBand> bands = LaneModel.Bands(forward, backward, Lane, true, Shoulder, median);

            Expect(Approx(width, 3 * Lane + 2 * Shoulder),
                "one-way: section width must be 3 lanes + 2 shoulders = " + F(3 * Lane + 2 * Shoulder)
                + " m, got " + F(width) + " m.", failures);

            string fault;
            Expect(LaneModel.IsContiguous(bands, width, Tol, out fault),
                "one-way: bands must tile the section with no gap or overlap -- " + fault, failures);

            int backwardLanes = 0;
            int medianBands = 0;
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Kind == LaneBandKind.Lane && bands[i].Direction < 0) { backwardLanes++; }
                if (bands[i].Kind == LaneBandKind.Median) { medianBands++; }
            }
            Expect(backwardLanes == 0 && medianBands == 0,
                "one-way: the section must contain no backward lane and no median band, got "
                + backwardLanes + " backward and " + medianBands + " median.", failures);

            builder.Append("RCMLANE trace  one-way 3 lanes  width ").Append(F(width))
                   .Append(" m  ").Append(LaneModel.Signature(bands)).Append("\n");
        }

        /// <summary> A divided road: two lanes each way about a 4 m median. The backward lanes must all sit left
        /// of the median band and the forward lanes all right of it -- the property a lane count alone cannot
        /// state -- and the median must be exactly the authored width, because that strip is what the author
        /// will later stand a barrier, a lamp column or a planted garden in. </summary>
        private static void CaseDividedPutsOpposingLanesEitherSideOfTheMedian(StringBuilder builder, List<string> failures)
        {
            double median = LaneModel.MedianWidth(2, true, Median);
            double width = LaneModel.CrossSectionWidth(2, 2, Lane, true, Shoulder, median);
            List<LaneBand> bands = LaneModel.Bands(2, 2, Lane, true, Shoulder, median);

            Expect(Approx(width, 4 * Lane + Median + 2 * Shoulder),
                "divided: section width must be 4 lanes + median + 2 shoulders = "
                + F(4 * Lane + Median + 2 * Shoulder) + " m, got " + F(width) + " m.", failures);

            string fault;
            Expect(LaneModel.IsContiguous(bands, width, Tol, out fault),
                "divided: bands must tile the section with no gap or overlap -- " + fault, failures);

            LaneBand med;
            bool found = LaneModel.TryMedianBand(bands, out med);
            Expect(found, "divided: a divided road must report a median band.", failures);
            if (!found) { return; }

            Expect(Approx(med.Width, Median),
                "divided: the median band must be the authored " + F(Median) + " m, got " + F(med.Width) + " m.",
                failures);
            Expect(Approx(med.Centre, 0.0),
                "divided: with a constant symmetric section the median must straddle the centre line, centre is "
                + F(med.Centre) + " m.", failures);

            int misplaced = 0;
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Kind != LaneBandKind.Lane) { continue; }
                bool leftOfMedian = bands[i].Right <= med.Left + Tol;
                bool rightOfMedian = bands[i].Left >= med.Right - Tol;
                bool correct = bands[i].Direction < 0 ? leftOfMedian : rightOfMedian;
                if (!correct) { misplaced++; }
            }
            Expect(misplaced == 0,
                "divided: " + misplaced + " lane(s) are on the wrong side of the median for their direction.",
                failures);

            builder.Append("RCMLANE trace  divided 2+2  width ").Append(F(width))
                   .Append(" m  median ").Append(F(med.Width)).Append(" m  ")
                   .Append(LaneModel.Signature(bands)).Append("\n");
        }

        /// <summary> Legacy data: a road serialised before RM-89 has no backward count at all, so the field
        /// arrives at its initialiser -- the negative sentinel -- and the total it carried forward must be
        /// halved back out. An ODD legacy total is the interesting one: a real road with an odd lane count has a
        /// climbing or passing lane, and that lane belongs to the forward direction. </summary>
        private static void CaseLegacyTotalSplits(StringBuilder builder, List<string> failures)
        {
            Expect(LaneModel.NeedsSplit(LaneModel.Unsplit),
                "legacy: the unsplit sentinel must be recognised as needing a split.", failures);
            Expect(!LaneModel.NeedsSplit(0),
                "legacy: a backward count of 0 is authored (a one-way), not unsplit, and must be left alone.",
                failures);

            Expect(LaneModel.Total(4, LaneModel.Unsplit) == 4,
                "legacy: an unsplit road's total must still read as the value it carried (4), got "
                + LaneModel.Total(4, LaneModel.Unsplit) + ".", failures);

            int forward, backward;
            LaneModel.SplitTotal(4, false, out forward, out backward);
            Expect(forward == 2 && backward == 2,
                "legacy: a 4-lane two-way total must split 2 / 2, got " + forward + " / " + backward + ".",
                failures);

            LaneModel.SplitTotal(5, false, out forward, out backward);
            Expect(forward == 3 && backward == 2,
                "legacy: a 5-lane two-way total must put the odd lane forward (3 / 2) -- an odd count is a "
                + "climbing or passing lane -- got " + forward + " / " + backward + ".", failures);

            builder.Append("RCMLANE trace  legacy 4 -> 2/2   5 -> 3/2\n");
        }

        /// <summary> The expansion the user asked for: a road going from two lanes to four must do it the way a
        /// real road does, over a taper, not at a step. Three things are asserted -- the taper length is the one
        /// the MUTCD relation gives for the design speed (3.6 m at 40 km/h is a low-speed case, W x V^2 / 155 =
        /// 37.16 m), the section grows by exactly one lane width across it, and the through lanes do NOT move
        /// while it opens. That last one is the point: pavement is added at the edge, so traffic already on the
        /// road keeps its line. A model that recomputed the section symmetrically would slide every lane
        /// sideways by half a lane, which is a swerve no road makes. </summary>
        private static void CaseWideningOpensOverATaper(StringBuilder builder, List<string> failures)
        {
            const double speed = 40.0;
            const double startAt = 100.0;

            double taper = LaneModel.TaperLengthMetres(TaperLane, speed);
            double expected = TaperLane * speed * speed / 155.0;
            Expect(Approx(taper, expected),
                "widening: the taper for a " + F(TaperLane) + " m lane at " + F(speed) + " km/h must be "
                + F(expected) + " m (low-speed W x V^2 / 155), got " + F(taper) + " m.", failures);

            AuxiliaryLane aux = AuxiliaryLane.Widening(1, startAt, TaperLane, speed);
            Expect(Approx(aux.Full - aux.Start, taper),
                "widening: the authored lane must open over the taper length, it opens over "
                + F(aux.Full - aux.Start) + " m.", failures);

            double median = LaneModel.MedianWidth(2, true, Median);
            double baseWidth = LaneModel.CrossSectionWidth(2, 2, TaperLane, true, Shoulder, median);

            double left, right;
            List<LaneBand> before = LaneModel.BandsAt(startAt - 1.0, aux, 2, 2, TaperLane,
                true, Shoulder, median, out left, out right);
            Expect(Approx(right - left, baseWidth),
                "widening: before the taper starts the section must still be " + F(baseWidth) + " m, got "
                + F(right - left) + " m.", failures);

            double halfLeft, halfRight;
            List<LaneBand> half = LaneModel.BandsAt(startAt + taper * 0.5, aux, 2, 2, TaperLane,
                true, Shoulder, median, out halfLeft, out halfRight);
            Expect(Approx(halfRight - halfLeft, baseWidth + TaperLane * 0.5),
                "widening: half way along the taper the section must have grown by half a lane to "
                + F(baseWidth + TaperLane * 0.5) + " m, got " + F(halfRight - halfLeft) + " m.", failures);

            string fault;
            Expect(LaneModel.IsContiguousBetween(half, halfLeft, halfRight, 1e-6, out fault),
                "widening: a part-open taper must still tile the section with no gap or overlap -- " + fault,
                failures);

            double fullLeft, fullRight;
            List<LaneBand> open = LaneModel.BandsAt(startAt + taper + 50.0, aux, 2, 2, TaperLane,
                true, Shoulder, median, out fullLeft, out fullRight);
            Expect(Approx(fullRight - fullLeft, baseWidth + TaperLane),
                "widening: past the taper the road must carry a full extra lane (" + F(baseWidth + TaperLane)
                + " m) and keep it -- a widening is permanent, not a bay -- got " + F(fullRight - fullLeft)
                + " m.", failures);

            LaneBand innerBefore, innerAfter;
            bool haveBefore = TryLane(before, 1, 0, out innerBefore);
            bool haveAfter = TryLane(open, 1, 0, out innerAfter);
            Expect(haveBefore && haveAfter,
                "widening: the innermost forward lane must be findable before and after the taper.", failures);
            if (haveBefore && haveAfter)
            {
                Expect(Approx(innerBefore.Left, innerAfter.Left) && Approx(innerBefore.Right, innerAfter.Right),
                    "widening: the through lanes must not move as the taper opens -- the innermost forward lane "
                    + "was [" + F(innerBefore.Left) + ", " + F(innerBefore.Right) + "] and became ["
                    + F(innerAfter.Left) + ", " + F(innerAfter.Right) + "].", failures);
            }

            int auxBands = 0;
            for (int i = 0; i < open.Count; i++)
            {
                if (open[i].Kind == LaneBandKind.Auxiliary) { auxBands++; }
            }
            Expect(auxBands == 1,
                "widening: exactly one auxiliary band must be open past the taper, got " + auxBands + ".",
                failures);

            builder.Append("RCMLANE trace  widening 2+2 -> 2+3  taper ").Append(F(taper))
                   .Append(" m at ").Append(F(speed)).Append(" km/h  ")
                   .Append(F(baseWidth)).Append(" -> ").Append(F(fullRight - fullLeft)).Append(" m\n");
        }

        /// <summary> Unequal approach legs, which is where a lane-count model breaks: a 4-lane arterial meeting
        /// a 2-lane street. The crossing carries the SMALLER through count, and the wider road's surplus lanes
        /// become turn bays that end at the stop line. The narrow road is NOT widened to match -- that would
        /// invent pavement nobody built -- and the junction never has to solve for two different numbers, which
        /// is what stops the maths breaking. </summary>
        private static void CaseUnequalApproachLegs(StringBuilder builder, List<string> failures)
        {
            int through, surplus;
            LaneModel.MatchApproach(4, 2, out through, out surplus);
            Expect(through == 2 && surplus == 2,
                "unequal legs: a 4-lane leg meeting a 2-lane leg must carry 2 through with 2 surplus, got "
                + through + " through / " + surplus + " surplus.", failures);

            LaneModel.MatchApproach(2, 4, out through, out surplus);
            Expect(through == 2 && surplus == 0,
                "unequal legs: the NARROW leg must keep its 2 lanes with no surplus and must not be widened, got "
                + through + " through / " + surplus + " surplus.", failures);

            LaneModel.MatchApproach(3, 3, out through, out surplus);
            Expect(through == 3 && surplus == 0,
                "unequal legs: matched legs must produce no surplus, got " + surplus + ".", failures);

            // The surplus is realised as a turn bay: full width at the stop line, tapered open upstream.
            const double stopLine = 200.0;
            const double storage = 45.0;
            const double speed = 50.0;
            AuxiliaryLane bay = AuxiliaryLane.TurnBay(1, stopLine, storage, TaperLane, speed);
            Expect(Approx(bay.WidthAt(stopLine, TaperLane), TaperLane),
                "turn bay: the bay must be full width at the stop line, got "
                + F(bay.WidthAt(stopLine, TaperLane)) + " m.", failures);
            Expect(Approx(bay.WidthAt(stopLine - storage, TaperLane), TaperLane),
                "turn bay: the bay must still be full width one storage length upstream of the stop line, got "
                + F(bay.WidthAt(stopLine - storage, TaperLane)) + " m.", failures);
            Expect(bay.WidthAt(stopLine - storage - LaneModel.TaperLengthMetres(TaperLane, speed) - 1.0, TaperLane) == 0.0,
                "turn bay: the bay must not exist upstream of its taper.", failures);

            builder.Append("RCMLANE trace  unequal legs 4x2 -> 2 through + 2 turn bays  bay taper ")
                   .Append(F(LaneModel.TaperLengthMetres(TaperLane, speed))).Append(" m\n");
        }

        /// <summary> The lateral anchors furniture is placed against. A lamp column standing in the median, a
        /// guard rail at the carriageway edge and a rail on the outside of the shoulder are three different
        /// offsets, and every one of them must come from the section rather than from a number typed twice. The
        /// carriageway-edge anchors must exclude the shoulder -- a rail placed on the carriageway edge belongs
        /// where the running surface ends, not out at the verge -- and asking for a median anchor on a road
        /// with no median must FAIL rather than silently return the centre line, which would drop a barrier
        /// down the middle of a two-way street. </summary>
        private static void CaseAnchors(StringBuilder builder, List<string> failures)
        {
            double median = LaneModel.MedianWidth(2, true, Median);
            List<LaneBand> divided = LaneModel.Bands(2, 2, Lane, true, Shoulder, median);
            double total = LaneModel.CrossSectionWidth(2, 2, Lane, true, Shoulder, median);

            double offset;
            Expect(LaneModel.TryAnchorOffset(divided, LaneAnchor.MedianCentre, out offset) && Approx(offset, 0.0),
                "anchors: the median centre of a symmetric divided road must be the centre line, got "
                + F(offset) + " m.", failures);

            Expect(LaneModel.TryAnchorOffset(divided, LaneAnchor.MedianLeft, out offset)
                    && Approx(offset, -Median * 0.5),
                "anchors: the median's left face must be at " + F(-Median * 0.5) + " m, got " + F(offset) + " m.",
                failures);

            Expect(LaneModel.TryAnchorOffset(divided, LaneAnchor.RightEdge, out offset)
                    && Approx(offset, total * 0.5),
                "anchors: the outside right edge must be at " + F(total * 0.5) + " m, got " + F(offset) + " m.",
                failures);

            Expect(LaneModel.TryAnchorOffset(divided, LaneAnchor.RightCarriagewayEdge, out offset)
                    && Approx(offset, total * 0.5 - Shoulder),
                "anchors: the right CARRIAGEWAY edge must sit inboard of the shoulder at "
                + F(total * 0.5 - Shoulder) + " m, got " + F(offset) + " m.", failures);

            List<LaneBand> undivided = LaneModel.Bands(1, 1, Lane, true, Shoulder,
                LaneModel.MedianWidth(1, false, 0.0));
            Expect(!LaneModel.TryAnchorOffset(undivided, LaneAnchor.MedianCentre, out offset),
                "anchors: a median anchor on an undivided road must be refused, not answered with the centre "
                + "line -- there is nowhere to stand a barrier.", failures);

            builder.Append("RCMLANE trace  anchors  median 0  medianLeft ").Append(F(-Median * 0.5))
                   .Append("  carriageway ").Append(F(total * 0.5 - Shoulder))
                   .Append("  outside ").Append(F(total * 0.5)).Append("\n");
        }

        /// <summary> C-06: the same authored numbers must produce a byte-identical section signature every time,
        /// including a part-open taper, where a float rounding difference would show up first. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            double median = LaneModel.MedianWidth(2, true, Median);
            AuxiliaryLane aux = AuxiliaryLane.Widening(1, 100.0, TaperLane, 40.0);
            double at = 100.0 + LaneModel.TaperLengthMetres(TaperLane, 40.0) * 0.5;

            string first = null;
            bool stable = true;
            for (int pass = 0; pass < 3; pass++)
            {
                double l, r;
                string sig = LaneModel.Signature(LaneModel.BandsAt(at, aux, 2, 2, TaperLane,
                    true, Shoulder, median, out l, out r));
                if (pass == 0) { first = sig; }
                else if (sig != first) { stable = false; }
            }
            Expect(stable, "determinism: the section signature changed between identical passes.", failures);
            builder.Append("RCMLANE trace  deterministic  ").Append(first).Append("\n");
        }

        /// <summary> Finds a running lane by direction and index out from the centre line. </summary>
        private static bool TryLane(IList<LaneBand> bands, int direction, int indexFromCentre, out LaneBand band)
        {
            for (int i = 0; i < bands.Count; i++)
            {
                if (bands[i].Kind == LaneBandKind.Lane
                    && bands[i].Direction == direction
                    && bands[i].IndexFromCentre == indexFromCentre)
                {
                    band = bands[i];
                    return true;
                }
            }
            band = new LaneBand(LaneBandKind.Lane, direction, indexFromCentre, 0, 0);
            return false;
        }

        private static bool Approx(double a, double b)
        {
            double d = a - b;
            return (d < 0 ? -d : d) <= 1e-6;
        }

        private static string F(double d)
        {
            return d.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
