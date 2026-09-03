#if UNITY_EDITOR
#region "Imports"
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-84: makes the RM-83 manual reachable from inside Unity. A menu item under the tool's
    /// own menu opens the HTML manual in the system browser, and the same <see cref="OpenManual"/> entry
    /// point is what the RM-52 status panel's "Documentation" button will call once that panel's GUI lands,
    /// so there is one code path, not two. The manual is a single self-contained HTML file at the tool root
    /// (<c>RoadCityMasterManual.html</c>); the tool folder is resolved from the root asmdef's GUID (never a
    /// hard-coded path — C-04), so it works wherever a customer installs the folder. Every branch warns
    /// rather than throws, because a customer who trimmed the manual to shrink a build has done nothing wrong
    /// and an exception in a menu item reads as a broken tool. </summary>
    public static class Documentation
    {
        /// <summary> The root `RoadCityMaster.asmdef`'s GUID. Resolving through it means the folder can live
        /// anywhere in a project. Do NOT recreate that asset — the GUID is the anchor. </summary>
        internal const string ToolAsmdefGuid = "e9c7aa1199abeb64c82a4831b3c7286f";

        internal const string ManualRelative = "RoadCityMasterManual.html";

        internal const string MissingRootMessage =
            "Road & City Master: could not locate the tool folder from its assembly definition; "
            + "the package may be incomplete.";

        /// <summary> Resolves the manual's AssetDatabase-relative path from the asmdef GUID. Returns false
        /// with a clear, non-throwing message when the tool root cannot be located. The path uses forward
        /// slashes; the root asmdef sits at &lt;root&gt;/RoadCityMaster.asmdef, so the tool root is its
        /// containing folder. </summary>
        public static bool TryGetManualAssetPath(out string path, out string message)
        {
            path = null;
            string asmdef = AssetDatabase.GUIDToAssetPath(ToolAsmdefGuid);
            if (string.IsNullOrEmpty(asmdef)) { message = MissingRootMessage; return false; }
            string root = Path.GetDirectoryName(asmdef);
            if (string.IsNullOrEmpty(root)) { message = MissingRootMessage; return false; }
            path = (root + "/" + ManualRelative).Replace('\\', '/');
            message = string.Empty;
            return true;
        }

        /// <summary> The message shown when the folder is present but the manual is not (a customer removed
        /// it). Pure and testable; names the file it looked for. </summary>
        public static string MissingManualMessage(string manualPath)
        {
            return "Road & City Master: manual not found. Expected " + ManualRelative
                + " inside the tool folder"
                + (string.IsNullOrEmpty(manualPath) ? string.Empty : " (" + manualPath + ")")
                + "; it may have been removed from this project.";
        }

        /// <summary> Turns an AssetDatabase-relative path into a browser-openable file URI, resolved against
        /// the project root (Directory.GetCurrentDirectory) — C-04-safe, and Uri encodes spaces so a project
        /// path such as "Universe Builder" opens correctly. </summary>
        public static string ToFileUri(string assetRelativePath)
        {
            string absolute = Path.GetFullPath(assetRelativePath);
            return new Uri(absolute).AbsoluteUri;
        }

        [MenuItem("Window/Road City Master/Open Manual", false, 100)]
        public static void OpenManual()
        {
            string path;
            string message;
            if (!TryGetManualAssetPath(out path, out message)) { Debug.LogWarning(message); return; }
            if (!File.Exists(path)) { Debug.LogWarning(MissingManualMessage(path)); return; }

            Application.OpenURL(ToFileUri(path));
        }
    }
}
#endif
