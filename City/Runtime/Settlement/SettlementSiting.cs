#region "Imports"
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-14. One free road end, as the siting pass needs to see it: everything about a terminus that
    /// decides whether a settlement belongs there, and nothing that needs a scene.
    ///
    /// The author draws and names the network first, and a road's END is where a place begins -- so a terminus is the
    /// INPUT to siting rather than a result of it. C-3's *Build settlements from roads* is what walks the scene and
    /// fills these in: <c>RoadTerminus.IsTerminus</c> decides which ends are free, <see cref="SettlementRoleEnum"/> and
    /// the cross-section class are read off the <c>Road</c>, and the tangent comes from the spline. Everything here is
    /// a plain reading, which is what lets <see cref="SettlementSiting"/> be run by a gate against synthetic ground
    /// with no scene at all. </summary>
    public struct RoadTerminusInput
    {
        /// <summary> The road's own name, exactly as the author sees it in the Hierarchy. Every refusal quotes it,
        /// because the author drew that road and the fix is theirs to make: *road7 ends in water* is actionable where
        /// *a candidate was refused* is not. </summary>
        public string roadName;

        /// <summary> Which end of that road this is. Quoted in every refusal alongside the name, because a road has
        /// two free ends and only one of them may be the one at fault. </summary>
        public RoadEnd whichEnd;

        /// <summary> The settlement-network role the author declared on the road (C-13). This is the axis that fixes
        /// the ORDER of the place proposed here, through <c>SettlementOrderCatalog.TryOrderFor</c> -- and the only one
        /// of the two road axes that can refuse anything. <c>SettlementRoleEnum.None</c> proposes nothing at all. </summary>
        public SettlementRoleEnum role;

        /// <summary> The road's CROSS-SECTION class -- the author's own <c>Road.roadClass</c>. Read here for the
        /// corroboration line only (<see cref="SettlementCandidate.corroboration"/>): it can report that a village is
        /// reached by a motorway, and it can never refuse a candidate or rewrite a lane count. The two axes stay
        /// separate on the author's own instruction and are still read together. </summary>
        public Road.RoadClassEnum crossSection;

        /// <summary> The terminus node's carriageway-centre position, in world metres. Its Y is the node's own, and is
        /// not the figure the water test uses -- see <see cref="roadSurfaceHeightMetres"/> and the ground probe. </summary>
        public Vector3 endNodeCentre;

        /// <summary> The ROAD's surface height at that node, which is where the anchor sits. Sampled ground would sink
        /// the anchor into the shoulder of any raised road, which is the defect <c>RoadTerminus.ComputePlacement</c>
        /// exists to prevent, so the same rule is used here rather than a second one. </summary>
        public float roadSurfaceHeightMetres;

        /// <summary> The road's tangent at that node, in the XZ plane. <c>RoadTerminus.OutwardDirectionXZ</c> turns it
        /// into the outward direction -- the tangent itself at the END node, its reverse at the START -- so which way a
        /// settlement grows away from the road it stands on is decided in one place for both features. </summary>
        public Vector2 tangentAtEndXZ;

        /// <summary> The author's archetype OVERRIDE, straight off <c>CityAuthoring.archetypeOverride</c>, defaulting to
        /// <c>SettlementArchetype.Auto</c>. <c>Auto</c> means *measure it*, which is the contract that component already
        /// documents, so the siting pass measures the archetype from the ground and the water it has to sample anyway
        /// and reports it as <see cref="SettlementCandidate.measuredArchetype"/>. Anything else is an author disagreeing
        /// with that measurement: the override is what takes force, the measurement is still carried, and the inspector
        /// can show both -- which is what makes an override visible AS an override. </summary>
        public SettlementArchetype archetypeOverride;
    }

    /// <summary> B-15 / C-14. How the ground and the water reach the siting pass: one surface reader, one water level and
    /// the spacing they are read on. Nothing else. This is what keeps <see cref="SettlementSiting"/> pure -- it holds no
    /// provider, no terrain and no component, so a gate can hand it synthetic ground and get the same refusals an author
    /// would.
    ///
    /// Both halves are the tool's existing conventions rather than new ones, which is the point of the struct. Ground
    /// arrives through <c>ITerrainSurfaceReader</c>, the same interface <c>SuitabilityMapBuilder</c> reads every sample
    /// through -- the provider in connected mode, the engine's terrain standalone, an array in a gate. Water arrives as
    /// one authored world-Y with <c>float.NegativeInfinity</c> meaning *no water here*, which is exactly what
    /// <c>BuildableLandTest</c> and <c>TunnelRunReader</c> read. Build one with <see cref="FromSurface"/>.
    ///
    /// It does NOT grow the provider surface, and R-5 is not relaxed for it: no member is added to
    /// <c>IPCGDataProvider</c> and nothing under <c>Assets/UniverseCreator/</c> is touched.
    ///
    /// Two different quantities use the two different "not a number" conventions, and they do not clash because they
    /// answer different questions:
    ///   * GROUND -- a value that is not finite means *this tool cannot tell you the ground here*, which is
    ///     <c>RoadNodeGround</c>'s own convention for ground outside any terrain. Unknown ground never reads as dry.
    ///   * WATER -- <c>float.NegativeInfinity</c> means *no water here*, and is the answer for most of a dry world.
    ///     Any other non-finite value means the water is UNKNOWN, which again never reads as dry. </summary>
    public struct SettlementGroundProbe
    {
        /// <summary> Where the ground comes from: the same reader every other sampling pass in the city layer takes, so a
        /// site is judged against the same surface a corridor is priced against. In connected mode that reader is the
        /// provider, in standalone mode the engine's own terrain, and in a gate an array -- and none of the three is a
        /// second convention. A height that is not finite means unknown ground, which this file refuses rather than
        /// reads as dry. </summary>
        public ITerrainSurfaceReader surface;

        /// <summary> The water surface, as one authored world-Y with <c>float.NegativeInfinity</c> for a world with no
        /// water in it. This is the tool's water convention and nothing else: <c>BuildableLandTest.Evaluate</c> and
        /// <c>TunnelRunReader</c> both take exactly this figure, so the site refused here and the lot refused there are
        /// reading one number rather than two copies of one. </summary>
        public float waterSurfaceY;

        /// <summary> Whether <see cref="waterSurfaceY"/> was actually set by a caller. A float has no null, and a probe
        /// left at its default would carry a water level of zero -- which reads as a sea at y = 0 rather than as nobody
        /// having said. The two are different facts and only one of them makes a site safe to accept, so the factory sets
        /// this and <see cref="IsUsable"/> refuses without it. </summary>
        public bool waterStated;

        /// <summary> The spacing the samples are read on, in metres. Tie it to the grid the footprint conform uses --
        /// the region test is specified to be read on that same grid, so the wetted fraction a candidate is refused for
        /// and the cells a footprint is later trimmed against are measurements of one thing rather than two. </summary>
        public float sampleSpacingMetres;

        /// <summary> How many rings of <see cref="sampleSpacingMetres"/> the search for dry ground may walk before it
        /// gives up. It bounds the one unbounded thing in this file: a road ending in the middle of an ocean has no dry
        /// ground to find, and the refusal still has to arrive. </summary>
        public int maxDrySearchRings;

        /// <summary> The ring count <see cref="FromSurface"/> uses: far enough that a road ending in a pond finds its
        /// own bank, and near enough that the answer is bounded. It is a search bound and not a rule about settlements,
        /// which is why it is a plain number here rather than an authored field on an order. </summary>
        public const int DefaultDrySearchRings = 64;

        /// <summary> A probe over a surface reader and the tool's one water level. Pass <c>float.NegativeInfinity</c> as
        /// <paramref name="waterSurfaceY"/> to state that this world has no water, which is a statement the pass will
        /// accept sites on -- unlike silence, which it will not. </summary>
        public static SettlementGroundProbe FromSurface(ITerrainSurfaceReader surface, float waterSurfaceY,
            float spacingMetres)
        {
            SettlementGroundProbe probe = new SettlementGroundProbe();
            probe.surface = surface;
            probe.waterSurfaceY = waterSurfaceY;
            probe.waterStated = true;
            probe.sampleSpacingMetres = spacingMetres;
            probe.maxDrySearchRings = DefaultDrySearchRings;
            return probe;
        }

        /// <summary> Whether this probe can answer at all, naming the first thing that is missing. It refuses an unstated
        /// water level rather than treating it as a dry world, because *nobody wired the water up* and *there is no water*
        /// are different facts and only one of them makes a site safe to accept. </summary>
        public bool IsUsable(out string reason)
        {
            if (surface == null)
            {
                reason = "no surface reader was given to the siting pass, so no candidate can be proved to stand on dry"
                         + " land. Pass SettlementGroundProbe.FromSurface the same ITerrainSurfaceReader the corridor"
                         + " passes read, so a site and a corridor are judged against one surface";
                return false;
            }
            if (!waterStated)
            {
                reason = "no water level was given to the siting pass. That is not the same as a world with no water:"
                         + " pass SettlementGroundProbe.FromSurface with float.NegativeInfinity to state that there is"
                         + " none, so an acceptance reads as a decision instead of an omission";
                return false;
            }
            if (float.IsNaN(waterSurfaceY) || float.IsPositiveInfinity(waterSurfaceY))
            {
                reason = "the siting pass was given a water level of " + waterSurfaceY.ToString("0.###")
                         + " m, which states neither a water surface nor the absence of one. Pass a world-Y, or"
                         + " float.NegativeInfinity for a world with no water";
                return false;
            }
            reason = string.Empty;
            return SpacingIsUsable(out reason);
        }

        /// <summary> The two grid numbers, checked apart from the surface and the water. There is deliberately no minimum
        /// spacing written here: a floor would be a second copy of the one <c>BuildableLandTest</c> already keeps, free to
        /// disagree with it, so a caller that passes nonsense is refused instead of quietly corrected. </summary>
        private bool SpacingIsUsable(out string reason)
        {
            if (!(sampleSpacingMetres > 0f) || float.IsInfinity(sampleSpacingMetres))
            {
                reason = "the siting pass was given a sample spacing of " + sampleSpacingMetres.ToString("0.###")
                         + " m, and ground cannot be read on a grid with no spacing. Pass the spacing the footprint"
                         + " conform uses, so the wetted fraction and the trimmed cells measure the same grid";
                return false;
            }
            if (maxDrySearchRings < 1)
            {
                reason = "the search for dry ground was given " + maxDrySearchRings + " rings, so it would look nowhere"
                         + " and every refusal would report an unknown distance to the nearest bank";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> The ground elevation at an XZ, false when this tool cannot say. A caller must treat false as
        /// *unknown ground*, never as *no ground*: the two lead to opposite decisions. </summary>
        public bool TryGround(float x, float z, out float elevationMetres)
        {
            elevationMetres = surface.SampleHeight(x, z);
            if (float.IsNaN(elevationMetres) || float.IsInfinity(elevationMetres))
            {
                elevationMetres = float.NaN;
                return false;
            }
            return true;
        }

        /// <summary> The water surface, which takes no position because the tool holds one level for the whole world.
        /// Returns false only when the water is UNKNOWN -- which <see cref="IsUsable"/> has already refused, so a caller
        /// that got past that guard will not see it; a world with no water returns true with <paramref name="hasWater"/>
        /// false, the ordinary answer for dry ground. </summary>
        public bool TryWater(out float elevationMetres, out bool hasWater)
        {
            elevationMetres = waterSurfaceY;
            hasWater = false;
            if (float.IsNegativeInfinity(elevationMetres)) { return true; }
            if (float.IsNaN(elevationMetres) || float.IsPositiveInfinity(elevationMetres))
            {
                elevationMetres = float.NaN;
                return false;
            }
            hasWater = true;
            return true;
        }
    }

    /// <summary> B-15 / C-14. One PROPOSED settlement site, with every number that was measured to reach the proposal
    /// and, when it was refused, the reason in the author's terms. A candidate is not a settlement: C-3's *Build
    /// settlements from roads* is what turns an accepted candidate into a <c>CityAuthoring</c> component, and C-4's
    /// <c>SiteTestOperator</c> is what weighs the ground inside the region it proposes.
    ///
    /// Refused candidates are RETURNED, not dropped. An author who drew a road into a lake needs to be told which road
    /// and why; a siting pass that silently returned three sites instead of four would leave them looking for the
    /// fourth. Every refusal names the road and the end, and carries the measurement it turned on. </summary>
    public struct SettlementCandidate
    {
        /// <summary> The road whose free end this is, and which end -- carried on every candidate so a refusal is
        /// addressed to the drawing that caused it. </summary>
        public string roadName;

        /// <summary> Which of the road's two ends the anchor stands at. </summary>
        public RoadEnd whichEnd;

        /// <summary> The role the road declared, which is what fixed the order. </summary>
        public SettlementRoleEnum role;

        /// <summary> The road's cross-section class, read for <see cref="corroboration"/> and for nothing else. </summary>
        public Road.RoadClassEnum crossSection;

        /// <summary> The settlement order this site is proposed at -- 1 capital, 2 city, 3 town, 4 village, as
        /// <c>SettlementOrderCatalog</c> numbers them. Derived from <see cref="role"/> through the catalogue, never
        /// typed here. </summary>
        public int orderIndex;

        /// <summary> The order's own display name, so a report reads *village* rather than *order 4*. </summary>
        public string orderName;

        /// <summary> The archetype MEASURED from the ground and the water around this site, which is what
        /// <c>CityAuthoring.archetypeOverride</c>'s <c>Auto</c> default promises. Carried even when an author has
        /// overridden it, so an inspector can show the measurement beside the override. </summary>
        public SettlementArchetype measuredArchetype;

        /// <summary> The archetype IN FORCE: the author's override where they set one, the measurement otherwise. This
        /// is the one the wetted-fraction ceiling is read for. </summary>
        public SettlementArchetype archetype;

        /// <summary> Where the settlement starts: the terminus itself, at the ROAD's surface height rather than sampled
        /// ground, through <c>RoadTerminus.ComputePlacement</c>. </summary>
        public Vector3 anchor;

        /// <summary> The horizontal direction the settlement grows in: away from the road it stands on, from
        /// <c>RoadTerminus.OutwardDirectionXZ</c>. </summary>
        public Vector2 outwardXZ;

        /// <summary> The yaw that faces <see cref="outwardXZ"/>, from <c>RoadTerminus.YawDegreesFor</c>, so a marker or
        /// a gateway placed at the anchor squares to the road that arrives. </summary>
        public float yawDegrees;

        /// <summary> The population the region was sized for: the order's own reference population. C-14 does not rank
        /// candidates against each other -- the rank-size distribution is applied by <c>SiteRankOperator</c>, which is
        /// where a candidate stops being one of four road ends and becomes the second-largest city -- so the honest
        /// figure at this stage is the order's reference place. </summary>
        public int proposedPopulation;

        /// <summary> The built-up area that population implies, in square metres, from the order's sub-linear allometry
        /// (<c>SettlementOrderClass.BuiltAreaM2</c>). At the reference population this IS the order's fitted reference
        /// area, which is what the spec means by a region sized from the order's fitted area. </summary>
        public float proposedBuiltAreaM2;

        /// <summary> The proposed region in WORLD XZ metres: a square of <see cref="proposedBuiltAreaM2"/>, laid with
        /// the anchor on its near edge so it reaches AWAY from the road rather than back over it. This is the rect the
        /// water was measured in and the rect C-4 weighs. </summary>
        public Rect regionWorldXZ;

        /// <summary> The same region in the convention <c>CityAuthoring.regionXZ</c> serialises -- relative to the
        /// settlement's centre Transform -- on the understanding that C-3 places that component AT
        /// <see cref="anchor"/>. Both forms are carried because both conventions already exist in shipped code, and
        /// converting between them at each call site is where a sign error would live. </summary>
        public Rect regionRelativeXZ;

        /// <summary> Ground elevation at the anchor, in metres, or <c>float.NaN</c> where the tool could not say. Note
        /// that this is NOT <c>anchor.y</c>: the anchor rides at the road's surface height, and the water test is a
        /// question about the ground under it. </summary>
        public float anchorGroundElevation;

        /// <summary> Water-surface elevation at the anchor, <c>float.NegativeInfinity</c> for dry, <c>float.NaN</c> for
        /// unknown. Quoted in the refusal when it stands above <see cref="anchorGroundElevation"/>. </summary>
        public float anchorWaterElevation;

        /// <summary> Metres to the nearest sample this pass could prove dry, when the anchor itself was not, or
        /// <c>float.PositiveInfinity</c> when the search found none. It is in the refusal because it is the number that
        /// tells the author how far to drag the road end. </summary>
        public float nearestDryMetres;

        /// <summary> The fraction of the proposed region that stands under water, measured on the probe's grid. </summary>
        public float wettedFraction;

        /// <summary> The fraction of the region whose ground this tool could not read. It is reported apart from
        /// <see cref="wettedFraction"/> and then counted WITH it against the ceiling: unusable ground is unusable
        /// whether it is wet or merely unknown, and a site the tool cannot prove is dry is not a site. </summary>
        public float unprovenFraction;

        /// <summary> The ceiling those two were compared against -- authored per order and per archetype in
        /// <c>SettlementOrderCatalog</c>, so a capital cannot be mostly water while a fishing village on a spit
        /// legitimately is nearly all edge. It is not decided in this file. </summary>
        public float wettedCeiling;

        /// <summary> Reilly's breakpoint in metres, measured from the larger neighbour that came closest to swallowing
        /// this site, or <c>float.NaN</c> where there was no larger neighbour to compare with. </summary>
        public float breakpointMetres;

        /// <summary> The road end that larger neighbour belongs to, so a catchment refusal names both places. </summary>
        public string catchmentNeighbour;

        /// <summary> Whether this site survived every test. False means <see cref="refusal"/> says why, in the author's
        /// terms, with the measurement in it. </summary>
        public bool accepted;

        /// <summary> Why this site was refused, or empty when it was not. </summary>
        public string refusal;

        /// <summary> What the two road axes said about each other -- empty when they agree. Never a refusal: a village
        /// reached by a motorway is a real thing an author may have meant, and the cross-section is theirs. </summary>
        public string corroboration;

        /// <summary> The road end in one phrase, for a report line or a refusal. </summary>
        public string Identity
        {
            get { return SettlementSiting.IdentityOf(roadName, whichEnd); }
        }

        /// <summary> One report line: what was proposed, where, how big, and either the water it holds or the reason it
        /// was refused. The corroboration line is appended when there is one, because it is a remark about the same
        /// candidate and an author reading a report wants it in the same place. Every figure is formatted by the one helper
        /// that formats it, so a report line and a refusal never write the same measurement two ways. </summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append(Identity).Append(": ");
            text.Append(string.IsNullOrEmpty(orderName) ? ("order " + orderIndex) : orderName);
            text.Append(" proposed at ").Append(SettlementSiting.PlaceXZ(anchor.x, anchor.z)).Append(", ");
            text.Append(SettlementSiting.SidePhrase(regionWorldXZ)).Append(" for ")
                .Append(proposedPopulation.ToString("n0")).Append(" people");
            if (accepted)
            {
                text.Append(" -- accepted as ").Append(archetype);
                if (measuredArchetype != archetype)
                {
                    text.Append(" (the ground measures ").Append(measuredArchetype).Append(")");
                }
                text.Append(", holding ").Append(SettlementSiting.Percent(wettedFraction)).Append(" water");
                if (unprovenFraction > 0f)
                {
                    text.Append(" and ").Append(SettlementSiting.Percent(unprovenFraction))
                        .Append(" unreadable ground");
                }
                text.Append(" against a ").Append(SettlementSiting.Percent(wettedCeiling)).Append(" ceiling");
            }
            else
            {
                text.Append(" -- REFUSED: ").Append(string.IsNullOrEmpty(refusal) ? "no reason was recorded" : refusal);
            }
            if (!string.IsNullOrEmpty(corroboration)) { text.Append(". ").Append(corroboration); }
            text.Append(".");
            return text.ToString();
        }
    }

    /// <summary> B-15 / C-14. Candidate settlement sites, from road ends. Pure and static: it reads no scene, creates no
    /// component, writes no asset and draws nothing. Given the free ends the author drew and the role each road
    /// declares, it produces a list of <see cref="SettlementCandidate"/> -- anchor, outward direction, order, and the
    /// PROPOSED region -- and refuses the ones that should not exist, each with the measurement it turned on.
    ///
    /// It computes; it does not create. C-3's *Build settlements from roads* is the editor action that turns an accepted
    /// candidate into a <c>CityAuthoring</c> component, and C-4's <c>SiteTestOperator</c> is what weighs the ground
    /// inside the region and accepts or rejects the site on suitability. This file answers the questions that come
    /// BEFORE either: is there ground here at all, and should a place exist here given the others.
    ///
    /// **Water is the first test, and it is a refusal rather than a trim.** *"make sure never in lakes or inside ocean
    /// okay"*. <c>FootprintConform.ClassifyCell</c> removes water cells, but it does that to a footprint already
    /// proposed -- it trims, and a trim has no way to say no. So the anchor is tested here, before a zone is chosen,
    /// before a street is laid and before C-4 runs a single stage, and a road ending in water is reported as exactly
    /// that: the road's name, which end, the water elevation, the ground under it, and how far the nearest dry ground
    /// is. Water is present at an XZ when the authored water elevation there is a real number standing ABOVE the ground
    /// at that same XZ -- one sentence that covers a lake and an ocean alike, and the same sentence the shoreline
    /// setback uses, which is what keeps the two consistent. The rule bans a site IN water and never a site BESIDE it:
    /// ports, beaches and lakesides are the point, and a coast with no towns on it is not a real coast.
    ///
    /// **Both road axes are read, and only one of them can refuse.** <see cref="SettlementRoleEnum"/> fixes the order
    /// through the catalogue. The cross-section class is read alongside it and produces a corroboration line -- a
    /// village reached by a motorway, a capital reached by a two-lane street -- which is reported by name and never
    /// rewrites the author's road or rejects the site.
    ///
    /// **What is NOT decided here.** The population band, the spacing band, the area exponent and the wetted ceiling are
    /// all authored in <c>SettlementOrderCatalog</c> (C-15) and fitted from real places in <c>SettlementFitTable</c>
    /// (C-16); this file reads them and never carries a copy. Which candidate becomes the largest city is
    /// <c>SiteRankOperator</c>'s, through the rank-size law. Nothing here reaches for <c>System.Random</c> or the
    /// clock: every number below is a measurement or a reading, and the same scene yields the same candidates. </summary>
    public static class SettlementSiting
    {
        /// <summary> The fewest samples the region is read on along each side. A wetted fraction taken from one sample is
        /// not a fraction, and the probe's spacing is chosen for the footprint grid rather than for a village-sized
        /// square, so the step is tightened where the region is small. It is never loosened. </summary>
        public const int MinimumRegionSamplesPerSide = 8;

        /// <summary> The most samples any single sweep takes along one side, which bounds the cost of this pass no
        /// matter how large a capital's region or how fine an author's grid. It is a budget, not a rule about
        /// settlements. </summary>
        public const int MaximumSamplesPerSide = 64;

        /// <summary> How far around the site the archetype measurement looks, as a multiple of the region's own width.
        /// Three widths is far enough that a pond inside the region reads as enclosed and an ocean does not, and it is a
        /// bound rather than a threshold: nothing is accepted or refused on it. </summary>
        public const int ShorelineWindowInRegionWidths = 3;

        /// <summary> Directions sampled around a shore point when measuring shelter -- 36 is one every 10 degrees. </summary>
        public const int ShelterRingSamples = 36;

        /// <summary> The wet share of a full circle around a point on a straight shoreline: half of it, which is
        /// geometry rather than a tuned figure. More water than this around a shore point means the water wraps the
        /// land -- an inlet, a bay, a harbour; less means the land wraps the water, an open or convex coast. This is the
        /// one line that separates a port from a beach, and it is a measurement of the author's own ground. </summary>
        public const float StraightShoreWetFraction = 0.5f;

        /// <summary> The most report lines a single run prints in full before it starts counting, so a scene with two
        /// hundred road ends produces a report an author reads rather than a wall they scroll past. </summary>
        public const int MaximumReportedLines = 24;

        /// <summary> Propose a settlement site for every road end that declares a settlement role, refusing the ones that
        /// stand in water, hold too much water, or fall inside a larger neighbour's catchment. Returns how many were
        /// ACCEPTED; <paramref name="candidatesOut"/> receives accepted and refused alike, in the order the termini were
        /// given, because an author who drew a road into a lake has to be told which road.
        ///
        /// <paramref name="reportOut"/> is the author-facing summary, ready to hand to <c>Debug.Log</c> by the caller --
        /// this file logs nothing itself, so a gate can read the same words without the console filling up. </summary>
        public static int Propose(IList<RoadTerminusInput> termini, SettlementOrderCatalog catalog,
            SettlementGroundProbe probe, IList<SettlementCandidate> candidatesOut, out string reportOut)
        {
            if (candidatesOut == null)
            {
                reportOut = "Road & City Master: the siting pass was given nowhere to put its candidates, so it did"
                            + " nothing. This is a caller fault, not an authoring one";
                return 0;
            }
            candidatesOut.Clear();
            if (catalog == null)
            {
                reportOut = "Road & City Master: no settlement order catalogue was given to the siting pass, so no road"
                            + " role can be turned into an order and no region can be sized. Assign one on the city, or"
                            + " create one through Assets > Create > Road City Master > Settlement Order Catalog";
                return 0;
            }
            string probeReason;
            if (!probe.IsUsable(out probeReason))
            {
                reportOut = "Road & City Master: the siting pass cannot read the ground -- " + probeReason + ".";
                return 0;
            }
            return ProposeChecked(termini, catalog, probe, candidatesOut, out reportOut);
        }

        /// <summary> One road end in one phrase. The one place that phrase is built, so a refusal, a report line and a
        /// skipped-road note all name a road the same way. </summary>
        public static string IdentityOf(string roadName, RoadEnd whichEnd)
        {
            return (string.IsNullOrEmpty(roadName) ? "an unnamed road" : roadName) + " at its " + whichEnd + " end";
        }

        /// <summary> The pass itself, once the caller's arguments are known to be answerable. Split from
        /// <see cref="Propose"/> so the guards read as guards rather than as the first third of a long method. </summary>
        private static int ProposeChecked(IList<RoadTerminusInput> termini, SettlementOrderCatalog catalog,
            SettlementGroundProbe probe, IList<SettlementCandidate> candidatesOut, out string reportOut)
        {
            List<string> skipped = new List<string>();
            int roleless = 0;
            int terminusCount = termini == null ? 0 : termini.Count;
            for (int i = 0; i < terminusCount; i++)
            {
                RoadTerminusInput terminus = termini[i];

                // A road that declares no role is not part of the settlement network and seeds nothing. It is counted
                // rather than named: in a scene of two hundred roads, most of them are this, and a report that listed
                // them all would bury the four lines that matter.
                if (terminus.role == SettlementRoleEnum.None) { roleless++; continue; }

                SettlementOrderClass order;
                string orderReason;
                if (!catalog.TryOrderFor(terminus.role, out order, out orderReason))
                {
                    skipped.Add(IdentityOf(terminus.roadName, terminus.whichEnd) + " proposes nothing: " + orderReason);
                    continue;
                }
                candidatesOut.Add(BuildCandidate(terminus, order, catalog, probe));
            }

            // The catchment comparison is a question about the SET, so it runs once the whole set exists.
            int refusedByCatchment = ApplyCatchmentRejections(candidatesOut);

            int accepted = 0;
            for (int i = 0; i < candidatesOut.Count; i++)
            {
                if (candidatesOut[i].accepted) { accepted++; }
            }
            reportOut = BuildReport(candidatesOut, accepted, roleless, refusedByCatchment, skipped);
            return accepted;
        }

        /// <summary> One candidate, from one terminus and the order its role selected: the readings first, then the two
        /// water tests in the order the rule sets them -- the anchor, then the region -- and nothing generated for a site
        /// that fails either. </summary>
        private static SettlementCandidate BuildCandidate(RoadTerminusInput terminus, SettlementOrderClass order,
            SettlementOrderCatalog catalog, SettlementGroundProbe probe)
        {
            TerminusPlacement placement = RoadTerminus.ComputePlacement(terminus.endNodeCentre,
                terminus.roadSurfaceHeightMetres, terminus.tangentAtEndXZ, terminus.whichEnd);

            SettlementCandidate candidate = new SettlementCandidate();
            candidate.roadName = terminus.roadName;
            candidate.whichEnd = terminus.whichEnd;
            candidate.role = terminus.role;
            candidate.crossSection = terminus.crossSection;
            candidate.orderIndex = order.orderIndex;
            candidate.orderName = order.displayName;
            candidate.anchor = placement.position;
            candidate.yawDegrees = placement.yawDegrees;
            candidate.outwardXZ = RoadTerminus.OutwardDirectionXZ(terminus.tangentAtEndXZ, terminus.whichEnd);
            candidate.proposedPopulation = order.referencePopulation;
            candidate.proposedBuiltAreaM2 = order.BuiltAreaM2(order.referencePopulation);
            candidate.anchorGroundElevation = float.NaN;
            candidate.anchorWaterElevation = float.NaN;
            candidate.nearestDryMetres = float.NaN;
            candidate.breakpointMetres = float.NaN;
            candidate.catchmentNeighbour = string.Empty;
            candidate.refusal = string.Empty;
            candidate.corroboration = Corroboration(terminus.crossSection, order);

            if (!(candidate.proposedBuiltAreaM2 > 0f))
            {
                candidate.refusal = "the " + OrderName(candidate) + " order gives a reference place of "
                    + order.referencePopulation.ToString("n0") + " people " + order.referenceBuiltAreaM2.ToString("0")
                    + " m2 of built-up ground, so there is no region to propose. Both figures are authored on the"
                    + " settlement order catalogue and both have to be above zero";
                return candidate;
            }
            candidate.regionWorldXZ = RegionFor(candidate.anchor, candidate.outwardXZ, candidate.proposedBuiltAreaM2);
            candidate.regionRelativeXZ = new Rect(candidate.regionWorldXZ.x - candidate.anchor.x,
                candidate.regionWorldXZ.y - candidate.anchor.z, candidate.regionWorldXZ.width,
                candidate.regionWorldXZ.height);
            return RunWaterTests(catalog, probe, terminus.archetypeOverride, candidate);
        }

        /// <summary> The order's own display name where it has one. A blank name is an authoring slip on the catalogue and
        /// the message says so rather than printing nothing. </summary>
        private static string OrderName(SettlementCandidate candidate)
        {
            return OrderNameOf(candidate.orderName, candidate.orderIndex);
        }

        /// <summary> The same name, for the one caller that holds the order rather than a candidate. </summary>
        private static string OrderNameOf(string displayName, int orderIndex)
        {
            return string.IsNullOrEmpty(displayName) ? "unnamed order " + orderIndex : displayName;
        }

        /// <summary> The proposed region: a square of the order's fitted built-up area, standing on the road end rather than
        /// straddling it. The anchor sits at the middle of the near edge and the square runs outward along the road's own
        /// direction, because the settlement begins where the road ends and does not spread back down it. C-3 writes the
        /// relative form of this into <c>CityAuthoring.regionXZ</c>; C-4 weighs the world form. Public so the gate can assert
        /// the side length and the offset instead of trusting this comment. </summary>
        /// <remarks> A square is the shape the fitted numbers support. <c>SettlementOrderClass.BuiltAreaM2</c> returns an
        /// area and nothing about elongation, so any other aspect ratio here would be invented. C-4's operators reshape it
        /// against the ground; this is the proposal they start from. A zero <paramref name="outwardXZ"/> centres the square on
        /// the anchor, which is the degenerate answer rather than a second copy of RoadTerminus's own fallback. </remarks>
        public static Rect RegionFor(Vector3 anchor, Vector2 outwardXZ, float builtAreaM2)
        {
            float side = builtAreaM2 > 0f ? Mathf.Sqrt(builtAreaM2) : 0f;
            float half = side * 0.5f;
            Vector2 centre = new Vector2(anchor.x + outwardXZ.x * half, anchor.z + outwardXZ.y * half);
            return new Rect(centre.x - half, centre.y - half, side, side);
        }

        /// <summary> The two water tests, in the order the rule fixes: the anchor, then the region. The archetype is measured
        /// between them, because the ceiling the region is judged against depends on which archetype is in force, and the
        /// author's override -- anything other than <c>Auto</c> -- is what decides that. Both readings are kept on the
        /// candidate whether it passes or fails, so a refusal can be read without re-sampling anything. </summary>
        private static SettlementCandidate RunWaterTests(SettlementOrderCatalog catalog, SettlementGroundProbe probe,
            SettlementArchetype archetypeOverride, SettlementCandidate candidate)
        {
            if (!AnchorIsDry(probe, ref candidate)) { return candidate; }
            MeasureRegionWater(probe, ref candidate);
            candidate.measuredArchetype = MeasureArchetype(probe, candidate);
            candidate.archetype = archetypeOverride == SettlementArchetype.Auto
                ? candidate.measuredArchetype : archetypeOverride;
            if (!RegionWaterIsWithinCeiling(catalog, ref candidate)) { return candidate; }
            candidate.accepted = true;
            return candidate;
        }

        /// <summary> A position, written the way every message in this file writes one: metres, on the ground plane, rounded to
        /// the metre because a settlement anchor is not a millimetre measurement. One copy, so the report and a refusal never
        /// disagree about the format. </summary>
        public static string PlaceXZ(float x, float z)
        {
            return "(" + x.ToString("0") + ", " + z.ToString("0") + ") m";
        }

        /// <summary> What one sample says about one spot: dry ground, water, or a reading the tool will not guess at. The
        /// comparison that decides wet from dry lives here and nowhere else in this file. </summary>
        private enum SampleState
        {
            /// <summary> Ground the tool can prove stands above any water surface at this spot, or ground with no water over
            /// it at all. </summary>
            Dry = 0,

            /// <summary> A water surface at or above the ground. </summary>
            Wet = 1,

            /// <summary> No ground here -- off the terrain, or terrain the probe cannot answer for. Never read as dry. </summary>
            GroundUnreadable = 2,

            /// <summary> Ground, but a water figure that is not an elevation and not the negative infinity that means no water.
            /// Never read as dry either. </summary>
            WaterUnreadable = 3
        }

        /// <summary> One sample, classified. <paramref name="ground"/> and <paramref name="water"/> come back as read so a
        /// caller can put real numbers in a message instead of sampling twice. </summary>
        private static SampleState Classify(SettlementGroundProbe probe, float x, float z, out float ground,
            out float water, out bool hasWater)
        {
            water = float.NaN;
            hasWater = false;
            if (!probe.TryGround(x, z, out ground)) { return SampleState.GroundUnreadable; }
            if (!probe.TryWater(out water, out hasWater)) { return SampleState.WaterUnreadable; }
            if (!hasWater) { return SampleState.Dry; }
            return ground > water ? SampleState.Dry : SampleState.Wet;
        }

        /// <summary> Never in a lake, never in the sea: the anchor is sampled before anything else is computed, and a road end
        /// standing in water is refused outright with the water elevation and the distance to the nearest dry ground in the
        /// message. Ground that cannot be read is refused too -- a site the tool cannot prove is dry is not a site -- and so is
        /// water that reads back as a number the tool cannot compare. </summary>
        /// <remarks> The test is whether the ground stands above the water surface at all. The freeboard margin -- how far
        /// above it a building needs to be -- belongs to <c>BuildableLandTest.BuildabilityCriteria.waterFreeboard</c> and is
        /// not re-declared here, because a second copy of that number would be free to disagree with the first. </remarks>
        private static bool AnchorIsDry(SettlementGroundProbe probe, ref SettlementCandidate candidate)
        {
            string at = PlaceXZ(candidate.anchor.x, candidate.anchor.z);
            float ground;
            float water;
            bool hasWater;
            SampleState state = Classify(probe, candidate.anchor.x, candidate.anchor.z, out ground, out water,
                out hasWater);
            candidate.anchorGroundElevation = ground;
            if (hasWater) { candidate.anchorWaterElevation = water; }

            if (state == SampleState.GroundUnreadable)
            {
                candidate.refusal = "the ground at the road end " + at + " cannot be read, so the tool cannot show that this"
                    + " site is dry. Either the terrain does not reach this far or the road end hangs off it";
                return false;
            }
            if (state == SampleState.WaterUnreadable)
            {
                candidate.refusal = "the water surface at the road end " + at + " reads back as a figure the tool cannot"
                    + " compare against the ground at " + ground.ToString("0.0") + " m. A world with no water reports"
                    + " negative infinity; anything else has to be a real elevation";
                return false;
            }
            if (state == SampleState.Dry) { return true; }

            candidate.nearestDryMetres = NearestDryMetres(probe, candidate.anchor);
            candidate.refusal = "the road end " + at + " stands in water: the ground is at " + ground.ToString("0.0")
                + " m and the water surface is at " + water.ToString("0.0") + " m, " + (water - ground).ToString("0.0")
                + " m above it. " + DryGroundPhrase(probe, candidate.nearestDryMetres) + ". No settlement is placed in a lake"
                + " or in the sea, so move this road end onto dry ground";
            return false;
        }

        /// <summary> How far the author has to move the road end: the distance to the first dry sample found on rings stepping
        /// outward at the probe's own sample spacing, <c>ShelterRingSamples</c> directions to a ring. <c>float.NaN</c> when the
        /// search reached its last ring without finding one. </summary>
        /// <remarks> This is a figure for the message, not a decision. It is the nearest dry ground the fan happened to cross,
        /// so on a coarse spacing or a narrow spit there may be dry ground nearer than it reports -- which is why every phrase
        /// built from it says "about". The angular resolution is the one constant the shelter ring uses, so the two fans in this
        /// file cannot drift apart. Cost is bounded: rings times directions, and both are capped on the probe. </remarks>
        private static float NearestDryMetres(SettlementGroundProbe probe, Vector3 from)
        {
            float radiansPerStep = (Mathf.PI * 2f) / ShelterRingSamples;
            for (int ring = 1; ring <= probe.maxDrySearchRings; ring++)
            {
                float radius = probe.sampleSpacingMetres * ring;
                for (int i = 0; i < ShelterRingSamples; i++)
                {
                    float radians = radiansPerStep * i;
                    float ground;
                    float water;
                    bool hasWater;
                    SampleState state = Classify(probe, from.x + Mathf.Cos(radians) * radius,
                        from.z + Mathf.Sin(radians) * radius, out ground, out water, out hasWater);
                    if (state == SampleState.Dry) { return radius; }
                }
            }
            return float.NaN;
        }

        /// <summary> The sentence a refusal uses to say where the dry ground is, including the honest version for when the
        /// search found none inside its own reach. </summary>
        private static string DryGroundPhrase(SettlementGroundProbe probe, float nearestDryMetres)
        {
            if (float.IsNaN(nearestDryMetres))
            {
                return "No dry ground was found within " + (probe.sampleSpacingMetres * probe.maxDrySearchRings)
                    .ToString("0") + " m of it";
            }
            return "The nearest dry ground is about " + nearestDryMetres.ToString("0") + " m away";
        }

        /// <summary> How much of the proposed region is water, and how much of it is ground the tool cannot read: a grid sweep
        /// over the region, counted as fractions of the samples taken. Both fractions land on the candidate whether or not they
        /// pass the ceiling, because the author is owed the figure and not only the verdict. </summary>
        /// <remarks> The grid is sized from the probe's own sample spacing so the region is read at the resolution the world is
        /// readable at, then held between two bounds. <c>MinimumRegionSamplesPerSide</c> raises it on a small region, tightening
        /// the step: a fraction taken from one or two samples is not a fraction. <c>MaximumSamplesPerSide</c> caps it on a large
        /// one, which does coarsen the step below the probe's spacing -- that bound is a cost budget, and a fraction of samples
        /// on an evenly spaced grid is still an unbiased reading of the fraction of the area, only a grainier one. </remarks>
        private static void MeasureRegionWater(SettlementGroundProbe probe, ref SettlementCandidate candidate)
        {
            int perSide = SamplesPerSide(candidate.regionWorldXZ, probe.sampleSpacingMetres);
            float stepX = candidate.regionWorldXZ.width / (perSide - 1);
            float stepZ = candidate.regionWorldXZ.height / (perSide - 1);
            int wet = 0;
            int unproven = 0;
            int taken = 0;
            for (int ix = 0; ix < perSide; ix++)
            {
                float x = candidate.regionWorldXZ.xMin + stepX * ix;
                for (int iz = 0; iz < perSide; iz++)
                {
                    float ground;
                    float water;
                    bool hasWater;
                    SampleState state = Classify(probe, x, candidate.regionWorldXZ.yMin + stepZ * iz, out ground,
                        out water, out hasWater);
                    taken++;
                    if (state == SampleState.Wet) { wet++; }
                    else if (state != SampleState.Dry) { unproven++; }
                }
            }
            candidate.wettedFraction = taken > 0 ? (float)wet / taken : 0f;
            candidate.unprovenFraction = taken > 0 ? (float)unproven / taken : 0f;
        }

        /// <summary> The side of the sample grid: one sample per unit of the probe's spacing across the region, plus the closing
        /// edge, held between the two bounds. Public so the gate can assert the bounds hold at both extremes. </summary>
        public static int SamplesPerSide(Rect regionWorldXZ, float sampleSpacingMetres)
        {
            float side = Mathf.Max(regionWorldXZ.width, regionWorldXZ.height);
            int wanted = sampleSpacingMetres > 0f ? Mathf.CeilToInt(side / sampleSpacingMetres) + 1 : 0;
            return Mathf.Clamp(wanted, MinimumRegionSamplesPerSide, MaximumSamplesPerSide);
        }

        /// <summary> The second water test: a candidate that stands on dry ground can still be a settlement sitting mostly in a
        /// bay, so the region's wetted fraction is held against the ceiling its order and archetype allow. Ground the tool cannot
        /// read counts with the water, because ground it cannot read is not ground it may build on. </summary>
        /// <remarks> The ceiling comes from the catalogue and is fail-closed there: a missing ceiling refuses rather than
        /// permits, and this method passes that refusal through in the author's own terms. The four figures behind it are
        /// authored per order on <c>SettlementOrderClass</c> and are not restated here. </remarks>
        private static bool RegionWaterIsWithinCeiling(SettlementOrderCatalog catalog, ref SettlementCandidate candidate)
        {
            float ceiling;
            string reason;
            if (!catalog.TryWettedCeiling(candidate.orderIndex, candidate.archetype, out ceiling, out reason))
            {
                candidate.wettedCeiling = float.NaN;
                candidate.refusal = "the settlement order catalogue gives no water limit for a " + OrderName(candidate)
                    + " of archetype " + candidate.archetype + ": " + reason + ". Until it does, the site is refused rather"
                    + " than admitted on a limit nobody set";
                return false;
            }
            candidate.wettedCeiling = ceiling;
            float held = candidate.wettedFraction + candidate.unprovenFraction;
            if (held <= ceiling) { return true; }

            candidate.refusal = Percent(candidate.wettedFraction) + " of the proposed region "
                + SidePhrase(candidate.regionWorldXZ) + " around " + PlaceXZ(candidate.regionWorldXZ.center.x,
                candidate.regionWorldXZ.center.y) + " is water"
                + (candidate.unprovenFraction > 0f
                    ? " and " + Percent(candidate.unprovenFraction) + " is ground the tool cannot read" : string.Empty)
                + ", against the " + Percent(ceiling) + " a " + OrderName(candidate) + " of archetype "
                + candidate.archetype + " admits. The road end is dry but the settlement would not be, so either move it"
                + " inland or let a smaller order seed this end";
            return false;
        }

        /// <summary> A fraction, written as the percentage every message in this file writes. </summary>
        public static string Percent(float fraction)
        {
            return (fraction * 100f).ToString("0.0") + " %";
        }

        /// <summary> A region's extent, in metres, written once. Public because <c>SettlementCandidate.Describe</c> writes the
        /// same phrase and the two must not drift apart. </summary>
        public static string SidePhrase(Rect regionWorldXZ)
        {
            return regionWorldXZ.width.ToString("0") + " m by " + regionWorldXZ.height.ToString("0") + " m";
        }

        /// <summary> Which of the four archetypes the ground itself says this is. <c>CityAuthoring.archetypeOverride</c> left at
        /// <c>Auto</c> means "measure it", and this is the measurement: the water adjacency the region sweep already had to read,
        /// widened to a window <c>ShorelineWindowInRegionWidths</c> region-widths across so a shoreline can be seen leaving the
        /// settlement. Never returns <c>Auto</c>; <c>Auto</c> is a request, not an answer. </summary>
        /// <remarks> Three questions, in the order of how much they can be relied on. No water anywhere in the window and the
        /// site is inland: <c>Plain</c>. Water that never reaches the window's border is a body the tool can see all of, which
        /// is a lake: <c>Lakeside</c>. Water that runs off the border is a body larger than the window, so the split is between
        /// a sheltered edge and an exposed one, measured on a ring about the nearest water: a straight shoreline puts exactly
        /// half of that ring in the water, water partly enclosed by land puts less than half -- that is a harbour, so
        /// <c>Port</c> -- and anything at or above the straight-shore half faces open water, so <c>Beach</c>.
        /// <para> What it cannot know, and does not pretend to: a lake wider than the window reads as a coast, and a sheltered
        /// inlet of a lake reads as a port. Both are statements about what is measurable in the window, not about the world, and
        /// the author's override on the settlement component is the field for saying otherwise -- which is why the inspector
        /// shows the measured archetype beside the one in force rather than replacing it. </para></remarks>
        private static SettlementArchetype MeasureArchetype(SettlementGroundProbe probe, SettlementCandidate candidate)
        {
            Rect window = WindowFor(candidate.regionWorldXZ);
            bool anyWet;
            bool reachesBorder;
            Vector2 nearestWet;
            SurveyWindow(probe, window, candidate.regionWorldXZ.center, out anyWet, out reachesBorder, out nearestWet);
            if (!anyWet) { return SettlementArchetype.Plain; }
            if (!reachesBorder) { return SettlementArchetype.Lakeside; }

            float side = Mathf.Max(candidate.regionWorldXZ.width, candidate.regionWorldXZ.height);
            float wetArc = ShelterWetFraction(probe, nearestWet, side);
            return wetArc < StraightShoreWetFraction ? SettlementArchetype.Port : SettlementArchetype.Beach;
        }

        /// <summary> The window the archetype is measured in: the proposed region, concentric, <c>ShorelineWindowInRegionWidths</c>
        /// times as wide. Public so the gate can assert the width instead of trusting this comment. </summary>
        public static Rect WindowFor(Rect regionWorldXZ)
        {
            float width = regionWorldXZ.width * ShorelineWindowInRegionWidths;
            float height = regionWorldXZ.height * ShorelineWindowInRegionWidths;
            Vector2 centre = regionWorldXZ.center;
            return new Rect(centre.x - width * 0.5f, centre.y - height * 0.5f, width, height);
        }

        /// <summary> The window sweep behind the archetype: whether any of it is water, whether that water runs off the window's
        /// border, and the water sample nearest the region. </summary>
        /// <remarks> Only samples the tool can prove are water count as water here. A sample it cannot read makes the site
        /// neither a coast nor an inland one -- it has already counted against the site in the ceiling test, and counting it
        /// twice, as a shoreline it was never shown to be, would put the wrong archetype in the inspector. </remarks>
        private static void SurveyWindow(SettlementGroundProbe probe, Rect window, Vector2 nearest, out bool anyWet,
            out bool reachesBorder, out Vector2 nearestWet)
        {
            int perSide = SamplesPerSide(window, probe.sampleSpacingMetres);
            float stepX = window.width / (perSide - 1);
            float stepZ = window.height / (perSide - 1);
            anyWet = false;
            reachesBorder = false;
            nearestWet = nearest;
            float bestSquareMetres = float.PositiveInfinity;
            for (int ix = 0; ix < perSide; ix++)
            {
                float x = window.xMin + stepX * ix;
                for (int iz = 0; iz < perSide; iz++)
                {
                    float z = window.yMin + stepZ * iz;
                    float ground;
                    float water;
                    bool hasWater;
                    if (Classify(probe, x, z, out ground, out water, out hasWater) != SampleState.Wet) { continue; }
                    anyWet = true;
                    if (ix == 0 || iz == 0 || ix == perSide - 1 || iz == perSide - 1) { reachesBorder = true; }
                    float dx = x - nearest.x;
                    float dz = z - nearest.y;
                    float squareMetres = dx * dx + dz * dz;
                    if (squareMetres < bestSquareMetres)
                    {
                        bestSquareMetres = squareMetres;
                        nearestWet = new Vector2(x, z);
                    }
                }
            }
        }

        /// <summary> How much of a ring about the water nearest the settlement is itself water, at the settlement's own scale.
        /// This is the shelter measurement: a straight shoreline puts half the ring in the water, so less than half means land
        /// wraps round that water -- a harbour -- and half or more means it faces the open. </summary>
        /// <remarks> The ring radius is the region's own side, so the question asked is about water at the size of the place
        /// being sited rather than at some fixed distance that would mean one thing for a capital and another for a village.
        /// Samples that cannot be read are left out of both counts, and a ring with nothing readable on it returns a fully wet
        /// reading, which resolves to the exposed archetype: the measurement declines to call something a harbour on ground it
        /// could not see. </remarks>
        private static float ShelterWetFraction(SettlementGroundProbe probe, Vector2 about, float radiusMetres)
        {
            float radiansPerStep = (Mathf.PI * 2f) / ShelterRingSamples;
            int wet = 0;
            int counted = 0;
            for (int i = 0; i < ShelterRingSamples; i++)
            {
                float radians = radiansPerStep * i;
                float ground;
                float water;
                bool hasWater;
                SampleState state = Classify(probe, about.x + Mathf.Cos(radians) * radiusMetres,
                    about.y + Mathf.Sin(radians) * radiusMetres, out ground, out water, out hasWater);
                if (state == SampleState.GroundUnreadable || state == SampleState.WaterUnreadable) { continue; }
                counted++;
                if (state == SampleState.Wet) { wet++; }
            }
            return counted > 0 ? (float)wet / counted : 1f;
        }

        /// <summary> Reilly's breakpoint, measured from the larger of two centres: <c>d / (1 + sqrt(Pb / Pa))</c>, where
        /// <c>Pa</c> is the larger population and <c>Pb</c> the smaller. Beyond it the smaller centre is the nearer draw; inside
        /// it the larger one is. <c>float.NaN</c> for a distance or a population that cannot carry the formula. </summary>
        /// <remarks> W. J. Reilly, <i>The Law of Retail Gravitation</i>, 1931. Used in this tool as a rejection test only, never
        /// to move anything: a settlement whose own ground lies inside a larger neighbour's catchment is not a settlement of its
        /// own, it is that neighbour's suburb. Public because the gate asserts this arithmetic directly, and because a refusal
        /// that quotes a distance has to be checkable against the formula that produced it. </remarks>
        public static float BreakpointFromLargerMetres(float distanceMetres, int largerPopulation, int smallerPopulation)
        {
            if (!(distanceMetres > 0f) || float.IsInfinity(distanceMetres)) { return float.NaN; }
            if (largerPopulation <= 0 || smallerPopulation <= 0) { return float.NaN; }
            if (smallerPopulation > largerPopulation) { return float.NaN; }
            double ratio = (double)smallerPopulation / largerPopulation;
            return (float)(distanceMetres / (1.0 + Math.Sqrt(ratio)));
        }

        /// <summary> The radius of the circle holding a settlement's built-up area, <c>sqrt(area / pi)</c>. The proposed region is
        /// a square of that area; this is the same area read as a radius, which is what a distance test needs. </summary>
        public static float BuiltUpRadiusMetres(float builtAreaM2)
        {
            return builtAreaM2 > 0f ? Mathf.Sqrt(builtAreaM2 / Mathf.PI) : 0f;
        }

        /// <summary> The Reilly pass: a question about the set, not about one site, so it runs once the whole set exists. Every
        /// candidate that survived the water tests is compared against every larger one, and refused where that larger centre's
        /// catchment boundary falls inside its own built-up ground. Returns how many were refused this way. </summary>
        /// <remarks> Two decisions worth stating. Comparators are taken from a snapshot of the set as the water tests left it, so
        /// a candidate refused here still exerts the pull its population implies -- otherwise the result would depend on the order
        /// the author drew the roads, which is not a fact about the world. And where two populations are equal the earlier
        /// terminus is treated as the larger, a declared tie order rather than an accident of iteration, which is also why no two
        /// candidates can refuse each other. </remarks>
        private static int ApplyCatchmentRejections(IList<SettlementCandidate> candidates)
        {
            int count = candidates.Count;
            bool[] comparator = new bool[count];
            for (int i = 0; i < count; i++) { comparator[i] = candidates[i].accepted; }

            int refused = 0;
            for (int i = 0; i < count; i++)
            {
                SettlementCandidate mine = candidates[i];
                if (!mine.accepted) { continue; }
                float radiusMetres = BuiltUpRadiusMetres(mine.proposedBuiltAreaM2);
                int worst = -1;
                float leastClearance = float.PositiveInfinity;
                float breakpointMetres = float.NaN;
                float separationMetres = float.NaN;
                for (int j = 0; j < count; j++)
                {
                    if (j == i || !comparator[j]) { continue; }
                    SettlementCandidate other = candidates[j];
                    if (!IsLarger(other, j, mine, i)) { continue; }
                    float distance = DistanceXZ(other.anchor, mine.anchor);
                    float breakpoint = BreakpointFromLargerMetres(distance, other.proposedPopulation,
                        mine.proposedPopulation);
                    if (float.IsNaN(breakpoint)) { continue; }
                    float clearance = distance - breakpoint;
                    if (!(clearance < radiusMetres) || !(clearance < leastClearance)) { continue; }
                    leastClearance = clearance;
                    breakpointMetres = breakpoint;
                    separationMetres = distance;
                    worst = j;
                }
                if (worst < 0) { continue; }
                mine.accepted = false;
                mine.breakpointMetres = breakpointMetres;
                mine.catchmentNeighbour = candidates[worst].Identity;
                mine.refusal = CatchmentRefusalText(mine, candidates[worst], separationMetres, breakpointMetres,
                    leastClearance, radiusMetres);
                candidates[i] = mine;
                refused++;
            }
            return refused;
        }

        /// <summary> Which of two candidates is the larger centre, with the tie declared: more people wins, and equal populations
        /// go to the earlier terminus. </summary>
        private static bool IsLarger(SettlementCandidate other, int otherIndex, SettlementCandidate mine, int myIndex)
        {
            if (other.proposedPopulation != mine.proposedPopulation)
            {
                return other.proposedPopulation > mine.proposedPopulation;
            }
            return otherIndex < myIndex;
        }

        /// <summary> Distance on the ground plane, ignoring elevation: the catchment question is about how far apart two places
        /// are, not about the climb between them. </summary>
        private static float DistanceXZ(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        /// <summary> The catchment refusal, carrying the separation, the computed breakpoint and the built-up radius it fell
        /// inside, so the author can check the arithmetic and see which road end to move. </summary>
        private static string CatchmentRefusalText(SettlementCandidate mine, SettlementCandidate larger,
            float separationMetres, float breakpointMetres, float clearanceMetres, float radiusMetres)
        {
            string boundary = clearanceMetres >= 0f
                ? "leaving the boundary " + clearanceMetres.ToString("0") + " m short of this centre"
                : "putting the boundary " + (-clearanceMetres).ToString("0") + " m past this centre altogether";
            return "it sits " + separationMetres.ToString("0") + " m from " + larger.Identity + ", a "
                + OrderName(larger) + " of " + larger.proposedPopulation.ToString("n0") + " people. Reilly's breakpoint puts"
                + " that centre's catchment boundary " + breakpointMetres.ToString("0") + " m out from it, " + boundary
                + ", which is inside this " + OrderName(mine) + "'s own built-up radius of " + radiusMetres.ToString("0")
                + " m. Ground inside a larger neighbour's catchment is that neighbour's suburb rather than a settlement of its"
                + " own, so this end is left unseeded: move it further out, or give the road a role that seeds a larger order";
        }

        /// <summary> The sentence that reads both of the road's axes together. The role chose the order; the cross-section is
        /// read beside it and reported, never allowed to overrule it. </summary>
        /// <remarks> The two axes are separate on purpose: <c>Road.RoadClassEnum</c> is what the road is built as and
        /// <c>Road.settlementRole</c> is what it means to the settlement network, and merging them would make one field decide
        /// two unrelated things. So this states what each says and, in the three cases where the pair is worth a second look,
        /// says which -- a carrier with no carriageway, the register's narrowest carriageway feeding its largest order, its widest
        /// feeding its smallest. Every figure quoted comes from <c>RoadClassDefaults</c>, the cited register, and the two
        /// extremes are read out of that register rather than written down here, so they cannot fall out of step with it. </remarks>
        private static string Corroboration(Road.RoadClassEnum crossSection, SettlementOrderClass order)
        {
            string head = "Read together: the " + order.seededByRole + " role seeds the "
                + OrderNameOf(order.displayName, order.orderIndex) + " order, and the road end carries a " + crossSection
                + " cross-section";
            RoadClassProfile profile = RoadClassDefaults.For(crossSection);
            if (profile.LaneCount <= 0) { return head + ", which the road class register does not describe"; }

            head += " of " + profile.LaneCount + " lanes at " + profile.DesignSpeedKPH.ToString("0") + " kph";
            if (!profile.GeneratesMesh)
            {
                return head + " -- a carrier that builds no carriageway of its own, so this settlement is seeded off a route"
                    + " rather than off a street";
            }
            int narrowest;
            int widest;
            CarriagewayLaneRange(out narrowest, out widest);
            if (profile.LaneCount <= narrowest && order.orderIndex == SettlementOrderCatalog.CapitalOrder)
            {
                return head + " -- the narrowest carriageway the register ships, feeding the largest order it ships";
            }
            if (profile.LaneCount >= widest && order.orderIndex == SettlementOrderCatalog.VillageOrder)
            {
                return head + " -- the widest carriageway the register ships, feeding the smallest order it ships";
            }
            return head;
        }

        /// <summary> The lane counts at the two ends of the shipped register, counting only the classes that build a carriageway.
        /// Read from <c>RoadClassDefaults.All()</c> so adding a class cannot leave a stale number behind here. </summary>
        private static void CarriagewayLaneRange(out int narrowest, out int widest)
        {
            narrowest = int.MaxValue;
            widest = 0;
            foreach (RoadClassProfile profile in RoadClassDefaults.All())
            {
                if (!profile.GeneratesMesh || profile.LaneCount <= 0) { continue; }
                if (profile.LaneCount < narrowest) { narrowest = profile.LaneCount; }
                if (profile.LaneCount > widest) { widest = profile.LaneCount; }
            }
            if (narrowest == int.MaxValue) { narrowest = 0; }
        }

        /// <summary> The author-facing summary: the counts first, then a line each, refusals ahead of acceptances because the
        /// refusals are the ones with something to do. Capped at <c>MaximumReportedLines</c> lines with the remainder counted, so
        /// a scene of two hundred roads produces a report someone will actually read. </summary>
        private static string BuildReport(IList<SettlementCandidate> candidates, int accepted, int roleless,
            int refusedByCatchment, List<string> skipped)
        {
            int refused = candidates.Count - accepted;
            StringBuilder text = new StringBuilder();
            text.Append("Road & City Master: ").Append(accepted).Append(" of ").Append(candidates.Count)
                .Append(" proposed settlement sites stand on ground the tool can build on");
            if (refused > 0)
            {
                text.Append(", ").Append(refused).Append(" refused");
                if (refusedByCatchment > 0)
                {
                    text.Append(" (").Append(refusedByCatchment).Append(" of them as suburbs of a larger neighbour)");
                }
            }
            if (roleless > 0)
            {
                text.Append(". ").Append(roleless).Append(" further road ends declare no settlement role and were left alone");
            }
            if (skipped.Count > 0)
            {
                text.Append(". ").Append(skipped.Count).Append(" could not be turned into an order at all");
            }
            text.Append(".");

            int budget = MaximumReportedLines;
            int omitted = 0;
            for (int pass = 0; pass < 2; pass++)
            {
                bool wantAccepted = pass == 1;
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].accepted != wantAccepted) { continue; }
                    if (budget <= 0) { omitted++; continue; }
                    text.AppendLine().Append("  - ").Append(candidates[i].Describe());
                    budget--;
                }
            }
            for (int i = 0; i < skipped.Count; i++)
            {
                if (budget <= 0) { omitted++; continue; }
                text.AppendLine().Append("  - ").Append(skipped[i]).Append(".");
                budget--;
            }
            if (omitted > 0)
            {
                text.AppendLine().Append("  - and ").Append(omitted).Append(omitted == 1 ? " more line" : " more lines")
                    .Append(" not listed.");
            }
            return text.ToString();
        }
    }
}
