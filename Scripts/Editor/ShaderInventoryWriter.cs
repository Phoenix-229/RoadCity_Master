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
    /// <summary> RM-67's inventory, generated in the four places a shader audit drifts: the asset inventory, the
    /// project's own pipeline settings, the port gotchas this project has already paid for, and the material paths
    /// something loads that are not on disk.
    ///
    /// Four marker pairs, and still the one splicer -- <see cref="TerrainFeatureTableWriter"/>'s <c>FindBody</c> now
    /// serves eleven tables across five documents. Each splice is applied to the result of the previous one, in
    /// document order, because splicing shifts every offset after it. The path comes from
    /// <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
    public static class ShaderInventoryWriter
    {
        public const string InventoryBegin = "<!-- inventory-table:begin";
        public const string InventoryEnd = "<!-- inventory-table:end -->";
        public const string FactsBegin = "<!-- pipeline-facts-table:begin";
        public const string FactsEnd = "<!-- pipeline-facts-table:end -->";
        public const string GotchasBegin = "<!-- gotchas-table:begin";
        public const string GotchasEnd = "<!-- gotchas-table:end -->";
        public const string MissingBegin = "<!-- missing-material-table:begin";
        public const string MissingEnd = "<!-- missing-material-table:end -->";

        public const int TableCount = 4;

        public static string DocPath()
        {
            return Path.Combine(DispositionDocWriter.DocsRoot(), "ShaderInventory.md");
        }

        [MenuItem("Window/Road City Master/Documentation/Write the shader and material inventory")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(DocPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        public static string WantedBody(int index)
        {
            if (index == 0) { return "\n\n" + ShaderInventory.Table() + "\n"; }
            if (index == 1) { return "\n\n" + PipelineAudit.FactsTable() + "\n"; }
            if (index == 2) { return "\n\n" + PipelineAudit.GotchasTable() + "\n"; }
            return "\n\n" + PipelineAudit.MissingTable() + "\n";
        }

        public static void MarkersAt(int index, out string begin, out string end)
        {
            if (index == 0) { begin = InventoryBegin; end = InventoryEnd; return; }
            if (index == 1) { begin = FactsBegin; end = FactsEnd; return; }
            if (index == 2) { begin = GotchasBegin; end = GotchasEnd; return; }
            begin = MissingBegin; end = MissingEnd;
        }

        /// <summary> Replace all four marked spans with what the registers generate. Refuses, with a reason, rather
        /// than rewriting a document whose markers it cannot find. </summary>
        public static bool WriteInto(string path, out string message)
        {
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            string updated = existing;
            int rewritten = 0;
            for (int i = 0; i < TableCount; i++)
            {
                string begin, end;
                MarkersAt(i, out begin, out end);

                int bodyStart, bodyEnd;
                if (!TerrainFeatureTableWriter.FindBody(updated, begin, end, out bodyStart, out bodyEnd, out message))
                {
                    return false;
                }

                string wanted = WantedBody(i);
                string current = updated.Substring(bodyStart, bodyEnd - bodyStart);
                if (TerrainFeatureTableWriter.Normalise(current) == TerrainFeatureTableWriter.Normalise(wanted))
                {
                    continue;
                }

                updated = updated.Substring(0, bodyStart) + wanted + updated.Substring(bodyEnd);
                rewritten++;
            }

            if (rewritten == 0)
            {
                message = "All " + TableCount + " generated tables in '" + path + "' already match the registers; "
                    + "nothing written.";
                return true;
            }

            File.WriteAllText(path, updated);
            message = "Rewrote " + rewritten + " of " + TableCount + " generated tables in '" + path + "'.";
            return true;
        }

        public static bool ReadTableFrom(string path, int index, out string table, out string message)
        {
            table = string.Empty;
            string existing;
            if (!TerrainFeatureTableWriter.ReadDocument(path, out existing, out message)) { return false; }

            string begin, end;
            MarkersAt(index, out begin, out end);

            int bodyStart, bodyEnd;
            if (!TerrainFeatureTableWriter.FindBody(existing, begin, end, out bodyStart, out bodyEnd, out message))
            {
                return false;
            }

            table = existing.Substring(bodyStart, bodyEnd - bodyStart);
            message = "Read " + table.Length + " characters from marker pair " + index + " of '" + path + "'.";
            return true;
        }

        public static bool ReadWholeDoc(string path, out string contents, out string message)
        {
            return TerrainFeatureTableWriter.ReadDocument(path, out contents, out message);
        }

        /// <summary> The tool folder, so the gate can prove every asset the inventory names is on disk. </summary>
        public static string ToolRoot()
        {
            return Path.Combine(Application.dataPath, "RoadCityMaster");
        }
    }
}
#endif
