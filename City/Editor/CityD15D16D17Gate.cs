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
    /// <summary> D-11 through D-17's aggregate gate: the single headless certificate that the whole content register's
    /// cross-cutting promises hold. It COMPOSES the focused gates that each own one clause with its own verified API --
    /// the distribution-pole gate (NESC clearance refused in centimetres), the rail-track gate (a radius under the class
    /// minimum refused), the transport-hub gate (a port refused the water depth it lacks), plus the transmission,
    /// streaming and manifest gates -- and asserts DIRECTLY the invariants that no single focused gate owns:
    ///
    ///   * one shared catenary surface and no straight-cable solver beside it (D-1 / D-14);
    ///   * equal parameters return one cached mesh, for BOTH instanced families the tool builds itself -- the
    ///     transmission tower and the distribution pole (D-15's cost model);
    ///   * rail and metro PRESENCE is read from a fitted row, never a hard-coded literal (D-9);
    ///   * nothing is placed where the water surface rejects the ground (D-12 / B-15);
    ///   * a transmission leg crossing a settlement is reported before build (D-14);
    ///   * a dam computes a linker request and STATES it, deferring the terrain write to Universe Creator, without
    ///     touching UC on this side of the linker (D-13);
    ///   * every generated instance family declares a positive, fail-closed per-chunk ceiling (D-15);
    ///   * every manifest row names the placement rule that consumes it, and is REACHABLE as both a graph node
    ///     and an inspector entry -- the proof that no catalogued content is stub core (D-17).
    ///
    /// The gate is deliberately headless: frame time remains a device measurement, while the deterministic contracts
    /// beneath it are asserted here. Discovered by the suite through its public <see cref="Run"/>, so it needs no
    /// registration. </summary>
    public static class CityD15D16D17Gate
    {
        private const string Prefix = "RCMD15D17";

        [MenuItem("Window/Road City Master/Testing/D-11 to D-17")]
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
            CaseCachedPoleMesh(failures, output);
            CaseOverheadMesh(failures, output);
            CaseFittedTransitRow(failures, output);
            CaseWaterRejection(failures, output);
            CaseSettlementRefusal(failures, output);
            CaseDamLinker(failures, output);
            CaseInstanceBudgets(failures, output);
            CaseManifestReconciliation(failures, output);
            CaseManifestReachability(failures, output);

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

        /// <summary> Compose the focused gates that each own one D-clause with its own verified assertions, rather than
        /// duplicate their API here where a copy could drift. Each of these has been read to confirm it asserts the
        /// clause named beside it: the pole gate the NESC clearance, the rail gate the class-minimum radius, the hub
        /// gate the port water depth. This aggregate fails if any of them does, which is what an aggregate is for. </summary>
        private static void CaseExistingGates(List<string> failures, StringBuilder output)
        {
            string ignored;
            Expect(CityTransmissionLineGate.Run(out ignored), "D-14: transmission gate failed", failures);
            Expect(CityStreamingGate.Run(out ignored), "D-15: streaming gate failed", failures);
            Expect(CityPrefabManifestGate.Run(out ignored), "D-17: manifest gate failed", failures);
            Expect(CityDistributionPoleGate.Run(out ignored),
                "D-16: distribution-pole gate (NESC road clearance refused in centimetres) failed", failures);
            Expect(CityRailTrackGate.Run(out ignored),
                "D-16: rail-track gate (a radius under the class minimum refused) failed", failures);
            Expect(CityTransportHubGate.Run(out ignored),
                "D-16: transport-hub gate (a port refused the water depth it lacks) failed", failures);
            output.Append(Prefix)
                .AppendLine(" focused transmission, streaming, manifest, pole, rail and hub gates passed");
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

        /// <summary> D-15's cost model for the OTHER instanced family the tool builds itself: a distribution pole. The
        /// tower case above proves the transmission family; this proves the pole family, so both of the two mesh
        /// builders the house rules permit are shown to answer one cached mesh per parameter set. </summary>
        private static void CaseCachedPoleMesh(List<string> failures, StringBuilder output)
        {
            PoleGeometryClass shape = PoleGeometryClass.Default();
            PoleMeshKey key = PoleMeshKey.For(shape, null, PoleFittings.Line, 3, 0);
            Mesh first = PoleMeshFactory.Get(key, shape);
            Mesh second = PoleMeshFactory.Get(key, shape);
            Expect(first != null && ReferenceEquals(first, second),
                "D-15/D-2: equal pole parameters did not reuse one cached mesh", failures);
            output.Append(Prefix).AppendLine(" equal pole parameters reuse one cached mesh");
        }

        /// <summary> D-7's scene half: the overhead line builds a mast and its wires as real geometry, the mast is
        /// SEATED (its foundation runs below the ground it stands on and its head rises above the rail head), and both
        /// pieces are recognised as the builder's own so a rebuild clears them rather than leaking a kilometre of tube.
        ///
        /// The seating is checked in the mesh's own local space, which is exactly where a floating or buried mast would
        /// show: the piece origin is the mast's ground point, so a buried foundation is a vertex below zero and the mast
        /// head is a vertex well above it. A mesh whose lowest vertex were at or above zero would be a mast resting on
        /// the surface -- the defect B-13 forbids for a pole, asserted here for a mast. </summary>
        private static void CaseOverheadMesh(List<string> failures, StringBuilder output)
        {
            List<RailAlignmentSample> samples = new List<RailAlignmentSample>();
            RailOverheadPlan plan = new RailOverheadPlan();
            RailOverheadClass ohle = RailOverheadClass.Default();
            const float railHeadY = 100f;
            const float groundY = 99f;
            for (int i = 0; i < 3; i++)
            {
                float station = i * 30f;
                RailAlignmentSample sample = new RailAlignmentSample();
                sample.stationMetres = station;
                sample.position = new Vector3(0f, railHeadY, station);
                sample.tangent = new Vector3(0f, 0f, 1f);
                sample.radiusMetres = double.PositiveInfinity;
                sample.groundHeightMetres = groundY;
                samples.Add(sample);

                RailOverheadMast mast = new RailOverheadMast();
                mast.stationMetres = station;
                mast.railHeadWorldY = railHeadY;
                mast.railHeadAboveGroundMetres = railHeadY - groundY;
                mast.staggerSign = RailOverheadClass.StaggerSignAt(i);
                mast.staggerMetres = ohle.staggerMetres;
                mast.radiusMetres = double.PositiveInfinity;
                mast.basePoint = new Vector3(-2f * mast.staggerSign, groundY, station);
                mast.contactWireHeightMetres = 5.3f;
                mast.messengerHeightMetres = 5.3f + ohle.systemHeightMetres;
                plan.masts.Add(mast);
            }
            for (int i = 0; i + 1 < plan.masts.Count; i++)
            {
                RailOverheadSpan span = new RailOverheadSpan();
                span.fromMastIndex = i;
                span.toMastIndex = i + 1;
                span.spanMetres = 30f;
                span.messenger = TransmissionLine.Solve(30f, plan.masts[i].messengerHeightMetres,
                    plan.masts[i + 1].messengerHeightMetres, ohle.messengerSagRatio);
                plan.spans.Add(span);
            }

            Vector3 mastOrigin;
            Mesh mastMesh = RailOverheadMeshes.BuildMastPiece(plan, samples, ohle, 0,
                RailOverheadMeshes.MastsPerPiece, out mastOrigin);
            Vector3 wireOrigin;
            Mesh wireMesh = RailOverheadMeshes.BuildWirePiece(plan, samples, ohle, 0,
                RailOverheadMeshes.SpansPerPiece, out wireOrigin);

            Expect(mastMesh != null && mastMesh.vertexCount > 0, "D-7: no mast geometry was built", failures);
            Expect(wireMesh != null && wireMesh.vertexCount > 0, "D-7: no wire geometry was built", failures);
            if (mastMesh != null && mastMesh.vertexCount > 0)
            {
                float lowest = float.PositiveInfinity;
                float highest = float.NegativeInfinity;
                Vector3[] v = mastMesh.vertices;
                for (int i = 0; i < v.Length; i++)
                {
                    if (v[i].y < lowest) { lowest = v[i].y; }
                    if (v[i].y > highest) { highest = v[i].y; }
                }
                Expect(lowest < 0f, "D-7: the mast foundation does not run below the ground it stands on -- it floats",
                    failures);
                Expect(highest > (railHeadY - groundY),
                    "D-7: the mast does not rise above the rail head, so it holds no wire", failures);
            }

            string mastName = CityHierarchy.NameFor(GeneratedKind.Rail, "mast.rail.probe", 0);
            string wireName = CityHierarchy.NameFor(GeneratedKind.Rail, "wire.rail.probe", 0);
            string runName = CityHierarchy.NameFor(GeneratedKind.Rail, "run.rail.probe", 0);
            Expect(RailAlignmentBuilder.OwnsItsMesh(mastName) && RailAlignmentBuilder.OwnsItsMesh(wireName),
                "D-7: a mast or wire piece is not recognised as the builder's own, so a rebuild would leak its mesh",
                failures);
            Expect(!RailAlignmentBuilder.OwnsItsMesh(runName),
                "D-7: the run holder was wrongly claimed to own a mesh", failures);
            Expect(RailAlignmentBuilder.OverheadBuiltNote(plan, 1, 1).Contains("mast")
                   && RailAlignmentBuilder.OverheadBuiltNote(null, 0, 0).Length == 0,
                "D-7: the overhead build note does not describe a wired line, or speaks for an unwired one", failures);

            if (mastMesh != null) { UnityEngine.Object.DestroyImmediate(mastMesh); }
            if (wireMesh != null) { UnityEngine.Object.DestroyImmediate(wireMesh); }
            output.Append(Prefix).AppendLine(" the overhead line builds a seated mast and its wires, owned for cleanup");
        }

        /// <summary> D-9. Rail and metro PRESENCE at a settlement is a fitted-row lookup, not a literal: a table
        /// authored from a null catalogue carries no rail or metro rows, so the presence question answers "no" because
        /// the ROW is absent -- proving the answer follows the data and is never hard-coded to yes. </summary>
        private static void CaseFittedTransitRow(List<string> failures, StringBuilder output)
        {
            SettlementFitTable table = SettlementFitTable.CreateAuthored(null);
            SettlementRailFitRow railRow;
            SettlementRailFitRow metroRow;
            bool fallback;
            bool hasRail = table.TryGetRail(1, SettlementFitTable.WorldKey, out railRow, out fallback);
            bool hasMetro = table.TryGetUnderground(1, SettlementFitTable.WorldKey, out metroRow, out fallback);
            Expect(!hasRail && railRow == null,
                "D-16: rail presence did not come from a fitted row (a null-catalogue table reported rail)", failures);
            Expect(!hasMetro && metroRow == null,
                "D-16: metro presence did not come from a fitted row (a null-catalogue table reported metro)",
                failures);
            output.Append(Prefix).AppendLine(" rail and metro presence are read from a fitted row, never a literal");
        }

        /// <summary> D-12 / B-15. Nothing is placed where the water surface rejects the ground: the one shared
        /// <see cref="BuildableLandTest.WaterRejects"/> predicate refuses a lowest ground within the freeboard of the
        /// water and accepts one that stands well clear, so the city layer and the site test judge water in one voice. </summary>
        private static void CaseWaterRejection(List<string> failures, StringBuilder output)
        {
            BuildabilityCriteria criteria = BuildabilityCriteria.Default();
            string reason;
            bool inWater = BuildableLandTest.WaterRejects(2f, 5f, criteria, out reason);
            Expect(inWater && reason.Length > 0,
                "D-16: ground within the water freeboard was not refused", failures);
            bool dry = BuildableLandTest.WaterRejects(50f, 5f, criteria, out reason);
            Expect(!dry, "D-16: ground standing well clear of the water surface was wrongly refused", failures);
            output.Append(Prefix).AppendLine(" no structure is placed where the water surface rejects the ground");
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

        /// <summary> D-13. The dam computes a linker request and STATES it, without touching Universe Creator on this
        /// side. Two ends prove it: with no terrain reader the request is REFUSED with a stated reason rather than
        /// thrown (fail-closed, nothing touched); on readable ground it RESOLVES and its own description names the
        /// Universe Creator hand-off, which is the request saying the terrain write happens over there, not here. </summary>
        private static void CaseDamLinker(List<string> failures, StringBuilder output)
        {
            DamRequest none = DamWorks.Resolve(null, 0.0, 0.0, 0.0, 0.0, 8.0, 1.5, 120.0, 6.0, 2.0, 2.0,
                default(SpillwayType), 7.5, 10.0, 1.0, 1.5, 1.0, 5.0, 2000, 24);
            Expect(!none.resolved && none.refusal.Length > 0,
                "D-16: a dam with no terrain reader was not refused with a stated reason", failures);

            DamRequest onFlat = DamWorks.Resolve(new FlatGround(), 0.0, 0.0, 0.0, 0.0, 8.0, 1.5, 120.0, 6.0,
                2.0, 2.0, default(SpillwayType), 7.5, 10.0, 1.0, 1.5, 1.0, 5.0, 2000, 24);
            Expect(onFlat.resolved, "D-16: a dam on readable ground did not resolve into a linker request", failures);
            Expect(onFlat.Describe().Contains("Universe Creator"),
                "D-16: a resolved dam request does not state the Universe Creator hand-off it defers the terrain "
                + "write to", failures);
            output.Append(Prefix).AppendLine(" a dam states a linker request and defers the terrain write to UC");
        }

        /// <summary> D-15. Every GENERATED instance family declares a positive per-chunk ceiling, and the ceiling is
        /// fail-closed: a chunk exactly at the ceiling is allowed, one instance past it is refused with a message that
        /// names the ceiling. This asserts the WIRED budget the planners and the streaming node consult
        /// (<see cref="CityInstanceBudget"/>), which is the one the tool actually enforces. </summary>
        private static void CaseInstanceBudgets(List<string> failures, StringBuilder output)
        {
            List<CityInstanceCeilingVerdict> ceilings = CityInstanceBudget.AllCeilings();
            Expect(ceilings.Count == 8,
                "D-15: expected one per-chunk ceiling for each of the 8 generated families; got " + ceilings.Count,
                failures);
            for (int i = 0; i < ceilings.Count; i++)
            {
                CityInstanceFamily family = ceilings[i].family;
                int ceiling = CityInstanceBudget.CeilingFor(family);
                Expect(ceiling > 0,
                    "D-15: " + CityInstanceBudget.Name(family) + " declares no positive per-chunk ceiling", failures);
                CityInstanceCeilingVerdict atCeiling = CityInstanceBudget.Check(family, ceiling);
                Expect(atCeiling.withinCeiling,
                    "D-15: a chunk exactly at the ceiling for " + CityInstanceBudget.Name(family) + " was refused",
                    failures);
                CityInstanceCeilingVerdict over = CityInstanceBudget.Check(family, ceiling + 1);
                Expect(!over.withinCeiling && over.message.Contains("ceiling"),
                    "D-15: an overfull chunk for " + CityInstanceBudget.Name(family) + " was not refused with the "
                    + "ceiling named", failures);
            }
            output.Append(Prefix).AppendLine(" every generated instance family has a fail-closed per-chunk ceiling");
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

        /// <summary> D-17. The reconciliation's proof that no catalogued content is stub core: every manifest
        /// category is REACHABLE on both author surfaces. In the node graph, <see cref="CityNodeCatalog"/> makes
        /// exactly one element node per category, so the element ids equal the manifest ids -- none missing, none
        /// invented; in the inspector, <see cref="ManifestInstaller.EnsureAllCategories"/> seeds one content entry
        /// per row into a fresh set. Content offered on neither surface would show here as a count that no longer
        /// matches. </summary>
        private static void CaseManifestReachability(List<string> failures, StringBuilder output)
        {
            IReadOnlyList<ManifestCategory> categories = PrefabManifest.Categories;
            HashSet<string> manifestIds = new HashSet<string>();
            for (int i = 0; i < categories.Count; i++) { manifestIds.Add(categories[i].categoryId); }

            HashSet<string> elementIds = new HashSet<string>();
            IReadOnlyList<CityNodeDescriptor> entries = CityNodeCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                string id = entries[i].elementCategoryId;
                if (!string.IsNullOrEmpty(id)) { elementIds.Add(id); }
            }
            Expect(elementIds.Count == manifestIds.Count && elementIds.SetEquals(manifestIds),
                "D-17: the node graph does not offer exactly one element node per manifest category (graph "
                + elementIds.Count + ", manifest " + manifestIds.Count + ")", failures);

            CategoryContentSet set = ScriptableObject.CreateInstance<CategoryContentSet>();
            int seeded = ManifestInstaller.EnsureAllCategories(set);
            Expect(seeded == categories.Count,
                "D-17: a fresh inspector content set was not seeded one entry per manifest row (seeded "
                + seeded + ", rows " + categories.Count + ")", failures);
            UnityEngine.Object.DestroyImmediate(set);
            output.Append(Prefix)
                .AppendLine(" every manifest category is reachable as a node and an inspector entry");
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
