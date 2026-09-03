#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-36's AUTHORED ropeway constants. A ropeway (cable car / gondola) is ninety per cent existing
    /// machinery: the rope is RM-23's catenary, the towers and cabins are prefabs at nodes / at a fixed spacing
    /// (EdgeObjectMaker), and the clearance report is RM-23's SpanClearance. The only genuinely ropeway-specific
    /// numbers are AUTHORED here from ordinary aerial-ropeway practice (C-15): a haul rope hangs with a few per
    /// cent sag, cabins are spaced at a fixed interval along the line, and the line must keep a minimum height
    /// over the ground for the whole span. Values are AUTHORED, retunable per project. </summary>
    [Serializable]
    public sealed class RopewayClass
    {
        /// <summary> Rope sag as a fraction of span (the UNLOADED haul-rope shape; see the tooltip note). RM-23's
        /// solver takes the sag ratio, never the catenary constant. </summary>
        public float sagRatio = 0.04f;

        /// <summary> Minimum vertical distance the rope must keep above the ground at every station of every span,
        /// or the span is flagged. AUTHORED clearance for a passenger ropeway over open ground. </summary>
        public float minGroundClearanceMetres = 8f;

        /// <summary> Cabin (carrier) spacing along the line. AUTHORED gondola headway spacing. </summary>
        public float cabinSpacingMetres = 60f;

        /// <summary> Turn angle past which a node needs an angle tower rather than a suspension tower (RM-23's
        /// rule, reused). </summary>
        public float towerAngleThresholdDeg = 8f;

        /// <summary> A ropeway is a carrier spline with ROAD MESH GENERATION OFF: it makes no carriageway, kerb or
        /// markings -- only towers, rope and cabins. This is the shared answer to RM-25's "a road that makes no
        /// road" question. </summary>
        public bool generatesRoadMesh = false;

        public static RopewayClass Default()
        {
            return new RopewayClass();
        }
    }

    /// <summary> RM-36's verifiable core: the pure ropeway decisions, without a scene. The rope between two tower
    /// tops is RM-23's catenary solved as a SHARED utility (never a second sag solver -- that is how two copies
    /// drift apart), the tower type at a node is RM-23's turn-angle rule, and the ground-clearance report per span
    /// is RM-23's SpanClearance. What is added here is only the cabin spacing along the line and the "no road
    /// mesh" carrier answer. The terminal stations, the swept rope tube (SplinatedMeshMaker) and the tower/cabin
    /// prefab placement (EdgeObjectMaker) are the deferred scene half. </summary>
    public static class Ropeway
    {
        /// <summary> Solve the haul rope between two consecutive tower tops -- RM-23's two-point catenary, reused.
        /// x = 0 at the first tower, x = span at the second; heights are the tower-top elevations. </summary>
        public static Catenary SolveRope(double spanMetres, double towerTopHeight1, double towerTopHeight2,
            RopewayClass cls)
        {
            return TransmissionLine.Solve(spanMetres, towerTopHeight1, towerTopHeight2, cls.sagRatio);
        }

        /// <summary> The tower type at a node from its plan turn angle -- RM-23's rule, reused: a straight-through
        /// node is a suspension tower, a real turn is an angle tower. </summary>
        public static TowerType TowerTypeAtNode(double turnAngleDeg, RopewayClass cls)
        {
            return TransmissionLine.ChooseTowerType(turnAngleDeg, cls.towerAngleThresholdDeg);
        }

        /// <summary> The number of cabins that fit along a line of a given length at the authored spacing:
        /// floor(length / spacing) + 1, so a line always carries at least the one cabin at its start. A
        /// non-positive length or spacing carries none. </summary>
        public static int CabinCount(double lineLengthMetres, RopewayClass cls)
        {
            if (lineLengthMetres <= 0.0 || cls.cabinSpacingMetres <= 0f) { return 0; }
            return (int)Math.Floor(lineLengthMetres / cls.cabinSpacingMetres) + 1;
        }

        /// <summary> The station (metres from the start of the line) of the cabin at the given index, evenly
        /// spaced at the authored interval. Deterministic: a pure multiply, no accumulation drift (C-06). </summary>
        public static double CabinStationMetres(int cabinIndex, RopewayClass cls)
        {
            if (cabinIndex < 0) { return 0.0; }
            return cabinIndex * (double)cls.cabinSpacingMetres;
        }

        /// <summary> The ground-clearance report for one span -- RM-23's SpanClearance, reused: walk the span at
        /// evenly spaced stations, sample the ground under the rope, and flag the span if the tightest clearance
        /// is below the authored minimum rather than quietly running the rope through a hill. </summary>
        public static TransmissionLine.ClearanceResult SpanClearance(Catenary rope, IList<double> groundHeights,
            RopewayClass cls)
        {
            return TransmissionLine.SpanClearance(rope, groundHeights, cls.minGroundClearanceMetres);
        }
    }
}
