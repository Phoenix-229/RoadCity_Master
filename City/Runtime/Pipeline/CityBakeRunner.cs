// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-6. Everything a bake needs that does not live in the graph: the scene, the ground, the
    /// catalogues and the seed.
    ///
    /// This class exists because of one rule the graph cannot enforce on its own -- an operator must be a function of
    /// its input and its seed, and nothing else. Scene state therefore reaches an operator INSIDE its input, never by
    /// the operator reaching out for it, which is why the catalogue has source nodes at all: a source node is the one
    /// place a live value enters the graph, and this request is what the runner fills those nodes from. An operator
    /// that read <c>Terrain.activeTerrain</c> for itself would bake differently on two machines with the same seed.
    ///
    /// The timestamp is passed IN rather than read here for the same reason. C-6 forbids <c>System.DateTime</c> in the
    /// runner: a bake that stamps itself with the clock is a bake whose output changes when nothing changed. The
    /// caller supplies the string once, and the same string lands on every stamp of that bake. </summary>
    public sealed class CityBakeRequest
    {
        /// <summary> The scene object the five branches hang under. Null bakes data only and writes no objects, which
        /// is what a gate wants. </summary>
        public Transform cityRoot;

        /// <summary> The city's seed. Every operator seed in the bake is derived from this one integer through
        /// <see cref="CitySeed.ForOperator"/>, so the whole bake is reproducible from it. </summary>
        public int citySeed;

        /// <summary> The city's rectangle in world XZ metres, which a Region source node publishes. </summary>
        public Rect region;

        /// <summary> The ground as the linker's provider, for the street, buildable-land and pad passes. </summary>
        public IPCGDataProvider terrain;

        /// <summary> The ground as a height-and-normal reader, for the suitability and corridor passes. </summary>
        public ITerrainSurfaceReader surface;

        /// <summary> C-18. The roots the hand-placed sweep walks, filled from the city's own Scan roots list. Empty means
        /// nothing is scanned, which is deliberate: a sweep that defaulted to the whole scene would ask for ground to be
        /// levelled under objects nobody mentioned. </summary>
        public List<Transform> scanRoots;

        /// <summary> Water, land use and the existing network, as the suitability builder reads them. Any of the three
        /// may be null: the builder drops that factor from the legend rather than inventing a figure for it. </summary>
        public ISuitabilityWaterSource water;
        public ISuitabilityLandUseSource landUse;
        public ISuitabilityNetworkSource network;

        /// <summary> The authored catalogues a source node selects one entry out of. </summary>
        public RoadClassCatalog roadClasses;
        public ZoneCatalog zones;
        public ContentCatalog content;

        /// <summary> The city being baked, for the runtime stage: the spawn table and the bake stamp both read it.
        /// </summary>
        public CityData city;

        /// <summary> What the stamp records about this bake. The timestamp is the caller's, never the clock's. </summary>
        public string generatorVersion = string.Empty;
        public string sourceCityAssetGuid = string.Empty;
        public string bakedAtUtc = string.Empty;

        /// <summary> Which settlement order this city is, so an element node reads the right one of its four rows.
        /// </summary>
        public int settlementOrder;

        /// <summary> B-27 / D-6, D-8, D-9. The fitted settlement table a Settlement fit source node publishes, so the
        /// rail-presence, transit and underground nodes read one table rather than each finding their own. It is an
        /// asset like the three catalogues above and not a scene reference, so the editor fills it from a found
        /// <c>t:SettlementFitTable</c> or from <see cref="SettlementFitTable.CreateAuthored"/> -- never null, so those
        /// nodes refuse with "no fit table" only when an author deliberately clears it, not merely because none was
        /// authored yet. </summary>
        public SettlementFitTable settlementFit;
    }

    /// <summary> What one node did, recorded back onto the node itself so the window has nothing to compute. C-9's rule
    /// is that the window draws and the pipeline decides; a window that timed the bake itself would be a second
    /// measurement of the same thing, and the two would disagree. </summary>
    public struct CityNodeRun
    {
        public string nodeId;
        public CityNodeKind kind;
        public BakeStage stage;

        /// <summary> How long the node's own call took. Not the stage, and not the bake: one node. </summary>
        public double milliseconds;

        /// <summary> How many things the node produced -- poles, lots, spans, chunks. Negative means the node produced
        /// no countable list, which is different from producing none. </summary>
        public int itemCount;

        /// <summary> Why a node did nothing, when it did nothing. Empty on a clean run. </summary>
        public string note;

        /// <summary> False when the node was passed over: a comment, a blocked entry, or a node whose input never
        /// arrived. A passed-over node is not a failure -- a graph is allowed to be half-wired while it is being
        /// built. </summary>
        public bool ran;
    }

    /// <summary> The result of one bake: whether it ran, the sentence that says why if it did not, the stage sequence
    /// that was actually observed, and one row per node. </summary>
    public sealed class CityBakeReport
    {
        public bool ok;

        /// <summary> The refusal, or the summary. Always a complete sentence, because it is shown to a person.
        /// </summary>
        public string report = string.Empty;

        /// <summary> The stages the walk actually visited, in the order it visited them. This is recorded rather than
        /// assumed and then asserted with <see cref="CityBakePipeline.IsInCanonicalOrder"/> -- the one check that
        /// catches the walker's own loop being wrong, which no other check in the tool would notice. </summary>
        public List<BakeStage> observedStages = new List<BakeStage>();

        public List<CityNodeRun> runs = new List<CityNodeRun>();

        /// <summary> How many nodes ran, and how many were passed over with a reason. </summary>
        public int nodesRun;
        public int nodesPassedOver;

        /// <summary> The whole bake's wall time, from the first stage to the last. </summary>
        public double totalMilliseconds;
    }

    /// <summary> Where a node's outputs go and where its inputs come from. One dictionary keyed by node id and port
    /// name, and reads that follow an edge backwards.
    ///
    /// Keyed by NAME rather than by index because the catalogue names ports and the graph's edges carry those names:
    /// an index would be a second identity for the same thing, and the two would drift the first time a port was
    /// inserted. Boxed <c>object</c> rather than a generic store because one bus carries a <c>Rect</c>, a
    /// <c>StreetNetwork</c> and a <c>List&lt;Catenary&gt;</c>, and the catalogue's payload text is what says which is
    /// which -- the graph already refuses a wire whose two ends disagree, so the cast here is checked once, at the
    /// read, and reported as the node's own note rather than thrown. </summary>
    public sealed class CityBakeBus
    {
        private readonly Dictionary<string, object> published = new Dictionary<string, object>();

        /// <summary> Node id and port name joined by the unit separator, which is a control character and so cannot
        /// occur in either half. A dot would collide with a port name that contains one. </summary>
        private static string Key(string nodeId, string portName)
        {
            return nodeId + "\u001f" + portName;
        }

        /// <summary> Records what left one output port. Publishing twice for the same port overwrites, which cannot
        /// happen in one walk because a node runs once. </summary>
        public void Publish(string nodeId, string portName, object value)
        {
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(portName)) { return; }
            published[Key(nodeId, portName)] = value;
        }

        /// <summary> True when that output port has a value on the bus. </summary>
        public bool HasOutput(string nodeId, string portName)
        {
            return !string.IsNullOrEmpty(nodeId) && !string.IsNullOrEmpty(portName)
                   && published.ContainsKey(Key(nodeId, portName));
        }

        /// <summary> Reads a finished output by node and port without pretending it is an input edge. This is the
        /// hand-off used by editor tools after a bake; graph nodes continue to use <see cref="TryReadInput"/>. </summary>
        public bool TryTakeOutput(string nodeId, string portName, out object value)
        {
            value = null;
            if (string.IsNullOrEmpty(nodeId) || string.IsNullOrEmpty(portName)) { return false; }
            return published.TryGetValue(Key(nodeId, portName), out value);
        }

        /// <summary> Reads one input port by following the edge that arrives at it. Returns false, with a sentence,
        /// when nothing is wired there or when the far end never produced a value -- both of which are ordinary states
        /// for a graph that is still being built, and neither of which is a failure. </summary>
        public bool TryReadInput(CityGraph graph, CityGraphNode node, string portName, out object value,
            out string reason)
        {
            value = null;
            reason = string.Empty;
            if (graph == null || node == null || string.IsNullOrEmpty(portName))
            {
                reason = "there is no port to read";
                return false;
            }

            for (int i = 0; i < graph.edges.Count; i++)
            {
                CityGraphEdge edge = graph.edges[i];
                if (edge == null || edge.toNodeId != node.id || edge.toPort != portName) { continue; }
                if (published.TryGetValue(Key(edge.fromNodeId, edge.fromPort), out value)) { return true; }
                reason = "the port " + portName + " is wired from " + edge.fromNodeId + "." + edge.fromPort
                         + ", but that port produced no value, so there is nothing to read";
                return false;
            }

            reason = "nothing is wired into the port " + portName;
            return false;
        }
    }

    /// <summary> One bake in progress: the graph being walked, the values on the bus, the scene-side request, and the
    /// scene objects written so far.
    ///
    /// It exists so a per-kind call needs one argument instead of five, and so the two things a node may touch outside
    /// itself -- the bus and the hierarchy -- are reached through named methods rather than through fields a future
    /// node could write to directly. </summary>
    public sealed class CityBakeContext
    {
        public CityGraph graph;
        public CityBakeRequest request;
        public CityBakeBus bus = new CityBakeBus();

        /// <summary> How many objects of each generated kind this bake has written, so the next one gets the next
        /// ordinal. Per kind rather than per bake, because <see cref="CityHierarchy.NameFor"/> numbers within a kind.
        /// </summary>
        private readonly Dictionary<string, int> ordinals = new Dictionary<string, int>();

        /// <summary> Finds or creates the scene object for one generated thing, under the branch its kind belongs to
        /// and under the name <see cref="CityHierarchy.NameFor"/> gives it.
        ///
        /// FINDS before it creates, which is the whole point: a re-bake of the same city with the same seed produces
        /// the same names, so it reuses the objects it made last time instead of leaving a second copy beside them.
        /// That is also why the ordinal counter is per kind and per bake rather than a running total -- a total would
        /// shift every name the first time an earlier node produced one more item, and a re-bake would then duplicate
        /// everything downstream of it.
        ///
        /// Returns null when the request has no city root, which is how a data-only bake runs: the whole write is
        /// skipped rather than guarded at every call site. </summary>
        public Transform EnsureObject(GeneratedKind kind, string sourceElementId)
        {
            if (request == null || request.cityRoot == null) { return null; }

            string counterKey = ((int)kind).ToString() + "\u001f" + (sourceElementId ?? string.Empty);
            int ordinal;
            ordinals.TryGetValue(counterKey, out ordinal);
            ordinals[counterKey] = ordinal + 1;

            Transform branch = CityHierarchy.EnsureBranch(request.cityRoot, CityHierarchy.BranchFor(kind));
            if (branch == null) { return null; }

            string name = CityHierarchy.NameFor(kind, sourceElementId, ordinal);
            Transform existing = branch.Find(name);
            if (existing != null) { return existing; }

            GameObject created = new GameObject(name);
            created.transform.SetParent(branch, false);
            return created.transform;
        }
    }

    /// <summary> B-15 / C-7. One bake in progress, held between the frames that advance it.
    ///
    /// A bake that shows a progress bar and answers Cancel cannot run inside one call, because the editor only repaints
    /// between calls. So the walk that used to be a loop inside <see cref="CityBakeRunner.Run"/> is now a cursor: the
    /// caller holds this object and asks for one stage at a time. <see cref="CityBakeRunner.Run"/> still exists and
    /// still bakes in one call -- it just drives this cursor in a tight loop instead of owning the loop itself, so a
    /// batchmode bake and a ticketed editor bake walk the same code in the same order with the same seeds.
    ///
    /// The fields are public because this is state, not behaviour: every method that reads them lives in
    /// <see cref="CityBakeRunner"/>, and a property pair per field would say nothing a reader does not already know.
    /// Nothing here is written after <see cref="CityBakeRunner.Finish"/>. </summary>
    public sealed class CityBakeWalk
    {
        /// <summary> The context every node is given. Built once by TryBegin and never replaced. </summary>
        public CityBakeContext context;

        /// <summary> Filled in as the walk goes, and handed back by Finish. </summary>
        public CityBakeReport report = new CityBakeReport();

        /// <summary> Wall clock for the whole bake, started by TryBegin and stopped by Finish. </summary>
        public Stopwatch clock = new Stopwatch();

        /// <summary> Empty for a full bake. Set to one node's id for the window's single-node preview, which runs that
        /// node and passes over every other -- while still counting the passed-over ones, so the previewed node is
        /// handed the seed it would get in a full bake rather than a seed that only exists in preview. </summary>
        public string onlyNodeId = string.Empty;

        /// <summary> How many canonical stages have been stepped. Also the index of the next one. </summary>
        public int stageCursor;

        /// <summary> How many nodes the preview stepped around. Counted separately from
        /// <see cref="CityBakeReport.nodesPassedOver"/>, which means a node that refused for a reason written on itself:
        /// a preview skipping a node is the caller's choice, not the node's verdict, and folding the two together would
        /// send a person hunting for a note that was never written. </summary>
        public int skippedForPreview;

        /// <summary> True once Finish has run, so a caller that keeps pumping is told rather than silently re-summarising. </summary>
        public bool finished;

        /// <summary> Stages done over stages total, for the ticket's progress bar. Stage-grained on purpose: the node
        /// count of a stage is not known until the stage is walked, so a finer figure would be a guess that jumped
        /// backwards when the guess was wrong. </summary>
        public float Progress01
        {
            get
            {
                int total = CityBakePipeline.CanonicalOrder.Length;
                if (total <= 0) { return 1f; }
                float done = stageCursor;
                if (done > total) { done = total; }
                return done / total;
            }
        }
    }

    /// <summary> B-15 / C-6. The one thing that bakes a city graph: it validates, walks the stages in the canonical
    /// order exactly once, calls each node's shipped code or operator asset, and records what happened back onto the
    /// nodes.
    ///
    /// Static because a bake keeps no state of its own between bakes: what one bake needs is the graph plus the
    /// request, what it produced is the report plus the scene, and the little that has to survive between frames lives
    /// in the caller's <see cref="CityBakeWalk"/>. A field on the runner would invite a value that survived one bake
    /// into the next, which is the exact bug the seed rule exists to prevent.
    ///
    /// It walks the stages ONCE and in <see cref="CityBakePipeline.CanonicalOrder"/>, and then asserts the sequence it
    /// actually visited with <see cref="CityBakePipeline.IsInCanonicalOrder"/>. That looks redundant -- the loop reads
    /// the canonical order, so of course the result is canonical -- and it is the one check in the tool that would
    /// catch this loop itself being wrong. Every other check reads the graph; this one reads the walker. </summary>
    public static class CityBakeRunner
    {
        /// <summary> Bakes one graph in a single call, for batchmode and for any caller with no frames to spare.
        ///
        /// This is the cursor below driven in a tight loop, not a second walk. Returns false with a sentence in the
        /// report when the graph was refused, when the request was missing, or when the walk visited its stages out of
        /// order. </summary>
        public static bool Run(CityGraph graph, CityBakeRequest request, out CityBakeReport report)
        {
            CityBakeWalk walk;
            if (!TryBegin(graph, request, string.Empty, out walk, out report)) { return false; }

            while (StepOneStage(walk)) { }

            return Finish(walk, out report);
        }

        /// <summary> Opens a bake and hands back the cursor that advances it. Returns false with a sentence in the
        /// report when there is nothing legal to bake, and in that case the scene has not been touched.
        ///
        /// Validation happens BEFORE anything is written, which is why the refusal path leaves the scene untouched: a
        /// bake that half-wrote a city and then discovered the graph was invalid would leave the author to clean up
        /// after it.
        ///
        /// <paramref name="onlyNodeId"/> empty bakes the whole graph. Named, it runs that one node and passes over the
        /// rest -- see <see cref="CityBakeWalk.onlyNodeId"/> for why the passed-over ones are still counted. </summary>
        public static bool TryBegin(CityGraph graph, CityBakeRequest request, string onlyNodeId,
            out CityBakeWalk walk, out CityBakeReport report)
        {
            walk = null;
            report = new CityBakeReport();

            if (graph == null)
            {
                report.report = "there is no graph to bake";
                return false;
            }

            if (request == null)
            {
                report.report = "the bake was asked for without a request, so there is no seed, no ground and no"
                                + " region to bake from";
                return false;
            }

            string validation;
            if (!graph.Validate(out validation))
            {
                report.report = "nothing was baked, because the graph was refused: " + validation;
                return false;
            }

            CityBakeContext context = new CityBakeContext();
            context.graph = graph;
            context.request = request;

            walk = new CityBakeWalk();
            walk.context = context;
            walk.report = report;
            walk.onlyNodeId = onlyNodeId == null ? string.Empty : onlyNodeId;
            walk.clock.Start();
            return true;
        }

        /// <summary> Runs every node of the next canonical stage and returns true while stages remain.
        ///
        /// A whole stage per call rather than a single node, because the stage is where the order rule lives: a caller
        /// free to stop in the middle of one could resume into another and interleave the two by accident. The cost is
        /// that one heavy stage is one long frame, and the trade is deliberate -- the alternative is a Cancel that lands
        /// halfway through conforming a city.
        ///
        /// Node-list order within a stage rather than a second sort, because the graph's own order is the only order
        /// that exists and a re-sort here would be a second opinion about it. A stage is added to the observed sequence
        /// the first time a node of that stage is reached, so a stage with no nodes is absent rather than present and
        /// empty -- which is what makes skipping a stage legal and reversing one detectable. </summary>
        public static bool StepOneStage(CityBakeWalk walk)
        {
            if (walk == null || walk.finished || walk.context == null) { return false; }

            BakeStage[] order = CityBakePipeline.CanonicalOrder;
            if (walk.stageCursor < 0 || walk.stageCursor >= order.Length) { return false; }

            int stageIndex = walk.stageCursor;
            BakeStage stage = order[stageIndex];
            bool stageSeen = false;
            int indexInStage = 0;
            bool preview = !string.IsNullOrEmpty(walk.onlyNodeId);

            for (int n = 0; n < walk.context.graph.nodes.Count; n++)
            {
                CityGraphNode node = walk.context.graph.nodes[n];
                if (node == null || node.stage != stage) { continue; }

                if (!stageSeen)
                {
                    walk.report.observedStages.Add(stage);
                    stageSeen = true;
                }

                // The index still advances for a node the preview steps around, which is the whole point: the seed of
                // the previewed node is its position in the stage, so a preview that only counted what it ran would
                // hand node three the seed of node one and show a city the bake will not produce.
                if (preview && !string.Equals(node.id, walk.onlyNodeId, StringComparison.Ordinal))
                {
                    walk.skippedForPreview++;
                    indexInStage++;
                    continue;
                }

                RunOneNode(walk.context, node, stageIndex, indexInStage, walk.report);
                indexInStage++;
            }

            walk.stageCursor = stageIndex + 1;
            return walk.stageCursor < order.Length;
        }

        /// <summary> Closes the bake: stops the clock, checks the walker against the canonical order, and writes the
        /// sentence a person reads. Returns what <see cref="Run"/> returns, and is safe to call twice -- the second call
        /// hands back the same report rather than re-timing a finished bake. </summary>
        public static bool Finish(CityBakeWalk walk, out CityBakeReport report)
        {
            if (walk == null)
            {
                report = new CityBakeReport();
                report.report = "the bake was closed without ever being opened";
                return false;
            }

            report = walk.report;
            if (walk.finished) { return report.ok; }

            walk.finished = true;
            walk.clock.Stop();
            report.totalMilliseconds = walk.clock.Elapsed.TotalMilliseconds;

            // A walk closed before its last stage was cancelled, or the caller stopped pumping. Either way the city is
            // part-built, and saying "baked 14 nodes" about it would be true of the nodes and false of the city.
            int stagesTotal = CityBakePipeline.CanonicalOrder.Length;
            if (walk.stageCursor < stagesTotal)
            {
                report.ok = false;
                report.report = "the bake stopped after " + walk.stageCursor + " of " + stagesTotal
                                + " stages, so what is in the scene is a part-built city: bake again rather than"
                                + " reading it as finished";
                return false;
            }

            string outOfOrder;
            if (!CityBakePipeline.IsInCanonicalOrder(report.observedStages, out outOfOrder))
            {
                report.ok = false;
                report.report = "the bake ran but visited its stages in the wrong order, which is this runner's fault"
                                + " and not the graph's: " + outOfOrder;
                return false;
            }

            report.ok = true;
            report.report = Summarise(walk);
            return true;
        }

        /// <summary> Runs one node, times it, and writes the three telemetry fields back onto the node.
        ///
        /// The seed is <see cref="CitySeed.ForOperator"/> of the city seed, the stage's index in the canonical order,
        /// and the node's index WITHIN that stage. Within the stage rather than within the whole graph, so that adding
        /// a node to an earlier stage does not reseed every node after it; inserting a node ahead of others in the same
        /// stage still reseeds those, which is the honest cost of an index and the reason an author who wants a stable
        /// result appends rather than inserts. </summary>
        private static void RunOneNode(CityBakeContext context, CityGraphNode node, int stageIndex, int indexInStage,
            CityBakeReport report)
        {
            int operatorSeed = CitySeed.ForOperator(context.request.citySeed, stageIndex, indexInStage);

            int items;
            string note;
            Stopwatch clock = Stopwatch.StartNew();
            bool ran = CityNodeDispatch.Execute(context, node, operatorSeed, out items, out note);
            clock.Stop();

            CityNodeRun run = new CityNodeRun();
            run.nodeId = node.id;
            run.kind = node.kind;
            run.stage = node.stage;
            run.milliseconds = clock.Elapsed.TotalMilliseconds;
            run.itemCount = items;
            run.note = note == null ? string.Empty : note;
            run.ran = ran;
            report.runs.Add(run);

            node.lastRunMilliseconds = run.milliseconds;
            node.lastRunItemCount = run.itemCount;
            node.lastRunNote = run.note;

            if (ran) { report.nodesRun++; } else { report.nodesPassedOver++; }
        }

        /// <summary> The sentence a person reads when the bake succeeded: how many nodes ran, how many were passed over,
        /// which stages were visited, and how long it took.
        ///
        /// Written here rather than in the window because C-9's rule is that the pipeline decides and the window draws.
        /// A window that assembled this line would be a second description of the same bake, and the two would
        /// eventually disagree about what "passed over" counts. </summary>
        private static string Summarise(CityBakeWalk walk)
        {
            CityGraph graph = walk.context.graph;
            CityBakeReport report = walk.report;

            StringBuilder line = new StringBuilder();
            line.Append("baked ").Append(report.nodesRun).Append(report.nodesRun == 1 ? " node" : " nodes");

            if (report.nodesPassedOver > 0)
            {
                line.Append(" and passed over ").Append(report.nodesPassedOver)
                    .Append(report.nodesPassedOver == 1 ? " node" : " nodes")
                    .Append(" whose reason is on the node itself");
            }

            if (walk.skippedForPreview > 0)
            {
                line.Append(", previewing one node and leaving ").Append(walk.skippedForPreview)
                    .Append(walk.skippedForPreview == 1 ? " other unbaked" : " others unbaked");
            }

            line.Append(" out of ").Append(graph.nodes.Count).Append(" in ");

            if (report.observedStages.Count == 0)
            {
                line.Append("no stages at all, because the graph has no node in any stage");
            }
            else
            {
                line.Append(report.observedStages.Count)
                    .Append(report.observedStages.Count == 1 ? " stage (" : " stages (");
                for (int i = 0; i < report.observedStages.Count; i++)
                {
                    if (i > 0) { line.Append(" then "); }
                    line.Append(report.observedStages[i]);
                }
                line.Append(")");
            }

            line.Append(", in ").Append(report.totalMilliseconds.ToString("0.0")).Append(" ms");
            return line.ToString();
        }
    }
}
