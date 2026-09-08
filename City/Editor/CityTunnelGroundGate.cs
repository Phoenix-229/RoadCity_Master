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


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-98's tunnel gate: choosing a tunnel must not cut the hill the tunnel passes under.
    ///
    /// This is the gate for a defect that was reported from a scene and could not be caught by any test that existed,
    /// because nothing was wrong with either side's tunnel code. The bore was opened correctly and the hill was correctly
    /// left standing by the terraformer; what levelled it was the SENTENCE between the two tools. A build announces the
    /// ground it changed as boxes; the host turns each box into "this ground is a cut", pre-loads it, heals it and
    /// re-flattens it after every regeneration. Announce the span over a bore that way and the hill is levelled by a host
    /// doing exactly what it was told.
    ///
    /// So what is under test here is the sentence. Six cases, no scene, no terrain, no provider except a double that
    /// writes down what it was told:
    ///
    ///   1. A bore under a deep hill leaves every station's ground EXACTLY where it was -- equality, not a tolerance,
    ///      because "almost the same height" is how a hill sinks a centimetre per rebuild.
    ///   2. The host hears about the two portal notches and the tunnel opening, and NOT about the hill.
    ///   3. Ground too shallow for a bore becomes a box with fill over the roof, and the fill only ever goes UP.
    ///   4. An immersed tube's trench is dredged and backfilled to the bed it started at, so the bed is not announced
    ///      as changed ground either -- and the trench is wider than the tube by its 1:3 side slopes.
    ///   5. A bore driven too close under a water bed is REFUSED in words, and nothing is carved for a refused plan.
    ///   6. The legacy terraformer's own tunnel branch agrees with the policy about how much ground sits over a tunnel,
    ///      so the two halves of the tool cannot disagree about the same hill.
    ///
    /// Every figure below is derived from the classes, never typed in twice: a gate that hard-codes 8.3 m passes after
    /// someone changes the bore radius and the tool is wrong in the scene. </summary>
    public static class CityTunnelGroundGate
    {
        [MenuItem("Window/Road City Master/Testing/Tunnel ground and the linker")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError("RCMTUNNELGROUND FAILED"); }
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

            CaseOneDeepHillIsUntouched(failures);
            CaseTwoOnlyThePortalsAreAnnounced(failures);
            CaseThreeShallowGroundIsFilledNeverCut(failures);
            CaseFourImmersedTubeIsRestored(failures);
            CaseFiveShortCoverUnderWaterIsRefused(failures);
            CaseSixLegacyTerraformerAgrees(failures);

            StringBuilder sb = new StringBuilder();
            sb.Append("RM-98 tunnel ground / linker gate: ");
            sb.Append(failures.Count == 0 ? "PASS" : "FAIL");
            for (int i = 0; i < failures.Count; i++) { sb.Append("\n  - ").Append(failures[i]); }
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string what, List<string> failures)
        {
            if (!condition) { failures.Add(what); }
        }


        #region "The scene these cases share"
        // One tunnel, stated once. The road sits at 100 m so that a station's elevation reads as an obvious offset from
        // it, and the pavement build-up is a real 0.8 m so the bore's invert is below the carriageway rather than on it.
        private const double RoadSurface = 100.0;
        private const double Pavement = 0.8;
        private const double RunLength = 300.0;
        private const double CorridorHalfWidth = 12.0;

        private static TunnelClass Tunnel() { return new TunnelClass(); }
        private static TunnelCoverClass Cover() { return TunnelCoverClass.Default(); }
        private static GradeSeparationClass Separation() { return new GradeSeparationClass(); }

        /// <summary> A run over ground at the given height ABOVE the carriageway. </summary>
        private static TunnelLinkerBridge.TunnelRun MakeRun(double groundAboveRoad, bool underWater, bool immersedTube)
        {
            TunnelLinkerBridge.TunnelRun run = new TunnelLinkerBridge.TunnelRun();
            run.centreX = 500.0;
            run.centreZ = 250.0;
            run.lengthMetres = RunLength;
            run.alongX = true;
            run.roadSurfaceElevationMetres = RoadSurface;
            run.pavementThicknessMetres = Pavement;
            run.tightestGroundElevationMetres = RoadSurface + groundAboveRoad;
            run.corridorHalfWidthMetres = CorridorHalfWidth;
            run.underWater = underWater;
            run.immersedTube = immersedTube;
            run.burialDepthMetres = immersedTube ? 2.5 : 0.0;
            return run;
        }

        /// <summary> Stations along that run: evenly spaced, all inside the tunnel, all on ground the given height above
        /// the carriageway. The distance to the nearer portal is real, so the stations that genuinely are in a portal
        /// notch classify as one instead of being mislabelled protected ground. </summary>
        private static List<TunnelTerrainPolicy.StationContext> Stations(double groundAboveRoad, int count,
            bool underWater, bool immersedTube)
        {
            List<TunnelTerrainPolicy.StationContext> list = new List<TunnelTerrainPolicy.StationContext>();
            double step = RunLength / (count - 1);
            for (int i = 0; i < count; i++)
            {
                double along = i * step;
                double toNearer = along < RunLength - along ? along : RunLength - along;
                TunnelTerrainPolicy.StationContext s = new TunnelTerrainPolicy.StationContext();
                s.insideTunnel = true;
                s.distanceToNearerPortalMetres = toNearer;
                s.underWater = underWater;
                s.immersedTube = immersedTube;
                s.groundElevationMetres = RoadSurface + groundAboveRoad;
                s.roadSurfaceElevationMetres = RoadSurface;
                s.pavementThicknessMetres = Pavement;
                list.Add(s);
            }
            return list;
        }

        /// <summary> What the ordinary terraformer would have done at each station: pull the ground to the carriageway.
        /// This is the target a tunnel station must NOT be conformed to, so handing it in is what makes the scan's
        /// verdict mean something. </summary>
        private static List<double> ConformTargets(int count)
        {
            List<double> targets = new List<double>();
            for (int i = 0; i < count; i++) { targets.Add(RoadSurface); }
            return targets;
        }

        /// <summary> A gateway wired to a host double, with the double handed back. </summary>
        private static TerrainWriteGateway ConnectedGateway(out CountingProvider host)
        {
            host = new CountingProvider();
            return TerrainWriteGateway.ForProvider(host, "RM-98 gate host");
        }
        #endregion


        #region "Case 1: a bore under a deep hill is left exactly alone"
        private static void CaseOneDeepHillIsUntouched(List<string> failures)
        {
            const int count = 21;
            double deep = TunnelTerrainPolicy.RequiredBoredCoverMetres(Tunnel(), Cover())
                + TunnelTerrainPolicy.ExcavatedDiameterMetres(Tunnel()) + 10.0;

            List<TunnelTerrainPolicy.StationContext> stations = Stations(deep, count, false, false);
            List<double> targets = ConformTargets(count);
            TunnelTerrainPolicy.TunnelGroundScan scan = TunnelTerrainPolicy.ScanTunnelGround(stations, targets,
                Tunnel(), Cover(), Separation());

            Expect(scan.hillIntact, "a bore under deep ground still reported the hill as cut: " + scan.Describe(),
                failures);
            Expect(scan.firstCutStationIndex == -1,
                "a bore under deep ground named a cut station (" + scan.firstCutStationIndex + ")", failures);
            Expect(scan.coverFillCount == 0, "deep ground was given cover fill it does not need", failures);
            Expect(scan.leftIntactCount > 0, "no station over a deep bore was left intact at all", failures);
            Expect(scan.portalNotchCount > 0,
                "not one station fell in a portal notch, so the notch case was never exercised", failures);

            // The assertion the defect turns on: EQUALITY. A station left intact keeps the height it came in with, bit
            // for bit, because a hill that settles half a millimetre per rebuild is a hill that disappears.
            int checkedIntact = 0;
            for (int i = 0; i < stations.Count; i++)
            {
                GroundTreatment t = TunnelTerrainPolicy.ClassifyStation(stations[i], Tunnel(), Cover(), Separation());
                if (t != GroundTreatment.LeaveIntact) { continue; }
                double finished = TunnelTerrainPolicy.FinishedGroundElevationMetres(t, stations[i], targets[i],
                    Tunnel(), Cover(), Separation());
                if (finished != stations[i].groundElevationMetres)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture,
                        "station {0} over a bore finished at {1:0.#####} m instead of the {2:0.#####} m it came in at",
                        i, finished, stations[i].groundElevationMetres));
                    break;
                }
                checkedIntact++;
            }
            Expect(checkedIntact > 0, "no intact station was actually height-checked", failures);
            Expect(!TunnelTerrainPolicy.MayLowerGround(GroundTreatment.LeaveIntact),
                "protected ground reports that it may be lowered", failures);
            Expect(TunnelTerrainPolicy.GroundEndsUnchanged(GroundTreatment.LeaveIntact),
                "protected ground does not report that it ends where it started", failures);
        }
        #endregion


        #region "Case 2: the host hears about the portals, not about the hill"
        private static void CaseTwoOnlyThePortalsAreAnnounced(List<string> failures)
        {
            double deep = TunnelTerrainPolicy.RequiredBoredCoverMetres(Tunnel(), Cover()) * 2.0;
            TunnelLinkerBridge.TunnelRun run = MakeRun(deep, false, false);

            TunnelLinkerBridge.TunnelLinkPlan plan = TunnelLinkerBridge.Plan(run, TunnelMode.Connected, Tunnel(),
                Cover(), Separation());
            Expect(plan.viable, "a bore under deep ground was not viable: " + plan.Describe(), failures);
            Expect(plan.spanIsUntouched,
                "the ground over a bore with ample cover was still going to be worked on: " + plan.Describe(),
                failures);
            Expect(plan.span.treatment == GroundTreatment.LeaveIntact,
                "the span over a bore is labelled " + plan.span.treatment + " rather than LeaveIntact", failures);
            Expect(plan.opening.treatment == GroundTreatment.LeaveIntact,
                "the bore opening is labelled " + plan.opening.treatment + " rather than LeaveIntact", failures);
            Expect(plan.firstPortal.treatment == GroundTreatment.PortalNotch
                && plan.secondPortal.treatment == GroundTreatment.PortalNotch,
                "a portal notch is not labelled as one", failures);

            // The notch is bounded: a tunnel mouth must not quietly become a long open cutting through the hill.
            double notch = TunnelTerrainPolicy.PortalNotchLengthMetres(Tunnel(), Cover());
            Expect(plan.firstPortal.SizeX <= notch + 1e-9,
                string.Format(CultureInfo.InvariantCulture, "the portal notch runs {0:0.##} m where {1:0.##} m is the "
                + "bound", plan.firstPortal.SizeX, notch), failures);
            Expect(notch < RunLength * 0.5,
                "the portal notches are long enough to meet in the middle, so there is no protected span left",
                failures);

            CountingProvider host;
            TerrainWriteGateway gateway = ConnectedGateway(out host);
            gateway.BeginBuild("RM-98 bore under a deep hill");
            TunnelLinkerBridge.TunnelLinkResult result = TunnelLinkerBridge.Execute(gateway, plan, "rm98.gate");
            gateway.EndBuild();

            Expect(result.executed, "the plan did not execute: " + result.Describe(), failures);
            Expect(result.announcedRegionCount == 2, string.Format(CultureInfo.InvariantCulture,
                "the bridge offered {0} regions where 2 -- the two portal notches -- is the whole point",
                result.announcedRegionCount), failures);

            Expect(host.tunnelOpenings.Count == 1, string.Format(CultureInfo.InvariantCulture,
                "the host was asked for {0} tunnel openings instead of 1", host.tunnelOpenings.Count), failures);
            Expect(result.openingHandle.IsValid, "the tunnel opening came back dead: " + result.Describe(), failures);
            Expect(gateway.OutstandingCount == 1,
                "the tunnel opening was not left held open, so a terrain regeneration would fill the bore in",
                failures);

            // The headline. Two boxes reach the host, and neither of them is the hill.
            Expect(host.notified.Count == 2, string.Format(CultureInfo.InvariantCulture,
                "the host was told {0} boxes had changed where 2 are the portal notches -- a third is the hill going "
                + "back into the cut registry", host.notified.Count), failures);
            Expect(gateway.UnannouncedCount >= 1,
                "no box was withheld from the host, so the protected ground was never actually protected", failures);

            double crown = TunnelTerrainPolicy.StructureCrownElevationMetres(RoadSurface, Pavement, false, Tunnel(),
                Cover(), Separation());
            for (int i = 0; i < host.notified.Count; i++)
            {
                Bounds b = host.notified[i];
                bool reachesTheHillTop = b.max.y > (float)(run.tightestGroundElevationMetres - 0.001);
                bool isAtAPortal = Mathf.Abs(b.center.x - (float)run.centreX) > (float)(RunLength * 0.4);
                if (reachesTheHillTop && !isAtAPortal)
                {
                    failures.Add(string.Format(CultureInfo.InvariantCulture,
                        "the host was handed a box reaching the hill top ({0:0.##} m, crown {1:0.##} m) away from a "
                        + "portal -- that box is the hill", b.max.y, crown));
                    break;
                }
            }
        }
        #endregion


        #region "Case 3: shallow ground is filled, never cut"
        private static void CaseThreeShallowGroundIsFilledNeverCut(List<string> failures)
        {
            // Ground below the finished cover line: not enough to bore under, so it becomes a box with an embankment
            // over the roof. The one thing that must not happen is the ground going DOWN.
            double target = TunnelTerrainPolicy.CoverFillTargetElevationMetres(RoadSurface, false, Tunnel(), Cover(),
                Separation());
            double shallow = (target - RoadSurface) - 2.5;

            const int count = 21;
            List<TunnelTerrainPolicy.StationContext> stations = Stations(shallow, count, false, false);
            List<double> targets = ConformTargets(count);
            TunnelTerrainPolicy.TunnelGroundScan scan = TunnelTerrainPolicy.ScanTunnelGround(stations, targets,
                Tunnel(), Cover(), Separation());

            Expect(scan.hillIntact,
                "shallow ground over a tunnel was reported as cut, but filling is the only thing that happens to it: "
                + scan.Describe(), failures);
            Expect(scan.coverFillCount > 0, "shallow ground over a tunnel was not given any cover fill", failures);

            int mid = count / 2;
            GroundTreatment t = TunnelTerrainPolicy.ClassifyStation(stations[mid], Tunnel(), Cover(), Separation());
            Expect(t == GroundTreatment.CoverFill,
                "a station on shallow ground mid-tunnel classified as " + t + " rather than CoverFill", failures);
            double finished = TunnelTerrainPolicy.FinishedGroundElevationMetres(t, stations[mid], targets[mid],
                Tunnel(), Cover(), Separation());
            Expect(finished >= stations[mid].groundElevationMetres, string.Format(CultureInfo.InvariantCulture,
                "cover fill LOWERED the ground, from {0:0.###} m to {1:0.###} m",
                stations[mid].groundElevationMetres, finished), failures);
            Expect(System.Math.Abs(finished - target) < 1e-9, string.Format(CultureInfo.InvariantCulture,
                "cover fill finished at {0:0.###} m rather than the {1:0.###} m the cover chain asks for", finished,
                target), failures);
            Expect(!TunnelTerrainPolicy.GroundEndsUnchanged(GroundTreatment.CoverFill),
                "cover fill claims the ground ends where it started, so the host would never hear about the embankment",
                failures);

            // And across the link: a span that really does change IS announced -- withholding it would leave an
            // embankment with trees standing in it.
            TunnelLinkerBridge.TunnelRun run = MakeRun(shallow, false, false);
            TunnelLinkerBridge.TunnelLinkPlan plan = TunnelLinkerBridge.Plan(run, TunnelMode.Connected, Tunnel(),
                Cover(), Separation());
            Expect(plan.viable, "a shallow tunnel on land was refused: " + plan.Describe(), failures);
            Expect(plan.span.treatment == GroundTreatment.CoverFill,
                "a shallow span is labelled " + plan.span.treatment + " rather than CoverFill", failures);
            Expect(!plan.spanIsUntouched, "a shallow span claims nothing happens to it", failures);

            CountingProvider host;
            TerrainWriteGateway gateway = ConnectedGateway(out host);
            gateway.BeginBuild("RM-98 shallow cut-and-cover");
            TunnelLinkerBridge.TunnelLinkResult result = TunnelLinkerBridge.Execute(gateway, plan, "rm98.gate");
            gateway.EndBuild();

            Expect(result.announcedRegionCount == 3, string.Format(CultureInfo.InvariantCulture,
                "a shallow tunnel offered {0} regions where 3 -- two notches and the embankment -- is right",
                result.announcedRegionCount), failures);
            Expect(host.notified.Count == 3, string.Format(CultureInfo.InvariantCulture,
                "the host heard about {0} boxes for a shallow tunnel where 3 is right", host.notified.Count), failures);
        }
        #endregion


        #region "Case 4: an immersed tube's bed ends up where it started"
        private static void CaseFourImmersedTubeIsRestored(List<string> failures)
        {
            double required = TunnelTerrainPolicy.RequiredCoverMetres(true, Tunnel(), Cover());
            double boxCover = TunnelTerrainPolicy.CrownElevationMetres(RoadSurface, Separation(), Cover())
                - RoadSurface;
            double bedAboveRoad = boxCover + required + 1.0;

            const int count = 21;
            List<TunnelTerrainPolicy.StationContext> stations = Stations(bedAboveRoad, count, true, true);
            List<double> targets = ConformTargets(count);
            TunnelTerrainPolicy.TunnelGroundScan scan = TunnelTerrainPolicy.ScanTunnelGround(stations, targets,
                Tunnel(), Cover(), Separation());
            Expect(scan.hillIntact, "an immersed tube's bed was reported as cut: " + scan.Describe(), failures);
            Expect(scan.backfillCount > 0, "no station under water was scheduled for backfill", failures);

            int mid = count / 2;
            GroundTreatment t = TunnelTerrainPolicy.ClassifyStation(stations[mid], Tunnel(), Cover(), Separation());
            Expect(t == GroundTreatment.RestoreAfterBackfill,
                "a mid-tube station classified as " + t + " rather than RestoreAfterBackfill", failures);
            double finished = TunnelTerrainPolicy.FinishedGroundElevationMetres(t, stations[mid], targets[mid],
                Tunnel(), Cover(), Separation());
            Expect(finished == stations[mid].groundElevationMetres, string.Format(CultureInfo.InvariantCulture,
                "a backfilled bed finished at {0:0.#####} m instead of the {1:0.#####} m it started at", finished,
                stations[mid].groundElevationMetres), failures);

            TunnelLinkerBridge.TunnelRun run = MakeRun(bedAboveRoad, true, true);
            TunnelLinkerBridge.TunnelLinkPlan plan = TunnelLinkerBridge.Plan(run, TunnelMode.Connected, Tunnel(),
                Cover(), Separation());
            Expect(plan.viable, "an immersed tube with legal cover was refused: " + plan.Describe(), failures);
            Expect(plan.span.treatment == GroundTreatment.RestoreAfterBackfill,
                "a tube's trench is labelled " + plan.span.treatment + " rather than RestoreAfterBackfill", failures);

            // The trench is wider than the tube, by the side slopes a dredged trench actually stands at.
            double tubeWidth = TunnelTerrainPolicy.ExcavatedDiameterMetres(Tunnel());
            double trenchHalf = TunnelTerrainPolicy.DredgedTrenchHalfWidthMetres(tubeWidth, run.burialDepthMetres,
                Cover());
            Expect(trenchHalf > tubeWidth * 0.5 + 1e-9, string.Format(CultureInfo.InvariantCulture,
                "the dredged trench is {0:0.##} m half-width for a {1:0.##} m tube -- the 1:3 side slopes are missing",
                trenchHalf, tubeWidth), failures);
            Expect(plan.span.SizeZ >= trenchHalf * 2.0 - 1e-6, string.Format(CultureInfo.InvariantCulture,
                "the announced trench is {0:0.##} m across where the dredged trench is {1:0.##} m", plan.span.SizeZ,
                trenchHalf * 2.0), failures);

            // The two layers must agree. The bridge DOES offer the trench, because work happens there; the gateway
            // withholds it from the host, because the bed ends where it started. Either half alone is a bug.
            CountingProvider host;
            TerrainWriteGateway gateway = ConnectedGateway(out host);
            gateway.BeginBuild("RM-98 immersed tube");
            TunnelLinkerBridge.TunnelLinkResult result = TunnelLinkerBridge.Execute(gateway, plan, "rm98.gate");
            gateway.EndBuild();

            Expect(result.announcedRegionCount == 3, string.Format(CultureInfo.InvariantCulture,
                "the tube offered {0} regions where 3 -- two notches and the trench -- is right",
                result.announcedRegionCount), failures);
            Expect(host.notified.Count == 2, string.Format(CultureInfo.InvariantCulture,
                "the host heard about {0} boxes for an immersed tube where 2 are the portals -- the third would be the "
                + "river bed handed over as a cut", host.notified.Count), failures);
        }
        #endregion


        #region "Case 5: too little cover under water is refused, in words"
        private static void CaseFiveShortCoverUnderWaterIsRefused(List<string> failures)
        {
            double required = TunnelTerrainPolicy.RequiredCoverMetres(true, Tunnel(), Cover());
            double boreCover = TunnelTerrainPolicy.StructureCrownElevationMetres(RoadSurface, Pavement, false,
                Tunnel(), Cover(), Separation()) - RoadSurface;
            double bedAboveRoad = boreCover + required - 1.5;   // a metre and a half short of the minimum

            TunnelLinkerBridge.TunnelRun run = MakeRun(bedAboveRoad, true, false);
            TunnelLinkerBridge.TunnelLinkPlan plan = TunnelLinkerBridge.Plan(run, TunnelMode.Connected, Tunnel(),
                Cover(), Separation());

            Expect(!plan.viable, "a bore driven inside the minimum cover under water was accepted: " + plan.Describe(),
                failures);
            Expect(plan.outcome == TunnelBuildOutcome.RefusedInsufficientCover,
                "a short-cover bore under water resolved to " + plan.outcome, failures);
            Expect(!string.IsNullOrEmpty(plan.refusal), "the refusal came back without a reason in words", failures);

            CountingProvider host;
            TerrainWriteGateway gateway = ConnectedGateway(out host);
            gateway.BeginBuild("RM-98 refused bore");
            TunnelLinkerBridge.TunnelLinkResult result = TunnelLinkerBridge.Execute(gateway, plan, "rm98.gate");
            gateway.EndBuild();

            Expect(!result.executed, "a refused plan was executed anyway", failures);
            Expect(host.tunnelOpenings.Count == 0, "a refused plan still had the host open the surface", failures);
            Expect(host.notified.Count == 0, "a refused plan still told the host ground had changed", failures);
            Expect(gateway.OutstandingCount == 0, "a refused plan left a hold outstanding", failures);

            // A standalone build of the same run is NOT a refusal: the shell is placed and the surface is simply never
            // opened, which is the honest standalone answer rather than a silent failure.
            TunnelLinkerBridge.TunnelLinkPlan alone = TunnelLinkerBridge.Plan(MakeRun(bedAboveRoad, false, false),
                TunnelMode.Standalone, Tunnel(), Cover(), Separation());
            Expect(alone.outcome == TunnelBuildOutcome.LinedButNotCut,
                "a standalone tunnel resolved to " + alone.outcome + " rather than LinedButNotCut", failures);
        }
        #endregion


        #region "Case 6: the legacy terraformer agrees about how much ground sits over a tunnel"
        private static void CaseSixLegacyTerraformerAgrees(List<string> failures)
        {
            // The legacy terraformer used to raise the ground over a tunnel by three centimetres -- a rendering nudge
            // standing in for cover. It now reads the same cover chain as the policy, and this is where the two are
            // checked against each other rather than trusted to stay in step.
            double expected = TunnelTerrainPolicy.TunnelGroundTopAboveRoadMetres(false, Tunnel(), Cover(),
                Separation());
            Expect(expected > 1.0, string.Format(CultureInfo.InvariantCulture,
                "the cover chain puts only {0:0.###} m of ground over a tunnel, which is a nudge and not cover",
                expected), failures);

            RoadCityMaster.Threading.TerraformingThreaded.UseTunnelCover(Tunnel(), Cover(), Separation());
            float actual = RoadCityMaster.Threading.TerraformingThreaded.TunnelGroundTopAboveRoad;
            Expect(Mathf.Abs(actual - (float)expected) < 1e-4f, string.Format(CultureInfo.InvariantCulture,
                "the terraformer keeps {0:0.###} m of ground over a tunnel where the policy says {1:0.###} m", actual,
                expected), failures);

            // A project with its own thicker roof must move the terraformer too, or the settings are decoration.
            TunnelCoverClass thick = TunnelCoverClass.Default();
            thick.roofThicknessMetres = Cover().roofThicknessMetres + 2.0;
            RoadCityMaster.Threading.TerraformingThreaded.UseTunnelCover(Tunnel(), thick, Separation());
            float moved = RoadCityMaster.Threading.TerraformingThreaded.TunnelGroundTopAboveRoad;
            Expect(moved > actual + 1.9f, string.Format(CultureInfo.InvariantCulture,
                "thickening the roof by 2 m moved the terraformer's cover from {0:0.###} m to {1:0.###} m", actual,
                moved), failures);

            RoadCityMaster.Threading.TerraformingThreaded.UseTunnelCover(Tunnel(), Cover(), Separation());
        }
        #endregion
    }
}
#endif
