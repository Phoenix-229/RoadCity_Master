// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
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
    /// the origin.
    ///
    /// B-17 made the floor two-directional. A node's authored depth is part of the floor, so a cutting is a legal
    /// request rather than something the clamp undoes; and the two conversions the terrain write needs -- the tile's
    /// own base plane, and world metres to a normalised heightmap sample -- live here beside it, because both of them
    /// went wrong in the terraform in exactly the way RM-94 went wrong here. </summary>
    public static class RoadNodeGround
    {
        /// <summary> The depth bias, in metres. Matches the 1-3 cm lifts Road.cs gives markings and overlays: enough
        /// to win the depth test at any sane view distance, small enough to be invisible. </summary>
        public const float RenderBiasMetres = 0.03f;

        /// <summary> The lowest world Y a node at this position may sit at. Negative infinity where no terrain covers
        /// the position, which reads as "no floor applies" at every call site below. </summary>
        public static float MinimumY(Vector3 worldPosition)
        {
            return MinimumY(worldPosition, 0f);
        }

        /// <summary> The lowest world Y a node at this position may sit at, given the depth its author ASKED FOR.
        ///
        /// B-17: the one-argument form above is a fill-only floor -- it can only ever push a node up -- so a cutting was
        /// impossible to author through it. The offset closes that. It is a signed height above the surface, so the rule
        /// is one rule and not two: floor = surface + offset + bias. At offset 0 that is exactly the old floor. At -5 m
        /// the floor is five metres INTO the hillside, so a node the author put there stays there and only a node deeper
        /// than they asked for is lifted -- which is the difference between a clamp and a refusal.
        ///
        /// The bias stays in the sum on the cut side too. It is the smallest clearance the depth buffer needs between
        /// the carriageway and the ground, and in a cut the ground is brought DOWN to the road, so after the terraform
        /// the road is 3 cm clear of it again exactly as in a fill. Dropping the bias for negative offsets would buy
        /// three centimetres of depth and cost the one guarantee this type exists to give. </summary>
        public static float MinimumY(Vector3 worldPosition, float authoredOffsetMetres)
        {
            Terrain terrain = TerrainUnder(worldPosition);
            if (terrain == null) { return float.NegativeInfinity; }
            return FloorFromSurface(SurfaceY(terrain, worldPosition), authoredOffsetMetres);
        }

        /// <summary> The floor, from a surface height that has already been found. The one home of the sum, so that a
        /// caller with a better surface than the live one does not have to re-state the rule to use it.
        ///
        /// B-17's third blocker -- the RATCHET -- is exactly such a caller. The live surface under a road that has
        /// already been built is ground the road itself raised, so a floor measured from it sits at the road, and the
        /// node can never be lowered again: each rebuild lifts the floor and nothing ever lowers it. A road that keeps
        /// its own pre-build ground can pass that ground in here instead and get the same rule applied to the right
        /// surface -- see <c>Road.NodeFloorY</c>. </summary>
        public static float FloorFromSurface(float surfaceWorldY, float authoredOffsetMetres)
        {
            if (float.IsNegativeInfinity(surfaceWorldY)) { return float.NegativeInfinity; }
            return surfaceWorldY + authoredOffsetMetres + RenderBiasMetres;
        }

        /// <summary> The position, lifted to clear the ground under it if it does not already. A position above the
        /// bias is returned untouched: this raises nodes off the surface, it does not snap them to it. </summary>
        public static Vector3 Lift(Vector3 worldPosition)
        {
            return Lift(worldPosition, 0f);
        }

        /// <summary> The position, moved no further than the depth its author asked for. Identical to the one-argument
        /// form at offset 0, and the only form a cut can pass through. The four node-creation sites keep calling the
        /// one-argument form: a node that has just been created has no authored depth yet, so its floor is the surface.
        /// </summary>
        public static Vector3 Lift(Vector3 worldPosition, float authoredOffsetMetres)
        {
            return LiftToFloor(worldPosition, MinimumY(worldPosition, authoredOffsetMetres));
        }

        /// <summary> The position, raised to a floor that has already been worked out. The clamp itself, once, so a
        /// caller with its own floor -- a road with its own pre-build ground, say -- does not have to write the
        /// comparison again. A floor of negative infinity leaves the position alone, which is how "no terrain here, so
        /// no floor applies" travels through every call site. </summary>
        public static Vector3 LiftToFloor(Vector3 worldPosition, float floorWorldY)
        {
            if (worldPosition.y < floorWorldY) { worldPosition.y = floorWorldY; }
            return worldPosition;
        }

        /// <summary> A terrain's own base plane, in world metres: the world Y its heightmap value 0 stands for.
        ///
        /// B-17: the terrain write clamped against world Y = 0 instead, which is the RM-94 frame-of-reference mistake
        /// in the other file. A Unity heightmap is normalised 0..1 over the tile's height range and anchored at the
        /// tile's own transform, so a terrain sitting at Y = -200 can represent ground down to -200 and the clamp was
        /// stopping it 200 m early -- or, on a terrain above the origin, letting it write a height it cannot hold.
        /// </summary>
        public static float BaseY(Terrain terrain)
        {
            if (terrain == null) { return 0f; }
            return terrain.transform.position.y;
        }

        /// <summary> A requested ground height, clamped to the lowest the tile can physically represent, and the
        /// shortfall in metres so the refusal can be said out loud rather than absorbed.
        ///
        /// This is the whole of B-17's second blocker. A heightmap cannot go below its own base plane, so where a road
        /// is cut deeper than the tile's range allows the ground genuinely cannot follow it down -- and the author sees
        /// terrain standing above their carriageway with nothing said. The shortfall is what has to be reported: it is
        /// the distance between the ground the cut asked for and the lowest ground there is, which is also exactly how
        /// far the tile has to be lowered, or its height range extended, to grant the cut.
        ///
        /// Pure arithmetic on purpose: the terraform calls it from a worker thread, where touching a Terrain is not
        /// allowed. Pass <see cref="BaseY"/> in from the main thread. </summary>
        public static float ClampToGroundFloor(float requestedWorldY, float terrainBaseWorldY,
            out float shortfallMetres)
        {
            if (requestedWorldY < terrainBaseWorldY)
            {
                shortfallMetres = terrainBaseWorldY - requestedWorldY;
                return terrainBaseWorldY;
            }
            shortfallMetres = 0f;
            return requestedWorldY;
        }

        /// <summary> A world height as the normalised 0..1 sample a Unity heightmap stores.
        ///
        /// B-17: the terraform divided the world height by the tile's range and forgot to take the tile's own base off
        /// first, so on any terrain not sitting at Y = 0 it wrote ground displaced by the tile's own elevation. The
        /// forward conversion in the same file has always added the base back, so the two were inverses of each other
        /// only in the one case where the base is zero. </summary>
        public static float ToHeightmap01(float worldY, float terrainBaseWorldY, float terrainHeightRangeMetres)
        {
            if (terrainHeightRangeMetres <= 0f) { return 0f; }
            float normalised = (worldY - terrainBaseWorldY) / terrainHeightRangeMetres;
            if (normalised < 0f) { normalised = 0f; }
            if (normalised > 1f) { normalised = 1f; }
            return normalised;
        }

        /// <summary> A normalised 0..1 heightmap sample as a world height. The exact inverse of
        /// <see cref="ToHeightmap01"/>, and here rather than written out at each site because the two went out of step
        /// once already: the forward form added the tile's base and the reverse form did not. </summary>
        public static float FromHeightmap01(float normalised, float terrainBaseWorldY,
            float terrainHeightRangeMetres)
        {
            return (normalised * terrainHeightRangeMetres) + terrainBaseWorldY;
        }

        /// <summary> The heightmap cell a world position falls on, in the terraform's own index order: the FIRST index
        /// runs along the terrain's Z axis and the second along X, which is the order <c>TerrainData.GetHeights</c>
        /// returns and the order <c>Terraforming</c> stores.
        ///
        /// NOT clamped, and truncating rather than rounding, because this is the terraform's existing mapping and not
        /// a tidier one: <c>TerraformingThreaded.GetTempHeightsCoordinates</c> has always taken the whole part of
        /// <c>normalised * resolution</c>, and its callers rely on an index at or past the resolution meaning "off this
        /// tile" and bail on it. Rounding here instead would move roughly half of all positions to the neighbouring
        /// cell, and a cell lookup that disagrees with the cell the terraform wrote is worse than no lookup at all --
        /// it would silently miss and fall back. Callers must range-check what they get back.
        ///
        /// B-17: this is the one home of that mapping, so the record the terraform makes can be read back with the
        /// same arithmetic that made it. Pure, no Terrain touched, for the same reason the two conversions above are.
        /// </summary>
        public static void ToHeightmapCell(Vector3 worldPosition, Vector3 terrainBasePosition, Vector3 terrainSize,
            int heightmapResolution, out int firstIndex, out int secondIndex)
        {
            firstIndex = CellIndex(worldPosition.z - terrainBasePosition.z, terrainSize.z, heightmapResolution);
            secondIndex = CellIndex(worldPosition.x - terrainBasePosition.x, terrainSize.x, heightmapResolution);
        }

        private static int CellIndex(float metresAlongAxis, float axisLengthMetres, int heightmapResolution)
        {
            if (axisLengthMetres <= 0f) { return -1; }
            return (int)((metresAlongAxis / axisLengthMetres) * heightmapResolution);
        }

        /// <summary> The sentence a refused cut is reported with. Beside <see cref="NoTerrainMessage"/> for the same
        /// reason: one refusal, one wording, and it names what to change rather than only what went wrong. </summary>
        public static string CutRefusedMessage(string roadName, int points, float worstShortfallMetres)
        {
            return "Road & City Master: the ground could not follow \"" + roadName + "\" all the way down at "
                + points + " point(s). The cut asks for ground up to " + worstShortfallMetres.ToString("0.##")
                + " m below the lowest height the terrain there can hold, so the terrain is left standing above the"
                + " carriageway at those points. Lower the terrain object, or raise its height range (Terrain >"
                + " Resolution > Terrain Height), or raise the road here.";
        }

        /// <summary> Whether any active terrain covers this position. B-13: a node in the open air has no ground to
        /// sample, so every later pass -- grade, terraform, splat, detail, the city siting that reads a road's ends --
        /// would be measuring a surface that is not there. The creation paths refuse rather than inventing a floor at
        /// the origin, which is the class of bug RM-94 removed from this very file. </summary>
        public static bool IsOverTerrain(Vector3 worldPosition)
        {
            return TerrainUnder(worldPosition) != null;
        }

        /// <summary> The one sentence every refusal uses, so four call sites cannot drift into four explanations.
        /// It names the place, because a refusal the author cannot locate is only slightly better than a silent
        /// one. </summary>
        public static string NoTerrainMessage(Vector3 worldPosition)
        {
            return "Road & City Master: no node was placed at ("
                + worldPosition.x.ToString("F1") + ", " + worldPosition.z.ToString("F1")
                + ") because no terrain covers that position. A road node has to sit on ground: the grade, the"
                + " terraform, the splat and detail passes and the city siting all sample the surface under it."
                + " Extend or add a terrain there, then place the node.";
        }

        /// <summary> World-space surface height at this position on this terrain. <c>Terrain.SampleHeight</c> reports
        /// the height relative to the terrain object, so the object's own Y has to be added back -- forgetting it is
        /// the same class of mistake this type exists to fix. </summary>
        public static float SurfaceY(Terrain terrain, Vector3 worldPosition)
        {
            return terrain.transform.position.y + terrain.SampleHeight(worldPosition);
        }

        /// <summary> World-space surface height under this position, or <c>float.NegativeInfinity</c> where no terrain
        /// covers it. B-13's elevation control reads this: the authored figure is a height ABOVE THE GROUND, and the
        /// ground has to be found before it can mean anything. </summary>
        public static float SurfaceYUnder(Vector3 worldPosition)
        {
            Terrain terrain = TerrainUnder(worldPosition);
            if (terrain == null) { return float.NegativeInfinity; }
            return SurfaceY(terrain, worldPosition);
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
