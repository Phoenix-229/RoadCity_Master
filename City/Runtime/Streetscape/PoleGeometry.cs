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
    /// <summary> B-27 / D-2. The SHAPE of a wood distribution pole, separately from the electrical figures on
    /// <see cref="DistributionPoleClass"/>. Two classes because they answer to different documents and are edited by
    /// different people: the clearances and tensions are NESC and conductor tables, these are the timber and the
    /// hardware catalogue. A pole that is strung to the wrong tension is an engineering error; a pole that is 35 ft
    /// instead of 40 ft is a purchasing decision.
    ///
    /// Every default is a real product dimension, not a chosen one:
    ///
    /// * A 40 ft (12.19 m) Class 4 southern pine pole, the ordinary distribution pole. ANSI O5.1 specifies poles by
    ///   CIRCUMFERENCE, so the diameters here are that table converted: 21 in around at the top is 0.170 m across,
    ///   and Class 4 at 40 ft is 33.5 in around six feet from the butt, which is 0.271 m across. The shaft is drawn
    ///   as a taper between those two because that is what the table describes -- a pole is a tree.
    /// * The cross-arm is the standard 8 ft timber arm, 3-5/8 in by 4-5/8 in in section (0.092 x 0.117 m), with
    ///   28 in (0.711 m) braces.
    /// * The pin insulator is an ANSI 55-4, 0.146 m across and 0.127 m tall.
    /// * The transformer is a 25 kVA single-phase pole-mount can, 0.53 m across and 0.74 m tall -- the size that
    ///   actually appears on a residential street, not a substation unit.
    /// * The ground wire is #6 copper (4.1 mm) and the guy is 3/8 in EHS strand (9.5 mm).
    ///
    /// <see cref="ShapeHash"/> exists because the editor-side <c>PoleMeshFactory</c> caches by parameter set: if
    /// an author edits a dimension here, every cached mesh built from the old value has to stop being returned, and
    /// a cache that could not notice would answer with last week's pole for the rest of the session. </summary>
    [Serializable]
    public sealed class PoleGeometryClass
    {
        public float poleLengthMetres = 12.19f;
        public float topDiameterMetres = 0.170f;
        public float buttDiameterMetres = 0.271f;
        public int shaftSides = 8;

        public float crossArmDepthMetres = 0.092f;
        public float crossArmHeightMetres = 0.117f;
        public float braceLengthMetres = 0.711f;
        public float braceThicknessMetres = 0.032f;
        public float armBelowTopMetres = 0.30f;

        public float insulatorDiameterMetres = 0.146f;
        public float insulatorHeightMetres = 0.127f;
        public int insulatorSides = 8;

        public float transformerDiameterMetres = 0.53f;
        public float transformerHeightMetres = 0.74f;
        public float transformerBelowArmMetres = 1.80f;

        public float cutoutHeightMetres = 0.30f;
        public float cutoutDiameterMetres = 0.09f;
        public float arresterHeightMetres = 0.22f;
        public float arresterDiameterMetres = 0.07f;

        public float neutralBelowArmMetres = 0.61f;
        public float secondaryBelowNeutralMetres = 0.61f;
        public float rackWidthMetres = 0.30f;

        public float groundWireDiameterMetres = 0.0041f;
        public float guyDiameterMetres = 0.0095f;
        public int wireSides = 4;

        public static PoleGeometryClass Default()
        {
            return new PoleGeometryClass();
        }

        /// <summary> A hash of every dimension that changes the mesh, so the factory's cache can tell one shape from
        /// another. Deterministic (C-06): a fixed field order and integer arithmetic only, no floating-point hash and
        /// no reflection, so the same figures give the same number on every machine and every run. Quantised to a
        /// tenth of a millimetre, which is finer than any of these products is manufactured to and coarse enough that
        /// a float's last bit cannot invent a second cache entry for one pole. </summary>
        public int ShapeHash()
        {
            int h = 17;
            h = Mix(h, poleLengthMetres);
            h = Mix(h, topDiameterMetres);
            h = Mix(h, buttDiameterMetres);
            h = h * 31 + shaftSides;
            h = Mix(h, crossArmDepthMetres);
            h = Mix(h, crossArmHeightMetres);
            h = Mix(h, braceLengthMetres);
            h = Mix(h, braceThicknessMetres);
            h = Mix(h, armBelowTopMetres);
            h = Mix(h, insulatorDiameterMetres);
            h = Mix(h, insulatorHeightMetres);
            h = h * 31 + insulatorSides;
            h = Mix(h, transformerDiameterMetres);
            h = Mix(h, transformerHeightMetres);
            h = Mix(h, transformerBelowArmMetres);
            h = Mix(h, cutoutHeightMetres);
            h = Mix(h, cutoutDiameterMetres);
            h = Mix(h, arresterHeightMetres);
            h = Mix(h, arresterDiameterMetres);
            h = Mix(h, neutralBelowArmMetres);
            h = Mix(h, secondaryBelowNeutralMetres);
            h = Mix(h, rackWidthMetres);
            h = Mix(h, groundWireDiameterMetres);
            h = Mix(h, guyDiameterMetres);
            h = h * 31 + wireSides;
            return h;
        }

        private static int Mix(int h, float value)
        {
            return h * 31 + (int)Mathf.Round(value * 10000f);
        }
    }


    /// <summary> Which fittings a pole carries. Flags rather than an enum of pole types, because a real run mixes
    /// them freely: the pole at a road crossing gets an arm and nothing else, the one feeding a house gets a
    /// transformer, a cut-out and an arrester, and the one at a turn gets guys. An enum would need a member per
    /// combination and would still be short of the next one. </summary>
    [Flags]
    public enum PoleFittings
    {
        None = 0,

        /// <summary> A timber cross-arm with pin insulators. Without it the phases sit on a pole-top bracket, which
        /// is what a single-phase tap actually uses. </summary>
        CrossArm = 1 << 0,
        NeutralRack = 1 << 1,
        SecondaryRack = 1 << 2,
        Transformer = 1 << 3,
        Cutout = 1 << 4,
        Arrester = 1 << 5,
        GroundWire = 1 << 6,

        /// <summary> A communication strand -- telephone or cable -- carried on the same pole, in the joint-use
        /// space NESC Rule 238 keeps below the supply conductors. It is a bit rather than part of
        /// <see cref="Line"/> because whether a plant shares the pole is an authored fact about the street and not
        /// a property of a distribution pole. </summary>
        Communication = 1 << 7,

        /// <summary> Standard three-phase pole on a distribution run: arm, neutral, ground wire down the shaft.
        /// </summary>
        Line = CrossArm | NeutralRack | GroundWire,

        /// <summary> A transformer pole: the line plus the can and its protection, and the secondary rack the
        /// service drops leave from. </summary>
        Transformed = Line | SecondaryRack | Transformer | Cutout | Arrester,
    }


    /// <summary> The parameter set one pole mesh is built from, and the cache key it is stored under.
    ///
    /// A struct with explicit equality rather than a string built per lookup: a run of 25 poles per rural kilometre
    /// asks the cache 25 times per kilometre of streamed road, and D-15 asserts no per-frame allocation. The fields
    /// are exactly what the geometry reads -- nothing decorative, or two poles that render identically would be
    /// cached twice and the instancing that makes the whole family affordable would quietly stop working.
    /// </summary>
    public struct PoleMeshKey : IEquatable<PoleMeshKey>
    {
        /// <summary> <see cref="PoleGeometryClass.ShapeHash"/>, so an edit to the timber or hardware dimensions
        /// produces different keys instead of stale meshes. </summary>
        public int shapeHash;

        public PoleFittings fittings;

        /// <summary> How much of the pole stands above ground: its length less the ANSI O5.1 setting depth. Held
        /// rather than derived so a pole shortened for a low clearance is a different mesh and not a scaled one --
        /// scaling a pole thickens it. </summary>
        public float exposedLengthMetres;

        public float settingDepthMetres;
        public float crossArmLengthMetres;
        public float phaseSpacingMetres;

        /// <summary> NESC Rule 238's supply-to-communication separation, taken from the electrical class. It is in
        /// the key because the clamp is drawn at that height: two poles with different separations are two different
        /// poles, and sharing one mesh between them would hang one strand at the other's height. </summary>
        public float communicationSeparationMetres;
        public int phaseCount;
        public int guyCount;

        /// <summary> Quantised to a millimetre before comparison, for the same reason the shape hash is: two poles
        /// the author cannot tell apart must not be two meshes. </summary>
        public bool Equals(PoleMeshKey other)
        {
            return shapeHash == other.shapeHash
                && fittings == other.fittings
                && phaseCount == other.phaseCount
                && guyCount == other.guyCount
                && Same(exposedLengthMetres, other.exposedLengthMetres)
                && Same(settingDepthMetres, other.settingDepthMetres)
                && Same(crossArmLengthMetres, other.crossArmLengthMetres)
                && Same(phaseSpacingMetres, other.phaseSpacingMetres)
                && Same(communicationSeparationMetres, other.communicationSeparationMetres);
        }

        public override bool Equals(object obj)
        {
            return obj is PoleMeshKey && Equals((PoleMeshKey)obj);
        }

        // determinism-exempt
        public override int GetHashCode()
        {
            int h = shapeHash;
            h = h * 31 + (int)fittings;
            h = h * 31 + phaseCount;
            h = h * 31 + guyCount;
            h = h * 31 + Quantise(exposedLengthMetres);
            h = h * 31 + Quantise(settingDepthMetres);
            h = h * 31 + Quantise(crossArmLengthMetres);
            h = h * 31 + Quantise(phaseSpacingMetres);
            h = h * 31 + Quantise(communicationSeparationMetres);
            return h;
        }

        private static int Quantise(float value)
        {
            return (int)Mathf.Round(value * 1000f);
        }

        private static bool Same(float a, float b)
        {
            return Quantise(a) == Quantise(b);
        }

        /// <summary> The key for one pole on a run: the geometry class's shape, the fittings asked for, and the
        /// setting depth the ANSI rule gives for this pole's length. </summary>
        public static PoleMeshKey For(PoleGeometryClass shape, DistributionPoleClass electrical,
            PoleFittings fittings, int phaseCount, int guyCount)
        {
            PoleGeometryClass s = shape != null ? shape : PoleGeometryClass.Default();
            DistributionPoleClass e = electrical != null ? electrical : DistributionPoleClass.Default();
            PoleMeshKey key = new PoleMeshKey();
            key.shapeHash = s.ShapeHash();
            key.fittings = fittings;
            key.settingDepthMetres = (float)e.SettingDepthMetres(s.poleLengthMetres);
            key.exposedLengthMetres = s.poleLengthMetres - key.settingDepthMetres;
            key.crossArmLengthMetres = e.crossArmLengthMetres;
            key.phaseSpacingMetres = e.phaseSpacingMetres;
            key.communicationSeparationMetres = e.communicationSeparationMetres;
            key.phaseCount = phaseCount < 0 ? 0 : phaseCount;
            key.guyCount = guyCount < 0 ? 0 : guyCount;
            return key;
        }
    }

    /// <summary> B-27 / D-2. Where the fittings sit on a pole of a given key, in metres above the BUTT, and the
    /// lateral offsets of the phases across its arm.
    ///
    /// This is the pole's ARITHMETIC, and it lives here rather than with the mesh because two callers need it and
    /// only one of them draws. The run that strings the conductors needs the same numbers the timber was drawn with:
    /// an attachment height computed separately from the arm it is drawn on is how a wire ends up half a metre above
    /// its own insulator. The mesh builder (<c>PoleMeshFactory</c>, in the editor assembly, because a baked run has
    /// no reason to carry a mesh builder into a player) asks this same struct and puts an insulator at every level it
    /// reports.
    ///
    /// Everything here is a pure function of the key and the shape class, which is what lets D-16 assert a pole's
    /// attachment heights and its phase spacing without a scene, a mesh or an editor. </summary>
    public struct PoleAttachments
    {
        /// <summary> Ground line: the setting depth above the butt. </summary>
        public float groundY;

        /// <summary> The pole's tip. </summary>
        public float topY;

        /// <summary> The cross-arm's centre line, or the pole-top bracket when there is no arm. </summary>
        public float armY;

        public float neutralY;
        public float secondaryY;

        /// <summary> Where a communication strand attaches, when the pole carries one: NESC Rule 238's separation
        /// below the LOWEST supply attachment on this pole, which is the secondary where a rack is fitted and the
        /// neutral where it is not. Reported whether or not the pole carries communication, because a run deciding
        /// whether it can carry it needs the height the strand WOULD take. </summary>
        public float communicationY;
        public float transformerTopY;

        /// <summary> Lateral offsets of the phase attachments from the pole centre, along the arm. One entry per
        /// phase, in the order they are drawn. </summary>
        public float[] phaseOffsets;

        /// <summary> The attachment levels for a key, from the arithmetic the mesh is assembled with. </summary>
        public static PoleAttachments For(PoleMeshKey key, PoleGeometryClass shape)
        {
            PoleGeometryClass s = shape != null ? shape : PoleGeometryClass.Default();
            PoleAttachments a = new PoleAttachments();
            a.groundY = key.settingDepthMetres;
            a.topY = key.settingDepthMetres + key.exposedLengthMetres;
            a.armY = a.topY - s.armBelowTopMetres;
            a.neutralY = a.armY - s.neutralBelowArmMetres;
            a.secondaryY = a.neutralY - s.secondaryBelowNeutralMetres;
            a.communicationY = ((key.fittings & PoleFittings.SecondaryRack) != 0 ? a.secondaryY : a.neutralY)
                               - key.communicationSeparationMetres;
            a.transformerTopY = a.armY - s.transformerBelowArmMetres;
            a.phaseOffsets = PhaseOffsets(key);
            return a;
        }

        /// <summary> Where the phases sit across the arm: centred on the pole at the class pin spacing, so three
        /// phases at 0.76 m land at -0.76, 0 and +0.76 and the arm's 2.44 m is long enough for them with the end
        /// margin a real arm has. An even count straddles the pole rather than putting a pin through it. Offsets that
        /// would run past the arm are pulled in to its end, which is a visible crowding rather than a pin hanging in
        /// the air -- and the pole line's own inspector reports the crowding, because an arm too short for its
        /// phases is an authoring mistake and not something to hide. </summary>
        private static float[] PhaseOffsets(PoleMeshKey key)
        {
            int n = key.phaseCount;
            if (n <= 0) { return new float[0]; }
            float[] offsets = new float[n];
            float spacing = key.phaseSpacingMetres > 0f ? key.phaseSpacingMetres : 0.76f;
            float limit = key.crossArmLengthMetres * 0.5f - spacing * 0.25f;
            if (limit < 0f) { limit = 0f; }
            float first = -spacing * (n - 1) * 0.5f;
            for (int i = 0; i < n; i++)
            {
                float d = first + spacing * i;
                if (d < -limit) { d = -limit; }
                if (d > limit) { d = limit; }
                offsets[i] = d;
            }
            return offsets;
        }
    }
}
