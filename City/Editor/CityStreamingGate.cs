// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-17's gate: proves the parts of LOD/STREAMING that carry the bugs and can be checked without
    /// a scene. The measured frame time on the named device is the author's gate and cannot be asserted
    /// headlessly; what CAN be, and is where the bugs live, is the PURE scheduling and selection maths beneath
    /// it -- that a world position lands in the right chunk (negatives floor DOWN, boundaries are half-open),
    /// that the 25+1 square ring decides residency by Chebyshev distance, that camera distance selects the right
    /// band and drops a chunk past the far band, that colliders follow the AUTHORED per-band table, that the
    /// spawn table bakes deterministically (C-06), and -- the load-spike rule the gate is really about -- that
    /// the frame-budget scheduler drains N buildings in ceil(N / budget) frames with every index emitted exactly
    /// once in FIFO order. Report lines are prefixed RCMSTREAM. </summary>
    public static class CityStreamingGate
    {
        [MenuItem("Window/Road City Master/Testing/City LOD and streaming")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityStreamingGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMSTREAM report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMSTREAM BEGIN\n");
            List<string> failures = new List<string>();

            CaseChunkAssignment(builder, failures);
            CaseLoadRing(builder, failures);
            CaseBandByDistance(builder, failures);
            CaseCollidersByBand(builder, failures);
            CaseScheduler(builder, failures);
            CaseSpawnTableDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMSTREAM FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMSTREAM result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMSTREAM END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        // PLACEHOLDER_CASES

        /// <summary> A world position lands in the expected cell: interior points, a half-open boundary (exactly
        /// one chunk edge belongs to the higher cell), and negative coordinates that must floor DOWN. </summary>
        private static void CaseChunkAssignment(StringBuilder builder, List<string> failures)
        {
            const float size = 100f;
            Expect(CityChunkGrid.ChunkOf(new Vector3(50f, 9f, 50f), size).Equals(new ChunkCoord(0, 0)),
                "chunk: (50,50) was not cell (0,0)", failures);
            Expect(CityChunkGrid.ChunkOf(new Vector3(150f, 0f, 250f), size).Equals(new ChunkCoord(1, 2)),
                "chunk: (150,250) was not cell (1,2)", failures);
            Expect(CityChunkGrid.ChunkOf(new Vector3(100f, 0f, 100f), size).Equals(new ChunkCoord(1, 1)),
                "chunk: the half-open boundary (100,100) was not cell (1,1)", failures);
            Expect(CityChunkGrid.ChunkOf(new Vector3(-1f, 0f, -1f), size).Equals(new ChunkCoord(-1, -1)),
                "chunk: (-1,-1) did not floor down to cell (-1,-1)", failures);
            Expect(CityChunkGrid.ChunkOf(new Vector3(-0.5f, 0f, -250f), size).Equals(new ChunkCoord(-1, -3)),
                "chunk: a negative coordinate was truncated toward zero instead of floored", failures);
            builder.Append("RCMSTREAM ").Append("chunk-assign".PadRight(20))
                   .Append("interior, half-open boundary and negatives floor correctly\n");
        }

        /// <summary> The 25+1 residency test is Chebyshev: at radius 2 a 5x5 block around the camera cell is
        /// resident, corners included, and ring 3 is out. </summary>
        private static void CaseLoadRing(StringBuilder builder, List<string> failures)
        {
            ChunkCoord cam = new ChunkCoord(0, 0);
            Expect(CityChunkGrid.IsWithinLoadRing(cam, new ChunkCoord(0, 0), 2),
                "ring: the camera's own cell was not resident", failures);
            Expect(CityChunkGrid.IsWithinLoadRing(cam, new ChunkCoord(2, 2), 2),
                "ring: the 5x5 corner (2,2) was not resident at radius 2", failures);
            Expect(CityChunkGrid.IsWithinLoadRing(cam, new ChunkCoord(-2, 1), 2),
                "ring: (-2,1) was not resident at radius 2", failures);
            Expect(!CityChunkGrid.IsWithinLoadRing(cam, new ChunkCoord(3, 0), 2),
                "ring: ring-3 cell (3,0) was wrongly resident at radius 2", failures);
            Expect(CityChunkGrid.RingDistance(new ChunkCoord(-1, 4), new ChunkCoord(2, 2)) == 3,
                "ring: Chebyshev distance was not max(|dx|,|dz|)", failures);
            builder.Append("RCMSTREAM ").Append("load-ring".PadRight(20))
                   .Append("5x5 Chebyshev ring resident, ring 3 out\n");
        }

        /// <summary> Camera distance selects the band, boundaries inclusive of the nearer band, and past the far
        /// band the chunk is not loaded at all. </summary>
        private static void CaseBandByDistance(StringBuilder builder, List<string> failures)
        {
            CityStreamingConfig cfg = CityStreamingConfig.Default();
            Expect(cfg.BandForDistance(100f) == LodBand.Near, "band: 100 m was not Near", failures);
            Expect(cfg.BandForDistance(150f) == LodBand.Near, "band: the Near boundary 150 m was not Near", failures);
            Expect(cfg.BandForDistance(151f) == LodBand.Mid, "band: 151 m was not Mid", failures);
            Expect(cfg.BandForDistance(400f) == LodBand.Mid, "band: the Mid boundary 400 m was not Mid", failures);
            Expect(cfg.BandForDistance(401f) == LodBand.Far, "band: 401 m was not Far", failures);
            Expect(cfg.IsLoadedAtDistance(1200f), "band: the Far boundary 1200 m was not loaded", failures);
            Expect(!cfg.IsLoadedAtDistance(1201f), "band: a chunk past the far band was still loaded", failures);
            builder.Append("RCMSTREAM ").Append("band-by-distance".PadRight(20))
                   .Append("Near<=150, Mid<=400, Far<=1200, unloaded beyond\n");
        }

        /// <summary> Colliders follow the AUTHORED per-band table: on in Near, off in Mid and Far. </summary>
        private static void CaseCollidersByBand(StringBuilder builder, List<string> failures)
        {
            CityStreamingConfig cfg = CityStreamingConfig.Default();
            Expect(cfg.CollidersEnabled(LodBand.Near), "colliders: Near band had colliders off", failures);
            Expect(!cfg.CollidersEnabled(LodBand.Mid), "colliders: Mid band had colliders on", failures);
            Expect(!cfg.CollidersEnabled(LodBand.Far), "colliders: Far band had colliders on", failures);
            builder.Append("RCMSTREAM ").Append("colliders-by-band".PadRight(20))
                   .Append("Near on, Mid/Far off (AUTHORED)\n");
        }

        /// <summary> The load-spike rule: the scheduler drains N indices in ceil(N / budget) pumps, each pump at
        /// most the budget, and every index emerges EXACTLY ONCE in FIFO order -- no drop, no duplicate, no
        /// stall in one frame. </summary>
        private static void CaseScheduler(StringBuilder builder, List<string> failures)
        {
            const int n = 100;
            const int budget = 8;
            CityStreamingScheduler scheduler = new CityStreamingScheduler();
            List<int> source = new List<int>();
            for (int i = 0; i < n; i++) { source.Add(i); }
            scheduler.EnqueueRange(source);

            List<int> drained = new List<int>();
            int pumps = 0;
            bool everyBatchWithinBudget = true;
            while (!scheduler.IsComplete)
            {
                List<int> batch = scheduler.Pump(budget);
                pumps++;
                if (batch.Count > budget || batch.Count == 0) { everyBatchWithinBudget = false; }
                drained.AddRange(batch);
                if (pumps > n + 1) { break; } // guard against a non-terminating pump
            }

            int expectedPumps = (n + budget - 1) / budget; // ceil(100 / 8) = 13
            Expect(pumps == expectedPumps,
                "scheduler: drained in " + pumps + " pumps, expected " + expectedPumps, failures);
            Expect(everyBatchWithinBudget, "scheduler: a pump returned 0 or more than the budget", failures);
            Expect(drained.Count == n, "scheduler: drained " + drained.Count + " indices, expected " + n, failures);
            bool inOrder = drained.Count == n;
            for (int i = 0; i < drained.Count && inOrder; i++) { if (drained[i] != i) { inOrder = false; } }
            Expect(inOrder, "scheduler: indices did not emerge exactly once in FIFO order", failures);
            Expect(scheduler.IsComplete && scheduler.PendingCount == 0,
                "scheduler: not marked complete after draining", failures);
            Expect(scheduler.Pump(budget).Count == 0, "scheduler: pumping an empty queue returned work", failures);

            builder.Append("RCMSTREAM ").Append("frame-budget".PadRight(20))
                   .Append(n).Append(" items / budget ").Append(budget).Append(" -> ").Append(pumps)
                   .Append(" pumps, FIFO, once each\n");
        }

        /// <summary> The spawn table bakes deterministically: the same city and chunk size produce the same rows
        /// in the same order (C-06), each building assigned to its position's chunk, and a residency query
        /// returns the buildings in the resident ring. </summary>
        private static void CaseSpawnTableDeterministic(StringBuilder builder, List<string> failures)
        {
            CityData city = ScriptableObject.CreateInstance<CityData>();
            city.buildings.Add(BuildingAt(new Vector3(50f, 0f, 50f)));    // index 0 -> chunk (0,0)
            city.buildings.Add(BuildingAt(new Vector3(250f, 0f, 50f)));   // index 1 -> chunk (2,0)
            city.buildings.Add(BuildingAt(new Vector3(-50f, 0f, 350f)));  // index 2 -> chunk (-1,3)

            CitySpawnTable a = CitySpawnTable.Build(city, 100f);
            CitySpawnTable b = CitySpawnTable.Build(city, 100f);
            Expect(a.items.Count == 3 && b.items.Count == 3, "spawn: table did not have one row per building", failures);

            bool identical = a.items.Count == b.items.Count;
            for (int i = 0; i < a.items.Count && identical; i++)
            {
                if (a.items[i].buildingIndex != b.items[i].buildingIndex
                    || !a.items[i].chunk.Equals(b.items[i].chunk)) { identical = false; }
            }
            Expect(identical, "spawn: two bakes of the same city produced different tables", failures);
            Expect(a.items[0].chunk.Equals(new ChunkCoord(0, 0))
                && a.items[1].chunk.Equals(new ChunkCoord(2, 0))
                && a.items[2].chunk.Equals(new ChunkCoord(-1, 3)),
                "spawn: a building was assigned to the wrong chunk", failures);

            // A camera in cell (0,0) at radius 2 reaches buildings 0 and 1 (rings 0 and 2) but not 2 (ring 3).
            List<int> resident = a.ResidentBuildingIndices(new ChunkCoord(0, 0), 2);
            Expect(resident.Count == 2 && resident[0] == 0 && resident[1] == 1,
                "spawn: the resident set for cell (0,0) radius 2 was wrong", failures);

            builder.Append("RCMSTREAM ").Append("spawn-table".PadRight(20))
                   .Append("deterministic bake, correct chunks, ring query correct\n");
        }

        /// <summary> A building whose only field the streaming maths reads is its world position. </summary>
        private static BuildingInstance BuildingAt(Vector3 position)
        {
            BuildingInstance b = new BuildingInstance();
            b.position = position;
            b.rotation = Quaternion.identity;
            b.scale = Vector3.one;
            return b;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
