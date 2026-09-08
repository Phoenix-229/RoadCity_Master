#region "Imports"
using System.Collections.Generic;
using UnityEngine;
#endregion


namespace RoadCityMaster.City
{
    /// <summary> B-15 / C-15. The authored catalogue of settlement orders and archetypes: four
    /// <see cref="SettlementOrderClass"/> records saying how big each order of place is and how far apart, and five
    /// <see cref="SettlementArchetypeClass"/> rows saying which zones each character of place may use.
    ///
    /// It is deliberately the same shape as <see cref="RoadClassCatalog"/> -- a <c>ScriptableObject</c> holding lists of
    /// <c>[Serializable]</c> records, with <see cref="ResetToDefaults"/> on the context menu -- and for the same two
    /// reasons: an author can edit the numbers that decide a settlement system's shape without touching code, and a gate
    /// can read them without a scene. The road layer's own catalogue proved the shape; this one reuses it rather than
    /// inventing a second convention.
    ///
    /// WHAT LIVES HERE AND WHAT DOES NOT. This asset holds the figures a settlement's size, spacing, fingerprint and
    /// water tolerance are decided from. It holds no scene state, samples no ground, and decides nothing about a
    /// particular site: which order a terminus may propose comes from the road's <c>SettlementRoleEnum</c>, whether the
    /// site is dry comes from C-14's siting pass, and both of those read this catalogue rather than carrying copies of
    /// its numbers. The zone taxonomy itself is <c>ZoneCatalog</c>'s -- the rows here name zones by their stable ids, so
    /// this asset does not become a second list of zones.
    ///
    /// THREE KINDS OF NUMBER, and <see cref="fittedFromData"/> is how a reader tells them apart. The spacing band is
    /// CITED from Christaller's own empirical figures, in miles, converted here by an exact constant. The inter-order
    /// ratio is DERIVED from <see cref="christallerK"/> and, as <see cref="InterOrderSpacingRatio"/> says in those
    /// words, not quoted from Christaller. Everything else -- the population bands, the rank-size exponent, the
    /// allometric exponents and the family weights -- is FITTED from GeoNames by C-16's importer, and what
    /// <see cref="ResetToDefaults"/> writes are placeholders of the right magnitude so a fresh catalogue runs. Until the
    /// importer has run, <see cref="fittedFromData"/> is false and every report that quotes these figures says so.
    /// </summary>
    [CreateAssetMenu(fileName = "SettlementOrderCatalog",
        menuName = "Road City Master/Settlement Order Catalog", order = 4)]
    public sealed class SettlementOrderCatalog : ScriptableObject
    {
        /// <summary> The four orders. Lookup is by <see cref="SettlementOrderClass.orderIndex"/> and by
        /// <see cref="SettlementOrderClass.seededByRole"/>, never by position, so an author may reorder the list
        /// freely. </summary>
        public List<SettlementOrderClass> orders = new List<SettlementOrderClass>();

        /// <summary> The five archetype rows, one per value of <see cref="SettlementArchetype"/>. Looked up by
        /// <see cref="SettlementArchetypeClass.archetype"/>, so position is not significant here either. </summary>
        public List<SettlementArchetypeClass> archetypes = new List<SettlementArchetypeClass>();

        /// <summary> Christaller's K for the arrangement this tool assumes: 4, the TRANSPORT principle, under which a
        /// lower-order place sits at the midpoint of the higher order's hexagon edge -- that is, ON THE ROAD between two
        /// higher-order centres, because that arrangement minimises the length of road needed to serve the system.
        ///
        /// That is the author's own instruction stated from the literature's chair: the roads are drawn first and a
        /// place begins at a road's free end. It is authored rather than fixed at 4 because the marketing principle
        /// (K = 3, a place at the hexagon's vertex) and the administrative principle (K = 7) are the other two
        /// arrangements the same source describes, and a project modelling one of those should be able to say so.
        /// </summary>
        [Min(1)] public int christallerK = 4;

        /// <summary> Whether the FITTED figures in this catalogue came from a real import, or are still the placeholders
        /// <see cref="ResetToDefaults"/> writes. C-16's importer sets it true as it writes the fit.
        ///
        /// It exists so that no report ever presents a placeholder as a measurement. The register's rule is that the
        /// rank-size exponent must not be ASSUMED to be 1 and the allometric exponents must never be quoted from the
        /// paper that gives their form; a shipped default is not an assumption as long as something says out loud that it
        /// is a default, and this flag is that something. </summary>
        public bool fittedFromData;

        /// <summary> Where the fit came from, written by C-16's importer -- the dump, its date, the record count and the
        /// continent filter if one was used. Free text, because it is provenance for a human to read rather than
        /// anything code branches on, and empty while <see cref="fittedFromData"/> is false. </summary>
        public string fitProvenance = "";

        /// <summary> Metres in one international mile, exactly. Christaller's figures are in miles, and converting them
        /// at the point of authoring rather than storing miles keeps every distance in this tool in one unit. </summary>
        public const float MetresPerMile = 1609.344f;

        /// <summary> Order indices, so code refers to the four rungs by name rather than by a bare integer -- the same
        /// service <c>RoadClassCatalog</c>'s three id constants provide. 1 is the largest. </summary>
        public const int CapitalOrder = 1;
        public const int CityOrder = 2;
        public const int TownOrder = 3;
        public const int VillageOrder = 4;

        /// <summary> The town band's anchor, in miles: Christaller records the satellites around Cambridge at 10 to 15
        /// miles out, and the Californian railroad towns at about 12 miles apart -- which falls inside that band -- with
        /// the larger ones about 60 miles apart. These two numbers are the only distances the source puts on the page,
        /// and the whole spacing ladder is built from them. </summary>
        public const float TownSpacingAnchorMinMiles = 10f;
        public const float TownSpacingAnchorMaxMiles = 15f;

        // ------------------------------------------------------------------ the derived ratio

        /// <summary> The spacing ratio between one order and the next: the square root of
        /// <see cref="christallerK"/>. A market area K times larger has a linear dimension root-K larger, so at K = 4
        /// each order sits twice as far from its own kind as the order below it.
        ///
        /// This ratio is DERIVED FROM THE K VALUE, NOT QUOTED FROM CHRISTALLER. The source gives the K values and it
        /// gives the two distances quoted above; it does not give a multiplier between orders. It is computed here rather
        /// than stored as a number precisely so that nobody can later mistake it for a figure read off a page -- and a
        /// later pass must not promote it to a citation.
        ///
        /// That the ladder built from it lands the capital band's ceiling on 60 miles, which is the source's other
        /// figure, is a corroboration of the derivation. It is not a second citation and must not be written up as
        /// one. </summary>
        public float InterOrderSpacingRatio
        {
            get { return Mathf.Sqrt(Mathf.Max(1, christallerK)); }
        }

        /// <summary> Miles as metres, by <see cref="MetresPerMile"/>. Used where a cited figure is authored, so the
        /// citation stays legible in the code that writes it. </summary>
        public static float Miles(float miles)
        {
            return miles * MetresPerMile;
        }

        // ------------------------------------------------------------------ lookups: fail-closed, like ZoneCatalog

        /// <summary> The order with the given index, or null when the catalogue has none. Linear scan: a catalogue holds
        /// four orders, not thousands, so an index would cost more to keep valid than it saves -- the same reasoning
        /// <c>RoadClassCatalog.Get</c> records. </summary>
        public SettlementOrderClass Get(int orderIndex)
        {
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != null && orders[i].orderIndex == orderIndex) { return orders[i]; }
            }
            return null;
        }

        /// <summary> True when an order with the given index exists, returning it. </summary>
        public bool TryGet(int orderIndex, out SettlementOrderClass order)
        {
            order = Get(orderIndex);
            return order != null;
        }

        /// <summary> The order a settlement role seeds, or a refusal naming why there is none. THIS IS THE ROLE-TO-ORDER
        /// TABLE, and it is a scan over authored data rather than a switch statement, which is why
        /// <c>SettlementRoleEnum</c>'s header defers the table to this asset instead of listing it: a switch here would
        /// be a second copy of the mapping, free to disagree with the catalogue an author edits.
        ///
        /// <c>SettlementRoleEnum.None</c> is refused rather than answered with a default order, because a road that
        /// declares no role seeds nothing at all -- answering it with the smallest order would turn every ordinary road
        /// in a scene into a village factory. </summary>
        public bool TryOrderFor(SettlementRoleEnum role, out SettlementOrderClass order, out string reason)
        {
            order = null;
            if (role == SettlementRoleEnum.None)
            {
                reason = "this road declares no settlement role, so it seeds no settlement and there is no order to look"
                         + " up. A road is part of the settlement network only when an author says it is";
                return false;
            }
            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i] != null && orders[i].seededByRole == role) { order = orders[i]; break; }
            }
            if (order == null)
            {
                reason = "no order in this settlement catalogue is seeded by the road role " + role + ", so a road"
                         + " carrying it reaches nothing. Either the catalogue is missing an order or two orders were"
                         + " given the same role and one of them lost it -- Reset to defaults restores the four";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> The row for one archetype, or null when the catalogue has none. </summary>
        public SettlementArchetypeClass Get(SettlementArchetype archetype)
        {
            for (int i = 0; i < archetypes.Count; i++)
            {
                if (archetypes[i] != null && archetypes[i].archetype == archetype) { return archetypes[i]; }
            }
            return null;
        }

        /// <summary> True when a row for the archetype exists, returning it. </summary>
        public bool TryGet(SettlementArchetype archetype, out SettlementArchetypeClass row)
        {
            row = Get(archetype);
            return row != null;
        }

        /// <summary> The wetted-fraction ceiling for one order and one archetype -- the single number C-14's region test
        /// compares a candidate's measured wetted fraction against, and the reason that number is authored here instead
        /// of written into the comparison.
        ///
        /// It refuses rather than substituting a default, because a missing ceiling is not a permissive one: a site the
        /// tool cannot judge is not a site it may accept. </summary>
        public bool TryWettedCeiling(int orderIndex, SettlementArchetype archetype, out float ceiling,
            out string reason)
        {
            ceiling = 0f;
            SettlementOrderClass order = Get(orderIndex);
            if (order == null)
            {
                reason = "this settlement catalogue holds no order with the index " + orderIndex + ", so how much water"
                         + " its region may hold is not authored anywhere. Reset to defaults restores the four orders";
                return false;
            }
            ceiling = order.WettedFractionCeiling(archetype);
            if (ceiling < 0f || ceiling > 1f)
            {
                reason = "the " + order.displayName + " order allows a wetted fraction of "
                         + ceiling.ToString("0.###") + " for the " + archetype + " archetype, and a fraction of an area"
                         + " lies between zero and one. A ceiling outside it cannot be compared against a measurement";
                ceiling = 0f;
                return false;
            }
            reason = string.Empty;
            return true;
        }

        /// <summary> Whether an archetype admits a zone, answered from the authored rows. Refuses when the archetype has
        /// no row, for the same fail-closed reason: an unknown archetype admitting every zone would be the permissive
        /// default the zone registry was built to remove. </summary>
        public bool TryAdmitsZone(SettlementArchetype archetype, string zoneId, out bool admitted, out string reason)
        {
            admitted = false;
            SettlementArchetypeClass row = Get(archetype);
            if (row == null)
            {
                reason = "this settlement catalogue holds no row for the " + archetype + " archetype, so which zones it"
                         + " admits is not authored anywhere. Reset to defaults restores the five";
                return false;
            }
            admitted = row.AdmitsZone(zoneId);
            reason = string.Empty;
            return true;
        }

        // ------------------------------------------------------------------ the shipped catalogue

        /// <summary> Overwrites both lists with the four orders and the five archetype rows.
        ///
        /// The spacing bands are BUILT HERE rather than typed: the town band is Christaller's Cambridge figure converted
        /// from miles, and the other three are that band stepped by <see cref="InterOrderSpacingRatio"/>, which is
        /// derived from the K value. Written this way the ladder cannot drift out of step with K, and the derivation is
        /// visible in the code that performs it instead of hidden behind four literals.
        ///
        /// Every FITTED figure below is a placeholder of the right magnitude, and <see cref="fittedFromData"/> is left
        /// false to say so. The reference triples are chosen so that area and street length PER HEAD fall as the order
        /// rises -- 300 m2 and 4 m for a capital, 625 m2 and 11.25 m for a village -- because that is what a sub-linear
        /// exponent means at the scale of the whole ladder, and <see cref="Validate"/> asserts it.
        ///
        /// Called from the asset's context menu, and by C-12's gate to obtain a known-good catalogue. </summary>
        [ContextMenu("Reset to default settlement orders")]
        public void ResetToDefaults()
        {
            float ratio = InterOrderSpacingRatio;
            float townMin = Miles(TownSpacingAnchorMinMiles);
            float townMax = Miles(TownSpacingAnchorMaxMiles);

            orders = new List<SettlementOrderClass>
            {
                new SettlementOrderClass
                {
                    orderIndex = CapitalOrder, displayName = "Capital / big city",
                    seededByRole = SettlementRoleEnum.MainHighway,
                    populationMin = 250000, populationMax = 20000000, rankSizeExponent = 1f,
                    spacingMinMetres = townMin * ratio * ratio, spacingMaxMetres = townMax * ratio * ratio,
                    builtAreaExponent = 0.85f, streetLengthExponent = 0.8f,
                    referencePopulation = 1000000, referenceBuiltAreaM2 = 300000000f,
                    referenceStreetLengthMetres = 4000000f,
                    familyWeightRegularSmallBlocks = 0.35f, familyWeightIrregularSmallBlocks = 0.2f,
                    familyWeightMixedBlockSizes = 0.3f, familyWeightLargeIrregularBlocks = 0.15f,
                    // A capital's region is large and overwhelmingly land; its harbour is a basin inside a city, not the
                    // city. So the plain figure is the tightest of the four ceilings and the port figure is modest.
                    wettedCeilingPlain = 0.05f, wettedCeilingPort = 0.25f,
                    wettedCeilingBeach = 0.2f, wettedCeilingLakeside = 0.2f
                },
                new SettlementOrderClass
                {
                    orderIndex = CityOrder, displayName = "City",
                    seededByRole = SettlementRoleEnum.Intercity,
                    populationMin = 50000, populationMax = 250000, rankSizeExponent = 1f,
                    spacingMinMetres = townMin * ratio, spacingMaxMetres = townMax * ratio,
                    builtAreaExponent = 0.85f, streetLengthExponent = 0.8f,
                    referencePopulation = 120000, referenceBuiltAreaM2 = 45000000f,
                    referenceStreetLengthMetres = 600000f,
                    familyWeightRegularSmallBlocks = 0.3f, familyWeightIrregularSmallBlocks = 0.25f,
                    familyWeightMixedBlockSizes = 0.3f, familyWeightLargeIrregularBlocks = 0.15f,
                    wettedCeilingPlain = 0.08f, wettedCeilingPort = 0.35f,
                    wettedCeilingBeach = 0.28f, wettedCeilingLakeside = 0.28f
                },
                new SettlementOrderClass
                {
                    orderIndex = TownOrder, displayName = "Town",
                    seededByRole = SettlementRoleEnum.TownConnector,
                    populationMin = 5000, populationMax = 50000, rankSizeExponent = 1f,
                    // The CITED band, unstepped: Christaller's Cambridge satellites at 10 to 15 miles. Every other
                    // order's band is this one multiplied or divided by the derived ratio, which is why the town row
                    // uses the anchors directly.
                    spacingMinMetres = townMin, spacingMaxMetres = townMax,
                    builtAreaExponent = 0.85f, streetLengthExponent = 0.8f,
                    referencePopulation = 12000, referenceBuiltAreaM2 = 6000000f,
                    referenceStreetLengthMetres = 90000f,
                    familyWeightRegularSmallBlocks = 0.2f, familyWeightIrregularSmallBlocks = 0.35f,
                    familyWeightMixedBlockSizes = 0.25f, familyWeightLargeIrregularBlocks = 0.2f,
                    wettedCeilingPlain = 0.12f, wettedCeilingPort = 0.45f,
                    wettedCeilingBeach = 0.4f, wettedCeilingLakeside = 0.4f
                },
                new SettlementOrderClass
                {
                    orderIndex = VillageOrder, displayName = "Village",
                    seededByRole = SettlementRoleEnum.VillageConnector,
                    populationMin = 200, populationMax = 5000, rankSizeExponent = 1f,
                    spacingMinMetres = townMin / ratio, spacingMaxMetres = townMax / ratio,
                    builtAreaExponent = 0.85f, streetLengthExponent = 0.8f,
                    referencePopulation = 800, referenceBuiltAreaM2 = 500000f,
                    referenceStreetLengthMetres = 9000f,
                    // A village is the least planned of the four: its lanes follow the ground and the field boundaries,
                    // so the irregular and large-irregular families carry most of the weight and the regular grid the
                    // least. The capital's weights are the mirror of this, and every row sums to one.
                    familyWeightRegularSmallBlocks = 0.1f, familyWeightIrregularSmallBlocks = 0.45f,
                    familyWeightMixedBlockSizes = 0.15f, familyWeightLargeIrregularBlocks = 0.3f,
                    // The loosest ceilings of the ladder, and deliberately so: a fishing village IS its harbour, and a
                    // hamlet on a lake shore is a handful of houses around water. The region being measured is small
                    // enough that one bay fills a large fraction of it without the site being unbuildable.
                    wettedCeilingPlain = 0.2f, wettedCeilingPort = 0.65f,
                    wettedCeilingBeach = 0.6f, wettedCeilingLakeside = 0.6f
                }
            };

            // The five rows. THE ZONE LISTS ARE THE WIRING THE REGISTER FOUND MISSING: the quay zone, the port hub kind
            // and the port prefab category all existed, and nothing said which zones a harbour town may use that an
            // inland village may not. Zones are named by their ZoneCatalog ids, so this list stays a list of references
            // and never becomes a second taxonomy -- ValidateAgainstZones refuses an id the catalogue cannot resolve.
            archetypes = new List<SettlementArchetypeClass>
            {
                new SettlementArchetypeClass
                {
                    archetype = SettlementArchetype.Auto, displayName = "Auto (measured from the ground)",
                    // Auto carries the same list as Plain, for the same reason its wetted ceiling resolves to Plain's:
                    // before the ground has been sampled the tool must judge the site as the strictest character it
                    // could turn out to be. Once C-14 has measured the site the resolved archetype's own row applies.
                    admittedZoneIds = new string[]
                    {
                        "zone.residential.low", "zone.residential.medium", "zone.commercial.centre",
                        "zone.industrial.general", "zone.informal.settlement"
                    },
                    requiresWaterEdge = false
                },
                new SettlementArchetypeClass
                {
                    archetype = SettlementArchetype.Plain, displayName = "Plain (inland)",
                    admittedZoneIds = new string[]
                    {
                        "zone.residential.low", "zone.residential.medium", "zone.commercial.centre",
                        "zone.industrial.general", "zone.informal.settlement"
                    },
                    requiresWaterEdge = false
                },
                new SettlementArchetypeClass
                {
                    archetype = SettlementArchetype.Port, displayName = "Port (working waterfront)",
                    // The only row that admits the quay zone, and the reason the zone exists. A port admits everything
                    // an inland place does as well: a harbour town still has houses, a centre and a works.
                    admittedZoneIds = new string[]
                    {
                        "zone.residential.low", "zone.residential.medium", "zone.commercial.centre",
                        "zone.industrial.general", "zone.informal.settlement", "zone.port.logistics"
                    },
                    requiresWaterEdge = true
                },
                new SettlementArchetypeClass
                {
                    archetype = SettlementArchetype.Beach, displayName = "Beach (resort shoreline)",
                    // No general industry: a resort shoreline that admitted a works would put one on the sea front,
                    // which is the one place a beach settlement never puts it. It is not the quay zone either -- a beach
                    // is a shore people arrive at, not a shore cargo is worked on.
                    admittedZoneIds = new string[]
                    {
                        "zone.residential.low", "zone.residential.medium", "zone.commercial.centre",
                        "zone.informal.settlement"
                    },
                    requiresWaterEdge = true
                },
                new SettlementArchetypeClass
                {
                    archetype = SettlementArchetype.Lakeside, displayName = "Lakeside",
                    // The same list as Beach, because the two differ in WHICH water they stand on and not in what may be
                    // built beside it -- exactly as their wetted ceilings are equal for the same reason.
                    admittedZoneIds = new string[]
                    {
                        "zone.residential.low", "zone.residential.medium", "zone.commercial.centre",
                        "zone.informal.settlement"
                    },
                    requiresWaterEdge = true
                }
            };
            fittedFromData = false;
            fitProvenance = "";
        }

        // ------------------------------------------------------------------ validation: the ladder, not the rungs

        /// <summary> Whether the catalogue as a whole is usable, with a report either way.
        ///
        /// THE DIVISION OF LABOUR WITH <see cref="SettlementOrderClass.IsSelfConsistent"/> IS DELIBERATE: a record judges
        /// only itself -- its own bands ordered, its own exponents sub-linear -- and every rule that involves TWO records
        /// is judged here. Written the other way each record would need to see the list it lives in, and the same rule
        /// would be asserted four times with four chances to disagree.
        ///
        /// What it refuses: a ladder that is not four rungs; a repeated or out-of-range order index; a repeated role, or
        /// an order seeded by no road at all; any record that fails its own test; population or spacing bands that do not
        /// fall as the order index rises; area per head that does not RISE as the order index rises, which is
        /// sub-linearity stated at the scale of the whole ladder rather than inside one row; a missing archetype row; and
        /// a water-edge flag that disagrees with the archetype's own character.
        ///
        /// What it does NOT refuse: the spacing ladder being exactly <see cref="InterOrderSpacingRatio"/> apart. An
        /// author may widen a band for their own world and the catalogue is theirs to edit; that the SHIPPED ladder is
        /// stepped by the derived ratio is C-12's assertion, made on a freshly reset catalogue. </summary>
        public bool Validate(out string report)
        {
            List<string> problems = new List<string>();

            if (orders.Count != 4)
            {
                problems.Add("this catalogue holds " + orders.Count + " orders and the settlement ladder has four rungs"
                             + " -- capital, city, town, village -- one for each settlement role a road can carry."
                             + " Reset to defaults restores them");
            }
            bool[] indexSeen = new bool[5];
            List<SettlementRoleEnum> rolesSeen = new List<SettlementRoleEnum>();
            for (int i = 0; i < orders.Count; i++)
            {
                SettlementOrderClass order = orders[i];
                if (order == null)
                {
                    problems.Add("the order at position " + i + " is empty");
                    continue;
                }
                string reason;
                if (!order.IsSelfConsistent(out reason)) { problems.Add(reason); }
                if (order.orderIndex < CapitalOrder || order.orderIndex > VillageOrder)
                {
                    problems.Add("the order " + order.displayName + " carries the index " + order.orderIndex
                                 + ", and the four rungs are numbered " + CapitalOrder + " to " + VillageOrder);
                }
                else if (indexSeen[order.orderIndex])
                {
                    problems.Add("two orders both carry the index " + order.orderIndex + ", so a lookup by index"
                                 + " reaches whichever the scan meets first and the other is unreachable");
                }
                else { indexSeen[order.orderIndex] = true; }
                if (rolesSeen.Contains(order.seededByRole))
                {
                    problems.Add("two orders are both seeded by the road role " + order.seededByRole + ", so a road"
                                 + " carrying it would seed either -- a role names exactly one order");
                }
                else { rolesSeen.Add(order.seededByRole); }
            }
            // The pairwise rules, walked down the ladder. Each compares an order with the one below it, which is why
            // they cannot live in a record's own test: rung 2 knows nothing about rung 3.
            for (int index = CapitalOrder; index < VillageOrder; index++)
            {
                SettlementOrderClass higher = Get(index);
                SettlementOrderClass lower = Get(index + 1);
                if (higher == null || lower == null) { continue; }
                // Touching bands are correct and overlapping ones are not: AdmitsPopulation treats its ceiling as
                // exclusive, so 250,000 belongs to the capital and not to the city below it. Anything less than
                // touching would leave populations no order admits.
                if (higher.populationMin < lower.populationMax)
                {
                    problems.Add("the " + higher.displayName + " order starts at " + higher.populationMin
                                 + " people while the " + lower.displayName + " order below it reaches "
                                 + lower.populationMax + ", so the two bands overlap and a place of that size belongs"
                                 + " to both orders");
                }
                if (higher.spacingMinMetres <= lower.spacingMinMetres
                    || higher.spacingMaxMetres <= lower.spacingMaxMetres)
                {
                    problems.Add("the " + higher.displayName + " order sits no further from its own kind than the "
                                 + lower.displayName + " order below it. A higher order serves a larger market area, so"
                                 + " it stands further apart -- that is what the spacing ladder means");
                }
                if (higher.ReferenceAreaPerPersonM2 >= lower.ReferenceAreaPerPersonM2)
                {
                    problems.Add("the " + higher.displayName + " order's reference point gives "
                                 + higher.ReferenceAreaPerPersonM2.ToString("0.#") + " m2 of built area per head and the"
                                 + " smaller " + lower.displayName + " order gives "
                                 + lower.ReferenceAreaPerPersonM2.ToString("0.#") + ". Built area grows more slowly than"
                                 + " population, so land per head must FALL as the order rises -- a reference point that"
                                 + " says otherwise contradicts the sub-linear exponent beside it");
                }
            }
            // One row per archetype, asked of the enum itself rather than of a second list of its values -- a list here
            // would be the duplicate that stops agreeing the day a sixth character of place is added.
            System.Array archetypeValues = System.Enum.GetValues(typeof(SettlementArchetype));
            for (int v = 0; v < archetypeValues.Length; v++)
            {
                SettlementArchetype value = (SettlementArchetype)archetypeValues.GetValue(v);
                int rowCount = 0;
                for (int j = 0; j < archetypes.Count; j++)
                {
                    if (archetypes[j] != null && archetypes[j].archetype == value) { rowCount++; }
                }
                if (rowCount == 0)
                {
                    problems.Add("no row says which zones the " + value + " archetype admits, so a settlement of that"
                                 + " character could be sited and then have nothing it is allowed to build");
                    continue;
                }
                if (rowCount > 1)
                {
                    problems.Add("this catalogue holds " + rowCount + " rows for the " + value + " archetype, and a"
                                 + " lookup answers with whichever it meets first -- one character, one row");
                }
                SettlementArchetypeClass row = Get(value);
                string reason;
                if (!row.IsSelfConsistent(out reason)) { problems.Add(reason); }
                // The three water characters are exactly the three the water rule was written for. A row claiming
                // otherwise would either strand a port inland or send a plain village looking for a shore.
                bool standsOnWater = value == SettlementArchetype.Port || value == SettlementArchetype.Beach
                                     || value == SettlementArchetype.Lakeside;
                if (row.requiresWaterEdge != standsOnWater)
                {
                    problems.Add("the " + value + " row " + (standsOnWater ? "does not require" : "requires")
                                 + " a water edge, and the " + value + " character of place "
                                 + (standsOnWater ? "is defined by standing on one" : "stands inland by definition"));
                }
            }
            for (int j = 0; j < archetypes.Count; j++)
            {
                if (archetypes[j] == null) { problems.Add("the archetype row at position " + j + " is empty"); }
            }
            if (problems.Count > 0)
            {
                report = "this settlement order catalogue cannot be used yet: "
                         + string.Join("; ", problems.ToArray()) + ".";
                return false;
            }
            string provenance = "still the shipped placeholders, so nothing that quotes them may call them a"
                                + " measurement";
            if (fittedFromData)
            {
                provenance = "fitted from " + (string.IsNullOrEmpty(fitProvenance) ? "an import that recorded no"
                    + " provenance, which is itself worth fixing" : fitProvenance);
            }
            report = orders.Count + " settlement orders, " + archetypes.Count + " archetype rows, K = " + christallerK
                     + " so each order stands " + InterOrderSpacingRatio.ToString("0.###")
                     + " times as far from its own kind as the order below it. Spacing is cited from Christaller in"
                     + " miles; the ratio is derived from K and is not his figure. The fitted figures are " + provenance
                     + ".";
            return true;
        }

        /// <summary> Whether every zone id the archetype rows name can be resolved in a zone catalogue, with a report
        /// either way. Separate from <see cref="Validate"/> because it needs the OTHER catalogue: an order catalogue can
        /// be judged on its own, but whether "zone.port.logistics" exists is a question only the zone taxonomy answers.
        ///
        /// This is what keeps the rows a list of REFERENCES rather than a second taxonomy. A mistyped id is refused here
        /// and named, instead of silently admitting nothing at bake time -- which is the same fail-closed rule
        /// <c>ZoneCatalog.Resolve</c> follows by returning null rather than a default zone. </summary>
        public bool ValidateAgainstZones(ZoneCatalog zones, out string report)
        {
            if (zones == null)
            {
                report = "no zone catalogue was supplied, so the zone ids these archetype rows name cannot be checked"
                         + " against anything.";
                return false;
            }
            List<string> problems = new List<string>();
            int checkedIds = 0;
            for (int i = 0; i < archetypes.Count; i++)
            {
                SettlementArchetypeClass row = archetypes[i];
                if (row == null || row.admittedZoneIds == null) { continue; }
                for (int z = 0; z < row.admittedZoneIds.Length; z++)
                {
                    string zoneId = row.admittedZoneIds[z];
                    checkedIds++;
                    if (zones.Contains(zoneId)) { continue; }
                    problems.Add("the " + row.archetype + " row admits the zone \"" + zoneId + "\", which the zone"
                                 + " catalogue does not hold");
                }
            }
            if (problems.Count > 0)
            {
                report = "these archetype rows name zones the zone catalogue does not hold: "
                         + string.Join("; ", problems.ToArray()) + ". A zone is DEFINED once in the zone catalogue and"
                         + " only NAMED here, so an id that does not resolve is a typo rather than a new zone --"
                         + " correct the spelling or add the zone where zones live.";
                return false;
            }
            report = "all " + checkedIds + " zone ids named across " + archetypes.Count + " archetype rows resolve in a"
                     + " zone catalogue holding " + zones.Count + " zones.";
            return true;
        }
    }
}
