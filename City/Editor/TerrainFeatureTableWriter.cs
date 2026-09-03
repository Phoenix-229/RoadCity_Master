#if UNITY_EDITOR
#region "Imports"
using System.IO;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> Putting the availability table into the manual, rather than trusting anyone to retype it.
    ///
    /// § 10.24 wants one table with three consumers. Two of them are documents, and a document is where a copied table
    /// goes stale: someone adds a feature, updates the code and the inspector, and the manual keeps promising the old
    /// list for a year. So the manual carries markers, this writes between them, and the gate reads the same markers
    /// and fails if what is in the file is not what the code would generate. That makes drift a build failure instead
    /// of a support question.
    ///
    /// The path is built from <see cref="Application.dataPath"/>, never hard-coded (C-04). </summary>
    public static class TerrainFeatureTableWriter
    {
        public const string BeginMarker = "<!-- availability-table:begin";
        public const string EndMarker = "<!-- availability-table:end -->";

        /// <summary> The city layer's manual. </summary>
        public static string ManualPath()
        {
            return Path.Combine(Application.dataPath,
                Path.Combine("RoadCityMaster", Path.Combine("City",
                    Path.Combine("Documentation", "CityLayerManual.md"))));
        }

        /// <summary> The world side's own manual, which carries the same marker pair (UC-47).
        ///
        /// § 10.24's table has three consumers and two of them are documents. The integration chapter on the world
        /// side has to state exactly what this side states -- an integrator reading one document and a user reading
        /// the other must not be told different things about the same feature -- so it gets the generated table
        /// rather than a typed copy of it, from this one splicer. </summary>
        public static string WorldManualPath()
        {
            return Path.Combine(Application.dataPath,
                Path.Combine("UniverseCreator", "UniverseCreator_Documentation.md"));
        }

        /// <summary> Every document that carries the table, in the order the menu item writes them. Kept as one list
        /// so adding a third consumer means adding a path, not another copy of the write-and-report loop. </summary>
        public static string[] DocumentPaths()
        {
            return new string[] { ManualPath(), WorldManualPath() };
        }

        [MenuItem("Window/Road City Master/Documentation/Write the terrain availability table")]
        public static void WriteFromMenu()
        {
            string[] paths = DocumentPaths();
            bool allWritten = true;
            for (int i = 0; i < paths.Length; i++)
            {
                string message;
                bool written = WriteInto(paths[i], out message);
                if (written) { Debug.Log(message); }
                else { Debug.LogError(message); allWritten = false; }
            }
            if (allWritten) { AssetDatabase.Refresh(); }
        }

        /// <summary> Replace whatever sits between the markers with the table the code would generate. Returns false,
        /// with a reason, rather than writing a file it does not understand. </summary>
        public static bool WriteInto(string path, out string message)
        {
            string existing;
            if (!ReadFile(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!FindBody(existing, BeginMarker, EndMarker, out bodyStart, out bodyEnd, out message)) { return false; }

            string wanted = "\n\n" + TerrainFeatureAvailability.ToMarkdown() + "\n";
            string current = existing.Substring(bodyStart, bodyEnd - bodyStart);
            if (Normalise(current) == Normalise(wanted))
            {
                message = "The availability table in '" + path + "' already matches the code; nothing written.";
                return true;
            }

            string updated = existing.Substring(0, bodyStart) + wanted + existing.Substring(bodyEnd);
            File.WriteAllText(path, updated);
            message = "Wrote " + TerrainFeatureAvailability.Count + " availability rows into '" + path + "'.";
            return true;
        }

        /// <summary> The table currently in the file, for the gate to compare against the code. </summary>
        public static bool ReadTableFrom(string path, out string table, out string message)
        {
            table = string.Empty;
            string existing;
            if (!ReadFile(path, out existing, out message)) { return false; }

            int bodyStart, bodyEnd;
            if (!FindBody(existing, BeginMarker, EndMarker, out bodyStart, out bodyEnd, out message)) { return false; }

            table = existing.Substring(bodyStart, bodyEnd - bodyStart);
            message = "Read " + table.Length + " characters of table from '" + path + "'.";
            return true;
        }

        private static bool ReadFile(string path, out string contents, out string message)
        {
            contents = string.Empty;
            if (!File.Exists(path))
            {
                message = "No document at '" + path + "', so there is nothing to splice a generated table into.";
                return false;
            }
            contents = File.ReadAllText(path);
            message = string.Empty;
            return true;
        }

        /// <summary> The span between the end of the begin comment and the start of the end marker. Both markers must
        /// be present and in order; a document missing them is reported rather than rewritten by guesswork.
        ///
        /// The markers are parameters rather than this class's own constants because § 10.27's provenance register keeps
        /// a second generated table in the same manual: two marker pairs, one splicer. A copy of this method with the
        /// other pair's names in it would be precisely the duplication these tables exist to prevent. </summary>
        public static bool FindBody(string contents, string beginMarker, string endMarker,
            out int bodyStart, out int bodyEnd, out string message)
        {
            bodyStart = 0;
            bodyEnd = 0;

            int begin = contents.IndexOf(beginMarker);
            if (begin < 0)
            {
                message = "The document has no '" + beginMarker + "' marker.";
                return false;
            }

            int beginClose = contents.IndexOf("-->", begin);
            if (beginClose < 0)
            {
                message = "The begin marker is never closed with '-->'.";
                return false;
            }

            int end = contents.IndexOf(endMarker, beginClose);
            if (end < 0)
            {
                message = "The document has no '" + endMarker + "' marker after the begin marker.";
                return false;
            }

            bodyStart = beginClose + 3;
            bodyEnd = end;
            message = string.Empty;
            return true;
        }

        /// <summary> Reading a whole document, or a reason it could not be read. Shared with the provenance writer so
        /// that both report a missing manual in the same words. </summary>
        public static bool ReadDocument(string path, out string contents, out string message)
        {
            return ReadFile(path, out contents, out message);
        }

        /// <summary> Compare on content, not on whitespace: an editor that trims trailing spaces or rewrites line
        /// endings must not read as a drifted table. </summary>
        public static string Normalise(string text)
        {
            if (text == null) { return string.Empty; }
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            bool atLineStart = true;
            int pendingNewlines = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r') { continue; }
                if (c == '\n') { pendingNewlines++; atLineStart = true; continue; }
                if ((c == ' ' || c == '\t') && atLineStart) { continue; }
                if (pendingNewlines > 0 && sb.Length > 0) { sb.Append('\n'); }
                pendingNewlines = 0;
                atLineStart = false;
                sb.Append(c);
            }
            return TrimTrailingSpaces(sb.ToString());
        }

        private static string TrimTrailingSpaces(string text)
        {
            string[] lines = text.Split('\n');
            System.Text.StringBuilder sb = new System.Text.StringBuilder(text.Length);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) { sb.Append('\n'); }
                sb.Append(lines[i].TrimEnd());
            }
            return sb.ToString();
        }
    }
}
#endif
