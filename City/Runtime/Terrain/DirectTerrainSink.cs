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
    /// <summary> Standalone mode: the tool terraforms the terrain itself, exactly as the vendored tool always has.
    ///
    /// This file is the ONE place in the tool permitted to touch <c>TerrainData</c>, and RM-57's gate is a search that
    /// proves it: every other terrain modification anywhere in the tool goes through
    /// <see cref="TerrainWriteGateway"/>, which reaches the engine only through here. The calls below are deliberately
    /// the same calls the vendored terraforming file made directly before RM-57 -- nothing about the standalone
    /// result changes, only where the call lives (RM-58 keeps that promise). </summary>
    public sealed class DirectTerrainSink : ITerrainWriteSink, ITerrainSurfaceReader
    {
        private int nextHandle;
        private int notifyCount;
        private TerrainRegion lastNotified = TerrainRegion.Empty;

        public string SinkName { get { return "direct"; } }

        public TerrainWritePath Path { get { return TerrainWritePath.DirectWrite; } }

        /// <summary> How many times a changed region was announced. Standalone has nobody to announce to, but the
        /// count and the region are still recorded so the two modes can be compared. </summary>
        public int NotifyCount { get { return notifyCount; } }

        /// <summary> The last region announced, for the report. </summary>
        public TerrainRegion LastNotifiedRegion { get { return lastNotified; } }

        #region "Writes -- the only TerrainData writes in the tool"
        public void WriteHeights(Terrain terrain, float[,] heights)
        {
            if (terrain == null || terrain.terrainData == null || heights == null) { return; }
            terrain.terrainData.SetHeights(0, 0, heights);
        }

        public void WriteDetailLayer(Terrain terrain, int layerIndex, int[,] density)
        {
            if (terrain == null || terrain.terrainData == null || density == null) { return; }
            if (layerIndex < 0 || layerIndex >= terrain.terrainData.detailPrototypes.Length) { return; }
            terrain.terrainData.SetDetailLayer(0, 0, layerIndex, density);
        }

        public void WriteTrees(Terrain terrain, TreeInstance[] trees)
        {
            if (terrain == null || terrain.terrainData == null || trees == null) { return; }
            terrain.terrainData.treeInstances = trees;
        }

        public void WriteSplat(Terrain terrain, int x, int z, float[,,] alphamaps)
        {
            if (terrain == null || terrain.terrainData == null || alphamaps == null) { return; }
            terrain.terrainData.SetAlphamaps(x, z, alphamaps);
        }

        public void WriteHoles(Terrain terrain, int x, int z, bool[,] holes)
        {
            if (terrain == null || terrain.terrainData == null || holes == null) { return; }
            terrain.terrainData.SetHoles(x, z, holes);
        }

        /// <summary> RM-98. The three things that have to happen after a write for physics to match what is drawn.
        ///
        /// The heightmap flush comes first. <c>SyncHeightmap</c> notifies every <see cref="Terrain"/> sharing the data;
        /// its predecessor <c>ApplyDelayedHeightmapModification</c> is obsolete in Unity 6 and calling both is a
        /// deprecation warning for no added effect. Skipping it leaves the chunk drawing its previous surface, which
        /// reads as "the build did nothing".
        ///
        /// The holes texture is flushed separately and only when holes changed, because the collider does not see a
        /// hole edit until that texture is synced -- this is the flag-left-set hole in its purest form: the drawn
        /// surface has an opening, the collider does not, or the reverse.
        ///
        /// The collider's data is then re-assigned. It is already the same object, and that is the point: assigning it
        /// is what makes the physics engine rebuild its shape from the samples that are there now. Without it a
        /// character can fall through ground that draws perfectly. </summary>
        public void Settle(Terrain terrain, bool holesChanged)
        {
            if (terrain == null || terrain.terrainData == null) { return; }
            settleCount++;

            terrain.terrainData.SyncHeightmap();
            if (holesChanged)
            {
                terrain.terrainData.SyncTexture(TerrainData.HolesTextureName);
                holeSettleCount++;
            }

            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider == null) { return; }
            collider.terrainData = terrain.terrainData;
            colliderSettleCount++;
        }
        #endregion

        #region "Settle counters, so a gate can prove the flush happened"
        private int settleCount;
        private int holeSettleCount;
        private int colliderSettleCount;

        /// <summary> How many times a tile was settled. A build that wrote heights and settled nothing is the stale
        /// collider defect, and it is visible here without a scene. </summary>
        public int SettleCount { get { return settleCount; } }

        /// <summary> How many of those settles also flushed the holes texture. </summary>
        public int HoleSettleCount { get { return holeSettleCount; } }

        /// <summary> How many of those settles reached an actual <see cref="TerrainCollider"/>. A tile with no collider
        /// component counts as settled but not as collided, which is a real difference worth being able to see.
        /// </summary>
        public int ColliderSettleCount { get { return colliderSettleCount; } }
        #endregion

        #region "Notification and holds"
        /// <summary> Nothing downstream owns this terrain, so there is nobody to tell -- but the region is recorded,
        /// because "which ground changed" is the same question in both modes and the gate compares the answers.
        /// </summary>
        public void NotifyRegionChanged(TerrainRegion region)
        {
            lastNotified = region;
            notifyCount++;
        }

        /// <summary> Standalone can hold a cut region open (it means nothing more than "the tool is working here"),
        /// but it cannot hold a tunnel opening or a pad against a regeneration that does not exist. Those two are
        /// refused with a negative handle, and the gateway turns that into words. </summary>
        public int RequestHold(TerrainRequestKind kind, TerrainRegion region)
        {
            if (kind != TerrainRequestKind.CutRegion) { return -1; }
            nextHandle++;
            return nextHandle;
        }

        /// <summary> Nothing was held open outside this object, so releasing is bookkeeping only. </summary>
        public void ReleaseHold(TerrainRequestKind kind, int providerHandle)
        {
        }
        #endregion

        #region "Reading the surface"
        private readonly List<Terrain> terrains = new List<Terrain>();
        private readonly List<Vector3> tilePositions = new List<Vector3>();
        private readonly List<Vector3> tileSizes = new List<Vector3>();
        private readonly List<Bounds> activeBounds = new List<Bounds>();
        private int lastTile = -1;

        /// <summary> The terrains this sink samples. Supplied by the caller rather than found here, so sampling stays
        /// a function of what it was given (C-06) and a test can drive it without a scene.
        ///
        /// Each tile's position and size are copied out once, here, because a road samples the ground thousands of
        /// times and <c>terrainData.size</c> is a call across into the engine every time. The world bounds list is
        /// built once for the same reason: <see cref="GetActiveChunkBounds"/> is asked repeatedly during a build and
        /// must not allocate a new list on each call. </summary>
        public void UseTerrains(IList<Terrain> sceneTerrains)
        {
            terrains.Clear();
            tilePositions.Clear();
            tileSizes.Clear();
            activeBounds.Clear();
            lastTile = -1;
            if (sceneTerrains == null) { return; }
            for (int i = 0; i < sceneTerrains.Count; i++)
            {
                Terrain t = sceneTerrains[i];
                if (t == null || t.terrainData == null) { continue; }
                Vector3 position = t.transform.position;
                Vector3 size = t.terrainData.size;
                terrains.Add(t);
                tilePositions.Add(position);
                tileSizes.Add(size);
                activeBounds.Add(TerrainRegion.FromTerrainTile(position, size).ToBounds());
            }
        }

        /// <summary> Which tile a world point sits on, or -1. The tile that answered last is tried first, because
        /// consecutive samples along a road are almost always on the same tile -- that turns a scan over every tile
        /// in the scene into one comparison for the common case. The answer does not depend on the order samples
        /// arrive in: tiles do not overlap, so at most one can contain a point. </summary>
        private int TileAt(float worldX, float worldZ)
        {
            if (lastTile >= 0 && lastTile < terrains.Count && Covers(lastTile, worldX, worldZ)) { return lastTile; }
            for (int i = 0; i < terrains.Count; i++)
            {
                if (!Covers(i, worldX, worldZ)) { continue; }
                lastTile = i;
                return i;
            }
            return -1;
        }

        private bool Covers(int index, float worldX, float worldZ)
        {
            Vector3 p = tilePositions[index];
            Vector3 size = tileSizes[index];
            return worldX >= p.x && worldX <= p.x + size.x && worldZ >= p.z && worldZ <= p.z + size.z;
        }

        public float SampleHeight(float worldX, float worldZ)
        {
            int index = TileAt(worldX, worldZ);
            if (index < 0) { return 0f; }
            return terrains[index].SampleHeight(new Vector3(worldX, 0f, worldZ)) + tilePositions[index].y;
        }

        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            int index = TileAt(worldX, worldZ);
            if (index < 0) { return Vector3.up; }
            Vector3 p = tilePositions[index];
            Vector3 size = tileSizes[index];
            TerrainData data = terrains[index].terrainData;
            if (data == null) { return Vector3.up; }
            return data.GetInterpolatedNormal((worldX - p.x) / size.x, (worldZ - p.z) / size.z);
        }

        /// <summary> Standalone terrain does not stream, so every terrain it was given is "active". The list is the
        /// one built in <see cref="UseTerrains"/>, handed out read-only rather than copied per call. </summary>
        public List<Bounds> GetActiveChunkBounds()
        {
            return activeBounds;
        }
        #endregion
    }
}
