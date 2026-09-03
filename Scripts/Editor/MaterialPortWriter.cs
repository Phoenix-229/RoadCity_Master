#if UNITY_EDITOR
#region "Imports"
using System.IO;
using RoadCityMaster.City.Editor;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> RM-68's record, generated in the two places a port drifts: the recipe register and the Built-in to
    /// URP translation table.
    ///
    /// Two marker pairs, and still the one splicer -- <see cref="TerrainFeatureTableWriter"/>'s <c>FindBody</c> now
    /// serves thirteen tables across six documents. The path comes from <see cref="Application.dataPath"/>, never a
    /// literal (C-04). </summary>
    public static class MaterialPortWriter
    {
        public const string RecipesBegin = "<!-- port-recipes-table:begin";
        public const string RecipesEnd = "<!-- port-recipes-table:end -->";
        public const string MappingBegin = "<!-- port-mapping-table:begin";
        public const string MappingEnd = "<!-- port-mapping-table:end -->";

        public const int TableCount = 2;

        public static string DocPath()
        {
            return Path.Combine(Application.dataPath,
                Path.Combine("RoadCityMaster", Path.Combine("Docs", "MaterialPort.md")));
        }

        [MenuItem("Window/Road City Master/Documentation/Write the material port record")]
        public static void WriteFromMenu()
        {
            string message;
            bool written = WriteInto(DocPath(), out message);
            if (written) { Debug.Log(message); AssetDatabase.Refresh(); }
            else { Debug.LogError(message); }
        }

        public static string WantedBody(int index)
        {
            if (index == 0) { return "\n\n" + MaterialPortRecipes.Table() + "\n"; }
            return "\n\n" + MaterialPortRecipes.MappingTable() + "\n";
        }

        public static void MarkersAt(int index, out string begin, out string end)
        {
            if (index == 0) { begin = RecipesBegin; end = RecipesEnd; return; }
            begin = MappingBegin; end = MappingEnd;
        }

        /// <summary> Replace both marked spans with what the register generates. Refuses, with a reason, rather than
        /// rewriting a document whose markers it cannot find. </summary>
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
                message = "Both generated tables in '" + path + "' already match the register; nothing written.";
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
    }
}
#endif
