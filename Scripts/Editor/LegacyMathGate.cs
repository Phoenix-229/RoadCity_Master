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


namespace RoadCityMaster
{
    /// <summary> RM-94's gate: proves the legacy geometry census is COMPLETE rather than merely written. It reads the
    /// scoped legacy source files off disk, strips their comments and string literals, extracts every numeric literal
    /// with a real fractional part, and fails on any one the census does not name. That is the whole point: a census
    /// anyone can add to but nobody can leave a hole in. Adding a magic number to a legacy geometry file now breaks the
    /// suite until somebody says what it is for.
    ///
    /// Two scoping decisions, both deliberate and both load-bearing:
    ///
    ///   * COMMENTS AND STRINGS ARE STRIPPED FIRST. This is not tidiness. RoadCreationT contains 303.1898f -- a
    ///     hard-coded world coordinate from one debugging session years ago -- and 0.035f, and both are inside
    ///     commented-out blocks. A sweep that reads them as live mathematics reports dead scaffolding with the same
    ///     weight as a real defect, and the real defects are then impossible to find in the noise.
    ///   * ONLY LITERALS WITH A NONZERO FRACTIONAL PART ARE SWEPT. 2.0f, 0.0f and 1.0f carry no chosen magnitude --
    ///     they are exact integers that happen to be written with a decimal point -- and integer literals like 4f or
    ///     16f are overwhelmingly counts, divisors and loop bounds. Sweeping them would bury the census in rows saying
    ///     "this 2 is a 2". The integer literals that DO carry a decision are named by the census anyway: the 20f
    ///     clamp ceiling that permits a 152 m signal pole, and the 500f that lets a lane be half a kilometre wide.
    ///     Naming extra literals is free; the sweep only ever fails in the other direction.
    ///
    /// Report lines are prefixed RCMLEGACYMATH. </summary>
    public static class LegacyMathGate
    {
        /// <summary> The legacy files the census covers. Every one is checked to exist, so a rename fails the gate
        /// loudly instead of quietly reducing the sweep to nothing -- which is how a green suite comes to mean
        /// "swept no files". </summary>
        private static readonly string[] ScopedFiles =
        {
            "Scripts/RoadCreationT.cs",
            "Scripts/SplinatedMeshMaker.cs",
            "Scripts/RoadConstructorBufferMaker.cs",
            "Scripts/EdgeObjectMaker.cs",
            "Scripts/Intersections.cs",
            "Scripts/IntersectionObjects.cs",
            "Scripts/RoadIntersection.cs",
            "Scripts/Construction.cs",
            "Scripts/Construction2DRect.cs",
            "Scripts/Construction3DTri.cs",
            "Scripts/RoadAutomation.cs",
            "Scripts/Road.cs",
            "Scripts/RoadUtility.cs",
        };

        [MenuItem("Window/Road City Master/Testing/Legacy geometry census (every magic number named)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.LegacyMathGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMLEGACYMATH BEGIN\n");
            List<string> failures = new List<string>();

            CaseEveryRowIsWellFormed(builder, failures);
            CaseScopedFilesExist(builder, failures);
            CaseTheSweepFindsNothingUnnamed(builder, failures);
            CaseTheKnownDefectsStayNamed(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMLEGACYMATH FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMLEGACYMATH result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMLEGACYMATH END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Every row says where it is, what it produces, what it rests on, what it assumes and what its
        /// real-world counterpart is -- and a Replace row additionally says what the value BECOMES. That last clause is
        /// the one that stops the census from filling up with "this is wrong" rows that never name a replacement. </summary>
        private static void CaseEveryRowIsWellFormed(StringBuilder builder, List<string> failures)
        {
            IList<LegacyMathRow> rows = LegacyMathRegister.Rows();
            Expect(rows.Count > 0, "rows: the census is empty (setup)", failures);
            foreach (LegacyMathRow row in rows)
            {
                Expect(row.IsWellFormed(),
                    "rows: " + row.file + " / " + row.member + " is incomplete for a " + row.disposition + " row",
                    failures);
            }
            builder.Append("RCMLEGACYMATH ").Append("rows-well-formed".PadRight(24))
                   .Append(rows.Count).Append(" rows, each naming its assumption and counterpart\n");
        }

        /// <summary> Every scoped file is on disk. A census of files that have been renamed is a census of nothing, and
        /// the sweep below would pass trivially. </summary>
        private static void CaseScopedFilesExist(StringBuilder builder, List<string> failures)
        {
            string root = DispositionDocWriter.ToolRoot();
            int found = 0;
            for (int i = 0; i < ScopedFiles.Length; i++)
            {
                string path = Path.Combine(root, ScopedFiles[i].Replace('/', Path.DirectorySeparatorChar));
                if (File.Exists(path)) { found++; }
                else { failures.Add("scope: " + ScopedFiles[i] + " is not on disk, so the sweep cannot read it"); }
            }
            builder.Append("RCMLEGACYMATH ").Append("scope-on-disk".PadRight(24))
                   .Append(found).Append(" of ").Append(ScopedFiles.Length).Append(" scoped files present\n");
        }

        /// <summary> The sweep. Every fractional literal in live code in the scoped files is named by the census. </summary>
        private static void CaseTheSweepFindsNothingUnnamed(StringBuilder builder, List<string> failures)
        {
            HashSet<string> named = Normalise(LegacyMathRegister.AllLiterals());
            string root = DispositionDocWriter.ToolRoot();
            int swept = 0;
            int distinct = 0;
            HashSet<string> seen = new HashSet<string>();

            for (int i = 0; i < ScopedFiles.Length; i++)
            {
                string path = Path.Combine(root, ScopedFiles[i].Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) { continue; }

                string live = StripCommentsAndStrings(File.ReadAllText(path));
                List<string> literals = FractionalLiterals(live);
                swept += literals.Count;

                for (int j = 0; j < literals.Count; j++)
                {
                    string key = literals[j];
                    if (seen.Add(key)) { distinct++; }
                    if (!named.Contains(key))
                    {
                        failures.Add("unnamed: " + ScopedFiles[i] + " uses " + key
                            + " and no census row names it -- say what it produces and where the value comes from");
                    }
                }
            }

            Expect(swept > 0, "sweep: no literals were found at all, which means the sweep is broken (setup)", failures);
            builder.Append("RCMLEGACYMATH ").Append("sweep-complete".PadRight(24))
                   .Append(swept).Append(" fractional literals in live code, ")
                   .Append(distinct).Append(" distinct, all named\n");
        }

        /// <summary> The findings that are defects rather than merely unsourced values are pinned in BOTH directions.
        /// A still-broken defect must stay recorded as Replace until it is actually fixed -- without that, the cheapest
        /// way to make the census pass would be to soften the row to AuthoredKeep and leave the code exactly as broken
        /// as it is. A defect that HAS been fixed is pinned as Sourced, so the row cannot quietly go back to Replace,
        /// and cannot be deleted along with the record of what was wrong with it.
        ///
        /// As of the end of RM-94 every defect the census found is in the second group, so every assertion below is an
        /// ExpectSourced. <see cref="ExpectDisposition"/> is deliberately kept rather than deleted with its last call
        /// site: the next census finding needs it, and a gate that can only assert "fixed" cannot hold a new defect
        /// open. </summary>
        private static void CaseTheKnownDefectsStayNamed(StringBuilder builder, List<string> failures)
        {
            // Scripts/RoadCreationT.cs / "0.475f" was here. RM-94 fixed it: the turn-lane marking strips take their
            // width in metres from PavementMarkings and convert it against the measured carriageway span, so there is
            // no lane-count branch left to be missing a case for -- and the backward end of the same two strips, which
            // had no branch at all, now reads the same two figures as the forward end.
            ExpectSourced("Scripts/RoadCreationT.cs", "0.475f",
                "the turn-lane marking points, now a painted width in metres instead of a fitted fraction", failures);
            ExpectSourced("Scripts/RoadCreationT.cs", "0.03f",
                "the outer marking points, now one edge-strip width for every lane total and both ends of the node",
                failures);
            ExpectSourced("Scripts/RoadCreationT.cs", "0.10f",
                "the ramp vertex drop, now the shoulder's own cross-fall instead of a constant fitted to one width",
                failures);
            // Scripts/IntersectionObjects.cs / "7.6f" was here. RM-94 fixed it: hMod is gone, so the mast is built at
            // its modelled height, inside the MUTCD band, and only the arm answers to the crossing width.
            ExpectSourced("Scripts/IntersectionObjects.cs", "7.6f",
                "the signal mast, no longer stretched taller by the width of the intersection", failures);
            ExpectSourced("Scripts/IntersectionObjects.cs", "0.45f",
                "the furniture offset, now metres from the travelled-way edge instead of a slice of the shoulder",
                failures);
            // Scripts/Road.cs / "500f" was here. RM-94 fixed it: RoadFieldLimits owns the lane-width and
            // segment-resolution bands for both the runtime clamp and the inspector's declared range, so the row is
            // Sourced now and asserting Replace would make this gate demand its own defect back.
            ExpectSourced("Scripts/Road.cs", "500f",
                "the lane-width clamp, now reading the same band the inspector declares", failures);
            ExpectSourced("Scripts/RoadAutomation.cs and Scripts/Construction.cs", "0.03f",
                "the node placement floor, now measured from the ground instead of from world zero", failures);
            ExpectSourced("Scripts/RoadCreationT.cs", "5.5f",
                "the intersection approach, now a taper length instead of a multiple of road width", failures);

            int replace = LegacyMathRegister.CountOf(MathDisposition.Replace);
            int unsourced = LegacyMathRegister.UnsourcedGeometryCount();
            Expect(unsourced == replace,
                "defects: the unsourced-geometry count and the Replace count disagree, so a Replace row was filed "
                + "under a non-geometry role", failures);

            builder.Append("RCMLEGACYMATH ").Append("known-defects".PadRight(24))
                   .Append(replace).Append(" computations to replace, ")
                   .Append(LegacyMathRegister.CountOf(MathDisposition.Sourced)).Append(" already sourced, ")
                   .Append(LegacyMathRegister.CountOf(MathDisposition.AuthoredKeep)).Append(" authored, ")
                   .Append(LegacyMathRegister.CountOf(MathDisposition.Delete)).Append(" to delete\n");
        }

        /// <summary> The census is identical on repeat (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            IList<LegacyMathRow> a = LegacyMathRegister.Rows();
            IList<LegacyMathRow> b = LegacyMathRegister.Rows();
            Expect(a.Count == b.Count, "determinism: the census count changed between calls", failures);
            if (a.Count > 0 && b.Count > 0)
            {
                Expect(a[0].file == b[0].file && a[0].member == b[0].member,
                    "determinism: the first row changed between calls", failures);
            }
            Expect(LegacyMathRegister.AllLiterals().Count == LegacyMathRegister.AllLiterals().Count,
                "determinism: the literal set changed between calls", failures);
            builder.Append("RCMLEGACYMATH ").Append("determinism".PadRight(24))
                   .Append("the census is identical on repeat\n");
        }

        // --- the sweep itself ---

        /// <summary> Removes // line comments, block comments, string literals (plain and verbatim) and char literals,
        /// replacing each with a space so token boundaries survive. What comes back is the code that actually runs.
        /// Written as a state machine rather than a regular expression because the cases genuinely interact: a // inside
        /// a string is not a comment, and a quote inside a comment does not open a string. </summary>
        public static string StripCommentsAndStrings(string source)
        {
            StringBuilder outText = new StringBuilder(source.Length);
            int i = 0;
            int n = source.Length;

            while (i < n)
            {
                char c = source[i];

                // Line comment.
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n') { i++; }
                    outText.Append('\n');
                    continue;
                }
                // Block comment.
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) { i++; }
                    i = i + 2 <= n ? i + 2 : n;
                    outText.Append(' ');
                    continue;
                }
                // Verbatim string: @"...", where "" is an escaped quote and \ is literal.
                if (c == '@' && i + 1 < n && source[i + 1] == '"')
                {
                    i += 2;
                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < n && source[i + 1] == '"') { i += 2; continue; }
                            i++;
                            break;
                        }
                        i++;
                    }
                    outText.Append(' ');
                    continue;
                }
                // Plain string.
                if (c == '"')
                {
                    i++;
                    while (i < n)
                    {
                        if (source[i] == '\\') { i += 2; continue; }
                        if (source[i] == '"') { i++; break; }
                        if (source[i] == '\n') { break; }
                        i++;
                    }
                    outText.Append(' ');
                    continue;
                }
                // Char literal.
                if (c == '\'')
                {
                    i++;
                    while (i < n)
                    {
                        if (source[i] == '\\') { i += 2; continue; }
                        if (source[i] == '\'') { i++; break; }
                        if (source[i] == '\n') { break; }
                        i++;
                    }
                    outText.Append(' ');
                    continue;
                }

                outText.Append(c);
                i++;
            }
            return outText.ToString();
        }

        /// <summary> Every numeric literal with a nonzero fractional part, normalised so 0.10f and 0.1f are the same
        /// value. Digits preceded by an identifier character are skipped, so a version suffix or a name ending in a
        /// digit cannot be read as a number. </summary>
        public static List<string> FractionalLiterals(string liveCode)
        {
            List<string> found = new List<string>();
            int i = 0;
            int n = liveCode.Length;

            while (i < n)
            {
                char c = liveCode[i];
                if (!char.IsDigit(c) && c != '.') { i++; continue; }

                // Not the start of a literal if the previous character could belong to an identifier or another number.
                if (i > 0)
                {
                    char prev = liveCode[i - 1];
                    if (char.IsLetterOrDigit(prev) || prev == '_' || prev == '.') { i++; continue; }
                }

                int start = i;
                while (i < n && char.IsDigit(liveCode[i])) { i++; }
                bool hasDot = false;
                if (i < n && liveCode[i] == '.' && i + 1 < n && char.IsDigit(liveCode[i + 1]))
                {
                    hasDot = true;
                    i++;
                    while (i < n && char.IsDigit(liveCode[i])) { i++; }
                }
                int end = i;
                // Skip a numeric suffix so it does not run into the next token.
                if (i < n && (liveCode[i] == 'f' || liveCode[i] == 'F' || liveCode[i] == 'd' || liveCode[i] == 'D'
                    || liveCode[i] == 'm' || liveCode[i] == 'M')) { i++; }

                if (!hasDot || end <= start)
                {
                    // A '.' that begins no literal consumed nothing above, so the cursor is still sitting on it and
                    // this continue would hand it straight back to itself. Member access after anything that is not an
                    // identifier character reaches here -- ].time, ).normalized, a fluent call wrapped onto its own
                    // line -- and the first such dot in the first scoped file would otherwise spin the sweep until the
                    // run was killed. Every other path has already moved i past the digits it read.
                    if (i == start) { i++; }
                    continue;
                }

                string text = liveCode.Substring(start, end - start);
                double value;
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) { continue; }
                if (value == System.Math.Floor(value)) { continue; }   // 2.0 carries no chosen magnitude.
                found.Add(Key(value));
            }
            return found;
        }

        // --- helpers ---

        /// <summary> The canonical form a literal is compared by: its value, not how it was typed. </summary>
        private static string Key(double value)
        {
            return value.ToString("R", CultureInfo.InvariantCulture);
        }

        private static HashSet<string> Normalise(HashSet<string> written)
        {
            HashSet<string> keys = new HashSet<string>();
            foreach (string text in written)
            {
                string trimmed = text.TrimEnd('f', 'F', 'd', 'D', 'm', 'M');
                double value;
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
                {
                    keys.Add(Key(value));
                }
            }
            return keys;
        }

        /// <summary> Asserts the disposition of one literal in one file. Keyed on the file as well as the literal
        /// because the same value legitimately appears with different verdicts in different places -- 0.03 is a defect
        /// as an absolute world-Y floor and entirely correct as a render bias, and a check that matched on the number
        /// alone would pass on whichever row happened to be registered first. </summary>
        private static void ExpectDisposition(string file, string literal, MathDisposition wanted, string what,
            List<string> failures)
        {
            foreach (LegacyMathRow row in LegacyMathRegister.Rows())
            {
                if (row.file != file) { continue; }
                for (int i = 0; i < row.literals.Length; i++)
                {
                    if (row.literals[i] != literal) { continue; }
                    Expect(row.disposition == wanted,
                        "defects: " + literal + " in " + file + " (" + what + ") is recorded as " + row.disposition
                        + ", but nothing has been changed in the code, so it is still " + wanted, failures);
                    return;
                }
            }
            failures.Add("defects: " + literal + " in " + file + " (" + what
                + ") has been dropped from the census entirely");
        }

        /// <summary> The other direction: a defect that HAS been fixed must stay recorded as fixed, and must not be
        /// dropped from the census. A row deleted once its code was corrected takes the record of the correction with
        /// it, and the next person to widen the clamp back out has nothing to contradict them. </summary>
        private static void ExpectSourced(string file, string literal, string what, List<string> failures)
        {
            foreach (LegacyMathRow row in LegacyMathRegister.Rows())
            {
                if (row.file != file) { continue; }
                for (int i = 0; i < row.literals.Length; i++)
                {
                    if (row.literals[i] != literal) { continue; }
                    Expect(row.disposition == MathDisposition.Sourced,
                        "defects: " + literal + " in " + file + " (" + what + ") is recorded as " + row.disposition
                        + ", but the code has been corrected, so the row is Sourced", failures);
                    return;
                }
            }
            failures.Add("defects: " + literal + " in " + file + " (" + what
                + ") has been dropped from the census entirely");
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
