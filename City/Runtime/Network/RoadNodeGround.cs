#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Where the ground is under a road node, and the smallest lift that keeps a node clear of it.
    ///
    /// RM-94: four sites in <c>RoadAutomation</c> and <c>Construction</c> guarded node placement with
    /// <c>if (position.y &lt; 0.03f) position.y = 0.03f;</c> against the ABSOLUTE world Y. The 3 cm is right -- it is
    /// the same centimetre-scale depth bias the carriageway already uses for markings and overlays, and a depth buffer
    /// needs one. The frame of reference was wrong. World Y = 0 is not sea level and not the ground: it is wherever
    /// the scene's origin happens to sit. In a valley, a cutting, a tunnel approach, or any scene whose terrain lies
    /// below the origin, that guard teleported every node up to 3 cm above the origin -- nowhere near the ground
    /// there -- and made a road below sea level impossible to author.
    ///
    /// The bias is now measured from the surface under the node, so it does its job at any elevation. Where there is
    /// no terrain under a node there is also nothing to z-fight with, so nothing is moved: no floor is invented from
    /// the origin. </summary>
    public static class RoadNodeGround
    {
        /// <summary> The depth bias, in metres. Matches the 1-3 cm lifts Road.cs gives markings and overlays: enough
        /// to win the depth test at any sane view distance, small enough to be invisible. </summary>
        public const float RenderBiasMetres = 0.03f;

        /// <summary> The lowest world Y a node at this position may sit at. Negative infinity where no terrain covers
        /// the position, which reads as "no floor applies" at every call site below. </summary>
        public static float MinimumY(Vector3 worldPosition)
        {
            Terrain terrain = TerrainUnder(worldPosition);
            if (terrain == null) { return float.NegativeInfinity; }
            return SurfaceY(terrain, worldPosition) + RenderBiasMetres;
        }

        /// <summary> The position, lifted to clear the ground under it if it does not already. A position above the
        /// bias is returned untouched: this raises nodes off the surface, it does not snap them to it. </summary>
        public static Vector3 Lift(Vector3 worldPosition)
        {
            float floor = MinimumY(worldPosition);
            if (worldPosition.y < floor) { worldPosition.y = floor; }
            return worldPosition;
        }

        /// <summary> World-space surface height at this position on this terrain. <c>Terrain.SampleHeight</c> reports
        /// the height relative to the terrain object, so the object's own Y has to be added back -- forgetting it is
        /// the same class of mistake this type exists to fix. </summary>
        public static float SurfaceY(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.transform.position.y + terrain.SampleHeight(worldPosition);
        }

        /// <summary> The active terrain whose XZ footprint contains this position, or null. Only X and Z are tested:
        /// a node under a terrain is still on it, and a terrain's Y extent says nothing about where its surface is.
        /// The first match wins, which is the right answer whenever terrains tile without overlapping -- and when
        /// they do overlap, no rule here can be better than arbitrary. </summary>
        public static Terrain TerrainUnder(Vector3 worldPosition)
        {
            Terrain[] all = Terrain.activeTerrains;
            if (all == null) { return null; }
            for (int i = 0; i < all.Length; i++)
            {
                Terrain terrain = all[i];
                if (terrain == null || terrain.terrainData == null) { continue; }
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;
                if (worldPosition.x < origin.x || worldPosition.x > origin.x + size.x) { continue; }
                if (worldPosition.z < origin.z || worldPosition.z > origin.z + size.z) { continue; }
                return terrain;
            }
            return null;
        }
    }
}
