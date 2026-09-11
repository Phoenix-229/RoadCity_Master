// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The two -- and only two -- licences a source file in the shipped product can carry (RM-79). A
    /// file is either upstream code that arrived under MIT (modified or not, it stays MIT) or a new file authored
    /// for this project (proprietary, governed by the product EULA). There is deliberately no third value: the one
    /// state RM-79 forbids is a file that is BOTH, and that is reported as ambiguity, never as a licence.
    /// </summary>
    public enum SourceLicence
    {
        UpstreamMit = 0,
        ProprietaryNew = 1
    }

    /// <summary> What a file's own header actually declares, read from its text. Missing means no licence header at
    /// all (a defect RM-80 fixes and RM-70 catches); ProprietaryOnly and MitModifiedOnly are the two good states;
    /// Conflicting means the file carries BOTH headers -- the genuinely ambiguous case RM-79 names, where MIT code
    /// and proprietary code have been smeared into one file. </summary>
    public enum LicenceHeaderState
    {
        Missing = 0,
        ProprietaryOnly = 1,
        MitModifiedOnly = 2,
        Conflicting = 3
    }

    /// <summary> The result of resolving one file: which licence it carries, what its header says, and whether that
    /// answer is determinate. Determinate is true only when the header alone settles it; a Missing header falls back
    /// to the path rule but is NOT determinate (RM-80 has to write the header), and a Conflicting header is never
    /// determinate. </summary>
    public struct FileLicenceResult
    {
        public SourceLicence licence;
        public LicenceHeaderState header;
        public bool determinate;
        public string note;
    }

    /// <summary> The machine-readable form of RM-79's written position: every source file resolves to exactly one
    /// licence, provably, with no file left ambiguous. The rule is pure and total -- <see cref="ClassifyByPath"/>
    /// always returns exactly one <see cref="SourceLicence"/> -- so RM-80 (headers on disk) and RM-70 (the check)
    /// share one authority rather than each re-deciding. RM-106's boundary is the rule: only the frozen set of
    /// vendored filenames is upstream MIT; every other source in the tool tree is proprietary new work. The header,
    /// when present, is authoritative over the path, so a vendored file a maintainer has since rewritten can still
    /// declare itself proprietary honestly, and the default falls to proprietary rather than crediting the upstream
    /// author with work they did not write. </summary>
    public static class LicenceRule
    {
        // Anchor phrases from the two RM-79 header templates. Chosen so neither is a substring of the other, and
        // so neither collides with the "LICENSE.md" / "LICENSE-MIT.txt" file names both headers happen to mention.
        private const string ProprietaryAnchor = "authored for this project";
        private const string MitAnchor = "Upstream file, MIT licensed";

        /// <summary> The path rule: a file whose NAME is in the frozen vendored set
        /// (<see cref="UpstreamProvenanceRule.IsVendorFile"/>) is upstream MIT; every other source in the tool tree
        /// is proprietary new work. Matching is by filename, not path, so § 10 moving a vendored file into a
        /// subfolder is not a licence change. Total by construction -- exactly one value, always. </summary>
        public static SourceLicence ClassifyByPath(string toolRelativePath)
        {
            if (string.IsNullOrEmpty(toolRelativePath)) { return SourceLicence.ProprietaryNew; }
            string p = toolRelativePath.Replace('\\', '/').Trim();
            if (p.Length > 0 && p[0] == '/') { p = p.Substring(1); }
            int cut = p.LastIndexOf('/');
            string fileName = cut >= 0 ? p.Substring(cut + 1) : p;
            return UpstreamProvenanceRule.IsVendorFile(fileName)
                ? SourceLicence.UpstreamMit : SourceLicence.ProprietaryNew;
        }

        /// <summary> Reads what a file's header declares, from its text, by the two anchor phrases. Carrying both is
        /// Conflicting; carrying neither is Missing. </summary>
        public static LicenceHeaderState HeaderStateOf(string fileText)
        {
            if (string.IsNullOrEmpty(fileText)) { return LicenceHeaderState.Missing; }
            string[] lines = fileText.Split(new[] { '\n' }, StringSplitOptions.None);
            bool hasProp = false;
            bool hasMit = false;
            int limit = lines.Length < 12 ? lines.Length : 12;
            for (int i = 0; i < limit; i++)
            {
                string line = lines[i].TrimStart('\uFEFF', ' ', '\t', '\r');
                if (line.StartsWith("// New file, authored for this project", StringComparison.Ordinal)
                    || line == "// authored for this project")
                {
                    hasProp = true;
                }
                if (line.StartsWith("// Upstream file, MIT licensed", StringComparison.Ordinal)
                    || line == "// Upstream file, MIT licensed")
                {
                    hasMit = true;
                }
            }
            if (hasProp && hasMit) { return LicenceHeaderState.Conflicting; }
            if (hasProp) { return LicenceHeaderState.ProprietaryOnly; }
            if (hasMit) { return LicenceHeaderState.MitModifiedOnly; }
            return LicenceHeaderState.Missing;
        }

        /// <summary> Resolves one file to a single licence. The header, when unambiguous, is authoritative; a Missing
        /// header falls back to the path rule but is flagged not-determinate (RM-80 must write it); a Conflicting
        /// header is the forbidden ambiguous state and is never determinate. </summary>
        public static FileLicenceResult Resolve(string toolRelativePath, string fileText)
        {
            FileLicenceResult r = new FileLicenceResult();
            r.header = HeaderStateOf(fileText);
            switch (r.header)
            {
                case LicenceHeaderState.ProprietaryOnly:
                    r.licence = SourceLicence.ProprietaryNew; r.determinate = true;
                    r.note = "header declares proprietary"; break;
                case LicenceHeaderState.MitModifiedOnly:
                    r.licence = SourceLicence.UpstreamMit; r.determinate = true;
                    r.note = "header declares upstream MIT"; break;
                case LicenceHeaderState.Conflicting:
                    r.licence = ClassifyByPath(toolRelativePath); r.determinate = false;
                    r.note = "AMBIGUOUS: file carries both licence headers"; break;
                default:
                    r.licence = ClassifyByPath(toolRelativePath); r.determinate = false;
                    r.note = "no licence header; classified by path (RM-80 must add the header)"; break;
            }
            return r;
        }

        /// <summary> Which anchor a file of the given licence must carry, so RM-80 can write and RM-70 can check the
        /// same template the rule reads. </summary>
        public static string ExpectedHeaderAnchor(SourceLicence licence)
        {
            return licence == SourceLicence.ProprietaryNew ? ProprietaryAnchor : MitAnchor;
        }
    }
}
