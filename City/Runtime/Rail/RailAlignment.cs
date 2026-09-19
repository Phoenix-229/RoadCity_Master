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
    /// <summary> B-27 / D-5's authored block for the FORMATION: everything about a piece of track that is a dimension
    /// rather than a rule. <see cref="RailTrackClass"/> already carries the rules -- gauge, minimum radius, cant and
    /// its rates, ruling gradient, design speed -- and none of them is a length of timber. Keeping the two apart is
    /// what lets one class be the standard the track is JUDGED by and the other the section it is BUILT to, so a
    /// heavier sleeper does not read as a change in the permitted cant.
    ///
    /// Every figure is real mainline practice for ballasted standard-gauge track and is cited where a source names it:
    ///
    /// * <see cref="trackCentresMetres"/> 4.0 m, centre to centre on double track. UIC 741 puts the conventional
    ///   minimum near 3.5 m; 4.0 m is the ordinary figure for new build up to 160 km/h and leaves the six-foot wide
    ///   enough to stand in, which is why it is the default rather than the minimum.
    /// * <see cref="sleeperSpacingMetres"/> 0.6 m. Concrete monobloc sleepers are laid at 600 mm centres (UIC
    ///   practice is 600 to 650 mm); it is the figure that decides how many sleepers a kilometre contains, so it is
    ///   also the figure that decides the mesh budget D-15 has to answer for.
    /// * <see cref="sleeperLengthMetres"/> 2.6 m by <see cref="sleeperWidthMetres"/> 0.26 m by
    ///   <see cref="sleeperHeightMetres"/> 0.2 m -- a standard-gauge concrete sleeper, 2.5 to 2.6 m long, 260 mm wide
    ///   at the rail seat and about 200 mm deep there.
    /// * <see cref="ballastShoulderMetres"/> 0.4 m beyond the sleeper end and <see cref="ballastDepthMetres"/> 0.3 m
    ///   below its underside: UIC 719R gives 300 mm minimum shoulder (400 to 500 mm where speed is high) and 250 to
    ///   350 mm of ballast under the sleeper. <see cref="ballastSideSlopeRun"/> 1.5 is the 1 : 1.5 shoulder slope.
    /// * <see cref="railHeightMetres"/> 0.172 m, <see cref="railHeadWidthMetres"/> 0.0716 m and
    ///   <see cref="railFootWidthMetres"/> 0.15 m are 60E1 (UIC 60), the ordinary mainline section: 172 mm tall,
    ///   72 mm across the head, 150 mm across the foot.
    /// * <see cref="loadingGaugeHeightMetres"/> 6.15 m above the top of rail is AAR Plate H, the double-stack
    ///   envelope -- the tallest ordinary vehicle a line has to clear.
    /// * <see cref="contactWireHeightMetres"/> 5.3 m nominal and <see cref="electricalClearanceMetres"/> 0.27 m are
    ///   EN 50119 for 25 kV AC: the wire hangs at 5.3 m and nothing earthed may come within 270 mm of it.
    /// * <see cref="flangewayWidthMetres"/> 0.075 m and <see cref="flangewayDepthMetres"/> 0.05 m are the groove a
    ///   wheel flange runs in through a level crossing (AREMA asks 2.5 in minimum width and 1.5 in minimum depth);
    ///   <see cref="crossingMarginMetres"/> 0.6 m is how far the crossing panel runs past each edge of the road.
    ///
    /// <see cref="maxEmbankmentMetres"/> and <see cref="maxCuttingMetres"/> are the only two figures here that are
    /// AUTHORED CHOICES rather than published dimensions, and they are labelled as such: no standard says how much
    /// earth a railway may move, because past a certain depth the real answer stops being earth and becomes a
    /// viaduct (B-16) or a tunnel (D-9). 8 m is where that crossover sits for an ordinary line, and an author
    /// building through mountains is expected to raise it or route around. </summary>
    [Serializable]
    public sealed class RailFormationClass
    {
        public float trackCentresMetres = 4f;

        public float sleeperSpacingMetres = 0.6f;
        public float sleeperLengthMetres = 2.6f;
        public float sleeperWidthMetres = 0.26f;
        public float sleeperHeightMetres = 0.2f;

        public float ballastShoulderMetres = 0.4f;
        public float ballastDepthMetres = 0.3f;
        public float ballastSideSlopeRun = 1.5f;

        public float railHeightMetres = 0.172f;
        public float railHeadWidthMetres = 0.0716f;
        public float railFootWidthMetres = 0.15f;

        public float loadingGaugeHeightMetres = 6.15f;
        public float contactWireHeightMetres = 5.3f;
        public float electricalClearanceMetres = 0.27f;

        public float flangewayWidthMetres = 0.075f;
        public float flangewayDepthMetres = 0.05f;
        public float crossingMarginMetres = 0.6f;

        public float maxEmbankmentMetres = 8f;
        public float maxCuttingMetres = 8f;

        public static RailFormationClass Default()
        {
            return new RailFormationClass();
        }

        /// <summary> How wide the finished formation is, across the ballast shoulders, for a given number of tracks.
        /// This is the figure the earthwork is cut to and the figure a bridge deck has to be, so it is worked out here
        /// once rather than assembled from three fields at every call site. </summary>
        public double FormationWidthMetres(int trackCount)
        {
            int tracks = trackCount > 0 ? trackCount : 1;
            double sleeper = sleeperLengthMetres > 0f ? sleeperLengthMetres : 2.6;
            double shoulder = ballastShoulderMetres > 0f ? ballastShoulderMetres : 0.4;
            return (tracks - 1) * (double)trackCentresMetres + sleeper + 2.0 * shoulder;
        }

        /// <summary> The vertical clearance a structure over this track has to leave above the top of rail: the
        /// loading gauge, or the contact wire plus its electrical clearance if that is higher.
        ///
        /// It is ONE figure rather than two competing ones deliberately. With the published numbers the loading gauge
        /// wins -- 6.15 m against 5.3 + 0.27 = 5.57 m -- so a bridge built to clear a double-stack container also
        /// clears the wire, and an author who lowers the gauge for a line that will never see one gets the wire figure
        /// instead without having to know which of the two was governing. A caller that took the maximum itself would
        /// be the second place that decision lived.
        ///
        /// <paramref name="electrified"/> is the RUN's answer and not the formation's, which is why it is a parameter:
        /// the same section of ballast, sleeper and rail carries an electrified main line and an unwired branch, and
        /// putting the wire on the section would make the branch a different formation for no dimensional reason.
        /// </summary>
        public double RequiredStructureClearanceMetres(bool electrified)
        {
            double gauge = loadingGaugeHeightMetres > 0f ? loadingGaugeHeightMetres : 6.15;
            if (!electrified) { return gauge; }
            double wire = contactWireHeightMetres + electricalClearanceMetres;
            return wire > gauge ? wire : gauge;
        }

        /// <summary> How many sleepers a run of this length carries, at the class spacing. Counts the sleeper at
        /// station zero, so a 60 m run at 0.6 m spacing is 101 sleepers and not 100 -- the fencepost the mesh budget
        /// in D-15 is counted against. </summary>
        public int SleeperCount(double runLengthMetres)
        {
            if (runLengthMetres <= 0.0) { return 0; }
            double spacing = sleeperSpacingMetres > 0f ? sleeperSpacingMetres : 0.6;
            return (int)Math.Floor(runLengthMetres / spacing) + 1;
        }
    }


    /// <summary> What the author answers for one railway. The plan is the SAME <see cref="HorizontalAlignment"/> a
    /// road is staked from, which is the point: the author places a railway the way they place a road -- the same
    /// point list, the same guided placement, the same scene handles -- and everything downstream of the point list
    /// is what differs.
    ///
    /// The design speed is <see cref="RailTrackClass.designSpeedKmh"/> and NOT the plan's own
    /// <c>designSpeedKmh</c>. The plan carries that field because a road's minimum radius comes from it, and a rail
    /// alignment is staked through <see cref="AlignmentResolver.ResolveToStandard"/>, which is handed rail's numbers
    /// and never reads the plan's speed at all. Two speeds for one railway would be a disagreement nobody finds, so
    /// the road field is left where it is and this file states which one governs. </summary>
    [Serializable]
    public sealed class RailAlignmentSettings
    {
        public bool enabled = true;
        public HorizontalAlignment plan = new HorizontalAlignment();
        public RailTrackClass track = RailTrackClass.Default();
        public RailFormationClass formation = RailFormationClass.Default();

        /// <summary> One track or two. More than two is a yard rather than a line and is D-10's business, so this is
        /// clamped rather than free: a six-track formation staked from one centreline would put the outer tracks on a
        /// radius nobody checked. </summary>
        public int trackCount = 1;

        /// <summary> How far apart the alignment is sampled along its length. 5 m is fine enough that a 300 m curve
        /// (the class minimum) is cut into sixty chords and a 0.05 m chord tolerance is comfortably met, and coarse
        /// enough that a ten-kilometre line is two thousand samples rather than a hundred thousand. </summary>
        public float sampleSpacingMetres = 5f;

        /// <summary> Whether this run carries overhead line equipment. Read by
        /// <see cref="RailFormationClass.RequiredStructureClearanceMetres"/> for the clearance a bridge must leave,
        /// and by <see cref="RailOverheadPlanner"/>, which hangs the masts and the contact wire off the same samples
        /// this plan produces. </summary>
        public bool electrified = true;

        /// <summary> The equipment strung above the track: mast spacing, stagger, sag and the lowest the wire may be
        /// pulled. Present even on an unwired run, and that is deliberate rather than untidy -- an author toggling
        /// <see cref="electrified"/> off to compare a diesel branch against an electrified one should get their
        /// overhead figures back when they toggle it on, not a reset class. <see cref="RailOverheadPlanner.Plan"/>
        /// returns nothing while the run is unwired, so the figures sit unread rather than acted on.
        ///
        /// The two heights this does NOT carry -- the nominal contact height and the electrical clearance -- stay on
        /// <see cref="RailFormationClass"/>, for the reason <see cref="RailOverheadClass"/> argues at length: a bridge
        /// asks the formation how much room to leave, and a second copy of the nominal height here would be the one an
        /// author edited while the bridge kept building to the other. </summary>
        public RailOverheadClass overhead = new RailOverheadClass();

        /// <summary> What the railway passes under, as the author's answers. Empty is the ordinary case and is not a
        /// refusal -- a railway with nothing over it is wired at the class nominal throughout.
        ///
        /// These are ASKED FOR rather than searched for, for the reason <see cref="RailOverheadStructure"/> gives: a
        /// bridge belongs to B-16, a tunnel to D-11 and a station canopy to D-8, and a planner that went looking for
        /// its own obstructions would be a fourth opinion about where they are. </summary>
        public List<RailOverheadStructure> overheadStructures = new List<RailOverheadStructure>();

        /// <summary> Where trains stop, as stations along the run. D-8 turns each of these into a platform and a
        /// building; this file only records them, checks they are on the run, and reports the cant at each -- a
        /// platform on canted track is a real defect, because the floor of the train and the edge of the platform
        /// stop being parallel. </summary>
        public List<float> stationStopMetres = new List<float>();

        /// <summary> The four surfaces a railway is made of, ASKED FOR rather than invented. The same rule the pole
        /// line follows and <c>CitySurfaceTypeGate.CaseMaterialsAuthoredNotGenerated</c> enforces: the tool builds the
        /// geometry and the author supplies what it is made of, because a material this file created would be a
        /// material nobody could re-skin without editing code.
        ///
        /// Four rather than three, and rather than one, because a railway's surfaces weather independently: the ballast
        /// is loose grey stone, a sleeper is creosoted timber or precast concrete, the rail head is polished steel with
        /// rust down its web, and a level-crossing panel is neither -- it is rubber or concrete, laid by the road
        /// authority and not by the railway. An empty slot is not a refusal; the build sentence names it and the
        /// geometry renders in Unity's missing-material pink. </summary>
        public Material ballastMaterial = null;
        public Material sleeperMaterial = null;
        public Material railMaterial = null;
        public Material crossingMaterial = null;

        /// <summary> B-27 / D-7. The three surfaces the ELECTRIFICATION is made of, asked for on the same rule as the
        /// four above. Three rather than one because an overhead line's parts weather nothing like each other: a mast
        /// is galvanised steel gone chalky, its registration arm and fittings are a mix of aluminium-bronze casting
        /// and porcelain, and the wire itself is drawn copper that polishes bright on its underside where the
        /// pantograph rubs it. One slot for all three would make the wire read as painted steel, which is the single
        /// most obvious way a rendered catenary looks wrong.
        ///
        /// They live on the settings rather than on the overhead class because a class is a STANDARD -- a span, a
        /// height, a dropper spacing, all of which come from the electrification system adopted -- and what colour
        /// the steel is on this particular line is not part of any standard. </summary>
        public Material ohleMastMaterial = null;
        public Material ohleBracketMaterial = null;
        public Material ohleWireMaterial = null;

        /// <summary> An optional authored mast to stand instead of the generated steel tube, placed at each mast
        /// position with +X facing the track.
        ///
        /// There is deliberately NO matching wire prefab. A mast is a repeated object -- the same casting bolted down
        /// every fifty metres -- so one mesh reused at many positions is exactly what the real thing is. A contact
        /// wire is not: its shape is the catenary its OWN span hangs in, solved per span from that span's length,
        /// tension and the height of the two masts it is strung between, and no two spans on a real run share it. A
        /// prefab there would freeze one span's sag and stamp it on every other, which is a wire that visibly does not
        /// touch the masts it is tied to. The wire is generated, and that is the only correct answer. </summary>
        public GameObject ohleMastPrefab = null;
    }


    /// <summary> One sampled station on a staked railway: where the track is, which way it points, what it is banked
    /// to and how much earth had to move to get it there. The mesh sweep in D-5's editor half needs all of it, and
    /// D-7 hangs its masts off the same list rather than re-sampling the alignment. </summary>
    [Serializable]
    public struct RailAlignmentSample
    {
        public float stationMetres;

        /// <summary> Top of rail on the track centreline, in world space. The formation's ballast and sleepers hang
        /// BELOW this, which is why the earthwork is measured from a lower plane than the one drawn here. </summary>
        public Vector3 position;

        /// <summary> The unit tangent WITH ITS GRADE, not flattened.
        ///
        /// This is the one place a rail sample deliberately differs from
        /// <c>RoadPowerLinePlanner.PointAt</c>, whose own documentation says the tangent is flattened "because a pole
        /// stands plumb". A pole does; a rail does not. The gradient is the quantity this whole file is judged on, and
        /// a tangent with the climb taken out of it would sweep a level track up a hill. </summary>
        public Vector3 tangent;

        public double radiusMetres;

        /// <summary> Applied cant, SIGNED: positive raises the rail on the right of travel. The sign follows the
        /// curvature reported by <see cref="AlignmentResolver.CurvatureAtStation"/>, which calls a left turn positive
        /// -- and a left turn puts the outer rail on the right. Magnitude is
        /// <see cref="RailTrack.AppliedCantMillimetres"/>, so it is capped at the class maximum here as everywhere.
        /// </summary>
        public double appliedCantMillimetres;

        public double cantDeficiencyMillimetres;

        /// <summary> What this radius can be run at, from <see cref="RailTrack.PermissibleSpeedKmh"/>. Positive
        /// infinity on a straight, so the lowest value along a run is the run's restriction and a straight never
        /// becomes one by accident. </summary>
        public double permissibleSpeedKmh;

        public float groundHeightMetres;

        /// <summary> Formation base height less ground height: positive is embankment, negative is cutting. This is
        /// the figure the terrain has to answer to -- where it is negative the ground must come DOWN to the railway,
        /// which is the case B-17 exists for. </summary>
        public float earthworkMetres;

        public double gradientPerMille;
    }


    /// <summary> A road offered to the crossing search: its centreline and how wide it is. The planner takes these
    /// rather than reading the scene, so the search is a pure function of two point lists and a gate can hand it two
    /// invented ones (C-06).
    ///
    /// <see cref="gradeSeparated"/> is the caller's answer, not this file's: whether a particular road passes OVER a
    /// railway is a bridge decision and it belongs to B-16, so a road already carried on a structure is offered with
    /// the flag set and the crossing search steps past it. Deciding it here would be a second answer to a question
    /// the bridge code already owns. </summary>
    [Serializable]
    public sealed class RailCrossingRoad
    {
        public string roadName = string.Empty;
        public List<Vector3> centreline = new List<Vector3>();
        public float halfWidthMetres = 4f;
        public bool gradeSeparated;
    }


    /// <summary> One level crossing: where a staked railway and a staked road cross on the flat.
    ///
    /// The skew is a MEASURED figure and not a refusal. A crossing at 60 degrees to the road is ordinary -- they are
    /// everywhere, and the answer to one is a longer panel, not a redesign -- so it is reported with the length that
    /// skew costs and left to the author. Inventing a second severity channel beside the plan's refusals would make
    /// the inspector answer "unbuildable" to something every railway in the world has. </summary>
    [Serializable]
    public struct RailLevelCrossing
    {
        public string roadName;
        public float railStationMetres;
        public Vector3 position;

        /// <summary> The angle between the track and the road, in degrees, where 90 is a square crossing. Never
        /// reported above 90: a road crossing at 120 degrees is the same crossing as one at 60 seen from the other
        /// side, and two numbers for one geometry is how a measured figure stops being comparable. </summary>
        public double crossingAngleDegrees;

        /// <summary> How long the crossing panel has to be ALONG THE TRACK: the road's width plus a margin each side,
        /// divided by the sine of the crossing angle. That division is the whole cost of a skew -- a 10 m road across
        /// square track needs 11.2 m of panel, and the same road at 30 degrees needs 22.4 m. </summary>
        public double panelLengthMetres;

        /// <summary> The applied cant where the road crosses, in mm. A level crossing on canted track has a road
        /// surface that has to twist through the cant to meet the rails, so this being non-zero is worth an author
        /// knowing even though it is not a refusal. </summary>
        public double appliedCantMillimetres;

        public double roadWidthMetres;
    }


    /// <summary> What a staked railway worked out to. Built on <c>RoadPowerLinePlan</c>'s shape on purpose: a list of
    /// what was placed, the measured figures the inspector prints, and a list of refusals that is empty or fatal --
    /// so the two authoring surfaces answer the same shape of question and D-16 asserts both the same way.
    ///
    /// The refusals are worth naming precisely, because which ones exist here is a design decision:
    ///
    /// * a curve tighter than <see cref="RailTrackClass.minimumRadiusMetres"/> -- it cannot be built as track;
    /// * earthwork past the authored maximum -- past that depth the real answer is a viaduct or a tunnel;
    /// * fewer than two plan points -- one point has no direction.
    ///
    /// A gradient failure is NOT among them, and that is not an omission. The vertical profile is solved by a
    /// gradient-LIMITED sweep, so the ruling gradient is satisfied by construction and cannot be violated; what the
    /// limit costs instead is earth, which is why the earthwork is what refuses. Nor is cant deficiency a refusal: a
    /// curve too tight for the design speed is buildable and ordinary, and what it gets is
    /// <see cref="lowestPermissibleSpeedKmh"/>. </summary>
    [Serializable]
    public sealed class RailAlignmentPlan
    {
        public List<RailAlignmentSample> samples = new List<RailAlignmentSample>();
        public List<RailLevelCrossing> crossings = new List<RailLevelCrossing>();
        public List<string> refusals = new List<string>();

        /// <summary> The staked alignment itself, kept so the editor half sweeps the cross-section along the same
        /// elements this plan was measured from rather than staking it a second time. </summary>
        public AlignmentSolution alignment;

        public double runLengthMetres;
        public int trackCount = 1;
        public double formationWidthMetres;
        public double requiredStructureClearanceMetres;

        /// <summary> The air gap the contact wire keeps below an earthed structure, in metres, taken straight off the
        /// formation for this class -- 0.15 m for a 750 V DC tramway, 0.27 m for a 25 kV line. Carried here so the
        /// overhead pass reads the class's own figure directly instead of trying to recover it by subtraction. </summary>
        public double electricalClearanceMetres = 0.27;

        public int sleeperCount;
        public int stationStopCount;

        public double steepestGradientPerMille;
        public double sharpestRadiusMetres = double.PositiveInfinity;
        public float sharpestRadiusStationMetres;
        public double maxAppliedCantMillimetres;
        public double maxCantDeficiencyMillimetres;
        public double totalTransitionLengthMetres;

        /// <summary> How many transitions the tangents were too short to fit, and how many curves ran into their
        /// neighbour. Both come from the staking, which shortens rather than fails, and both are REPORTED rather than
        /// refused -- a railway with a shortened transition is buildable and exists, but its cant ramps steeper than
        /// the class rate allows, so an author told nothing would believe the run met a limit it does not. </summary>
        public int shortenedTransitionCount;
        public int overlappingCurveCount;

        /// <summary> The slowest the run has to be taken, and where. Positive infinity means nothing on the run
        /// imposes a limit -- a wholly straight line -- which is why the inspector prints the design speed in that
        /// case rather than an infinity. </summary>
        public double lowestPermissibleSpeedKmh = double.PositiveInfinity;
        public float lowestPermissibleSpeedStationMetres;

        public float deepestEmbankmentMetres;
        public float deepestEmbankmentStationMetres;
        public float deepestCuttingMetres;
        public float deepestCuttingStationMetres;

        /// <summary> The most skewed crossing on the run, in degrees from square. Starts at 90 -- square -- so a run
        /// with no crossings reports the best possible value rather than zero, which would read as a crossing lying
        /// along the track. </summary>
        public double sharpestCrossingAngleDegrees = 90.0;

        /// <summary> Whether this railway can be built. Two samples is the floor because one sample is a point and a
        /// point has no track in it. </summary>
        public bool Buildable
        {
            get { return samples.Count > 1 && refusals.Count == 0; }
        }

        /// <summary> The one sentence that says what the railway is. Pure, so D-16 asserts the wording without a
        /// scene, and used by the inspector rather than re-worded there -- an inspector that says it differently is a
        /// second answer to the same question. </summary>
        public string Summary()
        {
            if (samples.Count < 2)
            {
                return "No railway: " + (refusals.Count > 0 ? refusals[0] : "nothing was staked") + ".";
            }
            string text = Kilometres(runLengthMetres) + " km of "
                          + (trackCount > 1 ? trackCount.ToString(CultureInfo.InvariantCulture) + "-track" : "single")
                          + " railway, " + Fixed(formationWidthMetres, "0.##") + " m wide over the ballast, "
                          + sleeperCount.ToString(CultureInfo.InvariantCulture) + " sleepers.";
            text += " Sharpest radius " + Radius() + ", steepest "
                    + Fixed(steepestGradientPerMille, "0.#") + " per mille.";
            if (maxAppliedCantMillimetres > 0.0)
            {
                text += " Cant up to " + Fixed(maxAppliedCantMillimetres, "0") + " mm";
                if (maxCantDeficiencyMillimetres > 0.0)
                {
                    text += ", deficiency up to " + Fixed(maxCantDeficiencyMillimetres, "0") + " mm";
                }
                text += ".";
            }
            if (!double.IsInfinity(lowestPermissibleSpeedKmh))
            {
                text += " Runnable at " + Fixed(lowestPermissibleSpeedKmh, "0")
                        + " km/h, set by the curve at " + Fixed(lowestPermissibleSpeedStationMetres, "0") + " m.";
            }
            if (crossings.Count > 0)
            {
                text += " " + crossings.Count.ToString(CultureInfo.InvariantCulture)
                        + (crossings.Count == 1 ? " level crossing" : " level crossings")
                        + ", most skewed " + Fixed(sharpestCrossingAngleDegrees, "0.#") + " degrees.";
            }
            text += " Earthwork " + Fixed(deepestEmbankmentMetres, "0.##") + " m up, "
                    + Fixed(deepestCuttingMetres, "0.##") + " m down.";
            if (refusals.Count > 0)
            {
                text += " " + refusals.Count.ToString(CultureInfo.InvariantCulture)
                        + (refusals.Count == 1 ? " refusal." : " refusals.");
            }
            return text;
        }

        /// <summary> The sharpest radius, or the plain word for a line that never turns. Infinity printed as a number
        /// would read as a very wide curve rather than as no curve at all. </summary>
        private string Radius()
        {
            if (double.IsInfinity(sharpestRadiusMetres)) { return "straight throughout"; }
            return Fixed(sharpestRadiusMetres, "0.#") + " m at "
                   + Fixed(sharpestRadiusStationMetres, "0") + " m";
        }

        private static string Kilometres(double metres)
        {
            return (metres / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static string Fixed(double value, string format)
        {
            return value.ToString(format, CultureInfo.InvariantCulture);
        }
    }


    /// <summary> B-27 / D-5's verifiable core: it stakes a railway, measures it, and refuses it. It computes and it
    /// refuses; it never mutates a scene, never touches an asset and never draws -- the mesh and the scene objects are
    /// the editor half's, which is also what keeps a built railway BAKED.
    ///
    /// The staking is NOT re-implemented here. <see cref="AlignmentResolver.ResolveToStandard"/> is the tool's one
    /// implementation of tangents, transitions, arcs and station mapping, and it is handed rail's two governing
    /// numbers -- the class minimum radius, and <see cref="RailTrack.TransitionLengthMetres"/> as the rule saying how
    /// long a transition a given radius wants. That is the whole of the difference between staking a road and staking
    /// a railway: the plane geometry is identical, the standard is not, and only one of the two was worth writing
    /// twice.
    ///
    /// The VERTICAL profile is this file's own, because <see cref="AlignmentPoint"/> deliberately carries no height
    /// and a railway cannot simply follow the ground -- 15 per mille is an order of magnitude gentler than the
    /// hillside it crosses. It is solved by a gradient-limited two-pass sweep, forward then backward, taking the mean
    /// of the two. The forward pass is the closest the profile can stay to the ground working from the start without
    /// exceeding the ruling gradient; the backward pass is the same working from the end; and their mean is a profile
    /// that (a) never exceeds the gradient between adjacent samples, because the mean of two differences each within
    /// the limit is itself within it, (b) sits ON the ground wherever the ground is gentle enough for both sweeps to
    /// reach it, so a level approach is not lifted onto an embankment for the sake of a hill a kilometre further on,
    /// and (c) is unchanged if the alignment is staked in the opposite direction, since reversing the line swaps the
    /// two sweeps and leaves their mean alone -- which is the reproducibility C-06 asks for, a line drawn the other
    /// way round being the same line.
    ///
    /// That construction is why there is no gradient REFUSAL. The gradient cannot be violated; what enforcing it
    /// costs is earth, and the earth is what refuses. Where the profile falls BELOW the ground the terrain has to be
    /// cut down to the railway, which is the case B-17 exists for and the reason the cutting depth is reported
    /// per-station rather than as one number. </summary>
    public static class RailAlignmentPlanner
    {
        /// <summary> Stake, measure and judge one railway. Pure: the ground arrives as a reader and the roads as point
        /// lists, so a gate can hand it a flat surface and two invented roads and get the same answer twice (C-06).
        /// Returns null with a note when there is nothing to stake -- a switched-off run and a one-point plan both say
        /// something, and neither is a failure. </summary>
        public static RailAlignmentPlan Plan(RailAlignmentSettings settings, ITerrainSurfaceReader ground,
            IList<RailCrossingRoad> roads, out string note)
        {
            note = string.Empty;
            if (settings == null) { note = "this railway has no settings"; return null; }
            if (!settings.enabled) { note = "this railway is switched off"; return null; }
            if (settings.plan == null || settings.plan.PointCount < 2)
            {
                note = "a railway needs at least two plan points, because one point has no direction";
                return null;
            }

            RailTrackClass track = settings.track != null ? settings.track : RailTrackClass.Default();
            RailFormationClass form = settings.formation != null
                ? settings.formation : RailFormationClass.Default();
            double speed = track.designSpeedKmh > 0f ? track.designSpeedKmh : 120.0;

            // The transition is sized at the speed the CURVE is run at, not at the line's design speed. Those are the
            // same number wherever the curve is wide enough to take the design speed, and two very different numbers
            // where it is not: a 25 m tramway curve asked for a transition at 50 km/h wants 283 m of one, eleven times
            // its own radius, and every curve on the line would then come back shortened. Real practice sizes a
            // transition at the LOCAL permissible speed -- the speed a curve needing a restriction is actually taken at
            // -- so the delegate takes the lower of the two. A straight returns positive infinity from
            // PermissibleSpeedKmh and therefore keeps the design speed, which is the right answer for a tangent.
            AlignmentSolution solution = AlignmentResolver.ResolveToStandard(settings.plan,
                track.minimumRadiusMetres,
                delegate(double radius)
                {
                    double runAt = RailTrack.PermissibleSpeedKmh(radius, track);
                    return RailTrack.TransitionLengthMetres(runAt < speed ? runAt : speed, radius, track);
                });
            if (solution.lengthMetres <= 0.0)
            {
                note = "the staked railway came out zero long, so there is nothing to measure";
                return null;
            }

            RailAlignmentPlan plan = new RailAlignmentPlan();
            plan.alignment = solution;
            plan.runLengthMetres = solution.lengthMetres;
            plan.trackCount = settings.trackCount > 1 ? 2 : 1;
            plan.formationWidthMetres = form.FormationWidthMetres(plan.trackCount);
            plan.requiredStructureClearanceMetres = form.RequiredStructureClearanceMetres(settings.electrified);
            plan.electricalClearanceMetres = form.electricalClearanceMetres;
            plan.sleeperCount = form.SleeperCount(solution.lengthMetres) * plan.trackCount;
            plan.totalTransitionLengthMetres = TotalTransitionLength(solution);

            Sample(plan, settings, track, form, ground, speed);
            RecordCurveRefusals(plan, solution, track);
            RecordEarthworkRefusals(plan, form);
            FindCrossings(plan, roads, form);
            RecordStationStops(plan, settings);
            return plan;
        }

        /// <summary> Every transition on the run, entry and exit, added up. Negligible curves are skipped because a
        /// deflection too small to curve has no transition to ease into. </summary>
        private static double TotalTransitionLength(AlignmentSolution solution)
        {
            double total = 0.0;
            for (int i = 0; i < solution.curves.Count; i++)
            {
                if (solution.curves[i].negligible) { continue; }
                total += 2.0 * solution.curves[i].transitionLengthMetres;
            }
            return total;
        }

        /// <summary> Walk the run at the authored spacing and fill in every sample. The station count is worked out so
        /// the last sample lands exactly on the end of the run rather than short of it: a railway that stops 3 m before
        /// its own end is a gap in the mesh, and forcing the last station is cheaper than a special case in the sweep.
        /// </summary>
        private static void Sample(RailAlignmentPlan plan, RailAlignmentSettings settings, RailTrackClass track,
            RailFormationClass form, ITerrainSurfaceReader ground, double speed)
        {
            double length = plan.runLengthMetres;
            double spacing = settings.sampleSpacingMetres > 0f ? settings.sampleSpacingMetres : 5.0;
            int count = (int)Math.Floor(length / spacing) + 1;
            if (count < 2) { count = 2; }
            double step = length / (count - 1);

            double[] stations = new double[count];
            double[] xs = new double[count];
            double[] zs = new double[count];
            double[] headingX = new double[count];
            double[] headingZ = new double[count];
            float[] groundY = new float[count];

            for (int i = 0; i < count; i++)
            {
                double station = i == count - 1 ? length : i * step;
                stations[i] = station;
                double x, z, dx, dz;
                if (!AlignmentResolver.PointAtStation(plan.alignment, station, out x, out z)) { x = 0.0; z = 0.0; }
                if (!AlignmentResolver.HeadingAtStation(plan.alignment, station, out dx, out dz))
                {
                    dx = 0.0;
                    dz = 1.0;
                }
                xs[i] = x;
                zs[i] = z;
                headingX[i] = dx;
                headingZ[i] = dz;
                groundY[i] = ground != null ? ground.SampleHeight((float)x, (float)z) : 0f;
            }

            double[] baseY = GradeLimitedProfile(groundY, step, track.maxGradientPerMille);
            double railTop = (double)form.ballastDepthMetres + form.sleeperHeightMetres + form.railHeightMetres;

            for (int i = 0; i < count; i++)
            {
                RailAlignmentSample s = new RailAlignmentSample();
                s.stationMetres = (float)stations[i];
                s.position = new Vector3((float)xs[i], (float)(baseY[i] + railTop), (float)zs[i]);
                double grade = GradeAt(baseY, step, i);
                s.gradientPerMille = grade * 1000.0;
                s.tangent = TangentWithGrade(headingX[i], headingZ[i], grade);
                s.radiusMetres = AlignmentResolver.RadiusAtStation(plan.alignment, stations[i]);
                double applied = RailTrack.AppliedCantMillimetres(speed, s.radiusMetres, track);
                double curvature = AlignmentResolver.CurvatureAtStation(plan.alignment, stations[i]);
                s.appliedCantMillimetres = curvature < 0.0 ? -applied : applied;
                s.cantDeficiencyMillimetres = RailTrack.CantDeficiencyMillimetres(speed, s.radiusMetres, track);
                s.permissibleSpeedKmh = RailTrack.PermissibleSpeedKmh(s.radiusMetres, track);
                s.groundHeightMetres = groundY[i];
                s.earthworkMetres = (float)(baseY[i] - groundY[i]);
                plan.samples.Add(s);
                RecordExtremes(plan, s);
            }
        }

        /// <summary> The gradient-limited two-pass sweep, as a public pure function so D-16 can prove the property the
        /// whole vertical design rests on -- that no adjacent pair of samples exceeds the ruling gradient -- without a
        /// terrain, a scene or a component.
        ///
        /// Each pass clamps the ground to what the previous sample allows, UP and DOWN alike: a railway may not dive
        /// into a valley any faster than it may climb out of one. Taking the mean of the two passes is what makes the
        /// result independent of which end the sweep started from. </summary>
        public static double[] GradeLimitedProfile(IList<float> groundHeights, double sampleSpacingMetres,
            double maxGradientPerMille)
        {
            int n = groundHeights != null ? groundHeights.Count : 0;
            double[] profile = new double[n];
            if (n == 0) { return profile; }
            if (n == 1) { profile[0] = groundHeights[0]; return profile; }

            double spacing = sampleSpacingMetres > 0.0 ? sampleSpacingMetres : 0.0;
            double rise = Math.Abs(maxGradientPerMille) / 1000.0 * spacing;

            double[] forward = new double[n];
            double[] backward = new double[n];
            forward[0] = groundHeights[0];
            for (int i = 1; i < n; i++)
            {
                forward[i] = Clamp(groundHeights[i], forward[i - 1] - rise, forward[i - 1] + rise);
            }
            backward[n - 1] = groundHeights[n - 1];
            for (int i = n - 2; i >= 0; i--)
            {
                backward[i] = Clamp(groundHeights[i], backward[i + 1] - rise, backward[i + 1] + rise);
            }
            for (int i = 0; i < n; i++) { profile[i] = 0.5 * (forward[i] + backward[i]); }
            return profile;
        }

        private static double Clamp(double value, double low, double high)
        {
            return value < low ? low : (value > high ? high : value);
        }

        /// <summary> The slope at one sample, as a rise over run. Forward difference except at the last sample, which
        /// takes the span behind it -- there is no span ahead of the end of the line, and reporting zero there would
        /// make a railway that ends mid-climb read as level. </summary>
        private static double GradeAt(double[] profile, double step, int index)
        {
            if (profile == null || profile.Length < 2 || step <= 0.0) { return 0.0; }
            int a = index < profile.Length - 1 ? index : profile.Length - 2;
            return (profile[a + 1] - profile[a]) / step;
        }

        /// <summary> The 3-D unit tangent from an XZ heading and a slope. The heading is already unit in XZ, so the
        /// slope goes straight into Y and the whole is normalised: the grade is IN the tangent, which is the difference
        /// between sweeping a railway up a hill and sweeping a level track through it. </summary>
        private static Vector3 TangentWithGrade(double headingX, double headingZ, double grade)
        {
            Vector3 t = new Vector3((float)headingX, (float)grade, (float)headingZ);
            float magnitude = t.magnitude;
            if (magnitude <= 1e-6f) { return Vector3.forward; }
            return t / magnitude;
        }

        /// <summary> Fold one sample into the run's extremes. Cant and gradient are compared by MAGNITUDE, because a
        /// railway falling at 12 per mille and one climbing at 12 per mille are the same steepness, and the sign is
        /// already carried on the sample for the sweep to bank by. The cutting depth is stored positive, so the
        /// inspector reads "3.1 m down" rather than "-3.1 m down". </summary>
        private static void RecordExtremes(RailAlignmentPlan plan, RailAlignmentSample s)
        {
            double gradient = Math.Abs(s.gradientPerMille);
            if (gradient > plan.steepestGradientPerMille) { plan.steepestGradientPerMille = gradient; }

            if (s.radiusMetres < plan.sharpestRadiusMetres)
            {
                plan.sharpestRadiusMetres = s.radiusMetres;
                plan.sharpestRadiusStationMetres = s.stationMetres;
            }

            double cant = Math.Abs(s.appliedCantMillimetres);
            if (cant > plan.maxAppliedCantMillimetres) { plan.maxAppliedCantMillimetres = cant; }
            if (s.cantDeficiencyMillimetres > plan.maxCantDeficiencyMillimetres)
            {
                plan.maxCantDeficiencyMillimetres = s.cantDeficiencyMillimetres;
            }
            if (s.permissibleSpeedKmh < plan.lowestPermissibleSpeedKmh)
            {
                plan.lowestPermissibleSpeedKmh = s.permissibleSpeedKmh;
                plan.lowestPermissibleSpeedStationMetres = s.stationMetres;
            }

            if (s.earthworkMetres > plan.deepestEmbankmentMetres)
            {
                plan.deepestEmbankmentMetres = s.earthworkMetres;
                plan.deepestEmbankmentStationMetres = s.stationMetres;
            }
            if (-s.earthworkMetres > plan.deepestCuttingMetres)
            {
                plan.deepestCuttingMetres = -s.earthworkMetres;
                plan.deepestCuttingStationMetres = s.stationMetres;
            }
        }

        /// <summary> Refuse every curve tighter than the class minimum, and count the two defects that are reported
        /// rather than refused.
        ///
        /// The verdict is taken from <see cref="RailTrack.CheckRadius"/> and worded by
        /// <see cref="RailTrack.DescribeRadiusRejection"/> rather than written here, so there is exactly one sentence
        /// in the tool for "this rail curve is too tight" and D-16 can assert it. The curve already carries
        /// <c>belowMinimumRadius</c> against the same number -- the staking was handed rail's minimum -- but a bool is
        /// not a sentence, and the station the author has to go and fix is not in it. </summary>
        private static void RecordCurveRefusals(RailAlignmentPlan plan, AlignmentSolution solution,
            RailTrackClass track)
        {
            for (int i = 0; i < solution.curves.Count; i++)
            {
                AlignmentCurve c = solution.curves[i];
                if (c.negligible) { continue; }
                if (c.transitionShortened) { plan.shortenedTransitionCount++; }
                if (c.overlapsNeighbour) { plan.overlappingCurveCount++; }

                RailTrack.RadiusCheck check = RailTrack.CheckRadius(c.radiusMetres, track);
                if (check.accepted) { continue; }
                plan.refusals.Add("Plan point " + (c.pointIndex + 1).ToString(CultureInfo.InvariantCulture) + ": "
                                  + RailTrack.DescribeRadiusRejection(check)
                                  + ". Widen the curve or move the point.");
            }
        }

        /// <summary> Refuse earthwork past the authored maximum. ONE refusal per direction rather than one per sample:
        /// a two-kilometre embankment sampled every 5 m would otherwise produce four hundred identical warnings and
        /// bury the curve refusal underneath them. The deepest station is named because that is where the author has
        /// to decide between raising the limit, routing around, and building the viaduct or tunnel that is the real
        /// answer at that depth. </summary>
        private static void RecordEarthworkRefusals(RailAlignmentPlan plan, RailFormationClass form)
        {
            if (form.maxEmbankmentMetres > 0f && plan.deepestEmbankmentMetres > form.maxEmbankmentMetres)
            {
                plan.refusals.Add("The railway stands "
                    + plan.deepestEmbankmentMetres.ToString("0.##", CultureInfo.InvariantCulture)
                    + " m above the ground at " + Station(plan.deepestEmbankmentStationMetres)
                    + ", past the authored " + form.maxEmbankmentMetres.ToString("0.##", CultureInfo.InvariantCulture)
                    + " m embankment limit. At that height the real answer is a viaduct, not more fill.");
            }
            if (form.maxCuttingMetres > 0f && plan.deepestCuttingMetres > form.maxCuttingMetres)
            {
                plan.refusals.Add("The railway runs "
                    + plan.deepestCuttingMetres.ToString("0.##", CultureInfo.InvariantCulture)
                    + " m below the ground at " + Station(plan.deepestCuttingStationMetres)
                    + ", past the authored " + form.maxCuttingMetres.ToString("0.##", CultureInfo.InvariantCulture)
                    + " m cutting limit. At that depth the real answer is a tunnel, not a deeper cutting.");
            }
        }

        /// <summary> Check the authored station stops are on the run and record the cant at each. A stop off the end of
        /// the line is a refusal because a platform nobody can reach is not a smaller version of a platform; a stop on
        /// canted track is only a warning, because it is buildable and real, and the number is what an author needs to
        /// see rather than a veto. </summary>
        private static void RecordStationStops(RailAlignmentPlan plan, RailAlignmentSettings settings)
        {
            if (settings.stationStopMetres == null) { return; }
            for (int i = 0; i < settings.stationStopMetres.Count; i++)
            {
                float station = settings.stationStopMetres[i];
                if (station < 0f || station > plan.runLengthMetres)
                {
                    plan.refusals.Add("Station stop " + (i + 1).ToString(CultureInfo.InvariantCulture) + " sits at "
                        + Station(station) + ", off a railway "
                        + plan.runLengthMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m long.");
                    continue;
                }
                plan.stationStopCount++;
            }
        }

        /// <summary> A station, worded the one way the whole file words it. </summary>
        private static string Station(double metres)
        {
            return metres.ToString("0.#", CultureInfo.InvariantCulture) + " m";
        }

        /// <summary> Find every place a staked road crosses this staked railway on the flat.
        ///
        /// This is the method <see cref="RailTrack"/>'s scope note hands level crossings to, and the reason it is here
        /// and not on either side alone: a crossing is not a property of a railway or of a road, it is a property of
        /// the PAIR, and only something holding both alignments at once can find one. A road that already passes over
        /// or under -- B-16's decision, arriving as <see cref="RailCrossingRoad.gradeSeparated"/> -- is not a crossing
        /// and is stepped past.
        ///
        /// The search is a plain segment-by-segment intersection in plan, after a bounding-box reject per road. That is
        /// O(rail x road) in the worst case and the box makes the ordinary case nearly free, which matters because a
        /// city hands this a hundred roads and only two of them come anywhere near the line. </summary>
        private static void FindCrossings(RailAlignmentPlan plan, IList<RailCrossingRoad> roads,
            RailFormationClass form)
        {
            if (roads == null || plan.samples.Count < 2) { return; }
            for (int r = 0; r < roads.Count; r++)
            {
                RailCrossingRoad road = roads[r];
                if (road == null || road.gradeSeparated) { continue; }
                if (road.centreline == null || road.centreline.Count < 2) { continue; }
                if (!BoxesTouch(plan.samples, road.centreline, road.halfWidthMetres)) { continue; }
                FindCrossingsOn(plan, road, form);
            }
        }

        /// <summary> Whether a road comes anywhere near the railway in plan, so the segment loop is only entered for a
        /// road that could possibly cross. The road's half width is added to its box, because a road whose centreline
        /// stops just short of the line still lays tarmac over it. </summary>
        private static bool BoxesTouch(List<RailAlignmentSample> samples, List<Vector3> road, float halfWidth)
        {
            float railMinX = float.MaxValue, railMaxX = float.MinValue;
            float railMinZ = float.MaxValue, railMaxZ = float.MinValue;
            for (int i = 0; i < samples.Count; i++)
            {
                Vector3 p = samples[i].position;
                if (p.x < railMinX) { railMinX = p.x; }
                if (p.x > railMaxX) { railMaxX = p.x; }
                if (p.z < railMinZ) { railMinZ = p.z; }
                if (p.z > railMaxZ) { railMaxZ = p.z; }
            }
            float roadMinX = float.MaxValue, roadMaxX = float.MinValue;
            float roadMinZ = float.MaxValue, roadMaxZ = float.MinValue;
            for (int i = 0; i < road.Count; i++)
            {
                Vector3 p = road[i];
                if (p.x < roadMinX) { roadMinX = p.x; }
                if (p.x > roadMaxX) { roadMaxX = p.x; }
                if (p.z < roadMinZ) { roadMinZ = p.z; }
                if (p.z > roadMaxZ) { roadMaxZ = p.z; }
            }
            float pad = halfWidth > 0f ? halfWidth : 0f;
            if (roadMaxX + pad < railMinX || roadMinX - pad > railMaxX) { return false; }
            if (roadMaxZ + pad < railMinZ || roadMinZ - pad > railMaxZ) { return false; }
            return true;
        }

        /// <summary> One road against every rail segment. Walked in ascending order on both sides so a road that
        /// crosses the line three times reports its three crossings in the same order every time (C-06). </summary>
        private static void FindCrossingsOn(RailAlignmentPlan plan, RailCrossingRoad road, RailFormationClass form)
        {
            double margin = form.crossingMarginMetres > 0f ? form.crossingMarginMetres : 0.0;
            double width = 2.0 * (road.halfWidthMetres > 0f ? road.halfWidthMetres : 4.0);

            for (int i = 0; i < plan.samples.Count - 1; i++)
            {
                RailAlignmentSample a = plan.samples[i];
                RailAlignmentSample b = plan.samples[i + 1];
                for (int j = 0; j < road.centreline.Count - 1; j++)
                {
                    double t, u;
                    if (!SegmentsCross(a.position, b.position, road.centreline[j], road.centreline[j + 1],
                            out t, out u))
                    {
                        continue;
                    }

                    float station = (float)(a.stationMetres + t * (b.stationMetres - a.stationMetres));
                    if (AlreadyRecorded(plan, road.roadName, station)) { continue; }

                    RailLevelCrossing crossing = new RailLevelCrossing();
                    crossing.roadName = road.roadName;
                    crossing.railStationMetres = station;
                    crossing.position = Vector3.Lerp(a.position, b.position, (float)t);
                    crossing.roadWidthMetres = width;
                    crossing.appliedCantMillimetres = a.appliedCantMillimetres
                        + t * (b.appliedCantMillimetres - a.appliedCantMillimetres);
                    crossing.crossingAngleDegrees = CrossingAngleDegrees(a.position, b.position,
                        road.centreline[j], road.centreline[j + 1]);
                    crossing.panelLengthMetres = PanelLengthMetres(width + 2.0 * margin,
                        crossing.crossingAngleDegrees);
                    plan.crossings.Add(crossing);

                    if (crossing.crossingAngleDegrees < plan.sharpestCrossingAngleDegrees)
                    {
                        plan.sharpestCrossingAngleDegrees = crossing.crossingAngleDegrees;
                    }
                }
            }
        }

        /// <summary> Whether this crossing has already been recorded for this road. Two neighbouring rail segments that
        /// share a vertex will both report a road passing exactly through it, and one crossing counted twice would put
        /// two sets of panels in the same place. Half a metre is the tolerance: closer than that is the same crossing,
        /// further apart than that on the same road is a road that genuinely doubles back. </summary>
        private static bool AlreadyRecorded(RailAlignmentPlan plan, string roadName, float stationMetres)
        {
            for (int i = 0; i < plan.crossings.Count; i++)
            {
                if (plan.crossings[i].roadName != roadName) { continue; }
                if (Math.Abs(plan.crossings[i].railStationMetres - stationMetres) < 0.5f) { return true; }
            }
            return false;
        }

        /// <summary> Whether two segments cross in PLAN, and where along each. Public and pure because it is the whole
        /// of the crossing search's geometry and D-16 proves it with four invented points rather than two alignments.
        ///
        /// Height is deliberately ignored. A road and a railway that cross in plan at different heights are a bridge,
        /// and whether they are is B-16's answer arriving as <see cref="RailCrossingRoad.gradeSeparated"/> -- deciding
        /// it here from two Y values would be a second answer to it, and a wrong one, since a road ramping up to a
        /// bridge is level with the track somewhere along the ramp. </summary>
        public static bool SegmentsCross(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2,
            out double alongA, out double alongB)
        {
            alongA = 0.0;
            alongB = 0.0;
            double rx = a2.x - a1.x;
            double rz = a2.z - a1.z;
            double sx = b2.x - b1.x;
            double sz = b2.z - b1.z;
            double denominator = rx * sz - rz * sx;
            if (Math.Abs(denominator) < 1e-9) { return false; }

            double qx = b1.x - a1.x;
            double qz = b1.z - a1.z;
            double t = (qx * sz - qz * sx) / denominator;
            double u = (qx * rz - qz * rx) / denominator;
            if (t < 0.0 || t > 1.0 || u < 0.0 || u > 1.0) { return false; }
            alongA = t;
            alongB = u;
            return true;
        }

        /// <summary> The angle between a rail segment and a road segment, in degrees, folded into 0 to 90 so a square
        /// crossing is 90 whichever way either line was drawn. </summary>
        public static double CrossingAngleDegrees(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2)
        {
            double rx = a2.x - a1.x;
            double rz = a2.z - a1.z;
            double sx = b2.x - b1.x;
            double sz = b2.z - b1.z;
            double rLength = Math.Sqrt(rx * rx + rz * rz);
            double sLength = Math.Sqrt(sx * sx + sz * sz);
            if (rLength <= 1e-9 || sLength <= 1e-9) { return 90.0; }
            double cosine = Math.Abs((rx * sx + rz * sz) / (rLength * sLength));
            if (cosine > 1.0) { cosine = 1.0; }
            return Math.Acos(cosine) * 180.0 / Math.PI;
        }

        /// <summary> How long the crossing panel is along the track: the road's full width plus its margins, divided by
        /// the sine of the crossing angle. The sine is floored at ten degrees' worth. That is not a fudge to avoid a
        /// division by zero -- the parallel case never reaches here, since parallel segments do not intersect -- it is
        /// the point at which a "crossing" has stopped being one: a road meeting the track at five degrees is a road
        /// running ALONGSIDE it that happens to touch, and reporting a 130 m panel for it would be arithmetic rather
        /// than an answer. </summary>
        public static double PanelLengthMetres(double totalWidthMetres, double crossingAngleDegrees)
        {
            double sine = Math.Sin(crossingAngleDegrees * Math.PI / 180.0);
            double floorSine = Math.Sin(10.0 * Math.PI / 180.0);
            if (sine < floorSine) { sine = floorSine; }
            return totalWidthMetres / sine;
        }
    }


    /// <summary> B-27 / D-5's authoring surface: a railway, placed the way a road is placed.
    ///
    /// It is a component rather than a node or a scriptable object because that is what the author asked for -- a
    /// railway is a CHILD placed like a road, with its own point list and its own inspector -- and because a railway
    /// belongs to the scene it runs through, not to a library.
    ///
    /// This component computes and refuses; it never mutates a scene, never touches an asset and never draws. The rail
    /// mesh, the ballast, the sleepers and the crossing panels are built by the editor half, which is also what keeps a
    /// built railway BAKED rather than assembled at play time. Reading the scene is not mutating it: the crossing
    /// search needs the roads, and asking which roads exist is the only way to find the pair a level crossing is a
    /// property of. </summary>
    [DisallowMultipleComponent]
    public sealed class RailAlignment : MonoBehaviour
    {
        public RailAlignmentSettings settings = new RailAlignmentSettings();

        /// <summary> How finely a road's spline is sampled before it is offered to the crossing search. 5 m matches the
        /// railway's own default sampling, so neither side of a crossing is the coarse one. </summary>
        public float roadSampleSpacingMetres = 5f;

        /// <summary> Plan this railway. The ground arrives as a reader rather than as a terrain so the same call works
        /// under a provider that owns the surface, and the roads may be supplied by the caller -- which is how a gate
        /// hands it two invented ones -- or left null to be read from the scene. </summary>
        public RailAlignmentPlan Plan(ITerrainSurfaceReader ground, IList<RailCrossingRoad> roads, out string note)
        {
            RailAlignmentSettings s = settings != null ? settings : new RailAlignmentSettings();
            IList<RailCrossingRoad> offered = roads != null ? roads : ResolveRoads();
            return RailAlignmentPlanner.Plan(s, ground, offered, out note);
        }

        /// <summary> Plan the overhead line for this railway, from an alignment already planned.
        ///
        /// It takes the alignment rather than planning one, and that is the seam this method exists to make: staking a
        /// railway is expensive and an author pressing "wire it" has already staked it, so re-staking here would double
        /// the work and -- worse -- give the wire a second answer to where the rail head is. A caller with no plan in
        /// hand calls <see cref="Plan"/> first and passes what it returns.
        ///
        /// Returns null with a note where there is nothing to wire, which includes an unwired run. </summary>
        public RailOverheadPlan PlanOverhead(RailAlignmentPlan alignment, ITerrainSurfaceReader ground,
            out string note)
        {
            RailAlignmentSettings s = settings != null ? settings : new RailAlignmentSettings();
            RailOverheadClass cls = s.overhead != null ? s.overhead : RailOverheadClass.Default();
            return RailOverheadPlanner.Plan(alignment, s, cls, s.overheadStructures, ground, out note);
        }

        /// <summary> Every road in the scene, as a centreline and a half width, sorted by name.
        ///
        /// The sort is the point of this method rather than an afterthought. <c>FindObjectsByType</c> promises no order
        /// -- its own wrapper says so -- and an unordered road list would make the crossing list come out in a
        /// different order from one selection to the next, which is exactly the kind of thing C-06 forbids and the kind
        /// nobody notices until a diff of two identical builds is not empty. Ordinal, so it does not depend on the
        /// editor's locale either. </summary>
        public List<RailCrossingRoad> ResolveRoads()
        {
            List<RailCrossingRoad> result = new List<RailCrossingRoad>();
            Road[] roads = EngineIntegration.FindObjectsByType<Road>();
            if (roads == null) { return result; }

            List<Road> ordered = new List<Road>(roads.Length);
            for (int i = 0; i < roads.Length; i++)
            {
                if (roads[i] != null) { ordered.Add(roads[i]); }
            }
            ordered.Sort(delegate(Road a, Road b)
            {
                return string.CompareOrdinal(a.name, b.name);
            });

            for (int i = 0; i < ordered.Count; i++)
            {
                OfferRoad(ordered[i], result);
            }
            return result;
        }

        /// <summary> Offer one road to the crossing search, as its AT-GRADE runs.
        ///
        /// A road is not grade separated as a whole -- it is grade separated where its spline says bridge or tunnel --
        /// so it is offered as one run per stretch of it on the ground, broken at those spans. That is what makes
        /// a road bridging the railway produce no crossing without this file deciding anything about bridges: the
        /// stretch that would have crossed simply is not in any run. Answering it the other way, by testing two Y
        /// values at the intersection, would find a crossing halfway up every approach ramp.
        ///
        /// The span test is <see cref="RoadPowerLine.InSpan"/> rather than a copy of it, which is why that method is
        /// public. </summary>
        private void OfferRoad(Road road, List<RailCrossingRoad> into)
        {
            SplineC spline = road.spline;
            float length = spline != null ? spline.distance : -1f;
            if (length <= 0f) { return; }

            float halfWidth = road.RoadWidth() * 0.5f;
            float spacing = roadSampleSpacingMetres > 0.5f ? roadSampleSpacingMetres : 5f;
            int intervals = Mathf.Max(1, Mathf.CeilToInt(length / spacing));

            RailCrossingRoad run = null;
            for (int i = 0; i <= intervals; i++)
            {
                float t = spline.TranslateDistBasedToParam(Mathf.Lerp(0f, length, (float)i / intervals));
                if (RoadPowerLine.InSpan(spline.BridgeParams, t) || RoadPowerLine.InSpan(spline.TunnelParams, t))
                {
                    run = CloseRun(run, into);
                    continue;
                }
                if (run == null)
                {
                    run = new RailCrossingRoad();
                    run.roadName = road.name;
                    run.halfWidthMetres = halfWidth;
                }
                run.centreline.Add(spline.GetSplineValue(t, false));
            }
            CloseRun(run, into);
        }

        /// <summary> Finish an at-grade run and start the next. A run of one point is dropped rather than kept: one
        /// point has no segment, and a segmentless run would be walked by the crossing search for nothing. </summary>
        private static RailCrossingRoad CloseRun(RailCrossingRoad run, List<RailCrossingRoad> into)
        {
            if (run != null && run.centreline.Count > 1) { into.Add(run); }
            return null;
        }
    }
}
