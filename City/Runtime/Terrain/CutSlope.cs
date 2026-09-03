#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-39's AUTHORED cut-slope constants. A cut slope carries the ground up and outward from the road
    /// edge until it meets the natural hillside (the CATCH POINT; the line of catch points along the road is the
    /// DAYLIGHT LINE). Its width is not fixed -- it is whatever the ratio needs to reach ground -- so the only
    /// authored quantities are the slope RATIO, the MARCH STEP, the MAXIMUM slope height past which the tool hands
    /// the decision back rather than reshaping a whole hillside, and the shared END TAPER length. Values AUTHORED
    /// from ordinary earthworks practice (C-15), retunable per road. </summary>
    [Serializable]
    public sealed class CutSlopeClass
    {
        /// <summary> Cut-slope ratio as RUN per RISE (horizontal metres per vertical metre). A stable soil cut is
        /// commonly around 1.5:1; steeper rock cuts and flatter loose-soil cuts retune this. Must be positive. </summary>
        public float slopeRunPerRise = 1.5f;

        /// <summary> The outward march step; the catch point is found to this resolution. </summary>
        public float stepMetres = 0.5f;

        /// <summary> Maximum slope height (rise) permitted before the tool STOPS and reports the station, rather
        /// than running an unbounded slope up a steepening hillside -- real practice benches, retains (RM-34) or
        /// re-aligns instead, which is the author's decision, not this tool's. </summary>
        public float maxSlopeHeightMetres = 15f;

        /// <summary> Length over which the slope height tapers to zero at each end so the cut daylights out of the
        /// ground; the SAME shared end-taper the retaining face uses (RM-34), never a second copy. </summary>
        public float endTaperLengthMetres = 5f;

        public static CutSlopeClass Default()
        {
            return new CutSlopeClass();
        }
    }

    /// <summary> RM-39's verifiable core: the pure per-station, per-side outward march that finds where a cut slope
    /// meets natural ground, without a scene. From the edge of RM-38's levelled band the slope rises at the
    /// authored ratio; each step samples natural terrain and the march stops at the first step where the slope has
    /// caught the ground (the catch point) -- so the cut is exactly as wide as it needs to be, never a fixed apron.
    /// Where the catch never arrives within the authored maximum height, the march reports the station rather than
    /// emitting an unbounded slope. The end taper is the shared RM-34 operation. The swept slope mesh, the live
    /// natural-terrain sampling and the bench/retain/re-align hand-off are the deferred scene half. </summary>
    public static class CutSlope
    {
        /// <summary> The slope's rise after marching a given RUN outward from the road edge, at the authored ratio
        /// (rise = run / (run-per-rise)). A non-positive ratio yields no rise. </summary>
        public static double RiseForRunMetres(double runMetres, CutSlopeClass cls)
        {
            if (cls.slopeRunPerRise <= 0.0) { return 0.0; }
            return runMetres / cls.slopeRunPerRise;
        }

        /// <summary> The RUN needed for the slope to gain a given rise (the inverse), used to place a catch point. </summary>
        public static double RunForRiseMetres(double riseMetres, CutSlopeClass cls)
        {
            return riseMetres * cls.slopeRunPerRise;
        }

        /// <summary> The outcome of marching one station/side out to the catch point. </summary>
        public struct CatchResult
        {
            public bool caught;                 // the slope met natural ground within the authored max height
            public bool exceededMax;            // the slope hit the authored max height first -> hand back
            public double catchRunMetres;       // horizontal distance from the band edge to the catch point
            public double catchRiseMetres;      // slope height reached at the catch (or the max, if exceeded)
            public int catchStepIndex;          // which march step caught, or -1
        }

        /// <summary> March outward from the band edge, step by step, raising the slope by the ratio and comparing to
        /// natural terrain sampled at each step. Stops at the first step whose slope elevation has reached natural
        /// ground (the catch), or -- if the rise passes the authored maximum first -- stops UNCAUGHT and flags the
        /// station so the author can bench, retain or re-align. <paramref name="naturalHeightsFromBandEdge"/> is the
        /// natural terrain elevation sampled at step 1, 2, ... outward from the band edge (index 0 == step 1); it is
        /// sampled from UNMODIFIED terrain in station order, which is what makes the result deterministic (C-06).
        /// <paramref name="bandEdgeElevationMetres"/> is the road/levelled-band elevation the slope starts from. </summary>
        public static CatchResult MarchToCatch(double bandEdgeElevationMetres,
            IList<double> naturalHeightsFromBandEdge, CutSlopeClass cls)
        {
            CatchResult r = new CatchResult();
            r.catchStepIndex = -1;
            if (naturalHeightsFromBandEdge == null || naturalHeightsFromBandEdge.Count == 0
                || cls.slopeRunPerRise <= 0.0 || cls.stepMetres <= 0.0)
            {
                return r;
            }
            for (int i = 0; i < naturalHeightsFromBandEdge.Count; i++)
            {
                double run = (i + 1) * (double)cls.stepMetres;
                double rise = RiseForRunMetres(run, cls);
                double slopeElevation = bandEdgeElevationMetres + rise;

                if (rise > cls.maxSlopeHeightMetres)
                {
                    // Ran past the authored ceiling before catching: hand the decision back at this station.
                    r.exceededMax = true;
                    r.catchRunMetres = run;
                    r.catchRiseMetres = cls.maxSlopeHeightMetres;
                    r.catchStepIndex = i;
                    return r;
                }
                if (slopeElevation >= naturalHeightsFromBandEdge[i])
                {
                    // The slope has reached the hillside -- this is the catch point / daylight line.
                    r.caught = true;
                    r.catchRunMetres = run;
                    r.catchRiseMetres = rise;
                    r.catchStepIndex = i;
                    return r;
                }
            }
            // Marched off the sampled reach without catching and without hitting the ceiling: still unresolved.
            r.exceededMax = false;
            r.caught = false;
            return r;
        }

        /// <summary> The slope height at a station once the shared end taper is applied: the full catch rise scaled
        /// by RM-34's end-taper toward each end, so the catch point walks in to the road edge and the cut opens out
        /// of the ground instead of leaving a wall of earth across the road. </summary>
        public static double TaperedSlopeHeightMetres(double fullCatchRiseMetres, double distanceFromEndMetres,
            CutSlopeClass cls)
        {
            return EndTaper.TaperedDimensionMetres(fullCatchRiseMetres, distanceFromEndMetres,
                cls.endTaperLengthMetres);
        }
    }
}
