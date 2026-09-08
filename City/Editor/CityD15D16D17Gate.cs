// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> D-15 through D-17's aggregate gate. It composes the existing focused gates and checks the three
    /// remaining cross-cutting promises: every content family has one mobile instance ceiling, a ceiling refuses an
    /// overfull chunk, and every manifest row names the placement rule that consumes it. The gate is deliberately
    /// headless; frame time remains a device measurement, while the deterministic contracts beneath it are asserted. </summary>
    public static class CityD15D16D17Gate
    {
        private const string Prefix = "RCMD15D17";

        [MenuItem("Window/Road City Master/Testing/D-15 to D-17")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
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
            List<string> failures = new List<string>();
            StringBuilder output = new StringBuilder();
            output.AppendLine(Prefix + " BEGIN");

            CaseExistingGates(failures, output);
            CaseOneCatenarySurface(failures, output);
            CaseCachedTowerMesh(failures, output);
            CaseSettlementRefusal(failures, output);
            CaseInstanceBudgets(failures, output);
            CaseManifestReconciliation(failures, output);

            for (int i = 0; i < failures.Count; i++)
            {
                output.Append(Prefix).Append(" FAIL   ").AppendLine(failures[i]);
            }
            output.Append(Prefix).Append(" result ")
                .Append(failures.Count == 0 ? "PASS" : "FAIL")
                .Append(" failures ").AppendLine(failures.Count.ToString());
            output.Append(Prefix).Append(" END");
            report = output.ToString();
            return failures.Count == 0;
        }

        private static void CaseExistingGates(List<string> failures, StringBuilder output)
        {
            string ignored;
            Expect(CityTransmissionLineGate.Run(out ignored), "D-16: transmission gate failed", failures);
            Expect(CityStreamingGate.Run(out ignored), "D-15: streaming gate failed", failures);
            Expect(CityPrefabManifestGate.Run(out ignored), "D-17: manifest gate failed", failures);
            output.Append(Prefix).AppendLine(" focused transmission, streaming and manifest gates passed");
        }

        private static void CaseOneCatenarySurface(List<string> failures, StringBuilder output)
        {
            MethodInfo solve = typeof(TransmissionLine).GetMethod("Solve", BindingFlags.Public | BindingFlags.Static);
            MethodInfo straight = typeof(DistributionPoleRun).GetMethod("StraightCableHeightAt",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            Expect(solve != null, "D-16: the shared TransmissionLine.Solve method is missing", failures);
            Expect(straight == null, "D-16: a straight cable solver still exists beside the catenary", failures);
            output.Append(Prefix).AppendLine(" one shared catenary surface, no straight cable solver");
        }

        private static void CaseCachedTowerMesh(List<string> failures, StringBuilder output)
        {
            TransmissionTowerClass shape = TransmissionTowerClass.Default();
            TowerMeshKey key = TowerMeshKey.For(shape, 3, TowerType.Suspension, false);
            Mesh first = TowerMeshFactory.Get(key, shape);
            Mesh second = TowerMeshFactory.Get(key, shape);
            Expect(first != null && ReferenceEquals(first, second),
                "D-15: equal tower parameters did not reuse one cached mesh", failures);
            output.Append(Prefix).AppendLine(" equal tower parameters reuse one cached mesh");
        }

        private static void CaseSettlementRefusal(List<string> failures, StringBuilder output)
        {
            TransmissionLineRunSettings settings = TransmissionLineRunSettings.Default();
            settings.routeCorners.Add(new Vector3(-10f, 0f, 0f));
            settings.routeCorners.Add(new Vector3(10f, 0f, 0f));
            List<Rect> bounds = new List<Rect> { new Rect(-1f, -1f, 2f, 2f) };
            TransmissionLineRunPlan plan = TransmissionLineRunPlanner.Plan(settings, new FlatGround(), bounds);
            Expect(plan.refusals.Count > 0 && plan.refusals[0].Contains("settlement"),
                "D-16: a transmission leg inside a settlement was not refused", failures);
            output.Append(Prefix).AppendLine(" settlement crossing is reported before build");
        }

        private static void CaseInstanceBudgets(List<string> failures, StringBuilder output)
        {
            IReadOnlyList<CityInstanceBudgetRule> rules = CityInstanceBudgets.All;
            Expect(rules.Count == 11, "D-15: expected one ceiling for each of the 11 content families", failures);
            for (int i = 0; i < rules.Count; i++)
            {
                string refusal;
                Expect(rules[i].maximumInstancesPerChunk > 0,
                    "D-15: " + rules[i].family + " has no positive ceiling", failures);
                Expect(CityInstanceBudgets.TryValidate(rules[i].family, rules[i].maximumInstancesPerChunk,
                    out refusal), "D-15: legal count failed for " + rules[i].family, failures);
                Expect(!CityInstanceBudgets.TryValidate(rules[i].family,
                    rules[i].maximumInstancesPerChunk + 1, out refusal) && refusal.Contains("ceiling"),
                    "D-15: overfull " + rules[i].family + " chunk was not refused", failures);
            }
            output.Append(Prefix).AppendLine(" every content family has a fail-closed per-chunk ceiling");
        }

        private static void CaseManifestReconciliation(List<string> failures, StringBuilder output)
        {
            IReadOnlyList<ManifestCategory> categories = PrefabManifest.Categories;
            HashSet<string> sections = new HashSet<string>();
            int missingConsumers = 0;
            for (int i = 0; i < categories.Count; i++)
            {
                sections.Add(categories[i].section);
                if (string.IsNullOrEmpty(categories[i].consumedBy)) { missingConsumers++; }
            }
            Expect(categories.Count == 140, "D-17: manifest reconciliation did not see all 140 rows", failures);
            Expect(sections.Count == 22, "D-17: manifest reconciliation did not see all 22 sections", failures);
            Expect(missingConsumers == 0,
                "D-17: " + missingConsumers + " manifest row(s) have no placement consumer", failures);
            output.Append(Prefix).Append(" manifest rows ").Append(categories.Count)
                .Append(", sections ").Append(sections.Count).AppendLine(", placement consumers complete");
        }

        private sealed class FlatGround : ITerrainSurfaceReader
        {
            public float SampleHeight(float worldX, float worldZ) { return 0f; }
            public Vector3 SampleNormal(float worldX, float worldZ) { return Vector3.up; }
            public List<Bounds> GetActiveChunkBounds() { return new List<Bounds>(); }
        }

        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
