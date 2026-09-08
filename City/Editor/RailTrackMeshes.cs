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
    /// <summary> B-27 / D-5's geometry half: the railway body -- a ballast prism, sleepers, rails and level-crossing
    /// panels -- as meshes swept along an alignment <see cref="RailAlignmentPlanner"/> has already staked and refused.
    ///
    /// It lives in the editor assembly for the same reason <see cref="MeshScratch"/> does: RM-62's provenance gate
    /// scans <c>City/Runtime</c> and <c>Scripts/CrossSection</c> for the mesh-building tokens and finds none, which is
    /// the tool's own statement that vertices are written on the editor side and BAKED, never assembled at play time.
    ///
    /// Nothing here is a second sweeper. Every strip goes through <see cref="MeshScratch.AddSweptStrip"/>, the one
    /// method that turns a profile and a line of frames into vertices. What this file owns is the PROFILES -- the
    /// prism, the 60E1 outline, a panel with real flangeway grooves -- and the FRAMES, which is where a railway stops
    /// being a road: a road's sweep can take its basis from the spline it follows, but a rail frame is ROLLED by the
    /// CANT the planner worked out, so it has to be built from the sample and cannot come from a curve's own basis.
    ///
    /// A run is cut into PIECES rather than built as one mesh, because a ten-kilometre line is one draw call that is
    /// never off screen and a phone renders every metre of it to show a hundred. Pieces SHARE their boundary sample,
    /// so consecutive pieces meet on the same ring of vertices and there is no seam to see.
    ///
    /// Two meshes per piece, not one. There are three material families on a railway -- aggregate, sleeper and steel --
    /// and <see cref="MeshScratch.SubmeshCount"/> is 2 by contract for the whole pole family, so the formation
    /// (ballast on submesh 0, sleepers on submesh 1) and the rails are separate renderers. Forcing sleepers to wear
    /// the ballast material to fit one mesh would be the geometry lying about what a railway is made of. </summary>
    public static class RailTrackMeshes
    {
        #region "The 60E1 rail outline"
        /// <summary> The three figures of the 60E1 (UIC 60) section that are NOT authored fields, kept here as
        /// constants beside the geometry that uses them.
        ///
        /// <see cref="RailFormationClass"/> carries the rail's height, head width and foot width because those are the
        /// figures a formation is dimensioned by -- the gauge is measured against the head, the sleeper is designed
        /// against the foot. These three are the section's INTERNAL shape, and a 60E1 with a different web thickness is
        /// not a 60E1. An inspector field for them would give an author nothing to gain and a standard rail profile to
        /// break, so they are constants: web 16.5 mm, head depth 49.5 mm, foot thickness 11.5 mm. </summary>
        public const float WebThicknessMetres = 0.0165f;
        public const float HeadDepthMetres = 0.0495f;
        public const float FootThicknessMetres = 0.0115f;
        #endregion


        #region "How a run is cut into pieces"
        /// <summary> How many samples one piece spans. 48 samples is 47 intervals, which at the default 5 m sampling is
        /// a piece a little under 240 m long -- short enough that a phone culls most of it and long enough that
        /// a ten-kilometre run is forty-odd renderers and not four hundred. It is a whole number of SAMPLES rather than
        /// a length in metres so that a piece boundary always lands ON a sample: a boundary between samples would need
        /// an interpolated ring at each end, and two interpolated rings computed twice are how a hairline seam
        /// appears. </summary>
        public const int SamplesPerPiece = 48;

        /// <summary> How many pieces a run of this many samples is cut into. Zero for fewer than two samples: one
        /// sample is a point, and a point has no segment to sweep. </summary>
        public static int PieceCount(int sampleCount)
        {
            int intervals = sampleCount - 1;
            if (intervals < 1) { return 0; }
            int per = SamplesPerPiece - 1;
            return (intervals + per - 1) / per;
        }

        /// <summary> The sample range one piece covers. Pieces SHARE their boundary sample -- piece 0 ends on the same
        /// sample piece 1 starts on -- so the two sweeps put rings in the same place and the meshes butt without a
        /// gap. Returns false for a range with nothing to sweep. </summary>
        public static bool PieceRange(int sampleCount, int piece, out int from, out int count)
        {
            from = 0;
            count = 0;
            if (piece < 0 || sampleCount < 2) { return false; }
            int per = SamplesPerPiece - 1;
            from = piece * per;
            if (from >= sampleCount - 1) { return false; }
            int last = Mathf.Min(sampleCount - 1, from + per);
            count = last - from + 1;
            return count >= 2;
        }

        /// <summary> Where a piece's vertices are measured from: its first sample. A run staked ten kilometres out from
        /// the world origin would otherwise put every vertex at that distance in a float, and the wobble that produces
        /// is visible on a rail head long before it is visible on a road. </summary>
        public static Vector3 PieceOrigin(IList<RailAlignmentSample> samples, int from)
        {
            if (samples == null || from < 0 || from >= samples.Count) { return Vector3.zero; }
            return samples[from].position;
        }
        #endregion


        #region "The rolled frame"
        /// <summary> The frame at one sample: which way is right and which way is up, with the CANT rolled in.
        ///
        /// This is the method that makes a railway not a road. The right vector starts as the horizontal perpendicular
        /// to the tangent, and is then ROTATED about the tangent by the angle the applied cant asks for --
        /// phi = asin(E / G), the same rail-centre gauge G the equilibrium-cant formula uses, which is why
        /// <see cref="RailTrackClass.cantFormulaGaugeMillimetres"/> is 1500 mm and not the 1435 mm nominal gauge: the
        /// outer rail is raised by E measured across the RAIL CENTRES, so that is the hypotenuse the roll angle is read
        /// from. Using the nominal gauge here would over-roll the track by four per cent.
        ///
        /// The roll is about the CENTRELINE, not about the inner rail. Real track is canted by packing the outer rail
        /// up, which raises the track's centre by half the cant -- but the planner's vertical profile is already the
        /// profile of the centreline, gradient-checked against the ground at that height, and rolling about the inner
        /// rail would lift that whole profile by up to 75 mm without telling the profile it had moved. Rolling about
        /// the centre keeps the one line the gradient was proven on exactly where it was proven.
        ///
        /// Positive cant raises the rail on the right of travel, which is the sign convention the sample carries, so
        /// a positive angle tilts the right vector UP. </summary>
        public static void Frame(RailAlignmentSample sample, double cantGaugeMillimetres,
            out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            forward = sample.tangent.sqrMagnitude > 1e-12f ? sample.tangent.normalized : Vector3.forward;
            Vector3 flat = Vector3.Cross(Vector3.up, forward);
            right = flat.sqrMagnitude > 1e-10f ? flat.normalized : Vector3.right;
            up = Vector3.Cross(forward, right).normalized;

            double gauge = cantGaugeMillimetres > 1.0 ? cantGaugeMillimetres : 1500.0;
            double ratio = sample.appliedCantMillimetres / gauge;
            if (ratio > 1.0) { ratio = 1.0; }
            if (ratio < -1.0) { ratio = -1.0; }
            if (ratio == 0.0) { return; }

            float roll = (float)System.Math.Asin(ratio);
            float c = Mathf.Cos(roll);
            float s = Mathf.Sin(roll);
            Vector3 rolledRight = right * c + up * s;
            up = up * c - right * s;
            right = rolledRight;
        }
        #endregion


        #region "A frame between samples"
        /// <summary> The frame at any station inside a piece, interpolated between the two samples that bracket it.
        ///
        /// Sleepers sit at 0.6 m and the alignment is sampled at 5 m, so most sleepers fall between two samples and
        /// something must answer for the metres between. Interpolating the sample is the right answer rather than a
        /// shortcut: it is the SAME straight line the sweep draws between those samples, so a sleeper lands on
        /// the ballast the sweep built and not 1 cm inside or outside it. On the class minimum 300 m radius a 5 m chord
        /// departs from the true arc by 10 mm at its middle, and ballast and sleeper are both wrong by that same
        /// 10 mm in the same direction, which is the only way an interpolation error is invisible.
        ///
        /// The search window is the piece, not the run, so this is a walk of at most 48 samples however long the line
        /// is. </summary>
        public static bool FrameAt(IList<RailAlignmentSample> samples, int from, int count, float station,
            double cantGaugeMillimetres, out Vector3 position, out Vector3 forward, out Vector3 right, out Vector3 up)
        {
            position = Vector3.zero;
            forward = Vector3.forward;
            right = Vector3.right;
            up = Vector3.up;
            if (samples == null || count < 2 || from < 0 || from + count > samples.Count) { return false; }

            int last = from + count - 1;
            if (station <= samples[from].stationMetres) { station = samples[from].stationMetres; }
            if (station >= samples[last].stationMetres) { station = samples[last].stationMetres; }

            int i = from;
            while (i < last - 1 && samples[i + 1].stationMetres < station) { i++; }

            RailAlignmentSample a = samples[i];
            RailAlignmentSample b = samples[i + 1];
            float span = b.stationMetres - a.stationMetres;
            float t = span > 1e-4f ? Mathf.Clamp01((station - a.stationMetres) / span) : 0f;

            RailAlignmentSample between = a;
            between.position = Vector3.Lerp(a.position, b.position, t);
            between.tangent = Vector3.Lerp(a.tangent, b.tangent, t);
            between.appliedCantMillimetres = a.appliedCantMillimetres
                + t * (b.appliedCantMillimetres - a.appliedCantMillimetres);

            position = between.position;
            Frame(between, cantGaugeMillimetres, out forward, out right, out up);
            return true;
        }
        #endregion

        #region "The profiles, measured down from top of rail"
        /// <summary> Where the rail's foot is, relative to the sample: a rail height below it. The sample is the top
        /// of rail, so every figure in this file is negative and the ballast is the deepest of them. </summary>
        public static float RailBottomY(RailFormationClass form)
        {
            return -(form.railHeightMetres > 0f ? form.railHeightMetres : 0.172f);
        }

        /// <summary> The top of the ballast, which is the underside of the sleeper: a rail height plus a sleeper
        /// height below the sample. </summary>
        public static float BallastCrownY(RailFormationClass form)
        {
            float sleeper = form.sleeperHeightMetres > 0f ? form.sleeperHeightMetres : 0.2f;
            return RailBottomY(form) - sleeper;
        }

        /// <summary> The bottom of the ballast, the class depth below its crown. </summary>
        public static float BallastBaseY(RailFormationClass form)
        {
            float depth = form.ballastDepthMetres > 0f ? form.ballastDepthMetres : 0.3f;
            return BallastCrownY(form) - depth;
        }

        /// <summary> Half the distance between the two rails of one track, measured CENTRE to centre.
        ///
        /// The nominal gauge is measured between the running edges -- the inner faces of the two heads -- so the rail
        /// centres are one head width further apart than the gauge: 1.435 + 0.0716 = 1.5066 m. That is the figure
        /// <see cref="RailTrackClass.cantFormulaGaugeMillimetres"/> rounds to 1500 mm, and seeing that number arrive
        /// here from the geometry and there from the cant equation is the check that the two halves of this task agree
        /// about what a track is. </summary>
        public static float RailCentreOffsetMetres(RailTrackClass track, RailFormationClass form)
        {
            float gauge = track != null && track.trackGaugeMetres > 0f ? track.trackGaugeMetres : 1.435f;
            float head = form.railHeadWidthMetres > 0f ? form.railHeadWidthMetres : 0.0716f;
            return 0.5f * (gauge + head);
        }

        /// <summary> Where one track's centre sits across the formation. A single track is on the alignment; two tracks
        /// straddle it at half the class track centres each, so the alignment stays the line the curve radius and the
        /// gradient were checked on rather than becoming one of the two running lines. </summary>
        public static float TrackOffsetMetres(RailFormationClass form, int trackCount, int track)
        {
            int n = trackCount > 0 ? trackCount : 1;
            float centres = form.trackCentresMetres > 0f ? form.trackCentresMetres : 4f;
            return (track - (n - 1) * 0.5f) * centres;
        }
        #endregion


        #region "The two sections"
        /// <summary> The ballast prism, as an OPEN four-point profile: toe, crown, crown, toe.
        ///
        /// Open because the underside is against the ground. A closed prism would carry a floor nothing can see, and
        /// would double the section's face count for it, and on a phone that floor is paid for on every piece of every
        /// line. The two sloped flanks are the 1:1.5 side slope UIC 719R gives a ballast shoulder, so the toe stands
        /// out from the crown by the depth times the slope run.
        ///
        /// The prism is one shape across the WHOLE formation rather than one per track: a double-track railway is built
        /// on one bed of ballast with the four rails on it, not two beds side by side, and drawing it as two would put
        /// a pair of slopes in the six-inch gap between the sleeper ends where no slope exists. </summary>
        public static List<Vector2> BallastSection(RailFormationClass form, int trackCount)
        {
            float crownY = BallastCrownY(form);
            float baseY = BallastBaseY(form);
            float crownHalf = (float)(form.FormationWidthMetres(trackCount) * 0.5);
            float slope = form.ballastSideSlopeRun > 0f ? form.ballastSideSlopeRun : 1.5f;
            float toeHalf = crownHalf + (crownY - baseY) * slope;

            List<Vector2> section = new List<Vector2>(4);
            section.Add(new Vector2(-toeHalf, baseY));
            section.Add(new Vector2(-crownHalf, crownY));
            section.Add(new Vector2(crownHalf, crownY));
            section.Add(new Vector2(toeHalf, baseY));
            return section;
        }

        /// <summary> One rail's 60E1 outline, CLOSED, centred on <paramref name="centreX"/> across the frame.
        ///
        /// Thirteen points for twelve unique corners, the last repeating the first, because a rail is a solid seen from
        /// every side and a rail drawn as an open ribbon would be a rail with no inner face -- which is precisely the
        /// face a viewer standing between the tracks sees. Written CLOCKWISE in the frame plane, which is the winding
        /// <see cref="MeshScratch.AddSweptStrip"/> reads as outward: along the head top, down the head's outer side,
        /// in under the head, down the web, out along the foot, round its edge, back along its underside and up again.
        ///
        /// The head, the web and the foot are the real proportions of the section, not a stylised I: at two metres the
        /// difference between a 16.5 mm web and a 30 mm one is the difference between a rail and a girder. </summary>
        public static List<Vector2> RailSection(RailFormationClass form, float centreX)
        {
            float top = 0f;
            float bottom = RailBottomY(form);
            float headBottom = -HeadDepthMetres;
            float footTop = bottom + FootThicknessMetres;
            float headHalf = 0.5f * (form.railHeadWidthMetres > 0f ? form.railHeadWidthMetres : 0.0716f);
            float footHalf = 0.5f * (form.railFootWidthMetres > 0f ? form.railFootWidthMetres : 0.15f);
            float webHalf = 0.5f * WebThicknessMetres;

            List<Vector2> s = new List<Vector2>(13);
            s.Add(new Vector2(centreX - headHalf, top));
            s.Add(new Vector2(centreX + headHalf, top));
            s.Add(new Vector2(centreX + headHalf, headBottom));
            s.Add(new Vector2(centreX + webHalf, headBottom));
            s.Add(new Vector2(centreX + webHalf, footTop));
            s.Add(new Vector2(centreX + footHalf, footTop));
            s.Add(new Vector2(centreX + footHalf, bottom));
            s.Add(new Vector2(centreX - footHalf, bottom));
            s.Add(new Vector2(centreX - footHalf, footTop));
            s.Add(new Vector2(centreX - webHalf, footTop));
            s.Add(new Vector2(centreX - webHalf, headBottom));
            s.Add(new Vector2(centreX - headHalf, headBottom));
            s.Add(new Vector2(centreX - headHalf, top));
            return s;
        }
        #endregion


        #region "The formation: ballast and sleepers"
        /// <summary> One piece of formation: the ballast prism on submesh 0 and its sleepers on submesh 1.
        ///
        /// Vertices are local to the piece's first sample, so the caller positions the object AT that sample. Returns
        /// null rather than an empty mesh for a range with nothing in it, because an empty mesh on a renderer is a
        /// draw call that draws nothing and a null is a piece the builder simply does not create. </summary>
        public static Mesh BuildFormation(IList<RailAlignmentSample> samples, int from, int count,
            RailFormationClass form, RailTrackClass track, int trackCount, string name)
        {
            if (samples == null || form == null || count < 2) { return null; }
            if (from < 0 || from + count > samples.Count) { return null; }

            RailTrackClass t = track != null ? track : RailTrackClass.Default();
            int tracks = trackCount > 0 ? trackCount : 1;
            Vector3 origin = PieceOrigin(samples, from);

            List<Vector3> origins, rights, ups;
            PieceFrames(samples, from, count, t.cantFormulaGaugeMillimetres, origin, out origins, out rights, out ups);

            MeshScratch scratch = new MeshScratch();
            scratch.AddSweptStrip(origins, rights, ups, BallastSection(form, tracks), 0);
            AddSleepers(scratch, samples, from, count, form, t, tracks, origin);
            return scratch.ToMesh(name);
        }

        /// <summary> The sleepers of one piece, one box per track at every sleeper station.
        ///
        /// The stations come off the RUN's grid, not the piece's: the first sleeper of a piece is the first multiple of
        /// the class spacing at or after the piece's start, so the 0.6 m pattern runs unbroken through a piece boundary
        /// instead of restarting there and putting two sleepers 0.1 m apart at every seam.
        ///
        /// A sleeper exactly ON a boundary belongs to the piece that starts there and not to the one that ends there,
        /// which is what the half-open station window does. The final piece takes its closing station too, so the last
        /// sleeper of a run is built once rather than not at all.
        ///
        /// One box per TRACK, not one across the formation: two tracks on one bed have two sets of sleepers with a
        /// gap between the ends, and a single 6.6 m sleeper spanning both would be a bridge tie, which is a different
        /// component of a different railway. </summary>
        private static void AddSleepers(MeshScratch scratch, IList<RailAlignmentSample> samples, int from, int count,
            RailFormationClass form, RailTrackClass track, int trackCount, Vector3 origin)
        {
            float spacing = form.sleeperSpacingMetres > 0f ? form.sleeperSpacingMetres : 0.6f;
            float length = form.sleeperLengthMetres > 0f ? form.sleeperLengthMetres : 2.6f;
            float width = form.sleeperWidthMetres > 0f ? form.sleeperWidthMetres : 0.26f;
            float height = form.sleeperHeightMetres > 0f ? form.sleeperHeightMetres : 0.2f;
            float seat = RailBottomY(form) - height * 0.5f;

            float startStation = samples[from].stationMetres;
            float endStation = samples[from + count - 1].stationMetres;
            bool takesEnd = from + count == samples.Count;
            float first = Mathf.Ceil(startStation / spacing - 1e-4f) * spacing;

            for (float station = first; station < endStation + (takesEnd ? 1e-3f : -1e-3f); station += spacing)
            {
                Vector3 position, forward, right, up;
                if (!FrameAt(samples, from, count, station, track.cantFormulaGaugeMillimetres,
                        out position, out forward, out right, out up))
                {
                    return;
                }
                for (int k = 0; k < trackCount; k++)
                {
                    Vector3 centre = position - origin + right * TrackOffsetMetres(form, trackCount, k) + up * seat;
                    scratch.AddOrientedBox(centre, right * (length * 0.5f), up * (height * 0.5f),
                        forward * (width * 0.5f), 1);
                }
            }
        }
        #endregion


        #region "The rails"
        /// <summary> The rings of frames one piece is swept through, with the origins made local to the piece.
        ///
        /// Extracted rather than written twice: the formation and the rails are swept along the SAME frames, and two
        /// copies of this loop would be two chances for the ballast and the rails to disagree about where the track is
        /// banked to -- which would show as rails floating off their bed on every curve. </summary>
        private static void PieceFrames(IList<RailAlignmentSample> samples, int from, int count,
            double cantGaugeMillimetres, Vector3 origin, out List<Vector3> origins, out List<Vector3> rights,
            out List<Vector3> ups)
        {
            origins = new List<Vector3>(count);
            rights = new List<Vector3>(count);
            ups = new List<Vector3>(count);
            for (int i = 0; i < count; i++)
            {
                Vector3 f, r, u;
                Frame(samples[from + i], cantGaugeMillimetres, out f, out r, out u);
                origins.Add(samples[from + i].position - origin);
                rights.Add(r);
                ups.Add(u);
            }
        }

        /// <summary> One piece of rail: two rails per track, all on submesh 0.
        ///
        /// A separate renderer from the formation because a railway has three material families -- aggregate, sleeper,
        /// steel -- and <see cref="MeshScratch.SubmeshCount"/> is 2 for the whole family this mesh builder serves. The
        /// second submesh of this mesh is therefore EMPTY, and the builder writes the rail material into both slots.
        /// That is one unused sub-mesh entry, taken instead of either breaking the two-slot contract the
        /// pole line depends on or making the sleepers wear the ballast material to fit everything into two meshes.
        ///
        /// Rails are on the same frames as the ballast, so they are banked with it: the outer rail of a canted curve is
        /// raised because its whole frame is rolled, not because this method lifts it. </summary>
        public static Mesh BuildRails(IList<RailAlignmentSample> samples, int from, int count,
            RailFormationClass form, RailTrackClass track, int trackCount, string name)
        {
            if (samples == null || form == null || count < 2) { return null; }
            if (from < 0 || from + count > samples.Count) { return null; }

            RailTrackClass t = track != null ? track : RailTrackClass.Default();
            int tracks = trackCount > 0 ? trackCount : 1;
            Vector3 origin = PieceOrigin(samples, from);

            List<Vector3> origins, rights, ups;
            PieceFrames(samples, from, count, t.cantFormulaGaugeMillimetres, origin, out origins, out rights, out ups);

            MeshScratch scratch = new MeshScratch();
            float half = RailCentreOffsetMetres(t, form);
            for (int k = 0; k < tracks; k++)
            {
                float centre = TrackOffsetMetres(form, tracks, k);
                scratch.AddSweptStrip(origins, rights, ups, RailSection(form, centre - half), 0);
                scratch.AddSweptStrip(origins, rights, ups, RailSection(form, centre + half), 0);
            }
            return scratch.ToMesh(name);
        }
        #endregion


        #region "The level-crossing panel"
        /// <summary> The panel at one level crossing: the surface a road is carried across the track on, with a real
        /// flangeway groove beside every rail.
        ///
        /// Vertices are local to <see cref="RailLevelCrossing.position"/>, so the caller places the object there.
        ///
        /// The deck is at TOP OF RAIL, which is what makes a crossing a crossing: the road surface comes up to the rail
        /// head so a wheel rolls over it, and the 75 mm gap beside each rail's gauge face is left OPEN to a depth of
        /// 50 mm because that is the channel a train's wheel flange runs in. Modelling the groove is not detail for its
        /// own sake -- a crossing drawn as a flat slab over the rails is the one that reads instantly as wrong, because
        /// every real one has two dark lines in it.
        ///
        /// The rails run through UNBROKEN and are not redrawn here. They are continuous through a real crossing, and
        /// are continuous in <see cref="BuildRails"/>, so a second copy of them inside the panel would be two rails in
        /// one place, z-fighting along the head. That leaves this mesh one material family, so submesh 1 is empty and
        /// the builder writes the panel material into both slots, exactly as it does for the rails.
        ///
        /// The panel is banked with the track, because its frames are the track's frames. A crossing on canted track is
        /// a real defect and the planner already reports the cant at each one; drawing the deck level while the rails
        /// under it were canted would hide the defect the planner just reported. </summary>
        public static Mesh BuildCrossingPanel(IList<RailAlignmentSample> samples, RailLevelCrossing crossing,
            RailFormationClass form, RailTrackClass track, int trackCount, string name)
        {
            if (samples == null || samples.Count < 2 || form == null) { return null; }
            double length = crossing.panelLengthMetres;
            if (length <= 0.1) { return null; }

            RailTrackClass t = track != null ? track : RailTrackClass.Default();
            int tracks = trackCount > 0 ? trackCount : 1;
            float half = (float)(length * 0.5);
            int steps = Mathf.Max(2, Mathf.CeilToInt((float)length));

            List<Vector3> origins = new List<Vector3>(steps + 1);
            List<Vector3> rights = new List<Vector3>(steps + 1);
            List<Vector3> ups = new List<Vector3>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float station = crossing.railStationMetres - half + (float)i / steps * (float)length;
                Vector3 position, forward, r, u;
                if (!FrameAt(samples, 0, samples.Count, station, t.cantFormulaGaugeMillimetres,
                        out position, out forward, out r, out u))
                {
                    return null;
                }
                origins.Add(position - crossing.position);
                rights.Add(r);
                ups.Add(u);
            }

            MeshScratch scratch = new MeshScratch();
            List<List<Vector2>> ribbons = PanelSections(form, t, tracks);
            for (int i = 0; i < ribbons.Count; i++)
            {
                scratch.AddSweptStrip(origins, rights, ups, ribbons[i], 0);
            }
            return scratch.ToMesh(name);
        }

        /// <summary> The panel's cross-section, as one OPEN ribbon per gap between rails: the deck, the grooves,
        /// and the faces that drop from the deck to the ballast crown.
        ///
        /// One ribbon per gap rather than one long section, because the rail heads have to stay EXPOSED. A single
        /// section across the whole formation would have to pass through each rail, burying the head under the deck and
        /// making the crossing a slab with rails drawn inside it.
        ///
        /// A gauge gap -- between the two rails of one track -- carries a flangeway groove at each end: the floor is a
        /// lip at the flangeway depth below top of rail, and the 75 mm channel between it and the rail face is where
        /// the wheel flange runs. A FIELD gap -- outside a track, or between two tracks -- has none: real panels are
        /// filled to the rail head on the field side, and inventing a groove there would put a channel where a car tyre
        /// runs.
        ///
        /// Every ribbon closes down to the ballast crown at its ends, so the panel reads as a slab sitting on the bed
        /// rather than as a sheet floating 37 cm above it. The two ends ALONG the track are left open, exactly as the
        /// ballast prism's are: the road's own surface abuts within the 0.6 m margin the panel runs past it by, so the
        /// faces a cap would add are faces the road is already covering. </summary>
        private static List<List<Vector2>> PanelSections(RailFormationClass form, RailTrackClass track, int tracks)
        {
            float crownHalf = (float)(form.FormationWidthMetres(tracks) * 0.5);
            float crownY = BallastCrownY(form);
            float headHalf = 0.5f * (form.railHeadWidthMetres > 0f ? form.railHeadWidthMetres : 0.0716f);
            float groove = form.flangewayWidthMetres > 0f ? form.flangewayWidthMetres : 0.075f;
            float grooveY = -(form.flangewayDepthMetres > 0f ? form.flangewayDepthMetres : 0.05f);
            float railHalf = RailCentreOffsetMetres(track, form);

            List<float> rails = new List<float>(tracks * 2);
            for (int k = 0; k < tracks; k++)
            {
                float centre = TrackOffsetMetres(form, tracks, k);
                rails.Add(centre - railHalf);
                rails.Add(centre + railHalf);
            }

            List<List<Vector2>> ribbons = new List<List<Vector2>>();
            ribbons.Add(FieldGap(-crownHalf, rails[0] - headHalf, crownY));
            for (int j = 0; j + 1 < rails.Count; j++)
            {
                float a = rails[j] + headHalf;
                float b = rails[j + 1] - headHalf;
                if (b - a < 0.02f) { continue; }
                bool isGauge = (j % 2) == 0;
                if (isGauge && b - a > 2f * groove + 0.05f)
                {
                    ribbons.Add(GaugeGap(a, b, groove, grooveY, crownY));
                }
                else
                {
                    ribbons.Add(FieldGap(a, b, crownY));
                }
            }
            ribbons.Add(FieldGap(rails[rails.Count - 1] + headHalf, crownHalf, crownY));
            return ribbons;
        }

        /// <summary> A gap with no groove: the deck, and a face at each end dropping to the ballast crown. Written
        /// left to right so the deck's own winding faces up. </summary>
        private static List<Vector2> FieldGap(float leftX, float rightX, float crownY)
        {
            List<Vector2> section = new List<Vector2>(4);
            section.Add(new Vector2(leftX, crownY));
            section.Add(new Vector2(leftX, 0f));
            section.Add(new Vector2(rightX, 0f));
            section.Add(new Vector2(rightX, crownY));
            return section;
        }

        /// <summary> A gap BETWEEN the two rails of one track: the same deck, with a flangeway groove let into each
        /// end. Eight points -- up the rail face to the groove floor, out along the floor, up the groove's inner wall,
        /// across the deck, and the mirror of that at the other rail. </summary>
        private static List<Vector2> GaugeGap(float leftX, float rightX, float groove, float grooveY, float crownY)
        {
            List<Vector2> section = new List<Vector2>(8);
            section.Add(new Vector2(leftX, crownY));
            section.Add(new Vector2(leftX, grooveY));
            section.Add(new Vector2(leftX + groove, grooveY));
            section.Add(new Vector2(leftX + groove, 0f));
            section.Add(new Vector2(rightX - groove, 0f));
            section.Add(new Vector2(rightX - groove, grooveY));
            section.Add(new Vector2(rightX, grooveY));
            section.Add(new Vector2(rightX, crownY));
            return section;
        }
        #endregion
    }
}
#endif
