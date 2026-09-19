// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> B-27 / D-7, the scene half. The one place in the tool that turns a <see cref="RailOverheadPlan"/> into
    /// masts, brackets and the three wires strung between them.
    ///
    /// The plan already decided everything a number can decide -- where every mast stands, how high the contact wire
    /// hangs at each, how the messenger sags between them and where the droppers go -- and refused what it had to. This
    /// file only turns those answers into triangles, which is the same division <see cref="RailAlignmentBuilder"/> and
    /// <see cref="PoleMeshFactory"/> both draw. It re-samples nothing and decides nothing: a builder that recomputed a
    /// wire height would be a second answer to a settled question.
    ///
    /// TWO SUBMESHES on a mast piece -- steel then bracket -- because a mast's shaft weathers like a rolled section and
    /// its cantilever brackets like tube, and the two take the author's <c>ohleMastMaterial</c> and
    /// <c>ohleBracketMaterial</c> slots. A wire piece is ONE material (<c>ohleWireMaterial</c>) written into both slots,
    /// the way <see cref="RailAlignmentBuilder"/> fills a rail's array, so a renderer never falls back on the
    /// last-material rule the way a short array does.
    ///
    /// EVERYTHING IS BUILT RELATIVE TO A PIECE ORIGIN and the object placed there, for the reason
    /// <see cref="ConductorMesh"/> gives: a contact wire is a 12 mm tube, and a float ten kilometres from the scene
    /// origin has lost most of the millimetre that tube is measured in, so a wire built in world space would wobble by
    /// most of its own radius. Subtracting the piece origin puts every vertex back near zero.
    ///
    /// WHERE A MAST SITS is <see cref="RailOverheadMast.basePoint"/>, which the planner put on the ground off the side
    /// of the formation and buried by the class's foundation depth here -- so a mast on a cutting slope reads as driven
    /// in rather than resting on air, which is the seating rule B-13 states for a pole applied to a mast. The wire
    /// heights are all ABOVE THE RAIL HEAD, so each is added to the sample's own top-of-rail world Y; the sample list is
    /// read for the centreline and the lateral frame the stagger is measured along, never re-staked. </summary>
    public static class RailOverheadMeshes
    {
        /// <summary> How many masts one steel piece carries, and how many spans one wire piece carries. Bounded for the
        /// same three reasons <see cref="RailTrackMeshes.SamplesPerPiece"/> is: the 65 535-vertex ceiling, the single
        /// bounding box a mesh as long as the line cannot cull behind, and the cost of rewriting the whole line when one
        /// curve moves. Eight masts is roughly half a kilometre, which culls sensibly and stays far under the ceiling.
        /// </summary>
        public const int MastsPerPiece = 8;
        public const int SpansPerPiece = 8;

        /// <summary> How many facets a mast, a bracket tube and a wire are drawn with. A mast is a post seen from tens of
        /// metres and six facets read as round; a wire's silhouette is its diameter and not its roundness, so five is a
        /// tube at any distance a railway is looked at from. </summary>
        private const int MastSides = 6;
        private const int BracketSides = 5;
        private const int WireSides = 4;

        /// <summary> How many points a messenger span is sampled at along its catenary. Ten segments over a 65 m span is
        /// a reading every 6.5 m, which is finer than the dropper spacing, so the sag reads as a curve and not as the
        /// chords between droppers. </summary>
        private const int MessengerSegmentsPerSpan = 10;

        /// <summary> How many mast/bracket pieces a plan needs. </summary>
        public static int MastPieceCount(int mastCount)
        {
            if (mastCount <= 0) { return 0; }
            return (mastCount + MastsPerPiece - 1) / MastsPerPiece;
        }

        /// <summary> How many wire pieces a plan needs. </summary>
        public static int WirePieceCount(int spanCount)
        {
            if (spanCount <= 0) { return 0; }
            return (spanCount + SpansPerPiece - 1) / SpansPerPiece;
        }

        /// <summary> Build the masts and their brackets for one piece, or null if the range holds none.
        ///
        /// The origin is the first mast's base, subtracted from every vertex so the object placed at that base carries
        /// small local coordinates. </summary>
        public static Mesh BuildMastPiece(RailOverheadPlan plan, IList<RailAlignmentSample> samples,
            RailOverheadClass cls, int fromMast, int mastSpan, out Vector3 origin)
        {
            origin = Vector3.zero;
            if (plan == null || samples == null || samples.Count == 0) { return null; }
            int end = Mathf.Min(fromMast + mastSpan, plan.masts.Count);
            if (fromMast < 0 || fromMast >= end) { return null; }

            origin = plan.masts[fromMast].basePoint;
            MeshScratch buffer = new MeshScratch();
            for (int i = fromMast; i < end; i++)
            {
                AddMast(buffer, plan.masts[i], samples, cls, origin);
            }
            return buffer.ToMesh("RCM OHLE Masts");
        }

        /// <summary> Build the three wires for a run of spans, or null if the range holds none. Origin is the first
        /// mast of the first span in the range. </summary>
        public static Mesh BuildWirePiece(RailOverheadPlan plan, IList<RailAlignmentSample> samples,
            RailOverheadClass cls, int fromSpan, int spanSpan, out Vector3 origin)
        {
            origin = Vector3.zero;
            if (plan == null || samples == null || samples.Count == 0) { return null; }
            int end = Mathf.Min(fromSpan + spanSpan, plan.spans.Count);
            if (fromSpan < 0 || fromSpan >= end) { return null; }

            origin = plan.masts[plan.spans[fromSpan].fromMastIndex].basePoint;
            MeshScratch buffer = new MeshScratch();
            for (int i = fromSpan; i < end; i++)
            {
                AddSpanWires(buffer, plan, plan.spans[i], samples, cls, origin);
            }
            return buffer.ToMesh("RCM OHLE Wires");
        }

        // ------------------------------------------------------------------ one mast

        /// <summary> One mast: the buried tapered shaft in submesh 0, and the registration bracket that reaches over the
        /// track to the contact wire in submesh 1.
        ///
        /// The shaft starts a foundation depth BELOW the mast's ground point and rises to a head that stands the class's
        /// own steel allowance above the messenger fixing. The bracket is a tube from the mast at messenger height out
        /// to the messenger's staggered attachment over the track, and a shorter drop tube from there down to the
        /// contact wire -- the registration arm that holds the wire at its stagger. </summary>
        private static void AddMast(MeshScratch buffer, RailOverheadMast mast, IList<RailAlignmentSample> samples,
            RailOverheadClass cls, Vector3 origin)
        {
            RailAlignmentSample sample = NearestSample(samples, mast.stationMetres);
            Vector3 right = LateralUnit(sample.tangent);
            Vector3 railHead = sample.position;

            Vector3 baseGround = mast.basePoint - origin;
            float shaftBottomY = baseGround.y - cls.mastFoundationDepthMetres;
            float messengerY = railHead.y + mast.messengerHeightMetres - origin.y;
            float headY = messengerY + cls.mastTopAboveMessengerMetres;
            float shaftHeight = headY - shaftBottomY;
            if (shaftHeight <= 0f) { return; }

            Vector3 shaftBase = new Vector3(baseGround.x, shaftBottomY, baseGround.z);
            buffer.AddTaperedTube(shaftBase, shaftHeight, cls.mastBaseDiameterMetres, cls.mastTopDiameterMetres,
                MastSides, 0);

            float lateral = mast.staggerMetres * mast.staggerSign;
            Vector3 centreXZ = new Vector3(railHead.x - origin.x, 0f, railHead.z - origin.z);
            Vector3 contactAttach = new Vector3(centreXZ.x + right.x * lateral,
                railHead.y + mast.contactWireHeightMetres - origin.y, centreXZ.z + right.z * lateral);
            Vector3 messengerAttach = new Vector3(contactAttach.x, messengerY, contactAttach.z);

            // The bracket leaves the mast at the messenger's own height and reaches over the track to the messenger
            // fixing; a shorter registration tube then drops to the contact wire. Both are on the bracket submesh.
            Vector3 mastAtMessenger = new Vector3(baseGround.x, messengerY, baseGround.z);
            buffer.AddSegment(mastAtMessenger, messengerAttach, cls.bracketTubeDiameterMetres, BracketSides, 1);
            buffer.AddSegment(messengerAttach, contactAttach, cls.bracketTubeDiameterMetres * 0.6f, BracketSides, 1);
        }

        // ------------------------------------------------------------------ one span's wires

        /// <summary> The messenger, the contact wire and the droppers of one span, all in submesh 0.
        ///
        /// The messenger is the solved catenary sampled along the span and hung between the two masts' real world
        /// attachment points: its dip below the straight chord joining them is <c>chord(x) - HeightAt(x)</c> in the
        /// solver's own rail-head units, applied as a vertical drop from the 3D chord. Anchoring both ends at the masts
        /// rather than trusting a rail-head datum keeps the wire on its brackets on a graded span, where the two rail
        /// heads differ. The contact wire is a straight chord -- that is what the droppers are for -- and each dropper
        /// is a short tube from the sampled messenger down to the sampled contact line. </summary>
        private static void AddSpanWires(MeshScratch buffer, RailOverheadPlan plan, RailOverheadSpan span,
            IList<RailAlignmentSample> samples, RailOverheadClass cls, Vector3 origin)
        {
            RailOverheadMast a = plan.masts[span.fromMastIndex];
            RailOverheadMast b = plan.masts[span.toMastIndex];

            Vector3 contactA, messengerA, contactB, messengerB;
            AttachPoints(a, samples, origin, out contactA, out messengerA);
            AttachPoints(b, samples, origin, out contactB, out messengerB);

            // Messenger: sampled catenary, hung between the two attachment points with the sag as a drop from the chord.
            List<Vector3> messengerPoints = new List<Vector3>(MessengerSegmentsPerSpan + 1);
            for (int s = 0; s <= MessengerSegmentsPerSpan; s++)
            {
                float t = (float)s / MessengerSegmentsPerSpan;
                messengerPoints.Add(MessengerPointAt(span, messengerA, messengerB, a, b, t));
            }
            buffer.AddPolylineTube(messengerPoints, cls.messengerWireDiameterMetres, WireSides, 0);

            // Contact wire: a straight chord between the two contact attachments.
            buffer.AddSegment(contactA, contactB, cls.contactWireDiameterMetres, WireSides, 0);

            // Droppers: from the sampled messenger down to the straight contact line at each dropper's offset.
            for (int d = 0; d < plan.droppers.Count; d++)
            {
                RailOverheadDropper dropper = plan.droppers[d];
                if (dropper.spanIndex != span.fromMastIndex) { continue; }
                float t = span.spanMetres > 0f ? dropper.offsetInSpanMetres / span.spanMetres : 0f;
                Vector3 top = MessengerPointAt(span, messengerA, messengerB, a, b, t);
                Vector3 bottom = Vector3.Lerp(contactA, contactB, t);
                buffer.AddSegment(top, bottom, cls.dropperWireDiameterMetres, WireSides, 0);
            }
        }

        /// <summary> The world contact and messenger attachment points at one mast, origin-subtracted. The stagger is
        /// laid along the lateral unit of the mast's own nearest sample, so the wire sits over the track and not over
        /// the offset foundation. </summary>
        private static void AttachPoints(RailOverheadMast mast, IList<RailAlignmentSample> samples, Vector3 origin,
            out Vector3 contact, out Vector3 messenger)
        {
            RailAlignmentSample sample = NearestSample(samples, mast.stationMetres);
            Vector3 right = LateralUnit(sample.tangent);
            Vector3 railHead = sample.position;
            float lateral = mast.staggerMetres * mast.staggerSign;
            float cx = railHead.x - origin.x + right.x * lateral;
            float cz = railHead.z - origin.z + right.z * lateral;
            contact = new Vector3(cx, railHead.y + mast.contactWireHeightMetres - origin.y, cz);
            messenger = new Vector3(cx, railHead.y + mast.messengerHeightMetres - origin.y, cz);
        }

        /// <summary> The messenger's world point a fraction along a span. The plan point interpolates the two masts'
        /// attachment points; the catenary's dip below its own chord is then subtracted so both ends stay pinned to the
        /// brackets and the middle sags. </summary>
        private static Vector3 MessengerPointAt(RailOverheadSpan span, Vector3 messengerA, Vector3 messengerB,
            RailOverheadMast a, RailOverheadMast b, float t)
        {
            Vector3 chord = Vector3.Lerp(messengerA, messengerB, t);
            double x = span.spanMetres * t;
            double chordHeight = a.messengerHeightMetres + (b.messengerHeightMetres - a.messengerHeightMetres) * t;
            double dip = chordHeight - span.messenger.HeightAt(x);
            if (dip < 0.0) { dip = 0.0; }
            chord.y -= (float)dip;
            return chord;
        }

        // ------------------------------------------------------------------ shared geometry

        /// <summary> The sample nearest a station. Nearest rather than interpolated, matching
        /// <see cref="RailOverheadPlanner"/>: the mast stands at a sampled point, and interpolating would invent a
        /// tangent the alignment never staked. </summary>
        private static RailAlignmentSample NearestSample(IList<RailAlignmentSample> samples, float station)
        {
            int best = 0;
            float bestGap = float.PositiveInfinity;
            for (int i = 0; i < samples.Count; i++)
            {
                float gap = Mathf.Abs(samples[i].stationMetres - station);
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return samples[best];
        }

        /// <summary> The unit vector to the right of travel in the horizontal plane, the same derivation
        /// <see cref="RailOverheadPlanner"/> uses so the mesh's stagger lands on the same side the planner measured it
        /// on. The grade is dropped because a mast stands plumb. </summary>
        private static Vector3 LateralUnit(Vector3 tangent)
        {
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
            if (flat.sqrMagnitude < 1e-8f) { return Vector3.right; }
            flat.Normalize();
            return new Vector3(flat.z, 0f, -flat.x);
        }
    }
}
#endif
