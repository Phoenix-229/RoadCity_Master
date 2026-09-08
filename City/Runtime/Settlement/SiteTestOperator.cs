#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-4. Everything the site test is handed, and nothing it could reach for itself. It carries no
    /// scene reference and no provider: the ground arrives as an <see cref="ITerrainSurfaceReader"/>, the cost surface as
    /// the raster <c>SuitabilityMapBuilder.Build</c> already produced, and every threshold as a serialised field on an
    /// object the author edited -- <see cref="BuildabilityCriteria"/>, <see cref="ZoneTypeClass"/>,
    /// <see cref="RoadClass"/>. That is what lets a gate run this test headless against an array.
    ///
    /// It asks the question no node asked before it: *is there a settlement's worth of buildable land here*, put BEFORE
    /// the street network, the blocks and the lots rather than after all three. </summary>
    public sealed class SiteTestInput
    {
        /// <summary> The cost surface the corridor stage already built for <see cref="roadClass"/>. Read, never rebuilt:
        /// the test takes the stored <c>slopeFraction</c>, <c>earthworkVolumeM3</c> and <c>forbidden</c> cells and the
        /// shipped resistance functions, so a site is judged by the same numbers a route is. </summary>
        public SuitabilityRasterClass suitability;

        /// <summary> The regions to judge, in world XZ metres -- <c>SettlementSiting</c>'s
        /// <c>SettlementCandidate.regionWorldXZ</c>, one per road end that declared a settlement role. </summary>
        public List<Rect> candidateRegionsXZ = new List<Rect>();

        /// <summary> The name of each region, parallel to <see cref="candidateRegionsXZ"/>: the candidate's own identity,
        /// so a refusal is addressed to the road that caused it. A short or null list is allowed and numbered instead; a
        /// wrong name would be worse than no name. </summary>
        public List<string> candidateIdentities = new List<string>();

        /// <summary> The buildability thresholds, authored: maximum slope in PER CENT, minimum lot area, water freeboard
        /// and maximum pad relief. </summary>
        public BuildabilityCriteria criteria;

        /// <summary> The water surface as one authored world-Y, with <see cref="float.NegativeInfinity"/> for a world
        /// with no water. The tool's one water convention, and the same figure <c>BuildableLandTest.Evaluate</c> and
        /// <c>TunnelRunReader</c> take. </summary>
        public float waterSurfaceY = float.NegativeInfinity;

        /// <summary> The zone that governs this ground: its maximum slope in DEGREES, its shoreline setback in metres,
        /// and whether it is bound to the water edge. </summary>
        public ZoneTypeClass zone;
        /// <summary> The functional road class this map was built for: its maximum gradient as a FRACTION, and its
        /// target spacing, which is what the network resistance decays over. The class is checked against the raster's
        /// own <c>roadClassId</c> rather than trusted, because a map built for a boulevard says nothing about a lane.
        /// </summary>
        public RoadClass roadClass;

        /// <summary> Where the ground comes from. The raster holds slope but no elevation, and relief and freeboard are
        /// elevation questions, so the surface is read for them -- through the interface every other city-layer sampling
        /// pass reads, never a provider held here. </summary>
        public ITerrainSurfaceReader surface;

        /// <summary> How far the nearest existing road is, when anything can say. Optional exactly as it is optional to
        /// the map builder: an absent source contributes nothing rather than inventing a distance. </summary>
        public ISuitabilityNetworkSource network;

        public SiteTestInput() { }

        public SiteTestInput(SuitabilityRasterClass suitability, ITerrainSurfaceReader surface, RoadClass roadClass,
            ZoneTypeClass zone, BuildabilityCriteria criteria, float waterSurfaceY)
        {
            this.suitability = suitability;
            this.surface = surface;
            this.roadClass = roadClass;
            this.zone = zone;
            this.criteria = criteria;
            this.waterSurfaceY = waterSurfaceY;
        }

        /// <summary> The candidate's name, or its number where the author gave none. </summary>
        public string IdentityAt(int index)
        {
            if (candidateIdentities != null && index >= 0 && index < candidateIdentities.Count
                && !string.IsNullOrEmpty(candidateIdentities[index]))
            {
                return candidateIdentities[index];
            }
            return "candidate " + (index + 1).ToString(CultureInfo.InvariantCulture);
        }
        /// <summary> Whether the test can run at all, naming the first thing that is missing or unauthored. It refuses
        /// rather than substituting a default, because every one of these numbers is an authored decision and a silent
        /// stand-in would refuse or accept ground on a figure the author never set. </summary>
        public bool IsUsable(out string reason)
        {
            if (suitability == null || suitability.widthCells <= 0 || suitability.depthCells <= 0)
            {
                reason = "no suitability raster was given to the site test, so there is no cost surface to reduce."
                         + " Build one with SuitabilityMapBuilder.Build for the road class being tested";
                return false;
            }
            int cells = SuitabilityRaster.CellCount(suitability);
            if (suitability.slopeFraction == null || suitability.slopeFraction.Length < cells
                || suitability.earthworkVolumeM3 == null || suitability.earthworkVolumeM3.Length < cells
                || suitability.forbidden == null || suitability.forbidden.Length < cells
                || suitability.cost == null || suitability.cost.Length < cells)
            {
                reason = "the suitability raster is " + SuitabilityRaster.SizeLine(suitability) + " but its per-cell"
                         + " arrays are shorter than that, so it was not filled by SuitabilityMapBuilder.Build";
                return false;
            }
            if (surface == null)
            {
                reason = "no surface reader was given to the site test. The raster holds slope but no elevation, and"
                         + " relief and freeboard are elevation questions, so neither can be measured without it";
                return false;
            }
            if (roadClass == null)
            {
                reason = "no road class was given to the site test, so the maximum gradient the streets of this"
                         + " settlement may climb is unknown";
                return false;
            }
            if (zone == null)
            {
                reason = "no zone type was given to the site test, so the maximum slope, the shoreline setback and the"
                         + " water-edge rule this ground is governed by are all unknown";
                return false;
            }
            if (roadClass.id != suitability.roadClassId)
            {
                reason = "the suitability raster was built for road class " + suitability.roadClassId
                         + " and the site test was given class " + roadClass.id + " (" + roadClass.displayName
                         + "). A lane width and a maximum gradient move every number in the map, so a map built for one"
                         + " class says nothing about another";
                return false;
            }
            return ThresholdsAreAuthored(out reason);
        }
        /// <summary> The five authored numbers, checked for having been authored at all. A zero maximum slope refuses
        /// every cell and a zero minimum lot area accepts a site with no land in it; both are the unset value of a
        /// serialised field rather than a decision, and saying so is more use than a run that refuses everything.
        /// </summary>
        private bool ThresholdsAreAuthored(out string reason)
        {
            if (!(criteria.maxSlopePercent > 0f) || !(criteria.minLotArea > 0f) || !(criteria.maxReliefMetres > 0f)
                || criteria.waterFreeboard < 0f || float.IsNaN(criteria.waterFreeboard))
            {
                reason = "the buildability criteria are unauthored (maximum slope "
                         + criteria.maxSlopePercent.ToString("0.#", CultureInfo.InvariantCulture) + " %, minimum lot area "
                         + criteria.minLotArea.ToString("0", CultureInfo.InvariantCulture) + " m^2, freeboard "
                         + criteria.waterFreeboard.ToString("0.#", CultureInfo.InvariantCulture) + " m, relief "
                         + criteria.maxReliefMetres.ToString("0.#", CultureInfo.InvariantCulture)
                         + " m). Pass BuildabilityCriteria.Default() or the zone's own figures";
                return false;
            }
            if (!(zone.maxSlopeDegrees > 0f) || zone.shorelineSetbackMetres < 0f)
            {
                reason = "zone " + zone.zoneId + " has a maximum slope of "
                         + zone.maxSlopeDegrees.ToString("0.#", CultureInfo.InvariantCulture) + " degrees and a shoreline"
                         + " setback of " + zone.shorelineSetbackMetres.ToString("0.#", CultureInfo.InvariantCulture)
                         + " m, which no ground can satisfy. Author them on the zone type";
                return false;
            }
            if (!(roadClass.maxGradient > 0f) || !(roadClass.targetSpacing > 0f))
            {
                reason = "road class " + roadClass.displayName + " has a maximum gradient of "
                         + roadClass.maxGradient.ToString("0.###", CultureInfo.InvariantCulture) + " and a target spacing"
                         + " of " + roadClass.targetSpacing.ToString("0.#", CultureInfo.InvariantCulture)
                         + " m; neither can be zero, since one bounds the ground its streets may climb and the other is"
                         + " the distance the network resistance decays over";
                return false;
            }
            if (float.IsNaN(waterSurfaceY) || float.IsPositiveInfinity(waterSurfaceY))
            {
                reason = "the site test was given a water level of "
                         + waterSurfaceY.ToString("0.#", CultureInfo.InvariantCulture) + " m, which states neither a"
                         + " water surface nor the absence of one. Pass a world-Y, or float.NegativeInfinity for a world"
                         + " with no water";
                return false;
            }
            if (candidateRegionsXZ == null || candidateRegionsXZ.Count < 1)
            {
                reason = "the site test was given no candidate regions to judge";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
    /// <summary> B-15 / C-4. What the ground under ONE candidate region measures, and the verdict that follows from it.
    ///
    /// Every field is either a measurement or the authored threshold it was compared against -- never a score. A score
    /// tells an author their site is bad; a measurement tells them it is 3.2 m of relief per lot on ground at 19 %, and
    /// which of the three authored slope limits refused it. That is the difference this struct exists for.
    ///
    /// A refused verdict is RETURNED rather than dropped, for the same reason <c>SettlementCandidate</c> keeps its
    /// refusals: an author whose city did not appear needs to be told which region and why. </summary>
    public struct SiteVerdict
    {
        /// <summary> The candidate this judges, named as the caller named it. </summary>
        public string identity;

        /// <summary> The region judged, in world XZ metres. </summary>
        public Rect regionXZ;

        /// <summary> Cells of the raster whose centres fall inside <see cref="regionXZ"/>. Zero means the region lies off
        /// the map, which is a refusal rather than a clean site. </summary>
        public int cellsInside;

        /// <summary> Cells whose ground the surface reader could actually answer for. </summary>
        public int cellsReadable;

        /// <summary> Cells that pass every authored test: readable, not forbidden ground, within the governing slope
        /// limit, clear of the water freeboard, on the correct side of the shoreline setback, and inside the pad relief
        /// limit. </summary>
        public int cellsBuildable;

        /// <summary> <see cref="cellsBuildable"/> over <see cref="cellsInside"/>, as a fraction. </summary>
        public float buildableFraction;

        /// <summary> The buildable cells as ground, in square metres. This is the figure <c>criteria.minLotArea</c> is
        /// compared against: a site that cannot hold one lot of the zone's own minimum is not a site. </summary>
        public float buildableAreaM2;

        /// <summary> Cells the raster itself marks as ground this road class cannot climb. </summary>
        public int cellsForbidden;

        /// <summary> Cells refused for slope, for water freeboard, for the shoreline setback, for pad relief, and for
        /// ground that could not be read. Kept apart because *why not there* is the question, and a single count of
        /// rejected cells answers none of it. </summary>
        public int cellsRefusedBySlope;
        public int cellsRefusedByWater;
        public int cellsRefusedByShoreline;
        public int cellsRefusedByRelief;
        public int cellsUnreadable;
        /// <summary> The steepest and the mean ground inside the region, as a FRACTION (rise over run), read from the
        /// raster's own <c>slopeFraction</c> so a site is judged by the figure a route was. </summary>
        public float worstSlopeFraction;
        public float meanSlopeFraction;

        /// <summary> The slope limit that actually bound this site, as a fraction, and the name of where it came from:
        /// the road class gradient, the criteria's per cent, or the zone's degrees. The three are authored in three
        /// different units and the strictest wins, so which one refused the ground is itself a finding. </summary>
        public float governingSlopeFraction;
        public string governingSlopeSource;

        /// <summary> The worst relief any single cell would ask a pad to absorb, in metres -- the largest
        /// max-minus-min over one cell's five samples. Compared against <c>criteria.maxReliefMetres</c>, which is a PAD
        /// limit and belongs to a cell, never to a whole settlement: a city on a valley side has 200 m of relief across
        /// it and every pad in it is flat. </summary>
        public float worstCellReliefMetres;

        /// <summary> The lowest and highest ground read anywhere inside the region, in metres, and their difference --
        /// the site's own relief, reported because an author reads a settlement's lie of the land from it, and NOT
        /// compared against the pad limit. </summary>
        public float lowestGroundY;
        public float highestGroundY;
        public float siteReliefMetres;

        /// <summary> How far the lowest buildable ground stands above the water surface, in metres, or
        /// <see cref="float.NaN"/> in a world with no water. The margin itself, not the pass or fail: an author siting a
        /// port wants to know it is 1.4 m clear. </summary>
        public float freeboardMetres;

        /// <summary> Metres from the nearest buildable cell to the water edge, or <see cref="float.NaN"/> with no water
        /// or no water within the searched band. A zone that is <c>boundToWaterEdge</c> is REFUSED when this is NaN, and
        /// that is the binding half of the shoreline rule: the setback says how close a building may come, the binding
        /// says a fishing village that touches no water is not a fishing village. </summary>
        public float metresToWaterEdge;

        /// <summary> The shortest distance from any buildable cell to an existing road, in metres, or
        /// <see cref="float.NaN"/> when no network source was given. Never invented: an absent source contributes
        /// nothing, exactly as it contributes nothing to the map builder. </summary>
        public float metresToNearestRoad;

        /// <summary> The earthwork the buildable ground would cost, in cubic metres PER METRE of road, summed and
        /// averaged over the buildable cells. The raster stores a per-metre bench volume rather than a per-cell volume
        /// (<c>SuitabilityMapBuilder</c> writes <c>BenchVolumePerMetreM3</c> straight into the cell), so it is carried
        /// in that unit and never multiplied into a site volume, which would be a formula this tool has not derived.
        /// </summary>
        public float totalEarthworkM3PerMetre;
        public float meanEarthworkM3PerMetre;
        /// <summary> The five resistances of the shipped cost model, averaged over the cells inside the region, each in
        /// its own 0..1 terms and NOT yet weighted. They are carried so <c>SiteRankOperator</c> can weight them with the
        /// author's own <c>SuitabilityWeights</c> and name the binding one, instead of this test collapsing them into a
        /// number nobody can take apart again. </summary>
        public float meanSlopeResistance;
        public float meanEarthworkResistance;
        public float meanWaterResistance;
        public float meanLandUseResistance;
        public float meanNetworkResistance;

        /// <summary> Whether a land-use resistance was measurable at all -- always false, and stated rather than left as
        /// a bare zero. The zone that governs this ground is the settlement's own INTENT, not land a route would have to
        /// buy and demolish, so pricing it as land use would penalise exactly the dense zone the author asked for. The
        /// factor is therefore left at zero for the same reason the map builder leaves it at zero with no source, and the
        /// report says so. </summary>
        public bool landUseMeasured;

        /// <summary> Whether a per-cell water fraction was measurable. The raster does not store one and no water source
        /// reaches a site test, so <see cref="meanWaterResistance"/> is zero and this is false. The site's water rule is
        /// the freeboard and the shoreline setback below, which are stricter than a crossing cost and are the rules an
        /// author authored for buildings rather than for a road. </summary>
        public bool waterFractionMeasured;

        /// <summary> The mean of the raster's OWN stored resistance over the cells inside the region -- what the map
        /// builder computed with whatever water and land-use sources it had. Carried beside the recomputed means so a
        /// reader can see both the figure the corridor stage produced and the figure this test could reproduce from
        /// stored measurements. </summary>
        public float meanStoredResistance;

        /// <summary> Whether this region can hold a settlement. </summary>
        public bool accepted;

        /// <summary> On a refusal, which of the cost model's factors carried it, so the inspector can group refusals by
        /// cause. <see cref="SuitabilityFactor.None"/> where the cause is not one of the five -- a region off the map, or
        /// too little buildable land in total. </summary>
        public SuitabilityFactor refusalFactor;

        /// <summary> The refusal in the author's words, with the measurement and the threshold in it. Empty on an
        /// accepted site. </summary>
        public string refusal;

        /// <summary> One line for the inspector and the report: what was measured and what it means. </summary>
        public string Describe()
        {
            StringBuilder text = new StringBuilder();
            text.Append(string.IsNullOrEmpty(identity) ? "site" : identity);
            text.Append(accepted ? ": buildable, " : ": refused, ");
            text.Append((buildableFraction * 100f).ToString("0.#", CultureInfo.InvariantCulture));
            text.Append(" % of ").Append(cellsInside.ToString(CultureInfo.InvariantCulture));
            text.Append(" cells (").Append(buildableAreaM2.ToString("0", CultureInfo.InvariantCulture));
            text.Append(" m^2), slope ").Append((meanSlopeFraction * 100f).ToString("0.#", CultureInfo.InvariantCulture));
            text.Append(" % mean and ").Append((worstSlopeFraction * 100f).ToString("0.#", CultureInfo.InvariantCulture));
            text.Append(" % worst against ").Append((governingSlopeFraction * 100f).ToString("0.#",
                CultureInfo.InvariantCulture));
            text.Append(" % (").Append(governingSlopeSource).Append("), worst pad relief ");
            text.Append(worstCellReliefMetres.ToString("0.#", CultureInfo.InvariantCulture)).Append(" m");
            if (!float.IsNaN(freeboardMetres))
            {
                text.Append(", freeboard ").Append(freeboardMetres.ToString("0.#", CultureInfo.InvariantCulture));
                text.Append(" m");
            }
            if (!float.IsNaN(metresToWaterEdge))
            {
                text.Append(", water edge ").Append(metresToWaterEdge.ToString("0.#", CultureInfo.InvariantCulture));
                text.Append(" m away");
            }
            if (!float.IsNaN(metresToNearestRoad))
            {
                text.Append(", nearest road ").Append(metresToNearestRoad.ToString("0", CultureInfo.InvariantCulture));
                text.Append(" m");
            }
            text.Append(", earthwork ").Append(meanEarthworkM3PerMetre.ToString("0.##", CultureInfo.InvariantCulture));
            text.Append(" m^3 per metre of road, mean");
            int refused = cellsRefusedBySlope + cellsRefusedByWater + cellsRefusedByShoreline + cellsRefusedByRelief
                          + cellsUnreadable + cellsForbidden;
            if (refused > 0)
            {
                text.Append(". Refused cells: ").Append(cellsRefusedBySlope.ToString(CultureInfo.InvariantCulture));
                text.Append(" for slope, ").Append(cellsForbidden.ToString(CultureInfo.InvariantCulture));
                text.Append(" impassable to this road class, ");
                text.Append(cellsRefusedByWater.ToString(CultureInfo.InvariantCulture)).Append(" for water, ");
                text.Append(cellsRefusedByShoreline.ToString(CultureInfo.InvariantCulture));
                text.Append(" inside the shoreline setback, ");
                text.Append(cellsRefusedByRelief.ToString(CultureInfo.InvariantCulture)).Append(" for pad relief, ");
                text.Append(cellsUnreadable.ToString(CultureInfo.InvariantCulture)).Append(" unreadable");
            }
            if (!accepted && !string.IsNullOrEmpty(refusal))
            {
                text.Append(". ").Append(refusal);
            }
            return text.ToString();
        }
    }
    /// <summary> B-15 / C-4. Every candidate's verdict, accepted and refused alike, plus what the whole run was measured
    /// against. This is what the operator outputs and what <c>SiteRankOperator</c> takes as its input, so it carries the
    /// provenance a ranking has to be able to trust: the road class and zone the ground was judged for, the cell size the
    /// areas were computed from, and whether a network source and a water surface existed at all.
    ///
    /// It is not a filtered list. Refusals stay in it because the author who drew a road that earned no city is the one
    /// person who needs the sentence explaining why. </summary>
    public sealed class SiteVerdictSet
    {
        /// <summary> One verdict per candidate region, in the order they were given. </summary>
        public List<SiteVerdict> verdicts = new List<SiteVerdict>();

        /// <summary> Whether the test ran at all. False when the input refused itself, and then
        /// <see cref="refusalReason"/> is the reason <c>SiteTestInput.IsUsable</c> gave. </summary>
        public bool ran;

        /// <summary> Why nothing was judged, when nothing was. Empty on a run that happened. </summary>
        public string refusalReason = string.Empty;

        /// <summary> The functional road class and the zone every verdict in this set was judged for, by id and name, so
        /// a set can never be silently ranked against the wrong ground rules. </summary>
        public int roadClassId;
        public string roadClassName = string.Empty;
        public string zoneId = string.Empty;

        /// <summary> The raster's cell size in metres and the ground one cell covers in square metres -- the figure every
        /// <c>buildableAreaM2</c> in here was multiplied out from. </summary>
        public float metresPerCell;
        public float cellAreaM2;

        /// <summary> The water surface every freeboard and setback in this set was measured against, as the tool's one
        /// water convention: a world-Y, or <see cref="float.NegativeInfinity"/> for a world with no water. </summary>
        public float waterSurfaceY = float.NegativeInfinity;

        /// <summary> Whether a network source answered, so a NaN <c>metresToNearestRoad</c> can be read as "nobody
        /// asked" rather than "nothing near". </summary>
        public bool networkMeasured;

        /// <summary> How many candidates can hold a settlement. </summary>
        public int AcceptedCount()
        {
            int count = 0;
            for (int i = 0; i < verdicts.Count; i++)
            {
                if (verdicts[i].accepted) { count++; }
            }
            return count;
        }
        /// <summary> The one line a caller logs: how many candidates were judged, how many survived, and what they were
        /// judged against. </summary>
        public string Summary()
        {
            if (!ran)
            {
                return "the site test did not run: " + refusalReason;
            }
            StringBuilder text = new StringBuilder();
            text.Append(AcceptedCount().ToString(CultureInfo.InvariantCulture)).Append(" of ");
            text.Append(verdicts.Count.ToString(CultureInfo.InvariantCulture));
            text.Append(" candidate sites can hold a settlement, judged for road class ");
            text.Append(roadClassName).Append(" and zone ").Append(zoneId).Append(" on ");
            text.Append(metresPerCell.ToString("0.#", CultureInfo.InvariantCulture)).Append(" m cells");
            if (float.IsNegativeInfinity(waterSurfaceY))
            {
                text.Append(", in a world with no water");
            }
            else
            {
                text.Append(", water at ").Append(waterSurfaceY.ToString("0.#", CultureInfo.InvariantCulture))
                    .Append(" m");
            }
            if (!networkMeasured)
            {
                text.Append(", with no existing-road source, so distance to the network was not measured");
            }
            return text.ToString();
        }

        /// <summary> The full report: the summary, then one line per candidate. What an author reads when their city did
        /// not appear where they drew the road for it. </summary>
        public string Report()
        {
            StringBuilder text = new StringBuilder();
            text.Append(Summary());
            for (int i = 0; i < verdicts.Count; i++)
            {
                text.Append('\n').Append("  ").Append(verdicts[i].Describe());
            }
            return text.ToString();
        }
    }
    /// <summary> B-15 / C-4. The node that asks whether a settlement's worth of buildable land exists, BEFORE the street
    /// network, the blocks and the lots -- which is the whole fix. <c>BuildableLandTest</c> does test water, but a lot is
    /// the fourth thing produced, so a sea-sited city today generates a network in the sea, finds blocks in the sea,
    /// subdivides lots in the sea, and only then rejects them one at a time. This asks first.
    ///
    /// It builds no second suitability map: it REDUCES the one the corridor stage already produced, cell by cell, over
    /// each candidate region. It invents no site-levelling volume formula -- <c>BenchVolumePerMetreM3</c> is the derived
    /// one that exists, pads belong to <c>BuildingPadConform</c> -- and it hard-codes no threshold: every limit it applies
    /// is a serialised field on <c>BuildabilityCriteria</c>, <c>ZoneTypeClass</c>, <c>RoadClass</c>, or on this asset.
    ///
    /// The seed is unused, and that is the honest signature of a measurement: two identical candidates on identical
    /// ground must earn identical verdicts, so there is nothing here for a seed to vary. Ties between equally good sites
    /// are <c>SiteRankOperator</c>'s to break, and it breaks them through <c>CitySeed.ForItem</c>. </summary>
    [CreateAssetMenu(fileName = "SiteTestOperator", menuName = "Road City Master/Operators/Site Test", order = 12)]
    public sealed class SiteTestOperator : CityOperator<SiteTestInput, SiteVerdictSet>
    {
        /// <summary> The least share of a candidate region that must be buildable for the region to be a site, as a
        /// fraction. A site is not the sum of its buildable cells scattered through a marsh: a street network needs
        /// contiguous ground, and a region three quarters refused cannot carry one. Authored here rather than on the zone
        /// because it is a property of how much land a settlement needs, not of what may be built on it, and the refusal
        /// names this field so an author knows which number to change. </summary>
        [SerializeField, Range(0.05f, 1f)] private float minBuildableFraction = 0.25f;

        /// <summary> The least number of buildable cells a site must have, whatever the fraction says. A region smaller
        /// than a few cells can be 100 % buildable and still be too small to measure honestly -- the fraction of one cell
        /// is either 0 or 1. </summary>
        [SerializeField, Min(1)] private int minBuildableCells = 4;

        /// <summary> Judges every candidate region and returns all of them, accepted and refused alike. </summary>
        public override SiteVerdictSet Execute(SiteTestInput input, int operatorSeed)
        {
            SiteVerdictSet set = new SiteVerdictSet();
            if (input == null)
            {
                set.ran = false;
                set.refusalReason = "no input was given to the site test";
                return set;
            }
            string reason;
            if (!input.IsUsable(out reason))
            {
                set.ran = false;
                set.refusalReason = reason;
                return set;
            }

            SuitabilityRasterClass map = input.suitability;
            set.ran = true;
            set.roadClassId = input.roadClass.id;
            set.roadClassName = input.roadClass.displayName;
            set.zoneId = input.zone.zoneId;
            set.metresPerCell = map.metresPerCell;
            set.cellAreaM2 = map.metresPerCell * map.metresPerCell;
            set.waterSurfaceY = input.waterSurfaceY;

            double governingSlope;
            string governingSource;
            GoverningSlopeFraction(input, out governingSlope, out governingSource);

            bool networkAnswered = false;
            for (int i = 0; i < input.candidateRegionsXZ.Count; i++)
            {
                set.verdicts.Add(Judge(input, i, governingSlope, governingSource, ref networkAnswered));
            }
            set.networkMeasured = networkAnswered;
            return set;
        }
        /// <summary> The one slope limit that governs this ground, as a FRACTION, and the name of where it came from.
        ///
        /// The codebase genuinely holds three authored slope limits in three different units -- <c>roadClass.maxGradient</c>
        /// as a fraction, <c>criteria.maxSlopePercent</c> as a percentage, <c>zone.maxSlopeDegrees</c> in degrees -- and no
        /// converter between them existed before this. Each is a real limit with a real author behind it: the streets must
        /// climb it, the pads must sit on it, and the zone was written for it. So the STRICTEST binds, converted once here,
        /// and the report names which one it was, because "too steep" without saying which of three numbers refused the
        /// ground sends an author to edit the wrong asset.
        ///
        /// Degrees convert through the tangent rather than by scaling, because a gradient is rise over run: 15 degrees is
        /// 26.8 %, not 16.7 %. </summary>
        private static void GoverningSlopeFraction(SiteTestInput input, out double fraction, out string source)
        {
            double fromClass = input.roadClass.maxGradient;
            double fromCriteria = input.criteria.maxSlopePercent / 100.0;
            double fromZone = Math.Tan(input.zone.maxSlopeDegrees * Math.PI / 180.0);

            fraction = fromClass;
            source = "road class " + input.roadClass.displayName + " maximum gradient";
            if (fromCriteria > 0.0 && fromCriteria < fraction)
            {
                fraction = fromCriteria;
                source = "the buildability criteria maximum slope of "
                         + input.criteria.maxSlopePercent.ToString("0.#", CultureInfo.InvariantCulture) + " %";
            }
            if (fromZone > 0.0 && fromZone < fraction)
            {
                fraction = fromZone;
                source = "zone " + input.zone.zoneId + " maximum slope of "
                         + input.zone.maxSlopeDegrees.ToString("0.#", CultureInfo.InvariantCulture) + " degrees";
            }
        }
        /// <summary> Judges ONE candidate region by reducing the raster cells whose centres fall inside it. The centre is
        /// what decides membership, because every figure stored in a cell was measured for the ground its centre stands on
        /// -- taking a cell a region merely clips would credit the site with land it does not own. </summary>
        private SiteVerdict Judge(SiteTestInput input, int index, double governingSlope, string governingSource,
            ref bool networkAnswered)
        {
            SuitabilityRasterClass map = input.suitability;
            Rect region = input.candidateRegionsXZ[index];
            SiteVerdict v = new SiteVerdict();
            v.identity = input.IdentityAt(index);
            v.regionXZ = region;
            v.governingSlopeFraction = (float)governingSlope;
            v.governingSlopeSource = governingSource;
            v.freeboardMetres = float.NaN;
            v.metresToWaterEdge = float.NaN;
            v.metresToNearestRoad = float.NaN;
            v.refusal = string.Empty;
            v.refusalFactor = SuitabilityFactor.None;

            int fromX, fromZ, toX, toZ;
            SuitabilityRaster.CellAt(map, region.xMin, region.yMin, out fromX, out fromZ);
            SuitabilityRaster.CellAt(map, region.xMax, region.yMax, out toX, out toZ);

            double platform = SuitabilityMapBuilder.PlatformWidthMetres(input.roadClass);
            double maxGradient = input.roadClass.maxGradient;
            double slopeSum = 0.0, storedSum = 0.0, resistSlopeSum = 0.0, resistEarthSum = 0.0, resistNetworkSum = 0.0;
            double earthworkSum = 0.0, worstSlope = 0.0, worstRelief = 0.0;
            double lowest = double.PositiveInfinity, highest = double.NegativeInfinity;
            double leastFreeboard = double.PositiveInfinity, nearestWaterEdge = double.PositiveInfinity;
            double nearestRoad = double.PositiveInfinity;
            int networkCells = 0;

            for (int cz = fromZ; cz <= toZ; cz++)
            {
                for (int cx = fromX; cx <= toX; cx++)
                {
                    double x = SuitabilityRaster.CellCentreXMetres(map, cx);
                    double z = SuitabilityRaster.CellCentreZMetres(map, cz);
                    if (x < region.xMin || x > region.xMax || z < region.yMin || z > region.yMax) { continue; }
                    int cell = SuitabilityRaster.Index(map, cx, cz);
                    if (cell < 0) { continue; }
                    v.cellsInside++;

                    double slope = map.slopeFraction[cell];
                    if (slope > worstSlope) { worstSlope = slope; }
                    slopeSum += slope;
                    storedSum += map.cost[cell];
                    earthworkSum += map.earthworkVolumeM3[cell];
                    resistSlopeSum += SuitabilityFactors.SlopeResistance(slope, maxGradient);
                    resistEarthSum += SuitabilityFactors.EarthworkResistance(slope, platform, maxGradient);
                    double metres;
                    if (input.network != null && input.network.TryMetresToNearestRoad(x, z, out metres))
                    {
                        networkAnswered = true;
                        networkCells++;
                        resistNetworkSum += SuitabilityFactors.NetworkResistance(metres, input.roadClass.targetSpacing);
                        if (metres < nearestRoad) { nearestRoad = metres; }
                    }

                    bool forbidden = map.forbidden[cell];
                    if (forbidden) { v.cellsForbidden++; }

                    double cellLow, cellHigh;
                    if (!CellGround(input.surface, x, z, map.metresPerCell, out cellLow, out cellHigh))
                    {
                        v.cellsUnreadable++;
                        continue;
                    }
                    v.cellsReadable++;
                    if (cellLow < lowest) { lowest = cellLow; }
                    if (cellHigh > highest) { highest = cellHigh; }
                    double relief = cellHigh - cellLow;
                    if (!forbidden && relief > worstRelief) { worstRelief = relief; }

                    double toWater = MetresToWaterEdge(input, x, z);
                    if (!double.IsNaN(toWater) && toWater < nearestWaterEdge) { nearestWaterEdge = toWater; }

                    bool buildable = !forbidden;
                    if (slope > governingSlope) { v.cellsRefusedBySlope++; buildable = false; }
                    string wet;
                    if (BuildableLandTest.WaterRejects((float)cellLow, input.waterSurfaceY, input.criteria, out wet))
                    {
                        v.cellsRefusedByWater++;
                        buildable = false;
                    }
                    if (!double.IsNaN(toWater) && toWater < input.zone.shorelineSetbackMetres)
                    {
                        v.cellsRefusedByShoreline++;
                        buildable = false;
                    }
                    if (relief > input.criteria.maxReliefMetres) { v.cellsRefusedByRelief++; buildable = false; }
                    if (!buildable) { continue; }

                    v.cellsBuildable++;
                    if (!float.IsNegativeInfinity(input.waterSurfaceY))
                    {
                        double freeboard = cellLow - input.waterSurfaceY;
                        if (freeboard < leastFreeboard) { leastFreeboard = freeboard; }
                    }
                }
            }
            if (v.cellsInside < 1)
            {
                v.accepted = false;
                v.refusal = "no cell of the suitability map has its centre inside this region, so it lies off the map ("
                            + SuitabilityRaster.SizeLine(map) + ") or is smaller than one cell. Move the road end onto"
                            + " the mapped ground, or build the map over it";
                return v;
            }

            double inside = v.cellsInside;
            v.worstSlopeFraction = (float)worstSlope;
            v.meanSlopeFraction = (float)(slopeSum / inside);
            v.meanStoredResistance = (float)(storedSum / inside);
            v.meanSlopeResistance = (float)(resistSlopeSum / inside);
            v.meanEarthworkResistance = (float)(resistEarthSum / inside);
            v.meanWaterResistance = 0f;
            v.meanLandUseResistance = 0f;
            v.landUseMeasured = false;
            v.waterFractionMeasured = false;
            v.meanNetworkResistance = networkCells > 0 ? (float)(resistNetworkSum / networkCells) : 0f;
            v.totalEarthworkM3PerMetre = (float)earthworkSum;
            v.meanEarthworkM3PerMetre = (float)(earthworkSum / inside);
            v.worstCellReliefMetres = (float)worstRelief;
            bool groundRead = !double.IsInfinity(lowest) && !double.IsInfinity(highest);
            v.lowestGroundY = groundRead ? (float)lowest : float.NaN;
            v.highestGroundY = groundRead ? (float)highest : float.NaN;
            v.siteReliefMetres = groundRead ? (float)(highest - lowest) : float.NaN;
            v.freeboardMetres = double.IsInfinity(leastFreeboard) ? float.NaN : (float)leastFreeboard;
            v.metresToWaterEdge = double.IsInfinity(nearestWaterEdge) ? float.NaN : (float)nearestWaterEdge;
            v.metresToNearestRoad = double.IsInfinity(nearestRoad) ? float.NaN : (float)nearestRoad;
            v.buildableFraction = (float)(v.cellsBuildable / inside);
            v.buildableAreaM2 = (float)(v.cellsBuildable * (double)map.metresPerCell * map.metresPerCell);
            DecideSite(input, ref v);
            return v;
        }
        /// <summary> The three site-level questions, asked after the cells are counted: does the zone's water binding
        /// hold, is there enough buildable land in absolute terms, and is enough of the region buildable. A per-cell
        /// refusal never refuses a site on its own -- a real city has steep ground and wet ground in it, and refusing a
        /// site because one cell failed would refuse every city ever built. </summary>
        private void DecideSite(SiteTestInput input, ref SiteVerdict v)
        {
            SuitabilityFactor factor;
            string cause;
            DominantRefusal(ref v, input, out factor, out cause);

            if (input.zone.boundToWaterEdge && float.IsNaN(v.metresToWaterEdge))
            {
                v.accepted = false;
                v.refusalFactor = SuitabilityFactor.Water;
                v.refusal = "zone " + input.zone.zoneId + " is bound to the water edge, and no water stands within "
                            + SearchRadiusMetres(input).ToString("0", CultureInfo.InvariantCulture)
                            + " m of any cell of this region. A port or a fishing village that touches no water is not"
                            + " one; site it on a shore, or clear boundToWaterEdge on the zone";
                return;
            }
            if (v.cellsBuildable < minBuildableCells)
            {
                v.accepted = false;
                v.refusalFactor = factor;
                v.refusal = "only " + v.cellsBuildable.ToString(CultureInfo.InvariantCulture)
                            + " buildable cells here, under the " + minBuildableCells.ToString(CultureInfo.InvariantCulture)
                            + " this operator requires: " + cause;
                return;
            }
            if (v.buildableAreaM2 < input.criteria.minLotArea)
            {
                v.accepted = false;
                v.refusalFactor = factor;
                v.refusal = "the buildable ground totals "
                            + v.buildableAreaM2.ToString("0", CultureInfo.InvariantCulture) + " m^2, which cannot hold one"
                            + " lot of the authored minimum " + input.criteria.minLotArea.ToString("0",
                                CultureInfo.InvariantCulture) + " m^2: " + cause;
                return;
            }
            if (v.buildableFraction < minBuildableFraction)
            {
                v.accepted = false;
                v.refusalFactor = factor;
                v.refusal = "only " + (v.buildableFraction * 100f).ToString("0.#", CultureInfo.InvariantCulture)
                            + " % of this region is buildable, under the "
                            + (minBuildableFraction * 100f).ToString("0.#", CultureInfo.InvariantCulture)
                            + " % a settlement needs for a connected street network: " + cause;
                return;
            }
            v.accepted = true;
            v.refusalFactor = SuitabilityFactor.None;
            v.refusal = string.Empty;
        }
        /// <summary> Which cause took the most land, as a factor of the shipped cost model plus a sentence with the real
        /// measurement in it. The slope sentence comes from <c>SuitabilityFactors.BindingSentence</c> so a site refusal
        /// reads in the same words as a corridor cell refusal -- "slope 14 %, against a 8 % maximum for this class" rather
        /// than "unsuitable".
        ///
        /// Pad relief and unreadable ground have NO factor in that model, and are not forced into one: a road is graded
        /// along its length and a building pad is not, so relief is a building constraint the five-factor route model
        /// never had. Their sentences are written here instead, and the factor stays
        /// <see cref="SuitabilityFactor.None"/>, which is truthful rather than convenient.
        ///
        /// Ties resolve toward the earlier cause in a fixed order, for <c>BindingFactor</c>'s own reason: a cause chosen
        /// by whichever tally the loop happened to compare first would make two identical sites report differently.
        /// </summary>
        private static void DominantRefusal(ref SiteVerdict v, SiteTestInput input, out SuitabilityFactor factor,
            out string cause)
        {
            int slopeCells = v.cellsRefusedBySlope + v.cellsForbidden;
            int waterCells = v.cellsRefusedByWater + v.cellsRefusedByShoreline;
            factor = SuitabilityFactor.None;
            int most = 0;
            int which = 0;
            if (slopeCells > most) { most = slopeCells; which = 1; }
            if (waterCells > most) { most = waterCells; which = 2; }
            if (v.cellsRefusedByRelief > most) { most = v.cellsRefusedByRelief; which = 3; }
            if (v.cellsUnreadable > most) { most = v.cellsUnreadable; which = 4; }

            if (which == 1)
            {
                factor = SuitabilityFactor.Slope;
                cause = most.ToString(CultureInfo.InvariantCulture) + " cells refused for slope -- "
                        + SuitabilityFactors.BindingSentence(SuitabilityFactor.Slope,
                            v.cellsForbidden > v.cellsRefusedBySlope, v.worstSlopeFraction, v.governingSlopeFraction,
                            v.meanEarthworkM3PerMetre, 0.0, 0.0, 0.0)
                        + ", the strictest of the three authored limits being " + v.governingSlopeSource;
                return;
            }
            if (which == 2)
            {
                cause = most.ToString(CultureInfo.InvariantCulture) + " cells refused for water: within the "
                        + input.criteria.waterFreeboard.ToString("0.#", CultureInfo.InvariantCulture)
                        + " m freeboard of the water surface, or inside zone " + input.zone.zoneId + "'s "
                        + input.zone.shorelineSetbackMetres.ToString("0.#", CultureInfo.InvariantCulture)
                        + " m shoreline setback";
                factor = SuitabilityFactor.Water;
                return;
            }
            if (which == 3)
            {
                cause = most.ToString(CultureInfo.InvariantCulture) + " cells whose ground rises more than the "
                        + input.criteria.maxReliefMetres.ToString("0.#", CultureInfo.InvariantCulture)
                        + " m a pad may absorb, the worst being "
                        + v.worstCellReliefMetres.ToString("0.#", CultureInfo.InvariantCulture) + " m across one cell";
                return;
            }
            if (which == 4)
            {
                cause = most.ToString(CultureInfo.InvariantCulture) + " cells whose ground the terrain could not answer"
                        + " for, so they cannot be shown to be buildable";
                return;
            }
            cause = "nothing refused a cell here; the region simply holds less ground than a settlement needs";
        }
        /// <summary> The five points a cell's ground is read at: its centre and its four corners, as fractions of the cell
        /// size. Five is what the pad question needs -- the centre says where the cell sits, the corners say how much the
        /// ground moves across it -- and it is the same max-minus-min definition of relief <c>BuildableLandTest</c> uses on
        /// a lot, so the two never disagree about what relief means. </summary>
        private static readonly float[] SampleOffsetX = { 0f, -0.5f, 0.5f, -0.5f, 0.5f };
        private static readonly float[] SampleOffsetZ = { 0f, -0.5f, -0.5f, 0.5f, 0.5f };

        /// <summary> Eight compass directions, unit length, for the ring search that finds the water edge. </summary>
        private static readonly float[] RingDirX = { 1f, 0.70710678f, 0f, -0.70710678f, -1f, -0.70710678f, 0f,
            0.70710678f };
        private static readonly float[] RingDirZ = { 0f, 0.70710678f, 1f, 0.70710678f, 0f, -0.70710678f, -1f,
            -0.70710678f };

        /// <summary> The lowest and highest ground in one cell, or false when the surface could not answer for any of the
        /// five points. Unknown ground is UNREADABLE, never dry: a non-finite height is the tool's own convention for "no
        /// answer here", and treating it as zero would site a settlement on data that does not exist. </summary>
        private static bool CellGround(ITerrainSurfaceReader surface, double x, double z, float metresPerCell,
            out double low, out double high)
        {
            low = double.PositiveInfinity;
            high = double.NegativeInfinity;
            for (int i = 0; i < SampleOffsetX.Length; i++)
            {
                float sx = (float)(x + SampleOffsetX[i] * metresPerCell);
                float sz = (float)(z + SampleOffsetZ[i] * metresPerCell);
                float y = surface.SampleHeight(sx, sz);
                if (float.IsNaN(y) || float.IsInfinity(y)) { low = double.NaN; high = double.NaN; return false; }
                if (y < low) { low = y; }
                if (y > high) { high = y; }
            }
            return true;
        }

        /// <summary> How far out the water edge is looked for: the zone's shoreline setback, rounded up to whole cells,
        /// plus one cell. Exactly what the decision needs -- a setback only cares whether water is nearer than itself --
        /// and no further, because a search radius is paid for once per cell of every candidate. </summary>
        private static double SearchRadiusMetres(SiteTestInput input)
        {
            double cell = input.suitability.metresPerCell;
            if (cell <= 0.0) { return 0.0; }
            int rings = (int)Math.Ceiling(input.zone.shorelineSetbackMetres / cell) + 1;
            return rings * cell;
        }
        /// <summary> Metres from a point to the water edge, or <see cref="float.NaN"/> when there is no water or none
        /// within the searched band.
        ///
        /// The water edge is not a curve this tool holds -- water is one authored world-Y, the whole tool's convention --
        /// so the edge is where the GROUND crosses that level, and it is found by sampling outward in rings of one cell
        /// through eight compass directions until ground at or below the water surface is met. The answer is therefore
        /// resolved to the cell, which is the resolution the ground itself was measured at; claiming a metre would claim
        /// precision the samples do not have.
        ///
        /// This is what makes the shoreline rule a SETBACK AND A BINDING rather than only a prohibition: the same distance
        /// refuses a cell that is too close and proves a water-bound zone has a shore at all. </summary>
        private static double MetresToWaterEdge(SiteTestInput input, double x, double z)
        {
            float level = input.waterSurfaceY;
            if (float.IsNegativeInfinity(level) || float.IsNaN(level) || float.IsPositiveInfinity(level))
            {
                return double.NaN;
            }
            double cell = input.suitability.metresPerCell;
            double radius = SearchRadiusMetres(input);
            if (cell <= 0.0 || radius <= 0.0) { return double.NaN; }

            float here = input.surface.SampleHeight((float)x, (float)z);
            if (!float.IsNaN(here) && !float.IsInfinity(here) && here <= level) { return 0.0; }

            for (double reach = cell; reach <= radius + 0.0001; reach += cell)
            {
                for (int d = 0; d < RingDirX.Length; d++)
                {
                    float sx = (float)(x + RingDirX[d] * reach);
                    float sz = (float)(z + RingDirZ[d] * reach);
                    float y = input.surface.SampleHeight(sx, sz);
                    if (float.IsNaN(y) || float.IsInfinity(y)) { continue; }
                    if (y <= level) { return reach; }
                }
            }
            return double.NaN;
        }
    }
}
