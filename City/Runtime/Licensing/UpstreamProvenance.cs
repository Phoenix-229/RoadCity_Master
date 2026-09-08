// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Whether a source file arrived in the vendored download or was written for this project. This is a
    /// question of fact with one answer per file, and it is deliberately separate from <see cref="SourceLicence"/>:
    /// provenance is what the baseline says, licence is what follows from it. </summary>
    public enum FileProvenance
    {
        VendorDerived = 0,
        ProjectAuthored = 1
    }


    /// <summary> What remains of the upstream author's work in a rewritten file. These are the four things RM-106 asks
    /// to be named, and they are not interchangeable: three of them are protected expression and one is not.
    ///
    /// Copyright reaches expression, not ideas. 17 U.S.C. § 102(b) excludes "any idea, procedure, process, system,
    /// method of operation, concept, principle, or discovery" from protection however it is described in the work, and
    /// the abstraction-filtration-comparison test courts apply to software (Computer Associates v. Altai, 2d Cir.
    /// 1992) filters exactly that out before comparing what is left. So <see cref="Algorithm"/> alone is the one
    /// remnant that does NOT retain the upstream copyright: keeping the method while replacing the structure, the
    /// names and the comments leaves nothing of the original's expression behind. Comments, identifiers and structure
    /// each do retain it, which is why the honest answer for almost every file in this tool is MIT-retained.
    ///
    /// A file whose remnant is <see cref="None"/> is a replacement, not a modification -- RM-79's "wholesale rewrite
    /// of a vendor file" case -- and moving it off MIT still requires the baseline diff, never this enum alone. </summary>
    [Flags]
    public enum UpstreamRemnant
    {
        None = 0,
        Comments = 1,
        Identifiers = 2,
        Structure = 4,
        Algorithm = 8
    }


    /// <summary> One vendor-derived file's determination after the overhaul: what § 10 did to it, what is left of the
    /// original, and therefore which licence it resolves to. The paragraph is the written justification RM-106
    /// requires; a file with no finding keeps its MIT header by default, which is the conservative direction. </summary>
    public struct ReplacementFinding
    {
        public string file;
        public UpstreamRemnant remains;
        public string replaced;
        public string remained;
    }


    /// <summary> The answer for one file after the overhaul, and whether it is safe to act on. </summary>
    public struct LicenceAfterOverhaul
    {
        public SourceLicence licence;
        public FileProvenance provenance;
        public UpstreamRemnant remains;
        public bool determinate;
        public bool needsBaselineDiff;
        public string note;
    }


    /// <summary> RM-106: RM-79's determination re-run against the post-overhaul tree, per file.
    ///
    /// THE FROZEN SET IS THE ONE THAT IS LISTED. The vendored download cannot grow -- it is a tag, not a branch -- so
    /// the 55 files it shipped are enumerable and will stay 55 for ever. What § 10 wrote grows with every task, and a
    /// register listing THAT would need editing on every commit and would be wrong the first time somebody forgot.
    /// So provenance is derived: a `.cs` file in the tool tree is vendor-derived if and only if its name is in the
    /// frozen set, and project-authored otherwise. A new gate added tomorrow classifies correctly with no edit here.
    ///
    /// THIS CLOSES THE GAP RM-79 WROTE DOWN AND COULD NOT CLOSE. <see cref="LicenceRule.ClassifyByPath"/> calls
    /// everything outside <c>City/</c> upstream MIT, and § 10 has since put 62 files of its own under <c>Scripts/</c> --
    /// 51 gates and registers under <c>Scripts/Editor/</c>, 7 cross-section profiles, 4 in the root. Stamped by path
    /// they would each carry a notice crediting the upstream author with work they did not write, in a product sold
    /// under a different licence. That is not a tidiness problem; it is a false attribution in both directions.
    ///
    /// THE PARTITION IS CORROBORATED, NOT ASSERTED. RM-64's <c>InspectorConsistencyChecklist</c> was written many
    /// tasks earlier to answer an unrelated question -- which editor files does the author actually look at -- and its
    /// answer splits <c>Scripts/Editor/</c> into 14 author-facing files and 51 excluded ones. Those 14 are exactly the
    /// 14 vendor editor files named below, and the 51 are exactly § 10's tooling. Two registers built for different
    /// purposes drawing the same line is worth more than either drawing it alone, and the gate asserts it. </summary>
    public static class UpstreamProvenanceRule
    {
        // The vendored download's own C# files, by name. Names are unique across the tool tree, so matching on the
        // name rather than the path survives § 10 having moved files into Scripts/Spline/ and Scripts/Terrain/
        // without turning a legitimate move into a licence change.
        private static readonly string[] _vendorFiles = new string[]
        {
            // Scripts/ root -- 29
            "Construction.cs", "Construction2DRect.cs", "Construction3DTri.cs", "EdgeObjectMaker.cs",
            "iConstructionMaker.cs", "IntersectionObjects.cs", "Intersections.cs", "Navigation.cs",
            "RenderQueue.cs", "RigidBody.cs", "Road.cs", "RoadAutomation.cs", "RoadCalcs1.cs", "RoadCalcs2.cs",
            "RoadConstructorBufferMaker.cs", "RoadCreationT.cs", "RoadEditorUtility.cs", "RoadIntersection.cs",
            "RoadSystem.cs", "RoadTerrainInfo.cs", "RoadUtility.cs", "RootUtils.cs", "SodiumFlare.cs",
            "SplinatedMeshMaker.cs", "ThreadedJob.cs", "TrafficLightController.cs", "TrafficLightSequence.cs",
            "UnitTests.cs", "WizardObject.cs",
            // Scripts/Spline/ -- 5
            "SplineC.cs", "SplineF.cs", "SplineI.cs", "SplineN.cs", "SplinePreviewNode.cs",
            // Scripts/Terrain/ -- 7
            "RoadTerrain.cs", "Terraforming.cs", "TerraformingThreaded.cs", "TerrainCalcs.cs",
            "TerrainCalcsStatic.cs", "TerrainHistoryMaker.cs", "TerrainHistoryUtility.cs",
            // Scripts/Editor/ -- 14, and the same 14 RM-64 found to be the author-facing ones
            "EditorMenu.cs", "EditorUtilities.cs", "HelpWindow.cs", "ObjExporter.cs", "RoadEditor.cs",
            "RoadIntersectionEditor.cs", "RoadSystemEditor.cs", "SaveWindow.cs", "SplineCEditor.cs",
            "SplineFEditor.cs", "SplineIEditor.cs", "SplineNEditor.cs", "TerrainEditor.cs", "Wizard.cs"
        };


        /// <summary> How many files the vendored download shipped. A constant of history, not a tuning value. </summary>
        public static int VendorFileCount { get { return _vendorFiles.Length; } }


        /// <summary> The frozen set, copied so a caller cannot edit history. </summary>
        public static string[] VendorFiles()
        {
            string[] copy = new string[_vendorFiles.Length];
            Array.Copy(_vendorFiles, copy, _vendorFiles.Length);
            return copy;
        }


        /// <summary> Ordinal by design: a file name differing only in case is a different file to the register, and
        /// saying so loudly beats resolving two files to one licence row. </summary>
        public static bool IsVendorFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) { return false; }
            for (int i = 0; i < _vendorFiles.Length; i++)
            {
                if (string.Equals(_vendorFiles[i], fileName, StringComparison.Ordinal)) { return true; }
            }
            return false;
        }


        /// <summary> Total, like <see cref="LicenceRule.ClassifyByPath"/>: exactly one value, always, never throws.
        /// A path under <c>City/</c> is project work whatever it is called, so a future name collision with a vendor
        /// file cannot drag a proprietary file back across the line. </summary>
        public static FileProvenance ProvenanceOf(string toolRelativePath)
        {
            if (string.IsNullOrEmpty(toolRelativePath)) { return FileProvenance.ProjectAuthored; }
            if (LicenceRule.ClassifyByPath(toolRelativePath) == SourceLicence.ProprietaryNew)
            {
                return FileProvenance.ProjectAuthored;
            }
            string p = toolRelativePath.Replace('\\', '/');
            int cut = p.LastIndexOf('/');
            string name = cut >= 0 ? p.Substring(cut + 1) : p;
            return IsVendorFile(name) ? FileProvenance.VendorDerived : FileProvenance.ProjectAuthored;
        }


        /// <summary> Whether what is left of the upstream file is protected expression. Comments, identifiers and
        /// structure are; the algorithm alone is not (17 U.S.C. § 102(b)). This is the whole legal content of the
        /// determination, in one line, so it can be read and disagreed with rather than buried in a branch. </summary>
        public static bool ExpressionRemains(UpstreamRemnant remains)
        {
            return (remains & (UpstreamRemnant.Comments | UpstreamRemnant.Identifiers | UpstreamRemnant.Structure)) != 0;
        }


        /// <summary> The determination for one file. Project-authored files are proprietary and that is settled by
        /// their absence from the baseline. A vendor-derived file with any expression left keeps its MIT notice --
        /// modification does not end the grant, and MIT says so in the same sentence that permits it. The only path
        /// that moves a file OFF MIT requires the RM-78 baseline diff to exist: without it the file stays MIT and the
        /// result is marked not-determinate, because "we rewrote it" has to be a diff and not a recollection. That is
        /// the conservative direction, and it costs the product nothing -- the notice does not restrict selling, does
        /// not reach <c>City/</c>, and obliges publishing nothing. </summary>
        public static LicenceAfterOverhaul Resolve(string toolRelativePath, UpstreamRemnant remains,
            bool baselineDiffAvailable)
        {
            LicenceAfterOverhaul r = new LicenceAfterOverhaul();
            r.provenance = ProvenanceOf(toolRelativePath);
            r.remains = remains;
            if (r.provenance == FileProvenance.ProjectAuthored)
            {
                r.licence = SourceLicence.ProprietaryNew;
                r.determinate = true;
                r.needsBaselineDiff = false;
                r.note = "project-authored: not present in the vendor baseline";
                return r;
            }
            if (ExpressionRemains(remains))
            {
                r.licence = SourceLicence.UpstreamMit;
                r.determinate = true;
                r.needsBaselineDiff = false;
                r.note = "vendor-derived, modified: upstream expression remains (" + Names(remains) + ")";
                return r;
            }
            r.licence = SourceLicence.UpstreamMit;
            r.needsBaselineDiff = true;
            r.determinate = baselineDiffAvailable;
            r.note = baselineDiffAvailable
                ? "vendor-derived, REPLACED: no upstream expression remains and the baseline diff shows it"
                : "UNDETERMINED: replacement is claimed but the RM-78 vendor-baseline tag does not exist, so the MIT"
                  + " notice is retained until the diff can be produced";
            if (baselineDiffAvailable) { r.licence = SourceLicence.ProprietaryNew; }
            return r;
        }


        /// <summary> The remnant flags as words, for the report and the per-file paragraph. </summary>
        public static string Names(UpstreamRemnant remains)
        {
            if (remains == UpstreamRemnant.None) { return "none"; }
            List<string> parts = new List<string>();
            if ((remains & UpstreamRemnant.Structure) != 0) { parts.Add("structure"); }
            if ((remains & UpstreamRemnant.Algorithm) != 0) { parts.Add("algorithm"); }
            if ((remains & UpstreamRemnant.Identifiers) != 0) { parts.Add("identifiers"); }
            if ((remains & UpstreamRemnant.Comments) != 0) { parts.Add("comments"); }
            return string.Join(", ", parts.ToArray());
        }


        private static ReplacementFinding F(string file, UpstreamRemnant remains, string replaced, string remained)
        {
            ReplacementFinding f = new ReplacementFinding();
            f.file = file; f.remains = remains; f.replaced = replaced; f.remained = remained;
            return f;
        }


        // RM-106's written paragraph per rewritten file. Only vendor-derived files § 10 changed substantially are
        // here; a vendor file with no row was edited lightly or not at all and keeps its notice for the same reason.
        private static readonly ReplacementFinding[] _findings = new ReplacementFinding[]
        {
            F("Scripts/RootUtils.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments,
                "the Burst-discard attributes and the version guards around them (RM-104), which named a type this"
                    + " assembly cannot see in order to strip a call out of a job that can never exist",
                "Ease, Intersects2D, ProcessTangents and the XML helpers, arithmetic and names unchanged -- down to"
                    + " the author's own typo in Ease's summary ('If less than k1 ir greater than k2'), which is about"
                    + " as direct a demonstration of surviving expression as a file can offer"),

            F("Scripts/SplinatedMeshMaker.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments,
                "loading and saving, which now run through RaoFormat's versioned reader and refuse with a reason"
                    + " instead of a bare XmlSerializer call in a try/catch (RM-105)",
                "all 83 serialised fields under their upstream names. Those names ARE the .rao format, so no amount of"
                    + " change to the I/O around them can remove the upstream expression from this file"),

            F("Scripts/EdgeObjectMaker.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments,
                "the same routing through RaoFormat, and the load path that now takes a refused edge object back off"
                    + " the node rather than leaving a blank one behind (RM-105, RM-55)",
                "the 37 serialised fields, their names, and the placement arithmetic that reads them"),

            F("Scripts/RoadUtility.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers,
                "the record-separator constant, which now names this tool, plus two legacy constants added beside it"
                    + " so files written with the old separator keep reading for ever (RM-105)",
                "the separator scheme itself and the utility surface around it, unchanged in structure and naming"),

            F("Scripts/Intersections.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Identifiers | UpstreamRemnant.Comments,
                "the turn-lane marking constants and the missing else branch RM-94's census found -- 0.475/0.488/0.492"
                    + " and 0.03/0.015/0.01, each triple one metric figure written three ways, switched on a lane"
                    + " total with no branch for an odd one",
                "the class structure, the iFLane/iBLane naming and the order the markings are walked in. This is the"
                    + " clearest case in the tree of RM-106's own sentence: the file's expression is still the"
                    + " original author's, wearing new arithmetic"),

            F("Scripts/Terrain/TerraformingThreaded.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments,
                "the threading and the reporting (RM-100, RM-101): the work is driven as a Task and its progress goes"
                    + " to BuildStatusModel instead of a modal progress bar raised from inside the terraform loop",
                "the terraform loop, its height comparisons and every identifier in them -- including the support"
                    + " tolerance branch RM-94 still has open"),

            F("Scripts/Road.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments,
                "clamp bands, sourced against AASHTO where a standard governs them, and the progress reporting"
                    + " (RM-93, RM-94, RM-101)",
                "the class, its serialised fields and the sequence UpdateRoad drives them in"),

            F("Scripts/Editor/RoadEditor.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Identifiers | UpstreamRemnant.Comments,
                "the chrome around the inspector: LegacyInspectorShell now draws the product header, the is-building"
                    + " line and RM-51's foldouts, and rebuild requests coalesce (RM-102)",
                "the inspector body itself, deliberately -- a shell rather than a rewrite, because the legacy editors"
                    + " encode undocumented knowledge of which buttons are destructive. The same applies to"
                    + " RoadSystemEditor.cs, SplineNEditor.cs and TerrainEditor.cs"),

            F("Scripts/Editor/EditorUtilities.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Identifiers | UpstreamRemnant.Comments,
                "every online link (RM-102): wiki, repository and issue tracker now point at this project's own"
                    + " repository, and the offline manual is resolved from the tool folder rather than a fixed path",
                "LoadTexture<T>, DrawLine and the class around them, upstream underscore parameters and the"
                    + " '//Horizontal bar' comment included"),

            F("Scripts/Editor/Wizard.cs",
                UpstreamRemnant.Structure | UpstreamRemnant.Identifiers | UpstreamRemnant.Comments,
                "library reads, which go through RaoFormat.TryReadFile and report a refusal by file and cause, and a"
                    + " refused load now removes the empty object from the node (RM-105, RM-55)",
                "the window itself: DoGUI's layout arithmetic, DoItem, the thumbnail style and the objectList naming")
        };


        /// <summary> The written determinations, in declaration order so two runs read alike. </summary>
        public static ReplacementFinding[] Findings()
        {
            ReplacementFinding[] copy = new ReplacementFinding[_findings.Length];
            Array.Copy(_findings, copy, _findings.Length);
            return copy;
        }


        /// <summary> The paragraph for one file, if it has one. Matched on the tool-relative path. </summary>
        public static bool TryFindingFor(string toolRelativePath, out ReplacementFinding finding)
        {
            finding = new ReplacementFinding();
            if (string.IsNullOrEmpty(toolRelativePath)) { return false; }
            string p = toolRelativePath.Replace('\\', '/');
            for (int i = 0; i < _findings.Length; i++)
            {
                if (string.Equals(_findings[i].file, p, StringComparison.Ordinal)) { finding = _findings[i]; return true; }
            }
            return false;
        }


        /// <summary> What remains in a file. A vendor-derived file with no written paragraph is treated as fully
        /// intact -- RM-106's rule that a file with no justification keeps its MIT header, expressed as the default
        /// rather than as a special case somewhere downstream. </summary>
        public static UpstreamRemnant RemnantFor(string toolRelativePath)
        {
            ReplacementFinding finding;
            if (TryFindingFor(toolRelativePath, out finding)) { return finding.remains; }
            if (ProvenanceOf(toolRelativePath) == FileProvenance.VendorDerived)
            {
                return UpstreamRemnant.Structure | UpstreamRemnant.Algorithm | UpstreamRemnant.Identifiers
                    | UpstreamRemnant.Comments;
            }
            return UpstreamRemnant.None;
        }


        /// <summary> The determination as text, generated from this register so the notice on disk, the manual and
        /// the gate cannot drift apart. RM-106 asks for the licence position re-tested file by file once the legacy
        /// files have been rewritten; this is that answer, written in one place and read from three. </summary>
        public static string Describe()
        {
            StringBuilder sb = new StringBuilder(8192);
            sb.Append("UPSTREAM PROVENANCE AND LICENCE POSITION").Append('\n');
            sb.Append("=======================================").Append('\n').Append('\n');
            sb.Append("This tool began as a vendored MIT-licensed road builder and was rewritten in place. Two")
              .Append(" questions follow, and they have different answers: which files came from the download, and")
              .Append(" which licence each file is under now.").Append('\n').Append('\n');
            sb.Append("The vendor set is frozen at ").Append(VendorFileCount.ToString(CultureInfo.InvariantCulture))
              .Append(" files, listed by name in this register; everything else in the tool was written for this")
              .Append(" project. The vendored files are the ones enumerated, not the new ones, because the vendor")
              .Append(" list cannot grow -- it is what a tag contains. A list of new files would need a line added")
              .Append(" every time one was written, and would be wrong the first time somebody forgot.")
              .Append('\n').Append('\n');
            sb.Append("Provenance is matched on file name, not on folder. Files were moved during the overhaul")
              .Append(" -- the spline and terraforming sources now sit in subfolders -- and moving a file is not a")
              .Append(" licence event. Nor is the reverse: a proprietary file placed under Scripts/ is proprietary,")
              .Append(" so classification by path alone would credit the upstream author with work they did not")
              .Append(" write, in a product sold under different terms. That is a false attribution in both")
              .Append(" directions, and it is why provenance is asked before the header is stamped.")
              .Append('\n').Append('\n');
            sb.Append("A file keeps the upstream MIT notice while upstream expression remains in it. Copyright")
              .Append(" reaches expression, not ideas: 17 U.S.C. 102(b) excludes any idea, procedure, process,")
              .Append(" system, method of operation, concept, principle or discovery, and the")
              .Append(" abstraction-filtration-comparison test filters those out before two programs are compared.")
              .Append(" An algorithm carried across is therefore not by itself protected expression, while comments,")
              .Append(" identifiers and structure each are. Only when all three are gone does the notice come off,")
              .Append(" and then only against a diff to the vendor baseline tag: \"we rewrote it\" is a diff, not a")
              .Append(" recollection. Until that diff can be produced the notice stays, which is the conservative")
              .Append(" direction and costs nothing.").Append('\n').Append('\n');
            sb.Append("FILE BY FILE").Append('\n');
            sb.Append("The files below were changed enough during the overhaul to be worth a written determination.")
              .Append(" A vendored file with no entry here was edited lightly or not at all, and keeps its notice")
              .Append(" for the same reason -- the absence of a justification is not a replacement.")
              .Append('\n').Append('\n');
            for (int i = 0; i < _findings.Length; i++)
            {
                LicenceAfterOverhaul r = Resolve(_findings[i].file, _findings[i].remains, false);
                sb.Append(_findings[i].file).Append('\n');
                sb.Append("  replaced: ").Append(_findings[i].replaced).Append('\n');
                sb.Append("  remains:  ").Append(_findings[i].remained).Append('\n');
                sb.Append("  verdict:  ").Append(Names(_findings[i].remains)).Append(" -- ");
                sb.Append(r.licence == SourceLicence.UpstreamMit ? "MIT notice retained" : "proprietary");
                sb.Append('\n').Append('\n');
            }
            sb.Append("THE POSITION").Append('\n').Append('\n');
            sb.Append("Every entry above retains the notice, and no file in this tool has moved off MIT. That is the")
              .Append(" honest result rather than a disappointing one: a rewrite that keeps the field names, the call")
              .Append(" order and the original comments has changed the arithmetic, not the expression. Retaining")
              .Append(" the notice costs nothing that matters here -- MIT does not restrict selling, it does not")
              .Append(" reach the work written for this project, and it obliges publishing nothing. It asks that the")
              .Append(" copyright line and the permission notice travel with the copies, which is what the")
              .Append(" third-party notice and the per-file headers do.").Append('\n').Append('\n');
            sb.Append("TWO LIMITS").Append('\n').Append('\n');
            sb.Append("This is engineering evidence, not legal advice. It records which files changed, how much, and")
              .Append(" what remains of the original, which is what somebody qualified needs in front of them; the")
              .Append(" advice itself is theirs to give and is tracked separately.").Append('\n').Append('\n');
            sb.Append("And nothing downstream should depend on the notice coming off. Retaining it permits a customer")
              .Append(" to redistribute those files -- not the module built on top of them. Removing it would buy a")
              .Append(" small tidiness at the price of the one licence risk in this project worth avoiding.")
              .Append('\n');
            return sb.ToString();
        }
    }
}
