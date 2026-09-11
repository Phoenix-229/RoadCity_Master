namespace RoadCityMaster.City
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary> B-27 / D-8. A sector is a spatial subdivision of a settlement's footprint,
    /// used for spacing infrastructure so that bus stations, rail stations and other hub-type
    /// features are distributed rather than clustered.
    ///
    /// A sector is NOT a zone (zones are land-use: residential, commercial, industrial).
    /// A sector is a purely spatial partition — each sector is a contiguous area of roughly equal
    /// size, and each one gets at most one of each hub kind. A capital with 5 bus stations and
    /// 5 sectors gets one per sector; a town with 2 stations and 4 sectors gets one in the two
    /// sectors nearest the centroid and none in the other two.
    ///
    /// The subdivision is a simple Voronoi partition of the footprint rectangle using k-means
    /// on the footprint's own grid points, seeded from the city seed + a "sector" stream key
    /// (C-06 determinism). k = max(busStationCount, railStationCount) so every station has a
    /// sector to sit in.
    ///
    /// This is the D-8 task's own type. It did not exist before this file. </summary>
    [Serializable]
    public sealed class SettlementSector
    {
        /// <summary> The sector's centroid in world XZ. </summary>
        public Vector2 centroid;

        /// <summary> The sector's approximate radius (half-diagonal of its bounding box). </summary>
        public float radiusMetres;

        /// <summary> The sector's index, 0-based. </summary>
        public int index;

        // --- Static methods ---

        /// <summary> Subdivide a settlement footprint into k sectors using a seeded k-means.
        /// k = max(busStationCount, railStationCount, 1) — at least one sector always exists.
        ///
        /// The seed is the city seed reinterpreted through (ulong)(uint)citySeed, the exact
        /// cast every other seeded node in CityNodeDispatch uses, keyed with StreamKey.
        /// </summary>
        /// <param name="footprintMin"> World-space XZ min of the settlement footprint. </param>
        /// <param name="footprintMax"> World-space XZ max of the settlement footprint. </param>
        /// <param name="k"> Number of sectors. </param>
        /// <param name="seed"> The city seed. </param>
        /// <returns> k sectors, each with centroid and radius. </returns>
        public static List<SettlementSector> Subdivide(
            Vector2 footprintMin, Vector2 footprintMax, int k, int seed)
        {
            if (k < 1) k = 1;
            List<SettlementSector> result = new List<SettlementSector>(k);
            if (k == 1)
            {
                SettlementSector single = new SettlementSector();
                single.index = 0;
                single.centroid = (footprintMin + footprintMax) * 0.5f;
                single.radiusMetres = (footprintMax - footprintMin).magnitude * 0.5f;
                result.Add(single);
                return result;
            }

            ulong citySeed = (ulong)(uint)seed;
            DeterministicRandom rng = DeterministicRandom.ForElement(citySeed, StreamKey);

            Vector2[] centroids = new Vector2[k];
            for (int i = 0; i < k; i++)
            {
                float t = i / (float)(k - 1);
                Vector2 pos = Vector2.Lerp(footprintMin, footprintMax, t);
                pos.x += (float)rng.NextRange(-50.0, 50.0);
                pos.y += (float)rng.NextRange(-50.0, 50.0);
                centroids[i] = pos;
            }

            List<Vector2> points = new List<Vector2>();
            for (float x = footprintMin.x; x <= footprintMax.x; x += 50f)
            {
                for (float y = footprintMin.y; y <= footprintMax.y; y += 50f)
                {
                    points.Add(new Vector2(x, y));
                }
            }
            if (points.Count == 0) { points.Add((footprintMin + footprintMax) * 0.5f); }

            int[] assignment = new int[points.Count];
            Vector2[] sums = new Vector2[k];
            int[] counts = new int[k];

            for (int iter = 0; iter < 20; iter++)
            {
                for (int i = 0; i < k; i++)
                {
                    sums[i] = Vector2.zero;
                    counts[i] = 0;
                }

                for (int p = 0; p < points.Count; p++)
                {
                    Vector2 pt = points[p];
                    int best = 0;
                    float minDist = float.MaxValue;
                    for (int i = 0; i < k; i++)
                    {
                        float dSq = (pt - centroids[i]).sqrMagnitude;
                        if (dSq < minDist)
                        {
                            minDist = dSq;
                            best = i;
                        }
                    }
                    assignment[p] = best;
                    sums[best] += pt;
                    counts[best]++;
                }

                for (int i = 0; i < k; i++)
                {
                    if (counts[i] > 0)
                    {
                        centroids[i] = sums[i] / (float)counts[i];
                    }
                }
            }

            for (int i = 0; i < k; i++)
            {
                SettlementSector sector = new SettlementSector();
                sector.index = i;
                sector.centroid = centroids[i];
                float maxDistSq = 0f;
                for (int p = 0; p < points.Count; p++)
                {
                    if (assignment[p] == i)
                    {
                        float dSq = (points[p] - centroids[i]).sqrMagnitude;
                        if (dSq > maxDistSq) maxDistSq = dSq;
                    }
                }
                sector.radiusMetres = Mathf.Sqrt(maxDistSq);
                if (sector.radiusMetres < 50f) sector.radiusMetres = 50f;
                result.Add(sector);
            }

            return result;
        }

        /// <summary> The stream key for seeded sector subdivision. A named constant because
        /// changing it re-rolls every settlement's sectors (C-06). </summary>
        public const string StreamKey = "sector-subdivision";
    }
}
