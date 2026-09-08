// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Why the local-constraint check rejected a proposed segment. The L-system reports
    /// WHICH of the three parts refused it, because a network that silently declines to grow in one
    /// direction is otherwise impossible to debug (RM-09). </summary>
    public enum SegmentRejection
    {
        /// <summary> The segment was accepted; not a rejection. </summary>
        None = 0,

        /// <summary> The endpoint fell outside the generation region. </summary>
        OutOfBounds = 1,

        /// <summary> The gradient between the segment's ends exceeded the road class's
        /// <see cref="RoadClass.maxGradient"/> (terrain sampled through the provider). </summary>
        Terrain = 2,

        /// <summary> The segment crossed or ran too close to an existing edge and could not be
        /// snapped into a junction. </summary>
        Collision = 3,

        /// <summary> The turn against the segment's predecessor was sharper than the class's
        /// <see cref="RoadClass.minCurveRadius"/> permits. </summary>
        Geometry = 4
    }


    /// <summary> A tally of what the street-network operator did, carried out alongside the network
    /// so a caller can see why growth stopped where it did. Pure data; produced by the operator,
    /// never read back into it. </summary>
    public sealed class StreetNetworkReport
    {
        /// <summary> How many segment proposals the L-system considered in total. </summary>
        public int proposed;

        /// <summary> How many it accepted as edges. </summary>
        public int accepted;

        /// <summary> Rejection counts indexed by <see cref="SegmentRejection"/>; entry 0 is unused. </summary>
        public readonly int[] rejectedByReason = new int[5];

        /// <summary> Records one rejection against its reason. </summary>
        public void Reject(SegmentRejection reason)
        {
            rejectedByReason[(int)reason]++;
        }

        /// <summary> A single-line human summary for the log. </summary>
        public override string ToString()
        {
            return "proposed " + proposed + " accepted " + accepted +
                   " rejected[bounds " + rejectedByReason[(int)SegmentRejection.OutOfBounds] +
                   " terrain " + rejectedByReason[(int)SegmentRejection.Terrain] +
                   " collision " + rejectedByReason[(int)SegmentRejection.Collision] +
                   " geometry " + rejectedByReason[(int)SegmentRejection.Geometry] + "]";
        }
    }


    /// <summary> The transient result of the street-network stage: the node and edge lists, plus the
    /// generation report. It is the currency passed from the network operator to the block operator
    /// (RM-10) and the thing written into <see cref="CityData.nodes"/> / <see cref="CityData.edges"/>
    /// before a bake. It is a plain class, not an asset -- the persisted form is <see cref="CityData"/>. </summary>
    public sealed class StreetNetwork
    {
        /// <summary> The network vertices, in insertion order; edges reference these by index. </summary>
        public readonly List<NetworkNode> nodes = new List<NetworkNode>();

        /// <summary> The network segments. </summary>
        public readonly List<NetworkEdge> edges = new List<NetworkEdge>();

        /// <summary> What the generator did to build this network. </summary>
        public readonly StreetNetworkReport report = new StreetNetworkReport();

        /// <summary> Adds a node at <paramref name="position"/>, or reuses the nearest existing node
        /// within <paramref name="snapDistance"/> instead of creating a near-duplicate. Returns the
        /// index of the node used. The scan is linear and its tie-break is the lowest index, so the
        /// result is deterministic and independent of any spatial structure -- the property RM-09's
        /// determinism gate depends on. </summary>
        public int AddNodeSnapped(Vector3 position, float snapDistance, out bool reused)
        {
            int best = -1;
            float bestSqr = snapDistance * snapDistance;

            for (int index = 0; index < nodes.Count; index++)
            {
                float sqr = (nodes[index].position - position).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    // Strictly-less keeps the first (lowest-index) node on a tie, so the choice never
                    // depends on visitation order.
                    if (best == -1 || sqr < bestSqr)
                    {
                        best = index;
                        bestSqr = sqr;
                    }
                }
            }

            if (best != -1)
            {
                reused = true;
                return best;
            }

            reused = false;
            nodes.Add(new NetworkNode { position = position });
            return nodes.Count - 1;
        }

        /// <summary> Adds an edge between two node indices unless one already joins that unordered
        /// pair, or the two indices are equal. Returns true when an edge was added. Deduplication is
        /// what lets the snapping above turn two streets that meet into a genuine junction rather than
        /// a pair of coincident segments. </summary>
        public bool AddEdge(int a, int b, int roadClassId, bool isDivided, float medianWidth)
        {
            if (a == b)
            {
                return false;
            }

            for (int index = 0; index < edges.Count; index++)
            {
                NetworkEdge existing = edges[index];
                if ((existing.a == a && existing.b == b) || (existing.a == b && existing.b == a))
                {
                    return false;
                }
            }

            edges.Add(new NetworkEdge
            {
                a = a,
                b = b,
                roadClassId = roadClassId,
                isDivided = isDivided,
                medianWidth = medianWidth
            });
            return true;
        }
    }
}
