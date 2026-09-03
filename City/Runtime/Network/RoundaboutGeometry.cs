#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-28's authored roundabout geometry, every value taken from NCHRP Report 672,
    /// *Roundabouts: An Informational Guide* (the AASHTO-aligned reference the spec cites) rather than chosen to
    /// look plausible at editor zoom. Defaults describe a single-lane roundabout. A roundabout is NOT "a circle
    /// where roads meet"; these are the tabulated quantities that make the geometry function. </summary>
    [Serializable]
    public sealed class RoundaboutClass
    {
        /// <summary> Inscribed circle diameter (ICD): the outer circle the legs trim to. Single-lane roundabouts
        /// sit in the 27-46 m range in NCHRP 672 (mini-roundabouts 13-27 m); 40 m is a mid-range single-lane value
        /// sized for a small truck design vehicle. </summary>
        public float inscribedCircleDiameterMetres = 40f;

        /// <summary> Entry width of a single approach lane, NCHRP single-lane range ~4.0-5.0 m. Exact decimal
        /// (double) for consistency with the derived circulatory width. </summary>
        public double entryWidthMetres = 4.5;

        /// <summary> The circulatory carriageway width is derived from the entry width by this factor; NCHRP gives
        /// circulatory width as roughly 1.0-1.2 x the entry width for a single lane so a design vehicle can track
        /// around. Held as an exact decimal (double): the ratio the guide states is 1.1, whereas a float 1.1f is
        /// 1.10000002... and drove the derived circulatory width ~3e-7 m off factor x entry width. </summary>
        public double circulatoryWidthFactor = 1.1;

        /// <summary> Entry radius, NCHRP single-lane typical 10-30 m; controls how sharply a driver turns in. </summary>
        public float entryRadiusMetres = 20f;

        /// <summary> Exit radius, kept at or above the entry radius so leaving is not tighter than entering
        /// (NCHRP guidance); within the same 10-30 m band. </summary>
        public float exitRadiusMetres = 25f;

        /// <summary> Minimum splitter-island length on each leg. NCHRP recommends at least 15 m, desirably ~30 m, so
        /// the island can channel and shelter a crossing pedestrian. </summary>
        public float minSplitterIslandLengthMetres = 15f;

        /// <summary> The maximum fastest-path radius R1 the entry may allow. NCHRP ties the entry design speed to
        /// R1 -- a larger R1 means a straighter, faster through path -- so capping R1 is what forces the deflection
        /// that slows a driver. 100 m is an AUTHORED single-lane cap consistent with a low entry design speed. </summary>
        public float maxFastestPathRadiusMetres = 100f;

        /// <summary> Minimum angular separation between adjacent legs. Below this two entries cannot both have clean
        /// geometry and the builder must refuse. AUTHORED at 45 deg from NCHRP's "space entries as evenly as
        /// possible" guidance (four even legs are 90 deg apart; 45 deg is a practical floor). </summary>
        public float minLegSeparationDeg = 45f;

        /// <summary> The circulatory carriageway is one plane; each leg's approach gradient must adjust to meet it.
        /// If that adjustment exceeds this, the leg kinks at the yield line -- the same defect RM-27 removes -- and
        /// the junction is flagged. AUTHORED at 4 % as a gentle approach-gradient limit near a junction. </summary>
        public float maxApproachGradientChangePercent = 4f;

        /// <summary> The tightest circulatory centreline radius the geometry may reach anywhere on the loop. A
        /// circular roundabout has one radius and satisfying it is trivial; a shape-flexible one (RM-61: ellipse or
        /// control polygon) has a DIFFERENT local radius at every station, and the tight end of an oval is where a
        /// design vehicle stops being able to track around. AUTHORED at 12 m, derived from NCHRP 672's smallest
        /// single-lane inscribed circle diameter of 32 m: subtract the two circulatory widths and the circulating
        /// centreline sits at roughly 13.5 m, so 12 m is just inside the smallest published single-lane geometry and
        /// refuses anything tighter than a mini-roundabout. </summary>
        public float minCirculatoryRadiusMetres = 12f;

        /// <summary> Circulatory carriageway cross-slope, as a decimal, sloping OUTWARD away from the central island
        /// (NCHRP 672 recommends about 2 % outward: it drains the loop away from the island, keeps the outer edge
        /// from ponding, and holds the circulatory plane visibly flat). For a vehicle circulating around the island
        /// that outward slope is ADVERSE superelevation, so the implied circulatory speed is solved with e negated --
        /// which is exactly why circulating speeds come out low. </summary>
        public double circulatoryCrossSlopeOutward = 0.02;

        /// <summary> How far an entry may deviate from square-on to the circulatory way it meets, in degrees. NCHRP 672
        /// wants legs aligned radially to the inscribed circle: a leg that arrives shallow lets a driver merge at speed
        /// without ever turning, and one that arrives past square forces a hook. On a circle "radial" and "square to
        /// the tangent" are the same thing; on RM-61's ellipse or fitted polygon they are NOT, and the angle that
        /// matters is the one against the LOCAL tangent at the entry point. AUTHORED at 30 deg either way. </summary>
        public float maxEntrySkewFromPerpendicularDeg = 30f;

        public static RoundaboutClass Default()
        {
            return new RoundaboutClass();
        }
    }


    /// <summary> RM-28's verifiable core: the pure NCHRP-672 SIZING of a roundabout and the two refusal paths the
    /// spec insists on -- everything that can be checked without a scene. The scene geometry itself (trimming legs
    /// to the inscribed circle, the closed circulatory carriageway, the raised island mesh) is deferred, and the
    /// STRUCTURAL FORK the spec demanded be settled by reading `SplineC.cs` is settled: SplineC does NOT support a
    /// closed loop (its `isClosed` path is a hard-coded false with only a vestigial branch), so the circulatory way
    /// must be built as arc segments or generated mesh directly, never as a closed SplineC road. That is recorded
    /// for the deferred geometry half; here we own the numbers and the rejections.
    ///
    /// Sized quantities, each from the cited guide: circulatory carriageway width from entry width; central island
    /// diameter from ICD minus the two circulatory widths; entry/exit radii and splitter-island length carried as
    /// authored ranges. Refusal paths: legs closer than the minimum separation angle are REFUSED (crossed geometry
    /// otherwise), and an entry whose fastest-path radius exceeds the cap fails the DEFLECTION requirement (a
    /// straight fast path that decorates rather than functions). Elevation: the legs resolve to ONE circulatory
    /// plane, and a leg whose approach gradient change to reach it exceeds the limit is flagged, not kinked. </summary>
    public static class RoundaboutGeometry
    {
        /// <summary> Circulatory carriageway width = factor x entry width (NCHRP single-lane relation). </summary>
        public static double CirculatoryWidthMetres(RoundaboutClass cls)
        {
            return cls.circulatoryWidthFactor * cls.entryWidthMetres;
        }

        /// <summary> Central island diameter = inscribed circle diameter minus the two circulatory widths. The
        /// island is the raised centre; a non-positive result means the ICD is too small for the chosen circulatory
        /// width and the design is invalid. </summary>
        public static double CentralIslandDiameterMetres(RoundaboutClass cls)
        {
            return cls.inscribedCircleDiameterMetres - 2.0 * CirculatoryWidthMetres(cls);
        }

        /// <summary> Whether the sized central island is valid: a positive diameter, i.e. the circulatory ring fits
        /// inside the inscribed circle with island left over. </summary>
        public static bool CentralIslandIsValid(RoundaboutClass cls)
        {
            return CentralIslandDiameterMetres(cls) > 0.0;
        }

        /// <summary> The outcome of checking that the legs are angularly far enough apart. Carries the worst
        /// (smallest) separation found and the minimum required, so a refusal can report the offending angle. </summary>
        public struct LegSeparationCheck
        {
            public bool accepted;
            public double worstSeparationDeg;
            public double minRequiredDeg;
        }

        /// <summary> Check the angular separation of the legs given their bearings in degrees. Sorts the bearings,
        /// measures every adjacent gap around the full circle (including the wrap from the last back to the first),
        /// and refuses if the smallest gap is below the class minimum. Fewer than two legs is trivially accepted. </summary>
        public static LegSeparationCheck CheckLegSeparations(IList<double> legBearingsDeg, RoundaboutClass cls)
        {
            LegSeparationCheck r = new LegSeparationCheck();
            r.minRequiredDeg = cls.minLegSeparationDeg;
            r.worstSeparationDeg = 360.0;
            r.accepted = true;
            if (legBearingsDeg == null || legBearingsDeg.Count < 2) { return r; }

            List<double> bearings = new List<double>(legBearingsDeg.Count);
            for (int i = 0; i < legBearingsDeg.Count; i++)
            {
                double b = legBearingsDeg[i] % 360.0;
                if (b < 0.0) { b += 360.0; }
                bearings.Add(b);
            }
            bearings.Sort();

            double worst = 360.0;
            for (int i = 0; i < bearings.Count; i++)
            {
                double next = (i + 1 < bearings.Count) ? bearings[i + 1] : bearings[0] + 360.0;
                double gap = next - bearings[i];
                if (gap < worst) { worst = gap; }
            }
            r.worstSeparationDeg = worst;
            r.accepted = worst >= cls.minLegSeparationDeg - 1e-9;
            return r;
        }

        /// <summary> Whether an entry deflects enough to slow a driver: its fastest-path radius R1 must be at or
        /// below the class cap. A larger R1 is a straighter, faster path and fails -- the deflection is what makes
        /// the roundabout function rather than decorate. A non-positive R1 (fully deflected) trivially passes. </summary>
        public static bool EntryDeflectionAdequate(double fastestPathRadiusMetres, RoundaboutClass cls)
        {
            if (fastestPathRadiusMetres <= 0.0) { return true; }
            return fastestPathRadiusMetres <= cls.maxFastestPathRadiusMetres;
        }

        /// <summary> Whether a splitter island on a leg is long enough to channel and shelter a crossing, per the
        /// class minimum. </summary>
        public static bool SplitterIslandAdequate(double lengthMetres, RoundaboutClass cls)
        {
            return lengthMetres >= cls.minSplitterIslandLengthMetres;
        }

        /// <summary> The result of resolving the legs to one circulatory plane: the chosen plane elevation, whether
        /// every leg can reach it within the approach-gradient limit, and the worst gradient change required. </summary>
        public struct PlaneResolve
        {
            public double planeElevationMetres;
            public bool allLegsWithinGradient;
            public double worstGradientChangePercent;
            public int worstLegIndex;
        }

        /// <summary> Resolve the single circulatory plane from the legs' elevations (the mean, so no leg is
        /// arbitrarily favoured) and check each leg can transition to it over its approach length within the
        /// gradient limit. A leg that cannot is FLAGGED with its gradient rather than kinked at the yield line.
        /// Approach lengths are supplied per leg; a non-positive length is treated as unable to absorb any change. </summary>
        public static PlaneResolve ResolveCirculatoryPlane(IList<double> legElevationsMetres,
            IList<double> legApproachLengthsMetres, RoundaboutClass cls)
        {
            PlaneResolve r = new PlaneResolve();
            r.planeElevationMetres = 0.0;
            r.allLegsWithinGradient = true;
            r.worstGradientChangePercent = 0.0;
            r.worstLegIndex = -1;
            if (legElevationsMetres == null || legElevationsMetres.Count == 0) { return r; }

            double sum = 0.0;
            for (int i = 0; i < legElevationsMetres.Count; i++) { sum += legElevationsMetres[i]; }
            r.planeElevationMetres = sum / legElevationsMetres.Count;

            for (int i = 0; i < legElevationsMetres.Count; i++)
            {
                double rise = Math.Abs(legElevationsMetres[i] - r.planeElevationMetres);
                double length = (legApproachLengthsMetres != null && i < legApproachLengthsMetres.Count)
                    ? legApproachLengthsMetres[i] : 0.0;
                double gradientPercent = length > 0.0 ? (rise / length) * 100.0
                    : (rise > 0.0 ? double.PositiveInfinity : 0.0);
                if (gradientPercent > r.worstGradientChangePercent)
                {
                    r.worstGradientChangePercent = gradientPercent;
                    r.worstLegIndex = i;
                }
            }
            r.allLegsWithinGradient = r.worstGradientChangePercent <= cls.maxApproachGradientChangePercent;
            return r;
        }
    }
}
