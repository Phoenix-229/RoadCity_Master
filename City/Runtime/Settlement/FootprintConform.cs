#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Why a painted cell was removed (or that it survived) during the RM-46 conform stage. Ordered so a
    /// cell that is both under water and steep reports the more fundamental reason (water) first. </summary>
    public enum ConformRemoval
    {
        Survives = 0,
        Water = 1,
        ShorelineSetback = 2,
        SteepSlope = 3
    }

    /// <summary> One painted cell handed to the conform stage: the zone painted there and the world facts a scene
    /// samples for it (whether it is water, how far it is from the nearest water edge, and its slope). In the scene
    /// half these come from the water bodies and the terrain / connected provider's SampleNormal; the conform logic
    /// is scene-free so it is testable. </summary>
    public struct ConformCell
    {
        public ZoneTypeClass zone;
        public bool isWater;
        public double distanceToWaterMetres;
        public double slopeDegrees;
    }

    /// <summary> The conform report: the painted area, the area removed and to what, as both absolute figures and a
    /// fraction -- because a footprint that loses forty per cent of itself to water is almost certainly a painting
    /// mistake and the number is the fastest way to notice. Deterministic and ordered (C-06) so RM-70 can assert. </summary>
    public struct ConformReport
    {
        public double paintedAreaM2;
        public double survivingAreaM2;
        public double removedAreaM2;
        public double removedFraction;
        public double removedToWaterM2;
        public double removedToSetbackM2;
        public double removedToSlopeM2;
    }

    /// <summary> RM-46's verifiable core: conform a painted footprint to water and slope BEFORE anything is built.
    /// Water cells produce no blocks; cells within a zone's authored shoreline setback of a water edge are removed
    /// (so the outermost building does not stand with its foundation in the water); ground steeper than a zone's
    /// authored slope limit is removed (so a lot does not become a terrace, the artefact RM-38 fixes on roads); and
    /// the removed area is reported rather than vanishing silently. The port zone is the stated exception -- a zero
    /// setback keeps its water edge, expressed as DATA not a code branch. The EXACT polygon clip against the coast
    /// (Q-11's offset/boolean machinery) is deferred; this is the per-cell conform and its report. </summary>
    public static class FootprintConform
    {
        /// <summary> Classify one painted cell: water first, then shoreline setback (skipped when the zone's setback
        /// is zero or negative -- the port exception), then the slope limit. </summary>
        public static ConformRemoval ClassifyCell(ConformCell cell)
        {
            if (cell.zone == null) { return ConformRemoval.Survives; }
            if (cell.isWater) { return ConformRemoval.Water; }
            if (cell.zone.shorelineSetbackMetres > 0f
                && cell.distanceToWaterMetres < cell.zone.shorelineSetbackMetres)
            {
                return ConformRemoval.ShorelineSetback;
            }
            if (cell.slopeDegrees > cell.zone.maxSlopeDegrees)
            {
                return ConformRemoval.SteepSlope;
            }
            return ConformRemoval.Survives;
        }

        /// <summary> Conform a grid of painted cells: classify each, aggregate the removed area by reason, and report
        /// both absolute figures and the removed fraction. The per-cell classifications are returned in the same
        /// order for the highlighted-region overlay the author toggles. </summary>
        public static ConformReport Conform(IList<ConformCell> cells, double cellAreaM2, out IList<ConformRemoval> perCell)
        {
            List<ConformRemoval> classes = new List<ConformRemoval>(cells == null ? 0 : cells.Count);
            ConformReport report = new ConformReport();
            if (cells == null || cells.Count == 0 || cellAreaM2 <= 0.0)
            {
                perCell = classes;
                return report;
            }

            for (int i = 0; i < cells.Count; i++)
            {
                ConformRemoval c = ClassifyCell(cells[i]);
                classes.Add(c);
                report.paintedAreaM2 += cellAreaM2;
                switch (c)
                {
                    case ConformRemoval.Survives: report.survivingAreaM2 += cellAreaM2; break;
                    case ConformRemoval.Water: report.removedToWaterM2 += cellAreaM2; break;
                    case ConformRemoval.ShorelineSetback: report.removedToSetbackM2 += cellAreaM2; break;
                    case ConformRemoval.SteepSlope: report.removedToSlopeM2 += cellAreaM2; break;
                }
            }

            report.removedAreaM2 = report.removedToWaterM2 + report.removedToSetbackM2 + report.removedToSlopeM2;
            report.removedFraction = report.paintedAreaM2 > 0.0
                ? report.removedAreaM2 / report.paintedAreaM2
                : 0.0;
            perCell = classes;
            return report;
        }
    }
}
