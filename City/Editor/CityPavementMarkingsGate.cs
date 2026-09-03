#if UNITY_EDITOR
#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> RM-32's gate: proves the dash-phase maths, the marked-section limits, the warning-line ratio, the
    /// data-driven palette and the unsealed-road rule without a scene. It asserts that paint presence is a pure
    /// function of ABSOLUTE arc length (so a dash keeps phase across an arbitrary segment join and does not restart
    /// or leave a half-dash), that a broken line repeats on its period and starts clean at the section start, that
    /// markings vanish outside the marked section (junction limits), that a warning line is DENSER than a lane line
    /// (ratio, not colour), that a marking colour is resolved ONLY through the palette so code never hard-codes
    /// "the centreline is yellow", that an unsealed road has no markings, and that everything is bit-identical on
    /// repeat (C-06). The ribbon/UV mesh and placed transverse quads are the deferred half. Report lines are
    /// prefixed RCMMARK. </summary>
    public static class CityPavementMarkingsGate
    {
        [MenuItem("Window/Road City Master/Testing/City pavement markings (arc-length phase)")]
        public static void RunFromMenu()
        {
            string report;
            if (Run(out report)) { Debug.Log(report); }
            else { Debug.LogError(report); }
        }

        /// <summary> Headless entry point: Unity.exe -batchmode -quit -executeMethod
        /// RoadCityMaster.City.Editor.CityPavementMarkingsGate.RunBatch. Exits 1 on failure. </summary>
        public static void RunBatch()
        {
            string report;
            bool passed = Run(out report);
            Debug.Log(report);
            EditorApplication.Exit(passed ? 0 : 1);
        }

        /// <summary> Runs every case and assembles the RCMMARK report. </summary>
        public static bool Run(out string report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("RCMMARK BEGIN\n");
            List<string> failures = new List<string>();

            CasePhaseIsArcLengthNotSegment(builder, failures);
            CaseSectionLimits(builder, failures);
            CaseWarningLineIsDenser(builder, failures);
            CaseColourIsDataNotHardCoded(builder, failures);
            CaseUnsealedHasNoMarkings(builder, failures);
            CaseLateralWidthIsMetresNotAShareOfTheRoad(builder, failures);
            CaseDeterministic(builder, failures);

            foreach (string failure in failures)
            {
                builder.Append("RCMMARK FAIL   ").Append(failure).Append("\n");
            }
            builder.Append("RCMMARK result ")
                   .Append(failures.Count == 0 ? "PASS" : "FAIL")
                   .Append("  failures ").Append(failures.Count).Append("\n");
            builder.Append("RCMMARK END");

            report = builder.ToString();
            return failures.Count == 0;
        }

        /// <summary> Paint presence depends only on absolute arc length: it repeats on the period, starts painted
        /// at the section start (no half-dash), and gives the SAME value either side of an arbitrary segment join
        /// because there is no per-segment state to restart. </summary>
        private static void CasePhaseIsArcLengthNotSegment(StringBuilder builder, List<string> failures)
        {
            const double mark = 3.0, gap = 9.0; // period 12
            Expect(PavementMarkings.IsPaintedAt(0.0, mark, gap),
                "phase: the pattern did not start painted at the section start (half-dash)", failures);
            Expect(!PavementMarkings.IsPaintedAt(6.0, mark, gap),
                "phase: mid-gap arc length reported painted", failures);
            // Periodicity: same state 12 m (one period) later.
            Expect(PavementMarkings.IsPaintedAt(1.0, mark, gap) == PavementMarkings.IsPaintedAt(13.0, mark, gap),
                "phase: the pattern did not repeat on its period", failures);
            // A mesh segment happens to be cut at 7.0 m; the value either side follows the single global phase.
            bool justBefore = PavementMarkings.IsPaintedAt(6.999, mark, gap);
            bool justAfter = PavementMarkings.IsPaintedAt(7.001, mark, gap);
            Expect(justBefore == justAfter,
                "phase: a dash restarted at a segment join instead of holding phase", failures);
            builder.Append("RCMMARK ").Append("phase-arc-length".PadRight(22))
                   .Append("dash phase driven by arc length, holds across joins\n");
        }

        /// <summary> Markings exist only within the marked section; outside the junction limits nothing is painted
        /// regardless of phase. </summary>
        private static void CaseSectionLimits(StringBuilder builder, List<string> failures)
        {
            const double start = 10.0, end = 90.0;
            Expect(PavementMarkings.IsWithinMarkedSection(50.0, start, end),
                "section: a point inside the marked section read outside", failures);
            Expect(!PavementMarkings.IsWithinMarkedSection(5.0, start, end),
                "section: a point before the section (junction limit) read inside", failures);
            Expect(!PavementMarkings.IsWithinMarkedSection(95.0, start, end),
                "section: a point past the section (junction limit) read inside", failures);
            builder.Append("RCMMARK ").Append("section-limits".PadRight(22))
                   .Append("markings stop at junction limits\n");
        }

        /// <summary> A warning / no-passing line is denser than an ordinary lane line -- a higher painted fraction,
        /// not a different colour. </summary>
        private static void CaseWarningLineIsDenser(StringBuilder builder, List<string> failures)
        {
            MarkingPatternClass p = MarkingPatternClass.Default();
            double lane = PavementMarkings.MarkFraction(p.laneLineMarkMetres, p.laneLineGapMetres);
            double warn = PavementMarkings.MarkFraction(p.warningLineMarkMetres, p.warningLineGapMetres);
            Expect(Math.Abs(lane - 3.0 / 12.0) < 1e-9,
                "warning: the lane line was not the authored 1:3 (0.25) painted fraction", failures);
            Expect(warn > lane, "warning: the warning line was not denser than the lane line", failures);
            builder.Append("RCMMARK ").Append("warning-ratio".PadRight(22))
                   .Append("warning line differs by ratio, not colour\n");
        }

        /// <summary> A marking colour comes only from the palette DATA: editing the palette changes the colour,
        /// proving no colour is hard-coded in the marking code. </summary>
        private static void CaseColourIsDataNotHardCoded(StringBuilder builder, List<string> failures)
        {
            MarkingPaletteClass def = MarkingPaletteClass.Default();
            Expect(def.ColourForRole(MarkingRole.Centreline) == Color.white,
                "palette: the authored default centreline was not the white-convention colour", failures);

            // Switch the standard by editing DATA (e.g. Q-14 selecting MUTCD yellow centreline).
            MarkingPaletteClass mutcd = MarkingPaletteClass.Default();
            mutcd.centrelineColour = Color.yellow;
            Expect(mutcd.ColourForRole(MarkingRole.Centreline) == Color.yellow,
                "palette: the centreline colour did not follow the palette data", failures);
            Expect(def.ColourForRole(MarkingRole.Centreline) != mutcd.ColourForRole(MarkingRole.Centreline),
                "palette: the colour was hard-coded rather than read from the palette", failures);
            builder.Append("RCMMARK ").Append("palette-data".PadRight(22))
                   .Append("colour resolved via palette role, never hard-coded\n");
        }

        /// <summary> An unsealed road (RM-41) carries no longitudinal markings; a sealed one does. </summary>
        private static void CaseUnsealedHasNoMarkings(StringBuilder builder, List<string> failures)
        {
            Expect(PavementMarkings.RoadHasMarkings(true), "unsealed: a sealed road reported no markings", failures);
            Expect(!PavementMarkings.RoadHasMarkings(false),
                "unsealed: an unsealed road was given markings", failures);
            builder.Append("RCMMARK ").Append("unsealed".PadRight(22))
                   .Append("unsealed road has no markings (RM-41)\n");
        }

        /// <summary> RM-94: a painted line is a WIDTH IN METRES, not a share of the carriageway it happens to be
        /// painted on. The intersection builder used to hold these as one hand-fitted fraction per lane total, which
        /// widened the paint whenever the author widened the lanes and matched no branch at all on an odd total. The
        /// property that replaces those branches is the one asserted here: convert a distance in metres to the
        /// interpolation fraction at any carriageway width, convert it back, and the metres must come out unchanged.
        /// The widths swept are 2, 3, 4 and 7 lanes at 3.5 m -- the odd totals RM-89 made authorable included. </summary>
        private static void CaseLateralWidthIsMetresNotAShareOfTheRoad(StringBuilder builder, List<string> failures)
        {
            double[] widths = new double[] { 7.0, 10.5, 14.0, 24.5 };
            double half = PavementMarkings.DoubleCentrelineHalfWidthMetres;
            double strip = PavementMarkings.EdgeStripWidthMetres;
            for (int i = 0; i < widths.Length; i++)
            {
                double w = widths[i];
                // From the centre: the fraction is half-way less the offset's share, so the metres it stands for are
                // (0.5 - f) x w. Painted 0.15 m from the centre on a 7 m road and on a 24.5 m road alike.
                double fromCentre = PavementMarkings.FractionAcrossFromCentre(half, w);
                Expect(Near((0.5 - fromCentre) * w, half),
                    "lateral: the centre marking moved off " + half + " m at a carriageway width of " + w + " m",
                    failures);
                // From the edge: the fraction is the offset's own share, so it stands for f x w metres.
                double fromEdge = PavementMarkings.FractionAcrossFromEdge(strip, w);
                Expect(Near(fromEdge * w, strip),
                    "lateral: the edge strip moved off " + strip + " m at a carriageway width of " + w + " m",
                    failures);
                Expect(fromCentre > 0.0 && fromCentre < 1.0 && fromEdge > 0.0 && fromEdge < 1.0,
                    "lateral: a marking fell outside the carriageway at a width of " + w + " m", failures);
            }

            // The two reference points of the interpolation.
            Expect(Near(PavementMarkings.FractionAcrossFromCentre(0.0, 7.0), 0.5),
                "lateral: a zero offset from the centre did not land on the centreline", failures);
            Expect(Near(PavementMarkings.FractionAcrossFromEdge(0.0, 7.0), 0.0),
                "lateral: a zero offset from the edge did not land on the edge", failures);

            // A double centre line is two normal lines with a gap no narrower than one of them, and the half-width is
            // half of it -- the relation the mesh builders rely on when they lay one side of the line at a time.
            Expect(Near(PavementMarkings.DoubleCentrelineWidthMetres * 0.5,
                        PavementMarkings.DoubleCentrelineHalfWidthMetres),
                "lateral: the double-centreline half-width is not half of its width", failures);
            Expect(PavementMarkings.DoubleCentrelineWidthMetres
                   >= (3.0 * PavementMarkings.NormalLineWidthMetres) - 1e-9,
                "lateral: the double centre line is narrower than two normal lines and a gap", failures);
            Expect(!string.IsNullOrEmpty(PavementMarkings.EdgeStripWidthSource),
                "lateral: the edge strip's width lost the note saying it is measured, not sourced", failures);

            // A width that cannot be divided by has no interior to place anything in, and must not produce an
            // infinity that flings a vertex off the side of the road.
            Expect(Near(PavementMarkings.FractionAcrossFromCentre(half, 0.0), 0.5),
                "lateral: a zero carriageway width did not fall back to the centre", failures);
            Expect(Near(PavementMarkings.FractionAcrossFromEdge(strip, -1.0), 0.0),
                "lateral: a negative carriageway width did not fall back to the edge", failures);
            // Wider paint than road is a misconfiguration; it clamps onto the surface rather than off it.
            Expect(Near(PavementMarkings.FractionAcrossFromEdge(9.0, 3.0), 1.0),
                "lateral: a marking wider than the road was not clamped onto it", failures);

            builder.Append("RCMMARK ").Append("lateral-width".PadRight(22))
                   .Append("marking width holds in metres at every carriageway width\n");
        }

        /// <summary> The maths is bit-identical on repeat -- pure arithmetic, no clock or hash (C-06). </summary>
        private static void CaseDeterministic(StringBuilder builder, List<string> failures)
        {
            bool a = PavementMarkings.IsPaintedAt(37.5, 3.0, 9.0);
            bool b = PavementMarkings.IsPaintedAt(37.5, 3.0, 9.0);
            double f1 = PavementMarkings.MarkFraction(3.0, 9.0);
            double f2 = PavementMarkings.MarkFraction(3.0, 9.0);
            Expect(a == b && f1 == f2,
                "determinism: two identical marking computations produced different values", failures);
            builder.Append("RCMMARK ").Append("determinism".PadRight(22))
                   .Append("identical inputs -> bit-identical phase/fraction\n");
        }

        /// <summary> Records a failure when a condition the case demands does not hold. </summary>
        private static void Expect(bool condition, string message, List<string> failures)
        {
            if (!condition) { failures.Add(message); }
        }

        /// <summary> Equality for the lateral cases, which convert metres to a fraction and back and so land a few
        /// ulps away from the figure they started at. </summary>
        private static bool Near(double a, double b)
        {
            double d = a - b;
            if (d < 0.0) { d = -d; }
            return d < 1e-9;
        }
    }
}
#endif
