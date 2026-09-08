// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster
{
    /// <summary> Everything a verification run has to mint, stamp or generate BEFORE the gates are asked anything, in
    /// one call.
    ///
    /// Three kinds of artefact in this tool are produced from code rather than typed by hand: the markdown documents
    /// that come out of the registers, the URP flare data asset that Unity itself has to create, and the script icons
    /// written into component <c>.meta</c> files. Each has its own menu item, which is right for an author changing one
    /// thing and wrong for a headless run — an operator nobody clicked is indistinguishable in a log from a defect, so
    /// the suite reports table drift, a missing asset and five default badges as seven failures that no edit caused.
    /// <see cref="RcmDocumentWriters"/> already made that argument for the documents; this makes it for the assets too,
    /// so one <c>-executeMethod</c> covers the whole preparation instead of three Unity launches.
    ///
    /// WHAT IS DELIBERATELY NOT HERE: <c>MaterialPortOperator</c>. It rewrites 471 material assets, which is a
    /// conversion performed once and reviewed, not a step a verification run may repeat behind the author's back. The
    /// three operators below only ever write what the register already says they should, so re-running them is a no-op
    /// on a clean tree — which is exactly the property that makes them safe to run unattended.
    ///
    /// Not named <c>Run(out string)</c>: <see cref="RcmGateSuite"/> discovers gates by that signature and this is not a
    /// gate. It is the preparation a gate run assumes has already happened. </summary>
    public static class RcmPrepareShip
    {
        private const string Prefix = "RCMPREP";
        private const string MenuPrepare = "Window/Road City Master/Setup/Mint, stamp and write everything generated";

        [MenuItem(MenuPrepare, false, 520)]
        public static void PrepareFromMenu()
        {
            string report;
            bool ok = Prepare(out report);
            if (ok) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.RcmPrepareShip.PrepareBatch. Exits 1 when any step failed. </summary>
        public static void PrepareBatch()
        {
            string report;
            bool ok = Prepare(out report);
            Debug.Log(report);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary> Runs all three steps and reports each one. Every step runs even after an earlier failure, because
        /// "what is still not generated" is the question the report exists to answer, and stopping at the first failure
        /// hides the rest — the same reason <see cref="RcmDocumentWriters.WriteAll"/> does not stop either.
        ///
        /// The asset steps come first and the single <see cref="AssetDatabase.Refresh"/> comes last: the flare mint and
        /// the icon stamp both touch files the importer owns, the documents are ordinary text writes, and one import
        /// pass at the end covers all of them rather than three. </summary>
        public static bool Prepare(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Prefix).Append(" BEGIN\n");
            List<string> failures = new List<string>();

            string flareReport;
            bool flareOk = FlarePortOperator.Mint(out flareReport);
            Step("the URP lens flare data asset", flareOk, flareReport, builder, failures);

            string iconReport;
            bool iconOk = RcmScriptIcons.StampAll(true, out iconReport);
            Step("the script icons", iconOk, iconReport, builder, failures);

            string docReport;
            bool docsOk = RcmDocumentWriters.WriteAll(out docReport);
            Step("the generated documents", docsOk, docReport, builder, failures);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            for (int i = 0; i < failures.Count; i++)
            {
                builder.Append(Prefix).Append(" FAIL ").Append(failures[i]).Append('\n');
            }
            builder.Append(Prefix).Append(" result ").Append(failures.Count == 0 ? "PASS" : "FAIL")
                .Append("  failures ").Append(failures.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
            builder.Append(Prefix).Append(" END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> One step, with the operator's own report quoted rather than paraphrased: it knows what it wrote and
        /// a second, shorter account of that is only a way to lose the detail. </summary>
        private static void Step(string what, bool ok, string operatorReport, StringBuilder builder,
            List<string> failures)
        {
            builder.Append(Prefix).Append(ok ? " ran     " : " FAILED  ").Append(what).Append('\n');
            builder.Append(operatorReport == null ? string.Empty : operatorReport).Append('\n');
            if (!ok) { failures.Add(what + " did not complete; its own report is above"); }
        }
    }
}
#endif
