// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> A designed part that joins two run types where they meet. The pair is ORDERED -- a guardrail
    /// approaching a parapet is not the same connection as a parapet running out to a guardrail -- so the catalog
    /// carries a row per direction, and the part is drawn from a real content category (RM-55), never conjured. A
    /// pair with no row is refused by name, which is the whole point: a bare butt joint between two stiffnesses is
    /// a defect, not a default. </summary>
    [Serializable]
    public struct RunTransition
    {
        public int fromTypeId;
        public int toTypeId;
        public string partCategory;
        public float lengthMetres;

        public RunTransition(int fromTypeId, int toTypeId, string partCategory, float lengthMetres)
        {
            this.fromTypeId = fromTypeId;
            this.toTypeId = toTypeId;
            this.partCategory = partCategory;
            this.lengthMetres = lengthMetres;
        }
    }


    /// <summary> The ordered pairs of run types that HAVE a transition part, and the part for each. Built with
    /// object initialisers like RM-59's road-class list. A lookup miss is not an error the caller may ignore: it is
    /// the signal to refuse the join and name the pair, so no two run types are ever spliced without a transition
    /// the author actually authored. </summary>
    [Serializable]
    public sealed class RunTransitionCatalog
    {
        public List<RunTransition> transitions = new List<RunTransition>();

        /// <summary> The transition part for an ordered pair, or false when none is defined. The scan is linear and
        /// ordered, so the first matching direction wins and the reverse direction is a separate row. </summary>
        public bool TryGet(int fromTypeId, int toTypeId, out RunTransition transition)
        {
            for (int i = 0; i < transitions.Count; i++)
            {
                if (transitions[i].fromTypeId == fromTypeId && transitions[i].toTypeId == toTypeId)
                {
                    transition = transitions[i];
                    return true;
                }
            }
            transition = new RunTransition();
            return false;
        }

        /// <summary> The refusal sentence for an undefined pair, naming both types, for a report or a gate. </summary>
        public static string RefusalFor(EdgeRunType from, EdgeRunType to)
        {
            string a = from != null ? from.displayName : "run";
            string b = to != null ? to.displayName : "run";
            return "a " + a + " run meets a " + b + " run with no transition part defined between them. A bare joint "
                + "between two run types with different stiffnesses is a defect; author a transition for this "
                + "ordered pair, or do not place the two runs end to end.";
        }

        /// <summary> The authored default: the approach-rail stiffness transition between a W-beam guardrail and a
        /// bridge parapet, both directions. 11.43 m is three 3.81 m rail sections, the standard length over which a
        /// flexible rail is stiffened to a rigid parapet. Every other pair is deliberately absent, so a fence run
        /// abutting a guardrail is refused until someone authors that joint. </summary>
        public static RunTransitionCatalog Default()
        {
            RunTransitionCatalog catalog = new RunTransitionCatalog();
            catalog.transitions = new List<RunTransition>
            {
                new RunTransition(0, 3, "Barrier/Transition/GuardrailToParapet", 11.43f),
                new RunTransition(3, 0, "Barrier/Transition/ParapetToGuardrail", 11.43f)
            };
            return catalog;
        }

        /// <summary> Formats a length the way the rest of the tool does, for report parity. </summary>
        public static string Metres(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + " m";
        }
    }
}
