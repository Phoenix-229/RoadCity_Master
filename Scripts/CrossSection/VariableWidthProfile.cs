#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.CrossSection
{
    /// <summary> The kinds of median real roads use, carried as data because the taxonomy tells an
    /// implementer which cases the geometry must cover -- not decoration. A <see cref="Opening"/> is where
    /// turning traffic crosses; where a median MAY be broken is a traffic decision, so openings are
    /// AUTHORED, never generated. </summary>
    public enum MedianKind
    {
        /// <summary> A flush painted median, below roughly a metre of width -- no raised geometry. </summary>
        PaintedFlush,

        /// <summary> A raised island with a kerb. </summary>
        RaisedIsland,

        /// <summary> A planted or depressed median wide enough to carry drainage. </summary>
        PlantedDepressed,

        /// <summary> A median opening where turning traffic crosses -- authored, not generated. </summary>
        Opening
    }


    /// <summary> RM-31's foundation: a cross-section element whose width VARIES with distance along the
    /// spline, driven by an AUTHORED width-over-distance curve rather than a constant offset. Build it once
    /// here, generalised, and RM-27's taper, RM-29's verge, RM-30's ditch and RM-34's edge beam become
    /// configuration rather than code -- each is a profile element whose width varies with station.
    ///
    /// This type is the PURE, deterministic core: given the curve and a station distance it answers the
    /// half-width, whether the element emits geometry there, its contribution to total road width, and the
    /// slope of its edge -- the quantities the sweep, the terrain falloff (RM-38) and the no-kink gate all
    /// read. The mesh emission that consumes these values lives in the sweep and is confirmed in the scene.
    ///
    /// Three requirements this type enforces or exposes, straight from the RM-31 spec:
    ///   * ZERO WIDTH is a legal value that DEGRADES CLEANLY -- at or below <see cref="minEmitHalfWidth"/>
    ///     the ribbon collapses to a line and <see cref="EmitsAt"/> is false, so the sweep emits NOTHING
    ///     rather than a degenerate or inverted triangle (the black-sliver failure).
    ///   * A widening element PUSHES THE CARRIAGEWAYS OUTWARD rather than eating lane width, so total road
    ///     width grows by exactly <see cref="TotalWidthContributionAt"/>. That coupling is not optional:
    ///     RM-38's falloff consumes total width, so an element that does not report its contribution
    ///     produces a cut too narrow exactly where the element is widest.
    ///   * The edge SLOPE (<see cref="WidthSlopeAt"/>) is exposed so a gate can prove the width curve is
    ///     continuous in its first derivative -- an abrupt slope change puts a visible crease down the edge
    ///     and along every element built on this system afterwards. </summary>
    [Serializable]
    public struct VariableWidthProfile
    {
        /// <summary> AUTHORED half-width in metres (Y) against arc-distance in metres along the spline (X).
        /// Half-width because the element is symmetric about its own centreline; full width is twice this.
        /// A curve with smooth/auto tangents is continuous in its first derivative by construction, which
        /// the no-kink gate verifies by sampling <see cref="WidthSlopeAt"/>. </summary>
        public AnimationCurve halfWidthOverDistance;

        /// <summary> At or below this half-width (metres) the element emits no geometry -- the ribbon is
        /// treated as collapsed to a line. Small and positive so a curve that touches exactly zero
        /// degrades cleanly instead of emitting a zero-area quad. </summary>
        public float minEmitHalfWidth;

        /// <summary> The half-width at a station, clamped to be non-negative: a negative curve value is
        /// meaningless for a width and is treated as the collapsed line (zero), never a mirrored ribbon. </summary>
        public float HalfWidthAt(float distance)
        {
            if (halfWidthOverDistance == null) { return 0f; }
            float h = halfWidthOverDistance.Evaluate(distance);
            return h < 0f ? 0f : h;
        }

        /// <summary> The full width (both sides) at a station. </summary>
        public float FullWidthAt(float distance)
        {
            return HalfWidthAt(distance) * 2f;
        }

        /// <summary> Whether the element has real width at a station and so should emit geometry. False
        /// where the width has collapsed, so the sweep skips it rather than emitting a degenerate face. </summary>
        public bool EmitsAt(float distance)
        {
            float emit = minEmitHalfWidth > 0f ? minEmitHalfWidth : 0.001f;
            return HalfWidthAt(distance) > emit;
        }

        /// <summary> How much this element adds to TOTAL road width at a station (the whole full width,
        /// because it pushes the carriageways outward). RM-38's falloff must consume this so the terrain
        /// cut widens with the element. </summary>
        public float TotalWidthContributionAt(float distance)
        {
            return FullWidthAt(distance);
        }

        /// <summary> The centreline-offset of ONE carriageway edge at a station, given the road's constant
        /// half-carriageway when this element is absent. The element's half-width is ADDED (outward push),
        /// so the edge moves out as the element grows -- the property the gate checks. </summary>
        public float CarriagewayEdgeOffsetAt(float distance, float baseHalfCarriageway)
        {
            return baseHalfCarriageway + HalfWidthAt(distance);
        }

        /// <summary> The slope of the half-width curve at a station, by central finite difference over a
        /// step of <paramref name="h"/> metres. This is the quantity that must be CONTINUOUS for the edge
        /// to have no crease; a gate samples it densely and rejects any abrupt jump. </summary>
        public float WidthSlopeAt(float distance, float h)
        {
            float step = h > 1e-4f ? h : 1e-4f;
            float before = HalfWidthAt(distance - step);
            float after = HalfWidthAt(distance + step);
            return (after - before) / (2f * step);
        }

        /// <summary> An absent element: a null curve reads as zero width everywhere and emits nothing. The
        /// emit threshold is a small positive default. </summary>
        public static VariableWidthProfile Default()
        {
            VariableWidthProfile p;
            p.halfWidthOverDistance = null;
            p.minEmitHalfWidth = 0.001f;
            return p;
        }

        /// <summary> A symmetric widen-hold-narrow median: zero half-width until <paramref name="startDist"/>,
        /// smoothly up to <paramref name="halfWidth"/> over <paramref name="taperLength"/> (the median NOSE),
        /// held to <paramref name="endDist"/>, then smoothly back to zero over the same nose length. Smooth
        /// tangents make it continuous in its first derivative, so the edge has no kink and the ends degrade
        /// cleanly to a line. AUTHORED shape -- the taper rate and hold are design values, not generated. </summary>
        public static VariableWidthProfile Median(float startDist, float endDist, float halfWidth,
            float taperLength)
        {
            float t = taperLength > 0.01f ? taperLength : 0.01f;
            // Every key carries FLAT (zero) in/out tangents. The two nose transitions become eased
            // smoothstep ramps -- continuous in their first derivative (zero slope at both ends of each
            // ramp), so no kink -- and the held section stays EXACTLY at halfWidth. SmoothTangents was
            // wrong here: it set each peak key's tangent to the slope between its distant neighbours, which
            // bulged the flat top above the authored half-width (the outward-push / total-width failures).
            AnimationCurve c = new AnimationCurve(
                new Keyframe(startDist, 0f, 0f, 0f),
                new Keyframe(startDist + t, halfWidth, 0f, 0f),
                new Keyframe(endDist - t, halfWidth, 0f, 0f),
                new Keyframe(endDist, 0f, 0f, 0f));
            VariableWidthProfile p;
            p.halfWidthOverDistance = c;
            p.minEmitHalfWidth = 0.001f;
            return p;
        }
    }
}
