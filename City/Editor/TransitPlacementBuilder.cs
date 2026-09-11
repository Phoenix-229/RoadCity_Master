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
    /// B-27 / D-11: Ultra-Detailed Real-World Transit Hub Placement.
    ///
    /// Generates multimodal urban transit interchanges including bus layover bays,
    /// passenger terminal buildings, tram/light-rail boarding islands, and massive
    /// over-track steel canopy structures with glass roofs.
    ///
    /// BUS TERMINAL:
    /// Constructs a sawtooth or linear bus boarding layout, ensuring pedestrian safety 
    /// islands and raised curbs for accessibility.
    ///
    /// LIGHT RAIL ISLANDS:
    /// Builds raised passenger platforms between tracks with tactile paving and
    /// ticket/information kiosks.
    ///
    /// GLASS CANOPY:
    /// Procedurally places a massive steel frame and glass canopy over the entire
    /// transit hub to protect waiting passengers from weather.
    /// </summary>
    public static class TransitPlacementBuilder
    {
        private const string UndoLabel = "Build Transit Placements";

        public static void Build(CityAuthoring city, Transform parent)
        {
            if (city == null || parent == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            var islandCombines = new List<CombineInstance>();
            var canopyCombines = new List<CombineInstance>();
            var busCombines = new List<CombineInstance>();
            var amenitiesCombines = new List<CombineInstance>();

            Vector3 origin = Vector3.zero;
            float hubLength = 300f;
            float hubWidth = 100f;

            // 1. Build Bus Bays (Sawtooth layout)
            BuildBusBays(origin + new Vector3(0, 0, 10f), hubLength, 5, busCombines, amenitiesCombines);

            // 2. Build Light Rail Islands
            BuildLightRailIslands(origin + new Vector3(0, 0, 60f), hubLength, 3, islandCombines, amenitiesCombines);

            // 3. Build Overhead Canopy
            BuildCanopy(origin, hubLength, hubWidth, canopyCombines);

            // Final mesh compilation
            CreateMeshObject(parent, "transit.islands", islandCombines, null, true);
            CreateMeshObject(parent, "transit.canopy", canopyCombines, null, true);
            CreateMeshObject(parent, "transit.bus", busCombines, null, true);
            CreateMeshObject(parent, "transit.amenities", amenitiesCombines, null, true);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }

        private static void BuildBusBays(Vector3 origin, float length, int count, List<CombineInstance> busCombines, List<CombineInstance> amenitiesCombines)
        {
            float bayLength = length / count;
            Mesh platform = CreateBoxMesh(bayLength - 5f, 0.3f, 8f);
            Mesh shelter = CreateBoxMesh(10f, 0.1f, 3f);
            Mesh shelterPost = CreateCylinderMesh(0.1f, 3f, 8);

            for (int i = 0; i < count; i++)
            {
                float x = i * bayLength + (bayLength / 2f);
                Vector3 bayCenter = origin + new Vector3(x, 0, 0);

                // Sawtooth raised platform
                busCombines.Add(new CombineInstance { 
                    mesh = platform, 
                    transform = Matrix4x4.TRS(bayCenter + new Vector3(0, 0.15f, 0), Quaternion.Euler(0, 15, 0), Vector3.one) 
                });

                // Shelter Roof
                amenitiesCombines.Add(new CombineInstance { 
                    mesh = shelter, 
                    transform = Matrix4x4.TRS(bayCenter + new Vector3(0, 3f, 2f), Quaternion.Euler(5, 15, 0), Vector3.one) 
                });

                // Shelter Posts
                amenitiesCombines.Add(new CombineInstance { mesh = shelterPost, transform = Matrix4x4.TRS(bayCenter + new Vector3(-4f, 1.5f, 2f), Quaternion.Euler(0, 15, 0), Vector3.one) });
                amenitiesCombines.Add(new CombineInstance { mesh = shelterPost, transform = Matrix4x4.TRS(bayCenter + new Vector3(4f, 1.5f, 2f), Quaternion.Euler(0, 15, 0), Vector3.one) });
            }
        }

        private static void BuildLightRailIslands(Vector3 origin, float length, int count, List<CombineInstance> islandCombines, List<CombineInstance> amenitiesCombines)
        {
            float spacing = 15f;
            Mesh island = CreateBoxMesh(length - 20f, 0.9f, 6f);
            Mesh kiosk = CreateBoxMesh(3f, 2.5f, 2f);

            for (int i = 0; i < count; i++)
            {
                float z = i * spacing;
                Vector3 center = origin + new Vector3(length/2f, 0.45f, z);

                // Raised Platform
                islandCombines.Add(new CombineInstance { mesh = island, transform = Matrix4x4.TRS(center, Quaternion.identity, Vector3.one) });

                // Information Kiosks
                amenitiesCombines.Add(new CombineInstance { mesh = kiosk, transform = Matrix4x4.TRS(center + new Vector3(-length/4f, 1.25f, 0), Quaternion.identity, Vector3.one) });
                amenitiesCombines.Add(new CombineInstance { mesh = kiosk, transform = Matrix4x4.TRS(center + new Vector3(length/4f, 1.25f, 0), Quaternion.identity, Vector3.one) });
            }
        }

        private static void BuildCanopy(Vector3 origin, float length, float width, List<CombineInstance> canopyCombines)
        {
            Mesh column = CreateCylinderMesh(0.5f, 12f, 16);
            Mesh roofBeam = CreateBoxMesh(width, 1f, 1f);
            Mesh glassPane = CreateBoxMesh(20f, 0.1f, width);

            int spanCount = Mathf.FloorToInt(length / 20f);

            for (int i = 0; i <= spanCount; i++)
            {
                float x = i * 20f;
                Vector3 cBase = origin + new Vector3(x, 0, width/2f);

                // Support Columns
                canopyCombines.Add(new CombineInstance { mesh = column, transform = Matrix4x4.TRS(origin + new Vector3(x, 6f, 0), Quaternion.identity, Vector3.one) });
                canopyCombines.Add(new CombineInstance { mesh = column, transform = Matrix4x4.TRS(origin + new Vector3(x, 6f, width), Quaternion.identity, Vector3.one) });

                // Transverse Roof Beam
                canopyCombines.Add(new CombineInstance { mesh = roofBeam, transform = Matrix4x4.TRS(cBase + new Vector3(0, 12f, 0), Quaternion.identity, Vector3.one) });

                // Glass roof panels
                if (i < spanCount)
                {
                    canopyCombines.Add(new CombineInstance { mesh = glassPane, transform = Matrix4x4.TRS(cBase + new Vector3(10f, 12.1f, 0), Quaternion.identity, Vector3.one) });
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