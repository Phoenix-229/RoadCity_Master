#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> What the comparison concluded about one feature. </summary>
    public enum DualModeVerdict
    {
        /// <summary> Both modes produced the same thing, and the availability table says they should have. </summary>
        Identical = 0,

        /// <summary> The two modes differ, and the table says this feature differs by mode. </summary>
        ExpectedDifference = 1,

        /// <summary> The two modes differ where the table says they must not, or agree where the table says
        /// standalone cannot do it at all. Both are failures, and both name the feature. </summary>
        UnexpectedDifference = 2,

        /// <summary> A provider-only feature was refused in standalone, as it should be, but the refusal does not say
        /// what to connect -- so a user meets a feature that does nothing and is told nothing. </summary>
        RefusalNotWorded = 3
    }


    /// <summary> One feature's output from one run, split along the two axes that behave differently.
    ///
    /// <see cref="plan"/> is everything that does not depend on who owns the terrain: centreline stations,
    /// cross-section widths, marking placement, edge-object positions, block subdivision, scatter transforms. It must
    /// be identical in both modes for every feature, with no exceptions, because none of it asks the ground a
    /// question. A difference here is not a mode difference at all -- it is an unseeded generator, a hash-ordered
    /// iteration or a build-order dependency, which is why RM-72 says the test's first useful output is a determinism
    /// defect rather than a mode regression.
    ///
    /// <see cref="request"/> is what the feature asked the terrain gateway for, in call order, WITHOUT the dispatch
    /// path -- RM-57's ledger promise. It must also be identical in both modes: the same fixture asks for the same
    /// ground either way, and only the answer changes.
    ///
    /// <see cref="outcome"/> is the answer, and it is the ONLY axis the availability table is allowed to excuse a
    /// difference on. </summary>
    public struct DualModeFeatureRecord
    {
        public string featureId;
        public string plan;
        public string request;
        public string outcome;

        /// <summary> The words the tool said when it could not do this, empty when it could. </summary>
        public string refusal;
    }


    /// <summary> One whole build of the fixture, in one mode. Everything a comparison needs and nothing that would
    /// make two runs of the same mode differ -- no timestamps, no instance ids, no object names. </summary>
    public sealed class DualModeRun
    {
        public string modeName = string.Empty;
        public TerrainProviderMode mode;

        /// <summary> Which path the gateway dispatched writes down. This ALWAYS differs between the modes -- it is
        /// the gateway's entire purpose -- so it is reported and never compared per feature. </summary>
        public TerrainWritePath path;

        /// <summary> The whole gateway ledger, which must match the other mode's exactly. </summary>
        public string ledgerSignature = string.Empty;

        /// <summary> How many boxes the build announced, and how many builds it closed. Batching is mode-independent,
        /// so these match too. </summary>
        public int notifyCount;
        public int buildCount;

        /// <summary> Holds still outstanding when the fixture finished. Must be zero in both modes: a provider-only
        /// feature that cannot be granted still must not leak, and one that IS granted must be released. </summary>
        public int outstandingHolds;

        /// <summary> Writes the connected sink refused to perform because the provider owns the ground. Zero in
        /// standalone by definition, and reported rather than compared. </summary>
        public int suppressedWrites;

        public readonly List<DualModeFeatureRecord> features = new List<DualModeFeatureRecord>();

        /// <summary> One feature by table id. <paramref name="found"/> is false when the fixture did not exercise it,
        /// which the comparison reports as a gap rather than passing over. </summary>
        public DualModeFeatureRecord Feature(string featureId, out bool found)
        {
            for (int i = 0; i < features.Count; i++)
            {
                if (features[i].featureId == featureId) { found = true; return features[i]; }
            }
            found = false;
            DualModeFeatureRecord missing = new DualModeFeatureRecord();
            missing.featureId = featureId;
            missing.plan = string.Empty;
            missing.request = string.Empty;
            missing.outcome = string.Empty;
            missing.refusal = string.Empty;
            return missing;
        }

        /// <summary> The run as one comparable string, for the twice-in-a-row check. Deliberately includes the
        /// dispatch path, because re-running the SAME mode must reproduce that too. </summary>
        public string Signature()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(mode).Append('|').Append(path).Append('|').Append(notifyCount).Append('|')
              .Append(buildCount).Append('|').Append(outstandingHolds).Append('|').Append(suppressedWrites);
            for (int i = 0; i < features.Count; i++)
            {
                sb.Append('\n').Append(features[i].featureId).Append('\n')
                  .Append(features[i].plan).Append('\n')
                  .Append(features[i].request).Append('\n')
                  .Append(features[i].outcome).Append('\n')
                  .Append(features[i].refusal);
            }
            sb.Append('\n').Append(ledgerSignature);
            return sb.ToString();
        }
    }


    /// <summary> RM-72's fixture: one small, FIXED scene, built through whichever terrain gateway it is handed.
    ///
    /// Fixed rather than generated. RM-09's network operator grows a city from a seed and samples the ground while it
    /// does, which would make the network itself a mode difference and bury the thing this test is looking for. The
    /// nodes below are authored coordinates, so the only variable between two runs is who owns the terrain.
    ///
    /// One instance of each feature class and no more, as the task requires: one divided section with a barrier run
    /// terminated at both ends, one roundabout, one tunnel, one culvert, one block, one junction. Coverage of code
    /// paths is the point; scale belongs to the profiling work. </summary>
    public static class DualModeFixture
    {
        /// <summary> The fixed seed. Every seeded generator in the fixture derives from this, so a flaky run is a
        /// defect in the generator rather than in the test. </summary>
        public const ulong CitySeed = 6072072071UL;

        /// <summary> The block operator's seed, kept separate because it is an int on the operator API. </summary>
        public const int OperatorSeed = 60720;

        private const string Requester = "dual-mode-fixture";

        #region "Authored fixture dimensions"
        // AUTHORED, NOT GENERATED -- fixture coordinates chosen to close exactly one face and to give one node four
        // legs. The dimensions are ordinary urban ones (a 120 m by 90 m block is within the 60-200 m range typical of
        // a gridded downtown) but nothing here is derived from a standard, and nothing downstream should read them as
        // if it were.
        private const float BlockLengthMetres = 120f;
        private const float BlockDepthMetres = 90f;
        private const float DividedRunMetres = 120f;
        private const float StubRunMetres = 70f;

        private const float MedianWidthMetres = 4f;
        private const float DesignSpeedKmh = 80f;

        // A two-lane road tunnel bore: 4.5 m radius clears a 7.3 m carriageway plus verges and the lining. AUTHORED.
        private const double TunnelBoreRadiusMetres = 4.5;
        private const double TunnelCoverMetres = 12.0;

        // Culvert crossing: a small watercourse under the collector, skewed as a real crossing usually is. AUTHORED.
        private const double CulvertFlowM3PerSecond = 1.2;
        private const double CulvertSkewDeg = 75.0;
        private const double CulvertStationMetres = 60.0;

        // The bridge that carries the divided run over that same watercourse. AUTHORED fixture dimensions: a 34 m span
        // sits inside the beam type's own min/max band rather than testing its edges, the footings are 3 m squares
        // (ordinary for a beam-bridge pier base) sunk 2 m, and 5 % is a workable approach gradient for an over-bridge.
        // None of these are derived from a standard and nothing downstream should read them as if they were.
        private const double BridgeSpanLengthMetres = 34.0;
        private const double BridgeApproachGradientFraction = 0.05;
        private const double BridgeFootingEdgeMetres = 3.0;
        private const double BridgeFootingDepthMetres = 2.0;
        private const double BridgeSpanOriginXMetres = 120.0;
        private const double BridgeSpanZMetres = -30.0;

        // Street trees at 8 m centres is common municipal practice for a medium-crown species. AUTHORED spacing.
        private const double TreeSpacingMetres = 8.0;
        private const double TreeJitterFraction = 0.15;
        #endregion


        #region "The build"
        /// <summary> Build the whole fixture through one gateway and record what every feature produced. The gateway
        /// is handed IN rather than read from <see cref="TerrainWriteGateway.Active"/>, so the two runs cannot
        /// accidentally share one mode decision. </summary>
        public static DualModeRun Build(TerrainWriteGateway gateway)
        {
            DualModeRun run = new DualModeRun();
            if (gateway == null) { return run; }

            run.mode = gateway.Mode;
            run.modeName = gateway.Mode == TerrainProviderMode.Connected ? "connected" : "standalone";
            run.path = gateway.PathForWrites;

            RoadClassCatalog catalog = ScriptableObject.CreateInstance<RoadClassCatalog>();
            BlockSubdivisionOperator blockOperator = ScriptableObject.CreateInstance<BlockSubdivisionOperator>();
            try
            {
                catalog.ResetToDefaults();
                blockOperator.catalog = catalog;
                blockOperator.footwayWidth = 2f;
                blockOperator.setback = 3f;
                blockOperator.targetLotArea = 800f;
                blockOperator.splitJitter = 0.35f;

                StreetNetwork network = AuthoredNetwork();
                ITerrainSurfaceReader reader = gateway.SurfaceReader;

                gateway.BeginBuild("dual mode fixture");
                RecordRoadCut(gateway, catalog, network, reader, run);
                RecordRoadRestore(gateway, catalog, reader, run);
                RecordRoadDetails(gateway, run);
                RecordRoadTrees(gateway, run);
                RecordRoadSplat(gateway, run);
                RecordTunnelCavity(gateway, run);
                RecordBridgeDeck(gateway, reader, run);
                RecordBuildingPad(gateway, run);
                RecordCrossChunk(reader, run);
                RecordSampling(reader, run);
                RecordCityBlocks(blockOperator, network, run);
                gateway.EndBuild();

                run.notifyCount = gateway.NotifyCount;
                run.buildCount = gateway.BuildCount;
                run.outstandingHolds = gateway.OutstandingCount;
                run.ledgerSignature = gateway.Ledger.Signature();

                ProviderTerrainSink throughProvider = gateway.Sink as ProviderTerrainSink;
                run.suppressedWrites = throughProvider != null ? throughProvider.SuppressedWriteCount : 0;
            }
            finally
            {
                Object.DestroyImmediate(blockOperator);
                Object.DestroyImmediate(catalog);
            }
            return run;
        }
        #endregion


        #region "The authored network and the shared helpers"
        /// <summary> Six nodes and six edges: one closed face (the block), one node with four legs (the junction), one
        /// divided run east, one stub south that a barrier run terminates on. Authored coordinates, so the network is
        /// the same object in both modes and cannot itself become the difference. </summary>
        public static StreetNetwork AuthoredNetwork()
        {
            StreetNetwork network = new StreetNetwork();
            bool reused;
            int a = network.AddNodeSnapped(new Vector3(0f, 0f, 0f), 0.5f, out reused);
            int b = network.AddNodeSnapped(new Vector3(BlockLengthMetres, 0f, 0f), 0.5f, out reused);
            int c = network.AddNodeSnapped(new Vector3(BlockLengthMetres, 0f, BlockDepthMetres), 0.5f, out reused);
            int d = network.AddNodeSnapped(new Vector3(0f, 0f, BlockDepthMetres), 0.5f, out reused);
            int east = network.AddNodeSnapped(new Vector3(BlockLengthMetres + DividedRunMetres, 0f, 0f), 0.5f,
                out reused);
            int south = network.AddNodeSnapped(new Vector3(BlockLengthMetres, 0f, -StubRunMetres), 0.5f, out reused);

            network.AddEdge(a, b, RoadClassCatalog.CollectorId, false, 0f);
            network.AddEdge(b, c, RoadClassCatalog.LocalId, false, 0f);
            network.AddEdge(c, d, RoadClassCatalog.LocalId, false, 0f);
            network.AddEdge(d, a, RoadClassCatalog.LocalId, false, 0f);
            network.AddEdge(b, east, RoadClassCatalog.ArterialId, true, MedianWidthMetres);
            network.AddEdge(b, south, RoadClassCatalog.LocalId, false, 0f);
            return network;
        }

        /// <summary> Every number in every signature goes through here: four decimals, invariant culture, and no
        /// negative zero -- a signature that reads "-0.0000" on one run and "0.0000" on the next would make the whole
        /// comparison flaky for no reason (C-06). </summary>
        private static string N(double value)
        {
            double v = value == 0.0 ? 0.0 : value;
            return v.ToString("F4", CultureInfo.InvariantCulture);
        }

        private static string N(float value)
        {
            return N((double)value);
        }

        private static string Point(Vector3 p)
        {
            return N(p.x) + "," + N(p.y) + "," + N(p.z);
        }

        /// <summary> Add one feature's record. The <paramref name="featureId"/> must be a real row id in RM-58's
        /// table; the comparison reports an id that is not, rather than skipping it. </summary>
        private static void Add(DualModeRun run, string featureId, string plan, string request, string outcome,
            string refusal)
        {
            DualModeFeatureRecord record;
            record.featureId = featureId;
            record.plan = plan;
            record.request = request;
            record.outcome = outcome;
            record.refusal = refusal != null ? refusal : string.Empty;
            run.features.Add(record);
        }
        #endregion


        #region "The corridor the divided section cuts"
        private const int CorridorSteps = 6;

        /// <summary> One 20 m box of the divided run's corridor. Fixed geometry, so the six boxes are the same six
        /// boxes in both modes -- which is what makes the ledger comparable at all. </summary>
        private static TerrainRegion CorridorBox(int step, float carriagewayWidthMetres)
        {
            double halfWidth = 0.5 * carriagewayWidthMetres + 0.5 * MedianWidthMetres + 3.0;
            double length = DividedRunMetres / CorridorSteps;
            double x0 = BlockLengthMetres + step * length;
            return TerrainRegion.FromExtents(x0, -2.0, -halfWidth, x0 + length, 3.0, halfWidth);
        }

        /// <summary> A real heightfield payload rather than a null one: a 5 by 5 patch falling on the run's authored
        /// grade. Nothing in this fixture owns a <see cref="UnityEngine.Terrain"/>, so in standalone the engine has
        /// nowhere to apply it -- what is under test here is the dispatch, the accounting and the refusals, exactly as
        /// in RM-57's gate, and the report says so rather than implying a heightfield was written. </summary>
        private static float[,] CorridorHeights(int step)
        {
            float[,] heights = new float[5, 5];
            for (int z = 0; z < 5; z++)
            {
                for (int x = 0; x < 5; x++)
                {
                    heights[z, x] = 0.01f * (step * 5 + x) + 0.002f * z;
                }
            }
            return heights;
        }

        /// <summary> The divided run's median as the resolver needs it. Every value is authored fixture data, and the
        /// road surface height is the ROAD's own, never sampled ground -- RM-26's anti-sink rule, which is also what
        /// keeps this half of the plan free of the mode difference. </summary>
        private static MedianSite FixtureMedianSite()
        {
            MedianSite site = new MedianSite();
            site.widthMetres = MedianWidthMetres;
            site.designSpeedKmh = DesignSpeedKmh;
            site.approachLengthMetres = 30f;
            site.backslopeAvailable = false;
            site.lateralOffsetMetres = 2.5f;
            site.startNodeCentre = new Vector3(BlockLengthMetres, 0f, 0f);
            site.endNodeCentre = new Vector3(BlockLengthMetres + DividedRunMetres, 0f, 0f);
            site.roadSurfaceHeightMetres = 0.4f;
            site.tangentAtEndXZ = new Vector2(1f, 0f);
            site.longitudinalGradeFraction = 0.015f;
            site.upstreamInvertMetres = -0.6f;
            site.downstreamInvertMetres = -1.2f;
            site.outfallPresent = true;
            site.lowPointStation = 90f;
            site.treeSpacingMetres = (float)TreeSpacingMetres;
            site.trunkDiameterMetres = 0.3f;
            return site;
        }

        /// <summary> The roundabout's three legs, in degrees. AUTHORED: spread wide enough that the separation check
        /// has something real to accept. </summary>
        private static IList<double> LegBearings()
        {
            return new List<double> { 0.0, 130.0, 240.0 };
        }
        #endregion


        #region "road.cut and road.restore -- better connected"
        /// <summary> How much ground this mode can actually reach. This is where "better connected" lives and it is a
        /// measurement, not an opinion: standalone can only touch terrain tiles that exist in the scene, where a
        /// connected provider reports the ground it owns. </summary>
        private static string Reach(ITerrainSurfaceReader reader)
        {
            int regions = 0;
            if (reader != null)
            {
                List<Bounds> active = reader.GetActiveChunkBounds();
                regions = active != null ? active.Count : 0;
            }
            return "granted, ground reachable in " + regions.ToString(CultureInfo.InvariantCulture) + " region(s)";
        }

        /// <summary> The divided section: its cross-section, its median contents, its barrier run's two terminals, the
        /// roundabout at the far end, and the six corridor boxes the cut asks for. </summary>
        private static void RecordRoadCut(TerrainWriteGateway gateway, RoadClassCatalog catalog,
            StreetNetwork network, ITerrainSurfaceReader reader, DualModeRun run)
        {
            RoadClass arterial = catalog.Get(RoadClassCatalog.ArterialId);
            StringBuilder plan = new StringBuilder();
            plan.Append("carriageway ").Append(N(arterial.TotalWidth))
                .Append(" lanes ").Append(arterial.laneAmount.ToString(CultureInfo.InvariantCulture))
                .Append(" median ").Append(N(MedianWidthMetres))
                .Append(" edges ").Append(network.edges.Count.ToString(CultureInfo.InvariantCulture));

            MedianPlan median = MedianContent.Resolve(FixtureMedianSite(), MedianTroughClass.Default());
            plan.Append("\nmedian ").Append(median.kind)
                .Append(" barrier ").Append(median.hasBarrier)
                .Append(" trough ").Append(median.hasTrough).Append(' ').Append(N(median.troughDepthMetres))
                .Append(" planting ").Append(median.hasPlanting).Append(' ').Append(N(median.plantingSetbackMetres))
                .Append(" refused ").Append(median.refused);
            plan.Append("\nterminal start ").Append(median.startTerminal.treatment)
                .Append(' ').Append(N(median.startTerminal.lengthMetres))
                .Append(" at ").Append(Point(median.startTerminal.placement.position))
                .Append(" yaw ").Append(N(median.startTerminal.placement.yawDegrees))
                .Append(" refused ").Append(median.startTerminal.refused);
            plan.Append("\nterminal end ").Append(median.endTerminal.treatment)
                .Append(' ').Append(N(median.endTerminal.lengthMetres))
                .Append(" at ").Append(Point(median.endTerminal.placement.position))
                .Append(" yaw ").Append(N(median.endTerminal.placement.yawDegrees))
                .Append(" refused ").Append(median.endTerminal.refused);

            RoundaboutClass roundabout = RoundaboutClass.Default();
            RoundaboutGeometry.LegSeparationCheck legs =
                RoundaboutGeometry.CheckLegSeparations(LegBearings(), roundabout);
            plan.Append("\nroundabout circulatory ").Append(N(RoundaboutGeometry.CirculatoryWidthMetres(roundabout)))
                .Append(" island ").Append(N(RoundaboutGeometry.CentralIslandDiameterMetres(roundabout)))
                .Append(" islandValid ").Append(RoundaboutGeometry.CentralIslandIsValid(roundabout))
                .Append(" legs ").Append(legs.accepted).Append(' ').Append(N(legs.worstSeparationDeg))
                .Append(" required ").Append(N(legs.minRequiredDeg));

            StringBuilder request = new StringBuilder();
            for (int step = 0; step < CorridorSteps; step++)
            {
                TerrainRegion box = CorridorBox(step, arterial.TotalWidth);
                TerrainWriteReceipt receipt = gateway.WriteHeights(null, CorridorHeights(step), box,
                    Requester + ".cut");
                if (step > 0) { request.Append('\n'); }
                request.Append("heights ").Append(box.Describe()).Append(" accepted ").Append(receipt.Accepted);
            }

            Add(run, "road.cut", plan.ToString(), request.ToString(), Reach(reader), string.Empty);
        }

        /// <summary> Deleting the divided section again. The same six boxes are handed back, in the same order, which
        /// is the property that stops a provider from keeping a road's shape after the road is gone. </summary>
        private static void RecordRoadRestore(TerrainWriteGateway gateway, RoadClassCatalog catalog,
            ITerrainSurfaceReader reader, DualModeRun run)
        {
            RoadClass arterial = catalog.Get(RoadClassCatalog.ArterialId);
            StringBuilder plan = new StringBuilder();
            StringBuilder request = new StringBuilder();
            for (int step = 0; step < CorridorSteps; step++)
            {
                TerrainRegion box = CorridorBox(step, arterial.TotalWidth);
                TerrainWriteReceipt receipt = gateway.RestoreHeights(null, CorridorHeights(step), box,
                    Requester + ".restore");
                if (step > 0) { plan.Append('\n'); request.Append('\n'); }
                plan.Append("restore extent ").Append(box.Describe())
                    .Append(" area ").Append(N(box.FootprintArea));
                request.Append("restore ").Append(box.Describe()).Append(" accepted ").Append(receipt.Accepted);
            }
            Add(run, "road.restore", plan.ToString(), request.ToString(), Reach(reader), string.Empty);
        }
        #endregion


        #region "road.details, road.trees, road.splat -- identical either way"
        /// <summary> Grass cleared along the corridor, and the drainage that shares it: one skewed culvert crossing
        /// under the collector, sized from its design flow, plus the roadside ditch that feeds it. None of it depends
        /// on who owns the terrain, so all of it must come out identical. </summary>
        private static void RecordRoadDetails(TerrainWriteGateway gateway, DualModeRun run)
        {
            CulvertClass culvert = CulvertClass.Default();
            RoadsideDitchClass ditch = RoadsideDitchClass.Default();
            Culvert.BarrelSizing barrel = Culvert.SizeBarrel(CulvertFlowM3PerSecond, culvert);
            double barrelLength = Culvert.BarrelLengthMetres(14.0, CulvertSkewDeg);
            double outletInvert = Culvert.OutletInvertElevationMetres(-1.2, 0.01, barrelLength);

            StringBuilder plan = new StringBuilder();
            plan.Append("culvert station ").Append(N(CulvertStationMetres))
                .Append(" skew ").Append(N(CulvertSkewDeg))
                .Append(" barrel ").Append(N(barrel.barrelDiameterMetres))
                .Append(" exceedsTable ").Append(barrel.exceedsTable)
                .Append(" length ").Append(N(barrelLength))
                .Append(" outletInvert ").Append(N(outletInvert))
                .Append(" dams ").Append(Culvert.InvertDamsStream(-1.2, -1.35));
            plan.Append("\nditch capacity ")
                .Append(N(Culvert.VDitchCapacityM3PerSecond(0.35, 0.012, ditch)))
                .Append(" drains ").Append(Culvert.DitchDrainsToOutfall(-0.6, -1.2, true));

            StringBuilder request = new StringBuilder();
            for (int step = 0; step < 2; step++)
            {
                TerrainRegion box = CorridorBox(step * 3, 14f);
                TerrainWriteReceipt receipt = gateway.WriteDetailLayer(null, 0, new int[4, 4], box,
                    Requester + ".details");
                if (step > 0) { request.Append('\n'); }
                request.Append("details ").Append(box.Describe()).Append(" accepted ").Append(receipt.Accepted);
            }
            Add(run, "road.details", plan.ToString(), request.ToString(), "granted", string.Empty);
        }

        /// <summary> The street trees on the collector's verge. The placement comes from RM-47's bake, seeded from the
        /// city seed and the ELEMENT id, so the row is a pure function of the fixture and cannot shift because the
        /// terrain arrived from somewhere else. Every along-distance is written into the plan, not just the count: a
        /// mode that moved one tree by a centimetre has to show up. </summary>
        private static void RecordRoadTrees(TerrainWriteGateway gateway, DualModeRun run)
        {
            IList<double> alongs = ScatterBake.ScatterAlongLine(CitySeed, "fixture.street.trees",
                BlockLengthMetres, TreeSpacingMetres, TreeJitterFraction);

            StringBuilder plan = new StringBuilder();
            plan.Append("street trees spacing ").Append(N(TreeSpacingMetres))
                .Append(" jitter ").Append(N(TreeJitterFraction))
                .Append(" count ").Append(alongs.Count);
            TreeInstance[] trees = new TreeInstance[alongs.Count];
            for (int i = 0; i < alongs.Count; i++)
            {
                plan.Append('\n').Append("tree ").Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(" along ").Append(N(alongs[i]));
                TreeInstance instance = new TreeInstance();
                instance.prototypeIndex = 0;
                instance.widthScale = 1f;
                instance.heightScale = 1f;
                instance.position = new Vector3((float)(alongs[i] / BlockLengthMetres), 0f, 0.25f);
                trees[i] = instance;
            }

            TerrainRegion verge = TerrainRegion.FromExtents(0.0, -1.0, 8.0, BlockLengthMetres, 12.0, 11.0);
            TerrainWriteReceipt receipt = gateway.WriteTrees(null, trees, verge, Requester + ".trees");
            string request = "trees " + verge.Describe() + " count " +
                             trees.Length.ToString(CultureInfo.InvariantCulture) +
                             " accepted " + receipt.Accepted;
            Add(run, "road.trees", plan.ToString(), request, "granted", string.Empty);
        }

        /// <summary> The painted surface. The pattern is phase-from-section-start arithmetic (RM-40), so a dash lands
        /// at the same station in either mode; the fixture reads the phase at five fixed stations rather than trusting
        /// the period alone, which is what catches a pattern that drifted. </summary>
        private static void RecordRoadSplat(TerrainWriteGateway gateway, DualModeRun run)
        {
            MarkingPatternClass pattern = MarkingPatternClass.Default();
            double period = PavementMarkings.PatternPeriodMetres(pattern.laneLineMarkMetres,
                pattern.laneLineGapMetres);

            StringBuilder plan = new StringBuilder();
            plan.Append("marked ").Append(PavementMarkings.RoadHasMarkings(true))
                .Append(" period ").Append(N(period))
                .Append(" markFraction ").Append(N(PavementMarkings.MarkFraction(pattern.laneLineMarkMetres,
                                                                                pattern.laneLineGapMetres)))
                .Append(" warningFraction ").Append(N(PavementMarkings.MarkFraction(pattern.warningLineMarkMetres,
                                                                                    pattern.warningLineGapMetres)));
            for (int i = 0; i < 5; i++)
            {
                double station = i * 17.5;   // deliberately not a multiple of the period
                plan.Append('\n').Append("station ").Append(N(station))
                    .Append(" painted ").Append(PavementMarkings.IsPaintedAt(station, pattern.laneLineMarkMetres,
                                                                            pattern.laneLineGapMetres))
                    .Append(" inSection ").Append(PavementMarkings.IsWithinMarkedSection(station, 6.0,
                                                                                        DividedRunMetres - 6.0));
            }

            TerrainRegion surface = TerrainRegion.FromExtents(0.0, -1.0, -7.0, DividedRunMetres, 2.0, 7.0);
            TerrainWriteReceipt receipt = gateway.WriteSplat(null, 0, 0, new float[4, 4, 2], surface,
                Requester + ".splat");
            string request = "splat " + surface.Describe() + " accepted " + receipt.Accepted;
            Add(run, "road.splat", plan.ToString(), request, "granted", string.Empty);
        }
        #endregion


        #region "tunnel.cavity and building.pad -- connected only, refused in words otherwise"
        /// <summary> The tunnel. Its SHELL is arithmetic -- lining radius, cover along the reach -- and belongs in the
        /// plan, identical either way, because a tunnel's geometry does not depend on who owns the ground. What differs
        /// is the OPENING: connected mode holds the surface cut through regeneration, standalone cannot and must say so
        /// in words. The hold is released again in the same pass, so the fixture never leaves the gateway holding
        /// something (RM-57's every-hold-released rule). </summary>
        private static void RecordTunnelCavity(TerrainWriteGateway gateway, DualModeRun run)
        {
            TunnelClass tunnel = TunnelClass.Default();
            double lining = TunnelCavity.LiningOuterRadiusMetres(TunnelBoreRadiusMetres, tunnel);
            List<double> bed = new List<double>();
            List<double> crown = new List<double>();
            for (int i = 0; i < 3; i++)
            {
                bed.Add(0.0);
                crown.Add(-TunnelCoverMetres - i * 0.5);
            }
            TunnelCavity.CoverScan scan = TunnelCavity.ScanWaterCover(bed, crown, tunnel);

            StringBuilder plan = new StringBuilder();
            plan.Append("bore ").Append(N(TunnelBoreRadiusMetres))
                .Append(" lining ").Append(N(lining))
                .Append(" encloses ").Append(TunnelCavity.LiningEnclosesCavity(lining, TunnelBoreRadiusMetres))
                .Append("\ncover sealedLegal ").Append(scan.sealedLegal)
                .Append(" worst ").Append(N(scan.worstCoverMetres))
                .Append(" failingStation ").Append(scan.failingStationIndex.ToString(CultureInfo.InvariantCulture));

            TerrainRegion portal = TerrainRegion.FromExtents(118.0, -14.0, -68.0, 126.0, 4.0, -60.0);
            string refusal;
            TerrainRequestHandle handle = gateway.RequestTunnelOpening(portal, Requester + ".tunnel", out refusal);
            // What was ASKED, and nothing about how it went. Whether the opening is granted differs by mode by design
            // -- that is the whole ConnectedOnly claim for this row -- and the comparison reads any request difference
            // as the fixture asking for different ground, which would be a determinism defect rather than a mode
            // difference. The grant is therefore part of the outcome below, where the table licenses a difference.
            string request = "tunnel opening " + portal.Describe();

            TunnelMode mode = gateway.Mode == TerrainProviderMode.Connected
                ? TunnelMode.Connected : TunnelMode.Standalone;
            StringBuilder outcome = new StringBuilder();
            outcome.Append("build ").Append(TunnelCavity.ResolveBuild(mode, false, scan).ToString());
            if (handle.IsValid)
            {
                outcome.Append(" opening held, released ").Append(gateway.ReleaseTunnelOpening(handle));
            }
            else
            {
                outcome.Append(" opening not held");
            }
            Add(run, "tunnel.cavity", plan.ToString(), request, outcome.ToString(), refusal);
        }

        /// <summary> A building pad on the fixture's one block. Same shape as the tunnel opening: the pad's FOOTPRINT
        /// is the same rectangle either way, and only the hold against regeneration differs. </summary>
        private static void RecordBuildingPad(TerrainWriteGateway gateway, DualModeRun run)
        {
            TerrainRegion pad = TerrainRegion.FromFootprint(40.0, 45.0, 9.0, 6.0, -1.0, 6.0);
            string plan = "pad footprint " + pad.Describe() + " area " + N(pad.FootprintArea);

            string refusal;
            TerrainRequestHandle handle = gateway.RequestBuildingPad(pad, Requester + ".pad", out refusal);
            // The footprint asked for, which is the same rectangle either way; the grant is in the outcome, for the
            // reason given on the tunnel opening above.
            string request = "pad " + pad.Describe();
            string outcome = handle.IsValid
                ? "pad held, released " + gateway.ReleaseBuildingPad(handle)
                : "pad not held";
            Add(run, "building.pad", plan, request, outcome, refusal);
        }

        /// <summary> The bridge that carries the collector over the fixture's watercourse.
        ///
        /// This is the one row of the availability table the fixture used not to exercise, and it is the row where the
        /// mode difference is easiest to state wrongly. The DECK does not differ: a span's length, its type's limits,
        /// where its piers stand and how far the raised road has to climb are all plan geometry, computed from
        /// <see cref="BridgeTypes"/> and <see cref="GradeSeparation"/> and identical whoever owns the ground. What
        /// differs is the two things that touch terrain -- the footings, which connected mode can ask a provider to
        /// hold flat through regeneration, and the question "does this pier land in water", which can only be answered
        /// for ground that is actually present.
        ///
        /// The water answer is therefore derived from <see cref="ITerrainSurfaceReader.GetActiveChunkBounds"/> and not
        /// from a sentence typed here: a support whose plan position falls inside reachable ground has an answer, one
        /// outside it does not. A standalone sink handed no terrains reaches nothing and says so, which is exactly the
        /// BetterConnected claim the table makes for this row -- measured rather than asserted.
        ///
        /// Every footing hold is released in the same pass, so the fixture never leaves the gateway holding ground
        /// (RM-57's every-hold-released rule). </summary>
        private static void RecordBridgeDeck(TerrainWriteGateway gateway, ITerrainSurfaceReader reader, DualModeRun run)
        {
            BridgeTypeSpec spec = BridgeTypeSpec.Beam();
            BridgeTypes.SpecValidation validation = BridgeTypes.ValidateSpec(spec);
            bool spanFits = BridgeTypes.SpanWithinType(BridgeSpanLengthMetres, spec);
            List<double> supports = BridgeTypes.SupportStationsMetres(BridgeSpanLengthMetres, spec);
            int piers = BridgeTypes.IntermediatePierCount(BridgeSpanLengthMetres, spec);

            GradeSeparationClass separation = GradeSeparationClass.Default();
            double startMetres, endMetres;
            GradeSeparation.BridgeSpanMetres(CulvertStationMetres, BridgeSpanLengthMetres,
                DividedRunMetres, out startMetres, out endMetres);
            double approach = GradeSeparation.RequiredApproachLengthMetres(
                separation.verticalClearanceMetres, BridgeApproachGradientFraction);

            StringBuilder plan = new StringBuilder();
            plan.Append("deck ").Append(spec.deckType.ToString())
                .Append(" placement ").Append(spec.placementRule.ToString())
                .Append(" span ").Append(N(BridgeSpanLengthMetres))
                .Append(" accepted ").Append(validation.accepted)
                .Append(" withinType ").Append(spanFits)
                .Append("\nstations ").Append(startMetres.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" to ").Append(endMetres.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" supports ").Append(supports != null
                    ? supports.Count.ToString(CultureInfo.InvariantCulture) : "0")
                .Append(" intermediatePiers ").Append(piers.ToString(CultureInfo.InvariantCulture))
                .Append("\nclearance ").Append(N(separation.verticalClearanceMetres))
                .Append(" approachPerSide ").Append(N(approach));
            for (int i = 0; supports != null && i < supports.Count; i++)
            {
                plan.Append("\n  support ").Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(" at ").Append(supports[i].ToString("0.###", CultureInfo.InvariantCulture)).Append(" m");
            }

            // One footing per support, each a small square of held ground centred on the support's plan position. The
            // span runs east along the divided run, so the station converts to a world X directly.
            StringBuilder request = new StringBuilder();
            StringBuilder outcome = new StringBuilder();
            List<TerrainRequestHandle> held = new List<TerrainRequestHandle>();
            string firstRefusal = string.Empty;
            int answered = 0;
            int unanswered = 0;

            for (int i = 0; supports != null && i < supports.Count; i++)
            {
                double footingX = BridgeSpanOriginXMetres + supports[i];
                TerrainRegion footing = TerrainRegion.FromFootprint(footingX, BridgeSpanZMetres,
                    BridgeFootingEdgeMetres, BridgeFootingEdgeMetres, -BridgeFootingDepthMetres, 1.0);

                string refusal;
                TerrainRequestHandle handle = gateway.RequestBuildingPad(footing,
                    Requester + ".bridge.footing." + i.ToString(CultureInfo.InvariantCulture), out refusal);
                if (handle.IsValid) { held.Add(handle); }
                if (string.IsNullOrEmpty(firstRefusal) && !string.IsNullOrEmpty(refusal)) { firstRefusal = refusal; }

                // Where each footing was asked for. Not whether it was granted: connected mode holds these and
                // standalone cannot, which is the BetterConnected claim for this row, and it is counted in the outcome
                // below as "footings held N released M" rather than folded into the ask.
                request.Append(i > 0 ? "\n" : string.Empty)
                       .Append("footing ").Append(i.ToString(CultureInfo.InvariantCulture))
                       .Append(" ").Append(footing.Describe());

                bool reachable = GroundReachable(reader, footingX, BridgeSpanZMetres);
                if (reachable) { answered++; } else { unanswered++; }
                outcome.Append(i > 0 ? "\n" : string.Empty)
                       .Append("support ").Append(i.ToString(CultureInfo.InvariantCulture))
                       .Append(reachable
                           ? " stands on reachable ground, water question answerable, landsInWater false"
                           : " stands on ground no tile covers, water question unanswerable here");
            }

            int released = 0;
            for (int i = 0; i < held.Count; i++)
            {
                if (gateway.ReleaseBuildingPad(held[i])) { released++; }
            }

            outcome.Append("\nfootings held ").Append(held.Count.ToString(CultureInfo.InvariantCulture))
                   .Append(" released ").Append(released.ToString(CultureInfo.InvariantCulture))
                   .Append("  water answerable ").Append(answered.ToString(CultureInfo.InvariantCulture))
                   .Append(" unanswerable ").Append(unanswered.ToString(CultureInfo.InvariantCulture))
                   .Append("\nsupportsMayStandInWater ").Append(spec.supportsMayStandInWater)
                   .Append(" allowedInWater ").Append(BridgeTypes.SupportAllowedAt(true, spec))
                   .Append(" allowedOnLand ").Append(BridgeTypes.SupportAllowedAt(false, spec));

            Add(run, "bridge.deck", plan.ToString(), request.ToString(), outcome.ToString(), firstRefusal);
        }

        /// <summary> Whether a plan position falls inside ground this mode can actually reach. An empty active list
        /// means nothing is reachable, which is the honest reading for a standalone sink that was handed no terrains;
        /// the Y extent is ignored because the question is about coverage, not height. </summary>
        private static bool GroundReachable(ITerrainSurfaceReader reader, double worldX, double worldZ)
        {
            if (reader == null) { return false; }
            List<Bounds> active = reader.GetActiveChunkBounds();
            if (active == null) { return false; }
            for (int i = 0; i < active.Count; i++)
            {
                Bounds b = active[i];
                if (worldX < b.min.x || worldX > b.max.x) { continue; }
                if (worldZ < b.min.z || worldZ > b.max.z) { continue; }
                return true;
            }
            return false;
        }
        #endregion


        #region "terrain.crosschunk -- connected only; terrain.sampling -- better connected"
        /// <summary> Asking about ground the tool does not own a tile of. The QUESTION is the same in both modes -- two
        /// fixed boxes, one over the block and one 400 m east of it, past where any single standalone tile would end --
        /// and that is what goes in the plan. The ANSWER is the mode difference: a provider reports the chunks it has
        /// active, a standalone sink that was handed no terrains reports none, and the words for that come from the
        /// availability table, not from a sentence typed into this test. </summary>
        private static void RecordCrossChunk(ITerrainSurfaceReader reader, DualModeRun run)
        {
            TerrainRegion near = TerrainRegion.FromFootprint(60.0, 45.0, 60.0, 45.0, -5.0, 20.0);
            TerrainRegion far = TerrainRegion.FromFootprint(460.0, 45.0, 60.0, 45.0, -5.0, 20.0);
            string plan = "query near " + near.Describe() + "\nquery far " + far.Describe();

            List<Bounds> chunks = reader != null ? reader.GetActiveChunkBounds() : null;
            int count = chunks != null ? chunks.Count : 0;
            int nearHits = 0;
            int farHits = 0;
            for (int i = 0; i < count; i++)
            {
                TerrainRegion chunk = TerrainRegion.FromBounds(chunks[i]);
                if (chunk.Intersects(near)) { nearHits++; }
                if (chunk.Intersects(far)) { farHits++; }
            }

            string request = "cross-chunk query, no terrain write requested";
            string outcome = "chunks " + count.ToString(CultureInfo.InvariantCulture) +
                             " nearHits " + nearHits.ToString(CultureInfo.InvariantCulture) +
                             " farHits " + farHits.ToString(CultureInfo.InvariantCulture);
            string refusal = count > 0
                ? string.Empty
                : TerrainFeatureAvailability.SentenceFor("terrain.crosschunk", TerrainProviderMode.Standalone);
            Add(run, "terrain.crosschunk", plan, request, outcome, refusal);
        }

        /// <summary> Sampling the ground at five fixed stations. Both modes answer -- this row is BETTER connected, not
        /// connected only -- so the numbers legitimately differ: a provider knows its own heightfield, a standalone sink
        /// with no tiles under it can only answer zero and straight up. The stations are the plan; the heights and
        /// normals are the outcome. </summary>
        private static void RecordSampling(ITerrainSurfaceReader reader, DualModeRun run)
        {
            StringBuilder plan = new StringBuilder();
            StringBuilder outcome = new StringBuilder();
            for (int i = 0; i < 5; i++)
            {
                float x = i * 30f;
                float z = 45f;
                if (i > 0) { plan.Append('\n'); outcome.Append('\n'); }
                plan.Append("station ").Append(N(x)).Append(',').Append(N(z));
                float height = reader != null ? reader.SampleHeight(x, z) : 0f;
                Vector3 normal = reader != null ? reader.SampleNormal(x, z) : Vector3.up;
                outcome.Append("height ").Append(N(height)).Append(" normal ").Append(Point(normal));
            }
            Add(run, "terrain.sampling", plan.ToString(), "surface read, no terrain write requested",
                outcome.ToString(), string.Empty);
        }
        #endregion


        #region "city.blocks -- identical either way, ground or no ground"
        /// <summary> The block, its lots and the planting on it. Subdivision is planar graph work on the authored
        /// network and RM-47's scatter is seeded per element, so NONE of this may move when the terrain changes hands.
        /// Every polygon vertex and every scatter point goes into the plan rather than a count, because a count is
        /// exactly what a subtly reordered subdivision would still match. </summary>
        private static void RecordCityBlocks(BlockSubdivisionOperator blocks, StreetNetwork network,
            DualModeRun run)
        {
            BlockSubdivisionResult result = blocks.Execute(new BlockSubdivisionInput(network), OperatorSeed);

            StringBuilder plan = new StringBuilder();
            plan.Append("faces ").Append(result.report.faces.ToString(CultureInfo.InvariantCulture))
                .Append(" blocks ").Append(result.report.blocks.ToString(CultureInfo.InvariantCulture))
                .Append(" collapsed ").Append(result.report.collapsed.ToString(CultureInfo.InvariantCulture))
                .Append(" lots ").Append(result.report.lots.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < result.blocks.Count; i++)
            {
                Block block = result.blocks[i];
                plan.Append("\nblock ").Append(block.id.ToString(CultureInfo.InvariantCulture))
                    .Append(" boundary ").Append(block.boundaryNodes.Count.ToString(CultureInfo.InvariantCulture));
                for (int v = 0; v < block.polygon.Count; v++)
                {
                    plan.Append(' ').Append(Point(block.polygon[v]));
                }
            }
            for (int i = 0; i < result.lots.Count; i++)
            {
                Lot lot = result.lots[i];
                plan.Append("\nlot ").Append(lot.id.ToString(CultureInfo.InvariantCulture))
                    .Append(" block ").Append(lot.blockId.ToString(CultureInfo.InvariantCulture))
                    .Append(" zone ").Append(lot.zoneId.ToString(CultureInfo.InvariantCulture));
                for (int v = 0; v < lot.polygon.Count; v++)
                {
                    plan.Append(' ').Append(Point(lot.polygon[v]));
                }
            }

            // Planting over the block's interior, with the carriageway corridor cut out of it by GEOMETRY (RM-47),
            // not by a second painted mask -- so the keep-out is the same rectangle the cut used.
            IList<ScatterPoint> points = ScatterBake.ScatterOverArea(CitySeed, "fixture.block.planting",
                BlockLengthMetres, BlockDepthMetres, 1.5, 4.0);
            List<ScatterRect> keepOut = new List<ScatterRect>();
            keepOut.Add(new ScatterRect(0.0, 0.0, BlockLengthMetres, 11.0));
            IList<ScatterPoint> kept = ScatterBake.FilterExcluded(points, keepOut);
            IList<ScatterPoint> thinned = ScatterBake.Thin(kept, 0.5);

            plan.Append("\nplanting drawn ").Append(points.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" kept ").Append(kept.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" thinned ").Append(thinned.Count.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < kept.Count; i++)
            {
                plan.Append("\nplant ").Append(i.ToString(CultureInfo.InvariantCulture))
                    .Append(' ').Append(N(kept[i].x)).Append(',').Append(N(kept[i].z));
            }

            Add(run, "city.blocks", plan.ToString(), "subdivision only, no terrain write requested",
                "granted", string.Empty);
        }
        #endregion
    }
}
#endif
