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
    /// <summary> B-15 / C-15. A settlement's CHARACTER, as opposed to its order: whether it stands on plain ground, at
    /// a harbour, on a beach or beside a lake. Four real characters plus <see cref="Auto"/>, which is not a fifth
    /// character but an instruction to measure one.
    ///
    /// It is separate from the order because the two answer different questions and vary independently: a capital can
    /// be a port and a village can be a port, and neither fact tells you anything about the other. The order comes from
    /// the settlement role on the road (<see cref="SettlementRoleEnum"/>); the archetype comes from the water beside
    /// the site, measured by C-14's siting pass from the ground the author actually built.
    ///
    /// Which zones each archetype may use is NOT decided here -- an enum cannot carry a table. That is
    /// <see cref="SettlementArchetypeClass"/> in <c>SettlementOrderCatalog</c>, keyed on these values, so the wiring
    /// *Ports, beaches and lakesides* found missing is one authored row per archetype rather than a switch statement in
    /// however many files ask the question. </summary>
    public enum SettlementArchetype
    {
        /// <summary> Measure it. The siting pass reads the water adjacency it already has to compute and resolves this
        /// to one of the four below, which is why it is the default on <see cref="CityAuthoring.archetypeOverride"/>:
        /// the author disagreeing with the measurement is an override, and an override should look like one.
        ///
        /// Nothing downstream may treat Auto as a character in its own right. Where a number has to be produced before
        /// the measurement exists -- the wetted ceiling is the one case -- Auto resolves to <see cref="Plain"/>, the
        /// strictest of the four, so an unmeasured site is judged conservatively rather than optimistically. </summary>
        [InspectorName("Auto (measure it from the ground)")]
        Auto = 0,

        /// <summary> Inland, with no water edge inside the region. The strictest wetted ceiling of the four: a plain
        /// settlement's rect holding a quarter water means the rect is in the wrong place. </summary>
        [InspectorName("Plain (inland)")]
        Plain = 1,

        /// <summary> A working harbour: the settlement reaches the water rather than standing back from it, which is
        /// what admits <c>zone.port.logistics</c> and its 0 m shoreline setback. </summary>
        [InspectorName("Port")]
        Port = 2,

        /// <summary> A coast the settlement stands back from: the edge is a strip of beach and path, which is the case
        /// <c>ZoneTypeClass.shorelineSetbackMetres</c> was authored for. </summary>
        [InspectorName("Beach")]
        Beach = 3,

        /// <summary> Beside still fresh water. The same test as a coast -- a water surface above the ground -- so the
        /// tool never needs to know which body it is looking at, only that it is there. </summary>
        [InspectorName("Lakeside")]
        Lakeside = 4
    }

    /// <summary> The four street-pattern families a settlement's fingerprint is drawn from.
    ///
    /// The COUNT and the METHOD are Louf and Barthelemy 2014, <i>A typology of street patterns</i> (J. R. Soc.
    /// Interface): a city is characterised not by its roads but by its BLOCKS -- each block's shape factor, meaning its
    /// area divided by the area of its circumscribed circle, conditioned on its area -- and that joint distribution
    /// sorts 131 real cities into four families. It separates cities that road-length and intersection-count statistics
    /// call identical, which is why it is the measurement this tool uses for <i>"no two look the same"</i>.
    ///
    /// The four NAMES below are THIS PROJECT'S, not the paper's. The paper numbers its groups; inventing English names
    /// for them and then citing the paper for the names would be a citation the source does not carry. Each name says
    /// where the family sits in the (area, shape-factor) plane, which is all a weight needs to mean.
    ///
    /// The families are declared here because the WEIGHTS are here -- one per order, on
    /// <see cref="SettlementOrderClass"/> -- and a weight vector with no index type is four unnamed floats. C-19's
    /// fingerprint measurement reads this enum; it does not declare a second one. </summary>
    public enum StreetPatternFamily
    {
        /// <summary> Small blocks with a high shape factor: short, near-rectangular, laid to a coherent direction. The
        /// planned-grid corner of the plane. </summary>
        [InspectorName("Regular small blocks")]
        RegularSmallBlocks = 0,

        /// <summary> Small blocks with a low shape factor: as short as the grid family but bent and unequal, which is
        /// what an organically grown core measures as. </summary>
        [InspectorName("Irregular small blocks")]
        IrregularSmallBlocks = 1,

        /// <summary> A wide spread of block areas at a middling shape factor: a core of one age with later, coarser
        /// extensions around it, which is the commonest real mixture. </summary>
        [InspectorName("Mixed block sizes")]
        MixedBlockSizes = 2,

        /// <summary> Large blocks with a low shape factor: few streets over a lot of ground, which is both a village's
        /// pattern and a capital's industrial fringe. </summary>
        [InspectorName("Large irregular blocks")]
        LargeIrregularBlocks = 3
    }

    /// <summary> B-15 / C-15. ONE order of settlement -- capital, city, town or village -- and every figure that says
    /// how big it is, how far from its neighbours, and what its streets measure like. Four of these live in a
    /// <see cref="SettlementOrderCatalog"/>, deliberately the same shape as <c>RoadClass</c> in
    /// <c>RoadClassCatalog</c>: a <c>[Serializable]</c> record in a <c>ScriptableObject</c> list, so an author can edit
    /// it in the inspector and a gate can read it without a scene.
    ///
    /// EVERY FIELD CARRIES ITS SOURCE IN ITS OWN COMMENT, and the sources are of three different kinds. Reading which
    /// kind a number is matters more than reading the number:
    ///
    ///   * FITTED -- the population band, the rank-size exponent, the allometric exponents and the family weights come
    ///     from C-16's import of GeoNames <c>cities1000</c>. What ships in <see cref="SettlementOrderCatalog"/> is a
    ///     PLACEHOLDER of the right magnitude so a fresh catalogue runs, and <c>SettlementOrderCatalog.fittedFromData</c>
    ///     is false until the importer has written a real fit. A consumer that wants to tell a fit from a placeholder
    ///     reads that flag; nothing here pretends a placeholder is a measurement.
    ///   * CITED -- the spacing band, which comes from Christaller's own empirical passages, in miles, converted here.
    ///   * DERIVED -- the inter-order spacing ratio, computed from the K value in the catalogue. It is not on this
    ///     record at all, because a ratio between two orders belongs to neither of them.
    ///
    /// The order does NOT decide which zones it may use; that is per-archetype, on
    /// <see cref="SettlementArchetypeClass"/>. It does decide the wetted-fraction ceiling, per archetype, because how
    /// much water a rect may hold depends on both -- a capital cannot be mostly water while a fishing village on a spit
    /// legitimately is nearly all edge. </summary>
    [Serializable]
    public sealed class SettlementOrderClass
    {
        /// <summary> 1 capital, 2 city, 3 town, 4 village: the id the catalogue is looked up by, and an ORDER in the
        /// arithmetic sense -- 1 is the largest, and every ladder rule below reads "as the index falls". Unique within a
        /// catalogue. </summary>
        public int orderIndex = 1;

        /// <summary> The author's own word for this order, shown in the inspector and in every report. Not a key. </summary>
        public string displayName = "";

        /// <summary> Which settlement role on a hand-drawn road seeds this order at its free ends. This is the whole
        /// role-to-order table, held as DATA on the four records rather than as a switch statement anywhere, which is
        /// what <c>SettlementRoleEnum</c>'s own header defers to and what <c>CityAuthoring.TryGetSourceRole</c> reaches
        /// the order through. <c>None</c> is not a legal value here -- a role that seeds nothing cannot seed an
        /// order -- and <see cref="SettlementOrderCatalog.Validate"/> refuses it. </summary>
        public SettlementRoleEnum seededByRole = SettlementRoleEnum.None;

        // ------------------------------------------------------------------ how big: FITTED from GeoNames (C-16)

        /// <summary> Smallest population this order admits. FITTED: the four bands come from quantiles of GeoNames
        /// <c>cities1000</c> by feature code -- capital from <c>PPLC</c> and <c>PPLA</c>, city from
        /// <c>PPLA2</c>/<c>PPLA3</c>, town from the upper <c>PPL</c> quantiles, village from the lower <c>PPL</c> and
        /// <c>PPLX</c> ones. The shipped value is a placeholder of the right magnitude, not a fit; see the class header
        /// on <c>fittedFromData</c>. </summary>
        public int populationMin = 200;

        /// <summary> Largest population this order admits, from the same fit. The bands descend and touch across the
        /// four orders -- one order's floor is the next one's ceiling -- so no population falls between two orders and
        /// none belongs to both. <see cref="SettlementOrderCatalog.Validate"/> asserts that ladder. </summary>
        public int populationMax = 5000;

        /// <summary> The rank-size exponent q in P(r) = P(1) / r^q, applied WITHIN this order: the second city of an
        /// order is not the first with a different seed, it is the second point on a measured distribution, which is
        /// what stops every place of one order coming out the same size.
        ///
        /// FITTED, and the register is explicit that it must not be ASSUMED to be 1. The shipped 1.0 is the neutral
        /// value that makes the arithmetic run before a fit exists -- it is flagged as a placeholder by
        /// <c>fittedFromData</c> being false, which is exactly the difference between a default and an assumption.
        /// Picking any other number here would be worse: it would be invented rather than merely provisional. </summary>
        public float rankSizeExponent = 1f;

        // ------------------------------------------------------------------ how far apart: CITED, then DERIVED

        /// <summary> Nearest-neighbour spacing floor, in metres. CITED: Christaller's empirical passages give the
        /// satellites around Cambridge at 10-15 miles out and the Californian railroad towns at about 12 miles apart
        /// with the larger ones about 60 miles apart. Those are the only numbers the source puts on the page, and they
        /// are the ones the town band is anchored on (10 miles = 16093.44 m, at 1 mile = 1609.344 m exactly).
        ///
        /// The other three bands are that anchor multiplied by the catalogue's inter-order ratio, which is DERIVED from
        /// the K value and not quoted from Christaller. That the capital band's ceiling then lands on 60 miles -- 15
        /// miles times two steps of the ratio -- is a corroboration of the derivation by the source's other figure, not
        /// a second citation. </summary>
        public float spacingMinMetres = 8046.72f;

        /// <summary> Nearest-neighbour spacing ceiling, in metres, from the same passage and the same ladder. A
        /// candidate closer to an existing place of its own order than <see cref="spacingMinMetres"/> is a suburb rather
        /// than a settlement, which is the judgement Reilly's breakpoint makes in C-14; this band is what the breakpoint
        /// is measured against. </summary>
        public float spacingMaxMetres = 12070.08f;

        // ------------------------------------------------------------------ how much of it: sub-linear allometry

        /// <summary> The exponent B in built-up area proportional to P^B, with B below 1. FITTED. The FORM is
        /// Bettencourt et al. 2007's sub-linear infrastructure allometry -- infrastructure grows more slowly than the
        /// population it serves -- and it is the form, not the exponent, that is cited: the exponent is fitted from the
        /// imported data and never quoted from the paper.
        ///
        /// This is what makes a village's region small AND dense-cored rather than a city scaled down: at B below 1 the
        /// area per head is larger for a small place, so the same rule gives a village more ground per person than a
        /// capital without either figure being authored twice. <see cref="SettlementOrderCatalog.Validate"/> refuses a
        /// value outside (0, 1), because outside that interval the word sub-linear is no longer true of it. </summary>
        public float builtAreaExponent = 0.85f;

        /// <summary> The same exponent for total street length. FITTED, same source and same restriction. It is a
        /// separate number from <see cref="builtAreaExponent"/> because area and network length are separate
        /// measurements that happen to share a form -- collapsing them into one field would be asserting they scale
        /// identically, which the fit is what decides. </summary>
        public float streetLengthExponent = 0.8f;

        /// <summary> The population the reference pair below was measured at. A fit is a SLOPE and a POINT; the exponent
        /// is the slope, and these three fields are the point.
        ///
        /// The point is stored as a real measurable triple -- this many people cover this much ground with this much
        /// street -- rather than as a coefficient, deliberately. A coefficient in area = a x P^B carries the units
        /// m^2 per person^B, which no author can sanity-check and no importer can report meaningfully; a population with
        /// an area beside it is a sentence about a real town. It is also exactly what C-16's fit produces. </summary>
        public int referencePopulation = 800;

        /// <summary> Built-up area in square metres AT <see cref="referencePopulation"/>. FITTED, and read through
        /// <see cref="BuiltAreaM2"/> rather than directly, so the exponent is applied in one place. </summary>
        public float referenceBuiltAreaM2 = 500000f;

        /// <summary> Total street length in metres AT <see cref="referencePopulation"/>. FITTED, read through
        /// <see cref="StreetLengthMetres"/>. It is the figure a street network is sized against, so a settlement's
        /// street total is a consequence of its population rather than of how long the generator felt like running.
        /// </summary>
        public float referenceStreetLengthMetres = 9000f;

        // ------------------------------------------------------------------ what it measures like: family weights

        /// <summary> How likely a settlement of this order is to draw the regular-grid family for its street pattern.
        /// FITTED per order and, in C-16's table, per continent -- which is what stops a European village being shaped
        /// like a North American one.
        ///
        /// The four weights are a DISTRIBUTION, not four independent switches: a settlement draws one family from them
        /// at a seeded point, then aims the generator's existing knobs at that family's conditional (area, shape-factor)
        /// target, and C-19 measures the result against it. Read them through
        /// <see cref="NormalisedFamilyWeight"/>, never directly, so an author who types four numbers that do not sum to
        /// one still gets a usable distribution instead of a silently skewed one. </summary>
        [Min(0f)] public float familyWeightRegularSmallBlocks = 0.1f;

        /// <summary> Weight for the irregular-small-blocks family: an organically grown core. Highest for the lowest
        /// orders, because a village that was never planned is the commonest case of one. </summary>
        [Min(0f)] public float familyWeightIrregularSmallBlocks = 0.45f;

        /// <summary> Weight for the mixed-sizes family: a core of one age with coarser extensions around it. Rises with
        /// the order, because being extended repeatedly is what a large place has done. </summary>
        [Min(0f)] public float familyWeightMixedBlockSizes = 0.15f;

        /// <summary> Weight for the large-irregular family: few streets over a lot of ground. It is high at both ends of
        /// the ladder for two different reasons -- a village has barely any streets, a capital has an industrial fringe
        /// -- which is why the four weights are authored per order rather than interpolated between two extremes.
        /// </summary>
        [Min(0f)] public float familyWeightLargeIrregularBlocks = 0.3f;

        // ------------------------------------------------------------------ how much water a region may hold

        /// <summary> The wetted-fraction ceiling for a PLAIN settlement of this order: the largest fraction of the
        /// proposed region that may lie under a water surface before C-14 refuses the candidate outright.
        ///
        /// AUTHORED, and it lives here rather than inside <c>SettlementSiting</c> on purpose -- a number buried in a
        /// comparison is neither editable by an author nor readable by a gate. It is NOT the shoreline setback and does
        /// not replace it: the setback trims a footprint that has already been accepted, while this decides whether
        /// there is a settlement here at all. The anchor test -- water directly over the terminus -- has no ceiling and
        /// no override; a settlement never starts under a water surface, whatever these four numbers say.
        ///
        /// The plain figure is the strictest of the four because an inland settlement's region holding this much water
        /// means the rect is in the wrong place, not that the place is coastal. It rises as the order falls only because
        /// a small rect is intruded on proportionally more by the same stream. </summary>
        [Range(0f, 1f)] public float wettedCeilingPlain = 0.2f;

        /// <summary> The same ceiling for a PORT. The most permissive of the four: a harbour is water inside the region
        /// by definition, and the quay zone that makes a port a port is authored with a 0 m shoreline setback precisely
        /// so it can reach the water. A ceiling tight enough to keep a capital dry would refuse every real harbour.
        /// </summary>
        [Range(0f, 1f)] public float wettedCeilingPort = 0.65f;

        /// <summary> The same ceiling for a BEACH settlement: looser than plain because the coast is inside the region,
        /// tighter than a port because the settlement stands BACK from the water rather than into it -- the strip of
        /// beach and path is land, and it is what the shoreline setback preserves. </summary>
        [Range(0f, 1f)] public float wettedCeilingBeach = 0.6f;

        /// <summary> The same ceiling for a LAKESIDE settlement. It matches the beach figure rather than sitting between
        /// two others, because the tool's definition of water is one test -- a surface above the ground -- and a lake
        /// shore and a sea shore are the same geometry to a settlement standing on it. Authoring a third number here
        /// would be claiming a difference the measurement cannot see. </summary>
        [Range(0f, 1f)] public float wettedCeilingLakeside = 0.6f;

        // ------------------------------------------------------------------ readers: every law applied in one place

        /// <summary> The wetted-fraction ceiling in force for one archetype. <see cref="SettlementArchetype.Auto"/>
        /// answers with the plain figure -- the strictest -- because Auto means the measurement has not happened yet,
        /// and an unmeasured site judged by a harbour's ceiling would be accepted on optimism. </summary>
        public float WettedFractionCeiling(SettlementArchetype archetype)
        {
            switch (archetype)
            {
                case SettlementArchetype.Port: return wettedCeilingPort;
                case SettlementArchetype.Beach: return wettedCeilingBeach;
                case SettlementArchetype.Lakeside: return wettedCeilingLakeside;
                default: return wettedCeilingPlain;
            }
        }

        /// <summary> Built-up area in square metres for a population of this order, by the sub-linear allometry: the
        /// reference point scaled by the population ratio raised to <see cref="builtAreaExponent"/>. This is the figure
        /// the region rect is sized from, so a rect is a consequence of a population rather than a number somebody
        /// typed. Refuses nothing -- a population at or below zero returns zero area, which is the honest answer to
        /// "how much ground does nobody need". </summary>
        public float BuiltAreaM2(int population)
        {
            return ScaleByPopulation(referenceBuiltAreaM2, builtAreaExponent, population);
        }

        /// <summary> Total street length in metres for a population of this order, by the same law with its own
        /// exponent. </summary>
        public float StreetLengthMetres(int population)
        {
            return ScaleByPopulation(referenceStreetLengthMetres, streetLengthExponent, population);
        }

        /// <summary> The one place the allometry is evaluated: reference value times (population / reference
        /// population) raised to the exponent. Both readers above go through it so the two cannot drift into two
        /// spellings of one law. </summary>
        private float ScaleByPopulation(float referenceValue, float exponent, int population)
        {
            if (population <= 0 || referencePopulation <= 0 || referenceValue <= 0f) { return 0f; }
            float ratio = population / (float)referencePopulation;
            return referenceValue * Mathf.Pow(ratio, exponent);
        }

        /// <summary> The population of the rank-th largest place of this order, by the rank-size law: the band's ceiling
        /// divided by the rank raised to <see cref="rankSizeExponent"/>, floored at the band's own minimum so the tenth
        /// town of an order is still a town. Rank 1 is the largest. A rank below 1 answers with the ceiling rather than
        /// refusing, because there is no meaningful place above the largest. </summary>
        public int PopulationAtRank(int rank)
        {
            if (rank <= 1) { return populationMax; }
            float scaled = populationMax / Mathf.Pow(rank, rankSizeExponent);
            if (scaled < populationMin) { return populationMin; }
            return Mathf.RoundToInt(scaled);
        }

        /// <summary> Whether a population falls in this order's band, ceiling exclusive so the touching bands do not
        /// both claim the boundary value -- the larger order takes it, because a place exactly at a city's floor is a
        /// city. The lowest order's floor is inclusive. </summary>
        public bool AdmitsPopulation(int population)
        {
            return population >= populationMin && population < populationMax;
        }

        /// <summary> The authored weight for one family, before normalisation. Public because a report that shows an
        /// author what they typed must show what they typed. </summary>
        public float FamilyWeight(StreetPatternFamily family)
        {
            switch (family)
            {
                case StreetPatternFamily.RegularSmallBlocks: return familyWeightRegularSmallBlocks;
                case StreetPatternFamily.IrregularSmallBlocks: return familyWeightIrregularSmallBlocks;
                case StreetPatternFamily.MixedBlockSizes: return familyWeightMixedBlockSizes;
                default: return familyWeightLargeIrregularBlocks;
            }
        }

        /// <summary> The sum of the four authored weights. Zero means the author cleared them all, which
        /// <see cref="NormalisedFamilyWeight"/> answers with an even split rather than a division by zero. </summary>
        public float FamilyWeightSum
        {
            get
            {
                return Mathf.Max(0f, familyWeightRegularSmallBlocks) + Mathf.Max(0f, familyWeightIrregularSmallBlocks)
                       + Mathf.Max(0f, familyWeightMixedBlockSizes) + Mathf.Max(0f, familyWeightLargeIrregularBlocks);
            }
        }

        /// <summary> One family's share of the distribution, in 0..1 and summing to 1 across the four. Negative authored
        /// weights are clamped to zero rather than allowed to cancel a sibling, and four zeroes give an even quarter
        /// each -- a settlement with no family is not a settlement, so the failure mode is "any of the four" and not a
        /// division by zero. </summary>
        public float NormalisedFamilyWeight(StreetPatternFamily family)
        {
            float sum = FamilyWeightSum;
            if (sum <= 0f) { return 0.25f; }
            return Mathf.Max(0f, FamilyWeight(family)) / sum;
        }

        /// <summary> The reference point's area per head, in square metres. The catalogue's ladder check reads it: at a
        /// sub-linear exponent this figure must FALL as the order rises, because a capital housing a thousand times the
        /// people of a village on a thousand times the ground would be linear growth, which is the one thing the form
        /// says a real settlement system does not do. </summary>
        public float ReferenceAreaPerPersonM2
        {
            get { return referencePopulation <= 0 ? 0f : referenceBuiltAreaM2 / referencePopulation; }
        }

        /// <summary> Whether this record's own numbers are internally consistent, naming the first that is not.
        /// Deliberately narrow: it judges THIS order and never its neighbours, so the catalogue's ladder rules -- the
        /// descending bands, the spacing ratio, the falling area per head -- are asserted once, in
        /// <see cref="SettlementOrderCatalog.Validate"/>, rather than half here and half there. </summary>
        public bool IsSelfConsistent(out string reason)
        {
            string label = string.IsNullOrEmpty(displayName) ? ("order " + orderIndex) : displayName;
            if (orderIndex < 1)
            {
                reason = label + " carries the order index " + orderIndex + ", and the index is the rung of the ladder"
                         + " -- 1 is the largest order, so there is no rung below 1";
                return false;
            }
            if (seededByRole == SettlementRoleEnum.None)
            {
                reason = label + " is seeded by no settlement role, so no road end could ever propose it. A road with no"
                         + " role seeds nothing, which means an order claiming that role is an order nothing reaches";
                return false;
            }
            if (populationMin < 0 || populationMax <= populationMin)
            {
                reason = label + " has the population band " + populationMin + " to " + populationMax + ", which encloses"
                         + " no population. The band is fitted from GeoNames quantiles, so a band this shape is an edit"
                         + " rather than a missing fit";
                return false;
            }
            if (rankSizeExponent <= 0f)
            {
                reason = label + " has the rank-size exponent " + rankSizeExponent.ToString("0.###") + ". At or below"
                         + " zero the law stops ordering places by size -- every rank would come out at least as large as"
                         + " the first, which is not a rank-size distribution";
                return false;
            }
            if (spacingMinMetres <= 0f || spacingMaxMetres < spacingMinMetres)
            {
                reason = label + " has the spacing band " + spacingMinMetres.ToString("0.#") + " m to "
                         + spacingMaxMetres.ToString("0.#") + " m, which no pair of places can satisfy. The band is"
                         + " anchored on Christaller's 10 to 15 miles and stepped by the derived inter-order ratio, so it"
                         + " is positive and ordered by construction";
                return false;
            }
            if (builtAreaExponent <= 0f || builtAreaExponent >= 1f || streetLengthExponent <= 0f
                || streetLengthExponent >= 1f)
            {
                reason = label + " has the allometric exponents " + builtAreaExponent.ToString("0.###") + " for area and "
                         + streetLengthExponent.ToString("0.###") + " for street length. Both must lie strictly between"
                         + " zero and one, because that interval is what the word sub-linear means: at 1 infrastructure"
                         + " grows exactly with population, and above it faster, neither of which the fitted form allows";
                return false;
            }
            if (referencePopulation <= 0 || referenceBuiltAreaM2 <= 0f || referenceStreetLengthMetres <= 0f)
            {
                reason = label + " has no usable reference point -- " + referencePopulation + " people on "
                         + referenceBuiltAreaM2.ToString("0.#") + " m2 with " + referenceStreetLengthMetres.ToString("0.#")
                         + " m of street. A fit is a slope and a point, and an exponent with no point behind it sizes"
                         + " nothing";
                return false;
            }
            if (FamilyWeightSum <= 0f)
            {
                reason = label + " weights all four street-pattern families at zero, so it draws no fingerprint at all."
                         + " An even split is what the reader falls back to, but a catalogue that means an even split"
                         + " should say so with four equal weights rather than with four empty ones";
                return false;
            }
            // The four ceilings are fractions of an area, so each one lies in [0, 1]. [Range] already says so in the
            // inspector, but an attribute constrains a SLIDER and not an assignment: a catalogue built in code, or one
            // whose numbers came from an importer, can carry 1.4 past it. A ceiling above one can never be exceeded and
            // a negative one can never be met, so both silently turn the water test off -- which is exactly the failure
            // the test exists to prevent, and it must be refused where the fields live rather than only where they are
            // read.
            if (wettedCeilingPlain < 0f || wettedCeilingPlain > 1f || wettedCeilingPort < 0f || wettedCeilingPort > 1f
                || wettedCeilingBeach < 0f || wettedCeilingBeach > 1f || wettedCeilingLakeside < 0f
                || wettedCeilingLakeside > 1f)
            {
                reason = label + " carries a wetted-fraction ceiling outside zero to one (plain "
                         + wettedCeilingPlain.ToString("0.###") + ", port " + wettedCeilingPort.ToString("0.###")
                         + ", beach " + wettedCeilingBeach.ToString("0.###") + ", lakeside "
                         + wettedCeilingLakeside.ToString("0.###") + "). Each is the fraction of a region's area that"
                         + " may be water, and a fraction of an area lies between none of it and all of it";
                return false;
            }
            if (wettedCeilingPlain > wettedCeilingPort || wettedCeilingPlain > wettedCeilingBeach
                || wettedCeilingPlain > wettedCeilingLakeside)
            {
                reason = label + " lets a plain inland settlement hold more water in its region than one of its"
                         + " water-adjacent archetypes. That inverts what the ceilings are for: the plain figure is the"
                         + " strictest of the four because water inside an inland rect means the rect is misplaced,"
                         + " while water inside a harbour's rect is the harbour";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }

    /// <summary> B-15 / C-15. ONE archetype's row: which zones a settlement of that character may use, and whether the
    /// character requires a water edge at all. Five of these live in a <see cref="SettlementOrderCatalog"/>, one per
    /// value of <see cref="SettlementArchetype"/>.
    ///
    /// This class IS the one line of wiring the register found missing. Everything a port needs already existed -- the
    /// quay zone with its 0 m shoreline setback in <c>ZoneCatalog</c>, the water-first cell classification in
    /// <c>FootprintConform</c>, <c>HubKind.Port</c> with its quay-edge depth, the port prefab category, and two gates
    /// asserting all of it -- and none of it was ever selected by anything, because nothing said WHICH zones a port
    /// admits and an inland village does not.
    ///
    /// Zones are named by their stable ids, not held as references. That is deliberate and it is the existing
    /// convention: <c>ZoneCatalog</c> resolves an id and returns null for an unknown one rather than mapping it to a
    /// default, so a mistyped id here fails closed and <see cref="SettlementOrderCatalog.ValidateAgainstZones"/> names
    /// it. Holding the zone objects instead would put a second copy of the taxonomy in this asset, which the zone
    /// registry's own header exists to prevent. </summary>
    [Serializable]
    public sealed class SettlementArchetypeClass
    {
        /// <summary> Which archetype this row is for. Unique within a catalogue, and
        /// <see cref="SettlementArchetype.Auto"/> gets a row like the rest: Auto is what a settlement carries before it
        /// is measured, so the zones it admits are the ones any dry site may use. </summary>
        public SettlementArchetype archetype = SettlementArchetype.Plain;

        /// <summary> The name shown in the inspector and in every siting report. Not a key. </summary>
        public string displayName = "";

        /// <summary> The stable zone ids a settlement of this archetype may paint, in authoring order. An empty list is
        /// refused rather than read as "all of them": a row that admits nothing would silently produce a settlement with
        /// no zones, and fail-closed is the registry's own rule. </summary>
        public string[] admittedZoneIds = new string[0];

        /// <summary> Whether this archetype may only be chosen for a site that actually has a water edge. True for port,
        /// beach and lakeside; false for plain and for Auto.
        ///
        /// It is the archetype-side counterpart of <c>ZoneTypeClass.boundToWaterEdge</c>, which says the same thing
        /// about a zone. Both are needed and neither is derivable from the other: the zone flag decides whether a zone
        /// must touch water, this one decides whether a whole settlement may claim to. C-14 reads it to refuse an
        /// override -- an author declaring a port on ground with no water beside it gets told which of the two facts is
        /// missing, rather than a harbour with no sea. </summary>
        public bool requiresWaterEdge;

        /// <summary> Whether this archetype admits the zone with the given id. Case-sensitive, because the ids are keys
        /// and <c>ZoneCatalog.Resolve</c> compares them the same way -- a lookup that matched case-insensitively here and
        /// exactly there would admit a zone the registry then refuses to resolve. </summary>
        public bool AdmitsZone(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId) || admittedZoneIds == null) { return false; }
            for (int i = 0; i < admittedZoneIds.Length; i++)
            {
                if (admittedZoneIds[i] == zoneId) { return true; }
            }
            return false;
        }

        /// <summary> Whether this row is usable on its own terms, naming the first thing that is not. Whether the ids it
        /// names exist is a different question, asked by <see cref="SettlementOrderCatalog.ValidateAgainstZones"/>
        /// against the one zone registry -- this method cannot answer it without holding a second copy of the taxonomy.
        /// </summary>
        public bool IsSelfConsistent(out string reason)
        {
            string label = string.IsNullOrEmpty(displayName) ? archetype.ToString() : displayName;
            if (admittedZoneIds == null || admittedZoneIds.Length == 0)
            {
                reason = "the " + label + " archetype admits no zones, so a settlement of that character would be zoned"
                         + " with nothing. An empty list is not read as every zone: the zone registry resolves an unknown"
                         + " id to null rather than to a default, and this row follows the same rule";
                return false;
            }
            for (int i = 0; i < admittedZoneIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(admittedZoneIds[i])) { continue; }
                reason = "the " + label + " archetype holds an empty zone id at position " + i + ", which is an author"
                         + " having pressed + without choosing a zone. It is refused rather than skipped, because a row"
                         + " that quietly ignores a blank teaches nobody that the blank is there";
                return false;
            }
            if (requiresWaterEdge && (archetype == SettlementArchetype.Plain || archetype == SettlementArchetype.Auto))
            {
                reason = "the " + label + " archetype requires a water edge, which would leave every inland site with no"
                         + " archetype at all. Plain is the character of ground with no water beside it, and Auto is what"
                         + " a settlement carries before the water has been measured";
                return false;
            }
            reason = string.Empty;
            return true;
        }
    }
}
