#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Project-relative path arithmetic, pure and scene-free. Every path this tool SHOWS an author or
    /// WRITES into an asset must be project-relative ("Assets/..."), never absolute: C-04 forbids an absolute path
    /// in the project, and a path carrying a user name is meaningless on any other machine. So the one place an
    /// absolute path is allowed to exist -- the moment a folder is picked in the editor -- is converted here and
    /// refused if it lies outside the project. No absolute path is hard-coded in this file; the project's own
    /// Assets folder is always supplied by the caller (from <c>Application.dataPath</c> in the editor half). </summary>
    public static class ProjectPaths
    {
        /// <summary> The single folder every project-relative path is rooted at. </summary>
        public const string AssetsRoot = "Assets";

        /// <summary> Forward slashes, no doubled separators, no trailing separator. Unity's own asset paths use
        /// '/' on every platform, so this is the canonical form to compare and to serialise. </summary>
        public static string Normalise(string path)
        {
            if (string.IsNullOrEmpty(path)) { return string.Empty; }
            string s = path.Replace('\\', '/');
            while (s.IndexOf("//", StringComparison.Ordinal) >= 0) { s = s.Replace("//", "/"); }
            while (s.Length > 1 && s[s.Length - 1] == '/') { s = s.Substring(0, s.Length - 1); }
            return s;
        }

        /// <summary> True for a path that is safe to serialise and to show: rooted at <see cref="AssetsRoot"/>,
        /// carrying no drive letter and no ".." segment that could climb out of the project. </summary>
        public static bool IsProjectRelative(string path)
        {
            string s = Normalise(path);
            if (s.Length == 0) { return false; }
            if (s.IndexOf(':') >= 0) { return false; }          // a drive letter or a URI scheme
            if (s[0] == '/') { return false; }                   // rooted at the filesystem, not the project
            if (s.IndexOf("..", StringComparison.Ordinal) >= 0) { return false; }
            if (s == AssetsRoot) { return true; }
            return s.StartsWith(AssetsRoot + "/", StringComparison.Ordinal);
        }

        /// <summary> Convert an absolute path to its project-relative form, given the project's own absolute
        /// Assets folder. Returns null when the path lies OUTSIDE the project -- the caller refuses rather than
        /// storing something no other machine can resolve. </summary>
        public static string ToProjectRelative(string absolutePath, string absoluteAssetsFolder)
        {
            string abs = Normalise(absolutePath);
            string root = Normalise(absoluteAssetsFolder);
            if (abs.Length == 0 || root.Length == 0) { return null; }
            if (string.Equals(abs, root, StringComparison.OrdinalIgnoreCase)) { return AssetsRoot; }
            if (abs.Length <= root.Length + 1) { return null; }
            if (!abs.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)) { return null; }
            return AssetsRoot + "/" + abs.Substring(root.Length + 1);
        }

        /// <summary> True when <paramref name="path"/> sits inside <paramref name="folder"/> (or IS it). The
        /// separator boundary is required, so "Assets/Trees" does not contain "Assets/TreesWip/a.fbx". </summary>
        public static bool IsUnder(string folder, string path)
        {
            string f = Normalise(folder);
            string p = Normalise(path);
            if (f.Length == 0 || p.Length == 0) { return false; }
            if (string.Equals(f, p, StringComparison.Ordinal)) { return true; }
            return p.StartsWith(f + "/", StringComparison.Ordinal);
        }

        /// <summary> The file name with no directory and no extension -- what an author reads in a list. </summary>
        public static string FileNameWithoutExtension(string path)
        {
            string s = Normalise(path);
            if (s.Length == 0) { return string.Empty; }
            int slash = s.LastIndexOf('/');
            if (slash >= 0) { s = s.Substring(slash + 1); }
            int dot = s.LastIndexOf('.');
            if (dot > 0) { s = s.Substring(0, dot); }
            return s;
        }

        /// <summary> The containing folder of a path, or empty when it has none. </summary>
        public static string FolderOf(string path)
        {
            string s = Normalise(path);
            int slash = s.LastIndexOf('/');
            return slash > 0 ? s.Substring(0, slash) : string.Empty;
        }
    }
}
