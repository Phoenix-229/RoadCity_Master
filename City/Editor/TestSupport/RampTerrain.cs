#if UNITY_EDITOR
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
using UniverseCreator.Integration;
#endregion


namespace RoadCityMaster.City.Editor
{
    /// <summary> A deterministic analytic ramp for the buildable-land gate (RM-11): a surface that
    /// tilts along +X at one grade up to <see cref="_kinkX"/> and a second grade beyond it, and is flat
    /// in Z. It exists BESIDE the gentle shared <see cref="FakePcgTerrain"/>, not instead of it -- the
    /// gentle surface has no cliff, no waterline and little relief, so it cannot exercise the steep,
    /// reshaped, high-relief or waterside verdicts. This one is built to trigger exactly those, one
    /// reason at a time, by choosing the grades and the kink:
    ///
    ///   (0, 0, 0, 0)     flat at Y=0            -- the waterside case (ground sits in the freeboard)
    ///   (0, g, g, 0)     one uniform grade g%   -- uniformly too steep, or gentle-but-deep for relief
    ///   (0, gL, gR, k)   flat-ish then steep    -- the reshape case (a buildable half survives a cut)
    ///
    /// Every method is a pure function of its arguments -- the same analytic height and a finite-
    /// difference normal exactly consistent with it -- so a gate that samples it is deterministic
    /// (C-06). The grade is read as rise/run percent, the same units RM-11's criteria and reasons use,
    /// so a <paramref name="gradeRightPercent"/> of 50 really does read back as a 50 % max slope. </summary>
    public sealed class RampTerrain : IPCGDataProvider
    {
        private readonly float _baseHeight;
        private readonly float _slopeLeft;    // rise per unit run, left of the kink  (percent / 100)
        private readonly float _slopeRight;   // rise per unit run, right of the kink (percent / 100)
        private readonly float _kinkX;

        /// <summary> Builds the ramp. <paramref name="gradeLeftPercent"/> applies to X below
        /// <paramref name="kinkX"/>, <paramref name="gradeRightPercent"/> to X above it; a kink of 0
        /// makes the right grade the only one a lot at X&gt;=0 ever sees, i.e. one uniform grade. </summary>
        public RampTerrain(float baseHeight, float gradeLeftPercent, float gradeRightPercent, float kinkX)
        {
            _baseHeight = baseHeight;
            _slopeLeft = gradeLeftPercent * 0.01f;
            _slopeRight = gradeRightPercent * 0.01f;
            _kinkX = kinkX;
        }

        /// <summary> The ramp height: the left grade carries X up to the kink, the right grade carries
        /// it the rest of the way, so the two segments meet continuously at the kink. Z is level. </summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            return _baseHeight
                 + _slopeLeft * Mathf.Min(worldX, _kinkX)
                 + _slopeRight * Mathf.Max(0f, worldX - _kinkX);
        }

        /// <summary> The surface normal from finite differences of <see cref="SampleHeight"/>, so it is
        /// exactly consistent with the height field this same provider reports. </summary>
        public Vector3 SampleNormal(float worldX, float worldZ)
        {
            float dx = SampleHeight(worldX + 1f, worldZ) - SampleHeight(worldX - 1f, worldZ);
            float dz = SampleHeight(worldX, worldZ + 1f) - SampleHeight(worldX, worldZ - 1f);
            return new Vector3(-dx * 0.5f, 1f, -dz * 0.5f).normalized;
        }

        /// <summary> One generous chunk covering the region the gate builds lots in. </summary>
        public List<Bounds> GetActiveChunkBounds()
        {
            return new List<Bounds> { new Bounds(Vector3.zero, new Vector3(3200f, 1000f, 3200f)) };
        }

        /// <summary> The write-side linker calls are inert here: the gate never cuts terrain. </summary>
        public void NotifyCutRegion(Bounds worldBounds) { }
        public int RequestTunnelOpening(Bounds worldBounds) { return 0; }
        public void ReleaseTunnelOpening(int handle) { }
    }
}
#endif
