// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> RM-31's median, built ON <see cref="VariableWidthProfile"/> rather than as a one-off: the
    /// variable-width element plus the median-specific facts a sweep and a marking pass need -- which KIND
    /// of median it is and how sharply its end tapers (the median NOSE). Building the median this way is
    /// what makes RM-27's taper, RM-29's verge, RM-30's ditch and RM-34's edge beam configuration of the
    /// same element rather than four more one-offs.
    ///
    /// The width profile carries the geometry (where it starts, how wide, where it ends, how cleanly it
    /// degrades to a line); this wrapper carries the semantics. The nose taper rate is a REAL quantity
    /// (metres of length per metre of half-width), not an arbitrary bevel, and it is AUTHORED. </summary>
    [Serializable]
    public struct MedianProfile
    {
        /// <summary> The variable-width element the median is drawn on -- the single source of its width at
        /// any station and of its contribution to total road width. </summary>
        public VariableWidthProfile width;

        /// <summary> Which median this is. Drives kerb/planting/drainage treatment downstream; an
        /// <see cref="MedianKind.Opening"/> is authored, never generated. </summary>
        public MedianKind kind;

        /// <summary> The median NOSE taper rate: metres of longitudinal length per metre of half-width the
        /// nose gains, so a wider island needs a longer nose. A real design quantity, AUTHORED. </summary>
        public float noseTaperRatePerMetre;

        /// <summary> The nose length implied by the taper rate for a given target half-width -- the length
        /// over which the median opens from a line to its full half-width without a kink. </summary>
        public float NoseLengthFor(float halfWidth)
        {
            return NoseLength(halfWidth, noseTaperRatePerMetre);
        }

        /// <summary> The same nose length, computed from a rate that is not yet stored in a profile. The instance
        /// method delegates here so the taper formula lives in exactly one place: a struct's fields must all be
        /// assigned before an instance method may be called on it, so the factory below needs the static form. </summary>
        public static float NoseLength(float halfWidth, float ratePerMetre)
        {
            float rate = ratePerMetre > 0.01f ? ratePerMetre : 0.01f;
            return halfWidth * rate;
        }

        /// <summary> A planted median that opens over <paramref name="startDist"/>..<paramref name="endDist"/>
        /// to the given half-width, its nose length set by an AUTHORED taper rate so the shape is consistent
        /// with the kind rather than picked per road. </summary>
        public static MedianProfile Planted(float startDist, float endDist, float halfWidth,
            float noseTaperRatePerMetre)
        {
            MedianProfile m;
            float rate = noseTaperRatePerMetre > 0.01f ? noseTaperRatePerMetre : 0.01f;
            m.kind = MedianKind.PlantedDepressed;
            m.noseTaperRatePerMetre = rate;
            m.width = VariableWidthProfile.Median(startDist, endDist, halfWidth,
                NoseLength(halfWidth, rate));
            return m;
        }
    }
}
