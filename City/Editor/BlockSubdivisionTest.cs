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
    /// <summary> RM-10's gate: proves the block-subdivision operator is a deterministic pure function
    /// of (input, params, seed) and that the lots it produces obey the two project rules -- every lot
    /// fronts a street, and the lots partition the block exactly (no overlap, and their union is the
    /// whole inset block).
    ///
    /// The operator is driven over a HAND-BUILT GRID network, not the L-system's output, on purpose:
    /// the L-system grows a tree (RM-09 measured 23 nodes / 22 edges -- no cycles, so no bounded
    /// faces), which would give the block operator nothing to subdivide. A small grid of streets has
    /// known closed faces, so the frontage, no-overlap and union assertions are actually exercised.
    /// This tests the block operator; RM-09 already gated the network operator.
    ///
    /// It does NOT judge that the lots look right in the Scene view -- that is the user's visual check.
    /// What it locks down is that subdivision is reproducible and geometrically well-formed. </summary>
    public static class BlockSubdivisionTest
    {
        private const float Spacing = 200f;   // metres between grid streets
        private const int GridN = 3;          // 3x3 nodes -> 2x2 = four square blocks
        private const int SameSeed = 12345;
        private const int OtherSeed = 67890;

        [MenuItem("Window/Road City Master/Testing/Block subdivision")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.BlockSubdivisionTest.RunBatch. Exits 1 on failure so a build
        /// script can gate on it. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Builds, subdivides, and checks. Report lines are prefixed RCMBLOCKS so a log grep
        /// can read the verdict without wading through Unity's surrounding output. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMBLOCKS BEGIN\n");
            List<string> failures = new List<string>();

            RoadClassCatalog catalog = ScriptableObject.CreateInstance<RoadClassCatalog>();
            BlockSubdivisionOperator op = ScriptableObject.CreateInstance<BlockSubdivisionOperator>();
            try
            {
                catalog.ResetToDefaults();
                op.catalog = catalog;
                op.footwayWidth = 2f;
                op.setback = 3f;
                op.targetLotArea = 800f;
                op.splitJitter = 0.35f;

                StreetNetwork net = BuildGridNetwork();
                BlockSubdivisionInput input = new BlockSubdivisionInput(net);

                BlockSubdivisionResult first = op.Execute(input, SameSeed);
                BlockSubdivisionResult second = op.Execute(input, SameSeed);
                BlockSubdivisionResult other = op.Execute(input, OtherSeed);

                builder.Append("RCMBLOCKS result ").Append(first.report.ToString()).Append("\n");

                Expect(first.report.faces >= 4, "grid produced fewer than four faces", failures);
                Expect(first.blocks.Count > 0, "no blocks were produced", failures);
                Expect(first.lots.Count > 0, "no lots were produced", failures);

                CheckDeterministic(first, second, failures);
                CheckFrontage(first, failures);
                CheckPartition(first, failures);
                CheckSeedsDiffer(first, other, failures);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(op);
                ScriptableObject.DestroyImmediate(catalog);
            }

            foreach (string failure in failures)
            {
                builder.Append("RCMBLOCKS FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMBLOCKS result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMBLOCKS END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A GridN x GridN lattice of streets: nodes on a regular grid (heights taken from the
        /// shared fake terrain so lot y-values are interpolated, not flat), horizontal and vertical
        /// edges between neighbours, all of the local class. Its interior squares are the closed faces
        /// the operator turns into blocks. </summary>
        private static StreetNetwork BuildGridNetwork()
        {
            FakePcgTerrain terrain = new FakePcgTerrain();
            StreetNetwork net = new StreetNetwork();
            int[,] nodeIndex = new int[GridN, GridN];

            for (int j = 0; j < GridN; j++)
            {
                for (int i = 0; i < GridN; i++)
                {
                    float x = i * Spacing;
                    float z = j * Spacing;
                    bool reused;
                    nodeIndex[i, j] = net.AddNodeSnapped(
                        new Vector3(x, terrain.SampleHeight(x, z), z), 0.01f, out reused);
                }
            }

            for (int j = 0; j < GridN; j++)
            {
                for (int i = 0; i < GridN; i++)
                {
                    if (i + 1 < GridN)
                    {
                        net.AddEdge(nodeIndex[i, j], nodeIndex[i + 1, j], RoadClassCatalog.LocalId, false, 0f);
                    }
                    if (j + 1 < GridN)
                    {
                        net.AddEdge(nodeIndex[i, j], nodeIndex[i, j + 1], RoadClassCatalog.LocalId, false, 0f);
                    }
                }
            }
            return net;
        }

        /// <summary> C-06: two runs on the same network and seed are field-for-field identical -- block
        /// ids, boundary node lists and polygons, and every lot's id, blockId, zoneId and polygon. Any
        /// nondeterminism (an unseeded random, an order-dependent face key) surfaces here. </summary>
        private static void CheckDeterministic(BlockSubdivisionResult a, BlockSubdivisionResult b,
            List<string> failures)
        {
            if (a.blocks.Count != b.blocks.Count || a.lots.Count != b.lots.Count)
            {
                failures.Add("re-run differs in size: blocks " + a.blocks.Count + "/" + b.blocks.Count +
                             " lots " + a.lots.Count + "/" + b.lots.Count);
                return;
            }

            for (int i = 0; i < a.blocks.Count; i++)
            {
                Block ba = a.blocks[i];
                Block bb = b.blocks[i];
                if (ba.id != bb.id || !IntsEqual(ba.boundaryNodes, bb.boundaryNodes) ||
                    !PolysEqual(ba.polygon, bb.polygon))
                {
                    failures.Add("re-run block " + i + " differs");
                    return;
                }
            }
            for (int i = 0; i < a.lots.Count; i++)
            {
                Lot la = a.lots[i];
                Lot lb = b.lots[i];
                if (la.id != lb.id || la.blockId != lb.blockId || la.zoneId != lb.zoneId ||
                    !PolysEqual(la.polygon, lb.polygon))
                {
                    failures.Add("re-run lot " + i + " differs");
                    return;
                }
            }
        }

        /// <summary> Every lot fronts a street: at least one lot edge's midpoint lies on an edge of its
        /// parent block's boundary. The midpoint test is deliberately independent of the operator's own
        /// collinear-overlap frontage rule, so it is a real oracle rather than a restatement. </summary>
        private static void CheckFrontage(BlockSubdivisionResult result, List<string> failures)
        {
            Dictionary<int, List<Vector3>> blockPolygon = new Dictionary<int, List<Vector3>>();
            for (int i = 0; i < result.blocks.Count; i++)
            {
                blockPolygon[result.blocks[i].id] = result.blocks[i].polygon;
            }

            for (int i = 0; i < result.lots.Count; i++)
            {
                Lot lot = result.lots[i];
                List<Vector3> boundary;
                if (!blockPolygon.TryGetValue(lot.blockId, out boundary))
                {
                    failures.Add("lot " + i + " references unknown block " + lot.blockId);
                    continue;
                }
                if (!LotFrontsBlock(lot.polygon, boundary))
                {
                    failures.Add("lot " + i + " (block " + lot.blockId + ") has no street frontage");
                }
            }
        }

        /// <summary> The lots of each block partition it exactly: the summed lot area equals the block's
        /// inset polygon area within a small tolerance. Equality of areas together with a subdivision
        /// that only ever splits (never drops or overlaps) is what verifies both "no two lots overlap"
        /// and "the union of the lots is the whole block". </summary>
        private static void CheckPartition(BlockSubdivisionResult result, List<string> failures)
        {
            Dictionary<int, float> lotAreaByBlock = new Dictionary<int, float>();
            for (int i = 0; i < result.lots.Count; i++)
            {
                Lot lot = result.lots[i];
                float sum;
                lotAreaByBlock.TryGetValue(lot.blockId, out sum);
                lotAreaByBlock[lot.blockId] = sum + Geometry2D.Area(lot.polygon);
            }

            for (int i = 0; i < result.blocks.Count; i++)
            {
                Block block = result.blocks[i];
                float blockArea = Geometry2D.Area(block.polygon);
                float lotArea;
                lotAreaByBlock.TryGetValue(block.id, out lotArea);
                float tolerance = Mathf.Max(1f, blockArea * 0.001f);
                if (Mathf.Abs(blockArea - lotArea) > tolerance)
                {
                    failures.Add("block " + block.id + " lots sum to " + lotArea.ToString("F2") +
                                 " but block area is " + blockArea.ToString("F2"));
                }
            }
        }

        /// <summary> A different seed produces genuinely different lots, not the same subdivision: the
        /// seeded split jitter is doing its job. Guarded so it only fires when both runs made lots. </summary>
        private static void CheckSeedsDiffer(BlockSubdivisionResult a, BlockSubdivisionResult b,
            List<string> failures)
        {
            if (a.lots.Count == 0 || b.lots.Count == 0)
            {
                return;
            }
            bool identical = a.lots.Count == b.lots.Count;
            if (identical)
            {
                for (int i = 0; i < a.lots.Count; i++)
                {
                    if (!PolysEqual(a.lots[i].polygon, b.lots[i].polygon))
                    {
                        identical = false;
                        break;
                    }
                }
            }
            Expect(!identical, "two different seeds produced identical lots", failures);
        }

        /// <summary> True when some edge of <paramref name="lot"/> has its midpoint on an edge of the
        /// block <paramref name="boundary"/> -- an independent read of "this lot touches a street". </summary>
        private static bool LotFrontsBlock(List<Vector3> lot, List<Vector3> boundary)
        {
            for (int i = 0; i < lot.Count; i++)
            {
                Vector2 p = Geometry2D.XZ(lot[i]);
                Vector2 q = Geometry2D.XZ(lot[(i + 1) % lot.Count]);
                Vector2 mid = (p + q) * 0.5f;
                for (int j = 0; j < boundary.Count; j++)
                {
                    Vector2 r = Geometry2D.XZ(boundary[j]);
                    Vector2 s = Geometry2D.XZ(boundary[(j + 1) % boundary.Count]);
                    if (PointOnSegment(mid, r, s, 0.05f))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary> True when point <paramref name="m"/> lies within <paramref name="eps"/> metres of
        /// segment r-s and between its ends. </summary>
        private static bool PointOnSegment(Vector2 m, Vector2 r, Vector2 s, float eps)
        {
            Vector2 rs = s - r;
            float len2 = rs.sqrMagnitude;
            if (len2 < 1e-8f)
            {
                return false;
            }
            float t = Vector2.Dot(m - r, rs) / len2;
            if (t < 0f || t > 1f)
            {
                return false;
            }
            Vector2 closest = r + rs * t;
            return (m - closest).sqrMagnitude <= eps * eps;
        }

        /// <summary> Exact element-wise equality of two polygons -- the determinism check wants bitwise
        /// reproducibility, so this is deliberately not an epsilon compare. </summary>
        private static bool PolysEqual(List<Vector3> a, List<Vector3> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                // Component-wise exact compare: Vector3's own == is an epsilon compare, which would
                // mask a sub-1e-5 nondeterminism this check exists to catch.
                if (a[i].x != b[i].x || a[i].y != b[i].y || a[i].z != b[i].z)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> Exact element-wise equality of two int lists. </summary>
        private static bool IntsEqual(List<int> a, List<int> b)
        {
            if (a.Count != b.Count)
            {
                return false;
            }
            for (int i = 0; i < a.Count; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary> Records a failure message when <paramref name="condition"/> is false. </summary>
        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(failureMessage);
            }
        }
    }
}
#endif
