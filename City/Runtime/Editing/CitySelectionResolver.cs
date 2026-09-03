#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-16's hierarchical selection, kept as a PURE deterministic function so it can be proven
    /// without a scene: the scene-view handle code (which uses Unity's <c>Handles</c> API) and the city
    /// window both call this rather than each carrying their own hit-test, which the no-duplication rule
    /// forbids.
    ///
    /// The rule the spec states is "click-to-select-the-broadest, click-again-to-descend, with the current
    /// level shown". This class encodes exactly that: at a click point it builds the ordered list of
    /// candidates present under the cursor -- Block, then Lot, then the nearest Lot corner, then the nearest
    /// network node -- and if the current selection is already one of them it advances to the NEXT (cycling
    /// back to the broadest at the end); otherwise it snaps to the broadest. All picking is worked in the XZ
    /// plane through the shared <see cref="Geometry2D"/> primitives; ties are broken by lowest id/index so
    /// the result never depends on list order after a re-bake (C-06). </summary>
    public static class CitySelectionResolver
    {
        /// <summary> Resolve one click into the next selection. <paramref name="clickXZ"/> is the cursor's
        /// world position projected to the XZ plane; <paramref name="pickRadius"/> is how close (in metres)
        /// a click must fall to a corner or node to pick it. <paramref name="current"/> is the selection
        /// before this click, which decides whether the click descends the hierarchy or resets it. </summary>
        public static CitySelection ResolveClick(CityData data, Vector2 clickXZ, CitySelection current,
            float pickRadius)
        {
            List<CitySelection> candidates = new List<CitySelection>();
            if (data == null) { return CitySelection.None; }

            // Broadest to narrowest, then the graph node behind the geometry.
            CitySelection block = HitBlock(data, clickXZ);
            if (block.kind != CityElementKind.None) { candidates.Add(block); }

            CitySelection lot = HitLot(data, clickXZ);
            if (lot.kind != CityElementKind.None) { candidates.Add(lot); }

            CitySelection corner = HitLotCorner(data, clickXZ, pickRadius);
            if (corner.kind != CityElementKind.None) { candidates.Add(corner); }

            CitySelection node = HitNode(data, clickXZ, pickRadius);
            if (node.kind != CityElementKind.None) { candidates.Add(node); }

            if (candidates.Count == 0) { return CitySelection.None; }

            // If the current selection is one of the candidates, descend to the next (cycling); else reset
            // to the broadest. This is the whole "click-to-broadest, click-again-to-descend" behaviour.
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Equals(current))
                {
                    return candidates[(i + 1) % candidates.Count];
                }
            }
            return candidates[0];
        }

        /// <summary> The block whose inset polygon contains the point, choosing the lowest
        /// <see cref="Block.id"/> when several overlap so the result is order-independent. </summary>
        public static CitySelection HitBlock(CityData data, Vector2 clickXZ)
        {
            Block best = null;
            for (int i = 0; i < data.blocks.Count; i++)
            {
                Block b = data.blocks[i];
                if (b == null || b.polygon == null || b.polygon.Count < 3) { continue; }
                if (Geometry2D.Contains(b.polygon, clickXZ) && (best == null || b.id < best.id))
                {
                    best = b;
                }
            }
            return best == null ? CitySelection.None : CitySelection.ForBlock(best.id);
        }

        /// <summary> The lot whose polygon contains the point, lowest <see cref="Lot.id"/> on overlap. </summary>
        public static CitySelection HitLot(CityData data, Vector2 clickXZ)
        {
            Lot best = null;
            for (int i = 0; i < data.lots.Count; i++)
            {
                Lot l = data.lots[i];
                if (l == null || l.polygon == null || l.polygon.Count < 3) { continue; }
                if (Geometry2D.Contains(l.polygon, clickXZ) && (best == null || l.id < best.id))
                {
                    best = l;
                }
            }
            return best == null ? CitySelection.None : CitySelection.ForLot(best.id);
        }

        /// <summary> The nearest lot corner within <paramref name="pickRadius"/> of the point. Ties are
        /// broken by lowest lot id, then lowest corner index, so the pick is deterministic. </summary>
        public static CitySelection HitLotCorner(CityData data, Vector2 clickXZ, float pickRadius)
        {
            int bestLotId = 0;
            int bestCorner = -1;
            float bestDist = pickRadius;
            bool found = false;
            for (int i = 0; i < data.lots.Count; i++)
            {
                Lot l = data.lots[i];
                if (l == null || l.polygon == null) { continue; }
                for (int c = 0; c < l.polygon.Count; c++)
                {
                    float d = Vector2.Distance(clickXZ, Geometry2D.XZ(l.polygon[c]));
                    // strict < keeps the earlier (lower id / lower index) candidate on a tie
                    if (d < bestDist)
                    {
                        bestDist = d;
                        bestLotId = l.id;
                        bestCorner = c;
                        found = true;
                    }
                }
            }
            return found ? CitySelection.ForLotCorner(bestLotId, bestCorner) : CitySelection.None;
        }

        /// <summary> The nearest network node within <paramref name="pickRadius"/> of the point, by its
        /// index into <see cref="CityData.nodes"/>; the lowest index wins a tie. </summary>
        public static CitySelection HitNode(CityData data, Vector2 clickXZ, float pickRadius)
        {
            int bestIndex = -1;
            float bestDist = pickRadius;
            for (int i = 0; i < data.nodes.Count; i++)
            {
                float d = Vector2.Distance(clickXZ, Geometry2D.XZ(data.nodes[i].position));
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIndex = i;
                }
            }
            return bestIndex < 0 ? CitySelection.None : CitySelection.ForNode(bestIndex);
        }
    }
}
