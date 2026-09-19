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
        private SerializedProperty shoulderCrossSlope;
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
        /// <summary> B-6, fault 1. The camera field was written straight onto the component -- both by the object field
        /// and by a repaint-time auto-pick -- so neither could be undone. It goes through the property now, and
        /// <see cref="isCameraPickFailed"/> is what lets the shared control say that a scene with two cameras has no
        /// obvious one, instead of silently trying again next repaint. </summary>
        private SerializedProperty editorPlayCamera;
        private bool isCameraPickFailed = false;

        private SerializedProperty editorCameraMetersPerSecond;
        private SerializedProperty isUsingMeshColliders;
        private SerializedProperty roadMaterialDropdown;
        private SerializedProperty isStatic;
        private SerializedProperty isLightmapped;

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
        /// shoulder width and its cross-fall and max grade belong beside the toggles that decide whether they apply;
        /// the terrain match drop belongs in the terrain box with the rest of the terrain controls. </summary>
        private static readonly string[] drawnInPlace = new string[]
        {
            "laneWidth", "shoulderWidth", "shoulderCrossSlope", "maxGrade", "matchTerrainSubtraction"
        };

        /// <summary> The reset-to-default icon buttons, held as delegates rather than built on each call: an inspector
        /// repaints on mouse movement, and a fresh delegate per field per repaint is garbage an author pays for as
        /// stutter while dragging. Bound once in <see cref="OnEnable"/>. </summary>
        private System.Action resetLaneWidth;
        private System.Action resetShoulderWidth;
        private System.Action resetShoulderCrossSlope;
        private System.Action resetMaxGrade;
        private System.Action resetTerrainSubtraction;

        private GUIStyle warningLabelStyle;
        private GUIStyle imageButton = null;
        // B-14. Gone from here: `maybeButton`, a button style whose only purpose was to colour the *disarmed* half of
        // the terrain-history reset pair. That pair is one button in a disabled group now, which Unity greys for
        // itself, so the style had no site left and its `GUI.skin.button` copy is one fewer skin read per repaint.
        private Texture2D warningLabelBG;
        // B-11. Gone from here: `loadButton` and the two textures it was built from. The branded command-button style is
        // `LegacyInspectorShell.CommandButtonStyle()`, which is where three other editors' copies of it went too.
        private Texture refreshButtonTexture = null;
        private Texture deleteButtonText = null;
        private Texture refreshButtonTextureReal = null;

        //Buffers:
        private bool isNeedingRoadUpdate = false;
        private bool isApplyingMaterialsCheck = false;
        private bool isApplyingMatsCheck = false;

        /// <summary> B-19. This inspector's own deferred-commit controls. One per Editor instance, because the pending
        /// text of a half-typed number belongs to the inspector the author is typing into: two of these open on two
        /// roads -- one of them a locked second inspector -- must not share a buffer. Reset at the top of every pass by
        /// <see cref="FocusOutFields.BeginFrame"/>, and read once after drawing to decide whether to ask for a build. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();
        #endregion


        private void OnEnable()
        {
            road = (Road)target;

            resetLaneWidth = ResetLaneWidth;
            resetShoulderWidth = ResetShoulderWidth;
            resetShoulderCrossSlope = ResetShoulderCrossSlope;
            resetMaxGrade = ResetMaxGrade;
            resetTerrainSubtraction = ResetTerrainSubtraction;

            isGizmosEnabled = serializedObject.FindProperty("isGizmosEnabled");
            lanesForwardProp = serializedObject.FindProperty("lanesForward");
            lanesBackwardProp = serializedObject.FindProperty("lanesBackward");
            laneWidth = serializedObject.FindProperty("laneWidth");
            isShouldersEnabled = serializedObject.FindProperty("isShouldersEnabled");
            shoulderWidth = serializedObject.FindProperty("shoulderWidth");
            shoulderCrossSlope = serializedObject.FindProperty("shoulderCrossSlope");
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
            editorPlayCamera = serializedObject.FindProperty("editorPlayCamera");

            editorCameraMetersPerSecond = serializedObject.FindProperty("EditorCameraMetersPerSecond");
            isUsingMeshColliders = serializedObject.FindProperty("isUsingMeshColliders");
            roadMaterialDropdown = serializedObject.FindProperty("roadMaterialDropdown");
            isStatic = serializedObject.FindProperty("isStatic");
            isLightmapped = serializedObject.FindProperty("isLightmapped");

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
        /// RM-110 (B-9) changed WHERE the numbers come from, because two of them had gone stale and were restoring
        /// values this tool no longer ships: this button wrote 5 m for a lane and 3 m for a shoulder against current
        /// defaults of 3.25 m and 1 m. RM-109 narrowed the shoulder and nothing brought the button with it, which is
        /// what a hand-copied constant does eventually. A "reset to default" that restores a non-default is worse than
        /// no button, because an author has no way to tell it lied to them.
        ///
        /// They read <see cref="RoadClassDefaults"/> now -- the register <see cref="Road.ApplyRoadClassDefaults(bool)"/>
        /// itself reads -- so the button restores exactly what this road's own class would apply, and the two cannot
        /// drift again because there is only one copy of each number. It also makes the answer correct per road rather
        /// than per tool: resetting a motorway's grade to 0.08 handed it a ceiling twice its class's own 0.04, which is
        /// not a default at all. A class the register does not carry returns an incomplete profile, and for that one the
        /// field initialiser is used rather than the register's zero. </summary>
        private void ResetLaneWidth()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                RoadClassProfile p = RoadClassDefaults.For(road.roadClass);
                laneWidth.floatValue = p.Complete ? (float)p.LaneWidthM : 3.25f;
            }
        }


        private void ResetShoulderWidth()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                RoadClassProfile p = RoadClassDefaults.For(road.roadClass);
                shoulderWidth.floatValue = p.Complete ? (float)p.ShoulderWidthM : 1.2f;
            }
        }


        /// <summary> RM-110, B-9. The shoulder cross-fall resets to the normal figure for a paved bituminous shoulder,
        /// not to a per-class number: the register carries no per-class cross-slope, and inventing one would be a
        /// figure with no source behind it. <see cref="CrossSection.CarriagewaySection.ShoulderCrossSlopeNormal"/> is
        /// the same constant the geometry falls back to for a zero, so the button and the fallback agree. </summary>
        private void ResetShoulderCrossSlope()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                shoulderCrossSlope.floatValue = (float)CrossSection.CarriagewaySection.ShoulderCrossSlopeNormal;
            }
        }


        private void ResetMaxGrade()
        {
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                RoadClassProfile p = RoadClassDefaults.For(road.roadClass);
                maxGrade.floatValue = p.Complete ? (float)p.MaxGrade : 0.08f;
            }
        }


        /// <summary> The terrain match drop keeps a literal because no class register carries it: it is a tool constant,
        /// not a road-class dimension, and it still matches the field initialiser it was copied from. </summary>
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
            fields.BeginFrame();
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
            if (GUILayout.Button("Update road", LegacyInspectorShell.CommandButtonStyle()))
            {
                TriggerRoadUpdate();
            }


            EditorGUILayout.LabelField("Hold ctrl and click terrain to add nodes.");
            EditorGUILayout.LabelField("Hold shift and click terrain to insert nodes.");
            EditorGUILayout.LabelField("Select nodes on spline to add objects.");

            // B-5, fault 1. Every one of this inspector's eleven section boundaries was a plain LabelField, drawn at
            // the same weight as the fields under it and as the computed figures printed among them, so nothing on the
            // page told an author where one subject ended and the next began. They go through the shell from here on,
            // which draws one boundary at one weight -- bold, with the same space above it -- and no colon, because a
            // heading has no control on its right to bind to.
            LegacyInspectorShell.SectionHeader("Road options",
                "What this road is and how it is built: its cross-section, the materials it is surfaced with, and the "
                + "gizmos that help you place its nodes.");

            EditorGUILayout.BeginVertical("box");
            // RM-102. The pair of buttons here used to be "Online manual" -- the upstream project's wiki, which is a
            // third party's site and documents a tool this one has diverged from -- and "Offline manual", which opens
            // ours. One manual, one button.
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            //Option: Gizmos input:
            // B-5, faults 2, 3 and 8, and the pattern for every control below. The colon and the trailing space went:
            // Unity's own prefix label separates itself from its control, so "Gizmos: " drew as "Gizmos:  " and lined
            // up with nothing. The casing went with it -- "Gizmo new Node preview Color" capitalised three words of
            // five while the row above it capitalised one -- and each control now carries the sentence an author reads
            // on hover, which is the only place a control can say what it DOES without spending a row of the inspector
            // saying it.
            //
            // B-11, and the rule for every control from here to the foot of the file. This inspector edits the road
            // through `serializedObject`: it reads the properties at :338 and writes them back at :988. So the ONLY
            // value a control may draw itself from is its own serialized property. Reading `road.<field>` instead --
            // which every toggle, every greying scope and every cascade gate below used to do -- asks the object what it
            // held BEFORE the write-back, so the row on screen can disagree with the value being stored for a frame,
            // and a control gated on another control's raw field lags a click behind the toggle that governs it. B-6
            // fault 2 fixed the same three lines in `RoadSystemEditor`; this is the rest of them.
            //
            // The comparisons in the change-detection block are the deliberate exception and are left alone: reading the
            // serialized value and the raw field there is how "did the author just move this" is asked, and answering it
            // needs both the new number and the old one.
            isGizmosEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Gizmos",
                "Draw this road's nodes and its spline in the scene view. They are the handles you place and move "
                + "nodes with, so turning them off makes the road un-editable in the scene until you turn them back on."),
                isGizmosEnabled.boolValue);
            road.newNodePreviewColor = EditorGUILayout.ColorField(new GUIContent("Gizmo new node color",
                "Colour of the preview node that follows the cursor while you hold ctrl over the terrain, before you "
                + "click to place it."), road.newNodePreviewColor);
            road.selectedColor = EditorGUILayout.ColorField(new GUIContent("Gizmo selected color",
                "Colour of the selected node -- the one the scene handles move and the one the node inspector below "
                + "belongs to."), road.selectedColor);
            road.defaultNodeColor = EditorGUILayout.ColorField(new GUIContent("Gizmo default color",
                "Colour of every node on this road that is not selected."), road.defaultNodeColor);

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
            //
            // B-19. `fields` goes in here as well as into the four in-place calls below. It is the same commit buffer
            // for both, which is what makes the rule uniform: whether a value happens to sit in a foldout or beside the
            // toggle that governs it, it reaches the road when the author presses Enter or moves focus away, and a road
            // is rebuilt once for the number they meant rather than once per letter on the way to it.
            LegacyInspectorShell.AllGroups(serializedObject, "Road", fields, drawnInPlace);

            // RM-109. The class popup drawn just above is what the road SAYS it is; this is what makes saying it mean
            // something. Nothing read the choice back out before, so a road could sit at Urban Two Lane with 5 m lanes
            // and a 3 m shoulder and never be contradicted.
            DrawRoadClassReconcile();

            //Option: Lane and road width:
            LegacyInspectorShell.Field(serializedObject, "Road", "laneWidth", fields, resetLaneWidth);
            // B-5, fault 4. The travelled-way width this road works out from its lane width and its lane counts used to
            // be printed right here as a bare label -- "Road width: 10.0 meters" -- in among the fields an author types
            // into, which is how a computed figure comes to read as a field that has stopped accepting input. It is
            // drawn with the inspector's three other measurements now, under the single heading RM-64's policy gives
            // them, at the foot of this inspector.

            //Option: Shoulders enabled:
            isShouldersEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Shoulders enabled",
                "Build a shoulder strip along both edges of the running lanes. It widens the road by the shoulder width "
                + "on each side, and it is the surface a stopped vehicle stands on."), isShouldersEnabled.boolValue);

            //Option: Shoulders width:
            // B-5, fault 7. The width used to VANISH when shoulders were switched off, and a control an author cannot
            // see is a control they cannot learn: the only way to discover that a shoulder has a width of its own was
            // to turn shoulders on and watch a row appear. Greyed instead, it stays legible with its unit and its band,
            // and the toggle immediately above it reads as the thing that governs it. The Field call inside the scope
            // is unchanged to the byte, because LegacyInspectorGate case 2 proves this field is still reachable by
            // finding this exact call.
            using (new EditorGUI.DisabledScope(!road.isShouldersEnabled))
            {
                LegacyInspectorShell.Field(serializedObject, "Road", "shoulderWidth", fields, resetShoulderWidth);
                // RM-110, B-9. The shoulder's cross-fall, drawn under the width it applies across and inside the same
                // greying scope, because a shoulder that is switched off has no fall either. It is the control the
                // author asked for and did not have: the fall has shaped the ramp vertices since RM-94 and the only
                // slope reaching the geometry was the constant normal, so a shoulder could be widened but never
                // steepened for drainage nor flattened to meet an existing verge.
                LegacyInspectorShell.Field(serializedObject, "Road", "shoulderCrossSlope", fields, resetShoulderCrossSlope);
            }

            // RM-102. The road definition -- metres of road per generated segment -- moved into the "what is generated
            // from it" foldout above, where it sits with the other question of that kind rather than between the
            // shoulder width and the material dropdown, neither of which it has anything to do with.

            //Dropdown:
            // B-5, fault 7 again, with the one wrinkle that makes it worth a comment: what governs this control is
            // "Use default materials" down in the materials section, not a toggle beside it. So it is drawn always and
            // greyed when that toggle is off, and its tooltip NAMES the toggle -- a control greyed by something the
            // author cannot see on the same screen otherwise reads as broken rather than as unavailable. The change
            // test below cannot fire while the popup is disabled, which is what disabling it means.
            using (new EditorGUI.DisabledScope(!road.isUsingDefaultMaterials))
            {
                int Old = roadMaterialDropdown.enumValueIndex;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(new GUIContent("Road material",
                    "Which of the tool's own surfacing sets to lay on this road. Available while \"Use default "
                    + "materials\" in the materials section is on; switch that off to assign your own materials there."));
                roadMaterialDropdown.enumValueIndex = EditorGUILayout.Popup(roadMaterialDropdown.enumValueIndex,
                    RoadMaterialDropdownEnumDesc, GUILayout.Width(250f));
                EditorGUILayout.EndHorizontal();
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
            isMaxGradeEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Max grade enforced",
                "Hold the centreline to the grade ceiling below while the road is built, lifting or dropping nodes that "
                + "would breach it. With this off the road follows your nodes however steeply they climb."),
                isMaxGradeEnabled.boolValue);

            //Option: Max grade value:
            // B-5, fault 7. Greyed, not hidden, for the same reason as the shoulder width above: the ceiling is part of
            // what an author needs to know the road HAS, whether or not it is being enforced this minute.
            using (new EditorGUI.DisabledScope(!road.isMaxGradeEnabled))
            {
                // RM-102. This was a 0–1 slider, which let an author set a 100% grade -- a 45° road -- and said
                // nothing about it. Through the drawer it is a fraction with AASHTO's 6–12% working band and a 15%
                // hard ceiling behind it, and a value past that ceiling is reported at the field, not clamped away.
                LegacyInspectorShell.Field(serializedObject, "Road", "maxGrade", fields, resetMaxGrade);
            }

            //Mesh colliders:
            isUsingMeshColliders.boolValue = EditorGUILayout.Toggle(new GUIContent("Use mesh colliders",
                "Give the generated road and shoulder meshes mesh colliders, so vehicles and characters can stand on "
                + "them. Without them the road is visible but nothing can drive on it."), isUsingMeshColliders.boolValue);
            //Option: Multi-threading option: workaround for UAS submission rules:
            isUsingMultithreading.boolValue = EditorGUILayout.Toggle(new GUIContent("Multithreading",
                "Build this road's geometry on worker threads, which is faster on any machine with more than one core. "
                + "Switch it off only to compare a build against the single-threaded result."),
                isUsingMultithreading.boolValue);
            //Static:
            isStatic.boolValue = EditorGUILayout.Toggle(new GUIContent("Static",
                "Mark the generated meshes static, which lets Unity batch them and bake lighting and occlusion for "
                + "them. Correct for a road that will not move once it is built."), isStatic.boolValue);
            //Used for lightmapping:
            isLightmapped.boolValue = EditorGUILayout.Toggle(new GUIContent("Lightmapped",
                "Generate the second UV set a baked lightmap needs on the road meshes. It costs vertices, so leave it "
                + "off unless you are baking lighting for this scene."), isLightmapped.boolValue);
            // B-5, fault 4. "Ramp Height:" was drawn here as a bare float field -- title-cased where nothing beside it
            // was, with no unit, no band, and nothing said about what it shapes -- while every other cross-section
            // dimension the road has went through the registry. It is a registry field now, drawn in the "what it looks
            // like" foldout above with its metres, its 0 to 2 m band and its own sentence, so nothing is drawn here.

            //Option: Save meshes as unity assets options:
            isSavingMeshes.boolValue = EditorGUILayout.Toggle(new GUIContent("Save mesh assets",
                "Write each generated mesh into the project as an asset instead of leaving it in the scene. It makes "
                + "the scene file far smaller and the build far slower."), isSavingMeshes.boolValue);
            if (isSavingMeshes.boolValue)
            {
                // B-6, fault 3. This was a hand-styled red text area holding a sentence that also existed, byte for
                // byte, in `RoadSystemEditor`. One sentence, one warning box, drawn the way the rest of the tool draws a
                // warning. `warningLabelStyle` survives below for the terrain-history reset, which is a different
                // warning about a different thing and is now its only remaining use.
                LegacyInspectorShell.SavingMeshesWarning();
            }


            RenderRoadHelpDialog();


            EditorGUILayout.EndVertical();

            LegacyInspectorShell.SectionHeader("Terrain options",
                "How this road reshapes the ground it is built on: the strip of terrain whose heights are matched to "
                + "the road surface, and the grass and trees cleared out of the road's way.");
            EditorGUILayout.BeginVertical("box");

            //Option: Terrain subtraction:
            LegacyInspectorShell.Field(serializedObject, "Road", "matchTerrainSubtraction", fields, resetTerrainSubtraction);

            //Option: Spline magnitude limit:
            // B-5, faults 5 and 8, on the seven sliders this inspector owns that are NOT registry fields. Each one is a
            // measurement and each one used to print a bare number, so they get the same unit cell the registry rows
            // get -- from InspectorFieldDrawer.Unit, the same method, so the column cannot drift apart between the two
            // kinds of row. The cell's own hover names the token rather than repeating the field's sentence: the
            // sentence is already on the label an inch to the left, and a literal repeated twice per row is a literal
            // that gets corrected once.
            EditorGUILayout.BeginHorizontal();
            fields.FloatSlider(magnitudeThreshold, "Spline magnitude limit", 128f, 8192f,
                "Ceiling in metres on the length of the curve tangents between nodes. Where two nodes are further "
                + "apart than this, the curve between them is pulled straighter instead of bowing out past them. Raise "
                + "it for long sweeping roads; lower it if a wide gap overshoots.");
            InspectorFieldDrawer.Unit("m", "metres");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                magnitudeThreshold.floatValue = 300f;
            }
            EditorGUILayout.EndHorizontal();

            // B-5, fault 7, and the one place in this inspector where obeying it meant MOVING controls rather than just
            // greying them. The three switches used to be stacked together with their five widths in a block below, so
            // greying the widths in place would have left an author looking at five grey rows with no way to tell which
            // of the three switches above governed which. Each switch now stands immediately over the widths it
            // governs, which is the same arrangement the shoulder width above is drawn in.
            //Option: Height modification
            isHeightModificationEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Height modification",
                "Match the terrain heights along this road to the road surface as it is built, so the ground meets the "
                + "carriageway instead of cutting through it or leaving it standing in the air."),
                isHeightModificationEnabled.boolValue);

            //Option: heights width
            using (new EditorGUI.DisabledScope(!road.isHeightModificationEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider(matchHeightsDistance, "Heights match width", 0.01f, 512f,
                    "Total width in metres of the strip whose heights are matched to the road. Half of it reaches "
                    + "beyond each edge of the running lanes, so 50 m leaves 25 m of matched ground on either side.");
                InspectorFieldDrawer.Unit("m", "metres");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    matchHeightsDistance.floatValue = 50f;
                }
                EditorGUILayout.EndHorizontal();
            }

            //Option: Active detail removal
            isDetailModificationEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Active detail removal",
                "Clear terrain detail -- grass and the other detail meshes -- out of the strip this road runs through, "
                + "so nothing is left growing up through the surface. On a large terrain carrying dense grass this is "
                + "memory-heavy: consider clearing the detail map yourself and leaving this off."),
                isDetailModificationEnabled.boolValue);

            //Option: details width and height
            using (new EditorGUI.DisabledScope(!road.isDetailModificationEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider(clearDetailsDistance, "Details clear width", 0.01f, 512f,
                    "Total width in metres of the strip detail is cleared from, centred on the road.");
                InspectorFieldDrawer.Unit("m", "metres");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearDetailsDistance.floatValue = 30f;
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider(clearDetailsDistanceHeight, "Details clear height", 0.01f, 512f,
                    "Vertical reach in metres. Detail whose ground sits further above or below the road surface than "
                    + "this is left in place, so a road in a cutting does not strip the slopes standing over it.");
                InspectorFieldDrawer.Unit("m", "metres");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearDetailsDistanceHeight.floatValue = 5f;
                }

                EditorGUILayout.EndHorizontal();
            }

            //Option: Active tree removal
            isTreeModificationEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Active tree removal",
                "Remove terrain trees standing in the strip this road runs through, so no trunk is left growing out of "
                + "the carriageway. On a large forested terrain this is memory-heavy: consider clearing the trees "
                + "yourself and leaving this off."), isTreeModificationEnabled.boolValue);

            //Option: tree widths and height
            using (new EditorGUI.DisabledScope(!road.isTreeModificationEnabled))
            {
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider(clearTreesDistance, "Trees clear width", 0.01f, 512f,
                    "Total width in metres of the strip trees are removed from, centred on the road. A width narrower "
                    + "than the road itself is raised to the road's own width, since no tree can stand in a lane.");
                InspectorFieldDrawer.Unit("m", "metres");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearTreesDistance.floatValue = 30f;
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider(clearTreesDistanceHeight, "Trees clear height", 0.01f, 512f,
                    "Vertical reach in metres. A tree whose ground sits further above or below the road surface than "
                    + "this is left standing, so a road through a valley does not fell the hillside above it.");
                InspectorFieldDrawer.Unit("m", "metres");
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    clearTreesDistanceHeight.floatValue = 50f;
                }
                EditorGUILayout.EndHorizontal();
            }


            //Option: terrain history save type:
            // B-5, faults 2 and 8, plus a LabelField doing a label's job for a control that had none of its own. The
            // toggle was drawn bare in a 50-pixel cell with its sentence beside it as a separate LabelField, so the
            // words did not belong to the control: they did not highlight with it, they did not grey with it, and they
            // could carry no hover. It is one toggle with one label now, the shape every other switch here has.
            isSavingTerrainHistoryOnDisk.boolValue = EditorGUILayout.Toggle(
                new GUIContent("Separate terrain history store",
                "Keep the record of what this road changed on the terrain in a file beside the scene instead of inside "
                + "the scene itself. It keeps the scene file small, and that record is what lets the ground be put back "
                + "as it was."), isSavingTerrainHistoryOnDisk.boolValue);

            // B-5, fault 4. The size of that store was printed here as a bare label, in among the switches an author
            // types into. It is drawn with this inspector's three other figures in the measured group at the foot.


            RenderTerrainHelpDialog();

 
            EditorGUILayout.EndVertical();



            LegacyInspectorShell.SectionHeader("Road and shoulder splitting",
                "Whether this road's mesh is cut into separate pieces where it meets other roads, and whether the tool "
                + "does that cutting or you do.");
            EditorGUILayout.BeginVertical("box");
            GUILayout.Space(4f);

            // B-5, faults 1, 2, 3, 7 and 8 together, and the section where fault 7 was doing the most damage. Turning
            // manual splitting on REPLACED the two automatic switches with two labels reading "Manual road splitting:
            // true" -- a value where a control had been, so both real settings became invisible and unreachable at the
            // same moment, and neither label was a control an author could put back. The switch that governs them is
            // drawn first now and the two it governs stay in place, greyed. Its refresh button is OUTSIDE that grey:
            // switching manual splitting back off is the button's whole purpose, so it is the one control here that
            // has to keep working in the manual state -- which is why the original drew a copy of it in both branches.
            //Option: manual splitting:
            EditorGUILayout.BeginHorizontal();
            isDynamicCutsEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Manual splitting",
                "Split this road yourself, at the nodes you choose, instead of letting the tool split it where it "
                + "meets other roads. The two automatic switches below are greyed while this is on."),
                isDynamicCutsEnabled.boolValue);
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                isDynamicCutsEnabled.boolValue = false;
                isRoadCutsEnabled.boolValue = true;
                isShoulderCutsEnabled.boolValue = true;
            }
            EditorGUILayout.EndHorizontal();

            //Option: road cuts:
            using (new EditorGUI.DisabledScope(road.isDynamicCutsEnabled))
            {
                isRoadCutsEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Auto split road",
                    "Cut the road mesh where it meets an intersection, so the piece inside the junction can be hidden "
                    + "and the junction's own surface drawn there instead."), isRoadCutsEnabled.boolValue);
            }

            //Option: shoulder cuts:
            using (new EditorGUI.DisabledScope(road.isDynamicCutsEnabled || !road.isShouldersEnabled))
            {
                isShoulderCutsEnabled.boolValue = EditorGUILayout.Toggle(new GUIContent("Auto split shoulders",
                    "Cut the shoulder meshes in the same places as the road. Available while this road has shoulders."),
                    isShoulderCutsEnabled.boolValue);
            }


            RenderCutsHelpDialog();

 
            EditorGUILayout.EndVertical();


            //Camera:
            LegacyInspectorShell.SectionHeader("Editor camera travel",
                "Fly a camera along this road without entering play mode, to look at what you have built from the "
                + "height and angle a driver on it would see.");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();

            //Option: Editor camera meters per sec
            fields.FloatSlider(editorCameraMetersPerSecond, "Camera speed", 1f, 512f,
                "How fast the camera travels along the road, in metres per second. The default 60 m/s is 216 km/h; a "
                + "motorway cruise is nearer 30 m/s and a town street nearer 14 m/s.");
            InspectorFieldDrawer.Unit("m/s", "metres per second");
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                editorCameraMetersPerSecond.floatValue = 60f;
            }
            EditorGUILayout.EndHorizontal();

            //Option: Editor camera auto rotate:
            isEditorCameraRotated.boolValue = EditorGUILayout.Toggle(new GUIContent("Camera auto rotate",
                "Turn the camera to face along the road as it travels, instead of holding the direction it set out in."),
                isEditorCameraRotated.boolValue);
            isCameraPickFailed = LegacyInspectorShell.CameraField(editorPlayCamera,
                "The camera that gets flown along this road. Fill it in from the scene with the button below, or drag "
                + "the camera to use into the field.", isCameraPickFailed);
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

            LegacyInspectorShell.SectionHeader("Materials",
                "What this road is surfaced with: the base material of the road and its shoulders, the markings painted "
                + "on them, and the physics materials that decide how much grip the finished surface has.");
            EditorGUILayout.BeginVertical("box");
            //Road material defaults:


            RenderRoadMaterials();

            RenderRoadMarkerMaterials();

            RenderShoulderMaterials();

            RenderShoulderMarkerMaterials();


            //Physics materials:
            LegacyInspectorShell.SectionHeader("Physics material defaults",
                "The physics materials given to the colliders this road generates, which is what decides how much "
                + "friction a wheel or a foot finds on the surface.");
            //Option: physical road mat:
            fields.Property(roadPhysicMaterial, new GUIContent("Road physics material",
                "Physics material for the road surface's own colliders. Left empty, those colliders fall back on "
                + "Unity's default friction rather than on anything this tool chose."));


            //Option: physical shoulder mat:
            fields.Property(shoulderPhysicMaterial, new GUIContent("Shoulder physics material",
                "Physics material for the shoulder colliders, which is normally the rougher of the two: a shoulder is "
                + "gravel or grass where the road is asphalt."));


            GUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            //Option: Apply above materials to entire road:
            // B-5, faults 2 and 8. Another bare toggle with its sentence sitting beside it as a LabelField, and the one
            // here mattered more than most: the words the author reads are a WARNING about what the button does, and
            // words that are not the control's label are not on the control's hover when they are needed.
            isApplyingMaterialsCheck = EditorGUILayout.Toggle(new GUIContent("Apply materials to entire road",
                "Arm the Apply button beside this, which lays the materials above onto every piece of this road at "
                + "once, including the pieces cut out at its junctions."), isApplyingMaterialsCheck);
            // B-14. The press used to be accepted and then discarded when the toggle beside it was clear -- a button
            // that answers a click with nothing at all, which is this entry's own definition of a control
            // indistinguishable from a stub. Disabling it says the same thing the swallowed press was trying to say,
            // and says it before the author clicks rather than after.
            using (new EditorGUI.DisabledScope(!isApplyingMaterialsCheck))
            {
                if (GUILayout.Button("Apply", EditorStyles.miniButton, GUILayout.Width(60f)))
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
            isResetingTH = EditorGUILayout.Toggle(new GUIContent("Reset road's terrain history",
                "Arm the button beside this. A reset throws away the record of the ground this road changed, after "
                + "which that ground can no longer be put back as it was -- read the warning it prints once armed."),
                isResetingTH);
            // B-14. This was two buttons in an if/else on the same flag, and the disarmed one read
            // `if (GUILayout.Button(...) && isResetingTH)` inside the branch where `isResetingTH` is false by
            // construction -- a body that could never run, guarding a press that was therefore silently dropped. One
            // button in a disabled group is the same gesture with the dead half deleted rather than left beside it.
            using (new EditorGUI.DisabledScope(!isResetingTH))
            {
                if (GUILayout.Button("Reset"))
                {
                    road.ResetTerrainHistory();
                    isResetingTH = false;
                    // The work is a deletion, it is irreversible by the warning's own admission, and it leaves nothing
                    // on screen that changed -- so without this line the author cannot tell a reset from a dropped
                    // click.
                    Debug.Log(InspectorFieldRegistry.ProductDisplayName + ": the terrain history of \"" + road.name
                        + "\" was reset. The ground this road changed can no longer be put back as it was.");
                }
            }
            EditorGUILayout.EndHorizontal();

            if (isResetingTH)
            {
                EditorGUILayout.LabelField("WARNING: This option can't be undone! Only reset the terrain history if you have changed terrain resolution data such as heightmap or detail resolutions. A rare event may occur when editing and compiling this addon's scripts that a terrain history reset may be necessary. Treat this reset as a last resort.", warningLabelStyle);
            }
            GUILayout.Space(6f);


            // B-5, fault 4, and the last of it. "Statistics:" was a box of bare labels -- a length and a node count here,
            // a road width and a terrain history size printed further up the page -- each drawn in the same LabelField
            // the editable rows used, in four separate places. All four are computed, none of them is editable, and
            // RM-64's policy gives measurements ONE heading, so all four are drawn here, closed until an author opens
            // them because a measurement is an answer they go looking for rather than somewhere they work. One group
            // and not four: BeginMeasured keys its open state by COMPONENT, so a second BeginMeasured("Road") on this
            // page would share this one's key and open and close with it.
            bool measuredOpen = LegacyInspectorShell.BeginMeasured("Road");
            if (measuredOpen)
            {
                LegacyInspectorShell.Measured("Travelled-way width", road.RoadWidth().ToString("F1"), "m");
                LegacyInspectorShell.Measured("Centreline length", road.spline.distance.ToString("F1"), "m");
                LegacyInspectorShell.Measured("Terrain history store", road.TerrainHistoryByteSize, null);
                LegacyInspectorShell.Measured("Nodes in last build", road.MostRecentNodeCount.ToString(), null);

                // RM-110, B-9. The three figures an author asked for and this inspector could not answer: how much
                // geometry the road actually is, and when it was last built.
                //
                // Both are computed here rather than stored, and for different reasons. The counts are summed from the
                // meshes under the road on each repaint, so nothing can be stale about them -- an author who deletes an
                // edge object or undoes a build sees the number move. The build time cannot be measured from anything,
                // so it is a stamp the road writes when a build finishes; a road that has never been built says so in
                // words instead of showing a date of nothing.
                //
                // Drawn inside the `if (measuredOpen)` and nowhere else, which is what keeps MeshTally's walk of the
                // child MeshFilters off the cost of every repaint of a closed foldout. "n0" for the counts, the same
                // spelling the terrain history size uses, because six unseparated digits are read wrongly at a glance.
                int meshVertices;
                int meshTriangles;
                road.MeshTally(out meshVertices, out meshTriangles);
                LegacyInspectorShell.Measured("Mesh vertices", meshVertices.ToString("n0"), null);
                LegacyInspectorShell.Measured("Mesh triangles", meshTriangles.ToString("n0"), null);

                string lastBuilt = road.LastBuildLocalText();
                LegacyInspectorShell.Measured("Last built", lastBuilt == null ? "never" : lastBuilt, null);
            }
            LegacyInspectorShell.EndMeasured(measuredOpen);

            bool isLaneChanged = false;
            bool isTerrainHistoryChanged = false;
            bool isEditorCameraSpeedChanged = false;

            // B-19. `fields.Changed` has to be part of this test, and it is not belt-and-braces: it is the whole point.
            // A deferred control commits on the frame the author presses Enter, moves focus away, or lets a slider
            // handle go -- and on that frame no Unity control reported a change, so GUI.changed is false. Gating this
            // block on GUI.changed alone would skip ApplyModifiedProperties() on exactly the frame that has something
            // to apply, and the next serializedObject.Update() would overwrite the committed value with the old one.
            if (GUI.changed || fields.Changed)
            {
                if (isGizmosEnabled.boolValue != road.isGizmosEnabled)
                {
                    road.ToggleWireframes();
                    SceneView.RepaintAll();
                }

                // B-19. A rebuild is asked for on the COMMIT, not on a difference. The three pre-handle tests below are
                // still right for what they do -- they name WHICH value moved, and each has a side effect of its own to
                // run -- but "does the serialised value differ from the live one" was the wrong question to ask about
                // rebuilding, because a per-keystroke control made it true on every frame of typing. The deferred commit
                // is what says the author finished, so that is what asks.
                //
                // Minus the editor camera speed, which is the one committed value on this inspector that is not part of
                // the road: it moves the scene view's fly speed and nothing about the mesh. It has to be tested first
                // for this subtraction to see it, which is why this sits below the pre-handles rather than above them.

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

                if (fields.Changed && !isEditorCameraSpeedChanged)
                {
                    isNeedingRoadUpdate = true;
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
                    // B-14. SetAllCutsToCurrentMaterials returns without a word when there is no road mesh, and this is
                    // the one press where that silence is a lie: the whole gesture is "put these materials on the road
                    // that is there now", and on an unbuilt road there are no renderers to put them on. The two other
                    // callers of it in this inspector are material CHANGES -- those land in the road's own fields and
                    // the next build carries them out, so nothing is lost and a warning there would be noise. Only this
                    // one accomplishes literally nothing, so only this one says so.
                    if (road.MeshRoad == null)
                    {
                        Debug.LogWarning(InspectorFieldRegistry.ProductDisplayName + ": \"" + road.name + "\" has no"
                            + " road mesh yet, so there was nothing for Apply to put the materials on. Build the road"
                            + " first; the materials listed here are already saved and the build will use them.");
                    }
                    road.SetAllCutsToCurrentMaterials();
                }

                EditorUtility.SetDirty(target);
            }
        }


        /// <summary> The four slot labels, spelled once. B-5, faults 2 and 3: they read `"  Mat #1: "` -- two spaces
        /// faking an indent, an abbreviation nothing else in the inspector uses, and a trailing colon. The indent is
        /// real indentation now (<see cref="EditorGUI.indentLevel"/>, in <see cref="MaterialSlot"/>), and the word is
        /// the one the rest of the tool uses. </summary>
        private static readonly string[] MaterialSlotLabels = { "Material 1", "Material 2", "Material 3", "Material 4" };


        /// <summary> One material slot: the object field, and the narrow button that clears it, on one row. Returns true
        /// on the frame the clear button is pressed, which is the ONLY thing the four callers still do for themselves --
        /// each writes null to its own property, so the sixteen assignments stay exactly where they were and this method
        /// needs no knowledge of which stack it is drawing.
        ///
        /// B-5's largest single piece of duplication lived here: sixteen copies of the same four lines, once per slot
        /// per stack, differing only in a property and a label. Sixteen copies of a row is sixteen places a column
        /// width, a button size or a label style can drift apart, and the four stacks HAD drifted -- only the road base
        /// stack carried the refresh button, and each stack spelled its heading its own way.
        ///
        /// The indent is what the two leading spaces in the old label were reaching for: these rows sit under the
        /// heading of the stack they belong to, and an author reading down four headings needs to see which rows hang
        /// off which. `indentLevel` indents the FIRST control in a horizontal group, which is this row's object field;
        /// the clear button follows it across, so a slot indents as one row rather than splitting. </summary>
        private bool MaterialSlot(SerializedProperty prop, int index, string tooltip)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginHorizontal();
            fields.Property(prop, new GUIContent(MaterialSlotLabels[index], tooltip));
            bool isClearing = GUILayout.Button(deleteButtonText, imageButton, GUILayout.Width(16f));
            EditorGUILayout.EndHorizontal();
            EditorGUI.indentLevel--;
            return isClearing;
        }


        private void RenderRoadMaterials()
        {
            LegacyInspectorShell.SectionHeader("Road base materials",
                "The material laid on the road's driving surface, with three further slots for a mesh that needs more "
                + "than one. Each slot opens once the slot above it has been filled.");

            //Option: Use default materials:
            // The refresh button was in the heading row, which is where it could not stay: a heading goes through the
            // shell now and the shell puts vertical space above it, and vertical space inside a horizontal group is
            // horizontal space. It belongs on this row anyway -- putting the tool's own materials back is exactly the
            // reset for this toggle, and every other reset button in this inspector sits on the row it resets.
            EditorGUILayout.BeginHorizontal();
            isUsingDefaultMaterials.boolValue = EditorGUILayout.Toggle(new GUIContent("Use default materials",
                "Surface this road with the set named by \"Road material\" at the top of this inspector instead of the "
                + "materials listed below. Switch it off to assign your own."), isUsingDefaultMaterials.boolValue);
            //Option: Set materials to default:
            if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
            {
                road.SetDefaultMats();
                road.SetAllCutsToCurrentMaterials();
            }
            EditorGUILayout.EndHorizontal();

            string slotTip = "Material for the road's driving surface. It is UV mapped on world coordinates, so the "
                + "tiling runs unbroken from one piece of road into the next.";
            // B-11. The clear button beside each slot DID NOTHING. It wrote `road.RoadMaterialN = null` -- a direct field
            // write, made between this inspector's serializedObject.Update() and its ApplyModifiedProperties(), so the
            // write-back at the end of the same frame put the material straight back from the serialised copy that had
            // never been told. The clear goes through the property, like the assignment beside it. The cascade asks the
            // property too, so a slot filled this frame opens the next one this frame instead of one repaint later.
            if (MaterialSlot(roadMaterial1, 0, slotTip)) { roadMaterial1.objectReferenceValue = null; }
            if (roadMaterial1.objectReferenceValue != null)
            {
                if (MaterialSlot(roadMaterial2, 1, slotTip)) { roadMaterial2.objectReferenceValue = null; }
                if (roadMaterial2.objectReferenceValue != null)
                {
                    if (MaterialSlot(roadMaterial3, 2, slotTip)) { roadMaterial3.objectReferenceValue = null; }
                    if (roadMaterial3.objectReferenceValue != null)
                    {
                        if (MaterialSlot(roadMaterial4, 3, slotTip)) { roadMaterial4.objectReferenceValue = null; }
                    }
                }
            }
        }


        private void RenderRoadMarkerMaterials()
        {
            //		//Road marker material defaults:
            LegacyInspectorShell.SectionHeader("Road marker materials",
                "The materials that carry this road's painted markings.");

            string slotTip = "Material for the lane markings. It is laid OVER the base material as a transparent "
                + "overlay, so a marker material with no transparency hides the road surface under it.";
            if (MaterialSlot(roadMaterialMarker1, 0, slotTip)) { roadMaterialMarker1.objectReferenceValue = null; }
            if (roadMaterialMarker1.objectReferenceValue != null)
            {
                if (MaterialSlot(roadMaterialMarker2, 1, slotTip)) { roadMaterialMarker2.objectReferenceValue = null; }
                if (roadMaterialMarker2.objectReferenceValue != null)
                {
                    if (MaterialSlot(roadMaterialMarker3, 2, slotTip)) { roadMaterialMarker3.objectReferenceValue = null; }
                    if (roadMaterialMarker3.objectReferenceValue != null)
                    {
                        if (MaterialSlot(roadMaterialMarker4, 3, slotTip)) { roadMaterialMarker4.objectReferenceValue = null; }
                    }
                }
            }
        }


        private void RenderShoulderMaterials()
        {
            //		//Shoulder material defaults:
            // The whole section stays HIDDEN when this road has no shoulders, which is the other half of B-5's fault 7:
            // a value-bearing field is greyed so an author can still read what the road HAS, but a section describing a
            // part the road does not have has nothing to read.
            if (isShouldersEnabled.boolValue)
            {
                LegacyInspectorShell.SectionHeader("Shoulder base materials",
                    "The material laid on the shoulder strips that run along each edge of the road.");

                string slotTip = "Material for the shoulder surface, which is normally the coarser of the two: a "
                    + "shoulder is gravel or grass where the road is asphalt.";
                if (MaterialSlot(shoulderMaterial1, 0, slotTip)) { shoulderMaterial1.objectReferenceValue = null; }
                if (shoulderMaterial1.objectReferenceValue != null)
                {
                    if (MaterialSlot(shoulderMaterial2, 1, slotTip)) { shoulderMaterial2.objectReferenceValue = null; }
                    if (shoulderMaterial2.objectReferenceValue != null)
                    {
                        if (MaterialSlot(shoulderMaterial3, 2, slotTip)) { shoulderMaterial3.objectReferenceValue = null; }
                        if (shoulderMaterial3.objectReferenceValue != null)
                        {
                            if (MaterialSlot(shoulderMaterial4, 3, slotTip)) { shoulderMaterial4.objectReferenceValue = null; }
                        }
                    }
                }
            }
        }


        private void RenderShoulderMarkerMaterials()
        {
            ////Shoulder marker material defaults:
            if (isShouldersEnabled.boolValue)
            {
                LegacyInspectorShell.SectionHeader("Shoulder marker materials",
                    "The materials that carry the markings painted on the shoulders.");

                string slotTip = "Material for the edge line at the outside boundary of the running lanes. It is laid "
                    + "over the shoulder surface as a transparent overlay.";
                if (MaterialSlot(shoulderMaterialMarker1, 0, slotTip))
                {
                    shoulderMaterialMarker1.objectReferenceValue = null;
                }
                if (shoulderMaterialMarker1.objectReferenceValue != null)
                {
                    if (MaterialSlot(shoulderMaterialMarker2, 1, slotTip))
                    {
                        shoulderMaterialMarker2.objectReferenceValue = null;
                    }
                    if (shoulderMaterialMarker2.objectReferenceValue != null)
                    {
                        if (MaterialSlot(shoulderMaterialMarker3, 2, slotTip))
                        {
                            shoulderMaterialMarker3.objectReferenceValue = null;
                        }
                        if (shoulderMaterialMarker3.objectReferenceValue != null)
                        {
                            if (MaterialSlot(shoulderMaterialMarker4, 3, slotTip))
                            {
                                shoulderMaterialMarker4.objectReferenceValue = null;
                            }
                        }
                    }
                }
            }
        }


        /// <summary> B-5. The head of a quick-help panel: the panel's name, the button that opens the offline manual,
        /// and the line pointing at the online one. It stood four times in this file in four different shapes -- one
        /// drew the heading and the button inside a horizontal group, one drew them with no group so the button fell
        /// onto its own line, one left the HelpBox out altogether, and every heading ended in a colon (fault 2). The
        /// shape is settled once here, so the four panels open the same way and a fifth cannot open a sixth way.
        ///
        /// <paramref name="title"/> arrives WITHOUT a colon and is not given one. </summary>
        private void RenderHelpHeader(string title)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(onlineHelpDesc, MessageType.Info);
        }


        /// <summary> One paragraph of help text, and the gap under it. Roughly thirty of these were written out as a
        /// LabelField and a Space in a row, and they had drifted the way repeated pairs do: most took the default label
        /// style, which does not wrap -- a help sentence longer than the inspector is wide was simply cut off at the
        /// edge -- and one took `miniLabel` for no reason but the order it was typed in.
        ///
        /// `wordWrappedLabel` is READ, never written: B-3's fault was mutating the instances the editor owns, and
        /// <see cref="InspectorStyleOwnershipGate"/> holds that line. These panels draw only from OnInspectorGUI, so
        /// the skin is always up by the time this runs. </summary>
        private void RenderHelpParagraph(string text)
        {
            EditorGUILayout.LabelField(text, EditorStyles.wordWrappedLabel);
            GUILayout.Space(4f);
        }


        /// <summary> The reset legend that closes a help panel: the icon, and what pressing it does. Five identical
        /// copies, one per panel.
        ///
        /// The picture is drawn with GUILayout.Label rather than GUILayout.Button. B-14: it was a Button whose result was
        /// thrown away, which is a control that lights up under the cursor, depresses when clicked, and does nothing --
        /// the clearest case in the tool of what this entry calls indistinguishable from a stub, and B-5's pass left it
        /// as one site to judge rather than five to find. A legend is not a control, so it is no longer drawn as one.
        /// The style is the same, so the glyph looks identical to the buttons it explains. </summary>
        private void RenderResetLegend()
        {
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
            EditorGUILayout.LabelField("= Resets that row to its default value.");
            EditorGUILayout.EndHorizontal();
        }


        private void RenderMaterialsHelpDialog()
        {
            //Help toggle for materials
            GUILayout.Space(4);
            isShowingMaterialsHelp = EditorGUILayout.Foldout(isShowingMaterialsHelp, status);
            if (isShowingMaterialsHelp)
            {
                EditorGUILayout.BeginVertical("box");
                RenderHelpHeader("Materials quick help");
                // B-5, the other half of fault 8. This panel held seven paragraphs and six of them described one field
                // each: what the base material is, how the marker material is mapped, what the physics materials are
                // for. Every one of those fields carries that sentence on its own hover now, and a description written
                // twice is a description corrected once -- the copy an author is NOT reading is the one that goes
                // stale. What stays is the paragraph that is not about a field: what becomes of these materials once
                // the road has been split into pieces.
                EditorGUILayout.LabelField("The materials above are this road's defaults. A road split at its "
                    + "junctions can carry different materials on each piece, set on that piece's own mesh renderer, "
                    + "and where a piece names its own material that is used instead of the default.",
                    EditorStyles.wordWrappedLabel);
                RenderResetLegend();
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
                RenderHelpHeader("Editor camera quick help");
                // Three paragraphs left this panel for the hovers of the three controls they described. This one stays
                // because it is not about a control: it is about the difference between what the scene view shows and
                // what the game camera will.
                EditorGUILayout.LabelField("Materials behave differently in the editor scene view than in play mode. "
                    + "If a surface flickers against another, or a marker will not sit on top of the road, look at it "
                    + "through the game camera before changing the materials.",
                    EditorStyles.wordWrappedLabel);
                RenderResetLegend();
                EditorGUILayout.EndVertical();
            }
        }


        private void RenderCutsHelpDialog()
        {
            isShowingCutsHelp = EditorGUILayout.Foldout(isShowingCutsHelp, status);
            if (isShowingCutsHelp)
            {
                EditorGUILayout.BeginVertical("box");
                RenderHelpHeader("Road splitting quick help");
                RenderHelpParagraph("Splitting cuts this road's mesh into separate pieces at the nodes, so each piece "
                    + "can carry its own materials -- a passing section with dotted lines, a no-passing stretch with "
                    + "double solid ones, road debris along one length and not another.");
                RenderHelpParagraph("It can also make collision cheaper, since several smaller mesh colliders cost "
                    + "less to test than one very large one. Automatic splitting is the right choice for most roads.");

                EditorGUILayout.LabelField("Which splitting option to choose?", EditorStyles.boldLabel);
                RenderHelpParagraph("No splitting: one material set for the whole road, and the fewest game objects. "
                    + "Right where collision is no bottleneck and the road needs no variation along its length.");
                RenderHelpParagraph("Automatic splitting: a piece per node, cut for you. Right where the road needs "
                    + "different markings along its length, or where one large mesh collider is costing too much.");
                RenderHelpParagraph("Manual splitting: the same, but you choose which nodes are cut. It leaves fewer "
                    + "and larger pieces than automatic splitting, and so fewer game objects.");

                EditorUtilities.DrawLine();
                EditorGUILayout.LabelField("Manual splitting", EditorStyles.boldLabel);
                RenderHelpParagraph("With manual splitting on you mark the nodes to cut at yourself. The colliders that "
                    + "result are larger and more complex than automatic splitting's, so check what collision costs in "
                    + "play mode: if it is slow, cut at more nodes.");
                RenderHelpParagraph("A hill you want double solid lines on: mark the node at each foot of the hill as "
                    + "a road cut. What lies between them becomes a piece of its own, and the no-passing lines go on "
                    + "that piece alone.");
                RenderHelpParagraph("A straight in the mountains you want passing allowed on: mark the node at each "
                    + "end of the straight. The piece between them takes the dotted line and the curves on either side "
                    + "keep the solid one.");
                RenderResetLegend();
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
                RenderHelpHeader("Terrain options quick help");
                // B-5, fault 8. Eleven pairs stood here -- a control's name in bold over one sentence describing it --
                // and all eleven are now that control's own hover, at the control. One of them was also WRONG, and had
                // been for as long as it stood here: "Heights match width" was described as the distance to the left
                // and right of the road, while TerraformingThreaded halves it into a distance either side, so an
                // author who trusted this panel matched twice the ground they had asked for. That is the argument
                // against a second copy of a field's description living where the field cannot correct it.
                RenderHelpParagraph("Height matching WRITES to the terrain, and what it overwrote is kept in this "
                    + "road's terrain history. That record is the only way the ground can be put back as it was, so "
                    + "the size shown beside it is the price of being able to undo this road.");
                RenderResetLegend();
                EditorGUILayout.EndVertical();
            }
        }


        private void RenderRoadHelpDialog()
        {
            isShowingRoadHelp = EditorGUILayout.Foldout(isShowingRoadHelp, status);
            if (isShowingRoadHelp)
            {
                EditorGUILayout.BeginVertical("box");
                RenderHelpHeader("Road options quick help");
                // B-5, fault 8, and the panel it emptied most: twelve pairs, each a control's name in bold over one
                // sentence describing it, every one of them now on that control's own hover. This panel was also the
                // fifth shape the four help headers came in -- it drew the heading and the Manual button with no
                // horizontal group at all, so the button sat on its own line here and beside the heading everywhere
                // else. What is left is the one note that points somewhere outside this inspector.
                RenderHelpParagraph("The gizmo switch above covers this road's own gizmos. Mesh collider gizmos belong "
                    + "to Unity, and are turned off in the scene view's own Gizmos menu.");
                RenderResetLegend();
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
