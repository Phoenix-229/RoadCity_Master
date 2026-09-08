// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(RoadIntersection))]
    public class RoadIntersectionEditor : Editor
    {
        #region "Vars"
        private RoadIntersection intersection;

        private SerializedProperty isAutoUpdatingIntersection;
        private SerializedProperty isDrawingGizmo;
        private SerializedProperty isLeftTurnYieldOnGreen;
        private SerializedProperty fixedTimeRegularLightLength;
        private SerializedProperty fixedTimeLeftTurnLightLength;
        private SerializedProperty fixedTimeAllRedLightLength;
        private SerializedProperty fixedTimeYellowLightLength;
        private SerializedProperty isTrafficPoleStreetLight;
        private SerializedProperty isTrafficLightGray;
        private SerializedProperty isRegularPoleAlignment;
        private SerializedProperty isLightsEnabled;
        private SerializedProperty streetLightRange;
        private SerializedProperty streetLightIntensity;
        private SerializedProperty streetLightColor;
        private SerializedProperty scalingSense;
        private SerializedProperty gradeMod;
        private SerializedProperty isUsingDefaultMaterials;
        private SerializedProperty markerCenter1;
        private SerializedProperty markerCenter2;
        private SerializedProperty markerCenter3;
        private SerializedProperty markerExtStretch1;
        private SerializedProperty markerExtStretch2;
        private SerializedProperty markerExtStretch3;
        private SerializedProperty markerExtTiled1;
        private SerializedProperty markerExtTiled2;
        private SerializedProperty markerExtTiled3;
        private SerializedProperty lane0Mat1;
        private SerializedProperty lane0Mat2;
        private SerializedProperty lane1Mat1;
        private SerializedProperty lane1Mat2;
        private SerializedProperty lane2Mat1;
        private SerializedProperty lane2Mat2;
        private SerializedProperty lane3Mat1;
        private SerializedProperty lane3Mat2;
        private SerializedProperty roadType;
        private SerializedProperty intersectionStopType;
        private SerializedProperty lightType;

        /// <summary> B-19. This inspector's own deferred-commit controls. One per Editor instance, because the pending
        /// text of a half-typed light length belongs to the inspector it is being typed into. Reset at the top of every
        /// pass by <see cref="FocusOutFields.BeginFrame"/>, and read once after drawing, beside GUI.changed, to decide
        /// whether anything is there to apply. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();


        #region "Editor only variables"
        //Editor only variables
        private const bool isDebug = false;
        private bool isShowingMarkerStretch = false;
        private bool isShowingMarkerTiled = false;
        private bool isShowingMarkerCenter = false;
        private bool isShowingTLSense = false;
        private bool isShowingTLPole = false;
        private bool isShowingLightHelp = false;
        private bool isShowingLanes = false;
        private bool isGradeCorrect = false;
        private bool isShowingDefaultMatHelp = false;
        private bool isShowingHelpLeftTurnGreen = false;
        private string status = "Show help";

        private GUIStyle imageButton = null;
        private Texture refreshButtonTexture = null;
        private Texture deleteButtonTexture = null;

        private const string helpText1 = "Each material added is rendered on top of the previous. Combine with transparent shaders which accept shadows to allow for easy marking.";

        //Checkers:
        // B-11. Gone from here: `loadButton` and the two textures it was built from. The branded command-button style is
        // `LegacyInspectorShell.CommandButtonStyle()`, which is where three other editors' copies of it went too.
        private bool isInitialized = false;
        #endregion
        #endregion


        private static string[] rTypeDescriptions = new string[]{
        "No turn lanes",
        "Left turn lane only",
        "Both left and right turn lanes"
    };


        private static string[] rTypeDescriptions_3Way = new string[]{
        "No turn lanes",
        "Left turn lane only"
    };


        private static string[] iStopTypeEnumDescriptions = new string[]{
        "Stop signs",
        "Traffic lights",
        "None"
//		"Traffic lights #2"
    };


        private static string[] iTrafficLightSequenceTypeDesc = new string[]{
        "Fixed time",
        "Other"
    };


        private void OnEnable()
        {
            intersection = (RoadIntersection)target;

            isAutoUpdatingIntersection = serializedObject.FindProperty("isAutoUpdatingIntersection");
            isDrawingGizmo = serializedObject.FindProperty("isDrawingGizmo");
            isLeftTurnYieldOnGreen = serializedObject.FindProperty("isLeftTurnYieldOnGreen");
            fixedTimeRegularLightLength = serializedObject.FindProperty("fixedTimeRegularLightLength");
            fixedTimeLeftTurnLightLength = serializedObject.FindProperty("fixedTimeLeftTurnLightLength");
            fixedTimeAllRedLightLength = serializedObject.FindProperty("fixedTimeAllRedLightLength");
            fixedTimeYellowLightLength = serializedObject.FindProperty("fixedTimeYellowLightLength");
            isTrafficPoleStreetLight = serializedObject.FindProperty("isTrafficPoleStreetLight");
            isTrafficLightGray = serializedObject.FindProperty("isTrafficLightGray");
            isRegularPoleAlignment = serializedObject.FindProperty("isRegularPoleAlignment");
            isLightsEnabled = serializedObject.FindProperty("isLightsEnabled");
            streetLightRange = serializedObject.FindProperty("streetLightRange");
            streetLightIntensity = serializedObject.FindProperty("streetLightIntensity");
            streetLightColor = serializedObject.FindProperty("streetLightColor");
            scalingSense = serializedObject.FindProperty("scalingSense");
            gradeMod = serializedObject.FindProperty("gradeMod");
            isUsingDefaultMaterials = serializedObject.FindProperty("isUsingDefaultMaterials");
            markerCenter1 = serializedObject.FindProperty("markerCenter1");
            markerCenter2 = serializedObject.FindProperty("markerCenter2");
            markerCenter3 = serializedObject.FindProperty("markerCenter3");
            markerExtStretch1 = serializedObject.FindProperty("markerExtStretch1");
            markerExtStretch2 = serializedObject.FindProperty("markerExtStretch2");
            markerExtStretch3 = serializedObject.FindProperty("markerExtStretch3");
            markerExtTiled1 = serializedObject.FindProperty("markerExtTiled1");
            markerExtTiled2 = serializedObject.FindProperty("markerExtTiled2");
            markerExtTiled3 = serializedObject.FindProperty("markerExtTiled3");
            lane0Mat1 = serializedObject.FindProperty("lane0Mat1");
            lane0Mat2 = serializedObject.FindProperty("lane0Mat2");
            lane1Mat1 = serializedObject.FindProperty("lane1Mat1");
            lane1Mat2 = serializedObject.FindProperty("lane1Mat2");
            lane2Mat1 = serializedObject.FindProperty("lane2Mat1");
            lane2Mat2 = serializedObject.FindProperty("lane2Mat2");
            lane3Mat1 = serializedObject.FindProperty("lane3Mat1");
            lane3Mat2 = serializedObject.FindProperty("lane3Mat2");
            roadType = serializedObject.FindProperty("roadType");
            intersectionStopType = serializedObject.FindProperty("intersectionStopType");
            lightType = serializedObject.FindProperty("lightType");
        }


        public override void OnInspectorGUI()
        {
            if (Event.current.type == EventType.ValidateCommand)
            {
                switch (Event.current.commandName)
                {
                    case "UndoRedoPerformed":
                        TriggerRoadUpdate(true);
                        break;
                }
            }

            serializedObject.Update();
            fields.BeginFrame();

            // RM-109. B-3, B-4. Per repaint, and given back on the next tick -- not once from Init() into settings the
            // whole editor shares.
            InspectorTextLayout.Apply();

            //Graphic null checks:
            if (!isInitialized)
            {
                Init();
            }

            EditorUtilities.DrawLine();
            // B-14. Every control in this inspector sat behind the two unguarded dereferences below. An intersection
            // whose roads or nodes have been deleted -- which is what deleting a road does to the intersections on it --
            // made that line throw before a single control was drawn, so the author could not reach the inspector at
            // all, not even to see what was wrong or to delete the leftover. A control that cannot be drawn reports even
            // less than one that answers a click with nothing, so the whole panel says what is missing instead.
            if (intersection.node1 == null || intersection.node2 == null
                || intersection.node1.spline == null || intersection.node2.spline == null
                || intersection.node1.spline.road == null || intersection.node2.spline.road == null)
            {
                EditorGUILayout.HelpBox("This intersection has lost one of the two road nodes it was made from, so"
                    + " there is nothing to draw options for. It is left over from a road or node that was deleted, and"
                    + " deleting this GameObject is safe.", MessageType.Warning);
                EditorUtilities.DrawLine();
                return;
            }
            EditorGUILayout.BeginHorizontal();
            {
                // B-14. Was `spline.transform.parent.GetComponent<Road>()` twice. SplineC already holds the road it
                // belongs to, so this asked the hierarchy for something it was handed -- two GetComponent walks on every
                // repaint of this inspector, and a third null to trip over if a spline object were ever reparented.
                Road road1 = intersection.node1.spline.road;
                Road road2 = intersection.node2.spline.road;
                EditorGUILayout.LabelField("First road: " + road1.name + " node: " + intersection.node1.name);
                EditorGUILayout.LabelField("Second road: " + road2.name + " node: " + intersection.node2.name);
            }
            EditorGUILayout.EndHorizontal();
            EditorUtilities.DrawLine();
            EditorGUILayout.LabelField("Intersection options", EditorStyles.boldLabel);
            if (GUILayout.Button("Update intersection", LegacyInspectorShell.CommandButtonStyle()))
            {
                TriggerRoadUpdate(true);
            }

            //Option: Auto update:
            isAutoUpdatingIntersection.boolValue = EditorGUILayout.Toggle("Auto-update:", intersection.isAutoUpdatingIntersection);

            //Option: Gizmo:
            isDrawingGizmo.boolValue = EditorGUILayout.Toggle("Gizmo:", intersection.isDrawingGizmo);

            //UI:
            if (intersection.intersectionType == RoadIntersection.IntersectionTypeEnum.ThreeWay)
            {
                EditorGUILayout.LabelField("Intersection type: 3 way");
            }
            else
            {
                EditorGUILayout.LabelField("Intersection type: 4 way");
            }
            // RM-102. One manual, one button -- see EditorUtilities.OpenOfflineManual.
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            EditorUtilities.DrawLine();
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button("Access objects on first road node"))
            {
                Selection.objects = new Object[1] { intersection.node1 };
            }
            if (GUILayout.Button("Access objects on second road node"))
            {
                Selection.objects = new Object[1] { intersection.node2 };
            }
            EditorGUILayout.EndVertical();
            //Option: Intersection turn lane options
            EditorUtilities.DrawLine();
            EditorGUILayout.LabelField("Intersection turn lane options:");
            if (intersection.intersectionType == RoadIntersection.IntersectionTypeEnum.ThreeWay)
            {
                roadType.enumValueIndex = (int)EditorGUILayout.Popup((int)intersection.roadType, rTypeDescriptions_3Way);
            }
            else
            {
                roadType.enumValueIndex = (int)EditorGUILayout.Popup((int)intersection.roadType, rTypeDescriptions);
            }

            //Option: Left yield on green:
            if (intersection.roadType != RoadIntersection.RoadTypeEnum.NoTurnLane)
            {
                isLeftTurnYieldOnGreen.boolValue = EditorGUILayout.Toggle("Left yield on green: ", intersection.isLeftTurnYieldOnGreen);

                isShowingHelpLeftTurnGreen = EditorGUILayout.Foldout(isShowingHelpLeftTurnGreen, status);
                if (isShowingHelpLeftTurnGreen)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("Left yield on green: If checked, replaces the standard 3-light left turn light with a five-light yield on green left turn signal structure and sign.");
                    EditorGUILayout.EndVertical();
                }
            }
            EditorUtilities.DrawLine();


            // B-16. The junction FORM and the fields the two non-at-grade forms need, drawn through the RM-51 groups
            // straight from the registry rather than as eight more raw rows -- so each carries its unit, its band and
            // its hover sentence instead of the flat layout growing eight more exceptions. The legacy popups below are
            // not registry fields and stay exactly where they were.
            LegacyInspectorShell.SectionHeader("Junction form",
                "Whether this junction meets on one surface at all: priority and signalised do and differ only in who"
                + " waits, a roundabout circulates around an island, and an interchange carries one road over the"
                + " other and adds ramps beside the crossing rather than replacing it.");
            LegacyInspectorShell.AllGroups(serializedObject, "RoadIntersection", fields);
            DrawJunctionVerdict(intersection);
            EditorUtilities.DrawLine();


            //Option: Intersection stop type:
            intersectionStopType.enumValueIndex = (int)EditorGUILayout.Popup("Intersection stop type:", (int)intersection.intersectionStopType, iStopTypeEnumDescriptions);

            // B-16. The form decides which of these four values means anything, so the mismatch is said here, beside the
            // popup that caused it, rather than only inside the resolve.
            string stopRefusal;
            if (!intersection.StopTypeIsCompatible(out stopRefusal))
            {
                EditorGUILayout.HelpBox(stopRefusal, MessageType.Warning);
            }


            if (intersection.intersectionStopType == RoadIntersection.iStopTypeEnum.TrafficLight1 || intersection.intersectionStopType == RoadIntersection.iStopTypeEnum.TrafficLight2)
            {
                //Option: Traffic light timing type:
                lightType.enumValueIndex = (int)EditorGUILayout.Popup("Traffic light timing:", (int)intersection.lightType, iTrafficLightSequenceTypeDesc);

                //Options: Traffic fixed light timings:
                if (intersection.lightType == RoadIntersection.LightTypeEnum.Timed)
                {
                    EditorGUILayout.LabelField("Traffic light fixed time lengths (in seconds):");
                    // B-19. All four of these were raw sliders writing the property on every frame of a drag, which is
                    // a signal timing rewritten thirty times a second and an undo stack with thirty entries in it for
                    // one gesture. Through the deferred control the handle moves freely, the number beside it greys to
                    // say "not committed yet", and the property is written once -- when the button comes up, or when a
                    // typed number is confirmed with Enter or by moving focus away. The reset buttons keep their place
                    // on the value's row: the control opens its own horizontal group inside this one.
                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider(fixedTimeRegularLightLength, "Green length: ", 0.1f, 180f);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        fixedTimeRegularLightLength.floatValue = 30f;
                    }
                    EditorGUILayout.EndHorizontal();

                    if (intersection.roadType != RoadIntersection.RoadTypeEnum.NoTurnLane)
                    {
                        EditorGUILayout.BeginHorizontal();
                        fields.FloatSlider(fixedTimeLeftTurnLightLength, "Left turn only length: ", 0.1f, 180f);
                        if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                        {
                            fixedTimeLeftTurnLightLength.floatValue = 10f;
                        }
                        EditorGUILayout.EndHorizontal();
                    }

                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider(fixedTimeAllRedLightLength, "All red length: ", 0.1f, 180f);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        fixedTimeAllRedLightLength.floatValue = 1f;
                    }
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider(fixedTimeYellowLightLength, "Yellow light length: ", 0.1f, 180f);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        fixedTimeYellowLightLength.floatValue = 3f;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }


            if (intersection.intersectionStopType == RoadIntersection.iStopTypeEnum.TrafficLight1 || intersection.intersectionStopType == RoadIntersection.iStopTypeEnum.TrafficLight2)
            {
                //Option: Traffic light poles:
                EditorUtilities.DrawLine();
                EditorGUILayout.LabelField("Traffic light poles:");
                isTrafficPoleStreetLight.boolValue = EditorGUILayout.Toggle("Street lights: ", intersection.isTrafficPoleStreetLight);

                //Option: Traffic light pole gray color:
                isTrafficLightGray.boolValue = EditorGUILayout.Toggle("Gray color: ", intersection.isTrafficLightGray);

                //Option: Normal pole alignment:
                isRegularPoleAlignment.boolValue = EditorGUILayout.Toggle("Normal pole alignment: ", intersection.isRegularPoleAlignment);
                isShowingTLPole = EditorGUILayout.Foldout(isShowingTLPole, status);
                if (isShowingTLPole)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("Street lights: If checked, attaches street lights to each intersection pole. Point lights optional and can be manipulated in the next option segment.");
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("Gray color: If checked, sets the traffic light pole base materials to gray galvanized steel. If unchecked the material used is black metal paint.");
                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("Normal pole alignment: Recommended to keep this option on unless abnormal twisting of light objects is occurring. Turn this option off if the roads immediately surrounding your intersection are curved at extreme angles and cause irregular twisting of the traffic light objects. Turning this option off will attempt to align the poles perpendicular to the adjacent relevant road without any part existing over the main intersection bounds.");
                    EditorGUILayout.EndVertical();
                }

                //Option: Point lights enabled:
                EditorUtilities.DrawLine();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Point lights:");
                EditorGUILayout.EndHorizontal();
                isLightsEnabled.boolValue = EditorGUILayout.Toggle("  Point lights enabled: ", intersection.isLightsEnabled);

                //Options: Street point light options:
                if (intersection.isTrafficPoleStreetLight)
                {
                    //Option: Street light range:
                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider(streetLightRange, "  Street light range: ", 1f, 128f);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        streetLightRange.floatValue = 30f;
                    }
                    EditorGUILayout.EndHorizontal();

                    //Option: Street light intensity:
                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider(streetLightIntensity, "  Street light intensity: ", 0f, 8f);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        streetLightIntensity.floatValue = 1f;
                    }
                    EditorGUILayout.EndHorizontal();

                    //Option: Street light color:
                    EditorGUILayout.BeginHorizontal();
                    streetLightColor.colorValue = EditorGUILayout.ColorField("  Street light color: ", intersection.streetLightColor);
                    if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        streetLightColor.colorValue = new Color(1f, 0.7451f, 0.27451f, 1f);
                    }
                    EditorGUILayout.EndHorizontal();
                }
                isShowingLightHelp = EditorGUILayout.Foldout(isShowingLightHelp, status);
                if (isShowingLightHelp)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("Point lights: Enabled means that point lights for the traffic lights (and street lights, if enabled) will be turned on. This is accessible via script \"bLightsEnabled\"");

                    GUILayout.Space(4f);
                    EditorGUILayout.LabelField("If street pole lights enabled: Street light range, intensity and color: These settings directly correlate to the standard point light settings.");

                    GUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();
                    // B-14. A legend, not a control -- see DoDefaultHelpMat.
                    GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                    EditorGUILayout.LabelField(" = Resets settings to default.");
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                EditorUtilities.DrawLine();

                //Option: Traffic light scaling sensitivity:
                EditorGUILayout.LabelField("Traffic light scaling sensitivity: *Does not auto-update");
                EditorGUILayout.BeginHorizontal();
                // B-19. The raw control here was the label-less `Slider(value, min, max)` overload, with the heading
                // above it standing in for a label. The deferred control always draws a prefix label -- so rather than
                // pass an empty one and leave an unexplained gap where the label column is, the value gets a short name
                // of its own and the heading above keeps the sentence that does not fit in a label column.
                fields.FloatSlider(scalingSense, "  Sensitivity:", 0f, 200f);
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    scalingSense.floatValue = 170f;
                }
                EditorGUILayout.EndHorizontal();
                GUILayout.Space(4f);
                isShowingTLSense = EditorGUILayout.Foldout(isShowingTLSense, status);
                if (isShowingTLSense)
                {
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField("Increasing this value will increase the scaling sensitivity relative to the size of the intersection. Higher scaling value = bigger traffic lights at further distances. Default value is 170.");
                    GUILayout.Space(4f);
                    EditorGUILayout.BeginHorizontal();
                    // B-14. A legend, not a control -- see DoDefaultHelpMat.
                    GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                    EditorGUILayout.LabelField(" = Resets settings to default.");
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                }
                GUILayout.Space(4f);
            }

            //Option: Grade correction modifier:
            EditorUtilities.DrawLine();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Grade correction factor: *Does not auto-update");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            // B-19. Same as the sensitivity slider above: a short label of its own, the explanation left in the heading.
            fields.FloatSlider(gradeMod, "  Factor:", 0.01f, 2f);
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                gradeMod.floatValue = 0.375f;
            }
            EditorGUILayout.EndHorizontal();
            isGradeCorrect = EditorGUILayout.Foldout(isGradeCorrect, status);
            if (isGradeCorrect)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("If using extreme road grades immediately surrounding the intersection, terrain height matching errors may occur at the point of road expansion leading to the intersection.");
                EditorGUILayout.LabelField("Raise this value if you see terrain poking through the road at the road expansion areas.");
                EditorGUILayout.LabelField("Lower this value if you are not using road mesh colliders and notice dips at the expansion points.");
                EditorGUILayout.LabelField("Default value is 0.375 meters, which is the maximum value for a linear range of 0% to 20% grade.");
                EditorGUILayout.LabelField("Recommended to keep grades and angles small leading up to intersections.");
                GUILayout.Space(4f);
                EditorGUILayout.BeginHorizontal();
                // B-14. A legend, not a control -- see DoDefaultHelpMat.
                GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField(" = Resets settings to default.");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
            }

            // B-14. The four "reset this group to the default materials" icons below are recorded here and acted on at
            // the foot of this inspector, beside the other secondary effects, instead of running under the press.
            // ResetCenterMaterials and its three siblings write the seventeen live Material fields directly, and this
            // pass reads those same fields twice after the icons are drawn: the "show Mat #2 only if #1 is set"
            // cascades count rows from them, and the change detector compares them against the properties. Run under
            // the press, a reset that nulls Mat #2 and #3 -- which all four of them do -- removed rows from a pass whose
            // Layout had already counted them ("Getting control N's position in a group with only N controls"), and the
            // detector then read the fresh defaults against stale properties and could not tell an author's own
            // assignment from the tool putting its defaults back. Deferred, the fields move once the layout is finished
            // and ApplyModifiedProperties has run, which is where every other effect in this inspector happens.
            bool isCenterMaterialsReset = false;
            bool isExtStretchedMaterialsReset = false;
            bool isExtTiledMaterialsReset = false;
            bool isLanesMaterialsReset = false;

            //Option: Use default materials:
            EditorUtilities.DrawLine();
            isUsingDefaultMaterials.boolValue = EditorGUILayout.Toggle("Use default materials:", intersection.isUsingDefaultMaterials);
            isShowingDefaultMatHelp = EditorGUILayout.Foldout(isShowingDefaultMatHelp, status);
            if (isShowingDefaultMatHelp)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Checking this option will reset all materials for this intersection and use the default intersection materials that come with this addon.");
                EditorGUILayout.EndVertical();
            }


            EditorUtilities.DrawLine();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Center marker material(s):");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                isCenterMaterialsReset = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            //Options: Center materials:
            //		EditorGUILayout.BeginHorizontal();
            //		EditorGUILayout.PropertyField (t_MarkerCenter1, new GUIContent ("Center material #1:"));
            //		if(tInter.MarkerCenter1 != null && GUILayout.Button(btnDeleteText,GSDImageButton,GUILayout.Width(16f))){ t_MarkerCenter1 = null; }
            //		EditorGUILayout.EndHorizontal();
            //		if(tInter.MarkerCenter1 != null){
            //			EditorGUILayout.BeginHorizontal();
            //			EditorGUILayout.PropertyField (t_MarkerCenter2, new GUIContent ("Center material #2:"));
            //			if(tInter.MarkerCenter2 != null && GUILayout.Button(btnDeleteText,GSDImageButton,GUILayout.Width(16f))){ t_MarkerCenter2 = null; }
            //			EditorGUILayout.EndHorizontal();
            //			if(tInter.MarkerCenter2 != null){
            //				EditorGUILayout.BeginHorizontal();
            //				EditorGUILayout.PropertyField (t_MarkerCenter3, new GUIContent ("Center material #3:"));
            //				if(tInter.MarkerCenter3 != null && GUILayout.Button(btnDeleteText,GSDImageButton,GUILayout.Width(16f))){ t_MarkerCenter3 = null; }
            //				EditorGUILayout.EndHorizontal();
            //			}
            //		}

            EditorGUILayout.BeginHorizontal();
            fields.Property(markerCenter1, new GUIContent("  Mat #1: "));
            // B-14. This X, and the sixteen like it in the material groups below, wrote `intersection.<field> = null`:
            // straight onto the live component, past the SerializedObject the row beside it is drawn from. Three things
            // went wrong at once. The row's own object field kept showing the material for the rest of the frame, since
            // it reads the property, which still held it. The change detector at the foot of this inspector compares
            // property against field, so a press that moved only the field could not be seen as a material change and
            // UpdateMaterials() never ran -- the reference went but the marking stayed on the intersection. And the field
            // it nulled is the same one the "show Mat #2 only if #1 is set" tests below read, so the press removed rows
            // in the middle of a pass whose Layout had already counted them, which is what IMGUI reports as "Getting
            // control N's position in a group with only N controls" on the next repaint. Writing the property instead
            // answers all three: the row clears, ApplyModifiedProperties carries the null to the field at the end of the
            // frame where every other value in this inspector lands, the detector sees the difference and lifts the
            // material off the mesh, and the row count cannot change under a pass that is already drawing.
            if (intersection.markerCenter1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                markerCenter1.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();

            if (intersection.markerCenter1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerCenter2, new GUIContent("  Mat #2: "));
                if (intersection.markerCenter2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerCenter2.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (intersection.markerCenter2 != null && intersection.markerCenter1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerCenter3, new GUIContent("  Mat #3: "));
                if (intersection.markerCenter3 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerCenter3.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            isShowingMarkerCenter = EditorGUILayout.Foldout(isShowingMarkerCenter, status);
            if (isShowingMarkerCenter)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Center marker materials require transparent shaders. Covers the center area of the intersection. Displayed in order #1 on bottom to #4 on top. Combine with transparent shaders which accept shadows to allow for easy marking.");
                DoDefaultHelpMat();
                DoDeleteHelpMat();
                EditorGUILayout.EndVertical();
            }
            EditorUtilities.DrawLine();

            //Options: Marker ext stretched materials:
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Exterior fitted marker material(s):");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                isExtStretchedMaterialsReset = true;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            fields.Property(markerExtStretch1, new GUIContent("  Mat #1: "));
            if (intersection.markerExtStretch1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                markerExtStretch1.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();

            if (intersection.markerExtStretch1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerExtStretch2, new GUIContent("  Mat #2: "));
                if (intersection.markerExtStretch2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerExtStretch2.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (intersection.markerExtStretch2 != null && intersection.markerExtStretch1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerExtStretch3, new GUIContent("  Mat #3: "));
                if (intersection.markerExtStretch3 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerExtStretch3.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            isShowingMarkerStretch = EditorGUILayout.Foldout(isShowingMarkerStretch, status);
            if (isShowingMarkerStretch)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Fitted marker materials require transparent shaders. Covers the exterior area of the intersection with the UV's stretched to match at a 1:1 ratio. Should be use for intersection markings and any visual effects like dirt." + helpText1);
                DoDefaultHelpMat();
                DoDeleteHelpMat();
                EditorGUILayout.EndVertical();
            }
            EditorUtilities.DrawLine();

            //Options: Marker ext tiled materials:
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Exterior tiled marker material(s):");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                isExtTiledMaterialsReset = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            fields.Property(markerExtTiled1, new GUIContent("  Mat #1: "));
            if (intersection.markerExtTiled1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                markerExtTiled1.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();

            if (intersection.markerExtTiled1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerExtTiled2, new GUIContent("  Mat #2: "));
                if (intersection.markerExtTiled2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerExtTiled2.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }
            if (intersection.markerExtTiled2 != null && intersection.markerExtTiled1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(markerExtTiled3, new GUIContent("  Mat #3: "));
                if (intersection.markerExtTiled3 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    markerExtTiled3.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            isShowingMarkerTiled = EditorGUILayout.Foldout(isShowingMarkerTiled, status);
            if (isShowingMarkerTiled)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Covers the exterior area of the intersection with the UV's tiled matching world coordinates. Tiled and used for road pavement textures. UV coordinates will match up seamlessly with road pavement." + helpText1);
                DoDefaultHelpMat();
                DoDeleteHelpMat();
                EditorGUILayout.EndVertical();
            }
            EditorUtilities.DrawLine();

            //Option: Lane section 0:
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Lanes marker materials:");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                isLanesMaterialsReset = true;
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);

            EditorGUILayout.BeginHorizontal();
            fields.Property(lane0Mat1, new GUIContent("Lane section 0 mat #1:"));
            if (intersection.lane0Mat1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                lane0Mat1.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();
            if (intersection.lane0Mat1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(lane0Mat2, new GUIContent("Lane section 0 mat #2:"));
                if (intersection.lane0Mat2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    lane0Mat2.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }


            //Option: Lane section 1:
            EditorGUILayout.BeginHorizontal();
            fields.Property(lane1Mat1, new GUIContent("Lane section 1 mat #1:"));
            if (intersection.lane1Mat1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                lane1Mat1.objectReferenceValue = null;
            }
            EditorGUILayout.EndHorizontal();
            if (intersection.lane1Mat1 != null)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(lane1Mat2, new GUIContent("Lane section 1 mat #2:"));
                if (intersection.lane1Mat2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    lane1Mat2.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
            }

            //Option: Lane section 2:
            if (intersection.roadType == RoadIntersection.RoadTypeEnum.BothTurnLanes || intersection.roadType == RoadIntersection.RoadTypeEnum.TurnLane)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(lane2Mat1, new GUIContent("Lane section 2 mat #1:"));
                if (intersection.lane2Mat1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    lane2Mat1.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
                if (intersection.lane2Mat1 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    fields.Property(lane2Mat2, new GUIContent("Lane section 2 mat #2:"));
                    if (intersection.lane2Mat2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        lane2Mat2.objectReferenceValue = null;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            GUILayout.Space(4f);

            //Option: Lane section 3:
            if (intersection.roadType == RoadIntersection.RoadTypeEnum.BothTurnLanes)
            {
                EditorGUILayout.BeginHorizontal();
                fields.Property(lane3Mat1, new GUIContent("Lane section 3 mat #1:"));
                if (intersection.lane3Mat1 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    lane3Mat1.objectReferenceValue = null;
                }
                EditorGUILayout.EndHorizontal();
                if (intersection.lane3Mat1 != null)
                {
                    EditorGUILayout.BeginHorizontal();
                    fields.Property(lane3Mat2, new GUIContent("Lane section 3 mat #2:"));
                    if (intersection.lane3Mat2 != null && GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        lane3Mat2.objectReferenceValue = null;
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            isShowingLanes = EditorGUILayout.Foldout(isShowingLanes, status);
            if (isShowingLanes)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField("Covers individual lane sections in the intersection. Used for high-definition line marking.");
                EditorGUILayout.LabelField("Lane section 0 = Left side of the intersection where oncoming traffic travels. Use lane texture which matches the number of lanes used on the road system, with a white left line and a yellow right line.");

                if (intersection.roadType == RoadIntersection.RoadTypeEnum.BothTurnLanes)
                {
                    EditorGUILayout.LabelField("Lane section 1 = Left turn lane. Use a single lane texture with a yellow left line and a white right line.");
                    EditorGUILayout.LabelField("Lane section 2 = Right outgoing traffic lane. Use lane texture which matches the number of lanes used on the road system with a white right line.");
                    EditorGUILayout.LabelField("Lane section 3 = Right turn lane. Use a single lane texture with a white right line.");
                }
                else if (intersection.roadType == RoadIntersection.RoadTypeEnum.TurnLane)
                {
                    EditorGUILayout.LabelField("Lane section 1 = Left turn lane. Use a single lane texture with a yellow left line and a white right line.");
                    EditorGUILayout.LabelField("Lane section 2 = Right outgoing traffic lane. Use lane texture which matches the number of lanes used on the road system with a white right line.");
                }
                else if (intersection.roadType == RoadIntersection.RoadTypeEnum.NoTurnLane)
                {
                    EditorGUILayout.LabelField("Lane section 1 = Right outgoing traffic lane. Use lane texture which matches the number of lanes used on the road system with a white right line.");
                }

                EditorGUILayout.LabelField(helpText1);
                DoDefaultHelpMat();
                DoDeleteHelpMat();
                EditorGUILayout.EndVertical();
            }
            EditorUtilities.DrawLine();

            //if(isDebug)
            //{
            //	Line();
            //	EditorGUILayout.LabelField("Debug");
            //	if(tInter.Node1 != null)
            //  {
            //      EditorGUILayout.LabelField("  Node1: " + tInter.Node1.transform.name);
            //  }
            //  else
            //  {
            //      EditorGUILayout.LabelField("  Node1: null");
            //  }
            //	if(tInter.Node2 != null)
            //  {
            //      EditorGUILayout.LabelField("  Node2: " + tInter.Node2.transform.name);
            //  }
            //  else
            //  {
            //      EditorGUILayout.LabelField("  Node2: null");
            //  }
            //	if(tInter.Node1 != null)
            //  {
            //      EditorGUILayout.LabelField("  UID1: " + tInter.Node1.UID);
            //  }
            //  else
            //  {
            //      EditorGUILayout.LabelField("  UID1: null");
            //  }
            //	if(tInter.Node2 != null)
            //  {
            //      EditorGUILayout.LabelField("  UID2: " + tInter.Node2.UID);
            //  }
            //  else
            //  {
            //      EditorGUILayout.LabelField("  UID2: null");
            //  }
            //	EditorGUILayout.LabelField("  Same spline: " + tInter.bSameSpline);
            //	EditorGUILayout.LabelField("  bFlipped: " + tInter.bFlipped);
            //	EditorGUILayout.LabelField("  IgnoreSide: " + tInter.IgnoreSide);
            //	EditorGUILayout.LabelField("  IgnoreCorner: " + tInter.IgnoreCorner);
            //			
            //	if(tInter.iStopType == RoadIntersection.iStopTypeEnum.TrafficLight1 || tInter.iStopType == RoadIntersection.iStopTypeEnum.TrafficLight2)
            //  {
            //		if(tInter.LightsRR != null)
            //      {
            //          EditorGUILayout.LabelField("  LightsRR: " + tInter.LightsRR);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsRR: null");
            //      }
            //		if(tInter.LightsRR != null || tInter.LightsRR.MR_Main != null)
            //      {
            //          EditorGUILayout.LabelField("   MR_Main: " + tInter.LightsRR.MR_Main);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsRR.MR_Main: null");
            //      }
            //	    if(tInter.LightsRR != null || tInter.LightsRR.MR_Left != null)
            //      {
            //          EditorGUILayout.LabelField("   MR_Left: " + tInter.LightsRR.MR_Left);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsRR.MR_Left: null");
            //      }
            //	    if(tInter.LightsRR != null || tInter.LightsRR.MR_Right != null)
            //      {
            //          EditorGUILayout.LabelField("   MR_Right: " + tInter.LightsRR.MR_Right);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsRR.MR_Right: null");
            //      }
            //	    if(tInter.LightsRL != null)
            //      {
            //          EditorGUILayout.LabelField("  LightsRL: " + tInter.LightsRL);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsRL: null");
            //      }
            //		if(tInter.LightsLL != null)
            //      {
            //          EditorGUILayout.LabelField("  LightsLL: " + tInter.LightsLL);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsLL: null");
            //      }
            //		if(tInter.LightsLR != null)
            //      {
            //          EditorGUILayout.LabelField("  LightsLR: " + tInter.LightsLR);
            //      }
            //      else
            //      {
            //          EditorGUILayout.LabelField("  LightsLR: null");
            //      }
            //	}
            //  Line();
            //}


            //Set change bools:
            bool isTrafficLightPoleColorToggled = false;
            bool isPointLightsToggled = false;
            bool isGizmoChanged = false;
            bool isMaterialChanged = false;
            // B-14. Separate from isMaterialChanged, and true only when the "Use default materials:" toggle has just
            // been ticked ON this frame. It is what drives ResetMaterialsAll() at the foot of this inspector, which used
            // to run on ANY material change while the toggle merely happened to be set -- and since the toggle ships
            // set, that meant every material an author dropped into a slot was replaced by the tool's default one frame
            // later. The toggle's own promise, in its help text, is about the moment it is checked; this flag is that
            // moment and nothing else.
            bool isDefaultMaterialsTicked = false;
            bool isRoadUpdate = false;

            // B-19. `fields.Changed` belongs in this test and is not belt-and-braces. A deferred control commits on the
            // frame the author presses Enter, moves focus away, or lets a slider handle go -- and on that frame no Unity
            // control reported a change, so GUI.changed is false. Gating this block on GUI.changed alone would skip
            // ApplyModifiedProperties() on exactly the frame that had something to apply, and the next
            // serializedObject.Update() would put the old value back over the committed one.
            if (GUI.changed || fields.Changed)
            {
                //Option: Gizmo:
                if (isDrawingGizmo.boolValue != intersection.isDrawingGizmo)
                {
                    isGizmoChanged = true;
                }

                //Option: Intersection turn lane options
                if ((int)intersection.roadType != roadType.enumValueIndex)
                {
                    isMaterialChanged = true;
                    isRoadUpdate = true;
                }

                //Option: Left yield on green:
                if (isLeftTurnYieldOnGreen.boolValue != intersection.isLeftTurnYieldOnGreen)
                {
                    isRoadUpdate = true;
                }

                //Option: Intersection stop type:
                if (intersectionStopType.enumValueIndex != (int)intersection.intersectionStopType)
                {
                    isRoadUpdate = true;
                }

                //Option: Traffic light poles:
                if (isTrafficPoleStreetLight.boolValue != intersection.isTrafficPoleStreetLight)
                {
                    isRoadUpdate = true;
                }

                //Option: Traffic light pole gray color:
                if (isTrafficLightGray.boolValue != intersection.isTrafficLightGray)
                {
                    isTrafficLightPoleColorToggled = true;
                }

                //Option: Normal pole alignment:
                if (isRegularPoleAlignment.boolValue != intersection.isRegularPoleAlignment)
                {
                    isRoadUpdate = true;
                }

                //Option: Point lights enabled:
                if (isLightsEnabled.boolValue != intersection.isLightsEnabled)
                {
                    isPointLightsToggled = true;
                }

                //Option: Street light range:
                if (!RootUtils.IsApproximately(streetLightRange.floatValue, intersection.streetLightRange, 0.01f))
                {
                    isPointLightsToggled = true;
                }

                //Option: Street light intensity:
                if (!RootUtils.IsApproximately(streetLightIntensity.floatValue, intersection.streetLightIntensity, 0.01f))
                {
                    isPointLightsToggled = true;
                }

                //Option: Street light color:
                if (streetLightColor.colorValue != intersection.streetLightColor)
                {
                    isPointLightsToggled = true;
                }

                //Option: Use default materials:
                if (isUsingDefaultMaterials.boolValue != intersection.isUsingDefaultMaterials)
                {
                    isMaterialChanged = true;
                    // The property holds what the row now shows, the field what the intersection was wearing, so this is
                    // true for a tick and false for an untick. Unticking resets nothing: it hands the seventeen slots
                    // below to the author with whatever is in them, which is the only way in to customising them.
                    isDefaultMaterialsTicked = isUsingDefaultMaterials.boolValue;
                }

                // B-14. Two things stood here, seventeen copies each: a block that cleared the live field whenever its
                // property was null, and the comparison below with a leading `field != null` guard on it. Together they
                // made two of the three gestures an author can make on these seventeen material slots invisible. The
                // mirror nulled the field before the comparison read it, so a material being CLEARED could not be seen;
                // the guard rejected the case where the field was still empty, so a material being ASSIGNED into an
                // empty slot could not be seen either. Only swapping one material for another ever reached
                // UpdateMaterials() -- dropping a marking material into an empty slot, the ordinary gesture, changed the
                // inspector row and nothing on the intersection itself.
                //
                // The mirror is gone: ApplyModifiedProperties() thirty lines below writes every property onto its field,
                // which is the same nulling done once by the one authority for it. And the comparison is stated once and
                // asked of each slot -- the property holds what the author has just put in the row, the field holds what
                // the intersection is wearing, so "they differ" IS the material change, with null on either side meaning
                // cleared or newly assigned. The `||` chain short-circuits, which costs nothing because every operand is
                // a comparison with no side effect -- unlike a chain of GUILayout.Button calls, where short-circuiting
                // is the layout bug this same entry fixed in SplineNEditor.
                if (IsMaterialChanged(markerCenter1, intersection.markerCenter1)
                    || IsMaterialChanged(markerCenter2, intersection.markerCenter2)
                    || IsMaterialChanged(markerCenter3, intersection.markerCenter3)
                    || IsMaterialChanged(markerExtStretch1, intersection.markerExtStretch1)
                    || IsMaterialChanged(markerExtStretch2, intersection.markerExtStretch2)
                    || IsMaterialChanged(markerExtStretch3, intersection.markerExtStretch3)
                    || IsMaterialChanged(markerExtTiled1, intersection.markerExtTiled1)
                    || IsMaterialChanged(markerExtTiled2, intersection.markerExtTiled2)
                    || IsMaterialChanged(markerExtTiled3, intersection.markerExtTiled3)
                    || IsMaterialChanged(lane0Mat1, intersection.lane0Mat1)
                    || IsMaterialChanged(lane0Mat2, intersection.lane0Mat2)
                    || IsMaterialChanged(lane1Mat1, intersection.lane1Mat1)
                    || IsMaterialChanged(lane1Mat2, intersection.lane1Mat2)
                    || IsMaterialChanged(lane2Mat1, intersection.lane2Mat1)
                    || IsMaterialChanged(lane2Mat2, intersection.lane2Mat2)
                    || IsMaterialChanged(lane3Mat1, intersection.lane3Mat1)
                    || IsMaterialChanged(lane3Mat2, intersection.lane3Mat2))
                {
                    isMaterialChanged = true;
                    // B-14. And the toggle above stops claiming otherwise. An author who puts their own material in one
                    // of these seventeen slots is not using the tool's defaults any more, so leaving "Use default
                    // materials:" ticked would leave the inspector stating something untrue about the mesh in front of
                    // it -- and would arm the reset at the foot of this inspector against the next slot they fill.
                    // Written to the property, like everything else in these rows, so ApplyModifiedProperties below
                    // carries it to the field. Not when the toggle itself was the gesture this frame: a tick resets all
                    // seventeen slots to defaults, so it must be left saying exactly that.
                    if (!isDefaultMaterialsTicked)
                    {
                        isUsingDefaultMaterials.boolValue = false;
                    }
                }


                //Apply changes:
                serializedObject.ApplyModifiedProperties();


                //Apply secondary effects:
                if (isGizmoChanged)
                {
                    MeshRenderer[] meshRenderers = intersection.transform.GetComponentsInChildren<MeshRenderer>();
                    int count = meshRenderers.Length;
                    for (int i = 0; i < count; i++)
                    {
                        EngineIntegration.SetSelectedRenderState(meshRenderers[i], intersection.isDrawingGizmo);
                    }
                    SceneView.RepaintAll();
                }

                if (isPointLightsToggled)
                {
                    intersection.TogglePointLights(intersection.isLightsEnabled);
                }

                if (isTrafficLightPoleColorToggled)
                {
                    intersection.ToggleTrafficLightPoleColor();
                }

                // B-14. The four group resets, run here rather than under the icon that asked for them -- see the flags
                // where they are declared. Each is told not to update the mesh itself, because the one UpdateMaterials()
                // below covers all four together, and a reset is a material change like any other.
                if (isCenterMaterialsReset)
                {
                    intersection.ResetCenterMaterials(false);
                    isMaterialChanged = true;
                }

                if (isExtStretchedMaterialsReset)
                {
                    intersection.ResetExtStrechtedMaterials(false);
                    isMaterialChanged = true;
                }

                if (isExtTiledMaterialsReset)
                {
                    intersection.ResetExtTiledMaterials(false);
                    isMaterialChanged = true;
                }

                if (isLanesMaterialsReset)
                {
                    intersection.ResetLanesMaterials(false);
                    isMaterialChanged = true;
                }

                // B-14. The tick comes first and on its own: ResetMaterialsAll ends in UpdateMaterials(), so it already
                // does what the branch below would, and running both would put every material on the mesh twice.
                if (isDefaultMaterialsTicked)
                {
                    intersection.ResetMaterialsAll();
                }
                else if (isMaterialChanged)
                {
                    intersection.UpdateMaterials();
                }

                if (isRoadUpdate)
                {
                    TriggerRoadUpdate();
                }

                EditorUtility.SetDirty(intersection);
            }
        }


        /// <summary> True when the material an author has just put in a row differs from the one the intersection is
        /// wearing now. B-14: the one statement of a comparison that stood written out seventeen times, once per
        /// material slot, each copy carrying a null guard that hid a slot being filled or emptied. The property is what
        /// the row shows and the field is what the mesh has, so a difference either way round -- including to or from
        /// null -- is a material change. The property's own null check is the seventeen copies' protection against a
        /// FindProperty that missed, kept once. </summary>
        private static bool IsMaterialChanged(SerializedProperty _property, Material _current)
        {
            return _property != null && _property.objectReferenceValue != _current;
        }


        public void OnSceneGUI()
        {
            Event current = Event.current;
            int controlID = GUIUtility.GetControlID(GetHashCode(), FocusType.Passive);

            //		if (Event.current.type == EventType.MouseDrag && Event.current.button == 0){
            //			//Update relevant splines:
            //			tInter.Node1.spline.Setup();
            //			if(!tInter.bSameSpline){
            //				tInter.Node2.spline.Setup();
            //			}
            //			bMouseDragHasProcessed = false;
            //		}
            //		
            //		if(Event.current.type == EventType.MouseUp && Event.current.button == 0){
            //			if(!bMouseDragHasProcessed){
            //				tInter.Node1.transform.position = tInter.transform.position;
            //				tInter.Node2.transform.position = tInter.transform.position;
            //				tInter.StartCoroutine(TriggerRoadUpdate());
            //			} 
            //			bMouseDragHasProcessed = true;
            //		}

            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                // B-14. `node1`/`node2` were read here with no check, while the F5 branch below checks both -- so every
                // left-click release in the Scene view, with a leftover intersection selected, was a
                // NullReferenceException. TriggerRoadUpdate reports the same loss when the author asks for the update on
                // purpose; a mouse-up is not asking, so this one is quiet and simply does not drag nodes that are gone.
                if (intersection.node1 != null && intersection.node2 != null
                    && intersection.transform.position != intersection.node1.transform.position)
                {
                    intersection.node1.transform.position = intersection.transform.position;
                    intersection.node2.transform.position = intersection.transform.position;
                    intersection.height = intersection.transform.position.y;
                    TriggerRoadUpdate();
                }
            }



            if (current.type == EventType.ValidateCommand)
            {
                switch (current.commandName)
                {
                    case "UndoRedoPerformed":
                        TriggerRoadUpdate(true);
                        break;
                }
            }

            if (current.type == EventType.MouseDown)
            {
                return;
            }

            if (Selection.activeGameObject == intersection.transform.gameObject)
            {
                if (current.keyCode == KeyCode.F5)
                {
                    if (intersection != null && intersection.node1 != null && intersection.node2 != null)
                    {
                        TriggerRoadUpdate();
                    }
                }
            }

            //		switch(current.type){
            //			case EventType.layout:
            ////			if(current.type != EventType.MouseDown){
            //		        HandleUtility.AddDefaultControl(controlID);
            ////			}
            //		    break;
            //				
            //		}

            if (controlID == 1)
            {
                //Do nothing	
            }
            if (GUI.changed)
            {
                EditorUtility.SetDirty(intersection);
            }
        }


        /// <summary> Say what the authored form resolves to, in words, directly under the fields that decide it.
        ///
        /// The two at-grade forms resolve to a control and to nothing else -- no geometry is derived and no spline is
        /// sampled -- so they answer in one line and cost nothing per repaint. Only a roundabout or an interchange runs
        /// the resolve, which is exactly the case where the author is dimensioning something and wants the refusal while
        /// they are still working on it. B-19's fields commit on focus-out, so every figure quoted here is a committed
        /// one rather than a half-typed one, which is what makes a live verdict readable instead of flickering.
        ///
        /// A report, never a control: nothing here writes to the component, and `junctionPlan` is [NonSerialized], so the
        /// resolve caching into it leaves no scene diff and no dirty flag behind. </summary>
        private void DrawJunctionVerdict(RoadIntersection intersection)
        {
            if (intersection == null)
            {
                return;
            }

            if (City.JunctionForm.IsAtGrade(intersection.junctionType))
            {
                EditorGUILayout.LabelField("Resolves to", ControlDescription(intersection.ResolveControl()));
                return;
            }

            City.JunctionPlan plan = intersection.ResolveJunction();
            if (!plan.resolved)
            {
                EditorGUILayout.HelpBox(plan.message, MessageType.Warning);
                return;
            }

            if (plan.form == City.JunctionTypeEnum.Roundabout)
            {
                EditorGUILayout.LabelField("Circulatory way", plan.circulatoryWidthMetres.ToString("0.##")
                    + " m wide around a " + plan.centralIslandDiameterMetres.ToString("0.##") + " m island");
                EditorGUILayout.LabelField("Tightest leg gap", plan.worstLegSeparationDeg.ToString("0.#") + " deg");
                EditorGUILayout.LabelField("Circulating plane", plan.circulatoryPlaneElevationMetres.ToString("0.##")
                    + " m, worst approach change " + plan.worstApproachGradientChangePercent.ToString("0.##") + " %");
                return;
            }

            EditorGUILayout.LabelField("Raised road", (plan.raisedRoadIsA ? "first" : "second") + ", by "
                + plan.raiseHeightMetres.ToString("0.##") + " m over "
                + plan.requiredApproachLengthMetres.ToString("0.#") + " m of approach on each side");
            EditorGUILayout.LabelField("Deck span to mark", plan.bridgeSpanStartMetres.ToString("0.#") + " m to "
                + plan.bridgeSpanEndMetres.ToString("0.#") + " m along it");
            EditorGUILayout.LabelField("Each ramp", plan.rampLengthMetres.ToString("0.#") + " m long, radius "
                + plan.rampRadiusMetres.ToString("0.#") + " m, grade "
                + (plan.rampGradientFraction * 100.0).ToString("0.##") + " %");
        }


        /// <summary> The one place the four controls are put into words. The control is DERIVED from the form by
        /// <see cref="City.JunctionForm.ControlFor"/>, so this names what that decided and decides nothing itself. </summary>
        private static string ControlDescription(City.JunctionControl control)
        {
            switch (control)
            {
                case City.JunctionControl.Signals: return "signals, so the timings below apply";
                case City.JunctionControl.EntryYield: return "yield at every entry, so no stop line is signed";
                case City.JunctionControl.Uncontrolled: return "no shared surface, so nothing is controlled here";
                default: return "stop or yield on the minor approach";
            }
        }


        /// <summary> Ask the road this intersection sits on to rebuild, carrying the second spline with it.
        ///
        /// B-14. Two of the three ways this declines were silent, and one of them is what the "Update intersection"
        /// button does -- the author's only way to say "redo this junction now" -- so a press could answer with nothing
        /// at all. Both now say why, and only on the forced path. Forced means a discrete gesture: the button, or an
        /// undo/redo. The unforced path runs from a mouse-up in the Scene view and from F5 held down, so a line per
        /// call there would be noise rather than a report, and its refusal is the author's own auto-update toggle
        /// answering exactly as they set it.
        ///
        /// The null branch also grew the four dereferences it was missing. It tested node1 and node2 and then read
        /// `node1.spline.road` on the next line, so a node that outlived its spline or its road threw here instead of
        /// declining -- and OnSceneGUI reaches this from a mouse-up, with no inspector header guard in front of it. </summary>
        private void TriggerRoadUpdate(bool _isForced = false)
        {
            if (!_isForced && !intersection.isAutoUpdatingIntersection)
            {
                return;
            }

            if (intersection == null || intersection.node1 == null || intersection.node2 == null
                || intersection.node1.spline == null || intersection.node2.spline == null
                || intersection.node1.spline.road == null || intersection.node2.spline.road == null)
            {
                if (_isForced)
                {
                    string label = intersection == null ? "An intersection" : "\"" + intersection.name + "\"";
                    Debug.LogWarning("Road & City Master: " + label + " could not be updated, because it has lost one of"
                        + " the two road nodes it was made from. It is left over from a road or node that was deleted,"
                        + " and deleting this GameObject is safe.");
                }
                return;
            }

            if (intersection.node1.spline.road.isEditorConstructing || intersection.node2.spline.road.isEditorConstructing)
            {
                if (_isForced)
                {
                    Road building = intersection.node1.spline.road.isEditorConstructing
                        ? intersection.node1.spline.road : intersection.node2.spline.road;
                    Debug.Log("Road & City Master: \"" + intersection.name + "\" was not updated, because \""
                        + building.name + "\" is being built right now and an intersection cannot be laid into a road"
                        + " mid-build. Watch that build finish in Unity's Background Tasks, then press Update"
                        + " intersection again.");
                }
                return;
            }

            if (!intersection.isSameSpline)
            {
                intersection.node1.spline.road.PiggyBacks = new SplineC[1];
                intersection.node1.spline.road.PiggyBacks[0] = intersection.node2.spline;
            }
            intersection.node1.spline.road.isUpdateRequired = true;
        }


        private void Init()
        {
            isInitialized = true;
            string basePath = RoadEditorUtility.GetBasePath();

            EditorUtilities.LoadTexture(ref refreshButtonTexture, basePath + "/Editor/Icons/refresh2.png");
            EditorUtilities.LoadTexture(ref deleteButtonTexture, basePath + "/Editor/Icons/delete.png");

            if (imageButton == null)
            {
                imageButton = new GUIStyle(GUI.skin.button);
                imageButton.contentOffset = new Vector2(0f, 0f);
                imageButton.border = new RectOffset(0, 0, 0, 0);
                imageButton.fixedHeight = 16f;
                imageButton.padding = new RectOffset(0, 0, 0, 0);
                imageButton.normal.background = null;
            }
        }


        /// <summary> The legend for the reset icon that sits on the material rows. B-14: the five legends in this
        /// inspector all drew their glyph with GUILayout.Button and threw the result away -- pictures that highlight
        /// under the cursor and depress when clicked, in a tool whose real reset icons look exactly the same. Drawn with
        /// Label instead, in the same style, so the glyph is identical and the press is not offered. </summary>
        private void DoDefaultHelpMat()
        {
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
            EditorGUILayout.LabelField(" = Resets material(s) to default materials.");
            EditorGUILayout.EndHorizontal();
        }


        private void DoDeleteHelpMat()
        {
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            // B-14. A legend, not a control -- see DoDefaultHelpMat.
            GUILayout.Label(deleteButtonTexture, imageButton, GUILayout.Width(16f));
            EditorGUILayout.LabelField(" = Removes material(s) from intersection.");
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
