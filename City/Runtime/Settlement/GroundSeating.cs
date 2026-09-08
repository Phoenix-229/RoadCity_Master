// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> C-17. Which way a seated object stands once it has been pulled to the ground. Per ELEMENT, never
    /// global: the difference between a park that reads as real and one that reads as decals is that a tree grows
    /// vertical out of a slope while a bench sits on it. </summary>
    public enum GroundAlignment
    {
        /// <summary> Upright whatever the ground does: a tree, a lamp column, a bollard, a post, a sign, a pole, a
        /// hydrant. Anything that GROWS or is DRIVEN into the ground is plumb, and tilting it is the single most
        /// obvious tell that a scene was scattered rather than placed. The default, because it is the safe wrong
        /// answer -- a vertical bench looks stiff, a tilted tree looks broken. </summary>
        Vertical = 0,

        /// <summary> Tilted onto the local surface plane: a bench, a bin, a picnic table, a paving slab, a cycle
        /// stand -- anything that RESTS on the ground and would show daylight under one end if it did not follow it.
        /// </summary>
        SurfaceNormal = 1
    }


    /// <summary> The authored half of the pull-to-ground: the three numbers that are not derived from the surface, each
    /// tied to a figure this package already uses rather than picked here.
    ///
    /// <see cref="minimumEmbedMetres"/> sits BELOW <c>GroundDriftClass.supportToleranceMetres</c> (25 mm, the finished
    /// formation earthworks tolerance), so seating an object can never by itself push it outside the tolerance the same
    /// package elsewhere calls agreement. <see cref="maximumEmbedMetres"/> IS
    /// <c>GroundDriftClass.visibleMismatchMetres</c> (150 mm, about one kerb upstand): past that a base is not being
    /// seated, it is being sunk, and the object should be moved or the ground graded instead.
    /// <see cref="maximumTiltDegrees"/> is atan(1/3) -- the conventional maximum stable graded earthwork batter -- so an
    /// object on a face steeper than anything a site would be built to is stood up rather than laid against it, and the
    /// clamp is REPORTED so the caller can tell that happened. </summary>
    public sealed class GroundSeatingClass
    {
        public GroundAlignment alignment = GroundAlignment.Vertical;
        public float minimumEmbedMetres = 0.010f;
        public float maximumEmbedMetres = 0.150f;
        public float maximumTiltDegrees = 18.435f;

        public static GroundSeatingClass Default() { return new GroundSeatingClass(); }

        /// <summary> Anything that GROWS out of the ground or is DRIVEN into it: a tree, a shrub, a grass clump, a lamp
        /// column, a post, a sign, a bollard, a hydrant. Plumb. Named for the relationship rather than for one family,
        /// because the vegetation the author places and the lamp column the tool places want the same answer for the
        /// same reason. </summary>
        public static GroundSeatingClass ForDrivenOrGrown()
        {
            GroundSeatingClass cls = new GroundSeatingClass();
            cls.alignment = GroundAlignment.Vertical;
            return cls;
        }

        /// <summary> Anything that RESTS on the ground rather than standing in it: a bench, a bin, a picnic table, a
        /// paving slab, a cycle stand. Follows the local surface, because one that did not would show daylight under
        /// one end. </summary>
        public static GroundSeatingClass ForResting()
        {
            GroundSeatingClass cls = new GroundSeatingClass();
            cls.alignment = GroundAlignment.SurfaceNormal;
            return cls;
        }
    }
    /// <summary> What one seating produced. Every number the decision was made from comes back out, because the caller
    /// writes a remark and a remark that says "seated" without saying how deep or how far off plumb cannot be checked
    /// against the scene by the person reading it.
    ///
    /// <see cref="seated"/> false is not an error: it is the honest answer when there is no surface reader, or the reader
    /// gave back something that is not a surface. The <see cref="position"/> is then the planned one, UNCHANGED, so a
    /// caller that ignores the flag places the object exactly where it would have placed it before -- a wrong height is a
    /// worse failure than an un-pulled one, because it cannot be told apart from a measurement. </summary>
    public struct GroundSeat
    {
        public readonly bool seated;
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        /// <summary> The surface height sampled at the placement, before the embed was taken off it. </summary>
        public readonly float surfaceHeightMetres;

        /// <summary> How far below that surface the base was put. Positive downward. </summary>
        public readonly float embedMetres;

        /// <summary> The angle between world up and the object's own up after any clamp -- 0 for everything plumb, and
        /// never more than the class's cap. </summary>
        public readonly float tiltDegrees;

        /// <summary> One clause for the caller's remark. Empty when nothing needs saying. </summary>
        public readonly string note;

        public GroundSeat(bool seated, Vector3 position, Quaternion rotation, float surfaceHeightMetres,
            float embedMetres, float tiltDegrees, string note)
        {
            this.seated = seated;
            this.position = position;
            this.rotation = rotation;
            this.surfaceHeightMetres = surfaceHeightMetres;
            this.embedMetres = embedMetres;
            this.tiltDegrees = tiltDegrees;
            this.note = note ?? string.Empty;
        }

        /// <summary> The planned placement handed straight back with the reason it could not be improved on. </summary>
        public static GroundSeat NotSeated(Vector3 plannedPosition, Quaternion plannedRotation, string why)
        {
            return new GroundSeat(false, plannedPosition, plannedRotation, plannedPosition.y, 0f, 0f, why);
        }
    }
    /// <summary> C-17. The pull-to-ground, and the division of labour it exists to serve: the tool decides WHERE a thing
    /// goes and HOW it meets the ground; the author supplies the prefab that goes there. For natural content -- trees,
    /// flowers, grass -- the tool places nothing at all, it leaves a placement, and this is what makes that placement
    /// land on the ground instead of hanging over it.
    ///
    /// THE THREE DECISIONS ARE MADE HERE, not left to a caller, because a caller that had to choose would choose
    /// differently in each of the four places that place objects:
    ///
    ///   1. EMBED DEPTH IS DERIVED, NOT A CONSTANT. A flat base of radius r standing plumb on a slope of angle s floats
    ///      its downhill edge by r*tan(s) -- that is where daylight shows, and that is therefore the depth. The radius
    ///      comes from the prefab's own measured footprint (<see cref="PrefabPlacementSpec.footprintSize"/>), so a street
    ///      tree and a bollard get different depths from the same code and neither gets a number somebody picked.
    ///   2. ALIGNMENT IS PER ELEMENT. <see cref="GroundAlignment"/> carries it, and <see cref="GroundSeatingClass"/> has
    ///      a named preset per family rather than a bool a caller might set either way.
    ///   3. THE SURFACE SAMPLED IS WHATEVER THE READER HOLDS WHEN THIS IS CALLED, so ORDERING IS THE CALLER'S DUTY AND
    ///      THE WHOLE BUG. Call it AFTER the road and pad conform has written the heightmap -- with
    ///      <c>TerrainWriteGateway.SurfaceReader</c>, which reads the terrain as it now is -- or a tree is pulled to the
    ///      ground the site USED to have and stands proud of the verge that was cut beside it.
    ///
    /// WHAT IT IS NOT. It does not judge whether a placement is any good: a base half over a kerb still seats, on the
    /// surface it finds. That is C-18's footprint scan, which stations the whole footprint and can see a dent. This
    /// samples one point and answers one question. Nothing here writes to the terrain. </summary>
    public static class GroundSeating
    {
        #region "Pure -- geometry, no reader"
        /// <summary> Pass this as the embed depth to have it derived from the footprint and the slope. Negative because
        /// no real depth is, so an element's parameter row can hold "derive it" in the same float as "80 mm" and there is
        /// no second flag to get out of step with it. </summary>
        public const float DeriveEmbedFromFootprint = -1f;

        /// <summary> Half the diagonal of the plan rectangle: the furthest any part of the base sits from the pivot, and
        /// so the edge that lifts first. <see cref="PrefabContract"/> fixes the pivot at the base and plan-centred, which
        /// is what makes half the diagonal the right radius rather than half the width.
        ///
        /// Zero when the footprint was never measured. The content catalogue records no bounds for furniture, so
        /// <see cref="Vector2.zero"/> is the normal case there, not a fault -- and a radius guessed for it would be read
        /// downstream as a measurement. </summary>
        public static float FootprintRadiusMetres(Vector2 footprintSizeMetres)
        {
            float width = Mathf.Max(0f, footprintSizeMetres.x);
            float depth = Mathf.Max(0f, footprintSizeMetres.y);
            if (width <= 0f && depth <= 0f) { return 0f; }
            return 0.5f * Mathf.Sqrt((width * width) + (depth * depth));
        }

        /// <summary> tan(slope) read off a surface normal: the horizontal part of the normal over its vertical part. Used
        /// rather than the angle because the height a slope drops across a distance IS distance*tan, and going through
        /// degrees would be two trig calls to get back to the same number.
        ///
        /// Capped at 1000 for a normal lying flat enough to be an overhang, where the true value is infinite. The cap
        /// never reaches a result: every depth built from it goes through
        /// <see cref="GroundSeatingClass.maximumEmbedMetres"/>. </summary>
        public static float SlopeTangent(Vector3 surfaceNormal)
        {
            Vector3 n = surfaceNormal.normalized;
            float vertical = Mathf.Abs(n.y);
            float horizontal = new Vector2(n.x, n.z).magnitude;
            if (vertical <= 0.001f) { return 1000f; }
            return horizontal / vertical;
        }
        #endregion


        #region "Embed depth -- the derivation, not a magic number"
        /// <summary> How deep the base goes below the sampled surface.
        ///
        /// A PLUMB base on a slope is the case that needs depth: the base stays horizontal, the ground does not, and the
        /// downhill edge lifts by radius*tan(slope). Sink the object by exactly that and the downhill edge is flush while
        /// the uphill edge is buried -- which is what a post driven into a bank actually looks like, and is why the answer
        /// is the full drop rather than half of it. Half would leave half the daylight, which is the defect.
        ///
        /// A SURFACE-ALIGNED base has no drop to bridge: it was rotated into the same plane the ground is in, so it needs
        /// only the hairline that keeps a coplanar face from z-fighting the terrain, and that is
        /// <see cref="GroundSeatingClass.minimumEmbedMetres"/> whatever the slope is.
        ///
        /// An UNMEASURED footprint gets the same hairline, for the same reason it gets no radius: the depth a bench needs
        /// is a fact about the bench, and the tool does not have it. It is a visible seam on a steep verge and it is
        /// meant to be -- <see cref="PrefabPlacementSpec.footprintSize"/> filled in removes it. </summary>
        public static float EmbedDepthMetres(Vector2 footprintSizeMetres, Vector3 surfaceNormal, GroundSeatingClass cls)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();
            float floor = Mathf.Max(0f, use.minimumEmbedMetres);
            float ceiling = Mathf.Max(floor, use.maximumEmbedMetres);

            if (use.alignment == GroundAlignment.SurfaceNormal) { return floor; }

            float radius = FootprintRadiusMetres(footprintSizeMetres);
            if (radius <= 0f) { return floor; }

            float drop = radius * SlopeTangent(surfaceNormal);
            return Mathf.Clamp(drop, floor, ceiling);
        }

        /// <summary> The authored depth if there is one, the derived depth if the caller asked for it. A depth given as
        /// zero is honoured as zero -- an author who says "sit it exactly on the surface" has said something, and turning
        /// that into the hairline would be overriding them. Only <see cref="DeriveEmbedFromFootprint"/>, or any other
        /// negative, asks for the derivation. </summary>
        public static float ResolveEmbedMetres(float requestedEmbedMetres, Vector2 footprintSizeMetres,
            Vector3 surfaceNormal, GroundSeatingClass cls)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();
            if (requestedEmbedMetres < 0f)
            {
                return EmbedDepthMetres(footprintSizeMetres, surfaceNormal, use);
            }
            return Mathf.Min(requestedEmbedMetres, Mathf.Max(0f, use.maximumEmbedMetres));
        }
        #endregion


        #region "Orientation -- yaw is the tool's, tilt is the ground's"
        /// <summary> The rotation to stand the object in. The YAW is never touched: the operator decided which way the
        /// bench faces the footway and which way the sign faces the traffic, and the ground does not get a vote on that.
        /// What alignment decides is only whether the object's up is world up or the surface's.
        ///
        /// The clamp is <see cref="Quaternion.RotateTowards"/> from plumb toward laid-on-the-plane, which caps the whole
        /// angular delta rather than the tilt alone -- so the returned <paramref name="tiltDegrees"/> is measured back off
        /// the RESULT instead of being reported as the cap on trust. A normal that points sideways or downward is not a
        /// surface anything rests on, and gets plumb: better a stiff bench than one lying on its side. </summary>
        public static Quaternion Orientation(Quaternion plannedFacing, Vector3 surfaceNormal, GroundSeatingClass cls,
            out float tiltDegrees)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();
            tiltDegrees = 0f;

            Vector3 forward = plannedFacing * Vector3.forward;
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.000001f) { flat = Vector3.forward; }
            flat = flat.normalized;
            Quaternion plumb = Quaternion.LookRotation(flat, Vector3.up);

            if (use.alignment == GroundAlignment.Vertical) { return plumb; }

            Vector3 n = surfaceNormal.normalized;
            if (n.sqrMagnitude < 0.000001f || n.y <= 0f) { return plumb; }

            Vector3 onPlane = Vector3.ProjectOnPlane(flat, n);
            if (onPlane.sqrMagnitude < 0.000001f)
            {
                // The planned facing was straight up the normal, which only happens on a wall. Any direction in the plane
                // is as good as any other, so take a repeatable one rather than leaving LookRotation to fail.
                onPlane = Vector3.ProjectOnPlane(Vector3.Cross(n, Vector3.up), n);
                if (onPlane.sqrMagnitude < 0.000001f) { return plumb; }
            }

            Quaternion laid = Quaternion.LookRotation(onPlane.normalized, n);
            Quaternion seated = Quaternion.RotateTowards(plumb, laid, Mathf.Max(0f, use.maximumTiltDegrees));
            tiltDegrees = Vector3.Angle(Vector3.up, seated * Vector3.up);
            return seated;
        }
        #endregion


        #region "Seat -- the one call a placer makes"
        /// <summary> Pull one planned placement to the ground.
        ///
        /// Only X and Z of <paramref name="plannedPosition"/> are used when the ground can be read; its Y is what comes
        /// back when it cannot, which is why the whole vector is taken rather than a pair of floats. The reader is asked
        /// for the NORMAL first: a zero-length normal is not a surface under any interpretation, so it is the one
        /// reader-health test that does not need to know whether an empty chunk list means "all loaded" (standalone) or
        /// "nothing loaded" (connected). Whether the point is inside loaded ground is the caller's business, because the
        /// caller is the one that knows which mode it built the request in.
        ///
        /// <see cref="GroundContactMode"/> decides how much of the class applies, and two of the three modes deliberately
        /// ignore most of it:
        ///   * <c>OnPad</c> -- the pad was cut flat and level for this object, so the surface height IS the pad elevation
        ///     and there is nothing to bridge. Embedding a building into its own pad would sink it, and tilting it onto a
        ///     level plane is a no-op that only risks a rounding wobble, so both are refused outright.
        ///   * <c>Piled</c> -- the pad marks the reference plane and the ground was never brought up. Seated at the
        ///     surface with no embed, and the note says plainly that pile length is not modelled here, because a caller
        ///     that assumed it was would be building a stilt house standing in the mud.
        ///   * <c>SkirtToTerrain</c> -- the full behaviour, and what the furniture and vegetation paths pass. </summary>
        public static GroundSeat Seat(ITerrainSurfaceReader surface, Vector3 plannedPosition, Quaternion plannedFacing,
            GroundContactMode mode, GroundSeatingClass cls, Vector2 footprintSizeMetres, float requestedEmbedMetres)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();

            if (surface == null)
            {
                return GroundSeat.NotSeated(plannedPosition, plannedFacing,
                    "no terrain surface reader was supplied, so this is the planned height and not a measured one");
            }

            Vector3 normal = surface.SampleNormal(plannedPosition.x, plannedPosition.z);
            if (normal.sqrMagnitude < 0.000001f)
            {
                return GroundSeat.NotSeated(plannedPosition, plannedFacing,
                    "the terrain reader returned no surface normal here, so there was nothing to seat against");
            }

            float height = surface.SampleHeight(plannedPosition.x, plannedPosition.z);
            if (float.IsNaN(height) || float.IsInfinity(height))
            {
                return GroundSeat.NotSeated(plannedPosition, plannedFacing,
                    "the terrain reader returned no usable height here, so there was nothing to seat against");
            }

            return SeatAgainst(plannedPosition, plannedFacing, mode, use, footprintSizeMetres, requestedEmbedMetres,
                height, normal);
        }
        #endregion


        #region "SeatAgainst -- the same decision, against a height and normal already in hand"
        /// <summary> Everything <see cref="Seat"/> does after its two samples. Public for two reasons: a gate can exercise
        /// the whole decision with a hand-written height and normal and no terrain at all, and a caller that already
        /// sampled the surface for its own reasons should not pay for sampling it twice. </summary>
        public static GroundSeat SeatAgainst(Vector3 plannedPosition, Quaternion plannedFacing, GroundContactMode mode,
            GroundSeatingClass cls, Vector2 footprintSizeMetres, float requestedEmbedMetres, float surfaceHeightMetres,
            Vector3 surfaceNormal)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();
            Vector3 onSurface = new Vector3(plannedPosition.x, surfaceHeightMetres, plannedPosition.z);

            if (mode == GroundContactMode.OnPad)
            {
                return new GroundSeat(true, onSurface, Plumb(plannedFacing), surfaceHeightMetres, 0f, 0f,
                    "on its pad, which was cut level for it: no embed and no tilt");
            }
            if (mode == GroundContactMode.Piled)
            {
                return new GroundSeat(true, onSurface, Plumb(plannedFacing), surfaceHeightMetres, 0f, 0f,
                    "on piles: seated at the natural surface, and pile length is not modelled here");
            }

            float embed = ResolveEmbedMetres(requestedEmbedMetres, footprintSizeMetres, surfaceNormal, use);
            float tilt;
            Quaternion rotation = Orientation(plannedFacing, surfaceNormal, use, out tilt);

            return new GroundSeat(true, new Vector3(plannedPosition.x, surfaceHeightMetres - embed, plannedPosition.z),
                rotation, surfaceHeightMetres, embed, tilt,
                Note(use, footprintSizeMetres, requestedEmbedMetres, surfaceNormal, embed));
        }

        /// <summary> Yaw only, up is world up. <see cref="GroundSeatingClass.Default"/> is
        /// <see cref="GroundAlignment.Vertical"/>, so this is <see cref="Orientation"/> with the tilt half turned off
        /// rather than a second copy of the yaw-flattening. </summary>
        private static Quaternion Plumb(Quaternion plannedFacing)
        {
            float ignored;
            return Orientation(plannedFacing, Vector3.up, GroundSeatingClass.Default(), out ignored);
        }
        #endregion


        #region "Note -- the one clause that says a limit bit"
        /// <summary> At most one clause, most consequential first, and empty when the seat is unremarkable. One rather
        /// than three because these are written into a bake remark an author reads in a list, and a remark that reports
        /// every bound it did not exceed is a remark nobody finishes reading.
        ///
        /// All three cases are a limit BITING, which is the only thing worth a sentence: the object is seated either way,
        /// and what the author needs to know is that the ground here is outside what seating can hide. </summary>
        public static string Note(GroundSeatingClass cls, Vector2 footprintSizeMetres, float requestedEmbedMetres,
            Vector3 surfaceNormal, float embedMetres)
        {
            GroundSeatingClass use = cls ?? GroundSeatingClass.Default();
            float slopeDegrees = Vector3.Angle(Vector3.up, surfaceNormal.normalized);

            if (use.alignment == GroundAlignment.SurfaceNormal && slopeDegrees > use.maximumTiltDegrees + 0.01f)
            {
                return "the ground here falls at " + Degrees(slopeDegrees) + ", steeper than the "
                    + Degrees(use.maximumTiltDegrees) + " a graded batter is built to, so it was stood up to that cap "
                    + "instead of laid flat against the face";
            }

            bool derived = requestedEmbedMetres < 0f;
            if (derived && use.alignment == GroundAlignment.Vertical
                && embedMetres >= use.maximumEmbedMetres - 0.0001f && FootprintRadiusMetres(footprintSizeMetres) > 0f)
            {
                return "the ground falls further across this base than the " + Millimetres(use.maximumEmbedMetres)
                    + " embed limit can bridge, so the downhill edge is still proud: move it, or grade the ground here";
            }

            if (derived && FootprintRadiusMetres(footprintSizeMetres) <= 0f)
            {
                return "the footprint is unmeasured, so the embed is the " + Millimetres(embedMetres)
                    + " minimum rather than a depth derived from the base radius";
            }

            return string.Empty;
        }

        private static string Degrees(float value)
        {
            return value.ToString("0.#", CultureInfo.InvariantCulture) + " degrees";
        }

        private static string Millimetres(float metres)
        {
            return (metres * 1000f).ToString("0", CultureInfo.InvariantCulture) + " mm";
        }
        #endregion
    }
}
