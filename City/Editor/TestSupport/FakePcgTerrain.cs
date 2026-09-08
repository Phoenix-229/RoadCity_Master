// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> A deterministic stand-in for the terrain linker used by the City editor tests: a
    /// gentle analytic surface, so an operator that samples terrain has heights and normals without a
    /// scene or a baked <see cref="UnityEngine.Terrain"/>. Every method is a pure function of its
    /// arguments, which is what lets the tests that consume it be deterministic (C-06).
    ///
    /// It lives here, shared, rather than as a private class inside one test, so RM-09's network test
    /// and RM-10's block test drive the operators over the SAME implied surface -- and so the
    /// no-duplication rule is honoured: there is one fake terrain, not one per test. </summary>
    public sealed class FakePcgTerrain : IPCGDataProvider
    {
        /// <summary> A smooth analytic height: a faint overall tilt plus two low-frequency ripples, so
        /// slope varies across the region without any cliff a gradient check would trivially reject. </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            return 0.01f * worldX
                 + 2f * Mathf.Sin(worldX * 0.003f)
                 + 2f * Mathf.Cos(worldZ * 0.003f);
        }

        /// <summary> The surface normal from finite differences of <see cref="SampleHeight"/>, so it is
        /// exactly consistent with the height field the same provider reports. </summary>
        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            float dx = SampleHeight(worldX + 1f, worldZ) - SampleHeight(worldX - 1f, worldZ);
            float dz = SampleHeight(worldX, worldZ + 1f) - SampleHeight(worldX, worldZ - 1f);
            return new Vector3(-dx * 0.5f, 1f, -dz * 0.5f).normalized;
        }

        /// <summary> One generous chunk covering the region the tests grow networks in. </summary>
        public List<Bounds> GetActiveChunkBounds()
        {
            return new List<Bounds> { new Bounds(Vector3.zero, new Vector3(3200f, 1000f, 3200f)) };
        }

        /// <summary> The write-side linker calls are inert here: the tests never cut terrain. </summary>
        public void NotifyCutRegion(Bounds worldBounds) { }
        public int RequestTunnelOpening(Bounds worldBounds) { return 0; }
        public void ReleaseTunnelOpening(int handle) { }
    }
}
#endif
