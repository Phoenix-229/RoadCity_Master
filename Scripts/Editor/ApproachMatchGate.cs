#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RoadCityMaster.CrossSection;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-90's gate: proves a junction between two approaches carrying different numbers of lanes is
    /// reconciled by real geometry rather than by ignoring the difference.
    ///
    /// The corner build sees an approach as a width, so before RM-89 there was nothing to reconcile WITH: a
    /// four-lane road and a two-lane road met as two widths and the two surplus lanes simply stopped. What this
    /// measures is that they now end over a taper whose length is the cited MUTCD relation for the approach's
    /// design speed, that two equal approaches butt-join with no taper at all, that a lane-WIDTH difference is
    /// absorbed by a shifting taper rather than a step, and -- the part § 10.13 insists on -- that a case with
    /// no real resolution is REFUSED with the reason named, never emitted with a plausible number filled in.
    ///
    /// One identity is asserted deliberately: dropping two lanes as a single 7.2 m offset gives exactly the same
    /// length as two consecutive 3.6 m drops. That is only true if both paths go through the same relation, so
    /// it is the check that would catch a second taper formula appearing in the junction code.
    ///
    /// Report lines are prefixed RCMAPPROACH. </summary>
    public static class ApproachMatchGate
    {
        // AUTHORED test approaches: a 3.6 m lane (the width the metric taper table is worked against), a
        // 60 km/h arterial and a 50 km/h local street -- both inside the low-speed branch of the relation, which
        // is the branch an urban junction actually uses.
        private const float Lane = 3.6f;
        private const float NarrowLane = 3.0f;
        private const float Arterial = 60f;
        private const float Local = 50f;

        [MenuItem("Window/Road City Master/Testing/Approach lane matching (unequal legs)", false, 1090)]
        private static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMAPPROACH BEGIN\n");
            List<string> failures = new List<string>();

            CaseWideMeetsNarrow(builder, failures);
            CaseEqualApproachesButtJoin(builder, failures);
            CaseLaneWidthDifferenceShifts(builder, failures);
            CaseSequentialDropsAgreeWithOneCombinedDrop(builder, failures);
            CaseRefusals(builder, failures);
            CaseRefusalReachesThePanel(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMAPPROACH FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMAPPROACH result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMAPPROACH END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A four-lane arterial (2 each way) meeting a two-lane street (1 each way). One lane each way
        /// crosses; the arterial's other two do not, and they end over the taper the relation gives for 7.2 m at
        /// 60 km/h -- 167.226 m, worked as W x V^2 / 155. The street is NOT widened to match: its surplus must be
        /// zero, because widening it would invent pavement and push its kerb line out for no reason. </summary>
        private static void CaseWideMeetsNarrow(StringBuilder builder, List<string> failures)
        {
            ApproachSection wide = new ApproachSection(2, 2, Lane, 0f, Arterial);
            ApproachSection narrow = new ApproachSection(1, 1, Lane, 0f, Local);
            ApproachMatchVerdict v = ApproachMatch.Match(wide, narrow);

            Expect(v.resolved, "wide/narrow: the match must resolve, got refusal '" + v.refusal + "'.", failures);
            Expect(!v.buttJoin, "wide/narrow: four lanes meeting two is not a butt-join.", failures);
            Expect(v.throughForward == 1 && v.throughBackward == 1,
                "wide/narrow: one lane must cross in each direction, got " + v.throughForward + "F / "
                + v.throughBackward + "B.", failures);
            Expect(v.surplusForwardA == 1 && v.surplusBackwardA == 1,
                "wide/narrow: the arterial must carry one surplus lane each way, got " + v.surplusForwardA
                + "F / " + v.surplusBackwardA + "B.", failures);
            Expect(v.surplusForwardB == 0 && v.surplusBackwardB == 0,
                "wide/narrow: the narrow street must have no surplus -- it must not be widened to match, got "
                + v.surplusForwardB + "F / " + v.surplusBackwardB + "B.", failures);

            float expected = 2f * Lane * Arterial * Arterial / 155f;
            Expect(Approx(v.dropTaperMetres, expected),
                "wide/narrow: the taper for a " + F(2f * Lane) + " m drop at " + F(Arterial) + " km/h must be "
                + F(expected) + " m, got " + F(v.dropTaperMetres) + " m.", failures);
            Expect(v.dropTaperMetres > 0f,
                "wide/narrow: the surplus lanes must end over a taper, not at a step.", failures);
            Expect(v.citation.Contains("155"),
                "wide/narrow: the length must carry its cited basis, citation is '" + v.citation + "'.", failures);
            Expect(v.widthShiftTaperMetres == 0f,
                "wide/narrow: the lane widths agree, so no shifting taper is needed, got "
                + F(v.widthShiftTaperMetres) + " m.", failures);

            builder.Append("RCMAPPROACH trace  4x2  ").Append(v.ToString()).Append("\n");
            builder.Append("RCMAPPROACH trace  cite  ").Append(v.citation).Append("\n");
        }

        /// <summary> Two identical approaches: nothing is dropped, nothing shifts, and the carriageways meet
        /// lane edge to lane edge. A taper here would be a defect -- it would stretch a junction that has no
        /// reason to be stretched. </summary>
        private static void CaseEqualApproachesButtJoin(StringBuilder builder, List<string> failures)
        {
            ApproachSection a = new ApproachSection(2, 2, Lane, 4f, Arterial);
            ApproachSection b = new ApproachSection(2, 2, Lane, 4f, Arterial);
            ApproachMatchVerdict v = ApproachMatch.Match(a, b);

            Expect(v.resolved && v.buttJoin,
                "equal: two identical approaches must butt-join, got resolved=" + v.resolved + " butt="
                + v.buttJoin + " refusal='" + v.refusal + "'.", failures);
            Expect(v.dropTaperMetres == 0f && v.widthShiftTaperMetres == 0f,
                "equal: a butt-join must produce no taper at all, got drop " + F(v.dropTaperMetres)
                + " m and shift " + F(v.widthShiftTaperMetres) + " m.", failures);
            Expect(v.surplusForwardA + v.surplusBackwardA + v.surplusForwardB + v.surplusBackwardB == 0,
                "equal: a butt-join must have no surplus lane on either approach.", failures);
            Expect(v.citation == "",
                "equal: a butt-join has no length to cite, citation is '" + v.citation + "'.", failures);

            builder.Append("RCMAPPROACH trace  2x2  ").Append(v.ToString()).Append("\n");
        }

        /// <summary> Equal counts but a 0.6 m difference in lane width: nothing is dropped, but the carriageway
        /// edge has to move, and a lateral move is a SHIFTING taper -- half the length of a lane drop for the
        /// same offset, which is the standard relation between the two. A step here would be a kink in the kerb
        /// line. </summary>
        private static void CaseLaneWidthDifferenceShifts(StringBuilder builder, List<string> failures)
        {
            ApproachSection a = new ApproachSection(2, 2, Lane, 0f, Arterial);
            ApproachSection b = new ApproachSection(2, 2, NarrowLane, 0f, Arterial);
            ApproachMatchVerdict v = ApproachMatch.Match(a, b);

            Expect(v.resolved && !v.buttJoin,
                "width shift: equal counts with different lane widths must resolve but must not butt-join, got "
                + "resolved=" + v.resolved + " butt=" + v.buttJoin + ".", failures);
            Expect(v.dropTaperMetres == 0f,
                "width shift: no lane is dropped, so there must be no drop taper, got "
                + F(v.dropTaperMetres) + " m.", failures);

            float difference = Lane - NarrowLane;
            float expected = 0.5f * difference * Arterial * Arterial / 155f;
            Expect(Approx(v.widthShiftTaperMetres, expected),
                "width shift: a " + F(difference) + " m width change at " + F(Arterial) + " km/h must shift over "
                + F(expected) + " m (half a lane-drop taper), got " + F(v.widthShiftTaperMetres) + " m.",
                failures);
            Expect(v.citation.Contains("shifting taper"),
                "width shift: the shift must be cited, citation is '" + v.citation + "'.", failures);

            builder.Append("RCMAPPROACH trace  width  ").Append(v.ToString()).Append("\n");
        }

        /// <summary> The identity that would catch a duplicated formula: two lanes dropped one after the other
        /// must occupy exactly the same length as one combined drop of both widths. It holds because the taper
        /// relation is linear in the offset width in BOTH of its branches -- so if the junction ever grew its own
        /// taper arithmetic, these two numbers would part company. </summary>
        private static void CaseSequentialDropsAgreeWithOneCombinedDrop(StringBuilder builder, List<string> failures)
        {
            List<float> stations = ApproachMatch.DropStations(2, Lane, Arterial);
            Expect(stations.Count == 2,
                "drop stations: two surplus lanes must produce two stations, got " + stations.Count + ".",
                failures);
            if (stations.Count != 2) { return; }

            float one = (float)LaneModel.TaperLengthMetres(Lane, Arterial);
            Expect(Approx(stations[1], one),
                "drop stations: the inner surplus lane must end one taper (" + F(one) + " m) from the junction, "
                + "got " + F(stations[1]) + " m.", failures);
            Expect(stations[0] > stations[1],
                "drop stations: the OUTERMOST lane must end furthest from the junction -- a lane cannot be "
                + "removed from under the middle of a traffic stream -- got outer " + F(stations[0])
                + " m and inner " + F(stations[1]) + " m.", failures);

            ApproachMatchVerdict combined = ApproachMatch.Match(
                new ApproachSection(2, 0, Lane, 0f, Arterial),
                new ApproachSection(0, 0, Lane, 0f, Arterial));
            Expect(!combined.resolved,
                "drop stations: an approach with no lanes at all must be refused, not matched.", failures);

            ApproachMatchVerdict twoLaneDrop = ApproachMatch.Match(
                new ApproachSection(3, 0, Lane, 0f, Arterial),
                new ApproachSection(1, 0, Lane, 0f, Arterial));
            Expect(Approx(twoLaneDrop.dropTaperMetres, stations[0]),
                "drop stations: one combined 2-lane drop (" + F(twoLaneDrop.dropTaperMetres)
                + " m) must equal two consecutive single-lane drops (" + F(stations[0])
                + " m) -- if these differ, a second taper formula has appeared.", failures);

            builder.Append("RCMAPPROACH trace  drops  outer ").Append(F(stations[0]))
                   .Append(" m  inner ").Append(F(stations[1]))
                   .Append(" m  combined ").Append(F(twoLaneDrop.dropTaperMetres)).Append(" m\n");
        }

        /// <summary> The cases with no real resolution. Each must come back refused, with the reason readable by
        /// the author who has to fix it, and with no length filled in -- § 10.13 applied at the junction. </summary>
        private static void CaseRefusals(StringBuilder builder, List<string> failures)
        {
            ApproachMatchVerdict noWidth = ApproachMatch.Match(
                new ApproachSection(2, 2, 0f, 0f, Arterial),
                new ApproachSection(1, 1, Lane, 0f, Local));
            Expect(!noWidth.resolved && noWidth.refusal.Contains("lane width"),
                "refusal: a zero lane width must be refused by name, got resolved=" + noWidth.resolved
                + " refusal='" + noWidth.refusal + "'.", failures);

            ApproachMatchVerdict noSpeed = ApproachMatch.Match(
                new ApproachSection(2, 2, Lane, 0f, 0f),
                new ApproachSection(1, 1, Lane, 0f, Local));
            Expect(!noSpeed.resolved && noSpeed.refusal.Contains("design speed"),
                "refusal: a lane drop with no design speed must be refused rather than given a plausible "
                + "length, got resolved=" + noSpeed.resolved + " refusal='" + noSpeed.refusal + "'.", failures);
            Expect(noSpeed.dropTaperMetres == 0f,
                "refusal: a refused match must not carry a taper length, got " + F(noSpeed.dropTaperMetres)
                + " m.", failures);

            ApproachMatchVerdict opposed = ApproachMatch.Match(
                new ApproachSection(2, 0, Lane, 0f, Arterial),
                new ApproachSection(0, 2, Lane, 0f, Arterial));
            Expect(!opposed.resolved && opposed.refusal.Contains("zero through lanes"),
                "refusal: two one-ways with no shared direction must be refused, got resolved="
                + opposed.resolved + " refusal='" + opposed.refusal + "'.", failures);

            builder.Append("RCMAPPROACH trace  refusals 3  ").Append(noSpeed.ToString()).Append("\n");
        }

        /// <summary> § 10.13's other half: a refusal is worthless if it stops at the return value. This walks the
        /// whole path the task promises -- refused verdict, entry carrying the object and the station, RM-52's
        /// panel rendering the reason where the author will read it -- and the converse, that a junction which
        /// reconciles contributes NOTHING, because a blank entry in the panel is as misleading as a missing one.
        ///
        /// The station is asserted specifically because a refusal the author cannot navigate to is a refusal they
        /// cannot act on, and the one way this number goes wrong is an unmeasured spline reporting a negative
        /// length. </summary>
        private static void CaseRefusalReachesThePanel(StringBuilder builder, List<string> failures)
        {
            ApproachMatchVerdict refused = ApproachMatch.Match(
                new ApproachSection(2, 2, Lane, 0f, 0f),
                new ApproachSection(1, 1, Lane, 0f, Local));

            RefusalEntry entry;
            bool reported = ApproachMatch.TryRefusal(refused, "isec.4", "Mill Street / Quay Road", 214.5, out entry);
            Expect(reported, "panel: a refused verdict must produce a panel entry.", failures);
            Expect(entry.objectName == "Mill Street / Quay Road",
                "panel: the entry must name the junction so the author knows which one, got '"
                + entry.objectName + "'.", failures);
            Expect(entry.objectId == "isec.4",
                "panel: the entry must carry the object id the panel selects by, got '" + entry.objectId + "'.",
                failures);
            Expect(Approx((float)entry.stationMetres, 214.5f),
                "panel: the entry must carry the station of the junction, got " + F((float)entry.stationMetres)
                + " m.", failures);
            Expect(entry.reason == refused.refusal,
                "panel: the entry must carry the verdict's own reason verbatim rather than a summary of it.",
                failures);

            RefusalEntry negative;
            ApproachMatch.TryRefusal(refused, "isec.5", "Unmeasured", -1.0, out negative);
            Expect(negative.stationMetres == 0.0,
                "panel: an unmeasured approach must report station 0, not a negative station the author cannot "
                + "navigate to, got " + F((float)negative.stationMetres) + " m.", failures);

            ApproachMatchVerdict fine = ApproachMatch.Match(
                new ApproachSection(2, 2, Lane, 0f, Arterial),
                new ApproachSection(2, 2, Lane, 0f, Arterial));
            RefusalEntry none;
            bool quiet = ApproachMatch.TryRefusal(fine, "isec.9", "Cross Keys", 12.0, out none);
            Expect(!quiet, "panel: a junction that reconciles must contribute no refusal.", failures);
            Expect(none.reason == "" && none.objectName == "",
                "panel: a resolved verdict must yield an EMPTY entry, so a caller ignoring the return value "
                + "publishes a blank line rather than a phantom refusal, got '" + none.objectName + "' / '"
                + none.reason + "'.", failures);

            BuildStatus status = new BuildStatus();
            List<RefusalEntry> entries = new List<RefusalEntry> { entry };
            status.RecordBuild(new System.DateTime(2026, 9, 1, 0, 0, 0, System.DateTimeKind.Utc),
                90UL, BuildOutcome.Success, entries);
            string text = status.RenderText();
            Expect(text.Contains("Mill Street / Quay Road") && text.Contains("design speed"),
                "panel: RM-52's rendered panel must show the junction and the reason, got:\n" + text, failures);
            Expect(status.lastOutcome == BuildOutcome.RefusedSome,
                "panel: a build carrying a junction refusal must not read as a clean success, got "
                + status.lastOutcome + ".", failures);

            builder.Append("RCMAPPROACH trace  panel  ").Append(text.Replace("\n", " | ")).Append("\n");
        }

        /// <summary> C-06: the same two approaches must reconcile to a byte-identical verdict every time. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            string first = null;
            bool stable = true;
            for (int pass = 0; pass < 3; pass++)
            {
                ApproachMatchVerdict v = ApproachMatch.Match(
                    new ApproachSection(3, 2, Lane, 4f, Arterial),
                    new ApproachSection(1, 1, NarrowLane, 0f, Local));
                string s = v.ToString();
                if (pass == 0) { first = s; }
                else if (s != first) { stable = false; }
            }
            Expect(stable, "determinism: the verdict changed between identical passes.", failures);
            builder.Append("RCMAPPROACH trace  deterministic  ").Append(first).Append("\n");
        }

        private static bool Approx(float a, float b)
        {
            return Mathf.Abs(a - b) <= 0.01f;
        }

        private static string F(float f)
        {
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
