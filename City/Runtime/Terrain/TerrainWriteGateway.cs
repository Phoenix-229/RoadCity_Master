#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The ONE place in the tool through which every terrain modification passes (RM-57).
    ///
    /// Why a gateway and not a mode check at each write: the tool changes terrain from several places -- the
    /// terraforming file, the history restore, the splat utility -- and a mode check at each site is a guarantee that
    /// one will eventually be missed. One gateway with ONE mode decision is auditable, because the audit is then a
    /// search: either a direct <c>TerrainData</c> write exists outside <see cref="DirectTerrainSink"/> or it does not.
    ///
    /// The gateway also owns the two ordering rules the provider needs and no single call site could enforce:
    /// <list type="bullet">
    /// <item> the affected region is ACCUMULATED across a build and notified ONCE with the union, on delete as well
    /// as on build, so a hundred-node road makes one round trip and no changed ground goes unannounced; </item>
    /// <item> every hold that can be requested has a matching release ON THIS SAME CLASS, and anything still
    /// outstanding is listed by name, so a missing release is visible at review instead of after a scene has
    /// accumulated orphaned modifications. </item>
    /// </list>
    /// Mode is decided once, at construction, and is immutable for the life of the gateway. </summary>
    public sealed class TerrainWriteGateway
    {
        #region "The one mode decision"
        private readonly TerrainProviderMode mode;
        private readonly ITerrainWriteSink sink;
        private readonly string providerName;

        /// <summary> Build a gateway around a sink. The mode comes from whether a provider was reachable, and is
        /// fixed here -- there is deliberately no setter, because a mode that can change mid-build is a mode that can
        /// change between two writes of the same road. </summary>
        public TerrainWriteGateway(TerrainProviderMode mode, ITerrainWriteSink sink, string providerName)
        {
            this.mode = mode;
            this.sink = sink;
            this.providerName = providerName != null ? providerName : string.Empty;
        }

        public TerrainProviderMode Mode { get { return mode; } }

        /// <summary> The provider's name in connected mode, empty in standalone. </summary>
        public string ProviderName { get { return providerName; } }

        /// <summary> The sink, exposed only so a caller can name it in a report. </summary>
        public ITerrainWriteSink Sink { get { return sink; } }

        /// <summary> The surface as the CURRENT mode sees it: the provider when connected, the engine's terrain when
        /// standalone. Null only when nothing is installed. Reads go through the same one decision as writes, so a
        /// spline cannot be snapped to one surface and cut into another. </summary>
        public ITerrainSurfaceReader SurfaceReader { get { return sink as ITerrainSurfaceReader; } }

        /// <summary> Which path this gateway dispatches to, stated rather than inferred. </summary>
        public TerrainWritePath PathForWrites
        {
            get { return sink != null ? sink.Path : TerrainWritePath.Refused; }
        }

        /// <summary> The sentence the tool says about itself, so an author never has to deduce the mode from
        /// behaviour (§ 10.24 requires the difference to be a STATED contract). </summary>
        public string PathLine()
        {
            if (sink == null)
            {
                return "No terrain sink installed: terrain modification is refused until the tool is initialised.";
            }
            if (mode == TerrainProviderMode.Connected)
            {
                return string.Format(
                    "Connected to '{0}': terrain is requested through the provider, which owns it; the tool writes no heightfield.",
                    string.IsNullOrEmpty(providerName) ? sink.SinkName : providerName);
            }
            return "Standalone (no provider connected): the tool terraforms the terrain directly, as it always has.";
        }
        #endregion


        #region "The installed gateway"
        private static TerrainWriteGateway active;

        /// <summary> The gateway the tool is currently using. Never null, and its DEFAULT is standalone with the direct
        /// sink -- the road tool is a standalone product first, so a session where nobody ever connected a provider
        /// terraforms exactly as it always did. A refusing default would have silently disabled terraforming for every
        /// existing user, which is a worse failure than a missing connection.
        ///
        /// A connected gateway whose provider has since been destroyed is dropped here rather than kept. This static
        /// outlives any scene, so without that check exiting play mode or unloading a scene would leave the tool in
        /// connected mode talking to nothing: every write suppressed, no error, terrain editing quietly dead. Falling
        /// back to the direct path is the safe answer -- the tool keeps working, and reconnecting reinstalls.
        /// </summary>
        public static TerrainWriteGateway Active
        {
            get
            {
                if (active != null && !active.IsUsable) { active = null; }
                if (active == null)
                {
                    active = ForStandalone();
                }
                return active;
            }
        }

        /// <summary> Whether this gateway can still carry a write. False only for a connected gateway whose provider
        /// has been destroyed. </summary>
        public bool IsUsable
        {
            get
            {
                if (sink == null) { return false; }
                ProviderTerrainSink connected = sink as ProviderTerrainSink;
                return connected == null || connected.ProviderAlive;
            }
        }

        /// <summary> Standalone: the tool writes the terrain itself. </summary>
        public static TerrainWriteGateway ForStandalone()
        {
            return new TerrainWriteGateway(TerrainProviderMode.Standalone, new DirectTerrainSink(), string.Empty);
        }

        /// <summary> Connected: the provider owns the terrain and every modification is requested through it. A null
        /// provider is not a connection, and falls back to standalone rather than to a gateway that refuses. </summary>
        public static TerrainWriteGateway ForProvider(UniverseCreator.Integration.IPCGDataProvider provider,
            string providerName)
        {
            if (provider == null) { return ForStandalone(); }
            return new TerrainWriteGateway(TerrainProviderMode.Connected,
                new ProviderTerrainSink(provider, providerName), providerName);
        }

        /// <summary> Decide the mode ONCE from whether a provider is reachable, and install the result. This is the
        /// single decision point RM-57 requires; every write anywhere in the tool inherits it. </summary>
        public static TerrainWriteGateway InstallFor(UniverseCreator.Integration.IPCGDataProvider provider,
            string providerName)
        {
            TerrainWriteGateway gateway = provider != null ? ForProvider(provider, providerName) : ForStandalone();
            Install(gateway);
            return gateway;
        }

        /// <summary> Install the gateway for this session -- called once, where the linker is entered (§ 10.35).
        /// </summary>
        public static void Install(TerrainWriteGateway gateway)
        {
            active = gateway;
        }

        /// <summary> Drop the installed gateway. The next access rebuilds the standalone default, so uninstalling a
        /// provider connection returns the tool to writing terrain itself rather than to writing nothing. </summary>
        public static void Uninstall()
        {
            active = null;
        }
        #endregion


        #region "Build accumulation"
        private readonly TerrainRegionSet pending = new TerrainRegionSet();
        private bool buildOpen;
        private string buildLabel = string.Empty;
        private int notifyCount;
        private int buildCount;
        private readonly TerrainLedger ledger = new TerrainLedger();

        /// <summary> The ledger of what was asked for, in order, with the dispatch path deliberately excluded -- two
        /// builds of the same road in the two modes must produce the same ledger. </summary>
        public TerrainLedger Ledger { get { return ledger; } }

        /// <summary> How many times the provider has been told a region changed. Bounded per build by
        /// <see cref="TerrainRegionSet.DefaultLimit"/>, never one per segment. </summary>
        public int NotifyCount { get { return notifyCount; } }

        /// <summary> How many builds have been closed. With <see cref="NotifyCount"/> this is the ratio that says
        /// whether batching is working: it must stay far below one announcement per write. </summary>
        public int BuildCount { get { return buildCount; } }

        /// <summary> One box around everything accumulated so far in the open build. This is the extent, not what
        /// gets announced -- see <see cref="PendingRegions"/>. </summary>
        public TerrainRegion PendingRegion { get { return pending.Union; } }

        /// <summary> The tight boxes that will actually be announced when the build closes. </summary>
        public TerrainRegionSet PendingRegions { get { return pending; } }

        public bool BuildOpen { get { return buildOpen; } }

        /// <summary> Open a build. Every write between here and <see cref="EndBuild"/> contributes to one batch of
        /// announcements.
        ///
        /// Opening a build while one with the SAME label is already open keeps the accumulation going, so a routine
        /// re-entrant call is still one change. A DIFFERENT label means the previous build never closed -- something
        /// threw between its begin and its end -- and that build is closed here rather than silently absorbed. Left
        /// alone, a leaked build would keep accumulating for the rest of the session and eventually announce the union
        /// of every road ever built, which is precisely the map-sized announcement this design exists to avoid.
        /// </summary>
        public void BeginBuild(string label)
        {
            string wanted = label != null ? label : string.Empty;
            if (buildOpen && wanted != buildLabel)
            {
                EndBuild();
            }
            if (!buildOpen)
            {
                pending.Clear();
                touched.Clear();
                touchedHoles.Clear();
                buildLabel = wanted;
                buildOpen = true;
            }
        }

        /// <summary> Close the build and announce the ground that changed as a few tight boxes rather than one big
        /// one -- see <see cref="TerrainRegionSet"/> for why that distinction decides whether a road costs the
        /// provider a corridor or the whole map. A build that changed nothing announces nothing, because an empty
        /// region would send the provider to heal the wrong place. Returns the total extent.
        ///
        /// Two things happen in a fixed order, and the order is the point. The heights are committed and the terrain
        /// settled FIRST, so that when the host is told about a box the ground in it is already the ground that will be
        /// there; announcing first would have the host heal a surface that has not landed yet.
        ///
        /// Then the boxes are announced -- but not all of them. A box whose ground ENDS UP where it started is not
        /// announced at all, and that single omission is the fix for "choosing a tunnel cuts the hill". A bore's opening
        /// and the span above it are both held and accounted here; announced as changed ground they become entries in
        /// the host's cut registry, which the host then pre-loads, heals and re-flattens after every regeneration --
        /// levelling the hill the tunnel was driven under, while doing exactly what it was told. The host is still told
        /// about the tunnel OPENING through its own tunnel-opening request, which is a different sentence with a
        /// different meaning: open the surface here and keep it open. See
        /// <see cref="TunnelTerrainPolicy.GroundEndsUnchanged"/> and <see cref="TunnelLinkerBridge"/>. </summary>
        public TerrainRegion EndBuild()
        {
            if (buildOpen)
            {
                CloseTransactions(true);
                SettleTouched();
            }

            TerrainRegion extent = pending.Union;
            if (buildOpen && extent.hasArea && sink != null)
            {
                pending.Coalesce(TerrainRegionSet.DefaultLimit);
                for (int i = 0; i < pending.Count; i++)
                {
                    if (TunnelTerrainPolicy.GroundEndsUnchanged(pending[i].treatment))
                    {
                        unannouncedCount++;
                        continue;
                    }
                    sink.NotifyRegionChanged(pending[i]);
                    notifyCount++;
                }
            }
            if (buildOpen) { buildCount++; }
            buildOpen = false;
            buildLabel = string.Empty;
            pending.Clear();
            return extent;
        }

        private int unannouncedCount;

        /// <summary> How many boxes were deliberately NOT announced because their ground ends where it started -- the
        /// protected ground over a tunnel. A gate reads this to prove the hill was left alone; a non-zero count on a
        /// road with no tunnel in it would be a bug worth seeing. </summary>
        public int UnannouncedCount { get { return unannouncedCount; } }

        /// <summary> The label of the open build, for the report. </summary>
        public string BuildLabel { get { return buildLabel; } }

        /// <summary> Fold one changed region into the build's batch, and record it in the ledger. Writes outside an
        /// open build still accumulate, and are announced when a build is next closed, so a stray write is never
        /// silently unannounced. </summary>
        private void Account(TerrainWriteKind kind, TerrainRegion region, string requesterId)
        {
            pending.Add(region);
            ledger.Add(kind, region, requesterId);
        }
        #endregion


        #region "RM-99: what each road was built against"
        private readonly Dictionary<string, GroundConformRecord> conformRecords
            = new Dictionary<string, GroundConformRecord>();

        private readonly List<HostRegenerationStep> regenerationSequence = new List<HostRegenerationStep>();

        /// <summary> Remember the ground a finished road is standing on, read through whichever surface this gateway is
        /// installed against.
        ///
        /// This is called at the END of a road's build, once, and it is what makes the staleness verdict possible at all:
        /// without a record of the ground at build time there is nothing to compare new ground with, and the only
        /// available answers become "rebuild everything" and "hope". Recording through <see cref="SurfaceReader"/> rather
        /// than through a terrain lookup is what makes the record identical in both modes -- in connected mode it is the
        /// host's own surface that gets remembered, which is the surface that is going to change. </summary>
        public GroundConformRecord RecordConform(SplineC spline, string roadName)
        {
            GroundConformRecord record = GroundConformRecord.Record(spline, roadName, SurfaceReader,
                TerrainBuildSettings.Drift);
            conformRecords[record.roadName] = record;
            return record;
        }

        /// <summary> How many roads this gateway can currently speak for. A staleness report over zero records is not an
        /// all-clear, and <see cref="StaleRoadReport"/> prints the count for that reason. </summary>
        public int ConformRecordCount { get { return conformRecords.Count; } }

        /// <summary> Forget a road, for one that has been deleted. Returns whether there was anything to forget: a
        /// delete that silently matched nothing means the name changed somewhere, and the road would otherwise be
        /// reported as needing a refresh forever. </summary>
        public bool ForgetConform(string roadName)
        {
            return roadName != null && conformRecords.Remove(roadName);
        }

        /// <summary> Ask the ground where it is now and say which roads have to be rebuilt.
        ///
        /// Reads only -- see <see cref="GroundConformRecord"/> for why a verdict that rebuilds cannot be trusted to have
        /// judged. Every remembered road appears in the report whatever its verdict, so an all-clear says how many roads
        /// it is an all-clear over. </summary>
        public StaleRoadReport EvaluateStaleness()
        {
            StaleRoadReport report = new StaleRoadReport();
            ITerrainSurfaceReader ground = SurfaceReader;
            GroundDriftClass cls = TerrainBuildSettings.Drift;
            foreach (KeyValuePair<string, GroundConformRecord> pair in conformRecords)
            {
                report.Add(pair.Key, pair.Value.Evaluate(ground, cls));
            }
            return report;
        }

        /// <summary> Write down that a step of a host regeneration happened, so the ORDER can be checked against
        /// <see cref="HostRegenerationContract"/> afterwards rather than asserted in a comment. The journal is kept on
        /// the gateway because the gateway is the one object both sides of the handshake already hold. </summary>
        public void NoteRegenerationStep(HostRegenerationStep step)
        {
            regenerationSequence.Add(step);
        }

        public IList<HostRegenerationStep> RegenerationSequence { get { return regenerationSequence; } }

        /// <summary> What the recorded sequence got wrong, or <see cref="HostRegenerationViolation.None"/>. </summary>
        public HostRegenerationViolation CheckRegenerationOrder()
        {
            return HostRegenerationContract.Check(regenerationSequence);
        }

        /// <summary> Start a fresh journal for the next regeneration. Separate from <see cref="NoteRegenerationStep"/>
        /// so that forgetting to clear shows up as a long sequence rather than as a passing check over a sequence that
        /// was quietly reset by the first step. </summary>
        public void ClearRegenerationJournal()
        {
            regenerationSequence.Clear();
        }
        #endregion


        #region "RM-98: settling, and all-or-nothing builds"
        private readonly List<Terrain> touched = new List<Terrain>();
        private readonly List<bool> touchedHoles = new List<bool>();
        private readonly List<TerrainWriteTransaction> transactions = new List<TerrainWriteTransaction>();
        private readonly List<Terrain> transactionTerrains = new List<Terrain>();
        private WatertightWriteClass watertight = TerrainBuildSettings.Watertight;
        private int settleCount;
        private int abortCount;
        private string lastAbortReason = string.Empty;

        /// <summary> The rules the watertight path reads. Settable so an author's own tolerances travel with the road
        /// tool's settings rather than being frozen here. </summary>
        public WatertightWriteClass Watertight
        {
            get { return watertight; }
            set { watertight = value != null ? value : WatertightWriteClass.Default(); }
        }

        /// <summary> How many tiles this gateway has settled, across every build. </summary>
        public int SettleCount { get { return settleCount; } }

        /// <summary> How many builds ended in a roll-back rather than a commit. </summary>
        public int AbortCount { get { return abortCount; } }

        /// <summary> Why the last abort happened, in the words the caller used. </summary>
        public string LastAbortReason { get { return lastAbortReason; } }

        /// <summary> How many tiles the open build has touched. </summary>
        public int TouchedCount { get { return touched.Count; } }

        /// <summary> Remember that a tile was written this build, so it can be settled exactly once when the build
        /// closes. Recorded in BOTH modes even though only the direct sink acts on it, because RM-99 compares the two
        /// modes' accounts of which ground changed and an unrecorded tile would read as a difference between the modes
        /// rather than as the bookkeeping gap it is. </summary>
        private void Touch(Terrain terrain)
        {
            if (terrain == null) { return; }
            for (int i = 0; i < touched.Count; i++)
            {
                if (touched[i] == terrain) { return; }
            }
            touched.Add(terrain);
            touchedHoles.Add(false);
        }

        /// <summary> Note that the hole map of a tile changed, so its settle also flushes the holes texture. Separate
        /// from <see cref="Touch"/> because flushing that texture on every height write would cost a full texture
        /// upload per road for nothing. </summary>
        private void MarkHoles(Terrain terrain)
        {
            if (terrain == null) { return; }
            for (int i = 0; i < touched.Count; i++)
            {
                if (touched[i] == terrain) { touchedHoles[i] = true; return; }
            }
        }

        /// <summary> Open an all-or-nothing transaction over one tile. The gateway is the factory rather than the
        /// caller because it is the gateway that knows the sink, and because it then knows which tiles a transaction
        /// already covers and must not settle a second time. </summary>
        public TerrainWriteTransaction OpenTransaction(Terrain terrain, string requesterId)
        {
            if (terrain == null || sink == null) { return null; }
            TerrainWriteTransaction t = TerrainWriteTransaction.Open(new TerrainTileTarget(terrain, sink),
                requesterId);
            if (t == null) { return null; }
            transactions.Add(t);
            transactionTerrains.Add(terrain);
            return t;
        }

        /// <summary> The open transaction covering a tile, opening one the first time this build reaches it.
        ///
        /// A build normally writes one tile several times -- once per spline that crosses it -- and each
        /// <see cref="OpenTransaction"/> takes a fresh snapshot. Snapshotting again after the first write would record
        /// the ROAD as the original ground, so a roll-back would put the road's own conformed surface back instead of
        /// the hill, and the perimeter blend would carry the second write down to the first write's edge rather than to
        /// untouched ground. One transaction per tile per build is therefore not an optimisation, it is the correctness
        /// condition; the saved array read is incidental. </summary>
        public TerrainWriteTransaction TransactionFor(Terrain terrain, string requesterId)
        {
            if (terrain == null) { return null; }
            for (int i = 0; i < transactionTerrains.Count; i++)
            {
                if (transactionTerrains[i] == terrain && transactions[i] != null && transactions[i].IsOpen)
                {
                    return transactions[i];
                }
            }
            return OpenTransaction(terrain, requesterId);
        }

        /// <summary> Settle every tile this build touched that no transaction is going to settle for itself. </summary>
        private void SettleTouched()
        {
            if (sink == null) { return; }
            for (int i = 0; i < touched.Count; i++)
            {
                Terrain t = touched[i];
                if (t == null) { continue; }
                bool covered = false;
                for (int j = 0; j < transactionTerrains.Count; j++)
                {
                    if (transactionTerrains[j] == t) { covered = true; break; }
                }
                if (covered) { continue; }
                sink.Settle(t, touchedHoles[i]);
                settleCount++;
            }
        }

        /// <summary> Close every transaction the build opened, one way or the other. Each one settles its own tile, so
        /// this is counted here too and the two counts add up to the tiles that were written. </summary>
        private void CloseTransactions(bool commit)
        {
            for (int i = transactions.Count - 1; i >= 0; i--)
            {
                TerrainWriteTransaction t = transactions[i];
                if (t == null || !t.IsOpen) { continue; }
                if (commit) { t.Commit(); } else { t.RollBack(watertight); }
                settleCount++;
            }
            transactions.Clear();
            transactionTerrains.Clear();
        }

        /// <summary> Abandon the build: every transaction is rolled back to the ground it found, every touched tile is
        /// settled so physics matches what is there now, and the region is still announced when anything had actually
        /// reached the terrain -- a host that heard nothing would keep a healed corridor it no longer needs.
        ///
        /// This is the operation RM-99's asynchronous work depends on. Once a build can be cancelled halfway,
        /// interruption is normal rather than exceptional, and "half-written" is the only state a permanent hole can
        /// survive in. Returns the region that was put back. </summary>
        public TerrainRegion AbortBuild(string reason)
        {
            lastAbortReason = string.IsNullOrEmpty(reason) ? "(no reason given)" : reason;
            abortCount++;

            bool anythingApplied = false;
            for (int i = 0; i < transactions.Count; i++)
            {
                if (transactions[i] != null && transactions[i].Applied) { anythingApplied = true; break; }
            }
            CloseTransactions(false);
            SettleTouched();

            TerrainRegion extent = pending.Union;
            if (buildOpen && extent.hasArea && sink != null && (anythingApplied || touched.Count > 0))
            {
                pending.Coalesce(TerrainRegionSet.DefaultLimit);
                for (int i = 0; i < pending.Count; i++)
                {
                    sink.NotifyRegionChanged(pending[i]);
                    notifyCount++;
                }
            }

            buildOpen = false;
            buildLabel = string.Empty;
            pending.Clear();
            touched.Clear();
            touchedHoles.Clear();
            return extent;
        }
        #endregion


        #region "The writes"
        /// <summary> The heightfield. In standalone mode this is the vendored tool's own <c>SetHeights</c>; in
        /// connected mode the provider owns the terrain, so nothing is written and the region is requested instead.
        /// </summary>
        public TerrainWriteReceipt WriteHeights(Terrain terrain, float[,] heights, TerrainRegion region,
            string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Heights, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteHeights(terrain, heights); }
            Account(TerrainWriteKind.Heights, region, requesterId);
            return r;
        }

        /// <summary> The heightfield, written the watertight way: inside an all-or-nothing transaction, with the
        /// perimeter of the change carried back to the ground the tile had before the build started.
        ///
        /// This is the entry point the road's own terraforming uses, and the plain <see cref="WriteHeights"/> above is
        /// what remains for a caller that has already blended (a restore, which is putting a snapshot back and must not
        /// be softened at its edge, or a test that means to write an array verbatim). The difference is the whole of
        /// RM-98's fourth cause: a conform that stops dead at the corridor edge leaves the boundary sample owned by
        /// whichever pass ran last, and that hairline seam behaves like a chasm to a wheel and a capsule.
        ///
        /// <paramref name="written"/> is the sample window the worker actually changed -- the bounding box of the
        /// samples it recorded, not the corridor rounded up -- because the blend band is measured OUTWARD from it and a
        /// window that overstates the change would soften ground the road is standing on. Everything else is measured:
        /// the drop comes from comparing the array against the snapshot, and the number of blend rings comes from that
        /// drop and the tile's own sample spacing, so a 4 m cut on a metre-spaced terrain gets eleven rings and a
        /// 20 cm conform gets the floor of three. </summary>
        public TerrainWriteReceipt WriteHeightsWatertight(Terrain terrain, float[,] heights,
            HeightSampleWindow written, TerrainRegion region, string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Heights, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);

            TerrainWriteTransaction transaction = TransactionFor(terrain, requesterId);
            if (transaction == null || heights == null)
            {
                // No transaction is possible (no sink, or no terrain), so there is nothing to snapshot and nothing to
                // blend against. Writing the array unblended is still better than dropping the build on the floor.
                if (sink != null && sink.Path == TerrainWritePath.DirectWrite && heights != null)
                {
                    sink.WriteHeights(terrain, heights);
                }
                Account(TerrainWriteKind.Heights, region, requesterId);
                return r;
            }

            float[,] before = transaction.HeightsBefore;
            int resolution = heights.GetLength(0);
            double heightRange = 0.0;
            double spanX = 0.0;
            double spanZ = 0.0;
            if (terrain != null && terrain.terrainData != null)
            {
                Vector3 size = terrain.terrainData.size;
                heightRange = size.y;
                spanX = size.x;
                spanZ = size.z;
            }

            // The tighter of the two spacings decides the step limit: a tile that is not square would otherwise be
            // judged by its generous axis and pass a seam that is a wall along the other one.
            double spacingX = WatertightTerrainWrite.SampleSpacingMetres(spanX, resolution);
            double spacingZ = WatertightTerrainWrite.SampleSpacingMetres(spanZ, resolution);
            double spacing = spacingZ > 0.0 && spacingZ < spacingX ? spacingZ : spacingX;

            HeightSampleWindow changed = written;
            if (!changed.HasSamples) { changed = HeightSampleWindow.Whole(resolution); }

            double worstDelta = WatertightTerrainWrite.WorstDeltaMetres(heights, before, changed, heightRange);
            int blend = WatertightTerrainWrite.RequiredBlendSamples(worstDelta, spacing, watertight);
            HeightSampleWindow neighbourhood = WatertightTerrainWrite.NeighbourhoodOf(changed, blend, resolution,
                watertight);

            // Outward first, then down. The worker's array holds untouched ground everywhere it did not conform, so in
            // the band the written and original surfaces are the same and blending them would change nothing at all --
            // the whole drop would stay on the single sample at the corridor edge. Carrying the corridor's own edge
            // value out across the band first gives the blend a face to tilt.
            WatertightTerrainWrite.ExtendOutward(heights, changed, neighbourhood);
            WatertightTerrainWrite.BlendPerimeter(heights, before, neighbourhood, blend);

            transaction.ApplyHeights(heights);

            lastWorstDeltaMetres = worstDelta;
            lastBlendSamples = blend;
            lastNeighbourhood = neighbourhood;
            watertightWriteCount++;

            Account(TerrainWriteKind.Heights, region, requesterId);
            return r;
        }

        private double lastWorstDeltaMetres;
        private int lastBlendSamples;
        private HeightSampleWindow lastNeighbourhood;
        private int watertightWriteCount;

        /// <summary> The drop the last watertight write had to absorb, in metres. Measured, not configured. </summary>
        public double LastWorstDeltaMetres { get { return lastWorstDeltaMetres; } }

        /// <summary> How many blend rings the last watertight write used. </summary>
        public int LastBlendSamples { get { return lastBlendSamples; } }

        /// <summary> The window the last watertight write actually covered, blend band included. Its
        /// <see cref="HeightSampleWindow.clampedAtEdge"/> says whether the band ran into the tile edge, where the seam
        /// belongs to the neighbouring tile rather than to this write. </summary>
        public HeightSampleWindow LastNeighbourhood { get { return lastNeighbourhood; } }

        /// <summary> How many height writes went through the watertight path this gateway's lifetime. A build that
        /// conformed terrain and reports zero here has bypassed the blend. </summary>
        public int WatertightWriteCount { get { return watertightWriteCount; } }

        /// <summary> One detail (grass) layer's density map. </summary>
        public TerrainWriteReceipt WriteDetailLayer(Terrain terrain, int layerIndex, int[,] density,
            TerrainRegion region, string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.DetailLayer, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteDetailLayer(terrain, layerIndex, density); }
            Account(TerrainWriteKind.DetailLayer, region, requesterId);
            return r;
        }

        /// <summary> The tree instance set. </summary>
        public TerrainWriteReceipt WriteTrees(Terrain terrain, TreeInstance[] trees, TerrainRegion region,
            string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Trees, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteTrees(terrain, trees); }
            Account(TerrainWriteKind.Trees, region, requesterId);
            return r;
        }

        /// <summary> An alphamap block. </summary>
        public TerrainWriteReceipt WriteSplat(Terrain terrain, int x, int z, float[,,] alphamaps,
            TerrainRegion region, string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Splat, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteSplat(terrain, x, z, alphamaps); }
            Account(TerrainWriteKind.Splat, region, requesterId);
            return r;
        }

        /// <summary> A hole-map block. </summary>
        public TerrainWriteReceipt WriteHoles(Terrain terrain, int x, int z, bool[,] holes, TerrainRegion region,
            string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Holes, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            MarkHoles(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteHoles(terrain, x, z, holes); }
            Account(TerrainWriteKind.Holes, region, requesterId);
            return r;
        }

        /// <summary> Put terrain back as it was, when a road or structure is deleted. Restoring is a CHANGE, so it
        /// accumulates and notifies exactly like a build -- the spec is explicit that the provider must hear about a
        /// delete. In connected mode nothing is written: the provider knows the natural heightfield, and the tool
        /// releases what it requested instead of replaying a stored copy. </summary>
        public TerrainWriteReceipt RestoreHeights(Terrain terrain, float[,] heights, TerrainRegion region,
            string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Restore, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteHeights(terrain, heights); }
            Account(TerrainWriteKind.Restore, region, requesterId);
            return r;
        }

        /// <summary> Restore one detail layer. </summary>
        public TerrainWriteReceipt RestoreDetailLayer(Terrain terrain, int layerIndex, int[,] density,
            TerrainRegion region, string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Restore, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteDetailLayer(terrain, layerIndex, density); }
            Account(TerrainWriteKind.Restore, region, requesterId);
            return r;
        }

        /// <summary> Restore the tree set. </summary>
        public TerrainWriteReceipt RestoreTrees(Terrain terrain, TreeInstance[] trees, TerrainRegion region,
            string requesterId)
        {
            TerrainWriteReceipt r = Prepare(TerrainWriteKind.Restore, region, requesterId);
            if (!r.Accepted) { return r; }
            Touch(terrain);
            if (sink.Path == TerrainWritePath.DirectWrite) { sink.WriteTrees(terrain, trees); }
            Account(TerrainWriteKind.Restore, region, requesterId);
            return r;
        }

        /// <summary> The common half of every write: the receipt, and the two refusals that are stated in words
        /// rather than faked -- no sink at all, and a region nobody described. </summary>
        private TerrainWriteReceipt Prepare(TerrainWriteKind kind, TerrainRegion region, string requesterId)
        {
            TerrainWriteReceipt r;
            r.kind = kind;
            r.region = region;
            r.requesterId = requesterId != null ? requesterId : string.Empty;
            r.refusalReason = string.Empty;
            r.path = sink != null ? sink.Path : TerrainWritePath.Refused;

            if (sink == null)
            {
                r.path = TerrainWritePath.Refused;
                r.refusalReason = "no terrain sink is installed: the gateway will not write terrain it cannot describe";
                return r;
            }
            if (!region.hasArea)
            {
                r.path = TerrainWritePath.Refused;
                r.refusalReason = "the caller did not say WHICH region this change covers, so it could not be "
                    + "accumulated for the provider: pass a real region";
                return r;
            }
            return r;
        }
        #endregion


        #region "Holds: every request has a matching release"
        private int nextHandleId;
        private readonly List<TerrainRequestHandle> outstanding = new List<TerrainRequestHandle>();

        /// <summary> How many holds have been requested and not yet released. </summary>
        public int OutstandingCount { get { return outstanding.Count; } }

        /// <summary> Every outstanding hold, named, so a missing release is obvious at review. </summary>
        public IList<string> DescribeOutstanding()
        {
            List<string> lines = new List<string>();
            for (int i = 0; i < outstanding.Count; i++) { lines.Add(outstanding[i].Describe()); }
            return lines;
        }

        /// <summary> Ask for a region to be held open as about-to-change, so the provider can pre-load and heal it.
        /// Release with <see cref="ReleaseCutRegion"/>. </summary>
        public TerrainRequestHandle RequestCutRegion(TerrainRegion region, string requesterId, out string refusal)
        {
            return RequestHold(TerrainRequestKind.CutRegion, region, requesterId, out refusal);
        }

        /// <summary> Give back a cut region. </summary>
        public bool ReleaseCutRegion(TerrainRequestHandle handle)
        {
            return ReleaseHold(TerrainRequestKind.CutRegion, handle);
        }

        /// <summary> Ask for a hole through the surface for a tunnel mouth, kept cut across regenerations. Release
        /// with <see cref="ReleaseTunnelOpening"/>. </summary>
        public TerrainRequestHandle RequestTunnelOpening(TerrainRegion region, string requesterId, out string refusal)
        {
            return RequestHold(TerrainRequestKind.TunnelOpening, region, requesterId, out refusal);
        }

        /// <summary> Fill a tunnel opening back in. </summary>
        public bool ReleaseTunnelOpening(TerrainRequestHandle handle)
        {
            return ReleaseHold(TerrainRequestKind.TunnelOpening, handle);
        }

        /// <summary> Ask for a flat building pad to be held against terrain regeneration (RM-22). Release with
        /// <see cref="ReleaseBuildingPad"/>. </summary>
        public TerrainRequestHandle RequestBuildingPad(TerrainRegion region, string requesterId, out string refusal)
        {
            return RequestHold(TerrainRequestKind.BuildingPad, region, requesterId, out refusal);
        }

        /// <summary> Give back a building pad. </summary>
        public bool ReleaseBuildingPad(TerrainRequestHandle handle)
        {
            return ReleaseHold(TerrainRequestKind.BuildingPad, handle);
        }

        /// <summary> The one implementation behind the three named pairs. A sink that cannot grant a kind refuses in
        /// words and returns a DEAD handle -- never a plausible-looking zero that a caller would happily release.
        /// </summary>
        private TerrainRequestHandle RequestHold(TerrainRequestKind kind, TerrainRegion region, string requesterId,
            out string refusal)
        {
            refusal = string.Empty;
            if (sink == null)
            {
                refusal = "no terrain sink is installed: nothing can be held open";
                return TerrainRequestHandle.None;
            }
            if (!region.hasArea)
            {
                refusal = "a hold needs a region: none was given";
                return TerrainRequestHandle.None;
            }

            int providerHandle = sink.RequestHold(kind, region);
            if (providerHandle < 0)
            {
                refusal = RefusalFor(kind);
                return TerrainRequestHandle.None;
            }

            nextHandleId++;
            TerrainRequestHandle h;
            h.id = nextHandleId;
            h.kind = kind;
            h.providerHandle = providerHandle;
            h.requesterId = requesterId != null ? requesterId : string.Empty;
            h.region = region;
            outstanding.Add(h);

            // A hold is also a change the provider will want to hear about.
            pending.Add(region);
            return h;
        }

        /// <summary> Words for a hold this mode cannot grant. Refusing in words rather than faking is § 10.24's rule.
        /// </summary>
        private string RefusalFor(TerrainRequestKind kind)
        {
            if (kind == TerrainRequestKind.TunnelOpening)
            {
                return "a tunnel opening that stays cut through terrain regeneration needs a connected terrain "
                    + "provider; in standalone mode the tunnel shell is placed but the surface is not opened";
            }
            if (kind == TerrainRequestKind.BuildingPad)
            {
                return "a building pad held against terrain regeneration needs a connected terrain provider; in "
                    + "standalone mode the pad is terraformed once and stays until something else changes it";
            }
            return "this mode cannot hold a region open: " + PathLine();
        }

        /// <summary> Give a hold back. A handle that was never granted, or was already released, returns false rather
        /// than silently succeeding -- a double release is a bug worth seeing. </summary>
        private bool ReleaseHold(TerrainRequestKind kind, TerrainRequestHandle handle)
        {
            if (!handle.IsValid || handle.kind != kind) { return false; }
            for (int i = 0; i < outstanding.Count; i++)
            {
                if (outstanding[i].id != handle.id) { continue; }
                if (sink != null) { sink.ReleaseHold(kind, outstanding[i].providerHandle); }
                pending.Add(outstanding[i].region);
                outstanding.RemoveAt(i);
                return true;
            }
            return false;
        }

        /// <summary> Release everything still held, for a scene teardown. Returns how many were still outstanding --
        /// a non-zero count is a missing release somewhere, and it is reported rather than absorbed. </summary>
        public int ReleaseAll()
        {
            int count = outstanding.Count;
            for (int i = outstanding.Count - 1; i >= 0; i--)
            {
                if (sink != null) { sink.ReleaseHold(outstanding[i].kind, outstanding[i].providerHandle); }
                pending.Add(outstanding[i].region);
                outstanding.RemoveAt(i);
            }
            return count;
        }
        #endregion


        #region "Report"
        /// <summary> The gateway's own state in a few lines: which path, how many notifications, what is still held.
        /// </summary>
        public string Describe()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(PathLine());
            sb.Append(string.Format(
                "\n  writes recorded: {0} of {1}   builds: {2}   announcements: {3}   outstanding holds: {4}",
                ledger.Count, ledger.TotalAdded, buildCount, notifyCount, outstanding.Count));
            IList<string> held = DescribeOutstanding();
            for (int i = 0; i < held.Count; i++) { sb.Append("\n  still held: " + held[i]); }
            return sb.ToString();
        }
        #endregion
    }
}
