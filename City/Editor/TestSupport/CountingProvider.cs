#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> A host double that records the calls made to it, so "the host was told once, with the union" and "every
    /// tunnel opening was released" are counts rather than claims.
    ///
    /// It lives here, beside <see cref="FakePcgTerrain"/> and <see cref="RampTerrain"/>, because two gates need the same
    /// double: RM-57's gateway gate counts notifications and releases, and RM-98's tunnel-ground gate needs to see WHICH
    /// boxes reached the host in order to prove the hill over a bore was never announced as changed ground. A second copy
    /// of this class in the second gate is exactly the kind of duplication that lets two tests quietly start testing
    /// different things.
    ///
    /// The surface it reports is a plane, not noise, and every reply is a pure function of its arguments (C-06), so a
    /// gate driving it twice gets the same answer twice. </summary>
    public sealed class CountingProvider : IPCGDataProvider
    {
        /// <summary> Every box the host was told had changed, in the order it was told. </summary>
        public readonly List<Bounds> notified = new List<Bounds>();

        /// <summary> Tunnel openings currently held open. Non-empty at the end of a build is a missing release.
        /// </summary>
        public readonly List<int> openTunnels = new List<int>();

        /// <summary> Every box a tunnel opening was requested for, in order -- the sentence that means "open the surface
        /// here and keep it open", as opposed to a notification, which means "this ground changed, heal it". Keeping the
        /// two lists apart is what lets a gate prove the bore was carved and the hill above it left alone. </summary>
        public readonly List<Bounds> tunnelOpenings = new List<Bounds>();

        public int releasedTunnels;
        private int nextTunnelHandle = 100;

        public float SampleHeight(float worldX, float worldZ) { return 0.01f * worldX + 0.02f * worldZ; }

        public Vector3 SampleNormal(float worldX, float worldZ) { return Vector3.up; }

        public List<Bounds> GetActiveChunkBounds()
        {
            List<Bounds> b = new List<Bounds>();
            b.Add(new Bounds(Vector3.zero, new Vector3(4000f, 2000f, 4000f)));
            return b;
        }

        public void NotifyCutRegion(Bounds worldBounds) { notified.Add(worldBounds); }

        public int RequestTunnelOpening(Bounds worldBounds)
        {
            nextTunnelHandle++;
            openTunnels.Add(nextTunnelHandle);
            tunnelOpenings.Add(worldBounds);
            return nextTunnelHandle;
        }

        public void ReleaseTunnelOpening(int handle)
        {
            releasedTunnels++;
            openTunnels.Remove(handle);
        }
    }
}
#endif
