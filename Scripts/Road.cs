#region "Imports"
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using RoadCityMaster;
#endregion


namespace RoadCityMaster
{
    [ExecuteInEditMode]
    public class Road : MonoBehaviour
    {
        public enum RoadMaterialDropdownEnum
        {
            Asphalt,
            Dirt,
            Brick,
            Cobblestone
        };


        /// <summary>
        /// Real-world road classification. Selecting a class applies real-world default lane
        /// counts, lane widths (metres), shoulders and centre-divider behaviour via
        /// ApplyRoadClassDefaults(). Classes at or after RailSingle are spline-only carriers:
        /// they keep the spline (and any edge objects / splinated meshes that ride it) but emit
        /// no carriageway mesh (isGeneratingRoadMesh is set false for them).
        /// </summary>
        public enum RoadClassEnum
        {
            //Carriageway (mesh-generating) classes:
            UrbanSingleLane,   //1 lane, no centre line
            UrbanTwoLane,      //1+1 opposing lanes, centre line
            OneWayStreet,      //all lanes one direction
            UrbanFourLane,     //2+2 opposing lanes, centre divider
            Collector,         //2+2, wider lanes
            Arterial,          //3+3
            HighwayDual,       //divided 2+2 or 3+3 with hard shoulders
            Motorway,          //divided high-speed, wide shoulders
            ServiceLane,       //narrow single track
            //Spline-only carrier classes (no carriageway mesh):
            RailSingle,        //single rail track carrier
            RailDouble,        //twin rail track carrier
            Ropeway,           //cable / ropeway carrier
            PedestrianPath     //footway centreline carrier
        };


        #region "Vars"
        public GameObject MainMeshes;
        public GameObject MeshRoad;
        public GameObject MeshShoR;
        public GameObject MeshShoL;
        public GameObject MeshiLanes;
        public GameObject MeshiLanes0;
        public GameObject MeshiLanes1;
        public GameObject MeshiLanes2;
        public GameObject MeshiLanes3;
        public GameObject MeshiMainPlates;
        public GameObject MeshiMarkerPlates;

        [System.NonSerialized]
        public string editorTitleString = "";

        public SplineC spline;

        public int MostRecentNodeCount = -1;
        public GameObject splineObject;
        public RoadSystem roadSystem;
        public SplineC[] PiggyBacks = null;
        public bool isEditorProgressBar = false;
        //Unique ID
        public string UID;
        [SerializeField]
        public List<TerrainHistoryMaker> TerrainHistory;
        public string TerrainHistoryByteSize = "";
        [System.NonSerialized]
        public bool isUpdatingSpline = false;

        //Road editor options:
        /// <summary> Width of ONE travel lane, metres. Real travel lanes run ~3.0 m (urban local) to ~3.75 m
        /// (motorway); this initialiser is the <see cref="RoadClassEnum.UrbanTwoLane"/> row of
        /// <see cref="City.RoadClassDefaults"/>, because that is the class <see cref="appliedRoadClass"/> starts at.
        ///
        /// RM-109. It has to be that row and not a number of its own. The legacy initialiser here was 5 m — no real
        /// travel lane is 5 m wide, and worse, it made a brand-new road DISAGREE with the class it claimed to be. The
        /// override-preserving merge reads any field that differs from the applied class's default as something the
        /// author deliberately set, so a 5 m lane on a road that had never been touched was treated as an authored
        /// choice and survived every later class change. The road could not be talked out of it. </summary>
        public float laneWidth = 3.25f;
        public bool isShouldersEnabled = true;
        /// <summary> Width of the paved shoulder outside EACH outer lane, metres — the hard strip a vehicle pulls onto
        /// to clear the travelled way, and the surface that carries the cross-fall down to the ground.
        ///
        /// A shoulder is narrower than a lane, and how much narrower is what tells the two apart on sight: ~0.5-1.0 m
        /// on an urban street (a hard strip, not a refuge), 1.5-2.5 m on a collector or arterial, and 3.0-3.5 m only on
        /// a highway or motorway, where it must hold a stopped lorry entirely clear of the traffic lane. Those are the
        /// figures in <see cref="City.RoadClassDefaults"/>, per class, and this initialiser is the UrbanTwoLane row.
        ///
        /// RM-109. The legacy initialiser was 3 m against a 5 m lane. 3 m is a full urban lane width, so the shoulder
        /// read as an extra lane rather than as a shoulder — which is exactly what it looked like in the scene. </summary>
        public float shoulderWidth = 1f;
        /// <summary> Lanes running the spline's own direction. This carries the value of the old directionless
        /// <c>laneAmount</c> across through <see cref="FormerlySerializedAsAttribute"/>: on a road serialised before
        /// RM-89 the whole TOTAL arrives here and <see cref="lanesBackward"/> arrives as its unsplit sentinel, so
        /// <see cref="EnsureLaneModel"/> splits it once. No existing value is lost. </summary>
        [FormerlySerializedAs("laneAmount")] public int lanesForward = 1;
        /// <summary> Lanes running against the spline direction; 0 on a one-way road. Starts at
        /// <see cref="LaneModel.Unsplit"/> (-1), which is what marks a road whose counts have not been split yet --
        /// a field absent from the serialised data takes its initialiser, so a negative sentinel is the only value
        /// that can tell legacy data apart from a freshly constructed component. </summary>
        public int lanesBackward = LaneModel.Unsplit;
        /// <summary> Width in metres of the physical median between the opposing carriageways. Only occupies section
        /// width when there are backward lanes to separate AND <see cref="centerDivider"/> is a physical divider --
        /// painted markers separate the directions without taking any width. </summary>
        public float medianWidth = 0f;
        /// <summary> Auxiliary lanes authored along this road: speed-change lanes at an interchange diverge or merge,
        /// climbing lanes on a grade, turn bays at a junction approach, and permanent widenings. Each exists only
        /// between two distances along the spline and tapers in and out, which is what allows the lane count to change
        /// part way along a road instead of jumping at a node. Empty on an ordinary road of constant cross-section, and
        /// the section arithmetic takes a cheaper path when it is. </summary>
        public List<AuxiliaryLaneAuthored> auxLanes = new List<AuxiliaryLaneAuthored>();
        public float roadDefinition = 5f;
        public bool roadCornerDefinition = false;
        public bool isRoadCutsEnabled = true;
        public bool isShoulderCutsEnabled = true;
        public bool isDynamicCutsEnabled = false;
        public bool isMaxGradeEnabled = true;
        public float maxGrade = 0.08f;
        public bool isUsingDefaultMaterials = true;
        public bool isAutoUpdatingInEditor = true;

        public float matchTerrainSubtraction = 0.1f;
        public bool isRaisingRoad = false;

        /// <summary> Defines the width of height modification in meters </summary>
        public float matchHeightsDistance = 50f;
        public float clearDetailsDistance = 30f;
        public float clearDetailsDistanceHeight = 5f;
        public float clearTreesDistance = 30f;
        public float clearTreesDistanceHeight = 50f;

        public bool isHeightModificationEnabled = true;
        public bool isDetailModificationEnabled = true;
        public bool isTreeModificationEnabled = true;

        public bool isSavingTerrainHistoryOnDisk = true;
        public float magnitudeThreshold = 300f;
        public bool isGizmosEnabled = true;
        public bool isUsingMultithreading = true;
        public bool isSavingMeshes = false;
        public bool isUsingMeshColliders = true;

        //Real-world road classification and lane model:
        public RoadClassEnum roadClass = RoadClassEnum.UrbanTwoLane;
        /// <summary> The class whose defaults were last applied by <see cref="ApplyRoadClassDefaults(bool)"/>. Added
        /// (not substituted) so an upgraded road keeps its serialised lane counts and laneWidth unchanged; it lets a later
        /// class change refresh only the fields the author left at the outgoing class's default. </summary>
        [HideInInspector] public RoadClassEnum appliedRoadClass = RoadClassEnum.UrbanTwoLane;
        /// <summary> When false this Road keeps its spline (and any edge objects / splinated
        /// meshes that ride it) but generates NO carriageway mesh and applies NO road terrain
        /// deformation. This is the carrier used by rail / ropeway / centreline road classes. </summary>
        public bool isGeneratingRoadMesh = true;
        /// <summary> True: every lane runs the same direction (one-way). False: lanes are split
        /// into two opposing directions about the centre line (the real-world default). </summary>
        public bool isOneWay = false;
        /// <summary> Physical separation carried down the centre line between opposing carriageways. </summary>
        public CenterDividerTypeEnum centerDivider = CenterDividerTypeEnum.Markers;
        /// <summary> Posted/real-world speed limit in km/h, informational + drives design defaults. </summary>
        public float speedLimitKPH = 50f;
        /// <summary> RM-103. What the carriageway is MADE of, as the one object that holds every way an unsealed road
        /// differs from a sealed one (crown cross-slope, lane-marking policy, painted-verge width, side ditch, material
        /// set). Surface is its own axis, independent of <see cref="roadClass"/>: an arterial can be sealed or gravel,
        /// so changing the class leaves this alone and changing this leaves the class alone. Starts sealed because that
        /// is what the overwhelming majority of authored roads are, and because a road with no surface at all would
        /// make every reader of it ask what to do about nothing. </summary>
        public City.SurfaceTypeClass surfaceType = City.SurfaceTypeClass.Sealed();
        public bool isStatic = false;
        public bool isLightmapped = false;
        public float desiredRampHeight = 0.35f;


        public RoadMaterialDropdownEnum roadMaterialDropdown = RoadMaterialDropdownEnum.Asphalt;
        public RoadMaterialDropdownEnum tRoadMaterialDropdownOLD = RoadMaterialDropdownEnum.Asphalt;


        public Material RoadMaterial1;
        public Material RoadMaterial2;
        public Material RoadMaterial3;
        public Material RoadMaterial4;
        public Material RoadMaterialMarker1;
        public Material RoadMaterialMarker2;
        public Material RoadMaterialMarker3;
        public Material RoadMaterialMarker4;
        public Material ShoulderMaterial1;
        public Material ShoulderMaterial2;
        public Material ShoulderMaterial3;
        public Material ShoulderMaterial4;
        public Material ShoulderMaterialMarker1;
        public Material ShoulderMaterialMarker2;
        public Material ShoulderMaterialMarker3;
        public Material ShoulderMaterialMarker4;

        // `PhysicMaterial` became `PhysicsMaterial` in Unity 6 and the old type no longer exists, so the guarded
        // branch that declared it could not have compiled if it were ever taken. RM-92 pins the editor at 6000.5; the
        // field NAMES keep their spelling because they are serialized, and renaming them would orphan the physics
        // material on every road already saved in a scene.
        public PhysicsMaterial RoadPhysicMaterial;
        public PhysicsMaterial ShoulderPhysicMaterial;
        #endregion


        #region "Road Construction"
        #region "Vars"
        [System.NonSerialized]
        public Threading.TerrainCalcs TerrainCalcsJob;
        [System.NonSerialized]
        public Threading.RoadCalcs1 RoadCalcsJob1;
        [System.NonSerialized]
        public Threading.RoadCalcs2 RoadCalcsJob2;
        [System.NonSerialized]
        public RoadConstructorBufferMaker RCS;

        public string roadName = "";
        public bool isProfiling = false;
        public bool isSkippingStore = true;
        [System.NonSerialized]
        public float EditorConstructionStartTime = 0f;
        public bool isEditorError = false;
        // The exception itself, kept only so the inspector can show what went wrong in the pass that just failed.
        // System.Exception is not Unity-serializable at all, and isEditorError above is the part worth saving.
        [System.NonSerialized]
        public System.Exception exceptionError = null;


        private int editorTimer = 0;
        private int editorTimerMax = 0;

        /// <summary> RM-100. How long a single road build may run before it is declared abandoned and its workers are
        /// stopped.
        ///
        /// Three minutes, and it is a runaway guard rather than a deadline: the longest road this tool builds -- a fifty
        /// kilometre trunk route across several terrains, terraforming every one of them -- is seconds of arithmetic, so
        /// a build still running after three minutes is not slow, it is stuck. Named rather than left as a bare 180 in
        /// the middle of the pump, because a number that decides whether a build is thrown away should say so. </summary>
        private const float BuildAbandonedAfterSeconds = 180f;
        private int editorTimerSpline = 0;
        private const int editorTimerSplineMax = 2;
        [System.NonSerialized]
        public int editorProgress = 0;

        /// <summary> RM-100. True while this road's staged build is being pumped by <see cref="City.BuildDispatch"/>
        /// rather than by this road's own frame callback. Without it a queued road would be advanced twice per frame --
        /// once by the dispatcher's step and once by <c>Update</c> -- which would run two stages in one frame and undo
        /// the serialisation the queue exists to provide. </summary>
        [System.NonSerialized] public bool isDispatched = false;
        private const int gizmoNodeTimerMax = 2;
        public bool isUpdateRequired = false;
        public bool isTriggeringGC = false;
        private bool isTriggeredGCExecuting;
        private float triggerGCEnd = 0f;

        [System.NonSerialized]
        public bool isEditorCameraMoving = false;
        [System.NonSerialized]
        public float EditorCameraPos = 0f;
        private const float EditorCameraTimeUpdateInterval = 0.015f;
        private float EditorCameraNextMove = 0f;
        private bool isEditorCameraSetup = false;
        private float EditorCameraStartPos = 0f;
        private float EditorCameraEndPos = 1f;
        private float EditorCameraIncrementDistance = 0f;
        private float EditorCameraIncrementDistance_Full = 0f;
        public float EditorCameraMetersPerSecond = 60f;
        public bool isEditorCameraRotated = false;
        private Vector3 EditorCameraV1 = default(Vector3);
        private Vector3 EditorCameraV2 = default(Vector3);
        [System.NonSerialized]
        public Vector3 editorCameraOffset = new Vector3(0f, 5f, 0f);
        [System.NonSerialized]
        public Camera editorPlayCamera = null;
        private Vector3 editorCameraBadVec = default(Vector3);

        // Handed over by Terraforming.ProcessRoadTerrainHook (SetEditorTerrainCalcs) for the duration of one terrain
        // pass and set back to null at the end of it, at lines ~1159 and ~1200. It holds the heightmap float arrays the
        // pass is working on; serializing it would write a copy of terrain heights into the scene file.
        [System.NonSerialized]
        public List<Terraforming.TempTerrainData> EditorTTDList;

        public bool isEditorConstructing = false;
        public int editorConstructionID = 0;
        public bool isEditorSelected = false;
        public bool isEditorMouseHittingTerrain = false;
        public Vector3 editorMousePos = new Vector3(0f, 0f, 0f);
        public Color defaultNodeColor = new Color(0f, 1f, 1f, 0.75f);
        /// <summary> Connection node color </summary>
        public readonly Color Color_NodeConnColor = new Color(0f, 1f, 0f, 0.75f);
        /// <summary> The color of the nodes when they are selected </summary>
        public Color selectedColor = Color.yellow;
        /// <summary> Color of the node preview when adding a new node </summary>
        public Color newNodePreviewColor = Color.red;
        #endregion


        /// <summary> Make sure unused items are not using memory space in runtime </summary>
        private void CleanRunTime()
        {
            TerrainHistory = null;
            RCS = null;
        }


        /// <summary> The two editor callbacks this road registered, kept so they can be removed again exactly. They are
        /// plain <c>object</c> because the delegate types belong to <c>UnityEditor</c>, and this file no longer names
        /// it: <see cref="EngineIntegration"/> owns the subscription and hands the registration back as a token. </summary>
        [System.NonSerialized] private object editorTickToken;
        [System.NonSerialized] private object hierarchyChangedToken;


        private void OnEnable()
        {
            EnsureLaneModel();
            isEditorConstructing = false;

            // Unsubscribe before subscribing, so a road that is disabled and re-enabled REPLACES its handlers instead
            // of stacking a second copy of each. The old code could not do this: it subscribed an anonymous delegate,
            // which no later `-=` can match, so the handlers accumulated for the life of the editor session.
            EngineIntegration.UnsubscribeEditorTick(editorTickToken);
            editorTickToken = EngineIntegration.SubscribeEditorTick(EditorUpdate);
            EngineIntegration.UnsubscribeHierarchyChanged(hierarchyChangedToken);
            hierarchyChangedToken = EngineIntegration.SubscribeHierarchyChanged(HierarchyWindowChanged);

            if (spline == null || spline.nodes == null)
            {
                MostRecentNodeCount = 0;
            }
            else
            {
                MostRecentNodeCount = spline.GetNodeCount();
            }

            #if UNITY_EDITOR
            tRoadMaterialDropdownOLD = roadMaterialDropdown;
            CheckMats();
            #endif
        }


        /// <summary> Gives back both editor callbacks. This is the half the old code never had: it subscribed in
        /// <see cref="OnEnable"/> and had nowhere to unsubscribe, so closing a scene left the editor holding handlers
        /// on roads that no longer existed, and each reopen added more. Removing the tokens is exact — they are the
        /// delegates that were registered — and doing it here means the count returns to zero every time. </summary>
        private void OnDisable()
        {
            EngineIntegration.UnsubscribeEditorTick(editorTickToken);
            editorTickToken = null;
            EngineIntegration.UnsubscribeHierarchyChanged(hierarchyChangedToken);
            hierarchyChangedToken = null;
            isEditorConstructing = false;
            isEditorProgressBar = false;
        }


        public void Awake()
        {
            EnsureLaneModel();
            if (spline == null || spline.nodes == null)
            {
                MostRecentNodeCount = 0;
            }
            else
            {
                MostRecentNodeCount = spline.GetNodeCount();
            }
        }


        /// <summary> RM-100. True while this road is being built. The name matches
        /// <c>UniverseCreatorObject.IsGenerating</c> deliberately: an inspector or a progress surface that can watch one
        /// should not need a second vocabulary to watch the other. Backed by the flag the pump already keeps rather than
        /// by a second one, so it cannot disagree with what <see cref="AdvanceBuild"/> is doing. </summary>
        public bool IsGenerating
        {
            get { return isEditorConstructing; }
        }

        /// <summary> RM-100. The worker currently running for this road, or null between stages and when idle. A staged
        /// build has three workers in sequence, so this is whichever one is live -- which is what a progress surface
        /// needs and is why it is not a single stored task. </summary>
        public System.Threading.Tasks.Task CurrentTask
        {
            get
            {
                if (TerrainCalcsJob != null && !TerrainCalcsJob.IsDone) { return TerrainCalcsJob.Task; }
                if (RoadCalcsJob1 != null && !RoadCalcsJob1.IsDone) { return RoadCalcsJob1.Task; }
                if (RoadCalcsJob2 != null && !RoadCalcsJob2.IsDone) { return RoadCalcsJob2.Task; }
                return null;
            }
        }

        /// <summary> RM-100. Ask this road's build to stop. Cooperative: the workers unwind at their next check and the
        /// applies that would have installed the result are never reached, so a cancelled build leaves no partial road
        /// rather than half of one. </summary>
        public void CancelBuild()
        {
            AbortJobs();
            isEditorConstructing = false;
            isEditorProgressBar = false;
        }


        /// <summary> RM-100. Advance a staged road build by one frame's worth. Returns true while a build is still in
        /// flight.
        ///
        /// This body used to live inside <c>EditorUpdate</c>, which is registered on
        /// <c>EditorApplication.update</c> inside a <c>UNITY_EDITOR</c> block -- so outside the editor there was no pump
        /// at all, and a road asked to rebuild in a player set <c>isEditorConstructing</c> and then waited for a frame
        /// that never came. Pulling it out costs nothing and makes the same staged build work from a
        /// <c>MonoBehaviour.Update</c>, which is what the host's contract asks for: the build path carries no
        /// <c>UnityEditor</c> reference of its own.
        ///
        /// The stages are already the right shape and are left alone. <see cref="UpdateRoad"/>'s own tail reads the
        /// scene and starts the terrain worker; <c>ConstructRoad2</c>, <c>3</c> and <c>4</c> are the main-thread applies
        /// between the workers.
        /// That is prepare / work / apply, three times over, and it is why the editor was always ABLE to stay
        /// responsive -- what stopped it was the modal bar, not the build.
        ///
        /// Two things did change here. Job completion is now checked every call rather than once every
        /// <c>editorTimerMax</c> calls: a finished worker used to sit waiting for its turn to be noticed, which is
        /// latency added on purpose to save a lock acquisition per road per frame. And the runaway guard now actually
        /// stops the workers -- it used to clear the two flags and leave the threads running, so a build that was
        /// declared abandoned could still come back and apply itself over whatever the author did next. </summary>
        public bool AdvanceBuild()
        {
            //Custom garbage collection demands for editor:
            if (isTriggeringGC)
            {
                isTriggeringGC = false;
                triggerGCEnd = Time.realtimeSinceStartup + 1f;
                isTriggeredGCExecuting = true;
            }
            if (isTriggeredGCExecuting)
            {
                if (Time.realtimeSinceStartup > triggerGCEnd)
                {
                    isTriggeredGCExecuting = false;
                    RootUtils.ForceCollection();
                    triggerGCEnd = 200000f;
                }
            }

            if (isEditorConstructing)
            {
                if (isUsingMultithreading)
                {
                    editorTimer += 1;
                    if (editorTimer > editorTimerMax)
                    {
                        editorTimer = 0;
                        if ((Time.realtimeSinceStartup - EditorConstructionStartTime) > BuildAbandonedAfterSeconds)
                        {
                            AbortJobs();
                            isEditorConstructing = false;
                            isEditorProgressBar = false;
                            Debug.Log("Road & City Master: a road build has been running for more than "
                                + BuildAbandonedAfterSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                + " seconds and has been abandoned. Nothing was applied.");
                        }
                    }

                    if (isEditorError)
                    {
                        AbortJobs();
                        isEditorConstructing = false;
                        isEditorProgressBar = false;

                        isEditorError = false;
                        if (exceptionError != null)
                        {
                            System.Exception thrown = exceptionError;
                            exceptionError = null;
                            Debug.LogError(thrown.StackTrace);
                            throw thrown;
                        }
                    }

                    if (TerrainCalcsJob != null && TerrainCalcsJob.Update())
                    {
                        ConstructRoad2();
                    }
                    else if (RoadCalcsJob1 != null && RoadCalcsJob1.Update())
                    {
                        ConstructRoad3();
                    }
                    else if (RoadCalcsJob2 != null && RoadCalcsJob2.Update())
                    {
                        ConstructRoad4();
                    }
                }
            }
            else
            {
                // B-14. This flag was the last route into the SYNCHRONOUS build, and it is the route four
                // author-facing operations still took: creating an intersection (Intersections.cs), rebuilding one
                // (RoadIntersectionEditor), a node operation (SplineNEditor) and connecting two roads
                // (SplineC.ActivateEndNodeConnection) all end by setting it. `spline.TriggerSetup()` is
                // `road.UpdateRoad()` run inline, so all four bypassed RM-100's dispatch and RM-101's supersede: real
                // work ran on the main thread with nothing named in Background Tasks, which is the state B-14 was
                // filed about. RM-102 fixed the Road inspector's own rebuilds by funnelling them through
                // RequestUpdate and left this flag pointing at the old path for every other caller.
                //
                // The flag itself stays -- it is public, and an author's own editor script may set it -- but it now
                // means "ask for a build" rather than "build now, silently". The construction code is the same either
                // way: RequestUpdate is the queued front door onto UpdateRoad, not a second implementation of it, so
                // the geometry cannot differ. What changes is that each of the four gets a named, cancellable,
                // supersede-able ticket instead of an unannounced main-thread build.
                //
                // Deliberately not acted on while this road's own queued build is still concluding. Superseding from
                // inside the step that is finishing would ask a build that completed normally to stop, and its
                // Background Tasks row would then read as cancelled. The flag is left SET in that case rather than
                // cleared and dropped: isDispatched is false again on the next tick and the request is still standing.
                if (isUpdateRequired && !isDispatched)
                {
                    isUpdateRequired = false;
                    // The same null-spline guard the synchronous path carried. A road whose spline has been destroyed
                    // has nothing to build, and asking would only queue a refusal against its name.
                    if (spline != null) { RequestUpdate(); }
                }
            }

            if (isEditorConstructing || isEditorProgressBar)
            {
                RoadPublishProgress();
            }
            return isEditorConstructing;
        }


        private void EditorUpdate()
        {
            if (this == null)
            {
                // A destroyed road removes exactly the registration it made, which the old anonymous-delegate `-=`
                // never did: it built a second closure, removed nothing, and left this tick firing on a dead road for
                // the rest of the session.
                EngineIntegration.UnsubscribeEditorTick(editorTickToken);
                editorTickToken = null;
                isEditorConstructing = false;
                isEditorProgressBar = false;
                return;
            }

            if (!isDispatched) { AdvanceBuild(); }

            #if UNITY_EDITOR
            if (!Application.isPlaying && isUpdatingSpline)
            {
                editorTimerSpline += 1;
                if (editorTimerSpline > editorTimerSplineMax)
                {
                    editorTimerSpline = 0;
                    isUpdatingSpline = false;
                    // B-14. Was `spline.TriggerSetup()`, which is `UpdateRoad()` run inline: a node added or removed
                    // in the scene view rebuilt the whole road on the main thread with nothing named in Background
                    // Tasks. The debounce above already collapses a burst of node edits into one fire; going through
                    // the queued front door adds RM-101's supersede on top, so a fire that lands while this road is
                    // still building retires that build instead of running a second one beside it.
                    RequestUpdate();
                    // One accessor for this field everywhere it is written (OnEnable, Awake, the build path and here),
                    // so the inspector's "Total nodes" cannot depend on which path last set it.
                    MostRecentNodeCount = spline.GetNodeCount();
                }
            }


            double editorNow = EngineIntegration.EditorTimeSinceStartup;
            if (isEditorCameraMoving && EditorCameraNextMove < editorNow)
            {
                EditorCameraNextMove = (float)editorNow + EditorCameraTimeUpdateInterval;
                DoEditorCameraLoop();
            }
            #endif
        }


        public void DoEditorCameraLoop()
        {
            if (!isEditorCameraSetup)
            {
                isEditorCameraSetup = true;
                if (spline.isSpecialEndControlNode)
                {
                    //If control node, start after the control node:
                    EditorCameraEndPos = spline.nodes[spline.GetNodeCount() - 2].time;
                }
                if (spline.isSpecialStartControlNode)
                {
                    //If ends in control node, end construction before the control node:
                    EditorCameraStartPos = spline.nodes[1].time;
                }
                ChangeEditorCameraMetersPerSec();
            }

            // `Application.isEditor` is the runtime spelling of the `#if UNITY_EDITOR` that used to wrap this: both are
            // true in the editor's play mode, so the fly-through still stops when the road is deselected there, and a
            // player build still never asks about a selection it does not have.
            if (Application.isEditor && !EngineIntegration.IsObjectSelected(this.transform.gameObject))
            {
                QuitEditorCamera();
                return;
            }

            //EditorCameraPos_Full+=EditorCameraIncrementDistance_Full;
            //if(EditorCameraPos_Full > spline.distance)
            //{
            //  EditorCameraPos = EditorCameraStartPos;
            //  isEditorCameraMoving = false;
            //  EditorCameraPos_Full = 0f;
            //  return;
            //  }
            //EditorCameraPos = spline.TranslateDistBasedToParam(EditorCameraPos_Full);

            EditorCameraPos += EditorCameraIncrementDistance;
            if (EditorCameraPos > EditorCameraEndPos)
            {
                QuitEditorCamera();
                return;
            }
            if (EditorCameraPos < EditorCameraStartPos)
            {
                EditorCameraPos = EditorCameraStartPos;
            }

            spline.GetSplineValueBoth(EditorCameraPos, out EditorCameraV1, out EditorCameraV2);


            #if UNITY_EDITOR
            if (Application.isPlaying)
            {
                if (editorPlayCamera != null)
                {
                    editorPlayCamera.transform.position = EditorCameraV1;
                    if (isEditorCameraRotated)
                    {
                        editorPlayCamera.transform.position += editorCameraOffset;
                        if (EditorCameraV2 != editorCameraBadVec)
                        {
                            editorPlayCamera.transform.rotation = Quaternion.LookRotation(EditorCameraV2);
                        }
                    }
                }
            }
            else
            {
                // The pivot is written once, already offset, rather than assigned and then added to: the old code read
                // `lastActiveSceneView` four times without ever checking it for null, which threw whenever a scene was
                // open with no scene view on screen -- a batch-mode run, or a maximised game view.
                Vector3 pivot = EditorCameraV1;
                if (isEditorCameraRotated)
                {
                    pivot += editorCameraOffset;
                    if (EditorCameraV2 != editorCameraBadVec)
                    {
                        EngineIntegration.SetSceneViewRotation(Quaternion.LookRotation(EditorCameraV2));
                    }
                }
                EngineIntegration.SetSceneViewPivot(pivot);
                EngineIntegration.RepaintLastSceneView();
            }
            #endif
        }


        public void EditorCameraSetSingle()
        {
            if (editorPlayCamera == null)
            {
                Camera[] editorCameras = EngineIntegration.FindObjectsByType<Camera>();
                if (editorCameras != null && editorCameras.Length == 1)
                {
                    editorPlayCamera = editorCameras[0];
                }
            }
        }


        public void QuitEditorCamera()
        {
            EditorCameraPos = EditorCameraStartPos;
            isEditorCameraMoving = false;
            isEditorCameraSetup = false;
        }


        public void ChangeEditorCameraMetersPerSec()
        {
            EditorCameraIncrementDistance_Full = (EditorCameraMetersPerSecond / 60);
            EditorCameraIncrementDistance = (EditorCameraIncrementDistance_Full / spline.distance);
        }


        /// <summary> This is called when the hierarchy is changed in the editor. It no longer tries to unsubscribe
        /// itself on the way in: the old body did that with an anonymous delegate, which removed nothing, and if it had
        /// ever matched, the first hierarchy change of a session would have been the last one this road heard about.
        /// <see cref="OnDisable"/> owns the removal now, and it removes the registration this road actually made. </summary>
        private void HierarchyWindowChanged()
        {
            if (Application.isPlaying || !Application.isEditor)
            {
                return;
            }

            // Entering, in, or leaving play mode: the hierarchy is being rebuilt by the engine, not edited by an
            // author, and rebuilding roads in the middle of that writes into a scene that is about to be thrown away.
            if (EngineIntegration.IsPlayModeEnteringOrRunning)
            {
                return;
            }

            int count = 0;
            if (spline != null && spline.nodes != null)
            {
                // B-14. Was `GetNodeCountNonNull()`, compared against a MostRecentNodeCount that every writer fills
                // from `GetNodeCount()` -- the RAW list length. The two disagree by exactly the number of null entries
                // in the list, so a spline holding a single destroyed node made this test true on EVERY editor tick,
                // forever: `isUpdatingSpline` was re-armed each tick and the debounce fired a full main-thread road
                // rebuild every editorTimerSplineMax ticks for the rest of the session, with nothing in Background
                // Tasks to say why the editor was busy. Like-for-like now, which is all a change detector needs.
                //
                // Nothing is lost by not calling the other one here. Despite its name it is not a counter: it destroys
                // intersections whose node is gone as it walks the list, and that sweep is what CheckInvalidNodeCount
                // does from the build path -- so it still happens, from a build rather than from a tick.
                count = spline.GetNodeCount();
            }
            if (count != MostRecentNodeCount)
            {
                isUpdatingSpline = true;
            }
        }


        /// <summary> RM-100. Publish how far the road update has got, and stop there.
        ///
        /// This used to raise <c>EditorUtility.DisplayProgressBar</c>, which is a MODAL bar: it takes the editor's input
        /// away for the duration and repaints nothing else. That is the author's actual complaint -- the build was
        /// already staged across several editor frames, so the editor could have stayed usable the whole time and the
        /// only thing stopping it was the bar put up to say that it was busy.
        ///
        /// So the runtime side now only records the two figures, and whoever wants to show them reads them. The editor
        /// surface (RM-101) reads them into Unity's own background task area, where a build is a line in the status bar
        /// with a cancel button rather than a wall across the screen; a player build that regenerates a road reads them
        /// or ignores them. Nothing in this file touches <c>UnityEditor</c> to do it. </summary>
        private void RoadPublishProgress()
        {
            if (isEditorConstructing)
            {
                isEditorProgressBar = true;
                return;
            }
            isEditorProgressBar = false;
        }


        /// <summary> RM-101. This road's identity in the build queue, and deliberately not its name: a road system names
        /// its roads Road0, Road1 … per system, so a scene with two systems holds two "Road0" -- and superseding on the
        /// name would cancel one author's build because a different road happened to match. An entity id is unique for
        /// as long as a queue entry can live. One definition, read by <see cref="RequestUpdate"/> when it supersedes and
        /// by the inspectors when they ask whether THIS road is building, so the two can never disagree.
        ///
        /// `GetEntityId` and not the older per-object id call: Unity 6 made that one obsolete-as-error, and it is on
        /// this project's forbidden-API list for the same reason. The value it returns serves the identical purpose --
        /// unique for this object's lifetime, meaningless across sessions, never persisted. </summary>
        public string BuildOwnerId
        {
            get { return "road:" + GetEntityId().ToString(); }
        }


        /// <summary> RM-100. Ask for this road to be rebuilt, and return. The one entry every caller should use --
        /// per-road refresh, the refresh-all pass in <c>RoadSystem.UpdateAllRoads</c>, the inspector button, the node
        /// editor, the automation API -- so that clicking Refresh on two roads at once queues two builds instead of
        /// starting two, and so a cancel reaches whichever one is in flight.
        ///
        /// <see cref="UpdateRoad"/> is still what does the work and is still callable directly; this is the queued front
        /// door onto it, not a second implementation of it. That distinction is deliberate and is what C-06 rests on: the
        /// synchronous path and the queued path run the SAME construction code, in the same order, so they cannot
        /// produce different geometry.
        ///
        /// A road with multithreading switched off is built inline, because there is nothing to wait for -- the whole
        /// build happens inside <see cref="UpdateRoad"/> before it returns, and queueing it would only delay it by a
        /// frame. </summary>
        public void RequestUpdate(RoadUpdateTypeEnum _updateType = RoadUpdateTypeEnum.Full)
        {
            if (!isUsingMultithreading)
            {
                UpdateRoad(_updateType);
                return;
            }

            RoadUpdateTypeEnum updateType = _updateType;
            string what = "road " + transform.name;
            string owner = BuildOwnerId;

            // RM-101. An author dragging the lane-width slider asks for a build on every change. Without this the queue
            // fills with builds of this same road, each one's result thrown away by the next, and the author waits for
            // all of them to watch the last one land. Retire the older ones -- the queued ones are dropped and get their
            // finish, and one already in flight is asked to stop and ends from the pump -- then queue this one.
            City.BuildDispatch.Shared.Supersede(owner);

            City.BuildDispatch.Shared.EnqueueStaged(
                what,
                owner,
                delegate
                {
                    // Main thread: the road's own read-the-scene pass, which ends by starting the terrain worker. If it
                    // declined -- updates switched off for the system, fewer than two nodes, a spline-only carrier that
                    // finished inside the call -- it leaves isEditorConstructing false and the dispatcher records a
                    // refusal rather than a failure.
                    UpdateRoad(updateType);
                    isDispatched = isEditorConstructing;
                    return isEditorConstructing;
                },
                delegate
                {
                    bool more = AdvanceBuild();
                    City.BuildDispatch.Shared.Report(editorProgress * 0.01f);
                    return more;
                },
                delegate
                {
                    // Whatever happened -- finished, cancelled, threw -- this road must not be left holding workers or
                    // claiming to be mid-build.
                    if (isEditorConstructing) { CancelBuild(); }
                    isDispatched = false;
                    isEditorProgressBar = false;
                });
        }


        /// <summary> Starts the road update </summary>
        public void UpdateRoad(RoadUpdateTypeEnum _updateType = RoadUpdateTypeEnum.Full)
        {
            if (!roadSystem.isAllowingRoadUpdates)
            {
                spline.Setup();
                isEditorConstructing = false;
                return;
            }

            if (isEditorConstructing)
            {
                return;
            }

            RootUtils.SetupUniqueIdentifier(ref UID);

            RootUtils.StartProfiling(this, "UpdateRoadPrelim");

            // RM-94: these two clamps used to carry their own numbers -- roadDefinition 1-50 m and laneWidth
            // 0.2-500 m -- while the inspector declared 1-20 m and 2.5-5.0 m and cited AASHTO for the lane band. The
            // inspector warned about a 6 m lane and then built it anyway, and a width assigned from script never saw
            // the warning at all. RoadFieldLimits now owns both bands for the inspector and for here, so there is one
            // answer to "what will this tool accept".
            roadDefinition = RoadFieldLimits.ClampSegmentResolution(roadDefinition);
            laneWidth = RoadFieldLimits.ClampLaneWidth(laneWidth);

            EditorConstructionStartTime = Time.realtimeSinceStartup;
            editorTitleString = "Updating " + transform.name + "...";
            System.GC.Collect();

            if (isSavingTerrainHistoryOnDisk)
            {
                LoadTerrainHistory();
            }

            CheckMats();
            isEditorProgressBar = false;

            isProfiling = true;
            if (isUsingMultithreading)
            {
                isProfiling = false;
            }

            //Set all terrains to height 0:
            Terraforming.CheckAllTerrainsHeight0();

            editorProgress = 20;
            isEditorProgressBar = true;
            if (isEditorConstructing)
            {
                AbortJobs();

                isEditorConstructing = false;
            }

            //In here for intersection patching purposes:
            int nodeCount = spline.GetNodeCount();
            SplineN node = null;
            SplineN node1 = null;
            SplineN node2 = null;


            if (spline.CheckInvalidNodeCount())
            {
                spline.Setup();
                nodeCount = spline.GetNodeCount();
            }


            if (nodeCount > 1)
            {
                // Iterate over every node
                for (int i = 0; i < nodeCount; i++)
                {
                    //try
                    //{
                    node = spline.nodes[i];
                    //}
                    //catch
                    //{
                    //  Editor_bIsConstructing = false;
                    //	EditorUpdateMe = true;
                    //	return;	
                    //}

                    //If node is intersection with an invalid RoadIntersection, mark it as non-intersection. Just-in-case.
                    if (node.isIntersection && node.intersection == null)
                    {
                        node.isIntersection = false;
                        node.intersectionOtherNodeID = -1;
                        node.intersectionOtherNode = null;
                    }


                    //If node is intersection, re-setup:
                    if (node.isIntersection && node.intersection != null)
                    {
                        node1 = node.intersection.node1;
                        node2 = node.intersection.node2;
                        node.intersection.Setup(node1, node2);
                        node.intersection.DeleteRelevantChildren(node, node.spline.road.transform.name);

                        //If primary node on intersection, do more re-setup:
                        if (node.intersection.node1 == node)
                        {
                            node.intersection.lanesAmount = LaneTotal();
                            node.intersection.name = node.intersection.transform.name;
                        }
                        //Setup construction objects:
                        node.intersection.node1.intersectionConstruction = new iConstructionMaker();
                        node.intersection.node2.intersectionConstruction = new iConstructionMaker();
                    }


                    //Store materials and physical materials for road and or shoulder cuts on each node, if necessary:
                    node.StoreCuts();
                }
            }


            name = transform.name;

            spline.RoadWidth = RoadWidth();
            //RootUtils.StartProfiling(this, "SplineSetup");
            spline.Setup();
            //RootUtils.EndProfiling(this);
            nodeCount = spline.GetNodeCount();

            if (spline == null || spline.nodes == null)
            {
                MostRecentNodeCount = 0;
            }
            else
            {
                MostRecentNodeCount = spline.GetNodeCount();
            }


            if (isUsingDefaultMaterials)
            {
                SetDefaultMats();

                if (DetectInvalidDefaultMatsForUndo())
                {
                    SetAllCutsToCurrentMaterials();
                }
            }

            //Hiding in hierarchy:
            for (int i = 0; i < nodeCount; i++)
            {
                node = spline.nodes[i];
                if (node != null)
                {
                    if (node.isIntersection || node.isSpecialEndNode)
                    {
                        node.ToggleHideFlags(true);
                    }
                    else
                    {
                        node.ToggleHideFlags(false);
                    }
                }
            }


            // Delete mainMeshes of this road
            int childCount = transform.childCount;
            GameObject mainMeshes = null;
            for (int i = 0; i < childCount; i++)
            {
                if (transform.GetChild(i).transform.name.ToLower().Contains("mainmeshes"))
                {
                    mainMeshes = transform.GetChild(i).transform.gameObject;
                    Object.DestroyImmediate(mainMeshes);
                }
            }


            if (nodeCount < 2)
            {
                //Delete old objs and return:
                Object.DestroyImmediate(MainMeshes);
                Object.DestroyImmediate(MeshRoad);
                Object.DestroyImmediate(MeshShoR);
                Object.DestroyImmediate(MeshShoL);
                Object.DestroyImmediate(MeshiLanes);
                Object.DestroyImmediate(MeshiLanes0);
                Object.DestroyImmediate(MeshiLanes1);
                Object.DestroyImmediate(MeshiLanes2);
                Object.DestroyImmediate(MeshiLanes3);
                Object.DestroyImmediate(MeshiMainPlates);
                Object.DestroyImmediate(MeshiMarkerPlates);
                RootUtils.EndProfiling(this);
                return;
            }


            spline.HeightHistory = new List<KeyValuePair<float, float>>();

            //Spline-only carrier (rail / ropeway / centreline classes): the spline is fully set
            //up above and its old carriageway meshes were just deleted. Skip the carriageway mesh
            //buffer, road jobs and terrain deformation, but still run ConstructionCleanup so edge
            //objects and splinated meshes (the geometry that actually rides the spline) are built.
            if (!isGeneratingRoadMesh)
            {
                RootUtils.EndProfiling(this);
                ConstructionCleanup();
                return;
            }

            if (roadSystem == null)
            {
                roadSystem = transform.parent.GetComponent<RoadSystem>();
            }
            //Compatibility update.
            if (isUsingMultithreading)
            {
                isEditorConstructing = true;
            }
            else
            {
                isEditorConstructing = false;
            }
            editorConstructionID = 0;


            //Check if road takes place on only 1 terrain:
            Terrain terrain = RoadUtility.GetTerrain(spline.nodes[0].pos);
            bool isSameTerrain = true;
            for (int i = 1; i < nodeCount; i++)
            {
                if (terrain != RoadUtility.GetTerrain(spline.nodes[0].pos))
                {
                    isSameTerrain = false;
                    break;
                }
            }

            RCS = new RoadConstructorBufferMaker(this, _updateType);

            if (isSameTerrain)
            {
                RCS.tTerrain = terrain;
            }
            else
            {
                RCS.tTerrain = null;
            }
            terrain = null;

            RootUtils.EndProfiling(this);
            if (isUsingMultithreading)
            {
                if (RCS.isTerrainOn || TerrainHistory == null)
                {
                    Terraforming.ProcessRoadTerrainHook1(spline, this);
                }
                else
                {
                    ConstructRoad2();
                }
            }
            else
            {
                UpdateRoadNoMultiThreading();
            }
        }


        #region "Terrain history"
        public void StoreTerrainHistory(bool _isDiskOnly = false)
        {
            if (!_isDiskOnly)
            {
                Road road = this;
                RoadUtility.ConstructRoadStoreTerrainHistory(ref road);
            }

            if (isSavingTerrainHistoryOnDisk && TerrainHistory != null && TerrainHistory.Count > 0)
            {
                RootUtils.StartProfiling(this, "TerrainHistory_Save");
                TerrainHistoryUtility.SaveTerrainHistory(TerrainHistory, this);
                RootUtils.EndProfiling(this);
                TerrainHistory.Clear();
                TerrainHistory = null;
            }
            else
            {
                if (TerrainHistory != null && TerrainHistory.Count > 0)
                {
                    int terrainSize = 0;
                    for (int i = 0; i < TerrainHistory.Count; i++)
                    {
                        terrainSize += TerrainHistory[i].GetSize();
                    }
                    TerrainHistoryByteSize = (terrainSize * 0.001f).ToString("n0") + " kb";
                }
                else
                {
                    TerrainHistoryByteSize = "0 bytes";
                }
            }
        }


        public void ResetTerrainHistory()
        {
            Road tRoad = this;
            if (isSavingTerrainHistoryOnDisk && TerrainHistory != null)
            {
                TerrainHistoryUtility.DeleteTerrainHistory(this);
            }
            else
            {
                RoadUtility.ResetTerrainHistory(ref tRoad);
            }
        }


        /// <summary> Loads terrain history from disk </summary>
        public void LoadTerrainHistory(bool _isForced = false)
        {
            if (isSavingTerrainHistoryOnDisk || _isForced)
            {
                if (TerrainHistory != null)
                {
                    TerrainHistory.Clear();
                    TerrainHistory = null;
                }
                TerrainHistory = TerrainHistoryUtility.LoadTerrainHistory(this);
            }
            if (_isForced)
            {
                TerrainHistoryUtility.DeleteTerrainHistory(this);
            }
        }
        #endregion


        #region "Construction process"
        #region "No multithread"
        private void UpdateRoadNoMultiThreading()
        {
            if (isHeightModificationEnabled || isDetailModificationEnabled || isTreeModificationEnabled)
            {
                RootUtils.StartProfiling(this, "RoadCon_Terrain");
                if (RCS.isTerrainOn || TerrainHistory == null)
                {
                    Terraforming.ProcessRoadTerrainHook1(spline, this, false);
                    Terraforming.ProcessRoadTerrainHook2(spline, ref EditorTTDList);
                    //Store history.
                    StoreTerrainHistory();
                    int editorTTDListCount = EditorTTDList.Count;
                    for (int i = 0; i < editorTTDListCount; i++)
                    {
                        EditorTTDList[i] = null;
                    }
                    EditorTTDList = null;
                    System.GC.Collect();
                }
                RootUtils.EndProfiling(this);
            }

            editorProgress = 50;
            Road road = this;
            RootUtils.StartProfiling(this, "RoadCon_RoadPrelim");

            editorProgress = 80;
            Threading.RoadCreationT.RoadJobPrelim(ref road);
            RootUtils.EndStartProfiling(this, "RoadCon_Road1");
            editorProgress = 90;
            Threading.RoadCreationT.RoadJob1(ref RCS);
            RootUtils.EndStartProfiling(this, "MeshSetup1");
            editorProgress = 92;
            RCS.MeshSetup1();
            RootUtils.EndStartProfiling(this, "RoadCon_Road2");
            editorProgress = 94;
            Threading.RoadCreationT.RoadJob2(ref RCS);
            RootUtils.EndStartProfiling(this, "MeshSetup2");
            editorProgress = 96;
            RCS.MeshSetup2();
            RootUtils.EndProfiling(this);
            ConstructionCleanup();
        }
        #endregion


        /// <summary> Stores terrain history and nulls TerrainCalcsJob</summary>
        private void ConstructRoad2()
        {
            editorProgress = 40;
            if (RCS.isTerrainOn)
            {
                //Store history:
                Terraforming.ProcessRoadTerrainHook2(spline, ref EditorTTDList);
                StoreTerrainHistory();

                EditorTTDList.Clear();
                EditorTTDList = null;
                System.GC.Collect();
            }
            editorProgress = 60;

            if (TerrainCalcsJob != null)
            {
                TerrainCalcsJob.Abort();
                TerrainCalcsJob = null;
            }
            Road road = this;
            editorProgress = 72;
            RoadCalcsJob1 = new Threading.RoadCalcs1();
            RoadCalcsJob1.Setup(ref RCS, ref road);
            RoadCalcsJob1.Start();
        }


        private void ConstructRoad3()
        {
            editorProgress = 84;
            RCS.MeshSetup1();
            editorProgress = 96;
            if (RoadCalcsJob1 != null)
            {
                RoadCalcsJob1.Abort();
                RoadCalcsJob1 = null;
            }
            RoadCalcsJob2 = new Threading.RoadCalcs2();
            RoadCalcsJob2.Setup(ref RCS);
            RoadCalcsJob2.Start();
            editorProgress = 98;
        }


        private void ConstructRoad4()
        {
            RCS.MeshSetup2();
            ConstructionCleanup();
        }
        #endregion


        private void AbortJobs()
        {
            if (TerrainCalcsJob != null)
            {
                TerrainCalcsJob.Abort();
                TerrainCalcsJob = null;
            }
            if (RoadCalcsJob1 != null)
            {
                RoadCalcsJob1.Abort();
                RoadCalcsJob1 = null;
            }
            if (RoadCalcsJob2 != null)
            {
                RoadCalcsJob2.Abort();
                RoadCalcsJob2 = null;
            }
        }


        private void ConstructionCleanup()
        {
            FixDisplay();
            AbortJobs();

            isEditorConstructing = false;
            int nodeCount = spline.GetNodeCount();
            SplineN node;
            for (int i = 0; i < nodeCount; i++)
            {
                node = spline.nodes[i];
                if (node.isIntersection)
                {
                    if (node.intersectionConstruction != null)
                    {
                        node.intersectionConstruction.Nullify();
                        node.intersectionConstruction = null;
                    }
                }
                node.SetupSplinationLimits();
                node.SetupEdgeObjects(false);
                node.SetupSplinatedMeshes(false);
            }
            if (spline.HeightHistory != null)
            {
                spline.HeightHistory.Clear();
                spline.HeightHistory = null;
            }
            if (RCS != null)
            {
                RCS.Nullify();
                RCS = null;
            }

            if (isSavingMeshes)
            {
                EngineIntegration.SaveAssets();
            }

            isEditorProgressBar = false;

            //Make sure terrain history out of memory if necessary (redudant but keep):
            if (isSavingTerrainHistoryOnDisk && TerrainHistory != null)
            {
                TerrainHistory.Clear();
                TerrainHistory = null;
            }

            //Collect:
            isTriggeringGC = true;

            if (tRoadMaterialDropdownOLD != roadMaterialDropdown)
            {
                tRoadMaterialDropdownOLD = roadMaterialDropdown;
                SetAllCutsToCurrentMaterials();
            }

            if (PiggyBacks != null && PiggyBacks.Length > 0)
            {
                for (int i = 0; i < PiggyBacks.Length; i++)
                {
                    if (PiggyBacks[i] == null)
                    {
                        PiggyBacks = null;
                        break;
                    }
                }

                if (PiggyBacks != null)
                {
                    SplineC tPiggy = PiggyBacks[0];
                    SplineC[] NewPiggys = null;

                    PiggyBacks[0] = null;
                    if (PiggyBacks.Length > 1)
                    {
                        NewPiggys = new SplineC[PiggyBacks.Length - 1];
                        for (int i = 1; i < PiggyBacks.Length; i++)
                        {
                            NewPiggys[i - 1] = PiggyBacks[i];
                        }
                    }

                    if (NewPiggys != null)
                    {
                        tPiggy.road.PiggyBacks = NewPiggys;
                    }
                    NewPiggys = null;
                    tPiggy.TriggerSetup();
                }
            }
        }


        public void SetEditorTerrainCalcs(ref List<Terraforming.TempTerrainData> _tddList)
        {
            EditorTTDList = _tddList;
        }
        #endregion


        #region "Gizmos"
        private void OnDrawGizmosSelected()
        {
            if (isEditorMouseHittingTerrain)
            {
                Gizmos.color = newNodePreviewColor;
                Gizmos.DrawCube(editorMousePos, new Vector3(10f, 4f, 10f));
            }
        }
        #endregion


        /// <summary> Returns width of road: the running lanes only, the meaning this has always had. Shoulders and any
        /// median are outside it — <see cref="CrossSectionWidth"/> is the whole section. </summary>
        public float RoadWidth()
        {
            return (laneWidth * (float)LaneTotal());
        }


        /// <summary> Splits a pre-RM-89 lane TOTAL into per-direction counts exactly once, and does nothing on a road
        /// that has already been split. Called from Awake/OnEnable and lazily from every lane accessor, so the model is
        /// correct no matter which runs first. </summary>
        public void EnsureLaneModel()
        {
            if (!LaneModel.NeedsSplit(lanesBackward)) { return; }
            int f, b;
            LaneModel.SplitTotal(lanesForward, isOneWay, out f, out b);
            lanesForward = f;
            lanesBackward = b;
        }


        /// <summary> Total lanes across both directions — what the removed <c>laneAmount</c> field meant, so every
        /// caller that compared a lane count still reads the same number. </summary>
        public int LaneTotal()
        {
            EnsureLaneModel();
            return LaneModel.Total(lanesForward, lanesBackward);
        }


        /// <summary> Sets the lane model from a directionless total, splitting it by the current one-way setting. This
        /// is the write path that replaces assigning the old <c>laneAmount</c>. </summary>
        public void SetLaneTotal(int total)
        {
            int f, b;
            LaneModel.SplitTotal(total, isOneWay, out f, out b);
            lanesForward = f;
            lanesBackward = b;
        }


        /// <summary> True when the centre divider is a physical object occupying section width, rather than paint.
        /// Markers are painted lines: they separate the directions without widening the section. The rule itself is
        /// <c>City.RoadTypeChange.IsPhysicalDivider</c>, so the section builder and a type change cannot disagree about
        /// which dividers are things. </summary>
        public bool HasPhysicalMedian()
        {
            return City.RoadTypeChange.IsPhysicalDivider(centerDivider);
        }


        /// <summary> The median width that actually enters the cross-section: zero unless there are opposing lanes to
        /// separate, the divider is physical, and a width was authored. </summary>
        public float MedianWidthEffective()
        {
            EnsureLaneModel();
            return (float)LaneModel.MedianWidth(lanesBackward, HasPhysicalMedian(), medianWidth);
        }


        /// <summary> The whole cross-section width in metres: lanes + median + a shoulder each side. </summary>
        public float CrossSectionWidth()
        {
            EnsureLaneModel();
            return (float)LaneModel.CrossSectionWidth(lanesForward, lanesBackward, laneWidth,
                isShouldersEnabled, shoulderWidth, MedianWidthEffective());
        }


        /// <summary> The cross-section laid out left to right as contiguous bands measured from the spline centre
        /// line: left shoulder, backward lanes, median, forward lanes, right shoulder. </summary>
        public List<LaneBand> LaneBands()
        {
            EnsureLaneModel();
            return LaneModel.Bands(lanesForward, lanesBackward, laneWidth,
                isShouldersEnabled, shoulderWidth, MedianWidthEffective());
        }


        /// <summary> The cross-section at a distance along the road, which is NOT always the same section: any
        /// auxiliary lane authored in <see cref="auxLanes"/> opens and closes over its own taper, so the road may be
        /// two lanes here and three lanes 60 m further on. The reported edges are what the section actually spans,
        /// because an open auxiliary lane grows the road on one side only and the through lanes deliberately do not
        /// move. Pass a distance measured along the spline in metres. </summary>
        public List<LaneBand> LaneBandsAt(float distanceAlongMetres, out float leftEdge, out float rightEdge)
        {
            EnsureLaneModel();
            double left, right;
            List<LaneBand> bands = LaneModel.BandsAt(distanceAlongMetres,
                AuxiliaryLaneAuthored.ToModel(auxLanes), lanesForward, lanesBackward, laneWidth,
                isShouldersEnabled, shoulderWidth, MedianWidthEffective(), out left, out right);
            leftEdge = (float)left;
            rightEdge = (float)right;
            return bands;
        }


        /// <summary> The whole section width at a distance along the road, including any auxiliary lane open there. </summary>
        public float CrossSectionWidthAt(float distanceAlongMetres)
        {
            float left, right;
            LaneBandsAt(distanceAlongMetres, out left, out right);
            return right - left;
        }


        /// <summary> The speed a taper length has to be computed for. The authored limit is the design speed in
        /// practice; when it has not been set the road class's own figure is used, because a taper length of zero would
        /// open a lane instantaneously and no road does that. </summary>
        public float DesignSpeedKPH()
        {
            if (speedLimitKPH > 0f) { return speedLimitKPH; }
            return (float)RoadClassDefaults.For(roadClass).DesignSpeedKPH;
        }


        /// <summary> Authors a permanent widening: the road gains one lane in the given direction starting at a
        /// distance along the spline, tapering in over the length the design speed requires rather than appearing at
        /// once. Adding one on each side is how a two-lane road becomes four-lane part way along. The lane count
        /// fields stay as they are -- they describe the road before the widening -- and the extra lane is carried by
        /// the auxiliary list, which is what lets the count differ from one end of the road to the other. </summary>
        public void AddWidening(int direction, float startMetres)
        {
            if (auxLanes == null) { auxLanes = new List<AuxiliaryLaneAuthored>(); }
            auxLanes.Add(AuxiliaryLaneAuthored.From(
                AuxiliaryLane.Widening(direction, startMetres, laneWidth, DesignSpeedKPH())));
        }


        /// <summary> Authors a turn bay ending at a stop line: the surplus lanes of a wider approach where it meets a
        /// narrower road become turning capacity instead of forcing either road to change width. </summary>
        public void AddTurnBay(int direction, float stopLineMetres, float storageMetres)
        {
            if (auxLanes == null) { auxLanes = new List<AuxiliaryLaneAuthored>(); }
            auxLanes.Add(AuxiliaryLaneAuthored.From(
                AuxiliaryLane.TurnBay(direction, stopLineMetres, storageMetres, laneWidth, DesignSpeedKPH())));
        }


        /// <summary>
        /// Applies real-world defaults for the current roadClass: lane count, lane width in
        /// metres, shoulders, one-way vs opposing split, centre divider, design speed, max grade
        /// and whether a carriageway mesh is generated at all. Call after changing roadClass;
        /// the caller then runs UpdateRoad to rebuild. Widths follow common design guidance
        /// (urban lane ~3.0-3.5 m, highway ~3.65 m, motorway ~3.75 m).
        /// </summary>
        public void ApplyRoadClassDefaults()
        {
            ApplyRoadClassDefaults(false);
        }

        /// <summary> Applies the current roadClass's real-world defaults, read from the single cited source
        /// <see cref="RoadClassDefaults"/> so the applied and audited numbers can never drift. When
        /// <paramref name="preserveOverrides"/> is true, a field the author changed away from the OUTGOING class's
        /// default is kept; only fields still at that default are refreshed. Call after changing roadClass; the caller
        /// then runs UpdateRoad to rebuild. </summary>
        public void ApplyRoadClassDefaults(bool preserveOverrides)
        {
            // RM-103. The merge rule itself lives in City.RoadTypeChange.Merge, and this reads its own fields into a
            // state, merges, and writes the result back. It is deliberately not a second copy of the rule: a pure copy
            // for the gates beside a live copy for authors is how the two come to disagree, and then only one of them
            // is the road anybody actually gets.
            ApplyTypeState(City.RoadTypeChange.Merge(TypeState(), roadClass, appliedRoadClass, preserveOverrides));
            appliedRoadClass = roadClass;
        }


        /// <summary> RM-103. Everything this road's TYPE decides, read off the road as ONE value: lane counts and their
        /// direction split, lane and shoulder width, the centre divider and its median, design speed, grade limit,
        /// whether a carriageway mesh is generated at all, and the surface. The lane model is settled first, so a road
        /// still carrying a pre-RM-89 lane total is read as the split counts it means. </summary>
        public City.RoadTypeState TypeState()
        {
            EnsureLaneModel();
            return new City.RoadTypeState(roadClass, lanesForward, lanesBackward, isOneWay, laneWidth,
                isShouldersEnabled, shoulderWidth, centerDivider, medianWidth, speedLimitKPH, isMaxGradeEnabled,
                maxGrade, isGeneratingRoadMesh, surfaceType);
        }


        /// <summary> RM-103. Writes a type state back onto this road. Every member is written, none is skipped: a writer
        /// that left one field alone would make the state a description of the road in one direction and a lie in the
        /// other, and the round-trip proof would pass while an author's road quietly changed. This does NOT rebuild —
        /// <see cref="RequestUpdate"/> does, once, when the caller is done. </summary>
        public void ApplyTypeState(City.RoadTypeState state)
        {
            roadClass = state.roadClass;
            lanesForward = state.lanesForward;
            lanesBackward = state.lanesBackward;
            isOneWay = state.isOneWay;
            laneWidth = state.laneWidth;
            isShouldersEnabled = state.isShouldersEnabled;
            shoulderWidth = state.shoulderWidth;
            centerDivider = state.centerDivider;
            medianWidth = state.medianWidth;
            speedLimitKPH = state.speedLimitKPH;
            isMaxGradeEnabled = state.isMaxGradeEnabled;
            maxGrade = state.maxGrade;
            isGeneratingRoadMesh = state.isGeneratingRoadMesh;
            surfaceType = state.surface;
        }


        /// <summary> RM-103. Change what this road IS — its class, its surface, or both — on a road that already exists,
        /// and rebuild ONCE. Pass null for <paramref name="newSurface"/> to leave the surface as it is.
        ///
        /// The returned rows are what the author had overridden and what the incoming class asks for instead, computed
        /// BEFORE the merge because afterwards the road no longer remembers what it was. Nothing in that report is
        /// applied or clamped; the author's numbers survive the change, which is the whole reason the report exists.
        ///
        /// One <see cref="RequestUpdate"/>, at the end: class and surface both feed the same rebuild, so changing both
        /// at once costs one build, and RM-101's supersede drops any earlier build of this road still queued. Meshes
        /// belonging to the previous type are deleted by that rebuild before it decides whether to make new ones, so a
        /// road turned into a mesh-less class (rail, ropeway, centreline) does not keep its old carriageway. </summary>
        public List<City.TypeOverride> ChangeType(RoadClassEnum newClass, City.SurfaceTypeClass newSurface,
            bool preserveOverrides = true)
        {
            List<City.TypeOverride> report = City.RoadTypeChange.Overrides(TypeState(), appliedRoadClass, newClass);
            if (newSurface != null) { surfaceType = newSurface; }
            roadClass = newClass;
            ApplyRoadClassDefaults(preserveOverrides);
            RequestUpdate();
            return report;
        }


        /// <summary> The real installed footprint in metres of the current centre divider — the minimum width the
        /// cross-section must reserve for it. The figure and the reasoning behind it belong to
        /// <c>City.RoadTypeChange.DividerFootprintMetres</c>, which is the only place 0.61 m is written down. </summary>
        public float DividerFootprintMetres()
        {
            return City.RoadTypeChange.DividerFootprintMetres(centerDivider);
        }


        /// <summary> Real-world lanes per direction implied by the current lane model. One-way roads run every lane
        /// one direction; otherwise this is the forward count, which on an odd total carries the extra
        /// climbing/passing lane. </summary>
        public int LanesPerDirection()
        {
            EnsureLaneModel();
            return Mathf.Max(1, lanesForward);
        }


        #if UNITY_EDITOR
        public float EditorCameraTimer = 0f;
        #endif


        /// <summary> RM-100. Pump a staged build from the ordinary frame loop, so a road rebuilt at runtime finishes.
        ///
        /// The editor pumps the same <see cref="AdvanceBuild"/> from <c>EditorApplication.update</c>, which fires while
        /// the editor is idle and is why an edit-mode build progresses without a play session. Both callers exist
        /// because neither covers the other: <c>EditorApplication.update</c> does not run in a player, and
        /// <c>MonoBehaviour.Update</c> does not run in edit mode. Neither pumps a build the dispatcher owns -- see
        /// <see cref="isDispatched"/>. </summary>
        private void Update()
        {
            if (!isDispatched) { AdvanceBuild(); }

            #if UNITY_EDITOR
            if (Application.isEditor && isEditorCameraMoving)
            {
                EditorCameraTimer += Time.deltaTime;
                if (EditorCameraTimer > EditorCameraTimeUpdateInterval)
                {
                    EditorCameraTimer = 0f;
                    DoEditorCameraLoop();
                }
            }
            #endif
        }


        #region "Default materials retrieval"
        public bool DetectInvalidDefaultMatsForUndo()
        {
            string lowerName = "";
            int counter = 0;
            if (!MeshRoad)
            {
                return false;
            }

            string basePath = RoadEditorUtility.GetBasePath();

            MeshRenderer[] MRs = MeshRoad.GetComponentsInChildren<MeshRenderer>();
            Material tMat2Lanes = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble.mat");
            Material tMat4Lanes = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-4L.mat");
            Material tMat6Lanes = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-6L.mat");
            foreach (MeshRenderer MR in MRs)
            {
                lowerName = MR.transform.name.ToLower();
                if (lowerName.Contains("marker"))
                {
                    if (LaneTotal() == 2)
                    {
                        if (MR.sharedMaterials[0] == tMat4Lanes)
                        {
                            counter += 1;
                        }
                        else if (MR.sharedMaterials[0] == tMat6Lanes)
                        {
                            counter += 1;
                        }
                    }
                    else if (LaneTotal() == 4)
                    {
                        if (MR.sharedMaterials[0] == tMat2Lanes)
                        {
                            counter += 1;
                        }
                        else if (MR.sharedMaterials[0] == tMat6Lanes)
                        {
                            counter += 1;
                        }
                    }
                    else if (LaneTotal() == 6)
                    {
                        if (MR.sharedMaterials[0] == tMat2Lanes)
                        {
                            counter += 1;
                        }
                        else if (MR.sharedMaterials[0] == tMat4Lanes)
                        {
                            counter += 1;
                        }
                    }
                }
                if (counter > 1)
                {
                    return true;
                }
            }
            return false;
        }


        /// <summary> Assigns materials on all mesh renderers </summary>
        public void SetAllCutsToCurrentMaterials()
        {
            if (!MeshRoad)
            {
                return;
            }

            MeshRenderer[] MRs = MeshRoad.GetComponentsInChildren<MeshRenderer>();
            Material[] roadWorldMats = GetRoadWorldMaterials();
            Material[] roadMarkerMats = GetRoadMarkerMaterials();
            SetCutMaterials(MRs, roadWorldMats, roadMarkerMats);

            if (isShouldersEnabled)
            {
                roadWorldMats = GetShoulderWorldMaterials();
                roadMarkerMats = GetShoulderMarkerMaterials();
                if (MeshShoL != null)
                {
                    MRs = MeshShoL.GetComponentsInChildren<MeshRenderer>();
                    SetCutMaterials(MRs, roadWorldMats, roadMarkerMats);
                }
                if (MeshShoR != null)
                {
                    MRs = MeshShoR.GetComponentsInChildren<MeshRenderer>();
                    SetCutMaterials(MRs, roadWorldMats, roadMarkerMats);
                }
            }
        }


        /// <summary> Sets materials of each MR in _MRs </summary>
        private void SetCutMaterials(MeshRenderer[] _MRs, Material[] _roadWorldMats, Material[] _roadMarkerMats)
        {
            string lowerName;
            foreach (MeshRenderer MR in _MRs)
            {
                lowerName = MR.transform.name.ToLower();
                if (lowerName.Contains("marker"))
                {
                    if (_roadMarkerMats != null)
                    {
                        MR.sharedMaterials = _roadMarkerMats;
                    }
                }
                else if (lowerName.Contains("cut"))
                {
                    if (_roadWorldMats != null)
                    {
                        MR.sharedMaterials = _roadWorldMats;
                    }
                }
            }
        }


        public Material[] GetRoadWorldMaterials()
        {
            List<Material> roadMaterials = new List<Material>();
            if (RoadMaterial1 != null)
            {
                roadMaterials.Add(RoadMaterial1);
                if (RoadMaterial2 != null)
                {
                    roadMaterials.Add(RoadMaterial2);
                    if (RoadMaterial3 != null)
                    {
                        roadMaterials.Add(RoadMaterial3);
                        if (RoadMaterial4 != null)
                        {
                            roadMaterials.Add(RoadMaterial4);
                        }
                    }
                }
            }

            return roadMaterials.ToArray();
        }


        public Material[] GetRoadMarkerMaterials()
        {
            List<Material> roadMarkerMaterials = new List<Material>();
            if (RoadMaterialMarker1 != null)
            {
                roadMarkerMaterials.Add(RoadMaterialMarker1);
                if (RoadMaterialMarker2 != null)
                {
                    roadMarkerMaterials.Add(RoadMaterialMarker2);
                    if (RoadMaterialMarker3 != null)
                    {
                        roadMarkerMaterials.Add(RoadMaterialMarker3);
                        if (RoadMaterialMarker4 != null)
                        {
                            roadMarkerMaterials.Add(RoadMaterialMarker4);
                        }
                    }
                }
            }

            return roadMarkerMaterials.ToArray();
        }


        public Material[] GetShoulderWorldMaterials()
        {
            if (!isShouldersEnabled)
            {
                return null;
            }

            List<Material> shoulderMaterials = new List<Material>();
            if (ShoulderMaterial1 != null)
            {
                shoulderMaterials.Add(ShoulderMaterial1);
                if (ShoulderMaterial2 != null)
                {
                    shoulderMaterials.Add(ShoulderMaterial2);
                    if (ShoulderMaterial3 != null)
                    {
                        shoulderMaterials.Add(ShoulderMaterial3);
                        if (ShoulderMaterial4 != null)
                        {
                            shoulderMaterials.Add(ShoulderMaterial4);
                        }
                    }
                }
            }

            return shoulderMaterials.ToArray();
        }


        public Material[] GetShoulderMarkerMaterials()
        {
            if (!isShouldersEnabled)
            {
                return null;
            }

            List<Material> shoulderMarkerMaterials = new List<Material>();
            if (ShoulderMaterialMarker1 != null)
            {
                shoulderMarkerMaterials.Add(ShoulderMaterialMarker1);
                if (ShoulderMaterialMarker2 != null)
                {
                    shoulderMarkerMaterials.Add(ShoulderMaterialMarker2);
                    if (ShoulderMaterialMarker3 != null)
                    {
                        shoulderMarkerMaterials.Add(ShoulderMaterialMarker3);
                        if (ShoulderMaterialMarker4 != null)
                        {
                            shoulderMarkerMaterials.Add(ShoulderMaterialMarker4);
                        }
                    }
                }
            }

            return shoulderMarkerMaterials.ToArray();
        }
        #endregion


        #region "Materials"
        /// <summary> Loads the standard materials if the road uses default materials </summary>
        private void CheckMats()
        {
            if (!isUsingDefaultMaterials)
            {
                return;
            }

            string basePath = RoadEditorUtility.GetBasePath();

            if (!RoadMaterial1)
            {
                RoadMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Road1.mat");
                RoadMaterial2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");
            }

            if (!RoadMaterialMarker1)
            {
                if (LaneTotal() == 2)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks.mat");
                }
                else if (LaneTotal() == 4)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-4L.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks-4L.mat");
                }
                else if (LaneTotal() == 6)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-6L.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks-6L.mat");
                }
                else
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks.mat");
                }
            }

            // Can be simplified
            if (isShouldersEnabled && !ShoulderMaterial1)
            {
                ShoulderMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Shoulder1.mat");
                ShoulderMaterial2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");
            }

            if (isShouldersEnabled && !RoadPhysicMaterial)
            {
                RoadPhysicMaterial = RoadEditorUtility.LoadPhysicsMaterial(basePath + "/Physics/Pavement.physicMaterial");
            }
            if (isShouldersEnabled && !ShoulderPhysicMaterial)
            {
                ShoulderPhysicMaterial = RoadEditorUtility.LoadPhysicsMaterial(basePath + "/Physics/Dirt.physicMaterial");
            }
        }


        public void SetDefaultMats()
        {
            string basePath = RoadEditorUtility.GetBasePath();

            //Held before the reset below, because the lane-tiled copy this road may be carrying is reachable through
            //this one field and nothing else. The sweep at the end of the method decides whether it can be destroyed.
            Material materialsOnEntry = RoadMaterialMarker1;

            // Reset materials
            RoadMaterial1 = null;
            RoadMaterial2 = null;
            RoadMaterial3 = null;
            RoadMaterial4 = null;
            RoadMaterialMarker2 = null;
            RoadMaterialMarker3 = null;
            RoadMaterialMarker4 = null;


            if (roadMaterialDropdown == RoadMaterialDropdownEnum.Asphalt)
            {
                RoadMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Road1.mat");
                RoadMaterial2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");

                if (LaneTotal() == 2)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks.mat");
                }
                else if (LaneTotal() == 4)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-4L.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks-4L.mat");
                }
                else if (LaneTotal() == 6)
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble-6L.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks-6L.mat");
                }
                else
                {
                    RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/WhiteYellowDouble.mat");
                    RoadMaterialMarker2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/TireMarks.mat");
                }


                ShoulderMaterial1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Shoulder1.mat");
                ShoulderMaterial2 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/Markers/RoadDetailOverlay1.mat");

                RoadPhysicMaterial = RoadEditorUtility.LoadPhysicsMaterial(basePath + "/Physics/Pavement.physicMaterial");
                ShoulderPhysicMaterial = RoadEditorUtility.LoadPhysicsMaterial(basePath + "/Physics/Dirt.physicMaterial");
            }
            else if (roadMaterialDropdown == RoadMaterialDropdownEnum.Dirt)
            {
                RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/DirtRoad.mat");
                RoadMaterial1 = RoadMaterialMarker1;
            }
            else if (roadMaterialDropdown == RoadMaterialDropdownEnum.Brick)
            {
                RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/BrickRoad.mat");
                RoadMaterial1 = RoadMaterialMarker1;
            }
            else if (roadMaterialDropdown == RoadMaterialDropdownEnum.Cobblestone)
            {
                RoadMaterialMarker1 = RoadEditorUtility.LoadMaterial(basePath + "/Materials/CobblestoneRoad.mat");
                RoadMaterial1 = RoadMaterialMarker1;
            }


            if (roadMaterialDropdown == RoadMaterialDropdownEnum.Brick
            || roadMaterialDropdown == RoadMaterialDropdownEnum.Cobblestone
            || roadMaterialDropdown == RoadMaterialDropdownEnum.Dirt)
            {
                if (LaneTotal() > 2)
                {
                    //The tiling has to follow the lane count, and that cannot be written onto the shared ASSET loaded
                    //above -- it would re-tile every brick, cobblestone and dirt road in the project and dirty the
                    //.mat on disk. So a copy is made, and the copy is owned rather than abandoned: see the sweep
                    //below, which destroys the one this road was carrying before this call replaced it.
                    RoadMaterialMarker1 = new Material(RoadMaterialMarker1);
                    RoadMaterialMarker1.mainTextureScale = Vector2.Scale(RoadMaterialMarker1.mainTextureScale, new Vector2(LaneTotal() / 2, 1f));
                }
            }

            //Every branch above overwrites RoadMaterialMarker1, and the reset at the top of this method deliberately
            //leaves that one field alone, so the value captured on entry is whatever the previous call left behind. If
            //that was a lane-tiled copy this road minted -- an in-memory Material, not a file -- nothing points at it
            //any more and it would sit in memory for the rest of the session, one more orphan for every re-apply of
            //the material dropdown. It is only destroyed once it is certain to be unreferenced: an asset is never
            //touched, because destroying one deletes it from the project.
            if (materialsOnEntry != null
                && materialsOnEntry != RoadMaterialMarker1
                && materialsOnEntry != RoadMaterial1
                && materialsOnEntry != RoadMaterial2
                && materialsOnEntry != RoadMaterial3
                && materialsOnEntry != RoadMaterial4
                && materialsOnEntry != RoadMaterialMarker2
                && materialsOnEntry != RoadMaterialMarker3
                && materialsOnEntry != RoadMaterialMarker4
                && !EngineIntegration.IsPersistentAsset(materialsOnEntry))
            {
                Object.DestroyImmediate(materialsOnEntry);
            }


            int nodeCount = spline.GetNodeCount();
            for (int i = 0; i < nodeCount; i++)
            {
                if (spline.nodes[i] && spline.nodes[i].isIntersection && spline.nodes[i].intersection != null && spline.nodes[i].intersection.isUsingDefaultMaterials)
                {
                    spline.nodes[i].intersection.ResetMaterialsAll();
                }
            }
        }
        #endregion


        /// <summary> Toggles render state of every mesh of this road </summary>
        public void ToggleWireframes()
        {
            ToggleWireframesHelper(transform);

            if (spline != null)
            {
                ToggleWireframesHelper(spline.transform);
            }
        }


        /// <summary>  Toggles render state of each mesh in _MRs through isGizmosEnabled </summary>
        private void ToggleWireframesHelper(Transform _transform)
        {
            MeshRenderer[] _MRs = _transform.GetComponentsInChildren<MeshRenderer>();
            int meshRenderersCount = _MRs.Length;
            for (int i = 0; i < meshRenderersCount; i++)
            {
                EngineIntegration.SetSelectedRenderState(_MRs[i], isGizmosEnabled);
            }
        }


        /// <summary> Sets localPosition for meshes up to prevent z fighting </summary>
        private void FixDisplay()
        {
            FixDisplayMobile();
        }


        /// <summary> Sets localPosition for meshes up to prevent z fighting </summary>
        private void FixDisplayMobile()
        {
            //This road:
            Object[] markerObjs = transform.GetComponentsInChildren<MeshRenderer>();
            Vector3 vector;
            foreach (MeshRenderer MR in markerObjs)
            {
                if (MR.transform.name.Contains("Marker"))
                {
                    vector = new Vector3(0f, 0.02f, 0f);
                    MR.transform.localPosition = vector;
                }
                else if (MR.transform.name.Contains("SCut") || MR.transform.name.Contains("RoadCut")
                    || MR.transform.name.Contains("ShoulderR")
                    || MR.transform.name.Contains("ShoulderL"))
                {
                    vector = MR.transform.position;
                    vector.y += 0.01f;
                    MR.transform.position = vector;
                }
                else if (MR.transform.name.Contains("RoadMesh"))
                {
                    vector = MR.transform.position;
                    vector.y += 0.02f;
                    MR.transform.position = vector;
                }
                else if (MR.transform.name.Contains("Pavement"))
                {
                    vector = MR.transform.position;
                    vector.y -= 0.01f;
                    MR.transform.position = vector;
                }
            }


            //Intersections (all):
            markerObjs = roadSystem.GetComponentsInChildren<MeshRenderer>();
            foreach (MeshRenderer MR in markerObjs)
            {
                if (MR.transform.name.Contains("CenterMarkers"))
                {
                    vector = new Vector3(0f, 0.02f, 0f);
                    MR.transform.localPosition = vector;
                }
                else if (MR.transform.name.Contains("-Inter") && MR.transform.name.Contains("-Lane"))
                {
                    vector = new Vector3(0f, 0.02f, 0f);
                    MR.transform.localPosition = vector;
                }
                else if (MR.transform.name.Contains("-Inter") && MR.transform.name.Contains("-Stretch"))
                {
                    vector = new Vector3(0f, 0.03f, 0f);
                    MR.transform.localPosition = vector;
                }
                else if (MR.transform.name.Contains("-Inter") && MR.transform.name.Contains("-Tiled"))
                {
                    vector = new Vector3(0f, 0.01f, 0f);
                    MR.transform.localPosition = vector;
                }
            }
        }


        /// <summary> Sets localPosition up by 0.01 </summary>
        private void FixDisplayWin()
        {
            //This road:
            Object[] markerObjs = transform.GetComponentsInChildren<MeshRenderer>();
            Vector3 vector;
            foreach (MeshRenderer MR in markerObjs)
            {
                if (MR.transform.name.Contains("Marker"))
                {
                    vector = new Vector3(0f, 0.01f, 0f);
                    MR.transform.localPosition = vector;
                }
            }


            //Intersections (all):
            markerObjs = EngineIntegration.FindObjectsByType<MeshRenderer>();
            foreach (MeshRenderer MR in markerObjs)
            {
                if (MR.transform.name.Contains("-Inter") && MR.transform.name.Contains("-Lane"))
                {
                    vector = new Vector3(0f, 0.01f, 0f);
                    MR.transform.localPosition = vector;
                }
                else if (MR.transform.name.Contains("-Inter") && MR.transform.name.Contains("-Stretch"))
                {
                    vector = new Vector3(0f, 0.01f, 0f);
                    MR.transform.localPosition = vector;
                }
            }
        }
    }
}
