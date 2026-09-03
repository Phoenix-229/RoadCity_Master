#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(Road))]
    public class RoadEditor : Editor
    {
        private static string[] RoadMaterialDropdownEnumDesc = new string[]{
        "Asphalt",
        "Dirt",
        "Brick",
        "Cobblestone"
    };


        #region "Vars"
        private Road road;

        #region "Serialized properties:"
        private SerializedProperty isGizmosEnabled;
        private SerializedProperty lanesForwardProp;
        private SerializedProperty lanesBackwardProp;
        private SerializedProperty laneWidth;
        private SerializedProperty isShouldersEnabled;
        private SerializedProperty shoulderWidth;
        private SerializedProperty isUsingDefaultMaterials;
        private SerializedProperty isMaxGradeEnabled;
        private SerializedProperty maxGrade;
        private SerializedProperty isUsingMultithreading;
        private SerializedProperty isSavingMeshes;
        private SerializedProperty matchTerrainSubtraction;
        private SerializedProperty magnitudeThreshold;
        private SerializedProperty isHeightModificationEnabled;
        private SerializedProperty isDetailModificationEnabled;
        private SerializedProperty isTreeModificationEnabled;
        private SerializedProperty matchHeightsDistance;
        private SerializedProperty clearDetailsDistance;
        private SerializedProperty clearDetailsDistanceHeight;
        private SerializedProperty clearTreesDistance;
        private SerializedProperty clearTreesDistanceHeight;
        private SerializedProperty isSavingTerrainHistoryOnDisk;
        private SerializedProperty isRoadCutsEnabled;
        private SerializedProperty isDynamicCutsEnabled;
        private SerializedProperty isShoulderCutsEnabled;
        private SerializedProperty isEditorCameraRotated;
        private SerializedProperty editorCameraMetersPerSecond;
        private SerializedProperty isUsingMeshColliders;
        private SerializedProperty roadMaterialDropdown;
        private SerializedProperty isStatic;
        private SerializedProperty isLightmapped;
        private SerializedProperty desiredRampHeight;

        private SerializedProperty roadMaterial1;
        private SerializedProperty roadMaterial2;
        private SerializedProperty roadMaterial3;
        private SerializedProperty roadMaterial4;
        private SerializedProperty roadMaterialMarker1;
        private SerializedProperty roadMaterialMarker2;
        private SerializedProperty roadMaterialMarker3;
        private SerializedProperty roadMaterialMarker4;
        private SerializedProperty shoulderMaterial1;
        private SerializedProperty shoulderMaterial2;
        private SerializedProperty shoulderMaterial3;
        private SerializedProperty shoulderMaterial4;
        private SerializedProperty shoulderMaterialMarker1;
        private SerializedProperty shoulderMaterialMarker2;
        private SerializedProperty shoulderMaterialMarker3;
        private SerializedProperty shoulderMaterialMarker4;
        private SerializedProperty roadPhysicMaterial;
        private SerializedProperty shoulderPhysicMaterial;
        #endregion

        //Editor only variables:
        private string status = "Show help";
        private const string onlineHelpDesc = "Visit the online manual for the most effective help.";
        private bool isShowingCutsHelp = false;
        private bool isShowingMaterialsHelp = false;
        private bool isShowingRoadHelp = false;
        private bool isShowingTerrainHelp = false;
        private bool isShowingCameraHelp = false;
        private bool isResetingTH = false;
        private bool isInitialized = false;

        /// <summary> RM-102. The registry fields this inspector draws at its own position rather than letting the
        /// foldouts draw them, and therefore the ones the foldouts must leave alone -- otherwise one value would sit
        /// under two controls in the same inspector. Lane width belongs beside the road-width readout it feeds;
        /// shoulder width and max grade belong beside the toggles that decide whether they apply; the terrain match
        /// drop belongs in the terrain box with the rest of the terrain controls. </summary>
        private static readonly string[] drawnInPlace = new string[]
        {
            "laneWidth", "shoulderWidth", "maxGrade", "matchTerrainSubtraction"
        };

        /// <summary> The reset-to-default icon buttons, held as delegates rather than built on each call: an inspector
        /// repaints on mouse movement, and a fresh delegate per field per repaint is garbage an author pays for as
        /// stutter while dragging. Bound once in <see cref="OnEnable"/>. </summary>
        private System.Action resetLaneWidth;
        private System.Action resetShoulderWidth;
        private System.Action resetMaxGrade;
        private System.Action resetTerrainSubtraction;

        private GUIStyle warningLabelStyle;
        private GUIStyle loadButton = null;
        private GUIStyle imageButton = null;
        private GUIStyle maybeButton = null;
        private Texture2D warningLabelBG;
        private Texture2D loadBtnBG = null;
        private Texture2D loadBtnBGGlow = null;
        private Texture refreshButtonTexture = null;
        private Texture deleteButtonText = null;
        private Texture refreshButtonTextureReal = null;

        //Buffers:
        private bool isNeedingRoadUpdate = false;
        private bool isApplyingMaterialsCheck = false;
        private bool isApplyingMatsCheck = false;
        #endregion


        private void OnEnable()
        {
            road = (Road)target;

            resetLaneWidth = ResetLaneWidth;
            resetShoulderWidth = ResetShoulderWidth;
            resetMaxGrade = ResetMaxGrade;
            resetTerrainSubtraction = ResetTerrainSubtraction;

            isGizmosEnabled = serializedObject.FindProperty("isGizmosEnabled");
            lanesForwardProp = serializedObject.FindProperty("lanesForward");
            lanesBackwardProp = serializedObject.FindProperty("lanesBackward");
            laneWidth = serializedObject.FindProperty("laneWidth");
            isShouldersEnabled = serializedObject.FindProperty("isShouldersEnabled");
            shoulderWidth = serializedObject.FindProperty("shoulderWidth");
            isUsingDefaultMaterials = serializedObject.FindProperty("isUsingDefaultMaterials");
            isMaxGradeEnabled = serializedObject.FindProperty("isMaxGradeEnabled");
            maxGrade = serializedObject.FindProperty("maxGrade");
            isUsingMultithreading = serializedObject.FindProperty("isUsingMultithreading");
            isSavingMeshes = serializedObject.FindProperty("isSavingMeshes");
            matchTerrainSubtraction = serializedObject.FindProperty("matchTerrainSubtraction");
            magnitudeThreshold = serializedObject.FindProperty("magnitudeThreshold");
            isHeightModificationEnabled = serializedObject.FindProperty("isHeightModificationEnabled");
            isDetailModificationEnabled = serializedObject.FindProperty("isDetailModificationEnabled");
            isTreeModificationEnabled = serializedObject.FindProperty("isTreeModificationEnabled");
            matchHeightsDistance = serializedObject.FindProperty("matchHeightsDistance");
            clearDetailsDistance = serializedObject.FindProperty("clearDetailsDistance");
            clearDetailsDistanceHeight = serializedObject.FindProperty("clearDetailsDistanceHeight");
            clearTreesDistance = serializedObject.FindProperty("clearTreesDistance");
            clearTreesDistanceHeight = serializedObject.FindProperty("clearTreesDistanceHeight");
            isSavingTerrainHistoryOnDisk = serializedObject.FindProperty("isSavingTerrainHistoryOnDisk");
            isRoadCutsEnabled = serializedObject.FindProperty("isRoadCutsEnabled");
            isDynamicCutsEnabled = serializedObject.FindProperty("isDynamicCutsEnabled");
            isShoulderCutsEnabled = serializedObject.FindProperty("isShoulderCutsEnabled");
            isEditorCameraRotated = serializedObject.FindProperty("isEditorCameraRotated");
            editorCameraMetersPerSecond = serializedObject.FindProperty("EditorCameraMetersPerSecond");
            isUsingMeshColliders = serializedObject.FindProperty("isUsingMeshColliders");
            roadMaterialDropdown = serializedObject.FindProperty("roadMaterialDropdown");
            isStatic = serializedObject.FindProperty("isStatic");
            isLightmapped = serializedObject.FindProperty("isLightmapped");
            desiredRampHeight = serializedObject.FindProperty("desiredRampHeight");

            roadMaterial1 = serializedObject.FindProperty("RoadMaterial1");
            roadMaterial2 = serializedObject.FindProperty("RoadMaterial2");
            roadMaterial3 = serializedObject.FindProperty("RoadMaterial3");
            roadMaterial4 = serializedObject.FindProperty("RoadMaterial4");
            roadMaterialMarker1 = serializedObject.FindProperty("RoadMaterialMarker1");
            roadMaterialMarker2 = serializedObject.FindProperty("RoadMaterialMarker2");
            roadMaterialMarker3 = serializedObject.FindProperty("RoadMaterialMarker3");
            roadMaterialMarker4 = serializedObject.FindProperty("RoadMaterialMarker4");
            shoulderMaterial1 = serializedObject.FindProperty("ShoulderMaterial1");
            shoulderMaterial2 = serializedObject.FindProperty("ShoulderMaterial2");
            shoulderMaterial3 = serializedObject.FindProperty("ShoulderMaterial3");
            shoulderMaterial4 = serializedObject.FindProperty("ShoulderMaterial4");
            shoulderMaterialMarker1 = serializedObject.FindProperty("ShoulderMaterialMarker1");
            shoulderMaterialMarker2 = serializedObject.FindProperty("ShoulderMaterialMarker2");
            shoulderMaterialMarker3 = serializedObject.FindProperty("ShoulderMaterialMarker3");
            shoulderMaterialMarker4 = serializedObject.FindProperty("ShoulderMaterialMarker4");
            roadPhysicMaterial = serializedObject.FindProperty("RoadPhysicMaterial");
            shoulderPhysicMaterial = serializedObject.FindProperty("ShoulderPhysicMaterial");
        }


        private void Init()
        {
            isInitialized = true;
            string basePath = RoadEditorUtility.GetBasePath();

            EditorUtilities.LoadTexture(ref refreshButtonTexture, basePath + "/Editor/Icons/refresh2.png");
            EditorUtilities.LoadTexture(ref refreshButtonTextureReal, basePath + "/Editor/Icons/refresh.png");
            EditorUtilities.LoadTexture(ref deleteButtonText, basePath + "/Editor/Icons/delete.png");
            EditorUtilities.LoadTexture(ref warningLabelBG, basePath + "/Editor/Icons/WarningLabelBG.png");
            EditorUtilities.LoadTexture(ref loadBtnBG, basePath + "/Editor/Icons/otherbg.png");
            EditorUtilities.LoadTexture(ref loadBtnBGGlow, basePath + "/Editor/Icons/otherbg2.png");

            if (warningLabelStyle == null)
            {
                warningLabelStyle = new GUIStyle(GUI.skin.textArea);
                warningLabelStyle.normal.textColor = Color.red;
                warningLabelStyle.active.textColor = Color.red;
                warningLabelStyle.hover.textColor = Color.red;
                warningLabelStyle.normal.background = warningLabelBG;
                warningLabelStyle.active.background = warningLabelBG;
                warningLabelStyle.hover.background = warningLabelBG;
                warningLabelStyle.padding = new RectOffset(8, 8, 8, 8);
            }

            if (imageButton == null)
            {
                imageButton = new GUIStyle(GUI.skin.button);
                imageButton.contentOffset = new Vector2(0f, 0f);
                imageButton.border = new RectOffset(0, 0, 0, 0);
                imageButton.fixedHeight = 16f;
                imageButton.padding = new RectOffset(0, 0, 0, 0);
                imageButton.normal.background = null;
            }

            if (loadButton == null)
            {
                loadButton = new GUIStyle(GUI.skin.button);
                loadButton.contentOffset = new Vector2(0f, 1f);
                loadButton.normal.textColor = new Color(1f, 1f, 1f, 1f);
                loadButton.normal.background = loadBtnBG;
                loadButton.active.background = loadBtnBGGlow;
                loadButton.focused.background = loadBtnBGGlow;
                loadButton.hover.background = loadBtnBGGlow;
                loadButton.fixedHeight = 16f;
                loadButton.fixedWidth = 128f;
            }

            if (maybeButton == null)
            {
                maybeButton = new GUIStyle(GUI.skin.button);
                maybeButton.normal.textColor = new Color(0f, 0f, 0f, 1f);
            }
        }


        /// <summary> The lane TOTAL currently held in the serialised per-direction pair. The road stores the two
        /// directions separately so it knows which side of the centre line each lane is on; this reads the pair back as
        /// one number, which is all the apply path needs in order to notice that the count changed. RM-102 removed the
        /// 2 / 4 / 6 popup that used to WRITE through here, so this is now read-only in both senses. </summary>
        private int SerialisedLaneTotal()
        {
            if (lanesForwardProp == null || lanesBackwardProp == null) { return 0; }
            return LaneModel.Total(lanesForwardProp.intValue, lanesBackwardProp.intValue);
        }


        /// <summary> RM-102. The reset-to-default buttons that used to sit inline beside each raw float field, kept as
        /// they were -- an author who has typed a lane width they cannot undo needs the value put back, and the button
        /// has to be on the value's own row to read as belonging to it. What changed is only that the value beside them
        /// is now drawn through the registry's drawer, so it carries its unit and its limits.
        ///
        /// The numbers are the road's own field initialisers, which is why they live here and not in the registry: the
        /// registry records what a value is ALLOWED to be, from a cited real-world range, and that is a different
        /// question from what this tool ships as the starting point. </summary>
        private void ResetLaneWidth()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                laneWidth.floatValue = 5f;
            }
        }


        private void ResetShoulderWidth()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                shoulderWidth.floatValue = 3f;
            }
        }


        private void ResetMaxGrade()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                maxGrade.floatValue = 0.08f;
            }
        }


        private void ResetTerrainSubtraction()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                matchTerrainSubtraction.floatValue = 0.01f;
            }
        }


        public override void OnInspectorGUI()
        {
            if (Event.current.type == EventType.ValidateCommand)
            {
                switch (Event.current.commandName)
                {
                    case "UndoRedoPerformed":
                        TriggerRoadUpdate();
                        break;
                }
            }

            serializedObject.Update();

            // RM-109. B-3, B-4. The wrap this inspector's paragraphs need and a label column wide enough for its widest
            // prefix label, for this pass only. The wrap used to be set once from Init() -- once was enough to leave
            // every other window in the editor wrapped for the session, and now that the settings are handed back, once
            // is also not enough to keep OUR text wrapped, so it belongs here, per repaint. The column is what stops
            // `Gizmo new Node preview Color:` needing a second line and painting over the field below it.
            InspectorTextLayout.Apply();

            isNeedingRoadUpdate = false;
            //Graphic null checks:
            if (!isInitialized)
            {
                Init();
            }


            EditorUtilities.DrawLine();
            // RM-102. The heading was the GameObject's name alone -- "Road0" -- which tells an author nothing about
            // which tool owns the component they are looking at. Product name first, then the road.
            LegacyInspectorShell.Header(road.transform.name);
            // While this road is building: says so, and keeps every control below enabled. An author is allowed to
            // carry on editing during a build; the edit lands on the data and the queue retires the older build.
            LegacyInspectorShell.BuildLine(road.BuildOwnerId);
            if (GUILayout.Button("Update road", loadButton))
            {
                TriggerRoadUpdate();
            }


            EditorGUILayout.LabelField("Hold ctrl and click terrain to add nodes.");
            EditorGUILayout.LabelField("Hold shift and click terrain to insert nodes.");
            EditorGUILayout.LabelField("Select nodes on spline to add objects.");
            EditorGUILayout.LabelField("Road options:");


            EditorGUILayout.BeginVertical("box");
            // RM-102. The pair of buttons here used to be "Online manual" -- the upstream project's wiki, which is a
            // third party's site and documents a tool this one has diverged from -- and "Offline manual", which opens
            // ours. One manual, one button.
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            //Option: Gizmos input:
            isGizmosEnabled.boolValue = EditorGUILayout.Toggle("Gizmos: ", road.isGizmosEnabled);
            road.newNodePreviewColor = EditorGUILayout.ColorField("Gizmo new Node preview Color: ", road.newNodePreviewColor);
            road.selectedColor = EditorGUILayout.ColorField("Gizmo Selected Color: ", road.selectedColor);
            road.defaultNodeColor = EditorGUILayout.ColorField("Gizmo Default Color: ", road.defaultNodeColor);

            // RM-102. What used to be here was a "Lanes: Two / Four / Six" popup. It went, and the road's own
            // per-direction counts took its place inside the foldout below, for a reason that is about roads and not
            // about tidiness: a total of 2, 4 or 6 cannot describe a three-lane one-way, a two-plus-three climbing
            // section, or the unequal legs an auxiliary lane leaves behind, and RM-89 gave the road lanesForward and
            // lanesBackward precisely so those could be built. The popup would have kept quietly halving them.
            //
            // The foldouts are RM-51's -- what the road IS, what it LOOKS like, what it DOES to the world, what is
            // GENERATED from it -- and every field in them comes through InspectorFieldDrawer, so each shows its unit
            // and says so inline, naming the limit and its source, when a value is outside its real-world range,
            // instead of being silently clamped. Four fields are held back and drawn at their own positions below:
            // lane width beside the road-width readout it feeds, shoulder width and max grade beside the toggles that
            // decide whether they mean anything at all, and the terrain match drop with the other terrain controls.
            LegacyInspectorShell.AllGroups(serializedObject, "Road", drawnInPlace);

            // RM-109. The class popup drawn just above is what the road SAYS it is; this is what makes saying it mean
            // something. Nothing read the choice back out before, so a road could sit at Urban Two Lane with 5 m lanes
            // and a 3 m shoulder and never be contradicted.
            DrawRoadClassReconcile();

            //Option: Lane and road width:
            LegacyInspectorShell.Field(serializedObject, "Road", "laneWidth", resetLaneWidth);
            EditorGUILayout.LabelField("Road width: " + road.RoadWidth().ToString("F1") + " meters");

            //Option: Shoulders enabled:
            isShouldersEnabled.boolValue = EditorGUILayout.Toggle("Shoulders enabled:", road.isShouldersEnabled);

            //Option: Shoulders width:
            if (road.isShouldersEnabled)
            {
                LegacyInspectorShell.Field(serializedObject, "Road", "shoulderWidth", resetShoulderWidth);
            }

            // RM-102. The road definition -- metres of road per generated segment -- moved into the "what is generated
            // from it" foldout above, where it sits with the other question of that kind rather than between the
            // shoulder width and the material dropdown, neither of which it has anything to do with.

            //Dropdown:
            if (road.isUsingDefaultMaterials)
            {
                int Old = (int)road.roadMaterialDropdown;
                roadMaterialDropdown.enumValueIndex = (int)EditorGUILayout.Popup("Road material: ", (int)road.roadMaterialDropdown, RoadMaterialDropdownEnumDesc, GUILayout.Width(250f));
                if (roadMaterialDropdown.enumValueIndex != Old)
                {
                    if (roadMaterialDropdown.enumValueIndex > 0)
                    {
                        isShouldersEnabled.boolValue = false;
                    }
                    else
                    {
                        isShouldersEnabled.boolValue = true;
                    }
                }
            }

            //Option: Max grade enabled:
            isMaxGradeEnabled.boolValue = EditorGUILayout.Toggle("Max grade enforced: ", road.isMaxGradeEnabled);

            //Option: Max grade value:
            if (road.isMaxGradeEnabled)
            {
                // RM-102. This was a 0–1 slider, which let an author set a 100% grade -- a 45° road -- and said
                // nothing about it. Through the drawer it is a fraction with AASHTO's 6–12% working band and a 15%
                // hard ceiling behind it, and a value past that ceiling is reported at the field, not clamped away.
                LegacyInspectorShell.Field(serializedObject, "Road", "maxGrade", resetMaxGrade);
            }

            //Mesh colliders:
            isUsingMeshColliders.boolValue = EditorGUILayout.Toggle("Use mesh colliders: ", road.isUsingMeshColliders);
            //Option: Multi-threading option: workaround for UAS submission rules:
            isUsingMultithreading.boolValue = EditorGUILayout.Toggle("Multithreading: ", road.isUsingMultithreading);
            //Static:
            isStatic.boolValue = EditorGUILayout.Toggle("Static: ", road.isStatic);
            //Used for lightmapping:
            isLightmapped.boolValue = EditorGUILayout.Toggle("Lightmapped: ", road.isLightmapped);
            desiredRampHeight.floatValue = EditorGUILayout.FloatField("Ramp Height:", road.desiredRampHeight);

            //Option: Save meshes as unity assets options:
            isSavingMeshes.boolValue = EditorGUILayout.Toggle("Save mesh assets: ", road.isSavingMeshes);
            if (road.isSavingMeshes)
            {
                GUILayout.Label("WARNING: Saving meshes as assets is very slow and can increase road generation time by several minutes.", warningLabelStyle);
            }


            RenderRoadHelpDialog();


            EditorGUILayout.EndVertical();

            EditorGUILayout.LabelField("Terrain options:");
            EditorGUILayout.BeginVertical("box");

            //Option: Terrain subtraction:
            LegacyInspectorShell.Field(serializedObject, "Road", "matchTerrainSubtraction", resetTerrainSubtraction);

            //Option: Spline magnitude limit:
            EditorGUILayout.BeginHorizontal();
            magnitudeThreshold.floatValue = EditorGUILayout.Slider("Spline magnitude limit: ", road.magnitudeThreshold, 128f, 8192f);
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                magnitudeThreshold.floatValue = 300f;
            }
            EditorGUILayout.EndHorizontal();

            //Option: Height modification
            isHeightModificationEnabled.boolValue = EditorGUILayout.Toggle("Height modification: ", road.isHeightModificationEnabled);

            //Option: Active detail removal
            isDetailModificationEnabled.boolValue = EditorGUILayout.Toggle("Active detail removal: ", road.isDetailModificationEnabled);

            //Option: Active tree removal
            isTreeModificationEnabled.boolValue = EditorGUILayout.Toggle("Active tree removal: ", road.isTreeModificationEnabled);

            //Option: heights width
            if (road.isHeightModificationEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                matchHeightsDistance.floatValue = EditorGUILayout.Slider("Heights match width: ", road.matchHeightsDistance, 0.01f, 512f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    matchHeightsDistance.floatValue = 50f;
                }
                EditorGUILayout.EndHorizontal();
            }

            //Option: details width and height
            if (road.isDetailModificationEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                clearDetailsDistance.floatValue = EditorGUILayout.Slider("Details clear width: ", road.clearDetailsDistance, 0.01f, 512f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearDetailsDistance.floatValue = 30f;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                clearDetailsDistanceHeight.floatValue = EditorGUILayout.Slider("Details clear height: ", road.clearDetailsDistanceHeight, 0.01f, 512f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearDetailsDistanceHeight.floatValue = 5f;
                }

                EditorGUILayout.EndHorizontal();
            }

            //Option: tree widths and height
            if (road.isTreeModificationEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                clearTreesDistance.floatValue = EditorGUILayout.Slider("Trees clear width: ", road.clearTreesDistance, 0.01f, 512f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearTreesDistance.floatValue = 30f;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                clearTreesDistanceHeight.floatValue = EditorGUILayout.Slider("Trees clear height: ", road.clearTreesDistanceHeight, 0.01f, 512f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearTreesDistanceHeight.floatValue = 50f;
                }
                EditorGUILayout.EndHorizontal();
            }


            //Option: terrain history save type:
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Store terrain history separate from scene:");
            isSavingTerrainHistoryOnDisk.boolValue = EditorGUILayout.Toggle(road.isSavingTerrainHistoryOnDisk, GUILayout.Width(50f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("Terrain history size: " + road.TerrainHistoryByteSize);


            RenderTerrainHelpDialog();

 
            EditorGUILayout.EndVertical();



            GUILayout.Label("Road and shoulder splitting:");
            EditorGUILayout.BeginVertical("box");
            GUILayout.Space(4f);

            //Option: road cuts:
            if (!road.isDynamicCutsEnabled)
            {
                EditorGUILayout.BeginHorizontal();
                isRoadCutsEnabled.boolValue = EditorGUILayout.Toggle("Auto split road: ", road.isRoadCutsEnabled);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    isDynamicCutsEnabled.boolValue = false;
                    isRoadCutsEnabled.boolValue = true;
                    isShoulderCutsEnabled.boolValue = true;
                }
                EditorGUILayout.EndHorizontal();

                if (road.isShouldersEnabled)
                {
                    isShoulderCutsEnabled.boolValue = EditorGUILayout.Toggle("Auto split shoulders: ", road.isShoulderCutsEnabled);
                }
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Manual road splitting: true");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    isDynamicCutsEnabled.boolValue = false;
                    isRoadCutsEnabled.boolValue = true;
                    isShoulderCutsEnabled.boolValue = true;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.LabelField("Manual shoulder splitting: true");
            }
            isDynamicCutsEnabled.boolValue = EditorGUILayout.Toggle("Manual splitting: ", road.isDynamicCutsEnabled);


            RenderCutsHelpDialog();

 
            EditorGUILayout.EndVertical();


            //Camera:
            EditorGUILayout.LabelField("Editor camera travel:");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            //Option: Editor camera meters per sec
            editorCameraMetersPerSecond.floatValue = EditorGUILayout.Slider("Camera meters/sec:", road.EditorCameraMetersPerSecond, 1f, 512f);
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                editorCameraMetersPerSecond.floatValue = 60f;
            }
            EditorGUILayout.EndHorizontal();

            //Option: Editor camera auto rotate:
            isEditorCameraRotated.boolValue = EditorGUILayout.Toggle("Camera auto rotate: ", road.isEditorCameraRotated);
            if (road.editorPlayCamera == null)
            {
                road.EditorCameraSetSingle();
            }
            road.editorPlayCamera = (Camera)EditorGUILayout.ObjectField("Editor play camera:", road.editorPlayCamera, typeof(Camera), true);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset", GUILayout.Width(70f)))
            {
                road.QuitEditorCamera();
                road.DoEditorCameraLoop();
            }
            if (GUILayout.Button("<<", GUILayout.Width(40f)))
            {
                road.EditorCameraPos -= 0.1f;
                road.DoEditorCameraLoop();
            }
            if (road.isEditorCameraMoving == true)
            {
                if (GUILayout.Button("Pause", GUILayout.Width(70f)))
                {
                    road.isEditorCameraMoving = false;
                }
            }
            else
            {
                if (GUILayout.Button("Play", GUILayout.Width(70f)))
                {
                    road.isEditorCameraMoving = true;
                }
            }
            if (GUILayout.Button(">>", GUILayout.Width(40f)))
            {
                road.EditorCameraPos += 0.1f;
                road.DoEditorCameraLoop();
            }
            EditorGUILayout.EndHorizontal();


            RenderCameraHelpDialog();


            EditorGUILayout.EndVertical();

            GUILayout.Label("Materials:");
            EditorGUILayout.BeginVertical("box");
            //Road material defaults:


            RenderRoadMaterials();

            RenderRoadMarkerMaterials();

            RenderShoulderMaterials();

            RenderShoulderMarkerMaterials();


            //Physics materials:
            GUILayout.Label("Physics materials defaults:");
            //Option: physical road mat:
            EditorGUILayout.PropertyField(roadPhysicMaterial, new GUIContent("Road phys mat: "));


            //Option: physical shoulder mat:
            EditorGUILayout.PropertyField(shoulderPhysicMaterial, new GUIContent("Shoulder phys mat: "));


            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            //Option: Apply above materials to entire road:
            EditorGUILayout.LabelField("Apply above materials to entire road:");
            isApplyingMaterialsCheck = EditorGUILayout.Toggle(isApplyingMaterialsCheck, GUILayout.Width(20f));
            if (GUILayout.Button("Apply", EditorStyles.miniButton, GUILayout.Width(60f)))
            {
                if (isApplyingMaterialsCheck)
                {
                    isApplyingMatsCheck = true;
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Applying will overwrite any saved cuts' material(s).");

            RenderMaterialsHelpDialog();

            EditorGUILayout.EndVertical();

            //Reset terrain history:
            EditorUtilities.DrawLine();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Reset road's terrain history:");
            isResetingTH = EditorGUILayout.Toggle(isResetingTH, GUILayout.Width(20f));
            if (isResetingTH)
            {
                if (GUILayout.Button("Reset") && isResetingTH)
                {
                    road.ResetTerrainHistory();
                    isResetingTH = false;
                }
            }
            else
            {
                if (GUILayout.Button("Check to reset", maybeButton) && isResetingTH)
                {
                    road.ResetTerrainHistory();
                    isResetingTH = false;
                }
            }
            EditorGUILayout.EndHorizontal();

            if (isResetingTH)
            {
                EditorGUILayout.LabelField("WARNING: This option can't be undone! Only reset the terrain history if you have changed terrain resolution data such as heightmap or detail resolutions. A rare event may occur when editing and compiling this addon's scripts that a terrain history reset may be necessary. Treat this reset as a last resort.", warningLabelStyle);
            }
            GUILayout.Space(6f);


            EditorGUILayout.LabelField("Statistics:");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Length: " + road.spline.distance.ToString("F1") + " meters");
            EditorGUILayout.LabelField("Total nodes: " + road.MostRecentNodeCount.ToString());
            EditorGUILayout.EndVertical();

            bool isLaneChanged = false;
            bool isTerrainHistoryChanged = false;
            bool isEditorCameraSpeedChanged = false;

            if (GUI.changed)
            {
                if (isGizmosEnabled.boolValue != road.isGizmosEnabled)
                {
                    road.ToggleWireframes();
                    SceneView.RepaintAll();
                }
                //Option pre-handle: Lane count:
                if (SerialisedLaneTotal() != road.LaneTotal())
                {
                    isNeedingRoadUpdate = true;
                    isLaneChanged = true;
                }

                //Option pre-handle for terrain history:
                if (isSavingTerrainHistoryOnDisk.boolValue != road.isSavingTerrainHistoryOnDisk)
                {
                    isTerrainHistoryChanged = true;
                }

                //Option pre-handle for editor camera speed:
                if (!RootUtils.IsApproximately(editorCameraMetersPerSecond.floatValue, road.EditorCameraMetersPerSecond, 0.001f))
                {
                    isEditorCameraSpeedChanged = true;
                }

                //Apply serialization:
                serializedObject.ApplyModifiedProperties();


                //Option: Lane count:
                if (isLaneChanged)
                {
                    if (road.isUsingDefaultMaterials)
                    {
                        road.spline.ClearAllRoadCuts();
                        road.SetDefaultMats();
                        road.SetAllCutsToCurrentMaterials();
                    }
                }

                //Option: terrain history save type:
                if (isTerrainHistoryChanged)
                {
                    if (road.isSavingTerrainHistoryOnDisk)
                    {
                        road.StoreTerrainHistory(true);
                    }
                    else
                    {
                        road.LoadTerrainHistory(true);
                    }
                }

                //Option: Editor camera meters per sec
                if (isEditorCameraSpeedChanged)
                {
                    road.ChangeEditorCameraMetersPerSec();
                }

                //Update road:
                if (isNeedingRoadUpdate)
                {
                    TriggerRoadUpdate();
                }

                //Option: Apply above materials to entire road:
                if (isApplyingMatsCheck)
                {
                    isApplyingMatsCheck = false;
                    isApplyingMaterialsCheck = false;
                    road.SetAllCutsToCurrentMaterials();
                }

                EditorUtility.SetDirty(target);
            }
        }


        private void RenderRoadMaterials()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Road base material(s) defaults:");

            //Option: Set materials to default:
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                road.SetDefaultMats();
                road.SetAllCutsToCurrentMaterials();
            }
            EditorGUILayout.EndHorizontal();


            //Option: Use default materials:
            isUsingDefaultMaterials.boolValue = EditorGUILayout.Toggle("Use default materials:", road.isUsingDefaultMaterials);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(roadMaterial1, new GUIContent("  Mat #1: "));
            if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
            {
                road.RoadMaterial1 = null;
            }
            EditorGUILayout.EndHorizontal();


            if (road.RoadMaterial1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(roadMaterial2, new GUIContent("  Mat #2: "));
                if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                {
                    road.RoadMaterial2 = null;
                }
                EditorGUILayout.EndHorizontal();


                if (road.RoadMaterial2 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(roadMaterial3, new GUIContent("  Mat #3: "));
                    if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                    {
                        road.RoadMaterial3 = null;
                    }
                    EditorGUILayout.EndHorizontal();


                    if (road.RoadMaterial3 != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(roadMaterial4, new GUIContent("  Mat #4: "));
                        if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                        {
                            road.RoadMaterial4 = null;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }


        private void RenderRoadMarkerMaterials()
        {
            //		//Road marker material defaults:
            GUILayout.Label("Road marker material(s) defaults:");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(roadMaterialMarker1, new GUIContent("  Mat #1: "));
            if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
            {
                road.RoadMaterialMarker1 = null;
            }
            EditorGUILayout.EndHorizontal();

            if (road.RoadMaterialMarker1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(roadMaterialMarker2, new GUIContent("  Mat #2: "));
                if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                {
                    road.RoadMaterialMarker2 = null;
                }
                EditorGUILayout.EndHorizontal();


                if (road.RoadMaterialMarker2 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(roadMaterialMarker3, new GUIContent("  Mat #3: "));
                    if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                    {
                        road.RoadMaterialMarker3 = null;
                    }
                    EditorGUILayout.EndHorizontal();


                    if (road.RoadMaterialMarker3 != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(roadMaterialMarker4, new GUIContent("  Mat #4: "));
                        if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                        {
                            road.RoadMaterialMarker4 = null;
                        }
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }


        private void RenderShoulderMaterials()
        {
            //		//Shoulder material defaults:
            if (road.isShouldersEnabled)
            {
                GUILayout.Label("Shoulder base material(s) defaults:");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(shoulderMaterial1, new GUIContent("  Mat #1: "));
                if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                {
                    road.ShoulderMaterial1 = null;
                }
                EditorGUILayout.EndHorizontal();


                if (road.ShoulderMaterial1 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(shoulderMaterial2, new GUIContent("  Mat #2: "));
                    if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                    {
                        road.ShoulderMaterial2 = null;
                    }
                    EditorGUILayout.EndHorizontal();


                    if (road.ShoulderMaterial2 != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(shoulderMaterial3, new GUIContent("  Mat #3: "));
                        if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                        {
                            road.ShoulderMaterial3 = null;
                        }
                        EditorGUILayout.EndHorizontal();


                        if (road.ShoulderMaterial3 != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PropertyField(shoulderMaterial4, new GUIContent("  Mat #4: "));
                            if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                            {
                                road.ShoulderMaterial4 = null;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }


        private void RenderShoulderMarkerMaterials()
        {
            ////Shoulder marker material defaults:
            if (road.isShouldersEnabled)
            {
                GUILayout.Label("Shoulder marker material(s) defaults:");

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PropertyField(shoulderMaterialMarker1, new GUIContent("  Mat #1: "));
                if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                {
                    road.ShoulderMaterialMarker1 = null;
                }
                EditorGUILayout.EndHorizontal();


                if (road.ShoulderMaterialMarker1 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(shoulderMaterialMarker2, new GUIContent("  Mat #2: "));
                    if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                    {
                        road.ShoulderMaterialMarker2 = null;
                    }
                    EditorGUILayout.EndHorizontal();


                    if (road.ShoulderMaterialMarker2 != null)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.PropertyField(shoulderMaterialMarker3, new GUIContent("  Mat #3: "));
                        if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                        {
                            road.ShoulderMaterialMarker3 = null;
                        }
                        EditorGUILayout.EndHorizontal();


                        if (road.ShoulderMaterialMarker3 != null)
                        {
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.PropertyField(shoulderMaterialMarker4, new GUIContent("  Mat #4: "));
                            if (GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f)))
                            {
                                road.ShoulderMaterialMarker4 = null;
                            }
                            EditorGUILayout.EndHorizontal();
                        }
                    }
                }
            }
        }


        private void RenderMaterialsHelpDialog()
        {
            //Help toggle for materials
            GUILayout.Space(4);
            isShowingMaterialsHelp = EditorGUILayout.Foldout(isShowingMaterialsHelp, status);
            if (isShowingMaterialsHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("These default materials will be applied by default to their respective generated meshes. If using split roads and or shoulders, you can specific specific materials to use on them (on the mesh renderers of the cuts) and they will be used instead of the default materials listed above.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Road base material is UV mapped on a world vector basis for seamless tiles.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Road marker material is UV mapped to fit roads. Use these materials to place road lines and other road texture details. Note: if using road cuts, these are the materials which will be placed by default at the initial generation.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Shoulder base material is UV mapped on a world vector basis for seamless tiles.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Shoulder marker material is UV mapped on a world vector basis for seamless tiles. For intended use with transparent shadow receiving shaders. Marker materials are applied, optionally, on shoulder cuts.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("The physical material for road will be used on all road colliders. The physical material for shoulder will be used on all shoulder colliders. If using road and or shoulder cuts, you can specficy unique physics materials which will be used instead of the default physics materials.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Apply above materials button will clear all saved materials on the roads and all road and shoulder meshes will use the materials listed above.");
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }


        private void RenderCameraHelpDialog()
        {
            GUILayout.Space(4);
            isShowingCameraHelp = EditorGUILayout.Foldout(isShowingCameraHelp, status);
            if (isShowingCameraHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Editor camera quick help:", EditorStyles.boldLabel);
                if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
                {
                    RoadCityMaster.EditorUtilities.OpenOfflineManual();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(onlineHelpDesc, MessageType.Info);
                EditorGUILayout.LabelField("Use this section to travel along the road while in the editor sceneview.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Camera meters/sec is the speed at which the camera moves along the road.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Auto rotate will automatically rotate the camera to look forward at the current road's tangent. Note: You can still zoom in and out with the camera with this option selected.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Note: Materials act differently in the editor scene view compared to actual gameplay. Try the game camera if the materials are z fighting and having other issues.");
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }


        private void RenderCutsHelpDialog()
        {
            isShowingCutsHelp = EditorGUILayout.Foldout(isShowingCutsHelp, status);
            if (isShowingCutsHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Road splitting quick help:", EditorStyles.boldLabel);
                if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
                {
                    RoadCityMaster.EditorUtilities.OpenOfflineManual();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(onlineHelpDesc, MessageType.Info);

                EditorGUILayout.LabelField("Typically auto-split will be the best choice for performance and other reasons.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Choosing split options will split the road/shoulder up into pieces mirroring the locations of nodes.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Splitting allows for more detailed and flexible road texturing options such as passing sections, other different road lines per section, road debris and more.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Choosing split options may also speed up mesh collider collision calculations if bounds calculations are involved.");
                GUILayout.Space(4f);

                EditorGUILayout.LabelField("Which splitting option to choose?", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Choose no splitting if you desire a single material set for this entire road and your game experiences no collison processing slowdowns from one large mesh collider. This option will create less game objects than automatic and manual splitting.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Choose automatic road and shoulder splitting if you desire multiple materials (such as yellow double lines for certain sections and white dotted for others) for this road and or your game experiences collision processing slowdowns from one large mesh collider.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Choose manual road and shoulder splitting if you desire the same as automatic splitting and desire more freedom over the process. This option will result in less gameobjects and larger mesh colliders when compared to automatic splitting.");
                GUILayout.Space(4f);


                EditorUtilities.DrawLine();
                EditorGUILayout.LabelField("Manual splitting information: ");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Choosing manual splitting will force the user to select individual nodes to cut instead of the cuts being performed automatically. This option is recommended if bigger mesh colliders do not cause a slowdown in performance, as it lowers the overall gameobject count.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Manual splitting will not split up the mesh colliders like automatic cuts, so the colliders may get large & complex and cost more CPU to process collisions. If this option is chosen, please verify your game's collision processing speed and if you run into long collision processing times split more road sections");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Example usages of manual splitting");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Example hill: Goal is to have double yellow no passing lines on a two lane road but only while the road is near or on the hill. " +
                    "Pick nodes on either sides of the hill and mark both as road cut. Everything between these two nodes will be its own section, " +
                    "allowing you to apply double yellow no passing lines for just the hill.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Example mountains: In the mountains, the road is curvy and the grade is high. " +
                    "There's a flat straight spot that you want to allow passing in, by marking the road with white dotted passing lines. " +
                    "At the beginning of the flat straight section, mark the node as road cut. Now at the end of the flat straight section, mark this node as road cut. " +
                    "This will create a road section between the two nodes, allowing you to apply white dotted passing lines for just the flat straight section.");
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
            else
            {
                GUILayout.Space(4f);
            }
        }


        private void RenderTerrainHelpDialog()
        {
            isShowingTerrainHelp = EditorGUILayout.Foldout(isShowingTerrainHelp, status);
            if (isShowingTerrainHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Terrain options quick help:", EditorStyles.boldLabel);
                if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
                {
                    RoadCityMaster.EditorUtilities.OpenOfflineManual();
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.HelpBox(onlineHelpDesc, MessageType.Info);
                EditorGUILayout.LabelField("Terrain subtraction: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("This value, in meters, will be subtracted from the terrain match height to prevent z-fighting.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Spline magnitude limit: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Limits the magnitude of the spline nodes. Lower limit is better for typical roads with node seperation of around 100 to 300 meters. Higher limits will allow for less tension when using very spread out nodes.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Height Modification:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enables or disables height matching for the terrain.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Active detail removal:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enables or disables active detail removal. Memory intensive on large terrains with large amounts of details. Recommended to not use this option and instead remove details and trees via splat maps with other addons.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Active tree removal:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enables or disables active tree removal. Memory intensive on large terrains with large amounts of trees. Recommended to not use this option and instead remove details and trees via splat maps with other addons.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Heights match width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The distance to the left and right of the road in which terrain heights will be matched to the road.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Details clear width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The distance between the road and detail, width wise, in which details will be removed.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Details clear height:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The distance between the road and detail, height wise, in which details will be removed.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Tree clear width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The distance between the road and tree, width wise, in which trees will be removed.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Tree clear height:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The distance between the road and tree, height wise, in which trees will be removed.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Store terrain history separate from scene:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("If enabled, stores the terrain history immediately on disk after use, saving memory while in editor.");
                GUILayout.Space(4f);
                EditorGUILayout.LabelField("Terrain history size:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Shows the size, in kilobytes, of the terrain history in memory or on disk.");
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }


        private void RenderRoadHelpDialog()
        {
            isShowingRoadHelp = EditorGUILayout.Foldout(isShowingRoadHelp, status);
            if (isShowingRoadHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Road options quick help:", EditorStyles.boldLabel);
                if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
                {
                    RoadCityMaster.EditorUtilities.OpenOfflineManual();
                }
                EditorGUILayout.HelpBox(onlineHelpDesc, MessageType.Info);
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Gizmos:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enable or disable most gizmos for this road. Disable mesh collider gizmos via the unity menu if necessary or desired.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Lanes:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Select the number of lanes for this road.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Lane width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Modify the width per lane, in meters.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Road width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Displays the road width without considering shoulders, in meters.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Shoulders enabled:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Enables or disables shoulders.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Shoulders width:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Modify the width of shoulders, in meters.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Road definition: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The meter spacing between mesh triangles on the road and shoulder.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Use default materials: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("When enabled will use default materials for the road system, allowing certain aspects of generation to automatically determine the correct materials to utilize.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Max grade enforced: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("When enabled enforces a maximum grade on a per node basis.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Max road grade: ", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("The maximum road grade allowed on a per node basis.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Multithreading:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("When enabled allows for multi-threaded road generation.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Save mesh assets:", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("When enabled saves all generated meshes as .asset files.");
                EditorGUILayout.EndVertical();
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }
        }


        public void OnSceneGUI()
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);

            if (current.alt == true)
            {
                return;
            }

            if (Selection.Contains(road.transform.gameObject) && Selection.objects.Length > 1)
            {
                SetSelectionToRoad();
            }

            // Handle Ctrl and Shift when road is selected
            if (Selection.activeGameObject == road.transform.gameObject)
            {
                road.isEditorSelected = true;
                // Only handle MouseMove and MouseDrag events
                if (Event.current.type == EventType.MouseMove || Event.current.type == EventType.MouseDrag)
                {
                    if (current.control)
                    {
                        Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        RaycastHit hitInfo;
                        if (Physics.Raycast(worldRay, out hitInfo))
                        {
                            /* There used to be a check for whether this was a terrain */
                            road.editorMousePos = hitInfo.point;
                            road.isEditorMouseHittingTerrain = true;
                            if (road.spline && road.spline.previewSpline)
                            {
                                //Debug.Log("Drawing new node");
                                if (road.spline.previewSpline.nodes == null || road.spline.previewSpline.nodes.Count < 1)
                                {
                                    road.spline.Setup();
                                }
                                road.spline.previewSpline.mousePos = hitInfo.point;
                                road.spline.previewSpline.isDrawingGizmos = true;
                                SceneView.RepaintAll();
                            }
                        }

                        GUI.changed = true;
                    }
                    else if (current.shift)
                    {
                        Ray worldRay = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
                        RaycastHit hitInfo;
                        if (Physics.Raycast(worldRay, out hitInfo))
                        {
                            /* Used to check for terrain */
                            //					if(hitInfo.collider.transform.name.ToLower().Contains("terrain")){
                            road.editorMousePos = hitInfo.point;
                            road.isEditorMouseHittingTerrain = true;
                            if (road.spline && road.spline.previewSplineInsert)
                            {
                                if (road.spline.previewSplineInsert.nodes == null || road.spline.previewSplineInsert.nodes.Count < 1)
                                {
                                    road.spline.previewSplineInsert.DetermineInsertNodes();
                                }
                                road.spline.previewSplineInsert.mousePos = hitInfo.point;
                                road.spline.previewSplineInsert.isDrawingGizmos = true;
                                road.spline.previewSplineInsert.UpdateActionNode();
                                SceneView.RepaintAll();
                            }
                            //}else{
                            //	RS.Editor_MouseTerrainHit = false;	
                            //}
                        }

                        GUI.changed = true;
                    }
                    else
                    {
                        if (road.isEditorMouseHittingTerrain)
                        {
                            road.isEditorMouseHittingTerrain = false;
                            GUI.changed = true;
                        }
                        if (road.spline && road.spline.previewSpline)
                        {
                            //Debug.Log("not drawing new node");
                            road.spline.previewSpline.isDrawingGizmos = false;
                        }
                        if (road.spline && road.spline.previewSplineInsert)
                        {
                            //Debug.Log("not drawing insert node");
                            road.spline.previewSplineInsert.isDrawingGizmos = false;
                        }
                    }
                }
            }
            else
            {
                road.isEditorSelected = false;
                if (road.spline.previewSpline)
                {
                    road.spline.previewSpline.isDrawingGizmos = false;
                }
            }



            if (current.shift && road.spline.previewSpline != null)
            {
                road.spline.previewSpline.isDrawingGizmos = false;
            }
            bool isUsed = false;
            if (current.control)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    //Left click:
                    if (Event.current.button == 0)
                    {
                        if (road.isEditorMouseHittingTerrain)
                        {
                            //if((EditorApplication.timeSinceStartup - RS.spline.EditorOnly_LastNode_TimeSinceStartup) > 0.05)
                            //{
                            //	RS.spline.EditorOnly_LastNode_TimeSinceStartup = EditorApplication.timeSinceStartup;
                            Event.current.Use();
                            Construction.CreateNode(road);
                            isUsed = true;
                            //}
                        }
                        else
                        {
                            Debug.Log("Invalid surface for new node. Must be terrain.");
                        }
                    }
                }
            }
            else if (current.shift)
            {
                if (Event.current.type == EventType.MouseDown)
                {
                    //Left click:
                    if (Event.current.button == 0)
                    {
                        if (road.isEditorMouseHittingTerrain)
                        {
                            Event.current.Use();
                            Construction.InsertNode(road);
                            isUsed = true;
                        }
                        else
                        {
                            Debug.Log("Invalid surface for insertion node. Must be terrain.");
                        }
                    }
                }
            }


            if (current.type == EventType.ValidateCommand)
            {
                switch (current.commandName)
                {
                    case "UndoRedoPerformed":
                        TriggerRoadUpdate();
                        break;
                }
            }

            if (Selection.activeGameObject == road.transform.gameObject)
            {
                if (current.keyCode == KeyCode.F5)
                {
                    TriggerRoadUpdate();
                }
            }



            if (isUsed)
            {
                SetSelectionToRoad();
                switch (current.type)
                {
                    case EventType.Layout:
                        HandleUtility.AddDefaultControl(controlID);
                        break;
                }
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(road);
            }
        }


        /// <summary> RM-109. Reconciles this road against the class it says it is, and offers the one action that
        /// settles the difference.
        ///
        /// Two states need saying and they are different questions. Either the class was CHANGED and not applied yet
        /// (roadClass has moved away from appliedRoadClass), in which case the author has said what they want and the
        /// road has not moved -- so applying must KEEP the figures they deliberately overrode, which is what the
        /// merge's preserveOverrides is for. Or the class was never changed and the cross-section still does not match
        /// it: an older road, or one tuned by hand. There the LIST is the useful half, because it names every field
        /// that differs and what the class asks instead, so the button can say what it will do before it is pressed.
        ///
        /// What counts as a difference is not decided here: City.RoadTypeChange owns that and the figures come from the
        /// one cited table, so the inspector cannot show an author one account of their road while the type-change path
        /// acts on another. A road that already agrees with its class draws nothing at all. </summary>
        private void DrawRoadClassReconcile()
        {
            if (road == null) { return; }

            string className = ObjectNames.NicifyVariableName(road.roadClass.ToString());
            bool classMoved = road.roadClass != road.appliedRoadClass;
            System.Collections.Generic.List<City.TypeOverride> rows =
                City.RoadTypeChange.Overrides(road.TypeState(), road.roadClass, road.roadClass);
            if (!classMoved && rows.Count == 0) { return; }

            EditorGUILayout.Space();
            EditorGUILayout.BeginVertical("box");
            if (classMoved)
            {
                string kept = City.RoadTypeChange
                    .OverrideReport(road.TypeState(), road.appliedRoadClass, road.roadClass).TrimEnd();
                EditorGUILayout.HelpBox("Class is set to " + className + " but has not been applied yet. Applying "
                    + "settles lane count, lane width, shoulder width, divider, design speed and grade ceiling "
                    + "together, from the class table, and keeps anything you had already changed by hand."
                    + (kept.Length > 0 ? "\n\n" + kept : string.Empty), MessageType.Info);
                if (GUILayout.Button("Apply " + className + " defaults, keeping my changes"))
                {
                    ApplyClassToRoad(true);
                }
            }
            else
            {
                EditorGUILayout.LabelField("Cross-section does not match " + className, EditorStyles.boldLabel);
                DrawClassDifferences(rows, className);
                if (GUILayout.Button("Adopt the " + className + " cross-section"))
                {
                    ApplyClassToRoad(false);
                }
            }
            EditorGUILayout.EndVertical();
        }


        /// <summary> Wrapping row style for the difference list. Owned, built once, and never <c>EditorStyles.label</c>
        /// itself: that instance belongs to the editor, so switching word wrap on it switches word wrap on for every
        /// label in every window Unity has open, which is how a two-line prefix ends up painted across the control
        /// underneath it. </summary>
        private static GUIStyle classDiffRowStyle;


        /// <summary> The per-field list behind the "does not match" panel: what the road has, and what the class asks
        /// for instead. Conflicts first, because a value outside every band the table sanctions is worth reading before
        /// one that is merely different. The wording is the panel's own rather than TypeOverride.Message()'s: that
        /// sentence opens with "kept", which is true of a value carried ACROSS a class change and false here, where
        /// nothing has been changed and the author is being shown a road that drifted from its own class. </summary>
        private static void DrawClassDifferences(
            System.Collections.Generic.List<City.TypeOverride> _rows, string _className)
        {
            if (classDiffRowStyle == null)
            {
                classDiffRowStyle = new GUIStyle(EditorStyles.label);
                classDiffRowStyle.wordWrap = true;
            }

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _rows.Count; i++)
                {
                    if (_rows[i].isConflict != (pass == 0)) { continue; }
                    EditorGUILayout.LabelField((_rows[i].isConflict ? "! " : "• ") + _rows[i].field + " is "
                        + _rows[i].authorValue + ", " + _className + " asks for " + _rows[i].classDefault,
                        classDiffRowStyle);
                }
            }
        }


        /// <summary> Push the class's figures onto the road and rebuild once.
        ///
        /// The two serialization calls around the write are not decoration. This editor reads its properties at the top
        /// of OnInspectorGUI and writes them back at the end, so a direct field write made in between would be undone
        /// by that write-back, which still holds the values the properties had. Flushing first commits whatever the
        /// author typed above; re-reading afterwards means the write-back at the end sends the class figures out rather
        /// than the ones they replaced. </summary>
        private void ApplyClassToRoad(bool _preserveOverrides)
        {
            serializedObject.ApplyModifiedProperties();
            Undo.RecordObject(road, "Apply road class defaults");
            road.ChangeType(road.roadClass, null, _preserveOverrides);
            EditorUtility.SetDirty(road);
            serializedObject.Update();
        }


        /// <summary> The one way THIS INSPECTOR asks for a rebuild. Every control on the Road inspector that starts
        /// work calls this rather than building for itself, so how a rebuild is requested is decided once here rather
        /// than at each button that wants one.
        ///
        /// RM-102 changed what that means, and the change is the point of the task's "rapid edits produce one rebuild
        /// rather than many" clause. These sites used to set <c>road.isUpdateRequired</c>, which the editor loop
        /// consumed by calling <c>spline.TriggerSetup()</c> -> <c>road.UpdateRoad()</c>: the SYNCHRONOUS build. That
        /// path predates the queue and knows nothing about it, so it bypassed RM-100's dispatch and RM-101's supersede
        /// entirely -- dragging a slider ran a full build per mouse move, on the main thread, each one throwing away
        /// the last one's result while the editor froze for all of them.
        ///
        /// <see cref="LegacyInspectorShell.Rebuild"/> instead defers to the next editor frame (a build started from
        /// inside OnInspectorGUI runs against a half-drawn layout) and lands on <c>Road.RequestUpdate()</c>, which
        /// supersedes this road's build in flight rather than starting a second one beside it.
        ///
        /// It is not the only door onto the queue and does not claim to be: <c>isUpdateRequired</c> is public and four
        /// other author operations still set it. B-14 pointed that flag's consumer in <c>Road.cs</c> at
        /// <c>RequestUpdate()</c> too, so those reach the same queue by their own route -- not through here. </summary>
        private void TriggerRoadUpdate()
        {
            LegacyInspectorShell.Rebuild(road);
        }


        private void SetSelectionToRoad()
        {
            GameObject[] objects = new GameObject[1];
            objects[0] = road.transform.gameObject;
            Selection.objects = objects;
        }
    }
}
#endif
