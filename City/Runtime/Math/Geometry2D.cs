#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The city layer's shared planar-geometry primitives, worked in the XZ plane (a
    /// Vector2 here is an (x, z) pair). They live in one place so the two callers that need them --
    /// the street-network operator's collision test (RM-09) and the block/lot geometry (RM-10) --
    /// share one crossing test rather than each carrying its own copy, which the no-duplication rule
    /// forbids. Every method is a pure function of its arguments, so nothing here can compromise an
    /// operator's determinism. </summary>
    public static class Geometry2D
    {
        /// <summary> The 2D cross product (z-component of the 3D cross), the orientation primitive the
        /// crossing and area tests are built from. </summary>
        public static float Cross(Vector2 u, Vector2 v)
        {
            return u.x * v.y - u.y * v.x;
        }

        /// <summary> The XZ projection of a world point, dropping height. </summary>
        public static Vector2 XZ(Vector3 v)
        {
            return new Vector2(v.x, v.z);
        }

        /// <summary> True when segment ab strictly crosses segment cd (a proper intersection).
        /// Collinear overlaps and endpoint-only touches return false by design: a proposed street
        /// that merely meets an existing one at a node is a junction, not a collision, and a lot edge
        /// that shares an endpoint with another is an adjacency, not an overlap. </summary>
        public static bool SegmentsProperlyCross(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float d1 = Cross(d - c, a - c);
            float d2 = Cross(d - c, b - c);
            float d3 = Cross(b - a, c - a);
            float d4 = Cross(b - a, d - a);
            return ((d1 > 0f && d2 < 0f) || (d1 < 0f && d2 > 0f))
                && ((d3 > 0f && d4 < 0f) || (d3 < 0f && d4 > 0f));
        }

        /// <summary> Twice the signed area of the XZ polygon (the shoelace sum). Positive means the
        /// vertices wind counter-clockwise, negative clockwise; the magnitude is twice the area. The
        /// sign is how a bounded face is told from the unbounded outer face and how a collapsed inset
        /// is detected. </summary>
        public static float SignedDoubleArea(IList<Vector3> polygon)
        {
            float sum = 0f;
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 p = polygon[i];
                Vector3 q = polygon[(i + 1) % n];
                sum += p.x * q.z - q.x * p.z;
            }
            return sum;
        }

        /// <summary> The area of the XZ polygon: half the absolute signed area. </summary>
        public static float Area(IList<Vector3> polygon)
        {
            return Mathf.Abs(SignedDoubleArea(polygon)) * 0.5f;
        }

        /// <summary> The AREA centroid of the XZ polygon (the shoelace centroid), returned as an (x, z)
        /// pair. Unlike the mean of the vertices, this does not bias toward an edge that carries more
        /// vertices -- which is why RM-22 samples a pad's elevation here rather than at a corner average.
        /// A degenerate (near-zero-area) polygon falls back to the vertex mean, the best point available
        /// when there is no area to weight by. </summary>
        public static Vector2 Centroid(IList<Vector3> polygon)
        {
            int n = polygon.Count;
            float a2 = SignedDoubleArea(polygon);
            if (Mathf.Abs(a2) < 1e-6f)
            {
                Vector2 mean = Vector2.zero;
                for (int i = 0; i < n; i++) { mean += XZ(polygon[i]); }
                return n > 0 ? mean / n : Vector2.zero;
            }
            float cx = 0f, cz = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = XZ(polygon[i]);
                Vector2 q = XZ(polygon[(i + 1) % n]);
                float cross = p.x * q.y - q.x * p.y;
                cx += (p.x + q.x) * cross;
                cz += (p.y + q.y) * cross;
            }
            // a2 is TWICE the signed area, so the centroid's 1/(6A) becomes 1/(3*a2).
            float f = 1f / (3f * a2);
            return new Vector2(cx * f, cz * f);
        }

        /// <summary> True when point <paramref name="p"/> (an XZ pair) is inside the XZ
        /// <paramref name="polygon"/>, by the even-odd ray-crossing rule. A point exactly on an edge is
        /// reported inconsistently by any crossing test, which is acceptable here: the buildable-land
        /// grid samples the interior on a lattice, never the boundary, and the boundary is sampled
        /// separately along the edges (RM-11). Winding is irrelevant, so this works for both the CCW
        /// bounded faces of RM-10 and any hand-built polygon. </summary>
        public static bool Contains(IList<Vector3> polygon, Vector2 p)
        {
            bool inside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = polygon[i].x, zi = polygon[i].z;
                float xj = polygon[j].x, zj = polygon[j].z;
                // Does the horizontal ray at p.y cross edge j->i, and is the crossing to the right of p.x?
                bool crosses = (zi > p.y) != (zj > p.y)
                    && p.x < (xj - xi) * (p.y - zi) / (zj - zi) + xi;
                if (crosses)
                {
                    inside = !inside;
                }
            }
            return inside;
        }

        /// <summary> True when XZ polygons <paramref name="a"/> and <paramref name="b"/> overlap with
        /// positive area: either some edge pair properly crosses, or one polygon contains a vertex of
        /// the other (the containment case a pure edge test misses when one polygon sits wholly inside
        /// the other). Shared, unowned edges alone -- adjacency with no interior overlap -- return
        /// false, which is what the occupancy test wants: a candidate lot abutting a road corridor is
        /// not an overlap, a lot sitting on top of one is. </summary>
        public static bool PolygonsOverlap(IList<Vector3> a, IList<Vector3> b)
        {
            int na = a.Count, nb = b.Count;
            for (int i = 0; i < na; i++)
            {
                Vector2 a0 = XZ(a[i]);
                Vector2 a1 = XZ(a[(i + 1) % na]);
                for (int j = 0; j < nb; j++)
                {
                    Vector2 b0 = XZ(b[j]);
                    Vector2 b1 = XZ(b[(j + 1) % nb]);
                    if (SegmentsProperlyCross(a0, a1, b0, b1))
                    {
                        return true;
                    }
                }
            }
            if (nb > 0 && Contains(a, XZ(b[0])))
            {
                return true;
            }
            if (na > 0 && Contains(b, XZ(a[0])))
            {
                return true;
            }
            return false;
        }

        /// <summary> Sutherland-Hodgman clip of an XZ polygon against a single half-plane: keeps the
        /// part on the LEFT of the directed line through <paramref name="lineP"/> along
        /// <paramref name="lineDir"/> (left = positive <see cref="Cross"/> of the direction with the
        /// point offset). Y is carried through by linear interpolation at each cut so the clipped
        /// polygon keeps real terrain heights. Returns false when nothing survives (the whole polygon
        /// lay on the right). This is the primitive RM-11's slope reshape clips a lot with. </summary>
        public static bool ClipByHalfPlane(IList<Vector3> polygon, Vector2 lineP, Vector2 lineDir,
            List<Vector3> result)
        {
            result.Clear();
            int n = polygon.Count;
            if (n == 0)
            {
                return false;
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 curr = polygon[i];
                Vector3 prev = polygon[(i + n - 1) % n];
                float sideCurr = Cross(lineDir, XZ(curr) - lineP);
                float sidePrev = Cross(lineDir, XZ(prev) - lineP);
                bool currIn = sideCurr >= 0f;
                bool prevIn = sidePrev >= 0f;
                if (currIn != prevIn)
                {
                    // The edge prev->curr straddles the line; add the crossing point, y interpolated.
                    float t = sidePrev / (sidePrev - sideCurr);
                    result.Add(Vector3.LerpUnclamped(prev, curr, t));
                }
                if (currIn)
                {
                    result.Add(curr);
                }
            }
            return result.Count >= 3;
        }

        /// <summary> The intersection of two infinite lines, one through <paramref name="p"/> along
        /// <paramref name="pd"/> and one through <paramref name="q"/> along <paramref name="qd"/>.
        /// Returns false when the lines are parallel (or near-parallel within
        /// <paramref name="epsilon"/>), the case the mitred inset falls back to an offset point for.
        /// This is what turns two inward-offset street lines into the block's new corner. </summary>
        public static bool TryLineIntersection(Vector2 p, Vector2 pd, Vector2 q, Vector2 qd,
            out Vector2 point, float epsilon = 1e-6f)
        {
            float denom = Cross(pd, qd);
            if (Mathf.Abs(denom) < epsilon)
            {
                point = Vector2.zero;
                return false;
            }
            float t = Cross(q - p, qd) / denom;
            point = p + pd * t;
            return true;
        }

        /// <summary> The shortest distance from point <paramref name="p"/> to segment
        /// <paramref name="a"/>-<paramref name="b"/> (all XZ pairs). This is the setback primitive RM-12
        /// measures a building corner's clearance to a lot line with: a zoning setback is the minimum
        /// distance from the structure to the lot boundary, so a corner-to-nearest-edge distance is
        /// exactly the quantity the refuse-and-report check quotes. A degenerate (zero-length) segment
        /// falls back to the point-to-point distance. </summary>
        public static float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float lenSq = ab.sqrMagnitude;
            if (lenSq < 1e-12f)
            {
                return Vector2.Distance(p, a);
            }
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / lenSq);
            Vector2 closest = a + ab * t;
            return Vector2.Distance(p, closest);
        }

        /// <summary> The shortest distance from point <paramref name="p"/> to the closed XZ
        /// <paramref name="polygon"/>'s boundary -- the smallest distance to any of its edges. Used by
        /// RM-12 to measure how far a placed building's corner sits from the nearest lot line, which is
        /// the side-setback clearance. Returns <see cref="float.MaxValue"/> for a degenerate polygon of
        /// fewer than two vertices. </summary>
        public static float DistancePointToPolygonBoundary(IList<Vector3> polygon, Vector2 p)
        {
            int n = polygon.Count;
            if (n < 2)
            {
                return float.MaxValue;
            }
            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                float d = DistancePointToSegment(p, XZ(polygon[i]), XZ(polygon[(i + 1) % n]));
                if (d < best) { best = d; }
            }
            return best;
        }

        /// <summary> Where segment <paramref name="s0"/>-<paramref name="s1"/> meets the infinite line
        /// through <paramref name="lp"/> along <paramref name="ld"/>. Returns the parameter
        /// <paramref name="t"/> along the segment (0 at s0, 1 at s1) and the point; true only when the
        /// segment genuinely crosses the line, i.e. its two ends fall on strictly opposite sides.
        /// An endpoint lying exactly on the line, or a segment parallel to it, returns false so the
        /// lot-splitter never manufactures a degenerate crossing. </summary>
        public static bool TrySegmentLineCrossing(Vector2 s0, Vector2 s1, Vector2 lp, Vector2 ld,
            out float t, out Vector2 point)
        {
            t = 0f;
            point = s0;
            float side0 = Cross(ld, s0 - lp);
            float side1 = Cross(ld, s1 - lp);
            bool opposite = (side0 > 0f && side1 < 0f) || (side0 < 0f && side1 > 0f);
            if (!opposite)
            {
                return false;
            }
            t = side0 / (side0 - side1);
            point = s0 + (s1 - s0) * t;
            return true;
        }
    }
}
