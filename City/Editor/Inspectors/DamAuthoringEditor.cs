namespace RoadCityMaster.City.Editor.Inspectors
{
    using UnityEngine;
    using UnityEditor;
    using RoadCityMaster.City.Runtime.Network;

    /// <summary>
    /// B-27 / D-13: Advanced Dam Authoring Inspector.
    /// Exposes a massive suite of real-world physics and architectural controls.
    /// Allows fine-tuning of internal walk-in engineering galleries, monolithic pouring partitions,
    /// and the high-voltage electrical grid connection substation.
    /// </summary>
    [CustomEditor(typeof(DamAuthoring))]
    public class DamAuthoringEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DamAuthoring dam = (DamAuthoring)target;

            EditorGUI.BeginChangeCheck();

            // -------------------------------------------------------------
            // SECTION 1: GLOBAL TOPOGRAPHY
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Topography & Siting", EditorStyles.boldLabel);
            dam.crestLength = EditorGUILayout.FloatField(new GUIContent("Crest Length", "Length across the valley (m)"), dam.crestLength);
            dam.crestWidth = EditorGUILayout.FloatField(new GUIContent("Crest Width", "Width of the top of the dam. Must fit road networks (m)"), dam.crestWidth);
            dam.damHeight = EditorGUILayout.FloatField(new GUIContent("Dam Height", "Total vertical height from bedrock (m)"), dam.damHeight);

            // -------------------------------------------------------------
            // SECTION 2: STRUCTURAL PHYSICS & MONOLITHS
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Structural Profiles (Gravity Blocks)", EditorStyles.boldLabel);
            dam.upstreamSlope = EditorGUILayout.Slider(new GUIContent("Upstream Slope", "H:V ratio contacting the reservoir"), dam.upstreamSlope, 0f, 0.3f);
            dam.downstreamSlope = EditorGUILayout.Slider(new GUIContent("Downstream Slope", "H:V ratio forming the air face"), dam.downstreamSlope, 0.5f, 1.5f);
            dam.monolithWidth = EditorGUILayout.Slider(new GUIContent("Monolith Width", "Partition width for contraction joints"), dam.monolithWidth, 10f, 30f);

            // -------------------------------------------------------------
            // SECTION 3: POWERHOUSE & ELECTRICAL GRID
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Power Generation Complex", EditorStyles.boldLabel);
            dam.hasPowerhouse = EditorGUILayout.Toggle("Include Powerhouse", dam.hasPowerhouse);
            if (dam.hasPowerhouse)
            {
                EditorGUI.indentLevel++;
                dam.powerhouseWidth = EditorGUILayout.FloatField("Building Width", dam.powerhouseWidth);
                dam.powerhouseHeight = EditorGUILayout.FloatField("Building Height", dam.powerhouseHeight);
                dam.powerhouseDepth = EditorGUILayout.FloatField("Building Depth", dam.powerhouseDepth);
                dam.turbineCount = EditorGUILayout.IntSlider("Turbine Units", dam.turbineCount, 1, 12);
                dam.penstockRadius = EditorGUILayout.Slider("Penstock Pipe Radius", dam.penstockRadius, 1f, 5f);
                
                EditorGUILayout.Space();
                dam.includeSwitchyard = EditorGUILayout.Toggle("Build Switchyard (High Grid)", dam.includeSwitchyard);
                if (dam.includeSwitchyard)
                {
                    dam.transformerCount = EditorGUILayout.IntSlider("Transformer Units", dam.transformerCount, 1, 10);
                    dam.transmissionTowerCount = EditorGUILayout.IntSlider("Roof Transmission Towers", dam.transmissionTowerCount, 1, 6);
                }
                EditorGUI.indentLevel--;
            }

            // -------------------------------------------------------------
            // SECTION 4: ENGINEERING GALLERIES (WALK-IN INTERIORS)
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Engineering Interiors", EditorStyles.boldLabel);
            dam.includeWalkableGalleries = EditorGUILayout.Toggle("Carve Walk-In Galleries", dam.includeWalkableGalleries);
            if (dam.includeWalkableGalleries)
            {
                EditorGUI.indentLevel++;
                dam.galleryWidth = EditorGUILayout.FloatField("Door/Gallery Width", dam.galleryWidth);
                dam.galleryHeight = EditorGUILayout.FloatField("Door/Gallery Height", dam.galleryHeight);
                EditorGUI.indentLevel--;
            }

            // -------------------------------------------------------------
            // SECTION 5: HYDRAULICS
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Hydraulics & Spillway", EditorStyles.boldLabel);
            dam.spillwayWidthPercentage = EditorGUILayout.Slider("Spillway % of Crest", dam.spillwayWidthPercentage, 0.1f, 0.5f);
            dam.spillwayGateDepth = EditorGUILayout.FloatField("Radial Gate Depth", dam.spillwayGateDepth);

            // -------------------------------------------------------------
            // SECTION 6: MATERIALS
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Procedural Materials", EditorStyles.boldLabel);
            dam.bodyMaterial = (Material)EditorGUILayout.ObjectField("Concrete Body", dam.bodyMaterial, typeof(Material), false);
            dam.powerhouseMaterial = (Material)EditorGUILayout.ObjectField("Powerhouse Walls", dam.powerhouseMaterial, typeof(Material), false);
            dam.roofMaterial = (Material)EditorGUILayout.ObjectField("Roof Surfaces", dam.roofMaterial, typeof(Material), false);
            dam.spillwayMaterial = (Material)EditorGUILayout.ObjectField("Spillway Flow", dam.spillwayMaterial, typeof(Material), false);
            dam.turbineMaterial = (Material)EditorGUILayout.ObjectField("Turbines", dam.turbineMaterial, typeof(Material), false);
            dam.galleryMaterial = (Material)EditorGUILayout.ObjectField("Gallery Hollow", dam.galleryMaterial, typeof(Material), false);
            dam.metalGridMaterial = (Material)EditorGUILayout.ObjectField("High Grid / Metal", dam.metalGridMaterial, typeof(Material), false);

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(dam);
            }

            // -------------------------------------------------------------
            // REAL-WORLD STRUCTURAL CHECKS
            // -------------------------------------------------------------
            EditorGUILayout.Space();
            float baseW = dam.CalculateBaseWidth();
            float fosOvt = dam.CalculateFactorOfSafetyOverturning();
            float fosSld = dam.CalculateFactorOfSafetySliding();

            EditorGUILayout.HelpBox($"Calculated Base Width: {baseW:F1} m\n" +
                                    $"Factor of Safety (Overturning): {fosOvt:F2}\n" +
                                    $"Factor of Safety (Sliding): {fosSld:F2}", MessageType.Info);

            if (fosOvt < 1.5f || fosSld < 1.0f)
            {
                EditorGUILayout.HelpBox("CRITICAL WARNING: Structural Factor of Safety is below safe real-world limits. The dam may fail hydrostatically. Increase downstream slope or crest width.", MessageType.Error);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Execute Massive Real-World Mesh Generation", GUILayout.Height(40)))
            {
                DamBodyMeshes.BuildDam(dam);
            }
            
            if (GUILayout.Button("Clear Procedural Geometry", GUILayout.Height(30)))
            {
                DamBodyMeshes.ClearDam(dam);
            }
        }
    }
}
