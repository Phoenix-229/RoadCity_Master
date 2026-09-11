// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-7. The one way a city bake starts.
    ///
    /// <see cref="CityBakeRunner.Run"/> bakes in a single call, which is what a gate and a batchmode pass want and what
    /// an author does not: it holds the editor still for the length of a city and offers no way out. So every bake an
    /// author can start comes through here instead -- the same walk, one stage per frame, on a
    /// <see cref="BuildDispatch"/> ticket that shows up in Background Tasks with a progress bar and a Cancel that works.
    ///
    /// A modal <c>EditorUtility.DisplayProgressBar</c> would be the shorter road and is the exact mistake
    /// <c>BuildProgressReporter</c>'s header documents: it blocks the editor loop, so the bar it draws cannot repaint
    /// and the cancel it offers cannot be clicked.
    ///
    /// One entry point for both the inspector's Bake and the graph window's single-node preview, because the point of
    /// C-6 being the only runner is lost if a second caller reaches it without a ticket. The difference between the two
    /// is one argument. </summary>
    public static class CityBakeTicket
    {
        #region "Starting a bake"
        /// <summary> Queues a full bake of one graph. False with a sentence when the bake could not even be queued --
        /// play mode, no graph, no request, or a graph the runner refused -- and in that case nothing was written.
        ///
        /// The guard is asked FIRST, before the ticket exists, so a bake attempted in play mode is recorded as a blocked
        /// attempt and not as a cancelled build. </summary>
        public static bool Enqueue(CityGraph graph, CityBakeRequest request, out string refusal)
        {
            return Enqueue(graph, request, string.Empty, out refusal);
        }

        /// <summary> The same, for the window's single-node preview: <paramref name="onlyNodeId"/> names the one node to
        /// run. Every other node is stepped around but still counted, so the previewed node is handed the seed it would
        /// get in a full bake -- see <see cref="CityBakeWalk.onlyNodeId"/>. </summary>
        public static bool Enqueue(CityGraph graph, CityBakeRequest request, string onlyNodeId, out string refusal)
        {
            refusal = string.Empty;

            if (graph == null) { refusal = "there is no graph to bake"; return false; }
            if (request == null)
            {
                refusal = "the bake was asked for without a request, so there is no seed, no ground and no region to"
                          + " bake from";
                return false;
            }

            string owner = OwnerOf(graph, request);
            string label = LabelFor(graph, onlyNodeId);

            if (!CityGenerationGuard.RequireGenerationAllowed(label))
            {
                refusal = "a city cannot be baked in play mode: leave play mode and bake again";
                return false;
            }

            BuildDispatch dispatch = BuildDispatch.Shared;
            if (dispatch == null) { refusal = "there is no build dispatcher to take the bake"; return false; }

            // Bake pressed twice, or a preview asked for while the last one is still walking. The older bake of the
            // same city is stopped and this one replaces it, rather than both running and the author waiting through
            // two to see the result of one. Matching is on the owner, so two cities bake side by side.
            dispatch.Supersede(owner);

            CityBakeWalk walk = null;
            string beginRefusal = string.Empty;

            // prepare: main thread, and the only place allowed to read the graph and the terrain. TryBegin validates
            // before anything is written, so a refused graph fails here and leaves the scene untouched.
            Func<bool> prepare = delegate ()
            {
                CityBakeReport opening;
                if (CityBakeRunner.TryBegin(graph, request, onlyNodeId, out walk, out opening)) { return true; }

                beginRefusal = opening == null ? "the graph was refused" : opening.report;
                lastReport = opening;
                lastOwner = owner;
                return false;
            };

            // step: one canonical stage per pump. The dispatcher checks its own stop token after each call and concludes
            // the build as cancelled, so Cancel lands on a stage boundary and never between two nodes of one stage.
            Func<bool> step = delegate ()
            {
                if (walk == null) { return false; }
                bool more = CityBakeRunner.StepOneStage(walk);
                BuildDispatch d = BuildDispatch.Shared;
                if (d != null) { d.Report(walk.Progress01); }
                return more;
            };

            // finish: always runs, on the main thread, including for a build that was cancelled or dropped from the
            // queue. Finish is what turns a part-walked bake into a sentence saying so.
            Action finish = delegate ()
            {
                if (walk == null)
                {
                    if (lastReport == null)
                    {
                        CityBakeReport never = new CityBakeReport();
                        never.report = string.IsNullOrEmpty(beginRefusal)
                            ? "the bake never opened, and left no reason"
                            : beginRefusal;
                        lastReport = never;
                        lastOwner = owner;
                    }
                    return;
                }

                CityBakeReport closing;
                CityBakeRunner.Finish(walk, out closing);
                lastReport = closing;
                lastBus = walk.context != null ? walk.context.bus : null;
                lastOwner = owner;
            };

            if (!dispatch.EnqueueStaged(label, owner, prepare, step, finish))
            {
                refusal = "the build dispatcher would not take the bake";
                return false;
            }

            return true;
        }
        #endregion


        #region "Reading the result"
        /// <summary> The report of the last bake to close, or null. Held here because a
        /// <see cref="BuildDispatch"/> outcome carries a label and a message and not a typed payload, and an inspector
        /// that wants the per-node runs needs the object rather than a sentence about it.
        ///
        /// One slot, not a queue: a second bake of the same city supersedes the first, and a reader that missed a report
        /// two bakes ago has nothing to do with it. Cleared by <see cref="TryTakeReport"/> so a window can tell a fresh
        /// report from one it has already drawn. </summary>
        private static CityBakeReport lastReport;

        private static string lastOwner = string.Empty;
        private static CityBakeBus lastBus;

        /// <summary> Takes the last report and clears the slot. False when there is nothing new to read. </summary>
        public static bool TryTakeReport(out CityBakeReport report, out string owner)
        {
            report = lastReport;
            owner = lastOwner;
            lastReport = null;
            lastBus = null;
            lastOwner = string.Empty;
            return report != null;
        }

        /// <summary> Reads one output from the most recently finished bake owned by <paramref name="owner"/>. The
        /// output remains available until the report is taken, so an inspector can hand the same bake to multiple
        /// scene tools without making the bus a second persistent data store. </summary>
        public static bool TryTakeOutput(string owner, string nodeId, string portName, out object value)
        {
            value = null;
            if (lastBus == null || !string.Equals(owner, lastOwner, StringComparison.Ordinal)) { return false; }
            return lastBus.TryTakeOutput(nodeId, portName, out value);
        }

        /// <summary> Drops any report nobody collected, for a test that wants a clean slate. </summary>
        public static void Forget()
        {
            lastReport = null;
            lastBus = null;
            lastOwner = string.Empty;
        }
        #endregion


        #region "Naming"
        /// <summary> The owner a ticket is filed under: the city's own identity, so two cities bake side by side and one
        /// city cannot be baked twice at once.
        ///
        /// The source asset's GUID when there is one, because that is the identity that survives a rename and a reload.
        /// An unsaved graph has no GUID, so its instance id stands in -- good for one session, which is all an unsaved
        /// graph has. </summary>
        public static string OwnerOf(CityGraph graph, CityBakeRequest request)
        {
            if (request != null && !string.IsNullOrEmpty(request.sourceCityAssetGuid))
            {
                return "city:" + request.sourceCityAssetGuid;
            }

            if (graph == null) { return "city:none"; }
            int h = 0;
            string id = graph.GetEntityId().ToString();
            if (id != null)
            {
                for (int i = 0; i < id.Length; i++) h = h * 31 + id[i];
            }
            return "city:" + h.ToString();
        }

        /// <summary> What Background Tasks calls it. The graph's name, because that is what the author sees in the
        /// project window, with the node named as well for a preview so two entries are told apart. </summary>
        private static string LabelFor(CityGraph graph, string onlyNodeId)
        {
            string cityName = graph == null || string.IsNullOrEmpty(graph.name) ? "city" : graph.name;
            if (string.IsNullOrEmpty(onlyNodeId)) { return "baking " + cityName; }
            return "previewing " + onlyNodeId + " of " + cityName;
        }
        #endregion
    }
}
