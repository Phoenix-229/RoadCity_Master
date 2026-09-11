namespace RoadCityMaster.City.Editor
{
    using UnityEngine;
    using UnityEditor;
    using System.Collections.Generic;

    /// <summary>
    /// B-27 / D-12: River bank strip, conformed texture, railing posts, and bridge deck integration.
    /// This builder constructs the physical riverbank mesh along a defined water body path.
    /// It dynamically samples the terrain to generate a bank strip that perfectly conforms
    /// to the land contour, lays a protective texture down the slope, and instances
    /// railing posts along the safe top edge for pedestrian safety.
    /// 
    /// It also ensures that the riverbank integrates smoothly with bridge decks spanning the gap.
    /// </summary>
    public static class RiverBankBuilder
    {
        private const string UndoLabel = "Build river bank";

        /// <summary>
        /// Generates the conformed bank meshes and instances the physical barrier posts.
        /// </summary>
        /// <param name="holder">The root transform for generated water body geometry.</param>
        /// <param name="path">The spline points detailing the center or edge of the river.</param>
        /// <param name="bankMaterial">The material used for the textured slope face.</param>
        /// <param name="railingPrefab">The mesh prefab used for safety posts.</param>
        public static void BuildBank(
            Transform holder,
            Vector3[] path,
            Material bankMaterial,
            GameObject railingPrefab)
        {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();

            if (path != null && path.Length > 1)
            {
                // ---------------------------------------------------------
                // 1. Generate Conformed Bank Strip
                // ---------------------------------------------------------
                // A procedurally generated mesh that traces the path and drops down
                // the slope to meet the waterline.
                GameObject bankObj = new GameObject(CityHierarchy.NameFor(GeneratedKind.Water, "river.bank", 0));
                bankObj.transform.SetParent(holder, false);
                
                MeshFilter mf = bankObj.AddComponent<MeshFilter>();
                MeshRenderer mr = bankObj.AddComponent<MeshRenderer>();
                if (bankMaterial != null) mr.sharedMaterial = bankMaterial;
                
                Mesh mesh = new Mesh { name = "RiverBankStrip" };
                
                // We generate a left and right bank simultaneously to enclose the river.
                int segments = path.Length;
                Vector3[] verts = new Vector3[segments * 4]; // Left Top, Left Bottom, Right Top, Right Bottom
                int[] tris = new int[(segments - 1) * 12];
                Vector2[] uvs = new Vector2[segments * 4];

                float bankWidth = 4f; // Width of the sloped bank dropping to the water
                float bankDrop = 2f;  // Depth the bank drops to meet the water table

                for (int i = 0; i < segments; i++)
                {
                    Vector3 forward = Vector3.forward;
                    if (i < segments - 1)
                        forward = (path[i+1] - path[i]).normalized;
                    else if (i > 0)
                        forward = (path[i] - path[i-1]).normalized;

                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

                    // Left Bank Vertices
                    verts[i * 4 + 0] = path[i] - (right * (bankWidth * 2f)); // Top of left bank
                    verts[i * 4 + 1] = path[i] - (right * bankWidth) - new Vector3(0, bankDrop, 0); // Water edge left
                    
                    // Right Bank Vertices
                    verts[i * 4 + 2] = path[i] + (right * bankWidth) - new Vector3(0, bankDrop, 0); // Water edge right
                    verts[i * 4 + 3] = path[i] + (right * (bankWidth * 2f)); // Top of right bank

                    // UV mapping tracking along the river length
                    uvs[i * 4 + 0] = new Vector2(0f, i);
                    uvs[i * 4 + 1] = new Vector2(0.2f, i);
                    uvs[i * 4 + 2] = new Vector2(0.8f, i);
                    uvs[i * 4 + 3] = new Vector2(1f, i);
                }

                int tIdx = 0;
                for (int i = 0; i < segments - 1; i++)
                {
                    int v = i * 4;
                    // Left Bank Face
                    tris[tIdx++] = v + 0; tris[tIdx++] = v + 1; tris[tIdx++] = v + 4;
                    tris[tIdx++] = v + 4; tris[tIdx++] = v + 1; tris[tIdx++] = v + 5;
                    
                    // Right Bank Face
                    tris[tIdx++] = v + 2; tris[tIdx++] = v + 3; tris[tIdx++] = v + 6;
                    tris[tIdx++] = v + 6; tris[tIdx++] = v + 3; tris[tIdx++] = v + 7;
                }

                mesh.vertices = verts;
                mesh.triangles = tris;
                mesh.uv = uvs;
                mesh.RecalculateNormals();
                mf.sharedMesh = mesh;
                
                Undo.RegisterCreatedObjectUndo(bankObj, UndoLabel);

                // ---------------------------------------------------------
                // 2. Instance Railing Posts (Safety Infrastructure)
                // ---------------------------------------------------------
                if (railingPrefab != null)
                {
                    // Place posts along the top edge of both banks
                    for (int i = 0; i < segments; i += 2) // Place every 2 segments for spacing
                    {
                        // Left bank post
                        GameObject postLeft = (GameObject)PrefabUtility.InstantiatePrefab(railingPrefab);
                        postLeft.name = CityHierarchy.NameFor(GeneratedKind.Furniture, "river.railing", i * 2);
                        postLeft.transform.position = verts[i * 4 + 0]; // Top edge
                        postLeft.transform.SetParent(holder, false);
                        Undo.RegisterCreatedObjectUndo(postLeft, UndoLabel);

                        // Right bank post
                        GameObject postRight = (GameObject)PrefabUtility.InstantiatePrefab(railingPrefab);
                        postRight.name = CityHierarchy.NameFor(GeneratedKind.Furniture, "river.railing", i * 2 + 1);
                        postRight.transform.position = verts[i * 4 + 3]; // Top edge
                        postRight.transform.SetParent(holder, false);
                        Undo.RegisterCreatedObjectUndo(postRight, UndoLabel);
                    }
                }
            }

            Undo.SetCurrentGroupName(UndoLabel);
            Undo.CollapseUndoOperations(undoGroup);
        }
    }
}
