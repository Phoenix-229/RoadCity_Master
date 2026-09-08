// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> One offending line, named so a failure can be opened rather than searched for. </summary>
    public struct SweepFinding
    {
        public readonly string file;
        public readonly int line;
        public readonly string text;
        public readonly string reason;

        public SweepFinding(string file, int line, string text, string reason)
        {
            this.file = file;
            this.line = line;
            this.text = text;
            this.reason = reason;
        }

        public override string ToString()
        {
            return file + ":" + line.ToString(System.Globalization.CultureInfo.InvariantCulture)
                   + "  " + reason + "  |  " + text;
        }
    }


    /// <summary> What one sweep found in one tree. Carries the COUNTS as well as the findings, because the failure
    /// mode this whole task exists to close is a sweep whose path stopped resolving after a rename: it then walks
    /// nothing, finds nothing, and reports success. A result with no findings and no items checked is a FAILURE.
    ///
    /// A sweep whose rule is already owned by another gate carries that gate's prefix in <see cref="owner"/> and is
    /// not run here -- two scripts asserting the same rule is how the two answers drift apart. </summary>
    public sealed class SweepResult
    {
        public readonly int id;
        public readonly string title;
        public readonly string tree;
        public readonly string owner;
        public readonly List<SweepFinding> findings;
        public readonly List<SweepFinding> deferrals;
        public int filesScanned;
        public int itemsChecked;

        public SweepResult(int id, string title, string tree, string owner)
        {
            this.id = id;
            this.title = title;
            this.tree = tree;
            this.owner = owner;
            this.findings = new List<SweepFinding>();
            this.deferrals = new List<SweepFinding>();
        }

        /// <summary> True when this sweep is executed here rather than delegated to the gate that owns its rule. </summary>
        public bool Swept { get { return owner.Length == 0; } }

        /// <summary> A delegated sweep passes on its owner's verdict, not this one's. A swept one passes only when it
        /// found nothing AND actually looked at something. </summary>
        public bool Passed
        {
            get { return findings.Count == 0 && (!Swept || itemsChecked > 0); }
        }

        public void Add(string file, int line, string text, string reason)
        {
            findings.Add(new SweepFinding(file, line, Clip(text), reason));
        }

        public void Defer(string file, int line, string text, string reason)
        {
            deferrals.Add(new SweepFinding(file, line, Clip(text), reason));
        }

        private static string Clip(string text)
        {
            string trimmed = text.Trim();
            return trimmed.Length <= 120 ? trimmed : trimmed.Substring(0, 117) + "...";
        }

        /// <summary> The one report line per sweep per tree. Named the tree and the counts on purpose: a merged
        /// pass/fail tells nobody where to look, and a violation in the vendored tree gets dismissed as pre-existing
        /// unless the line says which tree it came from. </summary>
        public string Line()
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(id.ToString(System.Globalization.CultureInfo.InvariantCulture).PadRight(3))
                   .Append(title.PadRight(20))
                   .Append("tree ").Append(tree.PadRight(16));
            if (Swept)
            {
                builder.Append("files ").Append(filesScanned.ToString(
                           System.Globalization.CultureInfo.InvariantCulture).PadLeft(5))
                       .Append("  items ").Append(itemsChecked.ToString(
                           System.Globalization.CultureInfo.InvariantCulture).PadLeft(7))
                       .Append("  found ").Append(findings.Count.ToString(
                           System.Globalization.CultureInfo.InvariantCulture).PadLeft(3))
                       .Append("  deferred ").Append(deferrals.Count.ToString(
                           System.Globalization.CultureInfo.InvariantCulture).PadLeft(3));
            }
            else
            {
                builder.Append("owned by ").Append(owner);
            }
            return builder.ToString();
        }
    }
    /// <summary> RM-70: the ship-audit sweeps of § 7, made tree-parameterised so the road tool is measured by the same
    /// standard as the rest of the project rather than by a separate, weaker convention.
    ///
    /// Two rules decide whether extending a sweep to a second tree is worth anything, and both are enforced here.
    /// FIRST, per-tree reporting: every result names its tree and its counts, because a merged verdict tells nobody
    /// where to look and a finding in the vendored tree is otherwise waved away as pre-existing. SECOND, a sweep that
    /// matched ZERO items is a failure, not a pass -- a path that stopped resolving after a rename walks nothing,
    /// finds nothing, and reports success, which is the most expensive kind of green there is.
    ///
    /// The sweeps whose rule another gate already owns are NOT re-implemented: they carry that gate's prefix and pass
    /// on its verdict. Sweep 38's assembly half belongs to `RCMAUDIT`, which reads the compilation pipeline and so
    /// knows more than a text scan can; sweep 39 belongs to `RCMDET`. What is left over is text-level, and text-level
    /// is exactly what no other gate looks at: an API name in a runtime file, a product name in a comment, a
    /// drive-letter path in a string, a modal dialog on a code path that a player build can reach.
    ///
    /// Every scan splits into a pure `Scan…(file, text, result)` over text and a walk that feeds it files, so the
    /// gate can hand a sweep a synthetic violation and prove it is caught without writing anything to disk. </summary>
    public static class ShipAuditSweeps
    {
        /// <summary> This file necessarily contains every needle the sweeps look for, so the walk skips it by name.
        /// Keeping all the needles in ONE file is what makes that exclusion a single line rather than a list. </summary>
        public const string OwnSourceFileName = "ShipAuditSweeps.cs";

        /// <summary> The gate that owns sweep 38's assembly half: it asks the compilation pipeline which assembly
        /// compiles which file, which is a stronger question than any text scan can put. </summary>
        public const string AssemblyOwnerPrefix = "RCMAUDIT";

        /// <summary> The gate that owns sweep 39: byte-for-byte equality of a re-run generation. </summary>
        public const string DeterminismOwnerPrefix = "RCMDET";

        /// <summary> Sweep 33. Managed C# only (C-01) and no unseeded randomness on a generation path (C-06). </summary>
        private static readonly string[] ForbiddenApis =
        {
            "Unity.Mathematics",
            "Unity.Collections",
            "NativeArray",
            "BurstCompile",
            "RegisterGenericJobType",
            "UnityEngine.Random",
            "GetInstanceID()",
        };

        /// <summary> The pre-rename identifier, in fragments for the same reason. </summary>
        private const string Vendor = "Road" + "Architect";

        /// <summary> Sweep 34's needles, assembled from fragments so this file does not itself become the occurrence
        /// it is looking for -- the project's leftover-name scan reads source as text and would report it. </summary>
        private static readonly string[] ForeignProductNames =
        {
            Vendor,
            "Road" + " " + "Architect",
            "Map" + "Magic",
        };

        /// <summary> Lines that legitimately carry the old name, each with the reason it stays. They are not a
        /// convenience: an upstream URL, an offline manual's filename, a legacy record separator that shipped presets
        /// still contain and a `BinaryFormatter` binder fallback for scenes saved before the rename are all cases where
        /// changing the string would break something real. A sweep with no such list gets switched off instead. </summary>
        private static readonly string[][] ProductNameKeeps =
        {
            new[] { "github.com/", "upstream repository, wiki and issue links" },
            new[] { "microgsd.com", "upstream support page" },
            new[] { Vendor + "Manual", "the offline manual's own filename" },
            new[] { "embeddedt/" + Vendor + "/issues", "upstream bug reference in a comment" },
            new[] { "legacyNamespacePrefix", "type-name fallback for files saved before the rename" },
            new[] { Vendor + "System", "root object name in scenes and prefabs saved before the rename" },
            new[] { "FileSepStringLegacy", "record separator inside shipped preset files, still read" },
            new[] { Vendor + " -> RoadCityMaster", "a comment naming the rename itself" },
            new[] { "\"/" + Vendor + "/\"", "one-time migration of the pre-rename folder" },
            new[] { "\"" + Vendor + " Repository\"", "button text describing the upstream repository" },
            new[] { "\"" + Vendor + " Issues\"", "button text describing the upstream issue tracker" },
            new[] { "#### " + Vendor + " ####", "legacy record separator in shipped presets" },
        };

        /// <summary> Occurrences in the OTHER tree that this module is forbidden to touch. R-5 says no RM-nn task edits
        /// a file under the host product's folder, so these are recorded with their reason and counted separately --
        /// neither passed off as clean nor failed for a rule this task may not act on. </summary>
        private static readonly string[][] RecordedDeferrals =
        {
            new[] { "UCWaterBodyInspector.cs", "prose comparing an authoring gesture; R-5 forbids editing it here" },
            new[] { "UCWaterBody.cs", "prose comparing a node's grade readout; R-5 forbids editing it here" },
        };

        /// <summary> Sweep 35. A drive letter is caught by shape rather than by literal; these two cover the profile
        /// folder every authored path on this machine passes through, in both the verbatim (`@"C:\Users\"`) and escaped
        /// (`"C:\\Users\\"`) source forms.
        ///
        /// Two markers were considered and rejected, because this scan reads SOURCE TEXT rather than string values.
        /// `Desktop` on its own is a quality-tier name in both trees (`SplatCacheTier.Desktop`, the tooltip string
        /// "Desktop/Ultra"), and a UNC prefix is indistinguishable in source from an ordinary escaped separator --
        /// `Replace('\\', '/')` would report as a network path. Neither adds reach a drive letter does not already
        /// have, and both manufacture failures nobody is allowed to fix. </summary>
        private static readonly string[] AbsolutePathMarkers =
        {
            "Users/",
            "Users\\",
        };
        /// <summary> The tool's own tree. Derived from <see cref="Application.dataPath"/>, never a literal (C-04). </summary>
        public static string ToolTreeRoot()
        {
            return ShaderInventoryWriter.ToolRoot();
        }

        /// <summary> The host product's tree, swept read-only. It is named because RM-70 asks for both trees measured
        /// by the same standard; nothing here writes to it, which is what R-5 requires. </summary>
        public static string HostTreeRoot()
        {
            return Path.Combine(Application.dataPath, "UniverseCreator");
        }

        /// <summary> The label a report line carries for a tree: its folder name, so the line reads as the folder an
        /// author can open. </summary>
        public static string TreeLabel(string root)
        {
            return Path.GetFileName(root.TrimEnd('/', '\\'));
        }

        /// <summary> A line whose content is entirely a comment. Prose that NAMES a forbidden API -- "no
        /// `UnityEngine.Random` ever enters this" -- is the documentation of the rule, not a breach of it, and a scan
        /// that cannot tell the two apart is a scan whose failures get ignored. </summary>
        public static bool IsCommentOnly(string line)
        {
            string t = line.TrimStart();
            return t.StartsWith("//", StringComparison.Ordinal)
                || t.StartsWith("*", StringComparison.Ordinal)
                || t.StartsWith("/*", StringComparison.Ordinal);
        }

        /// <summary> An absolute path by SHAPE: a drive letter, a colon and a separator, where the letter does not
        /// continue a word. The word test is what keeps `http://` and `file://` out of the findings. </summary>
        public static bool HasDriveLetter(string line)
        {
            for (int i = 0; i + 2 < line.Length; i++)
            {
                char letter = line[i];
                bool alpha = (letter >= 'A' && letter <= 'Z') || (letter >= 'a' && letter <= 'z');
                if (!alpha || line[i + 1] != ':') { continue; }
                if (line[i + 2] != '/' && line[i + 2] != '\\') { continue; }
                if (i > 0)
                {
                    char before = line[i - 1];
                    bool word = (before >= 'A' && before <= 'Z') || (before >= 'a' && before <= 'z')
                                || (before >= '0' && before <= '9') || before == '_';
                    if (word) { continue; }
                }
                return true;
            }
            return false;
        }
        /// <summary> Splits source into lines without caring which line ending the file uses; a mixed-ending tree is
        /// normal in a vendored folder and must not shift a reported line number. </summary>
        public static string[] SplitLines(string text)
        {
            return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        }

        /// <summary> True when a source file is editor-side by location. The folder is the boundary the assembly split
        /// uses, so a text sweep asking the same question gets the same answer. </summary>
        public static bool IsEditorSideFile(string file)
        {
            string p = file.Replace('\\', '/');
            return p.Contains("/Editor/") || p.EndsWith("/Editor", StringComparison.Ordinal);
        }

        /// <summary> Sweep 33 over one file. Comment-only lines are counted as checked but never reported: naming the
        /// banned API in the sentence that bans it is the documentation, not the breach. </summary>
        public static void ScanForbiddenApi(string file, string text, SweepResult result)
        {
            string[] lines = SplitLines(text);
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                if (IsCommentOnly(lines[i])) { continue; }
                for (int n = 0; n < ForbiddenApis.Length; n++)
                {
                    if (lines[i].IndexOf(ForbiddenApis[n], StringComparison.Ordinal) < 0) { continue; }
                    result.Add(file, i + 1, lines[i], "forbidden API " + ForbiddenApis[n]);
                }
            }
        }

        /// <summary> Sweep 34 over one file. Three outcomes, not two: a breach, a line whose old name is correct as it
        /// stands (an upstream URL, a legacy record separator), and a file this module is forbidden to edit at all. The
        /// middle two are recorded with their reason and counted apart from the breaches. </summary>
        public static void ScanProductName(string file, string text, SweepResult result)
        {
            string[] lines = SplitLines(text);
            string name = Path.GetFileName(file);
            string deferral = DeferralReason(name);
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                string hit = FirstForeignName(lines[i]);
                if (hit == null) { continue; }
                if (deferral != null) { result.Defer(file, i + 1, lines[i], deferral); continue; }
                string keep = KeepReason(lines[i]);
                if (keep != null) { result.Defer(file, i + 1, lines[i], "kept: " + keep); continue; }
                result.Add(file, i + 1, lines[i], "foreign product name " + hit);
            }
        }

        /// <summary> The first foreign product name on a line, case-insensitively, or null. Case-insensitive because
        /// the spaced form appears in prose with any capitalisation and a sweep that only matched one is theatre. </summary>
        public static string FirstForeignName(string line)
        {
            for (int n = 0; n < ForeignProductNames.Length; n++)
            {
                if (line.IndexOf(ForeignProductNames[n], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return ForeignProductNames[n];
                }
            }
            return null;
        }

        private static string KeepReason(string line)
        {
            for (int k = 0; k < ProductNameKeeps.Length; k++)
            {
                if (line.IndexOf(ProductNameKeeps[k][0], StringComparison.Ordinal) >= 0)
                {
                    return ProductNameKeeps[k][1];
                }
            }
            return null;
        }

        private static string DeferralReason(string fileName)
        {
            for (int d = 0; d < RecordedDeferrals.Length; d++)
            {
                if (RecordedDeferrals[d][0] == fileName) { return "deferred: " + RecordedDeferrals[d][1]; }
            }
            return null;
        }
        /// <summary> Tracks whether the line being read sits inside a `#if UNITY_EDITOR` branch, nesting included. It
        /// exists because "the call is wrapped" is the whole difference between an editor-only convenience and a modal
        /// dialog a player build can reach, and that difference is invisible without following the directives. `#else`
        /// sets the branch to NOT editor-only: the other half of an editor guard is the runtime half. </summary>
        public sealed class EditorGuard
        {
            private readonly List<bool> stack = new List<bool>();

            public bool Guarded
            {
                get
                {
                    for (int i = 0; i < stack.Count; i++) { if (stack[i]) { return true; } }
                    return false;
                }
            }

            /// <summary> True when the line is a preprocessor directive, which is bookkeeping rather than code. </summary>
            public bool Feed(string line)
            {
                string t = line.TrimStart();
                if (t.Length == 0 || t[0] != '#') { return false; }
                bool editorOnly = t.IndexOf("UNITY_EDITOR", StringComparison.Ordinal) >= 0;
                if (t.StartsWith("#if", StringComparison.Ordinal)) { stack.Add(editorOnly); return true; }
                if (stack.Count == 0) { return true; }
                if (t.StartsWith("#elif", StringComparison.Ordinal)) { stack[stack.Count - 1] = editorOnly; return true; }
                if (t.StartsWith("#else", StringComparison.Ordinal)) { stack[stack.Count - 1] = false; return true; }
                if (t.StartsWith("#endif", StringComparison.Ordinal)) { stack.RemoveAt(stack.Count - 1); return true; }
                return true;
            }
        }

        /// <summary> Sweep 35 over one file: a drive letter by shape, or one of the folder names an authored absolute
        /// path on an author's own machine always passes through. Comment-only lines are exempt -- a comment giving an
        /// example path is documentation; the string literal a build would read is the breach. </summary>
        public static void ScanAbsolutePath(string file, string text, SweepResult result)
        {
            string[] lines = SplitLines(text);
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                if (IsCommentOnly(lines[i])) { continue; }
                if (HasDriveLetter(lines[i]))
                {
                    result.Add(file, i + 1, lines[i], "absolute path: drive letter in a literal");
                    continue;
                }
                for (int m = 0; m < AbsolutePathMarkers.Length; m++)
                {
                    if (lines[i].IndexOf(AbsolutePathMarkers[m], StringComparison.Ordinal) < 0) { continue; }
                    result.Add(file, i + 1, lines[i], "absolute path: contains " + AbsolutePathMarkers[m]);
                }
            }
        }

        /// <summary> Sweep 36 over one file. A modal dialog is not banned outright -- an author who just asked to delete
        /// something is owed a confirmation -- so the rule is placement, not absence: a dialog reachable from a runtime
        /// path outside `#if UNITY_EDITOR` is a breach, and every editor-side one is recorded so the census exists
        /// rather than living in a reviewer's memory. </summary>
        public static void ScanModalDialog(string file, string text, SweepResult result)
        {
            string[] lines = SplitLines(text);
            bool editorSide = IsEditorSideFile(file);
            EditorGuard guard = new EditorGuard();
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                if (guard.Feed(lines[i])) { continue; }
                if (IsCommentOnly(lines[i])) { continue; }
                if (lines[i].IndexOf("EditorUtility.DisplayDialog", StringComparison.Ordinal) < 0) { continue; }
                if (editorSide)
                {
                    result.Defer(file, i + 1, lines[i], "kept: editor-side, author-initiated");
                }
                else if (guard.Guarded)
                {
                    result.Defer(file, i + 1, lines[i], "kept: runtime file, inside an editor guard");
                }
                else
                {
                    result.Add(file, i + 1, lines[i], "modal dialog on a path a player build can reach");
                }
            }
        }

        /// <summary> Sweep 38's text half: every `UnityEditor` use in a runtime file must sit inside `#if UNITY_EDITOR`.
        /// The assembly half -- which assembly compiles which file -- belongs to <see cref="AssemblyOwnerPrefix"/>,
        /// which asks the compilation pipeline and therefore knows what no text scan can work out. </summary>
        public static void ScanEditorUseInRuntime(string file, string text, SweepResult result)
        {
            if (IsEditorSideFile(file)) { return; }
            string[] lines = SplitLines(text);
            EditorGuard guard = new EditorGuard();
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                if (guard.Feed(lines[i])) { continue; }
                if (guard.Guarded) { continue; }
                if (IsCommentOnly(lines[i])) { continue; }
                bool uses = lines[i].IndexOf("UnityEditor.", StringComparison.Ordinal) >= 0
                         || lines[i].IndexOf("using UnityEditor", StringComparison.Ordinal) >= 0;
                if (!uses) { continue; }
                result.Add(file, i + 1, lines[i], "editor API in a runtime file outside an editor guard");
            }
        }

        /// <summary> The declaration a line makes when it means to instance. A sweep with no way to say "yes, on
        /// purpose" is a sweep that gets deleted the first time someone legitimately needs an instance. </summary>
        public const string InstanceIntentMarker = "instanced-on-purpose";

        /// <summary> Sweep 40 over one file. `material` and `materials` on a Renderer -- and the same pair on a
        /// Collider -- are INSTANCING accessors: the setter copies every material handed to it and gives the component
        /// the copies. A build path that assigns the author's material ASSETS through them produces a road that no
        /// longer answers to the materials the author picked; the copies are not assets, so they do not survive a
        /// domain reload, and each rebuild mints a fresh set that nothing frees. The by-reference spellings are the
        /// shared ones, and they are what a build path must use.
        ///
        /// This swept the tool tree back to zero and exists so it stays there. One `materials =` reintroduced in a
        /// mesh helper reads as correct and shows up only as memory that climbs while an author edits, which is
        /// precisely the kind of defect review does not catch and a text rule does.
        ///
        /// A deliberate instance is still allowed, but it has to say so on the line. That is how the traffic-light
        /// reads stay in the census rather than being quietly exempted: those three MUST own their material, because
        /// they move `mainTextureOffset`, and writing that onto a shared asset would re-aim every light in the project
        /// and dirty the file on disk. </summary>
        public static void ScanRendererInstancing(string file, string text, SweepResult result)
        {
            string[] lines = SplitLines(text);
            result.filesScanned++;
            for (int i = 0; i < lines.Length; i++)
            {
                result.itemsChecked++;
                if (IsCommentOnly(lines[i])) { continue; }
                string spelling = InstancingAccessorOn(lines[i]);
                if (spelling == null) { continue; }
                if (lines[i].IndexOf(InstanceIntentMarker, StringComparison.Ordinal) >= 0)
                {
                    result.Defer(file, i + 1, lines[i], "kept: " + spelling + " instanced on purpose, declared here");
                    continue;
                }
                result.Add(file, i + 1, lines[i],
                    "instancing accessor " + spelling + " -- use the shared spelling, or declare "
                    + InstanceIntentMarker + " on the line");
            }
        }

        /// <summary> The instancing accessor a line uses, or null. Matched on the lower-case member name after a dot,
        /// which is what separates it from the shared spellings and from every field whose name merely ends in
        /// "Material": the next character has to end the identifier, so `materialCount` and `materialsOnEntry` are not
        /// hits and `sharedMaterial` never was one. </summary>
        public static string InstancingAccessorOn(string line)
        {
            string needle = "." + "material";
            int at = 0;
            while (at < line.Length)
            {
                at = line.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0) { return null; }
                int after = at + needle.Length;
                bool plural = after < line.Length && line[after] == 's';
                if (plural) { after++; }
                if (after >= line.Length || !IsIdentifierCharacter(line[after]))
                {
                    return plural ? needle + "s" : needle;
                }
                at = after;
            }
            return null;
        }

        /// <summary> True for a character that can continue a C# identifier, so a member name can be told from a
        /// longer name that merely starts with it. </summary>
        public static bool IsIdentifierCharacter(char c)
        {
            return c == '_' || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
        }

        /// <summary> Folders that are build output or version-control bookkeeping rather than authored content. Walking
        /// them reports generated files as defects and slows the sweep down for nothing. </summary>
        private static readonly string[] SkipFolders = { "obj", "Temp", "Logs", "ExportedObj" };

        /// <summary> A path Unity ignores: any segment starting with a dot. `.gitignore` and `.github/` live under the
        /// tool and correctly have no `.meta`, so a pairing check that does not skip them reports the version-control
        /// files as broken assets. Public because two callers need the same answer. </summary>
        public static bool IsHiddenFromUnity(string fullPath, string root)
        {
            string relative = fullPath.Substring(root.Length).Replace('\\', '/');
            string[] segments = relative.Split('/');
            for (int i = 0; i < segments.Length; i++)
            {
                if (segments[i].Length > 0 && segments[i][0] == '.') { return true; }
                for (int s = 0; s < SkipFolders.Length; s++)
                {
                    if (segments[i] == SkipFolders[s] && i < segments.Length - 1) { return true; }
                }
            }
            return false;
        }

        /// <summary> Every authored C# file in a tree, minus this one. </summary>
        public static List<string> SourceFiles(string root)
        {
            List<string> kept = new List<string>();
            if (!Directory.Exists(root)) { return kept; }
            string[] all = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < all.Length; i++)
            {
                if (IsHiddenFromUnity(all[i], root)) { continue; }
                if (Path.GetFileName(all[i]) == OwnSourceFileName) { continue; }
                kept.Add(all[i]);
            }
            kept.Sort(StringComparer.Ordinal);
            return kept;
        }

        /// <summary> Sweep 37. Every file AND folder carries a `.meta`, and no `.meta` is left without the thing it
        /// names. Both halves matter for a different reason: a file with no `.meta` has no identity yet, and an orphan
        /// `.meta` will be re-attached to the next file that lands on that path, handing a new asset an old asset's
        /// references. Counted in files examined so a walk that resolved nothing cannot report success. </summary>
        public static void MetaPairing(string root, SweepResult result)
        {
            if (!Directory.Exists(root))
            {
                result.Add(root, 0, "tree root does not exist", "the sweep's own path no longer resolves");
                return;
            }

            string[] files = Directory.GetFiles(root, "*", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (IsHiddenFromUnity(file, root)) { continue; }
                if (file.EndsWith("~", StringComparison.Ordinal)) { continue; }
                result.itemsChecked++;

                if (file.EndsWith(".meta", StringComparison.Ordinal))
                {
                    string owned = file.Substring(0, file.Length - 5);
                    if (!File.Exists(owned) && !Directory.Exists(owned))
                    {
                        result.Add(file, 0, Path.GetFileName(file), "orphaned .meta with no asset");
                    }
                }
                else
                {
                    result.filesScanned++;
                    if (!File.Exists(file + ".meta"))
                    {
                        result.Add(file, 0, Path.GetFileName(file), "no .meta, so the asset has no identity yet");
                    }
                }
            }

            string[] folders = Directory.GetDirectories(root, "*", SearchOption.AllDirectories);
            for (int i = 0; i < folders.Length; i++)
            {
                if (IsHiddenFromUnity(folders[i] + "/x", root)) { continue; }
                result.itemsChecked++;
                if (!File.Exists(folders[i] + ".meta"))
                {
                    result.Add(folders[i], 0, Path.GetFileName(folders[i]), "folder with no .meta");
                }
            }
        }
        /// <summary> The owner of a sweep this walk cannot honestly perform on the host tree. Sweep 38's assembly half
        /// reads the compilation pipeline against THIS tool's asmdef, and sweep 39 rebuilds a chunk twice; neither
        /// answer can be produced for a tree the module is forbidden to build in. Naming the host's own audit is the
        /// truthful result -- the alternative is a swept row reporting zero, which is exactly the silent pass RM-70
        /// exists to forbid. </summary>
        public const string HostOwnerName = "UCSelfTest";

        /// <summary> The road-tool checks RM-70 names as belonging in this sweep, each with the gate that already owns
        /// it. This table is a routing table, not a second implementation: every one of these questions is answered by
        /// a gate the suite already discovers, and duplicating any of them here would create two pieces of code that
        /// can disagree about the same fact. What RM-70 adds is the assertion that each owner still EXISTS and is still
        /// discoverable, so a check cannot fall out of the suite unnoticed. </summary>
        private static readonly string[][] RoadCheckOwners =
        {
            new[] { "RM-48", "the prefab import contract over every category set", "RCMPREFAB" },
            new[] { "RM-54", "the built sub-hierarchy's structure and naming", "RCMCITYTREE" },
            new[] { "RM-55", "that no substitute-content reference exists anywhere in the runtime", "RCMRESOLVE" },
            new[] { "RM-56", "each category's resolution state and where its content comes from", "RCMCONTENTSRC" },
            new[] { "RM-69", "GUID and `.meta` integrity across the tool tree", "RCMMETA" },
        };

        public static IList<string[]> RoadChecks() { return RoadCheckOwners; }

        /// <summary> Sweeps 33 through 39 over one tree, returned as one row each so a caller can report per tree and a
        /// failure names which tree it came from. Sweep 38 arrives as TWO rows because it asks two separable questions:
        /// which assembly a file compiles into, which only the compilation pipeline knows, and whether a runtime file's
        /// editor usage sits inside a guard, which is a text fact. The second is swept here; the first is delegated.
        ///
        /// Every file is read ONCE and handed to all five text scanners, because reading the tree five times to answer
        /// five questions about the same lines is the sort of thing that makes a sweep too slow to run. </summary>
        public static List<SweepResult> RunTree(string root, bool isToolTree)
        {
            string label = TreeLabel(root);
            string delegatedAssembly = isToolTree ? AssemblyOwnerPrefix : HostOwnerName;
            string delegatedDeterminism = isToolTree ? DeterminismOwnerPrefix : HostOwnerName;

            SweepResult api = new SweepResult(33, "forbidden-api", label, "");
            SweepResult product = new SweepResult(34, "product-name", label, "");
            SweepResult absolute = new SweepResult(35, "absolute-path", label, "");
            SweepResult modal = new SweepResult(36, "modal-dialog", label, "");
            SweepResult meta = new SweepResult(37, "meta-pairing", label, "");
            SweepResult guarded = new SweepResult(38, "editor-use-in-runtime", label, "");
            SweepResult assembly = new SweepResult(38, "assembly-membership", label, delegatedAssembly);
            SweepResult determinism = new SweepResult(39, "determinism", label, delegatedDeterminism);
            //Sweep 40 is a rule about THIS tool's build path and the materials an author hands it. The host tree
            //assigns a `material` field on its own render-pass data objects, which is a plain field and not the
            //instancing accessor at all, so sweeping it there would report a page of findings this module is
            //forbidden by R-5 to act on. It is delegated to the host's own audit instead of being run and ignored.
            SweepResult instancing = new SweepResult(40, "renderer-instancing", label,
                isToolTree ? "" : HostOwnerName);

            List<string> sources = SourceFiles(root);
            for (int i = 0; i < sources.Count; i++)
            {
                string text;
                try
                {
                    text = File.ReadAllText(sources[i]);
                }
                catch (IOException error)
                {
                    api.Add(sources[i], 0, error.Message, "file could not be read, so it was not swept");
                    continue;
                }

                ScanForbiddenApi(sources[i], text, api);
                ScanProductName(sources[i], text, product);
                ScanAbsolutePath(sources[i], text, absolute);
                ScanModalDialog(sources[i], text, modal);
                ScanEditorUseInRuntime(sources[i], text, guarded);
                if (instancing.Swept) { ScanRendererInstancing(sources[i], text, instancing); }
            }

            MetaPairing(root, meta);

            List<SweepResult> all = new List<SweepResult>();
            all.Add(api);
            all.Add(product);
            all.Add(absolute);
            all.Add(modal);
            all.Add(meta);
            all.Add(guarded);
            all.Add(assembly);
            all.Add(determinism);
            all.Add(instancing);
            return all;
        }

        /// <summary> Both trees, tool first. </summary>
        public static List<SweepResult> RunBothTrees()
        {
            List<SweepResult> all = RunTree(ToolTreeRoot(), true);
            all.AddRange(RunTree(HostTreeRoot(), false));
            return all;
        }

        /// <summary> The per-tree report. One line per sweep per tree, each naming the folder it swept and the count of
        /// items it checked, then the findings underneath it -- because a count with no way to see what was found is a
        /// number nobody can act on, and a finding with no count beside it cannot be told from a sweep that matched
        /// nothing. </summary>
        public static string Report(IList<SweepResult> results)
        {
            StringBuilder builder = new StringBuilder();
            string tree = null;
            for (int i = 0; i < results.Count; i++)
            {
                SweepResult result = results[i];
                if (result.tree != tree)
                {
                    tree = result.tree;
                    builder.Append("  tree ").Append(tree).Append('\n');
                }

                builder.Append("    ").Append(result.Line()).Append('\n');
                for (int f = 0; f < result.findings.Count; f++)
                {
                    builder.Append("      FOUND    ").Append(result.findings[f].ToString()).Append('\n');
                }
                for (int d = 0; d < result.deferrals.Count; d++)
                {
                    builder.Append("      allowed  ").Append(result.deferrals[d].ToString()).Append('\n');
                }
            }
            return builder.ToString();
        }
    }
}
#endif
