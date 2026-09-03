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
    /// <summary> RM-33's gate: proves the bridge-type spec validation and the support-placement / conflict maths
    /// without a scene. It asserts that an unfilled spec sheet is REFUSED with a message rather than built with
    /// defaults (and an inverted span range and a spacing-less evenly-spaced type too); that a span is checked
    /// against the type's authored range; that an arch lands only on its abutments (no intermediate piers); that a
    /// beam fills evenly-spaced piers no farther apart than the max spacing; that a suspension bridge's supports
    /// follow its authored tower fractions rather than span count; that a support in water is allowed only when the
    /// type permits it; that a pier landing on the network below is shifted within its allowed range and FLAGGED if
    /// it cannot clear; and that everything is bit-identical on repeat (C-06). Terrain fitting, prefab swap, the
    /// catenary cable (RM-23) and the deck mesh are the deferred half. Report lines are prefixed RCMBRIDGE. </summary>
    public static class CityBridgeTypesGate
    {
        [MenuItem("Window/Road City Master/Testing/City bridge types (supports)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityBridgeTypesGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMBRIDGE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMBRIDGE BEGIN\n");
            List<string> failures = new List<string>();

            CaseUnspecifiedRefused(builder, failures);
            CaseSpanRange(builder, failures);
            CaseArchNoIntermediatePiers(builder, failures);
            CaseBeamEvenlySpaced(builder, failures);
            CaseSuspensionFollowsTowers(builder, failures);
            CaseWaterRule(builder, failures);
            CaseConflictShiftThenFlag(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMBRIDGE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMBRIDGE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMBRIDGE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> An unfilled spec is refused with a message; an inverted span range and a spacing-less
        /// evenly-spaced type are refused; a filled beam spec is accepted. </summary>
        private static void CaseUnspecifiedRefused(StringBuilder builder, List<string> failures)
        {
            BridgeTypes.SpecValidation none = BridgeTypes.ValidateSpec(BridgeTypeSpec.Unspecified());
            Expect(!none.accepted && !string.IsNullOrEmpty(none.message),
                "spec: an unfilled bridge-type sheet was not refused with a message", failures);

            BridgeTypeSpec inverted = BridgeTypeSpec.Beam();
            inverted.minSpanMetres = 400f; // now min > max
            Expect(!BridgeTypes.ValidateSpec(inverted).accepted,
                "spec: an inverted span range was not refused", failures);

            BridgeTypeSpec noSpacing = BridgeTypeSpec.Beam();
            noSpacing.maxPierSpacingMetres = 0f;
            Expect(!BridgeTypes.ValidateSpec(noSpacing).accepted,
                "spec: an evenly-spaced type with no pier spacing was not refused", failures);

            Expect(BridgeTypes.ValidateSpec(BridgeTypeSpec.Beam()).accepted,
                "spec: a filled beam spec was refused", failures);
            builder.Append("RCMBRIDGE ").Append("spec-refusal".PadRight(20))
                   .Append("unfilled / invalid spec refused with a message\n");
        }

        /// <summary> A span is accepted inside the type's authored range and refused outside it. </summary>
        private static void CaseSpanRange(StringBuilder builder, List<string> failures)
        {
            BridgeTypeSpec beam = BridgeTypeSpec.Beam(); // 10-300
            Expect(BridgeTypes.SpanWithinType(100.0, beam), "span: 100 m was rejected for a 10-300 m beam", failures);
            Expect(!BridgeTypes.SpanWithinType(5.0, beam), "span: 5 m was accepted below the beam minimum", failures);
            Expect(!BridgeTypes.SpanWithinType(500.0, beam),
                "span: 500 m was accepted above the beam maximum", failures);
            builder.Append("RCMBRIDGE ").Append("span-range".PadRight(20))
                   .Append("span checked against the type's authored range\n");
        }

        /// <summary> An arch lands only on its abutments: two supports, zero intermediate piers. </summary>
        private static void CaseArchNoIntermediatePiers(StringBuilder builder, List<string> failures)
        {
            BridgeTypeSpec arch = BridgeTypeSpec.Arch();
            List<double> stations = BridgeTypes.SupportStationsMetres(120.0, arch);
            Expect(stations.Count == 2 && Math.Abs(stations[0]) < 1e-9 && Math.Abs(stations[1] - 120.0) < 1e-9,
                "arch: an arch did not land only on its two abutments", failures);
            Expect(BridgeTypes.IntermediatePierCount(120.0, arch) == 0,
                "arch: an arch reported intermediate piers", failures);
            builder.Append("RCMBRIDGE ").Append("arch-abutments".PadRight(20))
                   .Append("arch: supports only at the abutments\n");
        }

        /// <summary> A beam fills intermediate piers evenly, none farther apart than the max pier spacing. </summary>
        private static void CaseBeamEvenlySpaced(StringBuilder builder, List<string> failures)
        {
            BridgeTypeSpec beam = BridgeTypeSpec.Beam(); // max spacing 30
            List<double> stations = BridgeTypes.SupportStationsMetres(100.0, beam);
            // ceil(100/30) = 4 bays -> 3 intermediate piers, 5 supports, step 25.
            Expect(stations.Count == 5, "beam: 100 m at 30 m spacing did not produce 5 supports (4 even bays)",
                failures);
            bool evenStep = true;
            for (int i = 1; i < stations.Count; i++)
            {
                double step = stations[i] - stations[i - 1];
                if (Math.Abs(step - 25.0) > 1e-9) { evenStep = false; }
                if (step > beam.maxPierSpacingMetres + 1e-9) { evenStep = false; }
            }
            Expect(evenStep, "beam: the piers were not evenly spaced within the max spacing", failures);
            builder.Append("RCMBRIDGE ").Append("beam-even".PadRight(20))
                   .Append("beam: even bays no wider than the max pier spacing\n");
        }

        /// <summary> A suspension bridge's supports follow its authored tower fractions, not a bay count: a longer
        /// span keeps two towers, placed at the same fractions. </summary>
        private static void CaseSuspensionFollowsTowers(StringBuilder builder, List<string> failures)
        {
            BridgeTypeSpec susp = BridgeTypeSpec.Suspension(); // towers at 0.25, 0.75
            List<double> s1000 = BridgeTypes.SupportStationsMetres(1000.0, susp);
            Expect(s1000.Count == 4 && Math.Abs(s1000[1] - 250.0) < 1e-9 && Math.Abs(s1000[2] - 750.0) < 1e-9,
                "suspension: towers were not placed at the authored quarter points", failures);
            Expect(BridgeTypes.IntermediatePierCount(1000.0, susp) == 2
                   && BridgeTypes.IntermediatePierCount(1600.0, susp) == 2,
                "suspension: support count changed with span instead of following the towers", failures);
            builder.Append("RCMBRIDGE ").Append("suspension-towers".PadRight(20))
                   .Append("suspension: supports follow tower fractions, not span\n");
        }

        /// <summary> A support in water is allowed only when the type permits it. </summary>
        private static void CaseWaterRule(StringBuilder builder, List<string> failures)
        {
            Expect(BridgeTypes.SupportAllowedAt(false, BridgeTypeSpec.Arch()),
                "water: a support on land was refused", failures);
            Expect(!BridgeTypes.SupportAllowedAt(true, BridgeTypeSpec.Arch()),
                "water: an arch support in water was allowed though the type forbids it", failures);
            Expect(BridgeTypes.SupportAllowedAt(true, BridgeTypeSpec.Beam()),
                "water: a beam support in water was refused though the type permits it", failures);
            builder.Append("RCMBRIDGE ").Append("water-rule".PadRight(20))
                   .Append("support in water only when the type allows it\n");
        }

        /// <summary> A pier over the network below is shifted within its allowed range, or flagged if it cannot
        /// clear. </summary>
        private static void CaseConflictShiftThenFlag(StringBuilder builder, List<string> failures)
        {
            // Desired pier at 50 m, road below centred at 50 m with a 6 m half-width. Allowed shift 10 m clears it.
            BridgeTypes.SupportConflictResolve shifted =
                BridgeTypes.ResolveSupportConflict(50.0, 50.0, 6.0, 10.0);
            Expect(shifted.resolved && !shifted.flagged && Math.Abs(Math.Abs(shifted.stationMetres - 50.0) - 6.0) < 1e-9,
                "conflict: a pier over the road was not shifted clear within the allowed range", failures);

            // Same conflict but only 2 m of shift allowed -> cannot clear the 6 m half-width -> flagged.
            BridgeTypes.SupportConflictResolve flagged =
                BridgeTypes.ResolveSupportConflict(50.0, 50.0, 6.0, 2.0);
            Expect(!flagged.resolved && flagged.flagged,
                "conflict: a pier that could not clear the road was not flagged", failures);

            // A pier already clear of the conflict is kept unchanged.
            BridgeTypes.SupportConflictResolve clear =
                BridgeTypes.ResolveSupportConflict(80.0, 50.0, 6.0, 10.0);
            Expect(clear.resolved && !clear.flagged && Math.Abs(clear.stationMetres - 80.0) < 1e-9,
                "conflict: a pier already clear of the road was moved", failures);
            builder.Append("RCMBRIDGE ").Append("conflict-shift".PadRight(20))
                   .Append("pier over network shifted in range, else flagged\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            BridgeTypeSpec beam = BridgeTypeSpec.Beam();
            List<double> a = BridgeTypes.SupportStationsMetres(137.0, beam);
            List<double> b = BridgeTypes.SupportStationsMetres(137.0, beam);
            bool same = a.Count == b.Count;
            for (int i = 0; same && i < a.Count; i++) { if (a[i] != b[i]) { same = false; } }
            Expect(same, "determinism: two identical support layouts differed", failures);
            builder.Append("RCMBRIDGE ").Append("determinism".PadRight(20))
                   .Append("identical inputs -> bit-identical support stations\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
