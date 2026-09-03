#region "Imports"
using System;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> One functional class in the street hierarchy (Q-3, answered ROADS-FIRST +
    /// FUNCTIONAL CLASSIFICATION on 2026-08-29): arterial, collector, or local. It is the road
    /// analogue of the rivers' Strahler stream-order model -- a named real-world hierarchy rather
    /// than a random count -- and it is the owner of a segment's geometry limits, which
    /// <see cref="NetworkEdge"/> only references by <see cref="NetworkEdge.roadClassId"/>.
    ///
    /// Everything on this record is AUTHORED, NOT GENERATED: an author edits the catalogue asset,
    /// the generator only reads it. Two groups of fields:
    ///
    ///   * the WIDTH MODEL, which projects straight onto the road tool's own per-road fields when a
    ///     network edge is realised as a <see cref="RoadCityMaster.Road"/> (<c>laneWidth</c>,
    ///     <c>laneAmount</c>, shoulders, and the divided/median settings). The road tool has no
    ///     road-class type of its own -- it carries these as plain fields on each road -- so this
    ///     class is not a duplicate of anything the tool owns; it is the city layer's naming of a
    ///     concept the tool leaves nameless.
    ///   * the GEOMETRY LIMITS the L-system checks before it accepts a proposed segment
    ///     (<see cref="maxGradient"/>, <see cref="minCurveRadius"/>), plus the snapping tolerance
    ///     (<see cref="snapFractionOfWidth"/>) that decides when a proposed endpoint joins an
    ///     existing node instead of creating a near-duplicate. RM-09 states these rather than
    ///     pulling a tolerance from the air. </summary>
    [Serializable]
    public sealed class RoadClass
    {
        /// <summary> The id an edge stores in <see cref="NetworkEdge.roadClassId"/> to select this
        /// class. Unique within a <see cref="RoadClassCatalog"/>. </summary>
        public int id;

        /// <summary> Human-readable name shown in the inspector, e.g. "Arterial". Not used as a key. </summary>
        public string displayName = "";

        /// <summary> Width of one lane in metres. Maps to <see cref="RoadCityMaster.Road.laneWidth"/>. </summary>
        public float laneWidth = 3.5f;

        /// <summary> Number of lanes, counted across BOTH directions -- the number a road class is described by
        /// out loud ("a four-lane arterial"). The road tool stores the two directions separately, so this total
        /// is split on the way in by <see cref="RoadCityMaster.Road.SetLaneTotal"/>; an odd total puts the extra
        /// lane forward, which is what an odd lane count means on a real road (a climbing or passing lane). </summary>
        public int laneAmount = 2;

        /// <summary> Whether roads of this class have a central median (RM-59). Maps to
        /// <see cref="NetworkEdge.isDivided"/> and to the road tool's shoulder/divider settings. </summary>
        public bool isDivided;

        /// <summary> Median width in metres for a divided class; ignored when <see cref="isDivided"/>
        /// is false. Maps to <see cref="NetworkEdge.medianWidth"/>. </summary>
        public float medianWidth;

        /// <summary> Target spacing in metres between parallel roads of this class -- the functional
        /// classification's real-world knob. Arterials sit far apart, collectors between them, locals
        /// fill in. The L-system uses it to decide when to branch a lower class off this one. </summary>
        public float targetSpacing = 400f;

        /// <summary> Preferred distance in metres between successive nodes the generator lays along a
        /// road of this class. Bounded below by what <see cref="minCurveRadius"/> allows for a turn. </summary>
        public float segmentLength = 40f;

        /// <summary> Maximum gradient this class tolerates, as a fraction (rise/run): 0.08 is 8%.
        /// A proposed segment steeper than this is rejected by the geometry-legality check, which is
        /// distinct from the road tool's own <see cref="RoadCityMaster.Road.maxGrade"/> post-filter --
        /// the network refuses to author the segment at all rather than letting the mesher bend it. </summary>
        public float maxGradient = 0.08f;

        /// <summary> Minimum radius in metres a turn of this class may have. The road tool has NO
        /// minimum-radius field -- curvature there is implicit in node spacing -- so this limit lives
        /// here and is enforced when a segment's turn angle against its predecessor is checked. </summary>
        public float minCurveRadius = 30f;

        /// <summary> Snapping tolerance as a fraction of this class's <see cref="TotalWidth"/>: a
        /// proposed endpoint landing within this many road-widths of an existing node joins that node
        /// instead of creating a near-duplicate. AUTHORED and exposed because it is the single knob
        /// that most changes whether a generated network reads as a city or as a wire diagram. </summary>
        [Range(0f, 4f)]
        public float snapFractionOfWidth = 0.75f;

        /// <summary> Total carriageway width in metres: the lanes plus the median when divided. Used
        /// both to project onto the road tool and to scale the snapping tolerance. </summary>
        public float TotalWidth
        {
            get { return laneWidth * laneAmount + (isDivided ? medianWidth : 0f); }
        }

        /// <summary> The snapping distance in metres for this class: <see cref="snapFractionOfWidth"/>
        /// of its <see cref="TotalWidth"/>. </summary>
        public float SnapDistance
        {
            get { return snapFractionOfWidth * TotalWidth; }
        }
    }
}
