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
    /// <summary> The deck family of a bridge type. Which one a type uses is authored, never inferred. </summary>
    public enum BridgeDeckType
    {
        Beam = 0,
        Girder = 1,
        Arch = 2,
        Suspension = 3,
        CableStayed = 4
    }


    /// <summary> How a bridge type places its supports. This is the ONLY thing that needs code beyond the existing
    /// evenly-spaced flyover: an arch lands only on its abutments, a suspension bridge stands on two towers with a
    /// catenary main cable (RM-23's sag maths reused, NOT a second solver) and regular hangers, and a cable-stayed
    /// bridge's supports are a function of tower position rather than span count. </summary>
    public enum SupportPlacementRule
    {
        EvenlySpaced = 0,           // beam / girder — the existing flyover model
        AbutmentsOnly = 1,          // arch — no intermediate piers
        TowersWithCatenaryHangers = 2, // suspension — cable is RM-23's catenary
        TowersWithFanStays = 3      // cable-stayed — stays fan from tower(s)
    }


    /// <summary> RM-33's per-bridge-type SPEC SHEET, filled in at authoring time. A type whose sheet is not filled
    /// in (<see cref="specified"/> = false) is REFUSED with a message, never built with defaults. All ranges and
    /// placement rules are AUTHORED per type; a new deck / railing / pier MODEL is content (a prefab) and needs no
    /// code. Prefab role names are carried as string keys (name-based, no scene references here). </summary>
    [Serializable]
    public struct BridgeTypeSpec
    {
        public bool specified;
        public BridgeDeckType deckType;
        public SupportPlacementRule placementRule;
        public float minSpanMetres;
        public float maxSpanMetres;
        public float maxPierSpacingMetres;   // used only by EvenlySpaced
        public float[] towerFractions;       // used only by the tower rules; positions along the span in [0,1]
        public bool supportsMayStandInWater;

        /// <summary> An unspecified sheet: refused until an author fills it in. </summary>
        public static BridgeTypeSpec Unspecified()
        {
            return new BridgeTypeSpec { specified = false };
        }

        /// <summary> AUTHORED beam/girder type: evenly spaced piers, ~30 m typical max simple-span spacing. </summary>
        public static BridgeTypeSpec Beam()
        {
            return new BridgeTypeSpec
            {
                specified = true,
                deckType = BridgeDeckType.Beam,
                placementRule = SupportPlacementRule.EvenlySpaced,
                minSpanMetres = 10f,
                maxSpanMetres = 300f,
                maxPierSpacingMetres = 30f,
                towerFractions = null,
                supportsMayStandInWater = true
            };
        }

        /// <summary> AUTHORED arch type: supports only at the springing abutments, none in between. </summary>
        public static BridgeTypeSpec Arch()
        {
            return new BridgeTypeSpec
            {
                specified = true,
                deckType = BridgeDeckType.Arch,
                placementRule = SupportPlacementRule.AbutmentsOnly,
                minSpanMetres = 20f,
                maxSpanMetres = 200f,
                maxPierSpacingMetres = 0f,
                towerFractions = null,
                supportsMayStandInWater = false
            };
        }

        /// <summary> AUTHORED suspension type: two towers at the quarter points, catenary main cable, hangers. </summary>
        public static BridgeTypeSpec Suspension()
        {
            return new BridgeTypeSpec
            {
                specified = true,
                deckType = BridgeDeckType.Suspension,
                placementRule = SupportPlacementRule.TowersWithCatenaryHangers,
                minSpanMetres = 200f,
                maxSpanMetres = 2000f,
                maxPierSpacingMetres = 0f,
                towerFractions = new float[] { 0.25f, 0.75f },
                supportsMayStandInWater = true
            };
        }
    }

    /// <summary> RM-33's verifiable core: the pure part of the bridge-type system that can be checked without a
    /// scene -- validating a spec sheet (refuse an unfilled one with a message, refuse an inverted span range),
    /// deciding support STATIONS from the type's placement rule (evenly spaced vs. abutments-only vs. tower-based),
    /// the water rule, and the pier-vs-network-below conflict resolution (shift within the allowed range, then
    /// flag). Sampling terrain at each support, swapping pier prefabs, the catenary cable + hangers (RM-23) and
    /// the deck mesh are the deferred scene half. </summary>
    public static class BridgeTypes
    {
        /// <summary> The outcome of validating a spec sheet before anything is built. </summary>
        public struct SpecValidation
        {
            public bool accepted;
            public string message;
        }

        /// <summary> Validate a bridge-type spec: an unfilled sheet is refused with a message (never built with
        /// defaults), an inverted or non-positive span range is refused, and an evenly-spaced type with no pier
        /// spacing is refused. Otherwise accepted. </summary>
        public static SpecValidation ValidateSpec(BridgeTypeSpec spec)
        {
            SpecValidation v = new SpecValidation();
            if (!spec.specified)
            {
                v.accepted = false;
                v.message = "bridge type spec not filled in: refuse rather than build with defaults";
                return v;
            }
            if (spec.minSpanMetres <= 0f || spec.maxSpanMetres < spec.minSpanMetres)
            {
                v.accepted = false;
                v.message = "bridge type spec has an invalid span range (min <= 0 or max < min)";
                return v;
            }
            if (spec.placementRule == SupportPlacementRule.EvenlySpaced && spec.maxPierSpacingMetres <= 0f)
            {
                v.accepted = false;
                v.message = "evenly-spaced bridge type has no pier spacing";
                return v;
            }
            v.accepted = true;
            v.message = "ok";
            return v;
        }

        /// <summary> Whether a span sits inside the type's authored min/max span. </summary>
        public static bool SpanWithinType(double spanMetres, BridgeTypeSpec spec)
        {
            return spanMetres >= spec.minSpanMetres - 1e-9 && spanMetres <= spec.maxSpanMetres + 1e-9;
        }

        /// <summary> The absolute support stations (metres from the start of the span) the type requires. Abutments
        /// at 0 and span always exist; the rule decides what falls between: evenly-spaced fills intermediate piers
        /// no farther apart than the max pier spacing; abutments-only adds none; the tower rules place a support at
        /// each authored tower fraction. Stations are sorted and de-duplicated. </summary>
        public static List<double> SupportStationsMetres(double spanMetres, BridgeTypeSpec spec)
        {
            List<double> stations = new List<double>();
            if (spanMetres <= 0.0) { return stations; }
            stations.Add(0.0);
            stations.Add(spanMetres);

            switch (spec.placementRule)
            {
                case SupportPlacementRule.EvenlySpaced:
                    if (spec.maxPierSpacingMetres > 0f)
                    {
                        int bays = (int)Math.Ceiling(spanMetres / spec.maxPierSpacingMetres);
                        if (bays < 1) { bays = 1; }
                        double step = spanMetres / bays;
                        for (int i = 1; i < bays; i++) { stations.Add(i * step); }
                    }
                    break;
                case SupportPlacementRule.AbutmentsOnly:
                    break; // no intermediate supports
                case SupportPlacementRule.TowersWithCatenaryHangers:
                case SupportPlacementRule.TowersWithFanStays:
                    if (spec.towerFractions != null)
                    {
                        for (int i = 0; i < spec.towerFractions.Length; i++)
                        {
                            stations.Add(spec.towerFractions[i] * spanMetres);
                        }
                    }
                    break;
            }

            stations.Sort();
            for (int i = stations.Count - 1; i > 0; i--)
            {
                if (Math.Abs(stations[i] - stations[i - 1]) < 1e-6) { stations.RemoveAt(i); }
            }
            return stations;
        }

        /// <summary> The number of intermediate piers (supports that are not the two abutments). </summary>
        public static int IntermediatePierCount(double spanMetres, BridgeTypeSpec spec)
        {
            int total = SupportStationsMetres(spanMetres, spec).Count;
            return Math.Max(0, total - 2);
        }

        /// <summary> Whether a support may stand where it lands: on land always; in water only if the type allows
        /// it (otherwise it needs a foundation treatment the type has not authored, so it is refused). </summary>
        public static bool SupportAllowedAt(bool landsInWater, BridgeTypeSpec spec)
        {
            return !landsInWater || spec.supportsMayStandInWater;
        }

        /// <summary> The outcome of resolving a support that would land on the network below. </summary>
        public struct SupportConflictResolve
        {
            public bool resolved;
            public double stationMetres;
            public bool flagged;
        }

        /// <summary> Resolve a support that would drop onto the road/network below: if the desired station is clear
        /// of the conflict zone it is kept; otherwise it is shifted (toward the nearer clear side) by up to the
        /// allowed amount, and if that still cannot clear the zone the support is FLAGGED rather than dropped into
        /// the road. </summary>
        public static SupportConflictResolve ResolveSupportConflict(double desiredStationMetres,
            double conflictCentreMetres, double conflictHalfWidthMetres, double allowedShiftMetres)
        {
            SupportConflictResolve r = new SupportConflictResolve();
            r.stationMetres = desiredStationMetres;
            double delta = desiredStationMetres - conflictCentreMetres;
            if (Math.Abs(delta) >= conflictHalfWidthMetres - 1e-9)
            {
                r.resolved = true; // already clear
                r.flagged = false;
                return r;
            }
            // Shift to the nearer edge of the conflict zone.
            double target = delta >= 0.0
                ? conflictCentreMetres + conflictHalfWidthMetres
                : conflictCentreMetres - conflictHalfWidthMetres;
            double needed = Math.Abs(target - desiredStationMetres);
            if (needed <= allowedShiftMetres + 1e-9)
            {
                r.stationMetres = target;
                r.resolved = true;
                r.flagged = false;
            }
            else
            {
                r.resolved = false;
                r.flagged = true; // cannot clear within the allowed shift
            }
            return r;
        }
    }
}
