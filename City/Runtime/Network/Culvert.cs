#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-30's authored culvert sizing table. A culvert is NOT sized from vehicle clearance (that is what
    /// a road tunnel needs and is the wrong quantity entirely); it is sized from the stream's DESIGN FLOW. A full
    /// HDS-5 inlet/outlet-control solution is out of proportion here, so -- with the source named -- the barrel is
    /// picked from a small authored table of flow classes to circular-barrel diameters. Flow comes from the
    /// hydrology system; the table and its bounds are AUTHORED from FHWA HDS-5 practice (600 mm is the common
    /// maintenance-minimum barrel). </summary>
    [Serializable]
    public sealed class CulvertClass
    {
        /// <summary> Upper flow bound (m³/s) of each class except the last; a flow at or below a bound takes that
        /// class's diameter. Ascending. </summary>
        public float[] flowClassUpperBoundsM3PerSecond = { 0.5f, 2.0f, 5.0f, 10.0f };

        /// <summary> Circular barrel diameter (m) per class; one more entry than there are bounds (the final entry
        /// is the over-top-bound class). AUTHORED from HDS-5 practice. Held as double so a tabulated barrel is
        /// returned exactly -- float 0.6f = 0.60000002... and 2.4f = 2.40000009... breached the gate's 1e-9. </summary>
        public double[] barrelDiametresMetres = { 0.6, 0.9, 1.2, 1.8, 2.4 };

        public static CulvertClass Default()
        {
            return new CulvertClass();
        }
    }


    /// <summary> RM-30's authored roadside-ditch (swale) parameters. A ditch is a shallow channel in the road's own
    /// cross-section (RM-31's variable-width profile with a negative depth), and its capacity follows Manning's
    /// open-channel equation. Values AUTHORED from standard drainage practice: an earth/grassed channel Manning n
    /// ~0.03, a symmetric V with 3:1 (horizontal:vertical) side slopes for mowing and safety. </summary>
    [Serializable]
    public sealed class RoadsideDitchClass
    {
        /// <summary> Manning's roughness coefficient. Grassed earth channel ~0.03 (concrete-lined ~0.013). </summary>
        public float manningRoughness = 0.03f;

        /// <summary> Side-slope ratio z (horizontal per 1 vertical) of the symmetric V/trapezoidal channel. 3:1 is
        /// a mowable, traversable roadside slope. </summary>
        public float sideSlopeHorizontalPerVertical = 3f;

        public static RoadsideDitchClass Default()
        {
            return new RoadsideDitchClass();
        }
    }

    /// <summary> RM-30's verifiable core: the pure maths a hydrology-aware culvert placer and ditch variant need,
    /// without a scene. Detection is a planar road×stream crossing whose SKEW angle is data — a shallow crossing
    /// needs a longer barrel. Sizing comes from the stream's flow through the authored HDS-5 table, never from
    /// vehicle clearance. The INVERT is the value that makes or breaks the feature: it must sit at the stream bed
    /// and carry the channel slope through, and a barrel invert above the upstream bed DAMS the stream and is
    /// flagged. The ditch half checks Manning capacity and refuses a run with no outfall (a chain of closed basins
    /// is worse than no ditch). The barrel/opening mesh, the collider, the actual crossing intersection against the
    /// hydrology polylines and the ditch profile sweep are the deferred scene half. </summary>
    public static class Culvert
    {
        /// <summary> Barrel length across the road embankment given the crossing SKEW: length grows as the crossing
        /// leaves square (90°). A stream meeting the road at a shallow angle needs a much longer barrel than one
        /// crossing square. Angle is clamped away from 0° (a parallel "crossing" is not one). </summary>
        public static double BarrelLengthMetres(double roadEmbankmentWidthMetres, double crossingAngleDeg)
        {
            double angle = Math.Abs(crossingAngleDeg) % 180.0;
            if (angle > 90.0) { angle = 180.0 - angle; }
            if (angle < 1.0) { angle = 1.0; } // never divide by ~0
            double sin = Math.Sin(angle * Math.PI / 180.0);
            return roadEmbankmentWidthMetres / sin;
        }

        /// <summary> The barrel diameter and whether the flow exceeds the authored table (in which case the top
        /// size is returned and a full HDS-5 hydraulic design is flagged as needed). </summary>
        public struct BarrelSizing
        {
            public double barrelDiameterMetres;
            public bool exceedsTable;
        }

        /// <summary> Pick the barrel diameter for a design flow from the authored flow-class table. A flow at or
        /// below a class's upper bound takes that class; a flow above every bound takes the final class and is
        /// flagged as exceeding the table. </summary>
        public static BarrelSizing SizeBarrel(double designFlowM3PerSecond, CulvertClass cls)
        {
            BarrelSizing s = new BarrelSizing();
            int bounds = cls.flowClassUpperBoundsM3PerSecond.Length;
            for (int i = 0; i < bounds; i++)
            {
                if (designFlowM3PerSecond <= cls.flowClassUpperBoundsM3PerSecond[i])
                {
                    s.barrelDiameterMetres = cls.barrelDiametresMetres[i];
                    s.exceedsTable = false;
                    return s;
                }
            }
            s.barrelDiameterMetres = cls.barrelDiametresMetres[cls.barrelDiametresMetres.Length - 1];
            s.exceedsTable = true;
            return s;
        }

        /// <summary> Outlet invert = inlet invert dropped by the channel slope over the barrel length, so the
        /// barrel floor stays continuous with the stream bed's own fall. </summary>
        public static double OutletInvertElevationMetres(double inletInvertElevationMetres,
            double channelSlope, double barrelLengthMetres)
        {
            return inletInvertElevationMetres - channelSlope * barrelLengthMetres;
        }

        /// <summary> Whether the barrel dams the stream: true when the inlet invert sits above the upstream bed,
        /// which ponds water behind the road instead of passing it under. </summary>
        public static bool InvertDamsStream(double inletInvertElevationMetres, double upstreamBedElevationMetres)
        {
            return inletInvertElevationMetres > upstreamBedElevationMetres + 1e-6;
        }

        /// <summary> Manning open-channel capacity Q = (1/n)·A·R^(2/3)·S^(1/2), m³/s, for cross-section area A (m²),
        /// hydraulic radius R (m), longitudinal slope S and roughness n. Non-positive n, area, radius or slope give
        /// zero flow. </summary>
        public static double ManningFlowM3PerSecond(double areaM2, double hydraulicRadiusM,
            double longitudinalSlope, double manningRoughness)
        {
            if (manningRoughness <= 0.0 || areaM2 <= 0.0 || hydraulicRadiusM <= 0.0 || longitudinalSlope <= 0.0)
            {
                return 0.0;
            }
            return (1.0 / manningRoughness) * areaM2 * Math.Pow(hydraulicRadiusM, 2.0 / 3.0)
                   * Math.Sqrt(longitudinalSlope);
        }

        /// <summary> Capacity of a symmetric V ditch of a given flow depth: area = z·d², wetted perimeter =
        /// 2·d·√(1+z²), hydraulic radius = A/P, fed through Manning. </summary>
        public static double VDitchCapacityM3PerSecond(double flowDepthMetres, double longitudinalSlope,
            RoadsideDitchClass cls)
        {
            if (flowDepthMetres <= 0.0) { return 0.0; }
            double z = cls.sideSlopeHorizontalPerVertical;
            double area = z * flowDepthMetres * flowDepthMetres;
            double perimeter = 2.0 * flowDepthMetres * Math.Sqrt(1.0 + z * z);
            double radius = area / perimeter;
            return ManningFlowM3PerSecond(area, radius, longitudinalSlope, cls.manningRoughness);
        }

        /// <summary> Whether a ditch run drains to an outfall or forms a closed basin: it drains only when an
        /// outfall exists AND the downstream invert is below the upstream one (water can actually fall to it). A
        /// run that would pond is flagged (drains = false) rather than emitted. </summary>
        public static bool DitchDrainsToOutfall(double upstreamInvertMetres, double downstreamInvertMetres,
            bool outfallPresent)
        {
            return outfallPresent && downstreamInvertMetres < upstreamInvertMetres - 1e-6;
        }
    }
}
