// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
#endregion


//=====================================================
//==   NOTE THAT CUSTOM SERIALIZATION IS USED HERE   ==
//==     SOLELY TO COMPLY WITH UNDO REQUIREMENTS 	 ==
//=====================================================


namespace RoadCityMaster
{
    [CustomEditor(typeof(SplineN))]
    public class SplineNEditor : Editor
    {
        #region "Vars"
        private SplineN node;
        private int count = -1;
        private int currentCount = 0;
        public bool isSplinatedObjectHelp = false;
        public bool isEdgeObjectHelp = false;
        private bool isRemovingAll = false;
        private float horizRoadMax = 0;

        private SplineN node1 = null;
        private SplineN node2 = null;
        private bool isCreatingIntersection = false;


        #region "Button icons"
        private Texture deleteButtonTexture = null;
        private Texture copyButtonTexture = null;
        private Texture saveButtonTexture = null;
        private Texture loadButtonTexture = null;
        private Texture extrudeButtonTexture = null;
        private Texture edgeButtonTexture = null;
        private Texture helpButtonTexture = null;
        private Texture refreshButtonTexture = null;
        private Texture defaultButtonTexture = null;
        private Texture2D textAreaBG = null;
        private Texture2D manualBG = null;
        #endregion


        public bool isLoadingEOS = false;
        public int loadingEOSIndex = 0;
        public List<string> loadingEOSNames = null;
        public List<string> loadingEOSPaths = null;

        public bool isLoadingEOM = false;
        public int loadingEOMIndex = 0;
        public List<string> loadingEOMNames = null;
        public List<string> loadingEOMPaths = null;

        //Checkers:
        private Splination.SplinatedMeshMaker SMM = null;

        /// <summary> B-19. This inspector's own deferred-commit controls. One per Editor instance: the pending text of a
        /// half-typed extrusion offset belongs to the node it is being typed on. Reset at the top of every pass by
        /// <see cref="FocusOutFields.BeginFrame"/>, and read once beside GUI.changed to decide whether the extrusion and
        /// edge objects have to be rebuilt -- which on this inspector is the expensive part: the old code re-ran
        /// SplinatedMeshMaker.Setup for every object on the node on every frame a slider was being dragged.
        /// Control names carry the list index, because two extrusions on one node draw the same fields. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();

        private EndObjectsDefaultsEnum endObjectAdd = EndObjectsDefaultsEnum.None;
        private SMMDefaultsEnum SMMQuickAdd = SMMDefaultsEnum.None;
        private BridgeTopBaseDefaultsEnum BridgeTopBaseQuickAdd = BridgeTopBaseDefaultsEnum.None;
        private BridgeBottomBaseDefaultsEnum BridgeBottomBaseQuickAdd = BridgeBottomBaseDefaultsEnum.None;
        private HorizMatchingDefaultsEnum horizMatching = HorizMatchingDefaultsEnum.None;

        //BridgeWizardDefaultsEnum bridgeWizardQuickAdd = BridgeWizardDefaultsEnum.None;
        //RoadCityMaster.Splination.CollisionTypeEnum collisionType = RoadCityMaster.Splination.CollisionTypeEnum.SimpleMeshTriangle;
        //RoadCityMaster.Splination.RepeatUVTypeEnum repeatUVType = RoadCityMaster.Splination.RepeatUVTypeEnum.None;
        private EdgeObjects.EdgeObjectMaker EOM = null;
        private GUIStyle imageButton = null;
        // B-11. Gone from here: 'loadButton' and the two textures it was built from. The branded command-button style is
        // `LegacyInspectorShell.CommandButtonStyle()`, which is where three other editors' copies of it went too.
        // `loadButtonTexture` above is a different thing -- an icon drawn INSIDE buttons -- and stays.
        private GUIStyle manualButton = null;
        private GUIStyle guiButton = null;

        private bool isSceneRectSet = false;
        private Rect sceneRect = default(Rect);
        private Event currentEvent;
        private bool isInitialized = false;

        // Bridge
        private bool isBridgeStart = false;
        private bool isBridgeEnd = false;
        // Tunnel. B-18: the state was in SplineN and the operations were in SplineN, and this file did not contain the
        // word "tunnel" once -- so neither portal could be authored. Held the same way the bridge pair is, because the
        // two are the same control with a different verb.
        private bool isTunnelStart = false;
        private bool isTunnelEnd = false;
        private bool isRoadCut = false;

        private bool isSingleTemp;

        private string[] HorizMatchSubTypeDescriptions;
        #endregion


        #region "Enums"
        public enum EndObjectsDefaultsEnum
        {
            None,
            WarningSign1_Static,
            WarningSign2_Static,
            Atten_Static,
            Barrel1_Static,
            Barrel1_Rigid,
            Barrel3_Static,
            Barrel3_Rigid,
            Barrel7_Static,
            Barrel7_Rigid
        };


        private static string[] EndObjectsDefaultsEnumDesc = new string[]{
        "Quick add",
        "WarningSign1",
        "WarningSign2",
        "Attenuator",
        "1 Sand barrel Static",
        "1 Sand barrel Rigid",
        "3 Sand barrels Static",
        "3 Sand barrels Rigid",
        "7 Sand barrels Static",
        "7 Sand barrels Rigid"
    };


        public enum SMMDefaultsEnum
        {
            None, Custom,
            KRail,
            WBeamR,
            WBeamL,
            Railing1,
            Railing2,
            Railing3,
            Railing4,
            RailingBase05m,
            RailingBase1m
        };


        public enum BridgeTopBaseDefaultsEnum
        {
            None,
            BaseExact,
            Base1MOver,
            Base2MOver,
            Base3MDeep,
        };


        public enum BridgeBottomBaseDefaultsEnum
        {
            None,
            BridgeBase6,
            BridgeBase7,
            BridgeBase8,
            BridgeBaseGrid,
            BridgeSteel,
            BridgeBase2,
            BridgeBase3,
            BridgeBase4,
            BridgeBase5,
        };


        public enum BridgeWizardDefaultsEnum
        {
            None,
            ArchBridge12m,
            ArchBridge24m,
            ArchBridge48m,
            SuspensionBridgeSmall,
            SuspensionBridgeLarge,
            CausewayBridge1,
            CausewayBridge2,
            CausewayBridge3,
            CausewayBridge4,
            ArchBridge1,
            ArchBridge2,
            ArchBridge3,
            GridBridge,
            SteelBeamBridge
        };


        public enum HorizMatchingDefaultsEnum
        {
            None,
            MatchCenter,
            MatchRoadLeft,
            MatchShoulderLeft,
            MatchRoadRight,
            MatchShoulderRight
        };


        public enum EOMDefaultsEnum { None, Custom, StreetLightSingle, StreetLightDouble };


        private static string[] TheAxisDescriptionsSpline = new string[]{
        "X axis",
        "Z axis"
    };


        private static string[] RepeatUVTypeDescriptionsSpline = new string[]{
        "None",
        "X axis",
        "Y axis"
    };


        private static string[] TheCollisionTypeEnumDescSpline = new string[]{
        "None",
        "Simple triangle",
        "Simple trapezoid",
        "Meshfilter collision mesh",
        "Straight line box collider"
    };
        #endregion


        private void OnEnable()
        {
            node = (SplineN)target;
        }


        private void Init()
        {
            isInitialized = true;
            string basePath = RoadEditorUtility.GetBasePath();

            EditorUtilities.LoadTexture(ref deleteButtonTexture, basePath + "/Editor/Icons/delete.png");
            EditorUtilities.LoadTexture(ref copyButtonTexture, basePath + "/Editor/Icons/copy.png");
            EditorUtilities.LoadTexture(ref loadButtonTexture, basePath + "/Editor/Icons/load.png");
            EditorUtilities.LoadTexture(ref saveButtonTexture, basePath + "/Editor/Icons/save.png");
            EditorUtilities.LoadTexture(ref extrudeButtonTexture, basePath + "/Editor/Icons/extrude.png");
            EditorUtilities.LoadTexture(ref edgeButtonTexture, basePath + "/Editor/Icons/edge.png");
            EditorUtilities.LoadTexture(ref helpButtonTexture, basePath + "/Editor/Icons/help.png");
            EditorUtilities.LoadTexture(ref refreshButtonTexture, basePath + "/Editor/Icons/refresh.png");
            EditorUtilities.LoadTexture(ref defaultButtonTexture, basePath + "/Editor/Icons/refresh2.png");
            EditorUtilities.LoadTexture(ref textAreaBG, basePath + "/Editor/Icons/popupbg.png");
            EditorUtilities.LoadTexture(ref manualBG, basePath + "/Editor/Icons/manualbg.png");

            if (imageButton == null)
            {
                imageButton = new GUIStyle(GUI.skin.button);
                imageButton.contentOffset = new Vector2(0f, -2f);
                imageButton.border = new RectOffset(0, 0, 0, 0);
                imageButton.fixedHeight = 16f;
                imageButton.padding = new RectOffset(0, 0, 0, 0);
                imageButton.normal.background = null;
            }

            if (manualButton == null)
            {
                manualButton = new GUIStyle(GUI.skin.button);
                manualButton.contentOffset = new Vector2(0f, 1f);
                manualButton.normal.textColor = new Color(1f, 1f, 1f, 1f);
                manualButton.normal.background = manualBG;
                manualButton.fixedHeight = 16f;
                manualButton.fixedWidth = 128f;
            }

            if (guiButton == null)
            {
                guiButton = new GUIStyle(GUI.skin.button);
                guiButton.normal.textColor = new Color(0.5f, 1f, 0.5f, 1f);
            }

            float halfRoadWidth = node.spline.road.RoadWidth() * 0.5f;
            HorizMatchSubTypeDescriptions = new string[6];
            HorizMatchSubTypeDescriptions[0] = "Select preset";
            HorizMatchSubTypeDescriptions[1] = "Match center: 0 meters";
            HorizMatchSubTypeDescriptions[2] = "Match road left edge: -" + halfRoadWidth.ToString("F1") + " meters";
            HorizMatchSubTypeDescriptions[4] = "Match road right edge: " + halfRoadWidth.ToString("F1") + " meters";

            if (node.spline.road.isShouldersEnabled)
            {
                HorizMatchSubTypeDescriptions[3] = "Match shoulder left edge: -" + (halfRoadWidth + node.spline.road.shoulderWidth).ToString("F1") + " meters";
                HorizMatchSubTypeDescriptions[5] = "Match shoulder right edge: " + (halfRoadWidth + node.spline.road.shoulderWidth).ToString("F1") + " meters";
            }
            else
            {
                HorizMatchSubTypeDescriptions[2] = "Match shoulder left edge: -" + halfRoadWidth.ToString("F1") + " meters";
                HorizMatchSubTypeDescriptions[4] = "Match shoulder right edge: " + halfRoadWidth.ToString("F1") + " meters";
            }

            horizRoadMax = node.spline.road.RoadWidth() * 20;
        }


        public override void OnInspectorGUI()
        {
            fields.BeginFrame();

            #region "Event"
            if (Event.current.type == EventType.ValidateCommand)
            {
                switch (Event.current.commandName)
                {
                    case "UndoRedoPerformed":
                        UpdateSplineObjectsOnUndo();
                        break;
                }
            }

            if (Event.current.type != EventType.Layout && isCreatingIntersection)
            {
                isCreatingIntersection = false;
                Selection.activeGameObject = Intersections.CreateIntersection(node1, node2);
                return;
            }
            #endregion


            //Graphic null checks:
            if (!isInitialized)
            {
                Init();
            }

            // RM-109. B-3, B-4. Per repaint, and handed back on the next tick. The two assignments this replaces sat in
            // Init(), so they ran once and leaked Unity's shared label styles for the rest of the session.
            InspectorTextLayout.Apply();

            EditorUtilities.DrawLine();

            #region "Manuals on Top of SplineN Scripts"
            EditorGUILayout.LabelField(node.spline.road.name + "-" + node.name, EditorStyles.boldLabel);

            // RM-102. One manual, one button -- see EditorUtilities.OpenOfflineManual.
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(128f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            #endregion


            #region "Option: Elevation above the ground"
            // B-13's third requirement, in the author's words: "if you want an elevation you edit it in the node
            // inspector in the height or elevation to pull the ground up, so as not to float." So the figure here is a
            // height ABOVE THE GROUND UNDER THE NODE, not a world Y. World Y = 0 is wherever the scene origin happens
            // to sit -- the frame-of-reference mistake RM-94 removed from RoadNodeGround -- whereas an embankment 4 m
            // high is 4 m above the ground whatever that ground's own height is.
            //
            // It is DERIVED, never stored. A node's height lives in exactly one place, its transform, which
            // SplineC.Setup copies into `pos` on every build; a serialised second copy of the same quantity would be a
            // second source of truth to keep in step. So the field shows transform.y - groundY on the way out and
            // writes groundY + typed on the way in, and there is nothing new to keep in step.
            //
            // Intersection and special end nodes are left out for the reason the drop handler skips a snapped drop:
            // their position is exactly another node's, and that exact coincidence IS the junction or the road
            // connection. Raising one side of it would take the junction apart.
            if (!node.isIntersection && !node.isSpecialEndNode)
            {
                // B-17. This road's own ground where it has been recorded, the live surface otherwise. Read from the
                // live surface alone, the figure below collapsed towards zero the moment the road was built -- the
                // ground under a built road is the embankment the road raised, so a node 4 m up on its own embankment
                // read as 4 cm up, and typing 4 m again raised it another 4 m. The road records the ground it found
                // before it built; measured from that, the figure means the same thing before and after a build.
                float groundY = node.spline != null && node.spline.road != null
                    ? node.spline.road.NodeGroundY(node.transform.position)
                    : RoadNodeGround.SurfaceYUnder(node.transform.position);
                if (float.IsNegativeInfinity(groundY))
                {
                    // Creation and dragging both refuse a node with no terrain under it, so reaching this means the
                    // terrain was removed or resized after the node was placed. There is no ground to measure against,
                    // and inventing one is the bug RM-94 removed.
                    EditorGUILayout.LabelField("No terrain under this node, so it has no height above the ground.");
                }
                else
                {
                    float elevation = node.transform.position.y - groundY;
                    if (fields.FloatField("Elevation above ground: ", "nodeelevation", ref elevation,
                        "Metres this node sits above the ground under it. The road stays on the node and the terraform"
                        + " brings the ground up to meet it, which is what an embankment is. 0 sets the node on the"
                        + " surface; a negative figure sets it into the surface, and the ground is cut down to it."))
                    {
                        Undo.RecordObject(node.transform, "Set node elevation");
                        Vector3 raised = node.transform.position;
                        raised.y = groundY + elevation;
                        node.transform.position = raised;
                        // Deliberately NOT put through RoadNodeGround.Lift. Lift is there to catch a node buried by a
                        // mouse ray or a drag, where the depth was never asked for; a figure the author typed is an
                        // instruction. Clamping it at the surface would make a cutting impossible to author, which is
                        // the write B-17 governs.
                        if (node.IsLegitimate() && node.spline.road.isMaxGradeEnabled)
                        {
                            node.EnsureGradeValidity();
                        }
                        node.pos = node.transform.position;
                        TriggerRoadUpdate();
                    }

                    // B-17. What stops this node going lower, said beside the field that asks it to. The maximum grade
                    // clamp is correct road engineering -- a node cannot drop faster than the road can climb back out
                    // of it -- but it was silent, so a figure the author typed came back changed with no reason given
                    // and the tool looked broken. Stateless on purpose: it is recomputed from the node's neighbours
                    // every repaint, so it is true before the author types as well as after, and there is no remembered
                    // message to go stale or to flicker. Nothing here writes anything.
                    float lowestY, highestY;
                    string limitedBy;
                    if (node.GradeWindow(out lowestY, out highestY, out limitedBy))
                    {
                        if (!float.IsNegativeInfinity(lowestY))
                        {
                            EditorGUILayout.LabelField("Lowest allowed here",
                                (lowestY - groundY).ToString("0.##") + " m, at the "
                                + (node.spline.road.maxGrade * 100f).ToString("0.##") + " % maximum grade to "
                                + (string.IsNullOrEmpty(limitedBy) ? "the next node" : limitedBy));
                        }
                        if (!float.IsPositiveInfinity(highestY))
                        {
                            EditorGUILayout.LabelField("Highest allowed here",
                                (highestY - groundY).ToString("0.##") + " m, at the same maximum grade");
                        }
                    }
                }
                EditorUtilities.DrawLine();
            }
            #endregion


            #region "Option: Manual road cut"
            if (node.idOnSpline > 0 && node.idOnSpline < (node.spline.GetNodeCount() - 1) && !node.isIntersection && !node.isSpecialEndNode)
            {
                // && !cNode.bIsBridge_PreNode && !cNode.bIsBridge_PostNode){
                if (node.spline.road.isDynamicCutsEnabled)
                {
                    EditorUtilities.DrawLine();
                    isRoadCut = EditorGUILayout.Toggle("Cut road at this node: ", node.isRoadCut);
                }
                EditorUtilities.DrawLine();
            }
            #endregion


            #region "Option: Bridge options"
            bool isBridgeDisplayed = false;
            if (!node.isEndPoint)
            {
                //Bridge start:
                if (!node.isBridgeEnd && node.CanBridgeStart())
                {
                    isBridgeStart = EditorGUILayout.Toggle(" Bridge start", node.isBridgeStart);
                    isBridgeDisplayed = true;
                }
                //Bridge end:
                if (!node.isBridgeStart && node.CanBridgeEnd())
                {
                    isBridgeEnd = EditorGUILayout.Toggle(" Bridge end", node.isBridgeEnd);
                    isBridgeDisplayed = true;
                }

                if (isBridgeDisplayed)
                {
                    RoadCityMaster.EditorUtilities.DrawLine();
                }
            }
            #endregion


            #region "Option: Tunnel options"
            // B-18. The two toggles the whole tunnel chain was waiting for. Everything behind them already existed --
            // the state and the matcher in SplineN, the span in SplineC, the mesh in RoadCreationT, the raised ground in
            // TerraformingThreaded's RM-98 branch -- and none of it could be reached, because this file did not draw a
            // tunnel control at all.
            //
            // Refusals are SHOWN, not implied by an absent control, with one exception that is deliberate: "no tunnel
            // has been started before this node" is the resting state of nearly every node on nearly every road, so
            // printing it everywhere would be noise rather than a report. It is shown once the author is actually
            // tunnelling on this road -- see AnyTunnelStartOnSpline.
            bool isTunnelDisplayed = false;
            if (!node.isEndPoint)
            {
                string tunnelRefusal;

                //Tunnel start:
                if (!node.isTunnelEnd)
                {
                    if (node.CanTunnelStart(out tunnelRefusal))
                    {
                        isTunnelStart = EditorGUILayout.Toggle(" Tunnel start", node.isTunnelStart);
                        isTunnelDisplayed = true;
                    }
                    else if (!string.IsNullOrEmpty(tunnelRefusal))
                    {
                        EditorGUILayout.HelpBox("Tunnel start: " + tunnelRefusal, MessageType.Info);
                        isTunnelDisplayed = true;
                    }
                }

                //Tunnel end:
                if (!node.isTunnelStart)
                {
                    if (node.CanTunnelEnd(out tunnelRefusal))
                    {
                        isTunnelEnd = EditorGUILayout.Toggle(" Tunnel end", node.isTunnelEnd);
                        isTunnelDisplayed = true;
                    }
                    else if (!string.IsNullOrEmpty(tunnelRefusal)
                        && (node.isTunnelEnd || AnyTunnelStartOnSpline(node.spline)))
                    {
                        EditorGUILayout.HelpBox("Tunnel end: " + tunnelRefusal, MessageType.Info);
                        isTunnelDisplayed = true;
                    }
                }

                if (node.isTunnelStart && node.isTunnelMatched && node.tunnelCounterpartNode != null)
                {
                    EditorGUILayout.LabelField("Tunnel exits at",
                        "node " + node.tunnelCounterpartNode.idOnSpline);
                    isTunnelDisplayed = true;
                }

                // B-18. What the run passes under, authored on the START node only, because a tunnel is one run and a
                // run has one water level. These commit as they are edited rather than through the apply block below:
                // they change no matching, raise no span and re-run no setup, so routing them through the toggle path
                // would only make them look like they did.
                if (node.isTunnelStart)
                {
                    bool wasUnderWater = !float.IsNegativeInfinity(node.tunnelWaterSurfaceY);
                    bool isUnderWater = EditorGUILayout.Toggle(
                        new GUIContent(" Passes under water",
                            "Switch on for a tunnel under a river, a lake or the sea. The level is STATED here because"
                            + " the terrain provider carries no water query -- the tool cannot ask the host what is"
                            + " above the bore, so an author has to say."), wasUnderWater);
                    isTunnelDisplayed = true;

                    if (isUnderWater != wasUnderWater)
                    {
                        Undo.RecordObject(node, "Set tunnel water");
                        node.tunnelWaterSurfaceY = isUnderWater
                            ? node.transform.position.y
                            : float.NegativeInfinity;
                        if (!isUnderWater) { node.isTunnelImmersedTube = false; }
                        EditorUtility.SetDirty(node);
                    }

                    if (isUnderWater)
                    {
                        float waterY = float.IsNegativeInfinity(node.tunnelWaterSurfaceY)
                            ? node.transform.position.y
                            : node.tunnelWaterSurfaceY;
                        if (fields.FloatField("  Water surface height: ", "tunnelwatery", ref waterY,
                            "World height in metres of the water surface over this run. The bore has to keep the"
                            + " required cover between its crown and the bed under that water, or the tunnel is"
                            + " refused with the shortfall."))
                        {
                            Undo.RecordObject(node, "Set tunnel water height");
                            node.tunnelWaterSurfaceY = waterY;
                            EditorUtility.SetDirty(node);
                        }

                        bool isTube = EditorGUILayout.Toggle(
                            new GUIContent("  Immersed tube",
                                "On: the tube is sunk into a dredged trench in the bed and the bed is put back over"
                                + " it. Off: the bore passes beneath the bed and the bed is not touched at all. Which"
                                + " one is built is an engineering decision, so it is stated and never inferred."),
                            node.isTunnelImmersedTube);
                        if (isTube != node.isTunnelImmersedTube)
                        {
                            Undo.RecordObject(node, "Set immersed tube");
                            node.isTunnelImmersedTube = isTube;
                            EditorUtility.SetDirty(node);
                        }
                    }

                    DrawTunnelPlan(node);
                }

                if (isTunnelDisplayed)
                {
                    RoadCityMaster.EditorUtilities.DrawLine();
                }
            }
            #endregion


            if ((Selection.objects.Length == 1 && Selection.objects[0] is SplineN) || (node.specialNodeCounterpart == null && !node.isSpecialRoadConnPrimary))
            {
                //Do extrusion and edge objects overview:
                DoExtAndEdgeOverview();
            }
            else
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Road objects"))
                {
                    Selection.objects = new Object[1] { node.specialNodeCounterpart };
                }
                EditorGUILayout.EndHorizontal();
            }

            if (node.isSpecialRoadConnPrimary)
            {
                EditorGUILayout.LabelField("Road connection:", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical();
                if (GUILayout.Button("Update road connection"))
                {
                    SplineN node1 = node.originalConnectionNodes[0];
                    SplineN node2 = node.originalConnectionNodes[1];
                    node.specialNodeCounterpart.BreakConnection();
                    // B-14. Was `node.spline.road.UpdateRoad()` here, between the break and the reconnect: a full
                    // road build on the main thread, unannounced, whose result the reconnect immediately made stale --
                    // ActivateEndNodeConnection ends by asking for a build of its own. Two builds for one click, the
                    // first of them thrown away and neither named in Background Tasks.
                    //
                    // Moved after the reconnect rather than converted in place, because the queued call returns before
                    // it builds and the order mattered for a reason worth keeping: if the reconnect REFUSES -- either
                    // spline under three nodes -- the connection is already broken and the road must still be rebuilt,
                    // or it goes on drawing a connection that no longer exists. Asking afterwards covers both endings
                    // with one build. Nothing the reconnect reads is produced by a build: it reads idOnSpline, node
                    // positions and isSpecialEndNode, and the build path only ever reads that last one.
                    node1.spline.ActivateEndNodeConnection(node1, node2);
                    // A cross-road connection sets isUpdateRequired on node1's road, which is not necessarily this
                    // one. That covers the other road; this asks for the road the author is looking at. When they are
                    // the same road the dispatcher's supersede collapses the two into one build.
                    node.spline.road.RequestUpdate();
                }
                if (GUILayout.Button("Break road connection"))
                {
                    // B-14. The break alone changed no geometry, so the road went on drawing the connection until
                    // something unrelated rebuilt it -- a control that looked dead because its effect was invisible.
                    // Both roads are asked, because BreakConnection clears the special-node flags on both splines.
                    // The counterpart is read first: BreakConnection nulls specialNodeCounterpart as it goes.
                    SplineN counterpart = node.specialNodeCounterpart;
                    Road otherRoad = null;
                    if (counterpart != null && counterpart.spline != null) { otherRoad = counterpart.spline.road; }
                    node.specialNodeCounterpart.BreakConnection();
                    node.spline.road.RequestUpdate();
                    if (otherRoad != null && otherRoad != node.spline.road) { otherRoad.RequestUpdate(); }
                }
                if (GUILayout.Button("Access objects on other node"))
                {
                    Selection.objects = new Object[] { node.specialNodeCounterpart };
                }
                EditorGUILayout.EndVertical();
                if (node.specialNodeCounterpart != null)
                {
                    EditorGUILayout.LabelField(node.specialNodeCounterpart.spline.road.transform.name + " to " + node.specialNodeCounterpart.specialNodeCounterpart.spline.road.transform.name);
                }
                EditorGUILayout.LabelField("To break this road connection, click the \"Break road connection\" button.");
                EditorUtilities.DrawLine();
            }

            //Statistics:
            DoStats();
            EditorGUILayout.EndVertical();

            // B-19. `fields.Changed` has to be part of this test, and on this inspector it is the whole of the fix. A
            // deferred control commits on the frame the author presses Enter, moves focus away, or lets a slider handle
            // go -- and on that frame no Unity control reported a change, so GUI.changed is false. Without this term the
            // committed value would never reach UpdateSplineObjects()/UpdateEdgeObjects() below, and the extrusion the
            // author just resized would keep its old geometry until something else happened to dirty the inspector.
            if (GUI.changed || fields.Changed)
            {
                //Set snapshot for undo:
                Undo.RecordObject(node, "Modify node");

                //Option: Manual cut:
                if (node.idOnSpline > 0 && node.idOnSpline < (node.spline.GetNodeCount() - 1) && !node.isIntersection && !node.isSpecialEndNode)
                {
                    // && !cNode.bIsBridge_PreNode && !cNode.bIsBridge_PostNode){
                    if (node.spline.road.isDynamicCutsEnabled)
                    {
                        if (isRoadCut != node.isRoadCut)
                        {
                            node.isRoadCut = isRoadCut;
                        }
                    }
                }

                #region "Option: Bridge options"
                //Bridge start:
                if (!node.isEndPoint)
                {
                    if (!node.isBridgeEnd && node.CanBridgeStart())
                    {
                        if (isBridgeStart != node.isBridgeStart)
                        {
                            node.isBridgeStart = isBridgeStart;
                            node.BridgeToggleStart();
                        }
                    }
                }
                //Bridge end:
                if (!node.isEndPoint)
                {
                    if (!node.isBridgeStart && node.CanBridgeEnd())
                    {
                        if (isBridgeEnd != node.isBridgeEnd)
                        {
                            node.isBridgeEnd = isBridgeEnd;
                            node.BridgeToggleEnd();
                        }
                    }
                }
                #endregion


                #region "Option: Tunnel options"
                // B-18. Applied exactly as the bridge pair is: set the field, then call the operation that matches the
                // pair up. The guard is asked again here and not trusted from the draw pass, because the draw pass ran
                // against the node as it was before this frame's other commits.
                //Tunnel start:
                if (!node.isEndPoint)
                {
                    if (!node.isTunnelEnd && node.CanTunnelStart())
                    {
                        if (isTunnelStart != node.isTunnelStart)
                        {
                            node.isTunnelStart = isTunnelStart;
                            node.TunnelToggleStart();
                            ReportTunnelSpan(node);
                        }
                    }
                }
                //Tunnel end:
                if (!node.isEndPoint)
                {
                    if (!node.isTunnelStart && node.CanTunnelEnd())
                    {
                        if (isTunnelEnd != node.isTunnelEnd)
                        {
                            node.isTunnelEnd = isTunnelEnd;
                            node.TunnelToggleEnd();
                            ReportTunnelSpan(node);
                        }
                    }
                }
                #endregion

                if (node.CanSplinate())
                {
                    UpdateSplineObjects();
                    UpdateEdgeObjects();
                }

                EditorUtility.SetDirty(target);
            }
        }


        #region "Tunnels"
        /// <summary> B-18. Whether this spline has any unfinished tunnel on it.
        ///
        /// The one refusal that is suppressed until it is relevant. "No tunnel has been started before this node" is the
        /// truth at nearly every node on nearly every road, and a HelpBox saying so on all of them would train an author
        /// to stop reading them -- which costs more than the message gains. Once there IS a start waiting for its exit,
        /// the same sentence is the most useful thing on the panel. </summary>
        private static bool AnyTunnelStartOnSpline(SplineC spline)
        {
            if (spline == null || spline.nodes == null) { return false; }
            for (int index = 0; index < spline.nodes.Count; index++)
            {
                SplineN candidate = spline.nodes[index];
                if (candidate != null && candidate.isTunnelStart) { return true; }
            }
            return false;
        }


        /// <summary> B-18. What this tunnel will do to the ground, shown before the author commits to it.
        ///
        /// The planner is asked every repaint on purpose. It reads the spline, the road and the terrain surface and
        /// writes nothing -- no height, no hold, no announcement -- so the cost is a handful of terrain samples and the
        /// gain is that the refusal an author would otherwise meet only after clicking is on screen while they are still
        /// deciding. The sentence itself is the linker's, not a second wording of it. </summary>
        private static void DrawTunnelPlan(SplineN node)
        {
            City.TunnelAuthoringLink.TunnelAuthoringOutcome outcome = City.TunnelAuthoringLink.PlanFor(node);
            if (!outcome.planned)
            {
                if (!string.IsNullOrEmpty(outcome.refusal))
                {
                    EditorGUILayout.HelpBox("Tunnel: " + outcome.refusal, MessageType.Info);
                }
                return;
            }

            if (!outcome.plan.viable)
            {
                EditorGUILayout.HelpBox(outcome.refusal, MessageType.Warning);
                return;
            }

            EditorGUILayout.HelpBox(outcome.Describe(), MessageType.Info);
            if (outcome.mode == City.TunnelMode.Standalone)
            {
                EditorGUILayout.HelpBox("No terrain provider is connected, so the bore is lined and left visible and"
                    + " the hill is not opened. Connect a provider to have the ground cut.", MessageType.Info);
            }
        }


        /// <summary> B-18. The one place a toggled tunnel becomes a terrain write.
        ///
        /// Called after <c>TunnelToggleStart</c>/<c>TunnelToggleEnd</c> have settled the pair, because a run cannot be
        /// measured until it has both portals. A toggle that broke the pair leaves nothing to raise and gives the hole
        /// back instead -- which is the case an author hits every time they switch a tunnel off, so it is the one that
        /// must not warn. The work itself is queued on <see cref="City.BuildDispatch"/> and appears in Background Tasks
        /// under B-14; nothing about the terrain happens inside this click. </summary>
        private static void ReportTunnelSpan(SplineN node)
        {
            if (node == null) { return; }

            SplineN owner = node.isTunnelStart
                ? node
                : (node.isTunnelEnd && node.tunnelCounterpartNode != null ? node.tunnelCounterpartNode : null);

            if (owner == null || !owner.isTunnelMatched)
            {
                node.ReleaseTunnelOpening();
                if (node.tunnelCounterpartNode != null) { node.tunnelCounterpartNode.ReleaseTunnelOpening(); }
                return;
            }

            City.TunnelAuthoringLink.TunnelAuthoringOutcome raised = City.TunnelAuthoringLink.Raise(owner);
            if (!raised.queued && raised.planned)
            {
                // Only a measured-then-refused run reaches the console. A run that could not be read yet -- the road is
                // mid-rebuild -- is already on the panel, and saying it twice is how a log stops being read.
                Debug.LogWarning("Road & City Master: " + City.TunnelAuthoringLink.RequesterIdFor(owner) + " -- "
                    + (string.IsNullOrEmpty(raised.refusal) ? "the tunnel could not be planned" : raised.refusal));
            }
        }
        #endregion


        private void OnSelectionChanged()
        {
            Repaint();
        }


        private void DoExtAndEdgeOverview()
        {
            EditorGUILayout.LabelField("Extrusion & edge objects", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");

            // B-14. This was one `if` with `||` between two GUILayout.Button calls. `||` short-circuits, so on the frame
            // the author clicked "Save group" the icon button beside it was never drawn -- the Layout pass had counted
            // two controls in this row and the click pass emitted one, which is the mismatch IMGUI answers with
            // "Getting control 1's position in a group with only 1 controls" on the following repaint. Both presses are
            // read into locals first, so both buttons are drawn on every pass whatever either one returns.
            bool isSavingGroup = GUILayout.Button("Save group", EditorStyles.miniButton, GUILayout.Width(108f));
            bool isSavingGroupIcon = GUILayout.Button(saveButtonTexture, imageButton, GUILayout.Width(16f));
            if (isSavingGroup || isSavingGroupIcon)
            {
                SaveWindow saveWindow = EditorWindow.GetWindow<SaveWindow>();
                // The two branches that stood here were identical -- both opened the window as a BridgeWizard -- under
                // a Todo asking whether a group should always save as a bridge. Collapsed to the one thing they both
                // did, so the code no longer implies a distinction it does not make; the question the Todo asks is
                // about what SaveNodeObjects should write, not about this call, and is not settled by writing it twice.
                saveWindow.Initialize(ref sceneRect, SaveWindow.WindowTypeEnum.BridgeWizard, node);
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);


            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            // B-11. This drew with a private style built from `loadbg`/`loadbgglow` and a 35 px right padding that shoved
            // the label off centre; it now asks the shell for the same branded style `Update road` and
            // `Update intersection` use. The appearance therefore CHANGED here, deliberately: opening the wizard is a
            // command, and the tool had three different-looking brands for one kind of act. The explicit 128 px width is
            // gone with it, so the shell's `CommandButtonWidth` is what decides -- an option here would have overridden
            // the style's own `fixedWidth` and put this button back out of line with every other command.
            if (GUILayout.Button("Open Wizard", LegacyInspectorShell.CommandButtonStyle()))
            {
                // || GUILayout.Button(btnLoadText,GSDImageButton,GUILayout.Width(16f))){
                Wizard wizard = EditorWindow.GetWindow<Wizard>();
                if (sceneRect.x < 0)
                {
                    sceneRect.x = 0f;
                }
                if (sceneRect.y < 0)
                {
                    sceneRect.y = 0f;
                }
                wizard.rect = sceneRect;
                if (node.isBridgeStart)
                {
                    wizard.Initialize(Wizard.WindowTypeEnum.BridgeComplete, node);
                }
                else
                {
                    wizard.Initialize(Wizard.WindowTypeEnum.Extrusion, node);
                }
            }
            EditorGUILayout.EndHorizontal();
            
            
            GUILayout.Space(4f);
            isEdgeObjectHelp = EditorGUILayout.Foldout(isEdgeObjectHelp, "Quick help");
            if (isEdgeObjectHelp)
            {
                // B-14. Every picture in this legend was drawn with GUILayout.Button and its result thrown away: seven
                // controls that highlight under the cursor and depress when clicked, and answer with nothing. A legend
                // explains the buttons elsewhere in this inspector, it is not one of them, so it is drawn with Label.
                // The style is unchanged, so each glyph still looks exactly like the button it stands for.
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(extrudeButtonTexture, imageButton, GUILayout.Width(32f));
                EditorGUILayout.LabelField("= Extrusion objects", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Railings, bridge pieces, center dividers and other connected objects.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(edgeButtonTexture, imageButton, GUILayout.Width(32f));
                EditorGUILayout.LabelField("= Edge objects", EditorStyles.miniLabel);
                EditorGUILayout.LabelField("");
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.LabelField("Signs, street lights, bridge pillars and other unconnected road objects.", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(saveButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField("= Saves object config to library for use on other nodes.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(copyButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField("= Duplicates object onto current node.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(deleteButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField("= Deletes object.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(refreshButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField("= Refreshes object.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label(defaultButtonTexture, imageButton, GUILayout.Width(16f));
                EditorGUILayout.LabelField("= Resets setting(s) to default.", EditorStyles.miniLabel);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                EditorUtilities.DrawLine();
            }
            GUILayout.Space(4f);

            if (node.CanSplinate())
            {
                DoSplineObjects();
                DoEdgeObjects();
            }

            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            if (GUILayout.Button("Add custom extrusion object", EditorStyles.miniButton))
            {
                node.AddSplinatedObject();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4f);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            if (GUILayout.Button("Add custom edge object", EditorStyles.miniButton))
            {
                node.AddEdgeObject();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("");
            isRemovingAll = EditorGUILayout.Toggle(isRemovingAll, GUILayout.Width(20f));
            if (GUILayout.Button("Remove all", EditorStyles.miniButton, GUILayout.Width(100f)))
            {
                if (isRemovingAll)
                {
                    node.RemoveAllSplinatedObjects();
                    node.RemoveAllEdgeObjects();
                    isRemovingAll = false;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorUtilities.DrawLine();
        }


        private void DoStats()
        {
            EditorGUILayout.LabelField("Statistics:");
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Grade to next node: " + node.gradeToNext);
            EditorGUILayout.LabelField("Grade to prev node: " + node.gradeToPrev);
            EditorGUILayout.LabelField("Distance from start: " + node.dist.ToString("F3") + " meters");
            EditorGUILayout.LabelField("% of spline: " + ((node.dist / node.spline.distance) * 100f).ToString("F2") + "%");
            EditorGUILayout.LabelField("Parameter: " + node.time);
            EditorGUILayout.LabelField("Tangent: " + node.tangent);
            EditorGUILayout.LabelField("POS: " + node.pos);
            EditorGUILayout.LabelField("ID on spline: " + node.idOnSpline);
            EditorGUILayout.LabelField("Is intersection node: " + node.isIntersection);
            EditorGUILayout.LabelField("Is end node: " + node.isEndPoint);
            EditorGUILayout.LabelField("Is bridge start: " + node.isBridgeStart);
            EditorGUILayout.LabelField("Is bridge end: " + node.isBridgeEnd);
            EditorGUILayout.LabelField("Road: " + node.spline.road.transform.name);
            EditorGUILayout.LabelField("System: " + node.spline.road.roadSystem.transform.name);
            EditorGUILayout.SelectableLabel("UID: " + node.uID);
        }


        public void DoSplineObjects()
        {
            if (node.SplinatedObjects == null)
            {
                node.SplinatedObjects = new List<Splination.SplinatedMeshMaker>();
            }
            count = node.SplinatedObjects.Count;

            SMM = null;
            count = node.SplinatedObjects.Count;
            if (count == 0)
            {

            }

            currentCount = 0;
            for (int index = 0; index < node.SplinatedObjects.Count; index++)
            {
                currentCount += 1;
                SMM = node.SplinatedObjects[index];

                // B-19. Every deferred control needs a name unique to the value AND to the entry it belongs to: two
                // extrusions on one node draw the same fields, and a shared name would show a half-typed number from
                // #2 sitting in #1's box. The list position is what tells them apart.
                string key = "smm" + index.ToString() + "#";

                //RoadCityMaster.Splination.AxisTypeEnum tAxisTypeSpline = RoadCityMaster.Splination.AxisTypeEnum.Z;
                EditorGUILayout.BeginVertical("TextArea");

                if (SMM.isRequiringUpdate)
                {
                    SMM.Setup(true);
                }


                EditorGUILayout.BeginHorizontal();

                SMM.isToggled = EditorGUILayout.Foldout(SMM.isToggled, "#" + currentCount.ToString() + ": " + SMM.objectName);

                // B-14. A row marker saying "this row is an extrusion", not a command: it was a Button with its press
                // discarded, sitting immediately left of four buttons that do act, so the one picture in the row that
                // answers a click with nothing looked exactly like the four that do not.
                GUILayout.Label(extrudeButtonTexture, imageButton, GUILayout.Width(32f));
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.Setup();
                }
                if (GUILayout.Button(saveButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SaveWindow saveDialog = EditorWindow.GetWindow<SaveWindow>();
                    saveDialog.Initialize(ref sceneRect, SaveWindow.WindowTypeEnum.Extrusion, node, SMM);
                }
                if (GUILayout.Button(copyButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    Undo.RecordObject(node, "Copy");
                    node.CopySplinatedObject(ref SMM);
                    EditorUtility.SetDirty(node);
                }
                if (GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    Undo.RecordObject(node, "Delete");
                    node.RemoveSplinatedObject(index);
                    EditorUtility.SetDirty(node);
                }
                EditorGUILayout.EndHorizontal();
                if (!SMM.isToggled)
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }

                GUILayout.Space(8f);
                EditorGUILayout.LabelField("General options:");

                EditorGUILayout.BeginVertical("box");
                //Name:
                fields.StringField("Name:", key + "name", ref SMM.objectName,
                    "The name this extrusion is listed under, and the name given to the mesh object it builds.");

                //Game object (prefab):
                SMM.currentSplination = (GameObject)EditorGUILayout.ObjectField("Prefab:", SMM.currentSplination, typeof(GameObject), false);

                //Game object (prefab start cap):
                SMM.currentSplinationCap1 = (GameObject)EditorGUILayout.ObjectField("Prefab start cap:", SMM.currentSplinationCap1, typeof(GameObject), false);
                //Prefab start cap height offset:
                if (SMM.currentSplinationCap1 != null)
                {
                    fields.FloatField("  Height offset:", key + "cap1", ref SMM.capHeightOffset1,
                        "Metres to raise or lower the start cap relative to the extrusion it caps.");
                }

                //Game object (prefab end cap):
                SMM.currentSplinationCap2 = (GameObject)EditorGUILayout.ObjectField("Prefab end cap:", SMM.currentSplinationCap2, typeof(GameObject), false);
                //Prefab end cap height offset:
                if (SMM.currentSplinationCap2 != null)
                {
                    fields.FloatField("  Height offset:", key + "cap2", ref SMM.capHeightOffset2,
                        "Metres to raise or lower the end cap relative to the extrusion it caps.");
                }

                //Material overrides:
                SMM.isMaterialOverriden = EditorGUILayout.Toggle("Material override: ", SMM.isMaterialOverriden);
                if (SMM.isMaterialOverriden)
                {
                    SMM.SplinatedMaterial1 = (Material)EditorGUILayout.ObjectField("Override mat #1: ", SMM.SplinatedMaterial1, typeof(Material), false);
                    SMM.SplinatedMaterial2 = (Material)EditorGUILayout.ObjectField("Override mat #2: ", SMM.SplinatedMaterial2, typeof(Material), false);
                }

                //Axis:
                SMM.Axis = (Splination.AxisTypeEnum)EditorGUILayout.Popup("Extrusion axis: ", (int)SMM.Axis, TheAxisDescriptionsSpline, GUILayout.Width(250f));

                //Start time:
                if (SMM.StartTime < node.minSplination)
                {
                    SMM.StartTime = node.minSplination;
                }
                if (SMM.EndTime > node.maxSplination)
                {
                    SMM.EndTime = node.maxSplination;
                }
                EditorGUILayout.BeginHorizontal();

                // B-19. Start and end param drive the extrusion's length, and length is what SplinatedMeshMaker.Setup
                // rebuilds from -- so a dragged handle used to regenerate the whole extrusion mesh on every mouse-move
                // frame. The handle still slides freely; the value it reports arrives once, when the button comes up.
                // The "match node" buttons below need no change: a clicked GUILayout.Button sets GUI.changed itself.
                fields.FloatSlider("Start param: ", key + "start", ref SMM.StartTime,
                    node.minSplination, node.maxSplination - 0.01f,
                    "Where along this node's section of road the extrusion begins, as a spline parameter.");
                if (GUILayout.Button("match node", EditorStyles.miniButton, GUILayout.Width(80f)))
                {
                    SMM.StartTime = node.time;
                }
                if (SMM.StartTime >= SMM.EndTime)
                {
                    SMM.EndTime = (SMM.StartTime + 0.01f);
                }
                EditorGUILayout.EndHorizontal();


                //End time:
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("End param: ", key + "end", ref SMM.EndTime,
                    SMM.StartTime, node.maxSplination,
                    "Where along this node's section of road the extrusion ends, as a spline parameter.");
                if (GUILayout.Button("match next", EditorStyles.miniButton, GUILayout.Width(80f)))
                {
                    SMM.EndTime = node.nextTime;
                }
                if (SMM.StartTime >= SMM.EndTime)
                {
                    SMM.EndTime = (SMM.StartTime + 0.01f);
                }
                EditorGUILayout.EndHorizontal();


                //Straight line options:
                if (node.IsStraight())
                {
                    EditorGUILayout.BeginVertical("box");
                    SMM.isStretched = EditorGUILayout.Toggle("Straight line stretch:", SMM.isStretched);
                    if (SMM.isStretched)
                    {
                        //Stretch_UVThreshold:
                        fields.FloatSlider("UV stretch threshold:", key + "uvstretch", ref SMM.stretchUVThreshold,
                            0.01f, 0.5f,
                            "How far a UV may be stretched before the extrusion adds another repeat instead.");
                        //UV repeats:
                        SMM.RepeatUVType = (Splination.RepeatUVTypeEnum)EditorGUILayout.Popup("UV stretch axis: ", (int)SMM.RepeatUVType, RepeatUVTypeDescriptionsSpline, GUILayout.Width(250f));
                    }
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    SMM.isStretched = false;
                }

                SMM.isTrimStart = EditorGUILayout.Toggle("Trim start:", SMM.isTrimStart);
                SMM.isTrimEnd = EditorGUILayout.Toggle("Trim end:", SMM.isTrimEnd);

                //Static option:
                SMM.isStatic = EditorGUILayout.Toggle("Static: ", SMM.isStatic);

                //Splination method
                //			SMM.EM.bMatchRoadIncrements = EditorGUILayout.Toggle("Match road increments: ",SMM.bMatchRoadIncrements); 
                SMM.isMatchingTerrain = EditorGUILayout.Toggle("Match ground: ", SMM.isMatchingTerrain);

                //Vector min/max threshold:
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Vertex min/max threshold: ", key + "minmax", ref SMM.minMaxMod, 0.01f, 0.2f,
                    "How close two vertices must be, in metres, before they count as the same edge of the extrusion.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.minMaxMod = 0.05f;
                }
                EditorGUILayout.EndHorizontal();

                //Vertex matching precision:
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Vertex matching precision: ", key + "vertprec", ref SMM.vertexMatchingPrecision,
                    0f, 0.01f,
                    "Tolerance, in metres, for welding the extrusion's ends onto the neighbouring section.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.vertexMatchingPrecision = 0.005f;
                }
                EditorGUILayout.EndHorizontal();

                //UV repeats:
                if (!SMM.isStretched)
                {
                    SMM.RepeatUVType = (Splination.RepeatUVTypeEnum)EditorGUILayout.Popup("UV repeat axis: ", (int)SMM.RepeatUVType, RepeatUVTypeDescriptionsSpline, GUILayout.Width(250f));
                }

                if (SMM.isMatchingRoadDefinition)
                {
                    EditorGUILayout.BeginVertical("TextArea");
                    EditorGUILayout.BeginHorizontal();
                    SMM.isMatchingRoadDefinition = EditorGUILayout.Toggle("Match road definition: ", SMM.isMatchingRoadDefinition);
                    if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        SMM.isMatchingRoadDefinition = false;
                    }
                    EditorGUILayout.EndHorizontal();
                    if (SMM.isMatchingRoadDefinition)
                    {
                        EditorGUILayout.LabelField("  Only use this option if object length doesn't match the road definition.", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField("  Matching road definition requires a UV repeat type.", EditorStyles.miniLabel);
                        EditorGUILayout.LabelField("  If the material fails to scale properly, try flipping the Y rotation.", EditorStyles.miniLabel);
                    }
                    //Flip rotation option:
                    SMM.isFlippingRotation = EditorGUILayout.Toggle("  Flip Y rotation: ", SMM.isFlippingRotation);
                    EditorGUILayout.EndVertical();
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    SMM.isMatchingRoadDefinition = EditorGUILayout.Toggle("Match road definition: ", SMM.isMatchingRoadDefinition);
                    if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                    {
                        SMM.isMatchingRoadDefinition = false;
                    }
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();


                #region "Vertical offset"
                EditorGUILayout.LabelField("Vertical options:");
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Vertical raise magnitude:", key + "vraise", ref SMM.verticalRaise, -512f, 512f,
                    "Metres to lift or drop the whole extrusion relative to the road surface.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.verticalRaise = 0f;
                }
                EditorGUILayout.EndHorizontal();
                #endregion


                #region "Vertical curve"
                if (SMM.verticalCurve == null || SMM.verticalCurve.keys.Length < 2)
                {
                    EnforceCurve(ref SMM.verticalCurve);
                }
                EditorGUILayout.BeginHorizontal();
                SMM.verticalCurve = EditorGUILayout.CurveField("Curve: ", SMM.verticalCurve);
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    ResetCurve(ref SMM.verticalCurve);
                }
                EditorGUILayout.EndHorizontal();
                #endregion
                EditorGUILayout.EndVertical();


                #region "Horizontal offsets"
                EditorGUILayout.LabelField("Horizontal offset options:");
                EditorGUILayout.BeginVertical("box");
                horizMatching = HorizMatchingDefaultsEnum.None;
                horizMatching = (HorizMatchingDefaultsEnum)EditorGUILayout.Popup((int)horizMatching, HorizMatchSubTypeDescriptions, GUILayout.Width(100f));
                if (horizMatching != HorizMatchingDefaultsEnum.None)
                {
                    if (horizMatching == HorizMatchingDefaultsEnum.MatchCenter)
                    {
                        SMM.horizontalSep = 0f;
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchRoadLeft)
                    {
                        SMM.horizontalSep = (node.spline.road.RoadWidth() / 2) * -1;
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchShoulderLeft)
                    {
                        SMM.horizontalSep = ((node.spline.road.RoadWidth() / 2) + node.spline.road.shoulderWidth) * -1;
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchRoadRight)
                    {
                        SMM.horizontalSep = (node.spline.road.RoadWidth() / 2);
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchShoulderRight)
                    {
                        SMM.horizontalSep = (node.spline.road.RoadWidth() / 2) + node.spline.road.shoulderWidth;
                    }
                    horizMatching = HorizMatchingDefaultsEnum.None;
                }
                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Horiz offset magnitude:", key + "hsep", ref SMM.horizontalSep,
                    (-1f * horizRoadMax), horizRoadMax,
                    "Metres to move the extrusion sideways from the road centre. Negative is to the left.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.horizontalSep = 0f;
                }
                EditorGUILayout.EndHorizontal();
                #endregion


                //Horizontal curve:
                if (SMM.horizontalCurve == null || SMM.horizontalCurve.keys.Length < 2)
                {
                    EnforceCurve(ref SMM.horizontalCurve);
                }

                EditorGUILayout.BeginHorizontal();
                SMM.horizontalCurve = EditorGUILayout.CurveField("Curve: ", SMM.horizontalCurve);
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    ResetCurve(ref SMM.horizontalCurve);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                //Vertical cutoff:
                EditorGUILayout.LabelField("Vertical cutoff:");
                EditorGUILayout.BeginVertical("box");
                SMM.isVerticalCutoff = EditorGUILayout.Toggle("Height cutoff enabled:", SMM.isVerticalCutoff);
                if (SMM.isVerticalCutoff)
                {
                    SMM.isVerticalCutoffMatchingZero = EditorGUILayout.Toggle("Match spline height:", SMM.isVerticalCutoffMatchingZero);
                    SMM.isVerticalCutoffDownwards = EditorGUILayout.Toggle("Cut direction toggle:", SMM.isVerticalCutoffDownwards);
                    fields.FloatSlider("Height cut offset: ", key + "vcut", ref SMM.VerticalCutoff, -50f, 50f,
                        "Height, in metres, at which the extrusion's geometry is cut away.");
                    SMM.isVerticalMeshCutoffOppositeDir = EditorGUILayout.Toggle("Opposite dir mesh cut:", SMM.isVerticalMeshCutoffOppositeDir);
                    fields.FloatSlider("Mesh cut offset: ", key + "vmeshcut", ref SMM.VerticalMeshCutoffOffset,
                        -5f, 5f,
                        "Metres to shift the cut plane away from the height above.");
                }
                EditorGUILayout.EndVertical();

                //End type:
                EditorGUILayout.LabelField("Extrusion ending options:");
                EditorGUILayout.BeginVertical("box");
                SMM.isStartDown = EditorGUILayout.Toggle("Push start down:", SMM.isStartDown);
                SMM.isEndDown = EditorGUILayout.Toggle("Push end down:", SMM.isEndDown);
                if (SMM.isStartDown)
                {
                    SMM.isStartTypeDownOverriden = EditorGUILayout.Toggle("Override start down value: ", SMM.isStartTypeDownOverriden);
                    if (SMM.isStartTypeDownOverriden)
                    {
                        fields.FloatSlider("Downward movement: ", key + "startdown", ref SMM.startTypeDownOverride,
                            -10f, 10f,
                            "Metres the start of the extrusion is pushed down, instead of the automatic amount.");
                    }
                }
                if (SMM.isEndDown)
                {
                    SMM.isEndTypeDownOverriden = EditorGUILayout.Toggle("Override end down value: ", SMM.isEndTypeDownOverriden);
                    if (SMM.isEndTypeDownOverriden)
                    {
                        fields.FloatSlider("Downward movement: ", key + "enddown", ref SMM.endTypeDownOverride,
                            -10f, 10f,
                            "Metres the end of the extrusion is pushed down, instead of the automatic amount.");
                    }
                }
                EditorGUILayout.EndVertical();

                //Start and end objects:
                EditorGUILayout.LabelField("Start & end objects:");
                EditorGUILayout.BeginVertical("box");
                //End cap custom match start:
                SMM.isEndCapCustomMatchStart = EditorGUILayout.Toggle("Match objects to ends:", SMM.isEndCapCustomMatchStart);

                //End objects match ground:
                SMM.isEndObjectsMatchingGround = EditorGUILayout.Toggle("Force origins to ground:", SMM.isEndObjectsMatchingGround);

                //Start cap:
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Start object:");
                endObjectAdd = (EndObjectsDefaultsEnum)EditorGUILayout.Popup((int)endObjectAdd, EndObjectsDefaultsEnumDesc);
                if (endObjectAdd != EndObjectsDefaultsEnum.None)
                {
                    SMM.EndCapStart = GetEndObjectQuickAdd();
                    endObjectAdd = EndObjectsDefaultsEnum.None;
                }
                EditorGUILayout.EndHorizontal();


                SMM.EndCapStart = (GameObject)EditorGUILayout.ObjectField("Prefab:", SMM.EndCapStart, typeof(GameObject), false);
                if (SMM.EndCapStart != null)
                {
                    // B-19. Three separate boxes rather than one Unity Vector3Field, so tabbing X -> Y commits X and a
                    // half-typed Z does not move the object. Each axis carries its own control name.
                    fields.Vector3Field("Position offset:", key + "capoffs", ref SMM.EndCapCustomOffsetStart,
                        "Metres to move the start object from where the extrusion begins.");
                    fields.Vector3Field("Rotation offset:", key + "caprots", ref SMM.EndCapCustomRotOffsetStart,
                        "Degrees to turn the start object from the road's own direction.");
                }
                EditorGUILayout.EndVertical();

                //End cap:
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("End object:");
                endObjectAdd = (EndObjectsDefaultsEnum)EditorGUILayout.Popup((int)endObjectAdd, EndObjectsDefaultsEnumDesc);
                if (endObjectAdd != EndObjectsDefaultsEnum.None)
                {
                    SMM.EndCapEnd = GetEndObjectQuickAdd();
                    SMM.EndCapCustomRotOffsetEnd = new Vector3(0f, 180f, 0f);
                    endObjectAdd = EndObjectsDefaultsEnum.None;
                }
                EditorGUILayout.EndHorizontal();


                SMM.EndCapEnd = (GameObject)EditorGUILayout.ObjectField("Prefab:", SMM.EndCapEnd, typeof(GameObject), false);
                if (SMM.EndCapEnd != null)
                {
                    fields.Vector3Field("Position offset:", key + "capoffe", ref SMM.EndCapCustomOffsetEnd,
                        "Metres to move the end object from where the extrusion finishes.");
                    fields.Vector3Field("Rotation offset:", key + "caprote", ref SMM.EndCapCustomRotOffsetEnd,
                        "Degrees to turn the end object from the road's own direction.");
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.EndVertical();

                //Collision:
                EditorGUILayout.LabelField("Collision options:");
                EditorGUILayout.BeginVertical("box");
                SMM.CollisionType = (Splination.CollisionTypeEnum)EditorGUILayout.Popup("Collision type: ", (int)SMM.CollisionType, TheCollisionTypeEnumDescSpline, GUILayout.Width(320f));
                //Mesh collison convex option
                if (SMM.CollisionType != Splination.CollisionTypeEnum.None && SMM.CollisionType != Splination.CollisionTypeEnum.BoxCollision)
                {
                    SMM.isCollisionConvex = EditorGUILayout.Toggle(" Convex: ", SMM.isCollisionConvex);
                    SMM.isCollisionTrigger = EditorGUILayout.Toggle(" Trigger: ", SMM.isCollisionTrigger);
                }

                if (SMM.CollisionType == Splination.CollisionTypeEnum.SimpleMeshTriangle || SMM.CollisionType == Splination.CollisionTypeEnum.SimpleMeshTrapezoid)
                {
                    SMM.isSimpleCollisionAutomatic = EditorGUILayout.Toggle(" Automatic simple collision: ", SMM.isSimpleCollisionAutomatic);
                }
                //If not automatic simple collisions:
                if (!SMM.isSimpleCollisionAutomatic)
                {
                    if (SMM.CollisionType == Splination.CollisionTypeEnum.SimpleMeshTriangle)
                    {
                        // B-19. Nine sliders that between them define one collision triangle. Dragging any one of them
                        // used to rebuild the extrusion's collider on every frame of the drag; the corner now moves
                        // once, when the handle is let go. `CollisionTriBL` and its siblings are plain Vector3 fields on
                        // the SplinatedMeshMaker, so a ref to one axis writes straight into the real object.
                        EditorGUILayout.LabelField("Bottom left:");
                        fields.FloatSlider(" x-axis: ", key + "ctblx", ref SMM.CollisionTriBL.x, SMM.mMinX - 5f, SMM.mMaxX + 5f, null);
                        fields.FloatSlider(" y-axis: ", key + "ctbly", ref SMM.CollisionTriBL.y, SMM.mMinY - 5f, SMM.mMaxY + 5f, null);
                        fields.FloatSlider(" z-axis: ", key + "ctblz", ref SMM.CollisionTriBL.z, SMM.mMinZ - 5f, SMM.mMaxZ + 5f, null);

                        EditorGUILayout.LabelField("Bottom right:");
                        fields.FloatSlider(" x-axis: ", key + "ctbrx", ref SMM.CollisionTriBR.x, SMM.mMinX - 5f, SMM.mMaxX + 5f, null);
                        fields.FloatSlider(" y-axis: ", key + "ctbry", ref SMM.CollisionTriBR.y, SMM.mMinY - 5f, SMM.mMaxY + 5f, null);
                        fields.FloatSlider(" z-axis: ", key + "ctbrz", ref SMM.CollisionTriBR.z, SMM.mMinZ - 5f, SMM.mMaxZ + 5f, null);

                        EditorGUILayout.LabelField("Top:");
                        fields.FloatSlider(" x-axis: ", key + "cttx", ref SMM.CollisionTriT.x, SMM.mMinX - 5f, SMM.mMaxX + 5f, null);
                        fields.FloatSlider(" y-axis: ", key + "ctty", ref SMM.CollisionTriT.y, SMM.mMinY - 5f, SMM.mMaxY + 5f, null);
                        fields.FloatSlider(" z-axis: ", key + "cttz", ref SMM.CollisionTriT.z, SMM.mMinZ - 5f, SMM.mMaxZ + 5f, null);

                    }
                    else if (SMM.CollisionType == Splination.CollisionTypeEnum.SimpleMeshTrapezoid)
                    {
                        fields.Vector3Field(" Bottom left:", key + "cbbl", ref SMM.CollisionBoxBL, null);
                        fields.Vector3Field(" Bottom right:", key + "cbbr", ref SMM.CollisionBoxBR, null);
                        fields.Vector3Field(" Top left:", key + "cbtl", ref SMM.CollisionBoxTL, null);
                        fields.Vector3Field(" Top right:", key + "cbtr", ref SMM.CollisionBoxTR, null);
                    }
                }

                if (SMM.CollisionType == Splination.CollisionTypeEnum.BoxCollision)
                {
                    fields.Vector3Field("Box collider center offset:", key + "boxoff", ref SMM.boxColliderOffset,
                        "Metres to move the box collider's centre from the extrusion's own centre.");
                    SMM.isBoxColliderFlippedOnX = EditorGUILayout.Toggle("Flip center X:", SMM.isBoxColliderFlippedOnX);
                    SMM.isBoxColliderFlippedOnZ = EditorGUILayout.Toggle("Flip center Z:", SMM.isBoxColliderFlippedOnZ);


                    SMM.isStretchedSize = EditorGUILayout.Toggle("Box collider size edit:", SMM.isStretchedSize);
                    if (SMM.isStretchedSize)
                    {
                        fields.Vector3Field("Size:", key + "boxsize", ref SMM.boxColliderSize,
                            "The box collider's size in metres, instead of the size taken from the extrusion.");
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Size:", SMM.boxColliderSize.ToString());
                    }
                }
                EditorGUILayout.EndVertical();


                EditorGUILayout.LabelField("Rotation options:");
                EditorGUILayout.BeginVertical("box");

                //Custom rotation:
                //EOM.CustomRotation = EditorGUILayout.Vector3Field("Custom rotation: ",EOM.CustomRotation);
                EditorGUILayout.BeginHorizontal();
                //Flip rotation option:
                SMM.isFlippingRotation = EditorGUILayout.Toggle("Flip Y rotation: ", SMM.isFlippingRotation);


                //if(GUILayout.Button("Reset custom rotation",EditorStyles.miniButton,GUILayout.Width(160f))){
                //	SMM.CustomRotation = new Vector3(0f,0f,0f);
                //}
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SMM.CustomRotation = new Vector3(0f, 0f, 0f);
                }
                EditorGUILayout.EndHorizontal();
                //SMM.CustomRotation = EditorGUILayout.Vector3Field("",SMM.CustomRotation);
                //SMM.CustomRotation.x = EditorGUILayout.Slider("x-axis: ",SMM.CustomRotation.x,-360f,360f);
                //SMM.CustomRotation.y = EditorGUILayout.Slider("y-axis: ",SMM.CustomRotation.y,-360f,360f);
                //SMM.CustomRotation.z = EditorGUILayout.Slider("z-axis: ",SMM.CustomRotation.z,-360f,360f);
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();


                EditorGUILayout.LabelField("Deprecated options:");
                EditorGUILayout.BeginVertical("box");
                SMM.isExactSplination = EditorGUILayout.Toggle("Directional extrusion: ", SMM.isExactSplination);

                EditorGUILayout.EndVertical();
                // Big Lines
                EditorUtilities.DrawLine(4f, 4f);
                EditorUtilities.DrawLine(4f, 4f);
            }
        }


        public void UpdateSplineObjects()
        {
            if (node.SplinatedObjects == null)
            {
                node.SplinatedObjects = new List<Splination.SplinatedMeshMaker>();
            }
            count = node.SplinatedObjects.Count;
            for (int index = 0; index < count; index++)
            {
                SMM = node.SplinatedObjects[index];

                SMM.UpdatePositions();
                if (SMM.isStretched)
                {
                    SMM.CollisionType = Splination.CollisionTypeEnum.BoxCollision;
                    SMM.isMatchingRoadDefinition = false;
                    SMM.isMatchingTerrain = false;
                    SMM.isCollisionConvex = false;
                    SMM.isStartDown = false;
                    SMM.isEndDown = false;
                    SMM.isVerticalCutoff = false;
                    SMM.isExactSplination = false;
                    SMM.isEndTypeDownOverriden = false;
                }

                SMM.Setup(true);
            }
        }


        public void UpdateSplineObjectsOnUndo()
        {
            if (!node.CanSplinate())
            {
                return;
            }
            if (node.SplinatedObjects == null)
            {
                node.SplinatedObjects = new List<Splination.SplinatedMeshMaker>();
            }

            //Destroy all children:
            for (int index = node.transform.childCount - 1; index >= 0; index--)
            {
                Object.DestroyImmediate(node.transform.GetChild(index).gameObject);
            }

            //Re-setup the SMM:
            count = node.SplinatedObjects.Count;
            for (int index = 0; index < count; index++)
            {
                SMM = node.SplinatedObjects[index];
                SMM.UpdatePositions();
                if (SMM.isStretched)
                {
                    SMM.CollisionType = Splination.CollisionTypeEnum.BoxCollision;
                    SMM.isMatchingRoadDefinition = false;
                    SMM.isMatchingTerrain = false;
                    SMM.isCollisionConvex = false;
                    SMM.isStartDown = false;
                    SMM.isEndDown = false;
                    SMM.isVerticalCutoff = false;
                    SMM.isExactSplination = false;
                    SMM.isEndTypeDownOverriden = false;
                }

                SMM.Setup(true);
            }

            UpdateEdgeObjectsOnUndo();
        }


        /// <summary> Renders the EdgeObject items in the Editor </summary>
        public void DoEdgeObjects()
        {
            if (node.EdgeObjects == null)
            {
                node.EdgeObjects = new List<EdgeObjects.EdgeObjectMaker>();
            }
            count = node.EdgeObjects.Count;

            EOM = null;

            for (int index = 0; index < node.EdgeObjects.Count; index++)
            {
                EOM = node.EdgeObjects[index];

                // B-19. Same reasoning as the extrusion loop above: a node can carry several edge object sets that draw
                // identical fields, so every deferred control's name is qualified by the entry's list position.
                string key = "eom" + index.ToString() + "#";

                currentCount += 1;
                EditorGUILayout.BeginVertical("TextArea");

                if (EOM.isRequiringUpdate)
                {
                    EOM.Setup();
                }
                EOM.isRequiringUpdate = false;

                EditorGUILayout.BeginHorizontal();

                EOM.isToggled = EditorGUILayout.Foldout(EOM.isToggled, "#" + currentCount.ToString() + ": " + EOM.objectName);

                // B-14. The edge-object row's marker, same as the extrusion row's above: a picture, not a command.
                GUILayout.Label(edgeButtonTexture, imageButton, GUILayout.Width(32f));
                if (GUILayout.Button(refreshButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    EOM.Setup();
                }
                if (GUILayout.Button(saveButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    SaveWindow saveWindow = EditorWindow.GetWindow<SaveWindow>();
                    saveWindow.Initialize(ref sceneRect, SaveWindow.WindowTypeEnum.Edge, node, null, EOM);
                }

                if (GUILayout.Button(copyButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    Undo.RecordObject(node, "Copy");
                    node.CopyEdgeObject(index);
                    EditorUtility.SetDirty(node);
                }
                if (GUILayout.Button(deleteButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    Undo.RecordObject(node, "Delete");
                    node.RemoveEdgeObject(index);
                    EditorUtility.SetDirty(node);
                }
                EditorGUILayout.EndHorizontal();

                if (!EOM.isToggled)
                {
                    EditorGUILayout.EndVertical();
                    continue;
                }

                GUILayout.Space(8f);
                EditorGUILayout.LabelField("General options:");

                EditorGUILayout.BeginVertical("box");
                //Name:
                fields.StringField("Name: ", key + "name", ref EOM.objectName,
                    "The name this edge object set is listed under, and the name given to the objects it places.");

                //Edge object:
                EOM.edgeObject = (GameObject)EditorGUILayout.ObjectField("Edge object: ", EOM.edgeObject, typeof(GameObject), false);

                #region "Material override"
                EOM.isMaterialOverriden = EditorGUILayout.Toggle("Material override: ", EOM.isMaterialOverriden);
                if (!EOM.isMaterialOverriden)
                {
                    EOM.edgeMaterial1 = null;
                    EOM.edgeMaterial2 = null;
                }

                if (EOM.isMaterialOverriden)
                {
                    EOM.edgeMaterial1 = (Material)EditorGUILayout.ObjectField("Override mat #1: ", EOM.edgeMaterial1, typeof(Material), false);
                    EOM.edgeMaterial2 = (Material)EditorGUILayout.ObjectField("Override mat #2: ", EOM.edgeMaterial2, typeof(Material), false);
                }
                #endregion


                #region "Combine Mesh / MeshCollider"
                if (EOM.isSingle)
                {
                    EOM.isCombinedMesh = false;
                }
                else
                {
                    EOM.isCombinedMesh = EditorGUILayout.Toggle("Combine meshes: ", EOM.isCombinedMesh);

                    if (EOM.isCombinedMesh)
                    {
                        EOM.isCombinedMeshCollider = EditorGUILayout.Toggle("Combined mesh collider: ", EOM.isCombinedMeshCollider);
                    }
                }
                #endregion


                #region "SingleObject"
                isSingleTemp = EditorGUILayout.Toggle("Single object only: ", EOM.isSingle);
                if (isSingleTemp != EOM.isSingle)
                {
                    EOM.endTime = node.nextTime;
                    //EOM.endPos = node.spline.GetSplineValue(EOM.endTime, false);
                    EOM.singlePosition = node.time + 0.025f;

                    EOM.isSingle = isSingleTemp;

                    if (EOM.isSingle)
                    {
                        EOM.isCombinedMesh = false;
                    }
                }

                if (EOM.isSingle)
                {
                    // B-19. Every one of these commits on Enter, focus-out or handle-release. On this inspector that
                    // matters more than most: the apply gate below calls UpdateEdgeObjects(), which re-places every
                    // object in the set, so a dragged handle used to delete and re-instantiate a whole row of
                    // guard rails on every frame of the drag.
                    fields.FloatSlider("Single location: ", key + "singlepos", ref EOM.singlePosition, node.time, 1f,
                        "Where along this node's section of road the single object is placed.");

                    if (node.isBridgeStart && node.isBridgeMatched)
                    {
                        fields.FloatSlider("Bridge %: ", key + "bridgepct", ref EOM.singleOnlyBridgePercent, 0f, 1f,
                            "Where along the bridge the object is placed, as a fraction of the bridge's length.");
                        float dist = (EOM.singleOnlyBridgePercent * (node.bridgeCounterpartNode.dist - node.dist) + node.dist);
                        EOM.singlePosition = node.spline.TranslateDistBasedToParam(dist);
                    }
                }
                #endregion


                EOM.isStatic = EditorGUILayout.Toggle("Static: ", EOM.isStatic);
                EOM.isMatchingTerrain = EditorGUILayout.Toggle("Match ground height: ", EOM.isMatchingTerrain);

                if (!EOM.isSingle)
                {
                    fields.FloatSlider("Dist between objects: ", key + "metersep", ref EOM.meterSep, 1f, 256f,
                        "Spacing in metres between one placed object and the next.");
                }


                #region "Match Road"
                EOM.isStartMatchRoadDefinition = EditorGUILayout.Toggle("Match road definition: ", EOM.isStartMatchRoadDefinition);
                if (EOM.isStartMatchRoadDefinition)
                {
                    fields.FloatSlider("Position fine tuning: ", key + "startmatch", ref EOM.startMatchRoadDef, 0f, 1f,
                        "Nudges the first object's position within one road definition step.");
                }
                #endregion


                // Multi placed edge objects
                if (!EOM.isSingle)
                {
                    if (EOM.startTime < node.minSplination)
                    {
                        EOM.startTime = node.minSplination;
                    }
                    if (EOM.endTime > node.maxSplination)
                    {
                        EOM.endTime = node.maxSplination;
                    }


                    #region "Start param"
                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider("Start param: ", key + "start", ref EOM.startTime,
                        node.minSplination, EOM.endTime,
                        "Where along this node's section of road the run of objects begins.");

                    if (EOM.endTime < EOM.startTime)
                    {
                        EOM.endTime = Mathf.Clamp(EOM.startTime + 0.01f, 0f, 1f);
                    }


                    if (GUILayout.Button("match node", EditorStyles.miniButton, GUILayout.Width(80f)))
                    {
                        EOM.startTime = node.time;
                    }
                    EditorGUILayout.EndHorizontal();
                    #endregion


                    #region "End param"
                    EditorGUILayout.BeginHorizontal();
                    fields.FloatSlider("End param: ", key + "end", ref EOM.endTime,
                        EOM.startTime, node.maxSplination,
                        "Where along this node's section of road the run of objects ends.");
                    //Mathf.Clamp(EditorGUILayout.Slider( "End param: ", EOM.EndTime, 0f/*EOM.StartTime*/, 1f/*tNode.MaxSplination*/ ), 0f, 1f);
                    // FH EXPERIMENTAL fix for EdgeObjects???

                    if (EOM.startTime > EOM.endTime)
                    {
                        EOM.startTime = Mathf.Clamp(EOM.endTime - 0.01f, 0f, 1f);
                    }

                    if (GUILayout.Button("match next", EditorStyles.miniButton, GUILayout.Width(80f)))
                    {
                        EOM.endTime = node.nextTime;
                    }
                    EditorGUILayout.EndHorizontal();
                    #endregion
                }
                EditorGUILayout.EndVertical();


                #region "Vertical offset"
                EditorGUILayout.LabelField("Vertical options:");
                EditorGUILayout.BeginVertical("box");

                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Vertical raise magnitude:", key + "vraise", ref EOM.verticalRaise, -512f, 512f,
                    "Metres to lift or drop the placed objects relative to the road surface.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    EOM.verticalRaise = 0f;
                }
                EditorGUILayout.EndHorizontal();

                if (EOM.verticalCurve == null || EOM.verticalCurve.keys.Length < 2)
                {
                    EnforceCurve(ref EOM.verticalCurve);
                }
                EditorGUILayout.BeginHorizontal();
                EOM.verticalCurve = EditorGUILayout.CurveField("Curve: ", EOM.verticalCurve);
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    ResetCurve(ref EOM.verticalCurve);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                #endregion


                #region "Horizontal offsets"
                EditorGUILayout.LabelField("Horizontal offset options:");
                EditorGUILayout.BeginVertical("box");
                horizMatching = HorizMatchingDefaultsEnum.None;
                horizMatching = (HorizMatchingDefaultsEnum)EditorGUILayout.Popup((int)horizMatching, HorizMatchSubTypeDescriptions, GUILayout.Width(100f));
                if (horizMatching != HorizMatchingDefaultsEnum.None)
                {
                    if (horizMatching == HorizMatchingDefaultsEnum.MatchCenter)
                    {
                        EOM.horizontalSep = 0f;
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchRoadLeft)
                    {
                        EOM.horizontalSep = (node.spline.road.RoadWidth() * 0.5f) * -1;
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchShoulderLeft)
                    {
                        if (node.spline.road.isShouldersEnabled)
                        {
                            EOM.horizontalSep = ((node.spline.road.RoadWidth() * 0.5f) + node.spline.road.shoulderWidth) * -1;
                        }
                        else
                        {
                            EOM.horizontalSep = ((node.spline.road.RoadWidth() * 0.5f)) * -1;
                        }
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchRoadRight)
                    {
                        EOM.horizontalSep = (node.spline.road.RoadWidth() * 0.5f);
                    }
                    else if (horizMatching == HorizMatchingDefaultsEnum.MatchShoulderRight)
                    {
                        if (node.spline.road.isShouldersEnabled)
                        {
                            EOM.horizontalSep = (node.spline.road.RoadWidth() * 0.5f) + node.spline.road.shoulderWidth;
                        }
                        else
                        {
                            EOM.horizontalSep = (node.spline.road.RoadWidth() * 0.5f);
                        }
                    }
                    horizMatching = HorizMatchingDefaultsEnum.None;
                }

                EditorGUILayout.BeginHorizontal();
                fields.FloatSlider("Horiz offset magnitude:", key + "hsep", ref EOM.horizontalSep,
                    (-1f * horizRoadMax), horizRoadMax,
                    "Metres to move the placed objects sideways from the road centre. Negative is to the left.");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    EOM.horizontalSep = 0f;
                }
                EditorGUILayout.EndHorizontal();
                if (EOM.horizontalCurve == null || EOM.horizontalCurve.keys.Length < 2)
                {
                    EnforceCurve(ref EOM.horizontalCurve);
                }
                EditorGUILayout.BeginHorizontal();
                EOM.horizontalCurve = EditorGUILayout.CurveField("Curve: ", EOM.horizontalCurve);
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    ResetCurve(ref EOM.horizontalCurve);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                #endregion


                #region "Rotation/scale options"
                EditorGUILayout.LabelField("Rotation/scale options:");
                EditorGUILayout.BeginVertical("box");
                if (EOM.horizontalSep < 0f)
                {
                    EOM.isOncomingRotation = EditorGUILayout.Toggle("Auto rotate oncoming objects: ", EOM.isOncomingRotation);
                    EOM.subType = SignPlacementSubTypeEnum.Left;
                }
                else
                {
                    EOM.subType = SignPlacementSubTypeEnum.Right;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Custom rotation: ");
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    EOM.customRotation = new Vector3(0f, 0f, 0f);
                }
                EditorGUILayout.EndHorizontal();

                fields.FloatSlider("x-axis: ", key + "rotx", ref EOM.customRotation.x, -360f, 360f,
                    "Degrees of extra rotation about X applied to each placed object.");
                fields.FloatSlider("y-axis: ", key + "roty", ref EOM.customRotation.y, -360f, 360f,
                    "Degrees of extra rotation about Y applied to each placed object.");
                fields.FloatSlider("z-axis: ", key + "rotz", ref EOM.customRotation.z, -360f, 360f,
                    "Degrees of extra rotation about Z applied to each placed object.");
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField("Lock align rotation: ");
                EOM.isRotationAligning = EditorGUILayout.Toggle("Align objects to road rotation: ", EOM.isRotationAligning);
                if (!EOM.isRotationAligning)
                {
                    EOM.isXRotationLocked = EditorGUILayout.Toggle("Lock x axis: ", EOM.isXRotationLocked);
                    EOM.isYRotationLocked = EditorGUILayout.Toggle("Lock y axis: ", EOM.isYRotationLocked);
                    EOM.isZRotationLocked = EditorGUILayout.Toggle("Lock z axis: ", EOM.isZRotationLocked);
                }
                EditorGUILayout.EndVertical();

                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.BeginHorizontal();
                // B-19. One slider standing for all three axes, so it drives a local and the uniform vector is written
                // only on the pass the slider commits. The old line assigned `new Vector3(scale, scale, scale)` on every
                // repaint, which meant a non-uniform customScale set anywhere else was flattened the next time an author
                // so much as moved the mouse over this inspector.
                float scale = EOM.customScale.x;
                if (fields.FloatSlider("Custom scale: ", key + "scale", ref scale, 1f, 10f,
                    "Uniform scale applied to each placed object."))
                {
                    EOM.customScale = new Vector3(scale, scale, scale);
                }
                if (GUILayout.Button(defaultButtonTexture, imageButton, GUILayout.Width(16f)))
                {
                    EOM.customScale = new Vector3(1f, 1f, 1f);
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndVertical();
                #endregion
            }
        }


        public void UpdateEdgeObjects()
        {
            count = node.EdgeObjects.Count;
            for (int index = 0; index < node.EdgeObjects.Count; index++)
            {
                EOM = node.EdgeObjects[index];
                EOM.UpdatePositions();
                EOM.Setup();
            }
        }


        public void UpdateEdgeObjectsOnUndo()
        {
            if (!node.CanSplinate())
            {
                return;
            }
            count = node.EdgeObjects.Count;
            for (int index = 0; index < node.EdgeObjects.Count; index++)
            {
                EOM = node.EdgeObjects[index];
                EOM.Setup();
            }
        }


        #region "Quick adds"
        private void BridgeAddTopBase(float _horizSep = 0f, float _vertRaise = -0.01f, string _mat = "", bool _isOverridenPrefab = false, string _overridePrefab = "")
        {
            string basePath = RoadEditorUtility.GetBasePath();

            if (_mat == "")
            {
                _mat = basePath + "/Materials/Concrete2.mat";
            }
            SMM = node.AddSplinatedObject();
            string tBridgeTopBaseToAdd = "";
            string tName = "";
            if (node.spline.road.LaneTotal() == 2)
            {
                if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base1MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-19w-5l-1d.fbx";
                    tName = "BridgeTop1M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base2MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-20w-5l-1d.fbx";
                    tName = "BridgeTop2M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base3MDeep)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-18w-5l-3d.fbx";
                    tName = "BridgeTop0M-3M";
                }
                else
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-18w-5l-1d.fbx";
                    tName = "BridgeTop0M-1M";
                }
            }
            else if (node.spline.road.LaneTotal() == 4)
            {
                if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base1MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-29w-5l-1d.fbx";
                    tName = "BridgeTop1M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base2MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-30w-5l-1d.fbx";
                    tName = "BridgeTop2M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base3MDeep)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-28w-5l-3d.fbx";
                    tName = "BridgeTop0M-3M";
                }
                else
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-28w-5l-1d.fbx";
                    tName = "BridgeTop0M-1M";
                }
            }
            else
            {
                if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base1MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-39w-5l-1d.fbx";
                    tName = "BridgeTop1M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base2MOver)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-40w-5l-1d.fbx";
                    tName = "BridgeTop2M-1M";
                }
                else if (BridgeTopBaseQuickAdd == BridgeTopBaseDefaultsEnum.Base3MDeep)
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-38w-5l-3d.fbx";
                    tName = "BridgeTop0M-3M";
                }
                else
                {
                    tBridgeTopBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase-38w-5l-1d.fbx";
                    tName = "BridgeTop0M-1M";
                }
            }

            if (_isOverridenPrefab)
            {
                tBridgeTopBaseToAdd = _overridePrefab;
            }

            SMM.objectName = tName;
            SMM.currentSplination = EngineIntegration.LoadAssetFromPath<GameObject>(tBridgeTopBaseToAdd);
            SMM.horizontalSep = _horizSep;
            SMM.verticalRaise = _vertRaise;
            SMM.isMaterialOverriden = true;
            SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            SMM.Axis = Splination.AxisTypeEnum.Z;

            BridgeTopBaseQuickAdd = BridgeTopBaseDefaultsEnum.None;
            if (SMM.StartTime < node.minSplination)
            {
                SMM.StartTime = node.minSplination;
            }
            if (SMM.EndTime > node.maxSplination)
            {
                SMM.EndTime = node.maxSplination;
            }
        }


        private void BridgeAddBottomBase(float _horizSep = 0f, float _vertRaise = -1.01f, string _mat = "", bool _isOverridenPrefab = false, string _overridePrefab = "")
        {
            string basePath = RoadEditorUtility.GetBasePath();

            if (_mat == "")
            {
                _mat = basePath + "/Materials/Concrete2.mat";
            }
            SMM = node.AddSplinatedObject();
            string tBridgeBottomBaseToAdd = "";
            string tName = "";
            if (node.spline.road.LaneTotal() == 2)
            {
                if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase2)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase2-18w-5l-3d.fbx";
                    tName = "BridgeBase2";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase3)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase3-18w-5l-5d.fbx";
                    tName = "BridgeBase3";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase4)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase4-18w-5l-5d.fbx";
                    tName = "BridgeBase4";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase5)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase5-18w-5l-5d.fbx";
                    tName = "BridgeBase5";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase6)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase6On2Lanes.fbx";
                    tName = "BridgeArchBeam80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase7)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase7On2Lanes.fbx";
                    tName = "BridgeArchSolid80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase8)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase8On2Lanes.fbx";
                    tName = "BridgeArchSolid180";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBaseGrid)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseGrid-18w-5l-5d.fbx";
                    tName = "BridgeGrid";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeSteel)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseSteelBeam-18w-20l-3d.fbx";
                    tName = "BridgeSteelBeams";
                }
            }
            else if (node.spline.road.LaneTotal() == 4)
            {
                if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase2)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase2-28w-5l-3d.fbx";
                    tName = "BridgeBase2";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase3)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase3-28w-5l-5d.fbx";
                    tName = "BridgeBase3";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase4)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase4-28w-5l-5d.fbx";
                    tName = "BridgeBase4";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase5)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase5-28w-5l-5d.fbx";
                    tName = "BridgeBase5";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase6)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase6On4Lanes.fbx";
                    tName = "BridgeArchBeam80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase7)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase7On4Lanes.fbx";
                    tName = "BridgeArchSolid80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase8)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase8On4Lanes.fbx";
                    tName = "BridgeArchSolid180";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBaseGrid)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseGrid-28w-5l-5d.fbx";
                    tName = "BridgeGrid";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeSteel)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseSteelBeam-28w-20l-3d.fbx";
                    tName = "BridgeSteelBeams";
                }
            }
            else
            {
                if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase2)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase2-38w-5l-3d.fbx";
                    tName = "BridgeBase2";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase3)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase3-38w-5l-5d.fbx";
                    tName = "BridgeBase3";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase4)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase4-38w-5l-5d.fbx";
                    tName = "BridgeBase4";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase5)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase5-38w-5l-5d.fbx";
                    tName = "BridgeBase5";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase6)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase6On6Lanes.fbx";
                    tName = "BridgeArchBeam80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase7)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase7On6Lanes.fbx";
                    tName = "BridgeArchSolid80";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase8)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBase8On6Lanes.fbx";
                    tName = "BridgeArchSolid180";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBaseGrid)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseGrid-38w-5l-5d.fbx";
                    tName = "BridgeGrid";
                }
                else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeSteel)
                {
                    tBridgeBottomBaseToAdd = basePath + "/Mesh/Bridges/BridgeBaseSteelBeam-38w-20l-3d.fbx";
                    tName = "BridgeBeams";
                }
            }

            if (_isOverridenPrefab)
            {
                tBridgeBottomBaseToAdd = _overridePrefab;
            }

            SMM.currentSplination = EngineIntegration.LoadAssetFromPath<GameObject>(tBridgeBottomBaseToAdd);
            SMM.horizontalSep = _horizSep;
            SMM.verticalRaise = _vertRaise;
            SMM.isMaterialOverriden = true;
            SMM.objectName = tName;

            if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase2)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase3)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase4)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase5)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase6)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase7)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBase8)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(_mat);
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeBaseGrid)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Steel7.mat");
            }
            else if (BridgeBottomBaseQuickAdd == BridgeBottomBaseDefaultsEnum.BridgeSteel)
            {
                SMM.SplinatedMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Steel7.mat");
            }

            SMM.Axis = Splination.AxisTypeEnum.Z;

            BridgeTopBaseQuickAdd = BridgeTopBaseDefaultsEnum.None;
            if (SMM.StartTime < node.minSplination)
            {
                SMM.StartTime = node.minSplination;
            }
            if (SMM.EndTime > node.maxSplination)
            {
                SMM.EndTime = node.maxSplination;
            }
        }


        private void ExtrusionQuickAdd(bool _isHorizOverriden = false, float _horizSep = 0f, bool _isVertOverriden = false, float _vertRaise = 0f)
        {
            try
            {
                ExtrusionQuickAddDo();
            }
            catch (System.Exception exception)
            {
                SMMQuickAdd = SMMDefaultsEnum.None;
                throw exception;
            }
        }


        private void ExtrusionQuickAddDo()
        {
            if (SMMQuickAdd == SMMDefaultsEnum.KRail)
            {
                node.SplinatedObjectQuickAdd("KRail");
            }
        }


        private void ExtrudeHelper(string _path, string _name, float DefaultHoriz, Splination.AxisTypeEnum _axisType = Splination.AxisTypeEnum.Z, bool _isHorizOverriden = false, float _horizSep = 0f, bool _isVertOverriden = false, float _vertRaise = 0f, bool _isFlippingRot = false)
        {
            SMM = node.AddSplinatedObject();
            SMM.currentSplination = EngineIntegration.LoadAssetFromPath<GameObject>(_path);

            if (_isHorizOverriden)
            {
                SMM.horizontalSep = _horizSep;
            }
            else
            {
                SMM.horizontalSep = ((node.spline.road.RoadWidth() / 2) + node.spline.road.shoulderWidth) * -1f;
            }

            if (_isVertOverriden)
            {
                SMM.verticalRaise = _vertRaise;
            }
            else
            {
                if (node.isBridgeStart)
                {
                    SMM.verticalRaise = -0.01f;
                }
            }

            SMM.isFlippingRotation = _isFlippingRot;
            SMM.Axis = _axisType;
            if (SMM.StartTime < node.minSplination)
            {
                SMM.StartTime = node.minSplination;
            }
            if (SMM.EndTime > node.maxSplination)
            {
                SMM.EndTime = node.maxSplination;
            }
            SMM.objectName = _name;
        }
        #endregion


        public void OnSceneGUI()
        {
            currentEvent = Event.current;

            if (!isSceneRectSet)
            {
                try
                {
                    sceneRect = EditorWindow.GetWindow<SceneView>().position;
                }
                catch
                {
                    sceneRect = EditorWindow.GetWindow<EditorWindow>().position;
                }
                isSceneRectSet = true;
            }

            if (currentEvent.type == EventType.ValidateCommand)
            {
                switch (currentEvent.commandName)
                {
                    case "UndoRedoPerformed":
                        UpdateSplineObjectsOnUndo();
                        break;
                }
            }

            //Drag with left click release:
            if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
            {
                // The near-node search runs on every left-click release. It used to be a scene-wide
                // FindObjectsByType<SplineN>() -- an engine traversal plus a fresh array allocation per click, scaling
                // with the whole scene rather than the road. SplineN now keeps a registry of the enabled nodes, so the
                // same set comes from a plain list with no traversal and no allocation; the distance test is compared
                // squared to drop a per-node square root.
                IReadOnlyList<SplineN> nodeObjects = SplineN.ActiveNodes;
                Vector3 droppedPosition = node.transform.position;
                const float proximitySqr = 2f * 2f;
                // B-13. Whether the drop was absorbed by a snap onto another node. A snap makes this node's position
                // exactly equal to another node's, and that exact coincidence IS the connection or the intersection --
                // so the ground checks below must leave it alone. The node it snapped to is already on the ground.
                bool isSnappedToNode = false;
                for (int ni = 0; ni < nodeObjects.Count; ni++)
                {
                    SplineN xNode = nodeObjects[ni];
                    if (xNode == null) { continue; }
                    if ((xNode.transform.position - droppedPosition).sqrMagnitude < proximitySqr)
                    {
                        if (xNode == node)
                        {
                            continue;
                        }
                        if (node.isSpecialEndNode || xNode.isSpecialEndNode)
                        {
                            continue;
                        }
                        if (xNode.isEndPoint && node.isEndPoint)
                        {
                            //End point connection.
                            node.transform.position = xNode.transform.position;
                            isSnappedToNode = true;
                            //Activate special end node for tnode
                            TriggerRoadConnection(node, xNode);
                            break;
                        }
                        if (xNode.isIntersection)
                        {
                            continue;
                        }
                        if (xNode.isNeverIntersect)
                        {
                            continue;
                        }
                        if (node.isEndPoint && xNode.isEndPoint)
                        {
                            continue;
                        }
                        if (xNode.spline == node.spline)
                        {
                            //Don't let intersection be created on consecutive nodes:
                            if ((node.idOnSpline + 1) == xNode.idOnSpline || (node.idOnSpline - 1) == xNode.idOnSpline)
                            {
                                continue;
                            }
                        }
                        node.transform.position = xNode.transform.position;
                        isSnappedToNode = true;
                        TriggerIntersection(node, xNode);
                        break;
                    }
                    else
                    {
                        continue;
                    }
                }

                //Enforce maximum road grade:
                if (VectorDiff(node.transform.position, node.pos))
                {
                    // B-13. Creation lifts a node clear of the ground under it; DRAGGING did not, so the move tool
                    // could push the same node under the surface and leave it there -- buried, which is half of what
                    // this entry is about. Lift only ever raises, so a node the author deliberately elevated stays
                    // where they put it: an authored elevation is a request to raise the GROUND to it, not something
                    // to be pulled back down.
                    //
                    // And a node dragged off the terrain altogether is REFUSED rather than lifted, because there is
                    // nothing there to lift it clear of and every later pass would sample a surface that is not there.
                    // `node.pos` still holds where it was before the drag, so putting it back is exact.
                    //
                    // B-17. Through the ROAD's lift, not the plain one, where there is a road. The plain lift measures
                    // from the live surface, and on a road that has already been built the live surface under it is the
                    // carriageway the road itself raised -- so dragging a node down moved it straight back up to the
                    // road, every rebuild raised the floor again, and the node genuinely could not be lowered. The road
                    // keeps the ground it found before it built; measured from that, a drag downwards is a drag
                    // downwards.
                    bool isAccepted = true;
                    if (!isSnappedToNode)
                    {
                        if (RoadNodeGround.IsOverTerrain(node.transform.position))
                        {
                            Road ownerRoad = node.spline != null ? node.spline.road : null;
                            node.transform.position = ownerRoad != null
                                ? ownerRoad.LiftNode(node.transform.position, 0f)
                                : RoadNodeGround.Lift(node.transform.position);
                        }
                        else
                        {
                            Debug.LogWarning(RoadNodeGround.NoTerrainMessage(node.transform.position)
                                + " The node was returned to where it was before the drag.");
                            node.transform.position = node.pos;
                            isAccepted = false;
                        }
                    }

                    if (isAccepted)
                    {
                        node.pos = node.transform.position;
                        if (node.IsLegitimate() && node.spline.road.isMaxGradeEnabled)
                        {
                            node.EnsureGradeValidity();
                        }
                        TriggerRoadUpdate();
                    }
                }
            }

            if (Selection.activeGameObject == node.transform.gameObject)
            {
                if (currentEvent.keyCode == KeyCode.F5)
                {
                    TriggerRoadUpdate();
                }
            }

            if (GUI.changed)
            {
                EditorUtility.SetDirty(node);
            }
        }


        private bool VectorDiff(Vector3 _vect1, Vector3 _vect2)
        {
            if (!RootUtils.IsApproximately(_vect1.x, _vect2.x, 0.0001f))
            {
                return true;
            }
            if (!RootUtils.IsApproximately(_vect1.y, _vect2.y, 0.0001f))
            {
                return true;
            }
            if (!RootUtils.IsApproximately(_vect1.z, _vect2.z, 0.0001f))
            {
                return true;
            }
            return false;
        }


        private bool VectorEquals(ref Vector3 _v1, ref Vector3 _v2)
        {
            if (!RootUtils.IsApproximately(_v1.x, _v2.x, 0.001f))
            {
                return false;
            }
            if (!RootUtils.IsApproximately(_v1.y, _v2.y, 0.001f))
            {
                return false;
            }
            if (!RootUtils.IsApproximately(_v1.z, _v2.z, 0.001f))
            {
                return false;
            }
            return true;
        }


        #region "Triggers Intersections; Connections; Update"
        private void TriggerRoadConnection(SplineN _node1, SplineN _node2)
        {
            node.spline.ActivateEndNodeConnection(_node1, _node2);
        }


        private void TriggerIntersection(SplineN _node1, SplineN _node2)
        {
            isCreatingIntersection = true;
            node1 = _node1;
            node2 = _node2;
            Selection.activeGameObject = Intersections.CreateIntersection(_node1, _node2);
        }


        private void TriggerRoadUpdate()
        {
            if (node != null)
            {
                node.spline.road.isUpdateRequired = true;
            }
        }
        #endregion


        private void ResetCurve(ref AnimationCurve _curve)
        {
            _curve = null;
            _curve = new AnimationCurve();
            EnforceCurve(ref _curve);
        }


        private void EnforceCurve(ref AnimationCurve _curve)
        {
            if (_curve == null)
            {
                return;
            }
            if (_curve.keys.Length == 0)
            {
                _curve.AddKey(0f, 1f);
                _curve.AddKey(1f, 1f);
            }
            else if (_curve.keys.Length == 1)
            {
                _curve.keys[0].time = 0f;
                _curve.AddKey(1f, 1f);
            }
        }


        private GameObject GetEndObjectQuickAdd()
        {
            string basePath = RoadEditorUtility.GetBasePath();

            string path = "";
            if (endObjectAdd == EndObjectsDefaultsEnum.WarningSign1_Static)
            {
                path = basePath + "/Prefabs/Interactive/WarningSignStatic.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.WarningSign2_Static)
            {
                path = basePath + "/Prefabs/Interactive/WarningSignStatic2.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Atten_Static)
            {
                path = basePath + "/Prefabs/Interactive/AttenStatic.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel1_Static)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelStatic.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel1_Rigid)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelRigid.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel3_Static)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelStatic3.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel3_Rigid)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelRigid3.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel7_Static)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelStatic7.prefab";
            }
            else if (endObjectAdd == EndObjectsDefaultsEnum.Barrel7_Rigid)
            {
                path = basePath + "/Prefabs/Interactive/RoadBarrelRigid7.prefab";
            }
            else
            {
                return null;
            }

            return EngineIntegration.LoadAssetFromPath<GameObject>(path);
        }
    }
}
#endif
