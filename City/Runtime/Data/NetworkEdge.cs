// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One street segment of the network graph: a connection between two
    /// <see cref="NetworkNode"/>s, carrying the road-class decision for that segment.
    ///
    /// SAVED, in full. Like a node, an edge is graph input, never a derived result, so a re-bake
    /// leaves it untouched. Its endpoints are stored as indices into <see cref="CityData.nodes"/>,
    /// never as object references -- see the "indices, not references" note on
    /// <see cref="CityData"/>. </summary>
    [Serializable]
    public struct NetworkEdge
    {
        /// <summary> SAVED. Index into <see cref="CityData.nodes"/> of the first endpoint. </summary>
        public int a;

        /// <summary> SAVED. Index into <see cref="CityData.nodes"/> of the second endpoint. </summary>
        public int b;

        /// <summary> SAVED. Which road class this segment is. The class -- not this record -- owns
        /// the segment's geometry limits (width, grade, radius); this is only the reference to it. </summary>
        public int roadClassId;

        /// <summary> SAVED. Whether the segment is a divided road (RM-59). </summary>
        public bool isDivided;

        /// <summary> SAVED. Median width for a divided road (RM-60). Ignored when
        /// <see cref="isDivided"/> is false; kept rather than nulled so the value survives a
        /// divided-then-undivided-then-divided edit without being lost. </summary>
        public float medianWidth;
    }
}
