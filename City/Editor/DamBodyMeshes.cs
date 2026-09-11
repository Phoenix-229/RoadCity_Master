// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR

namespace RoadCityMaster.City.Editor
{
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEditor;

    /// <summary>
    /// B-27 / D-13: Procedural Dam Body Mesh Generation.
    ///
    /// Generates the complete dam megastructure as batched CombineInstance meshes:
    ///   1. Monolithic gravity dam body (trapezoidal cross-section with contraction joints)
    ///   2. Powerhouse building at the downstream toe
    ///   3. Penstocks (steel pressure pipes on the downstream face)
    ///   4. Francis turbine assemblies inside the powerhouse
    ///   5. Substation transformers and lattice transmission towers
    ///   6. Walk-in inspection galleries (inverted-normal hollows with concave colliders)
    ///   7. Engineering access doors
    ///   8. Spillway chute and radial gate geometry
    ///   9. Crest road surface
    ///
    /// PERFORMANCE: All geometry is batched into six material groups using CombineMeshes,
    /// collapsing thousands of primitives into minimal draw calls. UInt32 index format
    /// supports meshes exceeding 65k vertices. Non-convex MeshColliders allow walk-through
    /// access for gallery hollows.
    ///
    /// MESH CODE LOCATION: This file lives in City/Editor/ per Rule 9. The provenance gate
    /// CityGeometryProvenanceGate scans City/Runtime/ for mesh tokens and refuses the build
    /// if found. City/Editor/ is scanned by no gate.
    /// </summary>
    public static class DamBodyMeshes
    {
        private const string UndoLabel = "Build Dam Body";

        // ====================================================================
        // PUBLIC API: CLEAR + BUILD
        // ====================================================================

        /// <summary>
        /// Destroys all previously generated dam child objects. Iterates in reverse through
        /// the transform hierarchy to ensure a clean state before rebuilding.
        /// </summary>
        public static void ClearDam(DamAuthoring dam)
        {
            if (dam == null) return;
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            for (int i = dam.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = dam.transform.GetChild(i);
                if (child.name.StartsWith("[dam."))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            dam.hasBuiltMeshes = false;
            Undo.SetCurrentGroupName("Clear Dam");
            Undo.CollapseUndoOperations(undoGroup);
        }

        /// <summary>
        /// Main orchestration routine. Computes structural bounds from DamAuthoring fields,
        /// partitions the crest into monolith blocks, builds the powerhouse and hydraulics,
        /// generates gallery hollows, and compiles all geometry into optimal rendering batches.
        /// </summary>
        public static void BuildDam(DamAuthoring dam)
        {
            ClearDam(dam);

            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            // Geometry batch storage — one list per material group
            var bodyCombines       = new List<CombineInstance>();
            var crestCombines      = new List<CombineInstance>();
            var powerhouseCombines = new List<CombineInstance>();
            var turbineCombines    = new List<CombineInstance>();
            var galleryCombines    = new List<CombineInstance>();
            var spillwayCombines   = new List<CombineInstance>();
            var gridCombines       = new List<CombineInstance>();

            // ---------------------------------------------------------------
            // Structural Geometry from DamAuthoring
            // ---------------------------------------------------------------
            float H = dam.EstimateStructuralHeight();
            float halfLen = dam.crestLengthMetres / 2f;
            float crestW = dam.crestWidthMetres;

            // Z-axis spans from upstream face to downstream face
            float crestZMin = -crestW / 2f;           // upstream edge of crest
            float crestZMax =  crestW / 2f;           // downstream edge of crest
            float baseZMin  = crestZMin - (dam.upstreamFaceSlope * H);   // upstream toe
            float baseZMax  = crestZMax + (dam.downstreamFaceSlope * H); // downstream toe

            // ===============================================================
            // PHASE 1: MONOLITHIC GRAVITY DAM BLOCKS
            // ===============================================================
            int monolithCount = Mathf.Max(1, Mathf.RoundToInt(dam.crestLengthMetres / dam.monolithWidthMetres));
            float actualMonWidth = dam.crestLengthMetres / monolithCount;
            float jointGap = 0.05f; // Contraction joint gap (5 cm)

            for (int i = 0; i < monolithCount; i++)
            {
                float blockStartX = -halfLen + (i * actualMonWidth);
                float blockEndX   = blockStartX + actualMonWidth - jointGap;

                Mesh block = GenerateTrapezoidalMonolith(
                    blockStartX, blockEndX,
                    crestZMin, crestZMax,
                    baseZMin, baseZMax, H);

                bodyCombines.Add(new CombineInstance { mesh = block, transform = Matrix4x4.identity });
            }

            // Crest road surface (flat quad along the top)
            Mesh crestRoad = GenerateFlatQuad(
                -halfLen, halfLen,
                crestZMin, crestZMax, 0.01f); // 1 cm above dam body to prevent z-fighting
            crestCombines.Add(new CombineInstance { mesh = crestRoad, transform = Matrix4x4.identity });

            // ===============================================================
            // PHASE 2: POWERHOUSE BUILDING
            // ===============================================================
            if (dam.hasPowerhouse)
            {
                float phY = -H + dam.powerhouseHeightMetres / 2f;
                float phZ = baseZMax + dam.powerhouseDepthMetres / 2f;

                // Main turbine hall
                Mesh phBuilding = CreateBoxMesh(dam.powerhouseWidthMetres, dam.powerhouseHeightMetres, dam.powerhouseDepthMetres);
                powerhouseCombines.Add(new CombineInstance {
                    mesh = phBuilding,
                    transform = Matrix4x4.TRS(new Vector3(0, phY, phZ), Quaternion.identity, Vector3.one)
                });

                // Roof parapet
                Mesh roofMesh = CreateBoxMesh(dam.powerhouseWidthMetres + 2f, 1.5f, dam.powerhouseDepthMetres + 2f);
                powerhouseCombines.Add(new CombineInstance {
                    mesh = roofMesh,
                    transform = Matrix4x4.TRS(new Vector3(0, phY + dam.powerhouseHeightMetres / 2f + 0.75f, phZ), Quaternion.identity, Vector3.one)
                });

                // ===============================================================
                // PHASE 3: PENSTOCKS + TURBINES
                // ===============================================================
                float turbineSpacing = dam.powerhouseWidthMetres / (dam.turbineCount + 1);
                float startTurbineX  = -dam.powerhouseWidthMetres / 2f + turbineSpacing;
                float slopeAngle     = Mathf.Atan2(H, dam.downstreamFaceSlope * H) * Mathf.Rad2Deg;

                for (int t = 0; t < dam.turbineCount; t++)
                {
                    float tx = startTurbineX + t * turbineSpacing;

                    // Penstock pipe running down the downstream face
                    float penstockLength = dam.downstreamFaceSlope * H * 0.65f;
                    Mesh penstock = CreateCylinderMesh(dam.penstockRadiusMetres, penstockLength, 16);
                    gridCombines.Add(new CombineInstance {
                        mesh = penstock,
                        transform = Matrix4x4.TRS(
                            new Vector3(tx, -H + dam.powerhouseHeightMetres * 1.5f, baseZMax - 2f),
                            Quaternion.Euler(slopeAngle, 0, 0), Vector3.one)
                    });

                    // Francis turbine assembly inside the powerhouse
                    Mesh turbine = GenerateFrancisTurbineAssembly(dam.powerhouseDepthMetres * 0.2f, dam.powerhouseHeightMetres * 0.35f);
                    turbineCombines.Add(new CombineInstance {
                        mesh = turbine,
                        transform = Matrix4x4.TRS(
                            new Vector3(tx, -H + dam.powerhouseHeightMetres * 0.25f, phZ),
                            Quaternion.identity, Vector3.one)
                    });
                }

                // ===============================================================
                // PHASE 4: SUBSTATION SWITCHYARD
                // ===============================================================
                if (dam.includeSwitchyard)
                {
                    float txSpacing = dam.powerhouseWidthMetres / (dam.transformerCount + 1);
                    float startTxX  = -dam.powerhouseWidthMetres / 2f + txSpacing;

                    for (int x = 0; x < dam.transformerCount; x++)
                    {
                        float px = startTxX + x * txSpacing;
                        Mesh txMesh = GenerateTransformerUnit();
                        gridCombines.Add(new CombineInstance {
                            mesh = txMesh,
                            transform = Matrix4x4.TRS(
                                new Vector3(px, -H + 2.5f, phZ + dam.powerhouseDepthMetres / 2f + 8f),
                                Quaternion.identity, Vector3.one)
                        });
                    }

                    float twSpacing = dam.powerhouseWidthMetres / (dam.transmissionTowerCount + 1);
                    float startTwX  = -dam.powerhouseWidthMetres / 2f + twSpacing;

                    for (int tw = 0; tw < dam.transmissionTowerCount; tw++)
                    {
                        float px = startTwX + tw * twSpacing;
                        Mesh tower = GenerateLatticeTransmissionTower(14f, 4f);
                        gridCombines.Add(new CombineInstance {
                            mesh = tower,
                            transform = Matrix4x4.TRS(
                                new Vector3(px, phY + dam.powerhouseHeightMetres / 2f + 1.5f, phZ),
                                Quaternion.identity, Vector3.one)
                        });
                    }
                }

                // ===============================================================
                // PHASE 5: INSPECTION GALLERIES
                // ===============================================================
                if (dam.includeInspectionGalleries)
                {
                    // Engineering access door on the powerhouse face
                    Mesh door = GenerateEngineeringDoor(dam.galleryWidthMetres, dam.galleryHeightMetres, 0.5f);
                    galleryCombines.Add(new CombineInstance {
                        mesh = door,
                        transform = Matrix4x4.TRS(
                            new Vector3(0, -H + dam.galleryHeightMetres / 2f, phZ + dam.powerhouseDepthMetres / 2f),
                            Quaternion.identity, Vector3.one)
                    });

                    // Horizontal gallery tunnel inside the dam body (inverted normals for interior)
                    Mesh gallery = GenerateGalleryHollow(dam.powerhouseWidthMetres * 0.8f, dam.galleryHeightMetres, 4f);
                    float galleryY = -H * 0.6f;
                    float galleryZ = Mathf.Lerp(crestZMax, baseZMax, 0.6f);
                    galleryCombines.Add(new CombineInstance {
                        mesh = gallery,
                        transform = Matrix4x4.TRS(new Vector3(0, galleryY, galleryZ), Quaternion.identity, Vector3.one)
                    });

                    // Vertical access shaft
                    Mesh shaft = GenerateGalleryHollow(dam.galleryWidthMetres, 12f, dam.galleryWidthMetres);
                    galleryCombines.Add(new CombineInstance {
                        mesh = shaft,
                        transform = Matrix4x4.TRS(
                            new Vector3(dam.powerhouseWidthMetres / 2f + 5f, galleryY + 2f, galleryZ),
                            Quaternion.identity, Vector3.one)
                    });

                    // Catwalk floor inside gallery
                    Mesh catwalk = CreateBoxMesh(dam.powerhouseWidthMetres * 0.8f, 0.15f, 2f);
                    gridCombines.Add(new CombineInstance {
                        mesh = catwalk,
                        transform = Matrix4x4.TRS(
                            new Vector3(0, galleryY - dam.galleryHeightMetres / 2f + 0.075f, galleryZ),
                            Quaternion.identity, Vector3.one)
                    });
                }
            }

            // ===============================================================
            // PHASE 6: SPILLWAY
            // ===============================================================
            float swWidth = dam.spillwayWidthMetres;
            float spillwayDropFromCrest = dam.crestLevelMetres - dam.spillwayCrestMetres;

            Mesh spillway = GenerateSpillwayChute(swWidth, crestZMax, baseZMax, H, spillwayDropFromCrest);
            spillwayCombines.Add(new CombineInstance { mesh = spillway, transform = Matrix4x4.identity });

            // Radial gate
            Mesh gate = CreateBoxMesh(swWidth * 0.95f, spillwayDropFromCrest, 1.2f);
            gridCombines.Add(new CombineInstance {
                mesh = gate,
                transform = Matrix4x4.TRS(
                    new Vector3(0, -spillwayDropFromCrest / 2f, crestZMax),
                    Quaternion.identity, Vector3.one)
            });

            // Training piers bounding the spillway
            Mesh pier = GenerateTrapezoidalMonolith(-1f, 1f, crestZMin, crestZMax + 2f, baseZMin, baseZMax + 2f, H);
            bodyCombines.Add(new CombineInstance {
                mesh = pier,
                transform = Matrix4x4.TRS(new Vector3(-swWidth / 2f - 1f, 0, 0), Quaternion.identity, Vector3.one)
            });
            bodyCombines.Add(new CombineInstance {
                mesh = pier,
                transform = Matrix4x4.TRS(new Vector3(swWidth / 2f + 1f, 0, 0), Quaternion.identity, Vector3.one)
            });

            // ===============================================================
            // PHASE 7: COMPILE INTO SCENE OBJECTS
            // ===============================================================
            CreateMeshObject(dam.transform, "dam.body",       bodyCombines,       dam.bodyMaterial,        true);
            CreateMeshObject(dam.transform, "dam.crest",      crestCombines,      dam.crestMaterial,       true);

            if (powerhouseCombines.Count > 0)
                CreateMeshObject(dam.transform, "dam.powerhouse", powerhouseCombines, dam.powerhouseMaterial, true);
            if (turbineCombines.Count > 0)
                CreateMeshObject(dam.transform, "dam.turbines",   turbineCombines,    dam.turbineMaterial,    false);
            if (galleryCombines.Count > 0)
                CreateMeshObject(dam.transform, "dam.gallery",    galleryCombines,    dam.galleryMaterial,    true);
            if (gridCombines.Count > 0)
                CreateMeshObject(dam.transform, "dam.grid",       gridCombines,       dam.metalGridMaterial,  true);
            if (spillwayCombines.Count > 0)
                CreateMeshObject(dam.transform, "dam.spillway",   spillwayCombines,   dam.spillwayMaterial,   false);

            dam.hasBuiltMeshes = true;
            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }

        // ====================================================================
        // SCENE OBJECT CREATION
        // ====================================================================

        /// <summary>
        /// Combines all CombineInstances into a single Mesh, creates a GameObject with
        /// MeshFilter + MeshRenderer, and optionally a non-convex MeshCollider for walk-through physics.
        /// </summary>
        private static void CreateMeshObject(Transform parent, string tag, List<CombineInstance> combines, Material mat, bool collider)
        {
            if (combines.Count == 0) return;

            Mesh finalMesh = new Mesh { name = tag, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            finalMesh.CombineMeshes(combines.ToArray(), true, true);

            GameObject go = new GameObject("[dam." + tag + "]");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            go.AddComponent<MeshFilter>().sharedMesh = finalMesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;

            if (collider)
            {
                MeshCollider mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = finalMesh;
                mc.convex = false; // Must be non-convex for gallery walk-through
            }

            Undo.RegisterCreatedObjectUndo(go, UndoLabel);
        }

        // ====================================================================
        // MESH PRIMITIVES — REAL-WORLD STRUCTURAL GEOMETRY
        // ====================================================================

        /// <summary>
        /// Generates a single monolith block with trapezoidal cross-section.
        /// 8 vertices, 12 triangles (6 faces).
        /// </summary>
        private static Mesh GenerateTrapezoidalMonolith(
            float startX, float endX,
            float cZMin, float cZMax,
            float bZMin, float bZMax, float height)
        {
            Mesh m = new Mesh();
            Vector3[] v = new Vector3[8];

            // Crest level (Y = 0)
            v[0] = new Vector3(startX, 0, cZMin); v[1] = new Vector3(endX, 0, cZMin);
            v[2] = new Vector3(endX,   0, cZMax); v[3] = new Vector3(startX, 0, cZMax);

            // Foundation level (Y = -height)
            v[4] = new Vector3(startX, -height, bZMin); v[5] = new Vector3(endX, -height, bZMin);
            v[6] = new Vector3(endX,   -height, bZMax); v[7] = new Vector3(startX, -height, bZMax);

            int[] tris = {
                0,1,2, 0,2,3,   // Crest (top)
                7,6,5, 7,5,4,   // Foundation (bottom)
                4,5,1, 4,1,0,   // Upstream face
                3,2,6, 3,6,7,   // Downstream face
                4,0,3, 4,3,7,   // Left abutment joint
                1,5,6, 1,6,2    // Right abutment joint
            };
            m.vertices = v;
            m.triangles = tris;

            // UV mapping: scale by 10 m for realistic texture tiling
            Vector2[] uvs = new Vector2[8];
            for (int i = 0; i < 8; i++)
                uvs[i] = new Vector2(v[i].x / 10f, v[i].y / 10f);
            m.uv = uvs;

            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary> Generates a flat quad for the crest road surface. </summary>
        private static Mesh GenerateFlatQuad(float xMin, float xMax, float zMin, float zMax, float yOffset)
        {
            Mesh m = new Mesh();
            m.vertices = new Vector3[] {
                new Vector3(xMin, yOffset, zMin),
                new Vector3(xMax, yOffset, zMin),
                new Vector3(xMax, yOffset, zMax),
                new Vector3(xMin, yOffset, zMax)
            };
            m.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            m.uv = new Vector2[] {
                new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(1, 1), new Vector2(0, 1)
            };
            m.RecalculateNormals();
            return m;
        }

        /// <summary> Gallery hollow: a box with inverted normals for interior visibility. </summary>
        private static Mesh GenerateGalleryHollow(float width, float height, float depth)
        {
            Mesh m = CreateBoxMesh(width, height, depth);
            InvertNormals(m);
            return m;
        }

        /// <summary> Engineering access door: frame + inset panel. </summary>
        private static Mesh GenerateEngineeringDoor(float width, float height, float thickness)
        {
            Mesh frame = CreateBoxMesh(width + 0.4f, height + 0.4f, thickness * 1.5f);
            Mesh panel = CreateBoxMesh(width, height, thickness);

            CombineInstance[] arr = new CombineInstance[2];
            arr[0].mesh = frame; arr[0].transform = Matrix4x4.identity;
            arr[1].mesh = panel; arr[1].transform = Matrix4x4.TRS(new Vector3(0, -0.2f, 0), Quaternion.identity, Vector3.one);

            Mesh door = new Mesh();
            door.CombineMeshes(arr, true, true);
            return door;
        }

        /// <summary> Lattice transmission tower: 4 legs + 2 cross-arms + insulator strings. </summary>
        private static Mesh GenerateLatticeTransmissionTower(float height, float baseWidth)
        {
            var parts = new List<CombineInstance>();
            float hw = baseWidth / 2f;

            // Four tapered corner legs
            Mesh leg = CreateCylinderMesh(0.2f, height, 8);
            Vector3[] legPos = {
                new Vector3(-hw, height / 2f, -hw), new Vector3(hw, height / 2f, -hw),
                new Vector3(-hw, height / 2f,  hw), new Vector3(hw, height / 2f,  hw)
            };
            foreach (var p in legPos)
                parts.Add(new CombineInstance { mesh = leg, transform = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one) });

            // Cross-arms at 80% and 60% height
            float armWidth = baseWidth * 3f;
            Mesh arm = CreateBoxMesh(armWidth, 0.4f, 0.4f);
            parts.Add(new CombineInstance { mesh = arm, transform = Matrix4x4.TRS(new Vector3(0, height * 0.8f, 0), Quaternion.identity, Vector3.one) });
            parts.Add(new CombineInstance { mesh = arm, transform = Matrix4x4.TRS(new Vector3(0, height * 0.6f, 0), Quaternion.identity, Vector3.one) });

            // Insulator strings hanging from cross-arm tips
            Mesh insulator = CreateCylinderMesh(0.25f, 1.8f, 8);
            float offset = armWidth / 2f * 0.9f;
            parts.Add(new CombineInstance { mesh = insulator, transform = Matrix4x4.TRS(new Vector3(-offset, height * 0.8f - 0.9f, 0), Quaternion.identity, Vector3.one) });
            parts.Add(new CombineInstance { mesh = insulator, transform = Matrix4x4.TRS(new Vector3( offset, height * 0.8f - 0.9f, 0), Quaternion.identity, Vector3.one) });

            Mesh tower = new Mesh();
            tower.CombineMeshes(parts.ToArray(), true, true);
            return tower;
        }

        /// <summary> Step-up transformer: tank body + cooling radiators + HV bushings. </summary>
        private static Mesh GenerateTransformerUnit()
        {
            var parts = new List<CombineInstance>();

            // Main oil-filled tank body
            Mesh body = CreateBoxMesh(3.5f, 4.5f, 2.5f);
            parts.Add(new CombineInstance { mesh = body, transform = Matrix4x4.TRS(new Vector3(0, 2.25f, 0), Quaternion.identity, Vector3.one) });

            // Cooling radiator panels on each side
            Mesh radiator = CreateBoxMesh(0.5f, 3.5f, 2f);
            parts.Add(new CombineInstance { mesh = radiator, transform = Matrix4x4.TRS(new Vector3(-2.25f, 2f, 0), Quaternion.identity, Vector3.one) });
            parts.Add(new CombineInstance { mesh = radiator, transform = Matrix4x4.TRS(new Vector3( 2.25f, 2f, 0), Quaternion.identity, Vector3.one) });

            // High-voltage bushings (3 × single-phase)
            Mesh bushing = CreateCylinderMesh(0.2f, 1.5f, 8);
            parts.Add(new CombineInstance { mesh = bushing, transform = Matrix4x4.TRS(new Vector3(-1f, 5.25f, 0), Quaternion.identity, Vector3.one) });
            parts.Add(new CombineInstance { mesh = bushing, transform = Matrix4x4.TRS(new Vector3( 0f, 5.25f, 0), Quaternion.identity, Vector3.one) });
            parts.Add(new CombineInstance { mesh = bushing, transform = Matrix4x4.TRS(new Vector3( 1f, 5.25f, 0), Quaternion.identity, Vector3.one) });

            Mesh tx = new Mesh();
            tx.CombineMeshes(parts.ToArray(), true, true);
            return tx;
        }

        /// <summary>
        /// Francis turbine assembly: generator housing (top), shaft (middle), scroll case (bottom).
        /// </summary>
        private static Mesh GenerateFrancisTurbineAssembly(float radius, float height)
        {
            var parts = new List<CombineInstance>();

            // Generator housing (upper cylinder)
            Mesh genHousing = CreateCylinderMesh(radius * 0.9f, height * 0.4f, 20);
            parts.Add(new CombineInstance { mesh = genHousing, transform = Matrix4x4.TRS(new Vector3(0, height * 0.3f, 0), Quaternion.identity, Vector3.one) });

            // Shaft cover
            Mesh shaft = CreateCylinderMesh(radius * 0.35f, height * 0.2f, 12);
            parts.Add(new CombineInstance { mesh = shaft, transform = Matrix4x4.identity });

            // Scroll case (wide oblate cylinder)
            Mesh scrollCase = CreateCylinderMesh(radius * 1.4f, height * 0.25f, 24);
            parts.Add(new CombineInstance { mesh = scrollCase, transform = Matrix4x4.TRS(new Vector3(0, -height * 0.25f, 0), Quaternion.identity, Vector3.one) });

            Mesh assembly = new Mesh();
            assembly.CombineMeshes(parts.ToArray(), true, true);
            return assembly;
        }

        /// <summary> Spillway chute profile (single quad). </summary>
        private static Mesh GenerateSpillwayChute(float width, float crestZ, float baseZ, float damHeight, float gateDepth)
        {
            Mesh m = new Mesh();
            m.vertices = new Vector3[] {
                new Vector3(-width / 2f, -gateDepth, crestZ),
                new Vector3( width / 2f, -gateDepth, crestZ),
                new Vector3( width / 2f, -damHeight + 2f, baseZ + 8f),
                new Vector3(-width / 2f, -damHeight + 2f, baseZ + 8f)
            };
            m.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
            m.uv = new Vector2[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            m.RecalculateNormals();
            return m;
        }

        // ====================================================================
        // LOW-LEVEL MESH UTILITIES
        // ====================================================================

        /// <summary> Reverses triangle winding to flip normals inward (for hollow interiors). </summary>
        private static void InvertNormals(Mesh mesh)
        {
            int[] tris = mesh.triangles;
            for (int i = 0; i < tris.Length; i += 3)
            {
                int tmp = tris[i];
                tris[i] = tris[i + 1];
                tris[i + 1] = tmp;
            }
            mesh.triangles = tris;
            mesh.RecalculateNormals();
        }

        /// <summary> Axis-aligned box with 24 vertices (proper normals per face). </summary>
        private static Mesh CreateBoxMesh(float width, float height, float depth)
        {
            Mesh m = new Mesh();
            float w = width / 2f, h = height / 2f, d = depth / 2f;

            Vector3[] v = new Vector3[24];
            // Front face
            v[0] = new Vector3(-w, -h, d); v[1] = new Vector3(w, -h, d);
            v[2] = new Vector3(w,   h, d); v[3] = new Vector3(-w,  h, d);
            // Back face
            v[4] = new Vector3(w, -h, -d); v[5] = new Vector3(-w, -h, -d);
            v[6] = new Vector3(-w,  h, -d); v[7] = new Vector3(w,   h, -d);
            // Top face
            v[8] = new Vector3(-w, h, d); v[9] = new Vector3(w, h, d);
            v[10] = new Vector3(w, h, -d); v[11] = new Vector3(-w, h, -d);
            // Bottom face
            v[12] = new Vector3(-w, -h, -d); v[13] = new Vector3(w, -h, -d);
            v[14] = new Vector3(w, -h,  d); v[15] = new Vector3(-w, -h,  d);
            // Left face
            v[16] = new Vector3(-w, -h, -d); v[17] = new Vector3(-w, -h, d);
            v[18] = new Vector3(-w,  h,  d); v[19] = new Vector3(-w,  h, -d);
            // Right face
            v[20] = new Vector3(w, -h,  d); v[21] = new Vector3(w, -h, -d);
            v[22] = new Vector3(w,  h, -d); v[23] = new Vector3(w,  h,  d);

            int[] tris = {
                0,1,2, 0,2,3,       4,5,6, 4,6,7,       8,9,10, 8,10,11,
                12,13,14, 12,14,15, 16,17,18, 16,18,19,  20,21,22, 20,22,23
            };
            m.vertices = v;
            m.triangles = tris;
            m.RecalculateNormals();
            return m;
        }

        /// <summary> Cylinder with caps. Used for penstocks, masts, bushings. </summary>
        private static Mesh CreateCylinderMesh(float radius, float height, int segments)
        {
            Mesh m = new Mesh();
            int vCount = 2 + segments * 2;
            var verts = new Vector3[vCount];
            var tris  = new List<int>();
            float halfH = height / 2f;

            // Pole vertices
            verts[0] = new Vector3(0, halfH, 0);   // top centre
            verts[1] = new Vector3(0, -halfH, 0);  // bottom centre

            for (int i = 0; i < segments; i++)
            {
                float angle = (i * 2f * Mathf.PI) / segments;
                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                verts[2 + i * 2]     = new Vector3(x, halfH, z);
                verts[2 + i * 2 + 1] = new Vector3(x, -halfH, z);
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                int tl = 2 + i * 2, bl = 2 + i * 2 + 1;
                int tr = 2 + next * 2, br = 2 + next * 2 + 1;

                // Top cap
                tris.Add(0); tris.Add(tr); tris.Add(tl);
                // Bottom cap
                tris.Add(1); tris.Add(bl); tris.Add(br);
                // Side quad
                tris.Add(tl); tris.Add(bl); tris.Add(tr);
                tris.Add(tr); tris.Add(bl); tris.Add(br);
            }

            m.vertices = verts;
            m.triangles = tris.ToArray();
            m.RecalculateNormals();
            return m;
        }
    }
}

#endif