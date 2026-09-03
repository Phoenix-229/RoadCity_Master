#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-25's authored parameter block for a rail track type. Deliberately its OWN block, NOT wired to
    /// the road's <c>maxGradientPercent</c> or superelevation: real rail turns on radii an order of magnitude
    /// larger than road, climbs at gradients an order of magnitude gentler, and banks by CANT rather than highway
    /// superelevation, so sharing a number with the road system would let one system's tuning silently ruin the
    /// other's. Every value is AUTHORED against real mainline practice and cited (C-15). </summary>
    [Serializable]
    public sealed class RailTrackClass
    {
        /// <summary> Nominal track gauge. 1.435 m is international standard gauge (the running-edge to running-edge
        /// distance); it is the identity of the track type, not itself used in the cant formula. </summary>
        public float trackGaugeMetres = 1.435f;

        /// <summary> The gauge value the equilibrium-cant equation uses: the rail-centre to rail-centre distance,
        /// conventionally taken as 1500 mm for standard gauge (1435 mm nominal + ~65 mm across the two rail heads).
        /// Kept separate from <see cref="trackGaugeMetres"/> so the formula reads as the textbook one. </summary>
        public float cantFormulaGaugeMillimetres = 1500f;

        /// <summary> AUTHORED minimum horizontal curve radius. Mainline design minimums sit in the hundreds of
        /// metres (tramways go far tighter, high-speed lines far wider); 300 m is a defensible general-purpose
        /// mainline minimum. A curve tighter than this is REJECTED, never silently kinked. </summary>
        public float minimumRadiusMetres = 300f;

        /// <summary> AUTHORED ruling gradient, in per mille (‰) as rail engineering states it -- 15 ‰ = 1.5 %,
        /// a standard mainline ruling gradient. Rail climbs an order of magnitude gentler than road, which is why
        /// this must never be the road's percent value. </summary>
        public float maxGradientPerMille = 15f;

        /// <summary> AUTHORED maximum applied cant (superelevation of the outer rail), in mm. 150 mm is the common
        /// standard-gauge maximum; the track is banked by raising the outer rail up to this, no further. </summary>
        public float maxAppliedCantMillimetres = 150f;

        /// <summary> AUTHORED maximum permissible cant deficiency, in mm: how far the applied cant may fall short of
        /// the speed's equilibrium cant before the curve needs a speed restriction. ~110 mm is a common limit for
        /// conventional passenger stock. </summary>
        public float maxCantDeficiencyMillimetres = 110f;

        /// <summary> AUTHORED design speed for the line, km/h. Drives the equilibrium cant a curve wants. </summary>
        public float designSpeedKmh = 120f;

        public static RailTrackClass Default()
        {
            return new RailTrackClass();
        }
    }


    /// <summary> RM-25's verifiable core: the pure, scene-free rail-geometry decisions that carry the real maths and
    /// the rejection paths -- the part that can be proven headlessly. The rail body itself (two rails, sleepers, a
    /// ballast shoulder) is first of all a CROSS-SECTION swept along a <c>SplineC</c> backbone by the existing
    /// <c>SplinatedMeshMaker</c>, exactly as a road is: that sweep, the arc-length mapping and the terrain matching
    /// are the shared machinery and are NOT re-implemented here (no-duplication). What is genuinely different, and
    /// what lives here, is the constraint set: a minimum radius an order of magnitude larger than road, a gradient
    /// an order of magnitude gentler, and banking by CANT rather than highway superelevation.
    ///
    /// The equilibrium (balancing) cant for a curve follows the standard rail formula
    /// E = (G · V²) / (127 · R), with G the rail-centre gauge in mm, V the speed in km/h and R the radius in m --
    /// the direct analogue of road superelevation's e + f = V²/(127R), and the actual method rail engineers use,
    /// not a guess. Applied cant is that equilibrium value capped at the class maximum; the shortfall is the cant
    /// DEFICIENCY, which above its own limit means the curve needs a speed restriction. The rejection path matters
    /// as much as the geometry: a curve tighter than the minimum radius must REFUSE and name the radius it needed,
    /// because a kinked rail is the kind of error that survives all the way to a screenshot.
    ///
    /// SCOPE (stated, not deferred): this covers plain track and station placement only. A rail junction is a
    /// TURNOUT -- switch rails, a frog and a diverging route at a standardised angle -- which is not a road
    /// intersection and cannot come from the corner-blending <c>ThreeWay</c>/<c>FourWay</c> code; turnouts and
    /// level crossings are explicitly OUT of scope here and are their own task with their own geometry spec. </summary>
    public static class RailTrack
    {
        /// <summary> The equilibrium (balancing) cant in mm for a curve of the given radius at the given speed:
        /// E = (G · V²) / (127 · R). Returns 0 for a straight (non-positive or infinite radius) -- a straight track
        /// wants no cant. Deterministic: pure arithmetic, no clock or hash (C-06). </summary>
        public static double EquilibriumCantMillimetres(double speedKmh, double radiusMetres,
            double gaugeMillimetres)
        {
            if (radiusMetres <= 0.0 || double.IsInfinity(radiusMetres)) { return 0.0; }
            return (gaugeMillimetres * speedKmh * speedKmh) / (127.0 * radiusMetres);
        }

        /// <summary> The applied cant in mm: the equilibrium cant capped at the class maximum. The outer rail is
        /// raised by this and no more, so the track never banks past what the class allows. </summary>
        public static double AppliedCantMillimetres(double speedKmh, double radiusMetres, RailTrackClass cls)
        {
            double equilibrium = EquilibriumCantMillimetres(speedKmh, radiusMetres, cls.cantFormulaGaugeMillimetres);
            return Math.Min(equilibrium, cls.maxAppliedCantMillimetres);
        }

        /// <summary> The cant deficiency in mm: how far the applied cant falls short of the speed's equilibrium
        /// cant. Zero on a straight or when the applied cant already meets equilibrium; positive when the maximum
        /// applied cant cannot reach what the speed wants. </summary>
        public static double CantDeficiencyMillimetres(double speedKmh, double radiusMetres, RailTrackClass cls)
        {
            double equilibrium = EquilibriumCantMillimetres(speedKmh, radiusMetres, cls.cantFormulaGaugeMillimetres);
            double applied = Math.Min(equilibrium, cls.maxAppliedCantMillimetres);
            return Math.Max(0.0, equilibrium - applied);
        }

        /// <summary> The outcome of validating one curve radius against the rail's minimum. On rejection it NAMES
        /// the radius that was requested and the minimum it needed, so the caller can say so rather than silently
        /// producing a kinked rail. </summary>
        public struct RadiusCheck
        {
            public bool accepted;
            public double requestedRadiusMetres;
            public double minimumRadiusMetres;
        }

        /// <summary> Validate a curve radius against the class minimum. A radius at or above the minimum is
        /// accepted; a tighter one is rejected, carrying both values so the message can name the radius it needed.
        /// A non-positive requested radius (a straight, or garbage) is treated as no curve and accepted. </summary>
        public static RadiusCheck CheckRadius(double requestedRadiusMetres, RailTrackClass cls)
        {
            RadiusCheck r = new RadiusCheck();
            r.requestedRadiusMetres = requestedRadiusMetres;
            r.minimumRadiusMetres = cls.minimumRadiusMetres;
            r.accepted = requestedRadiusMetres <= 0.0 || requestedRadiusMetres >= cls.minimumRadiusMetres;
            return r;
        }

        /// <summary> A human-readable rejection message for a failed <see cref="CheckRadius"/>, naming the radius it
        /// needed. Empty string when the check was accepted. </summary>
        public static string DescribeRadiusRejection(RadiusCheck check)
        {
            if (check.accepted) { return string.Empty; }
            return "rail curve radius " + check.requestedRadiusMetres.ToString("0.#") +
                   " m is below the minimum " + check.minimumRadiusMetres.ToString("0.#") + " m";
        }

        /// <summary> Whether a gradient (in per mille) is within the class ruling gradient. Absolute-value safe so a
        /// descent is judged the same as a climb. </summary>
        public static bool MeetsGradient(double gradientPerMille, RailTrackClass cls)
        {
            return Math.Abs(gradientPerMille) <= cls.maxGradientPerMille;
        }

        /// <summary> Whether a curve is runnable at the given speed without exceeding the class cant-deficiency
        /// limit -- i.e. the applied cant, capped at the maximum, still leaves the deficiency within bounds. When
        /// this is false the curve needs a speed restriction rather than a rejection. </summary>
        public static bool MeetsCantDeficiency(double speedKmh, double radiusMetres, RailTrackClass cls)
        {
            return CantDeficiencyMillimetres(speedKmh, radiusMetres, cls) <= cls.maxCantDeficiencyMillimetres;
        }
    }
}
