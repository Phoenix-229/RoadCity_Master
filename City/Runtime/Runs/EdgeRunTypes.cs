// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> The family a run belongs to. The family, not a per-feature flag, decides which set of end
    /// treatments is legal and where the end lengths come from: a barrier ends through RM-60's terminal ladder
    /// (bury / flare / cushion), a fence ends at a post, a kerb ends at a dropped-kerb return. One enum so a run
    /// cannot be half barrier and half fence, and so six features cannot each invent their own end rule. </summary>
    public enum EdgeRunFamily
    {
        Barrier = 0,
        Fence = 1,
        Kerb = 2
    }


    /// <summary> Where the remainder of a run whose length is not a whole number of parts goes. Scaling a part is
    /// deliberately NOT a member: a stretched post or rail reads as a bug at any distance, so the model cannot
    /// express it. </summary>
    public enum RunRemainderPolicy
    {
        /// <summary> Lengthen the two end treatments within their authored adjustment range. Refuses if the
        /// remainder is larger than the two ends can take between them. </summary>
        AbsorbInEnds = 0,

        /// <summary> Change the per-part spacing by a small fraction, within a stated tolerance, so a whole number
        /// of parts fills the body exactly. </summary>
        DistributeSpacing = 1,

        /// <summary> Place whole parts, then one designated short closing part carrying the remainder. </summary>
        ClosingPart = 2
    }


    /// <summary> The kind of end a run terminates in. Barrier kinds mirror RM-60's <see cref="TerminalTreatment"/>
    /// one for one -- the choice is made by RM-60's code, not re-decided here. Fence and kerb kinds are their own.
    /// <see cref="None"/> means "not resolved": a segment carrying it on either end is refused and never placed,
    /// which is the property that stops a bare cut rail end or a floating fence post. </summary>
    public enum RunEndKind
    {
        None = 0,
        BarrierFlared = 1,
        BarrierBuried = 2,
        BarrierCrashCushion = 3,
        FenceTerminalPost = 10,
        FenceStrainerPost = 11,
        FenceGate = 12,
        FenceWallReturn = 13,
        KerbDropped = 20,
        KerbReturn = 21
    }


    /// <summary> What interrupts a run. Each opening splits one run into two fully terminated runs rather than
    /// leaving a hole with suppressed parts, because a suppressed part is exactly the open-ended geometry this task
    /// removes. A driveway or crossing is short; a bridge or junction is longer, and its own barrier or parapet
    /// takes over across it. </summary>
    public enum RunOpeningKind
    {
        Driveway = 0,
        SideRoad = 1,
        Crossing = 2,
        BusBay = 3,
        Bridge = 4,
        Junction = 5
    }


    /// <summary> One interruption of a run, as a station interval measured along the run. Openings are given in run
    /// stations, not world space, so the segmenter never has to project a point back onto the curve. </summary>
    public struct RunOpening
    {
        public float startStationMetres;
        public float endStationMetres;
        public RunOpeningKind kind;

        public RunOpening(float startStationMetres, float endStationMetres, RunOpeningKind kind)
        {
            this.startStationMetres = startStationMetres;
            this.endStationMetres = endStationMetres;
            this.kind = kind;
        }

        public float LengthMetres { get { return endStationMetres - startStationMetres; } }
    }


    /// <summary> One end treatment a fence or kerb type is allowed to terminate in, with the length it consumes
    /// along the run. Barrier types leave their set empty -- their end lengths come from RM-60's flare / cushion /
    /// buried arithmetic, not from a typed-in number. </summary>
    [Serializable]
    public struct RunEndOption
    {
        public RunEndKind kind;
        public float lengthMetres;

        public RunEndOption(RunEndKind kind, float lengthMetres)
        {
            this.kind = kind;
            this.lengthMetres = lengthMetres;
        }
    }
    /// <summary> An authored run type. Follows RM-59's <see cref="RoadClass"/> pattern: a plain serialisable class
    /// carried in a catalog, built with object initialisers, never a ScriptableObject of its own. It holds only
    /// what the solver cannot invent -- the real part length, how a remainder is absorbed, and (for a fence or kerb)
    /// which ends are legal and how long each is. </summary>
    [Serializable]
    public sealed class EdgeRunType
    {
        public int id;
        public string displayName = "";
        public EdgeRunFamily family = EdgeRunFamily.Barrier;

        /// <summary> The real, fixed length of one body part, in metres: a W-beam rail section, a fence panel, a
        /// kerb unit. A run of arbitrary length cannot be a whole number of these, which is why a remainder policy
        /// exists rather than a stretch. </summary>
        public float bodyPartLengthMetres = 3.81f;

        public RunRemainderPolicy remainderPolicy = RunRemainderPolicy.ClosingPart;

        /// <summary> DistributeSpacing only: the largest fraction the per-part spacing may be squeezed or stretched
        /// to swallow the remainder. Past a few percent the spacing change itself reads on a post line. </summary>
        public float spacingTolerance = 0.05f;

        /// <summary> ClosingPart only: the shortest a closing part may be. A remainder below this reads as an offcut,
        /// so one full part is dropped and folded into the closing part instead of placing a sliver. </summary>
        public float closingPartMinLengthMetres = 1.0f;

        /// <summary> AbsorbInEnds only: how much EACH end treatment may be lengthened to take up remainder. </summary>
        public float endAdjustmentRangeMetres = 0.5f;

        /// <summary> The fence / kerb ends this type may terminate in, most preferred first. Barrier types leave
        /// this empty: their ends are chosen by RM-60. An empty set on a fence or kerb type is a fail-closed
        /// refusal, never a licence to end mid-panel. </summary>
        public List<RunEndOption> allowedEnds = new List<RunEndOption>();

        /// <summary> The shortest end option, or 0 for a barrier (whose end length is site-driven). </summary>
        public float ShortestEndLengthMetres()
        {
            float shortest = float.MaxValue;
            for (int i = 0; i < allowedEnds.Count; i++)
            {
                if (allowedEnds[i].lengthMetres < shortest) { shortest = allowedEnds[i].lengthMetres; }
            }
            return shortest == float.MaxValue ? 0f : shortest;
        }
        /// <summary> A W-beam guardrail. Rail element 3.81 m (12 ft 6 in) and post spacing 1.905 m are the AASHTO
        /// standard sizes; the closing part is not allowed below one post bay, and the ends are RM-60's, so this
        /// type carries no end set of its own. </summary>
        public static EdgeRunType Guardrail()
        {
            return new EdgeRunType
            {
                id = 0,
                displayName = "W-beam guardrail",
                family = EdgeRunFamily.Barrier,
                bodyPartLengthMetres = 3.81f,
                remainderPolicy = RunRemainderPolicy.ClosingPart,
                closingPartMinLengthMetres = 1.905f,
                endAdjustmentRangeMetres = 0.5f
            };
        }

        /// <summary> A panel fence. 2.4 m is a common timber panel; a fence never ends mid-panel, so its ends are a
        /// strainer post (preferred, since a fence end takes the wire tension), a plain terminal post, a return into
        /// a wall, or a gate leaf (3 m). AUTHORED part length. </summary>
        public static EdgeRunType Fence()
        {
            EdgeRunType type = new EdgeRunType
            {
                id = 1,
                displayName = "Panel fence",
                family = EdgeRunFamily.Fence,
                bodyPartLengthMetres = 2.4f,
                remainderPolicy = RunRemainderPolicy.DistributeSpacing,
                spacingTolerance = 0.06f,
                closingPartMinLengthMetres = 0.6f,
                endAdjustmentRangeMetres = 0f
            };
            type.allowedEnds = new List<RunEndOption>
            {
                new RunEndOption(RunEndKind.FenceStrainerPost, 0.3f),
                new RunEndOption(RunEndKind.FenceTerminalPost, 0.15f),
                new RunEndOption(RunEndKind.FenceWallReturn, 0.3f),
                new RunEndOption(RunEndKind.FenceGate, 3.0f)
            };
            return type;
        }

        /// <summary> A precast kerb line. 0.914 m (3 ft) is the standard kerb unit; a kerb line ends in a radius
        /// return (preferred) or a dropped-kerb transition at a crossing. The dropped-kerb length is three units,
        /// the run-in plus the flush length plus the run-out. AUTHORED assembly of standard units. </summary>
        public static EdgeRunType Kerb()
        {
            EdgeRunType type = new EdgeRunType
            {
                id = 2,
                displayName = "Kerb line",
                family = EdgeRunFamily.Kerb,
                bodyPartLengthMetres = 0.914f,
                remainderPolicy = RunRemainderPolicy.DistributeSpacing,
                spacingTolerance = 0.1f,
                closingPartMinLengthMetres = 0.3f,
                endAdjustmentRangeMetres = 0f
            };
            type.allowedEnds = new List<RunEndOption>
            {
                new RunEndOption(RunEndKind.KerbReturn, 0.914f),
                new RunEndOption(RunEndKind.KerbDropped, 2.742f)
            };
            return type;
        }

        /// <summary> A bridge parapet: a barrier, so its free ends come from RM-60's ladder, but a distinct type so
        /// the guardrail that approaches it can require the stiffness-transition part between them rather than butt
        /// against it. Parapet units are cast in 3 m lengths. AUTHORED part length. </summary>
        public static EdgeRunType BridgeParapet()
        {
            return new EdgeRunType
            {
                id = 3,
                displayName = "Bridge parapet",
                family = EdgeRunFamily.Barrier,
                bodyPartLengthMetres = 3.0f,
                remainderPolicy = RunRemainderPolicy.ClosingPart,
                closingPartMinLengthMetres = 1.0f,
                endAdjustmentRangeMetres = 0.25f
            };
        }
    }
}
