#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-27's verifiable core: the lane-drop taper that resolves a lane-count mismatch at a junction.
    /// The real-world fix is NOT a special intersection type -- it is that the wider road stops being wider before
    /// it arrives. A real road drops a lane over a straight linear narrowing (a "lane drop taper") whose LENGTH is a
    /// published function of the width removed and the design speed, stated in both the MUTCD and AASHTO geometric
    /// design guidance. In the metric statement (the form used here, per the RM-27 spec):
    ///
    ///   L = W · S² / 155   for design speed S up to ~70 km/h, and
    ///   L = 0.62 · W · S   above that,
    ///
    /// with W the width dropped in metres and S the design speed in km/h. The ~70 km/h breakpoint is the metric
    /// echo of the customary ~45 mph one. The constants differ between the customary and metric statements of the
    /// same rule, so they are taken from the metric statement verbatim and cited here rather than transcribed from
    /// the customary one -- a factor error here produces tapers wrong by a factor, not slightly off.
    ///
    /// A taper IS a variable-width cross-section (RM-31's mechanism, already built): the width authored as a curve
    /// over the last L metres of the approach, after which the existing corner builder runs against the
    /// already-narrowed width. Because the taper needs room, an approach shorter than L cannot host the full taper;
    /// the honest behaviour -- and what <see cref="FitToApproach"/> returns -- is to SHORTEN the taper to the
    /// available length and FLAG the junction, never to silently produce a kink or push the taper through the
    /// previous intersection. Design speed S is the same quantity RM-40 needs (Q-13); it is an input here, to be
    /// sourced from the one shared per-road-class store once RM-40 lands, not a second field. </summary>
    public static class LaneDropTaper
    {
        /// <summary> The published breakpoint between the two taper-length statements, ~70 km/h (the metric echo of
        /// the customary ~45 mph). At or below it the low-speed statement applies. </summary>
        public const double LowSpeedThresholdKmh = 70.0;

        /// <summary> The lane-drop taper length in metres for dropping <paramref name="widthDroppedMetres"/> of
        /// width at design speed <paramref name="designSpeedKmh"/>, per the metric MUTCD/AASHTO statement above.
        /// Zero when there is nothing to drop or the speed is non-positive. </summary>
        public static double TaperLengthMetres(double widthDroppedMetres, double designSpeedKmh)
        {
            if (widthDroppedMetres <= 0.0 || designSpeedKmh <= 0.0) { return 0.0; }
            if (designSpeedKmh <= LowSpeedThresholdKmh)
            {
                return (widthDroppedMetres * designSpeedKmh * designSpeedKmh) / 155.0;
            }
            return 0.62 * widthDroppedMetres * designSpeedKmh;
        }

        /// <summary> The carriageway width at a distance into the taper: full width at the taper's start (distance
        /// 0), narrowed by the full dropped width at its end (distance = taper length), linear between, and clamped
        /// outside [0, L]. This is the width-over-distance curve RM-31's variable-width sweep consumes. </summary>
        public static double TaperedWidthAt(double fullWidthMetres, double widthDroppedMetres,
            double taperLengthMetres, double distanceIntoTaperMetres)
        {
            if (widthDroppedMetres <= 0.0) { return fullWidthMetres; }
            if (taperLengthMetres <= 0.0) { return fullWidthMetres - widthDroppedMetres; }
            double t = distanceIntoTaperMetres / taperLengthMetres;
            if (t <= 0.0) { return fullWidthMetres; }
            if (t >= 1.0) { return fullWidthMetres - widthDroppedMetres; }
            return fullWidthMetres - widthDroppedMetres * t;
        }

        /// <summary> The outcome of fitting a taper onto an approach of a given length. When the ideal taper fits it
        /// is used unchanged; when it does not, the taper is shortened to the available length and the junction is
        /// FLAGGED (<see cref="fits"/> = false) so the author is told rather than handed a silent kink. </summary>
        public struct TaperFit
        {
            public bool fits;
            public double taperLengthMetres;
            public double idealLengthMetres;
            public double availableLengthMetres;
        }

        /// <summary> Fit the ideal taper onto an approach of <paramref name="availableApproachMetres"/>: keep it if
        /// it fits, otherwise shorten to the available length and flag. </summary>
        public static TaperFit FitToApproach(double widthDroppedMetres, double designSpeedKmh,
            double availableApproachMetres)
        {
            double ideal = TaperLengthMetres(widthDroppedMetres, designSpeedKmh);
            TaperFit f = new TaperFit();
            f.idealLengthMetres = ideal;
            f.availableLengthMetres = availableApproachMetres;
            f.fits = ideal <= availableApproachMetres;
            f.taperLengthMetres = f.fits ? ideal : Math.Max(0.0, availableApproachMetres);
            return f;
        }
    }
}
