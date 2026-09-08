// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using RoadCityMaster.CrossSection;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-76's gate: the run model that sits on top of EdgeObjectMaker terminates, segments and
    /// transitions every edge-object run without ever leaving a bare cut part or a floating post. Headless and
    /// adversarial -- every expected number is a hand figure or an independent cross-check, never the solver's own
    /// formula run twice -- so a PASS means the seven acceptance rules hold, not that the code agrees with itself.
    /// The seven: (1) both ends carry a real treatment or the run is refused; (2) an opening makes two fully
    /// terminated runs, not one run with a hole; (3) a non-integer body absorbs its remainder at a stated place,
    /// never by scaling a part; (4) heights follow the ROAD surface, provable as the absence of any terrain reader;
    /// (5) lateral offset tracks the emitted, station-varying edge; (6) a type pair with no transition part is
    /// refused by name; (7) a sub-run shorter than its own two ends is refused. </summary>
    public static class CityEdgeRunGate
    {
        private const string Prefix = "RCMEDGERUN";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Edge-object runs")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
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
            _cases = 0;
            List<string> failures = new List<string>();

            CaseBarrierEndsAreRealNotBareCut(failures);
            CaseFenceEndsAtAPostNeverMidPanel(failures);
            CaseEmptyEndSetFailsClosed(failures);
            CaseOpeningSplitsIntoTwoTerminatedRuns(failures);
            CaseAdjacentOpeningsMergeAndClip(failures);
            CaseShortSubRunIsRefused(failures);
            CaseClosingPartCarriesRemainderNeverScales(failures);
            CaseDistributeSpacingStaysWithinTolerance(failures);
            CaseHeightFollowsRoadSurfaceNotTerrain(failures);
            CaseLateralOffsetTracksWidenedEdge(failures);
            CaseTransitionRefusedByNameWhereUndefined(failures);
            CaseTwoSolvesAreBitIdentical(failures);

            StringBuilder sb = new StringBuilder();
            sb.Append(Prefix).Append(" BEGIN\n");
            for (int i = 0; i < failures.Count; i++)
            {
                sb.Append(Prefix).Append(" FAIL   ").Append(failures[i]).Append('\n');
            }
            sb.Append(Prefix).Append(failures.Count == 0 ? " result PASS" : " result FAIL")
                .Append("  cases ").Append(_cases.ToString(CultureInfo.InvariantCulture))
                .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append(Prefix).Append(" END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        /// <summary> (1) A barrier run's ends come from RM-60's ladder: with ample room they resolve to a real
        /// treatment (not None) and eat a positive length -- never a bare cut rail. </summary>
        private static void CaseBarrierEndsAreRealNotBareCut(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Guardrail();
            r.totalLengthMetres = 200f;
            r.barrierSite = MedianContent.Site(9.0f, 100f);
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(!sol.refused, "a 200 m guardrail with 120 m of approach was refused: " + sol.refusal, failures);
            Expect(sol.SegmentCount == 1, "one uninterrupted run should be one segment, was "
                + sol.SegmentCount, failures);
            if (sol.SegmentCount != 1) { return; }
            EdgeRunSegment seg = sol.segments[0];
            Expect(seg.startEnd.kind != RunEndKind.None, "the near end of a barrier run resolved to None (a bare "
                + "cut), not a treatment.", failures);
            Expect(seg.endEnd.kind != RunEndKind.None, "the far end of a barrier run resolved to None (a bare cut), "
                + "not a treatment.", failures);
            Expect(!seg.startEnd.refused && !seg.endEnd.refused, "a barrier end with 120 m of room refused; ends "
                + "should resolve here.", failures);
            Expect(seg.startEnd.lengthMetres > 0f, "a resolved barrier end consumed zero length, which is a bare "
                + "cut by another name.", failures);
        }

        /// <summary> (1) A fence run ends at its most-preferred authored post, at that post's authored length --
        /// never mid-panel. The expected end and length are read from the authored type, not recomputed. </summary>
        private static void CaseFenceEndsAtAPostNeverMidPanel(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Fence();
            r.totalLengthMetres = 60f;
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(!sol.refused, "a 60 m panel fence was refused: " + sol.refusal, failures);
            if (sol.SegmentCount != 1) { Expect(false, "fence run was not one segment.", failures); return; }
            EdgeRunSegment seg = sol.segments[0];
            Expect(seg.startEnd.kind == RunEndKind.FenceStrainerPost, "a fence should end at its preferred strainer "
                + "post; near end was " + seg.startEnd.kind + ".", failures);
            Expect(seg.endEnd.kind == RunEndKind.FenceStrainerPost, "a fence should end at its preferred strainer "
                + "post; far end was " + seg.endEnd.kind + ".", failures);
            Expect(Num(seg.startEnd.lengthMetres) == Num(0.3f), "the strainer-post end should eat 0.3 m, ate "
                + Num(seg.startEnd.lengthMetres) + ".", failures);
        }

        /// <summary> (1, fail-closed) A fence type whose end set is emptied cannot end mid-panel: the run is refused
        /// by name, not ended on a bare panel edge. </summary>
        private static void CaseEmptyEndSetFailsClosed(List<string> failures)
        {
            EdgeRunType t = EdgeRunType.Fence();
            t.allowedEnds = new List<RunEndOption>();
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = t;
            r.totalLengthMetres = 60f;
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(sol.refused, "a fence type with no authored ends should fail closed, but the run was built.",
                failures);
            Expect(sol.refusal != null && sol.refusal.Contains("no end treatments"), "the fail-closed refusal "
                + "should name the missing end treatments; was: " + sol.refusal, failures);
        }

        /// <summary> (2) A driveway opening turns one run into TWO fully terminated runs -- not one run with a
        /// suppressed hole. Both sub-runs carry two real ends and neither is refused. </summary>
        private static void CaseOpeningSplitsIntoTwoTerminatedRuns(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Guardrail();
            r.totalLengthMetres = 200f;
            r.barrierSite = MedianContent.Site(9.0f, 100f);
            r.openings = new List<RunOpening> { new RunOpening(90f, 110f, RunOpeningKind.Driveway) };
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(sol.SegmentCount == 2, "a driveway in the middle should split into 2 runs, produced "
                + sol.SegmentCount + ".", failures);
            if (sol.SegmentCount != 2) { return; }
            EdgeRunSegment a = sol.segments[0];
            EdgeRunSegment b = sol.segments[1];
            Expect(Num(a.startStationMetres) == Num(0f) && Num(a.endStationMetres) == Num(90f), "first sub-run "
                + "should be [0, 90], was [" + Num(a.startStationMetres) + ", " + Num(a.endStationMetres) + "].",
                failures);
            Expect(Num(b.startStationMetres) == Num(110f) && Num(b.endStationMetres) == Num(200f), "second sub-run "
                + "should be [110, 200], was [" + Num(b.startStationMetres) + ", " + Num(b.endStationMetres) + "].",
                failures);
            Expect(!a.refused && !b.refused, "both sub-runs of the split should build; one was refused.", failures);
            Expect(a.startEnd.kind != RunEndKind.None && a.endEnd.kind != RunEndKind.None
                && b.startEnd.kind != RunEndKind.None && b.endEnd.kind != RunEndKind.None,
                "each split sub-run must carry two real ends; a None end means a bare cut at the opening.", failures);
        }

        /// <summary> (2) Overlapping and out-of-range openings are merged and clipped before segmentation, so the
        /// gaps are clean: two openings that overlap become one, and one running off the end is clipped to it. A
        /// total of 100 with openings [30,50], [45,60] and [90,120] leaves exactly the gaps [0,30] and [60,90].
        /// </summary>
        private static void CaseAdjacentOpeningsMergeAndClip(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Guardrail();
            r.totalLengthMetres = 100f;
            r.barrierSite = MedianContent.Site(9.0f, 100f);
            r.openings = new List<RunOpening>
            {
                new RunOpening(30f, 50f, RunOpeningKind.Driveway),
                new RunOpening(45f, 60f, RunOpeningKind.SideRoad),
                new RunOpening(90f, 120f, RunOpeningKind.Junction)
            };
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(sol.SegmentCount == 2, "merged/clipped openings should leave 2 gaps, left " + sol.SegmentCount
                + ".", failures);
            if (sol.SegmentCount != 2) { return; }
            EdgeRunSegment a = sol.segments[0];
            EdgeRunSegment b = sol.segments[1];
            Expect(Num(a.startStationMetres) == Num(0f) && Num(a.endStationMetres) == Num(30f), "first gap should "
                + "be [0, 30], was [" + Num(a.startStationMetres) + ", " + Num(a.endStationMetres) + "].", failures);
            Expect(Num(b.startStationMetres) == Num(60f) && Num(b.endStationMetres) == Num(90f), "second gap should "
                + "be [60, 90] (overlap merged, tail clipped), was [" + Num(b.startStationMetres) + ", "
                + Num(b.endStationMetres) + "].", failures);
            Expect(a.LengthMetres > 0f && b.LengthMetres > 0f, "segmentation produced a zero-length sub-run.",
                failures);
        }

        /// <summary> (7) A sub-run shorter than its own two ends is refused, not built as a stub. A fence's two
        /// strainer posts need 0.6 m between them; an opening that leaves 0.4 m at each end refuses both. </summary>
        private static void CaseShortSubRunIsRefused(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Fence();
            r.totalLengthMetres = 10f;
            r.openings = new List<RunOpening> { new RunOpening(0.4f, 9.6f, RunOpeningKind.Crossing) };
            EdgeRunSolution sol = EdgeRunSolver.Solve(r);

            Expect(sol.SegmentCount == 2, "the opening should leave two short stubs, produced " + sol.SegmentCount
                + ".", failures);
            Expect(sol.refused, "a run whose stubs are shorter than their own two ends should be refused.", failures);
            bool bothRefused = sol.SegmentCount == 2 && sol.segments[0].refused && sol.segments[1].refused;
            Expect(bothRefused, "both 0.4 m stubs should be refused (0.6 m of ends will not fit).", failures);
            bool named = sol.SegmentCount == 2 && sol.segments[0].refusal != null
                && sol.segments[0].refusal.Contains("shorter than its own two ends");
            Expect(named, "the short-stub refusal should say it is shorter than its own two ends; was: "
                + (sol.SegmentCount == 2 ? sol.segments[0].refusal : "(no segment)"), failures);
        }

        /// <summary> (3) A body whose length is not a whole number of 3.81 m rail parts closes with ONE short part
        /// carrying the remainder -- the spacing of the whole parts is untouched (never scaled). Both figures are
        /// worked by hand: 20 m gives 5 whole parts (19.05 m) with a 0.95 m offcut below the 1.905 m minimum, so
        /// one part is dropped and the closing part becomes 0.95 + 3.81 = 4.76 m over 4 whole parts. 21.5 m gives a
        /// legal 2.45 m closing part over 5 whole parts. Both cover the body exactly. </summary>
        private static void CaseClosingPartCarriesRemainderNeverScales(List<string> failures)
        {
            EdgeRunType g = EdgeRunType.Guardrail();
            RunQuantisation q1 = EdgeRunQuantiser.Quantise(g, 20f);
            Expect(!q1.refused, "a 20 m guardrail body should quantise, was refused: " + q1.refusal, failures);
            Expect(q1.wholePartCount == 4, "20 m should drop to 4 whole parts, gave " + q1.wholePartCount + ".",
                failures);
            Expect(Num(q1.closingPartLengthMetres) == Num(4.76f), "the dropped-part closing length should be "
                + "4.76 m, was " + Num(q1.closingPartLengthMetres) + ".", failures);
            Expect(Num(q1.partSpacingMetres) == Num(3.81f), "the whole-part spacing must stay 3.81 m (never "
                + "scaled), was " + Num(q1.partSpacingMetres) + ".", failures);
            Expect(Mathf.Abs(q1.BodyLengthCoveredMetres - 20f) <= 1e-3f, "the quantisation should cover the 20 m "
                + "body exactly, covered " + Num(q1.BodyLengthCoveredMetres) + ".", failures);

            RunQuantisation q2 = EdgeRunQuantiser.Quantise(g, 21.5f);
            Expect(q2.wholePartCount == 5, "21.5 m should keep 5 whole parts, gave " + q2.wholePartCount + ".",
                failures);
            Expect(Num(q2.closingPartLengthMetres) == Num(2.45f), "the closing part should be 2.45 m, was "
                + Num(q2.closingPartLengthMetres) + ".", failures);
            Expect(Num(q2.partSpacingMetres) == Num(3.81f), "the whole-part spacing must stay 3.81 m, was "
                + Num(q2.partSpacingMetres) + ".", failures);
            Expect(Mathf.Abs(q2.BodyLengthCoveredMetres - 21.5f) <= 1e-3f, "the quantisation should cover the "
                + "21.5 m body exactly, covered " + Num(q2.BodyLengthCoveredMetres) + ".", failures);
        }

        /// <summary> (3) The distribute-spacing policy nudges the fence's 2.4 m spacing within tolerance rather than
        /// scaling a panel, and refuses when the nudge would exceed it. 25 m over 10 panels is a 2.5 m spacing --
        /// a 4.17 % change, inside the 6 % tolerance. 13 m forces a 2.6 m spacing (8.33 %), past tolerance, so it is
        /// refused with the numbers named. </summary>
        private static void CaseDistributeSpacingStaysWithinTolerance(List<string> failures)
        {
            EdgeRunType f = EdgeRunType.Fence();
            RunQuantisation qa = EdgeRunQuantiser.Quantise(f, 25f);
            Expect(!qa.refused, "25 m of fence should distribute within tolerance, was refused: " + qa.refusal,
                failures);
            Expect(qa.wholePartCount == 10, "25 m at ~2.5 m spacing is 10 panels, gave " + qa.wholePartCount + ".",
                failures);
            Expect(Mathf.Abs(qa.partSpacingMetres - 2.5f) <= 1e-3f, "the distributed spacing should be 2.5 m, was "
                + Num(qa.partSpacingMetres) + ".", failures);
            Expect(Mathf.Abs(qa.BodyLengthCoveredMetres - 25f) <= 1e-3f, "distributed parts should cover 25 m, "
                + "covered " + Num(qa.BodyLengthCoveredMetres) + ".", failures);

            RunQuantisation qb = EdgeRunQuantiser.Quantise(f, 13f);
            Expect(qb.refused, "13 m of fence needs an 8.3 % spacing change and should be refused, was accepted.",
                failures);
            Expect(qb.refusal != null && qb.refusal.Contains("spacing change"), "the out-of-tolerance refusal "
                + "should name the spacing change; was: " + qb.refusal, failures);
        }

        /// <summary> (4) A run point's height is the ROAD surface plus the run's own vertical offset, and NOTHING
        /// else: a kerb sits above, a buried rail below, and neither consults terrain. Proven two ways -- the value
        /// (12.5 + 0.75 = 13.25, 12.5 - 0.4 = 12.1) and the ABSENCE: no public method of the run model takes a
        /// parameter whose type is a terrain reader, so a terrain height cannot enter even by accident. </summary>
        private static void CaseHeightFollowsRoadSurfaceNotTerrain(List<string> failures)
        {
            Expect(Num(EdgeRunGeometry.RunPointHeightMetres(12.5f, 0.75f)) == Num(13.25f), "a kerb 0.75 m above a "
                + "12.5 m road surface should sit at 13.25 m, was "
                + Num(EdgeRunGeometry.RunPointHeightMetres(12.5f, 0.75f)) + ".", failures);
            Expect(Num(EdgeRunGeometry.RunPointHeightMetres(12.5f, -0.4f)) == Num(12.1f), "a rail buried 0.4 m "
                + "below a 12.5 m road surface should sit at 12.1 m, was "
                + Num(EdgeRunGeometry.RunPointHeightMetres(12.5f, -0.4f)) + ".", failures);

            System.Type[] runModel =
            {
                typeof(EdgeRunGeometry), typeof(EdgeRunSolver), typeof(EdgeRunQuantiser),
                typeof(EdgeRunType), typeof(RunTransitionCatalog)
            };
            int terrainParams = 0;
            for (int i = 0; i < runModel.Length; i++)
            {
                MethodInfo[] methods = runModel[i].GetMethods(BindingFlags.Public | BindingFlags.Static
                    | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                for (int m = 0; m < methods.Length; m++)
                {
                    ParameterInfo[] ps = methods[m].GetParameters();
                    for (int p = 0; p < ps.Length; p++)
                    {
                        if (ps[p].ParameterType.Name.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase)
                            >= 0) { terrainParams++; }
                    }
                }
            }
            Expect(terrainParams == 0, "the run model must not accept a terrain reader anywhere (anti-sink is an "
                + "absence); found " + terrainParams + " terrain parameter(s).", failures);
        }

        /// <summary> (5) Lateral offset is read from the EMITTED cross-section edge at each station, so a run over a
        /// carriageway that widens from 3.6 m to 6.0 m half-width across 100 m moves outboard with it: the outer
        /// edge shifts by 4.8 m (2 x the half-width growth) and an extra offset adds on top of the tracked edge.
        /// A run at a constant offset that ignored the section would show zero shift. </summary>
        private static void CaseLateralOffsetTracksWidenedEdge(List<string> failures)
        {
            DividedRoadProfile profile = DividedRoad.Undivided(14.4f, 0.02f, 3.0f, 4);
            VariableWidthProfile widening;
            widening.halfWidthOverDistance = AnimationCurve.Linear(0f, 3.6f, 100f, 6.0f);
            widening.minEmitHalfWidth = 0.001f;
            profile.carriagewayHalfWidth = widening;

            float o0 = EdgeRunGeometry.LateralOffsetAt(profile, 0f, RunSide.Right, 0f);
            float o1 = EdgeRunGeometry.LateralOffsetAt(profile, 100f, RunSide.Right, 0f);
            float shift = EdgeRunGeometry.EdgeShiftMetres(profile, 0f, 100f, RunSide.Right);

            Expect(shift > 0.5f, "a run should track a widening shoulder; the edge shift over the widen was "
                + Num(shift) + " m (expected the road to widen).", failures);
            Expect(Mathf.Abs(o1) > Mathf.Abs(o0) + 0.5f, "the offset at the wide station (" + Num(o1)
                + ") should exceed the narrow one (" + Num(o0) + ") -- otherwise the run ignores the section.",
                failures);
            float withExtra = EdgeRunGeometry.LateralOffsetAt(profile, 0f, RunSide.Right, 2f);
            Expect(Num(withExtra) == Num(o0 + 2f), "a 2 m extra offset should sit 2 m outboard of the tracked edge; "
                + "edge " + Num(o0) + " + 2 should be " + Num(o0 + 2f) + ", was " + Num(withExtra) + ".", failures);
        }

        /// <summary> (6) An ordered type pair with an authored transition part returns it; a pair with none is a
        /// lookup miss the caller must refuse by name, not a silent butt joint. The authored guardrail<->parapet
        /// transition is 11.43 m (three 3.81 m rail sections) both directions; fence -> guardrail is deliberately
        /// undefined and must refuse. </summary>
        private static void CaseTransitionRefusedByNameWhereUndefined(List<string> failures)
        {
            RunTransitionCatalog cat = RunTransitionCatalog.Default();
            RunTransition tr;
            Expect(cat.TryGet(0, 3, out tr), "the guardrail -> parapet transition should be defined.", failures);
            Expect(tr.partCategory == "Barrier/Transition/GuardrailToParapet", "the guardrail -> parapet part "
                + "category was wrong: " + tr.partCategory, failures);
            Expect(Num(tr.lengthMetres) == Num(11.43f), "the stiffness transition should be 11.43 m, was "
                + Num(tr.lengthMetres) + ".", failures);
            Expect(cat.TryGet(3, 0, out tr), "the parapet -> guardrail transition (reverse direction) should be "
                + "defined as its own row.", failures);
            Expect(!cat.TryGet(1, 0, out tr), "fence -> guardrail is undefined and TryGet should miss, but it "
                + "returned a part.", failures);
            string refusal = RunTransitionCatalog.RefusalFor(EdgeRunType.Fence(), EdgeRunType.Guardrail());
            Expect(refusal != null && refusal.Contains("no transition part"), "the undefined-pair refusal should "
                + "name the missing transition part; was: " + refusal, failures);
        }

        /// <summary> (C-06) Solving the same request twice is bit-identical: nothing on the generation path uses a
        /// clock, a GUID or a hash, so a re-solve after moving an opening cannot drift. </summary>
        private static void CaseTwoSolvesAreBitIdentical(List<string> failures)
        {
            EdgeRunRequest r = new EdgeRunRequest();
            r.type = EdgeRunType.Guardrail();
            r.totalLengthMetres = 200f;
            r.barrierSite = MedianContent.Site(9.0f, 100f);
            r.openings = new List<RunOpening> { new RunOpening(90f, 110f, RunOpeningKind.Driveway) };
            string a = Summarise(EdgeRunSolver.Solve(r));
            string b = Summarise(EdgeRunSolver.Solve(r));
            Expect(a == b, "two solves of one request differed:\n  A: " + a + "\n  B: " + b, failures);
        }

        /// <summary> A stable, culture-invariant fingerprint of a solution for the determinism check. </summary>
        private static string Summarise(EdgeRunSolution sol)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(sol.refused ? "R" : "-").Append('|').Append(sol.refusal).Append('|').Append(sol.SegmentCount);
            if (sol.segments != null)
            {
                for (int i = 0; i < sol.segments.Count; i++)
                {
                    EdgeRunSegment s = sol.segments[i];
                    sb.Append("||").Append(Num(s.startStationMetres)).Append(',').Append(Num(s.endStationMetres))
                        .Append(',').Append((int)s.startEnd.kind).Append(',').Append((int)s.endEnd.kind).Append(',')
                        .Append(s.body.wholePartCount).Append(',').Append(Num(s.body.closingPartLengthMetres))
                        .Append(',').Append(s.refused ? "R" : "-").Append(',').Append(s.refusal);
                }
            }
            return sb.ToString();
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(failure); }
        }

        private static string Num(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }
    }
}
#endif
