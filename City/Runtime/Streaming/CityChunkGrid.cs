#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A cell in RM-17's square streaming grid, addressed by integer (x, z). Value type, addressed by
    /// stable integer coordinates so it round-trips and compares without a reference, and its
    /// <see cref="GetHashCode"/> is a fixed integer mix -- no <c>object.GetHashCode</c>, which would break C-06
    /// across sessions. </summary>
    [Serializable]
    public struct ChunkCoord : IEquatable<ChunkCoord>
    {
        public int x;
        public int z;

        public ChunkCoord(int x, int z) { this.x = x; this.z = z; }

        public bool Equals(ChunkCoord other) { return x == other.x && z == other.z; }
        public override bool Equals(object obj) { return obj is ChunkCoord other && Equals(other); }
        // determinism-exempt: pure integer-field override for dictionary keying, process-stable, not a generation-path string hash.
        public override int GetHashCode() { return x * 397 + z; }
        public override string ToString() { return "(" + x + "," + z + ")"; }
    }


    /// <summary> The pure chunk maths RM-17's loader runs -- assign a world position to a grid cell, and decide
    /// which cells are resident around the camera -- kept as a static solver so the sequence is provable
    /// headlessly (RM-70). Nothing here touches the scene; a loader calls it to decide what to instantiate.
    ///
    /// The grid is anchored at the world origin and square: cell (x, z) covers world XZ
    /// [x*size, (x+1)*size) x [z*size, (z+1)*size). Residency is a Chebyshev (square-ring) test so a radius-2
    /// ring is a 5x5 neighbourhood, matching the terrain's 25+1 model. All integer maths is
    /// <see cref="Mathf.FloorToInt"/>, so a negative coordinate floors DOWN (-1 m is chunk -1, never 0) and the
    /// result is identical on every machine (C-06). </summary>
    public static class CityChunkGrid
    {
        /// <summary> The cell a world position falls in, flooring so cell boundaries are half-open and negative
        /// coordinates map correctly. Only XZ matters; Y is ignored (streaming is planar). </summary>
        public static ChunkCoord ChunkOf(Vector3 worldPosition, float chunkSizeMetres)
        {
            float size = chunkSizeMetres > 0f ? chunkSizeMetres : 1f;
            return new ChunkCoord(
                Mathf.FloorToInt(worldPosition.x / size),
                Mathf.FloorToInt(worldPosition.z / size));
        }

        /// <summary> The world-XZ centre of a cell, at y = 0 -- the point a chunk-level camera-distance band
        /// selection measures against. </summary>
        public static Vector3 ChunkCentre(ChunkCoord cell, float chunkSizeMetres)
        {
            float size = chunkSizeMetres > 0f ? chunkSizeMetres : 1f;
            return new Vector3((cell.x + 0.5f) * size, 0f, (cell.z + 0.5f) * size);
        }

        /// <summary> The Chebyshev (chessboard) ring distance between two cells: <c>max(|dx|, |dz|)</c>. Ring 0
        /// is the same cell, ring 1 the 8 around it, and so on -- the natural metric for square-ring loading. </summary>
        public static int RingDistance(ChunkCoord a, ChunkCoord b)
        {
            int dx = a.x - b.x; if (dx < 0) { dx = -dx; }
            int dz = a.z - b.z; if (dz < 0) { dz = -dz; }
            return dx > dz ? dx : dz;
        }

        /// <summary> True when <paramref name="cell"/> is resident for a camera in <paramref name="cameraCell"/>
        /// at the given ring radius: its Chebyshev distance is within the radius. Radius 2 -> a 5x5 block. </summary>
        public static bool IsWithinLoadRing(ChunkCoord cameraCell, ChunkCoord cell, int loadRingRadius)
        {
            return RingDistance(cameraCell, cell) <= loadRingRadius;
        }
    }
}
