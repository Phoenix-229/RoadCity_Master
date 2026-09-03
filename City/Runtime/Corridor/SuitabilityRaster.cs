#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which factor decided a cell. Stored per cell so the overlay can NAME the binding constraint under the
    /// cursor instead of showing a blended colour nobody can act on: "slope 14 %, above the 8 % maximum for this class"
    /// teaches the author the terrain, while a heat map with no explanation gets switched off after a day. </summary>
    public enum SuitabilityFactor
    {
        /// <summary> Nothing binds: the cell is cheap for every factor. </summary>
        None = 0,

        /// <summary> Ground steeper than the class can climb, or steep enough to dominate the cost. </summary>
        Slope = 1,

        /// <summary> Water: a crossing costs what a structure costs, and the bank near it costs works. </summary>
        Water = 2,

        /// <summary> The cut and fill a road bench through this cell implies. </summary>
        Earthwork = 3,

        /// <summary> Land use or zoning resistance from the painted zones. </summary>
        LandUse = 4,

        /// <summary> Distance from what is already built: connecting to the existing network is cheaper. </summary>
        NetworkDistance = 5
    }


    /// <summary> RM-74's raster: an EDIT-TIME cost surface over the world's XZ extent, one cell holding everything the
    /// overlay and the corridor solver need.
    ///
    /// Geometry follows RM-44's mask exactly -- a world origin, a metres-per-cell size, a cell count -- because two
    /// rasters over the same world with two different addressing conventions is how an overlay ends up half a cell out
    /// from the mask it is meant to composite with. Cells are row-major, `index = cz * widthCells + cx`.
    ///
    /// Resolution is deliberately coarse. A corridor is a BAND, not a centreline, so 16 m cells answer the question the
    /// author is asking at a sixteenth of the memory a 4 m grid would cost, and § 10.3's ruling keeps the whole thing out
    /// of the player build: nothing at runtime reads this. </summary>
    [Serializable]
    public sealed class SuitabilityRasterClass
    {
        /// <summary> World X of the raster's minimum corner, in metres. </summary>
        public float originXMetres = 0f;

        /// <summary> World Z of the raster's minimum corner, in metres. </summary>
        public float originZMetres = 0f;

        /// <summary> Cell size in METRES, which is the number the inspector shows. A cell count on its own tells an
        /// author nothing about the ground it covers (RM-44's rule, and the same reason it is repeated here). </summary>
        public float metresPerCell = 16f;

        public int widthCells = 0;
        public int depthCells = 0;

        /// <summary> The road class this surface was built for. A different class is a different map -- a lane width and
        /// a maximum gradient both move every number in it -- so the class is stored WITH the raster rather than
        /// remembered by whoever built it. </summary>
        public int roadClassId = 0;

        /// <summary> Relative cost per cell, 0 = the cheapest ground this surface found, 1 = the most expensive ground
        /// still passable. Forbidden cells carry 1 as well and are told apart by <see cref="forbidden"/>: a cost of
        /// "impossible" as a very large number is how a least-cost search ends up routing through a cliff because the
        /// alternative was two cliffs. </summary>
        public float[] cost = new float[0];

        /// <summary> Ground slope per cell as a FRACTION (rise over run), kept because the readout quotes it and
        /// because it is the term the author is most often arguing with. </summary>
        public float[] slopeFraction = new float[0];

        /// <summary> Cubic metres of cut plus fill a road bench of this class through this cell implies. Real volume,
        /// not a score: it is what makes "expensive" mean something an estimator would recognise. </summary>
        public float[] earthworkVolumeM3 = new float[0];

        /// <summary> Which factor bound each cell, as a <see cref="SuitabilityFactor"/>. Stored as a byte because a
        /// world-sized raster of enums is otherwise four times the size for no gain. </summary>
        public byte[] binding = new byte[0];

        /// <summary> The binding factor's OWN measurement, in that factor's own unit -- a slope fraction for gradient,
        /// cubic metres per metre for earthwork, a wetted fraction for water, a plot ratio for land use, metres for
        /// distance to the network.
        ///
        /// One number per cell rather than five, because the readout only ever quotes the constraint that actually bound
        /// the cell, and storing all five would quintuple a world-sized raster to print four figures nobody asked for.
        /// It exists at all because the alternative is a readout that names a factor and then quotes zero for it, which
        /// is worse than saying nothing: it is a sentence that reads as a measurement and is not one. </summary>
        public float[] bindingMeasure = new float[0];

        /// <summary> Ground the class cannot climb. Advisory even here: RM-74's rule is that forbidden ground WARNS at
        /// draw time and never blocks, because a road may climb a wall in a game -- it just should not do so by
        /// accident. </summary>
        public bool[] forbidden = new bool[0];

        public SuitabilityRasterClass() { }

        public SuitabilityRasterClass(int widthCells, int depthCells, float metresPerCell)
        {
            this.widthCells = widthCells < 0 ? 0 : widthCells;
            this.depthCells = depthCells < 0 ? 0 : depthCells;
            this.metresPerCell = metresPerCell <= 0f ? 1f : metresPerCell;
            int count = this.widthCells * this.depthCells;
            cost = new float[count];
            slopeFraction = new float[count];
            earthworkVolumeM3 = new float[count];
            binding = new byte[count];
            bindingMeasure = new float[count];
            forbidden = new bool[count];
        }
    }


    /// <summary> Addressing and sampling for <see cref="SuitabilityRasterClass"/>. Pure functions, no engine calls, so
    /// the gate can build a surface and interrogate it headless. </summary>
    public static class SuitabilityRaster
    {
        public static double WorldWidthMetres(SuitabilityRasterClass m)
        {
            return m == null ? 0.0 : (double)m.widthCells * m.metresPerCell;
        }

        public static double WorldDepthMetres(SuitabilityRasterClass m)
        {
            return m == null ? 0.0 : (double)m.depthCells * m.metresPerCell;
        }

        public static int CellCount(SuitabilityRasterClass m)
        {
            return m == null ? 0 : m.widthCells * m.depthCells;
        }

        /// <summary> Row-major index, or -1 when the cell is off the raster. Off-raster is a QUESTION, not a cell:
        /// wrapping it would answer with ground from the far side of the world. </summary>
        public static int Index(SuitabilityRasterClass m, int cellX, int cellZ)
        {
            if (m == null) { return -1; }
            if (cellX < 0 || cellZ < 0 || cellX >= m.widthCells || cellZ >= m.depthCells) { return -1; }
            return cellZ * m.widthCells + cellX;
        }

        /// <summary> The cell a world point falls in, CLAMPED to the raster rather than wrapped (RM-44's rule). Returns
        /// false when the point was outside, so a caller that cares can say so instead of silently reading the edge.
        /// </summary>
        public static bool CellAt(SuitabilityRasterClass m, double worldXMetres, double worldZMetres,
            out int cellX, out int cellZ)
        {
            cellX = 0;
            cellZ = 0;
            if (m == null || m.widthCells <= 0 || m.depthCells <= 0) { return false; }

            double fx = (worldXMetres - m.originXMetres) / m.metresPerCell;
            double fz = (worldZMetres - m.originZMetres) / m.metresPerCell;
            int ix = (int)Math.Floor(fx);
            int iz = (int)Math.Floor(fz);
            bool inside = ix >= 0 && iz >= 0 && ix < m.widthCells && iz < m.depthCells;

            cellX = ix < 0 ? 0 : (ix >= m.widthCells ? m.widthCells - 1 : ix);
            cellZ = iz < 0 ? 0 : (iz >= m.depthCells ? m.depthCells - 1 : iz);
            return inside;
        }

        /// <summary> World X of a cell's centre. The centre rather than the corner, because every cost in the cell was
        /// measured for the ground the centre stands on. </summary>
        public static double CellCentreXMetres(SuitabilityRasterClass m, int cellX)
        {
            return m == null ? 0.0 : m.originXMetres + ((double)cellX + 0.5) * m.metresPerCell;
        }

        public static double CellCentreZMetres(SuitabilityRasterClass m, int cellZ)
        {
            return m == null ? 0.0 : m.originZMetres + ((double)cellZ + 0.5) * m.metresPerCell;
        }

        /// <summary> Cost at a world point, or 1 (the most expensive passable value) off the raster: an unmapped cell is
        /// not free ground. </summary>
        public static double CostAt(SuitabilityRasterClass m, double worldXMetres, double worldZMetres)
        {
            int cx;
            int cz;
            if (!CellAt(m, worldXMetres, worldZMetres, out cx, out cz)) { return 1.0; }
            int i = Index(m, cx, cz);
            return i < 0 || m.cost == null || i >= m.cost.Length ? 1.0 : m.cost[i];
        }

        /// <summary> Whether a world point is on ground the class cannot climb. Off the raster is NOT forbidden -- the
        /// tool has no evidence there, and inventing a refusal from missing data is worse than saying nothing.
        /// </summary>
        public static bool ForbiddenAt(SuitabilityRasterClass m, double worldXMetres, double worldZMetres)
        {
            int cx;
            int cz;
            if (!CellAt(m, worldXMetres, worldZMetres, out cx, out cz)) { return false; }
            int i = Index(m, cx, cz);
            return i >= 0 && m.forbidden != null && i < m.forbidden.Length && m.forbidden[i];
        }

        public static SuitabilityFactor BindingAt(SuitabilityRasterClass m, int cellX, int cellZ)
        {
            int i = Index(m, cellX, cellZ);
            if (i < 0 || m.binding == null || i >= m.binding.Length) { return SuitabilityFactor.None; }
            return (SuitabilityFactor)m.binding[i];
        }

        /// <summary> The size line the inspector shows: cells AND the ground they cover, in metres per cell, never in
        /// pixels alone. </summary>
        public static string SizeLine(SuitabilityRasterClass m)
        {
            if (m == null) { return "no suitability raster"; }
            return m.widthCells.ToString(CultureInfo.InvariantCulture) + " x "
                + m.depthCells.ToString(CultureInfo.InvariantCulture) + " cells at "
                + m.metresPerCell.ToString("0.##", CultureInfo.InvariantCulture) + " m per cell, covering "
                + WorldWidthMetres(m).ToString("0", CultureInfo.InvariantCulture) + " m x "
                + WorldDepthMetres(m).ToString("0", CultureInfo.InvariantCulture) + " m";
        }
    }
}
