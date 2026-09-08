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
    /// <summary> RM-16's per-element edit path, kept PURE so the scene handles, the inspector and the gate
    /// all mutate a <see cref="CityData"/> through ONE set of operations rather than each writing its own --
    /// the no-duplication rule again, and here it also guarantees the load-bearing invariant is applied
    /// identically everywhere: an author edit sets <see cref="Block.isOverridden"/> /
    /// <see cref="Lot.isOverridden"/>, so RM-20's re-bake (via <see cref="CityData.ClearDerived"/>) PRESERVES
    /// the hand-correction instead of silently recomputing it. Reverting clears the flag so the next re-bake
    /// recomputes the element from the network and the seed.
    ///
    /// These operations only touch the data; the editor caller is responsible for the surrounding
    /// <c>Undo.RecordObject</c>/<c>EditorUtility.SetDirty</c> that the spec's snippet shows (that belongs to
    /// the Editor assembly, not to this runtime type). </summary>
    public static class CityEditOps
    {
        /// <summary> The block with this <see cref="Block.id"/>, or null. </summary>
        public static Block FindBlock(CityData data, int blockId)
        {
            if (data == null) { return null; }
            for (int i = 0; i < data.blocks.Count; i++)
            {
                if (data.blocks[i] != null && data.blocks[i].id == blockId) { return data.blocks[i]; }
            }
            return null;
        }

        /// <summary> The lot with this <see cref="Lot.id"/>, or null. </summary>
        public static Lot FindLot(CityData data, int lotId)
        {
            if (data == null) { return null; }
            for (int i = 0; i < data.lots.Count; i++)
            {
                if (data.lots[i] != null && data.lots[i].id == lotId) { return data.lots[i]; }
            }
            return null;
        }

        /// <summary> Whether the block or lot the selection points at is currently an author override. A
        /// node or corner reports the state of its owning element (the lot, for a corner). </summary>
        public static bool IsOverridden(CityData data, CitySelection sel)
        {
            switch (sel.kind)
            {
                case CityElementKind.Block:
                {
                    Block b = FindBlock(data, sel.primaryId);
                    return b != null && b.isOverridden;
                }
                case CityElementKind.Lot:
                case CityElementKind.LotCorner:
                {
                    Lot l = FindLot(data, sel.primaryId);
                    return l != null && l.isOverridden;
                }
                default:
                    return false;
            }
        }

        /// <summary> Mark the selected block or lot as an author edit so a re-bake preserves it. A
        /// <see cref="CityElementKind.LotCorner"/> marks its owning lot. Returns false when the selection
        /// has no overridable target (a network node is graph input, never derived, so it is neither
        /// overridable nor recomputed). </summary>
        public static bool MarkOverridden(CityData data, CitySelection sel)
        {
            return SetOverride(data, sel, true);
        }

        /// <summary> Clear the override on the selected block or lot so the next re-bake recomputes it from
        /// the network and the seed. Returns false when there is no overridable target. </summary>
        public static bool Revert(CityData data, CitySelection sel)
        {
            return SetOverride(data, sel, false);
        }

        private static bool SetOverride(CityData data, CitySelection sel, bool value)
        {
            switch (sel.kind)
            {
                case CityElementKind.Block:
                {
                    Block b = FindBlock(data, sel.primaryId);
                    if (b == null) { return false; }
                    b.isOverridden = value;
                    return true;
                }
                case CityElementKind.Lot:
                case CityElementKind.LotCorner:
                {
                    Lot l = FindLot(data, sel.primaryId);
                    if (l == null) { return false; }
                    l.isOverridden = value;
                    return true;
                }
                default:
                    return false;
            }
        }

        /// <summary> Move one lot corner to a new XZ position (keeping its existing height) and mark the lot
        /// overridden -- the exact edit a scene handle drag performs, in one place so the handle code and the
        /// gate share it. Returns false when the selection is not a valid corner of an existing lot. </summary>
        public static bool MoveLotCorner(CityData data, CitySelection sel, Vector2 newXZ)
        {
            if (sel.kind != CityElementKind.LotCorner) { return false; }
            Lot l = FindLot(data, sel.primaryId);
            if (l == null || l.polygon == null) { return false; }
            if (sel.cornerIndex < 0 || sel.cornerIndex >= l.polygon.Count) { return false; }

            Vector3 existing = l.polygon[sel.cornerIndex];
            l.polygon[sel.cornerIndex] = new Vector3(newXZ.x, existing.y, newXZ.y);
            l.isOverridden = true; // an author edit is saved, not re-derived (RM-08)
            return true;
        }
    }
}
