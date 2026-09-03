#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which tower prefab a node gets. A real transmission line uses a different structure where the
    /// line changes direction (an ANGLE tower, braced to take the sideways pull of the turn) from where it runs
    /// straight (a SUSPENSION tower, which only carries the vertical load). The placer picks by the spline's
    /// turn angle at the node against an AUTHORED threshold -- guessing the threshold would put a light
    /// suspension tower on a sharp corner it cannot physically hold. </summary>
    public enum TowerType
    {
        Suspension = 0,
        Angle = 1
    }


    /// <summary> RM-23's authored parameter block for one class of overhead line. Every number here is AUTHORED
    /// against a real-world range rather than guessed (C-15), and the ranges are cited so a reviewer can check
    /// them:
    ///   * <see cref="spanLengthMetres"/> -- transmission spans are hundreds of metres (typ. 200-400 m between
    ///     lattice towers), an order of magnitude longer than a streetlight's tens of metres; a valley crossing
    ///     wants a longer span and a hand-placed tower, which is why this is a per-class value, not a constant.
    ///   * <see cref="sagRatio"/> -- the ONLY honest authored handle on the cable shape. The catenary constant
    ///     a = H/w (tension over weight) is something no author knows; sag as a fraction of span is a published,
    ///     tabulated quantity per conductor, ~2-5 % at everyday temperature. Default 0.03 (3 %).
    ///   * <see cref="minGroundClearanceMetres"/> -- statutory HV clearance to ground is real and jurisdictional
    ///     (roughly 6-10 m for sub-transmission over open ground); the default is AUTHORED and per-class.
    ///   * <see cref="angleTowerThresholdDeg"/> -- turn angle at a node beyond which the node needs an angle
    ///     tower rather than a suspension tower. AUTHORED. </summary>
    [Serializable]
    public sealed class TransmissionLineClass
    {
        public string conductorName = "AUTHORED-ACSR-generic";
        public float spanLengthMetres = 300f;
        public float sagRatio = 0.03f;
        public float minGroundClearanceMetres = 7.5f;
        public float angleTowerThresholdDeg = 5f;

        /// <summary> A defaulted instance carrying the AUTHORED real-world-range values above. </summary>
        public static TransmissionLineClass Default()
        {
            return new TransmissionLineClass();
        }
    }
}
