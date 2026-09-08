// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The runtime input to the street-network operator: the world region to fill and the
    /// terrain to consult while filling it.
    ///
    /// The terrain provider is passed IN here rather than fetched from the scene inside the operator,
    /// and that distinction is what keeps the operator a pure function of its declared input as
    /// section 10.4 requires. An operator that reached out to <c>FindObjectOfType</c> for the linker
    /// would be reading global state; an operator handed the provider as data is not -- re-running it
    /// with the same input (the same provider over the same terrain), the same serialised parameters,
    /// and the same seed yields byte-identical output, which is exactly RM-09's determinism gate.
    ///
    /// This is deliberately NOT a <c>[Serializable]</c> asset field: it is per-run context, not a
    /// tuning parameter, so it lives on the method input and never on the operator asset. </summary>
    public sealed class StreetNetworkInput
    {
        /// <summary> The terrain the constraint check samples for height and slope, via
        /// <see cref="IPCGDataProvider.SampleHeight"/> and <see cref="IPCGDataProvider.SampleNormal"/>.
        /// This is the read direction of the linker (Road &amp; City Master reads terrain); the
        /// operator never writes terrain. </summary>
        public IPCGDataProvider terrain;

        /// <summary> The world-space region, in the XZ plane and in metres, the network is grown to
        /// fill. A proposed node outside it is rejected with <see cref="SegmentRejection.OutOfBounds"/>. </summary>
        public Rect regionXZ;

        /// <summary> Constructs the input. </summary>
        /// <param name="terrain"> The terrain provider; may be null only in a test that grows on a
        /// flat implied surface. </param>
        /// <param name="regionXZ"> The XZ region to fill. </param>
        public StreetNetworkInput(IPCGDataProvider terrain, Rect regionXZ)
        {
            this.terrain = terrain;
            this.regionXZ = regionXZ;
        }
    }
}
