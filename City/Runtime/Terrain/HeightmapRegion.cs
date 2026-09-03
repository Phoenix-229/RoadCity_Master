#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Turning the indices a terraform actually touched into the world region the provider must hear about.
    ///
    /// This exists so the gateway's notification is TIGHT. A terraform writes the whole heightfield array back in one
    /// call, but it only changed the cells along the road corridor, and it knows exactly which ones. Notifying the
    /// whole tile would be the "over-notify and every build rebuilds the world" failure the spec warns about; deriving
    /// the box from the touched cells is both cheaper downstream and more accurate.
    ///
    /// One convention, stated once here rather than guessed at each call site: the engine's height and detail arrays
    /// are indexed <c>[first, second]</c> where the FIRST index advances along world Z and the SECOND along world X.
    /// The vendored code's index lists are named x/y, which is the opposite of what they address, so callers pass them
    /// positionally and this file is the only place the mapping is written down. </summary>
    public static class HeightmapRegion
    {
        /// <summary> The world region covered by a set of touched cells. <paramref name="firstIndices"/> advance along
        /// world Z, <paramref name="secondIndices"/> along world X, and <paramref name="resolution"/> is the array's
        /// side length (so the last index is <c>resolution - 1</c>). The vertical span is taken from the terrain tile,
        /// because a cut changes ground under and over the cells it touches. </summary>
        public static TerrainRegion FromCells(Vector3 terrainPosition, Vector3 terrainSize, int resolution,
            IList<ushort> firstIndices, IList<ushort> secondIndices, int count)
        {
            if (resolution < 2 || count < 1 || firstIndices == null || secondIndices == null)
            {
                return TerrainRegion.Empty;
            }

            int firstMin = int.MaxValue, firstMax = int.MinValue;
            int secondMin = int.MaxValue, secondMax = int.MinValue;
            int taken = 0;
            for (int i = 0; i < count; i++)
            {
                if (i >= firstIndices.Count || i >= secondIndices.Count) { break; }
                Widen(firstIndices[i], ref firstMin, ref firstMax);
                Widen(secondIndices[i], ref secondMin, ref secondMax);
                taken++;
            }
            return taken < 1 ? TerrainRegion.Empty
                : FromIndexRange(terrainPosition, terrainSize, resolution, firstMin, firstMax, secondMin, secondMax);
        }

        /// <summary> The same, for the history lists, whose indices are <c>int</c> rather than <c>ushort</c>. </summary>
        public static TerrainRegion FromCells(Vector3 terrainPosition, Vector3 terrainSize, int resolution,
            IList<int> firstIndices, IList<int> secondIndices, int count)
        {
            if (resolution < 2 || count < 1 || firstIndices == null || secondIndices == null)
            {
                return TerrainRegion.Empty;
            }

            int firstMin = int.MaxValue, firstMax = int.MinValue;
            int secondMin = int.MaxValue, secondMax = int.MinValue;
            int taken = 0;
            for (int i = 0; i < count; i++)
            {
                if (i >= firstIndices.Count || i >= secondIndices.Count) { break; }
                Widen(firstIndices[i], ref firstMin, ref firstMax);
                Widen(secondIndices[i], ref secondMin, ref secondMax);
                taken++;
            }
            return taken < 1 ? TerrainRegion.Empty
                : FromIndexRange(terrainPosition, terrainSize, resolution, firstMin, firstMax, secondMin, secondMax);
        }

        /// <summary> The same, over a window of the history lists, so one restore can be reported per detail layer.
        /// </summary>
        public static TerrainRegion FromCellWindow(Vector3 terrainPosition, Vector3 terrainSize, int resolution,
            IList<int> firstIndices, IList<int> secondIndices, int startIndex, int endIndex)
        {
            if (resolution < 2 || firstIndices == null || secondIndices == null) { return TerrainRegion.Empty; }

            int firstMin = int.MaxValue, firstMax = int.MinValue;
            int secondMin = int.MaxValue, secondMax = int.MinValue;
            int taken = 0;
            for (int i = startIndex; i < endIndex; i++)
            {
                if (i < 0 || i >= firstIndices.Count || i >= secondIndices.Count) { break; }
                Widen(firstIndices[i], ref firstMin, ref firstMax);
                Widen(secondIndices[i], ref secondMin, ref secondMax);
                taken++;
            }
            return taken < 1 ? TerrainRegion.Empty
                : FromIndexRange(terrainPosition, terrainSize, resolution, firstMin, firstMax, secondMin, secondMax);
        }

        /// <summary> The heightmap SAMPLE window covered by a set of touched cells, as the watertight path needs it.
        ///
        /// A sibling of <see cref="FromCells"/> rather than a second implementation of it: the same cell lists, read
        /// with the same first-is-Z convention, but left in sample space instead of converted to metres. The watertight
        /// write needs it in samples because everything it decides is counted in samples -- the skirt that puts the
        /// shared boundary sample inside the write, the blend rings, and the step limit at the tile's own spacing.
        ///
        /// Note the deliberate difference from <see cref="FromIndexRange"/>: that one widens by a cell on each side,
        /// because a cell is a corner and the ground that visibly moved reaches past the outermost touched corner. This
        /// one does NOT, because the samples that were written are exactly the samples that were written, and the
        /// outward growth here is the blend band's job -- growing twice would soften a ring of the road's own conformed
        /// surface. </summary>
        public static HeightSampleWindow WindowFromCells(IList<ushort> firstIndices, IList<ushort> secondIndices,
            int count)
        {
            HeightSampleWindow empty = HeightSampleWindow.None;
            if (count < 1 || firstIndices == null || secondIndices == null) { return empty; }

            int firstMin = int.MaxValue, firstMax = int.MinValue;
            int secondMin = int.MaxValue, secondMax = int.MinValue;
            int taken = 0;
            for (int i = 0; i < count; i++)
            {
                if (i >= firstIndices.Count || i >= secondIndices.Count) { break; }
                Widen(firstIndices[i], ref firstMin, ref firstMax);
                Widen(secondIndices[i], ref secondMin, ref secondMax);
                taken++;
            }
            if (taken < 1) { return empty; }
            return HeightSampleWindow.FromBounds(firstMin, firstMax, secondMin, secondMax);
        }

        private static void Widen(int value, ref int min, ref int max)
        {
            if (value < min) { min = value; }
            if (value > max) { max = value; }
        }

        /// <summary> Index extents to a world box. One cell outwards on each side: a heightfield cell is a corner, not
        /// an area, so the ground that visibly changed reaches beyond the outermost touched corner, and a whole cell is
        /// the honest round-up. </summary>
        private static TerrainRegion FromIndexRange(Vector3 terrainPosition, Vector3 terrainSize, int resolution,
            int firstMin, int firstMax, int secondMin, int secondMax)
        {
            int span = resolution - 1;
            double metresPerCellZ = (double)terrainSize.z / span;
            double metresPerCellX = (double)terrainSize.x / span;

            double minZ = terrainPosition.z + (firstMin - 1) * metresPerCellZ;
            double maxZ = terrainPosition.z + (firstMax + 1) * metresPerCellZ;
            double minX = terrainPosition.x + (secondMin - 1) * metresPerCellX;
            double maxX = terrainPosition.x + (secondMax + 1) * metresPerCellX;

            return TerrainRegion.FromExtents(minX, terrainPosition.y, minZ,
                maxX, terrainPosition.y + terrainSize.y, maxZ);
        }

        /// <summary> The same, for a detail layer whose resolution differs from the heightmap's. </summary>
        public static TerrainRegion FromDetailCells(Vector3 terrainPosition, Vector3 terrainSize, int detailResolution,
            IList<ushort> firstIndices, IList<ushort> secondIndices, int count)
        {
            return FromCells(terrainPosition, terrainSize, detailResolution, firstIndices, secondIndices, count);
        }

        /// <summary> The world region of one terrain tile, for a change whose extent genuinely is the whole tile.
        /// Callers that CAN name their cells should, and this is here for the ones that cannot. </summary>
        public static TerrainRegion WholeTile(Terrain terrain)
        {
            if (terrain == null || terrain.terrainData == null) { return TerrainRegion.Empty; }
            return TerrainRegion.FromTerrainTile(terrain.transform.position, terrain.terrainData.size);
        }

        /// <summary> The region covering a set of tree positions, in the terrain's normalised 0..1 space. </summary>
        public static TerrainRegion FromNormalisedPositions(Vector3 terrainPosition, Vector3 terrainSize,
            IList<Vector3> normalisedPositions)
        {
            TerrainRegion region = TerrainRegion.Empty;
            if (normalisedPositions == null) { return region; }
            for (int i = 0; i < normalisedPositions.Count; i++)
            {
                Vector3 n = normalisedPositions[i];
                region = region.Encapsulate(
                    terrainPosition.x + n.x * terrainSize.x,
                    terrainPosition.y + n.y * terrainSize.y,
                    terrainPosition.z + n.z * terrainSize.z);
            }
            return region;
        }
    }
}
