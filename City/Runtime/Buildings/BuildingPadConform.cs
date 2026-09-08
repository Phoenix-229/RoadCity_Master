// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-22: computes a building PAD as a REQUEST, never a terrain edit. It runs after a lot
    /// has passed RM-11's buildable test and before RM-12 places the prefab, and it takes two inputs --
    /// the footprint polygon from the lot record and terrain sampled through
    /// <see cref="IPCGDataProvider"/> -- returning a <see cref="PadRecord"/> per lot. Universe Creator
    /// owns the terrain and § 10.8 is one-way, so this layer must NOT poke heightmap values: the pad is
    /// handed to § 4's conform channel by RM-14, which is the only thing that edits ground. Poking
    /// heightmaps here would fight chunk streaming, the regenerate path and undo all at once.
    ///
    /// Determinism (C-06) is the whole game. Every apron is measured against NATURAL terrain, never
    /// against an already-graded neighbour, so no result depends on the order pads are processed. Where
    /// two aprons overlap the pads are merged into a GROUP that shares ONE elevation; the grouping is
    /// connected-components over apron overlap -- an order-independent relation -- labelled by the
    /// smallest member lot id. Apron width, freeboard and the group-elevation statistic are AUTHORED
    /// (Q-12), carried on <see cref="PadConformParams"/> rather than invented. The slope test itself is
    /// RM-11's: it has already run, so RM-22 reuses its verdict and does not re-measure slope here. </summary>
    public static class BuildingPadConform
    {
        /// <summary> Computes every lot's pad and merges overlapping aprons into shared-elevation
        /// groups. The returned list is ordered by lot id, and both the per-pad values and the grouping
        /// are independent of the order <paramref name="requests"/> arrives in (C-06). </summary>
        public static List<PadRecord> ComputePads(
            IReadOnlyList<PadRequest> requests, IPCGDataProvider terrain, PadConformParams p)
        {
            // Stable processing order by lot id, so the per-pad work and the grouping are both fixed
            // regardless of the order the caller supplied the lots in.
            List<PadRequest> ordered = new List<PadRequest>(requests);
            ordered.Sort((x, y) => x.lotId.CompareTo(y.lotId));

            int n = ordered.Count;
            PadRecord[] pads = new PadRecord[n];
            for (int i = 0; i < n; i++)
            {
                pads[i] = ComputeSingle(ordered[i], terrain, p);
            }

            GroupAndShareElevation(pads, p);
            return new List<PadRecord>(pads);
        }

        /// <summary> Computes one lot's pad before any grouping: its elevation by the chosen rule, its
        /// footprint flattened to that elevation, and its apron ring at natural terrain height. </summary>
        private static PadRecord ComputeSingle(PadRequest req, IPCGDataProvider terrain, PadConformParams p)
        {
            float elevation = ElevationOf(req, terrain, p);
            List<Vector3> footprint = FlattenTo(req.footprint, elevation);
            List<Vector3> apronRing = ApronRing(req.footprint, p.apronWidthMetres, terrain);
            return new PadRecord(req.lotId, elevation, footprint, apronRing,
                req.elevationRule, p.blendCurve, req.groundContactMode);
        }

        /// <summary> The pad elevation. Default is ONE sample at the footprint's area centroid (not a
        /// corner average, which biases an L-shape toward its busier wing). The alternate rule is
        /// min(corner heights) + freeboard, so a graded site sheds water outward. </summary>
        private static float ElevationOf(PadRequest req, IPCGDataProvider terrain, PadConformParams p)
        {
            IList<Vector3> fp = req.footprint;
            if (req.elevationRule == PadElevationRule.MinCornerPlusFreeboard)
            {
                float lo = float.PositiveInfinity;
                for (int i = 0; i < fp.Count; i++)
                {
                    lo = Mathf.Min(lo, terrain.SampleHeight(fp[i].x, fp[i].z));
                }
                return lo + p.freeboardMetres;
            }
            Vector2 c = Geometry2D.Centroid(fp);
            return terrain.SampleHeight(c.x, c.y);
        }

        /// <summary> The outer apron boundary: the footprint offset OUTWARD by the apron width, each
        /// vertex then dropped to natural terrain height. It reuses <see cref="PolygonInset"/>'s one
        /// mitred-offset routine with a NEGATIVE inset (so the left normal of a CCW polygon pushes each
        /// edge outward) rather than carrying a second copy of the corner maths (no-duplication). A
        /// degenerate footprint that the offset rejects falls back to the footprint itself. </summary>
        private static List<Vector3> ApronRing(IList<Vector3> footprint, float apronWidth, IPCGDataProvider terrain)
        {
            List<Vector3> ccw = new List<Vector3>(footprint);
            if (Geometry2D.SignedDoubleArea(ccw) < 0f) { ccw.Reverse(); }

            float[] offset = new float[ccw.Count];
            for (int i = 0; i < offset.Length; i++) { offset[i] = -apronWidth; }

            List<Vector3> ring;
            if (!PolygonInset.TryInset(ccw, offset, out ring))
            {
                ring = ccw;
            }
            for (int i = 0; i < ring.Count; i++)
            {
                Vector3 v = ring[i];
                ring[i] = new Vector3(v.x, terrain.SampleHeight(v.x, v.z), v.z);
            }
            return ring;
        }

        /// <summary> Merges pads whose apron rings overlap into groups and gives every member of a group
        /// the SAME group id (its smallest member lot id) and the SAME elevation. Grouping is
        /// connected-components via union-find over the overlap relation, which does not depend on the
        /// order pairs are visited, and each group's shared values are written independently -- so the
        /// final array is identical whatever order the components are enumerated in (C-06). </summary>
        private static void GroupAndShareElevation(PadRecord[] pads, PadConformParams p)
        {
            int n = pads.Length;
            int[] parent = new int[n];
            for (int i = 0; i < n; i++) { parent[i] = i; }
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Geometry2D.PolygonsOverlap(pads[i].apronRing, pads[j].apronRing))
                    {
                        Union(parent, i, j);
                    }
                }
            }

            Dictionary<int, List<int>> components = new Dictionary<int, List<int>>();
            for (int i = 0; i < n; i++)
            {
                int root = Find(parent, i);
                if (!components.TryGetValue(root, out List<int> list))
                {
                    list = new List<int>();
                    components[root] = list;
                }
                list.Add(i);
            }

            foreach (List<int> group in components.Values)
            {
                int groupId = int.MaxValue;
                for (int k = 0; k < group.Count; k++)
                {
                    groupId = Mathf.Min(groupId, pads[group[k]].lotId);
                }
                float elevation = GroupElevation(pads, group, p);
                for (int k = 0; k < group.Count; k++)
                {
                    PadRecord pad = pads[group[k]];
                    pad.padGroupId = groupId;
                    pad.padElevation = elevation;
                    SetY(pad.footprint, elevation);
                }
            }
        }

        /// <summary> A group's single shared elevation, by the AUTHORED
        /// <see cref="PadGroupElevationPolicy"/>. A lone pad keeps its own elevation. The spec fixes
        /// THAT a group shares one elevation but not WHICH statistic picks it, so this is a policy, not
        /// an invented formula. </summary>
        private static float GroupElevation(PadRecord[] pads, List<int> group, PadConformParams p)
        {
            if (group.Count == 1) { return pads[group[0]].padElevation; }
            switch (p.groupElevationPolicy)
            {
                case PadGroupElevationPolicy.Highest:
                {
                    float hi = float.NegativeInfinity;
                    for (int k = 0; k < group.Count; k++) { hi = Mathf.Max(hi, pads[group[k]].padElevation); }
                    return hi;
                }
                case PadGroupElevationPolicy.Lowest:
                {
                    float lo = float.PositiveInfinity;
                    for (int k = 0; k < group.Count; k++) { lo = Mathf.Min(lo, pads[group[k]].padElevation); }
                    return lo;
                }
                case PadGroupElevationPolicy.Mean:
                {
                    float sum = 0f;
                    for (int k = 0; k < group.Count; k++) { sum += pads[group[k]].padElevation; }
                    return sum / group.Count;
                }
                default: // AreaWeightedMean
                {
                    float wsum = 0f, esum = 0f;
                    for (int k = 0; k < group.Count; k++)
                    {
                        float w = Geometry2D.Area(pads[group[k]].footprint);
                        wsum += w;
                        esum += w * pads[group[k]].padElevation;
                    }
                    return wsum > 1e-6f ? esum / wsum : pads[group[0]].padElevation;
                }
            }
        }

        /// <summary> The weight of NATURAL terrain across the apron: 0 at the footprint edge (all pad),
        /// rising to 1 at the apron ring (all terrain), by the chosen curve. Both curves are MONOTONIC
        /// non-decreasing on [0,1], so the graded surface never overshoots into a lip or a trench. The
        /// solver (RM-14) and every gate call THIS function, so there is one blend, not two. </summary>
        public static float NaturalWeight01(float t01, PadBlendCurve curve)
        {
            float t = Mathf.Clamp01(t01);
            if (curve == PadBlendCurve.Smoothstep)
            {
                return t * t * (3f - 2f * t);
            }
            return t; // Linear
        }

        /// <summary> The apron height at fraction <paramref name="t01"/> of the way from the footprint
        /// edge (pad elevation) to the apron ring (natural terrain). </summary>
        public static float ApronHeight(float padElevation, float naturalHeight, float t01, PadBlendCurve curve)
        {
            return Mathf.LerpUnclamped(padElevation, naturalHeight, NaturalWeight01(t01, curve));
        }

        /// <summary> A copy of <paramref name="polygon"/> with every vertex's Y set to <paramref name="y"/>. </summary>
        private static List<Vector3> FlattenTo(IList<Vector3> polygon, float y)
        {
            List<Vector3> flat = new List<Vector3>(polygon.Count);
            for (int i = 0; i < polygon.Count; i++)
            {
                flat.Add(new Vector3(polygon[i].x, y, polygon[i].z));
            }
            return flat;
        }

        /// <summary> Sets every vertex's Y in place (used to re-seat a footprint at its group's shared
        /// elevation after merging). </summary>
        private static void SetY(List<Vector3> polygon, float y)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 v = polygon[i];
                polygon[i] = new Vector3(v.x, y, v.z);
            }
        }

        /// <summary> Union-find root with path halving. </summary>
        private static int Find(int[] parent, int i)
        {
            while (parent[i] != i)
            {
                parent[i] = parent[parent[i]];
                i = parent[i];
            }
            return i;
        }

        /// <summary> Unions two sets, keeping the smaller root so labels stay stable. </summary>
        private static void Union(int[] parent, int a, int b)
        {
            int ra = Find(parent, a);
            int rb = Find(parent, b);
            if (ra != rb)
            {
                parent[Mathf.Max(ra, rb)] = Mathf.Min(ra, rb);
            }
        }
    }
}
