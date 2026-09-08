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
    /// <summary> RM-10, part two: insets a block's face polygon inward by each bordering street's
    /// reservation to leave the buildable block. The face polygon runs down the street CENTRELINES, so
    /// the buildable land is that polygon pulled in, per edge, by the street's half-width plus footway
    /// plus zoning setback.
    ///
    /// This is the AUTHOR-CHOSEN "mitred inset with a collapse guard" (2026-08-29), not a full straight
    /// skeleton: each street edge is offset inward along its own inward normal by its own distance, and
    /// consecutive offset lines are intersected to form the new mitred corners -- which is exact for the
    /// convex and mildly-concave blocks a road grid produces. Where the offset would eat the block
    /// (a too-thin block, or a concave neck the naive offset turns into a self-crossing polygon) the
    /// result is REJECTED rather than emitted as garbage: <see cref="TryInset"/> returns false and the
    /// operator records the face as collapsed. A rejected block simply has no buildable land, which is
    /// the honest outcome for a strip too narrow to build on. The stated limitation is that such a neck
    /// is dropped whole rather than split into two blocks, which a straight skeleton would do. </summary>
    public static class PolygonInset
    {
        /// <summary> Insets the counter-clockwise <paramref name="polygon"/> inward, moving the edge
        /// from vertex i to vertex i+1 by <paramref name="insetPerEdge"/>[i] metres along its inward
        /// normal, and returns the mitred result. Returns false -- with <paramref name="result"/> null
        /// -- when the inset collapses the block to zero or negative area or makes it self-intersect.
        /// </summary>
        public static bool TryInset(IList<Vector3> polygon, IList<float> insetPerEdge, out List<Vector3> result)
        {
            result = null;
            int n = polygon.Count;
            if (n < 3 || insetPerEdge.Count != n)
            {
                return false;
            }

            // Each edge's inward-offset line, as a point on it and its (unit) direction.
            Vector2[] offsetPoint = new Vector2[n];
            Vector2[] offsetDir = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Geometry2D.XZ(polygon[i]);
                Vector2 b = Geometry2D.XZ(polygon[(i + 1) % n]);
                Vector2 dir = (b - a).normalized;
                Vector2 inward = new Vector2(-dir.y, dir.x); // left normal of a CCW polygon points in
                offsetPoint[i] = a + inward * insetPerEdge[i];
                offsetDir[i] = dir;
            }

            List<Vector3> inset = new List<Vector3>(n);
            for (int i = 0; i < n; i++)
            {
                int prev = (i - 1 + n) % n;
                Vector2 corner;
                if (!Geometry2D.TryLineIntersection(offsetPoint[prev], offsetDir[prev],
                                                    offsetPoint[i], offsetDir[i], out corner))
                {
                    // Consecutive edges are collinear: the two offset lines coincide, so the corner
                    // is simply vertex i pushed in along the shared normal.
                    corner = offsetPoint[i];
                }
                inset.Add(new Vector3(corner.x, polygon[i].y, corner.y));
            }

            if (Geometry2D.SignedDoubleArea(inset) <= 1e-3f || SelfIntersects(inset))
            {
                return false;
            }

            result = inset;
            return true;
        }

        /// <summary> True when any two non-adjacent edges of the polygon properly cross -- the sign
        /// that a concave neck folded the mitred offset over itself. Adjacent edges share a vertex and
        /// are skipped; the test is O(n squared), which is nothing for a block's handful of edges. </summary>
        private static bool SelfIntersects(List<Vector3> polygon)
        {
            int n = polygon.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 a = Geometry2D.XZ(polygon[i]);
                Vector2 b = Geometry2D.XZ(polygon[(i + 1) % n]);
                for (int j = i + 1; j < n; j++)
                {
                    if (j == i || j == (i + 1) % n || (j + 1) % n == i)
                    {
                        continue; // shares a vertex with edge i
                    }
                    Vector2 c = Geometry2D.XZ(polygon[j]);
                    Vector2 d = Geometry2D.XZ(polygon[(j + 1) % n]);
                    if (Geometry2D.SegmentsProperlyCross(a, b, c, d))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
