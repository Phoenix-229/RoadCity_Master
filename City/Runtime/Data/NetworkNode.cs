#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One vertex of the street-network graph: a point where roads meet, end, or bend.
    ///
    /// SAVED, in full. A node is part of the generator's input/author graph, never a derived
    /// result, so a re-bake must never move or drop one. Nodes are identified by their position
    /// in <see cref="CityData.nodes"/>; <see cref="NetworkEdge"/> refers to them by that index and
    /// never by object reference, because Unity's serialiser cannot round-trip a shared reference
    /// inside a plain [Serializable] graph and would silently duplicate the node instead. See the
    /// "indices, not references" note on <see cref="CityData"/>. </summary>
    [Serializable]
    public struct NetworkNode
    {
        /// <summary> SAVED. World-space position of the node. The generator seeds it and the author
        /// may move it; either way it is an input to everything downstream, not a product of it. </summary>
        public Vector3 position;
    }
}
