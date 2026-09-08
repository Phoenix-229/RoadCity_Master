#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UniverseCreator.Nodes;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-18. The caller the tunnel chain never had.
    ///
    /// Everything a tunnel needs already existed twice over and neither half could be reached from an inspector. The
    /// legacy geometry path had the state, the matcher, the mesh span and the raised ground; the City layer had the
    /// cover arithmetic, the opening box, the portal notches and the gateway that arbitrates a hole in the terrain. A
    /// sweep for callers of the second one found exactly one file outside its own types, and that file was its gate. A
    /// gate proving code works is not an author being able to use it.
    ///
    /// So this is deliberately thin, and it is the ONLY thing here that is new: it reads the author's matched pair
    /// through <see cref="TunnelRunReader"/>, plans it through <see cref="TunnelLinkerBridge"/>, and carries the plan
    /// across the link through <see cref="TerrainWriteGateway"/>. No geometry is computed here, no cover rule is
    /// restated, and no second decision about what a tunnel is gets made -- because the failure this prevents is
    /// exactly the one two independent tunnel paths produce: a lined bore whose ground was never cut, or a cut with no
    /// lining, depending on which of them ran.
    ///
    /// It reports through <see cref="BuildDispatch"/> rather than doing the work in the author's click, for the reason
    /// B-14 gives: a terrain write is a build, and a build the author cannot see the progress of or cancel is the freeze
    /// they complained about. </summary>
    public static class TunnelAuthoringLink
    {
        #region "What one attempt produced"
        /// <summary> The answer to "what happened to this tunnel", in the terms the inspector prints.
        ///
        /// Three states rather than two, because "refused" and "not planned" are different things to an author: a run
        /// that could not be MEASURED (no spline, no road, the pair not matched yet) is not the same as a run that was
        /// measured and then refused for want of cover, and only the second one is the tool saying no.
        ///
        /// There is deliberately no "executed" here. Nothing in this file can report that, because the write is queued
        /// on <see cref="BuildDispatch"/> and happens turns later; a flag set at queue time would be a claim the code
        /// cannot back, and B-14's Background Tasks is where the author watches it land. </summary>
        public struct TunnelAuthoringOutcome
        {
            /// <summary> A run was found and measured. False means there was nothing to plan. </summary>
            public bool planned;

            /// <summary> The terrain opening was accepted onto the build queue. The furthest this file can honestly
            /// report: whether the ground actually opened is the dispatcher's to say, in Background Tasks. </summary>
            public bool queued;

            /// <summary> Why not, in words, whenever the run could not be measured or the plan was refused. Never a
            /// code. </summary>
            public string refusal;

            public TunnelLinkerBridge.TunnelLinkPlan plan;
            public TunnelRunReader.SplineTunnelRun run;
            public TunnelMode mode;

            /// <summary> True when there is a tunnel here that the tool will build. </summary>
            public bool Viable { get { return planned && plan.viable; } }

            public string Describe()
            {
                if (!planned) { return "no tunnel to plan: " + Reason(); }
                if (!plan.viable) { return "tunnel refused: " + Reason(); }
                return plan.Describe();
            }

            private string Reason()
            {
                return string.IsNullOrEmpty(refusal) ? "no reason given" : refusal;
            }
        }
        #endregion


        #region "Which mode the link is in"
        /// <summary> Connected when a host owns the ground, standalone when the tool does.
        ///
        /// This is read from the gateway rather than authored, because it is not a preference: it is the fact of whether
        /// there is a provider to cut a hole. Standalone is not a failure and is not faked -- the bore is lined and
        /// visible and the ground is left alone, which <see cref="TunnelBuildOutcome.LinedButNotCut"/> says out loud so
        /// an author is never left wondering why the hill is still solid. </summary>
        public static TunnelMode ModeFor(TerrainWriteGateway gateway)
        {
            return gateway != null && gateway.Mode == TerrainProviderMode.Connected
                ? TunnelMode.Connected
                : TunnelMode.Standalone;
        }
        #endregion


        #region "Reading the author's run"
        /// <summary> The one run that belongs to this start node, measured.
        ///
        /// The whole spline is read and one entry kept, rather than a second traversal being written for a single run.
        /// <see cref="TunnelRunReader.Read"/> takes ONE water level for a call and only this node's is being used, so
        /// the other entries it returns were measured against the wrong water and are discarded unexamined -- which
        /// costs a few terrain samples and buys the guarantee that there is one implementation of "what is a tunnel run
        /// on this spline".
        ///
        /// The match is on the start time, with the tolerance <see cref="SplineC.IsInTunnel"/> already uses for the same
        /// comparison, because <c>TunnelParams</c> is built from <c>node.time</c> verbatim. </summary>
        public static bool TryReadRun(SplineN startNode, out TunnelRunReader.SplineTunnelRun run, out string refusal)
        {
            run = new TunnelRunReader.SplineTunnelRun();
            refusal = string.Empty;

            if (startNode == null)
            {
                refusal = "there is no node to read a tunnel from";
                return false;
            }
            if (!startNode.isTunnelStart)
            {
                refusal = "this node does not start a tunnel";
                return false;
            }
            if (!startNode.isTunnelMatched || startNode.tunnelCounterpartNode == null)
            {
                refusal = "this tunnel has no exit portal yet, so it has no length to plan. Switch on \"Tunnel end\""
                    + " at a later node.";
                return false;
            }

            SplineC spline = startNode.spline;
            if (spline == null)
            {
                refusal = "this node is not on a spline";
                return false;
            }
            Road road = spline.road;
            if (road == null)
            {
                refusal = "this spline has no road, so the corridor the portals have to clear is unknown";
                return false;
            }

            TerrainWriteGateway gateway = TerrainWriteGateway.Active;
            ITerrainSurfaceReader ground = gateway != null ? gateway.SurfaceReader : null;

            List<TunnelRunReader.SplineTunnelRun> runs = TunnelRunReader.Read(spline, road, ground,
                startNode.tunnelWaterSurfaceY, startNode.isTunnelImmersedTube, DefaultSamplesPerRun);

            for (int index = 0; index < runs.Count; index++)
            {
                if (RootUtils.IsApproximately(runs[index].startTime, startNode.time, 0.0001f))
                {
                    run = runs[index];
                    return true;
                }
            }

            // The pair is matched on the node and absent from the spline's own list. There are exactly two ways that
            // happens and they need different answers, so they are told apart rather than sharing one vague sentence.
            // SplineC builds a TunnelParams entry only for a start node that is NOT also a bridge start, so a node
            // carrying both drops its tunnel permanently -- CanTunnelStart refuses that now, and this is the backstop
            // for a scene saved before it did. Otherwise the span simply has not been rebuilt yet, which is a "not
            // yet" and not a "no".
            refusal = startNode.isBridgeStart
                ? "this node also starts a bridge, and the spline records only the bridge -- so the tunnel has no span"
                    + " at all. Switch the bridge off, or start the tunnel at another node."
                : "the spline has not recorded this tunnel's span yet, because the road is still rebuilding. It will be"
                    + " planned once the rebuild finishes.";
            return false;
        }

        /// <summary> How many stations the ground over a run is sampled at. <see cref="TunnelRunReader"/>'s own default,
        /// named here so the inspector path and a gate ask for the same fineness rather than one of them taking whatever
        /// the overload happened to pass. </summary>
        public const int DefaultSamplesPerRun = 21;
        #endregion


        #region "Planning, without touching anything"
        /// <summary> Measure the run and decide, and do nothing else. Main thread -- it reads the spline, the road and
        /// the terrain surface -- but it writes no heights, asks for no hold and announces no region, so an inspector
        /// can call it every repaint to show an author what their tunnel will do before they commit to it. </summary>
        public static TunnelAuthoringOutcome PlanFor(SplineN startNode)
        {
            TunnelAuthoringOutcome outcome = new TunnelAuthoringOutcome();
            outcome.refusal = string.Empty;
            outcome.mode = ModeFor(TerrainWriteGateway.Active);

            string refusal;
            TunnelRunReader.SplineTunnelRun run;
            if (!TryReadRun(startNode, out run, out refusal))
            {
                outcome.refusal = refusal;
                return outcome;
            }

            outcome.planned = true;
            outcome.run = run;
            outcome.plan = PlanRun(run.run, outcome.mode);
            if (!outcome.plan.viable)
            {
                outcome.refusal = ShortfallLine(outcome.plan, run);
                return outcome;
            }
            return outcome;
        }

        /// <summary> The pure half, split out so it can run on a pool thread: plain <c>double</c>s and the project's
        /// engineering classes, no <c>UnityEngine.Object</c> anywhere in it. </summary>
        public static TunnelLinkerBridge.TunnelLinkPlan PlanRun(TunnelLinkerBridge.TunnelRun run, TunnelMode mode)
        {
            return TunnelLinkerBridge.Plan(run, mode, TerrainBuildSettings.Tunnel, TerrainBuildSettings.Cover,
                TerrainBuildSettings.Separation);
        }

        /// <summary> A refusal an author can act on: how short the cover is, and WHERE.
        ///
        /// The shortfall is stated in metres rather than as "insufficient cover", because the two answers an author has
        /// -- lower the road or move the tunnel -- both need a number. The position comes from the reader's tightest
        /// sample, which is the station that actually failed rather than the middle of the run. </summary>
        private static string ShortfallLine(TunnelLinkerBridge.TunnelLinkPlan plan,
            TunnelRunReader.SplineTunnelRun run)
        {
            if (plan.outcome != TunnelBuildOutcome.RefusedInsufficientCover)
            {
                return string.IsNullOrEmpty(plan.refusal) ? "the linker gave no reason" : plan.refusal;
            }
            double shortfall = plan.requiredCoverMetres - plan.presentCoverMetres;
            if (shortfall < 0.0) { shortfall = 0.0; }
            return string.Format(CultureInfo.InvariantCulture,
                "{0} It is {1:0.##} m short at x {2:0.#}, z {3:0.#}, where the ground sits at {4:0.##} m."
                + " Lower the carriageway by that much, or move the tunnel to where the bed is deeper.",
                plan.refusal, shortfall, run.tightestGroundPoint.x, run.tightestGroundPoint.z,
                run.tightestGroundPoint.y);
        }
        #endregion


        #region "Carrying it across the link"
        /// <summary> Queue the tunnel's terrain opening as a build, and report it where every other terrain write is
        /// reported.
        ///
        /// Three phases, on the three threads the dispatcher allows them on, and the split is not decoration: the run is
        /// READ on the main thread because it touches a spline, a road and a terrain; it is PLANNED on a pool thread
        /// because that part is arithmetic on doubles; and it is EXECUTED on the main thread because that is where a
        /// hold on the terrain may be asked for. A plan that is not viable abandons the build in <c>prepare</c>, which
        /// the dispatcher records as a refusal rather than a failure -- the author asked for something the ground cannot
        /// carry, which is not the tool breaking.
        ///
        /// Any opening this tunnel is already holding is released first, so toggling the pair twice cannot leave two
        /// holes for one bore.
        ///
        /// The whole outcome comes back rather than a bool and a sentence, because a caller has to be able to tell a
        /// measured-then-REFUSED run from one that could not be read yet: the first is worth a console line, the second
        /// is already on the inspector panel and saying it twice is how a log stops being read. </summary>
        public static TunnelAuthoringOutcome Raise(SplineN startNode)
        {
            TunnelAuthoringOutcome outcome = PlanFor(startNode);
            if (!outcome.Viable) { return outcome; }

            SplineN node = startNode;
            string requester = RequesterIdFor(node);
            TunnelMode mode = outcome.mode;
            TunnelRunReader.SplineTunnelRun measured = outcome.run;
            TunnelLinkerBridge.TunnelLinkPlan link = outcome.plan;

            bool queued = BuildDispatch.Shared.Enqueue(
                "tunnel opening for " + requester,
                delegate
                {
                    // Main thread. Re-read rather than trusting the preview: an author can move a node between the
                    // click and the build's turn in the queue, and a hole cut for where the tunnel used to be is worse
                    // than no hole.
                    string readRefusal;
                    TunnelRunReader.SplineTunnelRun current;
                    if (!TryReadRun(node, out current, out readRefusal)) { return false; }
                    measured = current;
                    node.ReleaseTunnelOpening();
                    return true;
                },
                delegate (StopToken stop)
                {
                    link = PlanRun(measured.run, mode);
                },
                delegate
                {
                    // Main thread. The only place a height, a hold or an announcement happens.
                    if (!link.viable)
                    {
                        UnityEngine.Debug.LogWarning("Road & City Master: " + requester + " -- "
                            + ShortfallLine(link, measured));
                        return;
                    }
                    TerrainWriteGateway gateway = TerrainWriteGateway.Active;
                    gateway.BeginBuild("tunnel opening for " + requester);
                    TunnelLinkerBridge.TunnelLinkResult result = TunnelLinkerBridge.Execute(gateway, link, requester);
                    gateway.EndBuild();

                    if (!result.executed || !result.openingHandle.IsValid)
                    {
                        UnityEngine.Debug.LogWarning("Road & City Master: " + requester
                            + " -- the terrain would not open for this tunnel. " + result.Describe());
                        return;
                    }
                    node.tunnelOpeningHold = result.openingHandle;
                });

            if (!queued)
            {
                outcome.refusal = "the build queue would not take the tunnel opening";
                return outcome;
            }
            outcome.queued = true;
            return outcome;
        }

        /// <summary> What the hold is called in the gateway's outstanding list. Names the road and the node, because a
        /// leak report that says "rcm.tunnel" three times names nothing an author can go and look at. </summary>
        public static string RequesterIdFor(SplineN node)
        {
            if (node == null) { return "rcm.tunnel.unknown"; }
            string road = node.spline != null && node.spline.road != null
                ? node.spline.road.name
                : "no road";
            return "rcm.tunnel." + road + ".node" + node.idOnSpline.ToString(CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
