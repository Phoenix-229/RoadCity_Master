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
    /// <summary> RM-65's manual, generated in the four places where a manual drifts: the field reference, the refusal
    /// reference, the cited-standards table and the menu map. Everything else in <c>Docs/Manual.md</c> is prose and is
    /// written by hand, because prose that explains why a divided road has two carriageways cannot be generated from a
    /// register and pretending otherwise produces a feature tour nobody reads.
    ///
    /// The menu map is the fourth because of B-20: the manual twice stated a menu total that was wrong, and both numbers
    /// came from counting the tree by hand. <see cref="MenuMapRegister"/> reflects over the attributes Unity itself
    /// reads, so the count in the document is the count in the code -- including the five paths declared as a
    /// <c>const string</c>, which no text search for the attribute would have found.
    ///
    /// Four marker pairs, one splicer: <see cref="TerrainFeatureTableWriter"/>'s <c>FindBody</c> already serves the
    /// availability table, the provenance table and RM-64's checklist, and it serves these four too. Each splice is
    /// applied to the result of the previous one, in document order, because splicing shifts every offset after it.
    /// The path is built from <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
    public static class ToolManualWriter
    {
        public const string FieldsBegin = "<!-- fields-table:begin";
        public const string FieldsEnd = "<!-- fields-table:end -->";
        public const string RefusalsBegin = "<!-- refusals-table:begin";
        public const string RefusalsEnd = "<!-- refusals-table:end -->";
        public const string StandardsBegin = "<!-- standards-table:begin";
        public const string StandardsEnd = "<!-- standards-table:end -->";
        public const string MenusBegin = "<!-- menus-table:begin";
        public const string MenusEnd = "<!-- menus-table:end -->";


        /// <summary> The manual, beside RM-49's findings, RM-50's census and RM-64's checklist. </summary>
        public static string ManualPath()
        {
            return Path.Combine(DispositionDocWriter.DocsRoot(), "Manual.md");
        }

        [MenuItem("Window/Road City Master/Documentation/Write the manual's generated tables")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(ManualPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        /// <summary> The generated body each marker pair should hold, in document order. </summary>
        public static string WantedBody(int index)
        {
            if (index == 0) { return "\n\n" + ManualReferences.FieldsTable() + "\n"; }
            if (index == 1) { return "\n\n" + ManualReferences.RefusalsTable() + "\n"; }
            if (index == 2) { return "\n\n" + ManualReferences.StandardsTable() + "\n"; }
            return "\n\n" + MenuMapRegister.MapTable() + "\n";
        }

        /// <summary> The begin and end marker of the pair at <paramref name="index"/>. </summary>
        public static void MarkersAt(int index, out string begin, out string end)
        {
            if (index == 0) { begin = FieldsBegin; end = FieldsEnd; return; }
            if (index == 1) { begin = RefusalsBegin; end = RefusalsEnd; return; }
            if (index == 2) { begin = StandardsBegin; end = StandardsEnd; return; }
            begin = MenusBegin; end = MenusEnd;
        }

        public const int TableCount = 4;

        /// <summary> Replace all four marked spans with what the registers generate. Refuses, with a reason, rather
        /// than rewriting a document whose markers it cannot find. Returns true when the file matched already. </summary>
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

        /// <summary> One marked span as the file currently holds it, for the gate to compare against the register. </summary>
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

        /// <summary> The whole document, for the gate's checks on the hand-written prose. </summary>
        public static bool ReadWholeManual(string path, out string contents, out string message)
        {
            return TerrainFeatureTableWriter.ReadDocument(path, out contents, out message);
        }
    }
}
#endif
