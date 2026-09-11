// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR

namespace RoadCityMaster.City.Editor.Inspectors
{
    using UnityEngine;
    using UnityEditor;
    using RoadCityMaster.City;

    /// <summary>
    /// B-27 / D-13: Custom Inspector for DamAuthoring.
    ///
    /// Presents the dam's structural, hydraulic, and electrical parameters in logically
    /// grouped foldouts with real-time safety factor validation. Build/Clear buttons
    /// trigger DamBodyMeshes procedural generation.
    ///
    /// Shows the computed DamRequest as read-only measured figures:
    ///   - Reservoir surface level
    ///   - Structural height and base width
    ///   - Spillway and outlet discharge (Q)
    ///   - Sliding and overturning safety factors
    /// </summary>
    [CustomEditor(typeof(DamAuthoring))]
    public class DamAuthoringEditor : Editor
    {
        // Foldout state
        private bool foldGeometry = true;
        private bool foldSpillway = true;
        private bool foldOutlet = true;
        private bool foldPowerhouse = true;
        private bool foldElectrical = true;
        private bool foldGallery = true;
        private bool foldAppearance = true;
        private bool foldDiagnostics = false;

        // Serialised property caches — bound in OnEnable
        private SerializedProperty propCrestLevel, propCrestLength, propCrestWidth;
        private SerializedProperty propUpstreamSlope, propDownstreamSlope, propFreeboard, propMonolithWidth;
        private SerializedProperty propSpillwayType, propSpillwayCrest, propSpillwayWidth;
        private SerializedProperty propOutletInvert, propOutletArea;
        private SerializedProperty propHasPowerhouse, propPhWidth, propPhHeight, propPhDepth;
        private SerializedProperty propTurbineCount, propPenstockRadius;
        private SerializedProperty propIncludeSwitchyard, propTransformerCount, propTowerCount;
        private SerializedProperty propIncludeGalleries, propGalleryWidth, propGalleryHeight;
        private SerializedProperty propBodyPrefab, propBodyMat, propCrestMat, propPhMat;
        private SerializedProperty propTurbineMat, propGalleryMat, propMetalMat, propSpillwayMat;
        private SerializedProperty propDownstreamDepth;

        private void OnEnable()
        {
            propCrestLevel     = serializedObject.FindProperty("crestLevelMetres");
            propCrestLength    = serializedObject.FindProperty("crestLengthMetres");
            propCrestWidth     = serializedObject.FindProperty("crestWidthMetres");
            propUpstreamSlope  = serializedObject.FindProperty("upstreamFaceSlope");
            propDownstreamSlope= serializedObject.FindProperty("downstreamFaceSlope");
            propFreeboard      = serializedObject.FindProperty("freeboardMetres");
            propMonolithWidth  = serializedObject.FindProperty("monolithWidthMetres");

            propSpillwayType   = serializedObject.FindProperty("spillwayType");
            propSpillwayCrest  = serializedObject.FindProperty("spillwayCrestMetres");
            propSpillwayWidth  = serializedObject.FindProperty("spillwayWidthMetres");

            propOutletInvert   = serializedObject.FindProperty("outletInvertMetres");
            propOutletArea     = serializedObject.FindProperty("outletAreaSqMetres");

            propHasPowerhouse  = serializedObject.FindProperty("hasPowerhouse");
            propPhWidth        = serializedObject.FindProperty("powerhouseWidthMetres");
            propPhHeight       = serializedObject.FindProperty("powerhouseHeightMetres");
            propPhDepth        = serializedObject.FindProperty("powerhouseDepthMetres");
            propTurbineCount   = serializedObject.FindProperty("turbineCount");
            propPenstockRadius = serializedObject.FindProperty("penstockRadiusMetres");

            propIncludeSwitchyard = serializedObject.FindProperty("includeSwitchyard");
            propTransformerCount  = serializedObject.FindProperty("transformerCount");
            propTowerCount        = serializedObject.FindProperty("transmissionTowerCount");

            propIncludeGalleries = serializedObject.FindProperty("includeInspectionGalleries");
            propGalleryWidth     = serializedObject.FindProperty("galleryWidthMetres");
            propGalleryHeight    = serializedObject.FindProperty("galleryHeightMetres");

            propBodyPrefab     = serializedObject.FindProperty("bodyPrefab");
            propBodyMat        = serializedObject.FindProperty("bodyMaterial");
            propCrestMat       = serializedObject.FindProperty("crestMaterial");
            propPhMat          = serializedObject.FindProperty("powerhouseMaterial");
            propTurbineMat     = serializedObject.FindProperty("turbineMaterial");
            propGalleryMat     = serializedObject.FindProperty("galleryMaterial");
            propMetalMat       = serializedObject.FindProperty("metalGridMaterial");
            propSpillwayMat    = serializedObject.FindProperty("spillwayMaterial");
            propDownstreamDepth= serializedObject.FindProperty("downstreamWaterDepthMetres");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DamAuthoring dam = (DamAuthoring)target;

            // ---- BUILD / CLEAR BUTTONS ----
            EditorGUILayout.Space(4);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.backgroundColor = dam.hasBuiltMeshes ? new Color(1f, 0.6f, 0.6f) : new Color(0.6f, 1f, 0.6f);
                if (GUILayout.Button(dam.hasBuiltMeshes ? "Rebuild Dam" : "Build Dam", GUILayout.Height(28)))
                    DamBodyMeshes.BuildDam(dam);
                GUI.backgroundColor = Color.white;

                using (new EditorGUI.DisabledGroupScope(!dam.hasBuiltMeshes))
                {
                    if (GUILayout.Button("Clear Dam", GUILayout.Height(28)))
                        DamBodyMeshes.ClearDam(dam);
                }
            }
            EditorGUILayout.Space(4);

            // ---- DAM GEOMETRY ----
            foldGeometry = EditorGUILayout.BeginFoldoutHeaderGroup(foldGeometry, "Dam Geometry");
            if (foldGeometry)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propCrestLevel);
                EditorGUILayout.PropertyField(propCrestLength);
                EditorGUILayout.PropertyField(propCrestWidth);
                EditorGUILayout.PropertyField(propUpstreamSlope);
                EditorGUILayout.PropertyField(propDownstreamSlope);
                EditorGUILayout.PropertyField(propFreeboard);
                EditorGUILayout.PropertyField(propMonolithWidth);

                float H = dam.EstimateStructuralHeight();
                float baseW = dam.CalculateBaseWidth(H);
                int monoliths = Mathf.Max(1, Mathf.RoundToInt(dam.crestLengthMetres / dam.monolithWidthMetres));
                EditorGUILayout.HelpBox(
                    $"Structural height ≈ {H:F1} m  |  Base width = {baseW:F1} m  |  Monoliths = {monoliths}",
                    MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- SPILLWAY ----
            foldSpillway = EditorGUILayout.BeginFoldoutHeaderGroup(foldSpillway, "Spillway");
            if (foldSpillway)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propSpillwayType);
                EditorGUILayout.PropertyField(propSpillwayCrest);
                EditorGUILayout.PropertyField(propSpillwayWidth);

                float head = dam.crestLevelMetres - dam.freeboardMetres - dam.spillwayCrestMetres;
                float Q = dam.CalculateSpillwayDischarge(head);
                EditorGUILayout.HelpBox($"Head over spillway = {head:F1} m  |  Q = {Q:F1} m³/s", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- OUTLET ----
            foldOutlet = EditorGUILayout.BeginFoldoutHeaderGroup(foldOutlet, "Outlet Works");
            if (foldOutlet)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propOutletInvert);
                EditorGUILayout.PropertyField(propOutletArea);

                float headOutlet = dam.crestLevelMetres - dam.freeboardMetres - dam.outletInvertMetres;
                float Qo = dam.CalculateOutletDischarge(headOutlet);
                EditorGUILayout.HelpBox($"Head above outlet = {headOutlet:F1} m  |  Q = {Qo:F1} m³/s", MessageType.Info);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- POWERHOUSE ----
            foldPowerhouse = EditorGUILayout.BeginFoldoutHeaderGroup(foldPowerhouse, "Powerhouse");
            if (foldPowerhouse)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propHasPowerhouse);
                if (dam.hasPowerhouse)
                {
                    EditorGUILayout.PropertyField(propPhWidth);
                    EditorGUILayout.PropertyField(propPhHeight);
                    EditorGUILayout.PropertyField(propPhDepth);
                    EditorGUILayout.PropertyField(propTurbineCount);
                    EditorGUILayout.PropertyField(propPenstockRadius);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- ELECTRICAL GRID ----
            if (dam.hasPowerhouse)
            {
                foldElectrical = EditorGUILayout.BeginFoldoutHeaderGroup(foldElectrical, "Electrical Grid");
                if (foldElectrical)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(propIncludeSwitchyard);
                    if (dam.includeSwitchyard)
                    {
                        EditorGUILayout.PropertyField(propTransformerCount);
                        EditorGUILayout.PropertyField(propTowerCount);
                    }
                    EditorGUI.indentLevel--;
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            // ---- INSPECTION GALLERIES ----
            foldGallery = EditorGUILayout.BeginFoldoutHeaderGroup(foldGallery, "Inspection Galleries");
            if (foldGallery)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propIncludeGalleries);
                if (dam.includeInspectionGalleries)
                {
                    EditorGUILayout.PropertyField(propGalleryWidth);
                    EditorGUILayout.PropertyField(propGalleryHeight);
                }
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- APPEARANCE ----
            foldAppearance = EditorGUILayout.BeginFoldoutHeaderGroup(foldAppearance, "Appearance & Materials");
            if (foldAppearance)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(propBodyPrefab);
                EditorGUILayout.PropertyField(propBodyMat);
                EditorGUILayout.PropertyField(propCrestMat);
                EditorGUILayout.PropertyField(propPhMat);
                EditorGUILayout.PropertyField(propTurbineMat);
                EditorGUILayout.PropertyField(propGalleryMat);
                EditorGUILayout.PropertyField(propMetalMat);
                EditorGUILayout.PropertyField(propSpillwayMat);
                EditorGUILayout.PropertyField(propDownstreamDepth);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            // ---- DIAGNOSTICS ----
            foldDiagnostics = EditorGUILayout.BeginFoldoutHeaderGroup(foldDiagnostics, "Structural Diagnostics (Read-Only)");
            if (foldDiagnostics)
            {
                EditorGUI.indentLevel++;
                float H = dam.EstimateStructuralHeight();
                float baseW = dam.CalculateBaseWidth(H);
                float mass = dam.CalculateConcreteMass(H);
                float slideFoS = dam.CalculateSlidingSafetyFactor(H, 45f, 500000f);
                float overturnFoS = dam.CalculateOverturningSafetyFactor(H);

                EditorGUILayout.LabelField("Structural Height", $"{H:F1} m");
                EditorGUILayout.LabelField("Base Width", $"{baseW:F1} m");
                EditorGUILayout.LabelField("Concrete Volume", $"{dam.CalculateConcreteVolume(H):N0} m³");
                EditorGUILayout.LabelField("Concrete Mass", $"{mass:N0} kg ({mass / 1e6f:F2} Mt)");
                EditorGUILayout.LabelField("Sliding FoS (φ=45°, c=500kPa)", $"{slideFoS:F2}");
                EditorGUILayout.LabelField("Overturning FoS", $"{overturnFoS:F2}");

                if (slideFoS < 1.0f)
                    EditorGUILayout.HelpBox("CRITICAL: Sliding FoS < 1.0 — dam will fail!", MessageType.Error);
                else if (slideFoS < 1.5f)
                    EditorGUILayout.HelpBox("WARNING: Sliding FoS below 1.5 minimum (USBR).", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Sliding stability: SAFE", MessageType.Info);

                if (!dam.lastRequest.resolved)
                    EditorGUILayout.HelpBox("Until Universe Creator's update runs, this dam stands in an unchanged river.", MessageType.None);

                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            serializedObject.ApplyModifiedProperties();
        }
    }
}

#endif