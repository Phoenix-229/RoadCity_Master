#region "Imports"
using UnityEngine;
#endregion


namespace RoadCityMaster
{
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
        /// Two frame sources, because neither covers the other -- <c>Update</c> is the player and play mode, the
        /// editor's own update is edit mode, where an author clicking Refresh is not in a play session. Since RM-104
        /// this file names no editor type at all: the subscription goes through <see cref="EngineIntegration"/>, and
        /// the dispatcher itself never had one, which is what lets a player rebuild a road with the same code. </summary>
        private void Update()
        {
            City.BuildDispatch.Shared.Pump();
        }


        /// <summary> The editor-side frame source. RM-104 moved the subscription itself behind
        /// <see cref="EngineIntegration"/>, so this file names no editor type at all: it hands over a callback and
        /// keeps the token it gets back, which is what makes the removal in <see cref="OnDisable"/> exact. </summary>
        [System.NonSerialized] private object editorPumpToken;


        private void OnEnable()
        {
            EngineIntegration.UnsubscribeEditorTick(editorPumpToken);
            editorPumpToken = EngineIntegration.SubscribeEditorTick(EditorPumpBuilds);
        }


        private void OnDisable()
        {
            EngineIntegration.UnsubscribeEditorTick(editorPumpToken);
            editorPumpToken = null;
        }


        /// <summary> A named method rather than a lambda, because <c>-=</c> on an anonymous delegate removes a different
        /// instance than <c>+=</c> added and therefore removes nothing -- which is how an editor accumulates one dead
        /// subscription per domain reload. </summary>
        private void EditorPumpBuilds()
        {
            if (this == null)
            {
                EngineIntegration.UnsubscribeEditorTick(editorPumpToken);
                editorPumpToken = null;
                return;
            }
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


        /// <summary> Sets the editorPlayCamera to the first camera, if it is the only camera in this scene </summary>
        public void EditorCameraSetSingle()
        {
            if (editorPlayCamera == null)
            {
                Camera[] editorCams = EngineIntegration.FindObjectsByType<Camera>();
                if (editorCams != null && editorCams.Length == 1)
                {
                    editorPlayCamera = editorCams[0];
                }
            }
        }


        /// <summary> Updates all roads of this RoadSystem </summary>
        public void UpdateAllRoads()
        {
            Road[] allRoadObjs = GetComponentsInChildren<Road>();
            if (allRoadObjs == null || allRoadObjs.Length < 1) { return; }
            int roadCount = allRoadObjs.Length;
            SplineC[] piggys = null;
            if (roadCount > 1)
            {
                piggys = new SplineC[roadCount];
                for (int i = 0; i < roadCount; i++)
                {
                    piggys[i] = allRoadObjs[i].spline;
                }
            }

            Road road = allRoadObjs[0];
            if (piggys != null && piggys.Length > 0)
            {
                road.PiggyBacks = piggys;
            }
            // RM-100. Refresh-all and per-road refresh go through the one queued front door, so the two cannot start
            // builds that interleave -- and so the editor stays usable while a whole system rebuilds.
            road.RequestUpdate();
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
