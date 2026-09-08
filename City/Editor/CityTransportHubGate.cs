// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
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
    /// <summary> RM-43's gate: proves the hub-to-surroundings contract without a scene. It asserts that a rail station
    /// with no road of the required class within its access distance says so by name; that a port not adjacent to
    /// navigable water (or over water too shallow) says so; that an airport whose approach corridor is obstructed says
    /// so, NAMING the obstruction's position and penetration; that the approach surface geometry follows the adopted
    /// standard (diverging half-width, sloped height, footprint test); that no hub silently accepts an impossible
    /// site; that a multi-failure report lists its unmet requirements in a fixed order; and that the report is
    /// bit-identical on repeat (C-06). The prefab hub building and the scene sampling that fills the facts are the
    /// deferred half. Report lines are prefixed RCMHUB. </summary>
    public static class CityTransportHubGate
    {
        [MenuItem("Window/Road City Master/Testing/City transport hubs (connection contract)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityTransportHubGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMHUB report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMHUB BEGIN\n");
            List<string> failures = new List<string>();

            CaseRailStationNeedsRoad(builder, failures);
            CasePortNeedsNavigableWater(builder, failures);
            CaseAirportApproachObstructed(builder, failures);
            CaseApproachSurfaceGeometryFromStandard(builder, failures);
            CaseNoSilentAccept(builder, failures);
            CaseReportStableOrder(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMHUB FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMHUB result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMHUB END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A rail station with no road of the required class within its access distance says so by name; a
        /// station with the road, forecourt and no other unmet requirement is accepted. </summary>
        private static void CaseRailStationNeedsRoad(StringBuilder builder, List<string> failures)
        {
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.RailStation);

            HubSiteFacts noRoad = new HubSiteFacts();
            noRoad.hasRequiredRoadWithinAccess = false;
            noRoad.forecourtDepthAvailableMetres = 100.0;
            HubValidationResult r1 = TransportHub.Validate(req, noRoad);
            Expect(!r1.accepted, "rail: a station with no required road was accepted", failures);
            Expect(NamesRoadClass(r1, req.requiredRoadCategory),
                "rail: the report did not name the missing road class", failures);

            HubSiteFacts good = new HubSiteFacts();
            good.hasRequiredRoadWithinAccess = true;
            good.forecourtDepthAvailableMetres = 100.0;
            HubValidationResult r2 = TransportHub.Validate(req, good);
            Expect(r2.accepted && r2.unmetRequirements.Count == 0,
                "rail: a station with road and forecourt was not accepted", failures);

            builder.Append("RCMHUB ").Append("rail-road".PadRight(22))
                   .Append("missing road class named; complete site accepted\n");
        }

        /// <summary> A port not adjacent to navigable water says so; over water too shallow it names the depth; on
        /// deep enough water with road and forecourt it is accepted. </summary>
        private static void CasePortNeedsNavigableWater(StringBuilder builder, List<string> failures)
        {
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.Port);

            HubSiteFacts dry = new HubSiteFacts();
            dry.hasRequiredRoadWithinAccess = true;
            dry.forecourtDepthAvailableMetres = 100.0;
            dry.adjacentToNavigableWater = false;
            HubValidationResult r1 = TransportHub.Validate(req, dry);
            Expect(!r1.accepted && Contains(r1, "navigable water"),
                "port: a port not adjacent to navigable water was accepted or not named", failures);

            HubSiteFacts shallow = new HubSiteFacts();
            shallow.hasRequiredRoadWithinAccess = true;
            shallow.forecourtDepthAvailableMetres = 100.0;
            shallow.adjacentToNavigableWater = true;
            shallow.waterDepthMetres = 4.0; // below the 10 m the vessels imply
            HubValidationResult r2 = TransportHub.Validate(req, shallow);
            Expect(!r2.accepted && Contains(r2, "water depth"),
                "port: a port over water too shallow was accepted or the depth not named", failures);

            HubSiteFacts good = new HubSiteFacts();
            good.hasRequiredRoadWithinAccess = true;
            good.forecourtDepthAvailableMetres = 100.0;
            good.adjacentToNavigableWater = true;
            good.waterDepthMetres = 12.0;
            HubValidationResult r3 = TransportHub.Validate(req, good);
            Expect(r3.accepted, "port: a port on deep enough water with road and forecourt was not accepted", failures);

            builder.Append("RCMHUB ").Append("port-water".PadRight(22))
                   .Append("dry / shallow named; deep water site accepted\n");
        }

        /// <summary> An airport whose approach corridor is obstructed says so, naming the obstruction; a clear site is
        /// accepted. The obstruction facts come from sampling the approach surface built from the standard. </summary>
        private static void CaseAirportApproachObstructed(StringBuilder builder, List<string> failures)
        {
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.Airport);
            ApproachSurfaceClass surf = ApproachSurfaceClass.Default();

            // A hill on the extended centreline, 1000 m from the runway end, rising 60 m above datum.
            // Surface height there is slope * (1000 - 60) = 0.02 * 940 = 18.8 m, so it penetrates by 41.2 m.
            ApproachPenetration hill = TransportHub.EvaluateApproachObstacle(1000.0, 0.0, 60.0, surf);
            Expect(hill.penetrates, "airport: a 60 m hill in the corridor did not register as a penetration", failures);
            Expect(Math.Abs(hill.surfaceHeightMetres - 18.8) < 1e-6,
                "airport: the approach surface height at 1000 m was not 18.8 m", failures);
            Expect(Math.Abs(hill.penetrationMetres - 41.2) < 1e-6,
                "airport: the penetration depth was not 41.2 m", failures);

            HubSiteFacts obstructed = new HubSiteFacts();
            obstructed.hasRequiredRoadWithinAccess = true;
            obstructed.forecourtDepthAvailableMetres = 200.0;
            obstructed.approachObstructionCount = 1;
            obstructed.worstPenetrationMetres = hill.penetrationMetres;
            HubValidationResult r1 = TransportHub.Validate(req, obstructed);
            Expect(!r1.accepted && Contains(r1, "approach surface obstructed"),
                "airport: an obstructed approach was accepted or not named", failures);

            HubSiteFacts clear = new HubSiteFacts();
            clear.hasRequiredRoadWithinAccess = true;
            clear.forecourtDepthAvailableMetres = 200.0;
            clear.approachObstructionCount = 0;
            HubValidationResult r2 = TransportHub.Validate(req, clear);
            Expect(r2.accepted, "airport: a clear, well-connected airport site was not accepted", failures);

            builder.Append("RCMHUB ").Append("airport-approach".PadRight(22))
                   .Append("corridor obstruction named; clear site accepted\n");
        }

        /// <summary> The approach surface geometry follows the standard: the half-width diverges from the inner edge,
        /// the height is the slope over the run, a point beyond the footprint is not under the surface, and a low
        /// obstacle below the surface does not penetrate. </summary>
        private static void CaseApproachSurfaceGeometryFromStandard(StringBuilder builder, List<string> failures)
        {
            ApproachSurfaceClass surf = ApproachSurfaceClass.Default(); // inner half 75, start 60, div 0.15, slope 0.02, len 3000

            // At the inner edge (60 m from the runway end) the half-width is the authored inner half-width.
            Expect(Math.Abs(TransportHub.ApproachHalfWidthAt(60.0, surf) - 75.0) < 1e-6,
                "geometry: the inner-edge half-width was not the authored 75 m", failures);
            // 940 m into the surface (1000 m from the end) it has diverged by 0.15 * 940 = 141 m -> 216 m half-width.
            Expect(Math.Abs(TransportHub.ApproachHalfWidthAt(1000.0, surf) - 216.0) < 1e-6,
                "geometry: the half-width did not diverge at the authored rate", failures);
            // Before the inner edge and beyond the far end the point is not under the surface.
            Expect(TransportHub.ApproachHalfWidthAt(30.0, surf) == 0.0
                   && TransportHub.ApproachHalfWidthAt(4000.0, surf) == 0.0,
                "geometry: the surface footprint extended outside its length", failures);
            // Height is slope * run from the inner edge.
            Expect(Math.Abs(TransportHub.ApproachSurfaceHeightAt(1000.0, surf) - 18.8) < 1e-6,
                "geometry: the surface height did not follow slope * run", failures);
            // A point laterally outside the footprint does not penetrate however tall.
            ApproachPenetration wide = TransportHub.EvaluateApproachObstacle(1000.0, 400.0, 999.0, surf);
            Expect(!wide.penetrates, "geometry: an obstacle outside the footprint wrongly penetrated", failures);
            // A low obstacle under the surface does not penetrate.
            ApproachPenetration low = TransportHub.EvaluateApproachObstacle(1000.0, 0.0, 10.0, surf);
            Expect(!low.penetrates, "geometry: an obstacle below the surface wrongly penetrated", failures);

            builder.Append("RCMHUB ").Append("approach-geometry".PadRight(22))
                   .Append("diverging half-width, sloped height, footprint test\n");
        }

        /// <summary> No hub silently accepts an impossible site: any unmet requirement forces acceptance false and a
        /// non-empty report. </summary>
        private static void CaseNoSilentAccept(StringBuilder builder, List<string> failures)
        {
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.BusStation);
            HubSiteFacts impossible = new HubSiteFacts(); // nothing provided
            HubValidationResult r = TransportHub.Validate(req, impossible);
            Expect(!r.accepted && r.unmetRequirements.Count > 0,
                "silent: an impossible bus-station site was accepted or reported nothing unmet", failures);
            builder.Append("RCMHUB ").Append("no-silent-accept".PadRight(22))
                   .Append("an impossible site is never silently accepted\n");
        }

        /// <summary> A site failing several requirements lists them in a fixed order: road, then forecourt, then
        /// water, then approach -- so the report diffs stably between sessions. </summary>
        private static void CaseReportStableOrder(StringBuilder builder, List<string> failures)
        {
            // A port that fails road, forecourt and water at once.
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.Port);
            HubSiteFacts bad = new HubSiteFacts();
            bad.hasRequiredRoadWithinAccess = false;
            bad.forecourtDepthAvailableMetres = 0.0;
            bad.adjacentToNavigableWater = false;
            HubValidationResult r = TransportHub.Validate(req, bad);
            Expect(r.unmetRequirements.Count == 3, "order: the port did not report all three unmet requirements", failures);
            if (r.unmetRequirements.Count == 3)
            {
                Expect(r.unmetRequirements[0].Contains("road")
                       && r.unmetRequirements[1].Contains("forecourt")
                       && r.unmetRequirements[2].Contains("navigable water"),
                    "order: the unmet requirements were not in the fixed road/forecourt/water order", failures);
            }
            builder.Append("RCMHUB ").Append("stable-order".PadRight(22))
                   .Append("multi-failure report in a fixed, diffable order\n");
        }

        /// <summary> The report is bit-identical on repeat for identical facts -- pure logic (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            HubRequirementsClass req = HubRequirementsClass.For(HubKind.Port);
            HubSiteFacts facts = new HubSiteFacts();
            facts.hasRequiredRoadWithinAccess = true;
            facts.forecourtDepthAvailableMetres = 10.0; // below 60
            facts.adjacentToNavigableWater = true;
            facts.waterDepthMetres = 3.0; // below 10
            HubValidationResult a = TransportHub.Validate(req, facts);
            HubValidationResult b = TransportHub.Validate(req, facts);
            bool same = a.accepted == b.accepted && a.unmetRequirements.Count == b.unmetRequirements.Count;
            if (same)
            {
                for (int i = 0; i < a.unmetRequirements.Count; i++)
                {
                    if (a.unmetRequirements[i] != b.unmetRequirements[i]) { same = false; }
                }
            }
            Expect(same, "determinism: two validations of identical facts produced different reports", failures);
            builder.Append("RCMHUB ").Append("determinism".PadRight(22))
                   .Append("identical facts -> identical report\n");
        }

        /// <summary> Whether the report names the missing road class by its category key. </summary>
        private static bool NamesRoadClass(HubValidationResult r, string roadCategory)
        {
            for (int i = 0; i < r.unmetRequirements.Count; i++)
            {
                if (r.unmetRequirements[i].Contains(roadCategory)) { return true; }
            }
            return false;
        }

        /// <summary> Whether any unmet-requirement line contains the given text. </summary>
        private static bool Contains(HubValidationResult r, string text)
        {
            for (int i = 0; i < r.unmetRequirements.Count; i++)
            {
                if (r.unmetRequirements[i].Contains(text)) { return true; }
            }
            return false;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
