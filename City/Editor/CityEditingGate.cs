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
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-16's gate: proves the genuinely NEW editor LOGIC -- hierarchical selection and the
    /// per-element override edit path -- without a scene. The scene-view <c>Handles</c>, the city window and
    /// the inspector GUI are the author's scene check (they cannot be asserted headlessly); what CAN be
    /// asserted, and is where the bugs live, is that a click selects the BROADEST element first and DESCENDS
    /// on repeat (block -> lot -> corner -> node -> wrap), that a click on a new target RESETS to broadest,
    /// that picking is deterministic and order-independent (lowest id wins an overlap, C-06), and -- the
    /// load-bearing rule -- that an author edit sets isOverridden so <see cref="CityData.ClearDerived"/>
    /// (RM-20's re-bake) PRESERVES it, while a revert lets the re-bake recompute it. Report lines are
    /// prefixed RCMEDIT. </summary>
    public static class CityEditingGate
    {
        // Test city: one 100x100 block (id 10) enclosing two 30x30 lots (A id 100, B id 200), plus two
        // network nodes. One node sits on lot A's far corner (40,40) so a single click near it can reach
        // every level of the hierarchy in one descend cycle.
        private const float PickRadius = 5f;

        [MenuItem("Window/Road City Master/Testing/City editing selection")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityEditingGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMEDIT report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMEDIT BEGIN\n");
            List<string> failures = new List<string>();

            CaseBroadestThenDescend(builder, failures);
            CaseResetOnNewTarget(builder, failures);
            CaseCornerPick(builder, failures);
            CaseOverrideSurvivesRebake(builder, failures);
            CaseMoveCorner(builder, failures);
            CaseOrderIndependent(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMEDIT FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMEDIT result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMEDIT END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> A click near lot A's far corner reaches every level. The first click selects the
        /// broadest (the block); each repeat at the same point descends -- lot, corner, node -- and the
        /// fourth repeat wraps back to the block. </summary>
        private static void CaseBroadestThenDescend(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            Vector2 click = new Vector2(38f, 38f); // inside block, inside lot A, near corner (40,40) and node 1

            CitySelection s0 = CitySelectionResolver.ResolveClick(data, click, CitySelection.None, PickRadius);
            CitySelection s1 = CitySelectionResolver.ResolveClick(data, click, s0, PickRadius);
            CitySelection s2 = CitySelectionResolver.ResolveClick(data, click, s1, PickRadius);
            CitySelection s3 = CitySelectionResolver.ResolveClick(data, click, s2, PickRadius);
            CitySelection s4 = CitySelectionResolver.ResolveClick(data, click, s3, PickRadius);

            Expect(s0.Equals(CitySelection.ForBlock(10)),
                "descend: first click did not select the broadest (block), got " + Show(s0), failures);
            Expect(s1.Equals(CitySelection.ForLot(100)),
                "descend: second click did not descend to the lot, got " + Show(s1), failures);
            Expect(s2.kind == CityElementKind.LotCorner && s2.primaryId == 100 && s2.cornerIndex == 2,
                "descend: third click did not descend to the lot corner, got " + Show(s2), failures);
            Expect(s3.Equals(CitySelection.ForNode(1)),
                "descend: fourth click did not descend to the network node, got " + Show(s3), failures);
            Expect(s4.Equals(CitySelection.ForBlock(10)),
                "descend: fifth click did not wrap back to the block, got " + Show(s4), failures);

            builder.Append("RCMEDIT ").Append("descend".PadRight(20))
                   .Append(Show(s0)).Append(" -> ").Append(Show(s1)).Append(" -> ").Append(Show(s2))
                   .Append(" -> ").Append(Show(s3)).Append(" -> ").Append(Show(s4)).Append("\n");
        }

        /// <summary> Clicking a DIFFERENT target resets to that target's broadest element rather than
        /// descending: with lot A selected, a click inside lot B selects the block again, not lot B's
        /// interior levels. </summary>
        private static void CaseResetOnNewTarget(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            CitySelection lotA = CitySelection.ForLot(100);
            Vector2 clickInB = new Vector2(75f, 25f); // inside block and lot B, no corner/node within radius

            CitySelection next = CitySelectionResolver.ResolveClick(data, clickInB, lotA, PickRadius);
            Expect(next.Equals(CitySelection.ForBlock(10)),
                "reset: a click on a new target did not reset to the broadest, got " + Show(next), failures);
            builder.Append("RCMEDIT ").Append("reset".PadRight(20))
                   .Append("lot A + click in B -> ").Append(Show(next)).Append("\n");
        }

        /// <summary> The corner pick returns the specific nearest corner: a click by lot A's (10,10) corner
        /// picks corner index 0, not the far corner. </summary>
        private static void CaseCornerPick(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            CitySelection corner = CitySelectionResolver.HitLotCorner(data, new Vector2(11f, 11f), PickRadius);
            Expect(corner.kind == CityElementKind.LotCorner && corner.primaryId == 100 && corner.cornerIndex == 0,
                "corner: nearest corner to (11,11) was not lot 100 corner 0, got " + Show(corner), failures);
            builder.Append("RCMEDIT ").Append("corner-pick".PadRight(20))
                   .Append("(11,11) -> ").Append(Show(corner)).Append("\n");
        }

        /// <summary> The load-bearing rule: an author override survives a re-bake. Marking lot A overridden
        /// makes <see cref="CityData.ClearDerived"/> keep it while dropping the derived lot B; reverting lot
        /// A then lets ClearDerived drop it too. </summary>
        private static void CaseOverrideSurvivesRebake(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            bool marked = CityEditOps.MarkOverridden(data, CitySelection.ForLot(100));
            Expect(marked && CityEditOps.IsOverridden(data, CitySelection.ForLot(100)),
                "override: MarkOverridden did not flag lot 100", failures);
            Expect(!CityEditOps.MarkOverridden(data, CitySelection.ForNode(1)),
                "override: a network node was wrongly treated as overridable", failures);

            data.ClearDerived();
            Expect(FindsLot(data, 100) && !FindsLot(data, 200),
                "override: re-bake did not preserve the overridden lot / drop the derived one", failures);

            // Reverting the override lets the next re-bake recompute it.
            CityData data2 = BuildCity();
            CityEditOps.MarkOverridden(data2, CitySelection.ForLot(100));
            CityEditOps.Revert(data2, CitySelection.ForLot(100));
            data2.ClearDerived();
            Expect(!FindsLot(data2, 100),
                "override: a reverted lot was not recomputed (dropped) by the re-bake", failures);

            builder.Append("RCMEDIT ").Append("override-survives".PadRight(20))
                   .Append("kept overridden lot, dropped derived; revert -> recomputed\n");
        }

        /// <summary> A corner drag moves exactly that corner, keeps its height, and marks the lot overridden
        /// so the edit is saved rather than re-derived. </summary>
        private static void CaseMoveCorner(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            CitySelection sel = CitySelection.ForLotCorner(100, 2);
            Lot lot = CityEditOps.FindLot(data, 100);
            float keptY = lot.polygon[2].y;

            bool moved = CityEditOps.MoveLotCorner(data, sel, new Vector2(42f, 43f));
            Expect(moved, "move-corner: MoveLotCorner returned false for a valid corner", failures);
            Expect(Approx(lot.polygon[2].x, 42f) && Approx(lot.polygon[2].z, 43f) && Approx(lot.polygon[2].y, keptY),
                "move-corner: the corner did not move to (42,43) at its kept height", failures);
            Expect(Approx(lot.polygon[0].x, 10f) && Approx(lot.polygon[0].z, 10f),
                "move-corner: a neighbouring corner moved (only the dragged one may change)", failures);
            Expect(lot.isOverridden, "move-corner: the edited lot was not marked overridden", failures);
            Expect(!CityEditOps.MoveLotCorner(data, CitySelection.ForLotCorner(100, 99), Vector2.zero),
                "move-corner: an out-of-range corner index was not rejected", failures);
            builder.Append("RCMEDIT ").Append("move-corner".PadRight(20))
                   .Append("corner 2 -> (42,43), lot overridden, neighbours held\n");
        }

        /// <summary> Picking is order-independent: with two blocks overlapping the click, the lowest id wins
        /// regardless of the order the blocks sit in the list. </summary>
        private static void CaseOrderIndependent(StringBuilder builder, List<string> failures)
        {
            CityData ascending = BuildCity();
            ascending.blocks.Add(Square(11, 0f, 0f, 100f, 100f)); // second overlapping block, higher id

            CityData descending = BuildCity();
            descending.blocks.Insert(0, Square(11, 0f, 0f, 100f, 100f)); // same block, but first in the list

            CitySelection a = CitySelectionResolver.HitBlock(ascending, new Vector2(38f, 38f));
            CitySelection d = CitySelectionResolver.HitBlock(descending, new Vector2(38f, 38f));
            Expect(a.Equals(CitySelection.ForBlock(10)) && d.Equals(CitySelection.ForBlock(10)),
                "order: overlapping-block pick was not the lowest id in both list orders", failures);
            builder.Append("RCMEDIT ").Append("order-independent".PadRight(20))
                   .Append("lowest id 10 wins both orders\n");
        }

        /// <summary> The resolver is deterministic: the same click resolved twice yields the same selection
        /// (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            CityData data = BuildCity();
            Vector2 click = new Vector2(38f, 38f);
            CitySelection a = CitySelectionResolver.ResolveClick(data, click, CitySelection.ForBlock(10), PickRadius);
            CitySelection b = CitySelectionResolver.ResolveClick(data, click, CitySelection.ForBlock(10), PickRadius);
            Expect(a.Equals(b), "deterministic: two identical resolves differed", failures);
            builder.Append("RCMEDIT ").Append("deterministic".PadRight(20))
                   .Append(a.Equals(b) ? "identical" : "DIFFER").Append("\n");
        }

        /// <summary> The in-memory test city: one 100x100 block (id 10) with two 30x30 lots (A id 100 at
        /// [10..40], B id 200 at [60..90]) and two nodes, node 1 sitting on lot A's far corner (40,40). Lot
        /// polygons carry a non-zero height so a corner move can be checked to preserve it. </summary>
        private static CityData BuildCity()
        {
            CityData data = ScriptableObject.CreateInstance<CityData>();
            data.blocks.Add(Square(10, 0f, 0f, 100f, 100f));
            data.lots.Add(LotSquare(100, 10, 10f, 10f, 40f, 40f));
            data.lots.Add(LotSquare(200, 10, 60f, 10f, 90f, 40f));
            data.nodes.Add(new NetworkNode { position = new Vector3(5f, 0f, 5f) });   // index 0, far away
            data.nodes.Add(new NetworkNode { position = new Vector3(40f, 0f, 40f) });  // index 1, on lot A corner
            return data;
        }

        /// <summary> A block whose inset polygon is the axis-aligned rectangle, wound CCW as
        /// (min,min),(max,min),(max,max),(min,max) so corner index 2 is (maxX,maxZ). </summary>
        private static Block Square(int id, float minX, float minZ, float maxX, float maxZ)
        {
            Block b = new Block { id = id, isOverridden = false };
            b.polygon.Add(new Vector3(minX, 0f, minZ));
            b.polygon.Add(new Vector3(maxX, 0f, minZ));
            b.polygon.Add(new Vector3(maxX, 0f, maxZ));
            b.polygon.Add(new Vector3(minX, 0f, maxZ));
            return b;
        }

        /// <summary> A lot rectangle at height 3 m (so a corner move can be checked to keep its y), same
        /// corner order as <see cref="Square"/>. </summary>
        private static Lot LotSquare(int id, int blockId, float minX, float minZ, float maxX, float maxZ)
        {
            Lot l = new Lot { id = id, blockId = blockId, isOverridden = false };
            l.polygon.Add(new Vector3(minX, 3f, minZ));
            l.polygon.Add(new Vector3(maxX, 3f, minZ));
            l.polygon.Add(new Vector3(maxX, 3f, maxZ));
            l.polygon.Add(new Vector3(minX, 3f, maxZ));
            return l;
        }

        private static bool FindsLot(CityData data, int id)
        {
            return CityEditOps.FindLot(data, id) != null;
        }

        /// <summary> A short, culture-invariant rendering of a selection for the report and failure lines. </summary>
        private static string Show(CitySelection s)
        {
            switch (s.kind)
            {
                case CityElementKind.Block: return "Block(" + s.primaryId + ")";
                case CityElementKind.Lot: return "Lot(" + s.primaryId + ")";
                case CityElementKind.LotCorner: return "Corner(" + s.primaryId + "," + s.cornerIndex + ")";
                case CityElementKind.NetworkNode: return "Node(" + s.primaryId + ")";
                default: return "None";
            }
        }

        private static bool Approx(float a, float b)
        {
            return Mathf.Abs(a - b) <= 1e-3f;
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }



    }
}
#endif
