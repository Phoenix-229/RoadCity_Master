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
    /// <summary> RM-80's mechanism: the two per-file licence headers, and the pure, idempotent transform that
    /// stamps the correct one onto a source file. A file with no header is classified and gets that header
    /// prepended; a file that already carries a determinate header is left untouched (the transform never flips or
    /// doubles it); a file carrying BOTH headers is refused, never silently "fixed", because RM-79 names that the
    /// forbidden ambiguous state a human must resolve. `StampAllBatch` is the author-invoked run over the real tree;
    /// enforcing that every file ends up with exactly one header is RM-70's job, not this operator's.
    ///
    /// RM-106 changed what "classified" means here and nothing else. It used to be the path: a City/ file was
    /// proprietary and everything else was upstream MIT. That was written when Scripts/ held only vendored files, and
    /// § 10 has since put sixty-two of its own there -- the gates, the registers, the cross-section profiles. Stamped
    /// by path each of those would credit the upstream author with work they did not write, in a product sold under
    /// different terms, which is a false attribution in both directions. So the question asked is now provenance,
    /// from <see cref="UpstreamProvenanceRule"/>: was this file in the vendored download? The header still comes from
    /// <see cref="SourceLicence"/> and the transform is still the only one -- no second stamper was written. </summary>
    public static class LicenceHeaderOperator
    {
        // The two RM-80 header templates, verbatim from § 10.40. The first line of each carries the anchor phrase
        // RM-79's LicenceRule reads back, so a stamped file classifies to the licence it was stamped with.
        public const string ProprietaryHeader =
            "// New file, authored for this project — proprietary\n"
            + "// Copyright (c) 2026 <rights holder>. All rights reserved.\n"
            + "// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.\n"
            + "// Not open source. Redistribution of this file, in source or compiled form, is prohibited.\n";

        public const string MitHeader =
            "// Upstream file, MIT licensed — see RoadCityMaster/LICENSE-MIT.txt\n"
            + "// Modified for Road & City Master; modifications (c) 2026 <rights holder>.\n"
            + "// The original MIT notice and permission text are retained and are not superseded by LICENSE.md.\n";

        /// <summary> The header a file of the given licence must carry. </summary>
        public static string HeaderFor(SourceLicence licence)
        {
            return licence == SourceLicence.ProprietaryNew ? ProprietaryHeader : MitHeader;
        }

        /// <summary> Pure, deterministic, idempotent. Returns the file text with the correct header present.
        /// `changed` is true only when text was added; `note` explains the decision. A determinate header is
        /// preserved as-is; a Conflicting header is refused (returned unchanged, changed=false). </summary>
        public static string Stamp(string toolRelativePath, string fileText, out bool changed, out string note)
        {
            changed = false;
            LicenceHeaderState state = LicenceRule.HeaderStateOf(fileText);
            if (state == LicenceHeaderState.Conflicting)
            {
                note = "refused: file carries both headers (RM-79 ambiguous state; a human must resolve it)";
                return fileText;
            }
            if (state == LicenceHeaderState.ProprietaryOnly || state == LicenceHeaderState.MitModifiedOnly)
            {
                note = "already headed; left unchanged";
                return fileText;
            }
            LicenceAfterOverhaul verdict = UpstreamProvenanceRule.Resolve(toolRelativePath,
                UpstreamProvenanceRule.RemnantFor(toolRelativePath), false);
            SourceLicence licence = verdict.licence;
            changed = true;
            note = "stamped " + (licence == SourceLicence.ProprietaryNew ? "proprietary" : "upstream-MIT")
                + " header by provenance: " + verdict.note;
            return HeaderFor(licence) + fileText;
        }

        [MenuItem("Window/Road City Master/Licensing/Stamp per-file headers")]
        public static void StampAllFromMenu()
        {
            string report;
            StampAll(true, out report);
            Debug.Log(report);
        }

        public static void StampAllBatch()
        {
            string report;
            StampAll(true, out report);
            Debug.Log(report);
            EditorApplication.Exit(0);
        }

        /// <summary> Walks the real tool tree and stamps the missing headers. `write` false is a dry run (reports
        /// what it would do, touches nothing). Deterministic: files are visited in Ordinal order. Refused
        /// (Conflicting) files are reported by path and never modified. </summary>
        public static string StampAll(bool write, out string report)
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");
            List<string> files = EnumerateCs(root);
            int stamped = 0, already = 0, refused = 0;
            List<string> refusedPaths = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                string rel = ToRelative(root, files[i]);
                string text;
                try { text = File.ReadAllText(files[i]); }
                catch { continue; }
                bool changed;
                string note;
                string result = Stamp(rel, text, out changed, out note);
                if (note.StartsWith("refused")) { refused++; refusedPaths.Add(rel); continue; }
                if (changed)
                {
                    stamped++;
                    if (write) { File.WriteAllText(files[i], result); }
                }
                else { already++; }
            }
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("RCMLICSTAMP " + (write ? "WRITE" : "DRY-RUN"));
            sb.AppendLine("  files " + files.Count.ToString(CultureInfo.InvariantCulture)
                + "  stamped " + stamped.ToString(CultureInfo.InvariantCulture)
                + "  already " + already.ToString(CultureInfo.InvariantCulture)
                + "  refused " + refused.ToString(CultureInfo.InvariantCulture));
            for (int i = 0; i < refusedPaths.Count; i++) { sb.AppendLine("  REFUSED " + refusedPaths[i]); }
            report = sb.ToString();
            return report;
        }

        /// <summary> The tool's own .cs files as tool-relative paths, in Ordinal order. Public because RM-106's gate
        /// has to resolve a licence for every file in the shipped product, and a second tree walker would be a second
        /// answer to the question of what the product contains. </summary>
        public static List<string> ToolRelativeSources()
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster");
            List<string> files = EnumerateCs(root);
            List<string> relative = new List<string>(files.Count);
            for (int i = 0; i < files.Count; i++) { relative.Add(ToRelative(root, files[i])); }
            return relative;
        }

        private static List<string> EnumerateCs(string root)
        {
            List<string> found = new List<string>();
            if (!Directory.Exists(root)) { return found; }
            Collect(root, found);
            found.Sort(System.StringComparer.Ordinal);
            return found;
        }

        private static void Collect(string dir, List<string> found)
        {
            foreach (string file in Directory.GetFiles(dir, "*.cs")) { found.Add(file); }
            foreach (string sub in Directory.GetDirectories(dir))
            {
                string name = Path.GetFileName(sub);
                if (string.IsNullOrEmpty(name) || name[0] == '.' || name.EndsWith("~")) { continue; }
                Collect(sub, found);
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
