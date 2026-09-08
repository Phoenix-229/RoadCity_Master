// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One row of RM-20's Q-6 output form: a reference to a baked <see cref="BuildingInstance"/> plus
    /// the chunk it was assigned to. The building already carries its category, variant and world transform
    /// (SAVED, frozen at bake); this row adds only the streaming grid cell, so the spawn table is a thin index
    /// over the saved buildings and never duplicates their transforms. The LOD band is deliberately absent -- it
    /// is chosen at play time from camera distance and would go stale if baked. </summary>
    [Serializable]
    public struct CitySpawnItem
    {
        /// <summary> Index into <see cref="CityData.buildings"/> of the building this row spawns. </summary>
        public int buildingIndex;

        /// <summary> The streaming cell this building's position falls in, computed once at bake. </summary>
        public ChunkCoord chunk;

        public CitySpawnItem(int buildingIndex, ChunkCoord chunk)
        {
            this.buildingIndex = buildingIndex;
            this.chunk = chunk;
        }
    }


    /// <summary> The AUTHORED Q-6 runtime form (see <see cref="CityBakePipeline"/>): a PREFAB-INSTANCE SPAWN
    /// TABLE. The bake produces this table alongside the <see cref="CityData"/> asset; a runtime loader walks it,
    /// budgeting instantiation across frames (<see cref="CityStreamingScheduler"/>). Building it is pure and
    /// deterministic -- each saved building is assigned to its chunk in list order -- so RM-70 can assert the
    /// same city bakes the same table (C-06). </summary>
    [Serializable]
    public sealed class CitySpawnTable
    {
        /// <summary> The rows, one per saved building, in <see cref="CityData.buildings"/> order. </summary>
        public List<CitySpawnItem> items = new List<CitySpawnItem>();

        /// <summary> Build the table from a baked city by assigning each building to the chunk its world position
        /// falls in. Order-preserving and deterministic: the same city and chunk size always give the same rows
        /// in the same order. A null city yields an empty table. </summary>
        public static CitySpawnTable Build(CityData city, float chunkSizeMetres)
        {
            CitySpawnTable table = new CitySpawnTable();
            if (city == null || city.buildings == null) { return table; }
            for (int i = 0; i < city.buildings.Count; i++)
            {
                ChunkCoord chunk = CityChunkGrid.ChunkOf(city.buildings[i].position, chunkSizeMetres);
                table.items.Add(new CitySpawnItem(i, chunk));
            }
            return table;
        }

        /// <summary> The building indices whose chunk is resident for a camera in <paramref name="cameraCell"/>
        /// at the given ring radius -- the set a loader would enqueue for instantiation. Returned in table order
        /// so enqueueing is deterministic. </summary>
        public List<int> ResidentBuildingIndices(ChunkCoord cameraCell, int loadRingRadius)
        {
            List<int> resident = new List<int>();
            for (int i = 0; i < items.Count; i++)
            {
                if (CityChunkGrid.IsWithinLoadRing(cameraCell, items[i].chunk, loadRingRadius))
                {
                    resident.Add(items[i].buildingIndex);
                }
            }
            return resident;
        }
    }
}
