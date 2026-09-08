// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-62's gate: every road feature flows through the existing construction chain, and the register says
    /// which component builds what.
    ///
    /// § 10.27's rule is easy to agree with and easy to quietly break, because breaking it always looks like a
    /// shortcut: one feature that "just needs a quad" grows into a second mesh builder, and from then on every
    /// arc-length, UV, LOD and determinism fix has to be made twice. So the rule is checked rather than trusted. The
    /// register declares, separately, the SHAPE of what each of RM-26 ... RM-61 delivers and the COMPONENT that builds
    /// it; the cases below re-run the decision procedure over every row, so a row whose component does not follow from
    /// its shape fails the build even though both halves look reasonable on their own.
    ///
    /// The last three cases are the ones that catch reality rather than bookkeeping: a source scan of the city runtime
    /// and the cross-section code for the calls a mesh builder cannot avoid making, the manual's generated table
    /// against the register that generates it, and proof that handing the register out does not hand out the ability to
    /// edit it. What no gate can check is whether the code that calls a producer calls it WELL -- that is a review
    /// judgement, and § 10.9 is where an honest gap goes. </summary>
    public static class CityGeometryProvenanceGate
    {
        [MenuItem("Window/Road City Master/Testing/Geometry provenance (one construction chain, no second mesher)")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError("RCMPROV FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();

            CaseEveryTaskInRangeHasExactlyOneRow(failures);
            CaseEveryComponentFollowsFromItsShape(failures);
            CaseEveryRowCarriesABasis(failures);
            CaseNothingSitsInTheEscapeHatch(failures);
            CaseDecideIsTheProcedureTheTaskStates(failures);
            CaseEveryProducerCarriesWork(failures);
            CaseNoSecondMeshBuilderExists(failures);
            CaseManualCarriesTheGeneratedTable(failures);
            CaseTaskIdsAreCultureIndependent(failures);
            CaseHandingOutTheRegisterIsNotHandingOutEditRights(failures);

            StringBuilder sb = new StringBuilder();
            sb.Append(failures.Count == 0 ? "RCMPROV result PASS" : "RCMPROV result FAIL");
            sb.Append("  cases 10  failures ").Append(failures.Count);
            for (int i = 0; i < failures.Count; i++) { sb.Append("\n  - ").Append(failures[i]); }
            report = sb.ToString();
            return failures.Count == 0;
        }

        /// <summary> A task with no row is a task whose geometry nobody had to account for, and a task with two rows is
        /// two different answers to the same question. RM-62 asks for the whole range, so the range is what is checked
        /// -- not "most of it". </summary>
        private static void CaseEveryTaskInRangeHasExactlyOneRow(List<string> failures)
        {
            int expected = GeometryProvenanceRegister.LastTask - GeometryProvenanceRegister.FirstTask + 1;
            Expect(GeometryProvenanceRegister.Count == expected,
                "the register should carry one row for each of RM-" + GeometryProvenanceRegister.FirstTask
                + " ... RM-" + GeometryProvenanceRegister.LastTask + " (" + expected + "), found "
                + GeometryProvenanceRegister.Count, failures);

            List<int> missing = GeometryProvenanceRegister.MissingTasks();
            Expect(missing.Count == 0, "tasks with no provenance row: " + Numbers(missing), failures);

            List<int> malformed = GeometryProvenanceRegister.MalformedTasks();
            Expect(malformed.Count == 0,
                "tasks with a duplicate row, or a row outside the covered range: " + Numbers(malformed), failures);

            for (int t = GeometryProvenanceRegister.FirstTask; t <= GeometryProvenanceRegister.LastTask; t++)
            {
                ProvenanceRow row;
                bool found = GeometryProvenanceRegister.For(t, out row);
                Expect(found, "RM-" + t + " cannot be looked up in the register", failures);
                if (found)
                {
                    Expect(!string.IsNullOrEmpty(row.feature),
                        row.TaskId + " has a row with no feature named on it", failures);
                }
            }

            ProvenanceRow outside;
            Expect(!GeometryProvenanceRegister.For(GeometryProvenanceRegister.LastTask + 1, out outside),
                "the register answers for RM-" + (GeometryProvenanceRegister.LastTask + 1)
                + ", which is outside the range it claims to cover", failures);
        }

        /// <summary> The check with teeth. Shape and component are written down separately, so re-running the procedure
        /// over every row catches the row that was argued into a convenient producer. A hand-worked control follows: if
        /// `Decide` could be satisfied by any pairing at all, this case would prove nothing. </summary>
        private static void CaseEveryComponentFollowsFromItsShape(List<string> failures)
        {
            List<ProvenanceRow> wrong = GeometryProvenanceRegister.RowsThatDoNotFollow();
            StringBuilder named = new StringBuilder();
            for (int i = 0; i < wrong.Count; i++)
            {
                if (i > 0) { named.Append(", "); }
                named.Append(wrong[i].TaskId).Append(" declares ")
                     .Append(GeometryProvenanceRegister.ShapeName(wrong[i].shape))
                     .Append(" but names ").Append(GeometryProvenanceRegister.PathName(wrong[i].primary))
                     .Append(" instead of ")
                     .Append(GeometryProvenanceRegister.PathName(
                         GeometryProvenanceRegister.Decide(wrong[i].shape)));
            }
            Expect(wrong.Count == 0, "rows whose component does not follow from their shape: " + named, failures);

            Expect(GeometryProvenanceRegister.Decide(FeatureShape.ClosedArea) != GeometryPath.SplinatedMeshMaker,
                "control: a closed area must not resolve to the swept mesher, or this case cannot fail", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.DataOnly) != GeometryPath.EdgeObjectMaker,
                "control: data-only work must not resolve to a producer", failures);
        }

        /// <summary> A row without a basis is an assertion, and an assertion is what a register is supposed to replace.
        /// The length floor is deliberately low but non-zero: it catches "n/a" and "obvious", not brevity. </summary>
        private static void CaseEveryRowCarriesABasis(List<string> failures)
        {
            List<ProvenanceRow> bare = GeometryProvenanceRegister.RowsWithoutBasis();
            StringBuilder named = new StringBuilder();
            for (int i = 0; i < bare.Count; i++)
            {
                if (i > 0) { named.Append(", "); }
                named.Append(bare[i].TaskId);
            }
            Expect(bare.Count == 0, "rows with no usable basis: " + named, failures);

            ProvenanceRow[] rows = GeometryProvenanceRegister.Rows();
            for (int i = 0; i < rows.Length; i++)
            {
                Expect(rows[i].basis.IndexOf('|') < 0,
                    rows[i].TaskId + "'s basis contains a pipe, which would break the generated table", failures);
                Expect(rows[i].feature.IndexOf('|') < 0,
                    rows[i].TaskId + "'s feature contains a pipe, which would break the generated table", failures);
            }
        }

        /// <summary> The escape hatch is not a licence, and an empty hatch is the only passing answer. A row that lands
        /// there owes § 10.9 a written entry BEFORE any alternative code exists, so the failure names the obligation
        /// rather than suggesting the enum be widened. </summary>
        private static void CaseNothingSitsInTheEscapeHatch(List<string> failures)
        {
            List<ProvenanceRow> gaps = GeometryProvenanceRegister.GapsRequiringLog();
            StringBuilder named = new StringBuilder();
            for (int i = 0; i < gaps.Count; i++)
            {
                if (i > 0) { named.Append("; "); }
                named.Append(gaps[i].TaskId).Append(" (").Append(gaps[i].feature).Append(")");
            }
            Expect(gaps.Count == 0,
                "these rows claim to be none of the tool's producers and therefore owe a written § 10.9 gap entry "
                + "before any code is written for them: " + named, failures);

            ProvenanceRow[] rows = GeometryProvenanceRegister.Rows();
            for (int i = 0; i < rows.Length; i++)
            {
                Expect(rows[i].shape != FeatureShape.Neither,
                    rows[i].TaskId + " declares its shape as neither a sweep, a spacing, an area, a heightfield nor "
                    + "data, which is the one case § 10.9 exists for", failures);
            }
        }

        /// <summary> The procedure itself, spelled out in the order § 10.27 states it, and exhaustively: a shape added to
        /// the enum later without an arm here would fall through to the escape hatch, which is the safe direction but
        /// must still be visible rather than silent. The second half checks that the two families -- the four vendored
        /// chain components and the two city operators -- do not overlap and between them cover every producer, so
        /// "which of these built it?" always has exactly one answer. </summary>
        private static void CaseDecideIsTheProcedureTheTaskStates(List<string> failures)
        {
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.Alignment) == GeometryPath.SplineC,
                "an alignment must come from SplineC", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.RoadSurface)
                   == GeometryPath.RoadConstructorBufferMaker,
                "the road surface must come from the buffer maker", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.SweptProfile) == GeometryPath.SplinatedMeshMaker,
                "a swept profile must come from the splinated mesh maker", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.SpacedPrefabs) == GeometryPath.EdgeObjectMaker,
                "objects at a spacing must come from the edge object maker", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.ClosedArea) == GeometryPath.CityAreaOperator,
                "a closed area must go to the city area operator, not to a sweep", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.Heightfield) == GeometryPath.HeightfieldOperator,
                "heightfield work must go to the city heightfield operator", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.DataOnly) == GeometryPath.NoGeometry,
                "data-only work must produce no geometry", failures);
            Expect(GeometryProvenanceRegister.Decide(FeatureShape.Neither) == GeometryPath.LoggedGap,
                "a shape that is genuinely none of the above must be sent to § 10.9, not to a new mesher", failures);

            Array shapes = Enum.GetValues(typeof(FeatureShape));
            Expect(shapes.Length == 8, "the shape enum has grown to " + shapes.Length
                + " members; the procedure above must gain an arm for each new one", failures);

            int chain = 0;
            int operators = 0;
            Array paths = Enum.GetValues(typeof(GeometryPath));
            for (int i = 0; i < paths.Length; i++)
            {
                GeometryPath p = (GeometryPath)paths.GetValue(i);
                bool isChain = GeometryProvenanceRegister.IsVendoredChain(p);
                bool isOperator = GeometryProvenanceRegister.IsCityOperator(p);
                Expect(!(isChain && isOperator),
                    GeometryProvenanceRegister.PathName(p) + " counts as both a chain component and a city operator",
                    failures);
                if (isChain) { chain++; }
                if (isOperator) { operators++; }
                if (!isChain && !isOperator)
                {
                    Expect(p == GeometryPath.NoGeometry || p == GeometryPath.LoggedGap,
                        GeometryProvenanceRegister.PathName(p)
                        + " is a producer that belongs to neither family, which means a fifth builder exists", failures);
                }
            }
            Expect(chain == 4, "the vendored chain must have exactly four components, found " + chain, failures);
            Expect(operators == 2, "§ 10.4 must contribute exactly two operators, found " + operators, failures);
            Expect(paths.Length == 6 + 2, "the producer enum has " + paths.Length
                + " members; four chain, two operators, none and the logged gap is eight", failures);
        }

        /// <summary> Every producer the tool claims to have must actually be carrying work. A component named in the rule
        /// but used by no row is either a component that does not exist or a component nobody routed through, and both
        /// are worth finding out about here rather than in a review. The primary counts must also add up to the row
        /// count exactly, which is what stops a row being counted twice or not at all. </summary>
        private static void CaseEveryProducerCarriesWork(List<string> failures)
        {
            GeometryPath[] mustBeUsed = new GeometryPath[]
            {
                GeometryPath.SplineC, GeometryPath.RoadConstructorBufferMaker, GeometryPath.SplinatedMeshMaker,
                GeometryPath.EdgeObjectMaker, GeometryPath.CityAreaOperator, GeometryPath.HeightfieldOperator
            };
            for (int i = 0; i < mustBeUsed.Length; i++)
            {
                Expect(GeometryProvenanceRegister.UseCount(mustBeUsed[i]) > 0,
                    GeometryProvenanceRegister.PathName(mustBeUsed[i])
                    + " is named by the rule but by no row, so either it does not exist or nothing was routed through it",
                    failures);
            }

            Expect(GeometryProvenanceRegister.UseCount(GeometryPath.LoggedGap) == 0,
                "a row sits in the § 10.9 escape hatch", failures);

            ProvenanceRow[] rows = GeometryProvenanceRegister.Rows();
            int primaries = 0;
            int secondProducers = 0;
            for (int i = 0; i < rows.Length; i++)
            {
                primaries++;
                if (rows[i].secondary != GeometryPath.NoGeometry)
                {
                    secondProducers++;
                    Expect(rows[i].secondary != rows[i].primary,
                        rows[i].TaskId + " names the same component twice, which says nothing", failures);
                    Expect(GeometryProvenanceRegister.IsVendoredChain(rows[i].secondary)
                           || GeometryProvenanceRegister.IsCityOperator(rows[i].secondary),
                        rows[i].TaskId + " names a second producer that is neither chain nor operator", failures);
                }
            }
            Expect(primaries == GeometryProvenanceRegister.Count,
                "the register's row count and the rows handed out disagree", failures);
            Expect(secondProducers > 0,
                "no row has a second producer, which would mean the two-component case was never modelled -- a bridge "
                + "deck is a sweep whose piers are spaced objects", failures);
        }

        /// <summary> The case that checks reality instead of bookkeeping. A register can say the tool has one mesh
        /// builder while a file quietly builds a second, so the two trees § 10 actually authored -- the city runtime and
        /// the cross-section code -- are scanned for the calls a mesh builder cannot avoid making. Anything that fills a
        /// vertex buffer, assigns triangles, combines meshes or attaches a renderer is a producer, and a producer here
        /// would be the second path RM-62 exists to prevent.
        ///
        /// The vendored `Scripts/` tree is deliberately out of scope: it IS the construction chain, and it is where these
        /// calls belong. Comment-only lines are skipped so that explaining the rule is not a breach of it, and the roots
        /// come from <see cref="Application.dataPath"/> rather than a literal (C-04). </summary>
        private static void CaseNoSecondMeshBuilderExists(List<string> failures)
        {
            string assets = Application.dataPath;
            string[] roots = new string[]
            {
                Path.Combine(assets, Path.Combine("RoadCityMaster", Path.Combine("City", "Runtime"))),
                Path.Combine(assets, Path.Combine("RoadCityMaster", Path.Combine("Scripts", "CrossSection")))
            };
            string[] banned = new string[]
            {
                "new Mesh(", "SetVertices", "SetTriangles", "SetIndices", "SetUVs", "CombineMeshes",
                "RecalculateNormals", "sharedMesh", "MeshFilter", "CreatePrimitive"
            };

            List<string> hits = new List<string>();
            int scanned = 0;
            for (int rt = 0; rt < roots.Length; rt++)
            {
                if (!Directory.Exists(roots[rt]))
                {
                    Expect(false, "scan: folder not found at " + roots[rt], failures);
                    continue;
                }
                string[] files = Directory.GetFiles(roots[rt], "*.cs", SearchOption.AllDirectories);
                scanned += files.Length;
                for (int f = 0; f < files.Length; f++)
                {
                    string[] lines = File.ReadAllLines(files[f]);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        string trimmed = lines[i].TrimStart();
                        if (trimmed.StartsWith("//") || trimmed.StartsWith("/*") || trimmed.StartsWith("*"))
                        {
                            continue;
                        }
                        for (int b = 0; b < banned.Length; b++)
                        {
                            if (lines[i].IndexOf(banned[b], StringComparison.Ordinal) >= 0)
                            {
                                hits.Add(Path.GetFileName(files[f]) + ":" + (i + 1) + " '" + banned[b] + "'");
                            }
                        }
                    }
                }
            }

            Expect(scanned > 0, "scan: no source files were read, so the scan proves nothing", failures);
            Expect(hits.Count == 0,
                "a second mesh-building path exists in the city layer -- every one of these belongs in the vendored "
                + "construction chain instead: " + string.Join("; ", hits.ToArray()), failures);
        }

        /// <summary> The manual carries this register as a generated table between markers, and this case fails the build
        /// when the file and the code disagree. Without it the written findings RM-62 asks for would be a snapshot that
        /// silently ages: someone adds a feature, the register grows a row, and the document keeps naming the old set.
        /// The comparison is on content, not whitespace, so an editor trimming trailing spaces or rewriting line endings
        /// is not reported as drift. </summary>
        private static void CaseManualCarriesTheGeneratedTable(List<string> failures)
        {
            string path = GeometryProvenanceTableWriter.ManualPath();
            string inFile;
            string message;
            bool read = GeometryProvenanceTableWriter.ReadTableFrom(path, out inFile, out message);
            Expect(read, "the manual's provenance table could not be read: " + message, failures);
            if (!read) { return; }

            string generated = TerrainFeatureTableWriter.Normalise(GeometryProvenanceRegister.ToManualTable());
            string document = TerrainFeatureTableWriter.Normalise(inFile);
            Expect(document == generated,
                "the provenance table in the manual has drifted from the register; regenerate it with "
                + "Window/Road City Master/Documentation/Write the geometry provenance table", failures);

            for (int t = GeometryProvenanceRegister.FirstTask; t <= GeometryProvenanceRegister.LastTask; t++)
            {
                ProvenanceRow row;
                if (!GeometryProvenanceRegister.For(t, out row)) { continue; }
                Expect(document.IndexOf("| " + row.TaskId + " |", StringComparison.Ordinal) >= 0,
                    row.TaskId + " has no row in the manual's table", failures);
            }
        }

        /// <summary> Task ids are printed into documents and into refusal messages, so they must read the same on every
        /// machine (C-06). The register formats them through the invariant culture; this proves it by asking for them
        /// under a culture that formats numbers differently, and restores the thread afterwards so the gate cannot
        /// change another gate's result by running before it. </summary>
        private static void CaseTaskIdsAreCultureIndependent(List<string> failures)
        {
            System.Globalization.CultureInfo before = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                ProvenanceRow first;
                ProvenanceRow last;
                bool haveFirst = GeometryProvenanceRegister.For(GeometryProvenanceRegister.FirstTask, out first);
                bool haveLast = GeometryProvenanceRegister.For(GeometryProvenanceRegister.LastTask, out last);
                Expect(haveFirst && first.TaskId == "RM-26",
                    "the first row's id should read RM-26, read '" + (haveFirst ? first.TaskId : "none") + "'", failures);
                Expect(haveLast && last.TaskId == "RM-61",
                    "the last row's id should read RM-61, read '" + (haveLast ? last.TaskId : "none") + "'", failures);

                ProvenanceRow[] rows = GeometryProvenanceRegister.Rows();
                for (int i = 0; i < rows.Length; i++)
                {
                    Expect(rows[i].TaskId.Length == 5,
                        "task ids are padded to two digits, so '" + rows[i].TaskId + "' is wrong", failures);
                    Expect(rows[i].TaskId.StartsWith("RM-", StringComparison.Ordinal),
                        "'" + rows[i].TaskId + "' is not an RM id", failures);
                }
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = before;
            }
        }

        /// <summary> The register is the tool's own record of where its geometry comes from, so handing it to a caller
        /// must not hand over the ability to rewrite it. `Rows()` returns a copy; this case edits what it was given and
        /// then asks the register again. If the change survived, any inspector, gate or tool that merely READ the
        /// register could have altered the answer for everything that read it afterwards. </summary>
        private static void CaseHandingOutTheRegisterIsNotHandingOutEditRights(List<string> failures)
        {
            ProvenanceRow[] handed = GeometryProvenanceRegister.Rows();
            Expect(handed.Length > 0, "the register handed out no rows", failures);
            if (handed.Length == 0) { return; }

            int task = handed[0].taskNumber;
            FeatureShape shapeBefore = handed[0].shape;
            GeometryPath pathBefore = handed[0].primary;
            string basisBefore = handed[0].basis;

            handed[0].shape = FeatureShape.Neither;
            handed[0].primary = GeometryPath.LoggedGap;
            handed[0].basis = string.Empty;

            ProvenanceRow again;
            bool found = GeometryProvenanceRegister.For(task, out again);
            Expect(found, "RM-" + task + " disappeared from the register", failures);
            if (found)
            {
                Expect(again.shape == shapeBefore && again.primary == pathBefore && again.basis == basisBefore,
                    "editing the array Rows() handed out changed the register itself, so reading it is enough to "
                    + "rewrite it", failures);
            }

            Expect(GeometryProvenanceRegister.GapsRequiringLog().Count == 0,
                "after an edit to a handed-out copy the register reports a § 10.9 gap, which means the copy was not a "
                + "copy", failures);
            Expect(GeometryProvenanceRegister.RowsThatDoNotFollow().Count == 0,
                "after an edit to a handed-out copy the register reports a row that does not follow from its shape",
                failures);
        }

        /// <summary> A list of task numbers as RM ids, for a failure message someone has to act on. </summary>
        private static string Numbers(List<int> tasks)
        {
            if (tasks == null || tasks.Count == 0) { return "none"; }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < tasks.Count; i++)
            {
                if (i > 0) { sb.Append(", "); }
                sb.Append("RM-").Append(tasks[i].ToString("00", CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
