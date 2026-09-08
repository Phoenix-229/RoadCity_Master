// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What guided placement did, and why. </summary>
    public enum GuidanceOutcome
    {
        /// <summary> Guided placement is switched off. The node is exactly where the author put it. </summary>
        Disabled = 0,

        /// <summary> There is no suitability map or no solved corridor to be guided by. </summary>
        NoMap = 1,

        /// <summary> The node is outside the corridor band and nothing was near enough to snap to. </summary>
        OutsideCorridor = 2,

        /// <summary> The node is inside the band already, and was left alone. </summary>
        InsideCorridor = 3,

        /// <summary> The node was pulled onto the cheapest line through the corridor. </summary>
        MovedToCheapest = 4
    }


    /// <summary> The suggestion guided placement makes for one dragged node. </summary>
    public struct GuidanceResult
    {
        public GuidanceOutcome outcome;

        /// <summary> Where the node should sit. Equal to the input position unless the outcome is
        /// <see cref="GuidanceOutcome.MovedToCheapest"/> -- a guidance layer that quietly nudged a node it had decided
        /// not to snap would be indistinguishable from a bug. </summary>
        public double xMetres;
        public double zMetres;

        /// <summary> How far the suggestion moved the node, in metres; zero when it did not. </summary>
        public double movedMetres;

        /// <summary> Distance from the input position to the nearest cell of the cheapest line, in metres, or positive
        /// infinity when there is no such line. Reported even when no snap happened, because "38 m away" is what tells
        /// an author the corridor is over there rather than absent. </summary>
        public double metresToCheapestLine;

        /// <summary> What a metre of road costs where the node ended up, in multiples of the cheapest ground. </summary>
        public double relativeCost;

        /// <summary> One sentence, always present, always naming the reason. </summary>
        public string sentence;
    }


    /// <summary> The layer that joins RM-75's node editing to RM-74's cost surface -- OPT-IN, and inert when off.
    ///
    /// This is the only place in the alignment work that reads a suitability map, and it reads it to make a SUGGESTION.
    /// It moves nothing itself: it returns a position and a sentence, and the editor code that owns the drag decides
    /// whether to use them inside its own undo step. That separation is what lets a headless gate prove the two
    /// behaviours that matter -- that a switched-off guidance returns the author's own position untouched, and that a
    /// switched-on one snaps only within the tolerance it was given.
    ///
    /// The snap target is the CHEAPEST LINE, not the nearest point of the band. A corridor band is wide by design (it is
    /// the set of routes within a tolerance of the optimum), so snapping to its edge would attach the road to the most
    /// expensive ground that still counts as good. The least-cost path through it is the one line the solver can defend.
    /// </summary>
    public static class CorridorGuidance
    {
        /// <summary> Suggest a position for a node being dragged to (x, z).
        ///
        /// The plan supplies both the switch and the tolerance, so a project that has never turned guidance on cannot be
        /// affected by a change to this file. A null plan is treated as switched off, which is the safe direction: the
        /// author's hand wins. </summary>
        public static GuidanceResult Suggest(HorizontalAlignment plan, SuitabilityRasterClass map, CorridorPlan corridor,
            double xMetres, double zMetres)
        {
            GuidanceResult r = new GuidanceResult();
            r.xMetres = xMetres;
            r.zMetres = zMetres;
            r.metresToCheapestLine = double.PositiveInfinity;
            r.relativeCost = 1.0;

            if (plan == null || !plan.guidedPlacementEnabled)
            {
                r.outcome = GuidanceOutcome.Disabled;
                r.sentence = "guided placement is off, so the node is exactly where you put it";
                return r;
            }

            if (map == null || SuitabilityRaster.CellCount(map) <= 0 || corridor == null || !corridor.reached
                || corridor.pathCells == null || corridor.pathCells.Count == 0)
            {
                r.outcome = GuidanceOutcome.NoMap;
                r.sentence = "guided placement is on, but there is no solved corridor to be guided by yet";
                return r;
            }

            r.relativeCost = RelativeCostAt(map, xMetres, zMetres);
            int nearest = NearestPathCell(map, corridor, xMetres, zMetres, out r.metresToCheapestLine);
            bool insideBand = InsideBand(map, corridor, xMetres, zMetres);
            double tolerance = plan.guidedSnapToleranceMetres > 0f ? plan.guidedSnapToleranceMetres : 0.0;

            if (nearest >= 0 && r.metresToCheapestLine <= tolerance)
            {
                return Snap(map, corridor, r, nearest, xMetres, zMetres);
            }
            return NoSnap(r, insideBand, tolerance);
        }

        /// <summary> The cell of the cheapest line nearest a point, by index, or -1. Distances are measured to cell
        /// CENTRES, the same points RM-74 measured its costs at, and ties are broken by the path's own order so the
        /// answer never depends on how the loop was written (C-06). </summary>
        public static int NearestPathCell(SuitabilityRasterClass map, CorridorPlan corridor,
            double xMetres, double zMetres, out double metres)
        {
            metres = double.PositiveInfinity;
            int best = -1;
            if (map == null || corridor == null || corridor.pathCells == null || map.widthCells <= 0) { return -1; }

            for (int i = 0; i < corridor.pathCells.Count; i++)
            {
                int index = corridor.pathCells[i];
                if (index < 0) { continue; }
                int cx = index % map.widthCells;
                int cz = index / map.widthCells;
                double dx = SuitabilityRaster.CellCentreXMetres(map, cx) - xMetres;
                double dz = SuitabilityRaster.CellCentreZMetres(map, cz) - zMetres;
                double distance = Math.Sqrt(dx * dx + dz * dz);
                if (distance < metres)
                {
                    metres = distance;
                    best = index;
                }
            }
            return best;
        }

        /// <summary> Whether a world point stands on a cell the corridor band admits. </summary>
        public static bool InsideBand(SuitabilityRasterClass map, CorridorPlan corridor,
            double xMetres, double zMetres)
        {
            if (map == null || corridor == null || corridor.inCorridor == null) { return false; }
            int cx;
            int cz;
            if (!SuitabilityRaster.CellAt(map, xMetres, zMetres, out cx, out cz)) { return false; }
            int index = SuitabilityRaster.Index(map, cx, cz);
            return index >= 0 && index < corridor.inCorridor.Length && corridor.inCorridor[index];
        }

        /// <summary> What a metre of road costs at a point, in multiples of the cheapest ground -- RM-74's own scale, so
        /// the number a snap quotes is the number the overlay is showing. </summary>
        private static double RelativeCostAt(SuitabilityRasterClass map, double xMetres, double zMetres)
        {
            return SuitabilityFactors.CellCost(SuitabilityRaster.CostAt(map, xMetres, zMetres),
                SuitabilityRaster.ForbiddenAt(map, xMetres, zMetres));
        }

        private static int CellIndexAt(SuitabilityRasterClass map, double xMetres, double zMetres)
        {
            int cx;
            int cz;
            if (!SuitabilityRaster.CellAt(map, xMetres, zMetres, out cx, out cz)) { return -1; }
            return SuitabilityRaster.Index(map, cx, cz);
        }

        /// <summary> Pull the node onto the cheapest line -- unless it is standing on it already, in which case there is
        /// nothing to pull and saying so is more useful than a zero-length move. </summary>
        private static GuidanceResult Snap(SuitabilityRasterClass map, CorridorPlan corridor, GuidanceResult r,
            int targetIndex, double xMetres, double zMetres)
        {
            int own = CellIndexAt(map, xMetres, zMetres);
            if (own == targetIndex)
            {
                r.outcome = GuidanceOutcome.InsideCorridor;
                r.sentence = "already on the cheapest line through the corridor, where a metre costs "
                    + Number(r.relativeCost) + " metres of the easiest ground";
                return r;
            }

            int cx = targetIndex % map.widthCells;
            int cz = targetIndex / map.widthCells;
            double tx = SuitabilityRaster.CellCentreXMetres(map, cx);
            double tz = SuitabilityRaster.CellCentreZMetres(map, cz);

            r.outcome = GuidanceOutcome.MovedToCheapest;
            r.xMetres = tx;
            r.zMetres = tz;
            r.movedMetres = r.metresToCheapestLine;
            r.relativeCost = RelativeCostAt(map, tx, tz);
            r.sentence = "moved " + Number(r.movedMetres)
                + " m onto the cheapest line through the corridor, where a metre costs " + Number(r.relativeCost)
                + " metres of the easiest ground";
            return r;
        }

        /// <summary> Leave the node alone, and say which of the two reasons it was: inside the band but too far from its
        /// cheapest line, or outside the band altogether. Naming which one is the whole value of the readout -- a node
        /// 40 m from a corridor and a node 40 m from a corridor's best line need different decisions from the author.
        /// </summary>
        private static GuidanceResult NoSnap(GuidanceResult r, bool insideBand, double toleranceMetres)
        {
            string distance = double.IsInfinity(r.metresToCheapestLine)
                ? "no cheapest line was found"
                : Number(r.metresToCheapestLine) + " m from the cheapest line, further than the "
                    + Number(toleranceMetres) + " m snap";

            if (insideBand)
            {
                r.outcome = GuidanceOutcome.InsideCorridor;
                r.sentence = "inside the corridor, " + distance + ", so the node was left where you put it";
                return r;
            }

            r.outcome = GuidanceOutcome.OutsideCorridor;
            r.sentence = "outside the corridor, " + distance + ", so the node was left where you put it";
            return r;
        }

        private static string Number(double value)
        {
            if (double.IsInfinity(value)) { return "no limit"; }
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
