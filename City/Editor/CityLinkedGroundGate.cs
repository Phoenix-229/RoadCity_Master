// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-99: the solid-ground guarantee across the link.
    ///
    /// RM-98 proved that a write to a tile this tool owns cannot leave a step or a hole. That proof says nothing about
    /// the case that actually ships, where the world owns the ground and the tool asks. Four things can go wrong there
    /// and none of them can go wrong standalone:
    ///
    /// 1. The two paths disagree. The tool reads ground through <see cref="ITerrainSurfaceReader"/> either way, but one
    ///    implementation interpolates a heightfield and the other forwards to a provider computing whatever it computes.
    ///    A road built against one and standing on the other is a road at the wrong height, and the error is small
    ///    enough to look like a shading artefact until somebody drives on it.
    /// 2. The region handed over is not ground the provider has. A request that pokes past the declared chunks is not
    ///    refused -- the provider modifies the part it owns -- so half a corridor comes back untouched under a finished
    ///    road.
    /// 3. The host regenerates its terrain under a road that is already built, and nobody says anything. This is the one
    ///    with no standalone equivalent at all, because standalone nothing else is allowed to move the ground.
    /// 4. A linked build is cancelled halfway and the host is left holding an opening for a tunnel that no longer
    ///    exists.
    ///
    /// Each sweep below answers one of those, and every tolerance comes from the project's own
    /// <see cref="TerrainBuildSettings.Watertight"/> class rather than from a number typed in here -- a gate that
    /// carried its own copy of the probe tolerance would keep passing after somebody tightened the real one. </summary>
    public static class CityLinkedGroundGate
    {
        #region "The shared ground"
        // One tile, one plane, and a provider computing the same plane in closed form. A plane is chosen because it is
        // the only ground whose height at an arbitrary world point can be stated exactly, so a disagreement between the
        // two paths cannot be blamed on the two of them having sampled different shapes.
        private const int Res = 129;
        private const double Range = 600.0;
        private const double Span = 1000.0;
        private const double PerMetreX = 0.01;
        private const double PerMetreZ = 0.02;

        private static WatertightWriteClass Cls() { return TerrainBuildSettings.Watertight; }

        private static double Spacing() { return WatertightTerrainWrite.SampleSpacingMetres(Span, Res); }

        private static ArrayHeightfield PlaneTile()
        {
            ArrayHeightfield tile = new ArrayHeightfield(Res, Range, Span);
            tile.FillPlane(PerMetreX, PerMetreZ, 0.0);
            return tile;
        }
        #endregion


        [MenuItem("Window/Road City Master/Testing/Linked ground and host regeneration")]
        private static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            UnityEngine.Debug.Log(report);
            if (!passed) { UnityEngine.Debug.LogError("RCMLINKEDGROUND FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            Run(out report);
            UnityEngine.Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();
            StringBuilder measured = new StringBuilder();

            SweepModeAgreement(failures, measured);
            SweepRegionHandedOver(failures, measured);
            SweepOrdering(failures, measured);
            SweepStaleness(failures, measured);
            SweepCancelledLinkedBuild(failures, measured);
            SweepGatewayWiring(failures, measured);

            StringBuilder b = new StringBuilder();
            b.Append("RM-99 linked ground gate: ").Append(failures.Count == 0 ? "PASS" : "FAIL").Append('\n');
            for (int i = 0; i < failures.Count; i++) { b.Append("  - ").Append(failures[i]).Append('\n'); }
            b.Append(measured.ToString());
            report = b.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string complaint, List<string> failures)
        {
            if (!condition) { failures.Add(complaint); }
        }


        #region "Sweep 1 -- the same ground down both paths"
        /// <summary> Build the identical probe line twice: once through the tile, once through a provider, and require
        /// the two to agree.
        ///
        /// The comparison is held to <see cref="WatertightWriteClass.probeToleranceMetres"/> -- RM-98's own probe
        /// tolerance -- so "the probe sweep passes in both modes" is a statement about ONE standard rather than two. It
        /// is also held to something stricter and more informative: the disagreement must fit inside the height quantum
        /// a real tile of this range would introduce, which is the LARGEST difference the two paths are entitled to have
        /// once they agree about where they are. Anything bigger is a coordinate error, and a coordinate error is
        /// exactly what a loose tolerance hides. </summary>
        private static void SweepModeAgreement(List<string> failures, StringBuilder measured)
        {
            WatertightWriteClass cls = Cls();
            ArrayHeightfield tile = PlaneTile();
            CountingProvider provider = new CountingProvider();
            ProviderTerrainSink linked = new ProviderTerrainSink(provider, "counting provider");

            Vector2 from = new Vector2(100f, 150f);
            Vector2 to = new Vector2(900f, 850f);
            SurfaceLine standaloneLine = LinkedGroundCheck.ProbeLine(tile, from, to, cls);
            SurfaceLine linkedLine = LinkedGroundCheck.ProbeLine(linked, from, to, cls);

            Expect(tile.sampleCalls == standaloneLine.Count,
                "the tile was asked " + tile.sampleCalls.ToString(CultureInfo.InvariantCulture)
                + " times for a line of " + standaloneLine.Count.ToString(CultureInfo.InvariantCulture)
                + " probes, so the standalone side of the comparison did not come from the tile", failures);
            Expect(standaloneLine.unreadable == 0,
                standaloneLine.unreadable.ToString(CultureInfo.InvariantCulture)
                + " standalone probes fell outside the tile: the line is supposed to be inside it", failures);
            Expect(linkedLine.unreadable == 0, "the provider failed to answer "
                + linkedLine.unreadable.ToString(CultureInfo.InvariantCulture) + " probes", failures);

            int atProbe;
            double worst = LinkedGroundCheck.WorstModeDisagreementMetres(standaloneLine, linkedLine, out atProbe);
            double quantum = WatertightTerrainWrite.HeightQuantumMetres(Range);

            Expect(!double.IsPositiveInfinity(worst),
                "the two modes produced lines that cannot be compared -- different probe counts, or one answered where "
                + "the other did not, at probe " + atProbe.ToString(CultureInfo.InvariantCulture), failures);
            Expect(worst <= cls.probeToleranceMetres,
                "the two modes disagree by " + worst.ToString("F4", CultureInfo.InvariantCulture)
                + " m at probe " + atProbe.ToString(CultureInfo.InvariantCulture) + ", past the "
                + cls.probeToleranceMetres.ToString("F3", CultureInfo.InvariantCulture)
                + " m probe tolerance RM-98 holds a single surface to", failures);
            Expect(worst <= quantum,
                "the two modes disagree by " + worst.ToString("F4", CultureInfo.InvariantCulture)
                + " m, which is more than the tile's own height quantum of "
                + quantum.ToString("F5", CultureInfo.InvariantCulture)
                + " m: quantisation is the only difference the two paths are entitled to, so this is a coordinate error",
                failures);

            // Continuity, asked of both surfaces to the same limit RM-98 uses on a written one.
            double maxStep = WatertightTerrainWrite.MaximumSeamStepMetres(Spacing(), cls);
            Expect(standaloneLine.worstStepMetres <= maxStep,
                "the standalone surface steps " + standaloneLine.worstStepMetres.ToString("F4",
                    CultureInfo.InvariantCulture) + " m between neighbouring probes, past the "
                + maxStep.ToString("F3", CultureInfo.InvariantCulture) + " m traversable limit", failures);
            Expect(linkedLine.worstStepMetres <= maxStep,
                "the provider's surface steps " + linkedLine.worstStepMetres.ToString("F4",
                    CultureInfo.InvariantCulture) + " m between neighbouring probes, past the "
                + maxStep.ToString("F3", CultureInfo.InvariantCulture) + " m traversable limit", failures);

            measured.Append("  probes ").Append(standaloneLine.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" per mode at ").Append(cls.probeSpacingMetres.ToString("F2",
                        CultureInfo.InvariantCulture))
                    .Append(" m; worst mode disagreement ").Append(worst.ToString("F5", CultureInfo.InvariantCulture))
                    .Append(" m vs quantum ").Append(quantum.ToString("F5", CultureInfo.InvariantCulture))
                    .Append(" m and tolerance ")
                    .Append(cls.probeToleranceMetres.ToString("F3", CultureInfo.InvariantCulture)).Append(" m\n");
            measured.Append("  worst neighbour step standalone ")
                    .Append(standaloneLine.worstStepMetres.ToString("F4", CultureInfo.InvariantCulture))
                    .Append(" m, linked ")
                    .Append(linkedLine.worstStepMetres.ToString("F4", CultureInfo.InvariantCulture))
                    .Append(" m, limit ").Append(maxStep.ToString("F3", CultureInfo.InvariantCulture)).Append(" m\n");
        }
        #endregion


        #region "Sweep 2 -- the region actually handed over"
        private static void SweepRegionHandedOver(List<string> failures, StringBuilder measured)
        {
            CountingProvider provider = new CountingProvider();
            IList<Bounds> declared = provider.GetActiveChunkBounds();
            string why;

            TerrainRegion inside = TerrainRegion.FromBounds(new Bounds(new Vector3(200f, 20f, 300f),
                new Vector3(60f, 40f, 60f)));
            Expect(LinkedGroundCheck.RegionWithinDeclared(inside, declared, out why),
                "a region well inside the provider's declared ground was reported as outside it: " + why, failures);

            // Deliberately straddling the far edge: the half inside would be modified and the half outside would not,
            // which is the failure that shows up as a road that stops conforming partway along.
            TerrainRegion straddling = TerrainRegion.FromBounds(new Bounds(new Vector3(1990f, 20f, 0f),
                new Vector3(60f, 40f, 60f)));
            bool straddlingAccepted = LinkedGroundCheck.RegionWithinDeclared(straddling, declared, out why);
            Expect(!straddlingAccepted,
                "a region straddling the edge of the provider's declared ground was accepted, so half of it would be "
                + "silently dropped", failures);
            Expect(!straddlingAccepted && why.Contains("2000"),
                "the straddling region was refused without naming the extents, which leaves an author with a refusal "
                + "and no coordinates: \"" + why + "\"", failures);

            Expect(!LinkedGroundCheck.RegionWithinDeclared(inside, new List<Bounds>(), out why),
                "a region was accepted against a provider that declared no ground at all", failures);
            Expect(!LinkedGroundCheck.RegionWithinDeclared(TerrainRegion.Empty, declared, out why),
                "an empty region was accepted, which would announce nothing and be counted as work", failures);

            measured.Append("  declared ground x ")
                    .Append(declared[0].min.x.ToString("F0", CultureInfo.InvariantCulture)).Append("..")
                    .Append(declared[0].max.x.ToString("F0", CultureInfo.InvariantCulture)).Append(" z ")
                    .Append(declared[0].min.z.ToString("F0", CultureInfo.InvariantCulture)).Append("..")
                    .Append(declared[0].max.z.ToString("F0", CultureInfo.InvariantCulture)).Append('\n');
        }
        #endregion


        #region "Sweep 3 -- the ordering contract, proved by breaking it"
        /// <summary> Every wrong order is fed in as well as the right one. A check that has only ever seen the correct
        /// sequence is a check that returns None. </summary>
        private static void SweepOrdering(List<string> failures, StringBuilder measured)
        {
            List<HostRegenerationStep> correct = new List<HostRegenerationStep>(HostRegenerationContract.Steps);
            Expect(HostRegenerationContract.Check(correct) == HostRegenerationViolation.None,
                "the contract's own step order was rejected by its own check", failures);

            // A host regenerating two chunks does the middle steps twice. That must remain legal or the check gets
            // switched off by the first real caller.
            List<HostRegenerationStep> twoChunks = new List<HostRegenerationStep>();
            twoChunks.Add(HostRegenerationStep.ReleaseHolds);
            twoChunks.Add(HostRegenerationStep.RegenerateGround);
            twoChunks.Add(HostRegenerationStep.EvaluateDrift);
            twoChunks.Add(HostRegenerationStep.RegenerateGround);
            twoChunks.Add(HostRegenerationStep.EvaluateDrift);
            twoChunks.Add(HostRegenerationStep.RebuildOrReport);
            Expect(HostRegenerationContract.Check(twoChunks) == HostRegenerationViolation.None,
                "regenerating two chunks in one pass was rejected, so the check would be turned off by its first real "
                + "caller", failures);

            List<HostRegenerationStep> holding = new List<HostRegenerationStep>();
            holding.Add(HostRegenerationStep.RegenerateGround);
            holding.Add(HostRegenerationStep.ReleaseHolds);
            holding.Add(HostRegenerationStep.EvaluateDrift);
            holding.Add(HostRegenerationStep.RebuildOrReport);
            Expect(HostRegenerationContract.Check(holding) == HostRegenerationViolation.RegeneratedWhileHolding,
                "regenerating the ground while the tool still held openings on it was not caught", failures);

            List<HostRegenerationStep> early = new List<HostRegenerationStep>();
            early.Add(HostRegenerationStep.ReleaseHolds);
            early.Add(HostRegenerationStep.EvaluateDrift);
            early.Add(HostRegenerationStep.RegenerateGround);
            early.Add(HostRegenerationStep.RebuildOrReport);
            Expect(HostRegenerationContract.Check(early) == HostRegenerationViolation.EvaluatedBeforeRegenerating,
                "measuring drift against ground that had not been replaced yet was not caught, which is the sequence "
                + "that reports agreement immediately after a regeneration", failures);

            List<HostRegenerationStep> silent = new List<HostRegenerationStep>();
            silent.Add(HostRegenerationStep.ReleaseHolds);
            silent.Add(HostRegenerationStep.RegenerateGround);
            silent.Add(HostRegenerationStep.EvaluateDrift);
            Expect(HostRegenerationContract.Check(silent)
                    == HostRegenerationViolation.FinishedWithoutRebuildOrReport,
                "a sequence that measured the drift and then said nothing was accepted", failures);

            Expect(HostRegenerationContract.Check(new List<HostRegenerationStep>())
                    == HostRegenerationViolation.Incomplete,
                "an empty sequence was accepted as a completed regeneration", failures);
            Expect(HostRegenerationContract.Check(null) == HostRegenerationViolation.Incomplete,
                "a missing sequence was accepted as a completed regeneration", failures);

            Expect(HostRegenerationContract.Sentence.Length > 0
                    && HostRegenerationContract.Sentence.Contains("release"),
                "the contract has no sentence to show an author", failures);

            measured.Append("  ordering contract ")
                    .Append(HostRegenerationContract.Steps.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" steps, 5 wrong orders each named\n");
        }
        #endregion


        #region "Sweep 4 -- what the ground moving means"
        /// <summary> The verdicts, one synthetic road each. Records are assembled directly rather than by walking a
        /// spline: the rule being tested is what a measured difference MEANS, and building a road to produce the
        /// difference would make a failure ambiguous between the rule and the build. The spline walk itself is
        /// <see cref="GroundConformRecord.Record"/> and is exercised by the fixture's own builds. </summary>
        private static void SweepStaleness(List<string> failures, StringBuilder measured)
        {
            GroundDriftClass cls = TerrainBuildSettings.Drift;

            Expect(cls.supportToleranceMetres < cls.visibleMismatchMetres,
                "the formation tolerance is not smaller than the visible-mismatch threshold, so there is no advisory "
                + "band between them and every small settlement reads as urgent", failures);

            // Inside the formation tolerance the road is still bearing on the ground it was built to. Tested just inside
            // and just outside rather than exactly on the boundary: the tolerance is 25 mm, which float32 cannot hold
            // exactly, so an assertion pinned to the boundary would be testing the representation.
            Expect(VerdictFor(0.0, cls) == GroundVerdict.Agrees, "unmoved ground did not read as agreement", failures);
            Expect(VerdictFor(cls.supportToleranceMetres * 0.9, cls) == GroundVerdict.Agrees,
                "ground that moved less than the formation tolerance was reported as a defect, which is reporting the "
                + "specification as an error", failures);
            Expect(VerdictFor(cls.supportToleranceMetres * 1.5, cls) == GroundVerdict.RefreshAdvised,
                "ground that moved half again past the formation tolerance was still reported as agreeing", failures);

            GroundDrift buried = DriftFor(0.05, cls);
            Expect(buried.verdict == GroundVerdict.RefreshAdvised && buried.motion == GroundMotion.Rose,
                "ground 50 mm higher than the road was built to did not read as advised-and-buried, it read as "
                + buried.verdict.ToString() + "/" + buried.motion.ToString(), failures);

            GroundDrift floating = DriftFor(-0.30, cls);
            Expect(floating.verdict == GroundVerdict.RefreshRequired && floating.motion == GroundMotion.Fell,
                "ground 300 mm below the road did not read as required-and-floating, it read as "
                + floating.verdict.ToString() + "/" + floating.motion.ToString(), failures);
            Expect(floating.worstStation >= 0 && floating.Describe("road").Contains("floating"),
                "a floating road was not described as floating, or was not given a station to look at", failures);

            // A hillside re-graded rather than shifted: up at one station, down at another.
            GroundConformRecord reshaped = FlatRecord("reshaped", 3);
            MovingReader mover = new MovingReader();
            mover.deltaByX[0f] = 0.4f;
            mover.deltaByX[20f] = 0f;
            mover.deltaByX[40f] = -0.4f;
            GroundDrift reshapedDrift = reshaped.Evaluate(mover, cls);
            Expect(reshapedDrift.motion == GroundMotion.Reshaped,
                "ground that rose at one station and fell at another read as " + reshapedDrift.motion.ToString()
                + " rather than reshaped, which is the one case that cannot be answered by moving the road", failures);
            Expect(reshapedDrift.verdict == GroundVerdict.RefreshRequired,
                "a 400 mm reshape did not require a refresh", failures);

            // A bridge deck standing clear of the ground is the deck working. It must not be flagged.
            GroundConformRecord bridged = FlatRecord("all bridge", 3);
            for (int i = 0; i < bridged.stations.Count; i++)
            {
                GroundStation s = bridged.stations[i];
                s.grounded = false;
                bridged.stations[i] = s;
            }
            GroundDrift bridgeDrift = bridged.Evaluate(new OffsetReader(-25f), cls);
            Expect(bridgeDrift.verdict == GroundVerdict.Agrees,
                "a valley re-graded 25 m under a bridge was reported as the bridge being wrong", failures);
            Expect(bridgeDrift.stationsMeasured == 0,
                "bridge stations were measured against the ground, so every span would report as floating", failures);

            // The two answers that must never come out as agreement.
            GroundConformRecord empty = new GroundConformRecord();
            Expect(empty.Evaluate(new OffsetReader(0f), cls).verdict == GroundVerdict.NeverRecorded,
                "a road with no record was judged rather than reported as unrecorded", failures);
            Expect(FlatRecord("no reader", 3).Evaluate(null, cls).verdict == GroundVerdict.GroundUnavailable,
                "a road with no terrain to read was reported as agreeing, which is how a disconnected provider turns "
                + "into a scene full of floating roads", failures);
            Expect(FlatRecord("nan", 3).Evaluate(new OffsetReader(float.NaN), cls).verdict
                    == GroundVerdict.GroundUnavailable,
                "ground that answered not-a-number was not reported as unreadable", failures);

            // The report itself: it must be impossible to mistake "nothing needs refreshing" for "nothing was checked".
            StaleRoadReport report = new StaleRoadReport();
            report.Add("agreeing road", DriftFor(0.0, cls));
            report.Add("floating road", DriftFor(-0.30, cls));
            report.Add("unrecorded road", new GroundConformRecord().Evaluate(new OffsetReader(0f), cls));
            IList<string> needing = report.RoadsNeedingRefresh();
            Expect(report.Considered == 3,
                "the report lost a road: it considered " + report.Considered.ToString(CultureInfo.InvariantCulture)
                + " of 3", failures);
            Expect(needing.Count == 2 && needing.Contains("floating road") && needing.Contains("unrecorded road"),
                "the roads needing a refresh were not both named -- an unrecorded road is an action item, not a pass",
                failures);
            Expect(!report.AllAgree, "a report holding a floating road claimed everything agreed", failures);
            string described = report.Describe();
            Expect(described.Contains("considered 3") && described.Contains("floating road"),
                "the report does not state how many roads it considered, so a report that checked nothing looks like a "
                + "report that found nothing", failures);

            StaleRoadReport quiet = new StaleRoadReport();
            quiet.Add("one good road", DriftFor(0.0, cls));
            Expect(quiet.AllAgree && quiet.Describe().Contains("considered 1"),
                "an all-clear report does not say how many roads were behind the all-clear", failures);

            measured.Append("  drift bands: agrees below ")
                    .Append(cls.supportToleranceMetres.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" m, advised to ")
                    .Append(cls.visibleMismatchMetres.ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" m, required above; stations at ")
                    .Append(cls.stationSpacingMetres.ToString("F0", CultureInfo.InvariantCulture))
                    .Append(" m capped at ").Append(cls.maximumStations.ToString(CultureInfo.InvariantCulture))
                    .Append('\n');
        }

        /// <summary> A short flat record on ground at 100 m, stations 20 m apart along world X. </summary>
        private static GroundConformRecord FlatRecord(string name, int stationCount)
        {
            GroundConformRecord record = new GroundConformRecord();
            record.roadName = name;
            record.effectiveStationSpacingMetres = TerrainBuildSettings.Drift.stationSpacingMetres;
            for (int i = 0; i < stationCount; i++)
            {
                GroundStation s = new GroundStation();
                s.worldX = (float)(i * record.effectiveStationSpacingMetres);
                s.worldZ = 0f;
                s.groundYAtBuild = 100f;
                s.roadYAtBuild = 100f;
                s.grounded = true;
                record.stations.Add(s);
            }
            return record;
        }

        private static GroundDrift DriftFor(double deltaMetres, GroundDriftClass cls)
        {
            return FlatRecord("test road", 3).Evaluate(new OffsetReader((float)(100.0 + deltaMetres)), cls);
        }

        private static GroundVerdict VerdictFor(double deltaMetres, GroundDriftClass cls)
        {
            return DriftFor(deltaMetres, cls).verdict;
        }

        /// <summary> Ground at one fixed elevation everywhere, which is all a verdict test needs. </summary>
        private sealed class OffsetReader : ITerrainSurfaceReader
        {
            private readonly float elevation;

            public OffsetReader(float elevation) { this.elevation = elevation; }

            public float SampleHeight(float worldX, float worldZ) { return elevation; }

            public Vector3 SampleNormal(float worldX, float worldZ) { return Vector3.up; }

            public List<Bounds> GetActiveChunkBounds() { return new List<Bounds>(); }
        }

        /// <summary> Ground that moved by a different amount at each station, for the reshape case. </summary>
        private sealed class MovingReader : ITerrainSurfaceReader
        {
            public readonly Dictionary<float, float> deltaByX = new Dictionary<float, float>();

            public float SampleHeight(float worldX, float worldZ)
            {
                float delta;
                return deltaByX.TryGetValue(worldX, out delta) ? 100f + delta : 100f;
            }

            public Vector3 SampleNormal(float worldX, float worldZ) { return Vector3.up; }

            public List<Bounds> GetActiveChunkBounds() { return new List<Bounds>(); }
        }
        #endregion


        #region "Sweep 5 -- a linked build that was cancelled"
        /// <summary> Cancelling a linked build must leave the host's terrain exactly as it was, and must leave the host
        /// holding nothing.
        ///
        /// The first half is structural rather than measured, and that is the strongest form it can take: in connected
        /// mode every write goes to <see cref="ProviderTerrainSink"/>, which counts and discards. So "the host's terrain
        /// was not modified" is not a comparison that could come out either way -- it is the fact that the path with a
        /// write in it was never taken, and the count proves the writes were attempted rather than skipped by a test
        /// that built nothing. The second half is measured: the opening the build asked for has to come back. </summary>
        private static void SweepCancelledLinkedBuild(List<string> failures, StringBuilder measured)
        {
            CountingProvider provider = new CountingProvider();
            ProviderTerrainSink sink = new ProviderTerrainSink(provider, "counting provider");

            Expect(sink.ProviderAlive, "a live provider was reported as gone", failures);
            Expect(sink.Path == TerrainWritePath.RequestedThroughProvider,
                "connected mode did not declare that it requests rather than writes", failures);

            TerrainRegion mouth = TerrainRegion.FromBounds(new Bounds(new Vector3(120f, 100f, 60f),
                new Vector3(20f, 16f, 20f)));
            int handle = sink.RequestHold(TerrainRequestKind.TunnelOpening, mouth);
            Expect(handle > 0 && provider.openTunnels.Count == 1,
                "the tunnel opening the build asked for never reached the provider", failures);
            Expect(sink.OpenTunnelCount == 1,
                "the sink is not tracking the opening it requested, so a cancel has nothing to give back", failures);

            // Mid-build writes, then the cancel. The writes must all have been turned away.
            sink.WriteHeights(null, new float[4, 4]);
            sink.WriteHoles(null, 0, 0, new bool[3, 3]);
            sink.WriteSplat(null, 0, 0, new float[3, 3, 2]);
            sink.Settle(null, true);
            Expect(sink.SuppressedWriteCount == 3,
                "connected mode performed a write: " + sink.SuppressedWriteCount.ToString(CultureInfo.InvariantCulture)
                + " of 3 attempts were suppressed", failures);
            Expect(sink.SettleCount == 1 && sink.HoleSettleCount == 1,
                "the settle was not counted, so the two modes' settle counts cannot be compared", failures);
            Expect(provider.notified.Count == 0,
                "a write announced a region to the host even though nothing was written", failures);

            sink.ReleaseHold(TerrainRequestKind.TunnelOpening, handle);
            Expect(provider.openTunnels.Count == 0 && provider.releasedTunnels == 1,
                "cancelling the build left the host holding a tunnel opening for a tunnel that was never finished",
                failures);
            Expect(sink.OpenTunnelCount == 0, "the sink still thinks it holds an opening it gave back", failures);

            // Nothing about the ground may have changed, which in connected mode means the provider's own surface still
            // answers what it always did.
            Expect(Mathf.Abs(provider.SampleHeight(120f, 60f) - (0.01f * 120f + 0.02f * 60f)) < 1e-4f,
                "the host's surface answered differently after a cancelled build", failures);

            measured.Append("  cancelled linked build: ")
                    .Append(sink.SuppressedWriteCount.ToString(CultureInfo.InvariantCulture))
                    .Append(" writes suppressed, ")
                    .Append(provider.releasedTunnels.ToString(CultureInfo.InvariantCulture))
                    .Append(" openings returned, ")
                    .Append(provider.notified.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" regions announced\n");
        }
        #endregion


        #region "Sweep 6 -- the gateway actually holds the records"
        /// <summary> RM-98 was a library nobody called, and it passed its own tests the whole time. So this sweep leaves
        /// the library alone and tests the WIRING: that the gateway keys records by the same name the record normalised
        /// itself to, that a second recording of one road replaces it instead of accumulating a second copy, that
        /// forgetting a road reports whether it actually matched, that every stored road reaches the report, and that the
        /// regeneration journal on the gateway is the contract from <see cref="HostRegenerationContract"/> rather than a
        /// second implementation of it.
        ///
        /// What it deliberately does NOT do is measure ground movement -- that is Sweep 4's job, on records built to
        /// order. Getting stations into a gateway record requires a real <c>SplineC</c>, which requires a scene and a
        /// road network, and a gate that built one would be slow, order-dependent, and would still be testing
        /// <c>GroundConformRecord.Record</c> rather than the gateway. The spline walk is exercised where roads are
        /// actually built. Recording a null spline here is not a workaround: an unrecordable road is a case the
        /// production path has to survive, and the assertion below is that it comes back named as an action item instead
        /// of vanishing. </summary>
        private static void SweepGatewayWiring(List<string> failures, StringBuilder measured)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "counting provider");

            Expect(gateway.SurfaceReader != null,
                "a connected gateway offered no surface reader, so a record would have nothing to remember", failures);
            if (gateway.SurfaceReader != null)
            {
                float h = gateway.SurfaceReader.SampleHeight(120f, 60f);
                Expect(Mathf.Abs(h - (0.01f * 120f + 0.02f * 60f)) < 1e-4f,
                    "the gateway's surface reader did not answer with the host's own ground", failures);
            }

            Expect(gateway.ConformRecordCount == 0, "a fresh gateway already held conform records", failures);
            StaleRoadReport empty = gateway.EvaluateStaleness();
            Expect(empty != null && empty.Considered == 0 && empty.AllAgree,
                "an empty gateway did not report an empty verdict", failures);

            gateway.RecordConform(null, "route A");
            gateway.RecordConform(null, null);
            Expect(gateway.ConformRecordCount == 2,
                "the gateway did not store one record per road", failures);

            gateway.RecordConform(null, "route A");
            Expect(gateway.ConformRecordCount == 2,
                "re-recording a road added a second copy instead of replacing it", failures);

            StaleRoadReport report = gateway.EvaluateStaleness();
            Expect(report.Considered == 2, "a stored road never reached the report", failures);
            Expect(report.CountWith(GroundVerdict.NeverRecorded) == 2,
                "a road with no stations was not reported as never recorded", failures);
            Expect(report.RoadsNeedingRefresh().Count == 2,
                "an unrecordable road was quietly treated as agreeing", failures);

            // The unnamed road must be findable by the name the RECORD chose, not by the null it was passed --
            // otherwise it can never be forgotten and is reported as stale for the life of the session.
            Expect(gateway.ForgetConform("unnamed road"),
                "the gateway keyed a record by something other than the record's own name", failures);
            Expect(gateway.ForgetConform("route A"), "forgetting a stored road failed", failures);
            Expect(!gateway.ForgetConform("route A"),
                "forgetting an absent road claimed to have matched something", failures);
            Expect(!gateway.ForgetConform(null), "forgetting a null road name claimed a match", failures);
            Expect(gateway.ConformRecordCount == 0, "records survived being forgotten", failures);

            Expect(gateway.CheckRegenerationOrder() == HostRegenerationViolation.Incomplete,
                "a gateway that has done nothing reported a complete regeneration", failures);
            for (int i = 0; i < HostRegenerationContract.Steps.Count; i++)
            {
                gateway.NoteRegenerationStep(HostRegenerationContract.Steps[i]);
            }
            Expect(gateway.RegenerationSequence.Count == HostRegenerationContract.Steps.Count,
                "the gateway's journal lost a step", failures);
            Expect(gateway.CheckRegenerationOrder() == HostRegenerationViolation.None,
                "the contract's own order was rejected when journalled through the gateway", failures);

            gateway.ClearRegenerationJournal();
            Expect(gateway.CheckRegenerationOrder() == HostRegenerationViolation.Incomplete,
                "clearing the journal left the previous verdict standing", failures);

            measured.Append("  gateway wiring: reader ")
                    .Append(gateway.SurfaceReader != null ? "present" : "MISSING")
                    .Append(", ").Append(report.Considered.ToString(CultureInfo.InvariantCulture))
                    .Append(" roads considered, ")
                    .Append(HostRegenerationContract.Steps.Count.ToString(CultureInfo.InvariantCulture))
                    .Append(" journalled steps accepted\n");
        }
        #endregion
    }
}
#endif
