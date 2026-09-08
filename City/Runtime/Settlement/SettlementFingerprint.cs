// New file, authored for this project — proprietary
// Copyright (c) 2026 <rights holder>. All rights reserved.
// Licensed under the Road & City Master End User Licence Agreement; see LICENSE.md.
// Not open source. Redistribution of this file, in source or compiled form, is prohibited.
#region "Imports"
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-19. How different one settlement is from another, as a number.
    ///
    /// The author's requirement is <i>"the cities never to be same"</i>, and a requirement like that is worthless until
    /// something measures it. This is the measurement, and it is only that: NOTHING HERE GENERATES ANYTHING. It draws a
    /// target from an order, it measures a result, and it returns the distance between the two. What aims the existing
    /// parameters at a target is <c>SiteRankOperator</c>'s job, not this file's -- a measurement that also nudged the
    /// generator would be scoring its own work.
    ///
    /// THREE MEASURES, from two published sources:
    ///
    ///   * The block (area, shape-factor) joint distribution -- Louf and Barthelemy, <i>"A typology of street patterns"</i>,
    ///     J. R. Soc. Interface 11 (2014). Their shape factor is Phi = 4A / (pi D^2), where D is the diameter of the
    ///     block's circumscribing circle, so Phi is 1 for a disc and falls as a block gets longer or more bent. The
    ///     paper sorts cities by clustering these distributions; this file reduces a distribution to the four
    ///     <see cref="StreetPatternFamily"/> shares the orders already carry weights for, by nearest prototype. The
    ///     PROTOTYPES ARE THIS PROJECT'S, stated as constants below with which kind of number each one is; the shape
    ///     factor itself is the paper's.
    ///   * Orientation entropy -- Boeing, <i>"Urban spatial order: street network orientation, configuration, and
    ///     entropy"</i>, Applied Network Science 4 (2019). Street bearings into 36 bins, Shannon entropy of that
    ///     histogram, then the paper's orientation order phi = 1 - ((H - Hg) / (Hmax - Hg))^2, which is 1 for a perfect
    ///     grid and 0 for a perfectly disordered network.
    ///   * Circuity -- the route factor, or detour index: network distance over straight-line distance, aggregated over
    ///     sampled node pairs. Barthelemy, <i>"Spatial networks"</i>, Physics Reports 499 (2011), section 4.
    ///     NOT the edge-geometry circuity of the same Boeing paper, and the reason is in the code rather than left
    ///     implicit: a <see cref="NetworkEdge"/> is a STRAIGHT segment between two nodes, so a polyline-length over
    ///     chord-length ratio would be exactly 1.0 for every edge this tool produces. Shipping that as a measure of
    ///     anything would be shipping a number that cannot vary.
    ///
    /// Every figure this class reports carries its own "was it measured" flag rather than a sentinel an author has to
    /// know about, because a fingerprint drawn as a target legitimately carries fewer of the three than a measured one.
    /// </summary>
    public sealed class SettlementFingerprint
    {
        #region "The record"
        /// <summary> True for a fingerprint from <see cref="DrawTarget"/> -- what a settlement of this order and seed
        /// SHOULD measure like -- and false for one from <see cref="Measure"/>. Kept because the two are the same shape
        /// and a report that mixed them up would read as a measurement of something nobody built. </summary>
        public bool isTarget;
        /// <summary> The share of the settlement's blocks nearest each of the four families, indexed by
        /// <see cref="StreetPatternFamily"/> and summing to 1 when anything was measured at all. Private with an
        /// accessor rather than public: four shares that a caller could set one at a time would be free to stop summing
        /// to one, and every consumer here divides by that sum. </summary>
        private readonly float[] familyShare = new float[4];

        /// <summary> Boeing's raw entropy H of the bearing histogram, in nats. Reported beside
        /// <see cref="orientationOrder"/> and not replaced by it, because the order figure is a squared ratio and
        /// squashes the difference between a nearly-gridded town and a strongly gridded one. </summary>
        public float orientationEntropyNats;

        /// <summary> Boeing's orientation order phi: 1 for a perfect grid, 0 for a perfectly disordered network. </summary>
        public float orientationOrder;

        /// <summary> The route factor: total network distance over total straight-line distance across the sampled node
        /// pairs, so always at least 1. Zero means this fingerprint does not carry the figure -- a drawn target never
        /// does, for the reason <see cref="DrawTarget"/> gives. </summary>
        public float circuity;

        /// <summary> The median block area in square metres, and the spread of block areas as the standard deviation of
        /// their base-ten logarithms -- one "dex" being one factor of ten.
        ///
        /// The spread is reported rather than folded into the shares because of a real limitation of nearest-prototype
        /// assignment that is better stated than hidden: a settlement with many small blocks AND many large ones lands
        /// its blocks on the small and large prototypes, not on the middle one, so it reads as two families rather than
        /// as <see cref="StreetPatternFamily.MixedBlockSizes"/>. The spread is the figure that tells those two cases
        /// apart, and a caller comparing two settlements should read it. </summary>
        public float medianBlockAreaM2;
        public float logAreaSpreadDex;

        /// <summary> What the measurement actually had to work with. A distance computed from three blocks is a number
        /// with no meaning, and the only way a caller can know that is to be told the counts. </summary>
        public int blocksMeasured;
        public int edgesMeasured;
        public int nodePairsSampled;
        public int nodePairsUnreachable;

        /// <summary> Whether each of the three measures is carried at all. <see cref="Compare"/> uses exactly these:
        /// a term whose figure is missing from either side is left out of the distance instead of being compared
        /// against a zero that would read as a large difference. </summary>
        public bool HasFamilyShares
        {
            get { return familyShare[0] + familyShare[1] + familyShare[2] + familyShare[3] > 0f; }
        }

        public bool HasOrientation { get { return isTarget || edgesMeasured > 0; } }

        public bool HasCircuity { get { return circuity >= 1f; } }
        /// <summary> The share of blocks nearest one family. Out-of-range families answer zero rather than throwing:
        /// this is a reporting type, and a report is not the place to discover that an enum grew a fifth value. </summary>
        public float Share(StreetPatternFamily family)
        {
            int i = (int)family;
            return i >= 0 && i < familyShare.Length ? familyShare[i] : 0f;
        }

        /// <summary> Sets one share. Only the two factories below call it. </summary>
        private void SetShare(StreetPatternFamily family, float value)
        {
            int i = (int)family;
            if (i >= 0 && i < familyShare.Length) { familyShare[i] = value; }
        }

        /// <summary> The family with the largest share, and its share. The single word a report leads with. </summary>
        public StreetPatternFamily DominantFamily
        {
            get
            {
                int best = 0;
                for (int i = 1; i < familyShare.Length; i++)
                {
                    if (familyShare[i] > familyShare[best]) { best = i; }
                }
                return (StreetPatternFamily)best;
            }
        }

        /// <summary> One sentence, in figures with units, for a bake report or a gate report. Says what kind of
        /// fingerprint it is first, because "expected" and "measured" are the two readings and confusing them is the
        /// only way this sentence can mislead. </summary>
        public string Describe()
        {
            StringBuilder b = new StringBuilder();
            b.Append(isTarget ? "target: " : "measured: ");
            if (!HasFamilyShares)
            {
                b.Append("no blocks to measure");
            }
            else
            {
                b.Append("mostly ").Append(NameOf(DominantFamily)).Append(" (")
                    .Append((Share(DominantFamily) * 100f).ToString("0", CultureInfo.InvariantCulture))
                    .Append("% of ").Append(blocksMeasured.ToString(CultureInfo.InvariantCulture))
                    .Append(" blocks), median block ")
                    .Append(medianBlockAreaM2.ToString("0", CultureInfo.InvariantCulture))
                    .Append(" m2, areas spread over ")
                    .Append(logAreaSpreadDex.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" powers of ten");
            }
            if (HasOrientation)
            {
                b.Append("; street directions ")
                    .Append(orientationOrder.ToString("0.00", CultureInfo.InvariantCulture))
                    .Append(" of the way to a perfect grid");
                if (!isTarget)
                {
                    b.Append(" (entropy ").Append(orientationEntropyNats.ToString("0.00", CultureInfo.InvariantCulture))
                        .Append(" nats over ").Append(edgesMeasured.ToString(CultureInfo.InvariantCulture))
                        .Append(" streets)");
                }
            }
            if (HasCircuity)
            {
                b.Append("; a trip across it runs ")
                    .Append(((circuity - 1f) * 100f).ToString("0.0", CultureInfo.InvariantCulture))
                    .Append("% longer than the straight line, over ")
                    .Append(nodePairsSampled.ToString(CultureInfo.InvariantCulture)).Append(" pairs");
                if (nodePairsUnreachable > 0)
                {
                    b.Append(" (").Append(nodePairsUnreachable.ToString(CultureInfo.InvariantCulture))
                        .Append(" pairs had no route at all, so the network is in pieces)");
                }
            }
            return b.ToString();
        }

        /// <summary> The four family names as an author reads them. The enum's <c>InspectorName</c> attributes are the
        /// same words; reading them by reflection here would make a report depend on an editor-only attribute. </summary>
        private static string NameOf(StreetPatternFamily family)
        {
            if (family == StreetPatternFamily.RegularSmallBlocks) { return "regular small blocks"; }
            if (family == StreetPatternFamily.IrregularSmallBlocks) { return "irregular small blocks"; }
            if (family == StreetPatternFamily.MixedBlockSizes) { return "mixed block sizes"; }
            return "large irregular blocks";
        }
        #endregion


        #region "Constants -- and which kind of number each one is"
        /// <summary> DERIVED, computed here rather than quoted, so the comment cannot fall out of step with the value.
        /// The shape factor Phi = 4A / (pi D^2) of a square is 2/pi ~ 0.637: a square of side s has area s^2 and a
        /// circumscribing circle of diameter s*sqrt(2). The shape factor of a 1:4 strip is 16/(17 pi) ~ 0.300, by the
        /// same arithmetic with area 4s^2 and diameter s*sqrt(17). Those two shapes are the ends of the range a street
        /// block actually occupies, so they are what the split between "compact" and "drawn out" is taken from rather
        /// than a round number chosen to look reasonable. </summary>
        public static readonly double PhiOfSquare = 2.0 / Math.PI;
        public static readonly double PhiOfQuarterStrip = 16.0 / (17.0 * Math.PI);
        public static readonly double PhiSplit = (PhiOfSquare + PhiOfQuarterStrip) * 0.5;
        public static readonly double PhiSpan = PhiOfSquare - PhiOfQuarterStrip;
        /// <summary> AUTHORED. One hectare -- a 100 m square -- as the area at which a block stops being one a person
        /// walks around and starts being one they walk through. It is the middle of the (area, shape-factor) plane for
        /// this tool's purposes, and the four prototypes are placed relative to it. </summary>
        public const double AreaSplitM2 = 10000.0;

        /// <summary> AUTHORED. How far apart, in powers of ten of area, two blocks have to be before the difference
        /// weighs as much as the whole span between a square and a 1:4 strip. Block areas inside one settlement rarely
        /// run over more than about this much, so it is the scale that makes the two axes of the plane comparable
        /// instead of one of them dominating the nearest-prototype test. </summary>
        public const double LogAreaScaleDex = 1.5;

        /// <summary> AUTHORED. Where each family sits, as an offset in powers of ten from
        /// <see cref="AreaSplitM2"/>. Small families half a power of ten below a hectare (~3,000 m2, a terrace block);
        /// mixed at the hectare itself; large a whole power of ten above (~100,000 m2, an industrial parcel or the ground
        /// between village lanes). These four points are THIS PROJECT'S reading of the four family descriptions on
        /// <see cref="StreetPatternFamily"/>; the paper numbers its clusters and does not name coordinates. </summary>
        private static readonly double[] PrototypeAreaOffsetDex = { -0.5, -0.5, 0.0, 1.0 };

        /// <summary> DERIVED from the two reference shapes above, in the family order of the enum: the grid family sits
        /// at a square's shape factor, both irregular families at a 1:4 strip's, and the mixed family between them. </summary>
        private static readonly double[] PrototypePhi =
        {
            PhiOfSquare, PhiOfQuarterStrip, PhiSplit, PhiOfQuarterStrip
        };

        /// <summary> AUTHORED, inside a range the source reports. Boeing's orientation order runs from about 0.05 for
        /// organically grown networks to about 0.9 for the strongest American grids, and these four are where the
        /// families fall in it: a planned grid high, an organic core near the bottom, the other two between. What is
        /// authored is the MAPPING from family to orientation order; the range it sits in is the paper's. </summary>
        private static readonly double[] PrototypeOrientationOrder = { 0.65, 0.08, 0.25, 0.15 };

        /// <summary> AUTHORED. How far <see cref="DrawTarget"/> may move one family weight, as a fraction either way. A
        /// third is enough that two seeds of the same order draw targets an author can tell apart, and small enough that
        /// a village still draws a village's fingerprint. The jitter is MULTIPLICATIVE, which matters: a weight an order
        /// sets to zero stays zero, so a family an order forbids is never drawn for it. </summary>
        public const double TargetJitterFraction = 0.35;

        /// <summary> DERIVED. The route factor of a perfect square grid, 4/pi ~ 1.273: over pairs at a uniformly
        /// distributed bearing the mean of (|dx| + |dz|) / sqrt(dx^2 + dz^2) is (2/pi) * 2. It is the scale
        /// <see cref="Compare"/> divides a circuity difference by, so the three terms of the distance are comparable
        /// rather than one of them contributing hundredths while another contributes tenths. </summary>
        public static readonly double GridRouteFactor = 4.0 / Math.PI;
        /// <summary> CITED. Boeing's bin count, and the two entropies his orientation order is measured between: a
        /// perfect grid puts every bearing into 4 of the 36 bins, giving ln 4, and a perfectly disordered network spreads
        /// them evenly over all 36, giving ln 36. </summary>
        public const int OrientationBins = 36;
        public static readonly double GridEntropyNats = Math.Log(4.0);
        public static readonly double MaximumEntropyNats = Math.Log(OrientationBins);

        /// <summary> AUTHORED. How many nodes the route factor is measured FROM. Every node is measured TO, so twelve
        /// sources over a network of a few thousand nodes is tens of thousands of pairs -- far more than the figure needs
        /// -- while keeping the cost twelve shortest-path walks rather than one per node. The sources are taken at an even
        /// stride through the node list rather than drawn at random, so the figure does not depend on a seed and two
        /// measurements of the same network agree exactly. </summary>
        public const int RouteFactorSourceLimit = 12;

        /// <summary> AUTHORED. Pairs closer together than this are left out of the route factor. Within one block the
        /// ratio is dominated by which corner you started at rather than by the shape of the network, and a settlement's
        /// figure would then be decided by its densest few streets. </summary>
        public const double RouteFactorMinimumSeparationMetres = 100.0;

        /// <summary> AUTHORED. Above this many vertices a block's ring is sampled down at an even stride before the
        /// circumscribing circle is found, because the exact circle is found by testing pairs and triples of vertices and
        /// that is cubic in their count. Blocks come out of <c>PlanarFaceFinder</c> with a handful of corners, so this is
        /// a guard against a hand-edited or heavily inset ring rather than a limit anything normal meets. When it bites,
        /// the diameter it finds is of a ring inside the real one, so the shape factor is slightly HIGH -- which is
        /// stated here because a silently optimistic figure is worse than a documented one. </summary>
        public const int MaximumRingVerticesForExactCircle = 24;

        /// <summary> AUTHORED. What each measure is worth in <c>Compare</c>. The block mix carries the most because it is
        /// the thing a person sees from above and the thing the parameters aim at most directly; orientation next, being
        /// one number over a whole settlement; the route factor least, because it is the only one read from a sample
        /// rather than from every block or every edge. They are ordered rather than tuned -- <c>Compare</c> divides by
        /// the weight it actually used, so their absolute size never matters, only their ratio. </summary>
        public const double FamilyShareWeight = 0.5;
        public const double OrientationWeight = 0.3;
        public const double RouteFactorWeight = 0.2;
        #endregion


        #region "DrawTarget -- what an order and a seed SHOULD look like"
        /// <summary> The fingerprint a settlement of this order, built with this seed, is aiming at.
        ///
        /// The order supplies the family weights; the seed moves them. That is the whole of the uniqueness mechanism, and
        /// it is deliberately this small: a settlement is unique because its TARGET is unique, so the uniqueness survives
        /// every later stage instead of being sprinkled on at the end by jittering positions.
        ///
        /// The orientation order is DERIVED from the drawn shares rather than drawn separately, so the two figures agree
        /// with each other -- a target with the grid family's mass on it cannot also ask for disordered streets. The route
        /// factor is not drawn at all, and the reason is a shortage of evidence rather than a shortage of code: this file
        /// has no published per-family detour index to draw from, and inventing four numbers and citing a paper for them
        /// would be a citation the paper does not carry. It stays a measured-only figure, which is enough for the case it
        /// exists for -- comparing two settlements that were both built. </summary>
        public static SettlementFingerprint DrawTarget(SettlementOrderClass order, ulong seed)
        {
            SettlementFingerprint f = new SettlementFingerprint();
            f.isTarget = true;
            if (order == null) { return f; }
            DeterministicRandom rng = DeterministicRandom.ForElement(seed,
                "settlementFingerprint/" + order.orderIndex.ToString(CultureInfo.InvariantCulture));

            double[] drawn = new double[4];
            double sum = 0.0;
            for (int i = 0; i < 4; i++)
            {
                double weight = order.NormalisedFamilyWeight((StreetPatternFamily)i);
                double jitter = rng.NextRange(1.0 - TargetJitterFraction, 1.0 + TargetJitterFraction);
                drawn[i] = weight * jitter;
                sum += drawn[i];
            }

            if (sum <= 0.0)
            {
                // An order whose four weights are all zero. Drawing the middle family is the one answer that is not a
                // claim: it says "no family in particular" rather than dividing by zero or picking the first.
                f.SetShare(StreetPatternFamily.MixedBlockSizes, 1f);
            }
            else
            {
                for (int i = 0; i < 4; i++) { f.SetShare((StreetPatternFamily)i, (float)(drawn[i] / sum)); }
            }

            f.orientationOrder = (float)OrientationOrderFor(f);
            f.orientationEntropyNats = (float)EntropyForOrder(f.orientationOrder);
            return f;
        }

        /// <summary> The orientation order a set of shares implies: the share-weighted mean of the four family
        /// prototypes. </summary>
        private static double OrientationOrderFor(SettlementFingerprint f)
        {
            double total = 0.0;
            double weight = 0.0;
            for (int i = 0; i < 4; i++)
            {
                double share = f.Share((StreetPatternFamily)i);
                total += share * PrototypeOrientationOrder[i];
                weight += share;
            }
            return weight > 0.0 ? total / weight : 0.0;
        }

        /// <summary> Boeing's formula run backwards, so a drawn target carries an entropy as well as an order and the
        /// two agree: phi = 1 - ((H - Hg)/(Hmax - Hg))^2 gives H = Hg + (Hmax - Hg) * sqrt(1 - phi). </summary>
        private static double EntropyForOrder(double orientationOrder)
        {
            double clamped = orientationOrder < 0.0 ? 0.0 : (orientationOrder > 1.0 ? 1.0 : orientationOrder);
            return GridEntropyNats + (MaximumEntropyNats - GridEntropyNats) * Math.Sqrt(1.0 - clamped);
        }
        #endregion


        #region "Measure -- what a settlement that was built actually is"
        /// <summary> The fingerprint of a baked city. The convenience over the three-list form, because a caller with a
        /// <see cref="CityData"/> in hand should not have to know which three of its lists are the network. </summary>
        public static SettlementFingerprint Measure(CityData city)
        {
            if (city == null) { return new SettlementFingerprint(); }
            return Measure(city.blocks, city.nodes, city.edges);
        }

        /// <summary> The fingerprint of a set of blocks and the network that made them.
        ///
        /// Pure: it reads positions and returns figures. No clock, no scene, no Unity object -- so a gate can measure two
        /// synthetic networks with no terrain and no city in the scene, which is what case (n) needs.
        ///
        /// The three measures are independent of each other and each is skipped rather than faked when its input is
        /// missing: blocks with no polygon leave the shares empty, a network with no edges leaves the orientation figures
        /// out, and a network in pieces still yields a route factor from the pairs that do connect, with the count of the
        /// ones that did not beside it. </summary>
        public static SettlementFingerprint Measure(IList<Block> blocks, IList<NetworkNode> nodes,
            IList<NetworkEdge> edges)
        {
            SettlementFingerprint f = new SettlementFingerprint();
            MeasureBlocks(f, blocks);
            MeasureOrientation(f, nodes, edges);
            MeasureRouteFactor(f, nodes, edges);
            return f;
        }

        /// <summary> The Louf and Barthelemy half: every block's area and shape factor, then each block to its nearest
        /// family prototype in the (log area, shape factor) plane.
        ///
        /// Nearest prototype -- not a quadrant test -- because the four families are not four quadrants: there is no
        /// family for a large compact block, and a quadrant scheme would have to put one somewhere arbitrary. A distance
        /// test puts it wherever it genuinely is nearest, which for a large planned parcel is the mixed prototype. The two
        /// axes are made comparable by dividing area differences by <see cref="LogAreaScaleDex"/> and shape-factor
        /// differences by <see cref="PhiSpan"/>, so neither axis decides the answer on its own. </summary>
        private static void MeasureBlocks(SettlementFingerprint f, IList<Block> blocks)
        {
            if (blocks == null || blocks.Count == 0) { return; }

            int[] counts = new int[4];
            List<double> logAreas = new List<double>();
            List<float> areas = new List<float>();
            for (int i = 0; i < blocks.Count; i++)
            {
                Block block = blocks[i];
                if (block == null || block.polygon == null || block.polygon.Count < 3) { continue; }

                double area = Geometry2D.Area(block.polygon);
                if (area <= 0.0) { continue; }

                double diameter = CircumscribingDiameter(block.polygon);
                if (diameter <= 0.0) { continue; }

                double phi = 4.0 * area / (Math.PI * diameter * diameter);
                if (phi > 1.0) { phi = 1.0; }

                counts[(int)NearestFamily(area, phi)]++;
                logAreas.Add(Math.Log10(area));
                areas.Add((float)area);
            }

            int measured = counts[0] + counts[1] + counts[2] + counts[3];
            f.blocksMeasured = measured;
            if (measured == 0) { return; }

            for (int i = 0; i < 4; i++) { f.SetShare((StreetPatternFamily)i, (float)counts[i] / measured); }

            areas.Sort();
            f.medianBlockAreaM2 = areas[areas.Count / 2];

            double mean = 0.0;
            for (int i = 0; i < logAreas.Count; i++) { mean += logAreas[i]; }
            mean /= logAreas.Count;
            double variance = 0.0;
            for (int i = 0; i < logAreas.Count; i++)
            {
                double d = logAreas[i] - mean;
                variance += d * d;
            }
            f.logAreaSpreadDex = (float)Math.Sqrt(variance / logAreas.Count);
        }

        /// <summary> Which family a single block belongs to. </summary>
        private static StreetPatternFamily NearestFamily(double areaM2, double phi)
        {
            double dex = Math.Log10(areaM2) - Math.Log10(AreaSplitM2);
            int best = 0;
            double bestDistance = double.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                double dArea = (dex - PrototypeAreaOffsetDex[i]) / LogAreaScaleDex;
                double dPhi = (phi - PrototypePhi[i]) / PhiSpan;
                double distance = dArea * dArea + dPhi * dPhi;
                if (distance >= bestDistance) { continue; }
                bestDistance = distance;
                best = i;
            }
            return (StreetPatternFamily)best;
        }

        /// <summary> The diameter of the smallest circle that contains the whole block: the D in the paper's shape
        /// factor.
        ///
        /// Found exactly rather than approximated, because the smallest enclosing circle of a point set is fixed by either
        /// two of its points or three, so testing every pair and every triple and keeping the smallest circle that
        /// contains everything IS the answer -- no iteration, no tolerance to tune, and the same answer every run, which
        /// the purity contract needs. Ritter's growing sphere would have been one pass instead of three nested loops and
        /// would have reported a circle up to a tenth too large, which lands straight in the shape factor.
        ///
        /// Every coordinate is shifted so the first vertex is the origin before any squaring happens. A city ten
        /// kilometres from the world origin has coordinates around 1e4, and a circumcircle is computed from squares of
        /// coordinate differences; squaring the raw values would throw away the digits that matter for a 30 m block.
        /// </summary>
        private static double CircumscribingDiameter(IList<Vector3> ring)
        {
            double[] x;
            double[] z;
            int n = SampleRing(ring, out x, out z);
            if (n < 2) { return 0.0; }
            if (n == 2) { return Distance(x[0], z[0], x[1], z[1]); }

            double bestRadius = double.MaxValue;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double cx = (x[i] + x[j]) * 0.5;
                    double cz = (z[i] + z[j]) * 0.5;
                    double r = Distance(x[i], z[i], x[j], z[j]) * 0.5;
                    if (r >= bestRadius || !Encloses(x, z, n, cx, cz, r)) { continue; }
                    bestRadius = r;
                }
            }

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    for (int k = j + 1; k < n; k++)
                    {
                        double cx, cz, r;
                        if (!TryCircumcircle(x[i], z[i], x[j], z[j], x[k], z[k], out cx, out cz, out r)) { continue; }
                        if (r >= bestRadius || !Encloses(x, z, n, cx, cz, r)) { continue; }
                        bestRadius = r;
                    }
                }
            }

            if (bestRadius < double.MaxValue) { return bestRadius * 2.0; }

            // Unreachable for a real ring: some pair or triple always encloses the set. Kept because "unreachable" is a
            // claim, and the honest fallback is the widest pair, which is the smallest circle any set could need.
            double widest = 0.0;
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double d = Distance(x[i], z[i], x[j], z[j]);
                    if (d > widest) { widest = d; }
                }
            }
            return widest;
        }

        /// <summary> The ring as two coordinate arrays, shifted to start at the origin and sampled down at an even stride
        /// if it has more vertices than the exact circle search will take. Returns how many were kept. </summary>
        private static int SampleRing(IList<Vector3> ring, out double[] x, out double[] z)
        {
            x = new double[0];
            z = new double[0];
            if (ring == null || ring.Count == 0) { return 0; }

            int count = ring.Count;
            int step = 1;
            if (count > MaximumRingVerticesForExactCircle)
            {
                step = (count + MaximumRingVerticesForExactCircle - 1) / MaximumRingVerticesForExactCircle;
            }

            int kept = (count + step - 1) / step;
            x = new double[kept];
            z = new double[kept];
            double originX = ring[0].x;
            double originZ = ring[0].z;
            int w = 0;
            for (int i = 0; i < count && w < kept; i += step)
            {
                x[w] = ring[i].x - originX;
                z[w] = ring[i].z - originZ;
                w++;
            }
            return w;
        }

        private static double Distance(double ax, double az, double bx, double bz)
        {
            double dx = bx - ax;
            double dz = bz - az;
            return Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary> Whether every kept vertex lies inside the circle. The tolerance is relative to the radius, so it
        /// means the same thing for a 5 m courtyard and a 500 m industrial parcel. </summary>
        private static bool Encloses(double[] x, double[] z, int n, double cx, double cz, double r)
        {
            double slack = r * 1e-6 + 1e-6;
            double limit = (r + slack) * (r + slack);
            for (int i = 0; i < n; i++)
            {
                double dx = x[i] - cx;
                double dz = z[i] - cz;
                if (dx * dx + dz * dz > limit) { return false; }
            }
            return true;
        }

        /// <summary> The circle through three points, or false when they are collinear and there is none. </summary>
        private static bool TryCircumcircle(double ax, double az, double bx, double bz, double cx, double cz,
            out double centreX, out double centreZ, out double radius)
        {
            centreX = 0.0;
            centreZ = 0.0;
            radius = 0.0;

            double d = 2.0 * (ax * (bz - cz) + bx * (cz - az) + cx * (az - bz));
            if (Math.Abs(d) < 1e-9) { return false; }

            double a2 = ax * ax + az * az;
            double b2 = bx * bx + bz * bz;
            double c2 = cx * cx + cz * cz;
            centreX = (a2 * (bz - cz) + b2 * (cz - az) + c2 * (az - bz)) / d;
            centreZ = (a2 * (cx - bx) + b2 * (ax - cx) + c2 * (bx - ax)) / d;
            radius = Distance(centreX, centreZ, ax, az);
            return true;
        }

        /// <summary> The Boeing half: the entropy of the street bearings, and the orientation order that follows from it.
        ///
        /// Each street is counted in BOTH directions, as the paper does, because a street has no direction and counting it
        /// once would make a network's entropy depend on which endpoint the generator happened to write first. The 36 bins
        /// are offset by half a bin so that due north sits at the CENTRE of a bin rather than on the boundary between two:
        /// without the offset a perfect north-south grid splits its mass across two bins and measures as less ordered than
        /// it is, which is the one case the figure exists to recognise.
        ///
        /// Counts, not lengths. Weighting each bearing by its street's length is a defensible variation and is what some
        /// implementations offer, but the published formula this file cites is over counts, and a figure computed one way
        /// and cited from another is not the cited figure.
        ///
        /// The clamp at the end is not decoration. A network whose streets all run one way puts every bearing into two
        /// bins, giving an entropy BELOW the grid's four-bin entropy, which makes the squared ratio negative and the order
        /// figure greater than one. That is a real network -- a single ribbon village along one road -- so it is clamped to
        /// 1 and read as "as ordered as a grid", rather than allowed to report an impossible number. </summary>
        private static void MeasureOrientation(SettlementFingerprint f, IList<NetworkNode> nodes,
            IList<NetworkEdge> edges)
        {
            if (nodes == null || edges == null || edges.Count == 0) { return; }

            double[] bins = new double[OrientationBins];
            double total = 0.0;
            int counted = 0;
            double binWidth = 360.0 / OrientationBins;

            for (int i = 0; i < edges.Count; i++)
            {
                NetworkEdge edge = edges[i];
                if (edge.a < 0 || edge.a >= nodes.Count || edge.b < 0 || edge.b >= nodes.Count) { continue; }

                Vector3 a = nodes[edge.a].position;
                Vector3 b = nodes[edge.b].position;
                double dx = b.x - a.x;
                double dz = b.z - a.z;
                if (dx * dx + dz * dz < 1e-6) { continue; }

                double bearing = Math.Atan2(dx, dz) * 180.0 / Math.PI;
                AddBearing(bins, bearing, binWidth);
                AddBearing(bins, bearing + 180.0, binWidth);
                total += 2.0;
                counted++;
            }

            f.edgesMeasured = counted;
            if (counted == 0 || total <= 0.0) { return; }

            double entropy = 0.0;
            for (int i = 0; i < bins.Length; i++)
            {
                if (bins[i] <= 0.0) { continue; }
                double p = bins[i] / total;
                entropy -= p * Math.Log(p);
            }

            f.orientationEntropyNats = (float)entropy;

            double span = MaximumEntropyNats - GridEntropyNats;
            double ratio = span > 0.0 ? (entropy - GridEntropyNats) / span : 0.0;
            double order = 1.0 - ratio * ratio;
            f.orientationOrder = (float)(order < 0.0 ? 0.0 : (order > 1.0 ? 1.0 : order));
        }

        /// <summary> One bearing into its bin, with the half-bin offset. </summary>
        private static void AddBearing(double[] bins, double bearingDegrees, double binWidth)
        {
            double shifted = bearingDegrees + binWidth * 0.5;
            shifted = shifted % 360.0;
            if (shifted < 0.0) { shifted += 360.0; }
            int bin = (int)(shifted / binWidth);
            if (bin < 0) { bin = 0; }
            if (bin >= bins.Length) { bin = bins.Length - 1; }
            bins[bin] += 1.0;
        }
        #endregion


        #region "Route factor -- how much further than the straight line"
        /// <summary> Total network distance over total straight-line distance, across the pairs reachable from a small,
        /// evenly spread set of source nodes.
        ///
        /// Aggregated as one ratio of two sums rather than as the mean of the per-pair ratios, which is the standard way
        /// and is the right way here: a mean of ratios gives a 120 m hop between two adjacent junctions the same say as a
        /// two-kilometre crossing, and there are far more of the former. Pairs under
        /// <see cref="RouteFactorMinimumSeparationMetres"/> apart are dropped for the same reason.
        ///
        /// Shortest paths by Dijkstra over a binary heap, once per source. Straight segments mean every edge weight is
        /// just the distance between its endpoints, and there are no negative weights, so there is nothing here that
        /// wants a heavier algorithm.
        ///
        /// A network in pieces is reported, not hidden: pairs with no route at all are counted into
        /// <see cref="nodePairsUnreachable"/> and left out of both sums, so the ratio stays a ratio of real routes and the
        /// count beside it says how much of the network was unreachable. </summary>
        private static void MeasureRouteFactor(SettlementFingerprint f, IList<NetworkNode> nodes,
            IList<NetworkEdge> edges)
        {
            if (nodes == null || nodes.Count < 2 || edges == null || edges.Count == 0) { return; }

            int[] adjacencyStart;
            int[] adjacencyNode;
            double[] adjacencyLength;
            BuildAdjacency(nodes, edges, out adjacencyStart, out adjacencyNode, out adjacencyLength);

            int sources = nodes.Count < RouteFactorSourceLimit ? nodes.Count : RouteFactorSourceLimit;
            int stride = nodes.Count / sources;
            if (stride < 1) { stride = 1; }

            double networkTotal = 0.0;
            double straightTotal = 0.0;
            int pairs = 0;
            int unreachable = 0;
            double[] distance = new double[nodes.Count];

            for (int s = 0; s < sources; s++)
            {
                int source = s * stride;
                if (source >= nodes.Count) { break; }

                Dijkstra(source, nodes.Count, adjacencyStart, adjacencyNode, adjacencyLength, distance);
                Vector3 from = nodes[source].position;

                for (int t = source + 1; t < nodes.Count; t++)
                {
                    Vector3 to = nodes[t].position;
                    double straight = Distance(from.x, from.z, to.x, to.z);
                    if (straight < RouteFactorMinimumSeparationMetres) { continue; }

                    if (distance[t] >= double.MaxValue) { unreachable++; continue; }

                    networkTotal += distance[t];
                    straightTotal += straight;
                    pairs++;
                }
            }

            f.nodePairsSampled = pairs;
            f.nodePairsUnreachable = unreachable;
            if (pairs == 0 || straightTotal <= 0.0) { return; }

            double factor = networkTotal / straightTotal;

            // A route can never be shorter than the straight line, so a figure below one is arithmetic noise from a
            // network whose sampled pairs are all nearly collinear, not a discovery. Reported as exactly one.
            f.circuity = (float)(factor < 1.0 ? 1.0 : factor);
        }

        /// <summary> The network as a compressed adjacency list: for node i, the entries from <c>start[i]</c> to
        /// <c>start[i + 1]</c>. Built once per measurement, because the alternative -- a list of lists -- allocates one
        /// object per node and this runs over every node of a capital. </summary>
        private static void BuildAdjacency(IList<NetworkNode> nodes, IList<NetworkEdge> edges,
            out int[] start, out int[] target, out double[] length)
        {
            int n = nodes.Count;
            int[] degree = new int[n];
            for (int i = 0; i < edges.Count; i++)
            {
                NetworkEdge e = edges[i];
                if (e.a < 0 || e.a >= n || e.b < 0 || e.b >= n || e.a == e.b) { continue; }
                degree[e.a]++;
                degree[e.b]++;
            }

            start = new int[n + 1];
            for (int i = 0; i < n; i++) { start[i + 1] = start[i] + degree[i]; }

            int[] cursor = new int[n];
            for (int i = 0; i < n; i++) { cursor[i] = start[i]; }

            target = new int[start[n]];
            length = new double[start[n]];
            for (int i = 0; i < edges.Count; i++)
            {
                NetworkEdge e = edges[i];
                if (e.a < 0 || e.a >= n || e.b < 0 || e.b >= n || e.a == e.b) { continue; }

                Vector3 pa = nodes[e.a].position;
                Vector3 pb = nodes[e.b].position;
                double d = Distance(pa.x, pa.z, pb.x, pb.z);

                target[cursor[e.a]] = e.b;
                length[cursor[e.a]] = d;
                cursor[e.a]++;
                target[cursor[e.b]] = e.a;
                length[cursor[e.b]] = d;
                cursor[e.b]++;
            }
        }

        /// <summary> Shortest network distance from one source to every node, or <c>double.MaxValue</c> for a node in
        /// another piece of the network. Fills <paramref name="distance"/> rather than allocating it, so twelve sources
        /// cost one array between them. </summary>
        private static void Dijkstra(int source, int nodeCount, int[] start, int[] target, double[] length,
            double[] distance)
        {
            for (int i = 0; i < nodeCount; i++) { distance[i] = double.MaxValue; }
            distance[source] = 0.0;

            int[] heapNode = new int[target.Length + 1];
            double[] heapKey = new double[target.Length + 1];
            int heapCount = 0;
            HeapPush(heapNode, heapKey, ref heapCount, source, 0.0);

            while (heapCount > 0)
            {
                double key;
                int node = HeapPop(heapNode, heapKey, ref heapCount, out key);

                // The lazy-deletion check: a node already settled at a shorter distance is skipped rather than the heap
                // being searched for its stale entry, which is what makes the push-only heap correct.
                if (key > distance[node]) { continue; }

                for (int e = start[node]; e < start[node + 1]; e++)
                {
                    int next = target[e];
                    double through = key + length[e];
                    if (through >= distance[next]) { continue; }
                    distance[next] = through;
                    HeapPush(heapNode, heapKey, ref heapCount, next, through);
                }
            }
        }

        /// <summary> A binary min-heap over (node, distance) with lazy deletion, so a node may sit in it more than once
        /// and nothing is ever removed from the middle. The capacity is the directed-edge count plus one, which is an
        /// exact bound and not a guess: a push only happens when a relaxation finds a shorter route through one edge, so
        /// each directed edge can push at most once, and the source pushes once more. </summary>
        private static void HeapPush(int[] heapNode, double[] heapKey, ref int count, int node, double key)
        {
            if (count >= heapNode.Length) { return; }   // Unreachable by the bound above; a silent no-op beats a throw.

            int i = count;
            count++;
            heapNode[i] = node;
            heapKey[i] = key;

            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heapKey[parent] <= heapKey[i]) { break; }
                Swap(heapNode, heapKey, parent, i);
                i = parent;
            }
        }

        /// <summary> The smallest key in the heap, with the last entry moved to the root and sifted down. </summary>
        private static int HeapPop(int[] heapNode, double[] heapKey, ref int count, out double key)
        {
            int node = heapNode[0];
            key = heapKey[0];

            count--;
            heapNode[0] = heapNode[count];
            heapKey[0] = heapKey[count];

            int i = 0;
            while (true)
            {
                int left = (i * 2) + 1;
                if (left >= count) { break; }
                int right = left + 1;
                int smallest = right < count && heapKey[right] < heapKey[left] ? right : left;
                if (heapKey[i] <= heapKey[smallest]) { break; }
                Swap(heapNode, heapKey, i, smallest);
                i = smallest;
            }
            return node;
        }

        private static void Swap(int[] heapNode, double[] heapKey, int a, int b)
        {
            int node = heapNode[a];
            heapNode[a] = heapNode[b];
            heapNode[b] = node;

            double key = heapKey[a];
            heapKey[a] = heapKey[b];
            heapKey[b] = key;
        }
        #endregion

        #region "Compare -- one number, because case (n) needs something to threshold"
        /// <summary> How far apart two fingerprints are, 0 for a pair that measures the same and 1 for a pair as far
        /// apart as this scale can express. Case (n) of the city gate wants a number it can put a bound on, and a
        /// caller aiming a settlement at a target wants to know whether it got closer, so both get the same figure.
        /// <para>Only the terms BOTH sides carry are summed, and the total is divided by the weight actually used, so a
        /// measurement with no route factor is compared on the terms it has rather than being punished for the ones it
        /// lacks. A term missing on one side is not a difference of zero and it is not a difference of one: it is not
        /// evidence, and averaging over the weight used is what says so.</para></summary>
        public static float Compare(SettlementFingerprint a, SettlementFingerprint b)
        {
            string ignored;
            return Compare(a, b, out ignored);
        }

        /// <summary> The same distance with a sentence naming what drove it, for the gate's report and the inspector.
        /// The sentence names the largest weighted term rather than listing all three, because the one that dominates is
        /// the one an author would change something about. </summary>
        public static float Compare(SettlementFingerprint a, SettlementFingerprint b, out string why)
        {
            if (a == null || b == null)
            {
                // A missing fingerprint is not a match. Returning 0 here would read as "identical" to every caller
                // that thresholds this number, which is the opposite of what a null means.
                why = "One of the two fingerprints is missing, so there is nothing to compare and the distance is the "
                    + "largest it can be.";
                return 1f;
            }

            double weightUsed = 0.0;
            double total = 0.0;
            double worst = -1.0;
            string worstName = string.Empty;

            if (a.HasFamilyShares && b.HasFamilyShares)
            {
                double sum = 0.0;
                for (int i = 0; i < 4; i++)
                {
                    sum += Math.Abs(a.familyShare[i] - b.familyShare[i]);
                }

                // Two share vectors that sum to one are at most 2 apart in L1 -- all of one family against all of
                // another -- so halving it puts the term on the same 0..1 scale as the other two.
                double term = Clamp01(sum * 0.5);
                Accumulate(FamilyShareWeight, term, "the mix of block shapes and sizes", ref total, ref weightUsed,
                    ref worst, ref worstName);
            }
            if (a.HasOrientation && b.HasOrientation)
            {
                double term = Clamp01(Math.Abs(a.orientationOrder - b.orientationOrder));
                Accumulate(OrientationWeight, term, "how squarely the streets line up", ref total, ref weightUsed,
                    ref worst, ref worstName);
            }

            if (a.HasCircuity && b.HasCircuity)
            {
                // One grid's worth of detour is the unit: a perfect grid's route factor is 4/pi, so its EXCESS over the
                // straight line is 4/pi - 1, and two settlements that differ by that much differ by a whole term.
                double term = Clamp01(Math.Abs(a.circuity - b.circuity) / (GridRouteFactor - 1.0));
                Accumulate(RouteFactorWeight, term, "how far a trip runs past the straight line", ref total,
                    ref weightUsed, ref worst, ref worstName);
            }

            if (weightUsed <= 0.0)
            {
                why = "The two fingerprints have no measure in common -- one of them carries nothing that the other "
                    + "also carries -- so the distance is the largest it can be rather than a flattering zero.";
                return 1f;
            }

            double distance = Clamp01(total / weightUsed);
            why = "Distance " + distance.ToString("0.000", CultureInfo.InvariantCulture) + ", driven mostly by "
                + worstName + ". " + TermCountSentence(weightUsed);
            return (float)distance;
        }

        /// <summary> Adds one term and remembers it if it is the largest weighted contribution so far, which is what the
        /// sentence names. </summary>
        private static void Accumulate(double weight, double term, string name, ref double total, ref double weightUsed,
            ref double worst, ref string worstName)
        {
            total += weight * term;
            weightUsed += weight;
            if (weight * term <= worst) { return; }
            worst = weight * term;
            worstName = name;
        }

        /// <summary> Which terms went into the number, so a reader is never left guessing whether a small distance means
        /// "alike" or "barely measured". </summary>
        private static string TermCountSentence(double weightUsed)
        {
            double all = FamilyShareWeight + OrientationWeight + RouteFactorWeight;
            if (weightUsed >= all - 1e-6) { return "All three measures were available on both sides."; }
            return "Measures missing from one side were left out, so this compares "
                + (weightUsed / all).ToString("0.##", CultureInfo.InvariantCulture) + " of the full picture.";
        }

        private static double Clamp01(double value)
        {
            if (value < 0.0) { return 0.0; }
            if (value > 1.0) { return 1.0; }
            return value;
        }
        #endregion
    }
}
