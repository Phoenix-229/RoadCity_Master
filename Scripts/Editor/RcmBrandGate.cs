// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Verifies that the images a buyer sees are actually present and actually wired: the two shipped brand
    /// PNGs, their sizes, and the script icon on every component that appears in a scene.
    ///
    /// This exists because every one of those failures is invisible during development and obvious in a store listing.
    /// The inspector header draws nothing when its PNG is missing rather than falling back to a coloured band, so the
    /// only way a missing brand file announces itself is a gate that looks for it. The script icons live in .meta
    /// files, which are exactly the files a careless package export or a version-control ignore rule drops.
    ///
    /// Read-only by construction: it loads, measures and compares, and never stamps. <see cref="RcmScriptIcons"/> is
    /// the half that writes, and it is deliberately not a gate so that running the suite cannot modify the project.
    /// Report lines are prefixed RCMBRAND. </summary>
    public static class RcmBrandGate
    {
        #region "Vars"
        private const string Prefix = "RCMBRAND";

        /// <summary> Edge of the square icon cut. The banner is split into a 512-wide half and a 128 px square icon by
        /// the brand-split operator in Universe Creator; this gate re-states the number rather than referencing that
        /// operator so the RoadCityMaster editor assembly does not acquire a dependency on the Universe Creator editor
        /// assembly just to read one constant. If the split ever changes, both places change together. </summary>
        private const int IconEdge = 128;

        /// <summary> The brand half is 512 x 506: the 1024 x 506 source image cut down the middle. Asserted exactly,
        /// because a masthead that silently arrived at some other size means the split did not run on the image the
        /// author thinks it ran on. </summary>
        private const int BannerWidth = 512;
        private const int BannerHeight = 506;

        private const string BannerFileName = "ManualImages/RoadCityMaster.png";
        #endregion


        #region "Entry points"
        [MenuItem("Window/Road City Master/Testing/Brand images and script icons")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); } else { Debug.LogError(report); }
        }


        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            int failures = 0;

            failures += CheckImage(builder, RcmBrandHeader.IconAssetPath(), IconEdge, IconEdge, "icon");
            failures += CheckImage(builder, BannerPath(), BannerWidth, BannerHeight, "banner");
            failures += CheckIcons(builder);

            builder.Append(Prefix).Append(" result ").Append(failures == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures);

            report = builder.ToString();
            return failures == 0;
        }
        #endregion


        #region "Checks"
        private static string BannerPath()
        {
            string basePath = RoadEditorUtility.GetBasePath();
            if (string.IsNullOrEmpty(basePath)) { return string.Empty; }

            string path = basePath.Replace('\\', '/');
            if (!path.EndsWith("/")) { path += "/"; }
            return path + BannerFileName;
        }


        /// <summary> One brand PNG: on disk, loadable as a texture, and the size it is supposed to be.
        ///
        /// The size check is not pedantry. A texture importer set to a small max size, or a non-power-of-two scaling
        /// rule, silently changes what the loaded texture reports, and a masthead that arrives at 256 px looks merely
        /// slightly soft in a browser while being visibly wrong in an inspector drawn at a fixed 40 px -- the failure
        /// nobody notices until a screenshot goes on a store page. </summary>
        private static int CheckImage(StringBuilder builder, string path, int width, int height, string label)
        {
            if (string.IsNullOrEmpty(path))
            {
                builder.Append(Prefix).Append(" FAIL  ").Append(label)
                       .Append("  the tool root could not be resolved, so no brand path could be built\n");
                return 1;
            }

            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                builder.Append(Prefix).Append(" FAIL  ").Append(label).Append("  not found at ").Append(path).Append("\n");
                return 1;
            }

            if (tex.width != width || tex.height != height)
            {
                builder.Append(Prefix).Append(" FAIL  ").Append(label).Append("  ").Append(path)
                       .Append("  is ").Append(tex.width).Append(" x ").Append(tex.height)
                       .Append(", expected ").Append(width).Append(" x ").Append(height).Append("\n");
                return 1;
            }

            builder.Append(Prefix).Append(" ok    ").Append(label).Append("  ").Append(path)
                   .Append("  ").Append(tex.width).Append(" x ").Append(tex.height).Append("\n");
            return 0;
        }


        /// <summary> Every component an author sees in a scene carries the mark rather than Unity's default script
        /// badge. The census is produced by the stamper's own dry run, so the gate cannot disagree with the operator
        /// about which components are in scope -- one list, read twice, instead of two lists that drift. </summary>
        private static int CheckIcons(StringBuilder builder)
        {
            string stamperReport;
            int pending, already, missing;
            bool resolved = RcmScriptIcons.StampAll(false, out stamperReport, out pending, out already, out missing);
            builder.Append(stamperReport);

            if (!resolved)
            {
                builder.Append(Prefix).Append(" FAIL  script icons  ").Append(missing)
                       .Append(" target component(s) could not be resolved, or the mark could not be written")
                       .Append(" -- see the RCMICON lines above, which name each one and say which of the two it was\n");
                return 1;
            }

            if (pending > 0)
            {
                builder.Append(Prefix).Append(" FAIL  script icons  ").Append(pending)
                       .Append(" component(s) still carry Unity's default script badge -- run ")
                       .Append("Window/Road City Master/Setup/Stamp script icons\n");
                return 1;
            }

            builder.Append(Prefix).Append(" ok    script icons  all ").Append(already)
                   .Append(" scene components carry the brand mark\n");
            return 0;
        }
        #endregion
    }
}
#endif
