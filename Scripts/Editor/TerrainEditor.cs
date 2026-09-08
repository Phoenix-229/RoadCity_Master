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
        // B-11. Gone from here: 'loadButton' and the two textures behind it. It was the fourth hand-built copy of the
        // branded command style and the only DEAD one -- nothing in this file ever drew a button with it, and both its
        // rest and hover backgrounds were the same `FlexBG.png`, so it had no hover to draw either. The live style is
        // `LegacyInspectorShell.CommandButtonStyle()`; this file has no branded command to point at it, so the three
        // fields, the two loads and the builder are simply gone rather than repointed.
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

            // B-14. Every splat control below reads `terrain.terrain.terrainData` -- the three resolution-matching
            // options do it during a repaint, and the generate button does it inside MakeSplatMap. RoadTerrain fills
            // that field from a Terrain on its own GameObject, so it is null exactly when this component was added to
            // something that is not a terrain. Drawing the controls anyway means a NullReferenceException per repaint
            // from a popup the author only opened to look at, so the section says what is wrong instead of drawing.
            if (terrain.terrain == null)
            {
                EditorGUILayout.HelpBox("This component is not on a terrain, so there is nothing to make a splat map"
                    + " of. Move it onto a GameObject that has a Terrain component, or assign one to its \"terrain\""
                    + " field.", MessageType.Warning);
                RoadCityMaster.EditorUtilities.DrawLine();
                return;
            }

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
                // B-14. This read `roads[splatSingleChoiceIndex.intValue]` with no bounds check on an array built from
                // whatever roads are in the scene right now. With no roads it is empty and [0] throws; after a road is
                // deleted the stored index outlives the entry it named and throws just the same -- an
                // IndexOutOfRangeException on every repaint of an inspector the author merely has open, from a toggle
                // they ticked to make a choice. Say there is nothing to choose from, and clamp a stale index to a road
                // that does exist so the toggle keeps working instead of stalling the whole inspector.
                if (roadsString.Length == 0)
                {
                    EditorGUILayout.LabelField("No roads in the scene to choose from.");
                    roadSingleChoiceUID.stringValue = "";
                }
                else
                {
                    int choice = Mathf.Clamp(terrain.splatSingleChoiceIndex, 0, roadsString.Length - 1);
                    choice = EditorGUILayout.Popup(choice, roadsString, GUILayout.Width(150f));
                    splatSingleChoiceIndex.intValue = choice;
                    roadSingleChoiceUID.stringValue = roads[choice];
                }
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


        /// <summary> B-14. The one button in this inspector that does real work, and it was the one that said nothing
        /// about any of it. Three outcomes were indistinguishable from each other and from a dropped click: a file
        /// written, the save panel dismissed, and nothing produced because there was no road to trace. So each is said
        /// once, in the console, naming the file where there is one. No progress bar: EditorUtility.DisplayProgressBar
        /// is modal, and BuildProgressReporter records it as the freeze the author complained about -- for a call that
        /// blocks either way, the report that helps is its outcome, not a bar drawn during it. </summary>
        private void GenerateSplatMap()
        {
            byte[] bytes;
            string scope;
            if (terrain.isSplatSingleRoad && terrain.roadSingleChoiceUID != "")
            {
                bytes = RoadUtility.MakeSplatMap(terrain.terrain, terrain.splatBackground, terrain.splatForeground, terrain.splatResoWidth, terrain.splatResoHeight, terrain.splatWidth, terrain.isSplatSkipBridges, terrain.isSplatSkipTunnels, terrain.roadSingleChoiceUID);
                scope = "the chosen road";
            }
            else
            {
                bytes = RoadUtility.MakeSplatMap(terrain.terrain, terrain.splatBackground, terrain.splatForeground, terrain.splatResoWidth, terrain.splatResoHeight, terrain.splatWidth, terrain.isSplatSkipBridges, terrain.isSplatSkipTunnels);
                scope = "every road in the scene";
            }

            if (bytes == null || bytes.Length < 4)
            {
                // MakeSplatMap returns null for exactly one reason the author can act on: it found no built road to
                // trace. Either the chosen road has been deleted since it was chosen, or no road in the scene has been
                // built yet -- a road with no spline has no centre line to paint.
                Debug.LogWarning("Road & City Master: no splat map was made for \"" + terrain.transform.name + "\","
                    + " because there was no built road to trace for " + scope + ". Build the roads first; a splat map"
                    + " of the background colour alone carries nothing.");
                return;
            }

            string path = UnityEditor.EditorUtility.SaveFilePanel("Save splat map", Application.dataPath, "Splat", "png");
            if (string.IsNullOrEmpty(path))
            {
                Debug.Log("Road & City Master: the splat map for \"" + terrain.transform.name + "\" was made but not"
                    + " saved, because the save panel was dismissed.");
                return;
            }

            System.IO.File.WriteAllBytes(path, bytes);
            Debug.Log("Road & City Master: wrote a " + terrain.splatResoWidth + " x " + terrain.splatResoHeight
                + " splat map of " + scope + " on \"" + terrain.transform.name + "\" to " + path);
        }
    }
}
#endif
