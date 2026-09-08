// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Where a cell's water fraction comes from, if anything knows.
    ///
    /// RM-74 needs to price a crossing, and the city layer has no per-cell water map of its own -- hydrology belongs to
    /// whatever generated the terrain. So water arrives through an interface with no implementation required: a builder
    /// handed nothing reads every cell as dry and says so in the legend, rather than inventing a river. </summary>
    public interface ISuitabilityWaterSource
    {
        /// <summary> The fraction of the cell that is water, 0..1. Anything outside that range is clamped by the
        /// builder rather than trusted. </summary>
        double WaterFractionAt(double worldXMetres, double worldZMetres);
    }


    /// <summary> Where a cell's land use comes from, if anything knows.
    ///
    /// RM-74's own wording is "land use where it exists", and that qualifier is the whole contract: there is no
    /// positional zone map in the city layer (RM-63's <see cref="ZonePaint"/> resolves a painted cell that a caller
    /// already holds; it does not answer "what is at this metre"), so a source that has one supplies it and a builder
    /// without one leaves the factor at zero. TRY, not GET, because "no zoning painted here" is the ordinary answer
    /// over most of a world and must not be confused with "zoned at plot ratio 0". </summary>
    public interface ISuitabilityLandUseSource
    {
        bool TryLandUseAt(double worldXMetres, double worldZMetres, out double plotRatio, out double siteCoverage);
    }


    /// <summary> Where the distance to the existing network comes from, if anything knows.
    ///
    /// A world with no roads in it yet has no nearest road, and the honest consequence is that the factor contributes
    /// NOTHING -- not that every cell is maximally remote. A constant term added to every cell of a cost surface
    /// changes no path at all, so pretending to a distance would be arithmetic theatre; returning false is the truth
    /// and the legend reports the factor as unused. </summary>
    public interface ISuitabilityNetworkSource
    {
        bool TryMetresToNearestRoad(double worldXMetres, double worldZMetres, out double metres);
    }


    /// <summary> RM-74's builder: real ground in, one explainable cost surface out.
    ///
    /// This is the half that touches the world, and it touches it in exactly one way -- it SAMPLES. Nothing here
    /// creates a node, an edge, a mesh or a terrain write; the output is a raster of numbers that a solver reads and an
    /// overlay draws, and if the author never draws a road the world is bit-for-bit as it was. That is not a
    /// convention, it is what makes the map advisory (§ 10.3): a suitability map that quietly modified anything would
    /// be a generator wearing an analysis's clothes.
    ///
    /// Every sample goes through <see cref="ITerrainSurfaceReader"/>, so the same builder runs standalone against the
    /// engine's own terrain and connected against a provider (RM-57), and runs headless in a gate against a reader
    /// backed by an array -- which is the only reason the arithmetic here can be tested at all. The other three inputs
    /// are optional by design; a factor with no source contributes nothing and the legend says which ones did.
    ///
    /// Deterministic throughout (C-06): a fixed cell order, no random source, no clock, and every reported number
    /// formatted with the invariant culture. </summary>
    public static class SuitabilityMapBuilder
    {
        /// <summary> Terrain slope as a rise/run FRACTION, from a surface normal.
        ///
        /// The normal's horizontal magnitude over its vertical component is the tangent of the surface's tilt, which is
        /// precisely rise over run -- the same unit <c>RoadClass.maxGradient</c> is authored in, so the two are directly
        /// comparable without a degrees conversion anywhere in the path. A normal with no vertical component is a
        /// vertical wall; it is reported as an effectively unbounded slope rather than dividing by zero. </summary>
        public static double SlopeFractionFromNormal(Vector3 normal)
        {
            double horizontal = Math.Sqrt((double)normal.x * normal.x + (double)normal.z * normal.z);
            double vertical = normal.y;
            if (vertical < 0.0) { vertical = -vertical; }
            if (vertical <= 1e-6) { return horizontal <= 1e-6 ? 0.0 : 1000.0; }
            return horizontal / vertical;
        }

        /// <summary> The platform a class needs benched into a hillside: the carriageway itself, both shoulders and the
        /// median where there is one -- which is what <c>RoadClass.TotalWidth</c> already sums. Earthwork is quadratic
        /// in this width, so taking it from the class rather than assuming a figure is what makes a wide arterial cost
        /// what a wide arterial costs. </summary>
        public static double PlatformWidthMetres(RoadClass cls)
        {
            if (cls == null) { return 0.0; }
            double width = cls.TotalWidth;
            return width > 0.0 ? width : 0.0;
        }

        /// <summary> Build the surface. One pass, one cell at a time, in row-major order -- the same order
        /// <see cref="SuitabilityRaster.Index"/> defines, so a reader stepping the arrays and a reader stepping the world
        /// see the same cells.
        ///
        /// Each cell is sampled at its CENTRE rather than at a corner. A corner sample belongs to four cells and would
        /// make the map depend on which of them asked first; a centre sample is the cell's own ground and is the value an
        /// author reading the cursor readout would expect to see.
        ///
        /// The cross-slope used for earthwork is the terrain slope itself. That is AUTHORED, and it is the honest choice
        /// for a direction-free map: a road CROSSING a hillside has a cross-slope equal to the ground's, and a road
        /// running straight up it has none -- but that second case is the one gradient is already forbidding, so taking
        /// the ground's own slope prices the case the map is actually advising about, and prices it as the worst of the
        /// two rather than the best. </summary>
        public static SuitabilityRasterClass Build(int widthCells, int depthCells, float metresPerCell,
            double originXMetres, double originZMetres, RoadClass cls, SuitabilityWeights weights,
            ITerrainSurfaceReader surface, ISuitabilityWaterSource water, ISuitabilityLandUseSource landUse,
            ISuitabilityNetworkSource network, out string legend)
        {
            SuitabilityRasterClass map = new SuitabilityRasterClass(widthCells, depthCells, metresPerCell);
            map.originXMetres = (float)originXMetres;
            map.originZMetres = (float)originZMetres;
            map.roadClassId = cls != null ? cls.id : 0;

            if (cls == null || weights == null || !weights.IsUsable || surface == null)
            {
                legend = LegendFor(map, cls, weights, false, false, false, 0, 0.0, 0.0)
                    + "\n  the map is empty: " + WhyEmpty(cls, weights, surface);
                return map;
            }

            double maxGradient = cls.maxGradient;
            double platform = PlatformWidthMetres(cls);
            bool usedWater = false;
            bool usedLandUse = false;
            bool usedNetwork = false;
            int forbiddenCells = 0;
            double lowest = 1.0;
            double highest = 0.0;

            for (int cz = 0; cz < map.depthCells; cz++)
            {
                for (int cx = 0; cx < map.widthCells; cx++)
                {
                    int index = SuitabilityRaster.Index(map, cx, cz);
                    if (index < 0) { continue; }
                    double x = SuitabilityRaster.CellCentreXMetres(map, cx);
                    double z = SuitabilityRaster.CellCentreZMetres(map, cz);

                    double slope = SlopeFractionFromNormal(surface.SampleNormal((float)x, (float)z));
                    bool forbidden = SuitabilityFactors.IsForbidden(slope, maxGradient);
                    if (forbidden) { forbiddenCells++; }

                    double earthworkVolume = SuitabilityFactors.BenchVolumePerMetreM3(slope, platform);
                    double rSlope = SuitabilityFactors.SlopeResistance(slope, maxGradient);
                    double rEarthwork = SuitabilityFactors.EarthworkResistance(slope, platform, maxGradient);

                    double waterFraction = 0.0;
                    if (water != null)
                    {
                        waterFraction = Clamp01(water.WaterFractionAt(x, z));
                        if (waterFraction > 0.0) { usedWater = true; }
                    }
                    double rWater = SuitabilityFactors.WaterResistance(waterFraction);

                    double plotRatio = 0.0;
                    double siteCoverage = 0.0;
                    if (landUse != null && landUse.TryLandUseAt(x, z, out plotRatio, out siteCoverage))
                    { usedLandUse = true; }
                    else { plotRatio = 0.0; siteCoverage = 0.0; }
                    double rLandUse = SuitabilityFactors.LandUseResistance(plotRatio, siteCoverage);

                    double metresToRoad = 0.0;
                    double rNetwork = 0.0;
                    if (network != null && network.TryMetresToNearestRoad(x, z, out metresToRoad))
                    {
                        usedNetwork = true;
                        rNetwork = SuitabilityFactors.NetworkResistance(metresToRoad, cls.targetSpacing);
                    }

                    double resistance = SuitabilityFactors.Resistance(rSlope, rEarthwork, rWater, rLandUse, rNetwork,
                        weights);
                    if (forbidden) { resistance = 1.0; }

                    map.cost[index] = (float)resistance;
                    map.slopeFraction[index] = (float)slope;
                    map.earthworkVolumeM3[index] = (float)earthworkVolume;
                    map.forbidden[index] = forbidden;
                    map.binding[index] = (byte)SuitabilityFactors.BindingFactor(rSlope, rEarthwork, rWater, rLandUse,
                        rNetwork, weights);
                    map.bindingMeasure[index] = (float)MeasureFor((SuitabilityFactor)map.binding[index], slope,
                        earthworkVolume, waterFraction, plotRatio, metresToRoad);

                    if (resistance < lowest) { lowest = resistance; }
                    if (resistance > highest) { highest = resistance; }
                }
            }

            if (highest < lowest) { lowest = 0.0; highest = 0.0; }
            legend = LegendFor(map, cls, weights, usedWater, usedLandUse, usedNetwork, forbiddenCells, lowest, highest);
            return map;
        }

        private static double Clamp01(double value)
        {
            if (value <= 0.0) { return 0.0; }
            return value >= 1.0 ? 1.0 : value;
        }

        /// <summary> The binding factor's measurement, picked out of the five that were taken. Each factor's own unit --
        /// there is no common one, and converting them to a shared scale is exactly what the resistances already did;
        /// this is the number underneath that conversion, kept so the readout can quote ground rather than arithmetic.
        /// </summary>
        public static double MeasureFor(SuitabilityFactor binding, double slopeFraction, double earthworkVolumeM3,
            double waterFraction, double plotRatio, double metresToNearestRoad)
        {
            switch (binding)
            {
                case SuitabilityFactor.Slope: return slopeFraction;
                case SuitabilityFactor.Earthwork: return earthworkVolumeM3;
                case SuitabilityFactor.Water: return waterFraction;
                case SuitabilityFactor.LandUse: return plotRatio;
                case SuitabilityFactor.NetworkDistance: return metresToNearestRoad;
                default: return 0.0;
            }
        }

        /// <summary> The legend: the weights, the class, and WHICH FACTORS ACTUALLY HAD A SOURCE.
        ///
        /// The last part is the one that keeps the map honest. A five-factor overlay drawn from two factors looks
        /// identical to one drawn from five, and an author who does not know which is which will read a slope map as a
        /// planning map. So the legend states, per factor, whether anything answered -- and a factor with no source is
        /// named as unused rather than shown at its weight, because a weight applied to a constant zero is not a
        /// contribution. </summary>
        public static string LegendFor(SuitabilityRasterClass map, RoadClass cls, SuitabilityWeights weights,
            bool usedWater, bool usedLandUse, bool usedNetwork, int forbiddenCells, double lowest, double highest)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append("Suitability for ");
            sb.Append(cls != null && !string.IsNullOrEmpty(cls.displayName) ? cls.displayName : "no road class");
            sb.Append("\n  ").Append(SuitabilityRaster.SizeLine(map));
            sb.Append("\n  weights: ").Append(weights != null ? weights.Line() : "none");
            sb.Append("\n  sourced: gradient yes, earthwork yes, water ").Append(usedWater ? "yes" : "no source")
                .Append(", land use ").Append(usedLandUse ? "yes" : "no source")
                .Append(", existing network ").Append(usedNetwork ? "yes" : "no source");
            sb.Append("\n  relative cost ").Append(Number(lowest)).Append(" to ").Append(Number(highest))
                .Append(", so the dearest ground costs ")
                .Append(Number(SuitabilityFactors.CellCost(highest, false)))
                .Append(" times a metre of the cheapest");
            sb.Append("\n  ").Append(forbiddenCells.ToString(CultureInfo.InvariantCulture))
                .Append(" cells are steeper than this class can climb, which warns at draw time and blocks nothing");
            return sb.ToString();
        }

        /// <summary> Why an empty map is empty, in words. A blank overlay with no explanation is the exact failure
        /// § 10.24 forbids everywhere else in this tool, and it would be no better here. </summary>
        private static string WhyEmpty(RoadClass cls, SuitabilityWeights weights, ITerrainSurfaceReader surface)
        {
            if (cls == null) { return "no road class was given, and every number in the map is relative to one"; }
            if (weights == null) { return "no weights were given"; }
            if (!weights.IsUsable)
            {
                return "every weight is zero, which would make all ground equally good and the corridor a straight line";
            }
            if (surface == null)
            {
                return "no terrain reader was given, so there is no ground to measure -- "
                    + TerrainFeatureAvailability.WhatToConnect;
            }
            return "the reason is not known";
        }

        private static string Number(double value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture);
        }

        /// <summary> One cell in words: where it is, what a metre through it costs, and WHY. This is the cursor readout,
        /// and it lives here rather than in the overlay so a gate can assert the sentence without opening a scene view --
        /// the claim "the map explains itself" is only worth making if something checks it.
        ///
        /// The binding measurement is passed into every unit slot of
        /// <see cref="SuitabilityFactors.BindingSentence"/>, which is correct rather than sloppy: exactly one branch of
        /// that method runs, the one matching the binding factor, and it reads the slot holding its own unit. The
        /// alternative -- storing all five measurements per cell to fill the other slots with values no branch will read
        /// -- would quintuple a world-sized raster for nothing. </summary>
        public static string ReadoutFor(SuitabilityRasterClass map, int cellX, int cellZ, RoadClass cls)
        {
            int index = SuitabilityRaster.Index(map, cellX, cellZ);
            if (index < 0) { return "outside the mapped area"; }

            double maxGradient = cls != null ? cls.maxGradient : 0.0;
            SuitabilityFactor binding = (SuitabilityFactor)map.binding[index];
            double measure = map.bindingMeasure[index];
            double cost = SuitabilityFactors.CellCost(map.cost[index], map.forbidden[index]);

            return "cell " + cellX.ToString(CultureInfo.InvariantCulture) + ", "
                + cellZ.ToString(CultureInfo.InvariantCulture)
                + "  --  a metre here costs " + Number(cost) + " metres of the easiest ground"
                + "\n  " + SuitabilityFactors.BindingSentence(binding, map.forbidden[index], map.slopeFraction[index],
                    maxGradient, map.earthworkVolumeM3[index], measure, measure, measure);
        }
    }
}
