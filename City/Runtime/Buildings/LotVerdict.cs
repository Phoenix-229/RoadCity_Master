// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> What the buildable-land test (RM-11) decided about a lot. </summary>
    public enum Buildability
    {
        /// <summary> The whole lot passed every test; build on it as-is. </summary>
        Buildable,

        /// <summary> Part of the lot failed (too steep) but a large-enough buildable remnant survives;
        /// build on <see cref="LotVerdict.reshapedPolygon"/> instead of the original lot. </summary>
        Reshaped,

        /// <summary> The lot failed a test that has no remedy, or too little survived a reshape; no
        /// building goes here. <see cref="LotVerdict.reason"/> says which quantity and by how much. </summary>
        Rejected
    }


    /// <summary> The result of testing one candidate lot for buildability (RM-11). The return type IS
    /// the deliverable: a caller (RM-12 placement, the editor gate, an inspector) reads
    /// <see cref="result"/> to know what to do and <see cref="reason"/> to tell the author WHY, with a
    /// number. A rejection an author cannot interrogate turns into "the generator just doesn't put
    /// buildings there", which is the report that gets the test disabled -- so every rejection names
    /// the quantity, its measured value, and the limit it broke. </summary>
    public struct LotVerdict
    {
        /// <summary> Buildable, reshaped, or rejected. </summary>
        public Buildability result;

        /// <summary> A human-readable, quotable explanation naming the quantity, its value and the
        /// limit -- e.g. "max slope 33% exceeds zone max 25%". Always set, including on success. </summary>
        public string reason;

        /// <summary> The steepest ground grade sampled across the lot, as a percentage. </summary>
        public float maxSlope;

        /// <summary> The mean ground grade across the lot's samples, as a percentage. </summary>
        public float meanSlope;

        /// <summary> The height range across the lot in metres (max sample height - min sample
        /// height) -- the relief a pad would have to absorb. </summary>
        public float reliefMetres;

        /// <summary> The buildable remnant polygon, world space, set only when
        /// <see cref="result"/> is <see cref="Buildability.Reshaped"/>; null otherwise. </summary>
        public List<Vector3> reshapedPolygon;
    }
}
