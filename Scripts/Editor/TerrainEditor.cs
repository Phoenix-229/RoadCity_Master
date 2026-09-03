#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
#endregion


namespace RoadCityMaster
{
    [CustomEditor(typeof(RoadTerrain))]
    public class TerrainEditor : Editor
    {
        #region "Vars"
        private RoadTerrain terrain;

        //Serialized properties:
        SerializedProperty splatImageWidth;
        SerializedProperty splatImageHeight;
        SerializedProperty splatBackgroundColor;
        SerializedProperty splatForegroundColor;
        SerializedProperty isSkippingBridges;
        SerializedProperty isSkippingTunnels;
        SerializedProperty isSplatSingleRoad;
        SerializedProperty splatSingleChoiceIndex;
        SerializedProperty roadSingleChoiceUID;

        //Editor only variables:
        private bool isInitialized;
        private string[] roads = null;
        private string[] roadsString = null;
        private Texture refreshButtonText = null;
        private GUIStyle imageButton = null;
        private Texture2D loadButtonBG = null;
        private Texture2D loadButtonBGGlow = null;
        private GUIStyle loadButton = null;
        SplatImageResoMatchingEnum splatReso = SplatImageResoMatchingEnum.None;
        #endregion


        public enum SplatImageResoMatchingEnum
        {
            None,
            Match512x512,
            Match1024x1024,
            Match2048x2048,
            Match4096x4096,
            MatchHeightmapResolution,
            MatchDetailResolution,
            MatchTerrainSize
        };


        private static string[] TheSplatResoOptions = new string[]{
        "Select option to match resolution",
        "512 x 512",
        "1024 x 1024",
        "2048 x 2048",
        "4096 x 4096",
        "Match heightmap resolution",
        "Match detail resolution",
        "Match terrain size"
    };


        /// <summary> RM-102. The default button that sits on the splat width's own row, held as a delegate rather
        /// than built per call: an inspector repaints on mouse movement, and a method group converted to
        /// <see cref="System.Action"/> at the call site allocates a fresh delegate every one of those repaints.
        /// Bound once, in <see cref="OnEnable"/>. </summary>
        private System.Action resetSplatWidth;

        /// <summary> B-19. This inspector's own deferred-commit controls: a splat width typed here reaches the property
        /// when the author presses Enter or moves focus away, not on the keystroke. One per Editor instance, reset by
        /// <see cref="FocusOutFields.BeginFrame"/> at the top of every pass. </summary>
        private readonly FocusOutFields fields = new FocusOutFields();


        private void OnEnable()
        {
            terrain = (RoadTerrain)target;
            resetSplatWidth = ResetSplatWidth;

            splatImageWidth = serializedObject.FindProperty("splatResoWidth");
            splatImageHeight = serializedObject.FindProperty("splatResoHeight");
            splatBackgroundColor = serializedObject.FindProperty("splatBackground");
            splatForegroundColor = serializedObject.FindProperty("splatForeground");
            isSkippingBridges = serializedObject.FindProperty("isSplatSkipBridges");
            isSkippingTunnels = serializedObject.FindProperty("isSplatSkipTunnels");
            isSplatSingleRoad = serializedObject.FindProperty("isSplatSingleRoad");
            splatSingleChoiceIndex = serializedObject.FindProperty("splatSingleChoiceIndex");
            roadSingleChoiceUID = serializedObject.FindProperty("roadSingleChoiceUID");
        }


        /// <summary> Put the splat width back to the value a fresh RoadTerrain carries. The number is the component's
        /// own field initialiser, not the registry's business: the registry states the band a real splat stripe lives
        /// in and where that band comes from, while what THIS tool ships as its starting point is the tool's own
        /// choice. Looked up on the click rather than cached, because a click is not a repaint. </summary>
        private void ResetSplatWidth()
        {
            SerializedProperty prop = serializedObject.FindProperty("splatWidth");
            if (prop != null) { prop.floatValue = 30f; }
        }


        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            fields.BeginFrame();
            if(!isInitialized)
            {
                isInitialized = true;
                InitNullChecks();
            }

            RoadCityMaster.EditorUtilities.DrawLine();
            // RM-102. The product name first, so an author reading a stack of components knows which tool owns this
            // one, then the section heading -- this inspector governs splat map generation and nothing else.
            LegacyInspectorShell.Header(terrain.transform.name);
            EditorGUILayout.BeginHorizontal();
            //Main label:
            EditorGUILayout.LabelField("Splat map generation:", EditorStyles.boldLabel);
            // RM-102. One manual, one button -- see EditorUtilities.OpenOfflineManual.
            if (GUILayout.Button("Manual", EditorStyles.miniButton, GUILayout.Width(120f)))
            {
                RoadCityMaster.EditorUtilities.OpenOfflineManual();
            }
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(6f);

            //Splat Resolution input:
            splatImageWidth.intValue = terrain.splatResoWidth;
            splatImageHeight.intValue = terrain.splatResoHeight;
            EditorGUILayout.BeginHorizontal();
            splatReso = (SplatImageResoMatchingEnum)EditorGUILayout.Popup("Match resolutions:", (int)splatReso, TheSplatResoOptions);
            if (GUILayout.Button(refreshButtonText, imageButton, GUILayout.Width(16f)))
            {
                splatImageWidth.intValue = 1024;
                splatImageHeight.intValue = 1024;
            }
            EditorGUILayout.EndHorizontal();

            if (splatReso != SplatImageResoMatchingEnum.None)
            {
                if (splatReso == SplatImageResoMatchingEnum.MatchHeightmapResolution)
                {
                    splatImageWidth.intValue = terrain.terrain.terrainData.heightmapResolution;
                    splatImageHeight.intValue = terrain.terrain.terrainData.heightmapResolution;
                }
                else if (splatReso == SplatImageResoMatchingEnum.MatchDetailResolution)
                {
                    splatImageWidth.intValue = terrain.terrain.terrainData.detailResolution;
                    splatImageHeight.intValue = terrain.terrain.terrainData.detailResolution;
                }
                else if (splatReso == SplatImageResoMatchingEnum.MatchTerrainSize)
                {
                    splatImageWidth.intValue = (int)terrain.terrain.terrainData.size.x;
                    splatImageHeight.intValue = (int)terrain.terrain.terrainData.size.z;
                }
                else if (splatReso == SplatImageResoMatchingEnum.Match512x512)
                {
                    splatImageWidth.intValue = 512;
                    splatImageHeight.intValue = 512;
                }
                else if (splatReso == SplatImageResoMatchingEnum.Match1024x1024)
                {
                    splatImageWidth.intValue = 1024;
                    splatImageHeight.intValue = 1024;
                }
                else if (splatReso == SplatImageResoMatchingEnum.Match2048x2048)
                {
                    splatImageWidth.intValue = 2048;
                    splatImageHeight.intValue = 2048;
                }
                else if (splatReso == SplatImageResoMatchingEnum.Match4096x4096)
                {
                    splatImageWidth.intValue = 4096;
                    splatImageHeight.intValue = 4096;
                }
                splatReso = SplatImageResoMatchingEnum.None;
            }

            // RM-102. Both resolutions were raw IntFields, which let a splat image be authored at 3000 x 3000 with
            // nothing said about it. They now go through the RM-51 drawer, which carries the 512..4096 band and its
            // source and raises an inline message when the value is outside it instead of clamping the author's
            // number behind their back. They stay HERE, under the match-resolution popup, rather than moving into a
            // foldout: the popup above writes into these two properties, and a value whose control sits in a
            // collapsed group somewhere else cannot be seen changing.
            LegacyInspectorShell.Field(serializedObject, "RoadTerrain", "splatResoWidth", fields);
            LegacyInspectorShell.Field(serializedObject, "RoadTerrain", "splatResoHeight", fields);


            //Splat background color input:
            EditorGUILayout.BeginHorizontal();
            splatBackgroundColor.colorValue = EditorGUILayout.ColorField("Splat background:", terrain.splatBackground);
            //Default button:
            if (GUILayout.Button(refreshButtonText, imageButton, GUILayout.Width(16f)))
            {
                splatBackgroundColor.colorValue = new Color(0f, 0f, 0f, 1f);
            }
            EditorGUILayout.EndHorizontal();

            //Splat foreground color input:
            EditorGUILayout.BeginHorizontal();
            splatForegroundColor.colorValue = EditorGUILayout.ColorField("Splat foreground:", terrain.splatForeground);
            //Default button:
            if (GUILayout.Button(refreshButtonText, imageButton, GUILayout.Width(16f)))
            {
                splatForegroundColor.colorValue = new Color(1f, 1f, 1f, 1f);
            }
            EditorGUILayout.EndHorizontal();

            // RM-102. This was a Slider from 0.02 m to 256 m. A slider is the wrong control for it twice over: it
            // silently clamps, so an author who types 400 gets 256 and no explanation, and its own bounds bore no
            // relation to anything real -- a 2 cm splat stripe is invisible and a 256 m one paints a quarter of a
            // terrain tile per road. Through the drawer it carries the registry's real-world band and says so when
            // the value leaves it. It keeps its default button, on the value's own row, via the trailing overload.
            LegacyInspectorShell.Field(serializedObject, "RoadTerrain", "splatWidth", fields, resetSplatWidth);

            //Skip bridges:
            isSkippingBridges.boolValue = EditorGUILayout.Toggle("Skip bridges: ", terrain.isSplatSkipBridges);

            //Skip tunnels:
            isSkippingTunnels.boolValue = EditorGUILayout.Toggle("Skip tunnels: ", terrain.isSplatSkipTunnels);

            //Splat single road bool input:
            EditorGUILayout.BeginHorizontal();
            isSplatSingleRoad.boolValue = EditorGUILayout.Toggle("Splat a single road: ", terrain.isSplatSingleRoad);

            //Splat single road , road input:
            if (terrain.isSplatSingleRoad)
            {
                LoadSplatSingleChoice();
                splatSingleChoiceIndex.intValue = EditorGUILayout.Popup(terrain.splatSingleChoiceIndex, roadsString, GUILayout.Width(150f));
                roadSingleChoiceUID.stringValue = roads[splatSingleChoiceIndex.intValue];
            }

            EditorGUILayout.EndHorizontal();

            //Generate splatmap button:
            GUILayout.Space(8f);
            if (GUILayout.Button("Generate splatmap for this terrain"))
            {
                GenerateSplatMap();
            }
            GUILayout.Space(10f);

            // B-19. `fields.Changed` is not redundant beside GUI.changed. A deferred control commits on the frame the
            // author presses Enter, moves focus away, or lets a slider handle go, and on that frame no Unity control
            // reported a change -- so GUI.changed is false and this block would skip the one apply that had work to do,
            // losing the committed value at the next Update().
            if (GUI.changed || fields.Changed)
            {
                serializedObject.ApplyModifiedProperties();
                //Necessary?
                //EditorUtility.SetDirty(target);
            }
        }


        private void InitNullChecks()
        {
            string basePath = RoadEditorUtility.GetBasePath();

            RoadCityMaster.EditorUtilities.LoadTexture(ref refreshButtonText, basePath + "/Editor/Icons/refresh2.png");
            RoadCityMaster.EditorUtilities.LoadTexture(ref loadButtonBG, basePath + "/Editor/Icons/FlexBG.png");
            RoadCityMaster.EditorUtilities.LoadTexture(ref loadButtonBGGlow, basePath + "/Editor/Icons/FlexBG.png");

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
                loadButton.normal.background = loadButtonBG;
                loadButton.active.background = loadButtonBGGlow;
                loadButton.focused.background = loadButtonBGGlow;
                loadButton.hover.background = loadButtonBGGlow;
                loadButton.fixedHeight = 16f;
                loadButton.padding = new RectOffset(0, 0, 0, 0);
            }
        }


        private void LoadSplatSingleChoice()
        {
            roads = null;
            roadsString = null;
            Object[] allRoads = EngineIntegration.FindObjectsByType<Road>();
            int roadsCount = allRoads.Length;
            roads = new string[roadsCount];
            roadsString = new string[roadsCount];
            int counter = 0;
            foreach (Road road in allRoads)
            {
                roads[counter] = road.UID;
                roadsString[counter] = road.transform.name;
                counter += 1;
            }
        }


        private void GenerateSplatMap()
        {
            byte[] bytes = null;
            if (terrain.isSplatSingleRoad && terrain.roadSingleChoiceUID != "")
            {
                bytes = RoadUtility.MakeSplatMap(terrain.terrain, terrain.splatBackground, terrain.splatForeground, terrain.splatResoWidth, terrain.splatResoHeight, terrain.splatWidth, terrain.isSplatSkipBridges, terrain.isSplatSkipTunnels, terrain.roadSingleChoiceUID);
            }
            else
            {
                bytes = RoadUtility.MakeSplatMap(terrain.terrain, terrain.splatBackground, terrain.splatForeground, terrain.splatResoWidth, terrain.splatResoHeight, terrain.splatWidth, terrain.isSplatSkipBridges, terrain.isSplatSkipTunnels);
            }

            if (bytes != null && bytes.Length > 3)
            {
                string path = UnityEditor.EditorUtility.SaveFilePanel("Save splat map", Application.dataPath, "Splat", "png");
                if (path != null && path.Length > 3)
                {
                    System.IO.File.WriteAllBytes(path, bytes);
                }
                bytes = null;
            }
        }
    }
}
#endif
