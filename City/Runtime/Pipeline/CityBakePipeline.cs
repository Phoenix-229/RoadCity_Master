// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The five stages RM-20's bake runs, in the ONE order that is a correctness requirement and
    /// not a preference: each stage's output is the next one's input, so a bake that runs them out of order
    /// produces a world that is individually correct and collectively wrong -- trees in streets, or
    /// buildings on unconformed ground. The enum's integer order IS the canonical order. </summary>
    public enum BakeStage
    {
        /// <summary> Generate the city from the seed, or load the saved <see cref="CityData"/>. </summary>
        LoadOrGenerate = 0,

        /// <summary> Publish street/pad footprints for terrain conform (RM-14; blocked on § 4.2 today). </summary>
        PublishFootprints = 1,

        /// <summary> Let the terrain conform under the footprints run. </summary>
        ConformTerrain = 2,

        /// <summary> Bake scatter with the city's footprints excluded (RM-15; downstream of RM-14). </summary>
        BakeScatter = 3,

        /// <summary> Produce the runtime representation in Q-6's chosen output form. </summary>
        ProduceRuntime = 4
    }


    /// <summary> RM-20's bake ORDERING contract, kept pure so RM-70's self-test can assert the sequence
    /// rather than trust it (the spec's requirement). The bake wires scene objects, terrain and scatter --
    /// none of that is headless -- but the rule that decides whether a bake is correct is the ORDER its
    /// stages ran in, and that is a pure check over the observed stage sequence, provable without a scene.
    ///
    /// Q-6 (the runtime output FORM) is resolved here as an AUTHORED default, per § 10's governing rule and
    /// the standing don't-stop/use-recommended directive, NOT a cited decision: the runtime form is a
    /// PREFAB-INSTANCE SPAWN TABLE -- the bake writes the <see cref="CityData"/> asset plus a serialised
    /// table of (prefab, transform, chunk, LOD band), and a runtime loader instantiates them, budgeting
    /// across frames (RM-17). Chosen because Q-4 already answered "placed prefabs" and the tree already
    /// carries <see cref="PrefabContract"/>/<see cref="BuildingInstance"/>, so this reuses the existing model
    /// and stays streamable per chunk; pre-combined meshes are a later optimisation, not the first form. </summary>
    public static class CityBakePipeline
    {
        /// <summary> The stages in canonical order. </summary>
        public static readonly BakeStage[] CanonicalOrder =
        {
            BakeStage.LoadOrGenerate,
            BakeStage.PublishFootprints,
            BakeStage.ConformTerrain,
            BakeStage.BakeScatter,
            BakeStage.ProduceRuntime
        };

        /// <summary> What this build's bake stamps itself with, and the figure <see cref="BakeStamp.Evaluate"/> is asked
        /// to compare an existing city against.
        ///
        /// It lives here rather than on the request because the version describes the PIPELINE -- what a bake does -- and
        /// a caller that supplied its own would let two callers stamp the same output with two versions. It is a constant
        /// rather than read from the package because the package carries no version yet (the changelog's city layer is
        /// still Unreleased), and inventing one from a file's date would give the same bake a different stamp on two
        /// machines.
        ///
        /// AUTHORED, in the sense the inspector's range sources use the word: the number is deliberate, not measured.
        /// Bump the MINOR when a bake's output changes shape and the PATCH when it changes value, because that is what
        /// <see cref="BakeStamp.CompareSemanticVersions"/> orders and what turns an old city's inspector into a
        /// "baked by an older generator" line instead of a silent wrong answer. </summary>
        public const string GeneratorVersion = "1.0.0";

        /// <summary> True when the observed run ran its stages in canonical order with no stage repeated: the
        /// stage indices must strictly increase. A blocked stage that is SKIPPED (RM-14/RM-15 today) is
        /// allowed -- the rule forbids running a stage before an earlier one, not omitting one. Writes the
        /// FIRST violation to <paramref name="reason"/>, in the report discipline. </summary>
        public static bool IsInCanonicalOrder(IList<BakeStage> observed, out string reason)
        {
            reason = "in order";
            if (observed == null || observed.Count == 0) { return true; }
            int prev = -1;
            for (int i = 0; i < observed.Count; i++)
            {
                int idx = (int)observed[i];
                if (idx <= prev)
                {
                    reason = "stage " + observed[i] + " ran after " + (BakeStage)prev
                        + " -- out of canonical order (or repeated)";
                    return false;
                }
                prev = idx;
            }
            return true;
        }
    }
}
