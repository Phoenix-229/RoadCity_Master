// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Globalization;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-10. Which class of vessel a quay admits, decided by DEPTH and nothing else. A port is not a
    /// building placed near water; it is a navigable depth at the quay line, and the depth decides the vessel -- so this
    /// enum is ordered by the draught each class needs, and a shallow water body honestly admits only the small end of
    /// it. <see cref="None"/> is the answer for water too shallow for even an inland barge: it is not a port, and it is
    /// told so with the number. </summary>
    public enum VesselClass
    {
        None = 0,
        InlandBarge = 1,
        Handysize = 2,
        Panamax = 3,
        Suezmax = 4
    }


    /// <summary> B-27 / D-10. Which member of the port family a site was tested for. A water port is decided by depth; a
    /// dry port and a shipment yard are the same family WITHOUT water -- they need rail, and a dry port a trunk road too
    /// -- so they are answered by the presence of those, never by a draught they do not have. </summary>
    public enum PortKind
    {
        WaterPort = 0,
        DryPort = 1,
        ShipmentYard = 2
    }


    /// <summary> B-27 / D-10. THE ANSWER, for one site, to "what port can stand here": which kind was asked for, whether
    /// it can, and -- for a water port -- which vessel class the depth admits, carried beside the depth it was decided
    /// from so an author who expected a container terminal and got a barge quay can see it was the 3 m lake and not the
    /// tool that decided it.
    ///
    /// It carries a REFUSAL rather than a bool, on the folder's doctrine: a lake too shallow for a barge and a site with
    /// no rail are different refusals with different fixes, and a bool would hide which. </summary>
    [Serializable]
    public struct PortSitingReport
    {
        /// <summary> Whether the resolver could answer. False means <see cref="refusal"/> is set and the figures below
        /// are meaningless -- NOT that the site is a small port. </summary>
        public bool resolved;

        /// <summary> Why it could not answer, empty when it could. </summary>
        public string refusal;

        /// <summary> Which member of the family this answer is about. </summary>
        public PortKind kind;

        /// <summary> The vessel class the quay admits, for a water port; <see cref="VesselClass.None"/> for a dry port
        /// or shipment yard, which admit no vessel by water. </summary>
        public VesselClass admittedClass;

        /// <summary> The navigable depth read at the quay line, in metres, for a water port; zero otherwise. Kept even
        /// when it admits only a barge, because the depth is the whole of the decision. </summary>
        public double navigableDepthMetres;

        /// <summary> The depth the admitted class needs, in metres -- so the margin above it is visible. Zero for a dry
        /// port or shipment yard. </summary>
        public double requiredDepthMetres;

        /// <summary> Whether a rail connection was present, for the families that need one. </summary>
        public bool railPresent;

        /// <summary> Whether a trunk road was present, for the dry port that needs one. </summary>
        public bool trunkRoadPresent;

        /// <summary> One line, in the register's voice: the kind, the decision, and the figure it rests on. </summary>
        public string Describe()
        {
            if (!resolved)
            {
                return "port: cannot answer -- " + (string.IsNullOrEmpty(refusal) ? "no reason given" : refusal);
            }
            switch (kind)
            {
                case PortKind.DryPort:
                    return "dry port: sited -- rail and a trunk road both reach the settlement edge.";
                case PortKind.ShipmentYard:
                    return "shipment yard: sited -- a rail spur reaches it.";
                default:
                    return "water port: admits " + PortSiting.NameOf(admittedClass) + " at "
                           + Depth(navigableDepthMetres) + " (needs " + Depth(requiredDepthMetres) + ").";
            }
        }

        private static string Depth(double metres)
        {
            return metres.ToString("0.##", CultureInfo.InvariantCulture) + " m";
        }
    }


    /// <summary> B-27 / D-10. Turns a site's physical figures into a <see cref="PortSitingReport"/>: what class of
    /// vessel a depth admits, and whether a dry port or shipment yard has the rail and road it needs. A static resolver
    /// on the folder's doctrine -- it computes and refuses, it never mutates a scene, never touches an asset and never
    /// samples the terrain. The caller reads the depth off the water body (through the provider already in use, no new
    /// provider member -- R-5) and hands it here; this decides what it means.
    ///
    /// WHY DEPTH AND NOT A TYPE FIELD. The author asked that a port be *"told"* what its depth supports rather than
    /// choosing a terminal that the water cannot float. So the vessel class is a pure step function of the navigable
    /// depth against published draughts, and a water body 3 m deep gets a barge quay and the reason -- never a container
    /// terminal it cannot serve. The draughts are real and are the figures the task names, not a ladder invented here.
    ///
    /// WHY WATER IS NEVER STOOD IN. Every structure this resolves stands on the land side of the shore: a quay faces the
    /// water, it does not sit in it. The freeboard rule is B-15's <see cref="BuildableLandTest.WaterRejects"/>, applied
    /// by the builder when it places the quay; this resolver decides the vessel, not the pad, so it does not repeat that
    /// test -- a second freeboard rule here would be free to drift from the one every other structure uses. </summary>
    public static class PortSiting
    {
        /// <summary> The navigable depth, in metres, an inland barge needs at the quay. Below this a water body is not a
        /// port at all. Real: a loaded European barge sits about 2.5 m. </summary>
        public const double InlandBargeDraughtMetres = 2.5;

        /// <summary> The navigable depth a Handysize bulk carrier needs -- the smallest sea-going class, ~10 m. </summary>
        public const double HandysizeDraughtMetres = 10.0;

        /// <summary> The navigable depth a Panamax vessel needs: 12.04 m, the old Panama Canal draught limit and the
        /// figure the task names. </summary>
        public const double PanamaxDraughtMetres = 12.04;

        /// <summary> The navigable depth a Suezmax vessel needs: ~20.1 m, the Suez Canal draught limit. </summary>
        public const double SuezmaxDraughtMetres = 20.1;

        /// <summary> The largest vessel class a navigable depth admits: the deepest class whose draught the depth meets,
        /// or <see cref="VesselClass.None"/> when the depth is below even an inland barge. A step function, because a
        /// quay either floats a class or it does not -- there is no fractional vessel. </summary>
        public static VesselClass AdmittedByDepth(double navigableDepthMetres)
        {
            if (navigableDepthMetres >= SuezmaxDraughtMetres) { return VesselClass.Suezmax; }
            if (navigableDepthMetres >= PanamaxDraughtMetres) { return VesselClass.Panamax; }
            if (navigableDepthMetres >= HandysizeDraughtMetres) { return VesselClass.Handysize; }
            if (navigableDepthMetres >= InlandBargeDraughtMetres) { return VesselClass.InlandBarge; }
            return VesselClass.None;
        }

        /// <summary> The draught, in metres, a vessel class needs -- the depth its admission was tested against. </summary>
        public static double DraughtOf(VesselClass vessel)
        {
            switch (vessel)
            {
                case VesselClass.Suezmax: return SuezmaxDraughtMetres;
                case VesselClass.Panamax: return PanamaxDraughtMetres;
                case VesselClass.Handysize: return HandysizeDraughtMetres;
                case VesselClass.InlandBarge: return InlandBargeDraughtMetres;
                default: return 0.0;
            }
        }

        /// <summary> A vessel class's name for a report line. </summary>
        public static string NameOf(VesselClass vessel)
        {
            switch (vessel)
            {
                case VesselClass.Suezmax: return "Suezmax vessels";
                case VesselClass.Panamax: return "Panamax vessels";
                case VesselClass.Handysize: return "Handysize vessels";
                case VesselClass.InlandBarge: return "inland barges";
                default: return "no vessel";
            }
        }

        /// <summary> Answers the water-port question: whether the body is reachable, and what its navigable depth at the
        /// quay admits. Sea, lake and big river are all admissible -- the caller decides the body is one of those and
        /// reads its depth; this decides the vessel. A depth below an inland barge's draught is REFUSED with the number,
        /// which is the author's *"told"* -- it is not a container terminal and it is not silently one either. </summary>
        public static PortSitingReport ResolveWaterPort(double navigableDepthMetres, bool waterReachable)
        {
            PortSitingReport report = new PortSitingReport();
            report.kind = PortKind.WaterPort;
            report.navigableDepthMetres = navigableDepthMetres;

            if (!waterReachable)
            {
                report.refusal = "no sea, lake or navigable river was reachable from this site, so there is no water for"
                                 + " a quay to face";
                return report;
            }
            if (double.IsNaN(navigableDepthMetres) || double.IsInfinity(navigableDepthMetres))
            {
                report.refusal = "the navigable depth at the quay line could not be read, so no vessel class can be"
                                 + " admitted";
                return report;
            }

            VesselClass admitted = AdmittedByDepth(navigableDepthMetres);
            if (admitted == VesselClass.None)
            {
                report.refusal = "the navigable depth is "
                                 + navigableDepthMetres.ToString("0.##", CultureInfo.InvariantCulture)
                                 + " m, below the " + InlandBargeDraughtMetres.ToString("0.##",
                                     CultureInfo.InvariantCulture)
                                 + " m an inland barge needs, so this water floats no port -- it is water, not a quay";
                return report;
            }

            report.resolved = true;
            report.admittedClass = admitted;
            report.requiredDepthMetres = DraughtOf(admitted);
            return report;
        }

        /// <summary> Answers the dry-port question: an inland container depot is the port family WITHOUT water, so it is
        /// decided by the two land connections it lives on -- a rail spur and a trunk road, both at the settlement edge.
        /// Missing either is refused by name, because a dry port with no rail is a lorry park and a dry port with no
        /// trunk road strands the containers it lands. </summary>
        public static PortSitingReport ResolveDryPort(bool railPresent, bool trunkRoadPresent)
        {
            PortSitingReport report = new PortSitingReport();
            report.kind = PortKind.DryPort;
            report.railPresent = railPresent;
            report.trunkRoadPresent = trunkRoadPresent;

            if (!railPresent && !trunkRoadPresent)
            {
                report.refusal = "a dry port needs both a rail spur and a trunk road, and this site has neither";
                return report;
            }
            if (!railPresent)
            {
                report.refusal = "a dry port needs a rail spur to move containers off the road, and this site has none";
                return report;
            }
            if (!trunkRoadPresent)
            {
                report.refusal = "a dry port needs a trunk road to clear its containers, and this site has none";
                return report;
            }
            report.resolved = true;
            return report;
        }

        /// <summary> Answers the shipment-yard question: a yard needs rail, and only rail -- *"some railways passes in
        /// port and also shipment yard"* is exactly a yard the spur reaches. No trunk road is required, because a yard
        /// marshals what the rail brings; missing rail is refused by name. </summary>
        public static PortSitingReport ResolveShipmentYard(bool railPresent)
        {
            PortSitingReport report = new PortSitingReport();
            report.kind = PortKind.ShipmentYard;
            report.railPresent = railPresent;

            if (!railPresent)
            {
                report.refusal = "a shipment yard is marshalled by rail, and this site has no rail spur to reach it";
                return report;
            }
            report.resolved = true;
            return report;
        }
    }
}
