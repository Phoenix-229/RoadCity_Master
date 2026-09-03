#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using RoadCityMaster.CrossSection;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-60's gate: proves what a median holds, how its barrier ends, and whether its trough drains --
    /// without a scene. The width bands are asserted to be a TABLE with a stated basis per row and one lookup that
    /// picks the row, so "what is in this median" cannot drift between features. The barrier's ends are asserted to
    /// be resolved for every combination of room, speed and ground the ladder can see: no input produces a placed
    /// run with an unresolved end, which is the blunt-rail defect the task exists to prevent, and the end object's
    /// placement is asserted to be RM-26's own computation rather than a second implementation. The trough is
    /// asserted to be shaped by its side slopes and depth cap, to take its outfall verdict from RM-30's code, and to
    /// be refused BY STATION where it has nowhere to drain. Planting is refused with the width quoted, while the
    /// through-median sight line is reported and never refused. Finally a width change is asserted to re-solve all
    /// three, leaving nothing of the previous median behind. Report lines are prefixed RCMMEDIAN. </summary>
    public static class CityMedianContentGate
    {
        private const float Highway = 110f;

        [MenuItem("Window/Road City Master/Testing/City median content (bands / terminals / trough)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityMedianContentGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMMEDIAN report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMMEDIAN BEGIN\n");
            List<string> failures = new List<string>();

            CaseWidthBandsAreATable(builder, failures);
            CaseNarrowMedianGetsTerminatedBarrier(builder, failures);
            CaseBluntEndIsNeverEmitted(builder, failures);
            CaseTerminalPlacementIsRM26s(builder, failures);
            CaseFlareRatesComeFromTheTable(builder, failures);
            CaseWideMedianTroughIsShapedAndDrains(builder, failures);
            CaseTroughWithNoOutfallIsRefused(builder, failures);
            CasePlantingRefusedWithWidthQuoted(builder, failures);
            CaseSightLineIsReportedNotRefused(builder, failures);
            CaseWidthChangeResolvesEverything(builder, failures);

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append("RCMMEDIAN FAIL   ").Append(failures[i]).Append("\n");
            }
            builder.Append("RCMMEDIAN result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  cases 10  failures ").Append(failures.Count).Append("\n");
            report = builder.ToString();
            return failures.Count == 0;
        }
        /// <summary> Case 1: what a median holds is a TABLE, not a chain of ifs. Rows ascend, each row states a
        /// different kind, each row carries the basis it was chosen from, and one lookup picks the row -- inclusive at
        /// the threshold. The planting minimum is asserted to be READ from that table and to agree with the setback it
        /// is built from, so there is one such number in the tool instead of two that can drift apart. </summary>
        private static void CaseWidthBandsAreATable(StringBuilder builder, List<string> failures)
        {
            Expect(MedianContent.BandCount == 5, "band table has " + MedianContent.BandCount + " rows, expected 5",
                failures);
            bool ascending = true;
            bool kindsUnique = true;
            bool everyRowStatesItsBasis = true;
            for (int i = 0; i < MedianContent.BandCount; i++)
            {
                MedianWidthBand row = MedianContent.BandAt(i);
                if (string.IsNullOrEmpty(row.basis)) { everyRowStatesItsBasis = false; }
                if (i > 0)
                {
                    MedianWidthBand previous = MedianContent.BandAt(i - 1);
                    if (row.minimumWidthMetres <= previous.minimumWidthMetres) { ascending = false; }
                    if (row.kind == previous.kind) { kindsUnique = false; }
                }
                builder.Append("RCMMEDIAN band    from ").Append(F(row.minimumWidthMetres)).Append(" m  ")
                       .Append(row.kind).Append(row.authoredThreshold ? "  AUTHORED" : "  sourced").Append("\n");
            }
            Expect(ascending, "band thresholds are not strictly ascending, so a lookup cannot pick one row", failures);
            Expect(kindsUnique, "two bands claim the same content kind, so a threshold means nothing", failures);
            Expect(everyRowStatesItsBasis, "a band row carries no basis, so the author cannot be told why", failures);

            Expect(MedianContent.BandFor(0f).kind == MedianContentKind.PaintedFlush,
                "a zero-width median is not painted flush", failures);
            Expect(MedianContent.BandFor(2.4f).kind == MedianContentKind.Barrier,
                "2.4 m does not reach the barrier band, so the lookup is not inclusive at its threshold", failures);
            Expect(MedianContent.BandFor(2.39f).kind == MedianContentKind.KerbedIslandMarkings,
                "2.39 m already holds a barrier, so the lookup rounds up over a threshold", failures);
            Expect(MedianContent.BandFor(9f).kind == MedianContentKind.Trough, "9 m does not hold a trough", failures);
            Expect(MedianContent.BandFor(15f).kind == MedianContentKind.TroughAndPlanting,
                "15 m does not hold planting", failures);
            Expect(MedianContent.BandFor(400f).kind == MedianContentKind.TroughAndPlanting,
                "a width past the last row is not clamped to it", failures);

            Expect(!MedianContent.HoldsBarrier(MedianContentKind.Trough),
                "a trough median also holds a barrier: the kinds are being treated as cumulative", failures);
            Expect(MedianContent.HoldsTrough(MedianContentKind.TroughAndPlanting)
                && !MedianContent.HoldsPlanting(MedianContentKind.Trough),
                "trough and planting do not nest the way the table orders them", failures);

            float planting = MedianContent.MinimumPlantingWidthMetres();
            float fromSetback = 2f * MedianContent.PlantingSetbackMetres + MedianTroughClass.Default().bottomWidthMetres;
            Expect(Mathf.Abs(planting - 15f) < 1e-4f, "planting minimum is " + F(planting) + " m, expected 15 m",
                failures);
            Expect(Mathf.Abs(planting - fromSetback) < 1e-4f, "the planting band (" + F(planting)
                + " m) and the setback it is built from (" + F(fromSetback)
                + " m) are two different numbers for one rule", failures);
            builder.Append("RCMMEDIAN planting minimum ").Append(F(planting)).Append(" m = 2 x ")
                   .Append(F(MedianContent.PlantingSetbackMetres)).Append(" m setback + ")
                   .Append(F(MedianTroughClass.Default().bottomWidthMetres)).Append(" m trough bottom\n");
        }
        /// <summary> Case 2: a 3 m median on a 110 km/h road holds a barrier, and the barrier is TERMINATED AT BOTH
        /// ENDS. With 120 m of approach and a 1.2 m offset the ladder reaches its first affordable rung -- a flare of
        /// 1.2 x 20 = 24 m -- so both ends are flared, not blunt. The trough and planting fields are asserted empty:
        /// a barrier median is not also a trough median. </summary>
        private static void CaseNarrowMedianGetsTerminatedBarrier(StringBuilder builder, List<string> failures)
        {
            MedianSite site = MedianContent.Site(3f, Highway);
            MedianPlan plan = MedianContent.Resolve(site, MedianTroughClass.Default());

            Expect(plan.kind == MedianContentKind.Barrier, "a 3 m median resolved to " + plan.kind
                + " instead of a barrier", failures);
            Expect(plan.hasBarrier && !plan.refused, "a 3 m median with 120 m of approach did not get a barrier: "
                + (plan.refusal == "" ? "no reason given" : plan.refusal), failures);
            Expect(plan.startTerminal.treatment != TerminalTreatment.None
                && plan.endTerminal.treatment != TerminalTreatment.None,
                "a placed barrier has an unresolved end: start " + plan.startTerminal.treatment + ", end "
                + plan.endTerminal.treatment, failures);
            Expect(!plan.startTerminal.refused && !plan.endTerminal.refused,
                "a terminal refused on a road with 120 m of room at each end", failures);
            Expect(plan.startTerminal.lengthMetres > 0f && plan.endTerminal.lengthMetres > 0f,
                "a terminal was chosen with zero length, which builds nothing", failures);
            Expect(plan.startTerminal.treatment == TerminalTreatment.FlaredEnd
                && Mathf.Abs(plan.startTerminal.lengthMetres - 24f) < 1e-3f,
                "the near end is " + plan.startTerminal.treatment + " of " + F(plan.startTerminal.lengthMetres)
                + " m, expected a 24 m flare (1.2 m offset at 20:1)", failures);

            Expect(!plan.hasTrough && plan.troughDepthMetres == 0f && plan.troughBottomWidthMetres == 0f,
                "a 3 m barrier median also carries trough geometry", failures);
            Expect(!plan.hasPlanting && plan.plantingRefusal == "",
                "a 3 m median with no tree row authored still reports planting", failures);
            builder.Append("RCMMEDIAN barrier 3 m median at ").Append(F(Highway)).Append(" km/h  ends ")
                   .Append(plan.startTerminal.treatment).Append(" / ").Append(plan.endTerminal.treatment)
                   .Append("  ").Append(F(plan.startTerminal.lengthMetres)).Append(" m each\n");
        }
        /// <summary> Case 3: the defect this task exists to prevent. Every combination of room, speed and ground the
        /// ladder can see is swept, and the count of runs that were PLACED with an unresolved end must be zero -- a
        /// barrier is either terminated or refused, never blunt. The sweep is also asserted to reach all three rungs,
        /// so it cannot pass by never leaving the easy case, and the zero-room refusal is asserted to name the room it
        /// had and every length it tried. </summary>
        private static void CaseBluntEndIsNeverEmitted(StringBuilder builder, List<string> failures)
        {
            float[] speeds = { 50f, 60f, 70f, 80f, 100f, 110f, 130f };
            int placedWithNoTerminal = 0;
            int refusedButStillBuilt = 0;
            int flared = 0;
            int buried = 0;
            int cushioned = 0;
            int refusedEnds = 0;
            int combinations = 0;

            for (int s = 0; s < speeds.Length; s++)
            {
                for (int step = 0; step <= 80; step++)
                {
                    for (int g = 0; g < 2; g++)
                    {
                        MedianSite site = MedianContent.Site(3f, speeds[s]);
                        site.approachLengthMetres = step * 0.5f;
                        site.backslopeAvailable = g == 1;
                        MedianPlan plan = MedianContent.Resolve(site, MedianTroughClass.Default());
                        combinations++;

                        if (plan.hasBarrier && (plan.startTerminal.treatment == TerminalTreatment.None
                            || plan.endTerminal.treatment == TerminalTreatment.None)) { placedWithNoTerminal++; }
                        if (plan.hasBarrier && (plan.startTerminal.refused || plan.endTerminal.refused))
                        { refusedButStillBuilt++; }
                        if (plan.startTerminal.treatment == TerminalTreatment.FlaredEnd) { flared++; }
                        if (plan.startTerminal.treatment == TerminalTreatment.BuriedInBackslope) { buried++; }
                        if (plan.startTerminal.treatment == TerminalTreatment.CrashCushion) { cushioned++; }
                        if (plan.startTerminal.refused)
                        {
                            refusedEnds++;
                            Expect(plan.startTerminal.treatment == TerminalTreatment.None && plan.refused,
                                "a refused end at " + F(site.approachLengthMetres) + " m of room still produced "
                                + plan.startTerminal.treatment + " with plan.refused " + plan.refused, failures);
                        }
                    }
                }
            }

            Expect(placedWithNoTerminal == 0, placedWithNoTerminal + " of " + combinations
                + " swept medians placed a barrier with an unresolved end: that is a blunt rail facing traffic",
                failures);
            Expect(refusedButStillBuilt == 0, refusedButStillBuilt
                + " swept medians built a barrier whose end had already refused", failures);
            Expect(flared > 0 && buried > 0 && cushioned > 0 && refusedEnds > 0,
                "the sweep never reached every rung (flared " + flared + ", buried " + buried + ", cushioned "
                + cushioned + ", refused " + refusedEnds + "), so the count of blunt ends proves nothing", failures);

            MedianSite noRoom = MedianContent.Site(3f, Highway);
            noRoom.approachLengthMetres = 0f;
            BarrierTerminal end = MedianContent.ChooseTerminal(noRoom, RoadEnd.End);
            Expect(end.refused && end.treatment == TerminalTreatment.None,
                "an end with no road left to end in was not refused", failures);
            Expect(end.refusal.Contains("0 m") && end.refusal.Contains("24 m") && end.refusal.Contains("7.6 m")
                && end.refusal.Contains("backslope") && end.refusal.Contains("6.5 m"),
                "the refusal does not name the room and all three lengths tried: " + end.refusal, failures);
            builder.Append("RCMMEDIAN ends    swept ").Append(combinations).Append(" medians  flared ").Append(flared)
                   .Append("  buried ").Append(buried).Append("  cushioned ").Append(cushioned).Append("  refused ")
                   .Append(refusedEnds).Append("  blunt ").Append(placedWithNoTerminal).Append("\n");
        }
        /// <summary> Case 4: the end object's placement is RM-26's computation, not a second one written for medians.
        /// The plan's placement is compared EXACTLY against RoadTerminus.ComputePlacement for the same inputs, on a
        /// road raised 5 m above nodes whose own Y is wrong on purpose -- so the anti-sink height rule is inherited
        /// rather than re-derived -- and the two ends are asserted to face 180 degrees apart. The placement is also
        /// asserted to be resolved on a REFUSED end: that is still where the run would have ended. </summary>
        private static void CaseTerminalPlacementIsRM26s(StringBuilder builder, List<string> failures)
        {
            MedianSite site = MedianContent.Site(3f, Highway);
            site.roadSurfaceHeightMetres = 5f;
            site.startNodeCentre = new Vector3(12f, -3f, 20f);
            site.endNodeCentre = new Vector3(12f, 2f, 420f);
            site.tangentAtEndXZ = new Vector2(1f, 1f);

            MedianPlan plan = MedianContent.Resolve(site, MedianTroughClass.Default());
            TerminusPlacement expectedStart = RoadTerminus.ComputePlacement(site.startNodeCentre,
                site.roadSurfaceHeightMetres, site.tangentAtEndXZ, RoadEnd.Start);
            TerminusPlacement expectedEnd = RoadTerminus.ComputePlacement(site.endNodeCentre,
                site.roadSurfaceHeightMetres, site.tangentAtEndXZ, RoadEnd.End);

            Expect(plan.startTerminal.placement.position == expectedStart.position
                && plan.startTerminal.placement.yawDegrees == expectedStart.yawDegrees,
                "the near end sits at " + plan.startTerminal.placement.position + " yaw "
                + F(plan.startTerminal.placement.yawDegrees) + ", but RM-26 places it at " + expectedStart.position
                + " yaw " + F(expectedStart.yawDegrees) + ": there are two placement implementations", failures);
            Expect(plan.endTerminal.placement.position == expectedEnd.position
                && plan.endTerminal.placement.yawDegrees == expectedEnd.yawDegrees,
                "the far end does not match RM-26's placement", failures);

            Expect(plan.startTerminal.placement.position.y == 5f && plan.endTerminal.placement.position.y == 5f,
                "an end object took its height from the node (" + F(plan.startTerminal.placement.position.y) + " / "
                + F(plan.endTerminal.placement.position.y) + ") instead of the 5 m road surface: it would sink",
                failures);
            float apart = Mathf.Abs(Mathf.DeltaAngle(plan.startTerminal.placement.yawDegrees,
                plan.endTerminal.placement.yawDegrees));
            Expect(Mathf.Abs(apart - 180f) < 1e-3f, "the two ends face " + F(apart)
                + " degrees apart, expected 180: one of them faces back down its own road", failures);

            MedianSite noRoom = MedianContent.Site(3f, Highway);
            noRoom.roadSurfaceHeightMetres = 5f;
            noRoom.endNodeCentre = new Vector3(12f, 2f, 420f);
            noRoom.approachLengthMetres = 0f;
            BarrierTerminal refusedEnd = MedianContent.PlanTerminal(noRoom, RoadEnd.End);
            Expect(refusedEnd.refused && refusedEnd.placement.position == new Vector3(12f, 5f, 420f),
                "a refused end reported no position, so a report cannot say where the problem is", failures);
            builder.Append("RCMMEDIAN ends    placement from RM-26  near ").Append(expectedStart.position)
                   .Append(" yaw ").Append(F(expectedStart.yawDegrees)).Append("  far ").Append(expectedEnd.position)
                   .Append(" yaw ").Append(F(expectedEnd.yawDegrees)).Append("\n");
        }
        /// <summary> Case 5: the flare length is a real table, not a constant. Exact at every sourced row, linearly
        /// interpolated between rows (70 km/h lands halfway between 11:1 and 14:1), clamped past both ends so a speed
        /// off the table cannot yield a nonsense rate, and monotonic -- a faster road never gets a tighter flare. The
        /// ladder's order is also justified here: at every table speed the cushion is shorter than the flare, which is
        /// why a flare is tried first and a cushion is the last resort before refusing. </summary>
        private static void CaseFlareRatesComeFromTheTable(StringBuilder builder, List<string> failures)
        {
            float[] speeds = { 50f, 60f, 80f, 100f, 110f };
            float[] runs = { 8f, 11f, 14f, 18f, 20f };
            for (int i = 0; i < speeds.Length; i++)
            {
                float rate = MedianContent.FlareRunPerMetreOffset(speeds[i]);
                Expect(Mathf.Abs(rate - runs[i]) < 1e-4f, "the flare rate at " + F(speeds[i]) + " km/h is "
                    + F(rate) + ":1, expected " + F(runs[i]) + ":1", failures);
            }
            Expect(Mathf.Abs(MedianContent.FlareRunPerMetreOffset(30f) - 8f) < 1e-4f,
                "a speed below the table is not clamped to its first row", failures);
            Expect(Mathf.Abs(MedianContent.FlareRunPerMetreOffset(200f) - 20f) < 1e-4f,
                "a speed above the table is not clamped to its last row", failures);
            float seventy = MedianContent.FlareRunPerMetreOffset(70f);
            Expect(Mathf.Abs(seventy - 12.5f) < 1e-4f, "70 km/h interpolates to " + F(seventy)
                + ":1, expected 12.5:1 halfway between 11:1 and 14:1", failures);

            bool monotonic = true;
            float previous = 0f;
            for (int v = 30; v <= 200; v++)
            {
                float rate = MedianContent.FlareRunPerMetreOffset(v);
                if (rate < previous - 1e-4f) { monotonic = false; }
                previous = rate;
            }
            Expect(monotonic, "the flare rate falls somewhere as speed rises", failures);

            float length = MedianContent.FlareLengthMetres(4.5f, Highway);
            Expect(Mathf.Abs(length - 90f) < 1e-3f, "flaring 4.5 m at 110 km/h needs " + F(length)
                + " m, expected 90 m at 20:1", failures);
            Expect(MedianContent.FlareLengthMetres(0f, Highway) == 0f,
                "a barrier with no lateral offset still demands flare length", failures);

            for (int i = 0; i < speeds.Length; i++)
            {
                float cushion = MedianContent.CrashCushionLengthMetres(speeds[i]);
                float flare = MedianContent.FlareLengthMetres(1.2f, speeds[i]);
                Expect(cushion > 0f && cushion < flare, "at " + F(speeds[i]) + " km/h the cushion (" + F(cushion)
                    + " m) is not shorter than the flare (" + F(flare) + " m), so the ladder's order is wrong",
                    failures);
            }
            Expect(Mathf.Abs(MedianContent.CrashCushionLengthMetres(40f) - 2.8f) < 1e-4f
                && Mathf.Abs(MedianContent.CrashCushionLengthMetres(130f) - 6.5f) < 1e-4f,
                "the cushion table is not clamped past its ends", failures);
            builder.Append("RCMMEDIAN flare   50 8:1  60 11:1  70 ").Append(F(seventy)).Append(":1  80 14:1  100 18:1")
                   .Append("  110 20:1   4.5 m offset at 110 = ").Append(F(length)).Append(" m\n");
        }
        /// <summary> Case 6: a wide median holds a trough whose depth comes from its own side slopes -- 9 m gives
        /// 0.5 m, 12 m gives 0.75 m, 15 m gives 1.0 m at 6:1 -- and past 20 m the 1.2 m depth cap bites, after which
        /// the median gets a FLATTER trough rather than a deeper one. Depth is asserted never to exceed the cap and
        /// never to fall as the median widens. The capacity is asserted to be RM-30's own number for the effective
        /// shape, so the hydraulics are not implemented twice. </summary>
        private static void CaseWideMedianTroughIsShapedAndDrains(StringBuilder builder, List<string> failures)
        {
            MedianTroughClass trough = MedianTroughClass.Default();
            float[] widths = { 9f, 12f, 15f };
            float[] depths = { 0.5f, 0.75f, 1f };
            for (int i = 0; i < widths.Length; i++)
            {
                float depth = MedianContent.TroughDepthMetres(widths[i], trough);
                Expect(Mathf.Abs(depth - depths[i]) < 1e-4f, "a " + F(widths[i]) + " m median troughs "
                    + F(depth) + " m deep, expected " + F(depths[i]) + " m at 6:1 either side of a 3 m bottom",
                    failures);
                float slope = MedianContent.EffectiveSideSlopeHorizontalPerVertical(widths[i], trough);
                Expect(Mathf.Abs(slope - 6f) < 1e-3f, "below the depth cap the built side slope is " + F(slope)
                    + ":1 instead of the class's 6:1", failures);
            }

            MedianSite site = MedianContent.Site(12f, Highway);
            MedianPlan plan = MedianContent.Resolve(site, trough);
            Expect(plan.kind == MedianContentKind.Trough && plan.hasTrough && !plan.refused,
                "a 12 m median with a graded outfall did not get a trough: " + plan.troughRefusal, failures);
            Expect(Mathf.Abs(plan.troughDepthMetres - 0.75f) < 1e-4f
                && Mathf.Abs(plan.troughBottomWidthMetres - 3f) < 1e-4f,
                "the plan's trough is " + F(plan.troughDepthMetres) + " m deep on a "
                + F(plan.troughBottomWidthMetres) + " m bottom, expected 0.75 m on 3 m", failures);
            Expect(!plan.hasBarrier && plan.startTerminal.treatment == TerminalTreatment.None
                && plan.startTerminal.lengthMetres == 0f,
                "a 12 m trough median also carries a barrier and its terminals", failures);
            Expect(!plan.hasPlanting, "a 12 m median plants trees inside the clear zone", failures);

            float cappedDepth = MedianContent.TroughDepthMetres(20f, trough);
            float cappedSlope = MedianContent.EffectiveSideSlopeHorizontalPerVertical(20f, trough);
            Expect(Mathf.Abs(cappedDepth - 1.2f) < 1e-4f, "a 20 m median troughs " + F(cappedDepth)
                + " m deep, expected the 1.2 m cap", failures);
            Expect(cappedSlope > 7f, "past the depth cap the side slope stayed at " + F(cappedSlope)
                + ":1, so the extra width went into depth instead of flattening", failures);

            bool capRespected = true;
            bool neverFalls = true;
            float previousDepth = 0f;
            for (int step = 0; step <= 100; step++)
            {
                float depth = MedianContent.TroughDepthMetres(step * 0.5f, trough);
                if (depth > trough.maximumDepthMetres + 1e-4f) { capRespected = false; }
                if (depth < previousDepth - 1e-4f) { neverFalls = false; }
                previousDepth = depth;
            }
            Expect(capRespected, "the trough depth exceeds its own cap somewhere between 0 m and 50 m of median",
                failures);
            Expect(neverFalls, "the trough gets shallower as the median widens somewhere", failures);

            double fromMedian = MedianContent.TroughCapacityM3PerSecond(12f, trough, 0.3, 0.01);
            double fromCulvert = Culvert.VDitchCapacityM3PerSecond(0.3, 0.01,
                MedianContent.AsDitchClass(12f, trough));
            Expect(fromMedian == fromCulvert && fromMedian > 0.0, "the trough's capacity (" + fromMedian
                + " m3/s) is not RM-30's own figure (" + fromCulvert + " m3/s)", failures);
            builder.Append("RCMMEDIAN trough  12 m median  depth ").Append(F(plan.troughDepthMetres))
                   .Append(" m  bottom ").Append(F(plan.troughBottomWidthMetres))
                   .Append(" m  capacity at 0.3 m / 1 % ")
                   .Append(fromMedian.ToString("0.###", CultureInfo.InvariantCulture)).Append(" m3/s   20 m median  ")
                   .Append("depth ").Append(F(cappedDepth)).Append(" m capped, side slope ").Append(F(cappedSlope))
                   .Append(":1\n");
        }
        /// <summary> Case 7: a trough that cannot drain is refused BY STATION. All three invisible failures are
        /// exercised -- no outfall at all, an outfall that is not downhill (RM-30's own chain-of-basins test), and a
        /// grade too flat to move water -- and each refusal must name the station and the numbers behind the verdict.
        /// The same median with a real outfall is asserted to be accepted, so the case has teeth, and a refused trough
        /// is asserted to leave NO depth behind. </summary>
        private static void CaseTroughWithNoOutfallIsRefused(StringBuilder builder, List<string> failures)
        {
            MedianTroughClass trough = MedianTroughClass.Default();

            MedianSite noOutfall = MedianContent.Site(12f, Highway);
            noOutfall.lowPointStation = 260f;
            noOutfall.outfallPresent = false;
            MedianPlan a = MedianContent.Resolve(noOutfall, trough);
            Expect(!a.hasTrough && a.refused && a.troughDepthMetres == 0f,
                "a median trough with no outfall was built anyway, " + F(a.troughDepthMetres) + " m deep", failures);
            Expect(a.troughRefusal.Contains("260 m") && a.troughRefusal.Contains("nowhere to drain")
                && a.troughRefusal.Contains("outfall absent"),
                "the no-outfall refusal does not name the station and the missing outfall: " + a.troughRefusal,
                failures);

            MedianSite uphill = MedianContent.Site(12f, Highway);
            uphill.lowPointStation = 260f;
            uphill.upstreamInvertMetres = 1f;
            uphill.downstreamInvertMetres = 1.5f;
            MedianPlan b = MedianContent.Resolve(uphill, trough);
            Expect(!b.hasTrough && b.refused, "a trough draining uphill to a real outfall was accepted", failures);
            Expect(b.troughRefusal.Contains("1.5 m") && b.troughRefusal.Contains("outfall present"),
                "the uphill refusal does not quote the inverts it compared: " + b.troughRefusal, failures);

            MedianSite flat = MedianContent.Site(12f, Highway);
            flat.lowPointStation = 260f;
            flat.longitudinalGradeFraction = 0.002f;
            MedianPlan c = MedianContent.Resolve(flat, trough);
            Expect(!c.hasTrough && c.refused, "a trough graded at 0.2 % was accepted below the 0.5 % minimum",
                failures);
            Expect(c.troughRefusal.Contains("260 m") && c.troughRefusal.Contains("0.2 %")
                && c.troughRefusal.Contains("0.5 %"),
                "the flat-grade refusal does not quote the grade against the minimum: " + c.troughRefusal, failures);

            MedianSite good = MedianContent.Site(12f, Highway);
            good.lowPointStation = 260f;
            MedianPlan d = MedianContent.Resolve(good, trough);
            Expect(d.hasTrough && !d.refused && d.troughRefusal == "",
                "the same 12 m median falling 1 m at 1 % to a present outfall was also refused: " + d.troughRefusal,
                failures);
            Expect(MedianContent.TroughDrains(good) && !MedianContent.TroughDrains(noOutfall)
                && !MedianContent.TroughDrains(uphill),
                "RM-30's outfall test does not separate these three sites", failures);
            builder.Append("RCMMEDIAN drain   station 260 m  no outfall REFUSED  uphill outfall REFUSED  0.2 % ")
                   .Append("REFUSED  1 % to a real outfall accepted, ").Append(F(d.troughDepthMetres))
                   .Append(" m deep\n");
        }
        /// <summary> Case 8: planting is refused with the width quoted, and the refusal is about the trees only -- the
        /// median itself is still built, still troughed. A 9 m median with an authored tree row must be told its own
        /// width and the width it would need; a 16 m median must place trees at the full 6 m setback; and a 9 m median
        /// where nobody asked for trees must say nothing at all, because refusing something the author never requested
        /// is noise. </summary>
        private static void CasePlantingRefusedWithWidthQuoted(StringBuilder builder, List<string> failures)
        {
            MedianTroughClass trough = MedianTroughClass.Default();

            MedianSite narrow = MedianContent.Site(9f, Highway);
            narrow.treeSpacingMetres = 8f;
            narrow.trunkDiameterMetres = 0.6f;
            MedianPlan tooNarrow = MedianContent.Resolve(narrow, trough);
            Expect(!tooNarrow.hasPlanting && tooNarrow.plantingSetbackMetres == 0f,
                "a 9 m median planted trees inside the clear zone", failures);
            Expect(tooNarrow.plantingRefusal.Contains("9 m") && tooNarrow.plantingRefusal.Contains("15 m")
                && tooNarrow.plantingRefusal.Contains("6 m"),
                "the planting refusal does not quote the width it has, the width it needs and the setback: "
                + tooNarrow.plantingRefusal, failures);
            Expect(!tooNarrow.refused && tooNarrow.hasTrough,
                "refusing the trees also refused the median: a 9 m median is still a median with a trough", failures);

            MedianSite wide = MedianContent.Site(16f, Highway);
            wide.treeSpacingMetres = 8f;
            wide.trunkDiameterMetres = 0.6f;
            MedianPlan planted = MedianContent.Resolve(wide, trough);
            Expect(planted.kind == MedianContentKind.TroughAndPlanting && planted.hasPlanting
                && planted.plantingRefusal == "",
                "a 16 m median did not get planting: " + planted.plantingRefusal, failures);
            Expect(Mathf.Abs(planted.plantingSetbackMetres - 6f) < 1e-4f, "trees are set back "
                + F(planted.plantingSetbackMetres) + " m, expected the 6 m clear zone", failures);
            Expect(planted.hasTrough && planted.troughDepthMetres > 0f,
                "a 16 m median got planting but lost its trough", failures);

            MedianSite silent = MedianContent.Site(9f, Highway);
            MedianPlan noTrees = MedianContent.Resolve(silent, trough);
            Expect(noTrees.plantingRefusal == "",
                "a median where no tree row was authored still refuses planting: " + noTrees.plantingRefusal,
                failures);
            Expect(MedianContent.PlantingFits(15f) && !MedianContent.PlantingFits(14.99f),
                "the planting fit test does not agree with the 15 m band it is built from", failures);
            builder.Append("RCMMEDIAN plant   9 m REFUSED (needs 15 m)  16 m planted at ")
                   .Append(F(planted.plantingSetbackMetres)).Append(" m setback  9 m with no tree row silent\n");
        }
        /// <summary> Case 9: the through-median sight line is REPORTED, never refused. The opening between trunks is
        /// real geometry -- an 8 m row of 0.6 m trunks leaves 3.4 m of gap across a 30 degree line of sight and closes
        /// entirely below about 4.3 degrees -- and touching trunks are a wall from every direction. The plan is
        /// asserted to stand unrefused even when the row is completely opaque, because whether a blocked view matters
        /// is the author's call and not this code's. </summary>
        private static void CaseSightLineIsReportedNotRefused(StringBuilder builder, List<string> failures)
        {
            float opening30 = MedianContent.ClearSightOpeningMetres(8f, 0.6f, 30f);
            Expect(Mathf.Abs(opening30 - 3.4f) < 1e-3f, "an 8 m row of 0.6 m trunks leaves " + F(opening30)
                + " m across a 30 degree sight line, expected 3.4 m", failures);
            float opening90 = MedianContent.ClearSightOpeningMetres(8f, 0.6f, 90f);
            Expect(Mathf.Abs(opening90 - 7.4f) < 1e-3f, "square across the median the opening is " + F(opening90)
                + " m, expected 7.4 m", failures);

            float opaqueBelow = MedianContent.OpaqueBelowAngleDegrees(8f, 0.6f);
            Expect(Mathf.Abs(opaqueBelow - 4.301f) < 0.01f, "the row goes opaque below " + F(opaqueBelow)
                + " degrees, expected about 4.3 (asin 0.6 / 8)", failures);
            Expect(MedianContent.ClearSightOpeningMetres(8f, 0.6f, opaqueBelow) < 1e-3f,
                "at its own opacity angle the row still reports an opening", failures);

            bool widensWithAngle = true;
            float previous = -1f;
            for (int degrees = 0; degrees <= 90; degrees++)
            {
                float opening = MedianContent.ClearSightOpeningMetres(8f, 0.6f, degrees);
                if (opening < previous - 1e-4f) { widensWithAngle = false; }
                previous = opening;
            }
            Expect(widensWithAngle, "the sight opening narrows somewhere as the view squares up to the road",
                failures);

            Expect(MedianContent.OpaqueBelowAngleDegrees(0.6f, 0.6f) == 90f
                && MedianContent.ClearSightOpeningMetres(0.6f, 0.6f, 90f) == 0f,
                "touching trunks are not reported as a wall from every direction", failures);

            MedianSite dense = MedianContent.Site(16f, Highway);
            dense.treeSpacingMetres = 0.6f;
            dense.trunkDiameterMetres = 0.6f;
            MedianPlan plan = MedianContent.Resolve(dense, MedianTroughClass.Default());
            Expect(plan.hasPlanting && !plan.refused && plan.plantingRefusal == "",
                "a solid wall of trees in a 16 m median was refused instead of reported: " + plan.plantingRefusal,
                failures);
            builder.Append("RCMMEDIAN sight   8 m spacing / 0.6 m trunks  opening 3.4 m at 30 deg, ")
                   .Append(F(opening90)).Append(" m square, opaque below ").Append(F(opaqueBelow))
                   .Append(" deg  (reported, not refused)\n");
        }
        /// <summary> Case 10: changing the width re-solves all three, and leaves nothing of the previous median behind.
        /// The same site is resolved at 3 m, 20 m, 12 m and 3 m again: the barrier and its two end objects must be
        /// completely gone at 20 m -- treatment, length and placement all cleared, not merely unused -- planting must
        /// vanish again at 12 m, and the return to 3 m must reproduce the first plan field for field with exact
        /// comparisons (C-06). The width itself is then taken from RM-59's own emitted geometry, so the content is
        /// sized by the road that exists rather than by a number typed twice. </summary>
        private static void CaseWidthChangeResolvesEverything(StringBuilder builder, List<string> failures)
        {
            MedianTroughClass trough = MedianTroughClass.Default();
            MedianSite site = MedianContent.Site(3f, Highway);
            site.treeSpacingMetres = 8f;
            site.trunkDiameterMetres = 0.6f;

            MedianPlan first = MedianContent.Resolve(site, trough);
            site.widthMetres = 20f;
            MedianPlan widened = MedianContent.Resolve(site, trough);
            site.widthMetres = 12f;
            MedianPlan middle = MedianContent.Resolve(site, trough);
            site.widthMetres = 3f;
            MedianPlan again = MedianContent.Resolve(site, trough);

            Expect(first.hasBarrier && !first.hasTrough && !first.hasPlanting,
                "the 3 m median did not resolve to a barrier alone", failures);
            Expect(!widened.hasBarrier && widened.hasTrough && widened.hasPlanting,
                "the 20 m median did not resolve to a trough with planting", failures);
            Expect(widened.startTerminal.treatment == TerminalTreatment.None
                && widened.endTerminal.treatment == TerminalTreatment.None
                && widened.startTerminal.lengthMetres == 0f && widened.endTerminal.lengthMetres == 0f
                && widened.startTerminal.placement.position == Vector3.zero
                && widened.endTerminal.placement.position == Vector3.zero,
                "widening to 20 m left the barrier's end objects behind: start " + widened.startTerminal.treatment
                + " " + F(widened.startTerminal.lengthMetres) + " m at " + widened.startTerminal.placement.position,
                failures);
            Expect(middle.hasTrough && !middle.hasPlanting && middle.plantingRefusal != "",
                "narrowing to 12 m kept the planting or stopped explaining its absence", failures);
            Expect(middle.troughDepthMetres != widened.troughDepthMetres,
                "the 12 m and 20 m medians report the same trough depth, so the trough was not re-solved", failures);

            bool identical = again.kind == first.kind
                && again.widthMetres == first.widthMetres
                && again.hasBarrier == first.hasBarrier
                && again.startTerminal.treatment == first.startTerminal.treatment
                && again.startTerminal.lengthMetres == first.startTerminal.lengthMetres
                && again.startTerminal.placement.position == first.startTerminal.placement.position
                && again.startTerminal.placement.yawDegrees == first.startTerminal.placement.yawDegrees
                && again.endTerminal.treatment == first.endTerminal.treatment
                && again.endTerminal.lengthMetres == first.endTerminal.lengthMetres
                && again.hasTrough == first.hasTrough
                && again.troughDepthMetres == first.troughDepthMetres
                && again.troughBottomWidthMetres == first.troughBottomWidthMetres
                && again.hasPlanting == first.hasPlanting
                && again.plantingSetbackMetres == first.plantingSetbackMetres
                && again.troughRefusal == first.troughRefusal
                && again.plantingRefusal == first.plantingRefusal
                && again.refused == first.refused
                && again.refusal == first.refusal;
            Expect(identical, "returning to 3 m did not reproduce the first plan exactly: either the resolve carries "
                + "state between calls or it is not deterministic", failures);

            DividedRoadProfile profile = DividedRoad.Default();
            profile.medianHalfGap = DividedRoad.TransitionToUndivided(1.5f, 100f, 50f);
            float openWidth = MedianContent.MedianWidthAt(profile, 50f);
            float closedWidth = MedianContent.MedianWidthAt(profile, 200f);
            Expect(Mathf.Abs(openWidth - 3f) < 1e-4f, "RM-59's 1.5 m half-gap reads back as a " + F(openWidth)
                + " m median, expected 3 m", failures);
            Expect(closedWidth == 0f, "past the transition the median still measures " + F(closedWidth)
                + " m wide, so content would be placed in a median that has closed", failures);
            Expect(MedianContent.BandFor(openWidth).kind == MedianContentKind.Barrier
                && MedianContent.BandFor(closedWidth).kind == MedianContentKind.PaintedFlush,
                "the width taken from RM-59's geometry does not pick the bands 3 m and 0 m pick", failures);
            builder.Append("RCMMEDIAN change  3 m ").Append(first.kind).Append(" -> 20 m ").Append(widened.kind)
                   .Append(" -> 12 m ").Append(middle.kind).Append(" -> 3 m ").Append(again.kind)
                   .Append(" reproduced exactly   from RM-59: ").Append(F(openWidth)).Append(" m open, ")
                   .Append(F(closedWidth)).Append(" m closed\n");
        }

        /// <summary> Records a failure when a condition does not hold. </summary>
        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition) { failures.Add(failureMessage); }
        }

        /// <summary> Culture-invariant number formatting for the report (C-06). </summary>
        private static string F(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }
}
#endif
