// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(RoadSystem))]
    public class RoadSystemEditor : Editor
    {
        #region "Vars"
        //Main target for this editor file:
        private RoadSystem roadSystem;

        //Serialized properties:
        private SerializedProperty isAllowingUpdates;
        private SerializedProperty isTempMultithreading;
        private SerializedProperty isTempSaveMeshAssets;
        private SerializedProperty editorPlayCamera;

        //Editor only camera variables:
        private RoadIntersection[] intersections = null;
        private int intersectionIndex = 0;
        private SplineN[] bridges = null;
        private int bridgesIndex = 0;
        private bool isEditorCameraFlipped = false;
        private float cameraZoomFactor = 1f;
        private float cameraHeightOffset = 1f;
        private bool isCameraCustomRotated = false;
        private Vector3 customCameraRotation = new Vector3(0.5f, 0f, -0.5f);

        /// <summary> B-6, fault 1. Whether the author has already asked for the scene's only camera and been told the
        /// scene has no single one. Kept so the shared control can SAY so once, instead of a repaint silently retrying a
        /// search that has already declined to guess. </summary>
        private bool isCameraPickFailed = false;

        // B-6, fault 3. Gone from here: `loadButton`, `warningLabelStyle`, the three textures they were built from,
        // the `InitChecks()` that built them and the `isInitialized` flag that called it once. The branded button style
        // was this file's own copy of a style four other editors also hand-build -- consolidating those is B-11's
        // rebuild -- and it was the reason `Add road` was 128 px wide in one style while `Update all roads` was 120 px
        // in another. Every command button in this file is now one width in one style, and the red warning text area is
        // `LegacyInspectorShell.SavingMeshesWarning`, which is also where `RoadEditor`'s byte-identical copy went.
        //
        // `isBridgeInitialized` and `isIntersectionInitialized` are gone with them. They were latched true on first
        // paint and never cleared, so the two viewer lists were collected once per selection and could not notice a
        // road, an intersection or a matched bridge appearing while the system stayed selected -- and `DoHotKeyCheck`
        // reaches the same lists from `OnSceneGUI`, which can run before the first paint has collected anything. The
        // lists are collected in `OnEnable` and dropped on a hierarchy change instead, so they are never stale and
        // never null.
        #endregion



        private void OnEnable()
        {
            roadSystem = (RoadSystem)target;

            isAllowingUpdates = serializedObject.FindProperty("isAllowingRoadUpdates");
            isTempMultithreading = serializedObject.FindProperty("isMultithreaded");
            isTempSaveMeshAssets = serializedObject.FindProperty("isSavingMeshes");
            editorPlayCamera = serializedObject.FindProperty("editorPlayCamera");

            CollectViewTargets();
            EditorApplication.hierarchyChanged += InvalidateViewTargets;
        }


        private void OnDisable()
        {
            EditorApplication.hierarchyChanged -= InvalidateViewTargets;
        }


        /// <summary> The two "jump to" lists stop being trustworthy the moment the hierarchy moves, so they are dropped
        /// rather than patched: a road added, an intersection deleted or a bridge finally matched all change what there
        /// is to jump to, and none of them is worth a scan of its own. The next thing that needs a list rebuilds it.
        /// </summary>
        private void InvalidateViewTargets()
        {
            intersections = null;
            bridges = null;
        }


        /// <summary> Fills both lists, and pulls the two indices back inside them. Without the clamp, deleting the last
        /// intersection while its own index was selected left the index past the end of the new array and the next
        /// hotkey press threw. </summary>
        private void CollectViewTargets()
        {
            // Empty rather than null when there is nothing to ask: this runs from the hotkey check as well as from the
            // paint, and a system object destroyed with its inspector still open would otherwise leave both lists null
            // for a `.Length` further down. Every reader below is allowed to assume non-null because of this line.
            if (roadSystem == null)
            {
                intersections = new RoadIntersection[0];
                bridges = new SplineN[0];
                intersectionIndex = 0;
                bridgesIndex = 0;
                return;
            }

            intersections = roadSystem.GetComponentsInChildren<RoadIntersection>();
            if (intersections == null) { intersections = new RoadIntersection[0]; }
            if (intersectionIndex >= intersections.Length) { intersectionIndex = 0; }

            SplineN[] nodes = roadSystem.GetComponentsInChildren<SplineN>();
            List<SplineN> bridgeStarts = new List<SplineN>();
            if (nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    SplineN node = nodes[i];
                    if (node == null || !node.isBridgeStart || !node.isBridgeMatched) { continue; }
                    if (node.bridgeCounterpartNode == null) { continue; }
                    bridgeStarts.Add(node);
                }
            }
            bridges = bridgeStarts.ToArray();
            if (bridgesIndex >= bridges.Length) { bridgesIndex = 0; }
        }


        /// <summary> Both lists, non-null and current. Called from the paint and from the hotkey check, which is the
        /// other way in and can run first. </summary>
        private void EnsureViewTargets()
        {
            if (intersections == null || bridges == null) { CollectViewTargets(); }
        }



        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // RM-109. B-3, B-4. Was `EditorStyles.label.wordWrap = true` -- a write straight into a style the whole
            // editor shares, never given back. Same wrap plus a sized label column, restored on the next tick.
            InspectorTextLayout.Apply();
            EnsureViewTargets();

            //Brand header. It ends with its own separator rule, so the rule below is drawn only when the header did
            //not draw one -- otherwise the inspector opens with two rules stacked on each other.
            if (!RcmBrandHeader.Draw("Road and city network builder")) { RoadCityMaster.EditorUtilities.DrawLine(); }

            // B-6, fault 2. The pre-draw values, read from the PROPERTIES. Two of the options below are not this
            // object's alone -- each is copied into every child road -- and the copy has to happen when the author moves
            // the toggle, not merely whenever the stored value differs. The old test compared the property against the
            // raw field, which is the comparison the serializer itself is about to settle: it was true on the frame the
            // toggle moved, so every road was written before `ApplyModifiedProperties` had written the parent, and with
            // no undo step recorded for any of them.
            bool wasMultithreaded = isTempMultithreading.boolValue;
            bool wasSavingMeshes = isTempSaveMeshAssets.boolValue;

            // B-6, fault 6. Headed sections, in the order an author meets them -- add a road, choose how roads build,
            // say which camera to fly, jump to a place, find the manual. The full-width rules that used to stand between
            // individual controls are gone: a rule between everything marks nothing.
            LegacyInspectorShell.SectionHeader("Roads",
                "Adding a road to this system, and rebuilding every road already in it.");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add road",
                    "Create an empty road under this system and select it, ready for its first node."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                // Selecting the new road is `AddRoad`'s own job, through `EngineIntegration.SetActiveGameObject`. This
                // used to assign `Selection.activeObject` as well, which is two mechanisms for one outcome.
                roadSystem.AddRoad(true);
            }
            if (GUILayout.Button(new GUIContent("Update all roads",
                    "Queue a rebuild of every road in this system. Each is named in Unity's Background Tasks and can be "
                    + "cancelled on its own, and the editor stays usable while they run."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                roadSystem.UpdateAllRoads();
            }
            EditorGUILayout.EndHorizontal();

            // C-3. Settlements are their own section rather than two more buttons on the Roads row, because they are a
            // different act on the same system and the file's own rule is headed sections in the order an author meets
            // them: draw the network, then place the settlements the network seeds.
            LegacyInspectorShell.SectionHeader("Settlements",
                "Placing settlements under this system. The roads decide where they go: a road that declares a "
                + "settlement role seeds one at each of its free ends.");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Add city",
                    "Add one settlement at the point the Scene view is looking at, with its five branches and a runnable "
                    + "graph already on it. It records no source road, so it carries no order until the network says "
                    + "otherwise."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                RoadCityMaster.City.CityAuthoring created;
                string reason;
                if (RoadCityMaster.City.Editor.CitySettlementFactory.CreateCity(roadSystem, null,
                        RoadCityMaster.City.Editor.CitySettlementFactory.DefaultPlacementPoint(), null,
                        RoadCityMaster.City.RoadEnd.Start, true, out created, out reason))
                {
                    Debug.Log("Road & City Master: added the settlement " + created.name + " under "
                              + roadSystem.name + ".");
                }
                else
                {
                    Debug.LogWarning("Road & City Master: " + reason + ".");
                }
            }
            EditorGUILayout.EndHorizontal();
            // The one command in this file without a fixed width, and deliberately: its name does not fit 150 px, and
            // abbreviating the label would leave the command under one name in the inspector and another in the manual
            // -- the drift B-20 exists to catch. An expanding button is not a second width, it is the panel's, so it
            // reads at any inspector size instead of clipping at a narrow one.
            if (GUILayout.Button(new GUIContent("Build settlements from roads",
                    "Walk every road under this system that declares a settlement role and place one settlement at each "
                    + "of its free ends. A terminus that already carries a settlement is left exactly as it is, so this "
                    + "can be pressed again after drawing more roads. One undo step for the whole sweep."),
                EditorStyles.miniButton))
            {
                string report;
                if (RoadCityMaster.City.Editor.CitySettlementFactory.BuildSettlementsFromRoads(roadSystem, out report))
                {
                    Debug.Log("Road & City Master: " + report + ".");
                }
                else
                {
                    Debug.LogWarning("Road & City Master: " + report + ".");
                }
            }

            LegacyInspectorShell.SectionHeader("Build options",
                "How the roads in this system build. The last two are written into every road here.");
            isAllowingUpdates.boolValue = EditorGUILayout.Toggle(new GUIContent("Allow roads to update",
                "Let a road rebuild its geometry when something it is built from changes. With this off an update stops "
                + "after the spline has been recalculated -- nodes still move, no mesh is rebuilt -- which is how a "
                + "script builds a whole network before paying for one build."),
                isAllowingUpdates.boolValue);

            //Multi-threading input:
            isTempMultithreading.boolValue = EditorGUILayout.Toggle(new GUIContent("Multi-threading enabled",
                "Build road geometry on worker threads. Writes into every road in this system, and each road keeps its "
                + "own copy afterwards."),
                isTempMultithreading.boolValue);

            //Save mesh assets input:
            isTempSaveMeshAssets.boolValue = EditorGUILayout.Toggle(new GUIContent("Save mesh assets",
                "Write each generated mesh into the project as an asset instead of leaving it in the scene. Writes into "
                + "every road in this system, and each road keeps its own copy afterwards."),
                isTempSaveMeshAssets.boolValue);
            if (isTempSaveMeshAssets.boolValue) { LegacyInspectorShell.SavingMeshesWarning(); }

            LegacyInspectorShell.SectionHeader("Editor play camera",
                "The camera the two jump-to commands below move while the game is playing. Out of play mode they move "
                + "the scene view instead, and this is not used.");
            isCameraPickFailed = LegacyInspectorShell.CameraField(editorPlayCamera,
                "The camera moved to an intersection or a bridge while the game is playing. Fill it in from the scene "
                + "with the button below, or drag the camera to use into the field.", isCameraPickFailed);

            LegacyInspectorShell.SectionHeader("Jump to",
                "Moving the view from one intersection or bridge to the next. Nothing is shown here until this system "
                + "has one to move to.");
            IntersectionView();
            BridgeView();
            if (bridges.Length > 0 || intersections.Length > 0)
            {
                EditorGUILayout.LabelField("Hotkeys work while this system object is selected and the scene view has "
                    + "focus.", EditorStyles.miniLabel);
            }

            // B-22. Three sections follow that RM-108's own tombstone comments already claimed were drawn here. They
            // were not: the menu rows those comments deleted were the only way in, so the corridor overlay, the
            // alignment ribbon and the OBJ exporters had no author-facing entry point at all once the rows went. Each
            // belongs beside the thing it acts on for a reason a menu row cannot satisfy -- a row cannot say whether
            // there is a raster to draw, or what is selected, until after it has been pressed.
            //
            // The first two appear only once there IS something to draw, and that is not defensiveness. Both tools
            // return immediately with no map and no plan, so a section offering to switch one on with neither would be
            // the same defect as the deleted rows, wearing a control instead of a menu row. Both values arrive from a
            // bake, and nothing in the tool hands a baked map or plan to these scene tools yet: B-28 is that missing
            // feed, and until it lands these two sections stay closed.
            if (RoadCityMaster.City.Editor.SuitabilityOverlay.Map != null)
            {
                LegacyInspectorShell.SectionHeader("Corridor",
                    "The suitability raster the last bake measured, drawn over the ground it was measured on. Shown "
                    + "only while a bake has left one to draw.");
                bool isCorridorShown = EditorGUILayout.Toggle(new GUIContent("Show the overlay",
                    "Draw the raster in the scene view. It costs nothing while it is off."),
                    RoadCityMaster.City.Editor.SuitabilityOverlay.IsShown);
                if (isCorridorShown != RoadCityMaster.City.Editor.SuitabilityOverlay.IsShown)
                {
                    if (isCorridorShown) { RoadCityMaster.City.Editor.SuitabilityOverlay.Show(); }
                    else { RoadCityMaster.City.Editor.SuitabilityOverlay.Hide(); }
                }

                // Six menu rows became one popup because they were always one choice: the overlay draws exactly one
                // measurement at a time, and six commands let an author ask for two. Each row is worded by
                // SuitabilityOverlay.DisplayName, so the six sentences are spelled where the drawing reads them rather
                // than a second time here, and the list follows the enum instead of a hard-coded six.
                System.Array everyMode = System.Enum.GetValues(typeof(RoadCityMaster.City.Editor.SuitabilityDisplay));
                GUIContent[] modes = new GUIContent[everyMode.Length];
                for (int mode = 0; mode < modes.Length; mode++)
                {
                    modes[mode] = new GUIContent(RoadCityMaster.City.Editor.SuitabilityOverlay.DisplayName(
                        (RoadCityMaster.City.Editor.SuitabilityDisplay)everyMode.GetValue(mode)));
                }
                int wasMode = System.Array.IndexOf(everyMode,
                    RoadCityMaster.City.Editor.SuitabilityOverlay.Display);
                int nowMode = EditorGUILayout.Popup(new GUIContent("What the colours mean",
                    "Which measurement the overlay colours each cell by -- one at a time, because the overlay draws "
                    + "one at a time."), wasMode, modes);
                if (nowMode != wasMode && nowMode >= 0)
                {
                    RoadCityMaster.City.Editor.SuitabilityOverlay.SetDisplay(
                        (RoadCityMaster.City.Editor.SuitabilityDisplay)everyMode.GetValue(nowMode));
                }
                LegacyInspectorShell.Measured("Reading",
                    RoadCityMaster.City.Editor.SuitabilityOverlay.CursorLine.Length > 0
                        ? RoadCityMaster.City.Editor.SuitabilityOverlay.CursorLine
                        : "point at the ground in the scene view", null);
            }

            if (RoadCityMaster.City.Editor.AlignmentHandles.Plan != null)
            {
                LegacyInspectorShell.SectionHeader("Alignment",
                    "The horizontal alignment the last bake solved, drawn as a ribbon over the ground it was solved "
                    + "against. Shown only while a bake has left one to draw.");
                bool isRibbonShown = EditorGUILayout.Toggle(new GUIContent("Show the ribbon",
                    "Draw the alignment in the scene view, lifted clear of the ground so the curve stays legible "
                    + "against it. It costs nothing while it is off."),
                    RoadCityMaster.City.Editor.AlignmentHandles.IsShown);
                if (isRibbonShown != RoadCityMaster.City.Editor.AlignmentHandles.IsShown)
                {
                    if (isRibbonShown) { RoadCityMaster.City.Editor.AlignmentHandles.Show(); }
                    else { RoadCityMaster.City.Editor.AlignmentHandles.Hide(); }
                }
                // The suggestion is the ribbon's own output and belongs beside the control that draws it, not in a
                // console line an author has to go looking for.
                if (RoadCityMaster.City.Editor.AlignmentHandles.LastSuggestion.Length > 0)
                {
                    LegacyInspectorShell.Measured("Last suggestion",
                        RoadCityMaster.City.Editor.AlignmentHandles.LastSuggestion, null);
                }
            }

            // The export commands are unconditional, unlike the two sections above: they act on the SELECTION, and a
            // selection always has a count -- including zero, which is precisely the answer a menu row could not give
            // until after it had been pressed and had opened a dialog to say so. The count is taken with the same call
            // and the same flags the exporters use, so it reports what they will act on rather than a second opinion
            // about it, and it stops at five hundred and twelve meshes: past that the figure is a hint either way, and
            // an unbounded walk every repaint would make selecting a whole network cost something merely because this
            // inspector happens to be open.
            Transform[] exportable = Selection.GetTransforms(SelectionMode.Editable | SelectionMode.ExcludePrefab);
            int exportableMeshes = 0;
            for (int picked = 0; picked < exportable.Length && exportableMeshes < 512; picked++)
            {
                exportableMeshes += exportable[picked].GetComponentsInChildren<MeshFilter>().Length;
            }
            LegacyInspectorShell.SectionHeader("Export",
                "Writing the selected meshes out as Wavefront OBJ files, into an ExportedObj folder beside the "
                + "project. These act on what is selected in the scene, not on this system.");
            LegacyInspectorShell.Measured("Selection",
                exportable.Length + (exportable.Length == 1 ? " object, " : " objects, ")
                + (exportableMeshes < 512 ? exportableMeshes.ToString() : "512 or more")
                + (exportableMeshes == 1 ? " mesh" : " meshes"), null);
            // Disabled rather than hidden, and disabled on the mesh count rather than on the object count: an author
            // who has selected something without a mesh has selected the wrong thing, and a command that vanishes
            // cannot say so. Each exporter otherwise answers with the same dialog after the fact.
            using (new EditorGUI.DisabledScope(exportableMeshes == 0))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button(new GUIContent("One file per mesh",
                        "Write one OBJ for every mesh in the selection, children included. A road of twenty sections "
                        + "leaves twenty files."),
                    EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
                {
                    ObjExporter.ExportSelectionToSeparate();
                }
                if (GUILayout.Button(new GUIContent("One file per object",
                        "Write one OBJ for every selected object, with that object's own child meshes merged into it."),
                    EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
                {
                    ObjExporter.ExportEachSelectionToSingle();
                }
                EditorGUILayout.EndHorizontal();
                if (GUILayout.Button(new GUIContent("One file for the whole selection",
                        "Write everything selected into a single OBJ, named after the open scene and the number of "
                        + "meshes it holds."),
                    EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
                {
                    ObjExporter.ExportWholeSelectionToSingle();
                }
            }
            // The credit, and it is not decoration: the OBJ and MTL writers behind those three buttons are the Unity
            // wiki's ObjExporter kept as it arrived, and `ObjExporter.OpenLink` exists for no other purpose than to
            // reach the page they came from. It belongs under the buttons it is attribution FOR rather than in a menu
            // an author opens once, which is why RM-108's row could go and this line cannot. The licence position for
            // the file itself is THIRD-PARTY-NOTICES.md's, not this line's -- this is the source credit.
            EditorGUILayout.LabelField("Mesh writing from the Unity wiki's ObjExporter, kept as it arrived.",
                EditorStyles.miniLabel);
            if (GUILayout.Button(new GUIContent("Open the source page",
                    "Open the archived copy of the Unity wiki page these writers came from."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                ObjExporter.OpenLink();
            }

            // RM-102. One manual, one button. The pair here was "Online manual" -- the upstream project's wiki, a third
            // party's site documenting a tool this one has diverged from -- beside "Offline manual", which opens ours.
            //
            // B-14. "Quick help" is new here, and it is a fix rather than an addition. RM-108 deleted the HelpWindow
            // menu item on the stated grounds that this inspector already drew this button beside "Manual". It did not:
            // nothing in the tool called EditorMenu.ShowHelpWindow and nothing else opened HelpWindow, so the window
            // could not be reached at all -- a whole inspector that an author had no way to tell existed, which is the
            // exact test this entry applies. EditorMenu.ShowHelpWindow stays the only opener; this is a caller, not a
            // second copy of it.
            LegacyInspectorShell.SectionHeader("Help", "Where the tool explains itself.");
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("Manual",
                    "Open the tool's own manual, from the copy installed beside it."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            if (GUILayout.Button(new GUIContent("Quick help",
                    "Open the quick help window: what each part of the tool is for, in one page."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                EditorMenu.ShowHelpWindow();
            }
            EditorGUILayout.EndHorizontal();


            //Hotkey check:
            DoHotKeyCheck();

            if (GUI.changed)
            {
                // B-6, fault 2. Commit this object first, then tell the roads -- and record an undo step for each road
                // before writing it. `EditorUtility.SetDirty` is gone: `ApplyModifiedProperties` already dirties what it
                // wrote, and `Undo.RecordObjects` dirties the roads, so the old call marked the one object that did not
                // need it and none of the ones that did. An author who ticked "Save mesh assets" on a forty-road system
                // could not undo it; now one Ctrl+Z takes the system and all forty back together.
                bool isMultithreadingChanged = isTempMultithreading.boolValue != wasMultithreaded;
                bool isSavingMeshesChanged = isTempSaveMeshAssets.boolValue != wasSavingMeshes;

                serializedObject.ApplyModifiedProperties();

                if (isMultithreadingChanged)
                {
                    RecordEveryRoad("Multi-threading for every road");
                    roadSystem.UpdateAllRoadsMultiThreadedOption(isTempMultithreading.boolValue);
                }
                if (isSavingMeshesChanged)
                {
                    RecordEveryRoad("Save mesh assets for every road");
                    roadSystem.UpdateAllRoadsSavingMeshesOption(isTempSaveMeshAssets.boolValue);
                }
            }
        }


        /// <summary> Puts every road in this system into one undo step, before the option about to be written into all of
        /// them is written. The writing loop itself stays in <see cref="RoadSystem"/> -- there is one copy of "give this
        /// value to every road", and it is not in an editor. </summary>
        private void RecordEveryRoad(string _undoLabel)
        {
            Road[] roads = roadSystem.GetComponentsInChildren<Road>();
            if (roads == null || roads.Length < 1) { return; }
            Undo.RecordObjects(roads, _undoLabel);
        }


        private void IntersectionView()
        {
            if (intersections.Length < 1) { return; }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("View next intersection",
                    "Move the view to the next intersection in this system, in the order they sit under it."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                IncrementIntersection();
            }
            EditorGUILayout.LabelField("Hotkey K", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }


        private void IncrementIntersection()
        {
            if (intersections.Length > 0)
            {
                intersectionIndex += 1;
                if (intersectionIndex >= intersections.Length)
                {
                    intersectionIndex = 0;
                }
                ShowIntersection();
            }
        }


        private void BridgeView()
        {
            if (bridges.Length < 1) { return; }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button(new GUIContent("View next bridge",
                    "Move the view to the next matched bridge in this system. A bridge appears here once both of its "
                    + "nodes have found each other."),
                EditorStyles.miniButton, GUILayout.Width(LegacyInspectorShell.CommandButtonWidth)))
            {
                IncrementBridge();
            }
            EditorGUILayout.LabelField("Hotkey L", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();

            if (EditorApplication.isPlaying)
            {
                // B-6. `Flip camera Y:` set a field nothing ever read, so the control did nothing whatever: `ShowBridge`
                // picked its side from `Vector3.Cross(bridge axis, up)` and never asked. It negates that direction now,
                // which is the half-turn about world Y the label was promising -- the camera stands on the other side of
                // the bridge and looks back along the same line. It applies to a custom rotation as well, because "the
                // other side" means the same thing there.
                bool isCameraFlipped = EditorGUILayout.Toggle(new GUIContent("Flip camera side",
                    "Stand the camera on the other side of the bridge, looking back along the same line."),
                    isEditorCameraFlipped);
                if (isCameraFlipped != isEditorCameraFlipped)
                {
                    isEditorCameraFlipped = isCameraFlipped;
                    ShowBridge();
                }

                // B-19. B-14. These four stay immediate, and that is a decision rather than an oversight -- the one
                // place in the tool where a raw slider is still the right control.
                //
                // B-19 exists because a value committed per keystroke rebuilt the road on every keystroke, wrote an
                // undo step per keystroke, and re-read the property from under the author mid-typing. None of the
                // three can happen here: `cameraZoomFactor`, `cameraHeightOffset` and `customCameraRotation` are
                // fields on this Editor, not on the RoadSystem -- nothing serialises them, nothing rebuilds from
                // them, no undo step is recorded, and the only thing they touch is where the editor's play camera is
                // standing. Deferring them to mouse-up would mean dragging a zoom slider blind and seeing the frame
                // jump once when the button comes up, which is worse at the only job these controls have.
                //
                // <see cref="LegacyInspectorGate"/> allows exactly four of them in exactly this file, so a fifth --
                // or one of these moved onto a value the road is built from -- fails the gate rather than passing
                // quietly under this comment.
                float changeChecker = EditorGUILayout.Slider(new GUIContent("Zoom factor",
                    "How far out to the side the camera stands, as a multiple of half the bridge's length. Below 1 "
                    + "brings it in closer."),
                    cameraZoomFactor, 0.02f, 10f);
                if (!RootUtils.IsApproximately(changeChecker, cameraZoomFactor, 0.001f))
                {
                    cameraZoomFactor = changeChecker;
                    ShowBridge();
                }
                changeChecker = EditorGUILayout.Slider(new GUIContent("Height offset",
                    "Multiplies how high the camera stands. The height itself comes from the bridge's length -- 20 m "
                    + "for a short span rising to 120 m at a kilometre -- and from the zoom factor."),
                    cameraHeightOffset, 0f, 8f);
                if (!RootUtils.IsApproximately(changeChecker, cameraHeightOffset, 0.001f))
                {
                    cameraHeightOffset = changeChecker;
                    ShowBridge();
                }

                bool isCustomRotated = EditorGUILayout.Toggle(new GUIContent("Custom camera direction",
                    "Choose which way the camera stands from the bridge's middle yourself, instead of square to the "
                    + "span."), isCameraCustomRotated);
                if (isCustomRotated != isCameraCustomRotated)
                {
                    isCameraCustomRotated = isCustomRotated;
                    ShowBridge();
                }
                if (isCameraCustomRotated)
                {
                    // The direction is a vector on the ground plane: the height the camera stands at comes from the
                    // height offset above, so there is no Y to set here.
                    Vector3 changedRotation = default(Vector3);
                    changedRotation.x = EditorGUILayout.Slider(new GUIContent("Direction X",
                        "East-west part of the direction the camera stands in from the bridge's middle."),
                        customCameraRotation.x, -1f, 1f);
                    changedRotation.z = EditorGUILayout.Slider(new GUIContent("Direction Z",
                        "North-south part of the direction the camera stands in from the bridge's middle."),
                        customCameraRotation.z, -1f, 1f);

                    if (changedRotation != customCameraRotation)
                    {
                        customCameraRotation = changedRotation;
                        ShowBridge();
                    }
                }
            }
        }


        private void IncrementBridge()
        {
            if (bridges.Length > 0)
            {
                bridgesIndex += 1;
                if (bridgesIndex >= bridges.Length)
                {
                    bridgesIndex = 0;
                }
                ShowBridge();
            }
        }


        /// <summary> Puts the selected intersection on screen: the play camera when the game is running and one has been
        /// named, and the scene view's pivot otherwise.
        ///
        /// B-6. The scene-view branch used `SceneView.lastActiveSceneView` directly, which is null in a scene with no
        /// scene view -- a maximised game view, or a batch-mode run -- so a hotkey press threw there rather than
        /// declining to move a view that is not on screen. `EngineIntegration` already owns that guard for every other
        /// caller in the tool. </summary>
        private void ShowIntersection()
        {
            if (intersections.Length < 1) { return; }
            RoadIntersection intersection = intersections[intersectionIndex];
            if (intersection == null) { return; }

            Vector3 target = intersection.transform.position;
            if (EditorApplication.isPlaying && roadSystem.editorPlayCamera != null)
            {
                // 40 m back on each of two axes and 20 m up: a corner view from about 60 m out, which frames a junction
                // of any ordinary size without an argument about how big it is.
                Vector3 standOff = new Vector3(-40f, 20f, -40f);
                roadSystem.editorPlayCamera.transform.position = target + standOff;
                roadSystem.editorPlayCamera.transform.rotation = Quaternion.LookRotation(-standOff);
            }
            else
            {
                EngineIntegration.SetSceneViewPivot(target);
                EngineIntegration.RepaintLastSceneView();
            }
        }


        /// <summary> Puts the selected bridge on screen, framed by its own span: the camera stands out to the side by half
        /// the bridge's length and rises with it, so a 40 m culvert and a 900 m crossing are both filled rather than one
        /// of them being a dot. Out of play mode, or with no camera named, the scene view's pivot moves instead. </summary>
        private void ShowBridge()
        {
            if (bridges.Length < 1) { return; }
            SplineN bridgeStart = bridges[bridgesIndex];
            if (bridgeStart == null || bridgeStart.bridgeCounterpartNode == null) { return; }

            if (EditorApplication.isPlaying && roadSystem.editorPlayCamera != null)
            {
                Vector3 farEnd = bridgeStart.bridgeCounterpartNode.pos;
                Vector3 bridgePosition = ((bridgeStart.pos - farEnd) * 0.5f) + farEnd;
                float bridgeLength = Vector3.Distance(bridgeStart.pos, farEnd);

                //Rotation:
                Vector3 cameraRotation = Vector3.Cross((bridgeStart.pos - farEnd), Vector3.up);
                if (isCameraCustomRotated)
                {
                    cameraRotation = customCameraRotation;
                }
                else
                {
                    cameraRotation = cameraRotation.normalized;
                }
                // B-6. What "Flip camera side" actually does. Negating the standing direction is a half-turn about world
                // Y, which puts the camera on the far side of the span looking back along the same line.
                if (isEditorCameraFlipped) { cameraRotation = -cameraRotation; }

                //Calc offset:
                Vector3 bridgeOffset = cameraRotation * (bridgeLength * 0.5f * cameraZoomFactor);

                //Height offset:
                bridgeOffset.y = Mathf.Lerp(20f, 120f, (bridgeLength * 0.001f)) * cameraZoomFactor * cameraHeightOffset;

                roadSystem.editorPlayCamera.transform.position = bridgePosition + bridgeOffset;
                roadSystem.editorPlayCamera.transform.rotation = Quaternion.LookRotation(-bridgeOffset);
            }
            else
            {
                EngineIntegration.SetSceneViewPivot(bridgeStart.transform.position);
                EngineIntegration.RepaintLastSceneView();
            }
        }



        public void OnSceneGUI()
        {
            DoHotKeyCheck();
        }


        private void DoHotKeyCheck()
        {
            // The other way into the two viewers: `OnSceneGUI` can deliver a key press before this inspector has ever
            // painted, so the lists are filled here as well. `EnsureViewTargets` collects only when they are missing.
            EnsureViewTargets();

            Event current = Event.current;

            if (current.type == EventType.KeyDown)
            {
                if (current.keyCode == KeyCode.K)
                {
                    IncrementIntersection();
                }
                else if (current.keyCode == KeyCode.L)
                {
                    IncrementBridge();
                }
            }
        }
    }
}
#endif
