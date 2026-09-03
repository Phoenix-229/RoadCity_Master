#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Connected mode: the provider owns the terrain, so the tool REQUESTS rather than writes.
    ///
    /// The write methods below are therefore not stubs waiting to be filled in -- they are the contract. § 10.24 says
    /// that with a provider reachable "every modification of terrain ... is *requested* through the provider, and the
    /// provider owns the terrain", and the reason this matters is reversibility: the provider knows the natural
    /// heightfield, so deleting a road can restore real ground instead of replaying a copy the tool kept. A tool that
    /// wrote the heightfield here would take that away. Every suppressed write is counted, so "connected mode wrote
    /// nothing" is a number the gate can read rather than a claim.
    ///
    /// The provider surface (§ 10.8) grants one kind of hold of its own -- a tunnel opening. A cut region and a
    /// building pad are expressed with <c>NotifyCutRegion</c> over the claimed region and tracked here, because that
    /// is what the surface offers; a dedicated pad call would be a provider-side addition, which no RM task may make
    /// (R-5). </summary>
    public sealed class ProviderTerrainSink : ITerrainWriteSink, ITerrainSurfaceReader
    {
        private readonly IPCGDataProvider provider;
        private readonly string providerName;
        private int suppressedWrites;
        private int notifyCount;
        private int nextLocalHandle;
        private TerrainRegion lastNotified = TerrainRegion.Empty;
        private readonly List<int> tunnelHandles = new List<int>();

        public ProviderTerrainSink(IPCGDataProvider provider, string providerName)
        {
            this.provider = provider;
            this.providerName = string.IsNullOrEmpty(providerName) ? "terrain provider" : providerName;
        }

        public string SinkName { get { return providerName; } }

        public TerrainWritePath Path { get { return TerrainWritePath.RequestedThroughProvider; } }

        /// <summary> Whether the provider is still there to answer.
        ///
        /// This matters more than it looks. The gateway is reached through a static, and a static outlives a scene: a
        /// provider component that gets destroyed -- scene unloaded, play mode exited, object deleted -- would leave a
        /// gateway still in connected mode, suppressing every write and forwarding them to nothing. Terrain editing
        /// would simply stop working, with no error, which is the worst possible failure. Unity's <c>==</c> on
        /// <see cref="UnityEngine.Object"/> reports a destroyed object as null, so asking is cheap and the gateway
        /// falls back to its own direct path instead. A provider that is not a Unity object cannot be destroyed and is
        /// therefore always alive. </summary>
        public bool ProviderAlive
        {
            get
            {
                if (provider == null) { return false; }
                if (!(provider is UnityEngine.Object)) { return true; }
                return (UnityEngine.Object)provider != null;
            }
        }

        /// <summary> Writes the tool did NOT perform because the provider owns the terrain. </summary>
        public int SuppressedWriteCount { get { return suppressedWrites; } }

        /// <summary> How many times the provider was told a region changed. One build must add exactly one. </summary>
        public int NotifyCount { get { return notifyCount; } }

        /// <summary> The last region the provider was told about. </summary>
        public TerrainRegion LastNotifiedRegion { get { return lastNotified; } }

        /// <summary> Tunnel openings the provider is holding for this tool, by ITS handle. </summary>
        public int OpenTunnelCount { get { return tunnelHandles.Count; } }

        #region "Writes -- deliberately not performed"
        public void WriteHeights(Terrain terrain, float[,] heights) { suppressedWrites++; }

        public void WriteDetailLayer(Terrain terrain, int layerIndex, int[,] density) { suppressedWrites++; }

        public void WriteTrees(Terrain terrain, TreeInstance[] trees) { suppressedWrites++; }

        public void WriteSplat(Terrain terrain, int x, int z, float[,,] alphamaps) { suppressedWrites++; }

        public void WriteHoles(Terrain terrain, int x, int z, bool[,] holes) { suppressedWrites++; }

        /// <summary> RM-98/RM-99. The provider owns the terrain, so it owns the flush and the collider too: settling
        /// here would be this side reaching into ground it does not write, which is exactly the two-owners mistake that
        /// makes the hole come back after being fixed once. The call is counted instead, because the dual-mode test
        /// compares the two modes' settle counts -- they must match even though only one of them touches the engine.
        /// </summary>
        public void Settle(Terrain terrain, bool holesChanged)
        {
            settleCount++;
            if (holesChanged) { holeSettleCount++; }
        }

        private int settleCount;
        private int holeSettleCount;

        /// <summary> How many times a tile was settled -- see <see cref="Settle"/> for why the count exists when the
        /// call does nothing. </summary>
        public int SettleCount { get { return settleCount; } }

        /// <summary> How many of those settles concerned a hole change. </summary>
        public int HoleSettleCount { get { return holeSettleCount; } }
        #endregion

        #region "Notification and holds"
        /// <summary> The call that keeps the two systems consistent: the provider is told WHICH region changed, once,
        /// with the union the gateway accumulated, so chunk rebuilds, foliage removal, water re-evaluation and its own
        /// bakes can react. </summary>
        public void NotifyRegionChanged(TerrainRegion region)
        {
            lastNotified = region;
            notifyCount++;
            if (provider != null && region.hasArea) { provider.NotifyCutRegion(region.ToBounds()); }
        }

        public int RequestHold(TerrainRequestKind kind, TerrainRegion region)
        {
            if (provider == null || !region.hasArea) { return -1; }

            if (kind == TerrainRequestKind.TunnelOpening)
            {
                int handle = provider.RequestTunnelOpening(region.ToBounds());
                tunnelHandles.Add(handle);
                return handle;
            }

            // A cut region and a pad are both "the tool is claiming this ground". They are NOT announced here: the
            // gateway folds the claimed region into the build's union and announces it once at the end, which is the
            // whole reason a hundred-node road does not make a hundred round trips.
            nextLocalHandle++;
            return nextLocalHandle;
        }

        public void ReleaseHold(TerrainRequestKind kind, int providerHandle)
        {
            if (provider == null) { return; }
            if (kind == TerrainRequestKind.TunnelOpening)
            {
                provider.ReleaseTunnelOpening(providerHandle);
                tunnelHandles.Remove(providerHandle);
                return;
            }
            // Giving a claimed region back is itself a change, and it is announced the same way it was claimed: the
            // gateway unions the released region into the pending union, so the next end-of-build notification covers
            // the ground the provider now owns again.
        }
        #endregion

        #region "Reading the surface"
        public float SampleHeight(float worldX, float worldZ)
        {
            return provider != null ? provider.SampleHeight(worldX, worldZ) : 0f;
        }

        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            return provider != null ? provider.SampleNormal(worldX, worldZ) : Vector3.up;
        }

        public List<Bounds> GetActiveChunkBounds()
        {
            return provider != null ? provider.GetActiveChunkBounds() : new List<Bounds>();
        }
        #endregion
    }
}
