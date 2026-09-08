// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> Which end of a road a node sits at. A road's terminus can be its first node or its last, and the
    /// terminator must face OUTWARD at each -- outward is the road's tangent at the last node and the reverse of it
    /// at the first -- so the end has to be named. </summary>
    public enum RoadEnd
    {
        Start = 0,
        End = 1
    }


    /// <summary> A resolved terminus placement in world space: where the terminator prefab sits and which way it
    /// faces. The height is the ROAD's own surface height, not sampled terrain -- a terminator that samples the
    /// ground sinks into the shoulder on any raised road, which is the specific defect RM-26 exists to prevent. </summary>
    public struct TerminusPlacement
    {
        public Vector3 position;
        public float yawDegrees;
    }


    /// <summary> RM-26's verifiable core: the pure part of a road terminus -- detecting which road ends are
    /// termini, and computing WHERE and HOW a terminator sits once the author has chosen one. Two halves, exactly as
    /// the spec splits them.
    ///
    /// The first half is DETECTION, and the existing node data already answers it: a terminus is the first or last
    /// node of a road that is NOT part of an intersection. That test is written here against a plain boolean taken
    /// from the real node flag (<c>SplineN</c> already tracks intersection membership) rather than guessed, so two
    /// roads meeting end-to-end at a junction correctly show nothing, while a road that stops at the world bounds or
    /// because the author stopped drawing is treated the same as any other free end.
    ///
    /// The second half is deliberately NOT automatic selection: the tool must not decide that a dead end wants a
    /// guardrail rather than a bollard row, a planter or nothing, because the same geometry ends a mountain road, a
    /// cul-de-sac and a construction site. So the terminator is an author-selected prefab per road-end node,
    /// defaulting to NONE. What the code owns is placement QUALITY, not selection: centred on the carriageway,
    /// squared to the final node's heading, seated on the road's own surface, and repeatable so a re-bake lands the
    /// same prefab identically (C-06). The turning-bulb cul-de-sac is a sibling task that reuses RM-28's turning
    /// radius, explicitly out of scope here. </summary>
    public static class RoadTerminus
    {
        /// <summary> Whether a given road end is a terminus: it is one exactly when that endpoint node is NOT part of
        /// an intersection. Reads the same intersection-membership flag the node data already carries. </summary>
        public static bool IsTerminus(bool endpointBelongsToIntersection)
        {
            return !endpointBelongsToIntersection;
        }

        /// <summary> Whether a terminator should actually be placed at this end: only when the end is a terminus AND
        /// the author has assigned a prefab to its slot. The default -- no prefab assigned -- places nothing, which
        /// is why a plain road end shows nothing until the author chooses a treatment. </summary>
        public static bool ShouldPlace(bool endpointBelongsToIntersection, bool prefabAssigned)
        {
            return prefabAssigned && IsTerminus(endpointBelongsToIntersection);
        }

        /// <summary> The outward-facing horizontal direction of a terminator, in the XZ plane. At the END node
        /// outward is the road's tangent there; at the START node it is the reverse, so the terminator always faces
        /// away from the road it caps. Returns a normalised direction; a zero tangent yields +Z as a safe default. </summary>
        public static Vector2 OutwardDirectionXZ(Vector2 tangentAtEndXZ, RoadEnd whichEnd)
        {
            Vector2 dir = whichEnd == RoadEnd.End ? tangentAtEndXZ : -tangentAtEndXZ;
            float magnitude = dir.magnitude;
            if (magnitude < 1e-6f) { return new Vector2(0f, 1f); }
            return dir / magnitude;
        }

        /// <summary> The yaw (rotation about Y, degrees) that faces a terminator along a horizontal XZ direction,
        /// in Unity's convention: +Z is yaw 0, +X is yaw 90. This is how the terminator is "squared to the final
        /// node's heading". </summary>
        public static float YawDegreesFor(Vector2 outwardDirectionXZ)
        {
            return Mathf.Atan2(outwardDirectionXZ.x, outwardDirectionXZ.y) * Mathf.Rad2Deg;
        }

        /// <summary> Compute the full terminus placement. The XZ position is the node's carriageway-centre position
        /// unchanged (centred on the carriageway); the height is forced to the ROAD's surface height, never the
        /// sampled ground, so a raised road's terminator sits on the road rather than sinking into the shoulder; the
        /// yaw squares the terminator to the final node's heading, facing outward. Pure and repeatable (C-06). </summary>
        public static TerminusPlacement ComputePlacement(Vector3 endNodeCentre, float roadSurfaceHeightMetres,
            Vector2 tangentAtEndXZ, RoadEnd whichEnd)
        {
            Vector2 outward = OutwardDirectionXZ(tangentAtEndXZ, whichEnd);
            TerminusPlacement placement = new TerminusPlacement();
            placement.position = new Vector3(endNodeCentre.x, roadSurfaceHeightMetres, endNodeCentre.z);
            placement.yawDegrees = YawDegreesFor(outward);
            return placement;
        }
    }
}
