// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> RM-35's AUTHORED grade-separation constants. The vertical clearance is a PUBLISHED standard
    /// quantity, not a taste: AASHTO highway-over-highway minimum vertical clearance is ~16.5 ft ≈ 5.0 m plus a
    /// resurfacing allowance, so 5.1 m is used and RECORDED here; rail and electrified-rail clearances are greater
    /// and a project retargets this field to the standard it builds to. The maximum approach gradient is NOT stored
    /// here -- it is the crossing road CLASS's own maximum, passed in, because a motorway ramp and a lane ramp climb
    /// at different rates. </summary>
    [Serializable]
    public sealed class GradeSeparationClass
    {
        /// <summary> Minimum vertical clearance the raised road must gain over the lower one (metres). AUTHORED to
        /// AASHTO highway-over-highway practice (~5.0 m + allowance); retarget for rail. Held as an exact decimal
        /// (double) so 5.1 m is the value the approach-length maths divides -- a float 5.1f widened to
        /// 5.09999990... and its quotient by the class gradient drifted off the published length. </summary>
        public double verticalClearanceMetres = 5.1;

        public static GradeSeparationClass Default()
        {
            return new GradeSeparationClass();
        }
    }

    /// <summary> RM-35's verifiable core: the pure part of automatic grade separation, without a scene. Two road
    /// centrelines that cross in PLAN (XZ) without sharing a node are a crossing; a deterministic priority decides
    /// which road rises (never asking the major road to duck under the minor one, C-06 tie-break on identifier);
    /// the raised road must gain the published vertical clearance through an approach no steeper than its class's
    /// maximum gradient, which sets a minimum approach length (clearance ÷ gradient) on EACH side; and where the
    /// available approach is shorter than that, the tool REFUSES and reports the length it needed rather than
    /// emitting a ramp that breaks the gradient. Marking the crossing as a bridge span reuses SplineC's existing
    /// BridgeParams range model (a start/end pair), not new machinery. Terrain fitting and the actual profile
    /// raise are the deferred scene half. </summary>
    public static class GradeSeparation
    {
        /// <summary> The outcome of testing whether two plan segments cross. A crossing is a strict INTERIOR
        /// intersection of AB and CD -- endpoints that merely touch are a shared node (a junction), not a
        /// crossing, so they do not trigger a flyover. </summary>
        public struct PlanCrossing
        {
            public bool crosses;
            public double paramAlongAb;   // in (0,1) at the crossing
            public double paramAlongCd;   // in (0,1) at the crossing
            public double xMetres;
            public double zMetres;
        }

        /// <summary> Detect whether plan segments A->B and C->D cross in their interiors (XZ). Parallel or
        /// collinear segments do not cross; an intersection at a shared endpoint is a node, not a crossing. </summary>
        public static PlanCrossing DetectCrossing(double ax, double az, double bx, double bz,
            double cx, double cz, double dx, double dz)
        {
            PlanCrossing r = new PlanCrossing();
            double rX = bx - ax, rZ = bz - az;
            double sX = dx - cx, sZ = dz - cz;
            double denom = rX * sZ - rZ * sX;
            if (Math.Abs(denom) < 1e-12) { return r; } // parallel / degenerate -> no single crossing
            double t = ((cx - ax) * sZ - (cz - az) * sX) / denom;
            double u = ((cx - ax) * rZ - (cz - az) * rX) / denom;
            // Strict interior on both: endpoints touching is a node, not a crossing.
            if (t <= 1e-9 || t >= 1.0 - 1e-9 || u <= 1e-9 || u >= 1.0 - 1e-9) { return r; }
            r.crosses = true;
            r.paramAlongAb = t;
            r.paramAlongCd = u;
            r.xMetres = ax + t * rX;
            r.zMetres = az + t * rZ;
            return r;
        }

        /// <summary> Which of the two crossing roads is the one that rises. Priority is by road CLASS rank first
        /// (higher rank = more major); the MINOR road is the one moved, so a motorway is never asked to duck under
        /// a farm track. Where the ranks are equal, a stable tie-break on the road identifier (the higher id rises)
        /// keeps the choice deterministic (C-06). Returns 0 to raise road A, 1 to raise road B. </summary>
        public static int ChooseRaisedRoad(int classRankA, int identifierA, int classRankB, int identifierB)
        {
            if (classRankA != classRankB) { return classRankA < classRankB ? 0 : 1; } // raise the lower-ranked
            return identifierA >= identifierB ? 0 : 1; // equal class -> stable id tie-break
        }

        /// <summary> The minimum approach length needed on EACH side to gain the clearance without exceeding the
        /// class's maximum gradient: clearance ÷ gradient. A non-positive gradient can never climb, so it needs an
        /// infinite approach (always refused). </summary>
        public static double RequiredApproachLengthMetres(double clearanceMetres, double maxGradientFraction)
        {
            if (maxGradientFraction <= 0.0) { return double.PositiveInfinity; }
            return clearanceMetres / maxGradientFraction;
        }

        /// <summary> The outcome of resolving a detected crossing into a flyover. </summary>
        public struct SeparationResolve
        {
            public bool resolved;                     // a valid flyover was produced
            public bool flagged;                      // could not separate within the available approach
            public double raiseHeightMetres;          // how far the raised road climbs (the clearance)
            public double requiredApproachLengthMetres; // per side, quoted so the author can decide
        }

        /// <summary> Resolve a crossing: the raised road must climb the clearance through an approach no steeper
        /// than its class maximum, which needs <see cref="RequiredApproachLengthMetres"/> on each side. If that much
        /// approach is available the flyover is produced (raise = clearance); otherwise the tool REFUSES and quotes
        /// the length it needed rather than emitting a ramp that breaks the gradient. </summary>
        public static SeparationResolve ResolveSeparation(double clearanceMetres, double maxGradientFraction,
            double availableApproachLengthMetres)
        {
            SeparationResolve r = new SeparationResolve();
            r.requiredApproachLengthMetres = RequiredApproachLengthMetres(clearanceMetres, maxGradientFraction);
            if (availableApproachLengthMetres + 1e-9 >= r.requiredApproachLengthMetres)
            {
                r.resolved = true;
                r.flagged = false;
                r.raiseHeightMetres = clearanceMetres;
            }
            else
            {
                r.resolved = false;
                r.flagged = true; // refuse: not enough approach to separate at this crossing
                r.raiseHeightMetres = 0.0;
            }
            return r;
        }

        /// <summary> The bridge-span range (start/end station in metres along the raised road) that marks the
        /// crossing, centred on the crossing station and half a span each way -- the same (start,end) pair model
        /// SplineC.BridgeParams already carries, clamped to the road's length. </summary>
        public static void BridgeSpanMetres(double crossingStationMetres, double spanLengthMetres,
            double roadLengthMetres, out double startMetres, out double endMetres)
        {
            double half = spanLengthMetres * 0.5;
            startMetres = crossingStationMetres - half;
            endMetres = crossingStationMetres + half;
            if (startMetres < 0.0) { startMetres = 0.0; }
            if (endMetres > roadLengthMetres) { endMetres = roadLengthMetres; }
        }
    }
}
