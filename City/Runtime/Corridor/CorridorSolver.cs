#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-74's solver: the cheapest way across a cost surface, and the BAND of ways that are nearly as cheap.
    ///
    /// Two products, from the same arithmetic. The least-cost path is one line and is what an author would draw if they
    /// agreed with every weight; the corridor is the set of ground within a stated tolerance of that line's cost, and it
    /// is the more useful of the two -- a single line implies a precision the inputs do not have, while a band says "the
    /// road belongs in here, and where exactly is your decision". That is why RM-74 asks for a corridor at all rather
    /// than an alignment.
    ///
    /// Nothing here creates anything. The output is cell indices and a boolean band; no node, no edge, no spline, no
    /// terrain write. An author who runs this and then closes the window has changed nothing.
    ///
    /// DETERMINISM (C-06) is the hard part of any shortest-path implementation and is dealt with in one place: the
    /// frontier compares on accumulated cost and, when two cells tie to the last bit, on cell INDEX -- never on
    /// insertion order, never on a hash. Two runs over the same surface therefore return the same path, cell for cell,
    /// which is what makes the band worth showing an author twice. </summary>
    public static class CorridorSolver
    {
        /// <summary> The eight neighbour offsets, in a FIXED order. The order is part of the contract, not an accident of
        /// how the loop was typed: with an index tie-break below it is not needed for determinism, but a fixed order
        /// keeps the relaxation sequence itself reproducible, which is what makes a failing gate reproducible too.
        /// Cardinals first, then diagonals. </summary>
        private static readonly int[] NeighbourX = { 1, 0, -1, 0, 1, 1, -1, -1 };
        private static readonly int[] NeighbourZ = { 0, 1, 0, -1, 1, -1, 1, -1 };

        /// <summary> Unreached ground. Not <see cref="float.MaxValue"/>: sums against MaxValue overflow silently and a
        /// path can then appear to lead through unreachable cells. A negative sentinel cannot be produced by any real
        /// accumulation, since every cell costs at least 1 per metre. </summary>
        public const double Unreached = -1.0;

        /// <summary> Diagonal steps cost root two times a cardinal step, because they are root two times as long. Stating
        /// it once means the two step lengths cannot drift apart, which is the usual way a grid solver ends up preferring
        /// staircases to straight lines. </summary>
        public static double StepMetres(SuitabilityRasterClass map, int neighbour)
        {
            double side = map != null ? map.metresPerCell : 1.0;
            bool diagonal = NeighbourX[neighbour] != 0 && NeighbourZ[neighbour] != 0;
            return diagonal ? side * Math.Sqrt(2.0) : side;
        }

        /// <summary> The cost of stepping between two adjacent cells: the average of the two cells' per-metre costs,
        /// times the length of the step. Averaging is the standard cost-distance formulation and it is the right one --
        /// charging the whole step at the destination's rate would make a path's cost depend on which end you solved
        /// from, and a corridor built from two ends would then not be symmetric.
        ///
        /// Forbidden ground is traversable HERE and merely dear, which is deliberate and is RM-74's rule: the map warns
        /// and never refuses. A solver that treated it as impassable would silently return no path at all across a
        /// ridge, and an author would be told nothing rather than told the price. </summary>
        public static double StepCost(SuitabilityRasterClass map, int fromIndex, int toIndex, int neighbour)
        {
            double a = SuitabilityFactors.CellCost(map.cost[fromIndex], map.forbidden[fromIndex]);
            double b = SuitabilityFactors.CellCost(map.cost[toIndex], map.forbidden[toIndex]);
            return StepMetres(map, neighbour) * 0.5 * (a + b);
        }

        /// <summary> Cost-distance from one cell to every cell it can reach: Dijkstra over the eight-neighbour grid,
        /// with the predecessor of each cell recorded so a path can be traced back without a second search.
        ///
        /// Dijkstra rather than A*: there is no admissible heuristic worth having here, because the cheapest possible
        /// step is one metre of easy ground and a straight-line estimate against that bound prunes almost nothing on a
        /// surface whose costs span 1 to 6. It would add a tie-break nobody can reason about for no measurable gain.
        /// </summary>
        public static double[] Accumulate(SuitabilityRasterClass map, int startCellX, int startCellZ,
            out int[] predecessor)
        {
            int count = SuitabilityRaster.CellCount(map);
            double[] accumulated = new double[count];
            int[] previous = new int[count];
            for (int i = 0; i < count; i++) { accumulated[i] = Unreached; previous[i] = -1; }
            predecessor = previous;

            int start = SuitabilityRaster.Index(map, startCellX, startCellZ);
            if (start < 0) { return accumulated; }

            bool[] settled = new bool[count];
            Frontier frontier = new Frontier(count + 1);
            accumulated[start] = 0.0;
            frontier.Push(start, 0.0);

            int cell;
            double cost;
            while (frontier.TryPop(out cell, out cost))
            {
                if (settled[cell]) { continue; }
                settled[cell] = true;

                int cx = cell % map.widthCells;
                int cz = cell / map.widthCells;
                for (int n = 0; n < NeighbourX.Length; n++)
                {
                    int nx = cx + NeighbourX[n];
                    int nz = cz + NeighbourZ[n];
                    int next = SuitabilityRaster.Index(map, nx, nz);
                    if (next < 0 || settled[next]) { continue; }

                    double candidate = cost + StepCost(map, cell, next, n);
                    if (accumulated[next] >= 0.0 && candidate >= accumulated[next]) { continue; }
                    accumulated[next] = candidate;
                    previous[next] = cell;
                    frontier.Push(next, candidate);
                }
            }
            return accumulated;
        }

        /// <summary> The least-cost path as a list of cell indices from the start cell to the end cell, traced backwards
        /// through the predecessors and then reversed so it reads in travel order. Returns false when the end was never
        /// reached, which on a grid this connected means the end was off the raster -- and says so rather than returning
        /// a plausible half-path. </summary>
        public static bool TracePath(SuitabilityRasterClass map, double[] accumulated, int[] predecessor,
            int endCellX, int endCellZ, out List<int> pathCells, out double pathCost)
        {
            pathCells = new List<int>();
            pathCost = 0.0;

            int end = SuitabilityRaster.Index(map, endCellX, endCellZ);
            if (end < 0 || accumulated == null || predecessor == null) { return false; }
            if (end >= accumulated.Length || accumulated[end] < 0.0) { return false; }

            pathCost = accumulated[end];
            int walk = end;
            int guard = predecessor.Length + 1;
            while (walk >= 0 && guard > 0)
            {
                pathCells.Add(walk);
                walk = predecessor[walk];
                guard--;
            }
            pathCells.Reverse();
            return true;
        }

        /// <summary> The corridor: every cell whose total cost -- reaching it from one end plus reaching it from the
        /// other -- is within a tolerance of the cheapest such total.
        ///
        /// This is the standard corridor-analysis construction and it is worth saying why it is the right one. The sum
        /// of the two cost-distance surfaces is, at every cell, the cost of the cheapest route from A to B THAT PASSES
        /// THROUGH THAT CELL. Its minimum is the least-cost path's own cost, and the cells within a few per cent of that
        /// minimum are exactly the ground a route could use without meaningfully paying more. So the band is not a buffer
        /// drawn around a line -- it is a statement about cost, and a place where the band is wide is genuinely a place
        /// where the alignment does not matter much.
        ///
        /// The tolerance is a FRACTION of the minimum rather than an absolute cost, because a 40 km route and a 400 m one
        /// do not deserve the same slack in metres, and an author thinking "within 5 % of optimal" is thinking in the
        /// unit that transfers between them. </summary>
        public static bool[] Corridor(SuitabilityRasterClass map, double[] fromA, double[] fromB,
            double toleranceFraction, out double minimumTotal, out int cellsInCorridor)
        {
            int count = SuitabilityRaster.CellCount(map);
            bool[] inCorridor = new bool[count];
            minimumTotal = Unreached;
            cellsInCorridor = 0;
            if (fromA == null || fromB == null || fromA.Length != count || fromB.Length != count)
            { return inCorridor; }

            for (int i = 0; i < count; i++)
            {
                if (fromA[i] < 0.0 || fromB[i] < 0.0) { continue; }
                double total = fromA[i] + fromB[i];
                if (minimumTotal < 0.0 || total < minimumTotal) { minimumTotal = total; }
            }
            if (minimumTotal < 0.0) { return inCorridor; }

            double slack = toleranceFraction > 0.0 ? toleranceFraction : 0.0;
            double ceiling = minimumTotal * (1.0 + slack);
            for (int i = 0; i < count; i++)
            {
                if (fromA[i] < 0.0 || fromB[i] < 0.0) { continue; }
                if (fromA[i] + fromB[i] > ceiling) { continue; }
                inCorridor[i] = true;
                cellsInCorridor++;
            }
            return inCorridor;
        }

        /// <summary> The frontier: a binary min-heap of cells keyed on accumulated cost, with the CELL INDEX as the
        /// tie-break. The tie-break is the whole reason this is written out rather than borrowed from a sorted set.
        ///
        /// Two cells with identical accumulated cost are common on a cost surface -- flat ground produces them by the
        /// thousand -- and which of them is expanded first decides which of several equally cheap paths is the one
        /// reported. A comparison on cost alone leaves that to whatever order the heap happens to be in, so the same
        /// surface can yield two different (equally valid) paths on two runs, and an author watching the overlay flicker
        /// between them has no way to tell an ordering artefact from a real change. Comparing on index second makes the
        /// choice arbitrary but FIXED, which is what C-06 asks for.
        ///
        /// Lazy deletion rather than decrease-key: a cell can be pushed more than once and the stale entries are
        /// discarded when popped, which the settled array above does. That trades a little memory for not needing a
        /// position index, and on a grid where each cell has eight neighbours the bound is small and known. </summary>
        private sealed class Frontier
        {
            private int[] cells;
            private double[] costs;
            private int size;

            public Frontier(int capacity)
            {
                int initial = capacity < 8 ? 8 : capacity;
                cells = new int[initial];
                costs = new double[initial];
                size = 0;
            }

            public void Push(int cell, double cost)
            {
                if (size == cells.Length) { Grow(); }
                cells[size] = cell;
                costs[size] = cost;
                SiftUp(size);
                size++;
            }

            public bool TryPop(out int cell, out double cost)
            {
                if (size == 0) { cell = -1; cost = 0.0; return false; }
                cell = cells[0];
                cost = costs[0];
                size--;
                if (size > 0)
                {
                    cells[0] = cells[size];
                    costs[0] = costs[size];
                    SiftDown(0);
                }
                return true;
            }

            /// <summary> True when the entry at <paramref name="a"/> must come out before the one at
            /// <paramref name="b"/>: cheaper first, and on an exact tie the lower cell index first. </summary>
            private bool Precedes(int a, int b)
            {
                if (costs[a] < costs[b]) { return true; }
                if (costs[a] > costs[b]) { return false; }
                return cells[a] < cells[b];
            }

            private void SiftUp(int at)
            {
                while (at > 0)
                {
                    int parent = (at - 1) / 2;
                    if (!Precedes(at, parent)) { break; }
                    Swap(at, parent);
                    at = parent;
                }
            }

            private void SiftDown(int at)
            {
                while (true)
                {
                    int left = at * 2 + 1;
                    if (left >= size) { break; }
                    int best = left;
                    int right = left + 1;
                    if (right < size && Precedes(right, left)) { best = right; }
                    if (!Precedes(best, at)) { break; }
                    Swap(at, best);
                    at = best;
                }
            }

            private void Swap(int a, int b)
            {
                int cell = cells[a]; cells[a] = cells[b]; cells[b] = cell;
                double cost = costs[a]; costs[a] = costs[b]; costs[b] = cost;
            }

            private void Grow()
            {
                int[] biggerCells = new int[cells.Length * 2];
                double[] biggerCosts = new double[costs.Length * 2];
                Array.Copy(cells, biggerCells, size);
                Array.Copy(costs, biggerCosts, size);
                cells = biggerCells;
                costs = biggerCosts;
            }
        }

        /// <summary> How much dearer than optimal a route may be and still count as inside the corridor. AUTHORED at
        /// 5 %: wide enough that the band shows the real freedom an alignment has where the ground is indifferent, tight
        /// enough that it does not swallow a genuinely worse route on the far side of a ridge. </summary>
        public const double DefaultToleranceFraction = 0.05;

        /// <summary> The whole analysis in one call: cost-distance from each end, the least-cost path between them, and
        /// the corridor band around it. Two accumulations rather than one, because the band needs both -- and because
        /// solving from each end independently is what makes the result symmetric, so swapping the two endpoints
        /// returns the same corridor rather than a mirror of a different one. </summary>
        public static CorridorPlan Solve(SuitabilityRasterClass map, int fromCellX, int fromCellZ, int toCellX,
            int toCellZ, double toleranceFraction)
        {
            CorridorPlan plan = new CorridorPlan();
            plan.toleranceFraction = toleranceFraction > 0.0 ? toleranceFraction : DefaultToleranceFraction;
            if (map == null || SuitabilityRaster.CellCount(map) == 0) { return plan; }

            int[] predecessor;
            double[] fromA = Accumulate(map, fromCellX, fromCellZ, out predecessor);
            int[] unusedPredecessor;
            double[] fromB = Accumulate(map, toCellX, toCellZ, out unusedPredecessor);

            plan.reached = TracePath(map, fromA, predecessor, toCellX, toCellZ, out plan.pathCells, out plan.pathCost);
            plan.inCorridor = Corridor(map, fromA, fromB, plan.toleranceFraction, out plan.minimumTotal,
                out plan.cellsInCorridor);
            plan.forbiddenOnPath = CountForbidden(map, plan.pathCells);
            return plan;
        }

        /// <summary> How many cells of the reported path are ground this class cannot climb. Counted rather than
        /// prevented: this is the number the draw-time warning quotes, and it is the only thing forbidden ground does in
        /// RM-74 -- it is never a refusal. </summary>
        public static int CountForbidden(SuitabilityRasterClass map, List<int> pathCells)
        {
            if (map == null || pathCells == null) { return 0; }
            int forbidden = 0;
            for (int i = 0; i < pathCells.Count; i++)
            {
                int index = pathCells[i];
                if (index >= 0 && index < map.forbidden.Length && map.forbidden[index]) { forbidden++; }
            }
            return forbidden;
        }
    }


    /// <summary> What a corridor solve produced: a line, a band, and the numbers behind both.
    ///
    /// Deliberately inert. There is no Apply, no Commit and no reference to a road network on this type, because RM-74's
    /// output is advice -- an author reads it and then draws, or does not. A plan that could install itself would make
    /// "the map never creates anything" a promise in a comment rather than a property of the code. </summary>
    public sealed class CorridorPlan
    {
        /// <summary> Whether the far end was reachable at all. </summary>
        public bool reached = false;

        /// <summary> The least-cost path as cell indices, in travel order. </summary>
        public List<int> pathCells = new List<int>();

        /// <summary> The path's accumulated cost, in metres-of-cheapest-ground. </summary>
        public double pathCost = 0.0;

        /// <summary> Per cell: inside the corridor band or not. </summary>
        public bool[] inCorridor = new bool[0];

        public int cellsInCorridor = 0;

        /// <summary> The cheapest through-cost found anywhere, which is the least-cost path's own cost. </summary>
        public double minimumTotal = CorridorSolver.Unreached;

        public double toleranceFraction = CorridorSolver.DefaultToleranceFraction;

        /// <summary> Cells on the reported path that this class cannot climb. </summary>
        public int forbiddenOnPath = 0;

        /// <summary> The result in words, for a readout or a gate line. Every number carries its unit, and the
        /// tolerance is quoted so nobody has to guess how wide "the corridor" was drawn. </summary>
        public string Summary()
        {
            if (!reached)
            {
                return "no route: the far end could not be reached on this surface";
            }
            string line = "least-cost path " + pathCells.Count.ToString(CultureInfo.InvariantCulture)
                + " cells, cost " + pathCost.ToString("0.#", CultureInfo.InvariantCulture)
                + " metres of easiest ground; corridor " + cellsInCorridor.ToString(CultureInfo.InvariantCulture)
                + " cells within " + (toleranceFraction * 100.0).ToString("0.#", CultureInfo.InvariantCulture)
                + " % of optimal";
            if (forbiddenOnPath > 0)
            {
                line += "; WARNING " + forbiddenOnPath.ToString(CultureInfo.InvariantCulture)
                    + " cells on this path are steeper than the class can climb";
            }
            return line;
        }
    }
}
