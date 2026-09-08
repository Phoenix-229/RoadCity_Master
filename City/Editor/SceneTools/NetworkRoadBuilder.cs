// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using RoadCityMaster;
using RoadCityMaster.Roads;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-09's realizer: turns a generated <see cref="StreetNetwork"/> into REAL
    /// <see cref="RoadCityMaster.Road"/> objects authored through the existing road tool, never a
    /// parallel mesh system. This is the reuse rule made concrete -- the city layer decides WHERE
    /// roads go; the road tool decides how they look and how they mesh.
    ///
    /// It is deliberately EDITOR code, not an <see cref="ICityOperator"/>: it touches the scene
    /// (creates a <see cref="RoadSystem"/> and its roads), and operators are forbidden scene access.
    /// The pure network generation happens in <see cref="StreetNetworkOperator"/>; this only draws
    /// its result. </summary>
    public static class NetworkRoadBuilder
    {
        /// <summary> Realises every edge of <paramref name="network"/> as roads under a new
        /// <see cref="RoadSystem"/>. Edges are grouped into STROKES -- maximal same-class paths through
        /// degree-2 nodes -- so a straight run of segments becomes one multi-node road rather than a
        /// string of stubs, exactly as an author would draw it. Returns the created system, or null
        /// when there is nothing to build. </summary>
        public static RoadSystem Build(StreetNetwork network, RoadClassCatalog catalog, string systemName = "Generated Street Network")
        {
            if (network == null || catalog == null || network.edges.Count == 0)
            {
                return null;
            }

            RoadSystem system = new GameObject(systemName).AddComponent<RoadSystem>();

            // Gate updates off so no road re-meshes mid-build; the golden template does the same.
            system.isAllowingRoadUpdates = false;

            List<List<int>> strokes = ExtractStrokes(network);
            for (int s = 0; s < strokes.Count; s++)
            {
                RealizeStroke(system, network, catalog, strokes[s]);
            }

            // Two passes: the tool settles spline geometry on the first and meshes on the second.
            system.isAllowingRoadUpdates = true;
            system.UpdateAllRoads();
            system.UpdateAllRoads();
            return system;
        }

        /// <summary> Builds one road from a stroke's ordered node indices and maps its road class onto
        /// the tool's per-road fields. The class's divided/median data stays on the network -- the road
        /// tool has no median field -- so only the width, lane, and grade model is projected here. </summary>
        private static void RealizeStroke(RoadSystem system, StreetNetwork network, RoadClassCatalog catalog, List<int> strokeNodes)
        {
            if (strokeNodes.Count < 2)
            {
                return;
            }

            List<Vector3> positions = new List<Vector3>(strokeNodes.Count);
            for (int i = 0; i < strokeNodes.Count; i++)
            {
                positions.Add(network.nodes[strokeNodes[i]].position);
            }

            Road road = RoadAutomation.CreateRoadProgrammatically(system, ref positions);
            if (road == null)
            {
                return;
            }

            RoadClass rc = catalog.Get(StrokeClassId(network, strokeNodes));
            if (rc != null)
            {
                road.laneWidth = rc.laneWidth;
                // The class carries a directionless lane TOTAL, which is the number a class is described by; the road
                // stores the two directions separately, so the total is split on the way in rather than assigned raw.
                road.SetLaneTotal(rc.laneAmount);
                // A divided class carries its median width across too; it used to be dropped, which left a divided
                // class building an undivided section. The divider KIND is left as the author set it -- a class saying
                // "divided" says the carriageways are separated, not that a steel rail stands between them, and a
                // planted median has no barrier at all.
                road.medianWidth = rc.isDivided ? rc.medianWidth : 0f;
                road.isMaxGradeEnabled = true;
                road.maxGrade = rc.maxGradient;
            }

            // A blocking build inside a generator: never spin up worker threads for this road.
            road.isUsingMultithreading = false;
        }

        /// <summary> The road class of a stroke, read from the edge joining its first two nodes (all
        /// edges in a stroke share a class by construction). </summary>
        private static int StrokeClassId(StreetNetwork network, List<int> strokeNodes)
        {
            int a = strokeNodes[0];
            int b = strokeNodes[1];
            for (int i = 0; i < network.edges.Count; i++)
            {
                NetworkEdge e = network.edges[i];
                if ((e.a == a && e.b == b) || (e.a == b && e.b == a))
                {
                    return e.roadClassId;
                }
            }
            return RoadClassCatalog.LocalId;
        }

        /// <summary> Groups the network's edges into strokes: maximal chains of same-class edges that
        /// pass through degree-2 nodes. Edges are consumed in index order and adjacency is built in
        /// index order, so the grouping -- and thus the roads produced -- is deterministic. </summary>
        private static List<List<int>> ExtractStrokes(StreetNetwork network)
        {
            int nodeCount = network.nodes.Count;
            List<(int edge, int other)>[] adj = new List<(int edge, int other)>[nodeCount];
            for (int n = 0; n < nodeCount; n++)
            {
                adj[n] = new List<(int edge, int other)>();
            }
            for (int e = 0; e < network.edges.Count; e++)
            {
                NetworkEdge edge = network.edges[e];
                adj[edge.a].Add((e, edge.b));
                adj[edge.b].Add((e, edge.a));
            }

            bool[] visited = new bool[network.edges.Count];
            List<List<int>> strokes = new List<List<int>>();
            for (int e = 0; e < network.edges.Count; e++)
            {
                if (visited[e])
                {
                    continue;
                }
                visited[e] = true;
                int a = network.edges[e].a;
                int b = network.edges[e].b;
                int classId = network.edges[e].roadClassId;

                List<int> beyondA = GrowThrough(network, adj, visited, a, e, classId);
                List<int> beyondB = GrowThrough(network, adj, visited, b, e, classId);

                List<int> stroke = new List<int>(beyondA.Count + beyondB.Count + 2);
                for (int i = beyondA.Count - 1; i >= 0; i--)
                {
                    stroke.Add(beyondA[i]);
                }
                stroke.Add(a);
                stroke.Add(b);
                stroke.AddRange(beyondB);
                strokes.Add(stroke);
            }
            return strokes;
        }

        /// <summary> Walks away from a seed edge through <paramref name="throughNode"/> while each node
        /// it reaches is degree-2 and its onward edge shares the class, returning the ordered nodes and
        /// marking their edges visited. Stops at any junction, class change, or already-taken edge. </summary>
        private static List<int> GrowThrough(StreetNetwork network, List<(int edge, int other)>[] adj, bool[] visited, int throughNode, int viaEdge, int classId)
        {
            List<int> result = new List<int>();
            while (adj[throughNode].Count == 2)
            {
                int nextEdge = adj[throughNode][0].edge != viaEdge ? adj[throughNode][0].edge : adj[throughNode][1].edge;
                if (visited[nextEdge] || network.edges[nextEdge].roadClassId != classId)
                {
                    break;
                }
                NetworkEdge ne = network.edges[nextEdge];
                int nextNode = ne.a == throughNode ? ne.b : ne.a;
                visited[nextEdge] = true;
                result.Add(nextNode);
                viaEdge = nextEdge;
                throughNode = nextNode;
            }
            return result;
        }

    }
}
