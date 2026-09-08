// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-09's gate: proves the street-network operator is a deterministic pure function of
    /// (input, params, seed) and that its output realises into real road-tool <see cref="Road"/>
    /// objects. It runs the operator TWICE on the same seed and input and asserts the two networks are
    /// field-for-field identical (C-06 determinism); asserts every edge references valid, distinct
    /// nodes with no duplicate pair; asserts a DIFFERENT seed produces a different network (the seeded
    /// organic bend is doing its job, not producing one fixed layout); and realises the network,
    /// asserting a <see cref="RoadSystem"/> with the expected roads is built.
    ///
    /// It does NOT judge that the roads look right in the Scene view or mesh correctly -- that is the
    /// user's visual check. What it locks down is that generation is reproducible and that the realizer
    /// wires the network into the road tool at all. </summary>
    public static class StreetNetworkGenerationTest
    {
        [MenuItem("Window/Road City Master/Testing/Street network generation")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report))
            {
                Debug.Log(report);
            }
            else
            {
                Debug.LogError(report);
            }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.StreetNetworkGenerationTest.RunBatch. Exits 1 on failure so a
        /// build script can gate on it. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Generates, checks, and realises. Report lines are prefixed RCMNETGEN so a log grep
        /// can read the verdict without wading through Unity's surrounding output. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMNETGEN BEGIN\n");
            List<string> failures = new List<string>();
            RoadSystem system = null;

            try
            {
                RoadClassCatalog catalog = ScriptableObject.CreateInstance<RoadClassCatalog>();
                catalog.ResetToDefaults();

                StreetNetworkOperator op = ScriptableObject.CreateInstance<StreetNetworkOperator>();
                op.catalog = catalog;
                op.maxSegments = 400;

                IPCGDataProvider terrain = new FakePcgTerrain();
                Rect region = new Rect(-800f, -800f, 1600f, 1600f);
                StreetNetworkInput input = new StreetNetworkInput(terrain, region);

                StreetNetwork first = op.Execute(input, 12345);
                StreetNetwork second = op.Execute(input, 12345);
                StreetNetwork other = op.Execute(input, 67890);

                builder.Append("RCMNETGEN network nodes ").Append(first.nodes.Count)
                       .Append(" edges ").Append(first.edges.Count)
                       .Append(" (").Append(first.report.ToString()).Append(")\n");

                CheckNonEmpty(first, failures);
                CheckDeterministic(first, second, failures);
                CheckStructure(first, failures);
                CheckSeedsDiffer(first, other, failures);

                system = NetworkRoadBuilder.Build(first, catalog);
                CheckRealization(system, first, failures);

                ScriptableObject.DestroyImmediate(op);
                ScriptableObject.DestroyImmediate(catalog);
            }
            finally
            {
                if (system != null)
                {
                    Object.DestroyImmediate(system.gameObject);
                }
            }

            foreach (string failure in failures)
            {
                builder.Append("RCMNETGEN FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMNETGEN result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMNETGEN END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> The generator actually grew something: nodes, edges, and at least one accepted
        /// proposal recorded in the report. </summary>
        private static void CheckNonEmpty(StreetNetwork net, List<string> failures)
        {
            Expect(net.nodes.Count > 0, "network has no nodes", failures);
            Expect(net.edges.Count > 0, "network has no edges", failures);
            Expect(net.report.accepted > 0, "report recorded no accepted segments", failures);
            Expect(net.report.accepted == net.edges.Count,
                   "accepted count " + net.report.accepted + " != edge count " + net.edges.Count, failures);
        }

        /// <summary> C-06: two runs on the same seed and input are field-for-field identical -- node
        /// positions, every edge field, and every report tally. Any nondeterminism (an unseeded Random,
        /// an order-dependent snap) surfaces here as a mismatch. </summary>
        private static void CheckDeterministic(StreetNetwork a, StreetNetwork b, List<string> failures)
        {
            if (a.nodes.Count != b.nodes.Count || a.edges.Count != b.edges.Count)
            {
                failures.Add("re-run differs in size: nodes " + a.nodes.Count + "/" + b.nodes.Count +
                             " edges " + a.edges.Count + "/" + b.edges.Count);
                return;
            }

            for (int i = 0; i < a.nodes.Count; i++)
            {
                if (a.nodes[i].position != b.nodes[i].position)
                {
                    failures.Add("re-run node " + i + " position differs");
                    return;
                }
            }
            for (int i = 0; i < a.edges.Count; i++)
            {
                NetworkEdge e1 = a.edges[i];
                NetworkEdge e2 = b.edges[i];
                if (e1.a != e2.a || e1.b != e2.b || e1.roadClassId != e2.roadClassId ||
                    e1.isDivided != e2.isDivided || e1.medianWidth != e2.medianWidth)
                {
                    failures.Add("re-run edge " + i + " differs");
                    return;
                }
            }

            Expect(a.report.proposed == b.report.proposed && a.report.accepted == b.report.accepted,
                   "re-run report tallies differ", failures);
        }

        /// <summary> Every edge joins two distinct, in-range nodes, and no unordered node pair carries
        /// more than one edge. A generator that emitted a self-loop, an out-of-range index, or a
        /// duplicate would produce a network the downstream stages cannot trust. </summary>
        private static void CheckStructure(StreetNetwork net, List<string> failures)
        {
            int n = net.nodes.Count;
            HashSet<long> seen = new HashSet<long>();
            for (int i = 0; i < net.edges.Count; i++)
            {
                NetworkEdge e = net.edges[i];
                if (e.a == e.b)
                {
                    failures.Add("edge " + i + " is a self-loop");
                    continue;
                }
                if (e.a < 0 || e.a >= n || e.b < 0 || e.b >= n)
                {
                    failures.Add("edge " + i + " references out-of-range node");
                    continue;
                }
                int lo = Mathf.Min(e.a, e.b);
                int hi = Mathf.Max(e.a, e.b);
                long key = (long)lo * n + hi;
                if (!seen.Add(key))
                {
                    failures.Add("edge " + i + " duplicates pair (" + lo + "," + hi + ")");
                }
            }
        }

        /// <summary> A different seed produces a genuinely different network, not the same layout: the
        /// seeded organic bend is what separates one city from another. Guarded so it only fires when
        /// both networks actually grew. </summary>
        private static void CheckSeedsDiffer(StreetNetwork a, StreetNetwork b, List<string> failures)
        {
            if (a.nodes.Count == 0 || b.nodes.Count == 0)
            {
                return;
            }
            bool identical = a.nodes.Count == b.nodes.Count && a.edges.Count == b.edges.Count;
            if (identical)
            {
                for (int i = 0; i < a.nodes.Count; i++)
                {
                    if (a.nodes[i].position != b.nodes[i].position)
                    {
                        identical = false;
                        break;
                    }
                }
            }
            Expect(!identical, "two different seeds produced an identical network", failures);
        }

        /// <summary> The realizer built a <see cref="RoadSystem"/> carrying real roads: at least one,
        /// never more than the edge count (strokes only ever merge edges, never split them), and every
        /// road has at least two spline nodes. This is the reuse rule verified -- the network became
        /// road-tool roads, not a parallel mesh. </summary>
        private static void CheckRealization(RoadSystem system, StreetNetwork net, List<string> failures)
        {
            if (system == null)
            {
                failures.Add("realizer returned no RoadSystem");
                return;
            }

            Road[] roads = system.GetComponentsInChildren<Road>();
            Expect(roads.Length > 0, "RoadSystem has no roads", failures);
            Expect(roads.Length <= net.edges.Count,
                   "road count " + roads.Length + " exceeds edge count " + net.edges.Count, failures);

            for (int i = 0; i < roads.Length; i++)
            {
                int nodeCount = roads[i].splineObject != null ? roads[i].splineObject.transform.childCount : 0;
                if (nodeCount < 2)
                {
                    failures.Add("road " + i + " has fewer than two spline nodes (" + nodeCount + ")");
                }
            }
        }

        /// <summary> Records a failure message when <paramref name="condition"/> is false. </summary>
        private static void Expect(bool condition, string failureMessage, List<string> failures)
        {
            if (!condition)
            {
                failures.Add(failureMessage);
            }
        }
    }
}
#endif
