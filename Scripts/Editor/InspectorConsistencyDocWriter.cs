// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.IO;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-64's checklist document, generated from <see cref="InspectorConsistencyChecklist"/> and spliced
    /// between markers in <c>Docs/InspectorConsistency.md</c>, next to RM-49's findings as RM-64 asks.
    ///
    /// The splicing is <see cref="TerrainFeatureTableWriter"/>'s, reused for a third table rather than copied for a third
    /// time: finding a marked span and comparing on content rather than on whitespace is one problem, and it now has one
    /// implementation serving the availability table, the provenance table and this checklist. The path is built from
    /// <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
    public static class InspectorConsistencyDocWriter
    {
        public const string BeginMarker = "<!-- checklist-table:begin";
        public const string EndMarker = "<!-- checklist-table:end -->";

        /// <summary> The tool's own documentation folder -- the one holding RM-49's ScanFindings.md and RM-50's
        /// FieldCensus.md. </summary>
        public static string ChecklistPath()
        {
            return Path.Combine(Application.dataPath,
                Path.Combine("RoadCityMaster", Path.Combine("Docs", "InspectorConsistency.md")));
        }

        [MenuItem("Window/Road City Master/Documentation/Write the inspector consistency checklist")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(ChecklistPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        /// <summary> Replace whatever sits between the markers with the table the checklist would generate. Refuses, with
        /// a reason, rather than rewriting a document it does not recognise. </summary>
        public static bool WriteInto(string path, out string message)
        {
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!TerrainFeatureTableWriter.FindBody(existing, BeginMarker, EndMarker,
                    out bodyStart, out bodyEnd, out message))
            {
                return false;
            }

            string wanted = "\n\n" + InspectorConsistencyChecklist.ToManualTable() + "\n";
            string current = existing.Substring(bodyStart, bodyEnd - bodyStart);
            if (TerrainFeatureTableWriter.Normalise(current) == TerrainFeatureTableWriter.Normalise(wanted))
            {
                message = "The checklist in '" + path + "' already matches the register; nothing written.";
                return true;
            }

            string updated = existing.Substring(0, bodyStart) + wanted + existing.Substring(bodyEnd);
            File.WriteAllText(path, updated);
            message = "Wrote " + InspectorConsistencyChecklist.Count + " checklist rows into '" + path + "'.";
            return true;
        }

        /// <summary> The table currently in the file, for the gate to compare against the register. </summary>
        public static bool ReadTableFrom(string path, out string table, out string message)
        {
            table = string.Empty;
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!TerrainFeatureTableWriter.FindBody(existing, BeginMarker, EndMarker,
                    out bodyStart, out bodyEnd, out message))
            {
                return false;
            }

            table = existing.Substring(bodyStart, bodyEnd - bodyStart);
            message = "Read " + table.Length + " characters of checklist from '" + path + "'.";
            return true;
        }

        /// <summary> The directory the gate scans to prove the register covers every author-facing editor file. </summary>
        public static string EditorScriptFolder()
        {
            return Path.Combine(Application.dataPath,
                Path.Combine("RoadCityMaster", Path.Combine("Scripts", "Editor")));
        }
    }
}
#endif
