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
    /// <summary> The authored catalogue of <see cref="RoadClass"/> entries a city generation uses.
    /// One asset, edited in the inspector; the street-network operator reads it and never writes it.
    /// Its contents ARE the Q-3 answer made concrete -- a functional classification of arterial /
    /// collector / local, each with real spacing and geometry limits -- so it is where an author
    /// tunes the shape of every city without touching code.
    ///
    /// It is a <see cref="ScriptableObject"/> rather than a hard-coded table for the same reason the
    /// operators are (section 10.4): the parameters that decide a city's shape must be editable
    /// assets that hash into RM-20's bake record, not literals recompiled into a build. </summary>
    [CreateAssetMenu(fileName = "RoadClassCatalog", menuName = "Road City Master/Road Class Catalog", order = 1)]
    public sealed class RoadClassCatalog : ScriptableObject
    {
        /// <summary> The classes in this catalogue. Order is not significant -- lookup is by
        /// <see cref="RoadClass.id"/>, not by position -- so an author may reorder freely. </summary>
        public List<RoadClass> classes = new List<RoadClass>();

        /// <summary> Ids for the three baseline functional classes, so operator code and defaults can
        /// refer to them by name rather than by a bare integer. An author may add further classes with
        /// other ids; these three are only the baseline the default catalogue ships. </summary>
        public const int ArterialId = 0;
        public const int CollectorId = 1;
        public const int LocalId = 2;

        /// <summary> Finds the class with the given id, or null when the catalogue has none. Linear
        /// scan: a catalogue holds a handful of classes, not thousands, so an index would cost more to
        /// keep valid than it saves. </summary>
        public RoadClass Get(int id)
        {
            for (int index = 0; index < classes.Count; index++)
            {
                if (classes[index] != null && classes[index].id == id)
                {
                    return classes[index];
                }
            }

            return null;
        }

        /// <summary> True when a class with the given id exists, returning it in
        /// <paramref name="roadClass"/>. </summary>
        public bool TryGet(int id, out RoadClass roadClass)
        {
            roadClass = Get(id);
            return roadClass != null;
        }

        /// <summary> Overwrites <see cref="classes"/> with the baseline functional classification:
        /// a divided arterial spaced widely, an undivided collector between arterials, and a narrow
        /// local street filling the blocks. The numbers are ordinary real-world figures an author is
        /// expected to change; they exist so a fresh catalogue generates a sane city rather than an
        /// empty one. Called from the asset's context menu. </summary>
        [ContextMenu("Reset to default functional classification")]
        public void ResetToDefaults()
        {
            classes = new List<RoadClass>
            {
                new RoadClass
                {
                    id = ArterialId, displayName = "Arterial",
                    laneWidth = 3.65f, laneAmount = 4, isDivided = true, medianWidth = 4f,
                    targetSpacing = 1200f, segmentLength = 80f,
                    maxGradient = 0.06f, minCurveRadius = 200f, snapFractionOfWidth = 0.75f
                },
                new RoadClass
                {
                    id = CollectorId, displayName = "Collector",
                    laneWidth = 3.5f, laneAmount = 2, isDivided = false, medianWidth = 0f,
                    targetSpacing = 400f, segmentLength = 50f,
                    maxGradient = 0.08f, minCurveRadius = 90f, snapFractionOfWidth = 0.75f
                },
                new RoadClass
                {
                    id = LocalId, displayName = "Local",
                    laneWidth = 3.0f, laneAmount = 2, isDivided = false, medianWidth = 0f,
                    targetSpacing = 120f, segmentLength = 35f,
                    maxGradient = 0.12f, minCurveRadius = 30f, snapFractionOfWidth = 0.75f
                }
            };
        }
    }
}
