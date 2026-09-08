#if UNITY_EDITOR
#region "Imports"
using UnityEngine;
using UnityEditor;
using System.Text.RegularExpressions;
using System.IO;
#endregion


namespace RoadCityMaster
{
    public class SaveWindow : EditorWindow
    {
        public enum WindowTypeEnum
        {
            Extrusion,
            Edge,
            BridgeWizard
        };


        #region "Vars"
        private WindowTypeEnum windowType = WindowTypeEnum.Extrusion;

        private Texture2D temp2D = null;
        private Texture2D temp2D2 = null;
        private string thumbString = "";
        private string desc = "";
        private string fileName = "DefaultName";
        private string displayName = "DefaultName";
        private string displayName2 = "";
        private string titleText = "";
        //	private string tPath = "";
        private bool isFileExisting = false;
        private bool isBridge = false;

        private Splination.SplinatedMeshMaker[] tSMMs = null;
        private EdgeObjects.EdgeObjectMaker[] tEOMs = null;
        private string path = "";
        private const int titleLabelHeight = 20;
        #endregion


        private void OnGUI()
        {
            GUILayout.Space(4f);
            EditorGUILayout.LabelField(titleText, EditorStyles.boldLabel);

            temp2D2 = (Texture2D)EditorGUILayout.ObjectField("Square thumb (optional):", temp2D, typeof(Texture2D), false);
            if (temp2D2 != temp2D)
            {
                temp2D = temp2D2;
                thumbString = EngineIntegration.GetAssetPath(temp2D);
            }

            if (path.Length < 5)
            {
                path = RootUtils.GetDirLibrary();
            }

            EditorGUILayout.LabelField("Short description (optional):");
            desc = EditorGUILayout.TextArea(desc, GUILayout.Height(40f));

            // B-19. B-14. This one stays per-keystroke, and it is the second and last place in the tool where that is
            // the right answer.
            //
            // B-19 removed per-keystroke commits because of what they cost: a value written on every letter rebuilt the
            // road from a half-typed number, wrote an undo step per letter, and was re-read from under the author by the
            // next serializedObject.Update(). None of those exist here. `displayName` is a private field of this window,
            // not a serialised property; nothing is applied, no undo entry is recorded, and no road is rebuilt. The
            // whole per-keystroke cost is one Regex.Replace in SanitizeFilename and one File.Exists.
            //
            // And what it buys is the only thing this window does. The two readouts below -- "File exists already!" and
            // the full save path -- both read `fileName`, which SanitizeFilename derives from this value. They exist to
            // tell an author, before they press Save, exactly which file they are about to write and whether something
            // is already there. Deferred to focus-out, an author typing "TunnelRail" would read a path ending
            // DefaultName.rao and an existence warning about a file they are not saving, right up until they clicked
            // away -- a wrong answer where the window's entire purpose is to give the right one.
            //
            // <see cref="LegacyInspectorGate"/> case 10 records exactly TWO immediate controls in exactly this file:
            // this one, and the description area above it -- free text that nothing reads until Save is pressed, so
            // deferring it would buy nothing to pay for. Converting this window to deferred commits, or adding a THIRD
            // immediate control to it, fails the gate rather than passing quietly under this comment.
            displayName2 = EditorGUILayout.TextField("Display name:", displayName);
            if (string.Compare(displayName2, displayName) != 0)
            {
                displayName = displayName2;
                SanitizeFilename();

                CheckFileExistence();
            }


            if (isFileExisting)
            {
                EditorGUILayout.LabelField("File exists already!", EditorStyles.miniLabel);
            }

            EditorGUILayout.LabelField(TargetFilePath(), EditorStyles.miniLabel);

            GUILayout.Space(4f);

            isBridge = EditorGUILayout.Toggle("Is bridge related:", isBridge);
            GUILayout.Space(8f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
            if (windowType == WindowTypeEnum.Extrusion)
            {
                DoExtrusion();
            }
            else if (windowType == WindowTypeEnum.Edge)
            {
                DoEdgeObject();
            }
            else if (windowType == WindowTypeEnum.BridgeWizard)
            {
                DoBridge();
            }

            EditorGUILayout.EndHorizontal();
        }


        private void DoExtrusion()
        {
            if (GUILayout.Button("Save extrusion"))
            {
                SanitizeFilename();
                tSMMs[0].isBridge = isBridge;
                tSMMs[0].thumbString = thumbString;
                tSMMs[0].desc = desc;
                tSMMs[0].displayName = displayName;
                // B-14. Read before the save, because the window closes on the next line and the path it was showing
                // goes with it. A save writes a file outside the project the author is looking at and changes nothing on
                // screen, so without this the only difference between a save and a dropped click is that the window
                // shut -- which Cancel does too.
                string target = TargetFilePath();
                tSMMs[0].SaveToLibrary(fileName, false);
                Debug.Log("Road & City Master: saved the extrusion \"" + displayName + "\" to " + target);
                Close();
            }
        }


        private void DoEdgeObject()
        {
            if (GUILayout.Button("Save edge object"))
            {
                SanitizeFilename();
                tEOMs[0].isBridge = isBridge;
                tEOMs[0].thumbString = thumbString;
                tEOMs[0].desc = desc;
                tEOMs[0].displayName = displayName;
                string target = TargetFilePath();
                tEOMs[0].SaveToLibrary(fileName, false);
                Debug.Log("Road & City Master: saved the edge object \"" + displayName + "\" to " + target);
                Close();
            }
        }


        private void DoBridge()
        {
            if (GUILayout.Button("Save group"))
            {
                SanitizeFilename();
                WizardObject WO = new WizardObject();
                WO.thumbString = thumbString;
                WO.desc = desc;
                WO.displayName = displayName;
                WO.fileName = fileName;
                WO.isBridge = isBridge;
                WO.isDefault = false;

                string target = TargetFilePath();
                RoadUtility.SaveNodeObjects(ref tSMMs, ref tEOMs, ref WO);
                Debug.Log("Road & City Master: saved the group \"" + displayName + "\" -- "
                    + (tSMMs == null ? 0 : tSMMs.Length) + " extrusion(s) and "
                    + (tEOMs == null ? 0 : tEOMs.Length) + " edge object(s) -- to " + target);
                Close();
            }
        }


        private void SanitizeFilename()
        {
            Regex regex = new Regex("[^a-zA-Z0-9 -]");
            fileName = regex.Replace(displayName, "");
            fileName = fileName.Replace(" ", "-");
            fileName = fileName.Replace("_", "-");
        }


        /// <summary> The file this window will write when Save is pressed, for the window type it was opened as.
        ///
        /// B-14. The same three-branch path arithmetic was written out twice, once for the label that tells the author
        /// which file they are about to write and once inside <see cref="CheckFileExistence"/>, and the two copies did
        /// not agree: the label named &lt;library&gt;/Groups/&lt;name&gt;.rao while the existence check looked in
        /// &lt;library&gt;/B/&lt;name&gt;.rao -- a folder nothing writes to. So "File exists already!" could not appear
        /// for a group however many groups were already saved, and pressing Save quietly replaced one.
        /// RoadUtility.SaveNodeObjects writes Groups, so the label was the correct copy and this is it; the wrong copy
        /// is gone rather than corrected in place, because two copies is how they came to disagree. The save reports
        /// name this too, so there is now one statement of where a preset goes and three readers of it. </summary>
        private string TargetFilePath()
        {
            if (path.Length < 5)
            {
                path = RootUtils.GetDirLibrary();
            }
            if (windowType == WindowTypeEnum.Edge)
            {
                return Path.Combine(path, "EOM" + fileName + ".rao");
            }
            if (windowType == WindowTypeEnum.Extrusion)
            {
                return Path.Combine(path, "ESO" + fileName + ".rao");
            }
            return Path.Combine(Path.Combine(path, "Groups"), fileName + ".rao");
        }


        private void CheckFileExistence()
        {
            isFileExisting = File.Exists(TargetFilePath());
        }


        #region "Init"
        public void Initialize(ref Rect _rect, WindowTypeEnum _windowType, SplineN _node, Splination.SplinatedMeshMaker _SMM = null, EdgeObjects.EdgeObjectMaker _EOM = null)
        {
            int rectHeight = 300;
            int rectWidth = 360;
            float Rx = ((float)_rect.width / 2f) - ((float)rectWidth / 2f) + _rect.x;
            float Ry = ((float)_rect.height / 2f) - ((float)rectHeight / 2f) + _rect.y;

            if (Rx < 0)
            {
                Rx = _rect.x;
            }
            if (Ry < 0)
            {
                Ry = _rect.y;
            }
            if (Rx > (_rect.width + _rect.x))
            {
                Rx = _rect.x;
            }
            if (Ry > (_rect.height + _rect.y))
            {
                Ry = _rect.y;
            }

            Rect rect = new Rect(Rx, Ry, rectWidth, rectHeight);

            if (rect.width < 300)
            {
                rect.width = 300;
                rect.x = _rect.x;
            }
            if (rect.height < 300)
            {
                rect.height = 300;
                rect.y = _rect.y;
            }

            position = rect;
            windowType = _windowType;
            Show();
            titleContent.text = "Save";
            if (windowType == WindowTypeEnum.Extrusion)
            {
                titleText = "Save extrusion";
                tSMMs = new Splination.SplinatedMeshMaker[1];
                tSMMs[0] = _SMM;
                if (_SMM != null)
                {
                    fileName = _SMM.objectName;
                    displayName = fileName;
                }
            }
            else if (windowType == WindowTypeEnum.Edge)
            {
                titleText = "Save edge object";
                tEOMs = new EdgeObjects.EdgeObjectMaker[1];
                tEOMs[0] = _EOM;
                if (_EOM != null)
                {
                    fileName = _EOM.objectName;
                    displayName = fileName;
                }
            }
            else if (windowType == WindowTypeEnum.BridgeWizard)
            {
                isBridge = true;
                tSMMs = _node.SplinatedObjects.ToArray();
                tEOMs = _node.EdgeObjects.ToArray();
                titleText = "Save group";
                fileName = "Group" + Random.Range(0, 10000).ToString();
                displayName = fileName;
            }

            if (path.Length < 5)
            {
                path = RootUtils.GetDirLibrary();
            }

            CheckFileExistence();
        }
        #endregion
    }
}
#endif
