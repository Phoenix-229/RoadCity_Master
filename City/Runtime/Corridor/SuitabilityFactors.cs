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
    /// <summary> RM-74's weights: the authored half of a weighted-overlay cost surface.
    ///
    /// Weighted overlay is only defensible when the weights are visible, which is the whole reason this is a
    /// serialised record with five named fields rather than five constants inside the builder. An author who
    /// disagrees with the balance can move it and see the map change; an author who cannot see it has an opaque
    /// score, and an opaque score is indistinguishable from a guess.
    ///
    /// The defaults are AUTHORED (C-15) and ordered by how much each factor actually decides a route in practice:
    /// gradient first, because a class that cannot climb the ground cannot use it at all; earthwork second, because
    /// it is what the money is spent on; water third, because a crossing is a known unit cost rather than a
    /// prohibition; land use fourth; existing network last, since it shifts a route rather than choosing it. </summary>
    [Serializable]
    public sealed class SuitabilityWeights
    {
        public float slopeWeight = 0.45f;
        public float earthworkWeight = 0.25f;
        public float waterWeight = 0.15f;
        public float landUseWeight = 0.10f;
        public float networkWeight = 0.05f;

        /// <summary> The sum. Not forced to 1 -- an author raising one weight should not silently have another
        /// lowered -- but reported, and the builder normalises by this so the cost stays comparable. </summary>
        public float Total
        {
            get { return slopeWeight + earthworkWeight + waterWeight + landUseWeight + networkWeight; }
        }

        /// <summary> Whether the weights are usable at all. All-zero is the one genuinely broken case: it makes
        /// every cell cost the same and the corridor becomes a straight line, which reads as a bug in the solver
        /// rather than as an empty weight set. </summary>
        public bool IsUsable { get { return Total > 0.0001f; } }

        /// <summary> The weights as one line for a legend or a report. </summary>
        public string Line()
        {
            return "slope " + Pct(slopeWeight) + ", earthwork " + Pct(earthworkWeight)
                + ", water " + Pct(waterWeight) + ", land use " + Pct(landUseWeight)
                + ", network " + Pct(networkWeight);
        }

        private string Pct(float weight)
        {
            float total = Total;
            float share = total > 0.0001f ? weight / total : 0f;
            return (share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        }
    }


    /// <summary> The five factors, each as one function of real ground.
    ///
    /// Every factor returns a RESISTANCE in 0..1: 0 is ground that costs a road nothing beyond building it, 1 is the
    /// worst ground the class still tolerates. Keeping one range for all five is what makes a weighted mean of them
    /// meaningful -- mixing a percentage with a cubic-metre count and calling the sum a cost is how overlay analyses
    /// produce numbers nobody can defend. The conversion to a cell cost happens once, in <see cref="CellCost"/>.
    ///
    /// Nothing here is stateful, random or time-dependent (C-06): the same ground gives the same number on every
    /// machine and in every session. </summary>
    public static class SuitabilityFactors
    {
        /// <summary> How much dearer the worst tolerable cell is than the cheapest one. A cell's cost is
        /// 1 + <see cref="CostSpread"/> x resistance, so flat open ground costs 1 and the worst ground the class
        /// accepts costs 6. AUTHORED: the ratio decides how far a least-cost path will detour to avoid bad ground,
        /// and 6:1 is what makes a solver prefer a 3 km valley route over a 1 km climb -- the trade real route
        /// studies actually make. </summary>
        public const double CostSpread = 5.0;

        #region "Gradient -- the dominant term and the forbidden mask"
        /// <summary> Ground steeper than the class can climb. This is the forbidden mask, and it is a comparison
        /// against the class's own authored maximum gradient (RM-09's <c>RoadClass.maxGradient</c>, a rise/run
        /// fraction) rather than a number invented here: a local street and an arterial disagree about what is
        /// impossible, and that disagreement is exactly why the map changes when the class changes. </summary>
        public static bool IsForbidden(double slopeFraction, double maxGradientFraction)
        {
            if (maxGradientFraction <= 0.0) { return false; }
            return slopeFraction > maxGradientFraction;
        }

        /// <summary> Gradient resistance. Zero on the flat, 1 at the class limit, and quadratic between: both the
        /// earthwork needed to hold a grade and the operating cost of driving it rise faster than the grade itself,
        /// so a linear ramp would treat a 7% grade as merely a little worse than 4% when in route terms it is most
        /// of the way to unusable. The exponent is AUTHORED at 2 as the simplest monotone convex form.
        ///
        /// Above the limit it saturates at 1 rather than growing: forbidden ground is reported by
        /// <see cref="IsForbidden"/>, and letting an impossible cell also carry a huge cost would double-count it in
        /// the corridor arithmetic. </summary>
        public static double SlopeResistance(double slopeFraction, double maxGradientFraction)
        {
            if (maxGradientFraction <= 0.0) { return 0.0; }
            double ratio = slopeFraction / maxGradientFraction;
            if (ratio <= 0.0) { return 0.0; }
            if (ratio >= 1.0) { return 1.0; }
            return ratio * ratio;
        }
        #endregion


        #region "Earthwork -- what the money is actually spent on"
        /// <summary> Cubic metres of earth moved per metre of road, for a BALANCED SIDEHILL BENCH: the level platform
        /// is cut into the hillside on its uphill half and the spoil placed as fill on its downhill half, with the
        /// centreline left at natural grade. That is the standard way a road crosses a slope, and it is the case worth
        /// costing, because it is the one whose volume is set by geometry rather than by an alignment choice.
        ///
        /// The geometry is exact, not fitted. On cross-slope <paramref name="crossSlopeFraction"/> = s the natural
        /// ground rises s x (W/2) over the uphill half-width, so the cut section is a triangle of area
        /// 0.5 x (W/2) x (s W/2) = s W^2 / 8; the fill triangle downhill is its mirror and has the same area. So
        /// each is s W^2 / 8 and the TOTAL moved is s W^2 / 4 cubic metres per metre of road -- quadratic in width,
        /// which is why a wide arterial on a slope costs disproportionately more than a lane, and linear in slope,
        /// which is why a route that contours is cheap and one that traverses is not.
        ///
        /// This is the volume the earthwork factor is normalised against, and it is the same quantity RM-39's
        /// <see cref="CutSlopeClass"/> then shapes into an actual cut face -- this function does not shape anything,
        /// it only prices the ground. </summary>
        public static double BenchVolumePerMetreM3(double crossSlopeFraction, double platformWidthMetres)
        {
            if (crossSlopeFraction <= 0.0 || platformWidthMetres <= 0.0) { return 0.0; }
            return crossSlopeFraction * platformWidthMetres * platformWidthMetres / 4.0;
        }

        /// <summary> Half of the above: the cut alone, which is what a haul or a spoil calculation needs when the
        /// bench does not balance. Named separately so no caller has to remember which half of the total it holds.
        /// </summary>
        public static double BenchCutPerMetreM3(double crossSlopeFraction, double platformWidthMetres)
        {
            return BenchVolumePerMetreM3(crossSlopeFraction, platformWidthMetres) * 0.5;
        }

        /// <summary> Earthwork resistance: the bench volume this cell would need, against the volume the same class
        /// needs at its own gradient limit. Normalising against the CLASS rather than against a fixed cubic-metre
        /// figure is what makes the number comparable between a local street and an arterial -- 300 m3/m is ruinous
        /// for one and ordinary for the other, so an absolute threshold would rank the two on the wrong axis.
        ///
        /// Linear in volume, and deliberately so: unlike gradient, earthwork cost really is close to proportional to
        /// the quantity moved. It saturates at 1 for the same reason gradient does. </summary>
        public static double EarthworkResistance(double crossSlopeFraction, double platformWidthMetres,
            double maxGradientFraction)
        {
            if (maxGradientFraction <= 0.0 || platformWidthMetres <= 0.0) { return 0.0; }
            double reference = BenchVolumePerMetreM3(maxGradientFraction, platformWidthMetres);
            if (reference <= 0.0) { return 0.0; }
            double ratio = BenchVolumePerMetreM3(crossSlopeFraction, platformWidthMetres) / reference;
            if (ratio <= 0.0) { return 0.0; }
            return ratio >= 1.0 ? 1.0 : ratio;
        }
        #endregion


        #region "Water -- a crossing is a price, never a prohibition"
        /// <summary> How much dearer a metre of structure is than a metre of road on the ground. AUTHORED at 10:
        /// bridge and major-culvert deck rates against at-grade carriageway rates sit in that order of magnitude, and
        /// the exact figure matters less than the behaviour it buys -- at 10x a solver will detour up a valley to find
        /// a narrow crossing, but will still cross rather than refuse a route, which is what real route studies do.
        /// A wet cell is never forbidden here: <see cref="IsForbidden"/> is gradient's alone. </summary>
        public const double CrossingCostMultiple = 10.0;

        /// <summary> Water resistance from how much of a cell is water. A cell that is a quarter water needs a
        /// quarter of its length carried on structure, so its resistance is that fraction scaled by how much dearer
        /// structure is -- expressed against <see cref="CostSpread"/> so it lands on the same 0..1 scale as the other
        /// four factors instead of being a cost hiding in a resistance.
        ///
        /// Fully wet ground therefore saturates at 1 and costs 6x an easy dry cell, matching the crossing multiple
        /// once the spread is applied; anything shallower is priced pro rata. </summary>
        public static double WaterResistance(double waterFraction)
        {
            if (waterFraction <= 0.0) { return 0.0; }
            double resistance = waterFraction * ((CrossingCostMultiple - 1.0) / CostSpread);
            return resistance >= 1.0 ? 1.0 : resistance;
        }
        #endregion


        #region "Land use -- the value of what a route would have to buy"
        /// <summary> Land-use resistance from a zone's own authored intensity. There is no construction-cost field on
        /// <see cref="ZoneTypeClass"/> to read, and inventing one would be a number with no source; what the zone does
        /// carry is PLOT RATIO (floor area per unit of site area, the planning term for how much building the zone
        /// permits) and SITE COVERAGE. Improvement value per hectare rises with both, and improvement value is exactly
        /// what a route through developed land has to acquire and demolish -- so the intensity a planner already
        /// authored is the honest proxy, rather than a second table nobody would keep in step with the first.
        ///
        /// Normalised against an AUTHORED reference plot ratio of 4.0, which is dense urban: at or above it the cell
        /// is as dear as this factor can make it, and a plot ratio of 0.8 -- <see cref="ZoneTypeClass"/>'s own default
        /// -- lands at a fifth of that. Coverage enters as a modest additional term because a fully built site offers
        /// no gap to thread through even at a low plot ratio.
        ///
        /// Where nothing is painted the answer is 0: unzoned ground resists a road no more than its own terrain does,
        /// and a builder with no zoning source must not have one implied for it. </summary>
        public const double ReferencePlotRatio = 4.0;

        public static double LandUseResistance(double plotRatio, double siteCoverage)
        {
            double intensity = plotRatio > 0.0 ? plotRatio / ReferencePlotRatio : 0.0;
            double coverage = siteCoverage > 0.0 ? siteCoverage : 0.0;
            if (coverage > 1.0) { coverage = 1.0; }
            double resistance = intensity * 0.75 + coverage * 0.25;
            if (resistance <= 0.0) { return 0.0; }
            return resistance >= 1.0 ? 1.0 : resistance;
        }
        #endregion


        #region "Existing network -- the term that shifts a route rather than choosing it"
        /// <summary> Resistance from distance to the nearest existing road. Zero on the existing network and rising to
        /// 1 at the class's own <c>targetSpacing</c>, linearly.
        ///
        /// The direction is deliberate and is the opposite of the naive reading. Being NEAR existing road is cheap,
        /// not dear: the corridor is already cleared, services already run, access already exists, and a new link that
        /// shadows an old one is how networks actually grow. So proximity is a DISCOUNT, expressed as low resistance,
        /// and open country carries the full weight of the factor.
        ///
        /// The decay length is the class's authored target spacing rather than a constant, because that field already
        /// states how far apart roads of this class are meant to be: beyond it, a route is by the class's own
        /// definition in territory that wants its own road, and there is nothing left to discount. Linear, and with
        /// the smallest weight of the five, because this term should nudge a route toward a corridor -- never overrule
        /// the ground. </summary>
        public static double NetworkResistance(double metresToNearestRoad, double targetSpacingMetres)
        {
            if (targetSpacingMetres <= 0.0) { return 0.0; }
            if (metresToNearestRoad <= 0.0) { return 0.0; }
            double ratio = metresToNearestRoad / targetSpacingMetres;
            return ratio >= 1.0 ? 1.0 : ratio;
        }
        #endregion


        #region "The one place resistances become a cost"
        /// <summary> The weighted mean of the five resistances, normalised by the authored total so the result stays in
        /// 0..1 whatever the author has done to the weights. This is the only arithmetic that combines factors, which
        /// is the point: a reader who wants to know how the score is formed reads one expression, not five scattered
        /// multiplications. </summary>
        public static double Resistance(double slope, double earthwork, double water, double landUse, double network,
            SuitabilityWeights weights)
        {
            if (weights == null || !weights.IsUsable) { return 0.0; }
            double sum = slope * weights.slopeWeight + earthwork * weights.earthworkWeight
                + water * weights.waterWeight + landUse * weights.landUseWeight
                + network * weights.networkWeight;
            double resistance = sum / weights.Total;
            if (resistance <= 0.0) { return 0.0; }
            return resistance >= 1.0 ? 1.0 : resistance;
        }

        /// <summary> A cell's traversal cost, as a multiple of the cheapest possible ground. Easy ground costs 1 and
        /// the worst ground a class tolerates costs 1 + <see cref="CostSpread"/>, so a cost-distance solver reads the
        /// numbers as "how many metres of easy ground is a metre here worth" -- a ratio with a meaning, rather than an
        /// index. Never zero and never negative: a free cell would let a path wander through it at no cost, and a
        /// negative one would break the shortest-path arithmetic outright. </summary>
        public static double CellCost(double resistance, bool forbidden)
        {
            if (forbidden) { return 1.0 + CostSpread; }
            double clamped = resistance <= 0.0 ? 0.0 : (resistance >= 1.0 ? 1.0 : resistance);
            return 1.0 + CostSpread * clamped;
        }
        #endregion


        #region "Naming the constraint -- the difference between a map and a score"
        /// <summary> Which of the five factors contributed most to this cell's cost, by WEIGHTED contribution rather
        /// than by raw resistance. Weighted is the only defensible reading: a land-use resistance of 1.0 at a weight of
        /// 0.10 is doing less to the route than a slope resistance of 0.4 at 0.45, and telling an author that land use
        /// is what stopped them there would send them to change the wrong thing.
        ///
        /// Ties resolve toward the earlier factor in the declared order (gradient, earthwork, water, land use,
        /// network), which is fixed rather than incidental -- a tie broken by iteration order would make the reported
        /// reason depend on how the loop happened to be written, and two identical cells must report identically
        /// (C-06). <see cref="SuitabilityFactor.None"/> when nothing resists at all. </summary>
        public static SuitabilityFactor BindingFactor(double slope, double earthwork, double water, double landUse,
            double network, SuitabilityWeights weights)
        {
            if (weights == null || !weights.IsUsable) { return SuitabilityFactor.None; }

            double best = 0.0;
            SuitabilityFactor binding = SuitabilityFactor.None;
            Consider(slope * weights.slopeWeight, SuitabilityFactor.Slope, ref best, ref binding);
            Consider(earthwork * weights.earthworkWeight, SuitabilityFactor.Earthwork, ref best, ref binding);
            Consider(water * weights.waterWeight, SuitabilityFactor.Water, ref best, ref binding);
            Consider(landUse * weights.landUseWeight, SuitabilityFactor.LandUse, ref best, ref binding);
            Consider(network * weights.networkWeight, SuitabilityFactor.NetworkDistance, ref best, ref binding);
            return binding;
        }

        private static void Consider(double contribution, SuitabilityFactor factor, ref double best,
            ref SuitabilityFactor binding)
        {
            if (contribution <= 0.0) { return; }
            if (contribution <= best) { return; }
            best = contribution;
            binding = factor;
        }

        /// <summary> The binding constraint as a sentence with its real measurement in it, for a cursor readout or a
        /// warning. This is the difference between a map and a score: "cost 4.7" tells an author nothing they can act
        /// on, whereas "slope 14 %, above the 8 % maximum for this class" tells them both what is wrong and which
        /// number to change. Every branch therefore carries the measured quantity in the unit the author authored it
        /// in -- per cent for gradients, cubic metres per metre for earthwork, metres for distance -- and
        /// <see cref="CultureInfo.InvariantCulture"/> so the sentence reads the same on every machine (C-06). </summary>
        public static string BindingSentence(SuitabilityFactor binding, bool forbidden, double slopeFraction,
            double maxGradientFraction, double earthworkVolumeM3PerMetre, double waterFraction, double plotRatio,
            double metresToNearestRoad)
        {
            if (forbidden)
            {
                return "slope " + Percent(slopeFraction) + ", above the " + Percent(maxGradientFraction)
                    + " maximum for this class, so this class cannot climb here";
            }

            switch (binding)
            {
                case SuitabilityFactor.Slope:
                    return "slope " + Percent(slopeFraction) + ", against a " + Percent(maxGradientFraction)
                        + " maximum for this class";
                case SuitabilityFactor.Earthwork:
                    return "earthwork " + Metres(earthworkVolumeM3PerMetre)
                        + " cubic metres per metre of road to bench a level platform here";
                case SuitabilityFactor.Water:
                    return "water across " + Percent(waterFraction)
                        + " of the cell, which a crossing would have to span";
                case SuitabilityFactor.LandUse:
                    return "developed land at a plot ratio of " + Metres(plotRatio)
                        + ", which a route would have to acquire";
                case SuitabilityFactor.NetworkDistance:
                    return Metres(metresToNearestRoad)
                        + " m from the nearest existing road, so this route brings its own corridor";
                default:
                    return "nothing here resists a road beyond the cost of building it";
            }
        }

        private static string Percent(double fraction)
        {
            return (fraction * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        }

        private static string Metres(double value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture);
        }
        #endregion
    }
}
