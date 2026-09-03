#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-37's gate: proves the tunnel core without a scene -- the lining that encloses the cavity, the
    /// cavity release ledger that catches an orphaned hole, the under-water cover rule that refuses and names the
    /// station where the cover runs out, and the mode decision. It asserts that the lining's outer radius is
    /// larger than the bore so no terrain sliver shows through; that opening two cavities and releasing one leaves
    /// exactly one orphan the tool can report, and releasing both leaves none; that a bore with adequate cover
    /// under water is sealed-legal while one that surfaces into the bed is refused at the failing station; that
    /// standalone mode yields lined-but-not-cut geometry (never a faked hole) while connected mode cuts; and that
    /// everything is bit-identical on repeat (C-06). Requesting the real cavity, the swept lining and the portal
    /// headwalls are the deferred half. Report lines are prefixed RCMTUN. </summary>
    public static class CityTunnelCavityGate
    {
        [MenuItem("Window/Road City Master/Testing/City tunnel (cavity + underwater seal)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityTunnelCavityGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMTUN report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMTUN BEGIN\n");
            List<string> failures = new List<string>();

            CaseLiningEnclosesCavity(builder, failures);
            CaseReleaseLedgerCatchesOrphan(builder, failures);
            CaseUnderWaterCoverRefuses(builder, failures);
            CaseModeDecidesCut(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMTUN FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMTUN result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMTUN END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The lining's outer radius is larger than the bore, so it encloses the cavity with no sliver. </summary>
        private static void CaseLiningEnclosesCavity(StringBuilder builder, List<string> failures)
        {
            TunnelClass cls = TunnelClass.Default(); // bore 4, oversize 0.15
            double outer = TunnelCavity.LiningOuterRadiusMetres(cls.boreRadiusMetres, cls);
            Expect(Math.Abs(outer - 4.15) < 1e-9, "lining: the outer radius was not bore + oversize", failures);
            Expect(TunnelCavity.LiningEnclosesCavity(outer, cls.boreRadiusMetres),
                "lining: the lining did not strictly enclose the cavity (a sliver would show)", failures);
            Expect(!TunnelCavity.LiningEnclosesCavity(cls.boreRadiusMetres, cls.boreRadiusMetres),
                "lining: a lining equal to the bore was reported as enclosing it", failures);
            builder.Append("RCMTUN ").Append("lining-encloses".PadRight(22))
                   .Append("lining outer radius > bore, no terrain sliver\n");
        }

        /// <summary> Opening two cavities and releasing one leaves one orphan; releasing both leaves none. </summary>
        private static void CaseReleaseLedgerCatchesOrphan(StringBuilder builder, List<string> failures)
        {
            TunnelCavityLedger ledger = new TunnelCavityLedger();
            ledger.RequestOpening(1);
            ledger.RequestOpening(2);
            Expect(ledger.OpenCount == 2, "ledger: two openings were not both recorded", failures);

            ledger.ReleaseOpening(1);
            Expect(ledger.OpenCount == 1 && ledger.HasOrphanedCavities,
                "ledger: a forgotten release left no reportable orphan", failures);

            ledger.ReleaseOpening(2);
            Expect(ledger.OpenCount == 0 && !ledger.HasOrphanedCavities,
                "ledger: releasing every opening still reported an orphan", failures);
            builder.Append("RCMTUN ").Append("release-ledger".PadRight(22))
                   .Append("orphaned cavity caught, clean release clears it\n");
        }

        /// <summary> A bore with adequate cover under water is legal; one that surfaces into the bed is refused at
        /// the failing station. </summary>
        private static void CaseUnderWaterCoverRefuses(StringBuilder builder, List<string> failures)
        {
            TunnelClass cls = TunnelClass.Default(); // min cover 3 m
            // Bed well above the crown the whole way -> cover >= 3 -> legal.
            double[] bedOk = { 20, 20, 20, 20 };
            double[] crownOk = { 15, 15, 16, 15 }; // covers 5,5,4,5
            TunnelCavity.CoverScan ok = TunnelCavity.ScanWaterCover(bedOk, crownOk, cls);
            Expect(ok.sealedLegal && ok.failingStationIndex < 0,
                "cover: an adequately covered bore under water was refused", failures);

            // The crown rises to meet the bed at station 2 -> cover 1 m < 3 -> refused there.
            double[] bedBad = { 20, 20, 20, 20 };
            double[] crownBad = { 15, 16, 19, 15 }; // covers 5,4,1,5 -> fails first at index 2
            TunnelCavity.CoverScan bad = TunnelCavity.ScanWaterCover(bedBad, crownBad, cls);
            Expect(!bad.sealedLegal && bad.failingStationIndex == 2 && Math.Abs(bad.worstCoverMetres - 1.0) < 1e-9,
                "cover: a bore that lost cover under water was not refused at the failing station", failures);
            builder.Append("RCMTUN ").Append("water-cover".PadRight(22))
                   .Append("under water sealed only with cover, else refused\n");
        }

        /// <summary> Standalone yields lined-but-not-cut geometry; connected cuts, unless under water without
        /// cover, which is refused. </summary>
        private static void CaseModeDecidesCut(StringBuilder builder, List<string> failures)
        {
            TunnelClass cls = TunnelClass.Default();
            TunnelCavity.CoverScan dry = new TunnelCavity.CoverScan(); // unused when not under water

            Expect(TunnelCavity.ResolveBuild(TunnelMode.Standalone, false, dry)
                   == TunnelBuildOutcome.LinedButNotCut,
                "mode: standalone did not report lined-but-not-cut (it must not fake a hole)", failures);
            Expect(TunnelCavity.ResolveBuild(TunnelMode.Connected, false, dry)
                   == TunnelBuildOutcome.CutAndLined,
                "mode: connected on dry land did not cut and line the bore", failures);

            double[] bed = { 20, 20 };
            double[] crown = { 19, 19 }; // cover 1 < 3
            TunnelCavity.CoverScan noCover = TunnelCavity.ScanWaterCover(bed, crown, cls);
            Expect(TunnelCavity.ResolveBuild(TunnelMode.Connected, true, noCover)
                   == TunnelBuildOutcome.RefusedInsufficientCover,
                "mode: a connected bore under water without cover was not refused", failures);
            builder.Append("RCMTUN ").Append("mode-decides".PadRight(22))
                   .Append("standalone lines only, connected cuts, water refuses\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            TunnelClass cls = TunnelClass.Default();
            double a = TunnelCavity.LiningOuterRadiusMetres(3.7, cls);
            double b = TunnelCavity.LiningOuterRadiusMetres(3.7, cls);
            double[] bed = { 12, 11, 13 };
            double[] crown = { 8, 9, 7 };
            TunnelCavity.CoverScan s1 = TunnelCavity.ScanWaterCover(bed, crown, cls);
            TunnelCavity.CoverScan s2 = TunnelCavity.ScanWaterCover(bed, crown, cls);
            Expect(a == b && s1.worstCoverMetres == s2.worstCoverMetres
                   && s1.failingStationIndex == s2.failingStationIndex,
                "determinism: two identical tunnel computations produced different values", failures);
            builder.Append("RCMTUN ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical lining/cover\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
