// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.IO;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> Putting § 10.27's provenance register into the manual, between markers, so the written answer to "which
    /// component built this?" cannot drift away from the code that answers it.
    ///
    /// RM-62 asks for a findings document naming, for each of RM-26 ... RM-61, which component produced its geometry. A
    /// findings document that is retyped by hand is a findings document that is wrong within a month: someone adds a
    /// feature, the register grows a row, and the prose keeps describing the old set. So the register is the source, this
    /// splices it in, and the gate fails the build when the file and the code disagree.
    ///
    /// The splicing itself -- finding the marked span, and comparing on content rather than on whitespace -- is
    /// <see cref="TerrainFeatureTableWriter"/>'s, deliberately reused rather than copied: two tables in one manual
    /// should not mean two implementations of the same edit. The path comes from
    /// <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
    public static class GeometryProvenanceTableWriter
    {
        public const string BeginMarker = "<!-- provenance-table:begin";
        public const string EndMarker = "<!-- provenance-table:end -->";

        /// <summary> The same manual the availability table lives in. </summary>
        public static string ManualPath()
        {
            return TerrainFeatureTableWriter.ManualPath();
        }

        [MenuItem("Window/Road City Master/Documentation/Write the geometry provenance table")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(ManualPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        /// <summary> Replace whatever sits between the markers with the table the register would generate. Returns false,
        /// with a reason, rather than rewriting a document it does not recognise. </summary>
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

            string wanted = "\n\n" + GeometryProvenanceRegister.ToManualTable() + "\n";
            string current = existing.Substring(bodyStart, bodyEnd - bodyStart);
            if (TerrainFeatureTableWriter.Normalise(current) == TerrainFeatureTableWriter.Normalise(wanted))
            {
                message = "The provenance table in '" + path + "' already matches the register; nothing written.";
                return true;
            }

            string updated = existing.Substring(0, bodyStart) + wanted + existing.Substring(bodyEnd);
            File.WriteAllText(path, updated);
            message = "Wrote " + GeometryProvenanceRegister.Count + " provenance rows into '" + path + "'.";
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
            message = "Read " + table.Length + " characters of provenance table from '" + path + "'.";
            return true;
        }
    }
}
#endif
