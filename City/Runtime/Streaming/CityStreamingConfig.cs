// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The three representation bands RM-17 defines. Which band a chunk is drawn in is chosen at PLAY
    /// TIME by camera distance -- it selects between forms that were ALL produced at bake time (§ 10.3: no mesh
    /// is simplified on the fly, generation never runs at play time), so a band is a selection, not a
    /// computation. Ordered near-to-far. </summary>
    public enum LodBand
    {
        /// <summary> The player's chunk and its immediate ring: full detail -- individual buildings, furniture,
        /// colliders. The cost that matters here is draw calls and collider count. </summary>
        Near = 0,

        /// <summary> Reduced building detail, furniture culled, colliders off. The cost that matters is draw
        /// calls and whether batching survives the swap. </summary>
        Mid = 1,

        /// <summary> Combined silhouette geometry only, no interiors, no small props. The cost that matters is
        /// triangle count and texture memory. </summary>
        Far = 2
    }


    /// <summary> RM-17's tuning, kept as one AUTHORED block because every value here is a gameplay/performance
    /// decision the author owns rather than a physical constant -- there is no real-world "correct" LOD distance,
    /// only one that measures well on the named device (the gate). Defaults are grounded in real city scale (a
    /// ~100 m chunk is one large block; bands span a plausible view distance) and are marked AUTHORED, not
    /// guessed. This is a gate/selection input, never a generation input, so it does not touch determinism
    /// (C-06): it decides WHAT is shown, never WHAT is baked. </summary>
    [Serializable]
    public sealed class CityStreamingConfig
    {
        /// <summary> AUTHORED. Chunk edge length in metres. The streaming grid is square; a building's chunk is
        /// its world XZ floored by this. ~100 m is one large city block -- the scale the terrain streams at. </summary>
        public float chunkSizeMetres = 100f;

        /// <summary> AUTHORED. Chebyshev ring radius of resident chunks around the camera's chunk. Radius 2 is a
        /// 5x5 = 25-chunk neighbourhood; the player's own chunk is always resident (the "+1"), matching the
        /// 25+1 streaming model. Beyond this radius a chunk is unloaded regardless of band. </summary>
        public int loadRingRadius = 2;

        /// <summary> AUTHORED. Outer distance (m) of the Near band: within this, full detail + colliders. </summary>
        public float nearMaxMetres = 150f;

        /// <summary> AUTHORED. Outer distance (m) of the Mid band: reduced detail, furniture culled. </summary>
        public float midMaxMetres = 400f;

        /// <summary> AUTHORED. Outer distance (m) of the Far band: silhouette only. Past this, or past the load
        /// ring, the chunk is not present. </summary>
        public float farMaxMetres = 1200f;

        /// <summary> AUTHORED. Whether colliders are present per band -- given its own field because colliders
        /// are the item most often left on everywhere and are expensive in a way a triangle count does not show.
        /// Index by <see cref="LodBand"/>: Near on, Mid/Far off by default; which band is a gameplay call. </summary>
        public bool[] collidersEnabledByBand = { true, false, false };

        /// <summary> AUTHORED. Maximum prefab instantiations the loader performs in ONE frame. Streaming a chunk
        /// of a hundred buildings in a single frame is the load-spike hitch RM-17's gate tests for, so the loader
        /// budgets across frames; this is that budget. 8 keeps a boundary crossing under a frame's work. </summary>
        public int instantiationBudgetPerFrame = 8;

        /// <summary> A fresh config with the AUTHORED defaults. </summary>
        public static CityStreamingConfig Default()
        {
            return new CityStreamingConfig();
        }

        /// <summary> True when a chunk at this camera distance is present at all (within the Far band). Past
        /// <see cref="farMaxMetres"/> the chunk is unloaded and has no band. </summary>
        public bool IsLoadedAtDistance(float distanceMetres)
        {
            return distanceMetres <= farMaxMetres;
        }

        /// <summary> The band a chunk is drawn in at this camera distance. Callers first check
        /// <see cref="IsLoadedAtDistance"/>; a distance past the Far band is clamped to Far here so this never
        /// throws, but such a chunk should not be drawn. Boundaries are inclusive of the nearer band. </summary>
        public LodBand BandForDistance(float distanceMetres)
        {
            if (distanceMetres <= nearMaxMetres) { return LodBand.Near; }
            if (distanceMetres <= midMaxMetres) { return LodBand.Mid; }
            return LodBand.Far;
        }

        /// <summary> Whether colliders are present in the given band, per the AUTHORED
        /// <see cref="collidersEnabledByBand"/> table. Out-of-range or a short table reads as "off". </summary>
        public bool CollidersEnabled(LodBand band)
        {
            int i = (int)band;
            return collidersEnabledByBand != null && i >= 0 && i < collidersEnabledByBand.Length
                && collidersEnabledByBand[i];
        }
    }
}
