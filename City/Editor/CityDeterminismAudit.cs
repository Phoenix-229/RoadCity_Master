#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-18's gate: the CITY-level determinism audit, the same audit § 7.39 applies to
    /// terrain. C-06 is a constraint, not an aspiration, and a graph-based generator has more ways to
    /// break it than a grid-based one. This does NOT re-test each operator -- RCMNETGEN, RCMBLOCKS and
    /// RCMPAD already gate the network, block and pad stages one at a time. It tests the assembled
    /// DATA: it builds one coherent city (a grid network -> blocks -> lots -> pads) end to end and
    /// asserts the saved shape is byte-identical (a) across two runs in one session and (b) across two
    /// different load orders, reporting WHERE the first difference is rather than merely that one
    /// exists -- because a diff of two binary assets is unreadable. It prints a canonical FNV hash so
    /// two FRESH Editor sessions can be compared with one glance at the log (the one determinism break
    /// that cannot be caught inside a single process). And it runs the static smell-test the spec asks
    /// for: a scan of every City/ script for the constructs that make determinism impossible.
    ///
    /// The report is prefixed RCMDET. Buildings (RM-12) are not built yet, so the city assembled here
    /// carries no <see cref="BuildingInstance"/> list -- that coverage is explicitly deferred. </summary>
    public static class CityDeterminismAudit
    {
        [MenuItem("Window/Road City Master/Testing/City determinism audit")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityDeterminismAudit.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }
        /// <summary> Assembles the city twice on one seed, once more under a reversed load order, diffs
        /// the canonical dumps, scans the source, and prints the canonical hash. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMDET BEGIN\n");
            List<string> failures = new List<string>();

            const int Seed = 12345;
            List<string> a = AssembleCity(Seed, false);
            List<string> b = AssembleCity(Seed, false);
            List<string> c = AssembleCity(Seed, true);

            builder.Append("RCMDET city lines ").Append(a.Count).Append("\n");

            // (a) Two runs in one session must be byte-identical, field for field.
            CheckIdentical("session-repeat", a, b, builder, failures);
            // (b) A different load order (lots and pad requests fed reversed) must not change the DATA:
            // the block ids come from a stable face key and the pad grouping is union-find, both
            // order-independent, so the id-sorted canonical form is invariant.
            CheckIdentical("load-order", a, c, builder, failures);

            // (fresh sessions) cannot be proven inside one process; the canonical hash is printed so two
            // batch logs can be compared directly, and the smell-test below bans the one construct --
            // string GetHashCode -- that reproduces all afternoon and differs tomorrow.
            builder.Append("RCMDET canonical ").Append(Fnv1a64(a)).Append("\n");

            // The static smell test: every hit is a bug or needs a written exemption at the site.
            SmellTest(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMDET FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMDET result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMDET END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Compares two canonical dumps line for line and records the FIRST place they diverge
        /// -- the labelled field and both values -- so a failure names the defect instead of just
        /// asserting inequality. A length mismatch is reported as its own first difference. </summary>
        private static void CheckIdentical(string which, List<string> x, List<string> y,
            StringBuilder builder, List<string> failures)
        {
            int min = x.Count < y.Count ? x.Count : y.Count;
            for (int i = 0; i < min; i++)
            {
                if (x[i] != y[i])
                {
                    failures.Add(which + ": first difference at [" + i + "]  A=\"" + x[i] +
                                 "\"  B=\"" + y[i] + "\"");
                    return;
                }
            }
            if (x.Count != y.Count)
            {
                failures.Add(which + ": dumps differ in length " + x.Count + " vs " + y.Count +
                             " (first extra: \"" + (x.Count > y.Count ? x[min] : y[min]) + "\")");
                return;
            }
            builder.Append("RCMDET ").Append(which.PadRight(16)).Append("identical  lines ")
                   .Append(x.Count).Append("\n");
        }
        private const float Spacing = 200f;   // metres between grid streets
        private const int GridN = 3;           // 3x3 nodes -> four square blocks with real faces

        /// <summary> Builds one coherent city end to end -- grid network, blocks and lots from the
        /// block operator, pads from the pad conform -- and returns its canonical, id-sorted, labelled
        /// serialization. When <paramref name="reverseLoad"/> is set the lots are enumerated and the pad
        /// requests fed in reverse, exercising load-order independence: a correct city serializes the
        /// same regardless. </summary>
        private static List<string> AssembleCity(int seed, bool reverseLoad)
        {
            IPCGDataProvider terrain = new FakePcgTerrain();
            StreetNetwork net = BuildGridNetwork(terrain);

            RoadClassCatalog catalog = ScriptableObject.CreateInstance<RoadClassCatalog>();
            BlockSubdivisionOperator op = ScriptableObject.CreateInstance<BlockSubdivisionOperator>();
            BlockSubdivisionResult blocks;
            try
            {
                catalog.ResetToDefaults();
                op.catalog = catalog;
                op.footwayWidth = 2f;
                op.setback = 3f;
                op.targetLotArea = 800f;
                op.splitJitter = 0.35f;
                blocks = op.Execute(new BlockSubdivisionInput(net), seed);
            }
            finally
            {
                ScriptableObject.DestroyImmediate(op);
                ScriptableObject.DestroyImmediate(catalog);
            }

            // Pads from the lots. Feeding the request list reversed is the load-order perturbation: the
            // pad stage merges by union-find over apron overlap, an order-independent relation.
            List<PadRequest> requests = new List<PadRequest>(blocks.lots.Count);
            for (int i = 0; i < blocks.lots.Count; i++)
            {
                int idx = reverseLoad ? blocks.lots.Count - 1 - i : i;
                Lot lot = blocks.lots[idx];
                requests.Add(new PadRequest(lot.id, lot.polygon,
                    PadElevationRule.CentroidSample, GroundContactMode.OnPad));
            }
            List<PadRecord> pads = BuildingPadConform.ComputePads(requests, terrain, PadConformParams.Default());

            return Serialize(net, blocks, pads);
        }

        /// <summary> A GridN x GridN lattice of local streets whose interior squares are the closed
        /// faces the block operator subdivides -- the same construction RCMBLOCKS drives the block
        /// operator with, built here so the audit exercises the whole pipeline on real faces rather than
        /// the L-system's face-less tree. Node heights come from the shared analytic terrain. </summary>
        private static StreetNetwork BuildGridNetwork(IPCGDataProvider terrain)
        {
            StreetNetwork net = new StreetNetwork();
            int[,] nodeIndex = new int[GridN, GridN];
            for (int j = 0; j < GridN; j++)
            {
                for (int i = 0; i < GridN; i++)
                {
                    float x = i * Spacing;
                    float z = j * Spacing;
                    bool reused;
                    nodeIndex[i, j] = net.AddNodeSnapped(
                        new Vector3(x, terrain.SampleHeight(x, z), z), 0.01f, out reused);
                }
            }
            for (int j = 0; j < GridN; j++)
            {
                for (int i = 0; i < GridN; i++)
                {
                    if (i + 1 < GridN)
                    {
                        net.AddEdge(nodeIndex[i, j], nodeIndex[i + 1, j], RoadClassCatalog.LocalId, false, 0f);
                    }
                    if (j + 1 < GridN)
                    {
                        net.AddEdge(nodeIndex[i, j], nodeIndex[i, j + 1], RoadClassCatalog.LocalId, false, 0f);
                    }
                }
            }
            return net;
        }
        /// <summary> The canonical, deterministic serialization of the assembled city as labelled lines.
        /// Nodes keep index order (their index is their identity, referenced by edges); edges, blocks,
        /// lots and pads are sorted by a stable key so the DUMP does not depend on the order the
        /// operators happened to emit them, while every vertex list is kept in its natural order so a
        /// rotated or reversed loop -- a real determinism break -- still surfaces. Floats are written
        /// round-trip under the invariant culture, so a machine with a comma decimal separator cannot
        /// make two identical cities read differently. </summary>
        private static List<string> Serialize(StreetNetwork net, BlockSubdivisionResult res, List<PadRecord> pads)
        {
            List<string> lines = new List<string>();

            for (int i = 0; i < net.nodes.Count; i++)
            {
                lines.Add("node[" + i + "] " + V(net.nodes[i].position));
            }

            List<NetworkEdge> edges = new List<NetworkEdge>(net.edges);
            edges.Sort((p, q) =>
            {
                int plo = Mathf.Min(p.a, p.b), phi = Mathf.Max(p.a, p.b);
                int qlo = Mathf.Min(q.a, q.b), qhi = Mathf.Max(q.a, q.b);
                if (plo != qlo) { return plo.CompareTo(qlo); }
                if (phi != qhi) { return phi.CompareTo(qhi); }
                return p.roadClassId.CompareTo(q.roadClassId);
            });
            for (int i = 0; i < edges.Count; i++)
            {
                NetworkEdge e = edges[i];
                lines.Add("edge " + Mathf.Min(e.a, e.b) + "-" + Mathf.Max(e.a, e.b) +
                          " cls " + e.roadClassId + " div " + (e.isDivided ? 1 : 0) +
                          " med " + F(e.medianWidth));
            }

            List<Block> blocks = new List<Block>(res.blocks);
            blocks.Sort((p, q) => p.id.CompareTo(q.id));
            for (int i = 0; i < blocks.Count; i++)
            {
                lines.Add("block " + blocks[i].id + " nodes " + Ints(blocks[i].boundaryNodes) +
                          " poly " + Poly(blocks[i].polygon));
            }

            List<Lot> lots = new List<Lot>(res.lots);
            lots.Sort((p, q) => p.id.CompareTo(q.id));
            for (int i = 0; i < lots.Count; i++)
            {
                Lot l = lots[i];
                lines.Add("lot " + l.id + " blk " + l.blockId + " zone " + l.zoneId +
                          " poly " + Poly(l.polygon));
            }

            List<PadRecord> ordered = new List<PadRecord>(pads);
            ordered.Sort((p, q) => p.lotId.CompareTo(q.lotId));
            for (int i = 0; i < ordered.Count; i++)
            {
                PadRecord pad = ordered[i];
                lines.Add("pad " + pad.lotId + " grp " + pad.padGroupId + " elev " + F(pad.padElevation) +
                          " fp " + Poly(pad.footprint) + " ring " + Poly(pad.apronRing));
            }
            return lines;
        }

        private static string F(float f)
        {
            return f.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 v)
        {
            return "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
        }

        private static string Poly(List<Vector3> poly)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append(poly.Count).Append(":");
            for (int i = 0; i < poly.Count; i++) { sb.Append(V(poly[i])); }
            return sb.ToString();
        }

        private static string Ints(List<int> values)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) { sb.Append(","); }
                sb.Append(values[i]);
            }
            return sb.ToString();
        }

        /// <summary> A stable 64-bit FNV-1a hash of the whole canonical dump, printed so two FRESH
        /// Editor sessions can be compared with one glance at their logs. FNV, not
        /// string GetHashCode, precisely because the latter is not stable across processes -- the trap
        /// the smell test below bans. </summary>
        private static string Fnv1a64(List<string> lines)
        {
            ulong hash = 14695981039346656037UL;
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                for (int c = 0; c < line.Length; c++)
                {
                    hash ^= line[c];
                    hash *= 1099511628211UL;
                }
                hash ^= (byte)'\n';
                hash *= 1099511628211UL;
            }
            return hash.ToString("x16", CultureInfo.InvariantCulture);
        }
        /// <summary> The static smell test the spec asks for, run as a check rather than a rule people
        /// must remember: every City/ script is scanned for the five constructs that make determinism
        /// impossible in a generation path -- the two engine randoms, the wall clock, GUID minting, and
        /// -- the one that surprises people -- <c>GetHashCode()</c>, whose value for a string is not
        /// stable across .NET processes, so a city keyed on it reproduces all afternoon and is different
        /// tomorrow. Each hit is a failure UNLESS its own line, or the comment block directly above it,
        /// carries the written marker <c>determinism-exempt</c>; that is the "written exemption" the spec
        /// allows, placed at the site rather than in a list nobody finds.
        ///
        /// The block, not just one line above: this scanner used to look exactly one line up, which meant
        /// a three-line justification exempted nothing, because the marker had opened the block and the
        /// line adjacent to the construct was its last sentence. That is a formatting accident, not a
        /// missing exemption -- and it cost <c>RoadTypeChange.GetHashCode</c> a failure whose stated
        /// reasoning was sitting two lines higher. An exemption worth granting is usually worth several
        /// lines of why, so the rule that survives is: the marker must appear in the unbroken run of
        /// comment lines immediately above the construct. A blank line ends that run, so a marker cannot
        /// reach down the file from an unrelated block.
        ///
        /// The needles are assembled from fragments and
        /// this file is skipped, so the scanner never trips over its own pattern definitions. The scan
        /// root is derived from <see cref="Application.dataPath"/>, never a hard-coded drive (C-04). </summary>
        private static void SmellTest(StringBuilder builder, List<string> failures)
        {
            string[] needles =
            {
                "UnityEngine" + ".Random",
                "Random" + ".Range",
                "DateTime" + ".Now",
                "Guid" + ".NewGuid",
                "GetHash" + "Code()",
            };
            const string ExemptMarker = "determinism-exempt";

            string root = Application.dataPath + "/RoadCityMaster/City";
            if (!Directory.Exists(root))
            {
                failures.Add("smell-test: scan root not found (" + root + ")");
                return;
            }

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            int scanned = 0, hits = 0;
            for (int fi = 0; fi < files.Length; fi++)
            {
                if (Path.GetFileName(files[fi]) == "CityDeterminismAudit.cs") { continue; }
                scanned++;
                string[] src = File.ReadAllLines(files[fi]);
                string rel = files[fi].Replace('\\', '/');
                int cut = rel.IndexOf("/Assets/");
                rel = cut >= 0 ? rel.Substring(cut + 1) : rel;
                for (int ln = 0; ln < src.Length; ln++)
                {
                    for (int ni = 0; ni < needles.Length; ni++)
                    {
                        if (src[ln].IndexOf(needles[ni], System.StringComparison.Ordinal) < 0) { continue; }
                        if (src[ln].Contains(ExemptMarker) || MarkedInBlockAbove(src, ln, ExemptMarker)) { continue; }
                        hits++;
                        failures.Add("smell-test: " + rel + ":" + (ln + 1) + "  " + needles[ni]);
                    }
                }
            }
            builder.Append("RCMDET smell-test    scanned ").Append(scanned)
                   .Append(" files  hits ").Append(hits).Append("\n");
        }

        /// <summary> Walks up from <paramref name="line"/> through the unbroken run of comment lines directly above it,
        /// looking for the marker. Stops at the first line that is not a comment -- code, a blank line, or a brace --
        /// so the run belongs to the construct and cannot be inherited from a block further up the file.
        ///
        /// Accepts every comment opener C# has, including <c>///</c> (which <c>//</c> already covers) and the two body
        /// forms <c>*</c> and <c>*/</c> that a <c>/* */</c> block presents, because an author who justified an
        /// exemption inside a block comment has still written it at the site. </summary>
        private static bool MarkedInBlockAbove(string[] src, int line, string marker)
        {
            for (int ln = line - 1; ln >= 0; ln--)
            {
                string trimmed = src[ln].TrimStart();
                bool isComment = trimmed.StartsWith("//", System.StringComparison.Ordinal)
                              || trimmed.StartsWith("*", System.StringComparison.Ordinal)
                              || trimmed.StartsWith("/*", System.StringComparison.Ordinal);
                if (!isComment) { return false; }
                if (trimmed.Contains(marker)) { return true; }
            }
            return false;
        }
    }
}
#endif
