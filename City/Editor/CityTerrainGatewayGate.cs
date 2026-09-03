#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-57's gate: the terrain-modification gateway. It checks the four properties the spec singles out --
    /// ONE mode decision rather than a check at each site, ONE notification per build carrying the union of everything
    /// touched (on delete as well as on build), a matching release for every request, and the same road in both modes --
    /// plus the search the spec actually names: no direct <c>TerrainData</c> write anywhere in the tool outside the
    /// gateway's own sink. Everything here is scene-free: the sinks are driven with a null terrain, so what is under
    /// test is the dispatch, the accounting and the refusals, not the engine's own heightfield application. </summary>
    public static class CityTerrainGatewayGate
    {
        [MenuItem("Window/Road City Master/Testing/Terrain write gateway")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError("RCMGATEWAY FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            Run(out report);
            Debug.Log(report);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();

            CaseOneModeDecision(failures);
            CaseConnectedWritesNothing(failures);
            CaseNotifyBoundedAndTight(failures);
            CaseStraightRunIsOneAnnouncement(failures);
            CaseNotifyOnDelete(failures);
            CaseEmptyBuildNotifiesNothing(failures);
            CaseRegionIsRequired(failures);
            CaseEveryRequestHasARelease(failures);
            CaseStandaloneRefusesInWords(failures);
            CaseSameRoadSameLedgerBothModes(failures);
            CaseRegionUnionIsOrderIndependent(failures);
            CaseLeakedBuildIsClosed(failures);
            CaseLedgerIsBounded(failures);
            CaseNoTerrainWriteOutsideTheSink(failures);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(failures.Count == 0 ? "RCMGATEWAY result PASS" : "RCMGATEWAY result FAIL");
            sb.Append("  cases 14  failures ").Append(failures.Count);
            for (int i = 0; i < failures.Count; i++) { sb.Append("\n  - ").Append(failures[i]); }
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string what, List<string> failures)
        {
            if (!condition) { failures.Add(what); }
        }


        #region "A provider double that counts what it was told"
        // The double itself lives in TestSupport/CountingProvider.cs, because RM-98's tunnel-ground gate needs the same
        // one. It was a nested private class here until a second gate needed it; two copies of a test double are how two
        // tests quietly end up testing different things.

        private static TerrainRegion Cell(double x, double z)
        {
            return TerrainRegion.FromExtents(x, 0.0, z, x + 10.0, 8.0, z + 10.0);
        }
        #endregion


        #region "Cases"
        /// <summary> The mode is decided once and stated. A gateway cannot be talked into changing it, and the default
        /// with nobody connected is standalone -- the road tool's own behaviour, not a refusal. </summary>
        private static void CaseOneModeDecision(List<string> failures)
        {
            TerrainWriteGateway standalone = TerrainWriteGateway.ForStandalone();
            Expect(standalone.Mode == TerrainProviderMode.Standalone, "a standalone gateway is not in standalone mode",
                failures);
            Expect(standalone.PathForWrites == TerrainWritePath.DirectWrite,
                "standalone does not dispatch to the direct path", failures);
            Expect(standalone.PathLine().Contains("Standalone"), "standalone does not say so in words", failures);

            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway connected = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");
            Expect(connected.Mode == TerrainProviderMode.Connected, "a connected gateway is not in connected mode",
                failures);
            Expect(connected.PathForWrites == TerrainWritePath.RequestedThroughProvider,
                "connected does not dispatch through the provider", failures);
            Expect(connected.PathLine().Contains("TerrainSculptor"),
                "connected mode does not name the provider it is connected to", failures);

            // A null provider is not a connection: it must fall back to the tool's own path, never to a gateway that
            // silently writes nothing.
            TerrainWriteGateway noProvider = TerrainWriteGateway.ForProvider(null, "nobody");
            Expect(noProvider.Mode == TerrainProviderMode.Standalone,
                "a null provider was treated as a connection", failures);

            TerrainWriteGateway.Uninstall();
            Expect(TerrainWriteGateway.Active.Mode == TerrainProviderMode.Standalone,
                "the default gateway is not standalone", failures);
            Expect(TerrainWriteGateway.Active.PathForWrites == TerrainWritePath.DirectWrite,
                "the default gateway would not terraform: an unconnected tool must still work", failures);
        }

        /// <summary> In connected mode the provider owns the terrain, so the tool writes nothing -- and that is a
        /// number the sink reports, not a claim. </summary>
        private static void CaseConnectedWritesNothing(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            ProviderTerrainSink sink = new ProviderTerrainSink(provider, "TerrainSculptor");
            TerrainWriteGateway gateway = new TerrainWriteGateway(TerrainProviderMode.Connected, sink,
                "TerrainSculptor");

            gateway.BeginBuild("one road");
            TerrainWriteReceipt h = gateway.WriteHeights(null, new float[4, 4], Cell(0.0, 0.0), "road A");
            gateway.WriteDetailLayer(null, 0, new int[4, 4], Cell(10.0, 0.0), "road A");
            gateway.WriteTrees(null, new TreeInstance[0], Cell(20.0, 0.0), "road A");

            Expect(h.path == TerrainWritePath.RequestedThroughProvider,
                "a connected height write did not report the provider path", failures);
            Expect(sink.SuppressedWriteCount == 0,
                "the gateway called the provider sink's write methods: in connected mode a write must not be attempted "
                + "at all", failures);
            Expect(provider.notified.Count == 0,
                "the provider was notified mid-build: notification belongs at the end, once", failures);

            gateway.EndBuild();
            Expect(provider.notified.Count == 1, "closing a connected build did not notify the provider exactly once",
                failures);
        }

        /// <summary> A hundred-node road makes a handful of round trips instead of a hundred -- AND the ground it
        /// announces stays close to the ground it changed. Both halves matter. Announcing per segment is a hundred
        /// round trips; announcing one box around the whole road is one round trip that asks the provider to heal a
        /// map-sized rectangle, which for a diagonal road is mostly terrain nobody touched. This case pins both: the
        /// count is capped, nothing is missed, and the announced area stays a small fraction of the bounding box.
        /// </summary>
        private static void CaseNotifyBoundedAndTight(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");

            TerrainRegion expected = TerrainRegion.Empty;
            gateway.BeginBuild("long diagonal road");
            for (int i = 0; i < 100; i++)
            {
                TerrainRegion piece = Cell(i * 8.0, i * 3.0);
                expected = expected.Union(piece);
                gateway.WriteHeights(null, new float[4, 4], piece, "long road");
            }
            TerrainRegion extent = gateway.EndBuild();

            Expect(provider.notified.Count >= 1, "100 segment writes announced nothing at all", failures);
            Expect(provider.notified.Count <= TerrainRegionSet.DefaultLimit,
                "100 segment writes produced " + provider.notified.Count + " announcements, above the cap of "
                + TerrainRegionSet.DefaultLimit, failures);

            // Nothing may be missed: the boxes announced must together cover every segment written.
            TerrainRegion announcedUnion = TerrainRegion.Empty;
            double announcedArea = 0.0;
            for (int i = 0; i < provider.notified.Count; i++)
            {
                TerrainRegion box = TerrainRegion.FromBounds(provider.notified[i]);
                announcedUnion = announcedUnion.Union(box);
                announcedArea += box.FootprintArea;
            }
            Expect(announcedUnion.Contains(expected),
                "the announced boxes do not cover every segment that was written: " + announcedUnion.Describe()
                + " vs " + expected.Describe(), failures);
            Expect(extent.Describe() == expected.Describe(),
                "the build reported an extent that is not the union of its segments: " + extent.Describe() + " vs "
                + expected.Describe(), failures);

            // The property that protects the frame rate: far less ground announced than one box would have covered.
            Expect(announcedArea < expected.FootprintArea * 0.25,
                "the announcement covers " + announcedArea + " m2 of the " + expected.FootprintArea
                + " m2 bounding box -- batching is not tightening anything", failures);

            Expect(gateway.Ledger.Count == 100, "the ledger did not record all 100 writes", failures);
            Expect(gateway.BuildCount == 1, "closing one build counted " + gateway.BuildCount, failures);
            Expect(gateway.PendingRegion.hasArea == false, "closing a build did not clear the accumulated region",
                failures);
        }

        /// <summary> A straight run collapses to ONE announcement. This is the other half of the previous case: the
        /// batching must not be so timid that an ordinary road still costs many round trips. </summary>
        private static void CaseStraightRunIsOneAnnouncement(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");

            gateway.BeginBuild("straight road");
            for (int i = 0; i < 100; i++)
            {
                gateway.WriteHeights(null, new float[4, 4], Cell(i * 9.0, 0.0), "straight road");
            }
            gateway.EndBuild();

            Expect(provider.notified.Count == 1,
                "a straight 100-segment road produced " + provider.notified.Count
                + " announcements instead of 1: overlapping neighbours are not merging", failures);
        }

        /// <summary> Deleting a road is a change too. Restoring announces exactly like building, covering the ground
        /// put back -- otherwise a provider keeps the road's shape after the road is gone. </summary>
        private static void CaseNotifyOnDelete(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");

            gateway.BeginBuild("build road");
            gateway.WriteHeights(null, new float[4, 4], Cell(0.0, 0.0), "road A");
            gateway.EndBuild();
            int afterBuild = provider.notified.Count;
            Expect(afterBuild == 1, "building one segment announced " + afterBuild + " regions, not 1", failures);

            TerrainRegion expected = Cell(0.0, 0.0).Union(Cell(10.0, 10.0));
            gateway.BeginBuild("delete road");
            TerrainWriteReceipt r = gateway.RestoreHeights(null, new float[4, 4], Cell(0.0, 0.0), "road A");
            gateway.RestoreDetailLayer(null, 0, new int[4, 4], Cell(10.0, 10.0), "road A");
            TerrainRegion restored = gateway.EndBuild();

            Expect(r.kind == TerrainWriteKind.Restore, "a restore was not recorded as a restore", failures);
            Expect(provider.notified.Count > afterBuild, "the delete did not announce anything", failures);

            TerrainRegion deleteUnion = TerrainRegion.Empty;
            for (int i = afterBuild; i < provider.notified.Count; i++)
            {
                deleteUnion = deleteUnion.Union(TerrainRegion.FromBounds(provider.notified[i]));
            }
            Expect(deleteUnion.Contains(expected),
                "the delete announced " + deleteUnion.Describe() + ", which does not cover the ground it put back: "
                + expected.Describe(), failures);
            Expect(restored.Describe() == expected.Describe(),
                "the delete reported extent " + restored.Describe() + " instead of " + expected.Describe(), failures);
            Expect(gateway.BuildCount == 2, "two builds counted as " + gateway.BuildCount, failures);
        }

        /// <summary> A build that changed nothing announces nothing. An empty region is not a box at the origin, and
        /// notifying with one would send the provider to heal the wrong ground. </summary>
        private static void CaseEmptyBuildNotifiesNothing(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");
            gateway.BeginBuild("a road that touched nothing");
            TerrainRegion region = gateway.EndBuild();
            Expect(provider.notified.Count == 0, "a build that changed nothing still notified the provider", failures);
            Expect(!region.hasArea, "an empty build reported a region", failures);
        }

        /// <summary> A write that does not say WHICH ground it changed is refused in words, and does not enter the
        /// ledger -- because a change that cannot be accumulated cannot be announced. </summary>
        private static void CaseRegionIsRequired(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");
            gateway.BeginBuild("careless road");
            TerrainWriteReceipt r = gateway.WriteHeights(null, new float[4, 4], TerrainRegion.Empty, "road A");
            gateway.EndBuild();

            Expect(r.path == TerrainWritePath.Refused, "a write with no region was accepted", failures);
            Expect(r.refusalReason.Contains("region"), "the refusal does not say what was missing", failures);
            Expect(gateway.Ledger.Count == 0, "a refused write entered the ledger", failures);
            Expect(provider.notified.Count == 0, "a refused write still notified the provider", failures);
        }

        /// <summary> Every request that can be made has a matching release ON THE GATEWAY, and anything still held is
        /// listed by name -- so a missing release is visible at review rather than after a scene has accumulated
        /// orphaned modifications. A double release is reported, not absorbed. </summary>
        private static void CaseEveryRequestHasARelease(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");
            string refusal;

            TerrainRequestHandle cut = gateway.RequestCutRegion(Cell(0.0, 0.0), "road A", out refusal);
            TerrainRequestHandle tunnel = gateway.RequestTunnelOpening(Cell(50.0, 0.0), "tunnel mouth north",
                out refusal);
            TerrainRequestHandle pad = gateway.RequestBuildingPad(Cell(100.0, 0.0), "pad 7", out refusal);

            Expect(cut.IsValid && tunnel.IsValid && pad.IsValid,
                "a connected provider refused a hold it should grant", failures);
            Expect(gateway.OutstandingCount == 3,
                "the gateway is holding " + gateway.OutstandingCount + " requests, not 3", failures);
            Expect(provider.openTunnels.Count == 1, "the tunnel opening never reached the provider", failures);

            IList<string> held = gateway.DescribeOutstanding();
            bool named = false;
            for (int i = 0; i < held.Count; i++)
            {
                if (held[i].Contains("tunnel mouth north")) { named = true; }
            }
            Expect(named, "an outstanding hold does not name who asked for it", failures);

            Expect(gateway.ReleaseCutRegion(cut), "a cut region could not be released", failures);
            Expect(gateway.ReleaseTunnelOpening(tunnel), "a tunnel opening could not be released", failures);
            Expect(gateway.ReleaseBuildingPad(pad), "a building pad could not be released", failures);
            Expect(gateway.OutstandingCount == 0, "releasing everything left holds outstanding", failures);
            Expect(provider.releasedTunnels == 1 && provider.openTunnels.Count == 0,
                "the provider still holds a tunnel opening the tool released", failures);

            Expect(!gateway.ReleaseCutRegion(cut), "a double release reported success", failures);
            Expect(!gateway.ReleaseTunnelOpening(cut),
                "a handle was released as the wrong kind of hold", failures);

            // A hold the tool forgets is not silently absorbed: the teardown says how many were still open.
            TerrainRequestHandle forgotten = gateway.RequestBuildingPad(Cell(200.0, 0.0), "pad 8", out refusal);
            Expect(forgotten.IsValid, "a second pad hold was refused", failures);
            Expect(gateway.ReleaseAll() == 1, "the teardown did not report the hold nobody released", failures);
            Expect(gateway.OutstandingCount == 0, "the teardown left a hold outstanding", failures);
        }

        /// <summary> Standalone refuses what it cannot do IN WORDS rather than faking it, and the words say what to
        /// connect. A refusal returns an obviously-dead handle, never a plausible-looking zero. </summary>
        private static void CaseStandaloneRefusesInWords(List<string> failures)
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.ForStandalone();
            string refusal;

            TerrainRequestHandle cut = gateway.RequestCutRegion(Cell(0.0, 0.0), "road A", out refusal);
            Expect(cut.IsValid, "standalone refused to hold a cut region, which it can do", failures);

            TerrainRequestHandle tunnel = gateway.RequestTunnelOpening(Cell(50.0, 0.0), "tunnel mouth", out refusal);
            Expect(!tunnel.IsValid, "standalone handed back a tunnel opening it cannot hold", failures);
            Expect(refusal.Contains("provider"), "the standalone refusal does not say what is needed: " + refusal,
                failures);

            TerrainRequestHandle pad = gateway.RequestBuildingPad(Cell(100.0, 0.0), "pad 7", out refusal);
            Expect(!pad.IsValid, "standalone handed back a pad hold it cannot keep", failures);
            Expect(refusal.Contains("standalone"), "the pad refusal does not describe standalone behaviour: " + refusal,
                failures);

            Expect(gateway.OutstandingCount == 1, "a refused hold was recorded as outstanding", failures);
            Expect(gateway.ReleaseAll() == 1, "the granted hold was not released by the teardown", failures);

            // Standalone still writes: the direct path is not a degraded mode.
            gateway.BeginBuild("standalone road");
            TerrainWriteReceipt w = gateway.WriteHeights(null, new float[4, 4], Cell(0.0, 0.0), "road A");
            gateway.EndBuild();
            Expect(w.path == TerrainWritePath.DirectWrite, "standalone did not take the direct path", failures);
            Expect(w.Accepted, "standalone refused an ordinary height write", failures);
        }

        /// <summary> The same road in both modes asks for the same things in the same order. The ledger deliberately
        /// excludes the dispatch path, so an identical signature means the two modes differ only in HOW the change was
        /// carried out -- which is what "switching modes with the same road produces the same road" means for a check a
        /// machine can run. </summary>
        private static void CaseSameRoadSameLedgerBothModes(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway connected = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");
            TerrainWriteGateway standalone = TerrainWriteGateway.ForStandalone();

            DriveOneRoad(connected);
            DriveOneRoad(standalone);

            Expect(connected.Ledger.Count == standalone.Ledger.Count,
                "the two modes recorded a different number of changes", failures);
            Expect(connected.Ledger.Signature() == standalone.Ledger.Signature(),
                "the same road asked for different terrain in the two modes", failures);
            Expect(connected.NotifyCount == standalone.NotifyCount,
                "the two modes announced a different number of times", failures);
        }

        /// <summary> One scripted road, driven identically through whichever gateway it is given. </summary>
        private static void DriveOneRoad(TerrainWriteGateway gateway)
        {
            gateway.BeginBuild("road B");
            for (int i = 0; i < 12; i++)
            {
                TerrainRegion piece = Cell(i * 12.0, i * 5.0);
                gateway.WriteHeights(null, new float[4, 4], piece, "road B");
                gateway.WriteDetailLayer(null, 0, new int[4, 4], piece, "road B");
            }
            gateway.WriteTrees(null, new TreeInstance[0], Cell(0.0, 0.0), "road B");
            gateway.EndBuild();
        }

        /// <summary> The union does not depend on the order the segments arrived in, which is what makes the one
        /// notification per build reproducible (C-06). </summary>
        private static void CaseRegionUnionIsOrderIndependent(List<string> failures)
        {
            TerrainRegion a = Cell(0.0, 0.0);
            TerrainRegion b = Cell(90.0, 40.0);
            TerrainRegion c = Cell(-30.0, 20.0);

            string forward = a.Union(b).Union(c).Describe();
            string backward = c.Union(b).Union(a).Describe();
            string mixed = b.Union(c).Union(a).Describe();
            Expect(forward == backward && forward == mixed,
                "the accumulated region depends on the order the segments arrived in", failures);

            Expect(a.Union(TerrainRegion.Empty).Describe() == a.Describe(),
                "unioning with an empty region changed the region", failures);
            Expect(TerrainRegion.Empty.Union(a).Describe() == a.Describe(),
                "an empty region did not absorb into a real one", failures);
            Expect(a.Union(b).Contains(a) && a.Union(b).Contains(b),
                "the union does not contain both of its parts", failures);

            // A cell region is 10 m square, so the two-cell union spans the gap between them.
            TerrainRegion span = a.Union(b);
            Expect(span.SizeX > 99.0 && span.SizeZ > 49.0,
                "the union is smaller than the ground it must cover", failures);
        }

        /// <summary> A build that never closed does not poison the next one. If something throws between a begin and
        /// its end, the next begin closes the abandoned build instead of inheriting its ground -- otherwise the
        /// accumulation would run for the rest of the session and eventually announce every road ever built at once.
        /// </summary>
        private static void CaseLeakedBuildIsClosed(List<string> failures)
        {
            CountingProvider provider = new CountingProvider();
            TerrainWriteGateway gateway = TerrainWriteGateway.ForProvider(provider, "TerrainSculptor");

            gateway.BeginBuild("road that threw");
            gateway.WriteHeights(null, new float[4, 4], Cell(0.0, 0.0), "road A");
            // ...and no EndBuild: the caller blew up.

            gateway.BeginBuild("the next road");
            Expect(provider.notified.Count == 1,
                "the abandoned build was not closed when the next one opened", failures);
            Expect(!gateway.PendingRegion.Contains(Cell(0.0, 0.0)),
                "the new build inherited the abandoned build's ground", failures);

            gateway.WriteHeights(null, new float[4, 4], Cell(1000.0, 1000.0), "road B");
            TerrainRegion second = gateway.EndBuild();
            Expect(provider.notified.Count == 2, "the second build did not announce on its own", failures);
            Expect(!second.Contains(Cell(0.0, 0.0)),
                "the second build announced ground that belonged to the first", failures);

            // Re-entering the SAME build is still one change, not a flush.
            gateway.BeginBuild("one road");
            gateway.WriteHeights(null, new float[4, 4], Cell(0.0, 0.0), "road C");
            gateway.BeginBuild("one road");
            gateway.WriteHeights(null, new float[4, 4], Cell(9.0, 0.0), "road C");
            gateway.EndBuild();
            Expect(provider.notified.Count == 3,
                "a re-entrant begin with the same label split one change into several announcements", failures);
        }

        /// <summary> The gateway lives for the whole session, so its ledger must not grow for the whole session. Past
        /// the cap, entries are counted rather than stored: a long editing session cannot turn the record of what
        /// happened into a memory leak, and nothing is silently lost because the total keeps rising. </summary>
        private static void CaseLedgerIsBounded(List<string> failures)
        {
            TerrainWriteGateway gateway = TerrainWriteGateway.ForStandalone();
            int writes = TerrainLedger.Capacity + 100;

            gateway.BeginBuild("a very long editing session");
            for (int i = 0; i < writes; i++)
            {
                // Overlapping cells, so the region set stays at one box and this case measures the ledger only.
                gateway.WriteHeights(null, new float[2, 2], Cell(i * 5.0, 0.0), "road A");
            }
            gateway.EndBuild();

            Expect(gateway.Ledger.Count == TerrainLedger.Capacity,
                "the ledger holds " + gateway.Ledger.Count + " entries, not the cap of " + TerrainLedger.Capacity,
                failures);
            Expect(gateway.Ledger.TotalAdded == writes,
                "the ledger stopped counting at the cap: " + gateway.Ledger.TotalAdded + " of " + writes, failures);
            Expect(gateway.Ledger.Dropped == 100,
                "the ledger reports " + gateway.Ledger.Dropped + " dropped entries, not 100", failures);
        }

        /// <summary> The search the spec names: with the gateway in place, a direct TerrainData write outside it must
        /// not exist anywhere in the tool. This is the check that makes one mode decision worth having -- a per-site
        /// conditional could not be verified this way, because a search cannot tell a correct conditional from a
        /// forgotten one. The gateway's own sink is the single permitted home for these calls. </summary>
        private static void CaseNoTerrainWriteOutsideTheSink(List<string> failures)
        {
            string root = Path.Combine(Application.dataPath, "RoadCityMaster").Replace('\\', '/');
            if (!Directory.Exists(root))
            {
                failures.Add("could not find the tool folder to search for stray terrain writes");
                return;
            }

            string[] banned = new string[]
            {
                "SetHeights(", "SetHeightsDelayLOD(", "SetDetailLayer(", "SetAlphamaps(", "SetHoles(",
                "treeInstances ="
            };
            string permitted = "City/Runtime/Terrain/DirectTerrainSink.cs";
            string thisGate = "City/Editor/CityTerrainGatewayGate.cs";

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            int strays = 0;
            for (int f = 0; f < files.Length; f++)
            {
                string relative = files[f].Replace('\\', '/');
                if (relative.EndsWith(permitted) || relative.EndsWith(thisGate)) { continue; }

                string[] lines = File.ReadAllLines(files[f]);
                for (int i = 0; i < lines.Length; i++)
                {
                    string trimmed = lines[i].TrimStart();
                    if (trimmed.StartsWith("//") || trimmed.StartsWith("*") || trimmed.StartsWith("/*")) { continue; }
                    for (int b = 0; b < banned.Length; b++)
                    {
                        if (trimmed.Contains(banned[b]))
                        {
                            strays++;
                            failures.Add("a terrain write lives outside the gateway: " + banned[b] + " at "
                                + relative.Substring(relative.IndexOf("RoadCityMaster")) + ":" + (i + 1));
                        }
                    }
                }
            }
            Expect(strays == 0, "the tool writes terrain from " + strays + " place(s) the gateway does not own",
                failures);
        }
        #endregion
    }
}
#endif
