#if UNITY_EDITOR
#region "Imports"
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> The tool's own mark at the top of its parent inspector, so an author who selects the system object
    /// sees whose tool they are in rather than an unlabelled column of toggles.
    ///
    /// This is drawn from the shipped PNG rather than from a colour and a bold label, because the same image is the
    /// manual's masthead and the script icon in the hierarchy: three places showing one file means the product cannot
    /// end up looking like three products. The image is resolved through <c>RoadEditorUtility.GetBasePath()</c>, which
    /// finds the tool by its assembly definition's GUID, so the header survives the whole folder being moved or
    /// renamed -- a hard-coded "Assets/RoadCityMaster/..." would silently draw nothing the first time somebody
    /// reorganised their project.
    ///
    /// The texture is held in a static field and looked up once. An inspector's OnInspectorGUI runs on every repaint,
    /// several times a second while the mouse moves over it, and an AssetDatabase load per repaint is a cost paid
    /// forever for a value that never changes. A null result is cached too -- <c>attempted</c> -- so a missing image
    /// costs one failed lookup rather than one per frame. </summary>
    public static class RcmBrandHeader
    {
        #region "Vars"
        /// <summary> The 128 px square cut of the brand image. The full banner is very nearly square (512 x 506), so
        /// scaled into a header band it would come out no wider than this icon while costing sixteen times the
        /// texture memory; the icon is the crop made for exactly this purpose. </summary>
        private const string IconFileName = "ManualImages/RoadCityMaster_Icon.png";

        /// <summary> Drawn edge of the mark. Chosen to sit inside two default inspector rows so the header reads as a
        /// title rather than as content that pushed the real fields off the screen. </summary>
        private const float MarkEdge = 40f;

        private static Texture2D icon;
        private static bool attempted;
        private static GUIStyle titleStyle;
        private static GUIStyle subtitleStyle;
        #endregion


        /// <summary> The product name as it is shown to an author. The display name carries the ampersand; the
        /// identifier, the folder and the namespace do not. </summary>
        public const string ProductName = "Road & City Master";


        #region "Loading"
        /// <summary> Where the mark lives, as a project-relative asset path, or the empty string when the tool root
        /// cannot be resolved. Public because the operators that stamp and verify the icon have to be able to name the
        /// file they could not find: "brand mark not found" without a path sends an author looking through the whole
        /// package. </summary>
        public static string IconAssetPath()
        {
            string basePath = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(basePath)) { return string.Empty; }

            string path = basePath.Replace('\\', '/');
            if (!path.EndsWith("/")) { path += "/"; }
            return path + IconFileName;
        }


        /// <summary> The brand mark, or null when the image is not on disk. Looked up once per domain reload. </summary>
        public static Texture2D Icon()
        {
            if (attempted) { return icon; }
            attempted = true;

            string path = IconAssetPath();
            if (string.IsNullOrEmpty(path)) { return null; }

            icon = EngineIntegration.LoadAssetFromPath<Texture2D>(path);
            return icon;
        }


        /// <summary> Forgets the cached lookup. Called after the image is re-generated so the header picks up the new
        /// file without the author restarting the editor. </summary>
        public static void Forget()
        {
            icon = null;
            attempted = false;
        }
        #endregion


        #region "Drawing"
        /// <summary> Draws the mark, the product name and one line of subtitle as an inspector header, and reports
        /// whether anything was drawn.
        ///
        /// Nothing is drawn at all when the image is missing. A header that falls back to a text-only band would look
        /// like a deliberate design rather than a missing asset, and the author would never find out that the file
        /// their store listing depends on had not shipped. <see cref="RcmBrandGate"/> is what reports it instead.
        ///
        /// The return value exists because the header ends with a separator rule, and every caller already drew one of
        /// its own above its first control. A caller that ignores the result would show two rules when the image is
        /// present; one that draws its own rule only when this returned false shows exactly one either way. </summary>
        public static bool Draw(string subtitle)
        {
            Texture2D mark = Icon();
            if (mark == null) { return false; }

            EnsureStyles();

            GUILayout.Space(4f);
            Rect row = EditorGUILayout.GetControlRect(false, MarkEdge);
            Rect markRect = new Rect(row.x, row.y, MarkEdge, MarkEdge);
            GUI.DrawTexture(markRect, mark, ScaleMode.ScaleToFit);

            float textX = row.x + MarkEdge + 8f;
            float textWidth = row.width - MarkEdge - 8f;
            if (textWidth > 0f)
            {
                Rect titleRect = new Rect(textX, row.y + 3f, textWidth, 19f);
                GUI.Label(titleRect, ProductName, titleStyle);

                if (!string.IsNullOrEmpty(subtitle))
                {
                    Rect subRect = new Rect(textX, row.y + 21f, textWidth, 16f);
                    GUI.Label(subRect, subtitle, subtitleStyle);
                }
            }

            EditorUtilities.DrawLine();
            return true;
        }


        /// <summary> Styles are built once and kept. Building a GUIStyle inside a repaint allocates on every frame the
        /// mouse crosses the inspector, and the values here never change. Both derive from the editor's own label
        /// styles so the header follows the author's chosen editor theme instead of hard-coding a colour that
        /// disappears against one of them. </summary>
        private static void EnsureStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel);
                titleStyle.fontSize = 13;
                titleStyle.alignment = TextAnchor.MiddleLeft;
            }

            if (subtitleStyle == null)
            {
                subtitleStyle = new GUIStyle(EditorStyles.miniLabel);
                subtitleStyle.alignment = TextAnchor.MiddleLeft;
                subtitleStyle.wordWrap = false;
            }
        }
        #endregion
    }
}
#endif
