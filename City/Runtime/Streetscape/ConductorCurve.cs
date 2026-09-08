// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One conductor over one span: where it is attached at each end, and the curve it hangs in.
    ///
    /// Deliberately not "a wire on a pole line". A contact wire over a railway and a transmission conductor between
    /// two lattice towers are the same three facts, so they string themselves through this same struct and the tool
    /// ends up with one wire geometry instead of three that sag differently. </summary>
    public struct ConductorSpan
    {
        public Vector3 from;
        public Vector3 to;
        public Catenary catenary;

        public ConductorSpan(Vector3 from, Vector3 to, Catenary catenary)
        {
            this.from = from;
            this.to = to;
            this.catenary = catenary;
        }
    }


    /// <summary> B-27 / D-1. The POINTS a hanging wire passes through: the curve the solver produced, sampled at a
    /// fixed number of chords.
    ///
    /// Nothing here decides where a conductor goes or how far it sags -- <see cref="TransmissionLine.Solve"/> owns the
    /// curve and the run that strings the wire owns the attachments. This file only samples what it is handed, which is
    /// why it can be the one wire in the tool: a pole line, a railway's contact wire and a transmission span differ in
    /// their numbers and not in what a wire is.
    ///
    /// The TRIANGLES are not here. <c>ConductorMesh</c>, in the editor assembly, turns these points into a tube using
    /// the same buffer a pole is assembled in, for the reason its own summary gives: a pole line is baked, so the
    /// vertices belong to the editor and <c>City/Runtime</c> holds no mesh builder. What is left here is arithmetic --
    /// pure, and therefore assertable by a gate with no scene: D-16 checks that the middle of a span hangs BELOW both
    /// of its ends, which is the assertion that would have caught a wire strung as a straight line. </summary>
    public static class ConductorCurve
    {
        /// <summary> How many straight chords one span is drawn with. FIXED, not adaptive, so the vertex buffer is
        /// identical for identical input (C-06).
        ///
        /// Twelve is measured rather than picked. Approximating a parabolic sag of s with n equal chords leaves the
        /// middle of each chord s/n-squared below the curve, so a 40 m distribution span sagging 0.159 m is 1.1 mm out
        /// at twelve -- a tenth of the conductor's own diameter, which is the point at which more segments buy nothing
        /// a camera can see. </summary>
        public const int SegmentsPerSpan = 12;

        /// <summary> How many spans go into one conductor mesh.
        ///
        /// A conductor mesh cannot be instanced the way a pole can: both attachment heights and therefore the whole
        /// curve differ from span to span, so every span is unique geometry. That leaves a real choice -- one mesh for
        /// the run, which is a single draw call whose bounds cover the whole line and therefore never cull, or one per
        /// span, which culls but costs a call each. Eight spans is about 320 m: a few thousand vertices in one call,
        /// with bounds small enough that a run over a hill still drops the pieces behind the camera. </summary>
        public const int SpansPerPiece = 8;

        /// <summary> The world points one conductor hangs through. Public and pure: the gate asserts that the middle of
        /// a span is below both of its ends without building a mesh, which is the assertion that would have caught a
        /// wire strung as a straight line.
        ///
        /// The height at each point comes from the CURVE, at the distance along the span the curve was solved over; the
        /// horizontal path is the straight line between the two attachments. On a bend those two lengths differ -- the
        /// attachments sit a phase spacing off the poles' own line -- by centimetres over 40 m, and taking the height
        /// from the curve's own parameter keeps the sag exactly the sag that was checked for clearance rather than one
        /// rescaled to a slightly different length.
        ///
        /// The two ends are the attachment points themselves, not the curve evaluated at its ends. They agree to the
        /// last decimal when the caller solved the curve between these heights, and pinning them means a wire that
        /// touches its insulator even if a caller ever hands over a curve that does not quite. </summary>
        public static List<Vector3> Sample(ConductorSpan span, int segments)
        {
            if (segments < 1) { segments = 1; }
            List<Vector3> points = new List<Vector3>(segments + 1);
            double along = span.catenary.spanMetres;
            for (int i = 0; i <= segments; i++)
            {
                if (i == 0) { points.Add(span.from); continue; }
                if (i == segments) { points.Add(span.to); continue; }
                float t = (float)i / segments;
                points.Add(new Vector3(
                    Mathf.Lerp(span.from.x, span.to.x, t),
                    (float)span.catenary.HeightAt(t * along),
                    Mathf.Lerp(span.from.z, span.to.z, t)));
            }
            return points;
        }

        /// <summary> What a mesh of this many spans costs, without building it. For D-15's per-chunk ceiling, which is
        /// a figure the tool has to be able to state before an author presses anything. </summary>
        public static int VertexCount(int spanCount, int sides, int segments)
        {
            if (spanCount <= 0) { return 0; }
            if (sides < 3) { sides = 3; }
            if (segments < 1) { segments = 1; }
            return spanCount * (segments + 1) * (sides + 1);
        }
    }
}
