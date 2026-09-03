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
    /// <summary> RM-12's gate: proves <see cref="BuildingPlacement"/> puts a building ON THE GROUND
    /// (pivot base at the pad elevation, never floating or sunk), FACING ITS STREET (local +Z aimed
    /// along the frontage's outward normal, not a world axis), and INSIDE ITS LOT (footprint within the
    /// polygon with the zone's side clearance) -- and that it REFUSES, with a numbered reason, a
    /// building that breaks a zoning limit rather than placing it wrong. It also proves the solver is
    /// order-independent (C-06): the same requests in reverse yield byte-identical placements.
    ///
    /// The cases run on synthetic lots built in memory -- a placement is a pure function of its request,
    /// so no network, block pipeline or scene is needed, and re-deriving one here would duplicate the
    /// RM-10 gate for no gain. Report lines are prefixed RCMPLACE. </summary>
    public static class BuildingPlacementGate
    {
        [MenuItem("Window/Road City Master/Testing/Building placement")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.BuildingPlacementGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMPLACE report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMPLACE BEGIN\n");
            List<string> failures = new List<string>();

            CaseOnGroundAndFacing(builder, failures);
            CaseRefuseCoverage(builder, failures);
            CaseRefuseHeight(builder, failures);
            CaseRefuseOutsideLot(builder, failures);
            CaseOrderIndependent(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMPLACE FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMPLACE result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMPLACE END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A 10x12 m house on a 40x40 m residential lot: it must be PLACED, its base at the
        /// pad elevation (on the ground), its +Z aimed at the south street it fronts (facing the street),
        /// and its footprint inside the lot. </summary>
        private static void CaseOnGroundAndFacing(StringBuilder builder, List<string> failures)
        {
            Lot lot = SquareLot(1, 0f, 0f, 20f, ZoneParams.LowDensityResidentialId);
            const float pad = 5f;
            PlacementRequest req = Request(lot, SouthEdge(0f, 0f, 20f),
                ZoneParams.ForZone(lot.zoneId), pad, Spec(10f, 12f, 8f, "residential", 0));

            List<PlacementVerdict> verdicts;
            List<BuildingInstance> placed = BuildingPlacement.ComputePlacements(
                new List<PlacementRequest> { req }, out verdicts);

            Expect(placed.Count == 1 && verdicts[0].outcome == PlacementOutcome.Placed,
                "on-ground: the fitting house was not placed (" + verdicts[0].reason + ")", failures);
            if (placed.Count != 1) { return; }
            BuildingInstance b = placed[0];

            Expect(Approx(b.position.y, pad, 1e-4f),
                "on-ground: base Y " + b.position.y + " is not the pad elevation " + pad, failures);
            // Facing the street: +Z must point south (0,0,-1), the frontage's outward normal.
            Vector3 fwd = b.rotation * Vector3.forward;
            Expect(Vector3.Dot(fwd, new Vector3(0f, 0f, -1f)) > 0.999f,
                "facing: forward " + fwd + " does not aim at the south street", failures);
            builder.Append("RCMPLACE ").Append("on-ground+facing".PadRight(18))
                   .Append("pos ").Append(V(b.position)).Append("  fwd ").Append(V(fwd)).Append("\n");
        }

        /// <summary> A footprint that covers more of the lot than the zone allows is REFUSED, and the
        /// reason quotes the coverage numbers. </summary>
        private static void CaseRefuseCoverage(StringBuilder builder, List<string> failures)
        {
            Lot lot = SquareLot(1, 0f, 0f, 20f, ZoneParams.LowDensityResidentialId);
            PlacementRequest req = Request(lot, SouthEdge(0f, 0f, 20f),
                ZoneParams.ForZone(lot.zoneId), 0f, Spec(40f, 38f, 8f, "residential", 0));
            PlacementVerdict v = One(req);
            Expect(v.outcome == PlacementOutcome.Refused && v.reason.Contains("coverage"),
                "refuse-coverage: an oversized footprint was not refused with a coverage reason (" + v.reason + ")",
                failures);
            builder.Append("RCMPLACE ").Append("refuse-coverage".PadRight(18)).Append(v.reason).Append("\n");
        }

        /// <summary> A building taller than the zone cap is REFUSED with a height reason. </summary>
        private static void CaseRefuseHeight(StringBuilder builder, List<string> failures)
        {
            Lot lot = SquareLot(1, 0f, 0f, 20f, ZoneParams.LowDensityResidentialId);
            PlacementRequest req = Request(lot, SouthEdge(0f, 0f, 20f),
                ZoneParams.ForZone(lot.zoneId), 0f, Spec(10f, 10f, 30f, "residential", 0));
            PlacementVerdict v = One(req);
            Expect(v.outcome == PlacementOutcome.Refused && v.reason.Contains("height"),
                "refuse-height: a too-tall building was not refused with a height reason (" + v.reason + ")",
                failures);
            builder.Append("RCMPLACE ").Append("refuse-height".PadRight(18)).Append(v.reason).Append("\n");
        }

        /// <summary> A footprint deep enough to poke past the rear lot line is REFUSED for leaving the
        /// lot, not placed sticking into the neighbour. </summary>
        private static void CaseRefuseOutsideLot(StringBuilder builder, List<string> failures)
        {
            Lot lot = SquareLot(1, 0f, 0f, 20f, ZoneParams.LowDensityResidentialId);
            PlacementRequest req = Request(lot, SouthEdge(0f, 0f, 20f),
                ZoneParams.ForZone(lot.zoneId), 0f, Spec(10f, 38f, 8f, "residential", 0));
            PlacementVerdict v = One(req);
            Expect(v.outcome == PlacementOutcome.Refused && v.reason.Contains("lot"),
                "refuse-outside: a footprint leaving the lot was not refused (" + v.reason + ")", failures);
            builder.Append("RCMPLACE ").Append("refuse-outside".PadRight(18)).Append(v.reason).Append("\n");
        }

        /// <summary> Three lots placed in one order and its reverse must yield byte-identical instances
        /// (matched by the id-sorted serialisation), the determinism the whole layer rests on (C-06). </summary>
        private static void CaseOrderIndependent(StringBuilder builder, List<string> failures)
        {
            List<PlacementRequest> forward = new List<PlacementRequest>
            {
                Request(SquareLot(30, 0f, 0f, 20f, ZoneParams.LowDensityResidentialId),
                    SouthEdge(0f, 0f, 20f), ZoneParams.ForZone(1), 5f, Spec(10f, 12f, 8f, "residential", 0)),
                Request(SquareLot(10, 100f, 0f, 20f, ZoneParams.LowDensityResidentialId),
                    SouthEdge(100f, 0f, 20f), ZoneParams.ForZone(1), 6f, Spec(10f, 12f, 8f, "residential", 0)),
                Request(SquareLot(20, 200f, 0f, 20f, ZoneParams.LowDensityResidentialId),
                    SouthEdge(200f, 0f, 20f), ZoneParams.ForZone(1), 7f, Spec(10f, 12f, 8f, "residential", 0)),
            };
            List<PlacementRequest> reversed = new List<PlacementRequest> { forward[2], forward[1], forward[0] };

            List<PlacementVerdict> va, vb;
            string a = Serialize(BuildingPlacement.ComputePlacements(forward, out va));
            string b = Serialize(BuildingPlacement.ComputePlacements(reversed, out vb));
            Expect(a == b, "order-independent: placements differ between forward and reversed input", failures);
            builder.Append("RCMPLACE ").Append("order-independent".PadRight(18))
                   .Append(a == b ? "identical" : "DIFFER").Append("  placed ")
                   .Append(va.Count).Append("\n");
        }

        /// <summary> A counter-clockwise square lot centred at (cx, cz) with the given half-size, at
        /// Y=0, tagged with a zone id. </summary>
        private static Lot SquareLot(int id, float cx, float cz, float half, int zoneId)
        {
            Lot lot = new Lot();
            lot.id = id;
            lot.zoneId = zoneId;
            lot.polygon = new List<Vector3>
            {
                new Vector3(cx - half, 0f, cz - half),
                new Vector3(cx + half, 0f, cz - half),
                new Vector3(cx + half, 0f, cz + half),
                new Vector3(cx - half, 0f, cz + half),
            };
            return lot;
        }

        /// <summary> The lot's south edge (low-Z side) as the two world-space frontage points. Its
        /// outward normal points to -Z, so a building fronting it must face south. </summary>
        private static List<Vector3> SouthEdge(float cx, float cz, float half)
        {
            return new List<Vector3>
            {
                new Vector3(cx - half, 0f, cz - half),
                new Vector3(cx + half, 0f, cz - half),
            };
        }

        private static PrefabPlacementSpec Spec(float width, float depth, float height, string category, int variant)
        {
            return new PrefabPlacementSpec(category, variant, new Vector2(width, depth), height,
                GroundContactMode.OnPad);
        }

        private static PlacementRequest Request(Lot lot, List<Vector3> frontage, ZoneParams zone,
            float padLevel, PrefabPlacementSpec spec)
        {
            return new PlacementRequest(lot, frontage, zone, padLevel,
                CitySeed.ForItem(0, lot.id), spec);
        }

        /// <summary> Places one request and returns its single verdict. </summary>
        private static PlacementVerdict One(PlacementRequest req)
        {
            List<PlacementVerdict> verdicts;
            BuildingPlacement.ComputePlacements(new List<PlacementRequest> { req }, out verdicts);
            return verdicts[0];
        }

        /// <summary> A canonical, culture-invariant dump of the placed instances, so two runs can be
        /// compared byte for byte. The solver already returns them in lot-id order. </summary>
        private static string Serialize(List<BuildingInstance> placed)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < placed.Count; i++)
            {
                BuildingInstance b = placed[i];
                sb.Append(b.lotId).Append('|').Append(b.prefabCategory).Append('|').Append(b.variantIndex)
                  .Append('|').Append(V(b.position)).Append('|')
                  .Append(F(b.rotation.x)).Append(',').Append(F(b.rotation.y)).Append(',')
                  .Append(F(b.rotation.z)).Append(',').Append(F(b.rotation.w))
                  .Append('|').Append(V(b.scale)).Append('\n');
            }
            return sb.ToString();
        }

        private static string F(float f)
        {
            return f.ToString("R", CultureInfo.InvariantCulture);
        }

        private static string V(Vector3 v)
        {
            return "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")";
        }

        private static bool Approx(float a, float b, float eps)
        {
            return Mathf.Abs(a - b) <= eps;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }
    }
}
#endif
