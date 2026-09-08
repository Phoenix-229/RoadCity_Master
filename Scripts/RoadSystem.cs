// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt
// Modified for Road & City Master; modifications (c) 2026 <rights holder>.
// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.
#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-109. <c>[ExecuteAlways]</c> because everything this component does is authoring work that happens
    /// outside play mode: adding a road, refreshing every road, finding the single scene camera. Unity does not deliver
    /// <c>Awake</c>, <c>OnEnable</c>, <c>Start</c> or <c>Update</c> to a MonoBehaviour in edit mode without it, so
    /// those paths were assuming messages they were never sent. </summary>
    [ExecuteAlways]
    public class RoadSystem : MonoBehaviour
    {
        #region "Vars"
        public bool isMultithreaded = true;
        public bool isSavingMeshes = false;
        public bool isAllowingRoadUpdates = true;

        public Camera editorPlayCamera = null;
        #endregion


        #region "Pumping the build queue"
        /// <summary> RM-100. Advance the queued builds. One owner rather than one per road: forty roads each pumping the
        /// shared queue would perform forty transitions a frame, and the queue exists precisely so that a long list of
        /// roads is spread across frames instead of collapsing back into one blocking pass.
        ///
        /// RM-109 narrowed this to play mode, and that narrowing is the point. The queue is static; a driver that lives
        /// on a scene component can be stopped by that component being absent, disabled, loaded late, or not receiving
        /// the edit-mode message it relies on -- and a stopped driver does not error, it just leaves every build sitting
        /// at the head of the queue with a live progress row that never finishes. In the editor the owner is now
        /// <c>BuildPumpDriver</c>, which is static like the queue and runs from a domain load rather than from a scene.
        /// This <c>Update</c> is the owner in a player, where there is no such driver and no editor frame to hang one
        /// off. <c>Application.isPlaying</c> is always true in a player, so the guard costs a player nothing and keeps
        /// play-in-editor from being pumped twice a frame -- which would otherwise make a build progress at one rate in
        /// the editor and half that rate in a build. </summary>
        private void Update()
        {
            if (!Application.isPlaying) { return; }
            City.BuildDispatch.Shared.Pump();
        }
        #endregion


        /// <summary> Adds a new road to this RoadSystem </summary>
        public GameObject AddRoad(bool _isForceSelected = false)
        {
            Road[] roads = GetComponentsInChildren<Road>();
            int newRoadNumber = (roads.Length + 1);

            //Road:
            GameObject roadObj = new GameObject("Road" + newRoadNumber.ToString());

            EngineIntegration.RegisterUndo(roadObj, "Created road");

            roadObj.transform.parent = transform;
            Road road = roadObj.AddComponent<Road>();

            // RM-109. Give the new road the real cross-section of the class it says it is, before anything is built
            // from it. Every road is born declaring appliedRoadClass = UrbanTwoLane, but nothing on this path ever
            // applied that class's figures, so the road kept whatever the field initialisers happened to say and the
            // class dropdown was decoration. One call settles lane count, lane and shoulder width, divider, design
            // speed and grade ceiling together, from the single cited table in City.RoadClassDefaults -- so a two-lane
            // street arrives 2 x 3.25 m with a 1 m hard strip rather than 1 x 5 m with a 3 m shoulder that looked like
            // a second lane. The unconditional overload is correct HERE and only here: a road this young has no
            // authored values to preserve, and a later class change still goes through ChangeType's merge, which keeps
            // them.
            road.ApplyRoadClassDefaults();

            //Spline:
            GameObject splineObj = new GameObject("Spline");
            splineObj.transform.parent = road.transform;
            road.spline = splineObj.AddComponent<SplineC>();
            road.spline.splineRoot = splineObj;
            road.spline.road = road;
            road.splineObject = splineObj;
            road.roadSystem = this;
            RoadCityMaster.RootUtils.SetupUniqueIdentifier(ref road.UID);

            road.ResetTerrainHistory();

            EngineIntegration.SetActiveGameObject(roadObj, _isForceSelected);

            return roadObj;
        }


        // B-6 removed EditorCameraSetSingle(). It filled `editorPlayCamera` with the scene's only camera, and its one
        // caller was the inspector -- which called it WHILE DRAWING, so selecting this object wrote a scene reference
        // with no undo step behind it. The pick is an author-pressed button now, in
        // `LegacyInspectorShell.CameraField`, which writes the serialized property instead of the field so the undo
        // stack gets it; the rule about what counts as an obvious camera is still stated once, in
        // `EngineIntegration.OnlySceneCamera`. A setter here would only be a second way to do it without the undo step.


        /// <summary> Updates all roads of this RoadSystem: one queued build per road, so each is named in Unity's
        /// Background Tasks and can be cancelled on its own. </summary>
        public void UpdateAllRoads()
        {
            Road[] allRoadObjs = GetComponentsInChildren<Road>();
            if (allRoadObjs == null || allRoadObjs.Length < 1) { return; }

            // B-14. This handed road[0] a PiggyBacks array holding every spline in the system and requested ONE build.
            // Two things were wrong with that. The array's first entry was road[0]'s OWN spline, so road[0] was built
            // twice per press. And the hop from each road to the next happens in `Road.ConstructionCleanup` as
            // `spline.TriggerSetup()` -> `road.UpdateRoad()` -- the synchronous path, which raises no BuildDispatch
            // ticket. Forty roads were rebuilt and only the first was ever named in Background Tasks, which is
            // indistinguishable from a button that did nothing: the report this entry came from. A single null spline
            // anywhere in the array also made cleanup drop the array and silently abandon every road after it.
            //
            // PiggyBacks itself is untouched -- intersections still use it to rebuild the road on the far side of a
            // junction, which is a different job. The dispatcher is serial (one pending queue, one build in flight), so
            // these run in order exactly as the chain did, and each road's own Supersede applies to its own ticket.
            for (int i = 0; i < allRoadObjs.Length; i++)
            {
                // RM-100. Refresh-all and per-road refresh go through the one queued front door, so the two cannot
                // start builds that interleave -- and so the editor stays usable while a whole system rebuilds.
                if (allRoadObjs[i] != null) { allRoadObjs[i].RequestUpdate(); }
            }
        }


        //Workaround for submission rules:
        /// <summary> Writes isMultithreaded into roads of this system </summary>
        public void UpdateAllRoadsMultiThreadedOption(bool _isMultithreaded)
        {
            Road[] roads = GetComponentsInChildren<Road>();
            int roadsCount = roads.Length;
            Road road = null;
            for (int i = 0; i < roadsCount; i++)
            {
                road = roads[i];
                if (road != null)
                {
                    road.isUsingMultithreading = _isMultithreaded;
                }
            }
        }


        //Workaround for submission rules:
        /// <summary> Writes isSavingMeshes into roads of this system </summary>
        public void UpdateAllRoadsSavingMeshesOption(bool _isSavingMeshes)
        {
            Road[] roads = GetComponentsInChildren<Road>();
            int roadsCount = roads.Length;
            Road road = null;
            for (int i = 0; i < roadsCount; i++)
            {
                road = roads[i];
                if (road != null)
                {
                    road.isSavingMeshes = _isSavingMeshes;
                }
            }
        }
    }
}
