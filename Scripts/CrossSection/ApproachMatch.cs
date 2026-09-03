#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> One approach arriving at a junction, described the way RM-89 describes a road: lanes PER
    /// DIRECTION rather than as a total, plus the width of a lane, the median between the directions, and the
    /// design speed the approach is signed for. A junction reconciliation needs all four -- the counts decide
    /// which lanes continue, the width decides whether a lane can butt-join or has to shift, the median decides
    /// whether there is a physical island to end furniture against, and the speed is the only thing that turns
    /// a lane drop into a LENGTH. </summary>
    public struct ApproachSection
    {
        public int lanesForward;
        public int lanesBackward;
        public float laneWidthMetres;
        public float medianWidthMetres;
        public float designSpeedKPH;

        public ApproachSection(int forward, int backward, float laneWidth, float medianWidth, float designSpeedKPH)
        {
            lanesForward = forward;
            lanesBackward = backward;
            laneWidthMetres = laneWidth;
            medianWidthMetres = medianWidth;
            this.designSpeedKPH = designSpeedKPH;
        }

        /// <summary> Reads an approach straight off a road, so the junction and the road can never disagree
        /// about how many lanes arrive. </summary>
        public static ApproachSection FromRoad(Road road)
        {
            if (road == null) { return new ApproachSection(0, 0, 0f, 0f, 0f); }
            int backward = road.lanesBackward;
            if (LaneModel.NeedsSplit(backward))
            {
                int f, b;
                LaneModel.SplitTotal(LaneModel.Total(road.lanesForward, backward), road.isOneWay, out f, out b);
                return new ApproachSection(f, b, road.laneWidth, road.MedianWidthEffective(), road.DesignSpeedKPH());
            }
            return new ApproachSection(road.lanesForward, backward, road.laneWidth,
                road.MedianWidthEffective(), road.DesignSpeedKPH());
        }

        public int LaneTotal { get { return LaneModel.Total(lanesForward, lanesBackward); } }
    }


    /// <summary> What a junction does about two approaches that do not carry the same number of lanes. Either
    /// the counts and widths agree and the carriageways BUTT-JOIN -- lane edge to lane edge, no taper, which is
    /// what a junction of two equal roads is -- or the wider approach carries surplus lanes that must END, and
    /// they end over a lane-drop taper whose length comes from the design speed rather than from an
    /// eyeballed number. When neither is possible the verdict is REFUSED and carries the reason, so the caller
    /// reports it into the panel instead of emitting a junction that is geometrically wrong. </summary>
    public struct ApproachMatchVerdict
    {
        /// <summary> True when a real matching was found. False means <see cref="refusal"/> is set and nothing
        /// should be emitted. </summary>
        public bool resolved;

        /// <summary> True when the two approaches meet lane-edge to lane-edge with nothing to drop. </summary>
        public bool buttJoin;

        /// <summary> Lanes that cross the junction in each direction: the SMALLER of the two approaches' counts.
        /// The narrow road is never widened to match -- that would invent pavement nobody built. </summary>
        public int throughForward;
        public int throughBackward;

        /// <summary> Lanes the FIRST approach carries beyond the through count, per direction. These do not
        /// cross; they become turn bays ending at the stop line. </summary>
        public int surplusForwardA;
        public int surplusBackwardA;

        /// <summary> The same for the second approach. At most one side of a junction has surplus in a given
        /// direction, because the through count is the minimum of the two. </summary>
        public int surplusForwardB;
        public int surplusBackwardB;

        /// <summary> The taper length, in metres, that reconciles the surplus lanes with the through lanes. It is
        /// one number because it is one relation, used at both ends of the junction: on the APPROACH side the
        /// surplus lanes are turn bays and this is the length over which they open upstream of the stop line; on
        /// the DEPARTURE side, where a wide road leaves into a narrow one, this is the length over which they are
        /// dropped. Zero on a butt-join. Per-lane stations for a sequential drop come from
        /// <see cref="ApproachMatch.DropStations"/>. </summary>
        public float dropTaperMetres;

        /// <summary> Length of the shifting taper that absorbs a difference in LANE WIDTH between the two
        /// approaches, in metres. Zero when the widths agree. A lane-width change is a lateral shift, and a
        /// shifting taper is half the length of a lane-drop taper for the same offset. </summary>
        public float widthShiftTaperMetres;

        /// <summary> The cited basis for whatever length was produced, empty on a butt-join. </summary>
        public string citation;

        /// <summary> Why no matching exists. Empty when <see cref="resolved"/> is true. </summary>
        public string refusal;

        /// <summary> A stable one-line form of the verdict, for a report line or a determinism check. </summary>
        public override string ToString()
        {
            if (!resolved) { return "REFUSED " + refusal; }
            if (buttJoin) { return "BUTT " + throughForward + "F/" + throughBackward + "B"; }
            return "DROP " + throughForward + "F/" + throughBackward + "B"
                + "  surplusA " + surplusForwardA + "F/" + surplusBackwardA + "B"
                + "  surplusB " + surplusForwardB + "F/" + surplusBackwardB + "B"
                + "  taper " + N(dropTaperMetres) + " m"
                + (widthShiftTaperMetres > 0f ? "  shift " + N(widthShiftTaperMetres) + " m" : "");
        }

        private static string N(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }


    /// <summary> RM-90: reconciles two junction approaches whose per-direction lane counts differ.
    ///
    /// The corner build this feeds has always treated an approach as a WIDTH. Two widths meeting says nothing
    /// about which lanes continue through the junction and which of them end at it, so a four-lane arterial
    /// meeting a two-lane street produced a plate with no statement about the two surplus lanes -- they simply
    /// stopped at the kerb line. Real junctions do not do that. The crossing carries the SMALLER through count
    /// and the surplus lanes end over a taper, which is the same rule whether the surplus becomes a left-turn
    /// bay, a right-turn bay or is dropped outright.
    ///
    /// Two decisions here are deliberate and are the answer to "which road gets changed":
    ///
    /// The narrow approach is NEVER widened to match the wide one. Widening it would invent pavement that
    /// nobody built and would push the kerb line, the footway and every piece of furniture outward for no
    /// reason. What real practice does instead is match at the minimum and give the wider approach turn bays --
    /// which is why the surplus is reported per approach rather than as a single number.
    ///
    /// The taper LENGTH is never chosen here. It comes from <see cref="LaneModel.TaperLengthMetres"/>, the one
    /// place in the tool that holds the taper relation, so the junction and the open road cannot disagree about
    /// how long it takes to drop a lane at a given speed. When the design speed is unknown the verdict is
    /// REFUSED rather than filled in with a plausible number -- § 10.13's rule, applied at the junction. </summary>
    public static class ApproachMatch
    {
        /// <summary> A lane-width difference smaller than this is a rounding artefact, not a transition: the
        /// carriageways butt-join and the paint absorbs it. AUTHORED at 1 cm -- below the tolerance any real
        /// road is built to. </summary>
        public const float WidthEpsilonMetres = 0.01f;

        /// <summary> Reconciles two approaches. Order matters only in that surplus is reported per approach;
        /// the through counts and the taper length are symmetric, so matching B against A gives the same
        /// junction with the surplus reported on the other side. </summary>
        public static ApproachMatchVerdict Match(ApproachSection a, ApproachSection b)
        {
            ApproachMatchVerdict v = new ApproachMatchVerdict();
            v.citation = "";
            v.refusal = "";

            if (a.laneWidthMetres <= 0f || b.laneWidthMetres <= 0f)
            {
                v.refusal = "an approach has a lane width of " + N(Mathf.Min(a.laneWidthMetres, b.laneWidthMetres))
                    + " m; a junction cannot be reconciled against a carriageway with no width.";
                return v;
            }

            LaneModel.MatchApproach(a.lanesForward, b.lanesForward, out v.throughForward, out v.surplusForwardA);
            LaneModel.MatchApproach(a.lanesBackward, b.lanesBackward, out v.throughBackward, out v.surplusBackwardA);

            int throughIgnored;
            LaneModel.MatchApproach(b.lanesForward, a.lanesForward, out throughIgnored, out v.surplusForwardB);
            LaneModel.MatchApproach(b.lanesBackward, a.lanesBackward, out throughIgnored, out v.surplusBackwardB);

            if (v.throughForward + v.throughBackward <= 0)
            {
                v.refusal = "no lane crosses the junction: approach A carries " + a.lanesForward + " forward / "
                    + a.lanesBackward + " backward and approach B carries " + b.lanesForward + " / "
                    + b.lanesBackward + ", so every direction resolves to zero through lanes. Two one-ways "
                    + "pointing away from each other cannot be joined at grade.";
                return v;
            }

            float widthDifference = Mathf.Abs(a.laneWidthMetres - b.laneWidthMetres);
            int surplusLanesA = v.surplusForwardA + v.surplusBackwardA;
            int surplusLanesB = v.surplusForwardB + v.surplusBackwardB;
            bool needsDrop = surplusLanesA + surplusLanesB > 0;
            bool needsShift = widthDifference > WidthEpsilonMetres;

            if (!needsDrop && !needsShift)
            {
                v.resolved = true;
                v.buttJoin = true;
                return v;
            }

            // Both lengths are measured on the approach that is CHANGING, at that approach's own design speed:
            // a lane is dropped at the speed traffic is travelling when it runs out, not at the other road's.
            float dropSpeed = surplusLanesA > 0 ? a.designSpeedKPH : b.designSpeedKPH;
            float dropWidth = surplusLanesA > 0
                ? surplusLanesA * a.laneWidthMetres
                : surplusLanesB * b.laneWidthMetres;

            if (needsDrop && dropSpeed <= 0f)
            {
                v.refusal = "the approach dropping " + (surplusLanesA > 0 ? surplusLanesA : surplusLanesB)
                    + " lane(s) has no design speed, and a lane-drop taper length is a function of design "
                    + "speed. REFUSED rather than assigned a plausible length: set the approach's speed limit "
                    + "or its road class.";
                return v;
            }
            if (needsShift && a.designSpeedKPH <= 0f && b.designSpeedKPH <= 0f)
            {
                v.refusal = "the two approaches differ in lane width by " + N(widthDifference)
                    + " m and neither carries a design speed, so the shifting taper that absorbs the "
                    + "difference has no cited length. REFUSED rather than guessed.";
                return v;
            }

            if (needsDrop)
            {
                v.dropTaperMetres = (float)LaneModel.TaperLengthMetres(dropWidth, dropSpeed);
            }
            if (needsShift)
            {
                float shiftSpeed = Mathf.Max(a.designSpeedKPH, b.designSpeedKPH);
                v.widthShiftTaperMetres = (float)LaneModel.ShiftingTaperLengthMetres(widthDifference, shiftSpeed);
            }

            v.resolved = true;
            v.buttJoin = false;
            v.citation = Citation(needsDrop, dropWidth, dropSpeed, needsShift, widthDifference);
            return v;
        }

        /// <summary> The stations, measured back from the stop line, at which each surplus lane on one approach
        /// stops carrying traffic. Lanes end from the OUTSIDE in -- the outermost lane ends first, because a
        /// lane cannot be removed from under the middle of a traffic stream -- so the list is ordered
        /// outermost-first, each entry being that lane's own taper length for the design speed. The caller
        /// stacks them: the outermost lane's taper is the furthest from the junction. </summary>
        public static List<float> DropStations(int surplusLanes, float laneWidthMetres, float designSpeedKPH)
        {
            List<float> stations = new List<float>();
            if (surplusLanes <= 0 || laneWidthMetres <= 0f || designSpeedKPH <= 0f) { return stations; }
            float one = (float)LaneModel.TaperLengthMetres(laneWidthMetres, designSpeedKPH);
            float cumulative = 0f;
            for (int i = 0; i < surplusLanes; i++)
            {
                cumulative += one;
                stations.Insert(0, cumulative);
            }
            return stations;
        }

        /// <summary> Turns a refused verdict into the entry RM-52's panel shows, or reports false when the verdict
        /// resolved and there is nothing to report. The mapping lives here rather than in the intersection
        /// component for one reason: a refusal that reaches the author is part of what this task promises, so it
        /// has to be provable by a headless gate, and a gate cannot construct a junction in a scene. The caller
        /// supplies the identity and the station because only the caller knows where in the scene it stands.
        ///
        /// A resolved verdict yields an EMPTY entry rather than a partly-filled one, so a caller that ignores the
        /// return value publishes a blank line instead of a phantom refusal against a junction that is fine. </summary>
        public static bool TryRefusal(ApproachMatchVerdict verdict, string objectId, string objectName,
            double stationMetres, out RefusalEntry entry)
        {
            if (verdict.resolved || string.IsNullOrEmpty(verdict.refusal))
            {
                entry = new RefusalEntry("", "", 0.0, "");
                return false;
            }
            // A negative station would send the author's selection behind the start of the road. An unmeasured
            // spline reports a negative length, and that is the only way one gets here, so it reads as the start.
            double station = stationMetres > 0.0 ? stationMetres : 0.0;
            entry = new RefusalEntry(objectId, objectName, station, verdict.refusal);
            return true;
        }

        private static string Citation(bool needsDrop, float dropWidth, float dropSpeed,
            bool needsShift, float widthDifference)
        {
            string basis = dropSpeed <= 60f
                ? "MUTCD Table 6C-4 low-speed relation L = W x V^2 / 155 (metric)"
                : "MUTCD Table 6C-4 relation L = 0.62 x W x V (metric)";
            string s = "";
            if (needsDrop)
            {
                s = "lane drop of " + N(dropWidth) + " m at " + N(dropSpeed) + " km/h, " + basis;
            }
            if (needsShift)
            {
                if (s != "") { s += "; "; }
                s += "lane-width change of " + N(widthDifference) + " m over a shifting taper, half the "
                    + "lane-drop length for the same offset";
            }
            return s;
        }

        private static string N(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
