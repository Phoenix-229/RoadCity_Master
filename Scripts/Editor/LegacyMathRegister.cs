#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster
{
    /// <summary> What a numeric literal in the legacy code is actually FOR. The census exists because "magic number"
    /// was being used for four unrelated things at once, and only one of them is a road-geometry question. Separating
    /// them is most of the work: a UV coordinate has no real-world counterpart and never will, and demanding a citation
    /// for one is how a census turns into fabricated sources. </summary>
    public enum MathRole
    {
        /// <summary> A road dimension, distance, angle or gradient -- the only role where a design standard can apply. </summary>
        Geometry = 0,
        /// <summary> A value that decides how something LOOKS rather than where it is: a texture coordinate, a tiling
        /// rate, a colour channel, a gizmo's transparency, an index count. Dimensionless -- metres never enter it, so no
        /// design standard can govern it. Named for what it is rather than "TextureSpace", because a sodium lamp's
        /// colour belongs here and is not a texture coordinate; this census's own findings include two constants whose
        /// names lied about them, and repeating that mistake in the vocabulary would be poor form. </summary>
        Appearance = 1,
        /// <summary> A floating-point comparison epsilon or a vertex-coincidence tolerance. A modelling quantity: it
        /// says how close two computed points must be to count as the same point, which no standard governs. </summary>
        NumericalTolerance = 2,
        /// <summary> Inside a commented-out block or an unreachable branch. Produces nothing at run time. </summary>
        DeadCode = 3,
    }


    /// <summary> RM-94's four dispositions, one of which <see cref="Disposition"/> (RM-66's, for whole members) has no
    /// equivalent for, which is why this is a separate enum rather than a reuse. </summary>
    public enum MathDisposition
    {
        /// <summary> The value matches a cited standard and stays as it is. </summary>
        Sourced = 0,
        /// <summary> The value is wrong, or right by accident in a way that will not survive; <c>corrected</c> says
        /// what replaces it and the counterpart names the standard it comes from. </summary>
        Replace = 1,
        /// <summary> No standard governs the value; the stated reason says why, and it stays (C-15). </summary>
        AuthoredKeep = 2,
        /// <summary> The value is unreachable and goes away with the code around it. </summary>
        Delete = 3,
    }


    /// <summary> One geometric or physical computation in the legacy road code: where it is, what it produces, the
    /// literals it rests on, and the judgement. Deliberately NOT built on <see cref="NumberAudit"/>: that type answers
    /// "is this serialised asset value right", has three verdicts, and keys on a field name. This one answers "is this
    /// computation right", needs a fourth disposition for unreachable code, and keys on a member -- a computation can
    /// rest on several literals at once and often does. Forcing one type to do both would have meant expressing
    /// <see cref="MathDisposition.Delete"/> as an Authored note, which is a lie about the verdict. Immutable. </summary>
    public sealed class LegacyMathRow
    {
        public readonly string file;        // path relative to the tool root, forward slashes
        public readonly string member;      // the method or region the computation lives in
        public readonly string produces;    // what the computation yields, in words
        public readonly string[] literals;  // every literal the computation rests on, as written in the source
        public readonly MathRole role;
        public readonly MathDisposition disposition;
        public readonly string assumption;  // what the value assumes, or why it is what it is
        public readonly string counterpart; // the real-world counterpart, or "none: ..." with the reason
        public readonly string corrected;   // required for Replace: what the value becomes and where it comes from

        public LegacyMathRow(string file, string member, string produces, string[] literals,
            MathRole role, MathDisposition disposition, string assumption, string counterpart, string corrected)
        {
            this.file = file; this.member = member; this.produces = produces; this.literals = literals;
            this.role = role; this.disposition = disposition;
            this.assumption = assumption; this.counterpart = counterpart; this.corrected = corrected;
        }

        /// <summary> True when the row is internally complete for its disposition. Every row needs a place, a product,
        /// at least one literal, an assumption and a counterpart -- a counterpart of "none: ..." is an answer, an empty
        /// one is an unanswered question. A Replace row additionally needs the corrected value, which is what stops
        /// "replace this" from being recorded without saying with what. </summary>
        public bool IsWellFormed()
        {
            if (string.IsNullOrEmpty(file) || string.IsNullOrEmpty(member) || string.IsNullOrEmpty(produces))
            {
                return false;
            }
            if (literals == null || literals.Length == 0) { return false; }
            for (int i = 0; i < literals.Length; i++)
            {
                if (string.IsNullOrEmpty(literals[i])) { return false; }
            }
            if (string.IsNullOrEmpty(assumption) || string.IsNullOrEmpty(counterpart)) { return false; }
            if (disposition == MathDisposition.Replace && string.IsNullOrEmpty(corrected)) { return false; }
            if (role == MathRole.Geometry && disposition == MathDisposition.Sourced
                && counterpart.StartsWith("none"))
            {
                // A sourced geometry row whose counterpart is "none" is a contradiction: sourced means a source exists.
                return false;
            }
            return true;
        }
    }


    /// <summary> RM-94's legacy geometry census: one row per geometric or physical computation in the legacy road code,
    /// with the literals it rests on and a disposition. It is CODE rather than a Markdown table for the reason the
    /// whole Docs/ folder is scaffolding -- a table nobody can run drifts from the source the first time anyone edits a
    /// formula, and the drift is invisible. As code, <see cref="LegacyMathGate"/> can sweep the scoped files and fail
    /// when a literal appears that no row names, which is the difference between an inventory and a claim.
    ///
    /// Two findings shaped the sweep before a single row was written:
    ///
    ///   * MOST exotic literals in the legacy files are inside commented-out debug blocks -- 303.1898f, 0.035f and the
    ///     tHeight vectors in RoadCreationT are all dead. A sweep that does not strip comments reports dead
    ///     scaffolding as live mathematics and buries the real findings under it.
    ///   * The literal population is FOUR populations. Once UV coordinates, floating-point epsilons and dead code are
    ///     separated out, what remains -- the actual road geometry -- is small enough to judge one site at a time.
    ///
    /// The failure this census guards against is not "an authored number exists". It is "nobody knows which numbers are
    /// authored". </summary>
    public static class LegacyMathRegister
    {
        private static List<LegacyMathRow> _rows;

        // Reasons written once so the wording cannot drift between rows that share a judgement.

        private const string WhyEpsilon =
            "none: a floating-point comparison epsilon says how close two computed numbers must be to count as equal. "
            + "It is a property of the arithmetic, not of a road, and no design standard mentions one.";

        private const string WhyUv =
            "none: a texture coordinate. UV space is dimensionless and bounded 0-1 by the sampler; metres never enter "
            + "it, so there is nothing for a geometric standard to govern.";

        private const string WhyHalf =
            "none: a midpoint is one half by definition. 0.5f here is arithmetic, not a chosen value, and replacing it "
            + "with a named constant would hide that.";

        /// <summary> Every censused computation. </summary>
        public static IList<LegacyMathRow> Rows()
        {
            if (_rows != null) { return _rows; }
            List<LegacyMathRow> r = new List<LegacyMathRow>();

            AddRoadCreation(r);
            AddIntersectionObjects(r);
            AddSplinatedMesh(r);
            AddIntersectionGeometry(r);
            AddCoreAndConstruction(r);

            _rows = r;
            return _rows;
        }

        /// <summary> Every row for one file, in registered order. </summary>
        public static IList<LegacyMathRow> ForFile(string file)
        {
            List<LegacyMathRow> outList = new List<LegacyMathRow>();
            foreach (LegacyMathRow row in Rows()) { if (row.file == file) { outList.Add(row); } }
            return outList;
        }

        /// <summary> How many rows carry a given disposition. </summary>
        public static int CountOf(MathDisposition d)
        {
            int n = 0;
            foreach (LegacyMathRow row in Rows()) { if (row.disposition == d) { n++; } }
            return n;
        }

        /// <summary> Every distinct literal the census names, as written in the source. This is what the gate's sweep
        /// is checked against: a literal the sweep finds in a scoped file and this set does not contain is a
        /// computation nobody has looked at. </summary>
        public static HashSet<string> AllLiterals()
        {
            HashSet<string> set = new HashSet<string>();
            foreach (LegacyMathRow row in Rows())
            {
                for (int i = 0; i < row.literals.Length; i++) { set.Add(row.literals[i]); }
            }
            return set;
        }

        /// <summary> The census's headline number: geometry computations whose value is NOT backed by a standard and
        /// is not merely being kept as authored -- that is, the ones RM-95 through RM-97 owe a real figure for. </summary>
        public static int UnsourcedGeometryCount()
        {
            int n = 0;
            foreach (LegacyMathRow row in Rows())
            {
                if (row.role == MathRole.Geometry && row.disposition == MathDisposition.Replace) { n++; }
            }
            return n;
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scripts/RoadCreationT.cs -- the carriageway and intersection mesh builder, and by far the densest file.
        // ---------------------------------------------------------------------------------------------------------

        private static void AddRoadCreation(List<LegacyMathRow> r)
        {
            const string f = "Scripts/RoadCreationT.cs";

            // ---- The two findings that are real bugs, not merely unsourced values. ----

            r.Add(new LegacyMathRow(f, "CreateIntersectionConstruction (turn-lane marking points)",
                "the lateral position of the inner turn-lane marking, as a fraction of the carriageway width",
                new string[] { "0.475f", "0.488f", "0.492f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that a road has exactly 2, 4 or 6 lanes in total. The three fractions are switched on "
                + "LaneTotal() == 2 / 4 / 6 and there is NO else branch: on a 3-, 5-, 7- or 8-lane road no point is "
                + "added to iFLane1R at all, while iFLane1L has already had one added, so the two lists fall out of "
                + "step and the turn-lane geometry is built from mismatched arrays. RM-89 made odd totals authorable "
                + "(2 forward + 1 backward), so this is now reachable from the inspector.",
                "the three fractions are ONE metric constant expressed badly: (0.5 - fraction) x total width is "
                + "~0.16 m in all three cases (0.025 x 2 lanes, 0.012 x 4, 0.008 x 6 all give the same distance), "
                + "which is half of the MUTCD double centre line -- two 100 mm lines with a 100 mm gap, 300 mm "
                + "overall, half-width 150 mm.",
                "FIXED (RM-94): both marking strips now take a width in METRES from PavementMarkings and convert it "
                + "to the fraction the existing interpolation between the two carriageway edges wants, with no branch "
                + "on the lane total anywhere. The inner strip is PavementMarkings.DoubleCentrelineHalfWidthMetres -- "
                + "0.15 m, half of the 300 mm double centre line -- which is the distance all three fractions "
                + "resolved to at the lane width they were fitted at, so a default road does not move while every "
                + "other lane count and lane width now gets the same paint instead of none or the wrong width. Two "
                + "further findings came out of the fix and are recorded here rather than opened as new rows, because "
                + "they are the SAME strip seen from the other side of the node: the backward end (iBLane1R, "
                + "iBLane3L) had the opposite defect, no branch on the lane total at all, so on a 4-lane road its "
                + "edge strip was 0.03 of the carriageway where the forward end used 0.015 -- one strip twice as wide "
                + "behind the node as ahead of it -- and its inner point was a share of a LANE (laneWidth x 0.05) "
                + "rather than of the road, so the two ends agreed at no lane width but 3 m. Both ends now read the "
                + "same metres. The width divided by is measured off the two edge points themselves rather than taken "
                + "from roadWidth, because roadSeperation is reassigned for special end nodes and the edge points are "
                + "what the strip is actually drawn between. The laneWidth x 0.05f this removed was never one of the "
                + "tolerances the 0.05f row below describes; that literal still stands at its own sites and that row "
                + "still owns them."));

            r.Add(new LegacyMathRow(f, "CreateIntersectionConstruction (outer marking points)",
                "the lateral position of the outer turn-lane marking, as a fraction of the carriageway width",
                new string[] { "0.03f", "0.015f", "0.01f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "the same 2/4/6 assumption and the same missing else branch as the inner marking above.",
                "again one constant, not three: 0.03 x 2 lanes, 0.015 x 4 and 0.01 x 6 all put the point ~0.21 m "
                + "from the carriageway edge. The distance is real and measurable in the source; which marking "
                + "standard fixes 0.21 m is NOT established here and is deliberately not guessed.",
                "FIXED (RM-94): the same edit and the same structure -- one offset in metres divided by the width the "
                + "strip is actually drawn across, no branch on the lane total, both ends of the strip agreeing. The "
                + "VALUE is unchanged and still MEASURED, not sourced: PavementMarkings.EdgeStripWidthMetres holds "
                + "the 0.21 m all three fractions resolved to, its own summary says it was measured out of this "
                + "builder and that which standard fixes it is not established, and EdgeStripWidthSource repeats that "
                + "in a string a report can print, so the open question is now written where a reader of the geometry "
                + "will meet it instead of only here. What changed is the structure: the strip no longer widens when "
                + "the author widens a lane, and no lane total is left without paint. The 0.01f in this row's literal "
                + "list is also this file's squared-distance tolerance at about twenty-five other sites, which the "
                + "epsilon row above owns; only the marking use of it is gone."));

            r.Add(new LegacyMathRow(f, "CreateIntersectionConstruction (pre-intersection approach)",
                "how far ahead of an intersection corner construction begins, as a multiple of road width",
                new string[] { "5.5f", "4.5f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that the approach an intersection needs scales with the road's WIDTH. It does not -- it scales with "
                + "speed, because the distance is a taper. 5.5 road widths is 38.5 m on a 7 m road whatever the "
                + "design speed, and the 4.5f branch was unreachable: isOldMethod is assigned false and never written "
                + "again, so it was dead alongside its literal.",
                "the approach length over which an intersection reshapes a carriageway is a taper length, and the "
                + "MUTCD taper relation is already implemented in this tool as LaneModel.TaperLengthMetres (L = WS "
                + "and L = WS-squared/155 below 70 km/h), used by RM-89's lane model and RM-90's approach match.",
                "FIXED (RM-94): RoadCreationT.PreIntersectionStation asks LaneModel.TaperLengthMetres for the lane "
                + "width and design speed of the node's own road, and divides by spline.distance to get the same "
                + "normalised station the old line produced -- so the approach now lengthens with speed instead of "
                + "with width, and a zero-length spline returns zero rather than a division by zero. All three sites "
                + "that recomputed the distance call it. The 4.5f multiplier and the dead 0.1f precision branch are "
                + "gone; the isOldMethod flag itself survives at ONE remaining site, guarding the pre-2018 "
                + "corner-matching branch -- about 225 unreachable lines whose deletion belongs to the dead-code row "
                + "below, not here."));

            r.Add(new LegacyMathRow(f, "ramp vertex heights",
                "how far below the carriageway edge a ramp's outer vertices are dropped",
                new string[] { "0.10f", "0.35f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that a hand-found number is a design value. The line reads "
                + "RampR_R.y -= (_road.desiredRampHeight + 0.10f) and carries the trailing comment "
                + "'normal was 0.35f; Here was 0.45f' -- a record of somebody trying three values until the mesh "
                + "looked right, left in the source as the only documentation of why the fourth one won. Elsewhere "
                + "in the same file a bare 0.35 is subtracted from a world Y for the same purpose.",
                "a ramp shoulder's drop from the carriageway edge is a cross-slope over a width, not a fixed "
                + "vertical figure: AASHTO gives shoulder cross-slopes of roughly 2-6%, so the drop follows from the "
                + "shoulder width the author already sets. desiredRampHeight, which this adds to, is itself an "
                + "author-editable field -- the added constant is what has no owner.",
                "FIXED (RM-94): CarriagewaySection.ShoulderFallMetres(shoulderWidth) supplies the drop, so it is the "
                + "author's own shoulder taken down a shoulder cross-slope -- AASHTO's 2% to 6% band, clamped, with "
                + "4% as the normal bituminous figure -- and the height still comes from SurfaceHeightAtOffsetMetres, "
                + "RM-96's one place a height across the section is computed, so a ramp edge cannot disagree with the "
                + "surface it hangs off. desiredRampHeight is untouched: it is the author's field and the drop is "
                + "added to it exactly as before. At a 2.5 m shoulder and the normal slope the drop is 0.10 m, which "
                + "is what the hand-found constant happened to be, so a default road does not move -- while a 1 m "
                + "shoulder now falls 0.04 m instead of that same 0.10 m, and a road with no shoulder falls nothing "
                + "instead of being dropped below its own surface. The bare 0.35 subtracted from a world Y in "
                + "SetVectorHeight2's zero-protection fallback was the same drop for the same purpose and now reads "
                + "the same function through SplineC.road, guarded for a spline with no road and a road with no "
                + "shoulder. The three-values-tried comment is gone from the source; this row is its record. "
                + "EXTENDED (RM-110, B-9): the slope is now the author's own -- Road.shoulderCrossSlope, drawn in the "
                + "inspector beside the width it applies across -- rather than the band's normal figure supplied as a "
                + "default nobody could reach. Nothing about the derivation changed: the same function clamps the "
                + "authored figure into the same 2% to 6% band and still falls back to the 4% normal for a zero or a "
                + "negative, so a road serialised before the field existed builds the geometry it always built, and "
                + "the inspector's stated range is read from the constants the clamp uses rather than typed beside "
                + "them. All three sites -- both ramp vertices and the zero-protection fallback -- pass the same "
                + "authored slope, because two of them computing one shoulder edge from different slopes would put a "
                + "seam in the mesh wherever the height history happened to be silent."));

            r.Add(new LegacyMathRow(f, "adjacent-road separation thresholds",
                "how close another carriageway must be before the two are treated as one surface",
                new string[] { "1.5f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "that roads within one and a half lane widths of each other should be merged rather than left as two "
                + "overlapping meshes. Appears at five sites as roadSeperation + laneWidth x 1.5.",
                "none: a merge threshold is a modelling decision about when two authored roads become one surface. "
                + "It scales with lane width, which is the sound part -- the distance at which two wide roads read "
                + "as one is larger than for two narrow ones.",
                null));

            // ---- Snap radii: authored, but with a bound that no longer matches the field it clamps. ----
            r.Add(new LegacyMathRow(f, "CreateRoadMesh (merge points to intersection corners)",
                "the radius within which a cross-section point is snapped onto an intersection corner",
                new string[] { "1.75f", "1.25f", "1.35f", "3f", "5f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "a snap radius of 1.25 to 1.75 x Mathf.Clamp(laneWidth, 3f, 5f), and 1.35 x roadDefinition for the "
                + "squared corner test. Worth recording: the clamp floor of 3 m no longer matches the lane width the "
                + "inspector allows -- RM-93 registers laneWidth as 2.5-5.0 m -- so a deliberately narrow 2.5 m lane "
                + "is snapped as though it were 3 m. The effect is a slightly generous snap on the narrowest roads, "
                + "which is the safe direction to be wrong in, so it is recorded rather than changed under RM-94.",
                "none: a snap radius is a mesh-stitching tolerance. It exists because two independently generated "
                + "vertex rings must be welded, which is a modelling problem no road standard addresses. The "
                + "multipliers being scaled by lane width at all is the sound part -- a wider road needs a wider "
                + "weld -- and that much is deliberate.",
                null));

            // ---- Tolerances. One row per ROLE, because a tolerance's value means nothing without its job. ----

            r.Add(new LegacyMathRow(f, "spline and vector comparisons throughout",
                "whether two computed positions or parameters count as the same one",
                new string[] { "0.00001f", "0.0001f", "0.001f", "0.01f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "four epsilons at four scales: 1e-5 and 1e-4 on normalised spline parameters (dimensionless, 0-1), "
                + "1e-3 and 1e-2 on world distances in metres. The scales are consistent with what each compares, "
                + "which is the thing that would be wrong if they were chosen carelessly.",
                WhyEpsilon, null));

            r.Add(new LegacyMathRow(f, "intersection corner construction (nearest-point search)",
                "whether two intersection corner constructions have converged on the same point",
                new string[] { "0.07f", "0.05f", "0.1f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "a 7 cm vertex-coincidence tolerance, used at eight sites in the corner search, plus 5 cm and 10 cm "
                + "variants elsewhere. Recorded as a trap rather than a defect: the flag the 0.07f test sets is "
                + "named isHasBeen1mRRIgnoreMax, so the NAME says one metre while the test says seven centimetres. "
                + "Anyone reading the name and trusting it will mis-predict the branch by a factor of fourteen.",
                "none: a coincidence tolerance, as above. The naming is the finding here, not the number.",
                null));

            r.Add(new LegacyMathRow(f, "midpoint and half-width arithmetic throughout",
                "centre points between two vertices, and half-widths from full widths",
                new string[] { "0.5f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "that a midpoint lies halfway between its endpoints, and that a half-width is half a width. The "
                + "literal appears dozens of times in this file and every occurrence is one of those two.",
                WhyHalf, null));

            // ---- Texture space. Listed so the sweep has an answer for them, not because they are geometry. ----

            r.Add(new LegacyMathRow(f, "UV generation for the carriageway mesh",
                "texture coordinates and the along-road tiling rate of the road surface material",
                new string[] { "0.94f", "1.125f", "0.125f", "0.25f", "0.2f" },
                MathRole.Appearance, MathDisposition.AuthoredKeep,
                "0.94 is a U inset at the first ring (an edge inset avoids sampling across an atlas boundary, where "
                + "bilinear filtering would bleed a neighbouring tile into the road edge); 1.125 (= 9/8) stretches "
                + "the V rate; the rest are tiling fractions.",
                WhyUv, null));

            // ---- Dead code. The sweep must strip comments, or these three drown out everything above. ----

            r.Add(new LegacyMathRow(f, "commented-out debug and height-modification blocks",
                "nothing: every one of these is inside a // comment or an unreachable branch",
                new string[] { "303.1898f", "0.035f", "0.15f", "40f" },
                MathRole.DeadCode, MathDisposition.Delete,
                "303.1898f is a hard-coded world X coordinate in a commented-out block that spawned a marker cube at "
                + "one specific position in one specific scene during a debugging session years ago; 0.035f and "
                + "0.15f are in a commented-out height-modification experiment. None of it compiles into anything.",
                "none: unreachable code has no counterpart. It is recorded because deleting a commented block "
                + "without recording what was in it is how a future reader wonders whether something was lost.",
                null));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scripts/IntersectionObjects.cs -- signals, stop signs and their poles. The one legacy file whose literals
        // are mostly REAL dimensions in metres, which makes its one wrong scaling law easy to miss among them.
        // ---------------------------------------------------------------------------------------------------------

        private static void AddIntersectionObjects(List<LegacyMathRow> r)
        {
            const string f = "Scripts/IntersectionObjects.cs";

            r.Add(new LegacyMathRow(f, "CreateTrafficLight (mast height)",
                "the overall height of a mast-arm traffic signal pole",
                new string[] { "7.6f", "0.7f", "0.3334f", "20f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that a signal pole gets TALLER as the intersection gets wider. It does not. The code computes "
                + "hMod = (crossing distance / 10) x 0.7, clamps it to 1..20, and multiplies a 7.6 m base height by "
                + "it -- so a 100 m crossing raises the signal to 53 m and the clamp ceiling permits 152 m. The "
                + "quantity that genuinely grows with the crossing is the ARM's horizontal reach, which the adjacent "
                + "xMod already scales; height and reach were conflated.",
                "signal head clearance is fixed by the MUTCD, not by the intersection: a signal over a roadway hangs "
                + "roughly 4.6-5.8 m (15-19 ft) above the pavement, and that figure is a vehicle-clearance envelope "
                + "which does not change when the road gets wider. The 7.6 m base is itself a real mast-arm pole "
                + "height (~25 ft) and is the sound part of the expression.",
                "FIXED (RM-94): hMod is gone, and with it the branch that multiplied vertex z and the matching uv.y "
                + "above the mast landmark. The mast is now built at the height it was modelled at, which is inside "
                + "the MUTCD 4.6-7.8 m band for a signal face over a roadway, so there was never headroom to grow "
                + "into. The crossing width reaches only the arm, whose vertices are assigned the crossing distance "
                + "directly -- that assignment was always there and is what should have carried the width all along. "
                + "Two things found while doing it, recorded because they change what the fix could be: xMod's one "
                + "geometric effect was commented out upstream, so it was already inert and could not be made to "
                + "carry the width without reviving dead code; and the luminaire offset MaxHeight - 7.6 m, which the "
                + "stretch used to make nonzero, now evaluates to zero for the shipped mesh, putting the street light "
                + "back exactly where the prefab was authored. tempMaxHeight and xValue went with hMod: xValue's only "
                + "reader was already commented out. The member name in this row said CreateTrafficLightBase, which "
                + "is not a method in the file -- the code is in CreateTrafficLight, called from "
                + "CreateTrafficLightBases."));

            r.Add(new LegacyMathRow(f, "CreateTrafficLightBase (mesh landmark thresholds)",
                "which vertices of the imported signal-pole mesh are stretched when the pole is resized",
                new string[] { "7.5f", "3.5f", "3.8f", "4.8f", "6.2f", "5.808f", "5.8f", "1.6f", "1.8f", "3.2f", "2.5f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "that the signal-pole FBX has one specific shape. Every one of these is a coordinate measured off "
                + "InterTLB1.FBX / InterTLB2.FBX: vertices past z = 7.5 m are the mast head, the band between "
                + "4.8 m and 6.2 m is the arm, 5.808 m is where the collider centres, and the 1.6 / 1.8 / 3.2 / "
                + "2.5 m figures hang the signal heads and the sign below the top. They are real metres, so they "
                + "read as design values, but they describe the ASSET.",
                "none: these are the model's own dimensions, not a standard's. Recorded because that coupling is "
                + "invisible at the call site -- re-exporting the signal pole a few centimetres taller silently "
                + "moves vertices into the wrong band and the pole stretches in the wrong place, with no error. Any "
                + "replacement mesh must keep these landmarks or this method must be re-measured against it.",
                null));

            r.Add(new LegacyMathRow(f, "sign and signal placement from the corner",
                "how far from the intersection corner a sign or signal pole stands",
                new string[] { "0.45f", "0.35f", "0.025f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that a sign belongs at a FRACTION of the shoulder width from the corner (0.45 of it for a signal, "
                + "0.35 for a stop sign), and that its along-road setback is half the corner-to-corner distance plus "
                + "2.5% of the intersection diagonal. Both make the furniture's position depend on the intersection's "
                + "size in a way nothing physical does: on a road with no shoulder at all, 0.45 x 0 puts the pole in "
                + "the traffic lane.",
                "roadside furniture is placed by a lateral OFFSET from the edge of the travelled way and a clear "
                + "zone, both in metres -- the AASHTO Roadside Design Guide figures MedianContent already cites for "
                + "its 6 m planting setback. A fraction of a shoulder is not one of them.",
                "FIXED (RM-94): RoadsideFurniture (City/Runtime/Network/) owns the offset and all three sites read it. "
                + "The governing rule turned out to be MUTCD 2A.19 rather than the Roadside Design Guide clear zone "
                + "this row first pointed at, and the distinction matters: a clear zone answers how far a vehicle "
                + "leaving the road at speed will travel, which is a much larger figure, while an intersection is a "
                + "low-speed conflict area where the sign also has to be close enough to read. 2A.19 gives 0.6 m "
                + "beyond the shoulder edge, or 1.8 m from the edge of the travelled way where there is no shoulder, "
                + "and taking whichever is larger satisfies both clauses and supplies exactly the clamp this row "
                + "asked for -- a shoulderless road now gets 1.8 m instead of nothing. Checked rather than assumed "
                + "that the corner vectors are at the travelled-way edge: they are built from RoadWidth()/2, and "
                + "RoadWidth() is lanes only, so the offset is measured from the edge the MUTCD figure is measured "
                + "from. The along-road term keeps its half-width, which really is the distance from the node to the "
                + "intersection edge, and the 2.5%-of-diagonal setback becomes the MUTCD 3B.16 stop-line setback of "
                + "1.2 m -- it decides the station whose tangent the sign is turned to face, so it should be the "
                + "station a stopped driver is at. Two disagreements surfaced while doing it: the pole was placed "
                + "with 0.45 but the signal heads were hung along the arm using 0.35, so the heads were positioned "
                + "for a pole standing where no pole stood; and the kerb-face clause of 2A.19, which permits 0.3 m "
                + "where the right of way is restricted, is held as a documented exception and NOT applied "
                + "automatically, because that is an author's decision about a constrained site."));

            r.Add(new LegacyMathRow(f, "pole and arm colliders",
                "the cross-section of the box colliders standing in for the pole shaft and mast arm",
                new string[] { "0.35f", "0.175f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "a 0.35 m square shaft collider and a 0.175 m square arm collider.",
                "both are close to real steel signal hardware: mast-arm shafts run about 0.3 m across at the base "
                + "and arms taper from roughly 0.2 m down to 0.1 m, so a 0.175 m arm box is a fair average. These "
                + "are the two literals in this file that need no change at all.",
                null));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scripts/SplinatedMeshMaker.cs -- extrudes an authored cross-section along the spline (barriers, kerbs,
        // railings). Its literals are almost entirely tolerances and bounds margins; no road standard is involved.
        // ---------------------------------------------------------------------------------------------------------

        private static void AddSplinatedMesh(List<LegacyMathRow> r)
        {
            const string f = "Scripts/SplinatedMeshMaker.cs";

            r.Add(new LegacyMathRow(f, "extrusion vertex welding and spline sampling",
                "whether two extruded ring vertices are the same vertex, and where sampling stops",
                new string[] { "0.02f", "0.01f", "0.005f", "0.002f", "0.001f", "0.0001f", "0.99995f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "a 2 cm weld tolerance on extruded rings with finer variants for parameter comparisons, and an end "
                + "clamp of 0.99995 so the extrusion never samples the spline at t = 1 exactly -- where the forward "
                + "tangent is one-sided and the extrusion frame would be undefined. The end clamp is the one worth "
                + "understanding: it is not a fudge, it is avoiding a genuine singularity.",
                WhyEpsilon, null));

            r.Add(new LegacyMathRow(f, "bounds inflation before the cutoff and containment tests",
                "the search volume the extruded mesh is tested against",
                new string[] { "1.5f", "1.05f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "the mesh's own bounding box inflated 50% on all six sides for the containment search, and its max "
                + "height inflated 5% before the vertical cutoff test. Both are margins on a test volume: too small "
                + "and a vertex just outside the box is missed, too large and the search does more work. Neither "
                + "changes the geometry that ships.",
                "none: the size of a search volume is a performance-versus-safety choice in the algorithm, and the "
                + "safe direction is the generous one, which is the direction both of these err in.",
                null));

            r.Add(new LegacyMathRow(f, "serialised extrusion defaults (minMaxMod, VerticalMeshCutoffOffset)",
                "the default weld modifier and vertical cutoff offset a new extruded object starts with",
                new string[] { "0.002f", "0.04f", "0.05f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "these are not hard-coded mathematics at all -- they are serialised field initialisers an author can "
                + "see and change. Recorded so the sweep has an answer for them and so nobody later mistakes an "
                + "editable default for a buried constant. Their home is the field census, not this one.",
                "none: default values for author-editable tolerances. What they need is a unit label and a range in "
                + "the field registry, which is RM-51's job, not a design source.",
                null));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scripts/Intersections.cs and Scripts/RoadIntersection.cs -- where two roads meet.
        // ---------------------------------------------------------------------------------------------------------

        private static void AddIntersectionGeometry(List<LegacyMathRow> r)
        {
            const string f = "Scripts/Intersections.cs";

            r.Add(new LegacyMathRow(f, "corner and cut-position walks",
                "the step size used to walk along a spline looking for the corner cut position",
                new string[] { "0.25f", "1.25f", "0.05f", "0.001f", "1.2f", "0.999f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "the walk advances 0.25 m or 1.25 m of arc length at a time (divided by spline.distance to become a "
                + "parameter step), starts 0.05 along, converges when within 1.2 x an offset, and nudges by 0.001 to "
                + "escape a stall. 0.999 samples just short of the spline end for the same tangent reason as "
                + "SplinatedMeshMaker's 0.99995. These are search parameters: a smaller step is more accurate and "
                + "slower, and the answer the walk converges ON is geometry, but the stride is not.",
                "none: an iterative search's step size. What matters is that the walk terminates and that its result "
                + "is within tolerance of the true crossing, both of which are properties of the algorithm.",
                null));

            r.Add(new LegacyMathRow(f, "intersection extent from the authored width",
                "the working extent an intersection reserves, from its authored intersectionWidth",
                new string[] { "1.25f" },
                MathRole.Geometry, MathDisposition.AuthoredKeep,
                "a 25% margin on the authored intersection width, so corner construction has room outside the "
                + "nominal box. RoadIntersection's own field documentation states the relation as 10 x 1.25 and adds "
                + "'Never written, only read', which is worth having recorded: the margin is applied at the point of "
                + "use, never stored, so an author reading intersectionWidth in the inspector sees the nominal figure "
                + "and not the one the geometry actually uses.",
                "none: a working margin around a construction volume. The authored width it multiplies is the "
                + "design quantity; this is headroom for the algorithm.",
                null));

            r.Add(new LegacyMathRow(f, "mesh allocation and editor gizmos",
                "triangle-buffer sizes, and the debug cube drawn at an intersection in the scene view",
                new string[] { "1.5f", "20f", "12f", "2.5f", "0.7f" },
                MathRole.Appearance, MathDisposition.AuthoredKeep,
                "(int)(4f * 1.5f) and (int)(16f * 1.5f) are the triangle counts for a quad strip -- six indices per "
                + "four vertices -- written as a constant-folded product instead of as a count, which reads like a "
                + "magic multiplier and is not one. The 20 m tall, 2.5 m square blue cube is an editor gizmo marking "
                + "an intersection in the scene view.",
                "none: a triangle index count is topology, and a gizmo is drawn for a human, not built into the "
                + "world. Classified here as non-geometry so the sweep stops asking for a source. The 1.5 here is "
                + "unrelated to the 1.5 bounds inflation elsewhere despite sharing a value, which is the sort of "
                + "coincidence a census exists to separate.",
                null));

            r.Add(new LegacyMathRow("Scripts/RoadIntersection.cs", "streetlight colour and grade blending",
                "the colour a streetlight casts, and how far terrain is pulled down on a falling approach",
                new string[] { "0.7451f", "0.27451f", "0.75f" },
                MathRole.Appearance, MathDisposition.Sourced,
                "an RGB of (1, 0.7451, 0.27451) for the streetlight, and gradeModNegative 0.75 as the companion to "
                + "the 0.375 gradeMod for approaches that fall towards the intersection rather than rise.",
                "the colour is not arbitrary: 1 / 0.7451 / 0.27451 is exactly (255, 190, 70) in 8-bit, the warm "
                + "amber of a high-pressure sodium lamp, which is what the tool's own SodiumFlare and its bulb "
                + "asset represent. Recorded as sourced BECAUSE it looks like three random decimals and is not one "
                + "-- it is a byte triple divided by 255, and rounding it would visibly change the light. "
                + "gradeModNegative is an author-editable companion field, like gradeMod.",
                null));

            r.Add(new LegacyMathRow("Scripts/Road.cs", "editor timing and gizmo colours",
                "how often the scene-view camera check runs, and the transparency of node gizmos",
                new string[] { "0.015f", "0.75f" },
                MathRole.Appearance, MathDisposition.AuthoredKeep,
                "a 15 ms interval between editor camera-position checks (about one per frame at 60 fps, which is why "
                + "that figure and not a rounder one), and 0.75 alpha on the node and connection gizmo colours.",
                "none: a repaint interval is measured in frames and an alpha in a gizmo is a legibility choice. "
                + "Neither is a road dimension, and the sweep needs an answer for both.",
                null));
        }

        // ---------------------------------------------------------------------------------------------------------
        // Scripts/Road.cs, Scripts/Construction*.cs, Scripts/RoadAutomation.cs -- the core model and the geometry
        // primitives. Two of the strongest findings in the census are here, both of them contradictions rather than
        // merely unsourced values.
        // ---------------------------------------------------------------------------------------------------------

        private static void AddCoreAndConstruction(List<LegacyMathRow> r)
        {
            r.Add(new LegacyMathRow("Scripts/Road.cs", "UpdateRoad (lane width and segment resolution clamps)",
                "the lane width and the segment length the road model will accept at run time",
                new string[] { "0.2f", "500f", "1f", "50f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that a lane may be 20 cm wide or half a kilometre wide. This clamp CONTRADICTED the range the "
                + "inspector declares and enforces: RM-93 registers laneWidth as 2.5-5.0 m and cites AASHTO for it, "
                + "then the runtime silently accepted anything from 0.2 m to 500 m. Two limits disagreeing is worse "
                + "than either being wrong on its own -- the inspector warned about a 6 m lane it then built anyway, "
                + "and a value set from script bypassed the warning entirely. roadDefinition had the same split: 1-20 "
                + "m declared, 1-50 m clamped.",
                "AASHTO gives 2.7-3.75 m for a highway lane; the registry's 2.5-5.0 m band already widens that for "
                + "shared and rural lanes and names its source. There is no standard under which 500 m is a lane.",
                "FIXED (RM-94): RoadFieldLimits (City/Runtime/Network/) now owns both bands and the citation, and "
                + "both the clamp here and the inspector's declared range read from it, so one number governs both "
                + "paths instead of being typed twice. The owner is a RUNTIME type because the clamp runs in the "
                + "runtime assembly and cannot see the editor-only registry -- the inverse of what this row first "
                + "proposed. NaN clamps to the minimum rather than passing through, since every comparison against "
                + "NaN is false and one bad assignment would otherwise spread NaN across the cross-section."));

            r.Add(new LegacyMathRow("Scripts/RoadAutomation.cs and Scripts/Construction.cs",
                "node placement (minimum height)",
                "the lowest world Y a road node is allowed to sit at",
                new string[] { "0.03f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "that world Y = 0 is sea level and that no road is ever below it. The guard read "
                + "if (nodePosition.y < 0.03f) nodePosition.y = 0.03f, applied to an ABSOLUTE world coordinate at "
                + "four sites. Its intent was sound -- keep a node off exactly zero, where a terrain sample and a "
                + "road surface coincide and z-fight -- but as written it teleported every node in a valley, a "
                + "cutting, a tunnel approach or any scene whose terrain sits below the origin up to 3 cm above the "
                + "origin, which is nowhere near the ground there.",
                "the 3 cm itself is a real and appropriate render bias: Road.cs uses 0.01-0.03 m lifts for exactly "
                + "this purpose on markings and overlays, and a centimetre-scale offset is what a depth-bias needs. "
                + "The error is the frame of reference, not the magnitude.",
                "FIXED (RM-94): the same 3 cm, now measured from the terrain surface under the node instead of from "
                + "world zero, so the bias does its job at any elevation and a road can be built below sea level. "
                + "RoadNodeGround (City/Runtime/Network/) owns the sampling and all four sites call RoadNodeGround."
                + "Lift. Where no terrain covers the node there is nothing to z-fight with, so nothing is moved and "
                + "no floor is invented from the origin."));

            r.Add(new LegacyMathRow("Scripts/Road.cs", "cross-section and overlay offsets",
                "the vertical lift given to markings and overlays so they do not z-fight the carriageway",
                new string[] { "0.01f", "0.02f", "0.03f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "a 1 to 3 cm depth bias, stepped so overlays that stack (marking over surface over shoulder) each "
                + "get their own layer and resolve in a fixed order rather than by luck.",
                "none: a depth bias exists because a depth buffer has finite precision. Real paint is about 2 mm "
                + "thick and no road standard specifies a rendering offset.",
                null));

            r.Add(new LegacyMathRow("Scripts/Road.cs", "serialised design defaults",
                "the values a newly created road starts with",
                new string[] { "3.25f", "1.2f", "0.08f", "0.61f", "0.35f", "0.1f", "0.375f", "0.04f" },
                MathRole.Geometry, MathDisposition.Sourced,
                "laneWidth 3.25 m, shoulderWidth 1.2 m, maxGrade 0.08, the K-rail base 0.61 m, desiredRampHeight "
                + "0.35 m, matchTerrainSubtraction 0.1 m, RoadIntersection's gradeMod 0.375, and shoulderCrossSlope "
                + "0.04. The first four each cite a figure from outside the tool; the next three are the author's to "
                + "set; the last cites one too.",
                "3.25 m is the UrbanTwoLane row of City.RoadClassDefaults, cited there, and UrbanTwoLane is the class "
                + "every road is born declaring itself to be. RM-109 put it here in place of a 5 m initialiser: no "
                + "travel lane is 5 m wide, and a road that disagreed with its own class had the disagreement read as "
                + "an authored override by the preserving merge, so the 5 m survived every later class change and the "
                + "road could not be talked out of it. 1.2 m is the shoulder from that same UrbanTwoLane row, and it is "
                + "here for exactly the same reason RM-110 (B-10) put it there: AASHTO's rural collector and local "
                + "shoulders run 0.6-2.4 m as an edge strip, while a REFUGE is 3.0 m -- the 2.44 m design vehicle plus "
                + "a margin at each edge -- so an urban strip belongs at the bottom of the strip band and nowhere near "
                + "the lane width, because a shoulder near lane width is driven as a lane. The 1 m it replaced was not "
                + "a cited figure and left a brand-new road disagreeing with its own class in the same way the 5 m lane "
                + "did. 8% is inside AASHTO's 6-12% grade band for the terrain and "
                + "design speeds this tool targets, and 0.61 m is the AASHTO Roadside Design Guide F-shape concrete "
                + "barrier base width, already cited at its site in Road.cs. The remaining three are author-editable "
                + "defaults whose homes are the field census and the inspector registry, not a design standard -- "
                + "gradeMod is a slider the intersection editor exposes over 0.01-2. RM-110 (B-9) added "
                + "shoulderCrossSlope 0.04, which is CarriagewaySection.ShoulderCrossSlopeNormal -- AASHTO's normal "
                + "cross-slope for a paved bituminous shoulder, inside the same 2%-6% band the field is clamped into. "
                + "It is written as a literal here and not as a reference to that constant for one reason: a field "
                + "initialiser that reads a const is still a copy, and a road serialised before this field existed "
                + "arrives with 0 rather than with any initialiser at all -- so the value that actually protects an old "
                + "road is the clamp's fallback, which IS the constant, and this initialiser only decides what a "
                + "brand-new road starts at.",
                null));

            r.Add(new LegacyMathRow("Scripts/Construction2DRect.cs and Scripts/Construction3DTri.cs",
                "NearDist / NearDistSQ and the degenerate-quad nudge",
                "the proximity threshold for the point-in-shape tests, and the escape from a degenerate rectangle",
                new string[] { "0.15f", "0.0225f", "0.0002f", "0.0001f", "1.5f", "1.0000001f", "1.000001f" },
                MathRole.NumericalTolerance, MathDisposition.AuthoredKeep,
                "a 15 cm proximity threshold, stored TWICE -- once as 0.15 and once pre-squared as 0.0225 -- so a "
                + "distance test can skip the square root. That pairing is the finding: the two constants are "
                + "independent literals with no arithmetic linking them, so editing 0.15 without editing 0.0225 "
                + "leaves the squared and unsquared tests disagreeing, and every symptom of that appears in the "
                + "mesh rather than as an error. The 0.0002 nudge separates two points that share a coordinate, "
                + "which would otherwise give a rectangle zero area, and the 1.0000001 loop bounds make a float "
                + "for-loop actually reach 1.",
                WhyEpsilon, null));
        }
    }
}
#endif
