// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-81's packaging step: produce the shippable package for the chosen distribution form
    /// (full source, per <see cref="PackageRule"/>), after checking the three things the gate demands -- every
    /// imported file carries a paired `.meta` so GUIDs survive a clean install, the RM-80 notice files are all
    /// present, and no `.cs` is left with an undetermined licence (reported here, enforced by RM-70). The actual
    /// `ExportPackage` write is author-invoked; the checks are the value this step adds before it. </summary>
    public static class PackageExporter
    {
        private const string ToolAssetPath = "Assets/RoadCityMaster";

        [MenuItem("Window/Road City Master/Licensing/Check + export package")]
        public static void ExportFromMenu()
        {
            string report;
            Export(true, out report);
            Debug.Log(report);
        }

        public static void ExportBatch()
        {
            string report;
            Export(true, out report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        /// <summary> Runs the pre-flight checks and, when `write`, exports the package. `write` false is a dry run
        /// that reports the checks and touches nothing. </summary>
        public static string Export(bool write, out string report)
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RCMPKGEXPORT " + (write ? "EXPORT" : "DRY-RUN") + "  form " + PackageRule.ChosenForm);

            int missingMeta = 0, cs = 0, undetermined = 0;
            List<string> missingMetaPaths = new List<string>();
            if (Directory.Exists(root))
            {
                List<string> files = new List<string>();
                CollectAll(root, files);
                files.Sort(System.StringComparer.Ordinal);
                for (int i = 0; i < files.Count; i++)
                {
                    string rel = ToRelative(root, files[i]);
                    if (PackageRule.RequiresMeta(rel) && !File.Exists(files[i] + ".meta"))
                    {
                        missingMeta++;
                        if (missingMetaPaths.Count < 40) { missingMetaPaths.Add(rel); }
                    }
                    if (rel.EndsWith(".cs", System.StringComparison.OrdinalIgnoreCase))
                    {
                        cs++;
                        string text;
                        try { text = File.ReadAllText(files[i]); } catch { continue; }
                        if (!LicenceRule.Resolve(rel, text).determinate) { undetermined++; }
                    }
                }
            }

            IReadOnlyList<string> notices = PackageRule.RequiredNoticeFiles();
            int noticesPresent = 0;
            for (int i = 0; i < notices.Count; i++)
            {
                if (File.Exists(Path.Combine(root, notices[i]))) { noticesPresent++; }
                else { sb.AppendLine("  MISSING NOTICE " + notices[i]); }
            }

            sb.AppendLine("  cs " + Num(cs) + "  missing-meta " + Num(missingMeta)
                + "  notices " + Num(noticesPresent) + "/" + Num(notices.Count)
                + "  undetermined-licence " + Num(undetermined)
                + "  (undetermined is reported; RM-80 stamps headers, RM-70 enforces)");
            for (int i = 0; i < missingMetaPaths.Count; i++) { sb.AppendLine("  MISSING META " + missingMetaPaths[i]); }

            if (write)
            {
                string outPath = Path.Combine(Directory.GetCurrentDirectory(), "RoadCityMaster.unitypackage");
                AssetDatabase.ExportPackage(ToolAssetPath, outPath,
                    ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies);
                sb.AppendLine("  exported " + outPath);
            }
            report = sb.ToString();
            return report;
        }

        private static string Num(int n) { return n.ToString(CultureInfo.InvariantCulture); }

        private static void CollectAll(string dir, List<string> found)
        {
            foreach (string file in Directory.GetFiles(dir))
            {
                if (file.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase)) { continue; }
                found.Add(file);
            }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (string.IsNullOrEmpty(name) || name[0] == '.' || name.EndsWith("~")) { continue; }
                CollectAll(sub, found);
            }
        }

        private static string ToRelative(string root, string full)
        {
            string r = root.Replace('\\', '/');
            string f = full.Replace('\\', '/');
            if (f.StartsWith(r, System.StringComparison.OrdinalIgnoreCase))
            {
                string rel = f.Substring(r.Length);
                if (rel.Length > 0 && rel[0] == '/') { rel = rel.Substring(1); }
                return rel;
            }
            return f;
        }
    }
}
#endif
