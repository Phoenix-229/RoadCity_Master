#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-74's gate: the corridor map is arithmetic, so it is checked as arithmetic.
    ///
    /// A suitability map is the easiest kind of feature to ship broken, because every version of it produces a plausible
    /// coloured picture. A map with the weights transposed looks like a map. A map that has silently stopped reading
    /// gradient looks like a map. So the cases below do not check that something was drawn -- they check the six claims
    /// RM-74 actually makes: that the forbidden mask is the CLASS's own limit and not a constant, that cost is GRADED
    /// rather than binary, that changing the class changes the map, that the corridor is a band around the least-cost
    /// path rather than a buffer drawn around a line, that two runs agree to the last bit, and that NOTHING is created
    /// by any of it.
    ///
    /// The earthwork case is the one worth reading. It checks the bench volume against the closed-form geometry -- cut
    /// and fill each s W^2 / 8, total s W^2 / 4 -- rather than against a number this gate also computed, because a test
    /// that recomputes the implementation's own formula proves only that the formula is stable, not that it is right
    /// (C-15).
    ///
    /// Every fixture is analytic and every comparison is a tolerance on a stated quantity, so this runs headless with no
    /// scene, no terrain asset and no provider. </summary>
    public static class CityCorridorGate
    {
        private const string Prefix = "RCMCORR";

        [MenuItem("Window/Road City Master/Testing/Corridor and suitability")]
        public static void RunFromMenu()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            if (!passed) { Debug.LogError(Prefix + " FAILED"); }
        }

        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        public static bool Run(out string report)
        {
            List<string> failures = new List<string>();

            CaseForbiddenFollowsTheClass(failures);
            CaseGradientIsGradedNotBinary(failures);
            CaseEarthworkIsRealGeometry(failures);
            CaseCostHasAFloorAndACeiling(failures);
            CaseWeightsAreVisible(failures);
            CaseBindingIsWeightedNotRaw(failures);
            CaseReadoutNamesTheConstraint(failures);
            CaseChangingTheClassChangesTheMap(failures);
            CaseCorridorContainsTheLeastCostPath(failures);
            CaseWiderToleranceContainsNarrower(failures);
            CaseSameSurfaceGivesTheSameAnswerTwice(failures);
            CaseSwappingTheEndsGivesTheSameCorridor(failures);
            CaseUnsourcedFactorsAreNamedInTheLegend(failures);
            CaseNothingIsCreated(failures);

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.Append(Prefix).Append(" BEGIN\n");
            for (int i = 0; i < failures.Count; i++)
            {
                sb.Append(Prefix).Append(" FAIL   ").Append(failures[i]).Append('\n');
            }
            sb.Append(Prefix).Append(failures.Count == 0 ? " result PASS" : " result FAIL")
                .Append("  cases 14  failures ").Append(failures.Count).Append('\n');
            sb.Append(Prefix).Append(" END");
            report = sb.ToString();
            return failures.Count == 0;
        }

        private static void Expect(bool condition, string failure, List<string> failures)
        {
            if (!condition) { failures.Add(failure); }
        }

        /// <summary> Numbers in failure text, invariant (C-06): a gate line that reads "0,44" on one machine and "0.44"
        /// on another is a diff nobody can compare. </summary>
        private static string Num(double value)
        {
            return value.ToString("0.####", CultureInfo.InvariantCulture);
        }

        #region "Fixtures -- analytic ground and hand-made surfaces"
        /// <summary> An ordinary two-lane street: 7 m of platform and an 8 % maximum gradient, both from
        /// <see cref="RoadClass"/>'s own defaults rather than invented here, so the gate is checking the class the tool
        /// actually ships with. </summary>
        private static RoadClass LocalStreet()
        {
            RoadClass cls = new RoadClass();
            cls.id = 1;
            cls.displayName = "gate local street";
            return cls;
        }

        /// <summary> The same street with twice the gradient tolerance. It exists for one case only: ground that one
        /// class cannot climb and the other can, which is what makes "the mask is the class's, not a constant" a
        /// difference a machine can see. </summary>
        private static RoadClass ForgivingClass()
        {
            RoadClass cls = new RoadClass();
            cls.id = 2;
            cls.displayName = "gate mountain road";
            cls.maxGradient = 0.16f;
            return cls;
        }

        /// <summary> A uniform analytic slope, as an <see cref="ITerrainSurfaceReader"/>. RM-11's ramp supplies the
        /// height field and RM-57's provider sink adapts it, so the gate borrows two shipped pieces instead of adding a
        /// third fake terrain that could disagree with them. </summary>
        private static ITerrainSurfaceReader Ramp(float gradePercent)
        {
            return new ProviderTerrainSink(new RampTerrain(0f, gradePercent, gradePercent, 0f), "corridor gate ramp");
        }

        private static SuitabilityRasterClass BuildOn(RoadClass cls, ITerrainSurfaceReader reader, out string legend)
        {
            return SuitabilityMapBuilder.Build(24, 24, 16f, 0.0, 0.0, cls, new SuitabilityWeights(), reader,
                null, null, null, out legend);
        }

        /// <summary> A hand-made surface with one cheap row through expensive ground.
        ///
        /// The solver cases use this rather than a terrain, and deliberately: on an analytic ramp every cell in a region
        /// costs the same, so a path along any route costs the same and "did it find the cheap way" cannot be asked. A
        /// channel has exactly one cheap way, and its cells are known before the solver runs -- so the assertion is
        /// about the answer, not about the arithmetic that produced it. </summary>
        private static SuitabilityRasterClass Channel(int widthCells, int depthCells, int cheapRow)
        {
            SuitabilityRasterClass surface = new SuitabilityRasterClass(widthCells, depthCells, 10f);
            for (int cz = 0; cz < depthCells; cz++)
            {
                for (int cx = 0; cx < widthCells; cx++)
                {
                    int index = SuitabilityRaster.Index(surface, cx, cz);
                    if (index < 0) { continue; }
                    surface.cost[index] = cz == cheapRow ? 0f : 0.9f;
                    surface.binding[index] = (byte)(cz == cheapRow ? SuitabilityFactor.None : SuitabilityFactor.Slope);
                }
            }
            return surface;
        }
        #endregion

        #region "The mask, the grading and the geometry"
        /// <summary> The forbidden mask is the CLASS's limit, not a constant. Ground at a uniform 12 % is beyond a local
        /// street's 8 % and inside a mountain road's 16 %, so the same terrain must mask completely for one class and not
        /// at all for the other. A hard-coded threshold anywhere in the path fails this in one direction or the other.
        /// </summary>
        private static void CaseForbiddenFollowsTheClass(List<string> failures)
        {
            string legendStrict;
            string legendForgiving;
            SuitabilityRasterClass strict = BuildOn(LocalStreet(), Ramp(12f), out legendStrict);
            SuitabilityRasterClass forgiving = BuildOn(ForgivingClass(), Ramp(12f), out legendForgiving);

            int cells = SuitabilityRaster.CellCount(strict);
            int strictForbidden = 0;
            int forgivingForbidden = 0;
            for (int i = 0; i < cells; i++)
            {
                if (strict.forbidden[i]) { strictForbidden++; }
                if (forgiving.forbidden[i]) { forgivingForbidden++; }
            }

            Expect(cells == 576, "the fixture raster should hold 576 cells, holds " + cells, failures);
            Expect(strictForbidden == cells, "12 % ground is steeper than a local street's 8 % limit, so all " + cells
                + " cells should be forbidden; " + strictForbidden + " are", failures);
            Expect(forgivingForbidden == 0,
                "12 % ground is within a 16 % class's limit, so no cell should be forbidden; "
                + forgivingForbidden + " are", failures);
            Expect(legendStrict.Contains("576 cells are steeper"),
                "the legend should count the cells this class cannot climb: " + legendStrict, failures);
            Expect(legendForgiving.Contains("0 cells are steeper"),
                "the legend should say none are too steep for the forgiving class: " + legendForgiving, failures);
        }

        /// <summary> Cost below the limit is GRADED, not binary. This is the failure a forbidden mask hides: a map that
        /// prices every climbable cell alike still masks correctly, still colours plausibly, and is useless for routing,
        /// because a solver crossing it has no reason to prefer 2 % ground to 7 % ground. So gentle ground must cost
        /// strictly more than nothing and strictly less than the ceiling, and steep-but-legal ground must cost strictly
        /// more than gentle. </summary>
        private static void CaseGradientIsGradedNotBinary(List<string> failures)
        {
            string ignored;
            SuitabilityRasterClass gentle = BuildOn(LocalStreet(), Ramp(2f), out ignored);
            SuitabilityRasterClass steep = BuildOn(LocalStreet(), Ramp(6f), out ignored);
            int probe = SuitabilityRaster.Index(gentle, 12, 12);

            double gentleCost = gentle.cost[probe];
            double steepCost = steep.cost[probe];

            Expect(probe >= 0, "cell 12, 12 should exist on a 24 x 24 raster", failures);
            Expect(!gentle.forbidden[probe] && !steep.forbidden[probe],
                "2 % and 6 % ground are both inside a local street's 8 % limit and neither should be masked", failures);
            Expect(gentleCost > 0.0,
                "2 % ground should cost something rather than nothing, costs " + Num(gentleCost), failures);
            Expect(steepCost < 1.0, "6 % ground is legal for this class and should not reach the ceiling, costs "
                + Num(steepCost), failures);
            Expect(steepCost - gentleCost > 0.1, "6 % ground should cost materially more than 2 % ground; "
                + Num(gentleCost) + " against " + Num(steepCost), failures);
        }

        /// <summary> The earthwork term is GEOMETRY, checked against the closed form rather than against a number this
        /// gate asked the implementation for.
        ///
        /// A balanced sidehill bench of platform width W on cross-slope s cuts a triangle of area 0.5 x (W/2) x (sW/2)
        /// = s W^2 / 8 out of the uphill half and drops the mirror of it into the downhill half, so it moves s W^2 / 4
        /// cubic metres per metre of road and the cut is exactly half of that. Two consequences follow from the algebra
        /// and are asserted here because they are what make the term real: it is QUADRATIC in width, so a platform twice
        /// as wide costs four times the earthwork, and LINEAR in cross-slope. A fitted score would satisfy neither.
        /// </summary>
        private static void CaseEarthworkIsRealGeometry(List<string> failures)
        {
            double w = SuitabilityMapBuilder.PlatformWidthMetres(LocalStreet());
            Expect(System.Math.Abs(w - 7.0) < 1e-4,
                "the shipped two-lane default should bench 7 m of platform, benches " + Num(w), failures);

            double total = SuitabilityFactors.BenchVolumePerMetreM3(0.10, 7.0);
            double cut = SuitabilityFactors.BenchCutPerMetreM3(0.10, 7.0);
            Expect(System.Math.Abs(total - 1.225) < 1e-6, "0.10 x 7^2 / 4 is 1.225 m3 per metre, got " + Num(total),
                failures);
            Expect(System.Math.Abs(cut - 0.6125) < 1e-6, "the cut is half the moved volume, 0.6125 m3 per metre, got "
                + Num(cut), failures);
            Expect(System.Math.Abs((total - cut) - cut) < 1e-9,
                "a balanced bench fills with exactly what it cut, so cut and fill should match", failures);

            double wide = SuitabilityFactors.BenchVolumePerMetreM3(0.10, 14.0);
            Expect(System.Math.Abs(wide - 4.0 * total) < 1e-6,
                "earthwork is quadratic in width, so twice the platform should move four times the volume; "
                + Num(wide) + " against " + Num(4.0 * total), failures);
            double steeper = SuitabilityFactors.BenchVolumePerMetreM3(0.20, 7.0);
            Expect(System.Math.Abs(steeper - 2.0 * total) < 1e-6,
                "earthwork is linear in cross-slope, so twice the slope should move twice the volume; "
                + Num(steeper) + " against " + Num(2.0 * total), failures);
            Expect(SuitabilityFactors.BenchVolumePerMetreM3(0.0, 7.0) == 0.0,
                "flat ground needs no bench and should move nothing", failures);

            string ignored;
            SuitabilityRasterClass map = BuildOn(ForgivingClass(), Ramp(10f), out ignored);
            int probe = SuitabilityRaster.Index(map, 12, 12);
            Expect(System.Math.Abs(map.earthworkVolumeM3[probe] - 1.225) < 1e-3,
                "the raster should carry the real volume for 10 % ground under a 7 m platform, carries "
                + Num(map.earthworkVolumeM3[probe]), failures);
        }
        #endregion

        #region "Cost, weights and naming the constraint"
        /// <summary> A cell's cost has a FLOOR of 1 and a CEILING of 1 + spread, and the floor is the load-bearing half.
        /// A resistance-of-zero cell costing zero would make a metre of easy ground free, every route across easy ground
        /// cost nothing, and the least-cost path between two points on a plain undefined. The ceiling matters for the
        /// opposite reason: forbidden ground priced at infinity turns "expensive" into "impossible" and quietly breaks
        /// RM-74's rule that the map warns and never refuses. </summary>
        private static void CaseCostHasAFloorAndACeiling(List<string> failures)
        {
            double floor = SuitabilityFactors.CellCost(0.0, false);
            double ceiling = SuitabilityFactors.CellCost(1.0, false);
            double masked = SuitabilityFactors.CellCost(0.0, true);
            double middle = SuitabilityFactors.CellCost(0.5, false);

            Expect(System.Math.Abs(floor - 1.0) < 1e-9,
                "the cheapest ground should cost one metre per metre, costs " + Num(floor), failures);
            Expect(System.Math.Abs(ceiling - (1.0 + SuitabilityFactors.CostSpread)) < 1e-9,
                "the worst tolerable ground should cost 1 + the spread, costs " + Num(ceiling), failures);
            Expect(System.Math.Abs(masked - ceiling) < 1e-9,
                "forbidden ground should cost the worst tolerable rate rather than infinity, costs " + Num(masked),
                failures);
            Expect(System.Math.Abs(middle - (1.0 + 0.5 * SuitabilityFactors.CostSpread)) < 1e-9,
                "half the resistance should be half the spread above the floor, costs " + Num(middle), failures);
            Expect(SuitabilityFactors.CellCost(-3.0, false) >= 1.0,
                "a negative resistance should clamp to the floor, not below it", failures);
            Expect(SuitabilityFactors.CellCost(9.0, false) <= ceiling,
                "a resistance above 1 should clamp to the ceiling, not exceed it", failures);
        }

        /// <summary> The weights are VISIBLE, and visible means two things a machine can check: the legend states each
        /// factor's share, and moving a weight moves the stated share. An overlay whose balance is buried in constants is
        /// an opaque score, which RM-74 explicitly is not. </summary>
        private static void CaseWeightsAreVisible(List<string> failures)
        {
            SuitabilityWeights authored = new SuitabilityWeights();
            string line = authored.Line();
            Expect(line.Contains("slope") && line.Contains("earthwork") && line.Contains("water")
                && line.Contains("land use") && line.Contains("network"),
                "the weight line should name all five factors: " + line, failures);
            Expect(line.Contains("45%") && line.Contains("25%"),
                "the weight line should state each factor's share: " + line, failures);

            SuitabilityWeights slopeOnly = new SuitabilityWeights();
            slopeOnly.earthworkWeight = 0f;
            slopeOnly.waterWeight = 0f;
            slopeOnly.landUseWeight = 0f;
            slopeOnly.networkWeight = 0f;
            Expect(slopeOnly.Line() != line, "changing a weight should change the line an author reads", failures);
            Expect(slopeOnly.Line().Contains("slope 100%"),
                "one non-zero weight should read as the whole of the balance: " + slopeOnly.Line(), failures);

            string legend;
            BuildOn(LocalStreet(), Ramp(4f), out legend);
            Expect(legend.Contains("weights: " + line), "the map's own legend should quote the weights it used: "
                + legend, failures);
        }

        /// <summary> The named constraint is the largest WEIGHTED contribution, not the largest raw resistance. The
        /// difference is the whole value of the readout: on a cell where slope resists 0.5 at a 45 % weight and water
        /// resists 0.6 at a 15 % weight, water is the bigger number and slope is the bigger reason, and an overlay that
        /// names water there is telling the author to bridge something that is not the problem. Checked in both
        /// directions, so a method that simply always answers "slope" fails too. </summary>
        private static void CaseBindingIsWeightedNotRaw(List<string> failures)
        {
            SuitabilityWeights weights = new SuitabilityWeights();
            SuitabilityFactor slopeWins = SuitabilityFactors.BindingFactor(0.5, 0.0, 0.6, 0.0, 0.0, weights);
            SuitabilityFactor waterWins = SuitabilityFactors.BindingFactor(0.2, 0.0, 0.9, 0.0, 0.0, weights);
            SuitabilityFactor nothing = SuitabilityFactors.BindingFactor(0.0, 0.0, 0.0, 0.0, 0.0, weights);

            Expect(slopeWins == SuitabilityFactor.Slope,
                "slope resisting 0.5 at 45 % should outweigh water resisting 0.6 at 15 %, bound on " + slopeWins,
                failures);
            Expect(waterWins == SuitabilityFactor.Water,
                "water resisting 0.9 at 15 % should outweigh slope resisting 0.2 at 45 %, bound on " + waterWins,
                failures);
            Expect(nothing == SuitabilityFactor.None,
                "ground that resists nothing should name no constraint, named " + nothing, failures);
        }

        /// <summary> The readout NAMES the constraint and quotes it in its own unit. Two sentences are checked, because
        /// the two failures are different: on ground the class cannot climb it must say so and quote both the slope and
        /// the limit it exceeded, and on ordinary ground it must still say what the cost is made of rather than falling
        /// back to the forbidden wording. Off the raster it must refuse rather than read the nearest edge. </summary>
        private static void CaseReadoutNamesTheConstraint(List<string> failures)
        {
            RoadClass cls = LocalStreet();
            string ignored;
            SuitabilityRasterClass tooSteep = BuildOn(cls, Ramp(12f), out ignored);
            SuitabilityRasterClass gentle = BuildOn(cls, Ramp(3f), out ignored);

            string steepLine = SuitabilityMapBuilder.ReadoutFor(tooSteep, 12, 12, cls);
            string gentleLine = SuitabilityMapBuilder.ReadoutFor(gentle, 12, 12, cls);
            string outside = SuitabilityMapBuilder.ReadoutFor(gentle, 99, 99, cls);

            Expect(steepLine.Contains("cannot climb"),
                "the readout on forbidden ground should say the class cannot climb it: " + steepLine, failures);
            Expect(steepLine.Contains("12 %") && steepLine.Contains("8 %"),
                "it should quote the ground's slope and the limit it exceeded: " + steepLine, failures);
            Expect(steepLine.Contains("metres of the easiest ground"),
                "it should price the cell in metres of easy ground: " + steepLine, failures);
            Expect(!gentleLine.Contains("cannot climb"),
                "3 % ground is climbable and the readout should not say otherwise: " + gentleLine, failures);
            Expect(gentleLine.Contains("cubic metres per metre") || gentleLine.Contains("slope "),
                "the readout on ordinary ground should still quote a measurement: " + gentleLine, failures);
            Expect(outside == "outside the mapped area",
                "a cell off the raster should be refused rather than answered: " + outside, failures);
        }
        #endregion

        #region "The class, the path and the band"
        /// <summary> Changing the class changes the map. Two claims in one, because they fail separately: a class with a
        /// different gradient tolerance must price the SAME ground differently, and a class with a wider platform must
        /// carry a bigger earthwork volume on that ground. A builder that read a constant gradient limit would pass the
        /// second and fail the first; one that assumed a platform width would pass the first and fail the second.
        /// </summary>
        private static void CaseChangingTheClassChangesTheMap(List<string> failures)
        {
            string ignored;
            RoadClass street = LocalStreet();
            RoadClass mountain = ForgivingClass();
            SuitabilityRasterClass strictMap = BuildOn(street, Ramp(6f), out ignored);
            SuitabilityRasterClass forgivingMap = BuildOn(mountain, Ramp(6f), out ignored);
            int probe = SuitabilityRaster.Index(strictMap, 12, 12);

            Expect(strictMap.roadClassId == street.id && forgivingMap.roadClassId == mountain.id,
                "each raster should carry the class it was built for", failures);
            Expect(strictMap.cost[probe] > forgivingMap.cost[probe] + 0.1,
                "6 % ground should cost a 8 % class materially more than a 16 % class; "
                + Num(strictMap.cost[probe]) + " against " + Num(forgivingMap.cost[probe]), failures);

            RoadClass wide = LocalStreet();
            wide.id = 3;
            wide.laneAmount = 4;
            SuitabilityRasterClass wideMap = BuildOn(wide, Ramp(6f), out ignored);
            double narrowVolume = strictMap.earthworkVolumeM3[probe];
            double wideVolume = wideMap.earthworkVolumeM3[probe];
            Expect(System.Math.Abs(wideVolume - 4.0 * narrowVolume) < 1e-3,
                "twice the platform should bench four times the volume; " + Num(narrowVolume) + " against "
                + Num(wideVolume), failures);
        }

        /// <summary> The corridor is a statement about COST, and this is the case that proves it rather than assuming it.
        ///
        /// The fixture is a channel: one cheap row through ground five and a half times dearer, so the cheapest route is
        /// known before the solver runs. Four things must hold together. The path must lie entirely in the cheap row --
        /// a solver that ignored cost would cut the diagonal. The band's minimum through-cost must EQUAL the path's own
        /// cost, which is the identity the whole construction rests on. Every path cell must be inside the band, since a
        /// corridor that excludes the optimum is not a corridor. And the band must be smaller than the raster, because a
        /// tolerance that admits everything measures nothing. </summary>
        private static void CaseCorridorContainsTheLeastCostPath(List<string> failures)
        {
            SuitabilityRasterClass surface = Channel(20, 9, 4);
            CorridorPlan plan = CorridorSolver.Solve(surface, 0, 4, 19, 4,
                CorridorSolver.DefaultToleranceFraction);

            Expect(plan.reached, "the far end of a connected channel should be reachable", failures);
            Expect(plan.pathCells.Count == 20,
                "the path across 20 columns should be 20 cells, is " + plan.pathCells.Count, failures);

            int strayed = 0;
            int outsideBand = 0;
            for (int i = 0; i < plan.pathCells.Count; i++)
            {
                int cell = plan.pathCells[i];
                if (cell / surface.widthCells != 4) { strayed++; }
                if (!plan.inCorridor[cell]) { outsideBand++; }
            }
            Expect(strayed == 0, "the least-cost path should stay in the cheap row; " + strayed
                + " cells left it", failures);
            Expect(outsideBand == 0, "every cell of the optimum should be inside the corridor; " + outsideBand
                + " are not", failures);
            Expect(System.Math.Abs(plan.minimumTotal - plan.pathCost) < 1e-6,
                "the cheapest through-cost is the path's own cost; band " + Num(plan.minimumTotal) + " against path "
                + Num(plan.pathCost), failures);
            Expect(plan.cellsInCorridor >= 20 && plan.cellsInCorridor < 180,
                "the band should be a band, not the whole raster; " + plan.cellsInCorridor + " of 180 cells", failures);
        }

        /// <summary> A wider tolerance contains a narrower one, and contains MORE. Containment is the property that makes
        /// the tolerance an author-facing dial rather than a random reshuffle -- widening it must only ever add ground --
        /// and the strict growth is what shows the dial is connected to anything at all. </summary>
        private static void CaseWiderToleranceContainsNarrower(List<string> failures)
        {
            SuitabilityRasterClass surface = Channel(20, 9, 4);
            CorridorPlan tight = CorridorSolver.Solve(surface, 0, 4, 19, 4, 0.05);
            CorridorPlan loose = CorridorSolver.Solve(surface, 0, 4, 19, 4, 0.60);

            int lost = 0;
            for (int i = 0; i < tight.inCorridor.Length; i++)
            {
                if (tight.inCorridor[i] && !loose.inCorridor[i]) { lost++; }
            }
            Expect(lost == 0, "widening the tolerance should never drop ground; " + lost + " cells were lost", failures);
            Expect(loose.cellsInCorridor > tight.cellsInCorridor,
                "a 60 % tolerance should admit more ground than a 5 % one; " + tight.cellsInCorridor + " then "
                + loose.cellsInCorridor, failures);
            Expect(System.Math.Abs(loose.minimumTotal - tight.minimumTotal) < 1e-9,
                "the tolerance widens the band and must not move the optimum", failures);
        }
        #endregion

        #region "Determinism and symmetry"
        /// <summary> Two runs agree to the last bit (C-06). This is not a formality on a shortest-path solver: flat ground
        /// produces exact cost ties by the thousand, and a frontier that broke them on insertion order would return two
        /// different -- equally optimal -- paths on two runs, leaving an author unable to tell an ordering artefact from a
        /// real change. Both halves are checked, the raster and the solve, and both with exact equality rather than a
        /// tolerance: "nearly the same map" is the failure this case exists to catch. </summary>
        private static void CaseSameSurfaceGivesTheSameAnswerTwice(List<string> failures)
        {
            string firstLegend;
            string secondLegend;
            SuitabilityRasterClass first = BuildOn(LocalStreet(), Ramp(7f), out firstLegend);
            SuitabilityRasterClass second = BuildOn(LocalStreet(), Ramp(7f), out secondLegend);

            int differing = 0;
            for (int i = 0; i < first.cost.Length; i++)
            {
                if (first.cost[i] != second.cost[i] || first.binding[i] != second.binding[i]
                    || first.earthworkVolumeM3[i] != second.earthworkVolumeM3[i]
                    || first.bindingMeasure[i] != second.bindingMeasure[i]
                    || first.forbidden[i] != second.forbidden[i]) { differing++; }
            }
            Expect(differing == 0, "two builds of the same ground should agree cell for cell; " + differing
                + " cells differ", failures);
            Expect(firstLegend == secondLegend, "two builds should report the same legend", failures);

            SuitabilityRasterClass surface = Channel(20, 9, 4);
            CorridorPlan once = CorridorSolver.Solve(surface, 0, 4, 19, 4, 0.05);
            CorridorPlan twice = CorridorSolver.Solve(surface, 0, 4, 19, 4, 0.05);
            bool samePath = once.pathCells.Count == twice.pathCells.Count;
            for (int i = 0; samePath && i < once.pathCells.Count; i++)
            {
                if (once.pathCells[i] != twice.pathCells[i]) { samePath = false; }
            }
            Expect(samePath, "two solves of the same surface should return the same path, cell for cell", failures);
            Expect(once.pathCost == twice.pathCost, "and the same cost to the last bit; " + Num(once.pathCost)
                + " then " + Num(twice.pathCost), failures);
        }

        /// <summary> Swapping the ends returns the same corridor. The band is built from two independent accumulations and
        /// a step is charged at the AVERAGE of the two cells it joins, which is what makes the arithmetic symmetric; a
        /// solver charging the whole step at its destination would give A-to-B and B-to-A two different bands and an
        /// author two answers to the same question. The channel fixture has a unique optimum, so the path must come back
        /// exactly reversed rather than merely equal in cost. </summary>
        private static void CaseSwappingTheEndsGivesTheSameCorridor(List<string> failures)
        {
            SuitabilityRasterClass surface = Channel(20, 9, 4);
            CorridorPlan there = CorridorSolver.Solve(surface, 0, 4, 19, 4, 0.05);
            CorridorPlan back = CorridorSolver.Solve(surface, 19, 4, 0, 4, 0.05);

            int bandDiffers = 0;
            for (int i = 0; i < there.inCorridor.Length; i++)
            {
                if (there.inCorridor[i] != back.inCorridor[i]) { bandDiffers++; }
            }
            Expect(bandDiffers == 0, "the corridor should not depend on which end was asked first; " + bandDiffers
                + " cells differ", failures);
            Expect(System.Math.Abs(there.minimumTotal - back.minimumTotal) < 1e-9,
                "the cheapest through-cost should be the same in both directions", failures);
            Expect(System.Math.Abs(there.pathCost - back.pathCost) < 1e-9,
                "the path should cost the same in both directions; " + Num(there.pathCost) + " against "
                + Num(back.pathCost), failures);

            bool reversed = there.pathCells.Count == back.pathCells.Count;
            for (int i = 0; reversed && i < there.pathCells.Count; i++)
            {
                if (there.pathCells[i] != back.pathCells[back.pathCells.Count - 1 - i]) { reversed = false; }
            }
            Expect(reversed, "on a surface with one cheapest route, the return path should be that route reversed",
                failures);
        }
        #endregion

        #region "Honesty about sources, and creating nothing"
        /// <summary> Water for the west half of the world and nothing for the east. Gate-local because RM-74 ships no
        /// water source at all -- the city layer has no hydrology of its own -- and a factor with no implementation
        /// anywhere is exactly the one that has to be proved wired. </summary>
        private sealed class WetWesternHalf : ISuitabilityWaterSource
        {
            public double WaterFractionAt(double worldXMetres, double worldZMetres)
            {
                return worldXMetres < 192.0 ? 1.0 : 0.0;
            }
        }

        /// <summary> A factor with no source is NAMED as unused, and a factor with one really is read. This is the case
        /// that stops the overlay lying by omission: a five-factor map drawn from two factors is pixel-identical to one
        /// drawn from five, so the legend has to say which. The second half proves the legend is not merely optimistic --
        /// flooding the western half of flat ground must move that ground's cost and name water as the reason. </summary>
        private static void CaseUnsourcedFactorsAreNamedInTheLegend(List<string> failures)
        {
            string bare;
            BuildOn(LocalStreet(), Ramp(0f), out bare);
            Expect(bare.Contains("gradient yes, earthwork yes"),
                "the two factors that always have ground should be reported as sourced: " + bare, failures);
            Expect(bare.Contains("water no source") && bare.Contains("land use no source")
                && bare.Contains("existing network no source"),
                "a factor with nothing behind it should be named as unsourced: " + bare, failures);

            string wet;
            SuitabilityRasterClass flooded = SuitabilityMapBuilder.Build(24, 24, 16f, 0.0, 0.0, LocalStreet(),
                new SuitabilityWeights(), Ramp(0f), new WetWesternHalf(), null, null, out wet);
            int west = SuitabilityRaster.Index(flooded, 0, 12);
            int east = SuitabilityRaster.Index(flooded, 23, 12);

            Expect(wet.Contains("water yes"), "a sourced factor should be reported as sourced: " + wet, failures);
            Expect(flooded.cost[east] == 0f,
                "dry flat ground should resist nothing, resists " + Num(flooded.cost[east]), failures);
            Expect(flooded.cost[west] > flooded.cost[east],
                "ground a crossing would have to span should cost more than dry ground; " + Num(flooded.cost[west])
                + " against " + Num(flooded.cost[east]), failures);
            Expect(SuitabilityRaster.BindingAt(flooded, 0, 12) == SuitabilityFactor.Water,
                "water should be named as the constraint on flooded flat ground, named "
                + SuitabilityRaster.BindingAt(flooded, 0, 12), failures);
        }

        /// <summary> Nothing is created by any of it (RM-74, and § 10.3's ruling). Asserted three ways, because the
        /// promise can be broken at three depths. Structurally: the only door the builder has to the world is
        /// <see cref="ITerrainSurfaceReader"/>, which carries no write, no cut and no tunnel call, so a terrain change is
        /// not merely absent but unreachable. On the output side: <see cref="CorridorPlan"/> exposes no apply, commit or
        /// install, so a plan cannot install itself and the map stays advice. Empirically: a full build and solve leaves
        /// the open scene with the root objects it started with. </summary>
        private static void CaseNothingIsCreated(List<string> failures)
        {
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int before = scene.IsValid() ? scene.rootCount : -1;

            string ignored;
            SuitabilityRasterClass map = BuildOn(LocalStreet(), Ramp(9f), out ignored);
            CorridorPlan plan = CorridorSolver.Solve(map, 1, 1, 22, 22, 0.05);
            Expect(plan.reached, "the fixture solve should have run, so the count below means something", failures);

            int after = scene.IsValid() ? scene.rootCount : -1;
            Expect(before == after, "building a map and solving a corridor should create no scene object; "
                + before + " roots became " + after, failures);
            Expect(NoMethodNamedLike(typeof(ITerrainSurfaceReader),
                new string[] { "Write", "Notify", "Request", "Release", "Set", "Create" }),
                "the reader the builder samples through should carry no way to change the world", failures);
            Expect(NoMethodNamedLike(typeof(CorridorPlan),
                new string[] { "Apply", "Commit", "Install", "Create", "Write", "Spawn" }),
                "a corridor plan should have no way to install itself", failures);
        }

        /// <summary> Whether a type's public methods avoid a set of verbs. Reflection rather than a source scan, so it
        /// still holds after a rename or across a partial class. </summary>
        private static bool NoMethodNamedLike(System.Type type, string[] verbs)
        {
            System.Reflection.MethodInfo[] methods = type.GetMethods();
            for (int i = 0; i < methods.Length; i++)
            {
                for (int v = 0; v < verbs.Length; v++)
                {
                    if (methods[i].Name.Contains(verbs[v])) { return false; }
                }
            }
            return true;
        }
        #endregion
    }
}
#endif
