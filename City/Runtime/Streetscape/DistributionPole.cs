// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using UnityEngine;
using RoadCityMaster.CrossSection;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which side of a road frontage a pole run sits on. A real distribution run stays on ONE side
    /// for its whole length rather than alternating, so this is a per-run flag the planner holds stable. </summary>
    public enum FrontageSide
    {
        Left = 0,
        Right = 1
    }


    /// <summary> RM-24's authored parameter block for a distribution-pole run. AUTHORED against real ranges
    /// (C-15): distribution spans are TENS of metres -- 40 m nominal, 30 to 90 m in practice -- an order of
    /// magnitude shorter than RM-23's transmission spans. That difference changes the NUMBERS the span is solved
    /// with; it does not change the method. B-27: the cable is a catenary here as well, solved by the one solver
    /// in <see cref="TransmissionLine"/>, because the straight-segment simplification that used to live here was
    /// a second answer to a question the tool had already answered.
    ///
    /// The electrical figures are specification figures, not chosen ones:
    ///
    /// * <see cref="conductorUnitWeightNewtonsPerMetre"/> and <see cref="conductorRatedStrengthNewtons"/> describe
    ///   a bare ACSR distribution conductor -- 6/1 Raven, the ordinary overhead choice at this voltage.
    ///   <see cref="conductorDiameterMetres"/> is the same conductor's overall diameter, 0.398 in over its six
    ///   aluminium strands and steel core. It is here beside the weight and the strength, and not with the pole's
    ///   timber dimensions, because it is the third figure of one product row: the wire is DRAWN at the diameter it
    ///   is SAGGED at, and a geometry that took its thickness from somewhere else would be a different conductor.
    /// * <see cref="everydayTensionFractionOfRatedStrength"/> is 0.20. Everyday (unloaded, ambient) stringing
    ///   tension is held near 20 % of rated breaking strength so that ice and wind loading still leaves margin;
    ///   it is the figure that turns a weight and a span into a sag, through w.L/(8H).
    /// * <see cref="roadCrossingClearanceMetres"/> is 5.6 m, NESC (ANSI C2) Rule 232, Table 232-1, the road
    ///   column for open supply conductors of 750 V to 22 kV. <see cref="secondaryCrossingClearanceMetres"/> is
    ///   the same table's 4.7 m for 0 to 750 V -- a neutral or a service drop is allowed lower than a phase.
    /// * <see cref="communicationSeparationMetres"/> is 1.02 m (40 in), NESC Rule 238, the vertical separation a
    ///   communication attachment must keep below the supply conductors on a shared pole.
    /// * <see cref="settingDepthFraction"/> and <see cref="settingDepthConstantMetres"/> are the ANSI O5.1
    ///   setting rule: bury 10 % of the pole's length plus 0.61 m (2 ft). This is why a pole's mesh runs below
    ///   the ground it is seated on rather than stopping at it.
    /// * <see cref="crossArmLengthMetres"/> 2.44 m (8 ft) and <see cref="phaseSpacingMetres"/> 0.76 m (30 in) are
    ///   the standard wood cross-arm and its pin spacing.
    ///
    /// The remaining numbers are the conflict-resolution handles the frontage needs. </summary>
    [Serializable]
    public sealed class DistributionPoleClass
    {
        public float spanLengthMetres = 40f;
        public FrontageSide side = FrontageSide.Right;
        public float minSeparationFromFurnitureMetres = 2f;
        public float maxNudgeMetres = 5f;
        public float nudgeStepMetres = 0.5f;

        public float conductorUnitWeightNewtonsPerMetre = 3.03f;
        public float conductorRatedStrengthNewtons = 19100f;
        public float conductorDiameterMetres = 0.0101f;
        public float everydayTensionFractionOfRatedStrength = 0.20f;

        public float roadCrossingClearanceMetres = 5.6f;
        public float secondaryCrossingClearanceMetres = 4.7f;
        public float communicationSeparationMetres = 1.02f;

        public float settingDepthFraction = 0.10f;
        public float settingDepthConstantMetres = 0.61f;

        public float crossArmLengthMetres = 2.44f;
        public float phaseSpacingMetres = 0.76f;

        public static DistributionPoleClass Default()
        {
            return new DistributionPoleClass();
        }

        /// <summary> Everyday stringing tension in newtons: the rated breaking strength taken down to its everyday
        /// fraction. Kept as a method rather than a field because two of the three inputs are the author's and a
        /// stale cached third would be the kind of disagreement nobody finds. </summary>
        public double EverydayTensionNewtons()
        {
            double fraction = everydayTensionFractionOfRatedStrength > 0f
                ? everydayTensionFractionOfRatedStrength : 0.20;
            double rated = conductorRatedStrengthNewtons > 0f ? conductorRatedStrengthNewtons : 19100.0;
            return rated * fraction;
        }

        /// <summary> The sag ratio this conductor takes at a given span, from the parabolic approximation to the
        /// catenary that the industry strings by: sag = w.L^2/(8H), so sag/L = w.L/(8H). That ratio is what
        /// <see cref="TransmissionLine.Solve"/> takes, which is how a distribution span and a transmission span
        /// reach the same solver by different numbers. </summary>
        public double SagRatioAt(double spanMetres)
        {
            if (spanMetres <= 0.0) { return 0.0; }
            double weight = conductorUnitWeightNewtonsPerMetre > 0f
                ? conductorUnitWeightNewtonsPerMetre : 3.03;
            double tension = EverydayTensionNewtons();
            if (tension <= 0.0) { return 0.0; }
            return (weight * spanMetres) / (8.0 * tension);
        }

        /// <summary> How deep a pole of a given total length is buried, ANSI O5.1: 10 % of the length plus 0.61 m.
        /// </summary>
        public double SettingDepthMetres(double poleLengthMetres)
        {
            if (poleLengthMetres <= 0.0) { return settingDepthConstantMetres; }
            return poleLengthMetres * settingDepthFraction + settingDepthConstantMetres;
        }
    }


    /// <summary> RM-24's verifiable core: where distribution poles land along a road frontage, once the
    /// frontage is already carrying RM-13's streetlights, signs, bins and bus stops from the SAME
    /// <c>EdgeObjectMaker</c> mechanism. A pole is just a prefab repeated at a spacing -- that part is the
    /// existing maker -- so the real task, and the part that can be proven headlessly, is everything AROUND the
    /// spacing: keeping the run on one consistent side, and resolving the conflict where a pole would land on a
    /// furniture item already on that strip. The spacing/exclusion maths is RM-13's
    /// <see cref="FurnitureRunPlanner"/> reused, not re-implemented (no-duplication).
    ///
    /// Conflict resolution is (a) skip stations inside an intersection corner (delegated to the planner's
    /// exclusion spans), (b) skip or NUDGE a station that collides with an already-placed furniture item, and
    /// (c) do both in a FIXED order so the outcome is reproducible (C-06).
    ///
    /// B-27 removed the one thing in this file that was its own answer to a solved problem. The cable used to be
    /// a straight segment here, documented as an honest simplification, while RM-23's spans a few hundred metres
    /// away were catenaries. Two curves for one physical object is how a tool ends up with a pole line that sags
    /// in a city and not on a highway, and the author's photograph shows the sag at 40 m spans plainly. So the
    /// straight-segment method is gone and <see cref="CableAt"/> asks <see cref="TransmissionLine.Solve"/> --
    /// the same solver, at distribution numbers: a 6/1 ACSR conductor at 20 % of rated strength over 40 m sags
    /// 0.159 m, which is 16 cm of droop at eye level and not a mesh-width rounding error. </summary>
    public static class DistributionPoleRun
    {
        /// <summary> Resolve the pole stations for one run. Starts from the quantised, intersection-excluded
        /// candidate stations (RM-13's planner), then walks them in ascending order and resolves each against
        /// the furniture already on the strip: a colliding pole is nudged by whole steps -- the search order is
        /// fixed (0, +step, -step, +2*step, -2*step, ...) so it is reproducible -- and dropped only if no offset
        /// within <see cref="DistributionPoleClass.maxNudgeMetres"/> clears both the furniture and the exclusion
        /// spans while staying on the run. </summary>
        public static List<float> PlanPoleStations(float runLength, DistributionPoleClass cls,
            IList<Vector2> intersectionExclusions, IList<float> existingFurniture)
        {
            List<float> result = new List<float>();
            if (cls == null || runLength <= 0f) { return result; }

            List<float> candidates = FurnitureRunPlanner.PlanStations(
                runLength, cls.spanLengthMetres, intersectionExclusions);

            for (int i = 0; i < candidates.Count; i++)
            {
                float resolved;
                if (TryResolve(candidates[i], runLength, cls, intersectionExclusions,
                        existingFurniture, out resolved))
                {
                    result.Add(resolved);
                }
            }
            return result;
        }

        /// <summary> The fixed-order nudge search for one candidate station. Returns the first offset within the
        /// maximum nudge (tried as 0, +step, -step, +2*step, -2*step, ...) that clears every furniture item by
        /// the minimum separation, avoids the intersection exclusions, and stays within [0, runLength]. </summary>
        public static bool TryResolve(float station, float runLength, DistributionPoleClass cls,
            IList<Vector2> intersectionExclusions, IList<float> existingFurniture, out float resolved)
        {
            resolved = station;
            float step = cls.nudgeStepMetres > 0f ? cls.nudgeStepMetres : 0.5f;
            int maxSteps = Mathf.Max(0, Mathf.FloorToInt(cls.maxNudgeMetres / step));

            for (int k = 0; k <= maxSteps; k++)
            {
                // Try +k then -k so the search order is deterministic; k == 0 tries the original once.
                for (int sign = 1; sign >= -1; sign -= 2)
                {
                    float d = station + sign * k * step;
                    if (d < 0f || d > runLength) { continue; }
                    if (FurnitureRunPlanner.IsExcluded(d, intersectionExclusions)) { continue; }
                    if (ClearsFurniture(d, existingFurniture, cls.minSeparationFromFurnitureMetres))
                    {
                        resolved = d;
                        return true;
                    }
                    if (k == 0) { break; } // avoid testing the original station twice
                }
            }
            return false;
        }

        /// <summary> Whether a station is at least the minimum separation from every already-placed furniture
        /// item. </summary>
        public static bool ClearsFurniture(float station, IList<float> existingFurniture, float minSeparation)
        {
            if (existingFurniture == null) { return true; }
            for (int i = 0; i < existingFurniture.Count; i++)
            {
                if (Mathf.Abs(station - existingFurniture[i]) < minSeparation) { return false; }
            }
            return true;
        }

        /// <summary> The one catenary, at distribution numbers. Solves the span between two attachment heights with
        /// <see cref="TransmissionLine.Solve"/> at the sag ratio this conductor and tension produce over this span
        /// (<see cref="DistributionPoleClass.SagRatioAt"/>), and returns it whole so a caller that wants the wire's
        /// height at ten points along the span solves once rather than ten times.
        ///
        /// B-27 replaced <c>StraightCableHeightAt</c> with this. That method interpolated linearly and its own
        /// documentation called the result an honest simplification; it was honest and it was still a second curve
        /// for the same wire. </summary>
        public static Catenary SolveSpan(double spanMetres, double attachHeight1,
            double attachHeight2, DistributionPoleClass cls)
        {
            DistributionPoleClass c = cls != null ? cls : DistributionPoleClass.Default();
            return TransmissionLine.Solve(spanMetres, attachHeight1, attachHeight2, c.SagRatioAt(spanMetres));
        }

        /// <summary> The wire's height at a fraction t along one span. Same signature shape as the deleted straight
        /// method so every reader converts by adding the span and the class, but the answer now hangs. </summary>
        public static double CableAt(double spanMetres, double attachHeight1, double attachHeight2,
            DistributionPoleClass cls, double t)
        {
            if (spanMetres <= 0.0) { return attachHeight1; }
            double clamped = t < 0.0 ? 0.0 : (t > 1.0 ? 1.0 : t);
            Catenary cat = SolveSpan(spanMetres, attachHeight1, attachHeight2, cls);
            return cat.HeightAt(clamped * spanMetres);
        }

        /// <summary> Whether one distribution span keeps its clearance over the ground beneath it, and by how
        /// little at its worst station. Delegates to <see cref="TransmissionLine.SpanClearance"/> -- the checker
        /// the transmission runs use -- with the NESC road figure from the class, so a span that dips below the
        /// road crossing clearance is refused by the same arithmetic in both families.
        ///
        /// <paramref name="overRoadway"/> picks which of Table 232-1's two columns applies: a phase conductor
        /// crossing a road needs 5.6 m, a secondary or a neutral 4.7 m. </summary>
        public static TransmissionLine.ClearanceResult SpanClearance(double spanMetres, double attachHeight1,
            double attachHeight2, DistributionPoleClass cls, IList<double> groundHeights, bool overRoadway)
        {
            DistributionPoleClass c = cls != null ? cls : DistributionPoleClass.Default();
            double required = overRoadway ? c.roadCrossingClearanceMetres : c.secondaryCrossingClearanceMetres;
            Catenary cat = SolveSpan(spanMetres, attachHeight1, attachHeight2, c);
            return TransmissionLine.SpanClearance(cat, groundHeights, required);
        }
    }
}
