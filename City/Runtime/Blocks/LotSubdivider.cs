#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-10, part three: cuts an inset block into buildable lots by the Parish and Müller
    /// (SIGGRAPH 2001) recursive subdivision -- split the polygon along the long axis of its oriented
    /// bounding box, recurse until each part is under a target area, and never emit a lot without
    /// street frontage.
    ///
    /// Two project-specific rules are enforced rather than left to emerge (RM-10):
    ///   * FRONTAGE IS A REQUIREMENT. A split that would strand a child with no edge on the block
    ///     boundary (its only frontage) is refused, and the parent is kept whole instead -- the
    ///     "subdivide differently" branch. So every lot returned touches a street.
    ///   * TARGET AREA IS A PARAMETER, not a constant baked into the code, so a commercial core can
    ///     subdivide coarsely and a suburb finely from one code path. Until the zoning taxonomy exists
    ///     (RM-11 / RM-42) it is one authored value on the operator; later it becomes per-zone.
    ///
    /// Determinism is threaded explicitly: the split ratio is drawn from the per-face seed through a
    /// counter advanced in a fixed depth-first order, never from a shared unseeded random consumed in
    /// whatever order faces happen to be visited. Same block, same seed, same lots. </summary>
    public static class LotSubdivider
    {
        /// <summary> Deepest recursion allowed, a guard so a pathological block cannot recurse without
        /// bound; a real block reaches its target area long before this. </summary>
        private const int MaxDepth = 16;

        /// <summary> Subdivides <paramref name="blockPolygon"/> into lot polygons, each under
        /// <paramref name="targetArea"/> where the frontage rule allows, seeded from
        /// <paramref name="faceSeed"/>. Returns one polygon per lot, in a deterministic order. </summary>
        public static List<List<Vector3>> Subdivide(IList<Vector3> blockPolygon, float targetArea,
            float splitJitter, int faceSeed)
        {
            List<List<Vector3>> lots = new List<List<Vector3>>();
            if (blockPolygon.Count < 3)
            {
                return lots;
            }

            List<Vector3> root = new List<Vector3>(blockPolygon);
            int counter = 0;
            Recurse(root, blockPolygon, targetArea, splitJitter, faceSeed, ref counter, 0, lots);
            return lots;
        }

        /// <summary> Splits <paramref name="part"/> along its OBB long axis and recurses, or emits it
        /// as a lot when it is small enough, too deep, cannot be cleanly split, or when splitting it
        /// would landlock a child. Every part that reaches this method has already been checked to
        /// have frontage, so every emitted lot does too. </summary>
        private static void Recurse(List<Vector3> part, IList<Vector3> boundary, float targetArea,
            float splitJitter, int faceSeed, ref int counter, int depth, List<List<Vector3>> lots)
        {
            if (depth >= MaxDepth || Geometry2D.Area(part) <= targetArea)
            {
                lots.Add(part);
                return;
            }

            float jitter = Mathf.Clamp01(splitJitter);
            float ratio = Mathf.Clamp(0.5f + (Rand01(faceSeed, counter++) * 2f - 1f) * jitter, 0.2f, 0.8f);

            Vector2 cutPoint, cutDir;
            LongAxisCut(part, ratio, out cutPoint, out cutDir);

            List<Vector3> a, b;
            if (!TrySplit(part, cutPoint, cutDir, out a, out b))
            {
                lots.Add(part);
                return;
            }

            // Frontage rule: refuse a split that would strand a child with no street edge.
            if (!HasFrontage(a, boundary) || !HasFrontage(b, boundary))
            {
                lots.Add(part);
                return;
            }

            Recurse(a, boundary, targetArea, splitJitter, faceSeed, ref counter, depth + 1, lots);
            Recurse(b, boundary, targetArea, splitJitter, faceSeed, ref counter, depth + 1, lots);
        }

        /// <summary> Finds the part's oriented bounding box -- the minimum-area box aligned to one of
        /// its edges, the standard rotating-calipers result -- and returns the cut for splitting its
        /// LONG axis: a line perpendicular to that axis, positioned at <paramref name="ratio"/> along
        /// it. Splitting the long axis is what keeps lots from degenerating into slivers. </summary>
        private static void LongAxisCut(List<Vector3> part, float ratio, out Vector2 cutPoint, out Vector2 cutDir)
        {
            int n = part.Count;
            float bestArea = float.MaxValue;
            Vector2 bestU = new Vector2(1f, 0f);

            for (int i = 0; i < n; i++)
            {
                Vector2 u = (Geometry2D.XZ(part[(i + 1) % n]) - Geometry2D.XZ(part[i])).normalized;
                if (u.sqrMagnitude < 1e-8f)
                {
                    continue;
                }
                Vector2 v = new Vector2(-u.y, u.x);
                float minU, maxU, minV, maxV;
                Extents(part, u, out minU, out maxU);
                Extents(part, v, out minV, out maxV);
                float area = (maxU - minU) * (maxV - minV);
                if (area < bestArea)
                {
                    bestArea = area;
                    bestU = u;
                }
            }

            Vector2 axisU = bestU;
            Vector2 axisV = new Vector2(-axisU.y, axisU.x);
            float loU, hiU, loV, hiV;
            Extents(part, axisU, out loU, out hiU);
            Extents(part, axisV, out loV, out hiV);
            Vector2 centre = axisU * ((loU + hiU) * 0.5f) + axisV * ((loV + hiV) * 0.5f);

            if ((hiU - loU) >= (hiV - loV))
            {
                cutPoint = centre + axisU * ((ratio - 0.5f) * (hiU - loU));
                cutDir = axisV; // cut line runs across the short axis, dividing the long one
            }
            else
            {
                cutPoint = centre + axisV * ((ratio - 0.5f) * (hiV - loV));
                cutDir = axisU;
            }
        }

        /// <summary> The min and max projections of the part's XZ vertices onto a unit axis. </summary>
        private static void Extents(List<Vector3> part, Vector2 axis, out float min, out float max)
        {
            min = float.MaxValue;
            max = float.MinValue;
            for (int i = 0; i < part.Count; i++)
            {
                float d = Vector2.Dot(Geometry2D.XZ(part[i]), axis);
                if (d < min) { min = d; }
                if (d > max) { max = d; }
            }
        }

        /// <summary> Splits the polygon by the infinite line through <paramref name="lp"/> along
        /// <paramref name="ld"/>, returning the two halves. Succeeds only when the line crosses the
        /// boundary exactly twice -- a clean chord. A concave polygon a single line would cut into more
        /// than two pieces returns false, and the caller keeps that part whole (the honest limit of the
        /// author-chosen approach); the split point's height is interpolated so lots stay on the block
        /// surface. </summary>
        private static bool TrySplit(List<Vector3> poly, Vector2 lp, Vector2 ld,
            out List<Vector3> a, out List<Vector3> b)
        {
            a = null;
            b = null;
            int n = poly.Count;
            List<int> crossEdge = new List<int>(2);
            List<Vector3> crossPt = new List<Vector3>(2);
            for (int i = 0; i < n; i++)
            {
                Vector3 v0 = poly[i];
                Vector3 v1 = poly[(i + 1) % n];
                float t;
                Vector2 pt;
                if (Geometry2D.TrySegmentLineCrossing(Geometry2D.XZ(v0), Geometry2D.XZ(v1), lp, ld, out t, out pt))
                {
                    crossEdge.Add(i);
                    crossPt.Add(new Vector3(pt.x, Mathf.Lerp(v0.y, v1.y, t), pt.y));
                }
            }
            if (crossEdge.Count != 2)
            {
                return false;
            }

            int e0 = crossEdge[0];
            int e1 = crossEdge[1];
            Vector3 x0 = crossPt[0];
            Vector3 x1 = crossPt[1];

            a = new List<Vector3> { x0 };
            for (int i = e0 + 1; i <= e1; i++)
            {
                a.Add(poly[i]);
            }
            a.Add(x1);

            b = new List<Vector3> { x1 };
            for (int i = e1 + 1; i <= e0 + n; i++)
            {
                b.Add(poly[i % n]);
            }
            b.Add(x0);

            return Geometry2D.Area(a) > 1e-2f && Geometry2D.Area(b) > 1e-2f;
        }

        /// <summary> True when any edge of <paramref name="part"/> lies along an edge of the block
        /// <paramref name="boundary"/> -- the definition of street frontage, since every edge of the
        /// inset block borders a street. Cut edges introduced by subdivision lie in the interior and do
        /// not match, so a landlocked part has no frontage. </summary>
        private static bool HasFrontage(List<Vector3> part, IList<Vector3> boundary)
        {
            int bn = boundary.Count;
            for (int i = 0; i < part.Count; i++)
            {
                Vector2 p = Geometry2D.XZ(part[i]);
                Vector2 q = Geometry2D.XZ(part[(i + 1) % part.Count]);
                for (int j = 0; j < bn; j++)
                {
                    Vector2 r = Geometry2D.XZ(boundary[j]);
                    Vector2 s = Geometry2D.XZ(boundary[(j + 1) % bn]);
                    if (EdgeLiesOn(p, q, r, s))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary> True when segment p-q is collinear with segment r-s (both ends within 5 cm of the
        /// r-s line) and their overlap along that line exceeds half a metre -- i.e. p-q runs ALONG
        /// r-s, not merely touching it at a point. </summary>
        private static bool EdgeLiesOn(Vector2 p, Vector2 q, Vector2 r, Vector2 s)
        {
            Vector2 dir = s - r;
            float len = dir.magnitude;
            if (len < 1e-4f)
            {
                return false;
            }
            Vector2 n = dir / len;
            if (Mathf.Abs(Geometry2D.Cross(n, p - r)) > 0.05f || Mathf.Abs(Geometry2D.Cross(n, q - r)) > 0.05f)
            {
                return false;
            }
            float tp = Vector2.Dot(p - r, n);
            float tq = Vector2.Dot(q - r, n);
            float lo = Mathf.Max(0f, Mathf.Min(tp, tq));
            float hi = Mathf.Min(len, Mathf.Max(tp, tq));
            return hi - lo > 0.5f;
        }

        /// <summary> A deterministic float in [0, 1) from the face seed and a counter key, via the
        /// shared <see cref="CitySeed"/> mixer -- the same primitive the network operator uses, so no
        /// unseeded randomness enters the subdivision. </summary>
        private static float Rand01(int seed, int key)
        {
            return (uint)CitySeed.ForItem(seed, key) / (float)uint.MaxValue;
        }
    }
}
