// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The distribution form a build can take (RM-81). The trade-off is real and § 10.40 lays it out:
    /// full source protects nothing technically but is what Unity customers expect and can debug; a compiled
    /// proprietary assembly is only a speed bump because .NET decompiles in seconds and costs the customer the
    /// ability to read or patch; a mixed build makes the boundary visible in the package but doubles the build
    /// paths. </summary>
    public enum DistributionForm
    {
        FullSource = 0,
        CompiledProprietary = 1,
        Mixed = 2
    }

    /// <summary> RM-81's packaging rules, pure and deterministic so the gate can prove them without touching disk.
    /// The chosen form is stated here, not left to default: full source, because the legal boundary is already
    /// legible from the per-file headers and notice files (RM-80), so obfuscation would buy a speed bump at the
    /// cost of the debuggability customers expect -- the EULA, the notices and RM-78's baseline tag are the
    /// protection, not the packaging. </summary>
    public static class PackageRule
    {
        public const DistributionForm ChosenForm = DistributionForm.FullSource;

        /// <summary> The four files a built package must contain (RM-80's notices), by base name. </summary>
        public static IReadOnlyList<string> RequiredNoticeFiles()
        {
            return new[] { "LICENSE.md", "LICENSE-MIT.txt", "THIRD-PARTY-NOTICES.md", "NOTICE.md" };
        }

        /// <summary> Every shipped file that Unity imports needs a paired `.meta` so its GUID survives the move
        /// into a clean project (the RM-01/RM-78 GUID rule, one last time at the point of sale). The `.meta`
        /// files themselves are the only exception. </summary>
        public static bool RequiresMeta(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) { return false; }
            return !relPath.EndsWith(".meta", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary> Whether a base name is one of the required notice files. </summary>
        public static bool IsNoticeFile(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) { return false; }
            IReadOnlyList<string> req = RequiredNoticeFiles();
            for (int i = 0; i < req.Count; i++)
            {
                if (string.Equals(req[i], baseName, StringComparison.Ordinal)) { return true; }
            }
            return false;
        }

        /// <summary> The stated reasoning for the chosen form, so the decision travels with the code. </summary>
        public static string Rationale()
        {
            return "Full source: the boundary is already legible from per-file headers and notice files (RM-80); "
                + "a compiled assembly is a speed bump that decompiles in seconds and costs debuggability; the EULA, "
                + "notices and vendor-baseline tag (RM-78) are the protection, not obfuscation.";
        }
    }
}
