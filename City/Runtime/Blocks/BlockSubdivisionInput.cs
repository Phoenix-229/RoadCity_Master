// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The runtime input to the block-subdivision operator (RM-10): the street network whose
    /// closed faces become blocks.
    ///
    /// The network is passed IN here rather than fetched from a scene or an asset, for the same reason
    /// the terrain provider is on <see cref="StreetNetworkInput"/>: it keeps the operator a pure
    /// function of its declared input (section 10.4), so re-running it with the same network, the same
    /// serialised parameters, and the same seed yields a byte-identical set of blocks and lots -- the
    /// front half of RM-10's determinism gate. The road-class catalogue the inset distances read is an
    /// authored asset field on the operator, not per-run context, so it lives there and not here. </summary>
    public sealed class BlockSubdivisionInput
    {
        /// <summary> The street network to subdivide. Its <see cref="StreetNetwork.nodes"/> and
        /// <see cref="StreetNetwork.edges"/> are read as a planar graph; its report is ignored. </summary>
        public readonly StreetNetwork network;

        /// <summary> Constructs the input around a generated (or loaded) street network. </summary>
        public BlockSubdivisionInput(StreetNetwork network)
        {
            this.network = network;
        }
    }
}
