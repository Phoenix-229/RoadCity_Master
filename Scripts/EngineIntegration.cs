using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif


// The supported engine floor is 6000.4 (see `PipelineAudit.MinimumEngineVersion` and the README). RM-104 removed the
// last pre-Unity-6 fallbacks from the vendored scripts, so an older editor does not warn -- it fails on
// `PhysicsMaterial`, `FindObjectsByType`, `StaticEditorFlags.ContributeGI` and `TerrainData.detailResolution`, with a
// dozen unrelated-looking CS0246s and no hint of the cause. This says the cause once, at the top of the file that owns
// engine integration. The check is on the Unity 6 boundary rather than on 6000.4 exactly, because a `#if` cannot be
// verified until something compiles it and a guard that is wrong about its own define would be worse than none;
// 6000.0 to 6000.3 is caught at gate time instead, by `PipelineVersionGate`'s engine-floor case, which reads
// `Application.unityVersion` and compares minors.
#if !UNITY_6000_0_OR_NEWER
#error Road & City Master requires Unity 6000.4 or newer. This editor is older than Unity 6; the tool keeps no pre-Unity-6 API fallbacks.
#endif


namespace RoadCityMaster
{
    public static class EngineIntegration
    {
        /// <summary>
        /// Unity works with forward slash so we convert
        /// If you want to implement your own Asset creation and saving you should just use finalName
        /// or what applies to the engine you use
        /// </summary>
        public static string GetUnityFilePath(string _filePath)
        {
            string path;
            path = _filePath.Replace(Path.DirectorySeparatorChar, '/');
            path = path.Replace(Path.AltDirectorySeparatorChar, '/');
            return path;
        }


        public static void DisplayDialog(string _title, string _message, string _ok)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.DisplayDialog(_title, _message, _ok);
            #endif
        }


        public static void RepaintAllSceneView()
        {
            #if UNITY_EDITOR
            UnityEditor.SceneView.RepaintAll();
            #endif
        }


        public static void SetActiveGameObject(GameObject _object, bool _isSelected = true)
        {
            #if UNITY_EDITOR
            if (_isSelected)
            {
                UnityEditor.Selection.activeGameObject = _object;
            }
            #endif
        }


        public static GameObject InstantiatePrefab(GameObject _gameObject)
        {
            #if UNITY_EDITOR
            // Instantiate prefab instead of object
            return (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab(_gameObject);
            #else
            // Line to instantiate the object instead of an prefab
            return GameObject.Instantiate(_gameObject);
            #endif
        }


        public static GameObject GetObjectFromSource(GameObject _object)
        {
            #if UNITY_EDITOR
            // `PrefabUtility.GetPrefabParent` was the pre-2018.2 spelling and no longer exists on this engine, so the
            // version-guarded branch that called it could not have compiled if it were ever taken. RM-92 pins the
            // editor at 6000.5, which makes the guard a permanent true and the dead branch a removed API waiting to
            // be uncovered by whoever next edits this file.
            return UnityEditor.PrefabUtility.GetCorrespondingObjectFromSource(_object);
            #else
            //TODO: Check if this is correct to do
            //Return your object
            return null;
            #endif
        }


        /// <summary> Shows or hides the selection WIREFRAME on one renderer, leaving the selection outline alone.
        ///
        /// This is the sweep's one inverted case, and it inverted twice. The call this replaced was
        /// <c>SetSelectedWireframeHidden(renderer, hidden)</c>: a bool meaning "hide it", covering the wireframe only.
        /// The current API takes a [Flags] enum meaning "draw THIS", so the sense of the argument flips -- hence the
        /// parameter is named for what it turns ON. The second flip is the one that compiles cleanly and quietly
        /// changes behaviour: <c>EditorSelectedRenderState.Hidden</c> is 0, and it hides the selection OUTLINE as well,
        /// which the old call never did. Passing it for the off case would have made a selected road stop looking
        /// selected. <c>Highlight</c> alone is the faithful "wireframe hidden", and <c>Wireframe | Highlight</c> the
        /// faithful "wireframe shown", which is also what Unity's own default selection rendering draws. </summary>
        public static void SetSelectedRenderState(Renderer _renderer, bool _isDrawingWireframe)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetSelectedRenderState(_renderer,
                _isDrawingWireframe
                    ? (EditorSelectedRenderState.Wireframe | EditorSelectedRenderState.Highlight)
                    : EditorSelectedRenderState.Highlight);
            #endif
        }


        public static void CreateAsset(Object _object, string _path)
        {
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.CreateAsset(_object, _path);
            #endif
        }


        public static string GetAssetPath<T>(T _asset) where T : UnityEngine.Object
        {
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GetAssetPath(_asset);
            #else
            //TODO: Check if this is correct to do
            return "";
            #endif
        }


        public static void GenerateSecondaryUVSet(Mesh _mesh)
        {
            #if UNITY_EDITOR
            UnityEditor.Unwrapping.GenerateSecondaryUVSet(_mesh);
            #endif
        }


        public static void SaveAssets()
        {
            #if UNITY_EDITOR
            UnityEditor.AssetDatabase.SaveAssets();
            #endif
        }


        public static void SetStaticEditorFlags(GameObject _object)
        {
            #if UNITY_EDITOR
            // `StaticEditorFlags.LightmapStatic` was renamed `ContributeGI` in 2019.2 and the old member is obsolete,
            // so the guarded fallback was a deprecated flag on an engine that cannot reach it (RM-92 pins 6000.5).
            UnityEditor.GameObjectUtility.SetStaticEditorFlags(_object, UnityEditor.StaticEditorFlags.ContributeGI);
            #endif
        }


        // RM-101 removed ClearProgressBar(). It wrapped EditorUtility.ClearProgressBar, which only ever exists to
        // dismiss a MODAL bar -- and there is no longer a modal bar anywhere in the tool to dismiss. Progress is
        // reported into Unity's own background task area by BuildProgressReporter, whose entries are finished from the
        // outcome the dispatcher recorded, so there is no exit path that has to remember to clear anything. Leaving the
        // wrapper in place would have been an invitation to raise a modal bar again and clear it "properly".


        public static T LoadAssetFromPath<T>(string _path) where T: UnityEngine.Object
        {
            //RoadEditorUtility.GetBasePath() + "/Prefabs/Signs/StopSignAllway.prefab"
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(_path);
            #else
            // Get your prefab in your runtime build
            return null;
            #endif
        }


        public static void RegisterUndo(Object _objectToUndo, string _name)
        {
            #if UNITY_EDITOR
            UnityEditor.Undo.RegisterCreatedObjectUndo(_objectToUndo, _name);
            #endif
        }


        public static T[] FindObjectsByType<T>() where T : UnityEngine.Object
        {
            // `FindObjectsByType<T>(FindObjectsSortMode)` is obsolete from Unity 6 (CS0618): the sort order it
            // promised can no longer be maintained, so the overload was dropped rather than kept lying. The
            // parameterless call is the exact behaviour this method always had -- active objects only, order
            // unspecified. The pre-2022.2 fallback that called `FindObjectsOfType<T>()` is gone with RM-104: that
            // spelling is itself obsolete now, and RM-92 pins the editor at 6000.5, so the branch was a dead call to
            // a deprecated API waiting for whoever next edited this file.
            return(GameObject.FindObjectsByType<T>());
        }
        /// <summary> The project-relative path of the asset with this GUID, or an empty string when there is none —
        /// which is also what a player build gets, since a GUID is an editor-database concept. This is how the tool
        /// finds its own installed folder without hard-coding one (see <c>RoadEditorUtility.GetBasePath</c>). </summary>
        public static string AssetPathFromGuid(string _guid)
        {
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.GUIDToAssetPath(_guid);
            #else
            return "";
            #endif
        }


        /// <summary> True when this object is a file on disk rather than a copy living only in memory. Callers use it
        /// before destroying something they believe they minted themselves: an asset destroyed by mistake is deleted
        /// from the project, so the answer outside the editor — where the question cannot be asked at all — is the
        /// cautious <c>true</c>, meaning "leave it alone". </summary>
        public static bool IsPersistentAsset(UnityEngine.Object _object)
        {
            if (_object == null) { return false; }
            #if UNITY_EDITOR
            return UnityEditor.AssetDatabase.Contains(_object);
            #else
            return true;
            #endif
        }


        // ---- The editor loop and the scene viewport (RM-104) ----
        // Road.cs used to reach for EditorApplication and SceneView itself, across nine conditional blocks, and it
        // subscribed with `update += delegate { EditorUpdate(); }`. An anonymous delegate cannot be matched by any
        // later `-=`, because the `-=` writes a second, different closure: every OnEnable therefore added a
        // subscription that no OnDisable could remove, and a scene opened and closed a dozen times ticked a dozen
        // dead roads. These members take the callback ONCE and hand back the delegate they actually registered, so
        // the caller unsubscribes the same instance it subscribed and the removal is exact. The caller keeps it as a
        // plain `object` and needs no UnityEditor reference of its own.

        public static object SubscribeEditorTick(System.Action _tick)
        {
            #if UNITY_EDITOR
            if (_tick == null) { return null; }
            UnityEditor.EditorApplication.CallbackFunction registered =
                new UnityEditor.EditorApplication.CallbackFunction(_tick);
            UnityEditor.EditorApplication.update += registered;
            return registered;
            #else
            return null;
            #endif
        }


        public static void UnsubscribeEditorTick(object _token)
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.CallbackFunction registered =
                _token as UnityEditor.EditorApplication.CallbackFunction;
            if (registered != null) { UnityEditor.EditorApplication.update -= registered; }
            #endif
        }


        /// <summary> Subscribes to the hierarchy-changed notification. The pre-2018 name for this was
        /// <c>hierarchyWindowChanged</c>, which is obsolete on this engine; the old code subscribed to BOTH behind a
        /// version guard, so on any modern editor the handler ran twice per change. </summary>
        public static object SubscribeHierarchyChanged(System.Action _handler)
        {
            #if UNITY_EDITOR
            if (_handler == null) { return null; }
            UnityEditor.EditorApplication.hierarchyChanged += _handler;
            return _handler;
            #else
            return null;
            #endif
        }


        public static void UnsubscribeHierarchyChanged(object _token)
        {
            #if UNITY_EDITOR
            System.Action registered = _token as System.Action;
            if (registered != null) { UnityEditor.EditorApplication.hierarchyChanged -= registered; }
            #endif
        }


        /// <summary> Editor wall-clock seconds, used to pace the editor fly-through camera. Zero outside the editor,
        /// where there is no such camera to pace. </summary>
        public static double EditorTimeSinceStartup
        {
            get
            {
                #if UNITY_EDITOR
                return UnityEditor.EditorApplication.timeSinceStartup;
                #else
                return 0d;
                #endif
            }
        }


        /// <summary> True while the editor is entering, in, or leaving play mode — the window in which editor-side
        /// scene surgery must not run. Both of the questions Road.cs used to ask (<c>isPlayingOrWillChangePlaymode</c>
        /// and <c>isPlaying</c>) are answered here, because either one being true means the same thing to a caller. </summary>
        public static bool IsPlayModeEnteringOrRunning
        {
            get
            {
                #if UNITY_EDITOR
                return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode
                    || UnityEditor.EditorApplication.isPlaying;
                #else
                return Application.isPlaying;
                #endif
            }
        }


        public static bool IsObjectSelected(GameObject _object)
        {
            #if UNITY_EDITOR
            return _object != null && UnityEditor.Selection.Contains(_object);
            #else
            return false;
            #endif
        }


        // The scene-view camera. Each guards `lastActiveSceneView` for null, which the calling code did not: a scene
        // open with no scene view -- a batch-mode run, or a maximised game view -- threw rather than declining to move
        // a camera that is not on screen.

        public static void SetSceneViewPivot(Vector3 _pivot)
        {
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView == null) { return; }
            UnityEditor.SceneView.lastActiveSceneView.pivot = _pivot;
            #endif
        }


        public static void SetSceneViewRotation(Quaternion _rotation)
        {
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView == null) { return; }
            UnityEditor.SceneView.lastActiveSceneView.rotation = _rotation;
            #endif
        }


        public static void RepaintLastSceneView()
        {
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView == null) { return; }
            UnityEditor.SceneView.lastActiveSceneView.Repaint();
            #endif
        }
    }
}
