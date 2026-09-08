// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The role a marking plays, kept separate from its colour so the code never says "the centreline is
    /// yellow" -- that is a North American convention, not a universal one, and the colour is DATA (see
    /// <see cref="MarkingPaletteClass"/>). </summary>
    public enum MarkingRole
    {
        Centreline = 0,
        LaneLine = 1,
        EdgeLine = 2,
        WarningLine = 3,
        StopLine = 4,
        GiveWayLine = 5
    }


    /// <summary> RM-32's AUTHORED dash patterns, measured in METRES along the spline (the pattern is driven by arc
    /// length, never by per-segment mesh UVs, which is what makes dashes keep phase across segment joins). Values
    /// cited to the MUTCD broken-line pattern (a 3 m mark / 9 m gap, the metric echo of 10 ft / 30 ft, a 1:3 line);
    /// a warning / no-passing approach line changes the RATIO (denser, ~2:1) rather than the colour. Q-14 may
    /// switch the shipped standard by editing this DATA, not code. </summary>
    [Serializable]
    public sealed class MarkingPatternClass
    {
        public float laneLineMarkMetres = 3f;
        public float laneLineGapMetres = 9f;   // 1:3 broken lane line
        public float warningLineMarkMetres = 3f;
        public float warningLineGapMetres = 1.5f; // ~2:1 denser warning/no-passing line

        public static MarkingPatternClass Default()
        {
            return new MarkingPatternClass();
        }
    }


    /// <summary> RM-32's AUTHORED colour palette, keyed by <see cref="MarkingRole"/>. The default follows the
    /// WHITE-centreline convention (the UK / Vienna Convention family, the more internationally common one; yellow
    /// is reserved for restrictions), because the target market may well be one of those; Q-14 settles which
    /// standard ships and switches this by editing DATA. The code resolves a colour ONLY through
    /// <see cref="ColourForRole"/> -- it never hard-codes one. </summary>
    [Serializable]
    public sealed class MarkingPaletteClass
    {
        public Color centrelineColour = Color.white;
        public Color laneLineColour = Color.white;
        public Color edgeLineColour = Color.white;
        public Color warningLineColour = Color.white;
        public Color stopLineColour = Color.white;
        public Color giveWayLineColour = Color.white;

        /// <summary> The authored colour for a marking role. This is the ONLY path to a marking colour. </summary>
        public Color ColourForRole(MarkingRole role)
        {
            switch (role)
            {
                case MarkingRole.Centreline: return centrelineColour;
                case MarkingRole.LaneLine: return laneLineColour;
                case MarkingRole.EdgeLine: return edgeLineColour;
                case MarkingRole.WarningLine: return warningLineColour;
                case MarkingRole.StopLine: return stopLineColour;
                case MarkingRole.GiveWayLine: return giveWayLineColour;
                default: return Color.white;
            }
        }

        public static MarkingPaletteClass Default()
        {
            return new MarkingPaletteClass();
        }
    }

    /// <summary> RM-32's verifiable core: the pure PHASE maths of a dash pattern (the real engineering problem —
    /// paint is data), the marked-section limits, the ratio that distinguishes a warning line, and the unsealed-road
    /// rule. Every longitudinal query is a function of ABSOLUTE arc length from the start of the marked section, so
    /// there is no per-segment state and a dash cannot restart at a mesh join.
    ///
    /// The second half is LATERAL: how wide a line is and where it sits across the carriageway. That is in metres for
    /// the same reason the dash pattern is -- a painted line is a published width, not a share of the road it happens
    /// to be painted on -- and it lives here rather than in the mesh builders that consume it.
    ///
    /// The ribbon/UV mesh, the atlas and the placed transverse quads are the deferred scene half. </summary>
    public static class PavementMarkings
    {
        /// <summary> One mark + one gap, in metres. A non-positive gap makes a solid line (period 0). </summary>
        public static double PatternPeriodMetres(double markMetres, double gapMetres)
        {
            double period = markMetres + Math.Max(0.0, gapMetres);
            return period;
        }

        /// <summary> Whether paint is present at an absolute arc length along a broken line. Phase is the arc length
        /// modulo the pattern period, measured from the marked section's start (the caller passes section-relative
        /// arc length), so the answer depends ONLY on distance, never on which mesh segment the point falls in --
        /// this is what keeps a dash in phase across a segment boundary. A solid line (non-positive gap) is always
        /// painted. </summary>
        public static bool IsPaintedAt(double arcLengthFromSectionStartMetres, double markMetres, double gapMetres)
        {
            if (markMetres <= 0.0) { return false; }
            if (gapMetres <= 0.0) { return true; }
            double period = markMetres + gapMetres;
            double phase = arcLengthFromSectionStartMetres % period;
            if (phase < 0.0) { phase += period; }
            return phase < markMetres;
        }

        /// <summary> Whether an arc-length position lies within the marked section (markings stop at junction
        /// limits). Outside the section nothing is painted regardless of phase. </summary>
        public static bool IsWithinMarkedSection(double arcLengthMetres, double sectionStartMetres,
            double sectionEndMetres)
        {
            return arcLengthMetres >= sectionStartMetres - 1e-9 && arcLengthMetres <= sectionEndMetres + 1e-9;
        }

        /// <summary> The fraction of the period that is painted -- the property that distinguishes a warning line
        /// (denser) from an ordinary lane line, independent of colour. </summary>
        public static double MarkFraction(double markMetres, double gapMetres)
        {
            double period = PatternPeriodMetres(markMetres, gapMetres);
            if (period <= 0.0) { return 1.0; }
            return markMetres / period;
        }

        /// <summary> Whether a road carries longitudinal markings at all: a sealed road does, an unsealed road
        /// (RM-41) does not. </summary>
        public static bool RoadHasMarkings(bool roadIsSealed)
        {
            return roadIsSealed;
        }


        // ---- Lateral geometry: how WIDE a marking is, and where that puts it across the carriageway. ----
        //
        // RM-94: this half of the marking maths had no owner. The intersection builder carried it as fractions of the
        // whole carriageway -- one hand-fitted fraction per lane total -- so a painted line got wider whenever the
        // author widened the lanes, and every new lane total needed a new fraction nobody had fitted. A line's width
        // is a figure in metres and does not depend on how many lanes it separates, so it belongs beside the dash
        // pattern, which is already in metres for the same reason.

        /// <summary> The width in metres of a NORMAL longitudinal line. The MUTCD gives a normal line as 4 to 6 in
        /// wide; 100 mm is the metric figure at the narrow end of that range, and is also the usual minimum in the
        /// Vienna Convention family this tool's default palette follows. </summary>
        public const double NormalLineWidthMetres = 0.1;

        /// <summary> The overall width in metres of a DOUBLE centre line: two normal lines separated by a gap no
        /// narrower than one of the lines, so 300 mm across in total. </summary>
        public const double DoubleCentrelineWidthMetres = 0.3;

        /// <summary> Half of <see cref="DoubleCentrelineWidthMetres"/> -- the distance from the centreline out to the
        /// outer face of the paint on ONE side of it. This is the figure a mesh builder wants, because it lays each
        /// half of a double line as a strip running alongside the spline. </summary>
        public const double DoubleCentrelineHalfWidthMetres = 0.15;

        /// <summary> The width in metres of the painted strip laid along the carriageway EDGE where a road meets an
        /// intersection. MEASURED, NOT SOURCED: it is the distance this tool has always produced there, kept so the
        /// geometry does not move while it gains an owner. Which standard fixes it is not established -- a normal
        /// edge line is <see cref="NormalLineWidthMetres"/> wide and a wide one half again, and this is wider than
        /// either, so it is likely a strip that already includes a setback rather than the paint alone. Settle that
        /// before quoting this figure as a standard. </summary>
        public const double EdgeStripWidthMetres = 0.21;

        public const string EdgeStripWidthSource =
            "measured out of the legacy intersection builder, not taken from a marking standard";

        /// <summary> Where a marking sits across a carriageway, given as the 0..1 fraction that a straight
        /// interpolation between the two carriageway edges needs, for a marking a given distance from the
        /// CENTRELINE. The offset is measured towards the edge the fraction is counted FROM: zero returns the
        /// centreline's own half-way fraction, a positive offset walks back towards that edge, and a negative one
        /// walks across to the other. A non-positive or unmeasurable width has no interior to place anything in, so
        /// the centre is the only honest answer. </summary>
        public static double FractionAcrossFromCentre(double offsetFromCentreMetres, double carriagewayWidthMetres)
        {
            if (!(carriagewayWidthMetres > 0.0)) { return 0.5; }
            return Clamp01(0.5 - (offsetFromCentreMetres / carriagewayWidthMetres));
        }

        /// <summary> The same fraction for a marking measured from a carriageway EDGE instead of the centreline: it
        /// is counted from that edge, so an offset of zero lands exactly on it. </summary>
        public static double FractionAcrossFromEdge(double offsetFromEdgeMetres, double carriagewayWidthMetres)
        {
            if (!(carriagewayWidthMetres > 0.0)) { return 0.0; }
            return Clamp01(offsetFromEdgeMetres / carriagewayWidthMetres);
        }

        /// <summary> A fraction can only leave the carriageway if the marking is wider than the road it is painted
        /// on, which is a misconfiguration rather than a geometry worth honouring; clamping keeps the point on the
        /// surface instead of flinging a vertex off the side of it. </summary>
        private static double Clamp01(double value)
        {
            if (value < 0.0) { return 0.0; }
            return value > 1.0 ? 1.0 : value;
        }
    }
}
