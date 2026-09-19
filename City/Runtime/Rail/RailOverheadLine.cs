// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-27 / D-7. Everything about overhead line equipment that is a DIMENSION or a LIMIT rather than a
    /// geometry: how far apart the masts stand, how far the contact wire is allowed to wander off the track centreline,
    /// how deep the catenary sags and how low the wire may be pulled when a structure is in the way.
    ///
    /// It is a separate class from <see cref="RailFormationClass"/> for the reason that class already argues about the
    /// track standard: the formation is the section a railway is BUILT to and this is the equipment strung ABOVE it. The
    /// two figures that live on the formation and not here -- <see cref="RailFormationClass.contactWireHeightMetres"/>
    /// and <see cref="RailFormationClass.electricalClearanceMetres"/> -- stay there on purpose, because
    /// <see cref="RailFormationClass.RequiredStructureClearanceMetres"/> is the answer a BRIDGE asks for and a bridge
    /// asks the formation, not the wire. Copying them here would be the second place the nominal height lived, and the
    /// first time an author edited one a bridge would be built to the other.
    ///
    /// Every figure is real practice for a 25 kV AC overhead line and is cited where a source names it. Where a class
    /// figure differs for a tramway the difference is carried by <see cref="RailOverheadClasses"/> and not by a second
    /// struct, for the same reason <see cref="RailLineClasses"/> gives. </summary>
    [Serializable]
    public sealed class RailOverheadClass
    {
        /// <summary> The longest span allowed between masts on straight track, in metres. 65 m is the ordinary upper
        /// figure for a 25 kV simple catenary in still air; beyond it the contact wire's own sag between supports and
        /// its lateral blow-off in wind both stop being controllable with a single dropper run. </summary>
        public float maxSpanMetres = 65f;

        /// <summary> The shortest span the planner will use before it gives up and refuses, in metres. 45 m is not a
        /// physical floor -- masts stand at 27 m through a tramway's tightest curves -- it is the shortest span this
        /// CLASS is willing to pay for, and the task text names 45 to 65 as the straight-track window. Curves shorten
        /// spans below it through <see cref="minCurveSpanMetres"/>, which is a different and smaller number, because a
        /// curve that cannot be wired at 45 m is a curve that must be wired at 30 rather than refused. </summary>
        public float minSpanMetres = 45f;

        /// <summary> The absolute shortest span through a curve, in metres. Below this the masts are closer together
        /// than their own foundations allow and the run refuses with the radius that asked for it. 20 m is tramway
        /// practice in a street corner and is the tightest anything in this tool needs. </summary>
        public float minCurveSpanMetres = 20f;

        /// <summary> How far the contact wire is offset from the track centreline at each mast, in metres, alternating
        /// side to side. THE WHOLE REASON OVERHEAD LINE EQUIPMENT IS STAGGERED: a wire held dead over the centreline
        /// wears one groove across the middle of the pantograph and cuts it in half. 0.2 m each way is standard
        /// mainline stagger; a tramway uses less because its pantograph is narrower. </summary>
        public float staggerMetres = 0.2f;

        /// <summary> Half the working width of the pantograph head, in metres -- the distance either side of the track
        /// centreline within which the wire must stay for the head to stay in contact. 0.55 m is half of the 1.1 m
        /// effective width of a 1,600 mm European head once its horn is discounted, which is the figure this file
        /// checks the mid-span offset against and NOT the head's full width: a wire on the horn is a wire about to come
        /// off. </summary>
        public float pantographHalfSweepMetres = 0.55f;

        /// <summary> System height: how far the catenary (messenger) wire hangs above the contact wire at a mast, in
        /// metres. 1.4 m is standard for a 25 kV simple catenary. This is what the droppers span. </summary>
        public float systemHeightMetres = 1.4f;

        /// <summary> Spacing between droppers along a span, in metres. Real practice is roughly 4.5 to 6.75 m -- close
        /// enough that the contact wire between two droppers is effectively straight, which is the entire purpose of
        /// hanging them: the messenger sags in a catenary and the contact wire does not. </summary>
        public float dropperSpacingMetres = 5.4f;

        /// <summary> The shortest dropper the class will hang, in metres. Approaching a mast the messenger and contact
        /// wire converge, and a dropper shorter than this is a rigid link rather than a wire -- so the planner stops
        /// hanging them rather than emitting a 0.02 m one, which is what the real thing does. </summary>
        public float minDropperMetres = 0.15f;

        /// <summary> The messenger wire's sag as a fraction of its span, fed to
        /// <see cref="TransmissionLine.SolveCatenaryConstant"/>. 0.01 -- one per cent -- is the auto-tensioned figure
        /// for a 25 kV catenary and is an order of magnitude tighter than a distribution conductor's 3 %, which is why
        /// this is a class field and not a shared constant with <c>DistributionPoleClass</c>: a railway messenger is
        /// tensioned to about 15 kN and a village power line is not. </summary>
        public float messengerSagRatio = 0.01f;

        /// <summary> The lowest the contact wire may be pulled under a structure, in metres above the rail head. NOT a
        /// preference: this is the electrical floor, and a structure that cannot give it is refused with the figure.
        /// 4.17 m is the absolute minimum for 25 kV in UK practice (BS EN 50119's reduced-clearance case); anything
        /// lower needs the section isolated, which is a decision no procedural tool should make. </summary>
        public float constrainedMinimumHeightMetres = 4.17f;

        /// <summary> The steepest the contact wire is allowed to climb or fall along the track, as a gradient. Pulling
        /// the wire down for a bridge cannot be done in one span: a pantograph following a wire that drops faster than
        /// this bounces, and a bounced pantograph is a pulled-down wire. 1 in 500 is mainline practice at speed and is
        /// the figure the transition length is derived from. </summary>
        public float maxWireGradient = 0.002f;

        /// <summary> How far up the mast the messenger is fixed above the contact wire's own height at that mast, in
        /// metres. Distinct from <see cref="systemHeightMetres"/> only in that the bracket sits above the messenger
        /// itself; kept as its own figure because it is the height the MAST is built to and D-15's mesh budget counts
        /// mast height. </summary>
        public float bracketRiseMetres = 0.35f;

        // ---------------------------------------------------------------- what the equipment is physically made of

        /// <summary> How far the mast's own head rises above the point the messenger is fixed at, in metres. A mast is
        /// not cut off at its topmost fitting: the head carries the bracket's tie tube, the aerial earth wire on many
        /// lines, and enough steel above the highest bolt hole for the bolt to have metal around it. 0.6 m is what a
        /// 25 kV mast leaves.
        ///
        /// Kept SEPARATE from <see cref="bracketRiseMetres"/> even though both add height at the same place, because
        /// they are different questions with different answers. The bracket rise is how high the MESSENGER is fixed
        /// above the contact wire, and it is read by the planner to place a wire. This is how much steel stands above
        /// that fixing, and it is read only by the mesh. Folding them into one figure would mean an author who wanted
        /// a taller mast head would silently raise the messenger and with it every dropper on the line. </summary>
        public float mastTopAboveMessengerMetres = 0.6f;

        /// <summary> How far the mast is buried, in metres. Read ONLY by the mesh, which starts the shaft this far
        /// below the ground it stands on rather than at the ground exactly.
        ///
        /// A 2.2 m augered concrete foundation is what an ordinary 25 kV mast sits in, and the number is here rather
        /// than as a mesh constant because the depth is a property of the CLASS: a tramway mast in a footway is set
        /// far shallower than a mainline mast in an embankment, and the same mesh code has to build both.
        ///
        /// The buried length is not decoration. The ground under a mast is sampled at the mast's own position, off to
        /// the side of the formation where it is a different height from the rail head; a shaft that started exactly
        /// at that sample would show daylight under itself the moment the ground fell away between the sample and the
        /// shaft's own diameter -- which on a cutting slope is every mast on the run. Burying it is how a real
        /// foundation solves the same problem and costs one ring of vertices nobody sees. </summary>
        public float mastFoundationDepthMetres = 2.2f;

        /// <summary> The mast's diameter at ground level and at its head, in metres. Real 25 kV masts are rolled
        /// H-sections or tapered tubes around 300 mm across the base and 180 mm at the head; the mesh builds a tapered
        /// tube, which is the tube case exactly and is a fair silhouette of the H-section at any distance a railway is
        /// looked at from.
        ///
        /// Two figures rather than one because the taper is what makes a mast read as a mast: a parallel post of
        /// constant diameter reads as scaffolding, and the eye picks the difference up at a hundred metres even though
        /// it cannot measure either figure. </summary>
        public float mastBaseDiameterMetres = 0.3f;
        public float mastTopDiameterMetres = 0.18f;

        /// <summary> The diameter of the registration bracket's tube, in metres -- the arm that reaches out from the
        /// mast over the track and holds the contact wire at its staggered offset. 60 mm is ordinary steel tube for a
        /// cantilever's main member. </summary>
        public float bracketTubeDiameterMetres = 0.06f;

        /// <summary> The three wire diameters, in metres, and every one of them is a real conductor size rather than a
        /// figure chosen to look right.
        ///
        /// * The CONTACT wire is 107 mm² grooved hard-drawn copper to BS EN 50149, whose nominal diameter is 12.24 mm.
        ///   It is grooved rather than round -- the grooves are what the dropper clips grip -- and a round tube of the
        ///   same nominal diameter is the correct silhouette at every distance the wire is visible from.
        /// * The MESSENGER is 65 mm² cadmium-copper of nineteen 2.1 mm strands, so its overall diameter is five strand
        ///   diameters across: 10.5 mm. Thinner than the contact wire, which surprises people, and is right -- the
        ///   messenger carries weight and the contact wire carries a pantograph wearing it away, so the contact wire is
        ///   sized for the wear allowance.
        /// * A DROPPER is 12 mm² stranded copper, about 5 mm over.
        ///
        /// These live on the class and not in the mesh file because they differ by column -- a tramway's trolley wire
        /// is 80 mm² and an LGV's is 150 -- and a mesh holding one figure would build every column's wire at the
        /// mainline size. </summary>
        public float contactWireDiameterMetres = 0.01224f;
        public float messengerWireDiameterMetres = 0.0105f;
        public float dropperWireDiameterMetres = 0.005f;

        public static RailOverheadClass Default() { return new RailOverheadClass(); }

        /// <summary> The longest span this class may use through a curve of the given radius, in metres, before the
        /// contact wire's mid-span offset would leave the pantograph's sweep.
        ///
        /// THIS IS THE ONE PIECE OF REAL GEOMETRY IN THE FILE, so it is worth stating exactly. The wire runs in
        /// STRAIGHT CHORDS from mast to mast; the track runs in an ARC. So on a curve the wire cuts the corner, and the
        /// gap between the chord and the arc at mid-span is the versine, v = L^2/(8R). The wire is staggered to the
        /// OUTSIDE of the curve at each mast (which is why <see cref="StaggerSignAt"/> stops alternating on a curve),
        /// so mid-span the wire is at (stagger - versine) from the centreline, measured positive outward -- and once
        /// the versine exceeds the stagger the wire has crossed the centreline and keeps going inward. What must not
        /// happen is |stagger - versine| exceeding the pantograph's half sweep.
        ///
        /// The binding case is the inward one, so the limit is v &lt;= stagger + halfSweep, which rearranges to
        /// L &lt;= sqrt(8R(stagger + halfSweep)). On a 300 m mainline curve with 0.2 m stagger and a 0.55 m half
        /// sweep that is sqrt(8 * 300 * 0.75) = 42.4 m -- comfortably inside the 45 to 65 m straight window, which is
        /// exactly the shortening the task text asks for and is why a real mainline's masts crowd together through a
        /// curve. On a tramway's 25 m radius it is sqrt(8 * 25 * 0.5) = 10 m, which is BELOW
        /// <see cref="minCurveSpanMetres"/> -- and that is the refusal this method exists to make visible rather than
        /// something to quietly clamp. </summary>
        public double MaxSpanForRadiusMetres(double radiusMetres)
        {
            if (double.IsInfinity(radiusMetres) || radiusMetres <= 0.0) { return maxSpanMetres; }
            double lateralBudget = staggerMetres + pantographHalfSweepMetres;
            if (lateralBudget <= 0.0) { return minCurveSpanMetres; }
            double limit = Math.Sqrt(8.0 * radiusMetres * lateralBudget);
            return limit < maxSpanMetres ? limit : maxSpanMetres;
        }

        /// <summary> The mid-span offset of the contact wire from the track centreline, in metres, positive OUTWARD on
        /// a curve and positive toward the staggered side on a straight. On a straight the wire is a chord between two
        /// masts staggered to OPPOSITE sides, so mid-span it is on the centreline and this is zero -- which is correct
        /// and is why a straight is wired at the full span. On a curve both masts stagger outward and the chord cuts
        /// in by the versine. </summary>
        public double MidSpanOffsetMetres(double spanMetres, double radiusMetres, bool alternating)
        {
            if (alternating) { return 0.0; }
            if (double.IsInfinity(radiusMetres) || radiusMetres <= 0.0) { return staggerMetres; }
            double versine = (spanMetres * spanMetres) / (8.0 * radiusMetres);
            return staggerMetres - versine;
        }

        /// <summary> The length of track needed to raise or lower the wire by a given height at the class gradient, in
        /// metres. Used to decide how far AHEAD of a structure the wire has to start coming down -- which is the figure
        /// that turns "this bridge is too low" into "this bridge is too low and the wire cannot get there in time".
        /// </summary>
        public double TransitionLengthMetres(double heightChangeMetres)
        {
            double drop = Math.Abs(heightChangeMetres);
            if (drop <= 0.0) { return 0.0; }
            double gradient = maxWireGradient > 0f ? maxWireGradient : 0.002;
            return drop / gradient;
        }

        /// <summary> Which side the wire is staggered at a mast, +1 or -1, alternating with the mast index. Kept as a
        /// method rather than left to the caller so the alternation cannot start on a different foot in two places --
        /// two callers disagreeing about the parity would put two consecutive masts on the same side, which reads as a
        /// 0.4 m step in the wire. </summary>
        public static int StaggerSignAt(int mastIndex)
        {
            return (mastIndex & 1) == 0 ? 1 : -1;
        }

        /// <summary> Whether this class's figures are self-consistent enough to plan from. Checked once at the top of
        /// a plan rather than defended at every use, so a nonsense class produces one sentence naming the field rather
        /// than a run of masts at NaN. </summary>
        public bool IsShapeConsistent(out string reason)
        {
            if (maxSpanMetres <= 0f) { reason = "the longest span is " + Figure(maxSpanMetres) + " m"; return false; }
            if (minSpanMetres <= 0f || minSpanMetres > maxSpanMetres)
            {
                reason = "the span window runs from " + Figure(minSpanMetres) + " m up to "
                         + Figure(maxSpanMetres) + " m, which is backwards";
                return false;
            }
            if (minCurveSpanMetres <= 0f || minCurveSpanMetres > minSpanMetres)
            {
                reason = "the tightest curve span, " + Figure(minCurveSpanMetres)
                         + " m, is not shorter than the straight-track minimum of " + Figure(minSpanMetres) + " m";
                return false;
            }
            if (pantographHalfSweepMetres <= 0f)
            {
                reason = "the pantograph's half sweep is " + Figure(pantographHalfSweepMetres)
                         + " m, so no wire position is inside it";
                return false;
            }
            if (staggerMetres < 0f || staggerMetres >= pantographHalfSweepMetres)
            {
                reason = "the stagger of " + Figure(staggerMetres) + " m is outside the pantograph's half sweep of "
                         + Figure(pantographHalfSweepMetres) + " m at the mast itself";
                return false;
            }
            if (systemHeightMetres <= 0f)
            {
                reason = "the system height is " + Figure(systemHeightMetres)
                         + " m, so the messenger is not above the contact wire";
                return false;
            }
            if (dropperSpacingMetres <= 0f)
            {
                reason = "the dropper spacing is " + Figure(dropperSpacingMetres) + " m";
                return false;
            }
            if (messengerSagRatio <= 0f || messengerSagRatio >= 0.2f)
            {
                reason = "the messenger sag ratio is " + messengerSagRatio.ToString("0.####",
                             CultureInfo.InvariantCulture) + ", which is not a catenary";
                return false;
            }
            if (constrainedMinimumHeightMetres <= 0f)
            {
                reason = "the constrained minimum height is " + Figure(constrainedMinimumHeightMetres) + " m";
                return false;
            }
            if (maxWireGradient <= 0f || maxWireGradient >= 0.05f)
            {
                reason = "the wire gradient limit is " + maxWireGradient.ToString("0.#####",
                             CultureInfo.InvariantCulture) + ", which no pantograph follows";
                return false;
            }
            return IsBuildShapeConsistent(out reason);
        }

        /// <summary> The half of the shape check that only the MESH cares about: the mast's own dimensions and the
        /// three wire diameters.
        ///
        /// Split out rather than folded into the loop above so the split can be stated. Everything above this line is
        /// read by <see cref="RailOverheadPlanner"/> and decides WHERE the equipment goes; everything below it is read
        /// only by D-7's mesh and decides WHAT IT LOOKS LIKE. A planner that refused a whole electrified railway
        /// because an author had typed a zero into a mast diameter would be refusing a line whose wire positions are
        /// all perfectly good -- but a mesh that silently built a mast of zero diameter would leave a wire hanging in
        /// the air off nothing, so neither ignoring it nor refusing the plan is right on its own.
        ///
        /// The resolution is that this is called from <see cref="IsShapeConsistent"/> anyway -- so an author sees the
        /// sentence at the top of the inspector, where every other class complaint appears -- and is ALSO callable on
        /// its own, so the mesh can ask the narrower question without asking the planner's. Public and pure, so D-16
        /// asserts the wording without a scene. </summary>
        public bool IsBuildShapeConsistent(out string reason)
        {
            if (mastBaseDiameterMetres <= 0f || mastTopDiameterMetres <= 0f)
            {
                reason = "the mast measures " + Figure(mastBaseDiameterMetres) + " m across the base and "
                         + Figure(mastTopDiameterMetres) + " m across the head, so it has no thickness";
                return false;
            }
            if (mastTopDiameterMetres > mastBaseDiameterMetres)
            {
                reason = "the mast is " + Figure(mastTopDiameterMetres) + " m across the head and only "
                         + Figure(mastBaseDiameterMetres) + " m across the base, so it widens as it rises";
                return false;
            }
            if (mastTopAboveMessengerMetres < 0f)
            {
                reason = "the mast head stands " + Figure(mastTopAboveMessengerMetres)
                         + " m above the messenger fixing, so the messenger is fixed above the mast";
                return false;
            }
            if (mastFoundationDepthMetres < 0f)
            {
                reason = "the mast foundation is " + Figure(mastFoundationDepthMetres)
                         + " m deep, so the mast starts above the ground it stands on";
                return false;
            }
            if (bracketTubeDiameterMetres <= 0f)
            {
                reason = "the registration bracket's tube is " + Figure(bracketTubeDiameterMetres) + " m across";
                return false;
            }
            if (contactWireDiameterMetres <= 0f || messengerWireDiameterMetres <= 0f
                || dropperWireDiameterMetres <= 0f)
            {
                reason = "the contact wire, messenger and dropper measure " + Figure(contactWireDiameterMetres)
                         + " m, " + Figure(messengerWireDiameterMetres) + " m and "
                         + Figure(dropperWireDiameterMetres) + " m across, and a wire with no thickness is not a wire";
                return false;
            }
            if (contactWireDiameterMetres >= mastTopDiameterMetres)
            {
                reason = "the contact wire is " + Figure(contactWireDiameterMetres)
                         + " m across and the mast head only " + Figure(mastTopDiameterMetres)
                         + " m, so the wire is thicker than what holds it";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static string Figure(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }
    }


    /// <summary> One structure the railway passes under, offered to the planner rather than found by it.
    ///
    /// WHY THIS IS AN INPUT AND NOT A SEARCH. What is above a railway is a bridge, a tunnel portal, a station canopy or
    /// a footbridge, and every one of those is owned by other code -- B-16 owns bridges, D-11 owns tunnels, D-8 owns
    /// stations. A planner that went looking for its own overhead obstructions would be a fourth opinion about where
    /// they are. So the caller says "there is 5.1 m of headroom from station 800 to station 812", and this file's whole
    /// job is to answer whether the wire fits.
    ///
    /// <see cref="headroomMetres"/> is measured FROM THE RAIL HEAD, not from the ground, for the same reason the wire
    /// height is: the two figures have to be in the same datum to be subtracted, and the rail head is the one the
    /// railway is built to. A caller holding a world-space soffit height subtracts the sample's own
    /// <c>position.y</c> -- which <see cref="RailAlignmentSample.position"/> documents as top of rail -- rather than
    /// its <c>groundHeightMetres</c>. </summary>
    [Serializable]
    public sealed class RailOverheadStructure
    {
        public string structureName = string.Empty;
        public float fromStationMetres;
        public float toStationMetres;

        /// <summary> Clear height under the structure, in metres above the rail head. </summary>
        public float headroomMetres = 6f;

        /// <summary> Whether this is a tunnel rather than something the railway passes beneath. It changes no
        /// arithmetic -- a tunnel bore and a bridge soffit are both a ceiling -- and it is carried so the refusal names
        /// the right thing: "the tunnel at 1,200 m" and "the bridge at 1,200 m" send an author to two different
        /// files. </summary>
        public bool isTunnel;

        public float LengthMetres
        {
            get { return Mathf.Abs(toStationMetres - fromStationMetres); }
        }

        public bool Covers(float stationMetres)
        {
            float low = Mathf.Min(fromStationMetres, toStationMetres);
            float high = Mathf.Max(fromStationMetres, toStationMetres);
            return stationMetres >= low && stationMetres <= high;
        }
    }


    /// <summary> One mast, with the two wire heights it holds and which side it holds them to.
    ///
    /// <see cref="basePoint"/> is at the GROUND, not at the rail head, because a mast is founded in earth and its
    /// height is what D-15 counts; every wire height on it is above the rail head and is stated as such. That means the
    /// mesh has to add <c>railHeadAboveGroundMetres</c> to reach the first fitting, which is one addition in one place
    /// and is preferable to a base point floating at a height no foundation is at. </summary>
    [Serializable]
    public struct RailOverheadMast
    {
        public float stationMetres;

        /// <summary> Where the mast stands, on the ground, offset laterally clear of the formation. </summary>
        public Vector3 basePoint;

        /// <summary> Top of rail at this mast, in world Y. The datum every height below is measured from, carried so a
        /// mesh or a gate never has to go back to the sample list to find it. </summary>
        public float railHeadWorldY;

        /// <summary> How far the rail head is above the ground at the mast's own position, in metres. Not the sample's
        /// earthwork -- that is measured at the formation base on the centreline, and a mast stands off to the side
        /// where the ground is a different height. </summary>
        public float railHeadAboveGroundMetres;

        /// <summary> Contact wire height at this mast, in metres above the rail head. Equal to the class nominal on
        /// open track and lower where a structure pulled it down. </summary>
        public float contactWireHeightMetres;

        /// <summary> Messenger height at this mast, above the rail head: the contact height plus the system
        /// height. </summary>
        public float messengerHeightMetres;

        /// <summary> Which side the contact wire is staggered to here, +1 or -1, and how far. On a curve both are
        /// outward, so the sign follows the curvature rather than the mast index -- see
        /// <see cref="RailOverheadPlanner"/>. </summary>
        public int staggerSign;
        public float staggerMetres;

        /// <summary> Signed radius at the mast, positive infinity on straight track. Carried so an inspector can see
        /// WHY a span shortened without recomputing curvature. </summary>
        public double radiusMetres;

        /// <summary> Whether the wire at this mast is below the class nominal because a structure pulled it down. Not
        /// a refusal on its own -- a lowered wire that still clears the minimum is ordinary. </summary>
        public bool loweredForStructure;

        public string structureName;
    }


    /// <summary> One span of overhead line between two masts: the solved messenger catenary, the droppers hung from it,
    /// and the check that the contact wire stayed inside the pantograph's sweep at mid-span.
    ///
    /// The CONTACT wire is not a catenary and has no curve field, which is the whole point of a dropper: the messenger
    /// sags and the droppers get shorter toward mid-span by exactly the messenger's sag, so the contact wire runs
    /// almost dead level. Recording a catenary for it would be recording the shape the droppers exist to
    /// remove. </summary>
    [Serializable]
    public struct RailOverheadSpan
    {
        public int fromMastIndex;
        public int toMastIndex;
        public float spanMetres;

        /// <summary> The messenger wire, solved by <see cref="TransmissionLine.Solve"/> in the span's own vertical
        /// plane with x = 0 at the first mast and heights measured ABOVE THE RAIL HEAD. Using rail-head heights rather
        /// than world Y is deliberate: the solver's only use of the two endpoint heights is their difference, so either
        /// datum gives the same curve -- and a curve in rail-head units can be read straight against the class figures
        /// without knowing where in the world the span is. </summary>
        public Catenary messenger;

        /// <summary> How many droppers hang in this span, and the shortest one. The shortest is reported because a
        /// dropper run whose minimum has hit <see cref="RailOverheadClass.minDropperMetres"/> is a span where the
        /// system height ran out, which is worth seeing before it becomes a refusal in a tighter one. </summary>
        public int dropperCount;
        public float shortestDropperMetres;

        /// <summary> Where the contact wire sits laterally at mid-span, in metres from the track centreline, and
        /// whether that is inside the pantograph's half sweep. Signed, positive outward on a curve. </summary>
        public double midSpanOffsetMetres;
        public bool insideSweep;

        /// <summary> The lowest the contact wire gets across this span, in metres above the rail head, and the lowest
        /// headroom any structure offered over the same stretch. Both carried so the refusal can quote the pair rather
        /// than a difference -- "4.9 m of wire under 4.6 m of bridge" is actionable and "-0.3 m" is not. </summary>
        public float lowestContactHeightMetres;
        public float lowestHeadroomMetres;
    }


    /// <summary> One dropper: where it hangs and how long it is. Kept as its own record rather than as a length on the
    /// span, because D-15's mesh draws each one and a count plus a spacing is not enough to place them -- the last
    /// dropper in a span is not at the class spacing from the mast. </summary>
    [Serializable]
    public struct RailOverheadDropper
    {
        public int spanIndex;
        public float stationMetres;

        /// <summary> Distance from the span's first mast, in metres. </summary>
        public float offsetInSpanMetres;

        /// <summary> Messenger and contact heights here, both above the rail head; the dropper is the difference.
        /// </summary>
        public float messengerHeightMetres;
        public float contactHeightMetres;
        public float lengthMetres;
    }


    /// <summary> What an overhead line worked out to. Built on <see cref="RailAlignmentPlan"/>'s shape on purpose --
    /// what was placed, the measured figures the inspector prints, and a list of refusals that is empty or fatal -- so
    /// the rail authoring surfaces answer the same shape of question and D-16 asserts them the same way.
    ///
    /// Which things refuse here is a design decision and is worth naming:
    ///
    /// * a structure that cannot give <see cref="RailOverheadClass.constrainedMinimumHeightMetres"/> -- the wire
    ///   physically does not fit, and this is the refusal the task text asks for BY NAME, with the figure;
    /// * a curve whose radius cannot be wired at <see cref="RailOverheadClass.minCurveSpanMetres"/> -- masts closer
    ///   than their own foundations;
    /// * a class whose figures are not a catenary system at all;
    /// * an alignment with fewer than two samples, or one that is not electrified.
    ///
    /// A LOWERED WIRE IS NOT A REFUSAL, and that is the important half. Every electrified railway in the world drops
    /// its wire under bridges; a run that refused one would refuse most real railways. What a lowered wire gets is a
    /// mast flagged <see cref="RailOverheadMast.loweredForStructure"/> and a figure in the summary.
    ///
    /// NOR IS A SHORTENED SPAN A REFUSAL. Shortening through curves is the correct behaviour and the thing the task
    /// text asks for -- it is reported as <see cref="shortenedSpanCount"/> so an author can see the curve cost them
    /// masts, which is the same treatment <see cref="RailAlignmentPlan.shortenedTransitionCount"/> gets. </summary>
    [Serializable]
    public sealed class RailOverheadPlan
    {
        public List<RailOverheadMast> masts = new List<RailOverheadMast>();
        public List<RailOverheadSpan> spans = new List<RailOverheadSpan>();
        public List<RailOverheadDropper> droppers = new List<RailOverheadDropper>();
        public List<string> refusals = new List<string>();

        public double runLengthMetres;
        public double nominalContactHeightMetres;
        public double constrainedMinimumHeightMetres;

        /// <summary> How many spans were shortened below the class's straight-track window by a curve, and the
        /// shortest span the run ended up with. Reported, never refused. </summary>
        public int shortenedSpanCount;
        public float shortestSpanMetres = float.PositiveInfinity;
        public float longestSpanMetres;

        /// <summary> How many masts hold a wire below the class nominal, and the lowest the wire gets anywhere on the
        /// run, in metres above the rail head. The lowest is the figure that decides whether the run is legal, so it
        /// is the one the summary leads its clearance clause with. </summary>
        public int loweredMastCount;
        public float lowestContactHeightMetres = float.PositiveInfinity;
        public float lowestContactStationMetres;

        /// <summary> The worst mid-span lateral offset on the run, in metres from the centreline, and where. Compared
        /// against the class half sweep by the planner; carried so an inspector can print how close the run came to
        /// the limit rather than only whether it passed. </summary>
        public double worstMidSpanOffsetMetres;
        public float worstMidSpanOffsetStationMetres;

        public int structureCount;
        public float shortestDropperMetres = float.PositiveInfinity;

        /// <summary> Whether this overhead line can be built as it stands. One mast is not a line -- a single mast
        /// holds a wire that goes nowhere -- so two is the floor, matching <see cref="RailAlignmentPlan.Buildable"/>'s
        /// argument about a single sample. </summary>
        public bool Buildable
        {
            get { return masts.Count > 1 && refusals.Count == 0; }
        }

        /// <summary> The one sentence that says what the overhead line is. Pure, so D-16 asserts the wording without a
        /// scene, and read by the inspector rather than re-worded there. </summary>
        public string Summary()
        {
            if (masts.Count < 2)
            {
                return "No overhead line: " + (refusals.Count > 0 ? refusals[0] : "nothing was strung") + ".";
            }
            string text = masts.Count.ToString(CultureInfo.InvariantCulture) + " masts over "
                          + Fixed(runLengthMetres, "0.#") + " m, spans "
                          + Fixed(shortestSpanMetres, "0.#") + " to " + Fixed(longestSpanMetres, "0.#") + " m";
            if (shortenedSpanCount > 0)
            {
                text += " (" + shortenedSpanCount.ToString(CultureInfo.InvariantCulture)
                        + " shortened through curves)";
            }
            text += ". Contact wire " + Fixed(nominalContactHeightMetres, "0.##")
                    + " m above the rail head, lowest " + Fixed(lowestContactHeightMetres, "0.##") + " m at "
                    + Fixed(lowestContactStationMetres, "0") + " m against "
                    + Fixed(constrainedMinimumHeightMetres, "0.##") + " m permitted.";
            if (loweredMastCount > 0)
            {
                text += " " + loweredMastCount.ToString(CultureInfo.InvariantCulture)
                        + (loweredMastCount == 1 ? " mast" : " masts") + " lowered for "
                        + structureCount.ToString(CultureInfo.InvariantCulture)
                        + (structureCount == 1 ? " structure." : " structures.");
            }
            text += " " + droppers.Count.ToString(CultureInfo.InvariantCulture) + " droppers, shortest "
                    + Fixed(shortestDropperMetres, "0.##") + " m.";
            text += " Worst mid-span offset " + Fixed(worstMidSpanOffsetMetres, "0.###") + " m at "
                    + Fixed(worstMidSpanOffsetStationMetres, "0") + " m.";
            if (refusals.Count > 0)
            {
                text += " " + refusals.Count.ToString(CultureInfo.InvariantCulture)
                        + (refusals.Count == 1 ? " refusal." : " refusals.");
            }
            return text;
        }

        private static string Fixed(double value, string format)
        {
            if (double.IsInfinity(value) || double.IsNaN(value)) { return "none"; }
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }


    /// <summary> B-27 / D-7 in its provable form: given a staked railway's samples, the author's overhead class and
    /// whatever structures the railway passes under, work out where every mast stands, how high the wire hangs at each,
    /// how the messenger sags between them, where the droppers go, and which spans must be refused.
    ///
    /// It is a pure static function of its arguments -- no component, no scene, no clock, no random (C-06) -- for the
    /// two reasons <c>RoadPowerLinePlanner</c> gives for being one: a gate can wire a railway without building one, by
    /// handing it a synthesised sample list; and the doctrine this folder follows is that the computing type computes
    /// and refuses while something else does the scene work.
    ///
    /// WHY IT TAKES THE PLAN'S SAMPLES AND DOES NOT RE-SAMPLE. <see cref="RailAlignmentSample"/>'s own documentation
    /// says D-7 hangs its masts off that list rather than re-sampling the alignment, and the reason is a datum: the
    /// sample's <c>position</c> is TOP OF RAIL, which is exactly the height the task text says the wire is measured
    /// above. Re-staking the alignment here would give a second answer to where the rail head is, and the wire and the
    /// rail would then disagree by whatever the two stakings differed by -- which is the class of bug nobody finds,
    /// because both numbers look reasonable on their own.
    ///
    /// WHY MAST SPACING IS SOLVED FORWARD AND NOT QUANTISED. A pole line divides its run into even spans
    /// (<see cref="TransmissionLine.SpanCount"/>) because a distribution span's length is a preference. An overhead
    /// line's is a CONSTRAINT that varies along the run -- the curve at station 800 shortens the spans there and nothing
    /// else -- so the planner walks forward from station zero, and at each mast asks how far it may go given the
    /// tightest radius between here and there. Dividing the run evenly at the tightest radius on it would put mainline
    /// masts 42 m apart for ten kilometres because of one curve, which is not what a railway looks like. </summary>
    public static class RailOverheadPlanner
    {
        /// <summary> How many points a span's messenger and structure clearance are checked at. Nine over a 65 m span
        /// is a reading every 8 m, and it always includes both masts and the mid-span point where the messenger's sag
        /// is deepest -- the same count and the same argument as
        /// <see cref="RoadPowerLinePlanner.GroundSamplesPerSpan"/>. </summary>
        public const int SpanCheckPoints = 9;

        /// <summary> How far a mast stands clear of the edge of the formation, in metres. The mast is a structure beside
        /// the track and has to be outside the loading gauge; 0.5 m beyond the ballast shoulder is the ordinary figure
        /// and is measured from the formation's own half width so a double-track run pushes its masts further out
        /// without this number changing. </summary>
        public const float MastClearOfFormationMetres = 0.5f;

        /// <summary> Plan one overhead line, or say why not.
        ///
        /// <paramref name="alignment"/> is D-5's finished plan -- its samples are the datum and its
        /// <c>runLengthMetres</c> the extent. <paramref name="structures"/> may be null or empty, which is the common
        /// case and is not a refusal: a railway with nothing over it is wired at the class nominal throughout.
        /// <paramref name="ground"/> is read ONLY to find how high a mast's own foundation sits beside the track, and
        /// may be null -- in which case the mast's base falls back to the sample's own ground height, which is the
        /// centreline's and is right to within the cross-fall of the formation.
        ///
        /// Returns null with a note where there is nothing to wire. A run that is not electrified is one of those and
        /// is emphatically NOT a refusal: an unwired branch line is a complete railway, and refusing it would make
        /// <see cref="RailLineClasses.FitOut"/>'s own branch-line answer produce a broken plan. </summary>
        public static RailOverheadPlan Plan(RailAlignmentPlan alignment, RailAlignmentSettings settings,
            RailOverheadClass overhead, IList<RailOverheadStructure> structures, ITerrainSurfaceReader ground,
            out string note)
        {
            note = string.Empty;
            if (alignment == null) { note = "there is no staked railway to wire"; return null; }
            if (settings == null) { note = "this railway has no settings, so nothing says whether it is wired"; }
            if (settings != null && !settings.electrified)
            {
                note = "this railway is not electrified, so it carries no overhead line";
                return null;
            }
            if (alignment.samples.Count < 2)
            {
                note = "the railway has fewer than two samples, and a wire needs two ends";
                return null;
            }

            RailOverheadClass cls = overhead != null ? overhead : RailOverheadClass.Default();
            RailFormationClass form = settings != null && settings.formation != null
                ? settings.formation : RailFormationClass.Default();
            int trackCount = settings != null && settings.trackCount > 1 ? 2 : 1;

            RailOverheadPlan plan = new RailOverheadPlan();
            plan.runLengthMetres = alignment.runLengthMetres;
            plan.nominalContactHeightMetres = form.contactWireHeightMetres > 0f
                ? form.contactWireHeightMetres : 5.3;
            plan.constrainedMinimumHeightMetres = cls.constrainedMinimumHeightMetres;
            plan.structureCount = structures != null ? structures.Count : 0;

            string shapeReason;
            if (!cls.IsShapeConsistent(out shapeReason))
            {
                plan.refusals.Add("the overhead class is not a catenary system, so nothing can be strung from it: "
                                  + shapeReason);
                return plan;
            }

            // The nominal height has to clear the constrained minimum, or the class is asking for a wire that is
            // already below its own floor on OPEN TRACK -- which is a class error and not a structure refusal, and it
            // is checked here rather than in IsShapeConsistent because the nominal lives on the formation and the
            // minimum on the overhead class, so only this function sees both.
            if (plan.nominalContactHeightMetres < cls.constrainedMinimumHeightMetres)
            {
                plan.refusals.Add("the class strings its contact wire at "
                                  + Figure(plan.nominalContactHeightMetres) + " m above the rail head, which is below"
                                  + " the " + Figure(cls.constrainedMinimumHeightMetres)
                                  + " m electrical minimum before any structure is in the way");
                return plan;
            }

            PlaceMasts(plan, alignment, cls, form, structures, ground, trackCount);
            if (plan.refusals.Count > 0) { return plan; }
            CheckPerChunkCeiling(plan);
            if (plan.refusals.Count > 0) { return plan; }
            SmoothWireGradient(plan, cls);
            StringSpans(plan, alignment, cls);
            return plan;
        }

        /// <summary> D-15. Check that the masts do not exceed their instance ceiling in any one streaming chunk. </summary>
        private static void CheckPerChunkCeiling(RailOverheadPlan plan)
        {
            if (plan.masts.Count == 0) { return; }
            float chunk = CityStreamingConfig.DefaultChunkSizeMetres;
            List<Vector3> positions = new List<Vector3>(plan.masts.Count);
            for (int i = 0; i < plan.masts.Count; i++) { positions.Add(plan.masts[i].basePoint); }
            int busiest = CityInstanceBudget.BusiestChunkCount(positions, chunk);
            CityInstanceCeilingVerdict verdict = CityInstanceBudget.Check(CityInstanceFamily.OHLEMast, busiest);
            if (!verdict.withinCeiling) { plan.refusals.Add(verdict.message); }
        }

        /// <summary> Walk the run forward, planting a mast wherever the next one is allowed to be.
        ///
        /// The step is the class maximum reduced by the tightest radius found between here and there -- which is why
        /// the trial is done twice. The first pass asks the radius at the CURRENT mast for a candidate distance; the
        /// second re-asks over the stretch that candidate actually spans, because a curve that begins 30 m ahead is
        /// invisible from the mast and would otherwise be crossed by a full-length span. Two passes converge because
        /// the second span is never longer than the first, so the radius it sees is never wider. </summary>
        private static void PlaceMasts(RailOverheadPlan plan, RailAlignmentPlan alignment, RailOverheadClass cls,
            RailFormationClass form, IList<RailOverheadStructure> structures, ITerrainSurfaceReader ground,
            int trackCount)
        {
            double runLength = alignment.runLengthMetres;
            double lateral = 0.5 * form.FormationWidthMetres(trackCount) + MastClearOfFormationMetres;
            double station = 0.0;
            int mastIndex = 0;
            int guard = 0;
            int maxMasts = (int)(runLength / Math.Max(1.0, cls.minCurveSpanMetres)) + 8;

            while (true)
            {
                AddMast(plan, alignment, cls, structures, ground, lateral, station, mastIndex);
                if (station >= runLength - 0.001) { break; }
                if (++guard > maxMasts)
                {
                    plan.refusals.Add("the mast walk did not reach the end of the "
                                      + Figure(runLength) + " m run after " + guard.ToString(
                                          CultureInfo.InvariantCulture) + " masts, so the span solver is not"
                                      + " advancing");
                    return;
                }

                double trial = cls.MaxSpanForRadiusMetres(TightestRadiusBetween(alignment, station,
                    station + cls.maxSpanMetres));
                double allowed = cls.MaxSpanForRadiusMetres(TightestRadiusBetween(alignment, station,
                    station + trial));
                if (allowed < cls.minCurveSpanMetres)
                {
                    double radius = TightestRadiusBetween(alignment, station, station + cls.minCurveSpanMetres);
                    plan.refusals.Add("the curve at " + Figure(station) + " m has a radius of " + Figure(radius)
                                      + " m, which would need masts " + Figure(allowed) + " m apart to keep the"
                                      + " contact wire inside the pantograph's " + Figure(
                                          cls.pantographHalfSweepMetres) + " m half sweep -- closer than the "
                                      + Figure(cls.minCurveSpanMetres) + " m their own foundations allow");
                    return;
                }
                if (allowed < cls.minSpanMetres) { plan.shortenedSpanCount++; }

                double next = station + allowed;
                // The last span is pulled back onto the end of the run rather than overrunning it, and if that leaves
                // a stub shorter than the curve minimum the previous mast is where the run ends -- a 3 m final span
                // would be a mast standing on top of another one.
                if (next >= runLength - 0.001) { next = runLength; }
                else if (runLength - next < cls.minCurveSpanMetres) { next = runLength; }
                station = next;
                mastIndex++;
            }
        }

        /// <summary> Plant one mast: find the sample it stands at, offset it clear of the formation, and set the two
        /// wire heights -- the class nominal, or a structure's headroom where one is in the way.
        ///
        /// The stagger sign is the one place a curve changes the RULE rather than a number. On straight track the wire
        /// alternates side to side, because that is what wears the pantograph evenly. On a curve BOTH masts of a span
        /// must stagger to the OUTSIDE, because a chord already cuts inward by the versine and staggering the far mast
        /// inward as well would double the excursion -- see <see cref="RailOverheadClass.MaxSpanForRadiusMetres"/>.
        /// So the sign follows the curvature where there is any and the mast index where there is not. </summary>
        private static void AddMast(RailOverheadPlan plan, RailAlignmentPlan alignment, RailOverheadClass cls,
            IList<RailOverheadStructure> structures, ITerrainSurfaceReader ground, double lateral, double station,
            int mastIndex)
        {
            RailAlignmentSample sample = SampleAt(alignment, station);
            Vector3 right = LateralUnit(sample.tangent);

            RailOverheadMast mast = new RailOverheadMast();
            mast.stationMetres = (float)station;
            mast.railHeadWorldY = sample.position.y;
            mast.radiusMetres = sample.radiusMetres;

            // Which side the mast itself stands is the OPPOSITE of the stagger on a straight and the INSIDE of a
            // curve, so the bracket reaches over the track rather than away from it. Standing every mast on one side
            // would be simpler and would put half the brackets pointing into open air.
            double curvatureSign = CurvatureSign(sample);
            int staggerSign = curvatureSign != 0.0
                ? (int)curvatureSign : RailOverheadClass.StaggerSignAt(mastIndex);
            mast.staggerSign = staggerSign;
            mast.staggerMetres = cls.staggerMetres;

            Vector3 basePoint = sample.position - right * (float)(lateral * staggerSign);
            float groundY = ground != null ? ground.SampleHeight(basePoint.x, basePoint.z)
                : sample.groundHeightMetres;
            // A reader hands back NaN or an infinity for a point it has no loaded terrain under. Left alone that
            // becomes the mast's base Y and the mast hangs or sinks, with the not-a-number carrying up into the
            // contact wire strung from it. The centreline height the sample already carries is right to within the
            // cross-fall, so an unreadable offset point falls back to it rather than to a height that is not a number.
            if (float.IsNaN(groundY) || float.IsInfinity(groundY)) { groundY = (float)sample.groundHeightMetres; }
            basePoint.y = groundY;
            mast.basePoint = basePoint;
            mast.railHeadAboveGroundMetres = sample.position.y - groundY;

            float height = (float)plan.nominalContactHeightMetres;
            RailOverheadStructure over = StructureAt(structures, (float)station);
            if (over != null)
            {
                mast.structureName = over.structureName;
                // The wire has to sit BELOW the soffit by its own electrical clearance, not at it: the clearance is
                // the air gap 25 kV needs to an earthed structure, and a wire touching the soffit is a flashover.
                float permitted = over.headroomMetres - ClearanceOf(alignment, cls);
                if (permitted < height)
                {
                    height = permitted;
                    mast.loweredForStructure = true;
                    plan.loweredMastCount++;
                }
                if (height < cls.constrainedMinimumHeightMetres)
                {
                    plan.refusals.Add("the " + (over.isTunnel ? "tunnel" : "structure") + " "
                                      + NameOf(over) + "at " + Figure(station) + " m gives "
                                      + Figure(over.headroomMetres)
                                      + " m of headroom above the rail head, which leaves the contact wire at "
                                      + Figure(height) + " m once its " + Figure(ClearanceOf(alignment, cls))
                                      + " m electrical clearance is taken off -- below the "
                                      + Figure(cls.constrainedMinimumHeightMetres) + " m minimum by "
                                      + Figure(cls.constrainedMinimumHeightMetres - height) + " m");
                }
            }
            mast.contactWireHeightMetres = height;
            mast.messengerHeightMetres = height + cls.systemHeightMetres;

            if (height < plan.lowestContactHeightMetres)
            {
                plan.lowestContactHeightMetres = height;
                plan.lowestContactStationMetres = (float)station;
            }
            plan.masts.Add(mast);
        }

        /// <summary> The electrical clearance the wire keeps below a structure, in metres. Read straight off the
        /// formation figure the alignment carries -- a tramway's 0.15 m at 750 V DC and a 25 kV line's 0.27 m come from
        /// the one place <see cref="RailLineClasses.Formation"/> writes them, through
        /// <see cref="RailAlignmentPlan.electricalClearanceMetres"/>.
        ///
        /// It used to be recovered by subtracting a re-derived nominal wire height from the alignment's required
        /// structure clearance. That was circular: the nominal was itself the requirement minus 0.27, so the two
        /// cancelled and every class -- tram or 25 kV -- came back 0.27, and a tramway was lowered by the 25 kV air gap
        /// its 750 V wire does not need. The clearance is a class figure, so it is now read as one. </summary>
        private static float ClearanceOf(RailAlignmentPlan alignment, RailOverheadClass cls)
        {
            double clearance = alignment.electricalClearanceMetres;
            if (clearance > 0.0 && clearance < 1.0) { return (float)clearance; }
            return 0.27f;
        }

        /// <summary> Pull the wire down GRADUALLY into a lowered mast rather than in one step.
        ///
        /// This is the pass the task text's gradient requirement lives in. A mast under a bridge has its height set by
        /// the bridge; the masts either side of it have theirs set by the class nominal, and the wire between them
        /// would then drop the whole difference in one span -- a gradient of 0.5 m over 60 m, which is 1 in 120 and
        /// six times what a pantograph follows. So the drop is spread backward and forward over as many masts as
        /// <see cref="RailOverheadClass.TransitionLengthMetres"/> asks for, each mast taking as much of the difference
        /// as its own distance permits.
        ///
        /// It runs as a SEPARATE PASS after every mast is placed, and not inside the walk, because a mast cannot know
        /// how low the wire will have to be two masts ahead. Running it forward and then backward makes the result
        /// independent of which end the run was staked from, which C-06 requires: a railway reversed must be the same
        /// railway. </summary>
        private static void SmoothWireGradient(RailOverheadPlan plan, RailOverheadClass cls)
        {
            int count = plan.masts.Count;
            if (count < 3) { return; }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 0; k < count; k++)
                {
                    int i = pass == 0 ? k : count - 1 - k;
                    int prior = pass == 0 ? i - 1 : i + 1;
                    if (prior < 0 || prior >= count) { continue; }
                    RailOverheadMast here = plan.masts[i];
                    RailOverheadMast from = plan.masts[prior];
                    double distance = Math.Abs(here.stationMetres - from.stationMetres);
                    double allowedRise = distance * cls.maxWireGradient;
                    double ceiling = from.contactWireHeightMetres + allowedRise;
                    if (here.contactWireHeightMetres <= ceiling) { continue; }
                    here.contactWireHeightMetres = (float)ceiling;
                    here.messengerHeightMetres = here.contactWireHeightMetres + cls.systemHeightMetres;
                    plan.masts[i] = here;
                    if (here.contactWireHeightMetres < plan.lowestContactHeightMetres)
                    {
                        plan.lowestContactHeightMetres = here.contactWireHeightMetres;
                        plan.lowestContactStationMetres = here.stationMetres;
                    }
                }
            }
        }

        /// <summary> String the messenger across every span, hang the droppers from it, and check the contact wire
        /// stayed inside the pantograph's sweep.
        ///
        /// The messenger is solved by <see cref="TransmissionLine.Solve"/> -- the same solver the pole line uses, which
        /// is the point of the task text naming it: a railway messenger and a village conductor are the same physics at
        /// different tensions, and two catenary solvers in one tool would be two sets of last-bit differences. The
        /// heights handed to it are ABOVE THE RAIL HEAD, because the solver uses only their difference and rail-head
        /// units can be read straight against the class figures.
        ///
        /// The droppers are then the difference between the solved messenger and the contact wire, which runs in a
        /// STRAIGHT LINE between its two masts. That straight line is not an approximation: it is what the droppers are
        /// for, and a contact wire modelled as its own catenary would be a contact wire nobody had hung. </summary>
        private static void StringSpans(RailOverheadPlan plan, RailAlignmentPlan alignment, RailOverheadClass cls)
        {
            for (int i = 0; i + 1 < plan.masts.Count; i++)
            {
                RailOverheadMast a = plan.masts[i];
                RailOverheadMast b = plan.masts[i + 1];
                double spanMetres = b.stationMetres - a.stationMetres;
                if (spanMetres <= 0.0) { continue; }

                RailOverheadSpan span = new RailOverheadSpan();
                span.fromMastIndex = i;
                span.toMastIndex = i + 1;
                span.spanMetres = (float)spanMetres;
                span.messenger = TransmissionLine.Solve(spanMetres, a.messengerHeightMetres,
                    b.messengerHeightMetres, cls.messengerSagRatio);

                double radius = TightestRadiusBetween(alignment, a.stationMetres, b.stationMetres);
                bool alternating = a.staggerSign != b.staggerSign;
                span.midSpanOffsetMetres = cls.MidSpanOffsetMetres(spanMetres, radius, alternating);
                span.insideSweep = Math.Abs(span.midSpanOffsetMetres) <= cls.pantographHalfSweepMetres;
                if (!span.insideSweep)
                {
                    plan.refusals.Add("the contact wire sits " + Figure(Math.Abs(span.midSpanOffsetMetres))
                                      + " m off the track centreline halfway through the span at "
                                      + Figure(a.stationMetres) + " m, outside the pantograph's "
                                      + Figure(cls.pantographHalfSweepMetres) + " m half sweep");
                }
                if (Math.Abs(span.midSpanOffsetMetres) > Math.Abs(plan.worstMidSpanOffsetMetres))
                {
                    plan.worstMidSpanOffsetMetres = span.midSpanOffsetMetres;
                    plan.worstMidSpanOffsetStationMetres = a.stationMetres
                                                           + 0.5f * span.spanMetres;
                }

                HangDroppers(plan, cls, ref span, a, b, i);
                RecordSpanExtremes(plan, span, a, b);
                plan.spans.Add(span);
            }
        }

        /// <summary> Hang the droppers in one span and record the lowest contact height across it.
        ///
        /// The droppers are laid out from a COUNT rather than by stepping the class spacing until the far mast is
        /// reached, and the difference matters: stepping leaves a remainder against the last mast, so a 62 m span at
        /// 5.4 m spacing would end with a 0.8 m gap and a dropper almost on top of the bracket. Dividing the span into
        /// ceil(span / spacing) equal intervals and hanging one at each interior division puts them at no more than the
        /// class spacing, evenly, with none at either mast -- which is how a real span is dropped. </summary>
        private static void HangDroppers(RailOverheadPlan plan, RailOverheadClass cls, ref RailOverheadSpan span,
            RailOverheadMast a, RailOverheadMast b, int spanIndex)
        {
            span.shortestDropperMetres = float.PositiveInfinity;
            span.lowestContactHeightMetres = Math.Min(a.contactWireHeightMetres, b.contactWireHeightMetres);

            int intervals = (int)Math.Ceiling(span.spanMetres / Math.Max(0.1f, cls.dropperSpacingMetres));
            if (intervals < 1) { intervals = 1; }
            for (int d = 1; d < intervals; d++)
            {
                double offset = span.spanMetres * ((double)d / intervals);
                double messenger = span.messenger.HeightAt(offset);
                double contact = a.contactWireHeightMetres
                                 + (b.contactWireHeightMetres - a.contactWireHeightMetres)
                                 * (offset / span.spanMetres);
                double length = messenger - contact;
                if (length < span.shortestDropperMetres) { span.shortestDropperMetres = (float)length; }
                if (contact < span.lowestContactHeightMetres) { span.lowestContactHeightMetres = (float)contact; }
                if (length < cls.minDropperMetres) { continue; }

                RailOverheadDropper dropper = new RailOverheadDropper();
                dropper.spanIndex = spanIndex;
                dropper.offsetInSpanMetres = (float)offset;
                dropper.stationMetres = a.stationMetres + dropper.offsetInSpanMetres;
                dropper.messengerHeightMetres = (float)messenger;
                dropper.contactHeightMetres = (float)contact;
                dropper.lengthMetres = (float)length;
                plan.droppers.Add(dropper);
                span.dropperCount++;
            }
            if (float.IsInfinity(span.shortestDropperMetres))
            {
                span.shortestDropperMetres = cls.systemHeightMetres;
            }
        }

        /// <summary> Fold one span's figures into the run's. Separated from the stringing so the extremes are gathered
        /// in one place and a new figure cannot be added to the span and forgotten on the plan. </summary>
        private static void RecordSpanExtremes(RailOverheadPlan plan, RailOverheadSpan span, RailOverheadMast a,
            RailOverheadMast b)
        {
            if (span.spanMetres < plan.shortestSpanMetres) { plan.shortestSpanMetres = span.spanMetres; }
            if (span.spanMetres > plan.longestSpanMetres) { plan.longestSpanMetres = span.spanMetres; }
            if (span.shortestDropperMetres < plan.shortestDropperMetres)
            {
                plan.shortestDropperMetres = span.shortestDropperMetres;
            }
            if (span.lowestContactHeightMetres < plan.lowestContactHeightMetres)
            {
                plan.lowestContactHeightMetres = span.lowestContactHeightMetres;
                plan.lowestContactStationMetres = a.stationMetres + 0.5f * span.spanMetres;
            }
            span.lowestHeadroomMetres = Math.Min(a.contactWireHeightMetres, b.contactWireHeightMetres);
        }

        /// <summary> The tightest radius the alignment has anywhere between two stations, in metres, positive infinity
        /// where the stretch is straight throughout. Unsigned, because the span length a curve permits depends on how
        /// sharp it is and not on which way it turns. </summary>
        private static double TightestRadiusBetween(RailAlignmentPlan alignment, double fromStation,
            double toStation)
        {
            double low = Math.Min(fromStation, toStation);
            double high = Math.Max(fromStation, toStation);
            double tightest = double.PositiveInfinity;
            for (int i = 0; i < alignment.samples.Count; i++)
            {
                RailAlignmentSample s = alignment.samples[i];
                if (s.stationMetres < low - 0.001f || s.stationMetres > high + 0.001f) { continue; }
                double r = Math.Abs(s.radiusMetres);
                if (r > 0.0 && r < tightest) { tightest = r; }
            }
            return tightest;
        }

        /// <summary> The sample nearest a station. Nearest rather than interpolated: the mast stands at a sampled
        /// point, and interpolating the tangent and radius between two samples would invent a geometry the alignment
        /// never staked -- the same reason the crossing search reads samples rather than re-solving the spline.
        /// </summary>
        private static RailAlignmentSample SampleAt(RailAlignmentPlan alignment, double station)
        {
            int best = 0;
            double bestGap = double.PositiveInfinity;
            for (int i = 0; i < alignment.samples.Count; i++)
            {
                double gap = Math.Abs(alignment.samples[i].stationMetres - station);
                if (gap < bestGap) { bestGap = gap; best = i; }
            }
            return alignment.samples[best];
        }

        /// <summary> The unit vector pointing to the right of travel, in the horizontal plane. The tangent carries its
        /// grade (<see cref="RailAlignmentSample.tangent"/> says so deliberately), and a mast stands plumb, so the
        /// grade is dropped HERE rather than by flattening the sample -- which would take the climb out of the
        /// alignment for everybody. </summary>
        private static Vector3 LateralUnit(Vector3 tangent)
        {
            Vector3 flat = new Vector3(tangent.x, 0f, tangent.z);
            if (flat.sqrMagnitude < 1e-8f) { return Vector3.right; }
            flat.Normalize();
            return new Vector3(flat.z, 0f, -flat.x);
        }

        /// <summary> Which way the track turns at a sample, as +1, -1 or 0. Taken from the SIGNED cant rather than from
        /// the radius, because <see cref="RailAlignmentSample.radiusMetres"/> is unsigned and the cant's sign is
        /// already documented as following the curvature -- so reading the cant is reading the one answer the alignment
        /// gave rather than deriving a second one. </summary>
        private static double CurvatureSign(RailAlignmentSample sample)
        {
            if (double.IsInfinity(sample.radiusMetres)) { return 0.0; }
            if (sample.appliedCantMillimetres > 0.5) { return 1.0; }
            if (sample.appliedCantMillimetres < -0.5) { return -1.0; }
            return 0.0;
        }

        /// <summary> The structure covering a station, or null. The FIRST match wins and the rest are ignored, which is
        /// deliberate: two structures over one station is an authoring error, and taking the lower of the two would
        /// hide it behind a plausible refusal. </summary>
        private static RailOverheadStructure StructureAt(IList<RailOverheadStructure> structures, float station)
        {
            if (structures == null) { return null; }
            for (int i = 0; i < structures.Count; i++)
            {
                if (structures[i] == null) { continue; }
                if (structures[i].Covers(station)) { return structures[i]; }
            }
            return null;
        }

        private static string NameOf(RailOverheadStructure structure)
        {
            return string.IsNullOrEmpty(structure.structureName) ? "" : "\"" + structure.structureName + "\" ";
        }

        private static string Figure(double value)
        {
            if (double.IsInfinity(value)) { return "unbounded"; }
            return value.ToString(Math.Abs(value) < 10.0 ? "0.##" : "0.#", CultureInfo.InvariantCulture);
        }
    }
}
