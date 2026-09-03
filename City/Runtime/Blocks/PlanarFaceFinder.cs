#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-10, part one: finds the closed faces of the street network -- the city blocks --
    /// by the classic planar-subdivision face traversal, not a raster flood fill.
    ///
    /// The network is a planar graph. Each undirected edge is split into two directed half-edges; at
    /// every node the outgoing half-edges are sorted by angle; and a face is traced by repeatedly, on
    /// arriving at a node, taking the next edge CLOCKWISE from the reverse of the one just arrived on.
    /// Each such closed walk is one face. Bounded faces come out wound counter-clockwise (positive
    /// signed area); the single unbounded outer face comes out clockwise (negative) and is discarded.
    ///
    /// This is textbook computational geometry rather than an invention (section 10 forbids inventing
    /// one), and it is deterministic: half-edges are numbered from the edge list in index order, the
    /// angular sort breaks ties by half-edge id, and the walk visits half-edges in id order -- so the
    /// same network always yields the same faces in the same order. Preserving the block's true
    /// polygon (rather than a rasterised approximation) is what lets RM-10's inset and every downstream
    /// setback work from real geometry instead of a guess. </summary>
    public static class PlanarFaceFinder
    {
        /// <summary> Finds the bounded faces of <paramref name="net"/>, each returned as the ordered
        /// node indices of its closed boundary, wound counter-clockwise. Dangling spurs and the
        /// unbounded outer face contribute no bounded loop and are omitted. </summary>
        public static List<List<int>> FindFaces(StreetNetwork net)
        {
            List<List<int>> faces = new List<List<int>>();
            int edgeCount = net.edges.Count;
            if (edgeCount == 0 || net.nodes.Count == 0)
            {
                return faces;
            }

            int halfCount = edgeCount * 2;

            // Outgoing half-edges per node, each sorted CCW by direction angle (id breaks ties), and a
            // reverse map from a half-edge to its slot in its origin's sorted list.
            List<int>[] outgoing = BuildSortedOutgoing(net, halfCount);
            int[] slot = new int[halfCount];
            for (int node = 0; node < outgoing.Length; node++)
            {
                for (int i = 0; i < outgoing[node].Count; i++)
                {
                    slot[outgoing[node][i]] = i;
                }
            }

            bool[] visited = new bool[halfCount];
            for (int start = 0; start < halfCount; start++)
            {
                if (visited[start])
                {
                    continue;
                }

                List<int> loop = TraceFace(net, outgoing, slot, visited, start, halfCount);
                if (loop.Count >= 3)
                {
                    List<Vector3> pts = new List<Vector3>(loop.Count);
                    for (int i = 0; i < loop.Count; i++)
                    {
                        pts.Add(net.nodes[loop[i]].position);
                    }
                    // Bounded faces wind CCW (positive); the outer face and degenerate spurs do not.
                    if (Geometry2D.SignedDoubleArea(pts) > 1e-3f)
                    {
                        faces.Add(loop);
                    }
                }
            }

            return faces;
        }

        /// <summary> Origin node of a half-edge: even ids run a-&gt;b, odd ids run b-&gt;a. </summary>
        private static int Origin(StreetNetwork net, int half)
        {
            NetworkEdge e = net.edges[half >> 1];
            return (half & 1) == 0 ? e.a : e.b;
        }

        /// <summary> Destination node of a half-edge -- the origin of its twin. </summary>
        private static int Dest(StreetNetwork net, int half)
        {
            NetworkEdge e = net.edges[half >> 1];
            return (half & 1) == 0 ? e.b : e.a;
        }

        /// <summary> Builds each node's list of outgoing half-edges, sorted counter-clockwise by the
        /// direction angle of the edge, with the half-edge id as a deterministic tie-break for two
        /// edges that leave at the same angle. </summary>
        private static List<int>[] BuildSortedOutgoing(StreetNetwork net, int halfCount)
        {
            int nodeCount = net.nodes.Count;
            List<int>[] outgoing = new List<int>[nodeCount];
            for (int n = 0; n < nodeCount; n++)
            {
                outgoing[n] = new List<int>();
            }
            for (int half = 0; half < halfCount; half++)
            {
                outgoing[Origin(net, half)].Add(half);
            }

            for (int n = 0; n < nodeCount; n++)
            {
                List<int> list = outgoing[n];
                Vector3 origin = net.nodes[n].position;
                list.Sort((h1, h2) =>
                {
                    float a1 = Angle(origin, net.nodes[Dest(net, h1)].position);
                    float a2 = Angle(origin, net.nodes[Dest(net, h2)].position);
                    int byAngle = a1.CompareTo(a2);
                    return byAngle != 0 ? byAngle : h1.CompareTo(h2);
                });
            }
            return outgoing;
        }

        /// <summary> The XZ direction angle from <paramref name="from"/> to <paramref name="to"/>. </summary>
        private static float Angle(Vector3 from, Vector3 to)
        {
            return Mathf.Atan2(to.z - from.z, to.x - from.x);
        }

        /// <summary> Traces one face from <paramref name="start"/>, following, at each node reached,
        /// the outgoing edge one step CLOCKWISE from the reverse of the edge just travelled -- the
        /// entry immediately before the twin in the CCW-sorted list. Marks every half-edge it uses
        /// visited and returns the ordered origin nodes. The iteration cap is a guard against a
        /// malformed adjacency; a correct planar graph closes the walk well within it. </summary>
        private static List<int> TraceFace(StreetNetwork net, List<int>[] outgoing, int[] slot,
            bool[] visited, int start, int halfCount)
        {
            List<int> loop = new List<int>();
            int cur = start;
            int guard = halfCount + 4;
            while (guard-- > 0)
            {
                visited[cur] = true;
                loop.Add(Origin(net, cur));

                int v = Dest(net, cur);
                int twin = cur ^ 1;
                List<int> ring = outgoing[v];
                int pos = slot[twin];
                int nextPos = (pos - 1 + ring.Count) % ring.Count;
                cur = ring[nextPos];

                if (cur == start)
                {
                    break;
                }
            }
            return loop;
        }
    }
}
