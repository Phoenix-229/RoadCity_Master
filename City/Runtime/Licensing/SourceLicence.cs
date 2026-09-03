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
    /// share one authority rather than each re-deciding. § 10.2's boundary is the rule: new work lives under
    /// <c>City/</c> and is proprietary; everything else in the tool tree is upstream MIT. The header, when present,
    /// is authoritative over the path so a new file that lives outside <c>City/</c> can still declare itself
    /// proprietary honestly. </summary>
    public static class LicenceRule
    {
        // Anchor phrases from the two RM-79 header templates. Chosen so neither is a substring of the other, and
        // so neither collides with the "LICENSE.md" / "LICENSE-MIT.txt" file names both headers happen to mention.
        private const string ProprietaryAnchor = "authored for this project";
        private const string MitAnchor = "Upstream file, MIT licensed";

        /// <summary> The path rule: a file whose tool-relative path has a <c>City</c> segment is proprietary new
        /// work; anything else in the tool tree is upstream MIT. Total by construction -- exactly one value, always.
        /// </summary>
        public static SourceLicence ClassifyByPath(string toolRelativePath)
        {
            if (string.IsNullOrEmpty(toolRelativePath)) { return SourceLicence.UpstreamMit; }
            string p = toolRelativePath.Replace('\\', '/').Trim();
            if (p.Length > 0 && p[0] == '/') { p = p.Substring(1); }
            string lower = p.ToLowerInvariant();
            if (lower == "city" || lower.StartsWith("city/", StringComparison.Ordinal)
                || lower.Contains("/city/"))
            {
                return SourceLicence.ProprietaryNew;
            }
            return SourceLicence.UpstreamMit;
        }

        /// <summary> Reads what a file's header declares, from its text, by the two anchor phrases. Carrying both is
        /// Conflicting; carrying neither is Missing. </summary>
        public static LicenceHeaderState HeaderStateOf(string fileText)
        {
            if (string.IsNullOrEmpty(fileText)) { return LicenceHeaderState.Missing; }
            bool hasProp = fileText.Contains(ProprietaryAnchor);
            bool hasMit = fileText.Contains(MitAnchor);
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
