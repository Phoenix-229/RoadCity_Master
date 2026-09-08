// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One closed face of the street network -- a city block -- after the block is inset
    /// for the street reservation. Blocks are cut into <see cref="Lot"/>s (RM-10).
    ///
    /// DERIVED, but SERIALISED. A block's geometry is a deterministic function of the network and
    /// the subdivision parameters, so RM-20's re-bake recomputes it from the graph -- UNLESS
    /// <see cref="isOverridden"/> is set, in which case the re-bake preserves this record verbatim
    /// as an author decision. The geometry is nonetheless written into the saved asset, not left to
    /// be recomputed on load, because section 10.3 requires a baked city to open and enter play mode
    /// with no generator running. So "derived" here means "a re-bake may recompute this", not "this
    /// is absent from disk". <see cref="id"/> is what lets an override reattach to the right block
    /// after the rest of the list has been recomputed. </summary>
    [Serializable]
    public sealed class Block
    {
        /// <summary> Stable identity of this block, deterministic from the network face it encloses
        /// (RM-10 computes it; 0 means unassigned). SAVED with every override so the override can be
        /// matched back to its recomputed block after a re-bake reorders the list. </summary>
        public int id;

        /// <summary> The ordered node indices of the closed face this block was found from, into
        /// <see cref="CityData.nodes"/>. DERIVED from the network by RM-10. </summary>
        public List<int> boundaryNodes = new List<int>();

        /// <summary> The inset, buildable polygon of the block in world space -- the face polygon
        /// pulled in by each bordering street's half-width, footway and setback. DERIVED by RM-10;
        /// when <see cref="isOverridden"/> is set this is the author's hand-edited polygon and is
        /// preserved across a re-bake. </summary>
        public List<Vector3> polygon = new List<Vector3>();

        /// <summary> SAVED. When true this block is an author edit: the re-bake keeps it instead of
        /// recomputing it, and RM-16's inspector shows it as overridden with a revert option.
        /// Without this flag a re-bake silently discards hand-corrections -- the failure the
        /// saved-versus-derived rule exists to prevent. </summary>
        public bool isOverridden;
    }
}
