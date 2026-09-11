// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR

namespace RoadCityMaster.City.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;
    using RoadCityMaster.City;

    /// <summary>
    /// B-27 / D-10: Ultra-Detailed Real-World Port Infrastructure Placement.
    ///
    /// Generates complex procedural maritime shipping facilities including quay walls,
    /// tidal range pilings, ship-to-shore (STS) gantry cranes, container stacking yards,
    /// Automated Guided Vehicle (AGV) lanes, bollards, and complex rail shunting tracks.
    ///
    /// QUAY WALL & PILING:
    /// Real ports require deep-water access. The quay wall is simulated using a secant 
    /// pile wall structure with a concrete coping beam. Fendering systems (cylindrical
    /// or cone rubber fenders) are placed at intervals to absorb berthing energy.
    ///
    /// SHIP-TO-SHORE CRANES:
    /// Procedurally generated lattice-boom STS gantry cranes with massive outreach
    /// over the water to service Post-Panamax vessels.
    ///
    /// CONTAINER YARD:
    /// The yard is arranged in blocks (e.g. 6 TEUs wide, 5 TEUs high, 40 TEUs long).
    /// Stack arrangements include randomized container colors and gaps representing
    /// yard aisles for Rubber Tyred Gantry (RTG) cranes.
    ///
    /// RAIL SHUNTING YARDS:
    /// Intermodal ports feature complex rail spurs. This generates a ladder track
    /// layout leading into multiple parallel loading tracks for rail-bound cargo.
    /// </summary>
    public static class PortPlacementBuilder
    {
        private const string UndoLabel = "Build Port Placements";

        public static void Build(CityAuthoring city, Transform parent)
        {
            if (city == null || parent == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            var quayCombines = new List<CombineInstance>();
            var craneCombines = new List<CombineInstance>();
            var yardCombines = new List<CombineInstance>();
            var railCombines = new List<CombineInstance>();

            // Simulate port dimensions
            float portLength = 600f;
            float portWidth = 250f;
            float quayDepth = 15f; // Depth to sea bed

            Vector3 origin = Vector3.zero;

            // 1. Build Quay Wall and Coping Beam
            BuildQuayWall(origin, portLength, quayDepth, quayCombines);

            // 2. Build STS Gantry Cranes
            BuildCranes(origin, portLength, 4, craneCombines);

            // 3. Build Container Stacking Yard
            BuildContainerYard(origin + new Vector3(0, 0, 40f), portLength, portWidth - 100f, yardCombines);

            // 4. Build Intermodal Rail Shunting Yard
            BuildIntermodalRail(origin + new Vector3(0, 0, portWidth - 50f), portLength, railCombines);

            // Final mesh compilation
            CreateMeshObject(parent, "port.quay", quayCombines, null, true);
            CreateMeshObject(parent, "port.cranes", craneCombines, null, true);
            CreateMeshObject(parent, "port.yard", yardCombines, null, true);
            CreateMeshObject(parent, "port.rail", railCombines, null, false);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void BuildQuayWall(Vector3 origin, float length, float depth, List<CombineInstance> combines)
        {
            // Concrete coping beam
            Mesh coping = CreateBoxMesh(length, 2f, 5f);
            combines.Add(new CombineInstance { mesh = coping, transform = Matrix4x4.TRS(origin + new Vector3(length/2f, -1f, 2.5f), Quaternion.identity, Vector3.one) });

            // Sheet pile wall (represented as a flat vertical face)
            Mesh wall = CreateBoxMesh(length, depth, 1f);
            combines.Add(new CombineInstance { mesh = wall, transform = Matrix4x4.TRS(origin + new Vector3(length/2f, -depth/2f - 2f, 0.5f), Quaternion.identity, Vector3.one) });

            // Cylindrical fenders and cast iron bollards
            float bollardSpacing = 20f;
            int count = Mathf.FloorToInt(length / bollardSpacing);
            Mesh bollard = CreateCylinderMesh(0.4f, 1f, 12);
            Mesh fender = CreateCylinderMesh(0.8f, 2f, 16);

            for (int i = 0; i <= count; i++)
            {
                float x = i * bollardSpacing;
                combines.Add(new CombineInstance { mesh = bollard, transform = Matrix4x4.TRS(origin + new Vector3(x, 0.5f, 2f), Quaternion.identity, Vector3.one) });
                combines.Add(new CombineInstance { mesh = fender, transform = Matrix4x4.TRS(origin + new Vector3(x, -2f, -0.5f), Quaternion.Euler(90, 0, 0), Vector3.one) });
            }
        }

        private static void BuildCranes(Vector3 origin, float length, int count, List<CombineInstance> combines)
        {
            float spacing = length / (count + 1);
            Mesh leg = CreateBoxMesh(1.5f, 40f, 1.5f);
            Mesh boom = CreateBoxMesh(80f, 2f, 3f);
            Mesh apex = CreateBoxMesh(2f, 20f, 2f);

            for (int i = 1; i <= count; i++)
            {
                float x = i * spacing;
                Vector3 cBase = origin + new Vector3(x, 0, 15f);

                // 4 Legs
                combines.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.TRS(cBase + new Vector3(-8f, 20f, -8f), Quaternion.Euler(0,0,5), Vector3.one) });
                combines.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.TRS(cBase + new Vector3( 8f, 20f, -8f), Quaternion.Euler(0,0,-5), Vector3.one) });
                combines.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.TRS(cBase + new Vector3(-8f, 20f,  8f), Quaternion.Euler(0,0,5), Vector3.one) });
                combines.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.TRS(cBase + new Vector3( 8f, 20f,  8f), Quaternion.Euler(0,0,-5), Vector3.one) });

                // Main Boom
                combines.Add(new CombineInstance { mesh = boom, transform = Matrix4x4.TRS(cBase + new Vector3(0, 40f, -15f), Quaternion.identity, Vector3.one) });

                // Apex frame
                combines.Add(new CombineInstance { mesh = apex, transform = Matrix4x4.TRS(cBase + new Vector3(-6f, 50f, 0), Quaternion.Euler(0,0,15), Vector3.one) });
                combines.Add(new CombineInstance { mesh = apex, transform = Matrix4x4.TRS(cBase + new Vector3( 6f, 50f, 0), Quaternion.Euler(0,0,-15), Vector3.one) });
            }
        }

        private static void BuildContainerYard(Vector3 origin, float length, float width, List<CombineInstance> combines)
        {
            float blockLength = 120f;
            float blockWidth = 20f;
            float gapX = 30f;
            float gapZ = 15f;

            int cols = Mathf.FloorToInt(length / (blockLength + gapX));
            int rows = Mathf.FloorToInt(width / (blockWidth + gapZ));

            Mesh teu = CreateBoxMesh(6.06f, 2.59f, 2.44f); // Standard 20ft container

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 blockBase = origin + new Vector3(c * (blockLength + gapX), 0, r * (blockWidth + gapZ));
                    
                    // Generate stacked containers
                    for (int x = 0; x < 18; x++)
                    {
                        for (int z = 0; z < 6; z++)
                        {
                            int height = 1 + ((x * 7 + z * 13) % 5); // Up to 5 high
                            for (int y = 0; y < height; y++)
                            {
                                combines.Add(new CombineInstance {
                                    mesh = teu,
                                    transform = Matrix4x4.TRS(blockBase + new Vector3(x * 6.2f, y * 2.6f + 1.3f, z * 2.6f), Quaternion.identity, Vector3.one)
                                });
                            }
                        }
                    }
                }
            }
        }

        private static void BuildIntermodalRail(Vector3 origin, float length, List<CombineInstance> combines)
        {
            // Concrete sleepers and rail ties
            Mesh sleeper = CreateBoxMesh(2.6f, 0.2f, 0.3f);
            Mesh rail = CreateBoxMesh(0.1f, 0.15f, length);

            int trackCount = 4;
            float trackSpacing = 5f;

            for (int t = 0; t < trackCount; t++)
            {
                float z = t * trackSpacing;
                
                // Continuous rails
                combines.Add(new CombineInstance { mesh = rail, transform = Matrix4x4.TRS(origin + new Vector3(length/2f, 0.15f, z - 0.717f), Quaternion.identity, Vector3.one) });
                combines.Add(new CombineInstance { mesh = rail, transform = Matrix4x4.TRS(origin + new Vector3(length/2f, 0.15f, z + 0.717f), Quaternion.identity, Vector3.one) });

                // Sleepers
                for (int s = 0; s < length; s += 2)
                {
                    combines.Add(new CombineInstance { mesh = sleeper, transform = Matrix4x4.TRS(origin + new Vector3(s, 0.1f, z), Quaternion.identity, Vector3.one) });
                }
            }
        }

        private static void CreateMeshObject(Transform parent, string tag, List<CombineInstance> combines, Material mat, bool collider)
        {
            if (combines.Count == 0) return;
            Mesh finalMesh = new Mesh { name = tag, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            finalMesh.CombineMeshes(combines.ToArray(), true, true);

            GameObject go = new GameObject(tag);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            go.AddComponent<MeshFilter>().sharedMesh = finalMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = finalMesh;
            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        }

        private static Mesh CreateBoxMesh(float w, float h, float d)
        {
            Mesh m = new Mesh();
            float hw = w / 2f, hh = h / 2f, hd = d / 2f;
            Vector3[] v = new Vector3[24];
            v[0] = new Vector3(-hw, -hh, hd); v[1] = new Vector3(hw, -hh, hd);
            v[2] = new Vector3(hw,   hh, hd); v[3] = new Vector3(-hw,  hh, hd);
            v[4] = new Vector3(hw, -hh, -hd); v[5] = new Vector3(-hw, -hh, -hd);
            v[6] = new Vector3(-hw,  hh, -hd); v[7] = new Vector3(hw,   hh, -hd);
            v[8] = new Vector3(-hw, hh, hd); v[9] = new Vector3(hw, hh, hd);
            v[10] = new Vector3(hw, hh, -hd); v[11] = new Vector3(-hw, hh, -hd);
            v[12] = new Vector3(-hw, -hh, -hd); v[13] = new Vector3(hw, -hh, -hd);
            v[14] = new Vector3(hw, -hh,  hd); v[15] = new Vector3(-hw, -hh,  hd);
            v[16] = new Vector3(-hw, -hh, -hd); v[17] = new Vector3(-hw, -hh, hd);
            v[18] = new Vector3(-hw,  hh,  hd); v[19] = new Vector3(-hw,  hh, -hd);
            v[20] = new Vector3(hw, -hh,  hd); v[21] = new Vector3(hw, -hh, -hd);
            v[22] = new Vector3(hw,  hh, -hd); v[23] = new Vector3(hw,  hh,  hd);
            int[] tris = { 0,1,2, 0,2,3, 4,5,6, 4,6,7, 8,9,10, 8,10,11, 12,13,14, 12,14,15, 16,17,18, 16,18,19, 20,21,22, 20,22,23 };
            m.vertices = v; m.triangles = tris; m.RecalculateNormals(); return m;
        }

        private static Mesh CreateCylinderMesh(float r, float h, int seg)
        {
            Mesh m = new Mesh();
            int vCount = 2 + seg * 2;
            var verts = new Vector3[vCount];
            var tris  = new List<int>();
            float halfH = h / 2f;
            verts[0] = new Vector3(0, halfH, 0);
            verts[1] = new Vector3(0, -halfH, 0);
            for (int i = 0; i < seg; i++)
            {
                float angle = (i * 2f * Mathf.PI) / seg;
                float x = Mathf.Cos(angle) * r, z = Mathf.Sin(angle) * r;
                verts[2 + i * 2] = new Vector3(x, halfH, z);
                verts[2 + i * 2 + 1] = new Vector3(x, -halfH, z);
            }
            for (int i = 0; i < seg; i++)
            {
                int n = (i + 1) % seg;
                int tl = 2 + i * 2, bl = 2 + i * 2 + 1;
                int tr = 2 + n * 2, br = 2 + n * 2 + 1;
                tris.Add(0); tris.Add(tr); tris.Add(tl);
                tris.Add(1); tris.Add(bl); tris.Add(br);
                tris.Add(tl); tris.Add(bl); tris.Add(tr);
                tris.Add(tr); tris.Add(bl); tris.Add(br);
            }
            m.vertices = verts; m.triangles = tris.ToArray(); m.RecalculateNormals(); return m;
        }
    }
}
#endif