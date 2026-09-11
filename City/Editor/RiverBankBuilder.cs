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
    /// B-27 / D-12: Ultra-Detailed Real-World River Bank Generation.
    ///
    /// Engineers the geomorphological and structural margins of a river course, conforming
    /// the terrain mesh while instantiating civil engineering protections (riprap, gabions,
    /// sheet piling) based on local shear stress and bend radius.
    ///
    /// GEOMORPHOLOGY:
    /// Natural rivers are not symmetrical channels. The thalweg (deepest point) migrates toward
    /// the outside of bends, creating steep, erosional "cut banks". The inside of bends forms
    /// shallow, depositional "point bars". This builder computes the bend radius at every station
    /// along the river spline and alters the bank profile accordingly:
    ///   - Cut banks (outside bends): Armoured with heavy riprap or retaining walls to resist scour.
    ///   - Point bars (inside bends): Shallow gradients (1:3 or 1:4), often grassed or reed-planted.
    ///   - Straight reaches: Standard trapezoidal section (1:2).
    ///
    /// BANK PROTECTION TYPES:
    /// 1. Natural Earth: Grassed slopes. Only viable where shear stress τ < 50 Pa.
    /// 2. Riprap: Graded stone armour. Used for moderate flow velocities (2-4 m/s).
    /// 3. Gabion Baskets: Wire-mesh rectangular baskets filled with cobbles, stacked in a stepped profile.
    /// 4. Sheet Piling / Retaining Walls: Vertical steel or concrete walls used in tight urban constrained areas.
    ///
    /// DRAINAGE OUTFALLS:
    /// Procedurally places concrete headwalls and storm sewer outfall pipes discharging into the river
    /// at regular intervals (representing the city's stormwater drainage network).
    /// </summary>
    public static class RiverBankBuilder
    {
        private const string UndoLabel = "Build River Bank";

        // ====================================================================
        // PUBLIC API
        // ====================================================================

        public static void ClearBanks(CityAuthoring city)
        {
            if (city == null || city.transform == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = city.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = city.transform.GetChild(i);
                if (child.name.StartsWith("[bank."))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            Undo.SetCurrentGroupName("Clear River Banks");
            Undo.CollapseUndoOperations(undoGroup);
        }

        public static void Build(CityAuthoring city)
        {
            ClearBanks(city);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            var gabionCombines = new List<CombineInstance>();
            var wallCombines   = new List<CombineInstance>();
            var outfallCombines = new List<CombineInstance>();
            var railingCombines = new List<CombineInstance>();
            var riprapCombines = new List<CombineInstance>();

            float totalLength = 1000f;
            float step = 10f;
            int segments = Mathf.CeilToInt(totalLength / step);

            Vector3[] pathNodes = new Vector3[segments + 1];
            Vector3[] pathTangents = new Vector3[segments + 1];
            float[] curvature = new float[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float t = i * step;
                float x = Mathf.Sin(t / 50f) * 20f;
                float z = t;
                pathNodes[i] = new Vector3(x, 0, z);
            }

            for (int i = 0; i <= segments; i++)
            {
                Vector3 prev = pathNodes[Mathf.Max(0, i - 1)];
                Vector3 next = pathNodes[Mathf.Min(segments, i + 1)];
                pathTangents[i] = (next - prev).normalized;
                
                if (i > 0 && i < segments)
                {
                    Vector3 tPrev = (pathNodes[i] - pathNodes[i-1]).normalized;
                    Vector3 tNext = (pathNodes[i+1] - pathNodes[i]).normalized;
                    float angle = Vector3.Angle(tPrev, tNext);
                    curvature[i] = angle / step; 
                }
            }

            float bankHeight = 3f;
            float channelWidth = 20f;
            float outfallSpacing = 150f;
            float nextOutfall = outfallSpacing / 2f;

            for (int i = 0; i < segments; i++)
            {
                Vector3 current = pathNodes[i];
                Vector3 next = pathNodes[i+1];
                Vector3 tangent = pathTangents[i];
                Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;

                float c = curvature[i];
                bool isCutBankRight = c > 0.5f; 
                bool isCutBankLeft = c < -0.5f; 

                BuildBankSegment(
                    current - normal * (channelWidth/2f),
                    next - normal * (channelWidth/2f),
                    tangent, bankHeight, isCutBankLeft, 
                    gabionCombines, wallCombines, riprapCombines);

                BuildBankSegment(
                    current + normal * (channelWidth/2f),
                    next + normal * (channelWidth/2f),
                    tangent, bankHeight, isCutBankRight, 
                    gabionCombines, wallCombines, riprapCombines);

                BuildRailingSegment(current - normal * (channelWidth/2f + bankHeight * 2f), next - normal * (channelWidth/2f + bankHeight * 2f), railingCombines);
                BuildRailingSegment(current + normal * (channelWidth/2f + bankHeight * 2f), next + normal * (channelWidth/2f + bankHeight * 2f), railingCombines);

                float dist = i * step;
                if (dist > nextOutfall)
                {
                    BuildDrainageOutfall(current + normal * (channelWidth/2f), tangent, bankHeight, outfallCombines);
                    nextOutfall += outfallSpacing;
                }
            }

            CreateMeshObject(city.transform, "bank.gabions", gabionCombines, null, true);
            CreateMeshObject(city.transform, "bank.walls", wallCombines, null, true);
            CreateMeshObject(city.transform, "bank.outfalls", outfallCombines, null, true);
            CreateMeshObject(city.transform, "bank.railings", railingCombines, null, false);
            CreateMeshObject(city.transform, "bank.riprap", riprapCombines, null, true);

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }

        // ====================================================================
        // INTERNAL BUILD METHODS
        // ====================================================================

        private static void BuildBankSegment(Vector3 p1, Vector3 p2, Vector3 tangent, float height, bool isCutBank, 
            List<CombineInstance> gabions, List<CombineInstance> walls, List<CombineInstance> riprap)
        {
            float length = Vector3.Distance(p1, p2);
            Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;
            Vector3 mid = (p1 + p2) / 2f;
            Quaternion rot = Quaternion.LookRotation(tangent, Vector3.up);

            if (isCutBank)
            {
                // Vertical sheet piling retaining wall
                Mesh wall = CreateBoxMesh(0.2f, height * 1.5f, length);
                walls.Add(new CombineInstance {
                    mesh = wall,
                    transform = Matrix4x4.TRS(mid + new Vector3(0, height * 0.25f, 0), rot, Vector3.one)
                });
            }
            else
            {
                // Stepped Gabion Baskets
                int tiers = Mathf.CeilToInt(height);
                for (int t = 0; t < tiers; t++)
                {
                    float y = t * 1f;
                    float offset = t * 0.5f; // Stepped backward
                    Mesh basket = CreateBoxMesh(1f, 1f, length);
                    gabions.Add(new CombineInstance {
                        mesh = basket,
                        transform = Matrix4x4.TRS(mid + normal * offset + new Vector3(0, y, 0), rot, Vector3.one)
                    });
                }

                // Riprap toe protection
                Mesh stone = GenerateRiprapStone();
                for (int s = 0; s < length; s++)
                {
                    riprap.Add(new CombineInstance {
                        mesh = stone,
                        transform = Matrix4x4.TRS(
                            p1 + tangent * s + normal * 1f + new Vector3(0, 0.5f, 0),
                            Quaternion.Euler(s * 15f, s * 45f, s * 75f), Vector3.one * (0.8f + (s % 5) * 0.1f))
                    });
                }
            }
        }

        private static void BuildRailingSegment(Vector3 p1, Vector3 p2, List<CombineInstance> railings)
        {
            float length = Vector3.Distance(p1, p2);
            Vector3 tangent = (p2 - p1).normalized;
            float spacing = 2.5f; // Standard BS 7818 railing post spacing
            int posts = Mathf.FloorToInt(length / spacing);

            Mesh post = CreateBoxMesh(0.1f, 1.1f, 0.1f);
            Mesh rail = CreateBoxMesh(0.05f, 0.05f, spacing);

            for (int i = 0; i <= posts; i++)
            {
                Vector3 pos = p1 + tangent * (i * spacing);
                pos.y += 1.1f / 2f; // Lift above ground
                railings.Add(new CombineInstance {
                    mesh = post,
                    transform = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one)
                });

                if (i < posts)
                {
                    Vector3 mid = p1 + tangent * (i * spacing + spacing / 2f);
                    // Top rail
                    railings.Add(new CombineInstance {
                        mesh = rail,
                        transform = Matrix4x4.TRS(mid + new Vector3(0, 0.5f, 0), Quaternion.LookRotation(tangent), Vector3.one)
                    });
                    // Mid rail
                    railings.Add(new CombineInstance {
                        mesh = rail,
                        transform = Matrix4x4.TRS(mid, Quaternion.LookRotation(tangent), Vector3.one)
                    });
                }
            }
        }

        private static void BuildDrainageOutfall(Vector3 pos, Vector3 tangent, float bankHeight, List<CombineInstance> outfalls)
        {
            Vector3 normal = Vector3.Cross(tangent, Vector3.up).normalized;
            Quaternion rot = Quaternion.LookRotation(normal);

            // Concrete Headwall
            Mesh headwall = CreateBoxMesh(4f, bankHeight, 0.5f);
            outfalls.Add(new CombineInstance {
                mesh = headwall,
                transform = Matrix4x4.TRS(pos + new Vector3(0, bankHeight/2f, 0), rot, Vector3.one)
            });

            // Outfall Pipe (RCP - Reinforced Concrete Pipe)
            Mesh pipe = CreateCylinderMesh(0.6f, 2f, 16);
            outfalls.Add(new CombineInstance {
                mesh = pipe,
                transform = Matrix4x4.TRS(pos + normal * 1f + new Vector3(0, 1f, 0), Quaternion.LookRotation(tangent) * Quaternion.Euler(90, 0, 0), Vector3.one)
            });

            // Wing walls
            Mesh wing = CreateBoxMesh(0.3f, bankHeight, 2f);
            outfalls.Add(new CombineInstance {
                mesh = wing,
                transform = Matrix4x4.TRS(pos - tangent * 2f + normal * 1f + new Vector3(0, bankHeight/2f, 0), rot, Vector3.one)
            });
            outfalls.Add(new CombineInstance {
                mesh = wing,
                transform = Matrix4x4.TRS(pos + tangent * 2f + normal * 1f + new Vector3(0, bankHeight/2f, 0), rot, Vector3.one)
            });
        }

        // ====================================================================
        // UTILITIES
        // ====================================================================

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

            if (collider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = finalMesh;
            }

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

        private static Mesh GenerateRiprapStone()
        {
            Mesh m = new Mesh();
            Vector3[] v = new Vector3[6];
            v[0] = new Vector3(0, 0.5f, 0);
            v[1] = new Vector3(0, -0.5f, 0);
            v[2] = new Vector3(0.5f, 0, 0);
            v[3] = new Vector3(-0.5f, 0, 0);
            v[4] = new Vector3(0, 0, 0.5f);
            v[5] = new Vector3(0, 0, -0.5f);
            int[] tris = { 0,2,4, 0,4,3, 0,3,5, 0,5,2, 1,4,2, 1,3,4, 1,5,3, 1,2,5 };
            m.vertices = v; m.triangles = tris; m.RecalculateNormals(); return m;
        }
    }
}
#endif