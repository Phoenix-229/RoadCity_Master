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
    /// <summary> The non-generic face of a pipeline operator, so a stage can hold a list of
    /// operators of differing input/output types and a future canvas (Q-10) can inspect how they
    /// connect without knowing each operator's concrete types. Every operator is a
    /// <see cref="CityOperator{TIn, TOut}"/>, which supplies these. </summary>
    public interface ICityOperator
    {
        /// <summary> The type this operator consumes. Declared on the asset so the pipeline can
        /// check that one stage's output feeds the next stage's input. </summary>
        Type InputType { get; }

        /// <summary> The type this operator produces. </summary>
        Type OutputType { get; }
    }


    /// <summary> The contract every city-pipeline operator obeys (section 10.4). An operator is a
    /// <see cref="ScriptableObject"/> asset -- so a stage is an ordered list of assets in an
    /// inspector, editable without code and hashable straight into RM-20's bake record -- and it is
    /// a PURE FUNCTION of three things and nothing else:
    ///
    ///   * its declared input <typeparamref name="TIn"/>,
    ///   * its own serialised parameters (the fields a subclass adds to the asset),
    ///   * the seed passed to <see cref="Execute"/>, which the pipeline derives from the city seed
    ///     via <see cref="CitySeed.ForOperator"/>.
    ///
    /// From those, running the operator twice must yield byte-identical output. That forbids, by
    /// contract: reading the scene or any global state, touching the file system, and any use of an
    /// unseeded random source such as UnityEngine.Random or DateTime.Now (determinism-exempt: named
    /// here in prose, not called -- RM-18's smell test). An operator that needs
    /// randomness derives it from the seed it was given -- for per-item randomness, through
    /// <see cref="CitySeed.ForItem"/> keyed on the item, so the result does not depend on the order
    /// items happen to be visited in.
    ///
    /// Because <see cref="InputType"/> and <see cref="OutputType"/> are declared on the asset,
    /// layering a graph editor over these operators later is purely additive: it becomes one more
    /// way to arrange the same operators, and no operator has to change. Keeping that escape hatch
    /// open is the entire reason this contract is fixed now, at RM-08, before the first operator
    /// exists. </summary>
    public abstract class CityOperator<TIn, TOut> : ScriptableObject, ICityOperator
    {
        /// <summary> The declared input type, from <typeparamref name="TIn"/>. </summary>
        public Type InputType
        {
            get { return typeof(TIn); }
        }

        /// <summary> The declared output type, from <typeparamref name="TOut"/>. </summary>
        public Type OutputType
        {
            get { return typeof(TOut); }
        }

        /// <summary> Runs the operator. Must be a pure function of <paramref name="input"/>, this
        /// asset's own serialised fields, and <paramref name="operatorSeed"/>: no scene access, no
        /// file access, no unseeded randomness, and byte-identical output when re-run with the same
        /// arguments. </summary>
        /// <param name="input"> The stage input this operator transforms. </param>
        /// <param name="operatorSeed"> The operator's seed, from
        /// <see cref="CitySeed.ForOperator"/>. </param>
        public abstract TOut Execute(TIn input, int operatorSeed);
    }
}
