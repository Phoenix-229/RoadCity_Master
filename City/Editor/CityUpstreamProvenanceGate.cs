#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-106: the licence position, re-tested file by file now that the legacy files have been rewritten.
    ///
    /// RM-79 settled that every file resolves to exactly one licence and recorded the hole it was leaving: "a
    /// path-only classification would mislabel new proprietary files placed under Scripts/ as MIT; the header is the
    /// tie-breaker." Sixty-two files later that hole is load-bearing, so this gate holds the repaired answer down.
    ///
    /// Three things are being checked, and they fail for different reasons.
    ///
    /// The frozen set. <c>UpstreamProvenanceRule</c> enumerates the vendored files, not the new ones, because the
    /// vendor list is what a tag contains and cannot grow, while a list of new files needs a line added every time
    /// one is written and is wrong the first time somebody forgets. A list of names is only frozen if the names still
    /// name something, so case 1 requires each of them on disk exactly once. A vendored file deleted or renamed
    /// without thought would otherwise leave a licence claim pointing at nothing.
    ///
    /// The determination. Case 2 asks every .cs file in the shipped product for its licence and requires the answer
    /// to be exactly one, and determinate -- RM-79's clause, re-run after the rewrites rather than remembered from
    /// before them. Cases 5 and 6 pin the two directions it would drift: a vendored file with no written paragraph
    /// keeps its notice (absence of a justification is not a replacement), and a file that merely MOVED during § 10
    /// is not a licence event, which is why provenance is matched on name and not on folder.
    ///
    /// The evidence. Case 4 exercises the branch no real file reaches: claim the expression is gone, and without a
    /// diff to the RM-78 baseline tag the notice is retained and the answer is reported UNDETERMINED rather than
    /// guessed. That tag does not exist in this tree yet, so the branch is currently unreachable through real input
    /// -- which is precisely why it is tested synthetically. "We rewrote it" has to be a diff, not a recollection.
    ///
    /// And case 9 is the part worth reading twice. RM-64 built its author-facing inspector list by asking a different
    /// question -- does a user of this tool ever see this inspector. RM-106 built its vendored editor set from the
    /// download. The two sets were identical, neither derived from the other, so each is a check on the other, and if a
    /// future task moves a file into one it will be told about the other. B-11 is the first task to have been told: it
    /// added an author-facing editor file this project WROTE, so the checklist now names its own additions and case 9
    /// subtracts them, holding every other file to the identity.
    ///
    /// Report lines are prefixed RCMPROV. </summary>
    public static class CityUpstreamProvenanceGate
    {
        private const string Prefix = "RCMPROV";
        private static int _cases;

        [MenuItem("Window/Road City Master/Testing/Upstream provenance")]
        public static void RunFromMenu()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            _cases = 0;
            List<string> failures = new List<string>();
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(Prefix + " BEGIN");

            CaseTheFrozenSetIsOnDiskExactlyOnce(failures, sb);
            CaseEveryFileResolvesToOneDeterminateLicence(failures, sb);
            CaseEveryFindingIsVendorAndKeepsTheNotice(failures, sb);
            CaseReplacementWithoutADiffIsUndetermined(failures, sb);
            CaseAVendorFileWithNoParagraphKeepsItsNotice(failures, sb);
            CaseMovingAFileIsNotALicenceEvent(failures, sb);
            CaseProjectFilesUnderScriptsAreProprietary(failures, sb);
            CaseTheStampFollowsProvenance(failures, sb);
            CaseRm64AgreesAboutTheEditorFiles(failures, sb);
            CaseDescribeStatesTheWholePosition(failures, sb);
            CaseDeterministic(failures, sb);

            for (int i = 0; i < failures.Count; i++) { sb.AppendLine(Prefix + " FAIL   " + failures[i]); }
            sb.AppendLine(Prefix + " result " + (failures.Count == 0 ? "PASS" : "FAIL") + "  cases " + _cases
                + "  failures " + failures.Count);
            sb.Append(Prefix + " END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            _cases++;
            if (!condition) { failures.Add(message); }
        }

        private static string Fmt(string format, params object[] args)
        {
            return string.Format(CultureInfo.InvariantCulture, format, args);
        }

        private static void Line(StringBuilder sb, string label, string value)
        {
            sb.AppendLine(Prefix + " " + label.PadRight(26) + value);
        }
        private static string NameOf(string path)
        {
            string p = path.Replace('\\', '/');
            int cut = p.LastIndexOf('/');
            return cut >= 0 ? p.Substring(cut + 1) : p;
        }

        private static string Join(List<string> items)
        {
            if (items.Count == 0) { return "none"; }
            return string.Join(", ", items.ToArray());
        }


        /// <summary> Case 1: every name in the frozen vendor set is on disk exactly once, and the set repeats no name
        /// of its own. A frozen list of names is only frozen while the names still name something. </summary>
        private static void CaseTheFrozenSetIsOnDiskExactlyOnce(List<string> failures, StringBuilder sb)
        {
            string[] vendor = UpstreamProvenanceRule.VendorFiles();
            List<string> sources = LicenceHeaderOperator.ToolRelativeSources();
            Dictionary<string, int> onDisk = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
            {
                string name = NameOf(sources[i]);
                int had;
                onDisk[name] = onDisk.TryGetValue(name, out had) ? had + 1 : 1;
            }
            Dictionary<string, bool> seen = new Dictionary<string, bool>(StringComparer.Ordinal);
            List<string> repeated = new List<string>();
            List<string> missing = new List<string>();
            List<string> duplicated = new List<string>();
            for (int i = 0; i < vendor.Length; i++)
            {
                if (seen.ContainsKey(vendor[i])) { repeated.Add(vendor[i]); } else { seen[vendor[i]] = true; }
                int count;
                if (!onDisk.TryGetValue(vendor[i], out count)) { missing.Add(vendor[i]); }
                else if (count > 1) { duplicated.Add(Fmt("{0} x{1}", vendor[i], count)); }
            }
            Expect(repeated.Count == 0, Fmt("the frozen vendor set names the same file twice: {0}", Join(repeated)),
                failures);
            Expect(missing.Count == 0,
                Fmt("the register claims vendored files the tool tree does not contain: {0}", Join(missing)), failures);
            Expect(duplicated.Count == 0,
                Fmt("a vendored file name occurs more than once on disk, so its licence claim is ambiguous: {0}",
                    Join(duplicated)), failures);
            Expect(vendor.Length == UpstreamProvenanceRule.VendorFileCount,
                "VendorFiles and VendorFileCount disagree about the size of the frozen set.", failures);
            Line(sb, "frozen vendor set", Fmt("{0} claimed, {1} found, tool tree {2} source files",
                vendor.Length, vendor.Length - missing.Count, sources.Count));
        }
        /// <summary> Case 2: RM-79's clause, re-run after the rewrites instead of remembered from before them. Every
        /// .cs file in the shipped product resolves to exactly one licence, and none of them lands in an ambiguous
        /// state. The vendor count is asserted against the frozen set rather than against a number written here, so
        /// deleting a vendored file fails case 1 and not this one. </summary>
        private static void CaseEveryFileResolvesToOneDeterminateLicence(List<string> failures, StringBuilder sb)
        {
            List<string> sources = LicenceHeaderOperator.ToolRelativeSources();
            int mit = 0;
            int proprietary = 0;
            int vendorSeen = 0;
            List<string> indeterminate = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                LicenceAfterOverhaul r = UpstreamProvenanceRule.Resolve(sources[i],
                    UpstreamProvenanceRule.RemnantFor(sources[i]), false);
                if (!r.determinate) { indeterminate.Add(sources[i]); }
                if (r.licence == SourceLicence.UpstreamMit) { mit++; } else { proprietary++; }
                if (r.provenance == FileProvenance.VendorDerived) { vendorSeen++; }
            }
            Expect(sources.Count > 0, "no source files were found, so the licence sweep proved nothing.", failures);
            Expect(indeterminate.Count == 0,
                Fmt("files in the shipped product do not resolve to a determinate licence: {0}", Join(indeterminate)),
                failures);
            Expect(mit + proprietary == sources.Count, "a file resolved to neither licence.", failures);
            Expect(vendorSeen == UpstreamProvenanceRule.VendorFileCount,
                Fmt("the tree resolves {0} files as vendor-derived but the frozen set holds {1}", vendorSeen,
                    UpstreamProvenanceRule.VendorFileCount), failures);
            Expect(mit == vendorSeen, "the MIT notice was resolved for a file that is not vendor-derived.", failures);
            Line(sb, "licence sweep", Fmt("{0} files: {1} upstream MIT, {2} proprietary, 0 ambiguous",
                sources.Count, mit, proprietary));
        }
        /// <summary> Case 3: every written paragraph names a real vendored file, says what was replaced and what
        /// remained, and reaches a verdict that follows from what it says. This is the case that would catch a
        /// paragraph written about a file the register does not consider vendored -- an argument about the wrong
        /// file. </summary>
        private static void CaseEveryFindingIsVendorAndKeepsTheNotice(List<string> failures, StringBuilder sb)
        {
            ReplacementFinding[] findings = UpstreamProvenanceRule.Findings();
            int retained = 0;
            for (int i = 0; i < findings.Length; i++)
            {
                string file = findings[i].file;
                Expect(UpstreamProvenanceRule.ProvenanceOf(file) == FileProvenance.VendorDerived,
                    Fmt("{0} has a replacement paragraph but is not in the frozen vendor set", file), failures);
                Expect(!string.IsNullOrEmpty(findings[i].replaced) && !string.IsNullOrEmpty(findings[i].remained),
                    Fmt("{0}'s paragraph does not say both what was replaced and what remained", file), failures);
                LicenceAfterOverhaul r = UpstreamProvenanceRule.Resolve(file, findings[i].remains, false);
                Expect(r.determinate, Fmt("{0} does not reach a determinate licence", file), failures);
                Expect(UpstreamProvenanceRule.ExpressionRemains(findings[i].remains) == !r.needsBaselineDiff,
                    Fmt("{0}'s verdict does not follow from what its paragraph says remains", file), failures);
                if (r.licence == SourceLicence.UpstreamMit) { retained++; }
            }
            Expect(findings.Length > 0, "no replacement paragraphs were written, so nothing was determined.", failures);
            Expect(retained == findings.Length,
                "a rewritten file was moved off MIT; that requires a baseline diff, not a paragraph.", failures);
            Line(sb, "written determinations", Fmt("{0} files, {1} retain the notice, 0 moved off MIT",
                findings.Length, retained));
        }


        /// <summary> Case 4: the branch no real file reaches. Claim the expression is gone and the answer is not
        /// "proprietary" -- it is UNDETERMINED, with the notice retained, until a diff to the RM-78 baseline tag can
        /// be produced. Given the diff, the same input flips. The tag does not exist in this tree, so this is the only
        /// way the refusal path gets exercised at all. </summary>
        private static void CaseReplacementWithoutADiffIsUndetermined(List<string> failures, StringBuilder sb)
        {
            LicenceAfterOverhaul noDiff = UpstreamProvenanceRule.Resolve("Scripts/Road.cs", UpstreamRemnant.Algorithm,
                false);
            Expect(noDiff.needsBaselineDiff, "a claimed replacement must ask for the baseline diff.", failures);
            Expect(!noDiff.determinate, "a claimed replacement without a diff must not be reported determinate.",
                failures);
            Expect(noDiff.licence == SourceLicence.UpstreamMit,
                "a claimed replacement without a diff must keep the MIT notice.", failures);
            Expect(noDiff.note.StartsWith("UNDETERMINED", StringComparison.Ordinal),
                "the undetermined case must say so in its note, not merely in a flag.", failures);

            LicenceAfterOverhaul withDiff = UpstreamProvenanceRule.Resolve("Scripts/Road.cs",
                UpstreamRemnant.Algorithm, true);
            Expect(withDiff.determinate && withDiff.licence == SourceLicence.ProprietaryNew,
                "given the baseline diff, a fully replaced vendored file must resolve proprietary.", failures);
            Expect(UpstreamProvenanceRule.ExpressionRemains(UpstreamRemnant.Algorithm) == false,
                "an algorithm alone is not protected expression (17 U.S.C. 102(b)); the rule must say so.", failures);
            Line(sb, "replacement evidence", "algorithm-only claim: MIT retained, UNDETERMINED without the diff");
        }
        /// <summary> Case 5: a vendored file nobody wrote a paragraph about keeps its notice. The absence of a
        /// justification is not a replacement, and the default has to live in the rule rather than in whoever
        /// remembers to check -- so the remnant for an unargued vendored file is everything. </summary>
        private static void CaseAVendorFileWithNoParagraphKeepsItsNotice(List<string> failures, StringBuilder sb)
        {
            ReplacementFinding unused;
            Expect(!UpstreamProvenanceRule.TryFindingFor("Scripts/Navigation.cs", out unused),
                "Scripts/Navigation.cs was expected to have no written paragraph; this case needs one that has none.",
                failures);
            UpstreamRemnant remains = UpstreamProvenanceRule.RemnantFor("Scripts/Navigation.cs");
            Expect(UpstreamProvenanceRule.ExpressionRemains(remains),
                "an unargued vendored file must be treated as still carrying upstream expression.", failures);
            LicenceAfterOverhaul r = UpstreamProvenanceRule.Resolve("Scripts/Navigation.cs", remains, false);
            Expect(r.determinate && r.licence == SourceLicence.UpstreamMit && !r.needsBaselineDiff,
                "an unargued vendored file must resolve to MIT determinately, asking for no diff.", failures);

            ReplacementFinding found;
            Expect(UpstreamProvenanceRule.TryFindingFor("Scripts/RootUtils.cs", out found)
                && found.remained.Length > 0,
                "the lookup must return the paragraph for a file that has one.", failures);
            Line(sb, "unargued vendored file", Fmt("Navigation.cs remains: {0}", UpstreamProvenanceRule.Names(remains)));
        }


        /// <summary> Case 6: § 10 moved files -- the splines and the terraforming sources sit in subfolders now -- and
        /// moving a file is not a licence event. Provenance is therefore matched on name, and the two directions of the
        /// mistake are both pinned: a moved vendored file is still vendored, and a new file under Scripts/ is not
        /// vendored merely because of where it sits. </summary>
        private static void CaseMovingAFileIsNotALicenceEvent(List<string> failures, StringBuilder sb)
        {
            Expect(UpstreamProvenanceRule.ProvenanceOf("Scripts/Spline/SplineC.cs") == FileProvenance.VendorDerived,
                "a vendored spline source moved into Scripts/Spline/ must still be vendor-derived.", failures);
            Expect(UpstreamProvenanceRule.ProvenanceOf("Scripts/Terrain/Terraforming.cs")
                == FileProvenance.VendorDerived,
                "a vendored terraforming source moved into Scripts/Terrain/ must still be vendor-derived.", failures);
            Expect(UpstreamProvenanceRule.ProvenanceOf("Scripts/SplineC.cs") == FileProvenance.VendorDerived,
                "the same file at its original path must classify the same way.", failures);
            Expect(UpstreamProvenanceRule.ProvenanceOf("City/Runtime/Licensing/UpstreamProvenance.cs")
                == FileProvenance.ProjectAuthored, "a file under City/ must be project-authored.", failures);
            Expect(UpstreamProvenanceRule.ProvenanceOf("Scripts/Editor/RcmGateSuite.cs")
                == FileProvenance.ProjectAuthored,
                "a file § 10 wrote under Scripts/Editor/ must not be classified as vendored by its folder.", failures);
            Expect(UpstreamProvenanceRule.ProvenanceOf(null) == FileProvenance.ProjectAuthored
                && UpstreamProvenanceRule.ProvenanceOf("") == FileProvenance.ProjectAuthored,
                "the classification must be total: no path may throw.", failures);
            Line(sb, "path independence", "vendored by name, not by folder; empty and null answered");
        }
        /// <summary> Case 7: the hole RM-79 wrote down. § 10 put its gates, its registers and its cross-section
        /// profiles under Scripts/, where the old path rule said "upstream MIT". Every one of them must now resolve
        /// proprietary. The count is reported rather than asserted -- it grows with the next task, and a gate that has
        /// to be edited every time work is done is a gate that gets edited without being read. </summary>
        private static void CaseProjectFilesUnderScriptsAreProprietary(List<string> failures, StringBuilder sb)
        {
            List<string> sources = LicenceHeaderOperator.ToolRelativeSources();
            int underScripts = 0;
            List<string> misfiled = new List<string>();
            for (int i = 0; i < sources.Count; i++)
            {
                string p = sources[i].Replace('\\', '/');
                if (!p.StartsWith("Scripts/", StringComparison.Ordinal)) { continue; }
                if (UpstreamProvenanceRule.ProvenanceOf(p) != FileProvenance.ProjectAuthored) { continue; }
                underScripts++;
                LicenceAfterOverhaul r = UpstreamProvenanceRule.Resolve(p, UpstreamProvenanceRule.RemnantFor(p),
                    false);
                if (r.licence != SourceLicence.ProprietaryNew || !r.determinate) { misfiled.Add(p); }
            }
            Expect(underScripts > 0,
                "no project-authored files were found under Scripts/, so this case is not testing the hole it was"
                + " written for.", failures);
            Expect(misfiled.Count == 0,
                Fmt("project-authored files under Scripts/ resolved to the upstream licence: {0}", Join(misfiled)),
                failures);
            Line(sb, "new work under Scripts", Fmt("{0} project-authored files, all proprietary", underScripts));
        }


        /// <summary> Case 8: RM-80's stamper carries the determination -- no second mechanism was written. A vendored
        /// file gets the MIT header, a file § 10 wrote under Scripts/ gets the proprietary one, and both read back
        /// through RM-79's reader as the licence they were stamped with. The note says which question was asked, so a
        /// stamped tree can be audited without re-deriving it. </summary>
        private static void CaseTheStampFollowsProvenance(List<string> failures, StringBuilder sb)
        {
            string body = "using System;\nclass A {}\n";
            bool changed;
            string note;
            string mit = LicenceHeaderOperator.Stamp("Scripts/Road.cs", body, out changed, out note);
            Expect(changed && LicenceRule.HeaderStateOf(mit) == LicenceHeaderState.MitModifiedOnly,
                "a vendored file must still be stamped with the MIT header.", failures);
            Expect(note.IndexOf("provenance", StringComparison.Ordinal) >= 0,
                "the stamp's note must record that the decision was made on provenance.", failures);

            string prop = LicenceHeaderOperator.Stamp("Scripts/Editor/RcmGateSuite.cs", body, out changed, out note);
            Expect(changed && LicenceRule.HeaderStateOf(prop) == LicenceHeaderState.ProprietaryOnly,
                "a file § 10 wrote under Scripts/Editor/ must be stamped proprietary, not upstream MIT.", failures);
            Expect(prop.EndsWith(body, StringComparison.Ordinal),
                "the stamp must preserve the original code below the header.", failures);
            Line(sb, "header stamp", "vendored to MIT, new work to proprietary, both by provenance");
        }
        /// <summary> Case 9: two independent answers to the same question have to agree. RM-64 listed the editor files
        /// an author actually sees, by asking whether a user of this tool ever opens that inspector. RM-106 listed the
        /// editor files that came in the download. Neither was derived from the other, so each is a check on the other --
        /// and a future task that moves a file into one of them gets told about the other instead of silently disagreeing
        /// with it.
        ///
        /// The sets were identical when RM-106 wrote them and are no longer, because B-11 added an author-facing editor
        /// file this project wrote: a panel on Unity's own GameObject header, for generated objects that carry no
        /// component of ours. That is a real difference, not drift, so the checklist names its own additions in
        /// InspectorConsistencyChecklist.NewWorkAuthorFacing() and this case subtracts them before comparing. Every other
        /// file is still held to the identity, which is what keeps the corroboration worth having -- the alternative,
        /// filing our own author-facing file as tooling to keep the counts level, would have bought a green gate by
        /// recording something untrue. </summary>
        private static void CaseRm64AgreesAboutTheEditorFiles(List<string> failures, StringBuilder sb)
        {
            List<string> sources = LicenceHeaderOperator.ToolRelativeSources();
            Dictionary<string, bool> vendorEditor = new Dictionary<string, bool>(StringComparer.Ordinal);
            for (int i = 0; i < sources.Count; i++)
            {
                string p = sources[i].Replace('\\', '/');
                if (!p.StartsWith("Scripts/Editor/", StringComparison.Ordinal)) { continue; }
                if (p.IndexOf('/', "Scripts/Editor/".Length) >= 0) { continue; }
                if (UpstreamProvenanceRule.ProvenanceOf(p) == FileProvenance.VendorDerived)
                {
                    vendorEditor[NameOf(p)] = true;
                }
            }
            // The checklist's own declared additions. Each is held to being genuinely ours: a name here that RM-106 says
            // arrived in the download would be an exemption taken for a file that does not need one.
            IList<string> newWork = InspectorConsistencyChecklist.NewWorkAuthorFacing();
            Dictionary<string, bool> ours = new Dictionary<string, bool>(StringComparer.Ordinal);
            List<string> claimedButVendored = new List<string>();
            for (int i = 0; i < newWork.Count; i++)
            {
                ours[NameOf(newWork[i])] = true;
                if (vendorEditor.ContainsKey(NameOf(newWork[i]))) { claimedButVendored.Add(newWork[i]); }
            }
            IList<InspectorRecord> rows = InspectorConsistencyChecklist.Rows();
            Dictionary<string, bool> checklist = new Dictionary<string, bool>(StringComparer.Ordinal);
            // A row that is neither vendored nor declared is the case the count check used to catch on its own: a file
            // this project wrote, listed as author-facing, without anyone recording that the download did not carry it.
            List<string> undeclaredNewWork = new List<string>();
            for (int i = 0; i < rows.Count; i++)
            {
                checklist[rows[i].file] = true;
                if (vendorEditor.ContainsKey(rows[i].file)) { continue; }
                if (ours.ContainsKey(rows[i].file)) { continue; }
                undeclaredNewWork.Add(rows[i].file);
            }
            List<string> onlyInVendorSet = new List<string>();
            foreach (KeyValuePair<string, bool> pair in vendorEditor)
            {
                if (!checklist.ContainsKey(pair.Key)) { onlyInVendorSet.Add(pair.Key); }
            }
            List<string> declaredButUnlisted = new List<string>();
            foreach (KeyValuePair<string, bool> pair in ours)
            {
                if (!checklist.ContainsKey(pair.Key)) { declaredButUnlisted.Add(pair.Key); }
            }
            undeclaredNewWork.Sort(StringComparer.Ordinal);
            onlyInVendorSet.Sort(StringComparer.Ordinal);
            declaredButUnlisted.Sort(StringComparer.Ordinal);
            claimedButVendored.Sort(StringComparer.Ordinal);
            Expect(undeclaredNewWork.Count == 0,
                Fmt("RM-64 calls these editor files author-facing, RM-106 does not call them vendored, and the checklist"
                    + " does not declare them as its own new work: {0}", Join(undeclaredNewWork)), failures);
            Expect(onlyInVendorSet.Count == 0,
                Fmt("RM-106 calls these editor files vendored but RM-64's checklist does not list them: {0}",
                    Join(onlyInVendorSet)), failures);
            Expect(declaredButUnlisted.Count == 0,
                Fmt("the checklist declares these as its own author-facing files but carries no row for them: {0}",
                    Join(declaredButUnlisted)), failures);
            Expect(claimedButVendored.Count == 0,
                Fmt("these are declared as this project's own author-facing files but RM-106 says they came in the"
                    + " download: {0}", Join(claimedButVendored)), failures);
            Expect(rows.Count == vendorEditor.Count + ours.Count,
                Fmt("the checklist has {0} rows and the vendored editor set has {1} files with {2} declared as new work",
                    rows.Count, vendorEditor.Count, ours.Count), failures);
            IList<string> excluded = InspectorConsistencyChecklist.NotAuthorFacing();
            List<string> wronglyExcluded = new List<string>();
            for (int i = 0; i < excluded.Count; i++)
            {
                if (vendorEditor.ContainsKey(NameOf(excluded[i]))) { wronglyExcluded.Add(excluded[i]); }
            }
            Expect(wronglyExcluded.Count == 0,
                Fmt("a vendored author-facing editor file is on RM-64's excluded list: {0}", Join(wronglyExcluded)),
                failures);
            Line(sb, "RM-64 corroboration", Fmt("{0} vendored editor files + {1} of our own, {2} checklist rows, {3} excluded",
                vendorEditor.Count, ours.Count, rows.Count, excluded.Count));
        }
        /// <summary> Case 10: the written position is generated from the register, so the notice on disk and the manual
        /// cannot say something the rule does not. Docs/ is deleted when § 10 closes; this text is the thing that
        /// survives it, and it has to name every file it judges and state the ground it judges on. </summary>
        private static void CaseDescribeStatesTheWholePosition(List<string> failures, StringBuilder sb)
        {
            string text = UpstreamProvenanceRule.Describe();
            Expect(!string.IsNullOrEmpty(text) && text.Length > 2000,
                "the written position is missing or too short to be a determination.", failures);
            ReplacementFinding[] findings = UpstreamProvenanceRule.Findings();
            List<string> unnamed = new List<string>();
            for (int i = 0; i < findings.Length; i++)
            {
                if (text.IndexOf(findings[i].file, StringComparison.Ordinal) < 0) { unnamed.Add(findings[i].file); }
            }
            Expect(unnamed.Count == 0, Fmt("the written position does not name every file it judges: {0}",
                Join(unnamed)), failures);
            Expect(text.IndexOf("102(b)", StringComparison.Ordinal) >= 0,
                "the written position must cite the ground it stands on, not assert the conclusion.", failures);
            Expect(text.IndexOf("MIT notice retained", StringComparison.Ordinal) >= 0,
                "the written position must state each verdict in words.", failures);
            Expect(text.IndexOf(UpstreamProvenanceRule.VendorFileCount.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal) >= 0, "the written position must state the size of the frozen set.",
                failures);
            Expect(text.IndexOf("MicroGSD", StringComparison.Ordinal) < 0,
                "the generated position must not carry the upstream vendor's name; the notice file holds that.",
                failures);
            Expect(text.IndexOf("not legal advice", StringComparison.Ordinal) >= 0,
                "the written position must record that it is evidence and not advice.", failures);
            Expect(text.IndexOf("not the module built on top of them", StringComparison.Ordinal) >= 0,
                "the written position must record what retaining the notice actually permits.", failures);
            Line(sb, "written position", Fmt("{0} characters, {1} files named", text.Length, findings.Length));
        }


        /// <summary> Case 11: C-06. The determination is the same on two runs, and the register hands out copies --
        /// a caller that edits what it was given must not be able to edit the frozen set. </summary>
        private static void CaseDeterministic(List<string> failures, StringBuilder sb)
        {
            Expect(UpstreamProvenanceRule.Describe() == UpstreamProvenanceRule.Describe(),
                "the written position differs between two runs.", failures);
            LicenceAfterOverhaul a = UpstreamProvenanceRule.Resolve("Scripts/Road.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Comments, false);
            LicenceAfterOverhaul b = UpstreamProvenanceRule.Resolve("Scripts/Road.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Comments, false);
            Expect(a.licence == b.licence && a.provenance == b.provenance && a.determinate == b.determinate
                && a.needsBaselineDiff == b.needsBaselineDiff && a.note == b.note,
                "the same file and the same remnant gave two different answers.", failures);

            string[] handedOut = UpstreamProvenanceRule.VendorFiles();
            string first = handedOut.Length > 0 ? handedOut[0] : string.Empty;
            if (handedOut.Length > 0) { handedOut[0] = "tampered.cs"; }
            Expect(UpstreamProvenanceRule.VendorFiles()[0] == first,
                "editing the array VendorFiles handed out changed the frozen set.", failures);
            ReplacementFinding[] rows = UpstreamProvenanceRule.Findings();
            string firstFile = rows.Length > 0 ? rows[0].file : string.Empty;
            if (rows.Length > 0) { rows[0].file = "tampered.cs"; }
            Expect(UpstreamProvenanceRule.Findings()[0].file == firstFile,
                "editing the array Findings handed out changed the written determinations.", failures);
            Expect(UpstreamProvenanceRule.IsVendorFile("Road.cs") && !UpstreamProvenanceRule.IsVendorFile("road.cs"),
                "vendored names must be matched exactly, not case-insensitively.", failures);
            Line(sb, "determinism", "two runs identical; frozen set and determinations handed out as copies");
        }
    }
}
#endif
