#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> D-14. The SHAPE of a lattice transmission tower, separately from the electrical figures on
    /// <see cref="TransmissionLineClass"/>. Two classes for the same reason the distribution pole has two: the span,
    /// sag and clearance are conductor and NESC/statutory tables, edited by whoever engineers the line; these are the
    /// steelwork catalogue, edited by whoever buys the tower. A tower strung to the wrong sag is an engineering error;
    /// a tower 28 m tall instead of 32 m is a structural-standard decision.
    ///
    /// The configuration modelled is a single-circuit HORIZONTAL tower: the three phases hang from one wide cross-arm
    /// at a common height, and one earth (shield) wire runs along a peak ABOVE them. It is a real and common
    /// arrangement for sub-transmission (about 66-150 kV), and it is the one that lets the phases reuse a span's single
    /// solved catenary offset sideways -- exactly as the distribution pole line does -- rather than inventing a second
    /// curve per phase. Vertical and double-circuit towers exist; they are a different shape class, not a special case
    /// bolted onto this one.
    ///
    /// Every default is a real product/standard dimension for a ~132 kV single-circuit tower, not a chosen one, and
    /// each is cited so a reviewer can check it:
    ///
    ///   * <see cref="baseWidthMetres"/> -- the square spread of the four legs at ground. A lattice tower's base is
    ///     roughly a sixth to an eighth of its height; 6.5 m under a ~28 m tower sits in that band. This is the
    ///     dimension the FOUR-FOOT seating reads: the four legs stand at the corners of this square.
    ///   * <see cref="conductorCrossArmHeightMetres"/> -- the cross-arm centre above the tower base. The lowest
    ///     conductor then hangs an insulator-string below it, and the statutory ground clearance
    ///     (<see cref="TransmissionLineClass.minGroundClearanceMetres"/>) is measured from there down through the sag.
    ///   * <see cref="crossArmHalfLengthMetres"/> -- half the cross-arm, so three phases at
    ///     <see cref="phaseSpacingMetres"/> fit across it with an end margin. 4.6 m holds phases at -4, 0, +4 m.
    ///   * <see cref="phaseSpacingMetres"/> -- horizontal phase-to-phase spacing, ~3.5-4.5 m at 132 kV to hold the
    ///     air gap under swing. Default 4.0.
    ///   * <see cref="shieldWirePeakAboveCrossArmMetres"/> -- how far the earth-wire peak stands above the phases. The
    ///     shield wire has to sit high enough that the ~30 degree line down to the outer phase keeps the phases inside
    ///     its protective angle; 3.6 m over a 9.2 m arm does that.
    ///   * <see cref="suspensionInsulatorLengthMetres"/> -- an I-string of ~9-10 cap-and-pin discs at 146 mm plus end
    ///     hardware is ~1.5-1.7 m. On a SUSPENSION tower it hangs the phase straight DOWN from the arm.
    ///   * <see cref="strainInsulatorLengthMetres"/> -- a tension (strain) string is longer, but it lies along the
    ///     conductor pull, roughly HORIZONTAL, so on an ANGLE or terminal tower it does not drop the phase; the phase
    ///     attaches near the arm. Held as its own figure so the mesh draws the right string, not to move the
    ///     attachment height. </summary>
    [Serializable]
    public sealed class TransmissionTowerClass
    {
        public float baseWidthMetres = 6.5f;
        public float conductorCrossArmHeightMetres = 22.0f;
        public float crossArmHalfLengthMetres = 4.6f;
        public float phaseSpacingMetres = 4.0f;
        public float shieldWirePeakAboveCrossArmMetres = 3.6f;

        public float suspensionInsulatorLengthMetres = 1.6f;
        public float strainInsulatorLengthMetres = 2.3f;

        /// <summary> The steel members' face widths, for the mesh only. The leg is a heavier angle than the bracing;
        /// both are drawn as square members of these faces. They change the tower's look, not any attachment height,
        /// but they are in <see cref="ShapeHash"/> so an edit invalidates a cached mesh. </summary>
        public float legFaceMetres = 0.16f;
        public float braceFaceMetres = 0.09f;

        /// <summary> Conductor and earth-wire diameters, for the wire meshes. A 132 kV line commonly runs a medium
        /// ACSR (e.g. "Lynx", ~19.5 mm); the earth wire is a 7-strand steel ~9.8 mm. </summary>
        public float conductorDiameterMetres = 0.0195f;
        public float shieldWireDiameterMetres = 0.0098f;
        public int wireSides = 4;

        /// <summary> How many earth (shield) wires ride the peak. One is the ordinary single-circuit case; two are
        /// used where a wider protective angle or an OPGW-plus-earth pair is wanted. </summary>
        public int shieldWireCount = 1;

        /// <summary> A defaulted instance carrying the AUTHORED real-world-range values above. </summary>
        public static TransmissionTowerClass Default()
        {
            return new TransmissionTowerClass();
        }

        /// <summary> The foundation depth this tower is keyed in by, from its height: a lattice tower's footings go
        /// deeper as the tower grows, and a real figure is roughly a tenth of the tower height, floored so a short
        /// tower is still keyed in rather than set on the surface. This is what the run buries below the LOWEST of the
        /// four feet, the same way the pole line buries its ANSI setting depth. </summary>
        public double SettingDepthMetres()
        {
            double depth = TowerHeightMetres() * 0.1;
            return depth < 1.5 ? 1.5 : depth;
        }

        /// <summary> The tower's full height, base to earth-wire peak. Derived so the peak cannot drift away from the
        /// cross-arm it is measured from. </summary>
        public double TowerHeightMetres()
        {
            return conductorCrossArmHeightMetres + shieldWirePeakAboveCrossArmMetres;
        }

        /// <summary> The four legs' footing positions in the tower's own XZ, one at each corner of the base square, so
        /// the run can sample the ground under each foot and seat the tower to the lowest. Order is fixed for C-06:
        /// (-,-), (+,-), (+,+), (-,+), which is counter-clockwise, the winding the footprint scanner expects. </summary>
        public List<Vector2> FootOffsets()
        {
            float h = baseWidthMetres * 0.5f;
            return new List<Vector2>
            {
                new Vector2(-h, -h),
                new Vector2( h, -h),
                new Vector2( h,  h),
                new Vector2(-h,  h),
            };
        }

        /// <summary> A hash of every dimension that changes the mesh, so the editor-side tower-mesh factory's cache
        /// can tell one shape from another. Deterministic (C-06): a fixed field order and integer arithmetic only, no
        /// floating-point hash and no reflection, so the same figures give the same number on every machine and every
        /// run. Quantised to a tenth of a millimetre, finer than any tower is fabricated to and coarse enough that a
        /// float's last bit cannot invent a second cache entry for one tower. </summary>
        public int ShapeHash()
        {
            int h = 17;
            h = Mix(h, baseWidthMetres);
            h = Mix(h, conductorCrossArmHeightMetres);
            h = Mix(h, crossArmHalfLengthMetres);
            h = Mix(h, phaseSpacingMetres);
            h = Mix(h, shieldWirePeakAboveCrossArmMetres);
            h = Mix(h, suspensionInsulatorLengthMetres);
            h = Mix(h, strainInsulatorLengthMetres);
            h = Mix(h, legFaceMetres);
            h = Mix(h, braceFaceMetres);
            h = Mix(h, conductorDiameterMetres);
            h = Mix(h, shieldWireDiameterMetres);
            h = h * 31 + wireSides;
            h = h * 31 + shieldWireCount;
            return h;
        }

        private static int Mix(int h, float value)
        {
            return h * 31 + (int)Mathf.Round(value * 10000f);
        }
    }


    /// <summary> D-14. Where the wires attach on a tower of a given shape, in metres above the tower BASE, and the
    /// lateral offsets of the phases across the cross-arm.
    ///
    /// This is the tower's ARITHMETIC, and it lives here rather than with the mesh because two callers need it and
    /// only one of them draws. The run that strings the conductors needs the same numbers the steel was drawn with:
    /// an attachment height computed apart from the arm it hangs on is how a wire ends up floating above its own
    /// insulator string. The mesh builder (in the editor assembly, because a baked run carries no mesh builder into a
    /// player) asks this same struct and puts a string at every level it reports.
    ///
    /// The one thing that is NOT pure geometry is the phase drop, which depends on the tower TYPE: a
    /// <see cref="TowerType.Suspension"/> hangs its phases on a vertical I-string, so the conductor sits an
    /// insulator-length BELOW the arm; a <see cref="TowerType.Angle"/> (and a terminal) takes the pull on a strain
    /// string that lies along the conductor, roughly horizontal, so the phase attaches at the arm itself. That is why
    /// the type is a parameter here and not something the mesh decides on its own.
    ///
    /// Everything is a pure function of the shape, the phase count and the type -- no scene, no mesh, no clock, no
    /// random (C-06) -- which is what lets D-16 assert a tower's attachment heights and phase spacing with none of
    /// those present. </summary>
    public struct TransmissionTowerAttachments
    {
        /// <summary> The cross-arm centre line above the base. </summary>
        public float crossArmY;

        /// <summary> The height the phases actually attach at: the arm on an angle/terminal tower, an insulator-length
        /// below it on a suspension tower. </summary>
        public float conductorAttachY;

        /// <summary> The earth-wire peak above the base -- above the phases, always, which is the whole point of a
        /// shield wire. </summary>
        public float shieldWireY;

        /// <summary> The insulator string's length for this tower's type: the vertical drop on a suspension tower, the
        /// (roughly horizontal) strain string on an angle or terminal tower. Reported so the mesh draws the string the
        /// type calls for. </summary>
        public float insulatorLengthMetres;

        /// <summary> Lateral offsets of the phase attachments from the tower centre, along the arm, one per phase in
        /// draw order. </summary>
        public float[] phaseOffsets;

        /// <summary> The attachment levels for a shape, phase count and tower type, from the arithmetic the mesh is
        /// assembled with. <paramref name="isTerminal"/> takes a strain string like an angle tower even when the line
        /// runs straight through, because a dead-end carries the full one-sided pull however straight it is. </summary>
        public static TransmissionTowerAttachments For(TransmissionTowerClass shape, int phaseCount,
            TowerType type, bool isTerminal)
        {
            TransmissionTowerClass s = shape != null ? shape : TransmissionTowerClass.Default();
            TransmissionTowerAttachments a = new TransmissionTowerAttachments();
            a.crossArmY = s.conductorCrossArmHeightMetres;
            a.shieldWireY = s.conductorCrossArmHeightMetres + s.shieldWirePeakAboveCrossArmMetres;

            bool strain = isTerminal || type == TowerType.Angle;
            a.insulatorLengthMetres = strain ? s.strainInsulatorLengthMetres : s.suspensionInsulatorLengthMetres;
            // A strain string lies along the conductor pull (roughly horizontal), so it does not lower the phase; a
            // suspension I-string hangs the phase straight down by its length.
            a.conductorAttachY = strain ? a.crossArmY : a.crossArmY - s.suspensionInsulatorLengthMetres;

            a.phaseOffsets = PhaseOffsets(phaseCount, s.phaseSpacingMetres, s.crossArmHalfLengthMetres);
            return a;
        }

        /// <summary> Where the phases sit across the arm: centred on the tower at the class spacing, so three phases at
        /// 4 m land at -4, 0, +4 and an arm reaching 4.6 m each side holds them with an end margin. An even count
        /// straddles the tower centre rather than putting a phase through the steel. An offset that would run past the
        /// arm is pulled in to its end -- a visible crowding rather than a phase hanging off the tip -- and the run's
        /// own inspector reports it, because an arm too short for its phases is an authoring mistake, not something to
        /// hide. </summary>
        private static float[] PhaseOffsets(int phaseCount, float spacingMetres, float halfLengthMetres)
        {
            int n = phaseCount < 0 ? 0 : phaseCount;
            if (n == 0) { return new float[0]; }
            float[] offsets = new float[n];
            float spacing = spacingMetres > 0f ? spacingMetres : 4.0f;
            float limit = halfLengthMetres - spacing * 0.05f;
            if (limit < 0f) { limit = 0f; }
            float first = -spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float d = first + spacing * i;
                if (d < -limit) { d = -limit; }
                if (d > limit) { d = limit; }
                offsets[i] = d;
            }
            return offsets;
        }
    }
}
