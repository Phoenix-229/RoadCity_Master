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

        /// <summary> AUTHORED maximum rate of change of applied cant, mm/s: how fast the outer rail may rise UNDER A
        /// TRAIN as it runs through a transition. 55 mm/s is the common permissible maximum for conventional stock,
        /// 35 mm/s the desirable value. It is one of the three rules that decide how long a transition curve has to
        /// be, and it is the one that ties that length to the SPEED. </summary>
        public float maxCantRateMillimetresPerSecond = 55f;

        /// <summary> AUTHORED maximum rate of change of cant DEFICIENCY, mm/s -- the same limit applied to the part of
        /// the curve's demand the cant does not answer, which is the part the passenger feels sideways. Also 55 mm/s
        /// in common practice. Kept separate from the cant rate because a curve can be short of cant without being
        /// short of cant RATE, and the two limits then ask for different transition lengths. </summary>
        public float maxCantDeficiencyRateMillimetresPerSecond = 55f;

        /// <summary> AUTHORED maximum cant GRADIENT, mm of cant per metre of track: how steeply the outer rail may
        /// climb ALONG the track. That is a twist limit on the track itself rather than a comfort limit on the
        /// passenger, which is why it is a third figure and not a restatement of the other two. 2.5 mm/m is 1 in 400,
        /// the usual permissible maximum, against 1 in 600 desirable. It is the rule that still bites at low speed,
        /// where both rate limits go slack. </summary>
        public float maxCantGradientMillimetresPerMetre = 2.5f;

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
    /// SCOPE (stated, not deferred): this file is the constraint SET only -- the cant, radius and gradient rules a
    /// piece of plain track is judged by. Where those rules meet the world is <see cref="RailAlignmentPlanner"/>. A
    /// rail junction is a TURNOUT -- switch rails, a frog and a diverging route at a standardised angle -- which is
    /// not a road intersection and cannot come from the corner-blending <c>ThreeWay</c>/<c>FourWay</c> code, so
    /// turnouts stay explicitly OUT of scope, their own task with their own geometry spec. LEVEL CROSSINGS are no
    /// longer among them: <see cref="RailAlignmentPlanner"/> owns them, because a crossing is where a staked rail
    /// and a staked road meet, and only something holding both alignments can find one. </summary>
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

        /// <summary> The speed a curve of this radius can actually be run at, km/h, from the same cant equation read
        /// backwards: with the outer rail raised as far as the class allows and the deficiency taken to its limit,
        /// E + Ed = (G . V^2)/(127 . R) gives V = sqrt((E + Ed) . 127 . R / G).
        ///
        /// This exists so a tight curve has an answer other than "no". <see cref="CheckRadius"/> refuses a radius
        /// below the class minimum because such a curve cannot be built as track at all; a curve that is above the
        /// minimum but still too tight for the design speed is a different thing entirely -- it is buildable, it is
        /// everywhere in the real world, and what it needs is a SPEED RESTRICTION. Reporting the restriction is the
        /// difference between a tool that says a route is impossible and a tool that says the route is 95 km/h.
        ///
        /// A straight (non-positive or infinite radius) returns positive infinity: it imposes no limit of its own, and
        /// returning the design speed instead would make a caller taking the minimum of the two unable to tell a
        /// straight from a curve that happens to be fast enough. </summary>
        public static double PermissibleSpeedKmh(double radiusMetres, RailTrackClass cls)
        {
            if (cls == null) { return double.PositiveInfinity; }
            if (radiusMetres <= 0.0 || double.IsInfinity(radiusMetres)) { return double.PositiveInfinity; }
            double gauge = cls.cantFormulaGaugeMillimetres > 0f ? cls.cantFormulaGaugeMillimetres : 1500.0;
            double budget = cls.maxAppliedCantMillimetres + cls.maxCantDeficiencyMillimetres;
            if (budget <= 0.0) { return 0.0; }
            return Math.Sqrt(budget * 127.0 * radiusMetres / gauge);
        }

        /// <summary> How long the transition into a curve of this radius has to be, in metres: the LONGEST of the three
        /// rules rail practice applies, because each of them is a different thing going wrong and satisfying one does
        /// not excuse the others.
        ///
        /// * rate of change of applied cant: Ls = E . V / (3.6 . rc), the outer rail rising under a moving train;
        /// * rate of change of cant deficiency: Ls = Ed . V / (3.6 . rd), the sideways force building on a passenger;
        /// * cant gradient: Ls = E / ig, the twist the track itself is allowed along its length.
        ///
        /// The 3.6 is the km/h to m/s conversion, so E/rc is a time in seconds and V/3.6 turns it into a length. A
        /// straight wants no transition and returns zero, which is the same answer the road side gives and for the same
        /// reason: there is nothing to ease into. </summary>
        public static double TransitionLengthMetres(double speedKmh, double radiusMetres, RailTrackClass cls)
        {
            if (cls == null || radiusMetres <= 0.0 || double.IsInfinity(radiusMetres)) { return 0.0; }
            double applied = AppliedCantMillimetres(speedKmh, radiusMetres, cls);
            double deficiency = CantDeficiencyMillimetres(speedKmh, radiusMetres, cls);

            double cantRate = cls.maxCantRateMillimetresPerSecond > 0f
                ? cls.maxCantRateMillimetresPerSecond : 55.0;
            double deficiencyRate = cls.maxCantDeficiencyRateMillimetresPerSecond > 0f
                ? cls.maxCantDeficiencyRateMillimetresPerSecond : 55.0;
            double cantGradient = cls.maxCantGradientMillimetresPerMetre > 0f
                ? cls.maxCantGradientMillimetresPerMetre : 2.5;

            double byCantRate = applied * speedKmh / (3.6 * cantRate);
            double byDeficiencyRate = deficiency * speedKmh / (3.6 * deficiencyRate);
            double byCantGradient = applied / cantGradient;

            double longest = byCantRate > byDeficiencyRate ? byCantRate : byDeficiencyRate;
            return longest > byCantGradient ? longest : byCantGradient;
        }
    }
}
