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
    /// <summary> RM-59's gate: proves a divided road is one alignment carrying two carriageways rather than a
    /// wide road with a stripe down the middle, and -- the property the spec actually names -- that closing the
    /// median and levelling the two vertical profiles reproduces an undivided road of the same total width,
    /// vertex for vertex, so divided mode is a GENERALISATION and not a second code path.
    ///
    /// The reduction case is the one that would catch a duplicated emitter, and it is not circular: the
    /// reference it compares against is <see cref="DividedRoad.PlainSection"/>, a crown falling away at one
    /// cross-slope, written plainly and used by nothing else. Everything the sweep emits comes from
    /// <see cref="DividedRoad.Section"/>.
    ///
    /// The rest measures what § 10.25 says must not be discovered as a bug later: the two carriageways keeping
    /// their own elevations on a side slope with the median absorbing the difference, an offset curve
    /// continuous in its first derivative (a kinked one is measured too, so the check is known to have teeth),
    /// a gradient verdict PER carriageway against the road class maximum, a footprint that is the whole
    /// assembly rather than one carriageway, and a divided-to-undivided transition whose authored length is
    /// checked against the metric MUTCD taper relation instead of accepted on trust.
    ///
    /// Report lines are prefixed RCMDIVIDED. The swept mesh these numbers drive is confirmed in the scene, not
    /// asserted here. </summary>
    public static class DividedCarriagewayGate
    {
        // The test road: an AUTHORED rural divided highway 400 m long -- 3.6 m lanes, two per carriageway, a
        // 9 m median (4.5 m half-gap), 3 m outside shoulders, 2% normal crown.
        private const float RoadLength = 400f;
        private const float MedianHalfGap = 4.5f;
        private const float CarriagewayHalfWidth = 3.6f;
        private const float Shoulder = 3f;
        private const float CrossSlope = 0.02f;
        private const int PointsPerCarriageway = 4;
        private const float VertexTolerance = 1e-4f;
        [MenuItem("Window/Road City Master/Testing/Divided carriageways")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.DividedCarriagewayGate.RunBatch. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMDIVIDED report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMDIVIDED BEGIN\n");
            List<string> failures = new List<string>();

            CaseZeroOffsetsReproduceUndivided(builder, failures);
            CaseSplitLevelKeepsBothElevations(builder, failures);
            CaseMedianFollowsBothEdges(builder, failures);
            CaseOffsetCurveIsC1(builder, failures);
            CaseGradientCheckedPerCarriageway(builder, failures);
            CaseFootprintIsTheWholeAssembly(builder, failures);
            CaseCollapsedCarriagewayIsRefused(builder, failures);
            CaseTransitionLengthIsChecked(builder, failures);
            CaseTransitionEndsUndivided(builder, failures);
            CaseDeterministic(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMDIVIDED FAIL   ").Append(failures[i]).Append("\n");
            }
            builder.Append("RCMDIVIDED result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  cases 10  failures ").Append(failures.Count).Append("\n");
            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A divided road built with real numbers, for every case that needs one. </summary>
        private static DividedRoadProfile Divided()
        {
            DividedRoadProfile p = DividedRoad.Default();
            p.medianHalfGap = DividedRoad.ConstantHalfWidth(MedianHalfGap);
            p.carriagewayHalfWidth = DividedRoad.ConstantHalfWidth(CarriagewayHalfWidth);
            p.leftCrossSlope = CrossSlope;
            p.rightCrossSlope = CrossSlope;
            p.shoulderWidthMetres = Shoulder;
            p.pointsPerCarriageway = PointsPerCarriageway;
            return p;
        }
        /// <summary> THE property RM-59 names. Close the median, level the two vertical profiles, and the
        /// general emitter must produce exactly the undivided road of that total width -- same vertex count,
        /// same order, same positions -- against a reference crown computed independently. The shared inner edge
        /// is emitted once, which is why the merge count is checked too: two vertices at the same place would
        /// be a seam down the centre of a road that has no centre joint. The still-divided section is measured
        /// against the same reference so a comparison that passes everything cannot pass this case. </summary>
        private static void CaseZeroOffsetsReproduceUndivided(StringBuilder builder, List<string> failures)
        {
            float carriageway = CarriagewayHalfWidth * 2f;
            DividedRoadProfile closed = Divided();
            closed.medianHalfGap = DividedRoad.ConstantHalfWidth(0f);

            DividedSection section = DividedRoad.Section(closed, 100f);
            SectionPoint[] reference =
                DividedRoad.PlainSection(carriageway, CrossSlope, PointsPerCarriageway * 2);

            Expect(!section.refused, "reduction: the zero-gap section was refused: " + section.refusal, failures);
            if (section.refused) { return; }

            string difference;
            Expect(DividedRoad.SectionsMatch(section.points, reference, VertexTolerance, out difference),
                "reduction: the zero-gap divided road is not the undivided road -- " + difference, failures);
            Expect(section.points.Length == PointsPerCarriageway * 2 + 1,
                "reduction: " + section.points.Length + " vertices where an undivided road of "
                + (PointsPerCarriageway * 2) + " intervals has " + (PointsPerCarriageway * 2 + 1), failures);
            Expect(section.medianCount == 0,
                "reduction: a closed median still contributed " + section.medianCount + " vertices", failures);
            Expect(section.coalescedPoints == 1,
                "reduction: " + section.coalescedPoints + " vertices merged where exactly the shared inner edge "
                + "should have", failures);
            Expect(section.leftInnerEdgeIndex == section.rightInnerEdgeIndex,
                "reduction: the two carriageways report different inner edges on a road that has no median",
                failures);
            Expect(Mathf.Abs(section.totalWidthMetres - carriageway * 2f) < 1e-3f,
                "reduction: total width is " + F(section.totalWidthMetres) + " m, not the "
                + F(carriageway * 2f) + " m the two carriageways make", failures);

            // The same reference against the divided road: this must NOT match, or the comparison proves nothing.
            DividedSection open = DividedRoad.Section(Divided(), 100f);
            string ignored;
            Expect(!DividedRoad.SectionsMatch(open.points, reference, VertexTolerance, out ignored),
                "reduction: a road with a 9 m median compared equal to an undivided one, so the comparison is "
                + "not measuring anything", failures);

            builder.Append("RCMDIVIDED ").Append("reduction".PadRight(22))
                   .Append(section.points.Length).Append(" vertices, merged ").Append(section.coalescedPoints)
                   .Append(", total ").Append(F(section.totalWidthMetres)).Append(" m\n");
        }
        /// <summary> The split-level section on a side slope -- the case a single surface with a painted median
        /// cannot express at all. Each carriageway sits at its own elevation, its inner edge exactly where its
        /// own vertical profile says, and the median floor spans the difference, so the drop between the two
        /// appears as a measurable cross-fall across the median rather than being averaged away. </summary>
        private static void CaseSplitLevelKeepsBothElevations(StringBuilder builder, List<string> failures)
        {
            const float Drop = 2f;   // the left carriageway sits 2 m down the slope from the right
            DividedRoadProfile p = Divided();
            p.leftHeightOffset = AnimationCurve.Constant(0f, RoadLength, -Drop);
            p.rightHeightOffset = AnimationCurve.Constant(0f, RoadLength, 0f);

            DividedSection s = DividedRoad.Section(p, 200f);
            Expect(!s.refused, "split-level: the section was refused: " + s.refusal, failures);
            if (s.refused) { return; }

            Expect(s.points.Length == (PointsPerCarriageway + 1) * 2 + 1,
                "split-level: " + s.points.Length + " vertices where two carriageways plus a median floor point "
                + "make " + ((PointsPerCarriageway + 1) * 2 + 1), failures);
            Expect(s.coalescedPoints == 0,
                "split-level: " + s.coalescedPoints + " vertices merged, so a real median was collapsed", failures);
            Expect(s.medianCount == 1,
                "split-level: the median contributed " + s.medianCount + " vertices, not the one floor point",
                failures);
            if (s.points.Length != (PointsPerCarriageway + 1) * 2 + 1) { return; }

            SectionPoint leftInner = s.points[s.leftInnerEdgeIndex];
            SectionPoint floor = s.points[s.medianFloorIndex];
            SectionPoint rightInner = s.points[s.rightInnerEdgeIndex];

            Expect(Mathf.Abs(leftInner.lateral + MedianHalfGap) < 1e-3f,
                "split-level: the left inner edge is at " + F(leftInner.lateral) + " m, not at -"
                + F(MedianHalfGap) + " m", failures);
            Expect(Mathf.Abs(rightInner.lateral - MedianHalfGap) < 1e-3f,
                "split-level: the right inner edge is at " + F(rightInner.lateral) + " m, not at +"
                + F(MedianHalfGap) + " m", failures);
            Expect(Mathf.Abs(leftInner.height + Drop) < 1e-3f,
                "split-level: the left inner edge is at " + F(leftInner.height) + " m, not the authored -"
                + F(Drop) + " m", failures);
            Expect(Mathf.Abs(rightInner.height) < 1e-3f,
                "split-level: the right inner edge is at " + F(rightInner.height) + " m, not the authored 0 m",
                failures);
            Expect(Mathf.Abs(floor.lateral) < 1e-3f && Mathf.Abs(floor.height + Drop * 0.5f) < 1e-3f,
                "split-level: the median floor is at (" + F(floor.lateral) + ", " + F(floor.height)
                + ") rather than midway between the two inner edges", failures);

            float medianCrossfall = (rightInner.height - leftInner.height) / (MedianHalfGap * 2f);
            Expect(Mathf.Abs(medianCrossfall - Drop / (MedianHalfGap * 2f)) < 1e-4f,
                "split-level: the median absorbs a cross-fall of " + F(medianCrossfall) + " where the authored "
                + F(Drop) + " m over " + F(MedianHalfGap * 2f) + " m needs "
                + F(Drop / (MedianHalfGap * 2f)), failures);

            builder.Append("RCMDIVIDED ").Append("split-level".PadRight(22))
                   .Append("inner edges ").Append(F(leftInner.height)).Append(" m / ")
                   .Append(F(rightInner.height)).Append(" m, median cross-fall ").Append(F(medianCrossfall))
                   .Append("\n");
        }
        /// <summary> The offsets are functions of STATION, not constants -- a median that opens for a turning
        /// lane and closes again. At every station the inner edges must sit exactly where the offset curve says,
        /// the outer edges must have been pushed outward by the same amount (a widening median must not eat lane
        /// width), and the median floor must follow both edges. Where the curve collapses, the section must be
        /// undivided there rather than carrying a zero-width median. </summary>
        private static void CaseMedianFollowsBothEdges(StringBuilder builder, List<string> failures)
        {
            float carriageway = CarriagewayHalfWidth * 2f;
            DividedRoadProfile p = Divided();
            p.medianHalfGap = VariableWidthProfile.Median(50f, 350f, MedianHalfGap, 30f);

            int divided = 0;
            int undivided = 0;
            for (float station = 0f; station <= RoadLength; station += 5f)
            {
                DividedSection s = DividedRoad.Section(p, station);
                if (s.refused)
                {
                    Expect(false, "median-follows: refused at " + F(station) + " m: " + s.refusal, failures);
                    return;
                }
                float gap = p.medianHalfGap.EmitsAt(station) ? p.medianHalfGap.HalfWidthAt(station) : 0f;
                SectionPoint outerLeft = s.points[0];
                SectionPoint innerLeft = s.points[s.leftInnerEdgeIndex];
                SectionPoint innerRight = s.points[s.rightInnerEdgeIndex];
                SectionPoint outerRight = s.points[s.points.Length - 1];
                string at = " at " + F(station) + " m";

                Expect(Mathf.Abs(innerLeft.lateral + gap) < 1e-3f && Mathf.Abs(innerRight.lateral - gap) < 1e-3f,
                    "median-follows: inner edges at " + F(innerLeft.lateral) + " / " + F(innerRight.lateral)
                    + " m where the offset curve says " + F(gap) + " m" + at, failures);
                Expect(Mathf.Abs(outerLeft.lateral + (gap + carriageway)) < 1e-3f
                    && Mathf.Abs(outerRight.lateral - (gap + carriageway)) < 1e-3f,
                    "median-follows: the carriageways were not pushed outward -- outer edges at "
                    + F(outerLeft.lateral) + " / " + F(outerRight.lateral) + " m where "
                    + F(gap + carriageway) + " m is expected" + at, failures);
                Expect(Mathf.Abs(s.totalWidthMetres - 2f * (gap + carriageway)) < 1e-3f,
                    "median-follows: total width " + F(s.totalWidthMetres) + " m does not grow with the median"
                    + at, failures);

                if (gap > p.coalesceTolerance)
                {
                    divided++;
                    Expect(s.medianCount == 1, "median-follows: a " + F(gap * 2f) + " m median contributed "
                        + s.medianCount + " floor vertices" + at, failures);
                    if (s.medianCount == 1)
                    {
                        SectionPoint floor = s.points[s.medianFloorIndex];
                        Expect(Mathf.Abs(floor.height - (innerLeft.height + innerRight.height) * 0.5f) < 1e-4f,
                            "median-follows: the floor at " + F(floor.height)
                            + " m does not follow both edges" + at, failures);
                    }
                }
                else
                {
                    undivided++;
                    Expect(s.medianCount == 0, "median-follows: a collapsed median still emitted "
                        + s.medianCount + " vertices" + at, failures);
                    Expect(s.coalescedPoints == 1, "median-follows: the two inner edges did not become one "
                        + "vertex where the median has closed" + at, failures);
                    Expect(s.leftInnerEdgeIndex == s.rightInnerEdgeIndex && s.medianFloorIndex < 0,
                        "median-follows: a closed median still reports two separate inner edges or a floor"
                        + at, failures);
                }
            }

            Expect(divided > 0 && undivided > 0, "median-follows: the sampled road was never both divided and "
                + "undivided, so the station-varying offset was not exercised", failures);
            builder.Append("RCMDIVIDED ").Append("median-follows".PadRight(22))
                   .Append(divided).Append(" divided stations, ").Append(undivided).Append(" undivided\n");
        }
        /// <summary> The offset curve must be continuous in its first derivative or the carriageway edge creases
        /// at every control point. A deliberately kinked curve is measured beside the authored one so the check
        /// is known to have teeth: closing the same median with a STRAIGHT taper -- the obvious way to author it,
        /// and wrong -- puts a corner where the hold meets the taper, and the slope there jumps by the taper's
        /// whole slope instead of easing into it. The eased curve's jump comes only from its curvature, which is
        /// 6 x 4.5 m / (30 m)^2 = 0.03 per metre, so the two are an order apart rather than marginally so.
        /// </summary>
        private static void CaseOffsetCurveIsC1(StringBuilder builder, List<string> failures)
        {
            VariableWidthProfile smooth = VariableWidthProfile.Median(50f, 350f, MedianHalfGap, 30f);
            float atSmooth;
            float smoothJump = DividedRoad.MaxOffsetSlopeJump(smooth, 0f, RoadLength, 1f, out atSmooth);

            const float TaperSlope = MedianHalfGap / 10f;   // the same 4.5 m closed over 10 m, straight
            VariableWidthProfile kinked;
            kinked.halfWidthOverDistance = new AnimationCurve(
                new Keyframe(0f, MedianHalfGap, 0f, 0f),
                new Keyframe(200f, MedianHalfGap, 0f, -TaperSlope),
                new Keyframe(210f, 0f, -TaperSlope, 0f));
            kinked.minEmitHalfWidth = 0.001f;
            float atKink;
            float kinkJump = DividedRoad.MaxOffsetSlopeJump(kinked, 0f, RoadLength, 1f, out atKink);

            Expect(smoothJump < 0.06f, "C1: the authored offset curve's slope jumps " + F(smoothJump)
                + " per metre at " + F(atSmooth) + " m, which creases the carriageway edge", failures);
            Expect(kinkJump > 0.15f && kinkJump > smoothJump * 4f,
                "C1: a straight taper with a corner at " + F(atKink) + " m only jumped " + F(kinkJump)
                + " against the eased curve's " + F(smoothJump) + ", so the check cannot see a crease it should "
                + "reject", failures);

            builder.Append("RCMDIVIDED ").Append("offset C1".PadRight(22))
                   .Append("eased jump ").Append(F(smoothJump)).Append(" vs straight taper ").Append(F(kinkJump))
                   .Append(" per metre\n");
        }

        /// <summary> Each carriageway is checked against the road class's maximum gradient SEPARATELY, because a
        /// divided road can be legal on one side and illegal on the other: the alignment climbs at 4%, the left
        /// carriageway adds 5% of its own and breaks the 8% arterial limit, the right sheds 2% and stays inside
        /// it. One verdict for the pair would report the road as legal and hide the failing side. </summary>
        private static void CaseGradientCheckedPerCarriageway(StringBuilder builder, List<string> failures)
        {
            float limit = new City.RoadClass().maxGradient;   // 8% -- the class owns the limit, not this gate
            AnimationCurve alignment = AnimationCurve.Linear(0f, 0f, RoadLength, 0.04f * RoadLength);
            AnimationCurve left = AnimationCurve.Linear(0f, 0f, RoadLength, 0.05f * RoadLength);
            AnimationCurve right = AnimationCurve.Linear(0f, 0f, RoadLength, -0.02f * RoadLength);

            GradientVerdict leftVerdict =
                DividedRoad.CheckGradient(alignment, left, 0f, RoadLength, 5f, limit);
            GradientVerdict rightVerdict =
                DividedRoad.CheckGradient(alignment, right, 0f, RoadLength, 5f, limit);

            Expect(Mathf.Abs(leftVerdict.worstGradient - 0.09f) < 1e-3f,
                "per-carriageway gradient: the left carriageway measured " + F(leftVerdict.worstGradient)
                + " where 4% of alignment plus 5% of its own profile is 9%", failures);
            Expect(!leftVerdict.withinLimit, "per-carriageway gradient: " + F(leftVerdict.worstGradient)
                + " passed a limit of " + F(limit), failures);
            Expect(Mathf.Abs(rightVerdict.worstGradient - 0.02f) < 1e-3f,
                "per-carriageway gradient: the right carriageway measured " + F(rightVerdict.worstGradient)
                + " where 4% less 2% is 2%", failures);
            Expect(rightVerdict.withinLimit, "per-carriageway gradient: " + F(rightVerdict.worstGradient)
                + " was refused against a limit of " + F(limit), failures);
            Expect(Mathf.Abs(leftVerdict.limit - limit) < 1e-6f && Mathf.Abs(rightVerdict.limit - limit) < 1e-6f,
                "per-carriageway gradient: a verdict did not echo the limit it was measured against", failures);

            builder.Append("RCMDIVIDED ").Append("gradient per side".PadRight(22))
                   .Append("left ").Append(F(leftVerdict.worstGradient)).Append(" refused, right ")
                   .Append(F(rightVerdict.worstGradient)).Append(" accepted, limit ").Append(F(limit))
                   .Append("\n");
        }
        /// <summary> RM-38 terraforms the footprint, and the footprint of a divided road is BOTH carriageways
        /// plus the median plus each side's shoulder. If the flattened band were sized from one carriageway the
        /// median gap would sit outside the band, the blend would run through it, and the gap would fill with
        /// spoil instead of holding a trough. So the footprint is measured against the total plus two shoulders
        /// at every station, and it must track the median as it opens rather than being a fixed number. </summary>
        private static void CaseFootprintIsTheWholeAssembly(StringBuilder builder, List<string> failures)
        {
            DividedRoadProfile p = Divided();
            p.medianHalfGap = VariableWidthProfile.Median(50f, 350f, MedianHalfGap, 30f);

            float oneCarriageway = CarriagewayHalfWidth * 2f;
            int mismatches = 0;
            int tooNarrow = 0;
            float widest = 0f;
            float narrowest = float.MaxValue;
            for (float station = 0f; station <= RoadLength; station += 5f)
            {
                float total = DividedRoad.TotalWidthAt(p, station);
                float footprint = DividedRoad.FootprintWidthAt(p, station);
                if (Mathf.Abs(footprint - (total + 2f * Shoulder)) > VertexTolerance) { mismatches++; }
                if (footprint <= oneCarriageway) { tooNarrow++; }
                widest = Mathf.Max(widest, total);
                narrowest = Mathf.Min(narrowest, total);
            }

            Expect(mismatches == 0, "footprint: " + mismatches + " stations where the terraform width was not "
                + "the total plus both shoulders", failures);
            Expect(tooNarrow == 0, "footprint: " + tooNarrow + " stations where the terraform width did not even "
                + "cover one " + F(oneCarriageway) + " m carriageway", failures);
            Expect(Mathf.Abs(narrowest - oneCarriageway * 2f) < 1e-3f, "footprint: with the median closed the "
                + "total was " + F(narrowest) + " m, not the " + F(oneCarriageway * 2f) + " m of two "
                + "carriageways", failures);
            Expect(Mathf.Abs((widest - narrowest) - MedianHalfGap * 2f) < 1e-3f, "footprint: opening a "
                + F(MedianHalfGap * 2f) + " m median only grew the total by " + F(widest - narrowest)
                + " m, so the footprint is not tracking the median", failures);

            builder.Append("RCMDIVIDED ").Append("footprint".PadRight(22))
                   .Append("total ").Append(F(narrowest)).Append(" m to ").Append(F(widest))
                   .Append(" m, terraform ").Append(F(widest + 2f * Shoulder)).Append(" m at the widest\n");
        }

        /// <summary> A carriageway authored down to nothing is refused with the station and the width in the
        /// sentence, not emitted as a zero-area strip that later shows up as a pinched, shaded sliver in the
        /// scene. The healthy road is measured at the same station so the refusal is known to come from the
        /// collapse rather than from the station being out of range. </summary>
        private static void CaseCollapsedCarriagewayIsRefused(StringBuilder builder, List<string> failures)
        {
            const float Station = 120f;
            DividedRoadProfile collapsed = Divided();
            collapsed.carriagewayHalfWidth = DividedRoad.ConstantHalfWidth(0f);
            DividedSection bad = DividedRoad.Section(collapsed, Station);
            DividedSection good = DividedRoad.Section(Divided(), Station);

            Expect(bad.refused, "collapse: a carriageway of zero half-width was emitted instead of refused",
                failures);
            Expect(bad.points == null, "collapse: a refused section still handed back vertices", failures);
            Expect(bad.leftInnerEdgeIndex < 0 && bad.rightInnerEdgeIndex < 0 && bad.medianFloorIndex < 0,
                "collapse: a refused section reported vertex indices that address nothing", failures);
            Expect(bad.refusal != null && bad.refusal.Contains("120 m") && bad.refusal.Contains("collapsed"),
                "collapse: the refusal did not name the station and what went wrong: " + bad.refusal, failures);
            Expect(!good.refused && good.points != null, "collapse: the healthy road was refused at the same "
                + "station, so the refusal is not about the collapse", failures);

            builder.Append("RCMDIVIDED ").Append("collapse refused".PadRight(22))
                   .Append("refused at ").Append(F(Station)).Append(" m, healthy road still emits ")
                   .Append(good.points.Length).Append(" vertices\n");
        }

        /// <summary> Closing a median is a lateral shift of both carriageways, and a shift needs a taper long
        /// enough for the speed. The lengths come from the metric MUTCD taper rules, which now live in one place --
        /// <c>LaneModel.TaperLengthMetres</c>, which <c>DividedRoad</c> delegates to: L = 0.62 x W x V for 70 km/h and
        /// above, L = W x V^2 / 155 below it. So moving each carriageway 4.5 m at 110 km/h needs 306.9 m and the same
        /// shift at 50 km/h needs 72.58 m. The high-speed figure was 297 m while the constant was a rounded 0.6; 0.62
        /// is the exact conversion of the customary L = W x V (1/1.609344 = 0.6214), and the rounded value was
        /// shortening the taper. A 100 m transition at highway speed is refused with both the authored and the
        /// required length in the sentence, so the author is told what to change.
        /// </summary>
        private static void CaseTransitionLengthIsChecked(StringBuilder builder, List<string> failures)
        {
            const float Shift = MedianHalfGap;
            float highway = DividedRoad.MinimumTransitionLengthMetres(Shift, 110f);
            float urban = DividedRoad.MinimumTransitionLengthMetres(Shift, 50f);
            TransitionVerdict tooShort = DividedRoad.CheckTransition(100f, Shift, 110f);
            TransitionVerdict adequate = DividedRoad.CheckTransition(320f, Shift, 110f);

            Expect(Mathf.Abs(highway - 306.9f) < 0.01f, "transition: 4.5 m at 110 km/h wanted " + F(highway)
                + " m, not the 306.9 m the taper rule gives", failures);
            Expect(Mathf.Abs(urban - 72.581f) < 0.01f, "transition: 4.5 m at 50 km/h wanted " + F(urban)
                + " m, not the 72.58 m the taper rule gives", failures);
            Expect(urban < highway, "transition: the slow-speed taper came out no shorter than the highway one",
                failures);
            Expect(!tooShort.acceptable, "transition: a 100 m taper was accepted for a 306.9 m shift", failures);
            Expect(tooShort.refusal != null && tooShort.refusal.Contains("100 m")
                && tooShort.refusal.Contains("306.9 m"), "transition: the refusal did not quote both the authored "
                + "and the required length: " + tooShort.refusal, failures);
            Expect(adequate.acceptable && adequate.refusal == "", "transition: a 320 m taper was refused for a "
                + "306.9 m requirement", failures);

            builder.Append("RCMDIVIDED ").Append("transition length".PadRight(22))
                   .Append("110 km/h needs ").Append(F(highway)).Append(" m, 50 km/h needs ").Append(F(urban))
                   .Append(" m, 100 m refused\n");
        }
        /// <summary> The road the spec asks for: divided, then closing back to undivided before a junction. Past
        /// the transition the general emitter must be producing the plain undivided crown again -- the same
        /// reduction as case 1, but reached by driving along the road instead of by authoring a closed median.
        /// The two vertical profiles are checked separately from the plan: they still differ by 2 m where the
        /// median is open, and they have met by the time the median has closed, because carriageways that arrive
        /// at a junction at different levels do not join. </summary>
        private static void CaseTransitionEndsUndivided(StringBuilder builder, List<string> failures)
        {
            const float DividedEnd = 200f;
            const float TransitionLength = 306.9f;   // case 8's required length for this shift at 110 km/h
            const float Drop = 2f;
            const float Undivided = DividedEnd + TransitionLength + 20f;

            DividedRoadProfile p = Divided();
            p.medianHalfGap = DividedRoad.TransitionToUndivided(MedianHalfGap, DividedEnd, TransitionLength);
            p.leftHeightOffset = new AnimationCurve(
                new Keyframe(0f, -Drop, 0f, 0f),
                new Keyframe(DividedEnd, -Drop, 0f, 0f),
                new Keyframe(DividedEnd + TransitionLength, 0f, 0f, 0f));
            p.rightHeightOffset = AnimationCurve.Constant(0f, Undivided, 0f);

            DividedSection after = DividedRoad.Section(p, Undivided);
            SectionPoint[] plain = DividedRoad.PlainSection(CarriagewayHalfWidth * 2f, CrossSlope,
                PointsPerCarriageway * 2);
            string difference = "the section past the transition was refused";
            bool matches = false;
            if (!after.refused)
            {
                matches = DividedRoad.SectionsMatch(after.points, plain, VertexTolerance, out difference);
            }

            float atJump;
            float jump = DividedRoad.MaxOffsetSlopeJump(p.medianHalfGap, 0f, Undivided, 1f, out atJump);
            float openWidth = DividedRoad.TotalWidthAt(p, DividedEnd);
            float closedWidth = DividedRoad.TotalWidthAt(p, Undivided);

            Expect(matches, "transition end: past the taper the road was not the plain undivided crown: "
                + difference, failures);
            Expect(after.medianCount == 0 && after.medianFloorIndex < 0, "transition end: a median floor vertex "
                + "survived past the transition", failures);
            Expect(Mathf.Abs(openWidth - 2f * (MedianHalfGap + CarriagewayHalfWidth * 2f)) < 1e-3f,
                "transition end: the median had already started closing at " + F(DividedEnd) + " m -- width "
                + F(openWidth) + " m", failures);
            Expect(Mathf.Abs(closedWidth - CarriagewayHalfWidth * 4f) < 1e-3f, "transition end: closing the "
                + "median left the road " + F(closedWidth) + " m wide instead of " + F(CarriagewayHalfWidth * 4f)
                + " m, so width was invented to fill the gap", failures);
            Expect(jump < 0.01f, "transition end: the closing ramp creases -- slope jumps " + F(jump)
                + " per metre at " + F(atJump) + " m", failures);
            Expect(DividedRoad.HeightsMeetAt(p, Undivided, 0.001f), "transition end: the two carriageways had "
                + "not come to the same level by the time the median closed", failures);
            Expect(!DividedRoad.HeightsMeetAt(p, DividedEnd, 0.001f), "transition end: the split-level test "
                + "cannot tell a " + F(Drop) + " m height difference apart from none", failures);

            builder.Append("RCMDIVIDED ").Append("transition to plain".PadRight(22))
                   .Append(F(openWidth)).Append(" m divided closes to ").Append(F(closedWidth))
                   .Append(" m over ").Append(F(TransitionLength)).Append(" m, ramp jump ").Append(F(jump))
                   .Append("\n");
        }

        /// <summary> C-06: the same station must give the same vertices, exactly, every time. Compared with ==
        /// rather than a tolerance, because a tolerance here would hide the very drift the constraint forbids.
        /// The vertex indices the median content in RM-60 hangs off are compared too -- a stable position with a
        /// wandering index would move a barrier without moving the road. </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            DividedRoadProfile p = Divided();
            p.medianHalfGap = VariableWidthProfile.Median(50f, 350f, MedianHalfGap, 30f);
            p.leftHeightOffset = AnimationCurve.Linear(0f, -1.5f, RoadLength, 0.5f);
            p.rightHeightOffset = AnimationCurve.Linear(0f, 0f, RoadLength, 1f);

            int countDrift = 0;
            int vertexDrift = 0;
            int indexDrift = 0;
            int stations = 0;
            for (float station = 0f; station <= RoadLength; station += 25f)
            {
                DividedSection a = DividedRoad.Section(p, station);
                DividedSection b = DividedRoad.Section(p, station);
                stations++;
                if (a.refused || b.refused || a.points.Length != b.points.Length) { countDrift++; continue; }
                if (a.leftInnerEdgeIndex != b.leftInnerEdgeIndex
                    || a.rightInnerEdgeIndex != b.rightInnerEdgeIndex
                    || a.medianFloorIndex != b.medianFloorIndex) { indexDrift++; }
                for (int i = 0; i < a.points.Length; i++)
                {
                    if (a.points[i].lateral != b.points[i].lateral || a.points[i].height != b.points[i].height)
                    {
                        vertexDrift++;
                    }
                }
            }

            Expect(countDrift == 0, "determinism: " + countDrift + " stations changed vertex count between two "
                + "identical calls", failures);
            Expect(vertexDrift == 0, "determinism: " + vertexDrift + " vertices moved between two identical "
                + "calls", failures);
            Expect(indexDrift == 0, "determinism: " + indexDrift + " stations moved an inner-edge or median-floor "
                + "index between two identical calls", failures);

            builder.Append("RCMDIVIDED ").Append("deterministic".PadRight(22))
                   .Append(stations).Append(" stations, twice each, identical\n");
        }

        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition) { failures.Add(failureMessage); }
        }

        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif
