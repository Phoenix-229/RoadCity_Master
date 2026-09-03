#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One buildable parcel with frontage onto a street: the subdivision of a
    /// <see cref="Block"/> that a building is placed on (RM-10, RM-14).
    ///
    /// DERIVED, but SERIALISED -- exactly as <see cref="Block"/> is, and for the same reasons. A
    /// re-bake recomputes a lot from its block and the subdivision parameters unless
    /// <see cref="isOverridden"/> is set, and the geometry is written to disk so a baked city needs
    /// no generator at play time (section 10.3). <see cref="id"/> reattaches an override after a
    /// re-bake; <see cref="blockId"/> links the lot to its parent block. </summary>
    [Serializable]
    public sealed class Lot
    {
        /// <summary> Stable identity of this lot, deterministic from its parent block and its
        /// position in that block's subdivision (RM-10; 0 means unassigned). SAVED with every
        /// override so it can be matched back after a re-bake. </summary>
        public int id;

        /// <summary> <see cref="Block.id"/> of the block this lot belongs to. DERIVED from the
        /// subdivision. </summary>
        public int blockId;

        /// <summary> The lot polygon in world space. DERIVED by RM-10; when
        /// <see cref="isOverridden"/> is set this is the author's hand-edited polygon and survives a
        /// re-bake. </summary>
        public List<Vector3> polygon = new List<Vector3>();

        /// <summary> Which zoning class the lot carries (RM-11, RM-42; 0 means unzoned). DERIVED
        /// from zoning unless the lot is overridden. </summary>
        public int zoneId;

        /// <summary> SAVED. Marks the lot as an author edit; see <see cref="Block.isOverridden"/>. </summary>
        public bool isOverridden;
    }
}
