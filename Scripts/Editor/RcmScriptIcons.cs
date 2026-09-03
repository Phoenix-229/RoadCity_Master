#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Puts the tool's mark on its own components in the hierarchy and the scene view, in place of Unity's
    /// default grey C# script badge and the grey cuboid a script with no icon draws at its transform.
    ///
    /// A scene holding a road network shows one entry per node; with the default badge every one of them looks like an
    /// anonymous script belonging to nobody. The icon is stored in the script's own <c>.meta</c> file, which is why
    /// this is an operator that asks the editor to write rather than a hand-edited YAML change: the icon reference in a
    /// meta is a file GUID and a local file ID, and hand-writing either is how a meta ends up pointing at an asset that
    /// has since been re-imported under a different ID. <c>MonoImporter.SetIcon</c>, then
    /// <c>AssetDatabase.WriteImportSettingsIfDirty</c> to serialise it, then a forced re-import, makes Unity itself
    /// produce the bytes. <c>EditorGUIUtility.SetIconForObject</c> is the call this looks like it should use and does
    /// not: it never reaches the importer, so nothing is written -- see the note at the write site.
    ///
    /// Deliberately NOT named Run(out string): <see cref="RcmGateSuite"/> discovers gates by that exact signature, and
    /// a stamper that wrote to disk every time the suite ran would turn a read-only verification into an edit. The
    /// read-only half of this file's job lives in <see cref="RcmBrandGate"/>, which is a gate and does have that
    /// signature. </summary>
    public static class RcmScriptIcons
    {
        #region "Vars"
        private const string Prefix = "RCMICON";

        /// <summary> The components an author sees in a scene. Held as System.Type rather than as strings so that
        /// renaming one of them breaks this file at compile time instead of silently stamping nothing: a stamper that
        /// reports "0 of 5 matched" long after the rename is a report nobody reads. </summary>
        private static Type[] Targets()
        {
            return new Type[]
            {
                typeof(RoadSystem),
                typeof(Road),
                typeof(SplineN),
                typeof(RoadIntersection),
                typeof(RoadCityMaster.City.TerrainLinker)
            };
        }
        #endregion


        #region "Entry points"
        [MenuItem("Window/Road City Master/Setup/Stamp script icons")]
        public static void StampFromMenu()
        {
            string report;
            bool ok = StampAll(true, out report);
            if (ok) { Debug.Log(report); } else { Debug.LogError(report); }
        }


        [MenuItem("Window/Road City Master/Setup/Stamp script icons (report only)")]
        public static void ReportFromMenu()
        {
            string report;
            StampAll(false, out report);
            Debug.Log(report);
        }


        /// <summary> Headless entry: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.RcmScriptIcons.StampBatch. Exits 1 when a target could not be stamped. </summary>
        public static void StampBatch()
        {
            string report;
            bool ok = StampAll(true, out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }
        #endregion


        #region "Stamping"
        /// <summary> Assigns the brand mark to every target script's icon, reporting only the text.
        ///
        /// The counting overload below is the real one; this forwarder exists for the menu items and the batch entry,
        /// which have nothing to do with the numbers. </summary>
        public static bool StampAll(bool write, out string report)
        {
            int changed, already, missing;
            return StampAll(write, out report, out changed, out already, out missing);
        }


        /// <summary> Assigns the brand mark to every target script's icon.
        ///
        /// <paramref name="write"/> false performs the whole scan and reports exactly what would change without
        /// touching a file, so the census can be read before anyone agrees to a disk write, and so a read-only gate can
        /// reuse it. A script that already carries the right icon is reported as already-set and left alone even when
        /// write is true -- re-importing an unchanged asset costs an import and a version-control diff for nothing.
        ///
        /// <paramref name="changed"/> counts scripts stamped, or scripts that would be stamped when write is false;
        /// that second reading is what tells a gate how many components still show Unity's default badge. When write is
        /// true it counts only the stamps that were read back off disk afterwards, so it can never report a write that
        /// did not land.
        ///
        /// The return value reports whether every target RESOLVED and every attempted write STUCK -- not whether every
        /// target was already stamped. An unstamped icon is a job still to do; an unresolvable component is a broken
        /// build, and a write that does not survive its own re-import is a silent one, which is worse than either. </summary>
        public static bool StampAll(bool write, out string report, out int changed, out int already, out int missing)
        {
            StringBuilder builder = new StringBuilder();
            Type[] targets = Targets();
            Texture2D mark = RcmBrandHeader.Icon();
            changed = 0;
            already = 0;
            missing = 0;
            int notKept = 0;

            if (mark == null)
            {
                missing = targets.Length;
                report = Prefix + " FAIL   brand mark not found -- " + RcmBrandHeader.IconAssetPath()
                       + " is missing, so there is nothing to stamp\n";
                return false;
            }

            Dictionary<Type, MonoScript> scripts = ScriptsFor(targets);

            for (int i = 0; i < targets.Length; i++)
            {
                Type type = targets[i];
                MonoScript script;
                if (!scripts.TryGetValue(type, out script) || script == null)
                {
                    missing++;
                    builder.Append(Prefix).Append(" missing  ").Append(type.FullName)
                           .Append("  no MonoScript asset resolved to this type\n");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(script);
                MonoImporter importer = AssetImporter.GetAtPath(path) as MonoImporter;
                if (importer == null)
                {
                    missing++;
                    builder.Append(Prefix).Append(" missing  ").Append(type.FullName)
                           .Append("  no MonoImporter at ").Append(path).Append("\n");
                    continue;
                }

                // Asked of the IMPORTER, not of `EditorGUIUtility.GetIconForObject`. The utility answers from an
                // in-memory icon table that reports the icon it was handed even when the `.meta` on disk never took it,
                // so a census built on it says every component is stamped while the files say otherwise. The importer
                // answers from the imported asset, which is the thing an author actually sees in the hierarchy.
                if (importer.GetIcon() == mark)
                {
                    already++;
                    builder.Append(Prefix).Append(" already  ").Append(type.FullName).Append("  ").Append(path).Append("\n");
                    continue;
                }

                changed++;
                if (write)
                {
                    // `MonoImporter.SetIcon`, then `WriteImportSettingsIfDirty` to serialise it, then a forced import
                    // so the editor picks up what was written. All three, in that order.
                    //
                    // NOT `EditorGUIUtility.SetIconForObject`, which is the obvious call and does not work here. It
                    // sets the icon in an in-memory table and does not mark the script's importer dirty, so
                    // `WriteImportSettingsIfDirty` has nothing to flush and the meta keeps `icon: {instanceID: 0}`.
                    // Worse, `GetIconForObject` then returns the icon it was handed, so the operator's own read-back
                    // agreed with it: five components reported "stamped", five metas were untouched, and the failure
                    // only surfaced a session later when the brand gate read the files. The importer route writes
                    // `icon: {fileID: 2800000, guid: <the png's guid>, type: 3}` -- a real reference, with both halves
                    // produced by Unity rather than typed here.
                    importer.SetIcon(mark);
                    AssetDatabase.WriteImportSettingsIfDirty(path);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

                    // Read back off the re-imported importer rather than trusting the call. The importer instance is
                    // re-fetched because the import just replaced it, and this is the check that would have caught the
                    // bug above on the day it was written.
                    MonoImporter after = AssetImporter.GetAtPath(path) as MonoImporter;
                    if (after != null && after.GetIcon() == mark)
                    {
                        builder.Append(Prefix).Append(" stamped  ").Append(type.FullName).Append("  ").Append(path)
                               .Append("\n");
                    }
                    else
                    {
                        changed--;
                        notKept++;
                        builder.Append(Prefix).Append(" NOT-KEPT ").Append(type.FullName).Append("  ").Append(path)
                               .Append("  the icon did not survive the re-import; the meta still shows the default "
                               + "badge\n");
                    }
                }
                else
                {
                    builder.Append(Prefix).Append(" would    ").Append(type.FullName).Append("  ").Append(path).Append("\n");
                }
            }

            if (write && changed > 0) { AssetDatabase.SaveAssets(); }

            bool ok = missing == 0 && notKept == 0;
            builder.Append(Prefix).Append(" result ").Append(ok ? "PASS" : "FAIL")
                   .Append("  targets ").Append(targets.Length)
                   .Append("  ").Append(write ? "stamped " : "would-stamp ").Append(changed)
                   .Append("  already ").Append(already)
                   .Append("  missing ").Append(missing);
            if (notKept > 0) { builder.Append("  not-kept ").Append(notKept); }
            builder.Append("\n");

            report = builder.ToString();
            return ok;
        }


        /// <summary> The MonoScript asset behind each requested type, searched only inside the tool's own folder.
        ///
        /// Matching is by <c>MonoScript.GetClass()</c> and not by file name. Two packages may both ship a Road.cs, and
        /// a file-name match would stamp whichever one the search returned first. Restricting the search to the tool's
        /// base path keeps the scan bounded and keeps it from ever writing to another vendor's asset. </summary>
        private static Dictionary<Type, MonoScript> ScriptsFor(Type[] targets)
        {
            Dictionary<Type, MonoScript> found = new Dictionary<Type, MonoScript>();
            string basePath = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(basePath)) { return found; }

            string[] guids = AssetDatabase.FindAssets("t:MonoScript", new string[] { basePath.Replace('\\', '/') });
            for (int g = 0; g < guids.Length; g++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[g]);
                MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null) { continue; }

                Type cls = script.GetClass();
                if (cls == null) { continue; }

                for (int t = 0; t < targets.Length; t++)
                {
                    if (cls == targets[t] && !found.ContainsKey(cls)) { found.Add(cls, script); }
                }
            }
            return found;
        }
        #endregion
    }
}
#endif
